using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.UI;
using CandyCoat.Windows.SetupWizard;

namespace CandyCoat.Windows;

public class SetupWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    private int _currentStep;
    private int _lastSavedStep = -1;
    private readonly WizardState _state = new();

    private readonly SetupStep0_Welcome          _step0        = new();
    private readonly SetupStepCheckSync          _stepSync     = new();
    private readonly SetupStep1_CharacterProfile _step1        = new();
    private readonly SetupStep2_ModeSelection    _step2        = new();
    private readonly SetupStep3_RoleSelection    _step3        = new();
    private readonly SetupStep4_VenueKey         _stepVenueKey = new();
    private readonly SetupStep4_Finish           _step4        = new();

    public SetupWindow(Plugin plugin) : base("Candy Coat Setup##CandyCoatSetup")
    {
        _plugin = plugin;

        // Restore step and partial state from config so users can resume mid-setup
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

    public void Dispose() { }

    // ─── State restoration / persistence ───

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
        _state.VenueKeyUnlocked       = cfg.VenueKey == PluginConstants.VenueKey;
    }

    private void SaveProgress()
    {
        var cfg = _plugin.Configuration;
        cfg.SetupWizardStep = _currentStep;

        var name = $"{_state.FirstName} {_state.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(name))          cfg.CharacterName = name;
        if (!string.IsNullOrWhiteSpace(_state.HomeWorld)) cfg.HomeWorld  = _state.HomeWorld;
        if (!string.IsNullOrEmpty(_state.ProfileId))    cfg.ProfileId    = _state.ProfileId;
        if (!string.IsNullOrEmpty(_state.UserMode))     cfg.UserMode     = _state.UserMode;
        if (_state.SelectedPrimaryRole != StaffRole.None)
            cfg.PrimaryRole = _state.SelectedPrimaryRole;
        cfg.EnabledRoles     = _state.SelectedSecondaryRoles;
        cfg.MultiRoleEnabled = _state.MultiRoleToggle;
        cfg.Save();
    }

    // ─── Draw ───

    public override void Draw()
    {
        StyleManager.PushStyles();
        try
        {
            // Auto-save whenever the step changes
            if (_currentStep != _lastSavedStep)
            {
                SaveProgress();
                _lastSavedStep = _currentStep;
            }

            switch (_currentStep)
            {
                case 0: _step0.DrawContent(ref _currentStep, _state); break;
                case 1: _stepSync.DrawContent(ref _currentStep, _state, _plugin); break;
                case 2: _step1.DrawContent(ref _currentStep, _state, _plugin, this); break;
                case 3: DrawModeSelectionWithNav(); break;
                case 4: DrawRoleSelectionWithNav(); break;
                case 5: DrawVenueKeyWithNav(); break;
                case 6: DrawFinishWithNav(); break;
            }
        }
        finally
        {
            StyleManager.PopStyles();
        }
    }

    private void DrawModeSelectionWithNav()
    {
        _step2.DrawContent(ref _currentStep, _state);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Back##step3back"))
            _currentStep = 2;
    }

    private void DrawRoleSelectionWithNav()
    {
        _step3.DrawContent(ref _currentStep, _state, _plugin);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Back##step4back"))
            _currentStep = 3;

        bool canProceed = _state.SelectedPrimaryRole != StaffRole.None;
        ImGui.SameLine();
        if (!canProceed) ImGui.BeginDisabled();
        if (ImGui.Button("Next##step4next"))
            _currentStep = _state.SelectedPrimaryRole == StaffRole.Owner ? 5 : 6;
        if (!canProceed) ImGui.EndDisabled();
    }

    private void DrawVenueKeyWithNav()
    {
        _stepVenueKey.DrawContent(ref _currentStep, _state);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Back##step5back"))
            _currentStep = 4;

        bool canProceed = _state.VenueKeyUnlocked;
        ImGui.SameLine();
        if (!canProceed) ImGui.BeginDisabled();
        if (ImGui.Button("Next##step5next"))
            _currentStep = 6;
        if (!canProceed) ImGui.EndDisabled();
    }

    private void DrawFinishWithNav()
    {
        _step4.DrawContent(ref _currentStep, _state, _plugin, this);

        ImGui.Spacing();
        if (ImGui.Button("Back##step6back"))
            _currentStep = _state.SelectedPrimaryRole == StaffRole.Owner ? 5 : 4;
    }
}
