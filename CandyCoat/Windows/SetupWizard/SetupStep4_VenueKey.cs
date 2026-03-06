using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep4_VenueKey
{
    private static readonly Vector4 DimGrey = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 Green   = new(0.2f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 Red     = new(1f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Amber   = new(1f, 0.8f, 0.2f, 1f);

    private string _venueNameBuffer = string.Empty;
    private string _keyBuffer       = string.Empty;
    private bool   _pending         = false;
    private string? _error          = null;
    private bool _keyRevealed       = false;

    // ─── Una.Drawing node ────────────────────────────────────────────────────

    public Node BuildStepNode(WizardState state)
    {
        bool isOwner = state.SelectedPrimaryRole.HasFlag(StaffRole.Owner);

        return CandyUI.Column("stepVK-content", 8,
            CandyUI.Muted("stepVK-subtitle", "Step 5 of 6 — Venue Registration"),
            new Node
            {
                Id        = "stepVK-desc",
                NodeValue = isOwner
                    ? "Register your venue to generate a Venue Key to share with your staff."
                    : "Enter the Venue Key provided by your venue Owner to connect.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextSecondary),
                    FontSize  = 12,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            CandyUI.Separator("stepVK-sep"),
            // Reserve space for the owner/staff form drawn as overlay
            CandyUI.InputSpacer("stepVK-overlay-spacer", 0, 180)
        );
    }

    // ─── Raw ImGui overlay ────────────────────────────────────────────────────

    public void DrawOverlays(WizardState state, ref int step, Plugin plugin)
    {
        bool isOwner = state.SelectedPrimaryRole.HasFlag(StaffRole.Owner);

        if (state.VenueConfirmed)
        {
            DrawConfirmedState(state, isOwner);
            return;
        }

        if (isOwner)
            DrawOwnerRegister(state, plugin);
        else
            DrawStaffValidate(state, plugin);
    }

    private void DrawConfirmedState(WizardState state, bool isOwner)
    {
        if (isOwner)
        {
            ImGui.TextColored(Green, "\u2714 Venue registered!");
            ImGui.Spacing();
            ImGui.TextColored(DimGrey, "Venue:");
            ImGui.SameLine();
            ImGui.Text(state.VenueName);

            ImGui.TextColored(DimGrey, "ID:");
            ImGui.SameLine();
            ImGui.Text(state.VenueId.ToString());
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy##vkCopyId"))
                ImGui.SetClipboardText(state.VenueId.ToString());

            ImGui.TextColored(DimGrey, "Key:");
            ImGui.SameLine();
            var displayKey = _keyRevealed ? state.VenueKey : new string('\u2022', Math.Min(state.VenueKey.Length, 20));
            ImGui.Text(displayKey);
            ImGui.SameLine();
            if (ImGui.SmallButton(_keyRevealed ? "Hide##vkReveal" : "Show##vkReveal"))
                _keyRevealed = !_keyRevealed;
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy##vkCopyKey"))
                ImGui.SetClipboardText(state.VenueKey);

            ImGui.Spacing();
            ImGui.TextColored(Amber, "\u26a0 Share the Key with your staff — they enter it in the setup wizard.");
        }
        else
        {
            ImGui.TextColored(Green, $"\u2714 Connected to {state.VenueName}");
        }
    }

    private void DrawOwnerRegister(WizardState state, Plugin plugin)
    {
        ImGui.Text("Venue Name:");
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##vkVenueName", ref _venueNameBuffer, 100);
        ImGui.Spacing();

        bool canRegister = !_pending && !string.IsNullOrWhiteSpace(_venueNameBuffer);
        if (!canRegister) ImGui.BeginDisabled();
        if (ImGui.Button(_pending ? "Registering..." : "Register##vkRegister"))
        {
            _error = null;
            _pending = true;
            var nameCopy      = _venueNameBuffer;
            var profileIdCopy = state.ProfileId;
            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, venueId, venueKey, venueName) =
                        await plugin.SyncService.RegisterVenueAsync(nameCopy, profileIdCopy);

                    if (success)
                    {
                        state.VenueId        = venueId;
                        state.VenueKey       = venueKey;
                        state.VenueName      = venueName;
                        state.VenueConfirmed = true;
                    }
                    else
                    {
                        _error = "Registration failed. Please try again.";
                    }
                }
                catch (Exception ex)
                {
                    _error = $"Error: {ex.Message}";
                }
                finally
                {
                    _pending = false;
                }
            });
        }
        if (!canRegister) ImGui.EndDisabled();

        if (_error != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(Red, $"\u2718 {_error}");
        }
    }

    private void DrawStaffValidate(WizardState state, Plugin plugin)
    {
        ImGui.Text("Venue Key:");
        ImGui.SetNextItemWidth(260);
        bool submitted = ImGui.InputText("##vkKeyInput", ref _keyBuffer, 128,
            ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();

        bool canValidate = !_pending && !string.IsNullOrWhiteSpace(_keyBuffer);
        if (!canValidate) ImGui.BeginDisabled();
        if (ImGui.Button(_pending ? "Validating..." : "Validate##vkValidate") || (submitted && canValidate))
        {
            _error = null;
            _pending = true;
            var keyCopy = _keyBuffer;
            _ = Task.Run(async () =>
            {
                try
                {
                    var (valid, venueId, venueName) =
                        await plugin.SyncService.ValidateVenueKeyAsync(keyCopy);

                    if (valid)
                    {
                        state.VenueId        = venueId;
                        state.VenueKey       = keyCopy;
                        state.VenueName      = venueName;
                        state.VenueConfirmed = true;
                        _keyBuffer           = string.Empty;
                    }
                    else
                    {
                        _error     = "Invalid key — check with your Owner.";
                        _keyBuffer = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    _error = $"Error: {ex.Message}";
                }
                finally
                {
                    _pending = false;
                }
            });
        }
        if (!canValidate) ImGui.EndDisabled();

        if (_error != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(Red, $"\u2718 {_error}");
        }
    }
}
