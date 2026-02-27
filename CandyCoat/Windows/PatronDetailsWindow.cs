using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using CandyCoat.Data;
using CandyCoat.IPC;

namespace CandyCoat.Windows;

public class PatronDetailsWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly GlamourerIpc _glamourer;
    public Patron? SelectedPatron { get; set; }

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

        CandyCoat.UI.StyleManager.PushStyles();
        try
        {
            using var subTabBar = ImRaii.TabBar("PatronDetailsTabs");
            if (!subTabBar) return;

            DrawInfoTab(SelectedPatron);
            DrawGlamourTab(SelectedPatron);
        }
        finally
        {
            CandyCoat.UI.StyleManager.PopStyles();
        }
    }

    private void DrawInfoTab(Patron patron)
    {
        using var infoTab = ImRaii.TabItem("CRM Info");
        if (!infoTab) return;

        // Core details
        ImGui.Text($"Name: {patron.Name}");
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
