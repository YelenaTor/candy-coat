using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;

namespace CandyCoat.Windows.Tabs;

public class SettingsTab : ITab
{
    private readonly Plugin _plugin;

    public string Name => "Settings";

    public SettingsTab(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(Name);
        if (!tab) return;

        ImGui.TextUnformatted("Global Config");
        ImGui.Spacing();

        var enableGlam = _plugin.Configuration.EnableGlamourer;
        if (ImGui.Checkbox("Enable Glamourer Integration", ref enableGlam))
        {
            _plugin.Configuration.EnableGlamourer = enableGlam;
            _plugin.Configuration.Save();
        }

        var enableChat = _plugin.Configuration.EnableChatTwo;
        if (ImGui.Checkbox("Enable ChatTwo Integration", ref enableChat))
        {
            _plugin.Configuration.EnableChatTwo = enableChat;
            _plugin.Configuration.Save();
        }

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Custom Macros");
        ImGui.TextWrapped("Create Quick-Tells that appear on patron profiles. Use {name} to insert their first name naturally.");

        if (ImGui.Button("Add New Macro"))
        {
            _plugin.Configuration.Macros.Add(new MacroTemplate { Title = "New Macro", Text = "Hello {name}!" });
            _plugin.Configuration.Save();
        }
        ImGui.Spacing();

        for (int i = 0; i < _plugin.Configuration.Macros.Count; i++)
        {
            var m = _plugin.Configuration.Macros[i];
            ImGui.PushID($"Macro{i}");
            
            var title = m.Title;
            var text = m.Text;
            
            if (ImGui.InputText("Title", ref title, 50))
            {
                m.Title = title;
                _plugin.Configuration.Save();
            }
            if (ImGui.InputTextMultiline("Text", ref text, 500, new Vector2(-1, 60)))
            {
                m.Text = text;
                _plugin.Configuration.Save();
            }
            if (ImGui.Button("Delete"))
            {
                _plugin.Configuration.Macros.RemoveAt(i);
                _plugin.Configuration.Save();
                ImGui.PopID();
                break;
            }
            ImGui.Separator();
            ImGui.PopID();
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Management Access");
        ImGui.Spacing();
        
        if (_plugin.Configuration.IsManagementModeEnabled)
        {
            ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), "✔️ Management Mode Active");
        }
        else
        {
            var code = "";
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputTextWithHint("##mgmtcode", "Enter Passcode", ref code, 20, ImGuiInputTextFlags.Password))
            {
                if (code == "YXIII")
                {
                    _plugin.Configuration.IsManagementModeEnabled = true;
                    _plugin.Configuration.Save();
                }
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(Locked)");
        }
    }
}
