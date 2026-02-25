using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Services;

namespace CandyCoat.Windows.Tabs;

public class StaffTab : ITab
{
    private readonly ShiftManager _manager;

    public string Name => "Staff Shifts";

    public StaffTab(ShiftManager manager)
    {
        _manager = manager;
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(Name);
        if (!tab) return;
        DrawContent();
    }

    public void DrawContent()
    {
        ImGui.TextUnformatted("Shift Management");
        ImGui.Spacing();

        var currentShift = _manager.CurrentShift;
        if (currentShift != null)
        {
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), "You are currently CLOCKED IN.");
            var duration = currentShift.Duration;
            ImGui.Text($"Duration: {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}");
            ImGui.Text($"Earnings this shift: {currentShift.GilEarned:N0} Gil");
            
            ImGui.Spacing();
            if (ImGui.Button("Clock Out", new Vector2(150, 40)))
            {
                _manager.ClockOut();
            }
        }
        else
        {
            ImGui.TextDisabled("You are currently CLOCKED OUT.");
            ImGui.Spacing();
            if (ImGui.Button("Clock In", new Vector2(150, 40)))
            {
                _manager.ClockIn();
            }
        }
    }
}
