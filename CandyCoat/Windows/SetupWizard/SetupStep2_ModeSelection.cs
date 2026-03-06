using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep2_ModeSelection
{
    // ─── Una.Drawing node ────────────────────────────────────────────────────

    public Node BuildStepNode(WizardState state)
    {
        return CandyUI.Column("step2-content", 8,
            CandyUI.Muted("step2-subtitle", "Step 3 of 5 — Choose Your Mode"),
            new Node
            {
                Id        = "step2-desc",
                NodeValue = "How will you be using Candy Coat?",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextPrimary),
                    FontSize  = 13,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            // Reserve space for the two mode-selection cards drawn as overlay
            CandyUI.InputSpacer("step2-cards-spacer", 0, 140)
        );
    }

    // ─── Raw ImGui overlay ────────────────────────────────────────────────────

    public void DrawOverlays(WizardState state, ref int step)
    {
        var pink    = new Vector4(1f, 0.6f, 0.8f, 1f);
        var cardBg  = new Vector4(0.12f, 0.08f, 0.16f, 1f);

        const float CardWidth  = 200f;
        const float CardHeight = 120f;

        // Staff Card
        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg);
        using (var staffCard = ImRaii.Child("##staffCard", new Vector2(CardWidth, CardHeight), true))
        {
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.SetCursorPosX((CardWidth - ImGui.CalcTextSize("Staff").X) / 2f);
            ImGui.TextColored(pink, "Staff");
            ImGui.Spacing();
            ImGui.TextWrapped("Venue staff member — access role toolboxes, shifts, and patron tools.");
        }

        bool staffHovered = ImGui.IsItemHovered();
        if (staffHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(pink), 4f, ImDrawFlags.None, 1.5f);
        }

        if (ImGui.IsItemClicked())
        {
            state.UserMode = "Staff";
            step = 4;
        }

        ImGui.SameLine(0, 16f);

        // Patron Card (disabled)
        ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg);
        using (var patronCard = ImRaii.Child("##patronCard", new Vector2(CardWidth, CardHeight), true))
        {
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.SetCursorPosX((CardWidth - ImGui.CalcTextSize("Patron").X) / 2f);
            ImGui.TextDisabled("Patron");
            ImGui.Spacing();
            ImGui.TextDisabled("Venue guest access — coming in a future update.");
        }
        ImGui.EndDisabled();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Coming Soon — Not yet implemented");
    }
}
