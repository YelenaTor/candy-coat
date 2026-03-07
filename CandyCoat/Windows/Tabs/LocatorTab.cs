using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using CandyCoat.Data;
using CandyCoat.Services;
using CandyCoat.UI;
using Una.Drawing;

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

    private Node? _root;

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
        DrawContent();
    }

    public void DrawContent()
    {
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
            var tier = _plugin.Configuration.GetTier(patron);

            Vector4 color = patron.Status switch
            {
                PatronStatus.Regular    => tier == PatronTier.Elite
                                              ? new Vector4(1f, 0.85f, 0.2f, 1f)   // gold for Elite
                                              : new Vector4(1f, 0.5f, 0.8f, 1f),   // pink for Regular
                PatronStatus.Warning    => new Vector4(1f, 0.8f, 0.2f, 1f),
                PatronStatus.Blacklisted => new Vector4(1f, 0.2f, 0.2f, 1f),
                _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)
            };

            string icon = patron.Status switch
            {
                PatronStatus.Regular    => tier == PatronTier.Elite ? "★" : "♥",
                PatronStatus.Warning    => "⚠",
                PatronStatus.Blacklisted => "🚫",
                _ => "•"
            };

            string tierLabel = patron.Status == PatronStatus.Regular
                ? $" [{tier}]" : string.Empty;

            ImGui.TextColored(color, $"{icon} {patron.Name}{tierLabel} is here! ({distance:F1}m away)");

            if (patron.ActiveVip != null && !patron.ActiveVip.IsExpired)
            {
                ImGui.SameLine(0, 4f);
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "💎");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"VIP: {patron.ActiveVip.PackageName}");
            }

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

        using var patronList = ImRaii.Child("PatronList", new Vector2(0, 180), true);
        foreach (var p in _plugin.Configuration.Patrons)
        {
            var ptier = _plugin.Configuration.GetTier(p);
            var ptierStr = p.Status == PatronStatus.Regular ? $" [{ptier}]" : string.Empty;
            if (ImGui.Selectable($"- {p.Name}{ptierStr}##{p.Name}", SelectedPatron == p))
            {
                SelectedPatron = p;
                OnPatronSelected?.Invoke(p);
            }

            if (p.ActiveVip != null && !p.ActiveVip.IsExpired)
            {
                ImGui.SameLine(0, 4f);
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "💎");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"VIP: {p.ActiveVip.PackageName}");
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

    public Node BuildNode()
    {
        var root    = UdtHelper.CreateFromTemplate("locator-tab.xml", "locator-layout");
        var dynamic = root.QuerySelector("#locator-dynamic")!;

        // Track-patron input row — live inputs in DrawOverlays()
        var addCard = CandyUI.Card("locator-add-card");
        addCard.AppendChild(CandyUI.Label("locator-add-title", "Track a Patron", 13));

        var inputRow = CandyUI.Row("locator-input-row", 8);
        inputRow.AppendChild(CandyUI.InputSpacer("locator-fname",  120));
        inputRow.AppendChild(CandyUI.InputSpacer("locator-lname",  120));
        inputRow.AppendChild(CandyUI.InputSpacer("locator-world",  120));
        inputRow.AppendChild(CandyUI.InputSpacer("locator-track-btn", 60));
        addCard.AppendChild(inputRow);

        addCard.AppendChild(CandyUI.InputSpacer("locator-detect-btn", 140, 28));
        dynamic.AppendChild(addCard);

        dynamic.AppendChild(CandyUI.Separator("locator-sep2"));

        // Nearby summary card
        var nearby = _plugin.LocatorService.NearbyRegulars;
        var nearbyCard = CandyUI.Card("locator-nearby-card");

        if (nearby.Count == 0)
        {
            nearbyCard.AppendChild(CandyUI.Muted("locator-no-nearby", "No tracked patrons nearby."));
        }
        else
        {
            nearbyCard.AppendChild(CandyUI.Label("locator-nearby-title",
                $"{nearby.Count} tracked patron(s) nearby", 13));

            for (int i = 0; i < nearby.Count; i++)
            {
                var (patron, dist) = nearby[i];
                nearbyCard.AppendChild(CandyUI.Label($"locator-nearby-{i}",
                    $"{patron.Name}  ({dist:F1}m)"));
            }
        }
        dynamic.AppendChild(nearbyCard);

        // Patron list — rendered via DrawOverlays()
        dynamic.AppendChild(CandyUI.Muted("locator-list-label", "Regulars & Tracked List:"));
        dynamic.AppendChild(CandyUI.InputSpacer("locator-list-spacer", 440, 200));

        return _root = root;
    }

    public void DrawOverlays()
    {
        if (_root == null) return;

        static bool TryPlace(Node root, string id, out Rect r)
        {
            r = null!;
            var node = root.QuerySelector($"#{id}");
            if (node == null) return false;
            r = node.Bounds.ContentRect;
            if (r == null || (r.Width < 1 && r.Height < 1)) return false;
            ImGui.SetCursorScreenPos(new Vector2(r.X1, r.Y1));
            return true;
        }

        if (TryPlace(_root, "locator-fname", out _))
        {
            ImGui.SetNextItemWidth(120);
            ImGui.InputTextWithHint("##fname", "First Name", ref newPatronFirstName, 50);
        }

        if (TryPlace(_root, "locator-lname", out _))
        {
            ImGui.SetNextItemWidth(120);
            ImGui.InputTextWithHint("##lname", "Last Name", ref newPatronLastName, 50);
        }

        if (TryPlace(_root, "locator-world", out _))
        {
            ImGui.SetNextItemWidth(120);
            ImGui.InputTextWithHint("##world", "World", ref newPatronWorld, 50);
        }

        if (TryPlace(_root, "locator-track-btn", out _))
        {
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
                    newPatronLastName  = string.Empty;
                    newPatronWorld     = string.Empty;
                }
            }
        }

        if (TryPlace(_root, "locator-detect-btn", out _))
        {
            if (ImGui.Button("Detect Targeted"))
            {
                var target = Svc.Targets.Target;
                if (target != null && target.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                {
                    var nameParts = target.Name.ToString().Split(' ', 2);
                    newPatronFirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
                    newPatronLastName  = nameParts.Length > 1 ? nameParts[1] : string.Empty;
                    if (target is IPlayerCharacter pc && pc.HomeWorld.IsValid)
                        newPatronWorld = pc.HomeWorld.Value.Name.ToString();
                    else
                        newPatronWorld = Svc.PlayerState.HomeWorld.Value.Name.ToString();
                }
            }
        }

        if (TryPlace(_root, "locator-list-spacer", out var ls))
        {
            using var patronList = ImRaii.Child("PatronList", new Vector2(ls.Width, ls.Height), true);
            if (patronList)
            {
                foreach (var p in _plugin.Configuration.Patrons)
                {
                    var ptier    = _plugin.Configuration.GetTier(p);
                    var ptierStr = p.Status == PatronStatus.Regular ? $" [{ptier}]" : string.Empty;
                    if (ImGui.Selectable($"- {p.Name}{ptierStr}##{p.Name}", SelectedPatron == p))
                    {
                        SelectedPatron = p;
                        OnPatronSelected?.Invoke(p);
                    }

                    if (p.ActiveVip != null && !p.ActiveVip.IsExpired)
                    {
                        ImGui.SameLine(0, 4f);
                        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.8f, 0.2f, 1f), "\U0001f48e");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"VIP: {p.ActiveVip.PackageName}");
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
    }
}
