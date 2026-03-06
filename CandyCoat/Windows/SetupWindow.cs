using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.UI;
using CandyCoat.Windows.SetupWizard;
using Una.Drawing;

namespace CandyCoat.Windows;

public class SetupWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    private int _currentStep;
    private int _lastBuiltStep = -1;
    private int _lastSavedStep = -1;
    private readonly WizardState _state = new();

    private readonly SetupStep0_Welcome          _step0        = new();
    private readonly SetupStepCheckSync          _stepSync     = new();
    private readonly SetupStep1_CharacterProfile _step1        = new();
    private readonly SetupStep2_ModeSelection    _step2        = new();
    private readonly SetupStep3_RoleSelection    _step3        = new();
    private readonly SetupStep4_VenueKey         _stepVenueKey = new();
    private readonly SetupStep4_Finish           _step4        = new();

    // Una.Drawing root rebuilt whenever _currentStep changes
    private Node? _root;

    public SetupWindow(Plugin plugin) : base("Candy Coat Setup##CandyCoatSetup")
    {
        _plugin = plugin;

        _currentStep = plugin.Configuration.SetupWizardStep;
        RestoreStateFromConfig();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 420),
            MaximumSize = new Vector2(700, 560)
        };

        Flags |= ImGuiWindowFlags.NoCollapse;
        Flags |= ImGuiWindowFlags.NoResize;
    }

    public void Dispose()
    {
        _root?.Dispose();
        _root = null;
    }

    // ─── State restoration / persistence ─────────────────────────────────────

    private void RestoreStateFromConfig()
    {
        var cfg = _plugin.Configuration;
        var parts = cfg.CharacterName.Split(' ', 2);
        if (parts.Length >= 1) _state.FirstName = parts[0];
        if (parts.Length >= 2) _state.LastName  = parts[1];
        _state.HomeWorld              = cfg.HomeWorld;
        _state.ProfileId              = cfg.ProfileId;
        _state.IdGenerated            = !string.IsNullOrEmpty(cfg.ProfileId);
        _state.UserMode               = cfg.UserMode;
        _state.SelectedPrimaryRole    = cfg.PrimaryRole;
        _state.SelectedSecondaryRoles = cfg.EnabledRoles;
        _state.MultiRoleToggle        = cfg.MultiRoleEnabled;
        _state.HasGlamourerIntegrated = cfg.EnableGlamourer;
        _state.HasChatTwoIntegrated   = cfg.EnableChatTwo;

        if (!string.IsNullOrEmpty(cfg.VenueId) && System.Guid.TryParse(cfg.VenueId, out var restoredVenueId))
        {
            _state.VenueId        = restoredVenueId;
            _state.VenueKey       = cfg.VenueKey;
            _state.VenueName      = cfg.VenueName;
            _state.VenueConfirmed = true;
        }
    }

    private void SaveProgress()
    {
        var cfg = _plugin.Configuration;
        cfg.SetupWizardStep = _currentStep;

        var name = $"{_state.FirstName} {_state.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(name))             cfg.CharacterName = name;
        if (!string.IsNullOrWhiteSpace(_state.HomeWorld)) cfg.HomeWorld     = _state.HomeWorld;
        if (!string.IsNullOrEmpty(_state.ProfileId))      cfg.ProfileId     = _state.ProfileId;
        if (!string.IsNullOrEmpty(_state.UserMode))       cfg.UserMode      = _state.UserMode;
        if (_state.SelectedPrimaryRole != StaffRole.None)
            cfg.PrimaryRole = _state.SelectedPrimaryRole;
        cfg.EnabledRoles     = _state.SelectedSecondaryRoles;
        cfg.MultiRoleEnabled = _state.MultiRoleToggle;
        cfg.Save();
    }

    // ─── Root build ───────────────────────────────────────────────────────────

    private void BuildRoot(Vector2 region)
    {
        _root?.Dispose();

        // Build the content node for the active step
        Node stepContent = _currentStep switch
        {
            0 => _step0.BuildStepNode(_state),
            1 => _stepSync.BuildStepNode(_state),
            2 => _step1.BuildStepNode(_state),
            3 => _step2.BuildStepNode(_state),
            4 => _step3.BuildStepNode(_state),
            5 => _stepVenueKey.BuildStepNode(_state),
            6 => _step4.BuildStepNode(_state),
            _ => CandyUI.Column("step-empty", 0),
        };

        // Navigation row — each step controls its own buttons as overlays for steps
        // that need ImGui inputs; for steps without inputs we can use Una.Drawing buttons.
        // We always add a nav row but steps that self-handle nav (0, 1, 2, stepSync) get empty rows.
        Node navRow = BuildNavRow();

        var card = CandyUI.Card("wizard-card",
            stepContent,
            CandyUI.Separator("wizard-sep"),
            navRow
        );

        _root = new Node
        {
            Id    = "wizard-root",
            Style = new Style
            {
                Size            = new Size((int)region.X, (int)region.Y),
                Flow            = Flow.Vertical,
                BackgroundColor = new Color(CandyTheme.BgWindow),
                Padding         = new EdgeSize(16),
                Gap             = 0,
            },
        };
        _root.AppendChild(card);
    }

    private Node BuildNavRow()
    {
        // Steps that self-contain their nav (Back/Next embedded in their own overlay):
        // 0 (Welcome has "Get Started"), 1 (StepSync has "Continue"), 2 (CharProfile has "Continue")
        // 3 (ModeSelection handles step advance via card click)
        // Steps 4, 5, 6 need Back / conditional Next
        return _currentStep switch
        {
            0 => CandyUI.Row("wizard-nav", 8),   // welcome: no nav row needed — step handles it
            1 => CandyUI.Row("wizard-nav", 8),   // check sync: no nav row
            2 => CandyUI.Row("wizard-nav", 8),   // char profile: no nav row
            3 => BuildModeSelectionNav(),
            4 => BuildRoleSelectionNav(),
            5 => BuildVenueKeyNav(),
            6 => BuildFinishNav(),
            _ => CandyUI.Row("wizard-nav", 8),
        };
    }

    private Node BuildModeSelectionNav()
    {
        // Back only — mode selection advances via card click
        return CandyUI.Row("wizard-nav", 8,
            CandyUI.GhostButton("wizard-back", "Back", () => _currentStep = 2)
        );
    }

    private Node BuildRoleSelectionNav()
    {
        bool canProceed = _state.SelectedPrimaryRole != StaffRole.None;

        var backBtn = CandyUI.GhostButton("wizard-back", "Back", () => _currentStep = 3);
        var nextBtn = CandyUI.Button("wizard-next", "Next", () => { if (canProceed) _currentStep = 5; });
        if (!canProceed)
        {
            nextBtn.Style.BackgroundColor = new Color(CandyTheme.BgCard);
            nextBtn.Style.Color           = new Color(CandyTheme.TextMuted);
        }
        return CandyUI.Row("wizard-nav", 8, backBtn, nextBtn);
    }

    private Node BuildVenueKeyNav()
    {
        bool canProceed = _state.VenueConfirmed;

        var backBtn = CandyUI.GhostButton("wizard-back", "Back", () => _currentStep = 4);
        var nextBtn = CandyUI.Button("wizard-next", "Next", () => { if (canProceed) _currentStep = 6; });
        if (!canProceed)
        {
            nextBtn.Style.BackgroundColor = new Color(CandyTheme.BgCard);
            nextBtn.Style.Color           = new Color(CandyTheme.TextMuted);
        }
        return CandyUI.Row("wizard-nav", 8, backBtn, nextBtn);
    }

    private Node BuildFinishNav()
    {
        return CandyUI.Row("wizard-nav", 8,
            CandyUI.GhostButton("wizard-back", "Back", () => _currentStep = 5)
        );
    }

    // ─── Draw ─────────────────────────────────────────────────────────────────

    public override void Draw()
    {
        // Auto-save whenever the step changes
        if (_currentStep != _lastSavedStep)
        {
            SaveProgress();
            _lastSavedStep = _currentStep;
        }

        var region = ImGui.GetContentRegionAvail();

        // Rebuild when step changes (or first draw)
        if (_currentStep != _lastBuiltStep || _root == null)
        {
            BuildRoot(region);
            _lastBuiltStep = _currentStep;
        }
        else
        {
            _root!.Style.Size = new Size((int)region.X, (int)region.Y);
        }

        var renderPos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _root!.Render(ImGui.GetWindowDrawList(), renderPos);
        ImGui.Dummy(region);

        // Draw raw ImGui overlays on top of the Una.Drawing layer
        DrawStepOverlays();
    }

    private void DrawStepOverlays()
    {
        switch (_currentStep)
        {
            case 0: _step0.DrawOverlays(_state, ref _currentStep); break;
            case 1: _stepSync.DrawOverlays(_state, ref _currentStep, _plugin); break;
            case 2: _step1.DrawOverlays(_state, ref _currentStep, _plugin, this); break;
            case 3: _step2.DrawOverlays(_state, ref _currentStep); break;
            case 4: _step3.DrawOverlays(_state, ref _currentStep, _plugin); break;
            case 5: _stepVenueKey.DrawOverlays(_state, ref _currentStep, _plugin); break;
            case 6: _step4.DrawOverlays(_state, ref _currentStep, _plugin, this); break;
        }
    }
}
