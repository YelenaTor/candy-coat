using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.UI;

namespace CandyCoat.Windows.Tabs;

public class OverviewTab : ITab
{
    private readonly Plugin _plugin;

    public string Name => "Overview";

    public OverviewTab(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(Name);
        if (!tab) return;
        DrawContent();
    }

    public void DrawContent()
    {
        ImGui.TextUnformatted("Welcome to Candy Coat!");
        ImGui.Spacing();

        ImGui.TextWrapped("Your cute venue assistant is ready <3");
        ImGui.Spacing();
        ImGui.Separator();

        if (_plugin.Configuration.IsManagementModeEnabled)
        {
            ImGui.Text("Dashboard & Analytics");
            ImGui.Spacing();

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var dailyEarnings = _plugin.Configuration.DailyEarnings.TryGetValue(today, out var val) ? val : 0;

            ImGui.Text($"Today's Earnings: {dailyEarnings:N0} Gil");

            var totalEarnings = _plugin.Configuration.DailyEarnings.Values.Sum();
            ImGui.Text($"All-Time Earnings: {totalEarnings:N0} Gil");

            ImGui.Spacing();
            ImGui.Text("Top 5 Spenders:");

            var topSpenders = _plugin.Configuration.Patrons
                .Where(p => p.TotalGilSpent > 0)
                .OrderByDescending(p => p.TotalGilSpent)
                .Take(5);

            bool hasSpenders = false;
            foreach (var p in topSpenders)
            {
                hasSpenders = true;
                ImGui.Text($"- {p.Name}: {p.TotalGilSpent:N0} Gil");
            }

            if (!hasSpenders)
            {
                ImGui.TextDisabled("No spending data yet.");
            }
        }
        else
        {
            // Clock-in status
            var shift = _plugin.ShiftManager.CurrentShift;
            if (shift != null)
            {
                var dur = shift.Duration;
                ImGui.TextColored(StyleManager.SyncOk,
                    $"Clocked in — {dur.Hours:D2}:{dur.Minutes:D2}:{dur.Seconds:D2}");
                ImGui.Text($"Earnings this shift: {shift.GilEarned:N0} Gil");
            }
            else
            {
                ImGui.TextDisabled("Not clocked in.");
            }

            ImGui.Spacing();

            // Active bookings
            var activeBookings = _plugin.Configuration.Bookings.Count(b => b.State == BookingState.Active);
            ImGui.Text($"Active Bookings: {activeBookings}");

            // Waitlist queue
            var waitlistCount = _plugin.WaitlistManager.Entries.Count;
            ImGui.Text($"Waitlist Queue: {waitlistCount}");

            ImGui.Spacing();
            ImGui.TextDisabled("Head to Bookings, Waitlist, or Staff Shifts to get started.");
        }
    }
}
