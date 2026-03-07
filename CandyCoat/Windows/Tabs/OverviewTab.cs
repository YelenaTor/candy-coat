using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.UI;
using Una.Drawing;

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
                ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f),
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

    public Node BuildNode()
    {
        var root    = UdtHelper.CreateFromTemplate("overview-tab.xml", "overview-layout");
        var dynamic = root.QuerySelector("#overview-dynamic")!;

        var cfg = _plugin.Configuration;

        if (cfg.IsManagementModeEnabled)
        {
            var today          = DateTime.Now.ToString("yyyy-MM-dd");
            var dailyEarnings  = cfg.DailyEarnings.TryGetValue(today, out var dVal) ? dVal : 0;
            var totalEarnings  = cfg.DailyEarnings.Values.Sum();

            var earningsCard = CandyUI.Card("overview-earnings-card");
            earningsCard.AppendChild(CandyUI.Label("overview-earnings-title", "Dashboard & Analytics", 13));
            earningsCard.AppendChild(CandyUI.Label("overview-daily",  $"Today's Earnings: {dailyEarnings:N0} Gil"));
            earningsCard.AppendChild(CandyUI.Label("overview-total",  $"All-Time Earnings: {totalEarnings:N0} Gil"));
            dynamic.AppendChild(earningsCard);

            var spendersCard = CandyUI.Card("overview-spenders-card");
            spendersCard.AppendChild(CandyUI.Label("overview-spenders-title", "Top 5 Spenders", 13));

            var topSpenders = cfg.Patrons
                .Where(p => p.TotalGilSpent > 0)
                .OrderByDescending(p => p.TotalGilSpent)
                .Take(5)
                .ToList();

            if (topSpenders.Count == 0)
            {
                spendersCard.AppendChild(CandyUI.Muted("overview-no-spenders", "No spending data yet."));
            }
            else
            {
                for (int i = 0; i < topSpenders.Count; i++)
                {
                    var p = topSpenders[i];
                    spendersCard.AppendChild(CandyUI.Label($"overview-spender-{i}", $"{p.Name}: {p.TotalGilSpent:N0} Gil"));
                }
            }
            dynamic.AppendChild(spendersCard);
        }
        else
        {
            var shift = _plugin.ShiftManager.CurrentShift;
            var statusCard = CandyUI.Card("overview-status-card");

            if (shift != null)
            {
                var dur = shift.Duration;
                statusCard.AppendChild(CandyUI.Label("overview-clockin-label",
                    $"Clocked in — {dur.Hours:D2}:{dur.Minutes:D2}:{dur.Seconds:D2}"));
                statusCard.AppendChild(CandyUI.Muted("overview-shift-earnings",
                    $"Earnings this shift: {shift.GilEarned:N0} Gil"));
            }
            else
            {
                statusCard.AppendChild(CandyUI.Muted("overview-clocked-out", "Not clocked in."));
            }

            var activeBookings = cfg.Bookings.Count(b => b.State == BookingState.Active);
            var waitlistCount  = _plugin.WaitlistManager.Entries.Count;

            statusCard.AppendChild(CandyUI.Separator("overview-status-sep"));
            statusCard.AppendChild(CandyUI.Label("overview-bookings-count", $"Active Bookings: {activeBookings}"));
            statusCard.AppendChild(CandyUI.Label("overview-waitlist-count", $"Waitlist Queue: {waitlistCount}"));
            dynamic.AppendChild(statusCard);

            dynamic.AppendChild(CandyUI.Muted("overview-hint",
                "Head to Bookings, Waitlist, or Staff Shifts to get started."));
        }

        return root;
    }
}
