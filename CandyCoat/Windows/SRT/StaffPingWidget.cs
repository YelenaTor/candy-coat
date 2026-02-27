using System.Linq;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

/// <summary>
/// Compact "Staff Ping" widget injected into every SRT panel.
/// Lets any staff member quickly send a templated or freeform tell to an online colleague.
/// </summary>
public class StaffPingWidget
{
    private readonly Plugin _plugin;
    private int _selectedStaffIndex = 0;
    private int _selectedTemplate = 0;
    private string _customMessage = string.Empty;

    private static readonly string[] Templates =
    {
        "Room Ready",
        "Needs Escort",
        "Incident Here",
        "Help Needed",
        "[Custom]",
    };

    public StaffPingWidget(Plugin plugin) => _plugin = plugin;

    public void Draw()
    {
        if (!ImGui.CollapsingHeader("📣 Staff Ping")) return;

        var staff = _plugin.SyncService.OnlineStaff;
        if (staff.Count == 0)
        {
            ImGui.TextDisabled("No staff online. Enable sync in Settings.");
            return;
        }

        var names = staff.Select(s =>
            $"{s.CharacterName} [{s.Role}]{(s.IsDnd ? " [DND]" : string.Empty)}").ToArray();

        if (_selectedStaffIndex >= names.Length) _selectedStaffIndex = 0;

        ImGui.SetNextItemWidth(200);
        ImGui.Combo("##PingTarget", ref _selectedStaffIndex, names, names.Length);
        ImGui.SameLine();

        ImGui.SetNextItemWidth(110);
        ImGui.Combo("##PingTemplate", ref _selectedTemplate, Templates, Templates.Length);

        bool isCustom = _selectedTemplate == Templates.Length - 1;
        if (isCustom)
        {
            ImGui.SetNextItemWidth(-80);
            ImGui.InputTextWithHint("##PingCustom", "Type message...", ref _customMessage, 200);
            ImGui.SameLine();
        }

        if (ImGui.Button("Send##Ping"))
        {
            var target = staff[_selectedStaffIndex].CharacterName;
            var message = isCustom ? _customMessage : Templates[_selectedTemplate];
            if (!string.IsNullOrWhiteSpace(message))
            {
                Svc.Commands.ProcessCommand($"/t {target} [CC] {message}");
            }
        }
    }
}
