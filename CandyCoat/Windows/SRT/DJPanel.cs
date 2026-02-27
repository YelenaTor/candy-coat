using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.UI;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class DJPanel : IToolboxPanel
{
    public string Name => "DJ";
    public StaffRole Role => StaffRole.DJ;

    private readonly Plugin _plugin;

    // Set timer
    private bool _performanceActive = false;
    private DateTime _performanceStart;
    private readonly List<(string Song, TimeSpan Duration)> _segments = new();
    private DateTime _segmentStart;

    // Setlist
    private readonly List<(string Song, bool Played)> _setlist = new();
    private string _newSong = string.Empty;

    // Request queue
    private readonly List<(string Patron, string Song, int Status)> _requests = new(); // 0=pending 1=accepted 2=played 3=rejected
    private string _reqPatron = string.Empty;
    private string _reqSong = string.Empty;

    // Stream
    private string _streamUrl = string.Empty;

    // Tips
    private int _tipAmount = 0;
    private string _tipPatron = string.Empty;

    public DJPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void DrawContent()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "🎵 DJ Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

        DrawSetTimer();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawSetlist();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRequestQueue();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawStreamLink();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawCrowdMacros();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawTipsTracker();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPerformanceHistory();
    }

    private void DrawSetTimer()
    {
        ImGui.Text("Performance Timer");
        ImGui.Spacing();

        if (_performanceActive)
        {
            var total = DateTime.Now - _performanceStart;
            var segElapsed = DateTime.Now - _segmentStart;

            ImGui.TextColored(StyleManager.SyncOk,
                $"LIVE — {total.Hours:D2}:{total.Minutes:D2}:{total.Seconds:D2}");
            ImGui.Text($"Current segment: {segElapsed.Minutes:D2}:{segElapsed.Seconds:D2}");

            if (ImGui.Button("Mark Segment"))
            {
                _segments.Add(($"Segment {_segments.Count + 1}", DateTime.Now - _segmentStart));
                _segmentStart = DateTime.Now;
            }
            ImGui.SameLine();
            if (ImGui.Button("End Set"))
            {
                _segments.Add(($"Final", DateTime.Now - _segmentStart));
                _performanceActive = false;
                Svc.Chat.Print($"[Candy Coat] Set ended. Total: {total.Hours:D2}:{total.Minutes:D2}");
            }

            // Show segments
            if (_segments.Count > 0)
            {
                ImGui.TextDisabled("Segments:");
                foreach (var (song, dur) in _segments)
                    ImGui.BulletText($"{song}: {dur.Minutes:D2}:{dur.Seconds:D2}");
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
        ImGui.Text("Setlist");
        for (int i = 0; i < _setlist.Count; i++)
        {
            var (song, played) = _setlist[i];
            var check = played;
            if (ImGui.Checkbox($"##sl{i}", ref check))
                _setlist[i] = (song, check);
            ImGui.SameLine();
            if (played)
                ImGui.TextDisabled(song);
            else
                ImGui.Text(song);
        }

        ImGui.SetNextItemWidth(-50);
        ImGui.InputTextWithHint("##NewSong", "Add song...", ref _newSong, 200);
        ImGui.SameLine();
        if (ImGui.Button("+##AddSong"))
        {
            if (!string.IsNullOrWhiteSpace(_newSong))
            {
                _setlist.Add((_newSong, false));
                _newSong = string.Empty;
            }
        }
    }

    private void DrawRequestQueue()
    {
        ImGui.Text("Request Queue");

        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##ReqP", "From", ref _reqPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##ReqS", "Song", ref _reqSong, 200);
        ImGui.SameLine();
        if (ImGui.Button("Add Req"))
        {
            if (!string.IsNullOrWhiteSpace(_reqSong))
            {
                _requests.Add((_reqPatron, _reqSong, 0));
                _reqPatron = string.Empty;
                _reqSong = string.Empty;
            }
        }

        for (int i = 0; i < _requests.Count; i++)
        {
            var (patron, song, status) = _requests[i];
            var label = status switch { 0 => "⏳", 1 => "✓", 2 => "▶", 3 => "✗", _ => "?" };
            ImGui.Text($"{label} {patron}: {song}");
            ImGui.SameLine();
            ImGui.PushID($"rq{i}");
            if (status == 0)
            {
                if (ImGui.SmallButton("Accept")) _requests[i] = (patron, song, 1);
                ImGui.SameLine();
                if (ImGui.SmallButton("Reject")) _requests[i] = (patron, song, 3);
            }
            if (status == 1 && ImGui.SmallButton("Played")) _requests[i] = (patron, song, 2);
            ImGui.PopID();
        }
    }

    private void DrawStreamLink()
    {
        ImGui.Text("Stream Link");
        ImGui.SetNextItemWidth(-100);
        ImGui.InputTextWithHint("##StreamURL", "Twitch/YouTube URL", ref _streamUrl, 300);
        ImGui.SameLine();
        if (ImGui.Button("Share"))
        {
            if (!string.IsNullOrEmpty(_streamUrl))
                Svc.Commands.ProcessCommand($"/party 🎵 Tune in: {_streamUrl}");
        }
    }

    private void DrawCrowdMacros()
    {
        ImGui.Text("Crowd Engagement");
        if (ImGui.Button("Hype!", new Vector2(80, 22)))
            Svc.Commands.ProcessCommand("/say Make some noise! 🎵");
        ImGui.SameLine();
        if (ImGui.Button("Requests Open", new Vector2(120, 22)))
            Svc.Commands.ProcessCommand("/say Requests are OPEN! /tell me your song! 🎶");
        ImGui.SameLine();
        if (ImGui.Button("Last Song!", new Vector2(100, 22)))
            Svc.Commands.ProcessCommand("/say Last song coming up! 🎵");
        ImGui.SameLine();
        if (ImGui.Button("Emote", new Vector2(60, 22)))
            Svc.Commands.ProcessCommand("/cheer motion");
    }

    private void DrawTipsTracker()
    {
        ImGui.Text("Tips");
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##TipAmt", ref _tipAmount, 5000);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##TipP", "From", ref _tipPatron, 100);
        ImGui.SameLine();
        if (ImGui.Button("Log Tip##DJ"))
        {
            if (_tipAmount > 0)
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role = StaffRole.DJ,
                    Type = EarningsType.Tip,
                    PatronName = string.IsNullOrWhiteSpace(_tipPatron) ? "Unknown" : _tipPatron,
                    Description = "DJ Tip",
                    Amount = _tipAmount,
                });
                _plugin.Configuration.Save();
                _tipAmount = 0;
                _tipPatron = string.Empty;
            }
        }
    }

    private void DrawPerformanceHistory()
    {
        ImGui.Text("Performance History");
        var history = _plugin.Configuration.Earnings
            .Where(e => e.Role == StaffRole.DJ)
            .OrderByDescending(e => e.Timestamp)
            .Take(10).ToList();

        if (history.Count == 0)
        {
            ImGui.TextDisabled("No history.");
            return;
        }

        foreach (var e in history)
            ImGui.BulletText($"{e.Timestamp:MM/dd HH:mm} — {e.Description}: {e.Amount:N0} Gil");
    }
}
