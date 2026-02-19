using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using ImGuiNET;
using SamplePlugin.Services;

namespace CandyCoat.Windows;

public class SessionWindow : Window, IDisposable
{
    private readonly SessionManager _sessionManager;

    public SessionWindow(SessionManager sessionManager) : base("Candy Session##CandySessionWindow")
    {
        _sessionManager = sessionManager;
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        // Start closed, opened by button
        IsOpen = false; 
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!_sessionManager.IsCapturing)
        {
            ImGui.TextDisabled("No active session.");
            return;
        }

        ImGui.Text($"Session with: {_sessionManager.TargetName}");
        ImGui.Separator();
        
        var region = ImGui.GetContentRegionAvail();
        if (ImGui.BeginChild("SessionLog", region, true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            foreach (var msg in _sessionManager.Messages)
            {
                // Formatting
                ImGui.TextDisabled($"[{msg.Timestamp:HH:mm}]");
                ImGui.SameLine();
                
                if (msg.IsMe)
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), $"[You]:");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.8f, 1.0f), $"[{msg.Sender}]:");
                }
                
                ImGui.SameLine();
                ImGui.TextUnformatted(msg.Content.TextValue);
            }
            
            // Auto-scroll
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            {
                ImGui.SetScrollHereY(1.0f);
            }
            ImGui.EndChild();
        }
    }
}
