using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep4_Finish
{
    public void DrawContent(ref int step, WizardState state, Plugin plugin, SetupWindow window)
    {
        var dimGrey = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var pink    = new Vector4(1f, 0.6f, 0.8f, 1f);
        var green   = new Vector4(0.2f, 0.9f, 0.4f, 1f);
        var panelBg = new Vector4(0.1f, 0.07f, 0.14f, 1f);

        ImGui.TextColored(dimGrey, "Step 5 of 5 — Summary");
        ImGui.Spacing();
        ImGui.TextWrapped("Review your setup before launching Candy Coat.");
        ImGui.Spacing();

        // ── Summary child box ──
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

        // ── Launch button ──
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.8f, 0.4f, 0.6f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.5f, 0.7f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.7f, 0.3f, 0.5f, 1f));

        if (ImGui.Button("Launch Candy Coat", new Vector2(240, 40)))
        {
            var cfg = plugin.Configuration;
            cfg.CharacterName         = $"{state.FirstName} {state.LastName}";
            cfg.HomeWorld             = state.HomeWorld;
            cfg.ProfileId             = state.ProfileId;
            cfg.UserMode              = state.UserMode;
            cfg.PrimaryRole           = state.SelectedPrimaryRole;
            cfg.MultiRoleEnabled      = state.MultiRoleToggle;
            cfg.EnabledRoles          = state.MultiRoleToggle
                ? (state.SelectedSecondaryRoles | state.SelectedPrimaryRole)
                : state.SelectedPrimaryRole;
            cfg.EnableGlamourer       = state.HasGlamourerIntegrated;
            cfg.EnableChatTwo         = state.HasChatTwoIntegrated;

            cfg.IsSetupComplete = true;
            cfg.Save();

            if (cfg.EnableSync && !string.IsNullOrEmpty(state.ProfileId))
            {
                plugin.SyncService.UpsertProfileAsync(
                    state.ProfileId,
                    cfg.CharacterName,
                    cfg.HomeWorld,
                    state.UserMode,
                    state.HasGlamourerIntegrated,
                    state.HasChatTwoIntegrated);
            }

            window.IsOpen = false;
            plugin.OnSetupComplete();
        }

        ImGui.PopStyleColor(3);

        ImGui.Spacing();
        ImGui.Spacing();

        // ── Integrations box ──
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
