using System;
using System.Linq;
using System.Numerics;
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

    // Session state
    private bool _chSessionActive = false;
    private string _chSessionPatron = string.Empty;
    private DateTime _chSessionStart;
    private int _chSessionDurationMin = 30;
    private int _chSelectedRoomIndex = -1;
    private bool _chDndToggle = false;
    private bool _chAlert5Fired = false;
    private bool _chAlert2Fired = false;

    // Patron profile lookup
    private string _chLookupPatronName = string.Empty;

    // Input state
    private string _newMacroTitle = string.Empty;
    private string _newMacroText = string.Empty;
    private string _newNoteText = string.Empty;
    private int _chEarningsAmount = 0;

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
        // Session Timer (always visible)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tier1 = ImRaii.Child("##CHTier1", new Vector2(0, 140f), true))
        {
            ImGui.PopStyleColor();
            if (tier1) DrawSessionTimer();
        }

        ImGui.Spacing();

        using var tabs = ImRaii.TabBar("##CHTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!tabs) return;

        if (ImGui.BeginTabItem("Session##CH"))
        {
            DrawRoomAssignment();
            DrawUpcomingBookings();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Patron##CH"))
        {
            DrawPatronProfile();
            DrawPatronNotes();
            DrawPatronHistory();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Tools##CH"))
        {
            DrawMacroBankButtons();
            DrawEmoteWheel();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Earnings##CH"))
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

    private void DrawSessionTimer()
    {
        if (_chSessionActive)
        {
            var elapsed = DateTime.Now - _chSessionStart;
            var remaining = TimeSpan.FromMinutes(_chSessionDurationMin) - elapsed;

            ImGui.TextColored(StyleManager.SyncOk, $"IN SESSION \u2014 {_chSessionPatron}");
            ImGui.Text($"Elapsed:   {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}");

            if (remaining.TotalSeconds > 0)
            {
                var color = remaining.TotalMinutes <= 2 ? new Vector4(1f, 0.2f, 0.2f, 1f)
                          : remaining.TotalMinutes <= 5 ? new Vector4(1f, 0.8f, 0.2f, 1f)
                          : new Vector4(0.8f, 0.8f, 0.8f, 1f);
                ImGui.TextColored(color, $"Remaining: {remaining.Minutes:D2}:{remaining.Seconds:D2}");

                if (remaining.TotalMinutes <= 5 && !_chAlert5Fired)
                {
                    Svc.Chat.Print($"[Candy Coat] \u26a0 5 minutes remaining with {_chSessionPatron}!");
                    _chAlert5Fired = true;
                }
                if (remaining.TotalMinutes <= 2 && !_chAlert2Fired)
                {
                    Svc.Chat.Print($"[Candy Coat] \u26a0 2 minutes remaining with {_chSessionPatron}!");
                    _chAlert2Fired = true;
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
            if (target != null && string.IsNullOrEmpty(_chSessionPatron))
            {
                if (ImGui.Button("Use Target##CH")) _chSessionPatron = target.Name.ToString();
                ImGui.SameLine();
            }
            ImGui.SetNextItemWidth(180);
            ImGui.InputTextWithHint("##CHPatron", "Patron Name", ref _chSessionPatron, 100);
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("Duration (min)##CH", ref _chSessionDurationMin, 15);
            if (_chSessionDurationMin < 15) _chSessionDurationMin = 15;

            ImGui.Checkbox("Do Not Disturb##CH", ref _chDndToggle);
            if (_chDndToggle)
                ImGui.TextColored(StyleManager.SyncOk, "DND synced to staff.");

            if (!string.IsNullOrWhiteSpace(_chSessionPatron))
            {
                if (ImGui.Button("Start Session", new Vector2(120, 28)))
                {
                    _chSessionActive = true;
                    _chSessionStart = DateTime.Now;
                    _chAlert5Fired = false;
                    _chAlert2Fired = false;
                    _chLookupPatronName = _chSessionPatron;
                    if (_chSelectedRoomIndex >= 0 && _chSelectedRoomIndex < _plugin.Configuration.Rooms.Count)
                    {
                        var room = _plugin.Configuration.Rooms[_chSelectedRoomIndex];
                        room.Status = RoomStatus.Occupied;
                        room.OccupiedBy = _plugin.Configuration.CharacterName;
                        room.PatronName = _chSessionPatron;
                        room.OccupiedSince = DateTime.Now;
                        _plugin.Configuration.Save();
                    }
                    Svc.Chat.Print($"[Candy Coat] Session started with {_chSessionPatron}.");
                }
            }
        }
    }

    private void EndSession()
    {
        if (_chSelectedRoomIndex >= 0 && _chSelectedRoomIndex < _plugin.Configuration.Rooms.Count)
        {
            var room = _plugin.Configuration.Rooms[_chSelectedRoomIndex];
            room.Status = RoomStatus.Available;
            room.OccupiedBy = string.Empty;
            room.PatronName = string.Empty;
            room.OccupiedSince = null;
            _plugin.Configuration.Save();
        }
        Svc.Chat.Print($"[Candy Coat] Session ended with {_chSessionPatron}.");
        _chSessionActive = false;
        _chSessionPatron = string.Empty;
        _chSelectedRoomIndex = -1;
    }

    private void DrawRoomAssignment()
    {
        ImGui.Spacing();
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0) { ImGui.TextDisabled("No rooms defined. Add in Owner > Room Editor."); ImGui.Spacing(); return; }
        var names = rooms.Select(r => $"{r.Name} ({r.Status})").ToArray();
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("##CHRoomSelect", ref _chSelectedRoomIndex, names, names.Length);
        ImGui.Spacing();
    }

    private void DrawPatronProfile()
    {
        ImGui.Spacing();

        // Auto-populate from active session
        if (_chSessionActive && !string.IsNullOrEmpty(_chSessionPatron) && _chLookupPatronName != _chSessionPatron)
            _chLookupPatronName = _chSessionPatron;

        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##CHProfileName", "Patron Name", ref _chLookupPatronName, 100);

        if (string.IsNullOrWhiteSpace(_chLookupPatronName)) { ImGui.Spacing(); return; }

        var patron = _plugin.Configuration.Patrons
            .FirstOrDefault(p => p.Name.Equals(_chLookupPatronName, StringComparison.OrdinalIgnoreCase));

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

    private void DrawMacroBankButtons()
    {
        ImGui.Spacing();
        var macros = _plugin.Configuration.CandyHeartMacros;
        if (macros.Count == 0) { ImGui.TextDisabled("No macros. Add in Settings."); ImGui.Spacing(); return; }
        foreach (var m in macros)
        {
            if (ImGui.Button($"{m.Title}##CHbtn{m.Title}"))
            {
                var target = !string.IsNullOrEmpty(_chSessionPatron) ? _chSessionPatron
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

    private void DrawEmoteWheel()
    {
        ImGui.Spacing();
        EmoteBtn("Wink",      "/wink motion");     ImGui.SameLine();
        EmoteBtn("Blow Kiss", "/blowkiss motion"); ImGui.SameLine();
        EmoteBtn("Dote",      "/dote motion");     ImGui.SameLine();
        EmoteBtn("Beckon",    "/beckon motion");

        EmoteBtn("Smile",    "/smile motion");    ImGui.SameLine();
        EmoteBtn("Kneel",    "/kneel motion");    ImGui.SameLine();
        EmoteBtn("Curtsey",  "/curtsey motion");  ImGui.SameLine();
        EmoteBtn("Cheer",    "/cheer motion");

        EmoteBtn("Laugh",    "/laugh motion");    ImGui.SameLine();
        EmoteBtn("Bow",      "/bow motion");      ImGui.SameLine();
        EmoteBtn("Nod",      "/nod motion");      ImGui.SameLine();
        EmoteBtn("Clap",     "/clap motion");

        ImGui.Spacing();
    }

    private static void EmoteBtn(string label, string cmd)
    {
        if (ImGui.Button(label, new Vector2(75, 22))) Svc.Commands.ProcessCommand(cmd);
    }

    private void DrawEarningsLog()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Gil##CHEarn", ref _chEarningsAmount, 10000);
        ImGui.SameLine();
        if (ImGui.Button("Log Session Earnings##CH"))
        {
            if (_chEarningsAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role = StaffRole.CandyHeart,
                    Type = EarningsType.Session,
                    PatronName = !string.IsNullOrEmpty(_chSessionPatron) ? _chSessionPatron : "Unknown",
                    Description = "Session",
                    Amount = _chEarningsAmount
                });
                _plugin.Configuration.Save();
                _chEarningsAmount = 0;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Log Tip##CH"))
        {
            if (_chEarningsAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role = StaffRole.CandyHeart,
                    Type = EarningsType.Tip,
                    PatronName = !string.IsNullOrEmpty(_chSessionPatron) ? _chSessionPatron : "Unknown",
                    Description = "Tip",
                    Amount = _chEarningsAmount
                });
                _plugin.Configuration.Save();
                _chEarningsAmount = 0;
            }
        }
        ImGui.Spacing();
    }

    private void DrawPatronNotes()
    {
        ImGui.Spacing();
        var patronName = !string.IsNullOrEmpty(_chSessionPatron) ? _chSessionPatron : "(no active session)";
        ImGui.TextDisabled($"For: {patronName}");
        if (!string.IsNullOrEmpty(_chSessionPatron))
        {
            var notes = _plugin.Configuration.PatronNotes
                .Where(n => n.PatronName == _chSessionPatron && n.AuthorRole == StaffRole.CandyHeart)
                .OrderByDescending(n => n.Timestamp)
                .ToList();
            foreach (var n in notes) { ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}]"); ImGui.SameLine(); ImGui.TextWrapped(n.Content); }
            ImGui.SetNextItemWidth(-60);
            ImGui.InputTextWithHint("##CHNoteIn", "Add note...", ref _newNoteText, 500);
            ImGui.SameLine();
            if (ImGui.Button("Save##CHNote"))
            {
                if (!string.IsNullOrWhiteSpace(_newNoteText))
                {
                    _plugin.Configuration.PatronNotes.Add(new PatronNote
                    {
                        PatronName = _chSessionPatron,
                        AuthorRole = StaffRole.CandyHeart,
                        AuthorName = _plugin.Configuration.CharacterName,
                        Content = _newNoteText
                    });
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
        if (string.IsNullOrEmpty(_chSessionPatron)) { ImGui.TextDisabled("Start a session to see patron history."); ImGui.Spacing(); return; }
        var history = _plugin.Configuration.Earnings
            .Where(e => e.PatronName == _chSessionPatron && e.Role == StaffRole.CandyHeart)
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToList();
        if (history.Count == 0) { ImGui.TextDisabled("No history with this patron."); ImGui.Spacing(); return; }
        foreach (var e in history) ImGui.BulletText($"{e.Timestamp:MM/dd} \u2014 {e.Description}: {e.Amount:N0} Gil");
        ImGui.Spacing();
    }
}
