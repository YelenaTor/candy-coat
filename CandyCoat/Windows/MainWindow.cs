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

    private const string ProtectedRolePassword = "pixie13!?";

    // Manager password UI state (settings)
    private string _mgrPwBuffer    = string.Empty;
    private bool   _mgrPwSetResult = false;

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
        // The actual settings are drawn via raw ImGui in DrawSettingsOverlay.
        // This node just fills the content area so the sidebar renders correctly.
        return new Node
        {
            Style = new Style
            {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                BackgroundColor = new Color(CandyTheme.BgWindow),
            },
        };
    }

    // ─── Raw ImGui overlays ───────────────────────────────────────────────────

    /// <summary>
    /// Draws the settings panel using raw ImGui, positioned over the content area.
    /// Called after Render() so it appears on top of the Una.Drawing layer.
    /// </summary>
    private void DrawSettingsOverlay(Vector2 region)
    {
        // Content area starts after the sidebar (200px + padding on each side)
        const float sidebarW = 200f + 16f; // sidebar width + its internal padding
        var contentPos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin()
                         + new Vector2(sidebarW, 0);
        var contentSize = new Vector2(region.X - sidebarW, region.Y);

        ImGui.SetNextWindowPos(contentPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(contentSize, ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.10f, 0.08f, 0.13f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,  new Vector4(0.10f, 0.08f, 0.13f, 1f));
        ImGui.SetNextWindowBgAlpha(1f);

        bool open = true;
        ImGui.Begin("##SettingsOverlay", ref open,
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoBringToFrontOnFocus);

        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Settings");
        ImGui.Separator();
        ImGui.Spacing();
        DrawGeneralSettings();

        ImGui.End();
        ImGui.PopStyleColor(2);
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

    // ─── Settings (raw ImGui) ─────────────────────────────────────────────────

    private void DrawGeneralSettings()
    {
        var config = plugin.Configuration;

        // ── Role Management ──
        if (ImGui.CollapsingHeader("Role Management", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            ImGui.Text("Primary Role:");

            var allRoles  = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Where(Enum.GetValues<StaffRole>(), r => r != StaffRole.None));
            var roleLabels = System.Array.ConvertAll(allRoles, r => r.ToString());

            int primaryIdx = System.Array.IndexOf(allRoles, config.PrimaryRole);
            if (primaryIdx < 0) primaryIdx = 0;

            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("##primaryRoleSettings", ref primaryIdx, roleLabels, roleLabels.Length))
            {
                var chosen = allRoles[primaryIdx];
                bool mgmtLocked  = chosen == StaffRole.Management && string.IsNullOrEmpty(config.ManagerPassword);
                bool ownerLocked = chosen == StaffRole.Owner && !config.IsManagementModeEnabled;

                if (!mgmtLocked && !ownerLocked)
                {
                    config.PrimaryRole  = chosen;
                    config.EnabledRoles |= chosen;
                    config.Save();
                }
            }

            if (string.IsNullOrEmpty(config.ManagerPassword))
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f),
                    "\u26a0 Management role requires a Manager Password (set by Owner).");
            }

            ImGui.Spacing();
            var multiRole = config.MultiRoleEnabled;
            if (ImGui.Checkbox("Enable Multiple Roles", ref multiRole))
            {
                config.MultiRoleEnabled = multiRole;
                if (!multiRole) config.EnabledRoles = config.PrimaryRole;
                config.Save();
            }

            if (config.MultiRoleEnabled)
            {
                ImGui.Indent();
                foreach (StaffRole role in Enum.GetValues<StaffRole>())
                {
                    if (role == StaffRole.None || role == config.PrimaryRole) continue;
                    bool enabled     = config.EnabledRoles.HasFlag(role);
                    bool mgmtLocked  = role == StaffRole.Management && string.IsNullOrEmpty(config.ManagerPassword);
                    bool ownerLocked = role == StaffRole.Owner && !config.IsManagementModeEnabled;

                    if (mgmtLocked || ownerLocked)
                    {
                        ImGui.BeginDisabled();
                        ImGui.Checkbox($"{role} \ud83d\udd12##secondary", ref enabled);
                        ImGui.EndDisabled();
                    }
                    else
                    {
                        if (ImGui.Checkbox($"{role}##secondary", ref enabled))
                        {
                            if (enabled) config.EnabledRoles |= role;
                            else         config.EnabledRoles &= ~role;
                            config.Save();
                        }
                    }
                }
                ImGui.Unindent();
            }

            // ── Set Manager Password (Owner only) ──
            if (config.IsManagementModeEnabled)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "\ud83d\udd11 Set Manager Password");
                ImGui.TextDisabled("Controls who can be assigned the Management role.");
                ImGui.Spacing();
                ImGui.SetNextItemWidth(200);
                ImGui.InputTextWithHint("##mgrPwInput", "New password...", ref _mgrPwBuffer, 50, ImGuiInputTextFlags.Password);
                ImGui.SameLine();
                if (ImGui.Button("Save##saveMgrPw"))
                {
                    config.ManagerPassword = _mgrPwBuffer.Trim();
                    config.Save();
                    plugin.SyncService.UpsertVenueConfigAsync(config.ManagerPassword);
                    _mgrPwBuffer    = string.Empty;
                    _mgrPwSetResult = true;
                }
                if (_mgrPwSetResult) { ImGui.SameLine(); ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.4f, 1f), "\u2714 Saved"); }
            }

            ImGui.Spacing();
        }

        // ── Integrations ──
        if (ImGui.CollapsingHeader("Integrations", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            var enableGlam = config.EnableGlamourer;
            if (ImGui.Checkbox("Enable Glamourer Integration", ref enableGlam)) { config.EnableGlamourer = enableGlam; config.Save(); }
            var enableChat = config.EnableChatTwo;
            if (ImGui.Checkbox("Enable ChatTwo Integration", ref enableChat)) { config.EnableChatTwo = enableChat; config.Save(); }
            ImGui.Spacing();
        }

        // ── Custom Macros ──
        if (ImGui.CollapsingHeader("Custom Macros"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Create Quick-Tells that appear on patron profiles. Use {name} to insert their first name.");
            if (ImGui.Button("Add New Macro"))
            {
                config.Macros.Add(new Data.MacroTemplate { Title = "New Macro", Text = "Hello {name}!" });
                config.Save();
            }
            ImGui.Spacing();
            for (int i = 0; i < config.Macros.Count; i++)
            {
                var m = config.Macros[i];
                ImGui.PushID($"Macro{i}");
                var title = m.Title; var text = m.Text;
                if (ImGui.InputText("Title", ref title, 50)) { m.Title = title; config.Save(); }
                if (ImGui.InputTextMultiline("Text", ref text, 500, new Vector2(-1, 60))) { m.Text = text; config.Save(); }
                if (ImGui.Button("Delete")) { config.Macros.RemoveAt(i); config.Save(); ImGui.PopID(); break; }
                ImGui.Separator();
                ImGui.PopID();
            }
            ImGui.Spacing();
        }

        // ── Management Access ──
        if (ImGui.CollapsingHeader("Management Access"))
        {
            ImGui.Spacing();
            if (config.IsManagementModeEnabled)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), "\u2714\ufe0f Management Mode Active");
            }
            else
            {
                var code = "";
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputTextWithHint("##mgmtcode", "Enter Passcode", ref code, 20, ImGuiInputTextFlags.Password))
                {
                    if (code == ProtectedRolePassword) { config.IsManagementModeEnabled = true; config.Save(); }
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(Locked)");
            }
            ImGui.Spacing();
        }

        // ── Patron Alerts ──
        if (ImGui.CollapsingHeader("Patron Alerts"))
        {
            ImGui.Spacing();
            var enableAlerts = config.EnablePatronAlerts;
            if (ImGui.Checkbox("Enable Patron Entry Alerts", ref enableAlerts)) { config.EnablePatronAlerts = enableAlerts; config.Save(); }
            ImGui.TextDisabled("Shows an overlay when a tracked patron enters the instance.");
            ImGui.Spacing();
            if (config.EnablePatronAlerts)
            {
                ImGui.Indent();
                ImGui.Text("Alert Method:");
                ImGui.SameLine();
                var methodIdx = (int)config.AlertMethod;
                ImGui.SetNextItemWidth(120);
                if (ImGui.Combo("##alertMethod", ref methodIdx, new[] { "Panel", "Chat", "Both" }, 3)) { config.AlertMethod = (CandyCoat.Data.PatronAlertMethod)methodIdx; config.Save(); }
                ImGui.TextDisabled("Panel = on-screen card \u00b7 Chat = echo message \u00b7 Both = panel + chat");
                ImGui.Spacing();
                var regularOnly = config.AlertOnRegularOnly;
                if (ImGui.Checkbox("Only alert for Regular / Elite patrons", ref regularOnly)) { config.AlertOnRegularOnly = regularOnly; config.Save(); }
                ImGui.TextDisabled("Danger-status patrons always alert regardless.");
                ImGui.Spacing();
                var targetBtn = config.EnableTargetOnAlertClick;
                if (ImGui.Checkbox("Show 'Target' button on panel alerts", ref targetBtn)) { config.EnableTargetOnAlertClick = targetBtn; config.Save(); }
                ImGui.Spacing();
                var cooldown = config.AlertCooldownMinutes;
                ImGui.SetNextItemWidth(80);
                if (ImGui.InputInt("Cooldown (minutes)##alertCooldown", ref cooldown, 1)) { config.AlertCooldownMinutes = System.Math.Max(1, cooldown); config.Save(); }
                ImGui.TextDisabled("Minimum time before re-alerting for the same patron.");
                ImGui.Spacing();
                var dismissSecs = config.AlertDismissSeconds;
                ImGui.SetNextItemWidth(80);
                if (ImGui.InputInt("Auto-dismiss after (seconds)##alertDismiss", ref dismissSecs, 1)) { config.AlertDismissSeconds = System.Math.Max(3, dismissSecs); config.Save(); }
                ImGui.Unindent();
            }
            ImGui.Spacing();
        }

        // ── Candy Tells ──
        if (ImGui.CollapsingHeader("Candy Tells"))
        {
            ImGui.Spacing();
            var suppressInGame = config.TellSuppressInGame;
            if (ImGui.Checkbox("Suppress tells from in-game chat", ref suppressInGame))
            { config.TellSuppressInGame = suppressInGame; config.Save(); }
            ImGui.TextDisabled("Removes incoming /tell messages from the main chat window.");
            ImGui.Spacing();
            var autoOpen = config.TellAutoOpen;
            if (ImGui.Checkbox("Auto-open Tells window on incoming message", ref autoOpen))
            { config.TellAutoOpen = autoOpen; config.Save(); }
            ImGui.Spacing();
            var maxMsgs = config.TellHistoryMaxMessages;
            ImGui.SetNextItemWidth(100);
            if (ImGui.SliderInt("Max messages per conversation##tellMax", ref maxMsgs, 50, 500))
            { config.TellHistoryMaxMessages = System.Math.Max(50, maxMsgs); config.Save(); }
            ImGui.Spacing();
            if (ImGui.Button("Clear all conversation history##clearTells"))
            {
                config.TellHistory.Clear();
                config.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(Cannot be undone)");
            ImGui.Spacing();
        }

        // ── Support ──
        if (ImGui.CollapsingHeader("Support & Feedback"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Thank you for helping us improve Candy Coat! <3");
            ImGui.BulletText("Bugs & Crashes: Report via Discord (DM me) or GitHub Issues.");
            ImGui.BulletText("Suggestions: Use the #\ud83c\udf70-staff-bot-testing channel on Discord.");
            ImGui.Spacing();
            if (ImGui.Button("Open GitHub Issues")) ECommons.GenericHelpers.ShellStart("https://github.com/YelenaTor/candy-coat/issues");
            ImGui.SameLine();
            if (ImGui.Button("Copy Discord Link")) ImGui.SetClipboardText("https://discord.gg/your-discord-link");
            ImGui.Spacing();
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
