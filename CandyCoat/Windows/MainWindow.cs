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

    private readonly List<ITab> dashboardTabs = new();
    private readonly List<IToolboxPanel> srtPanels = new();

    // Sidebar state
    private enum SidebarSection { Dashboard, SRT, Settings }
    private SidebarSection _activeSection = SidebarSection.Dashboard;
    private int _selectedDashboardIndex = 0;
    private int _selectedSrtIndex = 0;

    private const float SidebarWidth = 160f;

    public MainWindow(Plugin plugin, VenueService venueService, WaitlistManager waitlistManager, ShiftManager shiftManager, PatronDetailsWindow detailsWindow, string goatImagePath)
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

        // Initialize SRT Panels
        srtPanels.Add(new SweetheartPanel(plugin));
        srtPanels.Add(new CandyHeartPanel(plugin));
        srtPanels.Add(new BartenderPanel(plugin));
        srtPanels.Add(new GambaPanel(plugin));
        srtPanels.Add(new DJPanel(plugin));
        srtPanels.Add(new ManagementPanel(plugin));
        srtPanels.Add(new OwnerPanel(plugin));
    }

    private void OnPatronSelected(Data.Patron? patron)
    {
        if (patron != null)
        {
            detailsWindow.OpenForPatron(patron);
        }
    }

    public void Dispose()
    {
        // Any cleanup if tabs require it in the future
    }

    public void OpenBookingsTab()
    {
        IsOpen = true;
        _activeSection = SidebarSection.Dashboard;
        _selectedDashboardIndex = 1; // Bookings
    }

    public override void Draw()
    {
        CandyCoat.UI.StyleManager.PushStyles();
        
        try 
        {
            var contentRegion = ImGui.GetContentRegionAvail();

            // ═══════════════════════════════════════════
            //  LEFT SIDEBAR
            // ═══════════════════════════════════════════
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.08f, 0.07f, 0.11f, 0.95f));
            ImGui.BeginChild("##Sidebar", new Vector2(SidebarWidth, contentRegion.Y), true);
            ImGui.PopStyleColor();
            
            DrawSidebar();
            
            ImGui.EndChild();

            // ═══════════════════════════════════════════
            //  RIGHT CONTENT PANEL
            // ═══════════════════════════════════════════
            ImGui.SameLine();
            ImGui.BeginChild("##Content", new Vector2(0, contentRegion.Y));
            
            DrawContentPanel();
            
            ImGui.EndChild();
        }
        finally
        {
            CandyCoat.UI.StyleManager.PopStyles();
        }
    }

    private void DrawSidebar()
    {
        // Header
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Candy Coat");
        ImGui.Spacing();
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
                
                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.8f, 1f));
                
                if (ImGui.Selectable($"  {dashboardTabs[i].Name}##dash{i}", isSelected))
                {
                    _activeSection = SidebarSection.Dashboard;
                    _selectedDashboardIndex = i;
                }
                
                if (isSelected)
                    ImGui.PopStyleColor();
            }
        }
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        // ── SRT Drawer ──
        var enabledRoles = plugin.Configuration.EnabledRoles;
        var visiblePanels = GetVisibleSrtPanels();

        if (visiblePanels.Count > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.15f, 0.2f, 0.25f, 1f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.2f, 0.3f, 0.35f, 1f));
            if (ImGui.CollapsingHeader("Sugar Role Toolbox", ImGuiTreeNodeFlags.DefaultOpen))
            {
                for (int i = 0; i < visiblePanels.Count; i++)
                {
                    bool isSelected = _activeSection == SidebarSection.SRT && _selectedSrtIndex == i;

                    if (isSelected)
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.9f, 1f, 1f));

                    if (ImGui.Selectable($"  {visiblePanels[i].Name}##srt{i}", isSelected))
                    {
                        _activeSection = SidebarSection.SRT;
                        _selectedSrtIndex = i;
                    }

                    if (isSelected)
                        ImGui.PopStyleColor();
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

        // ── Settings (pinned to bottom) ──
        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - footerHeight - ImGui.GetStyle().WindowPadding.Y);
        ImGui.Separator();

        bool settingsSelected = _activeSection == SidebarSection.Settings;
        if (settingsSelected)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 1f, 1f));
        
        if (ImGui.Selectable("⚙ Settings", settingsSelected))
        {
            _activeSection = SidebarSection.Settings;
        }

        if (settingsSelected)
            ImGui.PopStyleColor();
    }

    private void DrawContentPanel()
    {
        switch (_activeSection)
        {
            case SidebarSection.Dashboard:
                if (_selectedDashboardIndex >= 0 && _selectedDashboardIndex < dashboardTabs.Count)
                {
                    dashboardTabs[_selectedDashboardIndex].DrawContent();
                }
                break;

            case SidebarSection.SRT:
                var visiblePanels = GetVisibleSrtPanels();
                if (_selectedSrtIndex >= 0 && _selectedSrtIndex < visiblePanels.Count)
                {
                    visiblePanels[_selectedSrtIndex].DrawContent();
                }
                else
                {
                    ImGui.TextDisabled("Select a toolbox from the sidebar.");
                }
                break;

            case SidebarSection.Settings:
                DrawSettingsContent();
                break;
        }
    }

    private void DrawSettingsContent()
    {
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Settings");
        ImGui.Separator();
        ImGui.Spacing();

        var config = plugin.Configuration;

        // ── Role Management ──
        if (ImGui.CollapsingHeader("Role Management", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            ImGui.Text("Primary Role:");
            ImGui.Spacing();

            foreach (StaffRole role in Enum.GetValues<StaffRole>())
            {
                if (role == StaffRole.None) continue;
                bool isPrimary = config.PrimaryRole == role;
                if (ImGui.RadioButton(role.ToString(), isPrimary))
                {
                    config.PrimaryRole = role;
                    config.EnabledRoles |= role; // Primary always enabled
                    config.Save();
                }
            }

            ImGui.Spacing();
            var multiRole = config.MultiRoleEnabled;
            if (ImGui.Checkbox("Enable Multiple Roles", ref multiRole))
            {
                config.MultiRoleEnabled = multiRole;
                if (!multiRole)
                {
                    // Reset to primary only
                    config.EnabledRoles = config.PrimaryRole;
                }
                config.Save();
            }

            if (config.MultiRoleEnabled)
            {
                ImGui.Indent();
                foreach (StaffRole role in Enum.GetValues<StaffRole>())
                {
                    if (role == StaffRole.None || role == config.PrimaryRole) continue;
                    bool enabled = config.EnabledRoles.HasFlag(role);
                    if (ImGui.Checkbox($"{role}##secondary", ref enabled))
                    {
                        if (enabled)
                            config.EnabledRoles |= role;
                        else
                            config.EnabledRoles &= ~role;
                        config.Save();
                    }
                }
                ImGui.Unindent();
            }
            ImGui.Spacing();
        }

        // ── Integrations ──
        if (ImGui.CollapsingHeader("Integrations", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            var enableGlam = config.EnableGlamourer;
            if (ImGui.Checkbox("Enable Glamourer Integration", ref enableGlam))
            {
                config.EnableGlamourer = enableGlam;
                config.Save();
            }

            var enableChat = config.EnableChatTwo;
            if (ImGui.Checkbox("Enable ChatTwo Integration", ref enableChat))
            {
                config.EnableChatTwo = enableChat;
                config.Save();
            }
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
                
                var title = m.Title;
                var text = m.Text;
                
                if (ImGui.InputText("Title", ref title, 50))
                {
                    m.Title = title;
                    config.Save();
                }
                if (ImGui.InputTextMultiline("Text", ref text, 500, new Vector2(-1, 60)))
                {
                    m.Text = text;
                    config.Save();
                }
                if (ImGui.Button("Delete"))
                {
                    config.Macros.RemoveAt(i);
                    config.Save();
                    ImGui.PopID();
                    break;
                }
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
                ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), "✔️ Management Mode Active");
            }
            else
            {
                var code = "";
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputTextWithHint("##mgmtcode", "Enter Passcode", ref code, 20, ImGuiInputTextFlags.Password))
                {
                    if (code == "pixie13!?")
                    {
                        config.IsManagementModeEnabled = true;
                        config.Save();
                    }
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(Locked)");
            }
            ImGui.Spacing();
        }

        // ── Support ──
        if (ImGui.CollapsingHeader("Support & Feedback"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Thank you for helping us improve Candy Coat! <3");
            ImGui.BulletText("Bugs & Crashes: Report via Discord (DM me) or GitHub Issues.");
            ImGui.BulletText("Suggestions: Use the #🍰-staff-bot-testing channel on Discord.");
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "Reporting Format:");
            ImGui.TextDisabled("1. Description of the issue\n2. Steps to reproduce\n3. Attach any relevant screenshots or logs");

            if (ImGui.Button("Open GitHub Issues"))
            {
                ECommons.GenericHelpers.ShellStart("https://github.com/YelenaTor/candy-coat/issues");
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy Discord Link"))
            {
                ImGui.SetClipboardText("https://discord.gg/your-discord-link");
            }
            ImGui.Spacing();
        }
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
