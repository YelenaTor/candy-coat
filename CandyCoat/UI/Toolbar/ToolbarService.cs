using System;
using System.Collections.Generic;
using System.Numerics;
using CandyCoat.Data;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Hooks UiBuilder.Draw and renders a screen-anchored vertical toolbar strip
/// via ImGui.GetBackgroundDrawList(). Manages expand/collapse animation, button
/// state, and delegates balloon panel rendering to BalloonService.
/// </summary>
public sealed class ToolbarService : IDisposable
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const float CollapsedWidth = 44f;
    private const float ExpandedWidth  = 160f;
    private const float AnimSpeed      = 18f;

    // -------------------------------------------------------------------------
    // Private fields
    // -------------------------------------------------------------------------

    private readonly IDalamudPluginInterface              _pi;
    private readonly Configuration                       _config;
    private readonly BalloonService                      _balloon;
    private readonly Node                                _toolbarRoot;

    /// <summary>Ordered list of (entry, button) pairs currently in the toolbar.</summary>
    private readonly List<(IToolbarEntry Entry, ToolbarButton Button)> _buttons = new();

    /// <summary>Animated current width (lerps between CollapsedWidth and ExpandedWidth).</summary>
    private float _toolbarWidth = CollapsedWidth;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public ToolbarService(IDalamudPluginInterface pi, Configuration config)
    {
        _pi     = pi;
        _config = config;
        _balloon = new BalloonService(config);

        // Root toolbar node — vertical flex container rendered to the background draw list
        _toolbarRoot = new Node
        {
            Id    = "ToolbarRoot",
            Style = new Style
            {
                Flow            = Flow.Vertical,
                Size            = new Size((int)CollapsedWidth, 16),
                BackgroundColor = new Color("Toolbar.Bg"),
                BorderColor     = new BorderColor(new Color("Toolbar.Border")),
                BorderRadius    = 8f,
                StrokeWidth     = 1f,
                Padding         = new EdgeSize(6),
                Gap             = 2f,
            },
        };

        pi.UiBuilder.Draw += OnDraw;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replaces the current toolbar entries with the given list, filtered by role visibility.
    /// Disposes old buttons and rebuilds the toolbar node tree.
    /// </summary>
    public void SetEntries(List<IToolbarEntry> entries)
    {
        // Dispose existing buttons and clear the root's children
        foreach (var (_, btn) in _buttons)
        {
            btn.Root.Remove();
            btn.Dispose();
        }

        _buttons.Clear();

        foreach (var entry in entries)
        {
            // Role gate: None = always show; otherwise check EnabledRoles flag
            bool visible = entry.Role == StaffRole.None || _config.EnabledRoles.HasFlag(entry.Role);
            if (!visible) continue;

            var button = new ToolbarButton(
                entry.Id,
                entry.Icon,
                entry.Label,
                () => OnButtonClicked(entry)
            );

            _toolbarRoot.AppendChild(button.Root);
            _buttons.Add((entry, button));
        }
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        _pi.UiBuilder.Draw -= OnDraw;

        foreach (var (_, btn) in _buttons)
            btn.Dispose();

        _buttons.Clear();

        _balloon.Dispose();

        if (!_toolbarRoot.IsDisposed)
            _toolbarRoot.Dispose();
    }

    // -------------------------------------------------------------------------
    // Per-frame draw
    // -------------------------------------------------------------------------

    private void OnDraw()
    {
        float delta    = ImGui.GetIO().DeltaTime;
        var   viewport = ImGui.GetMainViewport();

        // ---- Toolbar position -----------------------------------------------
        float height     = GetToolbarHeight();
        Vector2 toolbarPos = CalculateToolbarPos(viewport, height);

        // ---- Hover detection — expand when mouse is over the toolbar AABB ---
        Vector2 mousePos = ImGui.GetIO().MousePos;
        bool hovered =
            mousePos.X >= toolbarPos.X && mousePos.X <= toolbarPos.X + _toolbarWidth &&
            mousePos.Y >= toolbarPos.Y && mousePos.Y <= toolbarPos.Y + height;

        // ---- Animate toolbar width -------------------------------------------
        float targetWidth = hovered ? ExpandedWidth : CollapsedWidth;
        _toolbarWidth = Lerp(_toolbarWidth, targetWidth, delta * AnimSpeed);

        // Snap to avoid perpetual micro-updates
        if (Math.Abs(_toolbarWidth - targetWidth) < 0.5f)
            _toolbarWidth = targetWidth;

        // ---- Update buttons --------------------------------------------------
        bool expanded = _toolbarWidth > CollapsedWidth + 10f;
        foreach (var (_, btn) in _buttons)
        {
            btn.SetExpanded(expanded);
            btn.Update(delta);
        }

        // ---- Update toolbar root node size -----------------------------------
        _toolbarRoot.Style.Size = new Size((int)_toolbarWidth, (int)GetToolbarHeight());

        // ---- Balloon ---------------------------------------------------------
        Vector2 balloonPos = CalculateBalloonPos(toolbarPos);
        _balloon.BalloonScreenPos = balloonPos;
        _balloon.Update(delta);

        // ---- Render ----------------------------------------------------------
        var dl = ImGui.GetBackgroundDrawList();
        _toolbarRoot.Render(dl, toolbarPos);
        _balloon.Render(dl);
        _balloon.DrawGhostWindow();
    }

    // -------------------------------------------------------------------------
    // Button click handler
    // -------------------------------------------------------------------------

    private void OnButtonClicked(IToolbarEntry entry)
    {
        if (_balloon.ActiveEntry?.Id == entry.Id)
        {
            // Same entry — toggle close
            _balloon.SetActiveEntry(null);

            // Clear all active states
            foreach (var (_, btn) in _buttons)
                btn.IsActive = false;

            return;
        }

        // Switch to new entry
        foreach (var (_, btn) in _buttons)
            btn.IsActive = false;

        _balloon.SetActiveEntry(entry);

        // Mark the matching button as active
        foreach (var (e, btn) in _buttons)
        {
            if (e.Id == entry.Id)
                btn.IsActive = true;
        }
    }

    // -------------------------------------------------------------------------
    // Position helpers
    // -------------------------------------------------------------------------

    private float GetToolbarHeight() => _buttons.Count * 44f + 16f;

    private Vector2 CalculateToolbarPos(ImGuiViewportPtr viewport, float height)
    {
        float vx = viewport.Pos.X;
        float vy = viewport.Pos.Y;
        float vw = viewport.Size.X;
        float vh = viewport.Size.Y;

        return _config.ToolbarAnchor switch
        {
            ToolbarAnchor.Left   => new Vector2(vx + 8f,                                  vy + (vh - height) / 2f),
            ToolbarAnchor.Right  => new Vector2(vx + vw - _toolbarWidth - 8f,             vy + (vh - height) / 2f),
            ToolbarAnchor.Top    => new Vector2(vx + (vw - _toolbarWidth) / 2f,           vy + 8f),
            ToolbarAnchor.Bottom => new Vector2(vx + (vw - _toolbarWidth) / 2f,           vy + vh - GetToolbarHeight() - 8f),
            _                    => new Vector2(vx + 8f,                                  vy + (vh - height) / 2f),
        };
    }

    private Vector2 CalculateBalloonPos(Vector2 toolbarPos)
    {
        return _config.ToolbarAnchor switch
        {
            ToolbarAnchor.Left   => new Vector2(toolbarPos.X + _toolbarWidth + 6f,        toolbarPos.Y),
            ToolbarAnchor.Right  => new Vector2(toolbarPos.X - _config.BalloonWidth - 6f, toolbarPos.Y),
            ToolbarAnchor.Top    => new Vector2(toolbarPos.X,                             toolbarPos.Y + GetToolbarHeight() + 6f),
            ToolbarAnchor.Bottom => new Vector2(toolbarPos.X,                             toolbarPos.Y - 500f - 6f),
            _                    => new Vector2(toolbarPos.X + _toolbarWidth + 6f,        toolbarPos.Y),
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static float Lerp(float a, float b, float t) =>
        a + (b - a) * Math.Clamp(t, 0f, 1f);
}
