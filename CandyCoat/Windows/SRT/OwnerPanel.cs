using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.UI;
using Una.Drawing;

using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class OwnerPanel : IToolboxPanel
{
    private readonly Plugin _plugin;

    public string Name => "Owner";
    public StaffRole Role => StaffRole.Owner;

    // Settings input
    private string _newItemName = string.Empty;
    private string _newItemDesc = string.Empty;
    private int _newItemPrice = 0;
    private int _newItemCategory = 0;
    private static readonly string[] CategoryLabels = { "Session", "Drink", "Game", "Performance", "Other" };
    private string _newRoomName = string.Empty;
    private string _venueNameInput = string.Empty;
    private bool _venueNameInit = false;
    private bool _venueKeyRevealed = false;

    // Content input
    private string _blPatron = string.Empty;
    private string _blReason = string.Empty;

    // VIP package fields — add form
    private string _vipNewName = string.Empty;
    private string _vipNewDesc = string.Empty;
    private string _vipNewPerks = string.Empty;
    private int _vipNewPrice = 0;
    private int _vipNewTierIdx = 0;
    private int _vipNewDurationIdx = 0;

    // VIP package fields — edit form
    private Guid _vipEditingId = Guid.Empty;
    private string _vipEditName = string.Empty;
    private string _vipEditDesc = string.Empty;
    private string _vipEditPerks = string.Empty;
    private int _vipEditPrice = 0;
    private int _vipEditTierIdx = 0;
    private int _vipEditDurationIdx = 0;
    private bool _vipEditIsActive = true;

    // VIP delete confirmation
    private Guid _vipDeleteConfirmId = Guid.Empty;

    private static readonly string[] VipTierLabels     = { "Bronze", "Silver", "Gold", "Platinum" };
    private static readonly string[] VipDurationLabels = { "Monthly", "One-Time", "Permanent" };

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);

    public OwnerPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features (Operational / Analytics) ──────────────────────────────────

    public void DrawContent()
    {
        using var tabs = ImRaii.TabBar("##OwnerTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!tabs) return;

        if (ImGui.BeginTabItem("Revenue##Owner"))
        {
            DrawRevenueDashboard();
            DrawStaffLeaderboard();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Patrons##Owner"))
        {
            DrawPatronAnalytics();
            DrawPatronNotes();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Manage##Owner"))
        {
            DrawBlacklistManagement();
            DrawExport();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Ping##Owner"))
        {
            ImGui.Spacing();
            _pingWidget.Draw();
            ImGui.EndTabItem();
        }
    }

    // ─── Settings (Configuration / Admin) ────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Owner Settings");
        ImGui.TextDisabled("Venue configuration, rooms, menu, and role cosmetics.");
        ImGui.Spacing();

        // Card: Venue Registration (read-only credentials display)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var regCard = ImRaii.Child("##OwnerVenueRegCard", new Vector2(0, 128f), true))
        {
            ImGui.PopStyleColor();
            if (regCard)
            {
                var cfg = _plugin.Configuration;
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Venue Registration");
                ImGui.Separator();
                ImGui.Spacing();

                // Venue Name
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Name:");
                ImGui.SameLine(60f);
                ImGui.Text(string.IsNullOrEmpty(cfg.VenueName) ? "(not set)" : cfg.VenueName);

                // Venue ID
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "ID:");
                ImGui.SameLine(60f);
                ImGui.Text(string.IsNullOrEmpty(cfg.VenueId) ? "(not set)" : cfg.VenueId);
                if (!string.IsNullOrEmpty(cfg.VenueId))
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Copy##OwnerCopyVenueId"))
                        ImGui.SetClipboardText(cfg.VenueId);
                }

                // Venue Key (masked with reveal toggle)
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Key:");
                ImGui.SameLine(60f);
                if (string.IsNullOrEmpty(cfg.VenueKey))
                {
                    ImGui.Text("(not set)");
                }
                else
                {
                    ImGui.Text(_venueKeyRevealed ? cfg.VenueKey : new string('\u2022', Math.Min(cfg.VenueKey.Length, 22)));
                    ImGui.SameLine();
                    if (ImGui.SmallButton(_venueKeyRevealed ? "Hide##OwnerKeyHide" : "Show##OwnerKeyShow"))
                        _venueKeyRevealed = !_venueKeyRevealed;
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Copy##OwnerCopyVenueKey"))
                        ImGui.SetClipboardText(cfg.VenueKey);
                }

                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.5f, 0.7f, 1f, 1f), "\u2139 Share the Key with staff — they enter it in the setup wizard.");
            }
        }

        ImGui.Spacing();

        // Card: Venue Info (editable name)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var venueCard = ImRaii.Child("##OwnerVenueCard", new Vector2(0, 80f), true))
        {
            ImGui.PopStyleColor();
            if (venueCard)
            {
                if (!_venueNameInit) { _venueNameInput = _plugin.Configuration.VenueName; _venueNameInit = true; }
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Venue Info");
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputText("Venue Name##Owner", ref _venueNameInput, 100)) { _plugin.Configuration.VenueName = _venueNameInput; _plugin.Configuration.Save(); }
            }
        }

        ImGui.Spacing();

        // Card: Room Editor
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var roomCard = ImRaii.Child("##OwnerRoomCard", new Vector2(0, 200f), true))
        {
            ImGui.PopStyleColor();
            if (roomCard)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Room Editor");
                ImGui.Separator();
                ImGui.Spacing();
                var rooms = _plugin.Configuration.Rooms;
                ImGui.SetNextItemWidth(150);
                ImGui.InputTextWithHint("##OwnerNewRoom", "Room Name", ref _newRoomName, 100);
                ImGui.SameLine();
                if (ImGui.Button("+ Add Room##Owner"))
                {
                    if (!string.IsNullOrWhiteSpace(_newRoomName)) { rooms.Add(new VenueRoom { Name = _newRoomName }); _plugin.Configuration.Save(); _newRoomName = string.Empty; }
                }
                using var roomScroll = ImRaii.Child("##OwnerRoomList", new Vector2(0, 100f), false);
                for (int i = 0; i < rooms.Count; i++)
                {
                    var room = rooms[i];
                    var color = room.Status switch { RoomStatus.Available => new Vector4(0.5f, 0.9f, 0.65f, 1.0f), RoomStatus.Occupied => new Vector4(1f, 0.4f, 0.4f, 1f), RoomStatus.Reserved => new Vector4(1f, 0.8f, 0.2f, 1f), _ => new Vector4(0.5f, 0.5f, 0.5f, 1f) };
                    ImGui.PushID($"ownrm{i}");
                    ImGui.TextColored(color, $"\u2022 {room.Name}: {room.Status}");
                    if (room.Status == RoomStatus.Occupied) { ImGui.SameLine(); ImGui.TextDisabled($"({room.OccupiedBy} + {room.PatronName})"); }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Remove##Owner")) { rooms.RemoveAt(i); _plugin.Configuration.Save(); ImGui.PopID(); break; }
                    ImGui.PopID();
                }
            }
        }

        ImGui.Spacing();

        // Card: Service Menu Editor
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var menuCard = ImRaii.Child("##OwnerMenuCard", new Vector2(0, 220f), true))
        {
            ImGui.PopStyleColor();
            if (menuCard)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Service Menu Editor");
                ImGui.Separator();
                ImGui.Spacing();
                var menu = _plugin.Configuration.ServiceMenu;
                ImGui.SetNextItemWidth(110);
                ImGui.InputTextWithHint("##OwnerItemN", "Name", ref _newItemName, 100);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(130);
                ImGui.InputTextWithHint("##OwnerItemD", "Description", ref _newItemDesc, 200);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(70);
                ImGui.InputInt("##OwnerItemP", ref _newItemPrice, 1000);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90);
                ImGui.Combo("##OwnerItemC", ref _newItemCategory, CategoryLabels, CategoryLabels.Length);
                ImGui.SameLine();
                if (ImGui.Button("+ Add##Owner"))
                {
                    if (!string.IsNullOrWhiteSpace(_newItemName)) { menu.Add(new ServiceMenuItem { Name = _newItemName, Description = _newItemDesc, Price = _newItemPrice, Category = (ServiceCategory)_newItemCategory }); _plugin.Configuration.Save(); _newItemName = string.Empty; _newItemDesc = string.Empty; _newItemPrice = 0; }
                }
                using var menuScroll = ImRaii.Child("##OwnerMenuList", new Vector2(0, 100f), false);
                for (int i = 0; i < menu.Count; i++)
                {
                    var item = menu[i];
                    ImGui.PushID($"ownmi{i}");
                    ImGui.Text($"[{item.Category}] {item.Name} \u2014 {item.Price:N0} Gil");
                    if (!string.IsNullOrEmpty(item.Description)) { ImGui.SameLine(); ImGui.TextDisabled($"({item.Description})"); }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Del##Owner")) { menu.RemoveAt(i); _plugin.Configuration.Save(); ImGui.PopID(); break; }
                    ImGui.PopID();
                }
                if (menu.Count == 0) ImGui.TextDisabled("No items in menu.");
            }
        }

        ImGui.Spacing();

        // Card: VIP Packages
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var vipCard = ImRaii.Child("##OwnerVipCard", new Vector2(0, 430f), true))
        {
            ImGui.PopStyleColor();
            if (vipCard)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "VIP Packages");
                ImGui.Separator();
                ImGui.Spacing();
                DrawVipPackages();
            }
        }

        ImGui.Spacing();

        // Card: Loyalty Tier Thresholds
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tierCard = ImRaii.Child("##OwnerTierCard", new Vector2(0, 140f), true))
        {
            ImGui.PopStyleColor();
            if (tierCard) DrawLoyaltyTierThresholds();
        }

        ImGui.Spacing();

        // Card: Role Cosmetic Defaults
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        var roleCount = Enum.GetValues<StaffRole>().Count(r => r != StaffRole.None);
        using (var cosCard = ImRaii.Child("##OwnerCosCard", new Vector2(0, roleCount * 30f + 50f), true))
        {
            ImGui.PopStyleColor();
            if (cosCard) DrawRoleCosmeticDefaults();
        }
    }

    // ─── VIP Package Management ──────────────────────────────────────────────

    private void DrawVipPackages()
    {
        var cfg      = _plugin.Configuration;
        var packages = cfg.VipPackages;

        // Package list
        if (packages.Count == 0)
        {
            ImGui.TextDisabled("No VIP packages yet. Create one below.");
        }
        else
        {
            float listH = Math.Min(packages.Count * 28f + 4f, 140f);
            using var listChild = ImRaii.Child("##VipPkgList", new Vector2(0, listH), false);
            for (int i = 0; i < packages.Count; i++)
            {
                var pkg = packages[i];
                ImGui.PushID($"vippkg{i}");

                var tierCol = VipColours.GetTierColour(pkg.Tier);
                ImGui.TextColored(tierCol, $"◆ {pkg.Tier}");
                ImGui.SameLine(0, 6f);
                ImGui.Text(pkg.Name);
                ImGui.SameLine(0, 6f);
                ImGui.TextDisabled($"[{pkg.DurationType}]");
                ImGui.SameLine(0, 6f);
                ImGui.TextDisabled($"{pkg.PriceGil:N0} Gil");
                ImGui.SameLine(0, 6f);
                if (pkg.IsActive)
                    ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), "Active");
                else
                    ImGui.TextDisabled("Disabled");
                ImGui.SameLine(0, 6f);
                if (ImGui.SmallButton("Edit##VipPkg"))
                {
                    _vipEditingId       = pkg.Id;
                    _vipEditName        = pkg.Name;
                    _vipEditDesc        = pkg.Description;
                    _vipEditPerks       = string.Join("\n", pkg.Perks);
                    _vipEditPrice       = pkg.PriceGil;
                    _vipEditTierIdx     = (int)pkg.Tier;
                    _vipEditDurationIdx = (int)pkg.DurationType;
                    _vipEditIsActive    = pkg.IsActive;
                }
                ImGui.SameLine(0, 4f);
                if (ImGui.SmallButton("Del##VipPkg"))
                    _vipDeleteConfirmId = pkg.Id;

                ImGui.PopID();
            }
        }

        // Inline edit form
        if (_vipEditingId != Guid.Empty)
        {
            var editPkg = packages.FirstOrDefault(p => p.Id == _vipEditingId);
            if (editPkg != null)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Edit Package");
                ImGui.Separator();
                ImGui.SetNextItemWidth(150f); ImGui.InputText("Name##VipE", ref _vipEditName, 100);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90f);  ImGui.Combo("Tier##VipE", ref _vipEditTierIdx, VipTierLabels, VipTierLabels.Length);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90f);  ImGui.Combo("Duration##VipE", ref _vipEditDurationIdx, VipDurationLabels, VipDurationLabels.Length);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(80f);  ImGui.InputInt("Price##VipE", ref _vipEditPrice, 0);
                ImGui.Checkbox("Active##VipE", ref _vipEditIsActive);
                ImGui.InputTextMultiline("Desc##VipEDesc", ref _vipEditDesc, 200, new Vector2(0, 36f));
                ImGui.InputTextMultiline("Perks (one per line)##VipEPerks", ref _vipEditPerks, 500, new Vector2(0, 54f));

                if (ImGui.Button("Save##VipE"))
                {
                    editPkg.Name         = _vipEditName;
                    editPkg.Description  = _vipEditDesc;
                    editPkg.Perks        = _vipEditPerks.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                    editPkg.PriceGil     = _vipEditPrice;
                    editPkg.Tier         = (VipTier)_vipEditTierIdx;
                    editPkg.DurationType = (VipDurationType)_vipEditDurationIdx;
                    editPkg.IsActive     = _vipEditIsActive;
                    cfg.Save();
                    _vipEditingId = Guid.Empty;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel##VipE")) _vipEditingId = Guid.Empty;
            }
            else
            {
                _vipEditingId = Guid.Empty;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Add new package form
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "+ Add Package");
        ImGui.SetNextItemWidth(150f); ImGui.InputTextWithHint("##VipNewN", "Name", ref _vipNewName, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);  ImGui.Combo("##VipNewT", ref _vipNewTierIdx, VipTierLabels, VipTierLabels.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(85f);  ImGui.Combo("##VipNewD", ref _vipNewDurationIdx, VipDurationLabels, VipDurationLabels.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);  ImGui.InputInt("##VipNewP", ref _vipNewPrice, 0);
        ImGui.InputTextMultiline("Desc##VipNewDesc", ref _vipNewDesc, 200, new Vector2(0, 36f));
        ImGui.InputTextMultiline("Perks (one per line)##VipNewPerks", ref _vipNewPerks, 500, new Vector2(0, 54f));

        if (ImGui.Button("Create##VipNew"))
        {
            if (!string.IsNullOrWhiteSpace(_vipNewName))
            {
                packages.Add(new VipPackageDefinition
                {
                    Name         = _vipNewName,
                    Description  = _vipNewDesc,
                    Perks        = _vipNewPerks.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    PriceGil     = _vipNewPrice,
                    Tier         = (VipTier)_vipNewTierIdx,
                    DurationType = (VipDurationType)_vipNewDurationIdx,
                    IsActive     = true
                });
                cfg.Save();
                _vipNewName = _vipNewDesc = _vipNewPerks = string.Empty;
                _vipNewPrice = _vipNewTierIdx = _vipNewDurationIdx = 0;
            }
        }

        // Delete confirmation popup
        if (_vipDeleteConfirmId != Guid.Empty)
            ImGui.OpenPopup("VipDeleteConfirm##Owner");

        if (ImGui.BeginPopup("VipDeleteConfirm##Owner"))
        {
            var delPkg = packages.FirstOrDefault(p => p.Id == _vipDeleteConfirmId);
            if (delPkg != null)
            {
                ImGui.Text($"Delete package: {delPkg.Name}?");
                var activeCount = cfg.Patrons.Count(p =>
                    p.ActiveVip != null && p.ActiveVip.PackageId == _vipDeleteConfirmId);
                if (activeCount > 0)
                    ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f),
                        $"Warning: {activeCount} patron{(activeCount != 1 ? "s" : "")} hold an active subscription.");

                if (ImGui.Button("Delete##VipDelConfirm"))
                {
                    packages.RemoveAll(p => p.Id == _vipDeleteConfirmId);
                    cfg.Save();
                    _vipDeleteConfirmId = Guid.Empty;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel##VipDelConfirm"))
                {
                    _vipDeleteConfirmId = Guid.Empty;
                    ImGui.CloseCurrentPopup();
                }
            }
            else
            {
                _vipDeleteConfirmId = Guid.Empty;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    // ─── Private Draw Helpers ────────────────────────────────────────────────

    private void DrawRevenueDashboard()
    {
        ImGui.Spacing();
        var config = _plugin.Configuration;
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var todayLegacy = config.DailyEarnings.TryGetValue(today, out var val) ? val : 0;
        var earningsTotal = config.Earnings.Sum(e => e.Amount);
        var earningsToday = config.Earnings.Where(e => e.Timestamp.Date == DateTime.Today).Sum(e => e.Amount);
        ImGui.Text($"Today's Earnings: {todayLegacy + earningsToday:N0} Gil");
        ImGui.Text($"All-Time Earnings: {config.DailyEarnings.Values.Sum() + earningsTotal:N0} Gil");
        ImGui.Spacing();
        ImGui.TextDisabled("By Role:");
        foreach (StaffRole role in Enum.GetValues<StaffRole>())
        {
            if (role == StaffRole.None) continue;
            var roleTotal = config.Earnings.Where(e => e.Role == role).Sum(e => e.Amount);
            if (roleTotal != 0) ImGui.BulletText($"{role}: {roleTotal:N0} Gil");
        }
        ImGui.Spacing();
        ImGui.TextDisabled("Last 7 Days:");
        for (int d = 0; d < 7; d++)
        {
            var date = DateTime.Today.AddDays(-d);
            var dateStr = date.ToString("yyyy-MM-dd");
            var daily = config.DailyEarnings.TryGetValue(dateStr, out var dv) ? dv : 0;
            var combined = daily + config.Earnings.Where(e => e.Timestamp.Date == date).Sum(e => e.Amount);
            if (combined != 0) ImGui.BulletText($"{date:MM/dd} ({date.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)}): {combined:N0} Gil");
        }
        ImGui.Spacing();
    }

    private void DrawStaffLeaderboard()
    {
        ImGui.Spacing();
        if (_plugin.SyncService.IsConnected) ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), "\u25cf Showing synced data."); else ImGui.TextColored(new Vector4(1.0f, 0.85f, 0.4f, 1.0f), "\u26a0 Local data only.");
        var myEarnings = _plugin.Configuration.Earnings.GroupBy(e => e.Role).Select(g => (Role: g.Key, Total: g.Sum(e => e.Amount))).OrderByDescending(x => x.Total).ToList();
        if (myEarnings.Count == 0) { ImGui.TextDisabled("No earnings logged."); ImGui.Spacing(); return; }
        foreach (var (role, total) in myEarnings) ImGui.BulletText($"{role}: {total:N0} Gil");
        ImGui.Spacing();
    }

    private void DrawPatronAnalytics()
    {
        ImGui.Spacing();
        var patrons = _plugin.Configuration.Patrons;
        var topVisitors = patrons.OrderByDescending(p => p.VisitCount).Take(5).ToList();
        if (topVisitors.Any(p => p.VisitCount > 0)) { ImGui.TextDisabled("Most Visits:"); foreach (var p in topVisitors.Where(p => p.VisitCount > 0)) ImGui.BulletText($"{p.Name}: {p.VisitCount} visits"); }
        var topSpenders = patrons.OrderByDescending(p => p.TotalGilSpent).Take(5).ToList();
        if (topSpenders.Any(p => p.TotalGilSpent > 0)) { ImGui.TextDisabled("Top Spenders:"); foreach (var p in topSpenders.Where(p => p.TotalGilSpent > 0)) ImGui.BulletText($"{p.Name}: {p.TotalGilSpent:N0} Gil"); }
        if (!topVisitors.Any(p => p.VisitCount > 0) && !topSpenders.Any(p => p.TotalGilSpent > 0)) ImGui.TextDisabled("No analytics data yet.");
        ImGui.Spacing();
    }

    private void DrawPatronNotes()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Owner sees notes from all roles.");
        var notes = _plugin.Configuration.PatronNotes.OrderByDescending(n => n.Timestamp).Take(20).ToList();
        if (notes.Count == 0) { ImGui.TextDisabled("No patron notes yet."); ImGui.Spacing(); return; }
        using var scroll = ImRaii.Child("OwnerNotes", new Vector2(0, 150), true);
        foreach (var n in notes) { ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}] [{n.AuthorRole}] {n.AuthorName}"); ImGui.SameLine(); ImGui.Text($"\u2192 {n.PatronName}:"); ImGui.SameLine(); ImGui.TextWrapped(n.Content); }
        ImGui.Spacing();
    }

    private void DrawBlacklistManagement()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##OwnerBLP", "Patron Name", ref _blPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##OwnerBLR", "Reason", ref _blReason, 200);
        ImGui.SameLine();
        if (ImGui.Button("Blacklist##Owner"))
        {
            if (!string.IsNullOrWhiteSpace(_blPatron))
            {
                var patron = _plugin.Configuration.Patrons.FirstOrDefault(p => p.Name == _blPatron);
                if (patron == null) { patron = new Patron { Name = _blPatron }; _plugin.Configuration.Patrons.Add(patron); }
                patron.Status = PatronStatus.Blacklisted;
                patron.BlacklistReason = _blReason;
                patron.BlacklistDate = DateTime.Now;
                patron.BlacklistFlaggedBy = _plugin.Configuration.CharacterName;
                _plugin.Configuration.Save();
                _blPatron = string.Empty; _blReason = string.Empty;
            }
        }
        var blacklisted = _plugin.Configuration.Patrons.Where(p => p.Status == PatronStatus.Blacklisted).ToList();
        if (blacklisted.Count == 0) { ImGui.TextDisabled("No blacklisted patrons."); ImGui.Spacing(); return; }
        using var blScroll = ImRaii.Child("BLList##Owner", new Vector2(0, 120), true);
        foreach (var p in blacklisted)
        {
            ImGui.PushID($"ownbl{p.Name}");
            ImGui.Text(p.Name);
            if (!string.IsNullOrEmpty(p.BlacklistReason)) { ImGui.SameLine(); ImGui.TextDisabled($"\u2014 {p.BlacklistReason}"); }
            ImGui.SameLine();
            if (ImGui.SmallButton("Unban##Owner")) { p.Status = PatronStatus.Neutral; p.BlacklistReason = string.Empty; p.BlacklistDate = null; p.BlacklistFlaggedBy = string.Empty; _plugin.Configuration.Save(); ImGui.PopID(); break; }
            ImGui.PopID();
        }
        ImGui.Spacing();
    }

    private void DrawLoyaltyTierThresholds()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Patron Loyalty Tier Thresholds");
        ImGui.Separator();
        ImGui.Spacing();
        var config = _plugin.Configuration;
        bool dirty = false;

        ImGui.TextColored(new Vector4(1f, 0.5f, 0.8f, 1f), "\u2665 Regular");
        ImGui.SameLine(); ImGui.TextDisabled("(OR condition)");
        ImGui.SetNextItemWidth(80);
        int rv = config.RegularTierVisits;
        if (ImGui.InputInt("Visits##RegV", ref rv, 1) && rv >= 1) { config.RegularTierVisits = rv; dirty = true; }
        ImGui.SameLine();
        int rg = config.RegularTierGil;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputInt("Gil##RegG", ref rg, 10000) && rg >= 0) { config.RegularTierGil = rg; dirty = true; }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), "\u2605 Elite");
        ImGui.SameLine(); ImGui.TextDisabled("(OR condition)");
        ImGui.SetNextItemWidth(80);
        int ev = config.EliteTierVisits;
        if (ImGui.InputInt("Visits##ElV", ref ev, 1) && ev >= 1) { config.EliteTierVisits = ev; dirty = true; }
        ImGui.SameLine();
        int eg = config.EliteTierGil;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputInt("Gil##ElG", ref eg, 100000) && eg >= 0) { config.EliteTierGil = eg; dirty = true; }

        if (dirty) config.Save();
    }

    private void DrawRoleCosmeticDefaults()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Role Cosmetic Defaults");
        ImGui.Separator();
        ImGui.Spacing();
        var config = _plugin.Configuration;
        bool dirty = false;
        foreach (StaffRole role in Enum.GetValues<StaffRole>())
        {
            if (role == StaffRole.None) continue;
            if (!config.RoleDefaults.TryGetValue(role, out var rd)) { rd = new RoleDefaultCosmetic(); config.RoleDefaults[role] = rd; dirty = true; }
            ImGui.PushID($"OwnerRD_{role}");
            bool enabled = rd.Enabled;
            if (ImGui.Checkbox($"##{role}_en", ref enabled)) { rd.Enabled = enabled; dirty = true; }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), role.ToString());
            ImGui.SameLine();
            int badgeIdx = System.Array.IndexOf(CosmeticRenderer.BadgeTemplates, rd.BadgeTemplate);
            if (badgeIdx < 0) badgeIdx = 1;
            ImGui.SetNextItemWidth(90);
            if (ImGui.Combo($"##badge_{role}", ref badgeIdx, CosmeticRenderer.BadgeTemplates, CosmeticRenderer.BadgeTemplates.Length)) { rd.BadgeTemplate = CosmeticRenderer.BadgeTemplates[badgeIdx]; dirty = true; }
            ImGui.SameLine();
            var glowColor = rd.GlowColor;
            if (ImGui.ColorEdit4($"Glow##{role}", ref glowColor, ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaBar)) { rd.GlowColor = glowColor; dirty = true; }
            ImGui.PopID();
        }
        if (dirty) config.Save();
    }

    private void DrawExport()
    {
        ImGui.Spacing();
        if (ImGui.Button("Copy Earnings Summary to Clipboard##Owner"))
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
            lines.Add($"All-Time: {config.DailyEarnings.Values.Sum() + config.Earnings.Sum(e => e.Amount):N0} Gil");
            lines.Add("");
            foreach (StaffRole role in Enum.GetValues<StaffRole>()) { if (role == StaffRole.None) continue; var rt = config.Earnings.Where(e => e.Role == role).Sum(e => e.Amount); if (rt != 0) lines.Add($"  {role}: {rt:N0} Gil"); }
            ImGui.SetClipboardText(string.Join("\n", lines));
            Svc.Chat.Print("[Candy Coat] Earnings summary copied to clipboard!");
        }
        ImGui.Spacing();
    }

    // ─── Una.Drawing ─────────────────────────────────────────────────────────

    private int _owActiveTab = 0;
    private static readonly string[] OwTabs = ["Revenue", "Patrons", "Manage", "Ping"];

    public Node BuildNode()
    {
        var root    = UdtHelper.CreateFromTemplate("srt-owner.xml", "owner-layout");
        var dynamic = root.QuerySelector("#srt-owner-dynamic")!;
        Node content = _owActiveTab switch {
            0 => BuildOwTabRevenue(),
            1 => BuildOwTabPatrons(),
            2 => BuildOwTabManage(),
            _ => BuildOwTabPing(),
        };
        dynamic.AppendChild(CandyUI.TabContainer("ow-tabs", OwTabs, _owActiveTab,
            idx => { _owActiveTab = idx; }, content));
        return root;
    }

    private Node BuildOwTabRevenue()
    {
        var col = CandyUI.Column("ow-revenue", 6);
        var config = _plugin.Configuration;
        var today       = DateTime.Now.ToString("yyyy-MM-dd");
        var todayLegacy = config.DailyEarnings.TryGetValue(today, out var dv) ? dv : 0;
        var earningsToday = config.Earnings.Where(e => e.Timestamp.Date == DateTime.Today).Sum(e => e.Amount);
        var earningsTotal = config.Earnings.Sum(e => e.Amount);

        var summaryCard = CandyUI.Card("ow-rev-summary-card");
        summaryCard.AppendChild(CandyUI.Label("ow-rev-today",
            $"Today's Earnings: {todayLegacy + earningsToday:N0} Gil", 13));
        summaryCard.AppendChild(CandyUI.Label("ow-rev-alltime",
            $"All-Time Earnings: {config.DailyEarnings.Values.Sum() + earningsTotal:N0} Gil", 13));
        col.AppendChild(summaryCard);

        col.AppendChild(CandyUI.Separator("ow-rev-sep1"));
        col.AppendChild(CandyUI.SectionHeader("ow-rev-role-hdr", "By Role"));
        var roleCard = CandyUI.Card("ow-rev-role-card");
        bool anyRole = false;
        foreach (StaffRole role in Enum.GetValues<StaffRole>())
        {
            if (role == StaffRole.None) continue;
            var roleTotal = config.Earnings.Where(e => e.Role == role).Sum(e => e.Amount);
            if (roleTotal != 0)
            {
                roleCard.AppendChild(CandyUI.Label($"ow-rev-role-{role}", $"{role}: {roleTotal:N0} Gil", 12));
                anyRole = true;
            }
        }
        if (!anyRole) roleCard.AppendChild(CandyUI.Muted("ow-rev-role-empty", "No earnings logged."));
        col.AppendChild(roleCard);

        col.AppendChild(CandyUI.Separator("ow-rev-sep2"));
        col.AppendChild(CandyUI.SectionHeader("ow-rev-7day-hdr", "Last 7 Days"));
        var weekCard = CandyUI.Card("ow-rev-week-card");
        bool anyDay = false;
        for (int d = 0; d < 7; d++)
        {
            var date        = DateTime.Today.AddDays(-d);
            var dateStr     = date.ToString("yyyy-MM-dd");
            var daily       = config.DailyEarnings.TryGetValue(dateStr, out var v) ? v : 0;
            var combined    = daily + config.Earnings.Where(e => e.Timestamp.Date == date).Sum(e => e.Amount);
            if (combined != 0)
            {
                weekCard.AppendChild(CandyUI.Label($"ow-rev-day-{d}",
                    $"{date:MM/dd} ({date.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)}): {combined:N0} Gil", 12));
                anyDay = true;
            }
        }
        if (!anyDay) weekCard.AppendChild(CandyUI.Muted("ow-rev-week-empty", "No data for last 7 days."));
        col.AppendChild(weekCard);

        // Staff leaderboard
        col.AppendChild(CandyUI.Separator("ow-rev-sep3"));
        col.AppendChild(CandyUI.SectionHeader("ow-rev-staff-hdr", "Staff Leaderboard"));
        var staffCard = CandyUI.Card("ow-rev-staff-card");
        var myEarnings = config.Earnings
            .GroupBy(e => e.Role)
            .Select(g => (Role: g.Key, Total: g.Sum(e => e.Amount)))
            .OrderByDescending(x => x.Total).ToList();
        if (myEarnings.Count == 0)
        {
            staffCard.AppendChild(CandyUI.Muted("ow-rev-staff-empty", "No earnings logged."));
        }
        else
        {
            for (int i = 0; i < myEarnings.Count; i++)
            {
                var (role, total) = myEarnings[i];
                staffCard.AppendChild(CandyUI.Label($"ow-staff-{i}", $"{role}: {total:N0} Gil", 12));
            }
        }
        col.AppendChild(staffCard);
        return col;
    }

    private Node BuildOwTabPatrons()
    {
        var col = CandyUI.Column("ow-patrons", 6);
        var patrons = _plugin.Configuration.Patrons;

        col.AppendChild(CandyUI.SectionHeader("ow-pat-analytics-hdr", "Patron Analytics"));
        var analyticsCard = CandyUI.Card("ow-pat-analytics-card");
        var topVisitors = patrons.Where(p => p.VisitCount > 0)
            .OrderByDescending(p => p.VisitCount).Take(5).ToList();
        if (topVisitors.Count > 0)
        {
            analyticsCard.AppendChild(CandyUI.Muted("ow-pat-visits-hdr", "Most Visits:", 11));
            for (int i = 0; i < topVisitors.Count; i++)
            {
                var p = topVisitors[i];
                analyticsCard.AppendChild(CandyUI.Label($"ow-pat-visit-{i}",
                    $"{p.Name}: {p.VisitCount} visits", 12));
            }
        }
        var topSpenders = patrons.Where(p => p.TotalGilSpent > 0)
            .OrderByDescending(p => p.TotalGilSpent).Take(5).ToList();
        if (topSpenders.Count > 0)
        {
            analyticsCard.AppendChild(CandyUI.Muted("ow-pat-spend-hdr", "Top Spenders:", 11));
            for (int i = 0; i < topSpenders.Count; i++)
            {
                var p = topSpenders[i];
                analyticsCard.AppendChild(CandyUI.Label($"ow-pat-spend-{i}",
                    $"{p.Name}: {p.TotalGilSpent:N0} Gil", 12));
            }
        }
        if (topVisitors.Count == 0 && topSpenders.Count == 0)
            analyticsCard.AppendChild(CandyUI.Muted("ow-pat-empty", "No analytics data yet."));
        col.AppendChild(analyticsCard);

        col.AppendChild(CandyUI.Separator("ow-pat-sep1"));
        col.AppendChild(CandyUI.SectionHeader("ow-pat-notes-hdr", "All Patron Notes"));
        col.AppendChild(CandyUI.Muted("ow-pat-notes-hint", "Owner sees notes from all roles.", 11));

        var notes = _plugin.Configuration.PatronNotes
            .OrderByDescending(n => n.Timestamp).Take(20).ToList();
        if (notes.Count == 0)
        {
            col.AppendChild(CandyUI.Muted("ow-pat-notes-empty", "No patron notes yet."));
        }
        else
        {
            var notesCard = CandyUI.Card("ow-pat-notes-card");
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                notesCard.AppendChild(CandyUI.Label($"ow-pat-note-{i}",
                    $"[{n.Timestamp:MM/dd HH:mm}] [{n.AuthorRole}] {n.PatronName}: {n.Content}", 11));
            }
            col.AppendChild(notesCard);
        }
        return col;
    }

    private Node BuildOwTabManage()
    {
        var col = CandyUI.Column("ow-manage", 6);
        col.AppendChild(CandyUI.SectionHeader("ow-bl-hdr", "Blacklist Management"));
        col.AppendChild(CandyUI.InputSpacer("ow-bl-sp", 0, 28));

        var blacklisted = _plugin.Configuration.Patrons
            .Where(p => p.Status == PatronStatus.Blacklisted).ToList();
        if (blacklisted.Count == 0)
        {
            col.AppendChild(CandyUI.Muted("ow-bl-empty", "No blacklisted patrons."));
        }
        else
        {
            var blCard = CandyUI.Card("ow-bl-card");
            for (int i = 0; i < blacklisted.Count; i++)
            {
                var p  = blacklisted[i];
                var pi = i;
                blCard.AppendChild(CandyUI.Row($"ow-bl-row-{pi}", 6,
                    CandyUI.Label($"ow-bl-name-{pi}",
                        string.IsNullOrEmpty(p.BlacklistReason)
                            ? p.Name
                            : $"{p.Name} — {p.BlacklistReason}", 12),
                    CandyUI.SmallButton($"ow-bl-unban-{pi}", "Unban", () =>
                    {
                        p.Status             = PatronStatus.Neutral;
                        p.BlacklistReason    = string.Empty;
                        p.BlacklistDate      = null;
                        p.BlacklistFlaggedBy = string.Empty;
                        _plugin.Configuration.Save();
                    })
                ));
            }
            col.AppendChild(blCard);
        }

        col.AppendChild(CandyUI.Separator("ow-manage-sep1"));
        col.AppendChild(CandyUI.Button("ow-export-btn", "Copy Earnings Summary to Clipboard", () =>
        {
            var cfg   = _plugin.Configuration;
            var lines = new System.Collections.Generic.List<string>
            {
                $"=== {cfg.VenueName} Earnings Report ===",
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}",
                ""
            };
            var tDay    = DateTime.Now.ToString("yyyy-MM-dd");
            var tLegacy = cfg.DailyEarnings.TryGetValue(tDay, out var tdv) ? tdv : 0;
            var tEntr   = cfg.Earnings.Where(e => e.Timestamp.Date == DateTime.Today).Sum(e => e.Amount);
            lines.Add($"Today: {tLegacy + tEntr:N0} Gil");
            lines.Add($"All-Time: {cfg.DailyEarnings.Values.Sum() + cfg.Earnings.Sum(e => e.Amount):N0} Gil");
            lines.Add("");
            foreach (StaffRole role in Enum.GetValues<StaffRole>())
            {
                if (role == StaffRole.None) continue;
                var rt = cfg.Earnings.Where(e => e.Role == role).Sum(e => e.Amount);
                if (rt != 0) lines.Add($"  {role}: {rt:N0} Gil");
            }
            ImGui.SetClipboardText(string.Join("\n", lines));
            Svc.Chat.Print("[Candy Coat] Earnings summary copied to clipboard!");
        }));
        return col;
    }

    private Node BuildOwTabPing()
    {
        var col = CandyUI.Column("ow-ping-tab", 6);
        col.AppendChild(CandyUI.Muted("ow-ping-note", "Staff ping widget below."));
        return col;
    }

    public Node BuildSettingsNode()
    {
        var root    = UdtHelper.CreateFromTemplate("srt-owner.xml", "owner-settings-layout");
        var dynamic = root.QuerySelector("#srt-owner-settings-dynamic")!;
        var col = CandyUI.Column("ow-settings", 8);
        col.AppendChild(CandyUI.SectionHeader("ow-settings-hdr", "Owner Settings"));
        col.AppendChild(CandyUI.Muted("ow-settings-desc", "Venue configuration, rooms, menu, and role cosmetics."));
        col.AppendChild(CandyUI.Separator("ow-settings-sep1"));

        var cfg = _plugin.Configuration;

        // Venue Registration card
        var regCard = CandyUI.Card("ow-settings-reg-card");
        regCard.AppendChild(CandyUI.SectionHeader("ow-settings-reg-hdr", "Venue Registration"));
        regCard.AppendChild(CandyUI.Label("ow-settings-vname",
            $"Name: {(string.IsNullOrEmpty(cfg.VenueName) ? "(not set)" : cfg.VenueName)}", 12));
        regCard.AppendChild(CandyUI.Label("ow-settings-vid",
            $"ID:   {(string.IsNullOrEmpty(cfg.VenueId) ? "(not set)" : cfg.VenueId)}", 12));
        regCard.AppendChild(CandyUI.Label("ow-settings-vkey",
            $"Key:  {(string.IsNullOrEmpty(cfg.VenueKey) ? "(not set)" : new string('\u2022', Math.Min(cfg.VenueKey.Length, 22)))}", 12));
        regCard.AppendChild(CandyUI.InputSpacer("ow-settings-reg-sp", 0, 28));
        regCard.AppendChild(CandyUI.Muted("ow-settings-reg-hint",
            "Share the Key with staff — they enter it in the setup wizard.", 11));
        col.AppendChild(regCard);

        col.AppendChild(CandyUI.Separator("ow-settings-sep2"));

        // Venue Info (editable name) card
        var venueCard = CandyUI.Card("ow-settings-venue-card");
        venueCard.AppendChild(CandyUI.SectionHeader("ow-settings-venue-hdr", "Venue Info"));
        venueCard.AppendChild(CandyUI.InputSpacer("ow-settings-vname-sp", 0, 28));
        col.AppendChild(venueCard);

        col.AppendChild(CandyUI.Separator("ow-settings-sep3"));

        // Room Editor card
        var roomCard = CandyUI.Card("ow-settings-rooms-card");
        roomCard.AppendChild(CandyUI.SectionHeader("ow-settings-rooms-hdr", "Room Editor"));
        roomCard.AppendChild(CandyUI.InputSpacer("ow-settings-addroom-sp", 0, 28));
        var rooms = cfg.Rooms;
        if (rooms.Count == 0)
        {
            roomCard.AppendChild(CandyUI.Muted("ow-settings-norooms", "No rooms yet."));
        }
        else
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                int ci   = i;
                roomCard.AppendChild(CandyUI.Row($"ow-room-row-{ci}", 6,
                    CandyUI.Label($"ow-room-name-{ci}",
                        $"• {room.Name}: {room.Status}", 12),
                    CandyUI.SmallButton($"ow-room-del-{ci}", "Remove", () =>
                    {
                        cfg.Rooms.RemoveAt(ci);
                        cfg.Save();
                    })
                ));
            }
        }
        col.AppendChild(roomCard);

        col.AppendChild(CandyUI.Separator("ow-settings-sep4"));

        // Service Menu Editor card
        var menuCard = CandyUI.Card("ow-settings-menu-card");
        menuCard.AppendChild(CandyUI.SectionHeader("ow-settings-menu-hdr", "Service Menu Editor"));
        menuCard.AppendChild(CandyUI.InputSpacer("ow-settings-additem-sp", 0, 28));
        var menu = cfg.ServiceMenu;
        if (menu.Count == 0)
        {
            menuCard.AppendChild(CandyUI.Muted("ow-settings-nomenu", "No items in menu."));
        }
        else
        {
            for (int i = 0; i < menu.Count; i++)
            {
                var item = menu[i];
                int ci   = i;
                menuCard.AppendChild(CandyUI.Row($"ow-menu-row-{ci}", 6,
                    CandyUI.Label($"ow-menu-item-{ci}",
                        $"[{item.Category}] {item.Name} — {item.Price:N0} Gil", 12),
                    CandyUI.SmallButton($"ow-menu-del-{ci}", "Del", () =>
                    {
                        cfg.ServiceMenu.RemoveAt(ci);
                        cfg.Save();
                    })
                ));
            }
        }
        col.AppendChild(menuCard);

        col.AppendChild(CandyUI.Separator("ow-settings-sep5"));

        // Loyalty Tier Thresholds card
        var tierCard = CandyUI.Card("ow-settings-tier-card");
        tierCard.AppendChild(CandyUI.SectionHeader("ow-settings-tier-hdr", "Patron Loyalty Tier Thresholds"));
        tierCard.AppendChild(CandyUI.InputSpacer("ow-settings-tier-sp", 0, 84));
        col.AppendChild(tierCard);

        col.AppendChild(CandyUI.Separator("ow-settings-sep6"));

        // Role Cosmetic Defaults card
        var cosCard = CandyUI.Card("ow-settings-cos-card");
        cosCard.AppendChild(CandyUI.SectionHeader("ow-settings-cos-hdr", "Role Cosmetic Defaults"));
        var roleCount = Enum.GetValues<StaffRole>().Count(r => r != StaffRole.None);
        cosCard.AppendChild(CandyUI.InputSpacer("ow-settings-cos-sp", 0, roleCount * 30 + 20));
        col.AppendChild(cosCard);
        dynamic.AppendChild(col);
        return root;
    }

    public void DrawOverlays()
    {
        // Blacklist patron inputs
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##OwnerBLP", "Patron Name", ref _blPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##OwnerBLR", "Reason", ref _blReason, 200);
        ImGui.SameLine();
        if (ImGui.Button("Blacklist##Owner"))
        {
            if (!string.IsNullOrWhiteSpace(_blPatron))
            {
                var patron = _plugin.Configuration.Patrons.FirstOrDefault(p => p.Name == _blPatron);
                if (patron == null)
                {
                    patron = new Patron { Name = _blPatron };
                    _plugin.Configuration.Patrons.Add(patron);
                }
                patron.Status             = PatronStatus.Blacklisted;
                patron.BlacklistReason    = _blReason;
                patron.BlacklistDate      = DateTime.Now;
                patron.BlacklistFlaggedBy = _plugin.Configuration.CharacterName;
                _plugin.Configuration.Save();
                _blPatron = string.Empty;
                _blReason = string.Empty;
            }
        }
    }

    public void DrawSettingsOverlays()
    {
        var cfg = _plugin.Configuration;

        // Venue name edit
        if (!_venueNameInit) { _venueNameInput = cfg.VenueName; _venueNameInit = true; }
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("Venue Name##Owner", ref _venueNameInput, 100))
        {
            cfg.VenueName = _venueNameInput;
            cfg.Save();
        }

        // Add room form
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##OwnerNewRoom", "Room Name", ref _newRoomName, 100);
        ImGui.SameLine();
        if (ImGui.Button("+ Add Room##Owner"))
        {
            if (!string.IsNullOrWhiteSpace(_newRoomName))
            {
                cfg.Rooms.Add(new VenueRoom { Name = _newRoomName });
                cfg.Save();
                _newRoomName = string.Empty;
            }
        }

        // Add menu item form
        ImGui.SetNextItemWidth(110);
        ImGui.InputTextWithHint("##OwnerItemN", "Name", ref _newItemName, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(130);
        ImGui.InputTextWithHint("##OwnerItemD", "Description", ref _newItemDesc, 200);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        ImGui.InputInt("##OwnerItemP", ref _newItemPrice, 1000);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.Combo("##OwnerItemC", ref _newItemCategory, CategoryLabels, CategoryLabels.Length);
        ImGui.SameLine();
        if (ImGui.Button("+ Add##Owner"))
        {
            if (!string.IsNullOrWhiteSpace(_newItemName))
            {
                cfg.ServiceMenu.Add(new ServiceMenuItem
                {
                    Name        = _newItemName,
                    Description = _newItemDesc,
                    Price       = _newItemPrice,
                    Category    = (ServiceCategory)_newItemCategory
                });
                cfg.Save();
                _newItemName  = string.Empty;
                _newItemDesc  = string.Empty;
                _newItemPrice = 0;
            }
        }

        // Loyalty tier thresholds
        bool dirty = false;
        ImGui.TextColored(new Vector4(1f, 0.5f, 0.8f, 1f), "\u2665 Regular");
        ImGui.SameLine();
        ImGui.TextDisabled("(OR condition)");
        ImGui.SetNextItemWidth(80);
        int rv = cfg.RegularTierVisits;
        if (ImGui.InputInt("Visits##RegV", ref rv, 1) && rv >= 1) { cfg.RegularTierVisits = rv; dirty = true; }
        ImGui.SameLine();
        int rg = cfg.RegularTierGil;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputInt("Gil##RegG", ref rg, 10000) && rg >= 0) { cfg.RegularTierGil = rg; dirty = true; }
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), "\u2605 Elite");
        ImGui.SameLine();
        ImGui.TextDisabled("(OR condition)");
        ImGui.SetNextItemWidth(80);
        int ev = cfg.EliteTierVisits;
        if (ImGui.InputInt("Visits##ElV", ref ev, 1) && ev >= 1) { cfg.EliteTierVisits = ev; dirty = true; }
        ImGui.SameLine();
        int eg = cfg.EliteTierGil;
        ImGui.SetNextItemWidth(110);
        if (ImGui.InputInt("Gil##ElG", ref eg, 100000) && eg >= 0) { cfg.EliteTierGil = eg; dirty = true; }
        if (dirty) cfg.Save();

        // Role cosmetic defaults
        bool cosDirty = false;
        foreach (StaffRole role in Enum.GetValues<StaffRole>())
        {
            if (role == StaffRole.None) continue;
            if (!cfg.RoleDefaults.TryGetValue(role, out var rd))
            {
                rd = new RoleDefaultCosmetic();
                cfg.RoleDefaults[role] = rd;
                cosDirty = true;
            }
            ImGui.PushID($"OwnerRD_{role}");
            bool enabled = rd.Enabled;
            if (ImGui.Checkbox($"##{role}_en", ref enabled)) { rd.Enabled = enabled; cosDirty = true; }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), role.ToString());
            ImGui.SameLine();
            int badgeIdx = System.Array.IndexOf(CosmeticRenderer.BadgeTemplates, rd.BadgeTemplate);
            if (badgeIdx < 0) badgeIdx = 1;
            ImGui.SetNextItemWidth(90);
            if (ImGui.Combo($"##badge_{role}", ref badgeIdx, CosmeticRenderer.BadgeTemplates, CosmeticRenderer.BadgeTemplates.Length))
            {
                rd.BadgeTemplate = CosmeticRenderer.BadgeTemplates[badgeIdx];
                cosDirty = true;
            }
            ImGui.SameLine();
            var glowColor = rd.GlowColor;
            if (ImGui.ColorEdit4($"Glow##{role}", ref glowColor,
                ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaBar))
            {
                rd.GlowColor = glowColor;
                cosDirty = true;
            }
            ImGui.PopID();
        }
        if (cosDirty) cfg.Save();
    }
}
