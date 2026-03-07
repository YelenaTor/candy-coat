using System;
using System.Collections.Generic;
using CandyCoat.Data;
using CandyCoat.Windows.Tabs;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Toolbar entry that hosts all dashboard ITab panels under a single balloon button.
/// Manages its own internal tab selection and renders the active tab's node tree.
/// </summary>
public sealed class OverviewEntry : IToolbarEntry, IDisposable
{
    // -------------------------------------------------------------------------
    // IToolbarEntry properties
    // -------------------------------------------------------------------------

    public string    Id    => "overview";
    public string    Icon  => "\uF015";
    public string    Label => "Overview";
    public StaffRole Role  => StaffRole.None;

    // -------------------------------------------------------------------------
    // Private fields
    // -------------------------------------------------------------------------

    private readonly List<ITab>  _tabs;
    private readonly TabStrip    _tabStrip;

    /// <summary>Root panel node returned by BuildPanel(). Cached — never rebuilt.</summary>
    private readonly Node _panelRoot;

    /// <summary>Content area child — swapped when the active tab changes.</summary>
    private readonly Node _contentArea;

    private int _activeIndex;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public OverviewEntry(List<ITab> tabs)
    {
        _tabs      = tabs;
        _tabStrip  = new TabStrip();

        // Content area — grows to fill the balloon's available height
        _contentArea = new Node
        {
            Id    = "OverviewContentArea",
            Style = new Style
            {
                Flow     = Flow.Vertical,
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
            },
        };

        // Panel root — vertical stack: [TabStrip, ContentArea]
        // Must be a root node (no parent assigned) so BalloonService can append it to
        // its own _contentArea, with _balloonRoot as the actual rendering root.
        _panelRoot = new Node
        {
            Id    = "OverviewPanelRoot",
            Style = new Style
            {
                Flow     = Flow.Vertical,
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
            },
        };

        _panelRoot.AppendChild(_tabStrip.Root);
        _panelRoot.AppendChild(_contentArea);

        // Build the tab strip with all tabs; activate first tab
        RebuildTabStrip();
        SwapContentTo(0);
    }

    // -------------------------------------------------------------------------
    // IToolbarEntry implementation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the cached panel root node. Must not be called before construction completes.
    /// </summary>
    public Node BuildPanel() => _panelRoot;

    /// <summary>
    /// Forwards overlay drawing to the currently active tab.
    /// </summary>
    public void DrawOverlays()
    {
        if (_tabs.Count == 0) return;
        _tabs[_activeIndex].DrawOverlays();
    }

    /// <summary>No settings panel for the overview entry.</summary>
    public Node? BuildSettingsPanel() => null;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Populates the TabStrip with one tab per ITab, using the current active tab id.
    /// </summary>
    private void RebuildTabStrip()
    {
        var tabDefs = new List<(string Id, string Label)>(_tabs.Count);
        foreach (var tab in _tabs)
            tabDefs.Add((tab.Name, tab.Name));

        string activeId = _tabs.Count > 0 ? _tabs[_activeIndex].Name : string.Empty;

        _tabStrip.SetTabs(tabDefs, activeId, OnTabClicked);
    }

    /// <summary>
    /// Called by TabStrip when the user clicks a tab.
    /// </summary>
    private void OnTabClicked(string tabId)
    {
        int idx = _tabs.FindIndex(t => t.Name == tabId);
        if (idx < 0 || idx == _activeIndex) return;

        _activeIndex = idx;
        _tabStrip.SetActiveTab(tabId);
        SwapContentTo(idx);
    }

    /// <summary>
    /// Clears the content area and appends the target tab's node tree.
    /// </summary>
    private void SwapContentTo(int index)
    {
        // Clear existing children
        while (_contentArea.ChildNodes.Count > 0)
            _contentArea.ChildNodes[0].Remove();

        if (_tabs.Count == 0 || index < 0 || index >= _tabs.Count) return;

        var tabNode = _tabs[index].BuildNode();
        _contentArea.AppendChild(tabNode);
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        _tabStrip.Dispose();

        foreach (var tab in _tabs)
            tab.Dispose();

        if (!_panelRoot.IsDisposed)
            _panelRoot.Dispose();
    }
}
