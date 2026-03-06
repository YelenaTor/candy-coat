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

        using var tabs = ImRaii.TabBar("##GRTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!tabs) return;

        if (ImGui.BeginTabItem("Queue##GR"))
        {
            DrawDoorQueue();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Tells##GR"))
        {
            DrawWelcomeTells();
            DrawVenueInfoDispatch();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Tools##GR"))
        {
            DrawEmoteShortcuts();
            DrawRoomAvailability();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Ping##GR"))
        {
            ImGui.Spacing();
            _pingWidget.Draw();
            ImGui.EndTabItem();
        }
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "\ud83d\udea8 Greeter Settings");
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
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Welcome Macro Bank");
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
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Venue Info Broadcasts");
                ImGui.Separator();
                ImGui.TextDisabled("Quick-fire venue info to chat channels.");
                ImGui.Spacing();

                using (var scroll = ImRaii.Child("##GRBcastList", new Vector2(0, 100f), false))
                {
                    for (int i = 0; i < cfg.GreeterBroadcasts.Count; i++)
                    {
                        var b = cfg.GreeterBroadcasts[i];
                        ImGui.PushID($"grbc{i}");
                        ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), $"[{b.Channel}]");
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
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Greeter Preferences");
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
            ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), "\ud83d\udfe2 On Door");
            ImGui.SameLine();
            if (ImGui.SmallButton("Take Break##GR")) _onDoor = false;
        }
        else
        {
            ImGui.TextColored(new Vector4(1.0f, 0.85f, 0.4f, 1.0f), "\u23f8 On Break");
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
        ImGui.TextColored(nearbyCount > 48 ? new Vector4(1.0f, 0.85f, 0.4f, 1.0f) : new Vector4(0.5f, 0.9f, 0.65f, 1.0f), $"{nearbyCount}");

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
                    PatronStatus.Blacklisted => new Vector4(1.0f, 0.45f, 0.45f, 1.0f),
                    PatronStatus.Warning     => new Vector4(1.0f, 0.85f, 0.4f, 1.0f),
                    _                        => new Vector4(0.5f, 0.9f, 0.65f, 1.0f),
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
                RoomStatus.Available => new Vector4(0.5f, 0.9f, 0.65f, 1.0f),
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

    // ─── Una.Drawing ─────────────────────────────────────────────────────────

    private int _grActiveTab = 0;
    private static readonly string[] GrTabs = ["Queue", "Tells", "Tools", "Ping"];

    public Node BuildNode()
    {
        Node content = _grActiveTab switch {
            0 => BuildGrTabQueue(),
            1 => BuildGrTabTells(),
            2 => BuildGrTabTools(),
            _ => BuildGrTabPing(),
        };
        var col = CandyUI.Column("gr-root", 6);
        col.AppendChild(CandyUI.SectionHeader("gr-door-hdr", "Door Status"));
        col.AppendChild(CandyUI.InputSpacer("gr-door-sp", 0, 100));
        col.AppendChild(CandyUI.Separator("gr-door-sep"));
        col.AppendChild(CandyUI.TabContainer("gr-tabs", GrTabs, _grActiveTab,
            idx => { _grActiveTab = idx; }, content));
        return col;
    }

    private Node BuildGrTabQueue()
    {
        var col = CandyUI.Column("gr-queue", 6);
        col.AppendChild(CandyUI.InputSpacer("gr-queue-add-sp", 0, 28));

        if (_doorQueue.Count == 0)
        {
            col.AppendChild(CandyUI.Muted("gr-queue-empty", "Door queue is empty."));
        }
        else
        {
            var cfg = _plugin.Configuration;
            var card = CandyUI.Card("gr-queue-card");
            for (int i = 0; i < _doorQueue.Count; i++)
            {
                var (name, addedAt) = _doorQueue[i];
                var wait = DateTime.Now - addedAt;
                var patron = cfg.Patrons.FirstOrDefault(p => p.Name == name);
                var tierSuffix = (cfg.GreeterShowTierBadge && patron != null)
                    ? $"[{cfg.GetTier(patron)}] " : "";
                int ci = i;
                card.AppendChild(CandyUI.Row($"gr-queue-row-{ci}", 6,
                    CandyUI.Label($"gr-queue-name-{ci}",
                        $"[{ci + 1}] {tierSuffix}{name} ({wait.Minutes}m {wait.Seconds}s)", 12),
                    CandyUI.SmallButton($"gr-queue-handoff-{ci}", "Handoff", () =>
                    {
                        Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                        {
                            Type    = Dalamud.Game.Text.XivChatType.Echo,
                            Message = $"[Candy Coat] Guest Ready: {name}"
                        });
                    }),
                    CandyUI.SmallButton($"gr-queue-seated-{ci}", "Seated", () =>
                    {
                        if (ci < _doorQueue.Count) _doorQueue.RemoveAt(ci);
                    })
                ));
            }
            col.AppendChild(card);
        }
        return col;
    }

    private Node BuildGrTabTells()
    {
        var col = CandyUI.Column("gr-tells", 6);
        var cfg = _plugin.Configuration;

        var target = _doorQueue.Count > 0 ? _doorQueue[0].Name
            : Svc.Targets.Target?.Name.ToString() ?? "";

        if (!string.IsNullOrEmpty(target))
            col.AppendChild(CandyUI.Muted("gr-tells-target", $"Target: {target}", 11));

        if (cfg.GreeterWelcomeMacros.Count == 0)
        {
            col.AppendChild(CandyUI.Muted("gr-tells-nomacros", "No welcome macros. Add in Settings."));
        }
        else
        {
            col.AppendChild(CandyUI.SectionHeader("gr-tells-hdr", "Welcome Tells"));
            var card = CandyUI.Card("gr-tells-card");
            for (int i = 0; i < cfg.GreeterWelcomeMacros.Count; i++)
            {
                var m = cfg.GreeterWelcomeMacros[i];
                int ci = i;
                card.AppendChild(CandyUI.Row($"gr-tell-row-{ci}", 6,
                    CandyUI.Button($"gr-tell-btn-{ci}", m.Title, () =>
                    {
                        var t = _doorQueue.Count > 0 ? _doorQueue[0].Name
                            : Svc.Targets.Target?.Name.ToString() ?? "";
                        if (!string.IsNullOrEmpty(t))
                        {
                            var firstName = t.Split(' ')[0];
                            var msg = m.Text.Replace("{name}", firstName).Replace("{venue}", cfg.VenueName);
                            Svc.Commands.ProcessCommand($"/t {t} {msg}");
                        }
                    }),
                    CandyUI.Muted($"gr-tell-preview-{ci}",
                        m.Text.Length > 35 ? m.Text[..35] + "..." : m.Text, 11)
                ));
            }
            col.AppendChild(card);
        }

        col.AppendChild(CandyUI.Separator("gr-tells-sep1"));
        col.AppendChild(CandyUI.SectionHeader("gr-bcast-hdr", "Venue Info Broadcasts"));

        if (cfg.GreeterBroadcasts.Count == 0)
        {
            col.AppendChild(CandyUI.Muted("gr-bcast-empty", "No broadcasts configured. Add in Settings."));
        }
        else
        {
            var bcastCard = CandyUI.Card("gr-bcast-card");
            for (int i = 0; i < cfg.GreeterBroadcasts.Count; i++)
            {
                var b = cfg.GreeterBroadcasts[i];
                int ci = i;
                bcastCard.AppendChild(CandyUI.Row($"gr-bcast-row-{ci}", 6,
                    CandyUI.Button($"gr-bcast-btn-{ci}", b.Label, () =>
                        Svc.Commands.ProcessCommand($"/{b.Channel} {b.Text}")),
                    CandyUI.Muted($"gr-bcast-ch-{ci}", $"[{b.Channel}]", 11)
                ));
            }
            col.AppendChild(bcastCard);
        }
        return col;
    }

    private Node BuildGrTabTools()
    {
        var col = CandyUI.Column("gr-tools", 6);
        col.AppendChild(CandyUI.SectionHeader("gr-tools-emotes-hdr", "Welcoming Emotes"));
        var emoteCard = CandyUI.Card("gr-tools-emotes-card");
        emoteCard.AppendChild(CandyUI.Row("gr-emotes-row", 4,
            CandyUI.SmallButton("gr-em-wave",    "Wave",    () => Svc.Commands.ProcessCommand("/wave motion")),
            CandyUI.SmallButton("gr-em-bow",     "Bow",     () => Svc.Commands.ProcessCommand("/bow motion")),
            CandyUI.SmallButton("gr-em-beckon",  "Beckon",  () => Svc.Commands.ProcessCommand("/beckon motion")),
            CandyUI.SmallButton("gr-em-curtsey", "Curtsey", () => Svc.Commands.ProcessCommand("/curtsey motion")),
            CandyUI.SmallButton("gr-em-smile",   "Smile",   () => Svc.Commands.ProcessCommand("/smile motion"))
        ));
        col.AppendChild(emoteCard);

        col.AppendChild(CandyUI.Separator("gr-tools-sep1"));
        col.AppendChild(CandyUI.SectionHeader("gr-rooms-hdr", "Room Availability"));

        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0)
        {
            col.AppendChild(CandyUI.Muted("gr-rooms-empty", "No rooms configured."));
        }
        else
        {
            var roomCard = CandyUI.Card("gr-rooms-card");
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                roomCard.AppendChild(CandyUI.Label($"gr-room-{i}",
                    $"• {room.Name}: {room.Status}", 12));
            }
            col.AppendChild(roomCard);
        }
        return col;
    }

    private Node BuildGrTabPing()
    {
        var col = CandyUI.Column("gr-ping-tab", 6);
        col.AppendChild(CandyUI.Muted("gr-ping-note", "Staff ping widget below."));
        return col;
    }

    public Node BuildSettingsNode()
    {
        var col = CandyUI.Column("gr-settings", 8);
        col.AppendChild(CandyUI.SectionHeader("gr-settings-hdr", "Greeter Settings"));
        col.AppendChild(CandyUI.Muted("gr-settings-desc", "Configure welcome macros, broadcasts, and preferences."));
        col.AppendChild(CandyUI.Separator("gr-settings-sep1"));

        var cfg = _plugin.Configuration;

        // Welcome Macro Bank card
        var macroCard = CandyUI.Card("gr-settings-macros-card");
        macroCard.AppendChild(CandyUI.SectionHeader("gr-settings-macros-hdr", "Welcome Macro Bank"));
        macroCard.AppendChild(CandyUI.Muted("gr-settings-macros-hint", "Use {name} and {venue} tokens.", 11));
        if (cfg.GreeterWelcomeMacros.Count == 0)
        {
            macroCard.AppendChild(CandyUI.Muted("gr-settings-nomacros", "No welcome macros yet."));
        }
        else
        {
            for (int i = 0; i < cfg.GreeterWelcomeMacros.Count; i++)
            {
                var m = cfg.GreeterWelcomeMacros[i];
                int ci = i;
                macroCard.AppendChild(CandyUI.Row($"gr-smacro-row-{ci}", 6,
                    CandyUI.Label($"gr-smacro-title-{ci}", m.Title, 12),
                    CandyUI.Muted($"gr-smacro-preview-{ci}",
                        m.Text.Length > 40 ? m.Text[..40] + "..." : m.Text, 11),
                    CandyUI.SmallButton($"gr-smacro-del-{ci}", "Del", () =>
                    {
                        cfg.GreeterWelcomeMacros.RemoveAt(ci);
                        cfg.Save();
                    })
                ));
            }
        }
        macroCard.AppendChild(CandyUI.InputSpacer("gr-settings-addmacro-sp", 0, 28));
        col.AppendChild(macroCard);

        col.AppendChild(CandyUI.Separator("gr-settings-sep2"));

        // Broadcasts card
        var bcastCard = CandyUI.Card("gr-settings-bcast-card");
        bcastCard.AppendChild(CandyUI.SectionHeader("gr-settings-bcast-hdr", "Venue Info Broadcasts"));
        bcastCard.AppendChild(CandyUI.Muted("gr-settings-bcast-hint", "Quick-fire venue info to chat channels.", 11));
        if (cfg.GreeterBroadcasts.Count == 0)
        {
            bcastCard.AppendChild(CandyUI.Muted("gr-settings-nobcast", "No broadcasts yet."));
        }
        else
        {
            for (int i = 0; i < cfg.GreeterBroadcasts.Count; i++)
            {
                var b = cfg.GreeterBroadcasts[i];
                int ci = i;
                bcastCard.AppendChild(CandyUI.Row($"gr-sbcast-row-{ci}", 6,
                    CandyUI.Label($"gr-sbcast-ch-{ci}", $"[{b.Channel}]", 11),
                    CandyUI.Label($"gr-sbcast-label-{ci}", b.Label, 12),
                    CandyUI.Muted($"gr-sbcast-preview-{ci}",
                        b.Text.Length > 30 ? b.Text[..30] + "..." : b.Text, 11),
                    CandyUI.SmallButton($"gr-sbcast-del-{ci}", "Del", () =>
                    {
                        cfg.GreeterBroadcasts.RemoveAt(ci);
                        cfg.Save();
                    })
                ));
            }
        }
        bcastCard.AppendChild(CandyUI.InputSpacer("gr-settings-addbcast-sp", 0, 28));
        col.AppendChild(bcastCard);

        col.AppendChild(CandyUI.Separator("gr-settings-sep3"));

        // Preferences card
        var prefCard = CandyUI.Card("gr-settings-pref-card");
        prefCard.AppendChild(CandyUI.SectionHeader("gr-settings-pref-hdr", "Greeter Preferences"));
        prefCard.AppendChild(CandyUI.InputSpacer("gr-settings-pref-sp", 0, 50));
        col.AppendChild(prefCard);

        return col;
    }

    public void DrawOverlays()
    {
        DrawDoorStatus();
        // Door queue add inputs
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
            if (!string.IsNullOrWhiteSpace(_queueNameInput)
                && !_doorQueue.Any(q => q.Name == _queueNameInput))
            {
                _doorQueue.Add((_queueNameInput, DateTime.Now));
                _queueNameInput = string.Empty;
            }
        }
    }

    public void DrawSettingsOverlays()
    {
        // Add welcome macro form
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
                _plugin.Configuration.GreeterWelcomeMacros.Add(
                    new MacroTemplate { Title = _newWelcomeMacroTitle, Text = _newWelcomeMacroText });
                _plugin.Configuration.Save();
                _newWelcomeMacroTitle = string.Empty;
                _newWelcomeMacroText  = string.Empty;
            }
        }

        // Add broadcast form
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
                _plugin.Configuration.GreeterBroadcasts.Add(new GreeterBroadcast
                {
                    Label   = _newBcastLabel,
                    Text    = _newBcastText,
                    Channel = ChannelLabels[_newBcastChannel]
                });
                _plugin.Configuration.Save();
                _newBcastLabel = string.Empty;
                _newBcastText  = string.Empty;
            }
        }

        // Preferences checkboxes
        var cfg         = _plugin.Configuration;
        var autoAdd     = cfg.GreeterAutoAddTargetOnOpen;
        if (ImGui.Checkbox("Auto-add targeted player to door queue##GR", ref autoAdd))
        {
            cfg.GreeterAutoAddTargetOnOpen = autoAdd;
            cfg.Save();
        }
        var showTier = cfg.GreeterShowTierBadge;
        if (ImGui.Checkbox("Show patron tier badge on queue entries##GR", ref showTier))
        {
            cfg.GreeterShowTierBadge = showTier;
            cfg.Save();
        }
    }
}
