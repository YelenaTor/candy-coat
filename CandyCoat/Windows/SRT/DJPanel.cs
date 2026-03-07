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

public class DJPanel : IToolboxPanel
{
    public string Name => "DJ";
    public StaffRole Role => StaffRole.DJ;

    private readonly Plugin _plugin;

    // Performance state
    private bool _performanceActive = false;
    private DateTime _performanceStart;
    private readonly List<(string Song, TimeSpan Duration)> _segments = new();
    private DateTime _segmentStart;
    private readonly List<(string Song, bool Played)> _setlist = new();
    private string _newSong = string.Empty;
    private readonly List<(string Patron, string Song, int Status)> _requests = new();
    private string _reqPatron = string.Empty;
    private string _reqSong = string.Empty;
    private string _streamUrl = string.Empty;
    private int _tipAmount = 0;
    private string _tipPatron = string.Empty;

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);

    public DJPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features ────────────────────────────────────────────────────────────

    public void DrawContent()
    {
        // Tier 1 — Performance Timer (fixed ~120px)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tier1 = ImRaii.Child("##DJTier1", new Vector2(0, 120f), true))
        {
            ImGui.PopStyleColor();
            if (tier1) DrawSetTimer();
        }

        ImGui.Spacing();

        using var tabs = ImRaii.TabBar("##DJTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!tabs) return;

        if (ImGui.BeginTabItem("Set##DJ"))
        {
            DrawSetlist();
            DrawRequestQueue();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Engage##DJ"))
        {
            DrawStreamLink();
            DrawCrowdMacros();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Tips##DJ"))
        {
            DrawTipsTracker();
            DrawPerformanceHistory();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Ping##DJ"))
        {
            ImGui.Spacing();
            _pingWidget.Draw();
            ImGui.EndTabItem();
        }
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "\ud83c\udfb5 DJ Settings");
        ImGui.TextDisabled("Configure stream URL and set preferences.");
        ImGui.Spacing();

        // Card: Stream Config
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card = ImRaii.Child("##DJSettCard", new Vector2(0, 110f), true))
        {
            ImGui.PopStyleColor();
            if (!card) return;

            ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "Default Stream URL");
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##DJStreamSett", "Twitch/YouTube URL", ref _streamUrl, 300);
            ImGui.TextDisabled("Paste URL here to pre-fill the stream link field.");
        }
    }

    // ─── Private Draw Helpers ────────────────────────────────────────────────

    private void DrawSetTimer()
    {
        if (_performanceActive)
        {
            var total = DateTime.Now - _performanceStart;
            var segElapsed = DateTime.Now - _segmentStart;
            ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), $"LIVE \u2014 {total.Hours:D2}:{total.Minutes:D2}:{total.Seconds:D2}");
            ImGui.Text($"Segment: {segElapsed.Minutes:D2}:{segElapsed.Seconds:D2}");

            if (ImGui.Button("Mark Segment##DJ"))
            {
                _segments.Add(($"Segment {_segments.Count + 1}", DateTime.Now - _segmentStart));
                _segmentStart = DateTime.Now;
            }
            ImGui.SameLine();
            if (ImGui.Button("End Set##DJ"))
            {
                _segments.Add(($"Final", DateTime.Now - _segmentStart));
                _performanceActive = false;
                Svc.Chat.Print($"[Candy Coat] Set ended. Total: {total.Hours:D2}:{total.Minutes:D2}");
            }
            if (_segments.Count > 0)
            {
                ImGui.TextDisabled("Segments:");
                foreach (var (seg, dur) in _segments) ImGui.BulletText($"{seg}: {dur.Minutes:D2}:{dur.Seconds:D2}");
            }
        }
        else
        {
            ImGui.TextDisabled("Not performing.");
            if (ImGui.Button("Start Set", new Vector2(120, 28)))
            {
                _performanceActive = true;
                _performanceStart = DateTime.Now;
                _segmentStart = DateTime.Now;
                _segments.Clear();
            }
        }
    }

    private void DrawSetlist()
    {
        ImGui.Spacing();
        for (int i = 0; i < _setlist.Count; i++)
        {
            var (song, played) = _setlist[i];
            var check = played;
            if (ImGui.Checkbox($"##djsl{i}", ref check)) _setlist[i] = (song, check);
            ImGui.SameLine();
            if (played) ImGui.TextDisabled(song); else ImGui.Text(song);
        }
        ImGui.SetNextItemWidth(-50);
        ImGui.InputTextWithHint("##DJNewSong", "Add song...", ref _newSong, 200);
        ImGui.SameLine();
        if (ImGui.Button("+##DJAddSong")) { if (!string.IsNullOrWhiteSpace(_newSong)) { _setlist.Add((_newSong, false)); _newSong = string.Empty; } }
        ImGui.Spacing();
    }

    private void DrawRequestQueue()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##DJReqP", "From", ref _reqPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##DJReqS", "Song", ref _reqSong, 200);
        ImGui.SameLine();
        if (ImGui.Button("Add##DJReq")) { if (!string.IsNullOrWhiteSpace(_reqSong)) { _requests.Add((_reqPatron, _reqSong, 0)); _reqPatron = string.Empty; _reqSong = string.Empty; } }
        for (int i = 0; i < _requests.Count; i++)
        {
            var (patron, song, status) = _requests[i];
            var label = status switch { 0 => "\u23f3", 1 => "\u2713", 2 => "\u25b6", 3 => "\u2717", _ => "?" };
            ImGui.Text($"{label} {patron}: {song}");
            ImGui.SameLine();
            ImGui.PushID($"djrq{i}");
            if (status == 0) { if (ImGui.SmallButton("Accept")) _requests[i] = (patron, song, 1); ImGui.SameLine(); if (ImGui.SmallButton("Reject")) _requests[i] = (patron, song, 3); }
            if (status == 1 && ImGui.SmallButton("Played")) _requests[i] = (patron, song, 2);
            ImGui.PopID();
        }
        ImGui.Spacing();
    }

    private void DrawStreamLink()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-100);
        ImGui.InputTextWithHint("##DJStreamURL", "Twitch/YouTube URL", ref _streamUrl, 300);
        ImGui.SameLine();
        if (ImGui.Button("Share##DJ")) { if (!string.IsNullOrEmpty(_streamUrl)) Svc.Commands.ProcessCommand($"/party \ud83c\udfb5 Tune in: {_streamUrl}"); }
        ImGui.Spacing();
    }

    private void DrawCrowdMacros()
    {
        ImGui.Spacing();
        if (ImGui.Button("Hype!", new Vector2(80, 22))) Svc.Commands.ProcessCommand("/say Make some noise! \ud83c\udfb5");
        ImGui.SameLine();
        if (ImGui.Button("Requests Open", new Vector2(120, 22))) Svc.Commands.ProcessCommand("/say Requests are OPEN! /tell me your song! \ud83c\udfb6");
        ImGui.SameLine();
        if (ImGui.Button("Last Song!", new Vector2(100, 22))) Svc.Commands.ProcessCommand("/say Last song coming up! \ud83c\udfb5");
        ImGui.SameLine();
        if (ImGui.Button("Emote##DJ", new Vector2(60, 22))) Svc.Commands.ProcessCommand("/cheer motion");
        ImGui.Spacing();
    }

    private void DrawTipsTracker()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##DJTipAmt", ref _tipAmount, 5000);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##DJTipP", "From", ref _tipPatron, 100);
        ImGui.SameLine();
        if (ImGui.Button("Log Tip##DJ"))
        {
            if (_tipAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry { Role = StaffRole.DJ, Type = EarningsType.Tip, PatronName = string.IsNullOrWhiteSpace(_tipPatron) ? "Unknown" : _tipPatron, Description = "DJ Tip", Amount = _tipAmount });
                _plugin.Configuration.Save();
                _tipAmount = 0; _tipPatron = string.Empty;
            }
        }
        ImGui.Spacing();
    }

    private void DrawPerformanceHistory()
    {
        ImGui.Spacing();
        var history = _plugin.Configuration.Earnings.Where(e => e.Role == StaffRole.DJ).OrderByDescending(e => e.Timestamp).Take(10).ToList();
        if (history.Count == 0) { ImGui.TextDisabled("No history."); ImGui.Spacing(); return; }
        foreach (var e in history) ImGui.BulletText($"{e.Timestamp:MM/dd HH:mm} \u2014 {e.Description}: {e.Amount:N0} Gil");
        ImGui.Spacing();
    }

    // ─── Una.Drawing ─────────────────────────────────────────────────────────

    private int _djActiveTab = 0;
    private static readonly string[] DjTabs = ["Set", "Engage", "Tips", "Ping"];

    public Node BuildNode()
    {
        var root    = UdtHelper.CreateFromTemplate("srt-dj.xml", "dj-layout");
        var dynamic = root.QuerySelector("#srt-dj-dynamic")!;
        Node content = _djActiveTab switch {
            0 => BuildDjTabSet(),
            1 => BuildDjTabEngage(),
            2 => BuildDjTabTips(),
            _ => BuildDjTabPing(),
        };
        var col = CandyUI.Column("dj-root", 6);
        col.AppendChild(CandyUI.SectionHeader("dj-timer-hdr", "Performance Timer"));
        col.AppendChild(CandyUI.InputSpacer("dj-timer-sp", 0, 120));
        col.AppendChild(CandyUI.Separator("dj-timer-sep"));
        col.AppendChild(CandyUI.TabContainer("dj-tabs", DjTabs, _djActiveTab,
            idx => { _djActiveTab = idx; }, content));
        dynamic.AppendChild(col);
        return root;
    }

    private Node BuildDjTabSet()
    {
        var col = CandyUI.Column("dj-set", 6);
        col.AppendChild(CandyUI.SectionHeader("dj-setlist-hdr", "Setlist"));
        col.AppendChild(CandyUI.InputSpacer("dj-setlist-sp", 0, 28));

        if (_setlist.Count > 0)
        {
            var card = CandyUI.Card("dj-setlist-card");
            for (int i = 0; i < _setlist.Count; i++)
            {
                var (song, played) = _setlist[i];
                card.AppendChild(CandyUI.Label($"dj-song-{i}",
                    (played ? "[x] " : "[ ] ") + song, 12));
            }
            col.AppendChild(card);
        }

        col.AppendChild(CandyUI.Separator("dj-set-sep1"));
        col.AppendChild(CandyUI.SectionHeader("dj-requests-hdr", "Request Queue"));
        col.AppendChild(CandyUI.InputSpacer("dj-req-sp", 0, 28));

        if (_requests.Count > 0)
        {
            var card = CandyUI.Card("dj-req-card");
            for (int i = 0; i < _requests.Count; i++)
            {
                var (patron, song, status) = _requests[i];
                var label = status switch { 0 => "\u23f3", 1 => "\u2713", 2 => "\u25b6", 3 => "\u2717", _ => "?" };
                card.AppendChild(CandyUI.Label($"dj-req-{i}", $"{label} {patron}: {song}", 12));
            }
            col.AppendChild(card);
        }
        return col;
    }

    private Node BuildDjTabEngage()
    {
        var col = CandyUI.Column("dj-engage", 6);
        col.AppendChild(CandyUI.SectionHeader("dj-stream-hdr", "Stream Link"));
        col.AppendChild(CandyUI.InputSpacer("dj-stream-sp", 0, 28));
        col.AppendChild(CandyUI.Separator("dj-engage-sep1"));
        col.AppendChild(CandyUI.SectionHeader("dj-crowd-hdr", "Crowd Macros"));
        var row = CandyUI.Row("dj-crowd-row", 4,
            CandyUI.Button("dj-hype-btn",     "Hype!",           () => Svc.Commands.ProcessCommand("/say Make some noise! \ud83c\udfb5")),
            CandyUI.Button("dj-requests-btn", "Requests Open",   () => Svc.Commands.ProcessCommand("/say Requests are OPEN! /tell me your song! \ud83c\udfb6")),
            CandyUI.Button("dj-last-btn",     "Last Song!",      () => Svc.Commands.ProcessCommand("/say Last song coming up! \ud83c\udfb5")),
            CandyUI.SmallButton("dj-emote-btn","Emote",          () => Svc.Commands.ProcessCommand("/cheer motion"))
        );
        col.AppendChild(row);
        return col;
    }

    private Node BuildDjTabTips()
    {
        var col = CandyUI.Column("dj-tips", 6);
        col.AppendChild(CandyUI.SectionHeader("dj-tips-hdr", "Log Tips"));
        col.AppendChild(CandyUI.InputSpacer("dj-tips-sp", 0, 28));
        col.AppendChild(CandyUI.Separator("dj-tips-sep1"));
        col.AppendChild(CandyUI.SectionHeader("dj-hist-hdr", "Performance History"));

        var history = _plugin.Configuration.Earnings
            .Where(e => e.Role == StaffRole.DJ)
            .OrderByDescending(e => e.Timestamp).Take(10).ToList();
        if (history.Count == 0)
        {
            col.AppendChild(CandyUI.Muted("dj-hist-empty", "No history."));
        }
        else
        {
            var card = CandyUI.Card("dj-hist-card");
            for (int i = 0; i < history.Count; i++)
            {
                var e = history[i];
                card.AppendChild(CandyUI.Label($"dj-hist-{i}",
                    $"{e.Timestamp:MM/dd HH:mm} — {e.Description}: {e.Amount:N0} Gil", 12));
            }
            col.AppendChild(card);
        }
        return col;
    }

    private Node BuildDjTabPing()
    {
        var col = CandyUI.Column("dj-ping-tab", 6);
        col.AppendChild(CandyUI.Muted("dj-ping-note", "Staff ping widget below."));
        return col;
    }

    public Node BuildSettingsNode()
    {
        var root    = UdtHelper.CreateFromTemplate("srt-dj-settings.xml", "dj-settings-layout");
        var dynamic = root.QuerySelector("#srt-dj-settings-dynamic")!;
        var col = CandyUI.Column("dj-settings", 8);
        col.AppendChild(CandyUI.SectionHeader("dj-settings-hdr", "DJ Settings"));
        col.AppendChild(CandyUI.Muted("dj-settings-desc", "Configure stream URL and set preferences."));
        col.AppendChild(CandyUI.Separator("dj-settings-sep1"));

        var streamCard = CandyUI.Card("dj-settings-stream-card");
        streamCard.AppendChild(CandyUI.SectionHeader("dj-settings-stream-hdr", "Default Stream URL"));
        streamCard.AppendChild(CandyUI.InputSpacer("dj-settings-stream-sp", 0, 28));
        streamCard.AppendChild(CandyUI.Muted("dj-settings-stream-hint",
            "Paste URL here to pre-fill the stream link field.", 11));
        col.AppendChild(streamCard);
        dynamic.AppendChild(col);
        return root;
    }

    public void DrawOverlays()
    {
        DrawSetTimer();
        // setlist add input
        ImGui.SetNextItemWidth(-50);
        ImGui.InputTextWithHint("##DJNewSong", "Add song...", ref _newSong, 200);
        ImGui.SameLine();
        if (ImGui.Button("+##DJAddSong"))
        {
            if (!string.IsNullOrWhiteSpace(_newSong))
            {
                _setlist.Add((_newSong, false));
                _newSong = string.Empty;
            }
        }
        // request add inputs
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##DJReqP", "From", ref _reqPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##DJReqS", "Song", ref _reqSong, 200);
        ImGui.SameLine();
        if (ImGui.Button("Add##DJReq"))
        {
            if (!string.IsNullOrWhiteSpace(_reqSong))
            {
                _requests.Add((_reqPatron, _reqSong, 0));
                _reqPatron = string.Empty;
                _reqSong   = string.Empty;
            }
        }
        // stream link input
        ImGui.SetNextItemWidth(-100);
        ImGui.InputTextWithHint("##DJStreamURL", "Twitch/YouTube URL", ref _streamUrl, 300);
        ImGui.SameLine();
        if (ImGui.Button("Share##DJ"))
        {
            if (!string.IsNullOrEmpty(_streamUrl))
                Svc.Commands.ProcessCommand($"/party \ud83c\udfb5 Tune in: {_streamUrl}");
        }
        // tips log inputs
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##DJTipAmt", ref _tipAmount, 5000);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##DJTipP", "From", ref _tipPatron, 100);
        ImGui.SameLine();
        if (ImGui.Button("Log Tip##DJ"))
        {
            if (_tipAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role        = StaffRole.DJ,
                    Type        = EarningsType.Tip,
                    PatronName  = string.IsNullOrWhiteSpace(_tipPatron) ? "Unknown" : _tipPatron,
                    Description = "DJ Tip",
                    Amount      = _tipAmount
                });
                _plugin.Configuration.Save();
                _tipAmount  = 0;
                _tipPatron  = string.Empty;
            }
        }
    }

    public void DrawSettingsOverlays()
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##DJStreamSett", "Twitch/YouTube URL", ref _streamUrl, 300);
    }
}
