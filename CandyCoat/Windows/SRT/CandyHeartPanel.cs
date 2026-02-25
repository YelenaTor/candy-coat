using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
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

    // Macro input
    private string _newMacroTitle = string.Empty;
    private string _newMacroText = string.Empty;

    // Note input
    private string _notePatron = string.Empty;
    private string _newNoteText = string.Empty;

    // Tips
    private int _tipAmount = 0;
    private string _tipPatron = string.Empty;

    public CandyHeartPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void DrawContent()
    {
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.9f, 1f), "💗 Candy Heart Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

        DrawActivePatrons();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawMacroBank();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawEscortHandoff();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawEmoteWheel();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawTipsTracker();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPatronNotes();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRoomStatus();
    }

    private void DrawActivePatrons()
    {
        ImGui.Text("Active Patrons");
        ImGui.Spacing();

        // Add patron
        if (ImGui.Button("Add Target"))
        {
            var t = Svc.Targets.Target;
            if (t != null) _newPatronName = t.Name.ToString();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##AddPatron", "Name", ref _newPatronName, 100);
        ImGui.SameLine();
        if (ImGui.Button("+##AddActive"))
        {
            if (!string.IsNullOrWhiteSpace(_newPatronName) && !_activePatrons.Any(p => p.Name == _newPatronName))
            {
                _activePatrons.Add((_newPatronName, 0));
                _newPatronName = string.Empty;
            }
        }

        // List
        for (int i = 0; i < _activePatrons.Count; i++)
        {
            var (name, statusIdx) = _activePatrons[i];
            ImGui.PushID($"ap{i}");

            ImGui.Text(name);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            var si = statusIdx;
            if (ImGui.Combo("##Status", ref si, StatusLabels, StatusLabels.Length))
                _activePatrons[i] = (name, si);
            ImGui.SameLine();
            if (ImGui.SmallButton("X"))
            {
                _activePatrons.RemoveAt(i);
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        if (_activePatrons.Count == 0)
            ImGui.TextDisabled("No patrons being attended.");
    }

    private void DrawMacroBank()
    {
        ImGui.Text("Quick-Tell Macros");
        var macros = _plugin.Configuration.CandyHeartMacros;

        foreach (var m in macros)
        {
            if (ImGui.Button($"{m.Title}##{m.Title}"))
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

        ImGui.SetNextItemWidth(80);
        ImGui.InputTextWithHint("##MacroT", "Title", ref _newMacroTitle, 50);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##MacroM", "{name} supported", ref _newMacroText, 200);
        ImGui.SameLine();
        if (ImGui.Button("+##AddMacro"))
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

    private void DrawEscortHandoff()
    {
        ImGui.Text("Escort Handoff");
        if (_plugin.SyncService.IsConnected)
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.4f, 1f), "🟢 Handoff will be synced to receiving staffer.");
        else
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Handoff is local-only. Enable sync in Settings.");

        if (_activePatrons.Count > 0)
        {
            if (ImGui.Button("Announce Handoff (Echo)"))
            {
                var patron = _activePatrons[0].Name;
                Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                {
                    Type = Dalamud.Game.Text.XivChatType.Echo,
                    Message = $"[CandyCoat] Handing off {patron} — please follow up."
                });
            }
        }
    }

    private void DrawEmoteWheel()
    {
        ImGui.Text("Emote Wheel");
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

        ImGui.TextDisabled("Playful:");
        EmoteBtn("Pose", "/pose motion"); ImGui.SameLine();
        EmoteBtn("Dance", "/dance motion"); ImGui.SameLine();
        EmoteBtn("Joy", "/joy motion"); ImGui.SameLine();
        EmoteBtn("Surprised", "/surprised motion");

        ImGui.TextDisabled("Elegant:");
        EmoteBtn("Bow", "/bow motion"); ImGui.SameLine();
        EmoteBtn("Curtsey", "/curtsey motion"); ImGui.SameLine();
        EmoteBtn("Beckon", "/beckon motion"); ImGui.SameLine();
        EmoteBtn("Kneel", "/kneel motion");
    }

    private static void EmoteBtn(string label, string cmd)
    {
        if (ImGui.Button(label, new Vector2(75, 22)))
            Svc.Commands.ProcessCommand(cmd);
    }

    private void DrawTipsTracker()
    {
        ImGui.Text("Tips Tracker");
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##TipAmt", ref _tipAmount, 5000);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##TipPatron", "From", ref _tipPatron, 100);
        ImGui.SameLine();
        if (ImGui.Button("Log Tip"))
        {
            if (_tipAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role = StaffRole.CandyHeart,
                    Type = EarningsType.Tip,
                    PatronName = string.IsNullOrWhiteSpace(_tipPatron) ? "Unknown" : _tipPatron,
                    Description = "Tip",
                    Amount = _tipAmount,
                });
                _plugin.Configuration.Save();
                _tipAmount = 0;
                _tipPatron = string.Empty;
            }
        }
    }

    private void DrawPatronNotes()
    {
        ImGui.Text("Patron Notes");
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##NotePat", "Patron", ref _notePatron, 100);

        if (!string.IsNullOrEmpty(_notePatron))
        {
            var notes = _plugin.Configuration.PatronNotes
                .Where(n => n.PatronName == _notePatron && n.AuthorRole == StaffRole.CandyHeart)
                .OrderByDescending(n => n.Timestamp).Take(5).ToList();

            foreach (var n in notes)
            {
                ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}]");
                ImGui.SameLine();
                ImGui.TextWrapped(n.Content);
            }

            ImGui.SetNextItemWidth(-50);
            ImGui.InputTextWithHint("##NoteIn", "Add note...", ref _newNoteText, 500);
            ImGui.SameLine();
            if (ImGui.Button("Save##Note"))
            {
                if (!string.IsNullOrWhiteSpace(_newNoteText))
                {
                    _plugin.Configuration.PatronNotes.Add(new PatronNote
                    {
                        PatronName = _notePatron,
                        AuthorRole = StaffRole.CandyHeart,
                        AuthorName = _plugin.Configuration.CharacterName,
                        Content = _newNoteText,
                    });
                    _plugin.Configuration.Save();
                    _newNoteText = string.Empty;
                }
            }
        }
    }

    private void DrawRoomStatus()
    {
        ImGui.Text("Room Status");
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0)
        {
            ImGui.TextDisabled("No rooms defined.");
            if (_plugin.SyncService.IsConnected)
                ImGui.TextColored(new Vector4(0.2f, 1f, 0.4f, 1f), "🟢 Room status synced.");
            else
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Room status is local-only. Enable sync in Settings.");
            return;
        }

        foreach (var room in rooms)
        {
            var color = room.Status switch
            {
                RoomStatus.Available => new Vector4(0.2f, 1f, 0.2f, 1f),
                RoomStatus.Occupied => new Vector4(1f, 0.4f, 0.4f, 1f),
                RoomStatus.Reserved => new Vector4(1f, 0.8f, 0.2f, 1f),
                _ => new Vector4(0.5f, 0.5f, 0.5f, 1f),
            };
            ImGui.TextColored(color, $"• {room.Name}: {room.Status}");
            if (room.Status == RoomStatus.Occupied && !string.IsNullOrEmpty(room.OccupiedBy))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({room.OccupiedBy} + {room.PatronName})");
            }
        }
    }
}
