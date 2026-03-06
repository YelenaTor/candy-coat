using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Services;
using CandyCoat.UI;
using Una.Drawing;

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

    public Node BuildNode()
    {
        var root = CandyUI.Column("staff-root", 8);
        root.AppendChild(CandyUI.SectionHeader("staff-header", "Staff Shifts"));
        root.AppendChild(CandyUI.Separator("staff-sep1"));

        var shift      = _manager.CurrentShift;
        var statusCard = CandyUI.Card("staff-status-card");

        if (shift != null)
        {
            var dur = shift.Duration;
            statusCard.AppendChild(CandyUI.Label("staff-clockin-status", "Currently CLOCKED IN", 13));
            statusCard.AppendChild(CandyUI.Label("staff-duration",
                $"Duration: {dur.Hours:D2}:{dur.Minutes:D2}:{dur.Seconds:D2}"));
            statusCard.AppendChild(CandyUI.Muted("staff-shift-earnings",
                $"Earnings this shift: {shift.GilEarned:N0} Gil"));
            statusCard.AppendChild(CandyUI.Button("staff-clockout-btn", "Clock Out",
                () => _manager.ClockOut()));
        }
        else
        {
            statusCard.AppendChild(CandyUI.Muted("staff-clocked-out", "Currently CLOCKED OUT."));
            statusCard.AppendChild(CandyUI.Button("staff-clockin-btn", "Clock In",
                () => _manager.ClockIn()));
        }
        root.AppendChild(statusCard);

        root.AppendChild(CandyUI.Separator("staff-sep2"));

        // Recent shifts card
        var historyCard = CandyUI.Card("staff-history-card");
        historyCard.AppendChild(CandyUI.Label("staff-history-title", "Recent Shifts", 13));

        var history = _manager.ShiftHistory.Take(5).ToList();
        if (history.Count == 0)
        {
            historyCard.AppendChild(CandyUI.Muted("staff-no-history", "No completed shifts yet."));
        }
        else
        {
            for (int i = 0; i < history.Count; i++)
            {
                var s      = history[i];
                var dur    = s.Duration;
                var durStr = $"{dur.Hours:D2}:{dur.Minutes:D2}";
                historyCard.AppendChild(CandyUI.Label($"staff-shift-{i}",
                    $"{s.StartTime:MM/dd}  {durStr}  {s.GilEarned:N0} Gil"));
            }
        }
        root.AppendChild(historyCard);

        return root;
    }
}
