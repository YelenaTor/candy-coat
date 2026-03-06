using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.Services;
using CandyCoat.Windows.Tabs;
using CandyCoat.Windows.SRT;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;
    private readonly VenueService venueService;
    private readonly PatronDetailsWindow detailsWindow;
    private readonly CosmeticWindow _cosmeticWindow;
    private readonly ProfileWindow _profileWindow;
    private readonly SrtFeatureWindow _srtFeatureWindow;

    private readonly List<ITab> dashboardTabs = new();
    private readonly List<IToolboxPanel> srtPanels = new();

    // Sidebar state
    private enum SidebarSection { Dashboard, SRT, Settings }
    private SidebarSection _activeSection = SidebarSection.Dashboard;
    private int _selectedDashboardIndex = 0;
    private int _selectedSrtIndex = 0;

    // Sidebar drawer expand/collapse state
    private bool _dashboardExpanded = true;
    private bool _srtExpanded = true;

    // Trade notifications banner
    private readonly List<(string Name, int Amount, bool Linked)> _tradeNotifications = new();

    // ─── Settings panel ──────────────────────────────────────────────────────
    private readonly SettingsPanel _settingsPanel;

    // ─── Una.Drawing node tree ────────────────────────────────────────────────
    private Node? _rootNode;
    private Node? _sidebarNode;
    private Node? _contentNode;

    // Track what state the tree was built for, so we only rebuild on change
    private SidebarSection _builtSection = (SidebarSection)(-1);
    private int            _builtDashIdx = -1;
    private int            _builtSrtIdx  = -1;
    private int            _builtUnread  = -1;
    private bool           _builtDashExpanded = true;
    private bool           _builtSrtExpanded  = true;
    private StaffRole      _builtEnabledRoles  = StaffRole.None;

    // ─── Constructor ─────────────────────────────────────────────────────────

    public MainWindow(Plugin plugin, VenueService venueService, WaitlistManager waitlistManager, ShiftManager shiftManager, PatronDetailsWindow detailsWindow, string goatImagePath, CosmeticWindow cosmeticWindow, ProfileWindow profileWindow, SrtFeatureWindow srtFeatureWindow)
        : base("Candy Coat - Sugar##CandyCoatMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(650, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
        this.venueService = venueService;
        this.detailsWindow = detailsWindow;
        _cosmeticWindow = cosmeticWindow;
        _profileWindow = profileWindow;
        _srtFeatureWindow = srtFeatureWindow;

        // Initialize Dashboard Tabs
        var bookingsTab = new BookingsTab(plugin, venueService);
        bookingsTab.OnPatronSelected += OnPatronSelected;

        var locatorTab = new LocatorTab(plugin, venueService);
        locatorTab.OnPatronSelected += OnPatronSelected;

        dashboardTabs.Add(new OverviewTab(plugin));
        dashboardTabs.Add(bookingsTab);
        dashboardTabs.Add(locatorTab);
        dashboardTabs.Add(new SessionTab(plugin));
        dashboardTabs.Add(new WaitlistTab(waitlistManager));
        dashboardTabs.Add(new StaffTab(shiftManager));

        // Subscribe to trade notifications
        plugin.TradeMonitorService.OnTradeDetected += (name, amount, linked) =>
            _tradeNotifications.Add((name, amount, linked));

        // Settings panel
        _settingsPanel = new SettingsPanel(plugin);

        // Initialize SRT Panels
        srtPanels.Add(new SweetheartPanel(plugin));
        srtPanels.Add(new CandyHeartPanel(plugin));
        srtPanels.Add(new BartenderPanel(plugin));
        srtPanels.Add(new GambaPanel(plugin));
        srtPanels.Add(new DJPanel(plugin));
        srtPanels.Add(new ManagementPanel(plugin));
        srtPanels.Add(new OwnerPanel(plugin));
        srtPanels.Add(new GreeterPanel(plugin));
    }

    private void OnPatronSelected(Data.Patron? patron)
    {
        if (patron != null)
            detailsWindow.OpenForPatron(patron);
    }

    public void Dispose()
    {
        _rootNode?.Dispose();
        _rootNode = null;
        foreach (var panel in srtPanels)
            if (panel is IDisposable d) d.Dispose();
    }

    public void OpenBookingsTab()
    {
        IsOpen = true;
        _activeSection = SidebarSection.Dashboard;
        _selectedDashboardIndex = 1; // Bookings
    }

    // ─── Icon helper (static so SrtFeatureWindow can call it) ────────────────

    internal static string PanelIcon(StaffRole role) => role switch
    {
        StaffRole.Sweetheart  => "\u2665",
        StaffRole.CandyHeart  => "\u2601",
        StaffRole.Bartender   => "\ud83c\udf78",
        StaffRole.Gamba       => "\ud83c\udfb2",
        StaffRole.DJ          => "\u266c",
        StaffRole.Management  => "\ud83d\udccb",
        StaffRole.Owner       => "\u2605",
        StaffRole.Greeter     => "\ud83d\udea8",
        _                     => "\u25cf",
    };

    // ─── Main Draw ───────────────────────────────────────────────────────────

    public override void Draw()
    {
        var region = ImGui.GetContentRegionAvail();

        // Rebuild sidebar/root if key state has changed
        int unread = CountUnreadTells();
        var enabledRoles = plugin.Configuration.EnabledRoles;

        bool needsRebuild = _rootNode == null
            || _builtSection        != _activeSection
            || _builtDashIdx        != _selectedDashboardIndex
            || _builtSrtIdx         != _selectedSrtIndex
            || _builtUnread         != unread
            || _builtDashExpanded   != _dashboardExpanded
            || _builtSrtExpanded    != _srtExpanded
            || _builtEnabledRoles   != enabledRoles;

        if (needsRebuild)
        {
            _rootNode?.Dispose();
            _rootNode = BuildRoot(region);

            _builtSection      = _activeSection;
            _builtDashIdx      = _selectedDashboardIndex;
            _builtSrtIdx       = _selectedSrtIndex;
            _builtUnread       = unread;
            _builtDashExpanded = _dashboardExpanded;
            _builtSrtExpanded  = _srtExpanded;
            _builtEnabledRoles = enabledRoles;
        }
        else
        {
            // Keep root sized to the available region every frame
            _rootNode!.Style.Size = new Size((int)region.X, (int)region.Y);
        }

        var renderPos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _rootNode!.Render(ImGui.GetWindowDrawList(), renderPos);

        // Reserve space so Dalamud's window system knows how much we used
        ImGui.Dummy(region);

        // ── Overlays: raw ImGui drawn on top of the node tree ──
        DrawTradeNotificationOverlay();

        if (_activeSection == SidebarSection.Settings)
        {
            DrawSettingsOverlay(region);
        }
        else if (_activeSection == SidebarSection.Dashboard
                 && _selectedDashboardIndex >= 0
                 && _selectedDashboardIndex < dashboardTabs.Count)
        {
            dashboardTabs[_selectedDashboardIndex].DrawOverlays();
        }
        else if (_activeSection == SidebarSection.SRT)
        {
            var visible = GetVisibleSrtPanels();
            if (_selectedSrtIndex >= 0 && _selectedSrtIndex < visible.Count)
            {
                var cfg = plugin.Configuration;
                if (cfg.SrtFeatureAttached)
                {
                    visible[_selectedSrtIndex].DrawOverlays();
                    visible[_selectedSrtIndex].DrawSettingsOverlays();
                }
                else
                {
                    visible[_selectedSrtIndex].DrawSettingsOverlays();
                }
            }
        }
    }

    // ─── Node tree construction ───────────────────────────────────────────────

    private Node BuildRoot(Vector2 region)
    {
        _sidebarNode = BuildSidebar();
        _contentNode = BuildContent();

        var root = CandyUI.WindowRoot(_sidebarNode, _contentNode);
        root.Style.Size = new Size((int)region.X, (int)region.Y);
        return root;
    }

    // ─── Sidebar ─────────────────────────────────────────────────────────────

    private Node BuildSidebar()
    {
        var children = new List<Node>();

        // Header: "Candy Coat" title
        children.Add(new Node
        {
            NodeValue = "Candy Coat",
            Style = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextAccent),
                FontSize  = 15,
                TextAlign = Anchor.MiddleLeft,
                Padding   = new EdgeSize(2, 0, 2, 0),
            },
        });

        // My Profile button
        bool profileOpen = _profileWindow.IsOpen;
        var profileNode = CandyUI.SidebarItem(
            "sidebar-profile",
            "  My Profile",
            profileOpen,
            () => _profileWindow.Toggle()
        );
        children.Add(profileNode);

        // Separator
        children.Add(CandyUI.Separator("sep-top"));

        // Dashboard drawer
        var dashItems = new List<Node>();
        for (int i = 0; i < dashboardTabs.Count; i++)
        {
            int   idx      = i;
            bool  isActive = _activeSection == SidebarSection.Dashboard && _selectedDashboardIndex == i;
            dashItems.Add(CandyUI.SidebarItem(
                $"dash-{i}",
                $"  {dashboardTabs[i].Name}",
                isActive,
                () =>
                {
                    _activeSection          = SidebarSection.Dashboard;
                    _selectedDashboardIndex = idx;
                }
            ));
        }
        children.Add(CandyUI.SidebarDrawer(
            "drawer-dashboard",
            "Dashboard",
            _dashboardExpanded,
            () => { _dashboardExpanded = !_dashboardExpanded; },
            dashItems.ToArray()
        ));

        // SRT drawer
        var visiblePanels = GetVisibleSrtPanels();
        if (visiblePanels.Count > 0)
        {
            var srtItems = new List<Node>();
            for (int i = 0; i < visiblePanels.Count; i++)
            {
                int   idx      = i;
                bool  isActive = _activeSection == SidebarSection.SRT && _selectedSrtIndex == i;
                var   panel    = visiblePanels[i];
                var   btn      = new Node
                {
                    Id        = $"srt-btn-{i}",
                    NodeValue = $"{PanelIcon(panel.Role)}  {panel.Name}",
                    Style     = isActive
                        ? new Style
                        {
                            AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                            Padding         = new EdgeSize(5, 10, 5, 10),
                            BorderRadius    = 8,
                            BackgroundColor = new Color(CandyTheme.BtnPrimary),
                            Color           = new Color(CandyTheme.BgWindow),
                            FontSize        = 13,
                            TextAlign       = Anchor.MiddleLeft,
                        }
                        : new Style
                        {
                            AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                            Padding         = new EdgeSize(5, 10, 5, 10),
                            BorderRadius    = 8,
                            BackgroundColor = new Color(CandyTheme.BgCard),
                            Color           = new Color(CandyTheme.TextSecondary),
                            FontSize        = 13,
                            TextAlign       = Anchor.MiddleLeft,
                        },
                };

                if (!isActive)
                {
                    btn.Stylesheet = new Stylesheet([
                        new Stylesheet.StyleDefinition(
                            $"#srt-btn-{idx}:hover",
                            new Style
                            {
                                BackgroundColor = new Color(CandyTheme.BtnGhostHover),
                                Color           = new Color(CandyTheme.TextPrimary),
                            }
                        ),
                    ]);
                }

                btn.OnClick += _ =>
                {
                    var prev      = _selectedSrtIndex;
                    _activeSection   = SidebarSection.SRT;
                    _selectedSrtIndex = idx;
                    OpenOrFocusSrtFeatureWindow(panel, idx != prev);
                };

                srtItems.Add(btn);
            }

            children.Add(CandyUI.SidebarDrawer(
                "drawer-srt",
                "Sugar Role Toolbox",
                _srtExpanded,
                () => { _srtExpanded = !_srtExpanded; },
                srtItems.ToArray()
            ));
        }
        else
        {
            children.Add(CandyUI.Muted("srt-empty-header", "Sugar Role Toolbox"));
            children.Add(CandyUI.Muted("srt-empty-1", "  No roles selected."));
            children.Add(CandyUI.Muted("srt-empty-2", "  Set up in Settings."));
        }

        // Spacer to push footer to bottom
        children.Add(new Node
        {
            Style = new Style
            {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
            },
        });

        // Footer separator
        children.Add(CandyUI.Separator("sep-footer"));

        // API status
        children.Add(new Node
        {
            NodeValue = "API: Backstage \u2022 Online",
            Style = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextSuccess),
                FontSize  = 11,
                TextAlign = Anchor.MiddleLeft,
            },
        });

        // Messages
        int unread     = CountUnreadTells();
        var tellLabel  = unread > 0 ? $"\u2709 Messages [{unread}]" : "\u2709 Messages";
        bool tellOpen  = plugin.TellWindow.IsOpen;
        var tellNode   = CandyUI.SidebarItem("sidebar-tells", tellLabel, tellOpen, () => plugin.TellWindow.Toggle());
        children.Add(tellNode);

        // Cosmetics
        bool cosOpen  = _cosmeticWindow.IsOpen;
        var cosNode   = CandyUI.SidebarItem("sidebar-cosmetics", "\u2728 Cosmetics", cosOpen, () => _cosmeticWindow.Toggle());
        children.Add(cosNode);

        // Settings
        bool settingsActive = _activeSection == SidebarSection.Settings;
        var settingsNode = CandyUI.SidebarItem(
            "sidebar-settings",
            "\u2699 Settings",
            settingsActive,
            () => { _activeSection = SidebarSection.Settings; }
        );
        children.Add(settingsNode);

        // Ko-Fi
        var kofiNode = new Node
        {
            Id        = "sidebar-kofi",
            NodeValue = "\u2615 Support on Ko-Fi",
            Stylesheet = new Stylesheet([
                new Stylesheet.StyleDefinition(
                    "#sidebar-kofi:hover",
                    new Style { Color = new Color(CandyTheme.TextPrimary) }
                ),
            ]),
            Style = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Padding   = new EdgeSize(5, 10, 5, 10),
                Color     = new Color(CandyTheme.TextWarning),
                FontSize  = 13,
                TextAlign = Anchor.MiddleLeft,
            },
        };
        kofiNode.OnClick += _ => ECommons.GenericHelpers.ShellStart("https://ko-fi.com/yorudev");
        children.Add(kofiNode);

        return CandyUI.Sidebar(children.ToArray());
    }

    // ─── Content Panel ───────────────────────────────────────────────────────

    private Node BuildContent()
    {
        switch (_activeSection)
        {
            case SidebarSection.Dashboard:
                if (_selectedDashboardIndex >= 0 && _selectedDashboardIndex < dashboardTabs.Count)
                    return WrapContent(dashboardTabs[_selectedDashboardIndex].BuildNode());
                break;

            case SidebarSection.SRT:
                return WrapContent(BuildSrtContentNode());

            case SidebarSection.Settings:
                // Settings is rendered as a raw-ImGui overlay; content node is a placeholder
                return BuildSettingsPlaceholder();
        }

        return CandyUI.ContentPanel();
    }

    private static Node WrapContent(Node inner)
    {
        return CandyUI.ContentPanel(inner);
    }

    private Node BuildSrtContentNode()
    {
        var visiblePanels = GetVisibleSrtPanels();
        if (visiblePanels.Count == 0)
        {
            _selectedSrtIndex = 0;
            return CandyUI.Muted("srt-no-roles", "No roles enabled. Set up in Settings.");
        }

        if (_selectedSrtIndex < 0 || _selectedSrtIndex >= visiblePanels.Count)
            _selectedSrtIndex = 0;

        var selectedPanel = visiblePanels[_selectedSrtIndex];
        var cfg           = plugin.Configuration;

        if (cfg.SrtFeatureAttached)
        {
            // Attached mode: features + settings tabs rendered via BuildNode / BuildSettingsNode
            return selectedPanel.BuildNode();
        }
        else
        {
            // Floating mode: show settings node in content panel
            return CandyUI.Column("srt-settings-content", 8,
                CandyUI.SectionHeader("srt-panel-header",
                    $"  {PanelIcon(selectedPanel.Role)}  {selectedPanel.Name} \u2014 Settings"),
                CandyUI.Muted("srt-panel-hint",
                    "  Configure your role toolbox. Features are in the floating window."),
                selectedPanel.BuildSettingsNode()
            );
        }
    }

    private Node BuildSettingsPlaceholder()
    {
        // Delegate to SettingsPanel — returns a placeholder node that fills the
        // content area. The actual settings UI is drawn via DrawSettingsOverlay().
        return _settingsPanel.BuildNode();
    }

    // ─── Raw ImGui overlays ───────────────────────────────────────────────────

    /// <summary>
    /// Draws the settings panel using raw ImGui, positioned over the content area.
    /// Delegates to SettingsPanel.DrawOverlays() which owns all settings logic.
    /// Called after Render() so it appears on top of the Una.Drawing layer.
    /// </summary>
    private void DrawSettingsOverlay(Vector2 region)
    {
        _settingsPanel.DrawOverlays(region);
    }

    private void DrawTradeNotificationOverlay()
    {
        if (_tradeNotifications.Count == 0) return;

        // Position trade notifications in the content area at the top
        for (int i = _tradeNotifications.Count - 1; i >= 0; i--)
        {
            var (name, amount, linked) = _tradeNotifications[i];
            if (linked)
                ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f),
                    $"\u2714 Trade: {amount:N0} Gil from {name} \u2014 linked to booking");
            else
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f),
                    $"\ud83d\udcb0 Trade: {amount:N0} Gil from {name} \u2014 no matching active booking");
            ImGui.SameLine();
            if (ImGui.SmallButton($"\u2715##tradeN{i}")) _tradeNotifications.RemoveAt(i);
        }
    }

    // ─── SrtFeatureWindow management ─────────────────────────────────────────

    private void OpenOrFocusSrtFeatureWindow(IToolboxPanel panel, bool panelChanged)
    {
        var cfg = plugin.Configuration;
        _srtFeatureWindow.SetPanel(panel);

        if (cfg.SrtFeatureAttached) return;

        if (!_srtFeatureWindow.IsOpen || panelChanged)
        {
            if (Position.HasValue && Size.HasValue)
            {
                _srtFeatureWindow.Position = new Vector2(Position.Value.X + Size.Value.X + 8f, Position.Value.Y);
                _srtFeatureWindow.PositionCondition = ImGuiCond.Appearing;
            }
            _srtFeatureWindow.IsOpen = true;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private int CountUnreadTells()
    {
        int count = 0;
        foreach (var conv in plugin.Configuration.TellHistory)
            count += conv.UnreadCount;
        return count;
    }

    private List<IToolboxPanel> GetVisibleSrtPanels()
    {
        var enabledRoles = plugin.Configuration.EnabledRoles;
        var visible = new List<IToolboxPanel>();
        foreach (var panel in srtPanels)
            if (enabledRoles.HasFlag(panel.Role))
                visible.Add(panel);
        return visible;
    }
}
