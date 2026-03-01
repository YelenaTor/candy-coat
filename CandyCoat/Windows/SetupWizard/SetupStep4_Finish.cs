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

        ImGui.TextColored(dimGrey, "Step 4 of 4 — Summary");
        ImGui.Spacing();
        ImGui.TextWrapped("Review your setup before launching Candy Coat.");
        ImGui.Spacing();

        // Summary child box
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.07f, 0.14f, 1f));
        using (var summary = ImRaii.Child("##finishSummary", new Vector2(280, 140), true))
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

            cfg.IsSetupComplete = true;
            cfg.Save();

            if (cfg.EnableSync && !string.IsNullOrEmpty(state.ProfileId))
            {
                plugin.SyncService.UpsertProfileAsync(
                    state.ProfileId,
                    cfg.CharacterName,
                    cfg.HomeWorld,
                    state.UserMode);
            }

            window.IsOpen = false;
            plugin.OnSetupComplete();
        }

        ImGui.PopStyleColor(3);
    }
}
