using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
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
    private bool _alert5Fired = false;
    private bool _alert2Fired = false;

    // Patron profile lookup
    private string _shLookupPatronName = string.Empty;

    // Input state
    private string _newNoteText = string.Empty;
    private int _earningsAmount = 0;
    private string _newMacroTitle = string.Empty;
    private string _newMacroText = string.Empty;

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);
    private static readonly Vector4 HeaderBg = new(0.22f, 0.16f, 0.28f, 1f);
    private static readonly Vector4 HeaderHover = new(0.30f, 0.22f, 0.36f, 1f);

    public SweetheartPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features ────────────────────────────────────────────────────────────

    public void DrawContent()
    {
        // Session Timer (always visible)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tier1 = ImRaii.Child("##SHTier1", new Vector2(0, 140f), true))
        {
            ImGui.PopStyleColor();
            if (tier1) DrawSessionTimer();
        }

        ImGui.Spacing();

        using var tabs = ImRaii.TabBar("##SHTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!tabs) return;

        if (ImGui.BeginTabItem("Session##SH"))
        {
            DrawRoomAssignment();
            DrawUpcomingBookings();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Patron##SH"))
        {
            DrawPatronProfile();
            DrawPatronNotes();
            DrawPatronHistory();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Tools##SH"))
        {
            DrawQuickTellButtons();
            DrawEmoteShortcuts();
            DrawServiceRateCard();
            DrawGlamourerPresets();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Earnings##SH"))
        {
            DrawEarningsLog();
            ImGui.Spacing();
            _pingWidget.Draw();
            ImGui.EndTabItem();
        }
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "♥ Sweetheart Settings");
        ImGui.TextDisabled("Configure your macro bank and role preferences.");
        ImGui.Spacing();

        // Card: Macro Bank
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card = ImRaii.Child("##SHMacroCard", new Vector2(0, 220f), true))
        {
            ImGui.PopStyleColor();
            if (!card) return;

            ImGui.TextColored(StyleManager.SectionHeader, "Quick-Tell Macro Bank");
            ImGui.Separator();
            ImGui.Spacing();

            var macros = _plugin.Configuration.SweetheartMacros;

            using (var scroll = ImRaii.Child("##SHMacroList", new Vector2(0, 120f), false))
            {
                for (int i = 0; i < macros.Count; i++)
                {
                    var m = macros[i];
                    ImGui.PushID($"shm{i}");
                    ImGui.Text(m.Title);
                    ImGui.SameLine();
                    ImGui.TextDisabled(m.Text.Length > 40 ? m.Text[..40] + "..." : m.Text);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Del##shmd"))
                    {
                        macros.RemoveAt(i);
                        _plugin.Configuration.Save();
                        ImGui.PopID();
                        break;
                    }
                    ImGui.PopID();
                }
                if (macros.Count == 0)
                    ImGui.TextDisabled("No macros yet. Add one below.");
            }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(100);
            ImGui.InputTextWithHint("##SHMacroT", "Title", ref _newMacroTitle, 50);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##SHMacroM", "Message ({name})", ref _newMacroText, 200);
            ImGui.SameLine();
            if (ImGui.Button("+##SHAddMacro"))
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

    private void DrawSessionTimer()
    {
        if (_sessionActive)
        {
            var elapsed = DateTime.Now - _sessionStart;
            var remaining = TimeSpan.FromMinutes(_sessionDurationMin) - elapsed;

            ImGui.TextColored(StyleManager.SyncOk, $"IN SESSION \u2014 {_sessionPatron}");
            ImGui.Text($"Elapsed:   {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}");

            if (remaining.TotalSeconds > 0)
            {
                var color = remaining.TotalMinutes <= 2 ? new Vector4(1f, 0.2f, 0.2f, 1f)
                          : remaining.TotalMinutes <= 5 ? new Vector4(1f, 0.8f, 0.2f, 1f)
                          : new Vector4(0.8f, 0.8f, 0.8f, 1f);
                ImGui.TextColored(color, $"Remaining: {remaining.Minutes:D2}:{remaining.Seconds:D2}");

                if (remaining.TotalMinutes <= 5 && !_alert5Fired)
                {
                    Svc.Chat.Print($"[Candy Coat] \u26a0 5 minutes remaining with {_sessionPatron}!");
                    _alert5Fired = true;
                }
                if (remaining.TotalMinutes <= 2 && !_alert2Fired)
                {
                    Svc.Chat.Print($"[Candy Coat] \u26a0 2 minutes remaining with {_sessionPatron}!");
                    _alert2Fired = true;
                }
            }
            else
            {
                float pulse = (float)(Math.Sin(ImGui.GetTime() * 2.0f) * 0.5f + 0.5f);
                ImGui.TextColored(new Vector4(1f, pulse, pulse, 1f), "TIME'S UP!");
            }

            ImGui.Spacing();
            if (ImGui.Button("End Session", new Vector2(120, 28)))
                EndSession();
        }
        else
        {
            var target = Svc.Targets.Target;
            if (target != null && string.IsNullOrEmpty(_sessionPatron))
            {
                if (ImGui.Button("Use Target##SH")) _sessionPatron = target.Name.ToString();
                ImGui.SameLine();
            }
            ImGui.SetNextItemWidth(180);
            ImGui.InputTextWithHint("##SHPatron", "Patron Name", ref _sessionPatron, 100);
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("Duration (min)##SH", ref _sessionDurationMin, 15);
            if (_sessionDurationMin < 15) _sessionDurationMin = 15;

            ImGui.Checkbox("Do Not Disturb##SH", ref _dndToggle);
            if (_dndToggle)
                ImGui.TextColored(StyleManager.SyncOk, "DND synced to staff.");

            if (!string.IsNullOrWhiteSpace(_sessionPatron))
            {
                if (ImGui.Button("Start Session", new Vector2(120, 28)))
                {
                    _sessionActive = true;
                    _sessionStart = DateTime.Now;
                    _alert5Fired = false;
                    _alert2Fired = false;
                    _shLookupPatronName = _sessionPatron;
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
        }
    }

    private void EndSession()
    {
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
        ImGui.Spacing();
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0) { ImGui.TextDisabled("No rooms defined. Add in Owner > Room Editor."); ImGui.Spacing(); return; }
        var names = rooms.Select(r => $"{r.Name} ({r.Status})").ToArray();
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("##SHRoomSelect", ref _selectedRoomIndex, names, names.Length);
        ImGui.Spacing();
    }

    private void DrawPatronProfile()
    {
        ImGui.Spacing();

        // Auto-populate from active session
        if (_sessionActive && !string.IsNullOrEmpty(_sessionPatron) && _shLookupPatronName != _sessionPatron)
            _shLookupPatronName = _sessionPatron;

        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##SHProfileName", "Patron Name", ref _shLookupPatronName, 100);

        if (string.IsNullOrWhiteSpace(_shLookupPatronName)) { ImGui.Spacing(); return; }

        var patron = _plugin.Configuration.Patrons
            .FirstOrDefault(p => p.Name.Equals(_shLookupPatronName, StringComparison.OrdinalIgnoreCase));

        if (patron == null)
        {
            ImGui.TextDisabled("Not in patron database.");
            ImGui.Spacing();
            return;
        }

        var cfg = _plugin.Configuration;
        var tier = cfg.GetTier(patron);
        var tierColor = tier switch
        {
            PatronTier.Elite   => new Vector4(1f, 0.85f, 0.2f, 1f),
            PatronTier.Regular => new Vector4(1f, 0.5f, 0.8f, 1f),
            _                  => new Vector4(0.7f, 0.7f, 0.7f, 1f),
        };
        ImGui.TextColored(tierColor, $"[{tier}]");

        if (patron.Status is PatronStatus.Warning or PatronStatus.Blacklisted)
        {
            ImGui.SameLine();
            var statusColor = patron.Status == PatronStatus.Blacklisted ? StyleManager.SyncError : StyleManager.SyncWarn;
            ImGui.TextColored(statusColor, $"[{patron.Status}]");
        }

        ImGui.TextDisabled("RP Hooks:");
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(patron.RpHooks)) ImGui.TextWrapped(patron.RpHooks);
        else ImGui.TextDisabled("None on file");

        ImGui.TextDisabled("Favourite Drink:");
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(patron.FavoriteDrink)) ImGui.Text(patron.FavoriteDrink);
        else ImGui.TextDisabled("\u2014");

        ImGui.TextDisabled("Allergies / Limits:");
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(patron.Allergies)) ImGui.TextWrapped(patron.Allergies);
        else ImGui.TextDisabled("\u2014");

        ImGui.Spacing();
    }

    private void DrawUpcomingBookings()
    {
        ImGui.Spacing();
        var bookings = _plugin.Configuration.Bookings
            .Where(b => b.State != BookingState.CompletedPaid && b.State != BookingState.CompletedUnpaid)
            .OrderBy(b => b.Timestamp)
            .ToList();

        if (bookings.Count == 0) { ImGui.TextDisabled("No upcoming bookings."); ImGui.Spacing(); return; }

        foreach (var b in bookings)
            ImGui.BulletText($"{b.PatronName} | {b.Service} | {b.Room} | {b.Gil:N0} Gil");

        ImGui.Spacing();
    }

    private void DrawQuickTellButtons()
    {
        ImGui.Spacing();
        var macros = _plugin.Configuration.SweetheartMacros;
        if (macros.Count == 0) { ImGui.TextDisabled("No macros. Add them in Settings."); ImGui.Spacing(); return; }
        foreach (var m in macros)
        {
            if (ImGui.Button($"{m.Title}##SHbtn{m.Title}"))
            {
                var target = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron
                    : Svc.Targets.Target?.Name.ToString() ?? "";
                if (!string.IsNullOrEmpty(target))
                {
                    var msg = m.Text.Replace("{name}", target.Split(' ')[0]);
                    Svc.Commands.ProcessCommand($"/t {target} {msg}");
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled(m.Text.Length > 40 ? m.Text[..40] + "..." : m.Text);
        }
        ImGui.Spacing();
    }

    private void DrawEmoteShortcuts()
    {
        ImGui.Spacing();
        EmoteBtn("Comfort",   "/comfort motion");   ImGui.SameLine();
        EmoteBtn("Smile",     "/smile motion");     ImGui.SameLine();
        EmoteBtn("Blow Kiss", "/blowkiss motion");  ImGui.SameLine();
        EmoteBtn("Kneel",     "/kneel motion");

        EmoteBtn("Bow",       "/bow motion");       ImGui.SameLine();
        EmoteBtn("Beckon",    "/beckon motion");    ImGui.SameLine();
        EmoteBtn("Doze",      "/doze motion");      ImGui.SameLine();
        EmoteBtn("Laugh",     "/laugh motion");

        EmoteBtn("Wave",      "/wave motion");      ImGui.SameLine();
        EmoteBtn("Hug",       "/hug motion");       ImGui.SameLine();
        EmoteBtn("Nuzzle",    "/nuzzle motion");    ImGui.SameLine();
        EmoteBtn("Pet",       "/pet motion");

        ImGui.Spacing();
    }

    private static void EmoteBtn(string label, string cmd)
    {
        if (ImGui.Button(label, new Vector2(75, 22))) Svc.Commands.ProcessCommand(cmd);
    }

    private void DrawServiceRateCard()
    {
        ImGui.Spacing();
        var items = _plugin.Configuration.ServiceMenu.Where(s => s.Category == ServiceCategory.Session).ToList();
        if (items.Count == 0) { ImGui.TextDisabled("No session services defined. Add in Owner > Menu Editor."); ImGui.Spacing(); return; }
        foreach (var item in items)
        {
            ImGui.BulletText($"{item.Name} \u2014 {item.Price:N0} Gil");
            if (!string.IsNullOrEmpty(item.Description)) { ImGui.SameLine(); ImGui.TextDisabled($"({item.Description})"); }
        }
        ImGui.Spacing();
    }

    private void DrawGlamourerPresets()
    {
        ImGui.Spacing();
        if (!_plugin.Configuration.EnableGlamourer) { ImGui.TextDisabled("Enable Glamourer in Settings."); ImGui.Spacing(); return; }
        if (ImGui.Button("Open Glamourer Designs", new Vector2(-1, 25))) Svc.Commands.ProcessCommand("/glamourer");
        ImGui.Spacing();
    }

    private void DrawEarningsLog()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Gil##SHEarn", ref _earningsAmount, 10000);
        ImGui.SameLine();
        if (ImGui.Button("Log Session Earnings##SH"))
        {
            if (_earningsAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry { Role = StaffRole.Sweetheart, Type = EarningsType.Session, PatronName = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron : "Unknown", Description = "Session", Amount = _earningsAmount });
                _plugin.Configuration.Save();
                _earningsAmount = 0;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Log Tip##SH"))
        {
            if (_earningsAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry { Role = StaffRole.Sweetheart, Type = EarningsType.Tip, PatronName = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron : "Unknown", Description = "Tip", Amount = _earningsAmount });
                _plugin.Configuration.Save();
                _earningsAmount = 0;
            }
        }
        ImGui.Spacing();
    }

    private void DrawPatronNotes()
    {
        ImGui.Spacing();
        var patronName = !string.IsNullOrEmpty(_sessionPatron) ? _sessionPatron : "(no active session)";
        ImGui.TextDisabled($"For: {patronName}");
        if (!string.IsNullOrEmpty(_sessionPatron))
        {
            var notes = _plugin.Configuration.PatronNotes.Where(n => n.PatronName == _sessionPatron && n.AuthorRole == StaffRole.Sweetheart).OrderByDescending(n => n.Timestamp).ToList();
            foreach (var n in notes) { ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}]"); ImGui.SameLine(); ImGui.TextWrapped(n.Content); }
            ImGui.SetNextItemWidth(-60);
            ImGui.InputTextWithHint("##SHNote", "Add note...", ref _newNoteText, 500);
            ImGui.SameLine();
            if (ImGui.Button("Save##SHNote"))
            {
                if (!string.IsNullOrWhiteSpace(_newNoteText))
                {
                    _plugin.Configuration.PatronNotes.Add(new PatronNote { PatronName = _sessionPatron, AuthorRole = StaffRole.Sweetheart, AuthorName = _plugin.Configuration.CharacterName, Content = _newNoteText });
                    _plugin.Configuration.Save();
                    _newNoteText = string.Empty;
                }
            }
        }
        ImGui.Spacing();
    }

    private void DrawPatronHistory()
    {
        ImGui.Spacing();
        if (string.IsNullOrEmpty(_sessionPatron)) { ImGui.TextDisabled("Start a session to see patron history."); ImGui.Spacing(); return; }
        var history = _plugin.Configuration.Earnings.Where(e => e.PatronName == _sessionPatron && e.Role == StaffRole.Sweetheart).OrderByDescending(e => e.Timestamp).Take(10).ToList();
        if (history.Count == 0) { ImGui.TextDisabled("No history with this patron."); ImGui.Spacing(); return; }
        foreach (var e in history) ImGui.BulletText($"{e.Timestamp:MM/dd} \u2014 {e.Description}: {e.Amount:N0} Gil");
        ImGui.Spacing();
    }
}
