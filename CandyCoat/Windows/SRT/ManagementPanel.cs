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

    // Incident log
    public enum Severity { Info, Warning, Critical }
    private readonly List<(DateTime Time, Severity Level, string Patron, string Note)> _incidents = new();
    private string _incidentNote = string.Empty;
    private string _incidentPatron = string.Empty;
    private int _incidentSeverity = 0;
    private static readonly string[] SeverityLabels = { "Info", "Warning", "Critical" };

    // Staff roster (local config)
    private string _newStaffName = string.Empty;
    private static readonly string[] RoleLabels;

    // Patron flagging
    private string _flagPatron = string.Empty;
    private string _flagNote = string.Empty;

    static ManagementPanel()
    {
        RoleLabels = Enum.GetValues<StaffRole>().Where(r => r != StaffRole.None).Select(r => r.ToString()).ToArray();
    }

    private readonly StaffPingWidget _pingWidget;

    public ManagementPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    public void DrawContent()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "📋 Management Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

        DrawLiveFloorBoard();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawShiftOverview();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRoomStatusBoard();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawIncidentLog();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPatronFlagging();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPatronNotes();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawCapacity();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        _pingWidget.Draw();
    }

    private void DrawLiveFloorBoard()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "Live Floor Board");
        ImGui.Spacing();

        var nearbyCount = _plugin.LocatorService.GetNearbyCount();
        ImGui.Text($"Nearby Players: ");
        ImGui.SameLine();
        var capacityColor = nearbyCount > 48
            ? new Vector4(1f, 0.8f, 0.2f, 1f)
            : StyleManager.SyncOk;
        ImGui.TextColored(capacityColor, $"{nearbyCount}");
        if (nearbyCount > 48)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "  ⚠ Near capacity");
        }

        ImGui.Spacing();

        var rooms = _plugin.Configuration.Rooms;
        var onlineStaff = _plugin.SyncService.OnlineStaff;

        if (rooms.Count == 0)
        {
            ImGui.TextDisabled("No rooms configured. Add rooms in Owner > Room Editor.");
            return;
        }

        using var table = ImRaii.Table("##FloorBoard", 5,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
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

            // Staff in room — match from synced staff or local character
            ImGui.TableNextColumn();
            if (!string.IsNullOrEmpty(room.OccupiedBy))
            {
                var staffRecord = onlineStaff.Find(s => s.CharacterName == room.OccupiedBy);
                var roleStr = staffRecord != null ? $" [{staffRecord.Role}]" : string.Empty;
                ImGui.Text($"{room.OccupiedBy}{roleStr}");
            }
            else
            {
                ImGui.TextDisabled("—");
            }

            ImGui.TableNextColumn();
            ImGui.Text(string.IsNullOrEmpty(room.PatronName) ? "—" : room.PatronName);

            // Timer since occupied
            ImGui.TableNextColumn();
            if (room.Status == RoomStatus.Occupied && room.OccupiedSince.HasValue)
            {
                var elapsed = DateTime.Now - room.OccupiedSince.Value;
                ImGui.TextColored(
                    elapsed.TotalMinutes > 60 ? new Vector4(1f, 0.8f, 0.2f, 1f) : new Vector4(0.8f, 0.8f, 0.8f, 1f),
                    $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}");
            }
            else
            {
                ImGui.TextDisabled("—");
            }
        }

        // Staff not in any room
        ImGui.Spacing();
        var assignedNames = rooms.Where(r => !string.IsNullOrEmpty(r.OccupiedBy))
                                  .Select(r => r.OccupiedBy).ToHashSet();
        var unassigned = onlineStaff.Where(s => !assignedNames.Contains(s.CharacterName)).ToList();
        if (unassigned.Count > 0)
        {
            ImGui.TextDisabled($"Unassigned online staff ({unassigned.Count}):");
            foreach (var s in unassigned)
            {
                var dnd = s.IsDnd ? " [DND]" : string.Empty;
                ImGui.BulletText($"{s.CharacterName} [{s.Role}]{dnd}");
            }
        }
    }

    private void DrawShiftOverview()
    {
        ImGui.Text("Shift Overview");
        var shiftManager = _plugin.ShiftManager;
        if (shiftManager.CurrentShift != null)
        {
            var d = shiftManager.CurrentShift.Duration;
            ImGui.TextColored(StyleManager.SyncOk,
                $"You: Clocked In — {d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}");
            ImGui.Text($"Earnings this shift: {shiftManager.CurrentShift.GilEarned:N0} Gil");
        }
        else
        {
            ImGui.TextDisabled("You: Clocked Out");
        }

        ImGui.Spacing();
        if (_plugin.SyncService.IsConnected)
            ImGui.TextColored(StyleManager.SyncOk, "🟢 Staff roster synced.");
        else
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Staff roster is local-only. Enable sync in Settings.");
    }

    private void DrawRoomStatusBoard()
    {
        ImGui.Text("Room Status Board");
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0)
        {
            ImGui.TextDisabled("No rooms defined. Set up in Owner > Room Editor.");
            if (_plugin.SyncService.IsConnected)
                ImGui.TextColored(StyleManager.SyncOk, "🟢 Room status synced.");
            else
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Room status is local-only. Enable sync in Settings.");
            return;
        }

        using var table = ImRaii.Table("##RoomBoard", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
        if (!table) return;

        ImGui.TableSetupColumn("Room", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Staff", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Patron", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableHeadersRow();

        foreach (var room in rooms)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(room.Name);
            ImGui.TableNextColumn();
            var color = room.Status switch
            {
                RoomStatus.Available => StyleManager.SyncOk,
                RoomStatus.Occupied  => new Vector4(1f, 0.4f, 0.4f, 1f),
                RoomStatus.Reserved  => new Vector4(1f, 0.8f, 0.2f, 1f),
                _                    => new Vector4(0.5f, 0.5f, 0.5f, 1f),
            };
            ImGui.TextColored(color, room.Status.ToString());
            ImGui.TableNextColumn(); ImGui.Text(room.OccupiedBy);
            ImGui.TableNextColumn(); ImGui.Text(room.PatronName);
        }
    }

    private void DrawIncidentLog()
    {
        ImGui.Text("Incident Log");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(80);
        ImGui.Combo("##Sev", ref _incidentSeverity, SeverityLabels, SeverityLabels.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputTextWithHint("##IncPatron", "Patron", ref _incidentPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-50);
        ImGui.InputTextWithHint("##IncNote", "What happened...", ref _incidentNote, 500);
        ImGui.SameLine();
        if (ImGui.Button("Log"))
        {
            if (!string.IsNullOrWhiteSpace(_incidentNote))
            {
                _incidents.Add((DateTime.Now, (Severity)_incidentSeverity, _incidentPatron, _incidentNote));
                _incidentNote = string.Empty;
                _incidentPatron = string.Empty;
            }
        }

        // Display — 3-column table avoids TextWrapped-after-SameLine layout corruption
        if (_incidents.Count == 0)
        {
            ImGui.TextDisabled("No incidents logged this session.");
            return;
        }

        using var incTable = ImRaii.Table("##IncidentLog", 3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(0, 160));
        if (!incTable) return;

        ImGui.TableSetupColumn("Time / Severity", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Patron", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        for (int i = _incidents.Count - 1; i >= Math.Max(0, _incidents.Count - 15); i--)
        {
            var (time, sev, patron, note) = _incidents[i];
            var sevColor = sev switch
            {
                Severity.Warning  => new Vector4(1f, 0.8f, 0.2f, 1f),
                Severity.Critical => new Vector4(1f, 0.3f, 0.3f, 1f),
                _                 => new Vector4(0.6f, 0.6f, 0.6f, 1f),
            };

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextColored(sevColor, $"[{time:HH:mm}] [{sev}]");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(patron);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(note);
        }
    }

    private void DrawPatronFlagging()
    {
        ImGui.Text("Patron Flagging");
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##FlagPat", "Patron Name", ref _flagPatron, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-80);
        ImGui.InputTextWithHint("##FlagNote", "Reason", ref _flagNote, 200);
        ImGui.SameLine();
        if (ImGui.Button("Flag"))
        {
            if (!string.IsNullOrWhiteSpace(_flagPatron))
            {
                var patron = _plugin.Configuration.Patrons.FirstOrDefault(p => p.Name == _flagPatron);
                if (patron != null)
                {
                    patron.Status = PatronStatus.Warning;
                    patron.Notes += $"\n[{DateTime.Now:MM/dd HH:mm} FLAGGED] {_flagNote}";
                }
                else
                {
                    var newPatron = new Patron
                    {
                        Name = _flagPatron,
                        Status = PatronStatus.Warning,
                        Notes = $"[{DateTime.Now:MM/dd HH:mm} FLAGGED] {_flagNote}",
                    };
                    _plugin.Configuration.Patrons.Add(newPatron);
                }
                _plugin.Configuration.Save();
                _flagPatron = string.Empty;
                _flagNote = string.Empty;
            }
        }
    }

    private void DrawPatronNotes()
    {
        ImGui.Text("All Patron Notes (Downstream)");
        ImGui.TextDisabled("Management sees notes from all roles.");

        var recentNotes = _plugin.Configuration.PatronNotes
            .OrderByDescending(n => n.Timestamp)
            .Take(15).ToList();

        if (recentNotes.Count == 0)
        {
            ImGui.TextDisabled("No patron notes yet.");
            return;
        }

        foreach (var n in recentNotes)
        {
            ImGui.TextDisabled($"[{n.Timestamp:MM/dd HH:mm}] [{n.AuthorRole}]");
            ImGui.SameLine();
            ImGui.Text($"{n.PatronName}:");
            ImGui.SameLine();
            ImGui.TextWrapped(n.Content);
        }
    }

    private void DrawCapacity()
    {
        ImGui.Text("Venue Capacity");
        var nearbyCount = _plugin.LocatorService.GetNearbyCount();
        ImGui.Text($"Nearby Players: {nearbyCount}");
        if (nearbyCount > 48)
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Venue is near capacity.");
        }
        ImGui.TextDisabled("Avoid using in heavily populated areas. Scanning many players may impact performance.");
    }
}
