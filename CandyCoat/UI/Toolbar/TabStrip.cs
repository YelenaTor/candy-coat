using System;
using System.Collections.Generic;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// A horizontal tab strip rendered at the top of the balloon panel.
/// Tabs flow horizontally. Since Una.Drawing has no Wrap layout property,
/// the strip uses a single horizontal row; overflow is clipped by the
/// fixed-height container.
/// </summary>
public sealed class TabStrip : IDisposable
{
    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>The root Una.Drawing node — append this to a parent node.</summary>
    public Node Root { get; }

    // -------------------------------------------------------------------------
    // Private fields
    // -------------------------------------------------------------------------

    private readonly Node _tabRow;

    /// <summary>Maps tab id → tab wrapper node for fast active-state updates.</summary>
    private readonly Dictionary<string, Node> _tabNodes = new();

    private string? _activeId;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public TabStrip()
    {
        // Inner row — all tabs laid out horizontally
        _tabRow = new Node
        {
            Id    = "TabStripRow",
            Style = new Style
            {
                Flow    = Flow.Horizontal,
                Gap     = 0f,
                Padding = new EdgeSize(0),
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
            },
        };

        // Separator line below the tab row
        var separator = new Node
        {
            Id    = "TabStripSeparator",
            Style = new Style
            {
                Size            = new Size(0, 1),
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                BackgroundColor = new Color("Balloon.Separator"),
            },
        };

        // Outer wrapper — vertical stack of [row + separator]
        Root = new Node
        {
            Id    = "TabStripRoot",
            Style = new Style
            {
                Flow            = Flow.Vertical,
                Gap             = 0f,
                BackgroundColor = new Color("Balloon.TabStripBg"),
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
            },
        };

        Root.AppendChild(_tabRow);
        Root.AppendChild(separator);
    }

    // -------------------------------------------------------------------------
    // Public methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds all tab nodes from the provided list.
    /// Previous tab nodes are disposed and replaced.
    /// </summary>
    /// <param name="tabs">Ordered list of (id, display label) pairs.</param>
    /// <param name="activeId">The tab id that should appear selected.</param>
    /// <param name="onTabClicked">Callback invoked with the tab id when a tab is clicked.</param>
    public void SetTabs(List<(string Id, string Label)> tabs, string activeId, Action<string> onTabClicked)
    {
        // Dispose old tab nodes
        foreach (var node in _tabNodes.Values)
            node.Dispose();

        _tabNodes.Clear();
        _tabRow.Clear();

        _activeId = activeId;

        foreach (var (tabId, tabLabel) in tabs)
        {
            // Capture loop variable to avoid closure bug
            var capturedId = tabId;

            bool isActive = string.Equals(tabId, activeId, StringComparison.Ordinal);

            var tabNode = BuildTabNode(capturedId, tabLabel, isActive);
            tabNode.OnClick += _ => onTabClicked(capturedId);

            _tabRow.AppendChild(tabNode);
            _tabNodes[tabId] = tabNode;
        }
    }

    /// <summary>
    /// Updates the active tab highlight without rebuilding the entire strip.
    /// No-op if the requested id is already active or not found.
    /// </summary>
    /// <param name="id">The tab id to make active.</param>
    public void SetActiveTab(string id)
    {
        if (string.Equals(_activeId, id, StringComparison.Ordinal)) return;

        // Deactivate previous
        if (_activeId != null && _tabNodes.TryGetValue(_activeId, out var prevNode))
            ApplyInactiveStyle(prevNode, _activeId);

        _activeId = id;

        // Activate new
        if (_tabNodes.TryGetValue(id, out var nextNode))
            ApplyActiveStyle(nextNode, id);
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        foreach (var node in _tabNodes.Values)
            node.Dispose();

        _tabNodes.Clear();

        if (!Root.IsDisposed)
            Root.Dispose();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a single tab node with its inner label node and hover stylesheet.
    /// </summary>
    private static string SanitizeId(string id) => id.Replace(" ", "-");

    private static Node BuildTabNode(string tabId, string label, bool isActive)
    {
        var safeId    = SanitizeId(tabId);
        var wrapperId = $"Tab-{safeId}";
        var labelId   = $"Tab-{safeId}-label";

        var labelNode = new Node
        {
            Id        = labelId,
            NodeValue = label,
            Style = new Style
            {
                FontSize    = 12,
                Color       = new Color(isActive ? "Tab.Active" : "Tab.Inactive"),
                TextAlign   = Anchor.MiddleCenter,
                TextOverflow = false,
            },
            InheritTags = true,
        };

        var wrapperStyle = new Style
        {
            Flow            = Flow.Horizontal,
            Anchor          = Anchor.MiddleLeft,
            Padding         = new EdgeSize(6, 12),
            Gap             = 0f,
            BackgroundColor = new Color(isActive ? "Tab.ActiveBg" : "Tab.InactiveBg"),
            BorderColor     = isActive
                ? new BorderColor(new Color("Tab.Active"))
                : null,
            BorderWidth     = isActive ? new EdgeSize(0, 0, 2, 0) : null,
            AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
        };

        var hoverWrapperStyle = new Style
        {
            BackgroundColor = new Color("Tab.HoverBg"),
        };

        var hoverLabelStyle = new Style
        {
            Color = new Color("Tab.HoverFg"),
        };

        var tabNode = new Node
        {
            Id         = wrapperId,
            Style      = wrapperStyle,
            Stylesheet = new Stylesheet(new List<Stylesheet.StyleDefinition>
            {
                new($"#{wrapperId}:hover", hoverWrapperStyle),
                new($"#{labelId}:hover",  hoverLabelStyle),
            }),
        };

        tabNode.AppendChild(labelNode);

        return tabNode;
    }

    /// <summary>
    /// Applies the active visual style to a tab wrapper node in-place.
    /// </summary>
    private static void ApplyActiveStyle(Node tabNode, string tabId)
    {
        tabNode.Style.BackgroundColor = new Color("Tab.ActiveBg");
        tabNode.Style.BorderColor     = new BorderColor(new Color("Tab.Active"));
        tabNode.Style.BorderWidth     = new EdgeSize(0, 0, 2, 0);

        var labelNode = tabNode.QuerySelector($"#Tab-{SanitizeId(tabId)}-label");
        if (labelNode != null)
            labelNode.Style.Color = new Color("Tab.Active");
    }

    /// <summary>
    /// Applies the inactive visual style to a tab wrapper node in-place.
    /// </summary>
    private static void ApplyInactiveStyle(Node tabNode, string tabId)
    {
        tabNode.Style.BackgroundColor = new Color("Tab.InactiveBg");
        tabNode.Style.BorderColor     = null;
        tabNode.Style.BorderWidth     = null;

        var labelNode = tabNode.QuerySelector($"#Tab-{SanitizeId(tabId)}-label");
        if (labelNode != null)
            labelNode.Style.Color = new Color("Tab.Inactive");
    }
}
