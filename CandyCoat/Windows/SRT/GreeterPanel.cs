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

public class GreeterPanel : IToolboxPanel
{
    private readonly Plugin _plugin;

    public string Name => "Greeter";
    public StaffRole Role => StaffRole.Greeter;

    // Door status
    private bool _onDoor = true;

    // Door queue (session-only, not persisted)
    private readonly List<(string Name, DateTime AddedAt)> _doorQueue = new();
    private string _queueNameInput = string.Empty;

    // Quick patron lookup
    private string _lookupQuery = string.Empty;

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);
    private static readonly Vector4 HeaderBg = new(0.22f, 0.16f, 0.28f, 1f);
    private static readonly Vector4 HeaderHover = new(0.30f, 0.22f, 0.36f, 1f);

    public GreeterPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features ────────────────────────────────────────────────────────────

    public void DrawContent()
    {
        // Tier 1 — Door Status (~100px fixed)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tier1 = ImRaii.Child("##GRTier1", new Vector2(0, 100f), true))
        {
            ImGui.PopStyleColor();
            if (tier1) DrawDoorStatus();
        }

        ImGui.Spacing();

        // Tier 2 — Collapsibles
        ImGui.PushStyleColor(ImGuiCol.Header, HeaderBg);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, HeaderHover);

        if (ImGui.CollapsingHeader("Door Queue##GR", ImGuiTreeNodeFlags.DefaultOpen))
            DrawDoorQueue();

        if (ImGui.CollapsingHeader("Welcome Tells##GR", ImGuiTreeNodeFlags.DefaultOpen))
            DrawWelcomeTells();

        if (ImGui.CollapsingHeader("Venue Info Dispatch##GR", ImGuiTreeNodeFlags.DefaultOpen))
            DrawVenueInfoDispatch();

        if (ImGui.CollapsingHeader("Emote Shortcuts##GR", ImGuiTreeNodeFlags.DefaultOpen))
            DrawEmoteShortcuts();

        if (ImGui.CollapsingHeader("Room Availability##GR"))
            DrawRoomAvailability();

        if (ImGui.CollapsingHeader("Staff Ping##GR"))
            _pingWidget.Draw();

        ImGui.PopStyleColor(2);
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "\ud83d\udea8 Greeter Settings");
        ImGui.TextDisabled("Configure welcome macros, broadcasts, and preferences.");
        ImGui.Spacing();

        var cfg = _plugin.Configuration;

        // Card 1: Welcome Macros
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card1 = ImRaii.Child("##GRMacroCard", new Vector2(0, 200f), true))
        {
            ImGui.PopStyleColor();
            if (card1)
            {
                ImGui.TextColored(StyleManager.SectionHeader, "Welcome Macro Bank");
                ImGui.Separator();
                ImGui.TextDisabled("Use {name} and {venue} tokens. Fires /t {target} {msg}.");
                ImGui.Spacing();

                using (var scroll = ImRaii.Child("##GRMacroList", new Vector2(0, 90f), false))
                {
                    for (int i = 0; i < cfg.GreeterWelcomeMacros.Count; i++)
                    {
                        var m = cfg.GreeterWelcomeMacros[i];
                        ImGui.PushID($"grwm{i}");
                        ImGui.Text(m.Title);
                        ImGui.SameLine();
                        ImGui.TextDisabled(m.Text.Length > 40 ? m.Text[..40] + "..." : m.Text);
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Del##grwmd"))
                        {
                            cfg.GreeterWelcomeMacros.RemoveAt(i);
                            cfg.Save();
                            ImGui.PopID();
                            break;
                        }
                        ImGui.PopID();
                    }
                    if (cfg.GreeterWelcomeMacros.Count == 0) ImGui.TextDisabled("No welcome macros yet.");
                }

                DrawAddWelcomeMacro();
            }
        }

        ImGui.Spacing();

        // Card 2: Venue Info Broadcasts
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card2 = ImRaii.Child("##GRBcastCard", new Vector2(0, 220f), true))
        {
            ImGui.PopStyleColor();
            if (card2)
            {
                ImGui.TextColored(StyleManager.SectionHeader, "Venue Info Broadcasts");
                ImGui.Separator();
                ImGui.TextDisabled("Quick-fire venue info to chat channels.");
                ImGui.Spacing();

                using (var scroll = ImRaii.Child("##GRBcastList", new Vector2(0, 100f), false))
                {
                    for (int i = 0; i < cfg.GreeterBroadcasts.Count; i++)
                    {
                        var b = cfg.GreeterBroadcasts[i];
                        ImGui.PushID($"grbc{i}");
                        ImGui.TextColored(StyleManager.SyncOk, $"[{b.Channel}]");
                        ImGui.SameLine();
                        ImGui.Text(b.Label);
                        ImGui.SameLine();
                        ImGui.TextDisabled(b.Text.Length > 30 ? b.Text[..30] + "..." : b.Text);
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Del##grbcd"))
                        {
                            cfg.GreeterBroadcasts.RemoveAt(i);
                            cfg.Save();
                            ImGui.PopID();
                            break;
                        }
                        ImGui.PopID();
                    }
                    if (cfg.GreeterBroadcasts.Count == 0) ImGui.TextDisabled("No broadcasts yet.");
                }

                DrawAddBroadcast();
            }
        }

        ImGui.Spacing();

        // Card 3: Greeter Preferences
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card3 = ImRaii.Child("##GRPrefCard", new Vector2(0, 90f), true))
        {
            ImGui.PopStyleColor();
            if (card3)
            {
                ImGui.TextColored(StyleManager.SectionHeader, "Greeter Preferences");
                ImGui.Separator();
                ImGui.Spacing();

                var autoAdd = cfg.GreeterAutoAddTargetOnOpen;
                if (ImGui.Checkbox("Automatically add targeted player to door queue on open##GR", ref autoAdd))
                {
                    cfg.GreeterAutoAddTargetOnOpen = autoAdd;
                    cfg.Save();
                }

                var showTier = cfg.GreeterShowTierBadge;
                if (ImGui.Checkbox("Show patron tier badge on door queue entries##GR", ref showTier))
                {
                    cfg.GreeterShowTierBadge = showTier;
                    cfg.Save();
                }
            }
        }
    }

    // ─── Private Draw Helpers ────────────────────────────────────────────────

    private void DrawDoorStatus()
    {
        // On Door / On Break toggle
        if (_onDoor)
        {
            ImGui.TextColored(StyleManager.SyncOk, "\ud83d\udfe2 On Door");
            ImGui.SameLine();
            if (ImGui.SmallButton("Take Break##GR")) _onDoor = false;
        }
        else
        {
            ImGui.TextColored(StyleManager.SyncWarn, "\u23f8 On Break");
            ImGui.SameLine();
            if (ImGui.SmallButton("Back to Door##GR")) _onDoor = true;
        }

        // Capacity indicator
        var nearbyCount = _plugin.LocatorService.GetNearbyCount();
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        ImGui.Text("Nearby:");
        ImGui.SameLine();
        ImGui.TextColored(nearbyCount > 48 ? StyleManager.SyncWarn : StyleManager.SyncOk, $"{nearbyCount}");

        ImGui.Spacing();

        // Quick patron lookup
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##GRLookup", "\ud83d\udd0d Search patron by name...", ref _lookupQuery, 100);
        if (!string.IsNullOrWhiteSpace(_lookupQuery))
        {
            var cfg = _plugin.Configuration;
            var matches = cfg.Patrons
                .Where(p => p.Name.Contains(_lookupQuery, StringComparison.OrdinalIgnoreCase))
                .Take(3).ToList();
            foreach (var p in matches)
            {
                var tier = cfg.GetTier(p);
                var tierColor = tier switch
                {
                    PatronTier.Elite   => new Vector4(1f, 0.85f, 0.2f, 1f),
                    PatronTier.Regular => new Vector4(1f, 0.5f, 0.8f, 1f),
                    _                  => new Vector4(0.7f, 0.7f, 0.7f, 1f),
                };
                ImGui.TextColored(tierColor, $"[{tier}]");
                ImGui.SameLine();
                ImGui.Text(p.Name);
                ImGui.SameLine();
                var statusColor = p.Status switch
                {
                    PatronStatus.Blacklisted => StyleManager.SyncError,
                    PatronStatus.Warning     => StyleManager.SyncWarn,
                    _                        => StyleManager.SyncOk,
                };
                ImGui.TextColored(statusColor, p.Status.ToString());
            }
            if (matches.Count == 0) ImGui.TextDisabled("No patrons match.");
        }
    }

    private void DrawDoorQueue()
    {
        ImGui.Spacing();
        var cfg = _plugin.Configuration;

        if (ImGui.Button("Add Target##GRQ"))
        {
            var t = Svc.Targets.Target;
            if (t != null && !_doorQueue.Any(q => q.Name == t.Name.ToString()))
                _doorQueue.Add((t.Name.ToString(), DateTime.Now));
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##GRQName", "Name", ref _queueNameInput, 100);
        ImGui.SameLine();
        if (ImGui.Button("+##GRQAdd"))
        {
            if (!string.IsNullOrWhiteSpace(_queueNameInput) && !_doorQueue.Any(q => q.Name == _queueNameInput))
            {
                _doorQueue.Add((_queueNameInput, DateTime.Now));
                _queueNameInput = string.Empty;
            }
        }

        ImGui.Spacing();

        if (_doorQueue.Count == 0) { ImGui.TextDisabled("Door queue is empty."); ImGui.Spacing(); return; }

        for (int i = 0; i < _doorQueue.Count; i++)
        {
            var (name, addedAt) = _doorQueue[i];
            var wait = DateTime.Now - addedAt;
            ImGui.PushID($"grq{i}");

            // Tier badge
            if (cfg.GreeterShowTierBadge)
            {
                var patron = cfg.Patrons.FirstOrDefault(p => p.Name == name);
                if (patron != null)
                {
                    var tier = cfg.GetTier(patron);
                    var tierColor = tier switch { PatronTier.Elite => new Vector4(1f, 0.85f, 0.2f, 1f), PatronTier.Regular => new Vector4(1f, 0.5f, 0.8f, 1f), _ => new Vector4(0.7f, 0.7f, 0.7f, 1f) };
                    ImGui.TextColored(tierColor, $"[{tier}]");
                    ImGui.SameLine();
                }
            }

            ImGui.Text($"[{i + 1}] {name}");
            ImGui.SameLine();
            ImGui.TextDisabled($"({wait.Minutes}m {wait.Seconds}s)");
            ImGui.SameLine();
            if (ImGui.SmallButton("\u2192 Handoff##GRQ"))
            {
                Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                {
                    Type = Dalamud.Game.Text.XivChatType.Echo,
                    Message = $"[Candy Coat] Guest Ready: {name}"
                });
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("\u2713 Seated##GRQ"))
            {
                _doorQueue.RemoveAt(i);
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
        ImGui.Spacing();
    }

    private void DrawWelcomeTells()
    {
        ImGui.Spacing();
        var cfg = _plugin.Configuration;
        if (cfg.GreeterWelcomeMacros.Count == 0) { ImGui.TextDisabled("No welcome macros. Add in Settings."); ImGui.Spacing(); return; }

        var target = _doorQueue.Count > 0 ? _doorQueue[0].Name : Svc.Targets.Target?.Name.ToString() ?? "";
        if (!string.IsNullOrEmpty(target))
        {
            ImGui.TextDisabled($"Target: {target}");
            ImGui.Spacing();
        }

        foreach (var m in cfg.GreeterWelcomeMacros)
        {
            if (ImGui.Button($"{m.Title}##GRwt{m.Title}"))
            {
                if (!string.IsNullOrEmpty(target))
                {
                    var firstName = target.Split(' ')[0];
                    var msg = m.Text.Replace("{name}", firstName).Replace("{venue}", cfg.VenueName);
                    Svc.Commands.ProcessCommand($"/t {target} {msg}");
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled(m.Text.Length > 35 ? m.Text[..35] + "..." : m.Text);
        }
        ImGui.Spacing();
    }

    private void DrawVenueInfoDispatch()
    {
        ImGui.Spacing();
        var broadcasts = _plugin.Configuration.GreeterBroadcasts;
        if (broadcasts.Count == 0) { ImGui.TextDisabled("No broadcasts configured. Add in Settings."); ImGui.Spacing(); return; }

        foreach (var b in broadcasts)
        {
            if (ImGui.Button($"{b.Label}##GRvib{b.Label}"))
                Svc.Commands.ProcessCommand($"/{b.Channel} {b.Text}");
            ImGui.SameLine();
            ImGui.TextDisabled($"[{b.Channel}]");
        }
        ImGui.Spacing();
    }

    private void DrawEmoteShortcuts()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Welcoming:");
        if (ImGui.Button("Wave##GR", new Vector2(65, 22))) Svc.Commands.ProcessCommand("/wave motion");
        ImGui.SameLine();
        if (ImGui.Button("Bow##GR", new Vector2(60, 22))) Svc.Commands.ProcessCommand("/bow motion");
        ImGui.SameLine();
        if (ImGui.Button("Beckon##GR", new Vector2(75, 22))) Svc.Commands.ProcessCommand("/beckon motion");
        ImGui.SameLine();
        if (ImGui.Button("Curtsey##GR", new Vector2(75, 22))) Svc.Commands.ProcessCommand("/curtsey motion");
        ImGui.SameLine();
        if (ImGui.Button("Smile##GR", new Vector2(65, 22))) Svc.Commands.ProcessCommand("/smile motion");
        ImGui.Spacing();
    }

    private void DrawRoomAvailability()
    {
        ImGui.Spacing();
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0) { ImGui.TextDisabled("No rooms configured."); ImGui.Spacing(); return; }
        foreach (var room in rooms)
        {
            var color = room.Status switch
            {
                RoomStatus.Available => StyleManager.SyncOk,
                RoomStatus.Occupied  => new Vector4(1f, 0.4f, 0.4f, 1f),
                RoomStatus.Reserved  => new Vector4(1f, 0.8f, 0.2f, 1f),
                _                    => new Vector4(0.5f, 0.5f, 0.5f, 1f),
            };
            ImGui.TextColored(color, $"\u2022 {room.Name}: {room.Status}");
        }
        ImGui.Spacing();
    }

    // ─── Settings Input State ────────────────────────────────────────────────

    private string _newWelcomeMacroTitle = string.Empty;
    private string _newWelcomeMacroText = string.Empty;

    private void DrawAddWelcomeMacro()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(90);
        ImGui.InputTextWithHint("##GRWMacT", "Title", ref _newWelcomeMacroTitle, 50);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##GRWMacM", "{name}, {venue} supported", ref _newWelcomeMacroText, 200);
        ImGui.SameLine();
        if (ImGui.Button("+##GRAddWM"))
        {
            if (!string.IsNullOrWhiteSpace(_newWelcomeMacroTitle))
            {
                _plugin.Configuration.GreeterWelcomeMacros.Add(new MacroTemplate { Title = _newWelcomeMacroTitle, Text = _newWelcomeMacroText });
                _plugin.Configuration.Save();
                _newWelcomeMacroTitle = string.Empty;
                _newWelcomeMacroText = string.Empty;
            }
        }
    }

    private string _newBcastLabel = string.Empty;
    private string _newBcastText = string.Empty;
    private int _newBcastChannel = 0;
    private static readonly string[] ChannelLabels = { "say", "yell", "echo" };

    private void DrawAddBroadcast()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(70);
        ImGui.InputTextWithHint("##GRBcastL", "Label", ref _newBcastLabel, 30);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(175);
        ImGui.InputTextWithHint("##GRBcastT", "Message text", ref _newBcastText, 300);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        ImGui.Combo("##GRBcastC", ref _newBcastChannel, ChannelLabels, ChannelLabels.Length);
        ImGui.SameLine();
        if (ImGui.Button("+##GRAddBcast"))
        {
            if (!string.IsNullOrWhiteSpace(_newBcastLabel) && !string.IsNullOrWhiteSpace(_newBcastText))
            {
                _plugin.Configuration.GreeterBroadcasts.Add(new GreeterBroadcast { Label = _newBcastLabel, Text = _newBcastText, Channel = ChannelLabels[_newBcastChannel] });
                _plugin.Configuration.Save();
                _newBcastLabel = string.Empty;
                _newBcastText = string.Empty;
            }
        }
    }
}
