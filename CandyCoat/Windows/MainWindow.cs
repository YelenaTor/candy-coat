using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.Services;
using CandyCoat.Windows.Tabs;
using CandyCoat.Windows.SRT;

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

    // Trade notifications banner
    private readonly List<(string Name, int Amount, bool Linked)> _tradeNotifications = new();

    private const string ProtectedRolePassword = "pixie13!?";

    // Manager password UI state (settings)
    private string _mgrPwBuffer    = string.Empty;
    private bool   _mgrPwSetResult = false;

    private const float SidebarWidth = 168f;

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
        CandyCoat.UI.StyleManager.PushStyles();

        try
        {
            var contentRegion = ImGui.GetContentRegionAvail();

            ImGui.PushStyleColor(ImGuiCol.ChildBg, CandyCoat.UI.StyleManager.SidebarBg);
            {
                using var sidebar = ImRaii.Child("##Sidebar", new Vector2(SidebarWidth, contentRegion.Y), true);
                ImGui.PopStyleColor();
                DrawSidebar();
            }

            ImGui.SameLine();
            {
                using var content = ImRaii.Child("##Content", new Vector2(0, contentRegion.Y));
                DrawContentPanel();
            }
        }
        finally
        {
            CandyCoat.UI.StyleManager.PopStyles();
        }
    }

    // ─── Sidebar ─────────────────────────────────────────────────────────────

    private void DrawSidebar()
    {
        // Header
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Candy Coat");
        ImGui.Spacing();

        var profileOpen = _profileWindow.IsOpen;
        if (profileOpen) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.9f, 1f));
        if (ImGui.Selectable("  My Profile##profileBtn", profileOpen, ImGuiSelectableFlags.None, new Vector2(SidebarWidth - 16f, 0)))
            _profileWindow.Toggle();
        if (profileOpen) ImGui.PopStyleColor();

        ImGui.Separator();
        ImGui.Spacing();

        // ── Dashboard Drawer ──
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.15f, 0.25f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.3f, 0.2f, 0.35f, 1f));
        if (ImGui.CollapsingHeader("Dashboard", ImGuiTreeNodeFlags.DefaultOpen))
        {
            for (int i = 0; i < dashboardTabs.Count; i++)
            {
                bool isSelected = _activeSection == SidebarSection.Dashboard && _selectedDashboardIndex == i;
                if (isSelected) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.8f, 1f));
                if (ImGui.Selectable($"  {dashboardTabs[i].Name}##dash{i}", isSelected))
                {
                    _activeSection = SidebarSection.Dashboard;
                    _selectedDashboardIndex = i;
                }
                if (isSelected) ImGui.PopStyleColor();
            }
        }
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        // ── SRT Drawer — rounded pill buttons ──
        var visiblePanels = GetVisibleSrtPanels();
        if (visiblePanels.Count > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.15f, 0.20f, 0.25f, 1f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.20f, 0.30f, 0.35f, 1f));
            if (ImGui.CollapsingHeader("Sugar Role Toolbox", ImGuiTreeNodeFlags.DefaultOpen))
            {
                for (int i = 0; i < visiblePanels.Count; i++)
                {
                    bool isSelected = _activeSection == SidebarSection.SRT && _selectedSrtIndex == i;

                    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);
                    ImGui.PushStyleColor(ImGuiCol.Button,
                        isSelected ? CandyCoat.UI.StyleManager.PastelPink : new Vector4(0.18f, 0.14f, 0.22f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered,
                        isSelected ? CandyCoat.UI.StyleManager.PastelPinkHover : new Vector4(0.28f, 0.20f, 0.36f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, CandyCoat.UI.StyleManager.PastelPinkActive);
                    ImGui.PushStyleColor(ImGuiCol.Text,
                        isSelected ? new Vector4(0.15f, 0.08f, 0.12f, 1f) : CandyCoat.UI.StyleManager.PastelText);

                    if (ImGui.Button(
                        $"{PanelIcon(visiblePanels[i].Role)}  {visiblePanels[i].Name}##srt{i}",
                        new Vector2(SidebarWidth - 16f, 26f)))
                    {
                        var prev = _selectedSrtIndex;
                        _activeSection = SidebarSection.SRT;
                        _selectedSrtIndex = i;
                        OpenOrFocusSrtFeatureWindow(visiblePanels[i], i != prev);
                    }

                    ImGui.PopStyleColor(4);
                    ImGui.PopStyleVar();
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f);
                }
            }
            ImGui.PopStyleColor(2);
        }
        else
        {
            ImGui.TextDisabled("Sugar Role Toolbox");
            ImGui.TextDisabled("  No roles selected.");
            ImGui.TextDisabled("  Set up in Settings.");
        }

        // ── Footer (API status, Messages, Cosmetics, Settings, Ko-Fi) ──
        var footerHeight = ImGui.GetFrameHeightWithSpacing() * 5 + ImGui.GetStyle().ItemSpacing.Y;
        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - footerHeight - ImGui.GetStyle().WindowPadding.Y);

        ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.4f, 1f), "API: Backstage \u2022 Online");
        ImGui.Separator();

        int unreadTells = 0;
        foreach (var conv in plugin.Configuration.TellHistory)
            unreadTells += conv.UnreadCount;
        var tellLabel = unreadTells > 0 ? $"\u2709 Messages [{unreadTells}]" : "\u2709 Messages";
        bool tellOpen = plugin.TellWindow.IsOpen;
        if (tellOpen) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.9f, 1f));
        if (ImGui.Selectable(tellLabel, tellOpen)) plugin.TellWindow.Toggle();
        if (tellOpen) ImGui.PopStyleColor();

        bool cosmeticsOpen = _cosmeticWindow.IsOpen;
        if (cosmeticsOpen) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.9f, 1f));
        if (ImGui.Selectable("\u2728 Cosmetics", cosmeticsOpen)) _cosmeticWindow.Toggle();
        if (cosmeticsOpen) ImGui.PopStyleColor();

        bool settingsSelected = _activeSection == SidebarSection.Settings;
        if (settingsSelected) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 1f, 1f));
        if (ImGui.Selectable("\u2699 Settings", settingsSelected)) _activeSection = SidebarSection.Settings;
        if (settingsSelected) ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, CandyCoat.UI.StyleManager.SyncWarn);
        if (ImGui.Selectable("\u2615 Support on Ko-Fi"))
            ECommons.GenericHelpers.ShellStart("https://ko-fi.com/yorudev");
        ImGui.PopStyleColor();
    }

    // ─── SrtFeatureWindow management ─────────────────────────────────────────

    private void OpenOrFocusSrtFeatureWindow(IToolboxPanel panel, bool panelChanged)
    {
        var cfg = plugin.Configuration;
        _srtFeatureWindow.SetPanel(panel);

        if (cfg.SrtFeatureAttached) return; // content shows in tab bar, window stays closed

        if (!_srtFeatureWindow.IsOpen || panelChanged)
        {
            // Position beside main window
            if (Position.HasValue && Size.HasValue)
            {
                _srtFeatureWindow.Position = new Vector2(Position.Value.X + Size.Value.X + 8f, Position.Value.Y);
                _srtFeatureWindow.PositionCondition = ImGuiCond.Appearing;
            }
            _srtFeatureWindow.IsOpen = true;
        }
    }

    // ─── Content Panel ───────────────────────────────────────────────────────

    private void DrawContentPanel()
    {
        DrawTradeNotifications();

        switch (_activeSection)
        {
            case SidebarSection.Dashboard:
                if (_selectedDashboardIndex >= 0 && _selectedDashboardIndex < dashboardTabs.Count)
                    dashboardTabs[_selectedDashboardIndex].DrawContent();
                break;

            case SidebarSection.SRT:
                DrawSrtContentPanel();
                break;

            case SidebarSection.Settings:
                DrawSettingsContent();
                break;
        }
    }

    private void DrawSrtContentPanel()
    {
        var visiblePanels = GetVisibleSrtPanels();
        if (visiblePanels.Count == 0)
        {
            _selectedSrtIndex = 0;
            ImGui.TextDisabled("No roles enabled. Set up in Settings.");
            return;
        }

        if (_selectedSrtIndex < 0 || _selectedSrtIndex >= visiblePanels.Count)
            _selectedSrtIndex = 0;

        var selectedPanel = visiblePanels[_selectedSrtIndex];
        var cfg = plugin.Configuration;

        if (cfg.SrtFeatureAttached)
        {
            // Tab bar mode: Features + Settings in one panel
            using var tabs = ImRaii.TabBar("##SrtFeatureBar");
            if (!tabs) return;

            if (ImGui.BeginTabItem($"{PanelIcon(selectedPanel.Role)}  Features##SrtF"))
            {
                // Detach button flush right
                var detachLabel = "\u2b21 Detach";
                var detachW = ImGui.CalcTextSize(detachLabel).X + ImGui.GetStyle().FramePadding.X * 2;
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - detachW - 4f);
                if (ImGui.SmallButton(detachLabel))
                {
                    cfg.SrtFeatureAttached = false;
                    cfg.Save();
                    OpenOrFocusSrtFeatureWindow(selectedPanel, true);
                }
                ImGui.NewLine();
                selectedPanel.DrawContent();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("\u2699  Settings##SrtS"))
            {
                selectedPanel.DrawSettings();
                ImGui.EndTabItem();
            }
        }
        else
        {
            // Floating mode: right panel shows Settings only
            ImGui.TextColored(CandyCoat.UI.StyleManager.SectionHeader,
                $"  {PanelIcon(selectedPanel.Role)}  {selectedPanel.Name} \u2014 Settings");
            ImGui.TextDisabled("  Configure your role toolbox. Features are in the floating window.");
            ImGui.Spacing();
            selectedPanel.DrawSettings();
        }
    }

    private void DrawSettingsContent()
    {
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Settings");
        ImGui.Separator();
        ImGui.Spacing();
        DrawGeneralSettings();
    }

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
                    bool enabled    = config.EnabledRoles.HasFlag(role);
                    bool mgmtLocked = role == StaffRole.Management && string.IsNullOrEmpty(config.ManagerPassword);
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
                ImGui.TextColored(CandyCoat.UI.StyleManager.SyncOk, "\u2714\ufe0f Management Mode Active");
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

    private void DrawTradeNotifications()
    {
        if (_tradeNotifications.Count == 0) return;

        for (int i = _tradeNotifications.Count - 1; i >= 0; i--)
        {
            var (name, amount, linked) = _tradeNotifications[i];
            if (linked)
                ImGui.TextColored(CandyCoat.UI.StyleManager.SyncOk, $"\u2714 Trade: {amount:N0} Gil from {name} \u2014 linked to booking");
            else
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), $"\ud83d\udcb0 Trade: {amount:N0} Gil from {name} \u2014 no matching active booking");
            ImGui.SameLine();
            if (ImGui.SmallButton($"\u2715##tradeN{i}")) _tradeNotifications.RemoveAt(i);
        }
        ImGui.Separator();
        ImGui.Spacing();
    }

    private List<IToolboxPanel> GetVisibleSrtPanels()
    {
        var enabledRoles = plugin.Configuration.EnabledRoles;
        var visible = new List<IToolboxPanel>();
        foreach (var panel in srtPanels)
        {
            if (enabledRoles.HasFlag(panel.Role))
                visible.Add(panel);
        }
        return visible;
    }
}
