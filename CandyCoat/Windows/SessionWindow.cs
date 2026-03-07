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
        var logSpacer = CandyUI.InputSpacer("session-log-spacer", 0, 0);
        logSpacer.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow);
        var logCard = CandyUI.Card("session-log-card", logSpacer);
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

        // Una.Drawing bounds are in screen space; ImGui cursor is window-content-relative.
        var origin = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();

        var spacerCopy = _root!.QuerySelector("#session-copy-spacer");
        var spacerSave = _root!.QuerySelector("#session-save-spacer");
        var spacerLog  = _root!.QuerySelector("#session-log-spacer");

        if (spacerCopy != null)
        {
            var r = spacerCopy.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            if (ImGui.SmallButton("Copy"))
                ImGui.SetClipboardText(_sessionManager.GetExportText());
        }

        if (spacerSave != null)
        {
            var r = spacerSave.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            if (ImGui.SmallButton("Save to File"))
                _sessionManager.SaveToFile(_configDir);
        }

        if (spacerLog != null)
        {
            var r = spacerLog.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            using var log = ImRaii.Child("SessionLog",
                new Vector2(r.Width, r.Height), false,
                ImGuiWindowFlags.HorizontalScrollbar);
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
}
