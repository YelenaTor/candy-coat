using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using CandyCoat.Services;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows;

public class SessionWindow : Window, IDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly string _configDir;
    private Node? _root;

    public SessionWindow(SessionManager sessionManager, string configDir) : base("Candy Session##CandySessionWindow")
    {
        _sessionManager = sessionManager;
        _configDir = configDir;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        // Start closed, opened by button
        IsOpen = false;
    }

    public void Dispose()
    {
        _root?.Dispose();
        _root = null;
    }

    private void BuildRoot()
    {
        _root?.Dispose();

        if (!_sessionManager.IsCapturing)
        {
            _root = CandyUI.Column("session-root", 8,
                CandyUI.Card("session-empty-card",
                    CandyUI.Muted("session-empty-lbl", "No active session.")));
            return;
        }

        // Header card: session target name + action buttons as overlay spacers
        var headerCard = CandyUI.Card("session-header-card",
            CandyUI.Row("session-header-row", 8,
                CandyUI.SectionHeader("session-target-lbl",
                    $"Session with: {_sessionManager.TargetName}"),
                CandyUI.InputSpacer("session-copy-spacer", 46, 22),
                CandyUI.InputSpacer("session-save-spacer", 80, 22)
            )
        );

        // Log area — messages rendered via ImGui overlay in DrawOverlays()
        var logCard = CandyUI.Card("session-log-card",
            CandyUI.InputSpacer("session-log-spacer", 0, 200));
        logCard.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow);

        _root = CandyUI.Column("session-root", 8,
            headerCard,
            logCard);
    }

    public override void Draw()
    {
        BuildRoot();

        var region = ImGui.GetContentRegionAvail();
        _root!.Style.Size = new Size((int)region.X, (int)region.Y);

        var pos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _root.Render(ImGui.GetWindowDrawList(), pos);
        ImGui.Dummy(region);

        DrawOverlays();
    }

    private void DrawOverlays()
    {
        if (!_sessionManager.IsCapturing) return;

        // Re-position cursor to header row area for Copy / Save buttons
        var windowPos  = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        // We use an inline ImGui child for the log area so we get scrolling
        ImGui.SetCursorPos(new Vector2(0, 0));
        ImGui.TextUnformatted($"Session with: {_sessionManager.TargetName}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy"))
            ImGui.SetClipboardText(_sessionManager.GetExportText());
        ImGui.SameLine();
        if (ImGui.SmallButton("Save to File"))
            _sessionManager.SaveToFile(_configDir);
        ImGui.Separator();

        var region = ImGui.GetContentRegionAvail();
        using var log = ImRaii.Child("SessionLog", region, true, ImGuiWindowFlags.HorizontalScrollbar);
        if (!log) return;

        foreach (var msg in _sessionManager.Messages)
        {
            ImGui.TextDisabled($"[{msg.Timestamp:HH:mm}]");
            ImGui.SameLine();

            if (msg.IsMe)
                ImGui.TextColored(new Vector4(0.75f, 0.6f, 1f, 1.0f), "[You]:");
            else
                ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.8f, 1.0f), $"[{msg.Sender}]:");

            ImGui.SameLine();
            ImGui.TextUnformatted(msg.Content.TextValue);
        }

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1.0f);
    }
}
