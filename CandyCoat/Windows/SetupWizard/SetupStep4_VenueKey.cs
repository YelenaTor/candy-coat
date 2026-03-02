using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep4_VenueKey
{
    private static readonly Vector4 DimGrey = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 Amber   = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Vector4 Red     = new(1f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Green   = new(0.2f, 0.9f, 0.4f, 1f);

    private string _keyBuffer  = string.Empty;
    private bool   _keyError   = false;

    public void DrawContent(ref int step, WizardState state)
    {
        ImGui.TextColored(DimGrey, "Step 5 of 6 — Venue Key");
        ImGui.Spacing();
        ImGui.TextWrapped("Enter the venue key to authenticate your plugin with the Sugar API.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (state.VenueKeyUnlocked)
        {
            ImGui.TextColored(Green, "\u2714 Venue key accepted.");
        }
        else
        {
            ImGui.Text("Venue Key:");
            ImGui.SetNextItemWidth(260);
            bool submitted = ImGui.InputText("##venueKey", ref _keyBuffer, 64,
                ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();
            if (ImGui.Button("Confirm##vkConfirm") || submitted)
            {
                if (_keyBuffer == PluginConstants.VenueKey)
                {
                    state.VenueKeyUnlocked = true;
                    _keyError  = false;
                    _keyBuffer = string.Empty;
                }
                else
                {
                    _keyError  = true;
                    _keyBuffer = string.Empty;
                }
            }

            if (_keyError)
            {
                ImGui.Spacing();
                ImGui.TextColored(Red, "\u2718 Invalid venue key. Please try again.");
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Disabled "Generate Venue ID" section ──
        ImGui.BeginDisabled();
        ImGui.Button("Generate Venue ID##genVenueId", new System.Numerics.Vector2(180, 0));
        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.TextColored(Amber, "\u26a0 Multi-venue staging is not yet available.");
        ImGui.TextColored(DimGrey, "Venue key management will be supported in a future update.");
    }
}
