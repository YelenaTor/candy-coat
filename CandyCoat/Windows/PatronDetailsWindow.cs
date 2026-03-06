using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using CandyCoat.Data;
using CandyCoat.IPC;
using CandyCoat.UI;

namespace CandyCoat.Windows;

public class PatronDetailsWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly GlamourerIpc _glamourer;
    public Patron? SelectedPatron { get; set; }

    // VIP tab state
    private int _vipDropdownIdx = 0;
    private int _vipOverridePrice = 0;

    public PatronDetailsWindow(Plugin plugin, GlamourerIpc glamourer)
        : base("Patron Details###PatronDetailsWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        _plugin = plugin;
        _glamourer = glamourer;
        IsOpen = false;
    }

    public override void Draw()
    {
        if (SelectedPatron == null) return;

        using var subTabBar = ImRaii.TabBar("PatronDetailsTabs");
        if (!subTabBar) return;

        DrawInfoTab(SelectedPatron);
        DrawGlamourTab(SelectedPatron);
        DrawVipTab(SelectedPatron);
    }

    private void DrawInfoTab(Patron patron)
    {
        using var infoTab = ImRaii.TabItem("CRM Info");
        if (!infoTab) return;

        // Core details
        ImGui.Text($"Name: {patron.Name}");
        var tier = _plugin.Configuration.GetTier(patron);
        var tierColor = tier switch
        {
            Data.PatronTier.Elite   => new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f),
            Data.PatronTier.Regular => new System.Numerics.Vector4(1f, 0.5f, 0.8f, 1f),
            _                       => new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f),
        };
        ImGui.SameLine();
        ImGui.TextColored(tierColor, $"[{tier}]");
        ImGui.Text($"Last Visit: {patron.LastVisitDate:d} at {patron.LastVisitDate:t}");

        if (_plugin.Configuration.IsManagementModeEnabled)
        {
            var statusStrs = Enum.GetNames<PatronStatus>();
            int currentStatus = (int)patron.Status;
            if (ImGui.Combo("Status", ref currentStatus, statusStrs, statusStrs.Length))
            {
                patron.Status = (PatronStatus)currentStatus;
                _plugin.Configuration.Save();
            }
        }
        else
        {
            bool isReg = patron.Status == PatronStatus.Regular;
            if (ImGui.Checkbox("Is Regular VIP", ref isReg))
            {
                patron.Status = isReg ? PatronStatus.Regular : PatronStatus.Neutral;
                _plugin.Configuration.Save();
            }
        }
        ImGui.Separator();

        // New CRM Fields
        var drink = patron.FavoriteDrink;
        if (ImGui.InputText("Favorite Drink", ref drink, 100))
        {
            patron.FavoriteDrink = drink;
            _plugin.Configuration.Save();
        }

        var allergies = patron.Allergies;
        if (ImGui.InputText("Allergies", ref allergies, 100))
        {
            patron.Allergies = allergies;
            _plugin.Configuration.Save();
        }

        ImGui.Spacing();

        // Multi-line Notes
        var notes = patron.Notes;
        if (ImGui.InputTextMultiline("Notes", ref notes, 2000, new Vector2(-1, 80)))
        {
            patron.Notes = notes;
            _plugin.Configuration.Save();
        }

        var hooks = patron.RpHooks;
        if (ImGui.InputTextMultiline("RP Hooks", ref hooks, 2000, new Vector2(-1, 80)))
        {
            patron.RpHooks = hooks;
            _plugin.Configuration.Save();
        }

        if (ImGui.Button("Scrape Open Search Info"))
        {
            var text = ScrapeSearchInfo();
            if (!string.IsNullOrEmpty(text))
            {
                patron.RpHooks = text;
                _plugin.Configuration.Save();
            }
            else
            {
                ECommons.DalamudServices.Svc.Log.Warning("Could not scrape search info. Is the Examine window open?");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Automated Actions");

        if (ImGui.Button("Send Default Welcome"))
        {
            ECommons.DalamudServices.Svc.Commands.ProcessCommand($"/t {patron.Name} Welcome to Candy Coat! Your VIP room is ready.");
        }

        // Custom macros
        foreach (var macro in _plugin.Configuration.Macros)
        {
            if (ImGui.Button($"Send: {macro.Title}"))
            {
                var cleanName = patron.Name.Split(' ')[0]; // use First Name
                var resolvedText = macro.Text.Replace("{name}", cleanName);
                ECommons.DalamudServices.Svc.Commands.ProcessCommand($"/t {patron.Name} {resolvedText}");
            }
        }
    }

    private void DrawGlamourTab(Patron patron)
    {
        using var glamTab = ImRaii.TabItem("Glamour Links");
        if (!glamTab) return;

        var allDesigns = _glamourer.GetDesignList();

        ImGui.Text("Assigned Outfits (Quick Swap)");
        foreach (var designId in patron.QuickSwitchDesignIds.ToArray())
        {
            var name = allDesigns.TryGetValue(designId, out var designName) ? designName : designId.ToString();

            if (ImGui.Button($"Apply: {name}"))
            {
                _glamourer.ApplyDesign(designId);
            }
            ImGui.SameLine();
            if (ImGui.Button($"Unlink##{designId}"))
            {
                patron.QuickSwitchDesignIds.Remove(designId);
                _plugin.Configuration.Save();
            }
        }

        ImGui.Separator();
        ImGui.Text("All Designs");

        using var designList = ImRaii.Child("DesignList", new Vector2(0, 200), true);
        foreach (var kvp in allDesigns)
        {
            if (ImGui.Selectable(kvp.Value))
            {
                if (!patron.QuickSwitchDesignIds.Contains(kvp.Key))
                {
                    patron.QuickSwitchDesignIds.Add(kvp.Key);
                    _plugin.Configuration.Save();
                }
            }
        }
    }

    // ─── VIP Tab ──────────────────────────────────────────────────────────────

    private void DrawVipTab(Patron patron)
    {
        using var vipTab = ImRaii.TabItem("💎 VIP");
        if (!vipTab) return;

        ImGui.Spacing();

        if (patron.ActiveVip == null)
            DrawVipAssignForm(patron);
        else if (!patron.ActiveVip.IsExpired)
            DrawVipActiveView(patron);
        else
            DrawVipExpiredView(patron);
    }

    private void DrawVipAssignForm(Patron patron)
    {
        ImGui.TextDisabled("No VIP Package Assigned");
        ImGui.Spacing();

        var activePackages = _plugin.Configuration.VipPackages
            .Where(p => p.IsActive)
            .ToList();

        if (activePackages.Count == 0)
        {
            ImGui.TextDisabled("No VIP packages configured. Ask the Owner to set up packages.");
            return;
        }

        var names = activePackages.Select(p => p.Name).ToArray();
        if (_vipDropdownIdx >= names.Length) _vipDropdownIdx = 0;

        ImGui.SetNextItemWidth(200f);
        ImGui.Combo("Package##VipAssign", ref _vipDropdownIdx, names, names.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110f);
        ImGui.InputInt("Override Price##VipAssign", ref _vipOverridePrice, 10000);
        ImGui.SameLine();

        if (ImGui.Button("Assign##VipAssign"))
        {
            var pkg = activePackages[_vipDropdownIdx];
            var sub = new VipSubscription
            {
                PackageId    = pkg.Id,
                PackageName  = pkg.Name,
                Tier         = pkg.Tier,
                DurationType = pkg.DurationType,
                PurchasedAt  = DateTime.Now,
                ExpiresAt    = pkg.DurationType == VipDurationType.Monthly
                                   ? DateTime.Now.AddMonths(1)
                                   : null,
                AssignedBy = _plugin.Configuration.CharacterName,
                PaidGil    = _vipOverridePrice > 0 ? _vipOverridePrice : pkg.PriceGil
            };
            patron.ActiveVip = sub;
            _plugin.Configuration.Save();
            _vipOverridePrice = 0;
        }

        ImGui.TextDisabled("Override Price: leave 0 to use package default");
    }

    private void DrawVipActiveView(Patron patron)
    {
        var vip = patron.ActiveVip!;
        var tierCol = VipColours.GetTierColour(vip.Tier);

        ImGui.TextColored(new Vector4(1f, 0.82f, 0.15f, 1f), "💎");
        ImGui.SameLine();
        ImGui.TextColored(tierCol, vip.PackageName);
        ImGui.SameLine();
        ImGui.TextDisabled($"[VIP {vip.Tier.ToString().ToUpperInvariant()}]");

        ImGui.Spacing();

        ImGui.Text($"Purchased:  {vip.PurchasedAt:yyyy-MM-dd}");
        ImGui.SameLine(0, 20f);
        ImGui.Text($"Assigned by: {vip.AssignedBy}");

        if (vip.ExpiresAt.HasValue)
        {
            var daysLeft = vip.DaysRemaining;
            ImGui.Text($"Expires:    {vip.ExpiresAt.Value:yyyy-MM-dd}");
            ImGui.SameLine(0, 20f);
            var expiryCol = daysLeft <= 7
                ? new Vector4(1f, 0.6f, 0.2f, 1f)
                : new Vector4(0.6f, 0.9f, 0.6f, 1f);
            ImGui.TextColored(expiryCol, $"({daysLeft} day{(daysLeft != 1 ? "s" : "")} remaining)");
        }
        else
        {
            ImGui.Text("Expires:    Never");
            ImGui.SameLine(0, 20f);
            ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1f), "(Permanent)");
        }

        ImGui.Text($"Paid:       {vip.PaidGil:N0} gil");

        // Perks from the package definition (if still exists)
        var pkg = _plugin.Configuration.VipPackages.FirstOrDefault(p => p.Id == vip.PackageId);
        if (pkg != null && pkg.Perks.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Perks:");
            foreach (var perk in pkg.Perks)
                ImGui.BulletText(perk);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (vip.DurationType == VipDurationType.Monthly)
        {
            if (ImGui.Button("Renew for 1 month##VipRenew"))
            {
                vip.ExpiresAt = DateTime.Now.AddMonths(1);
                _plugin.Configuration.Save();
            }
            ImGui.SameLine();
        }

        if (ImGui.Button("Remove VIP##VipRemove"))
        {
            patron.ActiveVip = null;
            _plugin.Configuration.Save();
        }
    }

    private void DrawVipExpiredView(Patron patron)
    {
        var vip = patron.ActiveVip!;

        ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f), "⚠ VIP EXPIRED");
        ImGui.SameLine();
        ImGui.TextDisabled($"— {vip.PackageName}");

        ImGui.Spacing();

        if (vip.ExpiresAt.HasValue)
        {
            var daysAgo = (int)(DateTime.Now - vip.ExpiresAt.Value).TotalDays;
            ImGui.Text($"Expired:  {vip.ExpiresAt.Value:yyyy-MM-dd}");
            ImGui.SameLine(0, 20f);
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f),
                $"({daysAgo} day{(daysAgo != 1 ? "s" : "")} ago)");
        }

        ImGui.Text($"Paid:     {vip.PaidGil:N0} gil");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (vip.DurationType == VipDurationType.Monthly)
        {
            if (ImGui.Button("Renew for 1 month##VipExpRenew"))
            {
                vip.ExpiresAt = DateTime.Now.AddMonths(1);
                _plugin.Configuration.Save();
            }
            ImGui.SameLine();
        }

        if (ImGui.Button("Remove VIP##VipExpRemove"))
        {
            patron.ActiveVip = null;
            _plugin.Configuration.Save();
        }
    }

    // ─── Misc ─────────────────────────────────────────────────────────────────

    public void OpenForPatron(Patron patron)
    {
        SelectedPatron = patron;
        IsOpen = true;
    }

    public void Dispose()
    {
        // Nothing disposable currently
    }

    private unsafe string? ScrapeSearchInfo()
    {
        try
        {
            var addonPtr = ECommons.DalamudServices.Svc.GameGui.GetAddonByName("CharacterInspect", 1);
            var addon = (AtkUnitBase*)addonPtr.Address;
            if (addon == null || !addon->IsVisible) return null;

            string longestText = "";
            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node == null || node->Type != NodeType.Text) continue;
                var textNode = (AtkTextNode*)node;
                var rawText = textNode->NodeText.ToString();
                if (rawText.Length > longestText.Length)
                    longestText = rawText;
            }
            return string.IsNullOrWhiteSpace(longestText) ? null : longestText;
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Warning($"[PatronDetailsWindow] ScrapeSearchInfo failed: {ex.Message}");
            return null;
        }
    }
}
