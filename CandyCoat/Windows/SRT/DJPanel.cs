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
        ImGui.TextColored(StyleManager.SectionHeader, "\ud83c\udfb5 DJ Settings");
        ImGui.TextDisabled("Configure stream URL and set preferences.");
        ImGui.Spacing();

        // Card: Stream Config
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card = ImRaii.Child("##DJSettCard", new Vector2(0, 110f), true))
        {
            ImGui.PopStyleColor();
            if (!card) return;

            ImGui.TextColored(StyleManager.SectionHeader, "Default Stream URL");
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
            ImGui.TextColored(StyleManager.SyncOk, $"LIVE \u2014 {total.Hours:D2}:{total.Minutes:D2}:{total.Seconds:D2}");
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
}
