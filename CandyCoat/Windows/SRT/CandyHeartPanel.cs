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

public class CandyHeartPanel : IToolboxPanel
{
    private readonly Plugin _plugin;

    public string Name => "Candy Heart";
    public StaffRole Role => StaffRole.CandyHeart;

    // Active patron tracking
    private readonly List<(string Name, int StatusIdx)> _activePatrons = new();
    private string _newPatronName = string.Empty;
    private static readonly string[] StatusLabels = { "Chatting", "Escorting", "Idle" };

    // Input state
    private string _newMacroTitle = string.Empty;
    private string _newMacroText = string.Empty;
    private string _notePatron = string.Empty;
    private string _newNoteText = string.Empty;
    private int _tipAmount = 0;
    private string _tipPatron = string.Empty;

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);
    private static readonly Vector4 HeaderBg = new(0.22f, 0.16f, 0.28f, 1f);
    private static readonly Vector4 HeaderHover = new(0.30f, 0.22f, 0.36f, 1f);

    public CandyHeartPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features ────────────────────────────────────────────────────────────

    public void DrawContent()
    {
        // Tier 1 — Active Patron Tracker (fixed ~120px)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tier1 = ImRaii.Child("##CHTier1", new Vector2(0, 120f), true))
        {
            ImGui.PopStyleColor();
            if (tier1) DrawActivePatrons();
        }

        ImGui.Spacing();

        // Tier 2 — Collapsibles
        ImGui.PushStyleColor(ImGuiCol.Header, HeaderBg);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, HeaderHover);

        if (ImGui.CollapsingHeader("Quick-Tell Macros##CH", ImGuiTreeNodeFlags.DefaultOpen))
            DrawMacroBankButtons();

        if (ImGui.CollapsingHeader("Escort Handoff##CH", ImGuiTreeNodeFlags.DefaultOpen))
            DrawEscortHandoff();

        if (ImGui.CollapsingHeader("Emote Wheel##CH"))
            DrawEmoteWheel();

        if (ImGui.CollapsingHeader("Tips Tracker##CH"))
            DrawTipsTracker();

        if (ImGui.CollapsingHeader("Patron Notes##CH"))
            DrawPatronNotes();

        if (ImGui.CollapsingHeader("Room Status##CH"))
            DrawRoomStatus();

        if (ImGui.CollapsingHeader("Staff Ping##CH"))
            _pingWidget.Draw();

        ImGui.PopStyleColor(2);
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "\ud83d\udc97 Candy Heart Settings");
        ImGui.TextDisabled("Configure your welcome macro bank.");
        ImGui.Spacing();

        // Card: Macro Bank
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card = ImRaii.Child("##CHMacroCard", new Vector2(0, 220f), true))
        {
            ImGui.PopStyleColor();
            if (!card) return;

            ImGui.TextColored(StyleManager.SectionHeader, "Welcome Macro Bank");
            ImGui.Separator();
            ImGui.Spacing();

            var macros = _plugin.Configuration.CandyHeartMacros;

            using (var scroll = ImRaii.Child("##CHMacroList", new Vector2(0, 120f), false))
            {
                for (int i = 0; i < macros.Count; i++)
                {
                    var m = macros[i];
                    ImGui.PushID($"chm{i}");
                    ImGui.Text(m.Title);
                    ImGui.SameLine();
                    ImGui.TextDisabled(m.Text.Length > 40 ? m.Text[..40] + "..." : m.Text);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Del##chmd"))
                    {
                        macros.RemoveAt(i);
                        _plugin.Configuration.Save();
                        ImGui.PopID();
                        break;
                    }
                    ImGui.PopID();
                }
                if (macros.Count == 0) ImGui.TextDisabled("No macros yet.");
            }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(80);
            ImGui.InputTextWithHint("##CHMacroT", "Title", ref _newMacroTitle, 50);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##CHMacroM", "{name} supported", ref _newMacroText, 200);
            ImGui.SameLine();
            if (ImGui.Button("+##CHAddMacro"))
            {
                if (!string.IsNullOrWhiteSpace(_newMacroTitle))
                {
                    macros.Add(new MacroTemplate { Title = _newMacroTitle, Text = _newMacroText });
                    _plugin.Configuration.Save();
                    _newMacroTitle = string.Empty;
                    _newMacroText = string.Empty;
                }
            }
        }
    }

    // ─── Private Draw Helpers ────────────────────────────────────────────────

    private void DrawActivePatrons()
    {
        ImGui.Text("Active Patrons");
        ImGui.Spacing();

        if (ImGui.Button("Add Target##CH"))
        {
            var t = Svc.Targets.Target;
            if (t != null) _newPatronName = t.Name.ToString();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##CHAddPatron", "Name", ref _newPatronName, 100);
        ImGui.SameLine();
        if (ImGui.Button("+##CHAddActive"))
        {
            if (!string.IsNullOrWhiteSpace(_newPatronName) && !_activePatrons.Any(p => p.Name == _newPatronName))
            {
                _activePatrons.Add((_newPatronName, 0));
                _newPatronName = string.Empty;
            }
        }

        for (int i = 0; i < _activePatrons.Count; i++)
        {
            var (name, statusIdx) = _activePatrons[i];
            ImGui.PushID($"chap{i}");
            ImGui.Text(name);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            var si = statusIdx;
            if (ImGui.Combo("##CHStatus", ref si, StatusLabels, StatusLabels.Length))
                _activePatrons[i] = (name, si);
            ImGui.SameLine();
            if (ImGui.SmallButton("X##CHRemove"))
            {
                _activePatrons.RemoveAt(i);
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        if (_activePatrons.Count == 0) ImGui.TextDisabled("No patrons being attended.");
    }

    private void DrawMacroBankButtons()
    {
        ImGui.Spacing();
        var macros = _plugin.Configuration.CandyHeartMacros;
        if (macros.Count == 0) { ImGui.TextDisabled("No macros. Add in Settings."); ImGui.Spacing(); return; }
        foreach (var m in macros)
        {
            if (ImGui.Button($"{m.Title}##CHbtn{m.Title}"))
            {
                var target = _activePatrons.Count > 0 ? _activePatrons[0].Name
                    : Svc.Targets.Target?.Name.ToString() ?? "";
                if (!string.IsNullOrEmpty(target))
                {
                    var msg = m.Text.Replace("{name}", target.Split(' ')[0]);
                    Svc.Commands.ProcessCommand($"/t {target} {msg}");
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled(m.Text.Length > 35 ? m.Text[..35] + "..." : m.Text);
        }
        ImGui.Spacing();
    }

    private void DrawEscortHandoff()
    {
        ImGui.Spacing();
        if (_plugin.SyncService.IsConnected)
            ImGui.TextColored(StyleManager.SyncOk, "\ud83d\udfe2 Handoff will be synced.");
        else
            ImGui.TextColored(StyleManager.SyncWarn, "\u26a0 Handoff is local-only.");

        if (_activePatrons.Count > 0)
        {
            if (ImGui.Button("Announce Handoff (Echo)##CH"))
            {
                Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                {
                    Type = Dalamud.Game.Text.XivChatType.Echo,
                    Message = $"[CandyCoat] Handing off {_activePatrons[0].Name} \u2014 please follow up."
                });
            }
        }
        ImGui.Spacing();
    }

    private void DrawEmoteWheel()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Flirty:");
        EmoteBtn("Blow Kiss", "/blowkiss motion"); ImGui.SameLine();
        EmoteBtn("Wink", "/wink motion"); ImGui.SameLine();
        EmoteBtn("Dote", "/dote motion"); ImGui.SameLine();
        EmoteBtn("Comfort", "/comfort motion");

        ImGui.TextDisabled("Friendly:");
        EmoteBtn("Wave", "/wave motion"); ImGui.SameLine();
        EmoteBtn("Smile", "/smile motion"); ImGui.SameLine();
        EmoteBtn("Cheer", "/cheer motion"); ImGui.SameLine();
        EmoteBtn("Laugh", "/laugh motion");

        ImGui.TextDisabled("Elegant:");
        EmoteBtn("Bow", "/bow motion"); ImGui.SameLine();
        EmoteBtn("Curtsey", "/curtsey motion"); ImGui.SameLine();
        EmoteBtn("Beckon", "/beckon motion"); ImGui.SameLine();
        EmoteBtn("Kneel", "/kneel motion");
        ImGui.Spacing();
    }

    private static void EmoteBtn(string label, string cmd)
    {
        if (ImGui.Button(label, new Vector2(75, 22))) Svc.Commands.ProcessCommand(cmd);
    }

    private void DrawTipsTracker()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##CHTipAmt", ref _tipAmount, 5000);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##CHTipP", "From", ref _tipPatron, 100);
        ImGui.SameLine();
        if (ImGui.Button("Log Tip##CH"))
        {
            if (_tipAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry { Role = StaffRole.CandyHeart, Type = EarningsType.Tip, PatronName = string.IsNullOrWhiteSpace(_tipPatron) ? "Unknown" : _tipPatron, Description = "Tip", Amount = _tipAmount });
                _plugin.Configuration.Save();
                _tipAmount = 0;
                _tipPatron = string.Empty;
            }
        }
        ImGui.Spacing();
    }

    private void DrawPatronNotes()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##CHNotePat", "Patron", ref _notePatron, 100);
        if (!string.IsNullOrEmpty(_notePatron))
        {
            var notes = _plugin.Configuration.PatronNotes.Where(n => n.PatronName == _notePatron && n.AuthorRole == StaffRole.CandyHeart).OrderByDescending(n => n.Timestamp).Take(5).ToList();
            foreach (var n in notes) { ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}]"); ImGui.SameLine(); ImGui.TextWrapped(n.Content); }
            ImGui.SetNextItemWidth(-50);
            ImGui.InputTextWithHint("##CHNoteIn", "Add note...", ref _newNoteText, 500);
            ImGui.SameLine();
            if (ImGui.Button("Save##CHNote"))
            {
                if (!string.IsNullOrWhiteSpace(_newNoteText))
                {
                    _plugin.Configuration.PatronNotes.Add(new PatronNote { PatronName = _notePatron, AuthorRole = StaffRole.CandyHeart, AuthorName = _plugin.Configuration.CharacterName, Content = _newNoteText });
                    _plugin.Configuration.Save();
                    _newNoteText = string.Empty;
                }
            }
        }
        ImGui.Spacing();
    }

    private void DrawRoomStatus()
    {
        ImGui.Spacing();
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0) { ImGui.TextDisabled("No rooms defined."); ImGui.Spacing(); return; }
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
            if (room.Status == RoomStatus.Occupied && !string.IsNullOrEmpty(room.OccupiedBy))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({room.OccupiedBy} + {room.PatronName})");
            }
        }
        ImGui.Spacing();
    }
}
