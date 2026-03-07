using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Manages the sliding balloon panel that expands from the toolbar when an entry is activated.
/// Renders via Una.Drawing and hosts a ghost ImGui window for DrawOverlays() input widgets.
/// </summary>
public sealed class BalloonService : IDisposable
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const float BalloonHeight   = 480f;
    private const float BalloonMaxWidth = 340f;
    private const float AnimSpeed       = 18f;

    // -------------------------------------------------------------------------
    // Public properties
    // -------------------------------------------------------------------------

    /// <summary>Currently active entry, or null when the balloon is closed.</summary>
    public IToolbarEntry? ActiveEntry { get; private set; }

    /// <summary>True when the balloon is visible (alpha above threshold).</summary>
    public bool IsVisible => _openAlpha > 0.01f;

    /// <summary>Screen position of the balloon's top-left corner. Set by ToolbarService each frame.</summary>
    public Vector2 BalloonScreenPos { get; set; }

    // -------------------------------------------------------------------------
    // Private fields
    // -------------------------------------------------------------------------

    private readonly Configuration _config;
    private readonly TabStrip      _tabStrip;

    private readonly Node _balloonRoot;
    private readonly Node _contentArea;

    /// <summary>Animation alpha: 0 = fully closed, 1 = fully open.</summary>
    private float _openAlpha;

    /// <summary>Animated pixel width of the balloon.</summary>
    private float _openWidth;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public BalloonService(Configuration config)
    {
        _config   = config;
        _tabStrip = new TabStrip();

        // Content area — fills remaining vertical space in the balloon
        _contentArea = new Node
        {
            Id    = "BalloonContent",
            Style = new Style
            {
                Flow     = Flow.Vertical,
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
            },
        };

        // Root balloon node — vertical stack: [TabStrip, ContentArea]
        _balloonRoot = new Node
        {
            Id    = "BalloonRoot",
            Style = new Style
            {
                Flow            = Flow.Vertical,
                Size            = new Size((int)BalloonMaxWidth, (int)BalloonHeight),
                BackgroundColor = new Color("Balloon.Bg"),
                BorderColor     = new BorderColor(new Color("Balloon.Border")),
                BorderWidth     = new EdgeSize(1),
                BorderRadius    = 6f,
                Opacity         = 0f,
                IsVisible       = false,
            },
        };

        _balloonRoot.AppendChild(_tabStrip.Root);
        _balloonRoot.AppendChild(_contentArea);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Toggle the balloon for the given entry.
    /// Passing the same entry as the currently active one closes the balloon.
    /// Passing a different entry (or null) opens/switches/closes accordingly.
    /// </summary>
    public void SetActiveEntry(IToolbarEntry? entry)
    {
        if (entry != null && ActiveEntry?.Id == entry.Id)
        {
            // Same entry clicked — close
            ActiveEntry = null;
            return;
        }

        ActiveEntry = entry;

        // Clear content area children
        while (_contentArea.ChildNodes.Count > 0)
            _contentArea.ChildNodes[0].Remove();

        if (entry == null) return;

        // Rebuild content for the new entry
        var panelNode = entry.BuildPanel();
        _contentArea.AppendChild(panelNode);

        // Update tab strip with a single tab representing this entry
        _tabStrip.SetTabs(
            new List<(string Id, string Label)> { (entry.Id, entry.Label) },
            entry.Id,
            _ => { /* Single-tab strip; no tab switching needed here */ }
        );
    }

    /// <summary>
    /// Advance the open/close animation. Must be called once per frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        float targetAlpha = ActiveEntry != null ? 1f : 0f;
        _openAlpha = Lerp(_openAlpha, targetAlpha, deltaTime * AnimSpeed);

        float targetWidth = ActiveEntry != null ? BalloonMaxWidth : 0f;
        _openWidth = Lerp(_openWidth, targetWidth, deltaTime * AnimSpeed);

        // Snap to fully closed to avoid perpetual micro-updates
        if (_openAlpha < 0.005f && ActiveEntry == null)
        {
            _openAlpha = 0f;
            _openWidth = 0f;
        }

        // Update root node visibility and size
        _balloonRoot.Style.IsVisible = IsVisible;
        _balloonRoot.Style.Opacity   = _openAlpha;
        _balloonRoot.Style.Size      = new Size((int)Math.Max(1, _openWidth), (int)BalloonHeight);
    }

    /// <summary>
    /// Renders the balloon Una.Drawing node tree. Must be called each frame after Update().
    /// Only renders when the balloon is visible.
    /// </summary>
    public void Render(ImDrawListPtr drawList)
    {
        if (!IsVisible) return;

        _balloonRoot.Render(drawList, BalloonScreenPos);
    }

    /// <summary>
    /// Opens a pinned, invisible ImGui window at BalloonScreenPos to provide a valid ImGui
    /// context for the active entry's DrawOverlays() call.
    /// Must be called each frame inside a valid ImGui render context.
    /// </summary>
    public void DrawGhostWindow()
    {
        if (ActiveEntry == null || _openAlpha <= 0.01f) return;

        const ImGuiWindowFlags GhostFlags =
            ImGuiWindowFlags.NoDecoration       |
            ImGuiWindowFlags.NoBackground        |
            ImGuiWindowFlags.NoMove              |
            ImGuiWindowFlags.NoSavedSettings     |
            ImGuiWindowFlags.NoFocusOnAppearing  |
            ImGuiWindowFlags.NoNav               |
            ImGuiWindowFlags.NoScrollbar         |
            ImGuiWindowFlags.NoScrollWithMouse;

        ImGui.SetNextWindowPos(BalloonScreenPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(_openWidth, BalloonHeight), ImGuiCond.Always);

        if (!ImGui.Begin("##BalloonGhost", GhostFlags))
        {
            ImGui.End();
            return;
        }

        try
        {
            ActiveEntry.DrawOverlays();
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Warning(ex, "[BalloonService] DrawOverlays threw an exception.");
        }

        ImGui.End();
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        _tabStrip.Dispose();

        if (!_balloonRoot.IsDisposed)
            _balloonRoot.Dispose();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static float Lerp(float a, float b, float t) =>
        a + (b - a) * Math.Clamp(t, 0f, 1f);
}
