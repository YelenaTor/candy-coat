using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep0_Welcome
{
    public void DrawContent(ref int step, WizardState state)
    {
        var pink = new Vector4(1f, 0.6f, 0.8f, 1f);

        // Centered heading
        var heading = "Welcome to Candy Coat";
        var windowWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX((windowWidth - ImGui.CalcTextSize(heading).X) / 2f);
        ImGui.TextColored(pink, heading);

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextWrapped("Candy Coat is your in-game venue assistant for Sugar Venue.");
        ImGui.Spacing();
        ImGui.TextWrapped("This short setup will create a Character Profile for Venue-Sync — a unique ID that identifies you across all worlds and instances.");
        ImGui.Spacing();
        ImGui.TextWrapped("Setup takes less than a minute.");
        ImGui.Spacing();
        ImGui.Spacing();

        // Centered outline button
        const float BtnWidth  = 180f;
        const float BtnHeight = 40f;
        ImGui.SetCursorPosX((windowWidth - BtnWidth) / 2f);

        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.6f, 0.8f, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(1f, 0.6f, 0.8f, 0.22f));
        ImGui.PushStyleColor(ImGuiCol.Text,          new Vector4(1f, 0.6f, 0.8f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border,        new Vector4(1f, 0.6f, 0.8f, 0.8f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);

        if (ImGui.Button("Get Started", new System.Numerics.Vector2(BtnWidth, BtnHeight)))
            step = 1;

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }
}
