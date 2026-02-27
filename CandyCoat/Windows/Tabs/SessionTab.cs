using Dalamud.Bindings.ImGui;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using CandyCoat.UI;

namespace CandyCoat.Windows.Tabs;

public class SessionTab : ITab
{
    private readonly Plugin _plugin;
    private string _manualTargetName = string.Empty;

    public string Name => "Session Capture";

    public SessionTab(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(Name);
        if (!tab) return;
        DrawContent();
    }

    public void DrawContent()
    {
        ImGui.TextUnformatted("Session Capture Control");
        ImGui.Separator();

        var manager = _plugin.SessionManager;

        if (manager.IsCapturing)
        {
            ImGui.TextColored(StyleManager.SyncOk, $"Capturing: {manager.TargetName}");
            
            if (ImGui.Button("Stop Capture"))
            {
                manager.StopCapture();
            }
            
            ImGui.SameLine();
            ImGui.TextDisabled("(Session Window should be open)");
        }
        else
        {
            ImGui.Text("Target Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##ManualSessionTarget", ref _manualTargetName, 100);

            if (ImGui.Button("Start Capture"))
            {
                if (!string.IsNullOrWhiteSpace(_manualTargetName))
                {
                    manager.StartCapture(_manualTargetName);
                    foreach (var w in _plugin.WindowSystem.Windows) 
                    {
                        if (w.WindowName.StartsWith("Candy Session")) 
                        {
                            w.IsOpen = true;
                            break;
                        }
                    }
                }
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Use Current Target"))
            {
                var target = Svc.Targets.Target;
                if (target is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc)
                {
                    _manualTargetName = pc.Name.ToString();
                }
            }
        }
        
        ImGui.Separator();
        ImGui.TextWrapped("Note: You can also right-click a player in Chat to start a session if ChatTwo is installed.");
    }
}
