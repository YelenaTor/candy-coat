using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep4_Finish
{
    // ─── Una.Drawing node ────────────────────────────────────────────────────

    public Node BuildStepNode(WizardState state)
    {
        return CandyUI.Column("step4-content", 8,
            CandyUI.Muted("step4-subtitle", "Final Step — Summary"),
            new Node
            {
                Id        = "step4-desc",
                NodeValue = "Review your setup before launching Candy Coat.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextPrimary),
                    FontSize  = 13,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            // Reserve space for the summary box, launch button, and integrations box
            CandyUI.InputSpacer("step4-overlay-spacer", 0, 280)
        );
    }

    // ─── Raw ImGui overlay ────────────────────────────────────────────────────

    public void DrawOverlays(WizardState state, ref int step, Plugin plugin, SetupWindow window)
    {
        var dimGrey = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var pink    = new Vector4(1f, 0.6f, 0.8f, 1f);
        var panelBg = new Vector4(0.1f, 0.07f, 0.14f, 1f);

        // Summary child box
        ImGui.PushStyleColor(ImGuiCol.ChildBg, panelBg);
        using (ImRaii.Child("##finishSummary", new Vector2(280, 140), true))
        {
            ImGui.PopStyleColor();

            ImGui.TextColored(dimGrey, "Character:");
            ImGui.SameLine();
            ImGui.Text($"{state.FirstName} {state.LastName} @ {state.HomeWorld}");

            ImGui.TextColored(dimGrey, "Profile ID:");
            ImGui.SameLine();
            ImGui.TextColored(pink, state.ProfileId);
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy##finCopy"))
                ImGui.SetClipboardText(state.ProfileId);

            ImGui.TextColored(dimGrey, "Mode:");
            ImGui.SameLine();
            ImGui.Text(state.UserMode);

            ImGui.TextColored(dimGrey, "Primary Role:");
            ImGui.SameLine();
            ImGui.Text(state.SelectedPrimaryRole.ToString());
        }

        ImGui.Spacing();
        ImGui.Spacing();

        // Launch button
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.8f, 0.4f, 0.6f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.5f, 0.7f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.7f, 0.3f, 0.5f, 1f));

        if (ImGui.Button("Launch Candy Coat", new Vector2(240, 40)))
        {
            var cfg = plugin.Configuration;
            cfg.CharacterName    = $"{state.FirstName} {state.LastName}";
            cfg.HomeWorld        = state.HomeWorld;
            cfg.ProfileId        = state.ProfileId;
            cfg.UserMode         = state.UserMode;
            cfg.PrimaryRole      = state.SelectedPrimaryRole;
            cfg.MultiRoleEnabled = state.MultiRoleToggle;
            cfg.EnabledRoles     = state.MultiRoleToggle
                ? (state.SelectedSecondaryRoles | state.SelectedPrimaryRole)
                : state.SelectedPrimaryRole;
            cfg.EnableGlamourer  = state.HasGlamourerIntegrated;
            cfg.EnableChatTwo    = state.HasChatTwoIntegrated;

            cfg.EnableSync = true;

            cfg.ApiUrl    = PluginConstants.ProductionApiUrl;
            cfg.VenueId   = state.VenueId != System.Guid.Empty ? state.VenueId.ToString() : cfg.VenueId;
            cfg.VenueKey  = !string.IsNullOrEmpty(state.VenueKey)  ? state.VenueKey  : cfg.VenueKey;
            cfg.VenueName = !string.IsNullOrEmpty(state.VenueName) ? state.VenueName : cfg.VenueName;

            cfg.IsSetupComplete = true;
            cfg.Save();

            plugin.SyncService.UpdateVenueKey(cfg.VenueKey);

            if (state.VenueConfirmed && !string.IsNullOrEmpty(state.ProfileId))
            {
                plugin.SyncService.UpsertProfileAsync(
                    state.ProfileId,
                    cfg.CharacterName,
                    cfg.HomeWorld,
                    state.UserMode,
                    cfg.VenueId,
                    state.HasGlamourerIntegrated,
                    state.HasChatTwoIntegrated);
            }

            window.IsOpen = false;
            plugin.OnSetupComplete();
        }

        ImGui.PopStyleColor(3);

        ImGui.Spacing();
        ImGui.Spacing();

        // Integrations box
        ImGui.PushStyleColor(ImGuiCol.ChildBg, panelBg);
        using (ImRaii.Child("##integrationsBox", new Vector2(280, 90), true))
        {
            ImGui.PopStyleColor();

            ImGui.TextColored(dimGrey, "Integrations");
            ImGui.Spacing();

            // Glamourer row
            ImGui.Text("Glamourer");
            ImGui.SameLine(180);
            if (state.HasGlamourerIntegrated)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.2f, 0.6f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.7f, 0.35f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.15f, 0.5f, 0.25f, 1f));
                if (ImGui.SmallButton("Enabled ##glam"))
                    state.HasGlamourerIntegrated = false;
                ImGui.PopStyleColor(3);
            }
            else
            {
                if (ImGui.SmallButton("Disabled##glam"))
                    state.HasGlamourerIntegrated = true;
            }

            ImGui.Spacing();

            // ChatTwo row
            ImGui.Text("ChatTwo");
            ImGui.SameLine(180);
            if (state.HasChatTwoIntegrated)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.2f, 0.6f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.7f, 0.35f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.15f, 0.5f, 0.25f, 1f));
                if (ImGui.SmallButton("Enabled ##chat"))
                    state.HasChatTwoIntegrated = false;
                ImGui.PopStyleColor(3);
            }
            else
            {
                if (ImGui.SmallButton("Disabled##chat"))
                    state.HasChatTwoIntegrated = true;
            }
        }
    }
}
