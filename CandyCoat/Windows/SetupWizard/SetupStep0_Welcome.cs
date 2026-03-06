using System.Numerics;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep0_Welcome
{
    // ─── Una.Drawing node ────────────────────────────────────────────────────

    public Node BuildStepNode(WizardState state)
    {
        return CandyUI.Column("step0-content", 8,
            new Node
            {
                Id        = "step0-title",
                NodeValue = "Welcome to Candy Coat",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextAccent),
                    FontSize  = 16,
                    TextAlign = Anchor.MiddleCenter,
                },
            },
            new Node
            {
                Id        = "step0-desc1",
                NodeValue = "Candy Coat is your in-game venue assistant for Sugar Venue.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextPrimary),
                    FontSize  = 13,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            new Node
            {
                Id        = "step0-desc2",
                NodeValue = "This short setup will create a Character Profile for Venue-Sync — a unique ID that identifies you across all worlds and instances.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextSecondary),
                    FontSize  = 12,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            new Node
            {
                Id        = "step0-desc3",
                NodeValue = "Setup takes less than a minute.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextMuted),
                    FontSize  = 12,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            // Spacer so the button overlay lands in the right place
            CandyUI.InputSpacer("step0-btn-spacer", 180, 40)
        );
    }

    // ─── Raw ImGui overlay ────────────────────────────────────────────────────

    public void DrawOverlays(WizardState state, ref int step)
    {
        var pink = new Vector4(1f, 0.6f, 0.8f, 1f);

        const float BtnWidth  = 180f;
        const float BtnHeight = 40f;

        var windowWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX((windowWidth - BtnWidth) / 2f + 4f);

        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.6f, 0.8f, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(1f, 0.6f, 0.8f, 0.22f));
        ImGui.PushStyleColor(ImGuiCol.Text,          pink);
        ImGui.PushStyleColor(ImGuiCol.Border,        new Vector4(1f, 0.6f, 0.8f, 0.8f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);

        if (ImGui.Button("Get Started", new Vector2(BtnWidth, BtnHeight)))
            step = 1;

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }
}
