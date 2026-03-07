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

    private const float CollapsedWidth  = 44f;
    private const float ExpandedWidth   = 132f;
    private const float AnimSpeed       = 18f;
    private const float DragHandleSize  = 32f;

    // -------------------------------------------------------------------------
    // Private fields
    // -------------------------------------------------------------------------

    private readonly IDalamudPluginInterface              _pi;
    private readonly Configuration                       _config;
    private readonly BalloonService                      _balloon;
    private readonly Node                                _toolbarRoot;
    private readonly Node                                _dragHandle;

    /// <summary>Ordered list of (entry, button) pairs currently in the toolbar.</summary>
    private readonly List<(IToolbarEntry Entry, ToolbarButton Button)> _buttons = new();

    /// <summary>Animated current width (lerps between CollapsedWidth and ExpandedWidth).</summary>
    private float _toolbarWidth = CollapsedWidth;

    // Drag-handle state
    private bool  _isDragging        = false;
    private float _dragStartMouseAxis = 0f;
    private float _dragStartOffset    = 0f;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public ToolbarService(IDalamudPluginInterface pi, Configuration config)
    {
        _pi     = pi;
        _config = config;
        _balloon = new BalloonService(config);

        // Drag handle — grip-vertical icon at the top of the toolbar strip
        _dragHandle = new Node
        {
            Id        = "ToolbarDragHandle",
            NodeValue = "\uF7A4", // FontAwesome grip-vertical
            Style     = new Style
            {
                Flow        = Flow.Horizontal,
                AutoSize    = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Size        = new Size(0, (int)DragHandleSize),
                Font        = 2,
                FontSize    = 12,
                TextAlign   = Anchor.MiddleCenter,
                Color       = new Color("Toolbar.Icon"),
                Padding     = new EdgeSize(4),
            },
            Stylesheet  = new Stylesheet(new List<Stylesheet.StyleDefinition>
            {
                new("#ToolbarDragHandle:hover", new Style { Color = new Color("Toolbar.IconActive") }),
            }),
        };

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

        _toolbarRoot.AppendChild(_dragHandle);

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

        // ---- Drag handle interaction -----------------------------------------
        Vector2 mousePos = ImGui.GetIO().MousePos;
        var handleBounds = _dragHandle.Bounds.ContentRect;
        bool overHandle  = handleBounds.Width > 0
            && mousePos.X >= handleBounds.X1 && mousePos.X <= handleBounds.X2
            && mousePos.Y >= handleBounds.Y1 && mousePos.Y <= handleBounds.Y2;

        bool isVertical = _config.ToolbarAnchor == ToolbarAnchor.Left
                       || _config.ToolbarAnchor == ToolbarAnchor.Right;

        if (overHandle && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _isDragging         = true;
            _dragStartMouseAxis = isVertical ? mousePos.Y : mousePos.X;
            _dragStartOffset    = _config.ToolbarOffset;
        }

        if (_isDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                float curAxis = isVertical ? mousePos.Y : mousePos.X;
                float maxOff  = isVertical
                    ? (viewport.Size.Y - height) / 2f - 8f
                    : (viewport.Size.X - _toolbarWidth) / 2f - 8f;
                _config.ToolbarOffset = Math.Clamp(
                    _dragStartOffset + (curAxis - _dragStartMouseAxis),
                    -maxOff, maxOff);
            }
            else
            {
                _isDragging = false;
                _config.Save();
            }
        }

        // ---- Hover detection — expand when mouse is over the toolbar AABB ---
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

    private float GetToolbarHeight() => _buttons.Count * 44f + DragHandleSize + 16f;

    private Vector2 CalculateToolbarPos(ImGuiViewportPtr viewport, float height)
    {
        float vx  = viewport.Pos.X;
        float vy  = viewport.Pos.Y;
        float vw  = viewport.Size.X;
        float vh  = viewport.Size.Y;
        float off = _config.ToolbarOffset;

        return _config.ToolbarAnchor switch
        {
            ToolbarAnchor.Left   => new Vector2(vx + 8f,                        vy + (vh - height) / 2f + off),
            ToolbarAnchor.Right  => new Vector2(vx + vw - _toolbarWidth - 8f,   vy + (vh - height) / 2f + off),
            ToolbarAnchor.Top    => new Vector2(vx + (vw - _toolbarWidth) / 2f + off, vy + 8f),
            ToolbarAnchor.Bottom => new Vector2(vx + (vw - _toolbarWidth) / 2f + off, vy + vh - height - 8f),
            _                    => new Vector2(vx + 8f,                        vy + (vh - height) / 2f + off),
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
