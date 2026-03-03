using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.UI;
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

    private static readonly Vector4 CardBg     = new(0.16f, 0.12f, 0.20f, 1f);
    private static readonly Vector4 HeaderBg   = new(0.22f, 0.16f, 0.28f, 1f);
    private static readonly Vector4 HeaderHover = new(0.30f, 0.22f, 0.36f, 1f);

    public OwnerPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features (Operational / Analytics) ──────────────────────────────────

    public void DrawContent()
    {
        ImGui.PushStyleColor(ImGuiCol.Header, HeaderBg);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, HeaderHover);

        if (ImGui.CollapsingHeader("Revenue Dashboard##Owner", ImGuiTreeNodeFlags.DefaultOpen))
            DrawRevenueDashboard();

        if (ImGui.CollapsingHeader("Earnings by Role##Owner", ImGuiTreeNodeFlags.DefaultOpen))
            DrawStaffLeaderboard();

        if (ImGui.CollapsingHeader("Patron Analytics##Owner"))
            DrawPatronAnalytics();

        if (ImGui.CollapsingHeader("All Patron Notes##Owner"))
            DrawPatronNotes();

        if (ImGui.CollapsingHeader("Blacklist Management##Owner"))
            DrawBlacklistManagement();

        if (ImGui.CollapsingHeader("Export##Owner"))
            DrawExport();

        if (ImGui.CollapsingHeader("Staff Ping##Owner"))
            _pingWidget.Draw();

        ImGui.PopStyleColor(2);
    }

    // ─── Settings (Configuration / Admin) ────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "\ud83d\udc51 Owner Settings");
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
                ImGui.TextColored(StyleManager.SectionHeader, "Venue Registration");
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
                    if (ImGui.SmallButton("\ud83d\udccb##OwnerCopyVenueId"))
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
                    if (ImGui.SmallButton(_venueKeyRevealed ? "\ud83d\udc41##OwnerKeyHide" : "\ud83d\udc41##OwnerKeyShow"))
                        _venueKeyRevealed = !_venueKeyRevealed;
                    ImGui.SameLine();
                    if (ImGui.SmallButton("\ud83d\udccb##OwnerCopyVenueKey"))
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
                ImGui.TextColored(StyleManager.SectionHeader, "Venue Info");
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
                ImGui.TextColored(StyleManager.SectionHeader, "Room Editor");
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
                    var color = room.Status switch { RoomStatus.Available => StyleManager.SyncOk, RoomStatus.Occupied => new Vector4(1f, 0.4f, 0.4f, 1f), RoomStatus.Reserved => new Vector4(1f, 0.8f, 0.2f, 1f), _ => new Vector4(0.5f, 0.5f, 0.5f, 1f) };
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
                ImGui.TextColored(StyleManager.SectionHeader, "Service Menu Editor");
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
                ImGui.TextColored(StyleManager.SectionHeader, "💎 VIP Packages");
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
                    ImGui.TextColored(StyleManager.SyncOk, "Active");
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
                ImGui.TextColored(StyleManager.SectionHeader, "Edit Package");
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
        ImGui.TextColored(StyleManager.SectionHeader, "+ Add Package");
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
        if (_plugin.SyncService.IsConnected) ImGui.TextColored(StyleManager.SyncOk, "\ud83d\udfe2 Showing synced data."); else ImGui.TextColored(StyleManager.SyncWarn, "\u26a0 Local data only.");
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
        ImGui.TextColored(StyleManager.SectionHeader, "Patron Loyalty Tier Thresholds");
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
        ImGui.TextColored(StyleManager.SectionHeader, "Role Cosmetic Defaults");
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
            ImGui.TextColored(StyleManager.SectionHeader, role.ToString());
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
}
