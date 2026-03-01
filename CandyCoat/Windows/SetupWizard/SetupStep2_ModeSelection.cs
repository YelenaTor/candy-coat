using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep2_ModeSelection
{
    public void DrawContent(ref int step, WizardState state)
    {
        var dimGrey = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var pink    = new Vector4(1f, 0.6f, 0.8f, 1f);
        var cardBg  = new Vector4(0.12f, 0.08f, 0.16f, 1f);
        var cardHov = new Vector4(0.18f, 0.12f, 0.24f, 1f);

        ImGui.TextColored(dimGrey, "Step 2 of 4 — Choose Your Mode");
        ImGui.Spacing();
        ImGui.TextWrapped("How will you be using Candy Coat?");
        ImGui.Spacing();
        ImGui.Spacing();

        const float CardWidth  = 200f;
        const float CardHeight = 120f;

        // ── Staff Card ──
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
            step = 3;
        }

        ImGui.SameLine(0, 16f);

        // ── Patron Card (disabled / coming soon) ──
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
