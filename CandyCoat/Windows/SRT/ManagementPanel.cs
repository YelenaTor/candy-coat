using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
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

    public ManagementPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void DrawContent()
    {
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f), "📋 Management Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

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
    }

    private void DrawShiftOverview()
    {
        ImGui.Text("Shift Overview");
        var shiftManager = _plugin.ShiftManager;
        if (shiftManager.CurrentShift != null)
        {
            var d = shiftManager.CurrentShift.Duration;
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f),
                $"You: Clocked In — {d.Hours:D2}:{d.Minutes:D2}:{d.Seconds:D2}");
            ImGui.Text($"Earnings this shift: {shiftManager.CurrentShift.GilEarned:N0} Gil");
        }
        else
        {
            ImGui.TextDisabled("You: Clocked Out");
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Staff roster is local-only. Cross-client sync planned for a future update.");
    }

    private void DrawRoomStatusBoard()
    {
        ImGui.Text("Room Status Board");
        var rooms = _plugin.Configuration.Rooms;
        if (rooms.Count == 0)
        {
            ImGui.TextDisabled("No rooms defined. Set up in Owner > Room Editor.");
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Room status is local-only. Other staff cannot see your room changes until sync is added.");
            return;
        }

        if (ImGui.BeginTable("##RoomBoard", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
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
                    RoomStatus.Available => new Vector4(0.2f, 1f, 0.2f, 1f),
                    RoomStatus.Occupied => new Vector4(1f, 0.4f, 0.4f, 1f),
                    RoomStatus.Reserved => new Vector4(1f, 0.8f, 0.2f, 1f),
                    _ => new Vector4(0.5f, 0.5f, 0.5f, 1f),
                };
                ImGui.TextColored(color, room.Status.ToString());
                ImGui.TableNextColumn(); ImGui.Text(room.OccupiedBy);
                ImGui.TableNextColumn(); ImGui.Text(room.PatronName);
            }
            ImGui.EndTable();
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

        // Display
        for (int i = _incidents.Count - 1; i >= Math.Max(0, _incidents.Count - 15); i--)
        {
            var (time, sev, patron, note) = _incidents[i];
            var sevColor = sev switch
            {
                Severity.Warning => new Vector4(1f, 0.8f, 0.2f, 1f),
                Severity.Critical => new Vector4(1f, 0.3f, 0.3f, 1f),
                _ => new Vector4(0.6f, 0.6f, 0.6f, 1f),
            };

            ImGui.TextColored(sevColor, $"[{time:HH:mm}] [{sev}]");
            ImGui.SameLine();
            if (!string.IsNullOrEmpty(patron))
            {
                ImGui.Text($"({patron})");
                ImGui.SameLine();
            }
            ImGui.TextWrapped(note);
        }
        if (_incidents.Count == 0)
            ImGui.TextDisabled("No incidents logged this session.");
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
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⚠ Avoid using in heavily populated areas (e.g. Limsa, Gridania). Scanning many players may impact performance.");
    }
}
