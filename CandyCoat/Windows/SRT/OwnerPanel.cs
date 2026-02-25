using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class OwnerPanel : IToolboxPanel
{
    private readonly Plugin _plugin;

    public string Name => "Owner";
    public StaffRole Role => StaffRole.Owner;

    // Menu editor
    private string _newItemName = string.Empty;
    private string _newItemDesc = string.Empty;
    private int _newItemPrice = 0;
    private int _newItemCategory = 0;
    private static readonly string[] CategoryLabels = { "Session", "Drink", "Game", "Performance", "Other" };

    // Room editor
    private string _newRoomName = string.Empty;

    // Blacklist
    private string _blPatron = string.Empty;
    private string _blReason = string.Empty;

    // Venue name
    private string _venueNameInput = string.Empty;
    private bool _venueNameInit = false;

    public OwnerPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void DrawContent()
    {
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), "👑 Owner Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

        DrawVenueInfo();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRevenueDashboard();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawStaffLeaderboard();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawServiceMenuEditor();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRoomEditor();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawBlacklistManagement();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPatronAnalytics();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPatronNotes();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawExport();
    }

    private void DrawVenueInfo()
    {
        ImGui.Text("Venue Info");
        if (!_venueNameInit)
        {
            _venueNameInput = _plugin.Configuration.VenueName;
            _venueNameInit = true;
        }
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("Venue Name", ref _venueNameInput, 100))
        {
            _plugin.Configuration.VenueName = _venueNameInput;
            _plugin.Configuration.Save();
        }
    }

    private void DrawRevenueDashboard()
    {
        ImGui.Text("Revenue Dashboard");
        ImGui.Spacing();

        var config = _plugin.Configuration;
        var totalEarnings = config.DailyEarnings.Values.Sum();
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var todayEarnings = config.DailyEarnings.TryGetValue(today, out var val) ? val : 0;

        // Earnings entries aggregate
        var earningsTotal = config.Earnings.Sum(e => e.Amount);
        var earningsToday = config.Earnings.Where(e => e.Timestamp.Date == DateTime.Today).Sum(e => e.Amount);

        ImGui.Text($"Today's Earnings: {todayEarnings + earningsToday:N0} Gil");
        ImGui.Text($"All-Time Earnings: {totalEarnings + earningsTotal:N0} Gil");

        ImGui.Spacing();

        // Per-role breakdown
        ImGui.TextDisabled("By Role:");
        foreach (StaffRole role in Enum.GetValues<StaffRole>())
        {
            if (role == StaffRole.None) continue;
            var roleTotal = config.Earnings.Where(e => e.Role == role).Sum(e => e.Amount);
            if (roleTotal != 0)
                ImGui.BulletText($"{role}: {roleTotal:N0} Gil");
        }

        // Recent 7 days
        ImGui.Spacing();
        ImGui.TextDisabled("Last 7 Days:");
        for (int d = 0; d < 7; d++)
        {
            var date = DateTime.Today.AddDays(-d);
            var dateStr = date.ToString("yyyy-MM-dd");
            var daily = config.DailyEarnings.TryGetValue(dateStr, out var dv) ? dv : 0;
            var dayEntries = config.Earnings.Where(e => e.Timestamp.Date == date).Sum(e => e.Amount);
            var combined = daily + dayEntries;
            if (combined != 0)
                ImGui.BulletText($"{date:MM/dd (ddd)}: {combined:N0} Gil");
        }
    }

    private void DrawStaffLeaderboard()
    {
        ImGui.Text("Staff Leaderboard");
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Leaderboard shows local data only. Cross-client staff earnings sync planned for a future update.");

        var myEarnings = _plugin.Configuration.Earnings
            .GroupBy(e => e.Role)
            .Select(g => (Role: g.Key, Total: g.Sum(e => e.Amount)))
            .OrderByDescending(x => x.Total).ToList();

        if (myEarnings.Count == 0)
        {
            ImGui.TextDisabled("No earnings logged.");
            return;
        }

        foreach (var (role, total) in myEarnings)
            ImGui.BulletText($"{role}: {total:N0} Gil");
    }

    private void DrawServiceMenuEditor()
    {
        ImGui.Text("Service Menu Editor");
        ImGui.Spacing();

        var menu = _plugin.Configuration.ServiceMenu;

        // Add new
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##ItemN", "Name", ref _newItemName, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##ItemD", "Description", ref _newItemDesc, 200);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("##ItemP", ref _newItemPrice, 1000);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.Combo("##ItemC", ref _newItemCategory, CategoryLabels, CategoryLabels.Length);
        ImGui.SameLine();
        if (ImGui.Button("+ Add"))
        {
            if (!string.IsNullOrWhiteSpace(_newItemName))
            {
                menu.Add(new ServiceMenuItem
                {
                    Name = _newItemName,
                    Description = _newItemDesc,
                    Price = _newItemPrice,
                    Category = (ServiceCategory)_newItemCategory,
                });
                _plugin.Configuration.Save();
                _newItemName = string.Empty;
                _newItemDesc = string.Empty;
                _newItemPrice = 0;
            }
        }

        // List
        for (int i = 0; i < menu.Count; i++)
        {
            var item = menu[i];
            ImGui.PushID($"mi{i}");
            ImGui.Text($"[{item.Category}] {item.Name} — {item.Price:N0} Gil");
            if (!string.IsNullOrEmpty(item.Description))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({item.Description})");
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Del"))
            {
                menu.RemoveAt(i);
                _plugin.Configuration.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        if (menu.Count == 0)
            ImGui.TextDisabled("No items in menu.");
    }

    private void DrawRoomEditor()
    {
        ImGui.Text("Room Editor");
        var rooms = _plugin.Configuration.Rooms;

        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##NewRoom", "Room Name", ref _newRoomName, 100);
        ImGui.SameLine();
        if (ImGui.Button("+ Add Room"))
        {
            if (!string.IsNullOrWhiteSpace(_newRoomName))
            {
                rooms.Add(new VenueRoom { Name = _newRoomName });
                _plugin.Configuration.Save();
                _newRoomName = string.Empty;
            }
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var color = room.Status switch
            {
                RoomStatus.Available => new Vector4(0.2f, 1f, 0.2f, 1f),
                RoomStatus.Occupied => new Vector4(1f, 0.4f, 0.4f, 1f),
                RoomStatus.Reserved => new Vector4(1f, 0.8f, 0.2f, 1f),
                _ => new Vector4(0.5f, 0.5f, 0.5f, 1f),
            };
            ImGui.PushID($"rm{i}");
            ImGui.TextColored(color, $"• {room.Name}: {room.Status}");
            if (room.Status == RoomStatus.Occupied)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({room.OccupiedBy} + {room.PatronName})");
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                rooms.RemoveAt(i);
                _plugin.Configuration.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
    }

    private void DrawBlacklistManagement()
    {
        ImGui.Text("Blacklist Management");
        ImGui.Spacing();

        // Add to blacklist
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##BLP", "Patron Name", ref _blPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##BLR", "Reason", ref _blReason, 200);
        ImGui.SameLine();
        if (ImGui.Button("Blacklist"))
        {
            if (!string.IsNullOrWhiteSpace(_blPatron))
            {
                var patron = _plugin.Configuration.Patrons.FirstOrDefault(p => p.Name == _blPatron);
                if (patron == null)
                {
                    patron = new Patron { Name = _blPatron };
                    _plugin.Configuration.Patrons.Add(patron);
                }
                patron.Status = PatronStatus.Blacklisted;
                patron.BlacklistReason = _blReason;
                patron.BlacklistDate = DateTime.Now;
                patron.BlacklistFlaggedBy = _plugin.Configuration.CharacterName;
                _plugin.Configuration.Save();
                _blPatron = string.Empty;
                _blReason = string.Empty;
            }
        }

        // List blacklisted
        var blacklisted = _plugin.Configuration.Patrons
            .Where(p => p.Status == PatronStatus.Blacklisted).ToList();

        if (blacklisted.Count == 0)
        {
            ImGui.TextDisabled("No blacklisted patrons.");
            return;
        }

        foreach (var p in blacklisted)
        {
            ImGui.PushID($"bl{p.Name}");
            ImGui.Text(p.Name);
            if (!string.IsNullOrEmpty(p.BlacklistReason))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"— {p.BlacklistReason}");
            }
            if (p.BlacklistDate.HasValue)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({p.BlacklistDate:MM/dd/yy})");
            }
            if (!string.IsNullOrEmpty(p.BlacklistFlaggedBy))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"by {p.BlacklistFlaggedBy}");
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Unban"))
            {
                p.Status = PatronStatus.Neutral;
                p.BlacklistReason = string.Empty;
                p.BlacklistDate = null;
                p.BlacklistFlaggedBy = string.Empty;
                _plugin.Configuration.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
    }

    private void DrawPatronAnalytics()
    {
        ImGui.Text("Patron Analytics");
        var patrons = _plugin.Configuration.Patrons;

        // Most visits
        var topVisitors = patrons.OrderByDescending(p => p.VisitCount).Take(5).ToList();
        if (topVisitors.Any(p => p.VisitCount > 0))
        {
            ImGui.TextDisabled("Most Visits:");
            foreach (var p in topVisitors.Where(p => p.VisitCount > 0))
                ImGui.BulletText($"{p.Name}: {p.VisitCount} visits");
        }

        // Highest spenders
        var topSpenders = patrons.OrderByDescending(p => p.TotalGilSpent).Take(5).ToList();
        if (topSpenders.Any(p => p.TotalGilSpent > 0))
        {
            ImGui.TextDisabled("Top Spenders:");
            foreach (var p in topSpenders.Where(p => p.TotalGilSpent > 0))
                ImGui.BulletText($"{p.Name}: {p.TotalGilSpent:N0} Gil");
        }

        if (!topVisitors.Any(p => p.VisitCount > 0) && !topSpenders.Any(p => p.TotalGilSpent > 0))
            ImGui.TextDisabled("No analytics data yet.");
    }

    private void DrawPatronNotes()
    {
        ImGui.Text("All Patron Notes (Owner View)");
        var notes = _plugin.Configuration.PatronNotes
            .OrderByDescending(n => n.Timestamp)
            .Take(20).ToList();

        if (notes.Count == 0)
        {
            ImGui.TextDisabled("No patron notes yet.");
            return;
        }

        foreach (var n in notes)
        {
            ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}] [{n.AuthorRole}] {n.AuthorName}");
            ImGui.SameLine();
            ImGui.Text($"→ {n.PatronName}:");
            ImGui.SameLine();
            ImGui.TextWrapped(n.Content);
        }
    }

    private void DrawExport()
    {
        ImGui.Text("Export");
        if (ImGui.Button("Copy Earnings Summary to Clipboard"))
        {
            var config = _plugin.Configuration;
            var lines = new List<string>();
            lines.Add($"=== {config.VenueName} Earnings Report ===");
            lines.Add($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            lines.Add("");

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var todayLegacy = config.DailyEarnings.TryGetValue(today, out var dv) ? dv : 0;
            var todayEntries = config.Earnings.Where(e => e.Timestamp.Date == DateTime.Today).Sum(e => e.Amount);
            lines.Add($"Today: {todayLegacy + todayEntries:N0} Gil");

            var totalLegacy = config.DailyEarnings.Values.Sum();
            var totalEntries = config.Earnings.Sum(e => e.Amount);
            lines.Add($"All-Time: {totalLegacy + totalEntries:N0} Gil");
            lines.Add("");

            foreach (StaffRole role in Enum.GetValues<StaffRole>())
            {
                if (role == StaffRole.None) continue;
                var roleTotal = config.Earnings.Where(e => e.Role == role).Sum(e => e.Amount);
                if (roleTotal != 0)
                    lines.Add($"  {role}: {roleTotal:N0} Gil");
            }

            ImGui.SetClipboardText(string.Join("\n", lines));
            Svc.Chat.Print("[Candy Coat] Earnings summary copied to clipboard!");
        }
    }
}
