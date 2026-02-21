using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using CandyCoat.Data;
using CandyCoat.Services;

namespace CandyCoat.Windows.Tabs;

public class LocatorTab : ITab
{
    private readonly Plugin _plugin;
    private readonly VenueService _venueService;
    private string newPatronFirstName = string.Empty;
    private string newPatronLastName = string.Empty;
    private string newPatronWorld = string.Empty;

    public Action<Patron?>? OnPatronSelected { get; set; }
    public Patron? SelectedPatron { get; set; }

    public string Name => "Locator";

    public LocatorTab(Plugin plugin, VenueService venueService)
    {
        _plugin = plugin;
        _venueService = venueService;
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(Name);
        if (!tab) return;

        ImGui.TextUnformatted("Add New Regular / Track Patron");
        
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##fname", "First Name", ref newPatronFirstName, 50);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##lname", "Last Name", ref newPatronLastName, 50);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##world", "World", ref newPatronWorld, 50);
        
        ImGui.SameLine();
        if (ImGui.Button("Track"))
        {
            var fullName = $"{newPatronFirstName} {newPatronLastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var p = _venueService.EnsurePatronExists(fullName);
                p.Status = PatronStatus.Regular;
                if (!string.IsNullOrWhiteSpace(newPatronWorld)) p.World = newPatronWorld;
                _plugin.Configuration.Save();
                
                newPatronFirstName = string.Empty;
                newPatronLastName = string.Empty;
                newPatronWorld = string.Empty;
            }
        }

        if (ImGui.Button("Detect Targeted"))
        {
            var target = Svc.Targets.Target;
            if (target != null && target.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
            {
                var nameParts = target.Name.ToString().Split(' ', 2);
                newPatronFirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
                newPatronLastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;
                // Get World - Cast to IPlayerCharacter for HomeWorld in API 14
                if (target is IPlayerCharacter pc && pc.HomeWorld.IsValid)
                {
                    newPatronWorld = pc.HomeWorld.Value.Name.ToString();
                }
                else
                {
                    newPatronWorld = Svc.PlayerState.HomeWorld.Value.Name.ToString();
                }
            }
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Nearby Regulars & Tracked:");
        
        // Read cached state from LocatorService instead of scanning every frame
        var nearbyPlayers = _plugin.LocatorService.NearbyRegulars;
        bool foundAny = nearbyPlayers.Count > 0;

        foreach (var (patron, distance) in nearbyPlayers)
        {
            Vector4 color = patron.Status switch
            {
                PatronStatus.Regular => new Vector4(1f, 0.5f, 0.8f, 1f),
                PatronStatus.Warning => new Vector4(1f, 0.8f, 0.2f, 1f),
                PatronStatus.Blacklisted => new Vector4(1f, 0.2f, 0.2f, 1f),
                _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)
            };
            
            string icon = patron.Status switch
            {
                PatronStatus.Regular => "♥",
                PatronStatus.Warning => "⚠",
                PatronStatus.Blacklisted => "🚫",
                _ => "•"
            };

            ImGui.TextColored(color, $"{icon} {patron.Name} is here! ({distance:F1}m away)");
            
            // Add Target Eye button
            ImGui.SameLine();
            if (ImGui.SmallButton($"👁##target{patron.Name}"))
            {
                var obj = Svc.Objects.FirstOrDefault(x => x.Name.ToString() == patron.Name);
                if (obj != null)
                {
                    Svc.Targets.Target = obj;
                }
            }
        }

        if (!foundAny)
        {
            ImGui.TextDisabled("No tracked patrons nearby.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Regulars & Tracked List:");
        foreach (var p in _plugin.Configuration.Patrons)
        {
            if (ImGui.Selectable($"- {p.Name}##{p.Name}", SelectedPatron == p))
            {
                SelectedPatron = p;
                OnPatronSelected?.Invoke(p);
            }

            if (ImGui.BeginPopupContextItem($"PatronContext{p.Name}"))
            {
                if (ImGui.Selectable("Remove"))
                {
                    _venueService.UntrackPatron(p);
                    if (SelectedPatron == p) 
                    {
                        SelectedPatron = null;
                        OnPatronSelected?.Invoke(null);
                    }
                }
                ImGui.EndPopup();
            }
        }
    }
}
