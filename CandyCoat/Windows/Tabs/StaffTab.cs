using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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
            ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), "You are currently CLOCKED IN.");
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

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Recent Shifts");

        var history = _manager.ShiftHistory.Take(5).ToList();
        if (history.Count == 0)
        {
            ImGui.TextDisabled("No completed shifts yet.");
        }
        else
        {
            foreach (var shift in history)
            {
                var dur = shift.Duration;
                var durStr = $"{dur.Hours:D2}:{dur.Minutes:D2}";
                ImGui.BulletText($"{shift.StartTime:MM/dd}  {durStr}  {shift.GilEarned:N0} Gil");
            }
        }
    }
}
