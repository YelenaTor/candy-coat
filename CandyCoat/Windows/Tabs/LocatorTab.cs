using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using CandyCoat.Data;
using CandyCoat.Services;

namespace CandyCoat.Windows.Tabs;

public class LocatorTab : ITab
{
    private readonly Plugin _plugin;
    private readonly VenueService _venueService;
    private string newPatronName = string.Empty;

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

        ImGui.TextUnformatted("Patron Locator");
        ImGui.Spacing();

        ImGui.InputText("Name to Track", ref newPatronName, 100);
        ImGui.SameLine();
        if (ImGui.Button("Track"))
        {
            if (!string.IsNullOrWhiteSpace(newPatronName))
            {
                _venueService.TrackPatron(newPatronName);
                newPatronName = string.Empty;
            }
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Nearby Tracked Patrons:");
        
        // Read cached state from LocatorService instead of scanning every frame
        var nearbyPlayers = _plugin.LocatorService.NearbyFavorites;
        bool foundAny = nearbyPlayers.Count > 0;

        foreach (var (patron, distance) in nearbyPlayers)
        {
            Vector4 color = patron.Status switch
            {
                PatronStatus.Favorite => new Vector4(1f, 0.5f, 0.8f, 1f),
                PatronStatus.Warning => new Vector4(1f, 0.8f, 0.2f, 1f),
                PatronStatus.Blacklisted => new Vector4(1f, 0.2f, 0.2f, 1f),
                _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)
            };
            
            string icon = patron.Status switch
            {
                PatronStatus.Favorite => "♥",
                PatronStatus.Warning => "⚠",
                PatronStatus.Blacklisted => "🚫",
                _ => "•"
            };

            ImGui.TextColored(color, $"{icon} {patron.Name} is here! ({distance:F1}m away)");
        }

        if (!foundAny)
        {
            ImGui.TextDisabled("No tracked patrons nearby.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Tracked Patrons:");
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
