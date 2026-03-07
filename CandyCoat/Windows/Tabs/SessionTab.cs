using Dalamud.Bindings.ImGui;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using CandyCoat.UI;
using Una.Drawing;
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
            ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), $"Capturing: {manager.TargetName}");
            
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

    public Node BuildNode()
    {
        var root    = UdtHelper.CreateFromTemplate("session-tab.xml", "session-layout");
        var dynamic = root.QuerySelector("#session-dynamic")!;

        var manager    = _plugin.SessionManager;
        var statusCard = CandyUI.Card("session-status-card");

        if (manager.IsCapturing)
        {
            statusCard.AppendChild(CandyUI.Label("session-capturing-label",
                $"Capturing: {manager.TargetName}"));
            statusCard.AppendChild(CandyUI.Button("session-stop-btn", "Stop Capture",
                () => manager.StopCapture()));
        }
        else
        {
            statusCard.AppendChild(CandyUI.Muted("session-idle-label", "No active capture."));
            // Input + start button rendered via DrawOverlays()
            statusCard.AppendChild(CandyUI.InputSpacer("session-target-input", 200));
            var btnRow = CandyUI.Row("session-btn-row", 8);
            btnRow.AppendChild(CandyUI.InputSpacer("session-start-btn",  120, 28));
            btnRow.AppendChild(CandyUI.InputSpacer("session-use-target-btn", 160, 28));
            statusCard.AppendChild(btnRow);
        }
        dynamic.AppendChild(statusCard);

        dynamic.AppendChild(CandyUI.Muted("session-hint",
            "Right-click a player in Chat to start a session (requires ChatTwo)."));

        return root;
    }

    public void DrawOverlays()
    {
        var manager = _plugin.SessionManager;
        if (manager.IsCapturing) return;

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
                _manualTargetName = pc.Name.ToString();
        }
    }
}
