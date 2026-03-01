using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using CandyCoat.Helpers;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep1_CharacterProfile
{
    public void DrawContent(ref int step, WizardState state)
    {
        var dimGrey = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var pink    = new Vector4(1f, 0.6f, 0.8f, 1f);
        var amber   = new Vector4(1f, 0.8f, 0.2f, 1f);

        ImGui.TextColored(dimGrey, "Step 1 of 4 — Character Profile");
        ImGui.Spacing();

        if (ImGui.Button("Auto-Detect"))
        {
            var player = Svc.Objects.LocalPlayer;
            if (player != null)
            {
                var parts = player.Name.ToString().Split(' ');
                if (parts.Length >= 2)
                {
                    state.FirstName = parts[0];
                    state.LastName  = parts[1];
                }
                state.HomeWorld = player.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;
            }
        }

        ImGui.Spacing();

        var firstName = state.FirstName;
        if (ImGui.InputText("First Name##step1fn", ref firstName, 30))
            state.FirstName = firstName;

        var lastName = state.LastName;
        if (ImGui.InputText("Last Name##step1ln", ref lastName, 30))
            state.LastName = lastName;

        var homeWorld = state.HomeWorld;
        if (ImGui.InputText("Home World##step1hw", ref homeWorld, 30))
            state.HomeWorld = homeWorld;

        ImGui.Spacing();

        if (!state.IdGenerated)
        {
            bool allFilled = !string.IsNullOrWhiteSpace(state.FirstName)
                          && !string.IsNullOrWhiteSpace(state.LastName)
                          && !string.IsNullOrWhiteSpace(state.HomeWorld);

            if (!allFilled) ImGui.BeginDisabled();
            if (ImGui.Button("Confirm Details"))
            {
                state.ProfileId   = ProfileIdHelper.Generate();
                state.IdGenerated = true;
            }
            if (!allFilled) ImGui.EndDisabled();
        }
        else
        {
            ImGui.Spacing();
            ImGui.TextColored(dimGrey, "Your Profile ID:");
            ImGui.TextColored(pink, state.ProfileId);

            ImGui.Spacing();

            if (ImGui.SmallButton("Copy to Clipboard"))
                ImGui.SetClipboardText(state.ProfileId);

            ImGui.SameLine();

            if (ImGui.SmallButton("Save as .txt"))
            {
                try
                {
                    var path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "CandyCoat_ProfileId.txt");
                    File.WriteAllText(path, state.ProfileId);
                }
                catch (Exception ex)
                {
                    Svc.Log.Warning($"[CandyCoat] Could not save Profile ID: {ex.Message}");
                }
            }

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, amber);
            ImGui.TextWrapped("Keep this ID safe. You cannot use Candy Coat without it.");
            ImGui.PopStyleColor();

            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.Button("Continue##step1next", new Vector2(120, 0)))
                step = 2;
        }
    }
}
