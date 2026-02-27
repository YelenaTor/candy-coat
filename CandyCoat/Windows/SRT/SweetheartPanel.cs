using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.UI;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class SweetheartPanel : IToolboxPanel
{
    private readonly Plugin _plugin;
    
    public string Name => "Sweetheart";
    public StaffRole Role => StaffRole.Sweetheart;

    // Session state
    private bool _sessionActive = false;
    private string _sessionPatron = string.Empty;
    private DateTime _sessionStart;
    private int _sessionDurationMin = 60;
    private int _selectedRoomIndex = -1;
    private bool _dndToggle = false;
    private string _sessionNotes = string.Empty;
    private bool _alert5Fired = false;
    private bool _alert2Fired = false;

    // Macro input
    private string _newMacroTitle = string.Empty;
    private string _newMacroText = string.Empty;

    // Note input
    private string _newNoteText = string.Empty;

    // Earnings input
    private int _earningsAmount = 0;

    private readonly StaffPingWidget _pingWidget;

    public SweetheartPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    public void DrawContent()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "♥ Sweetheart Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

        DrawSessionTimer();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRoomAssignment();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawServiceRateCard();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawQuickTells();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawGlamourerPresets();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawEarningsLog();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPatronNotes();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPatronHistory();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        _pingWidget.Draw();
    }

    private void DrawSessionTimer()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "Session Timer");
        ImGui.Spacing();

        if (_sessionActive)
        {
            var elapsed = DateTime.Now - _sessionStart;
            var remaining = TimeSpan.FromMinutes(_sessionDurationMin) - elapsed;

            ImGui.TextColored(StyleManager.SyncOk, $"IN SESSION — {_sessionPatron}");
            ImGui.Text($"Elapsed: {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}");

            if (remaining.TotalSeconds > 0)
            {
                var color = remaining.TotalMinutes <= 2 ? new Vector4(1f, 0.2f, 0.2f, 1f)
                          : remaining.TotalMinutes <= 5 ? new Vector4(1f, 0.8f, 0.2f, 1f)
                          : new Vector4(0.8f, 0.8f, 0.8f, 1f);
                ImGui.TextColored(color, $"Remaining: {remaining.Minutes:D2}:{remaining.Seconds:D2}");

                // Alerts
                if (remaining.TotalMinutes <= 5 && !_alert5Fired)
                {
                    Svc.Chat.Print($"[Candy Coat] ⚠ 5 minutes remaining with {_sessionPatron}!");
                    _alert5Fired = true;
                }
                if (remaining.TotalMinutes <= 2 && !_alert2Fired)
                {
                    Svc.Chat.Print($"[Candy Coat] ⚠ 2 minutes remaining with {_sessionPatron}!");
                    _alert2Fired = true;
                }
            }
            else
            {
                float t = (float)(ImGui.GetTime() * 2.0f);
                float pulse = (float)(Math.Sin(t) * 0.5f + 0.5f);
                ImGui.TextColored(new Vector4(1f, pulse, pulse, 1f), "TIME'S UP!");
            }

            if (ImGui.Button("End Session", new Vector2(150, 30)))
            {
                EndSession();
            }
        }
        else
        {
            // Start session
            var target = Svc.Targets.Target;
            if (target != null && string.IsNullOrEmpty(_sessionPatron))
            {
                if (ImGui.Button("Use Target"))
                    _sessionPatron = target.Name.ToString();
                ImGui.SameLine();
            }
            ImGui.SetNextItemWidth(180);
            ImGui.InputTextWithHint("##SessionPatron", "Patron Name", ref _sessionPatron, 100);

            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("Duration (min)", ref _sessionDurationMin, 15);
            if (_sessionDurationMin < 15) _sessionDurationMin = 15;

            if (!string.IsNullOrWhiteSpace(_sessionPatron))
            {
                if (ImGui.Button("Start Session", new Vector2(150, 30)))
                {
                    _sessionActive = true;
                    _sessionStart = DateTime.Now;
                    _alert5Fired = false;
                    _alert2Fired = false;

                    // Mark room occupied
                    if (_selectedRoomIndex >= 0 && _selectedRoomIndex < _plugin.Configuration.Rooms.Count)
                    {
                        var room = _plugin.Configuration.Rooms[_selectedRoomIndex];
                        room.Status = RoomStatus.Occupied;
                        room.OccupiedBy = _plugin.Configuration.CharacterName;
                        room.PatronName = _sessionPatron;
                        room.OccupiedSince = DateTime.Now;
                        _plugin.Configuration.Save();
                    }

                    Svc.Chat.Print($"[Candy Coat] Session started with {_sessionPatron}.");
                }
            }

            // DND
            ImGui.Checkbox("Do Not Disturb", ref _dndToggle);
            if (_dndToggle)
            {
                if (_plugin.SyncService.IsConnected)
                    ImGui.TextColored(StyleManager.SyncOk, "🟢 DND status synced to other staff.");
                else
                    ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ DND is local-only. Enable sync in Settings.");
            }
        }
    }

    private void EndSession()
    {
        // Free room
        if (_selectedRoomIndex >= 0 && _selectedRoomIndex < _plugin.Configuration.Rooms.Count)
        {
            var room = _plugin.Configuration.Rooms[_selectedRoomIndex];
            room.Status = RoomStatus.Available;
            room.OccupiedBy = string.Empty;
            room.PatronName = string.Empty;
            room.OccupiedSince = null;
            _plugin.Configuration.Save();
        }

        Svc.Chat.Print($"[Candy Coat] Session ended with {_sessionPatron}.");
        _sessionActive = false;
        _sessionPatron = string.Empty;
        _selectedRoomIndex = -1;
    }

    private void DrawRoomAssignment()
    {
        ImGui.Text("Room Assignment");
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0)
        {
            ImGui.TextDisabled("No rooms defined. Add in Owner > Room Editor.");
            return;
        }

        var names = rooms.Select(r => $"{r.Name} ({r.Status})").ToArray();
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("##RoomSelect", ref _selectedRoomIndex, names, names.Length);
    }

    private void DrawServiceRateCard()
    {
        ImGui.Text("Service Rate Card");
        var items = _plugin.Configuration.ServiceMenu
            .Where(s => s.Category == ServiceCategory.Session).ToList();

        if (items.Count == 0)
        {
            ImGui.TextDisabled("No session services defined. Add in Owner > Menu Editor.");
            return;
        }

        foreach (var item in items)
        {
            ImGui.BulletText($"{item.Name} — {item.Price:N0} Gil");
            if (!string.IsNullOrEmpty(item.Description))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"({item.Description})");
            }
        }
    }

    private void DrawQuickTells()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "Quick-Tell Templates");
        var macros = _plugin.Configuration.SweetheartMacros;

        foreach (var m in macros)
        {
            if (ImGui.Button($"{m.Title}##{m.Title}"))
            {
                var target = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron
                    : Svc.Targets.Target?.Name.ToString() ?? "";
                if (!string.IsNullOrEmpty(target))
                {
                    var firstName = target.Split(' ')[0];
                    var msg = m.Text.Replace("{name}", firstName);
                    Svc.Commands.ProcessCommand($"/t {target} {msg}");
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled(m.Text.Length > 40 ? m.Text[..40] + "..." : m.Text);
        }

        // Add new
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##NewMacroTitle", "Title", ref _newMacroTitle, 50);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##NewMacroText", "Message ({name} for patron)", ref _newMacroText, 200);
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

    private void DrawGlamourerPresets()
    {
        ImGui.Text("Outfit Presets");
        if (!_plugin.Configuration.EnableGlamourer)
        {
            ImGui.TextDisabled("Enable Glamourer in Settings.");
            return;
        }

        if (ImGui.Button("Open Glamourer Designs", new Vector2(-1, 25)))
            Svc.Commands.ProcessCommand("/glamourer");
    }

    private void DrawEarningsLog()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "Log Earnings");
        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Gil##EarnAmt", ref _earningsAmount, 10000);
        ImGui.SameLine();
        if (ImGui.Button("Log Session Earnings"))
        {
            if (_earningsAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role = StaffRole.Sweetheart,
                    Type = EarningsType.Session,
                    PatronName = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron : "Unknown",
                    Description = "Session",
                    Amount = _earningsAmount,
                });
                _plugin.Configuration.Save();
                _earningsAmount = 0;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Log Tip"))
        {
            if (_earningsAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role = StaffRole.Sweetheart,
                    Type = EarningsType.Tip,
                    PatronName = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron : "Unknown",
                    Description = "Tip",
                    Amount = _earningsAmount,
                });
                _plugin.Configuration.Save();
                _earningsAmount = 0;
            }
        }
    }

    private void DrawPatronNotes()
    {
        ImGui.Text("Patron Notes");
        var patronName = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron : "(no patron)";
        ImGui.TextDisabled($"For: {patronName}");

        if (!string.IsNullOrEmpty(_sessionPatron))
        {
            var notes = _plugin.Configuration.PatronNotes
                .Where(n => n.PatronName == _sessionPatron && n.AuthorRole == StaffRole.Sweetheart)
                .OrderByDescending(n => n.Timestamp).ToList();

            foreach (var n in notes)
            {
                ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}]");
                ImGui.SameLine();
                ImGui.TextWrapped(n.Content);
            }

            ImGui.SetNextItemWidth(-60);
            ImGui.InputTextWithHint("##NewNote", "Add note...", ref _newNoteText, 500);
            ImGui.SameLine();
            if (ImGui.Button("Save"))
            {
                if (!string.IsNullOrWhiteSpace(_newNoteText))
                {
                    _plugin.Configuration.PatronNotes.Add(new PatronNote
                    {
                        PatronName = _sessionPatron,
                        AuthorRole = StaffRole.Sweetheart,
                        AuthorName = _plugin.Configuration.CharacterName,
                        Content = _newNoteText,
                    });
                    _plugin.Configuration.Save();
                    _newNoteText = string.Empty;
                }
            }
        }
    }

    private void DrawPatronHistory()
    {
        ImGui.Text("Patron History");
        var patronName = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron : null;
        if (patronName == null)
        {
            ImGui.TextDisabled("Start a session to see patron history.");
            return;
        }

        var history = _plugin.Configuration.Earnings
            .Where(e => e.PatronName == patronName && e.Role == StaffRole.Sweetheart)
            .OrderByDescending(e => e.Timestamp)
            .Take(10).ToList();

        if (history.Count == 0)
        {
            ImGui.TextDisabled("No history with this patron.");
            return;
        }

        foreach (var e in history)
        {
            ImGui.BulletText($"{e.Timestamp:MM/dd} — {e.Description}: {e.Amount:N0} Gil");
        }
    }
}
