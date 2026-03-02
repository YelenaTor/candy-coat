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

public class ManagementPanel : IToolboxPanel
{
    private readonly Plugin _plugin;

    public string Name => "Management";
    public StaffRole Role => StaffRole.Management;

    public enum Severity { Info, Warning, Critical }
    private readonly List<(DateTime Time, Severity Level, string Patron, string Note)> _incidents = new();
    private string _incidentNote = string.Empty;
    private string _incidentPatron = string.Empty;
    private int _incidentSeverity = 0;
    private static readonly string[] SeverityLabels = { "Info", "Warning", "Critical" };

    private string _flagPatron = string.Empty;
    private string _flagNote = string.Empty;

    // Settings state
    private int _capacityWarning = 48;
    private bool _capacityInit = false;

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);

    public ManagementPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features ────────────────────────────────────────────────────────────

    public void DrawContent()
    {
        // Inner tab bar as per plan
        using var innerTabs = ImRaii.TabBar("##MgmtTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!innerTabs) return;

        if (ImGui.BeginTabItem("Floor##Mgmt"))
        {
            DrawLiveFloorBoard();
            DrawCapacity();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Incidents##Mgmt"))
        {
            DrawIncidentLog();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Patrons##Mgmt"))
        {
            DrawPatronFlagging();
            ImGui.Spacing();
            DrawPatronNotes();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Shift##Mgmt"))
        {
            DrawShiftOverview();
            ImGui.Spacing();
            _pingWidget.Draw();
            ImGui.EndTabItem();
        }
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "\ud83d\udccb Management Settings");
        ImGui.TextDisabled("Configure capacity thresholds and roster defaults.");
        ImGui.Spacing();

        // Card: Capacity Thresholds
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card = ImRaii.Child("##MgmtCapCard", new Vector2(0, 100f), true))
        {
            ImGui.PopStyleColor();
            if (!card) return;

            if (!_capacityInit) { _capacityWarning = 48; _capacityInit = true; }

            ImGui.TextColored(StyleManager.SectionHeader, "Capacity Thresholds");
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.SetNextItemWidth(80);
            ImGui.InputInt("Near-capacity warning at##Mgmt", ref _capacityWarning, 1);
            if (_capacityWarning < 1) _capacityWarning = 1;
            ImGui.TextDisabled("Players nearby before showing capacity alert.");
        }
    }

    // ─── Private Draw Helpers ────────────────────────────────────────────────

    private void DrawLiveFloorBoard()
    {
        ImGui.Spacing();
        var nearbyCount = _plugin.LocatorService.GetNearbyCount();
        ImGui.Text("Nearby Players: ");
        ImGui.SameLine();
        var capacityColor = nearbyCount > _capacityWarning ? new Vector4(1f, 0.8f, 0.2f, 1f) : StyleManager.SyncOk;
        ImGui.TextColored(capacityColor, $"{nearbyCount}");
        if (nearbyCount > _capacityWarning) { ImGui.SameLine(); ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "  \u26a0 Near capacity"); }
        ImGui.Spacing();

        var rooms = _plugin.Configuration.Rooms;
        var onlineStaff = _plugin.SyncService.OnlineStaff;

        if (rooms.Count == 0) { ImGui.TextDisabled("No rooms configured. Add rooms in Owner > Room Editor."); return; }

        using var table = ImRaii.Table("##FloorBoard", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        ImGui.TableSetupColumn("Room",   ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Staff",  ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Patron", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Time",   ImGuiTableColumnFlags.WidthFixed, 65);
        ImGui.TableHeadersRow();

        foreach (var room in rooms)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(room.Name);
            ImGui.TableNextColumn();
            var statusColor = room.Status switch
            {
                RoomStatus.Available   => StyleManager.SyncOk,
                RoomStatus.Occupied    => new Vector4(1f, 0.45f, 0.45f, 1f),
                RoomStatus.Reserved    => new Vector4(1f, 0.8f, 0.2f, 1f),
                RoomStatus.Maintenance => new Vector4(0.6f, 0.6f, 0.6f, 1f),
                _                      => Vector4.One,
            };
            ImGui.TextColored(statusColor, room.Status.ToString());
            ImGui.TableNextColumn();
            if (!string.IsNullOrEmpty(room.OccupiedBy))
            {
                var staffRecord = onlineStaff.Find(s => s.CharacterName == room.OccupiedBy);
                var roleStr = staffRecord != null ? $" [{staffRecord.Role}]" : string.Empty;
                ImGui.Text($"{room.OccupiedBy}{roleStr}");
            }
            else { ImGui.TextDisabled("\u2014"); }
            ImGui.TableNextColumn();
            ImGui.Text(string.IsNullOrEmpty(room.PatronName) ? "\u2014" : room.PatronName);
            ImGui.TableNextColumn();
            if (room.Status == RoomStatus.Occupied && room.OccupiedSince.HasValue)
            {
                var elapsed = DateTime.Now - room.OccupiedSince.Value;
                ImGui.TextColored(elapsed.TotalMinutes > 60 ? new Vector4(1f, 0.8f, 0.2f, 1f) : new Vector4(0.8f, 0.8f, 0.8f, 1f), $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}");
            }
            else { ImGui.TextDisabled("\u2014"); }
        }

        ImGui.Spacing();
        var assignedNames = rooms.Where(r => !string.IsNullOrEmpty(r.OccupiedBy)).Select(r => r.OccupiedBy).ToHashSet();
        var unassigned = onlineStaff.Where(s => !assignedNames.Contains(s.CharacterName)).ToList();
        if (unassigned.Count > 0)
        {
            ImGui.TextDisabled($"Unassigned online staff ({unassigned.Count}):");
            foreach (var s in unassigned) ImGui.BulletText($"{s.CharacterName} [{s.Role}]{(s.IsDnd ? " [DND]" : "")}");
        }
    }

    private void DrawCapacity()
    {
        ImGui.Spacing();
        var nearbyCount = _plugin.LocatorService.GetNearbyCount();
        if (nearbyCount > _capacityWarning) ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "\u26a0 Venue is near capacity.");
        ImGui.TextDisabled("Avoid using in heavily populated areas outside the venue.");
    }

    private void DrawShiftOverview()
    {
        ImGui.Spacing();
        var shiftManager = _plugin.ShiftManager;
        if (shiftManager.CurrentShift != null)
        {
            var d = shiftManager.CurrentShift.Duration;
            ImGui.TextColored(StyleManager.SyncOk, $"You: Clocked In \u2014 {d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}");
            ImGui.Text($"Earnings this shift: {shiftManager.CurrentShift.GilEarned:N0} Gil");
        }
        else
        {
            ImGui.TextDisabled("You: Clocked Out");
        }
        ImGui.Spacing();
        if (_plugin.SyncService.IsConnected)
            ImGui.TextColored(StyleManager.SyncOk, "\ud83d\udfe2 Staff roster synced.");
        else
            ImGui.TextColored(StyleManager.SyncWarn, "\u26a0 Staff roster is local-only.");
    }

    private void DrawIncidentLog()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("##MgmtSev", ref _incidentSeverity, SeverityLabels, SeverityLabels.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##MgmtIncPatron", "Patron", ref _incidentPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-50);
        ImGui.InputTextWithHint("##MgmtIncNote", "What happened...", ref _incidentNote, 500);
        ImGui.SameLine();
        if (ImGui.Button("Log##Mgmt"))
        {
            if (!string.IsNullOrWhiteSpace(_incidentNote))
            {
                _incidents.Add((DateTime.Now, (Severity)_incidentSeverity, _incidentPatron, _incidentNote));
                _incidentNote = string.Empty;
                _incidentPatron = string.Empty;
            }
        }
        if (_incidents.Count == 0) { ImGui.TextDisabled("No incidents logged this session."); return; }

        using var incTable = ImRaii.Table("##IncidentLog", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 160));
        if (!incTable) return;
        ImGui.TableSetupColumn("Time / Severity", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Patron", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        for (int i = _incidents.Count - 1; i >= System.Math.Max(0, _incidents.Count - 15); i--)
        {
            var (time, sev, patron, note) = _incidents[i];
            var sevColor = sev switch { Severity.Warning => new Vector4(1f, 0.8f, 0.2f, 1f), Severity.Critical => new Vector4(1f, 0.3f, 0.3f, 1f), _ => new Vector4(0.6f, 0.6f, 0.6f, 1f) };
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextColored(sevColor, $"[{time:HH:mm}] [{sev}]");
            ImGui.TableNextColumn(); ImGui.TextUnformatted(patron);
            ImGui.TableNextColumn(); ImGui.TextWrapped(note);
        }
    }

    private void DrawPatronFlagging()
    {
        ImGui.Spacing();
        ImGui.Text("Flag Patron");
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##MgmtFlagPat", "Patron Name", ref _flagPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-80);
        ImGui.InputTextWithHint("##MgmtFlagNote", "Reason", ref _flagNote, 200);
        ImGui.SameLine();
        if (ImGui.Button("Flag##Mgmt"))
        {
            if (!string.IsNullOrWhiteSpace(_flagPatron))
            {
                var patron = _plugin.Configuration.Patrons.FirstOrDefault(p => p.Name == _flagPatron);
                if (patron != null) { patron.Status = PatronStatus.Warning; patron.Notes += $"\n[{DateTime.Now:MM/dd HH:mm} FLAGGED] {_flagNote}"; }
                else { _plugin.Configuration.Patrons.Add(new Patron { Name = _flagPatron, Status = PatronStatus.Warning, Notes = $"[{DateTime.Now:MM/dd HH:mm} FLAGGED] {_flagNote}" }); }
                _plugin.Configuration.Save();
                _flagPatron = string.Empty;
                _flagNote = string.Empty;
            }
        }
        ImGui.Spacing();
    }

    private void DrawPatronNotes()
    {
        ImGui.Text("All Patron Notes");
        ImGui.TextDisabled("Management sees notes from all roles.");
        var recentNotes = _plugin.Configuration.PatronNotes.OrderByDescending(n => n.Timestamp).Take(15).ToList();
        if (recentNotes.Count == 0) { ImGui.TextDisabled("No patron notes yet."); return; }
        foreach (var n in recentNotes)
        {
            ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}] [{n.AuthorRole}]");
            ImGui.SameLine();
            ImGui.Text($"{n.PatronName}:");
            ImGui.SameLine();
            ImGui.TextWrapped(n.Content);
        }
    }
}
