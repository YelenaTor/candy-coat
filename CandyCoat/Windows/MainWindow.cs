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

    private readonly List<ITab> dashboardTabs = new();
    private readonly List<IToolboxPanel> srtPanels = new();

    // Sidebar state
    private enum SidebarSection { Dashboard, SRT, Settings }
    private SidebarSection _activeSection = SidebarSection.Dashboard;
    private int _selectedDashboardIndex = 0;
    private int _selectedSrtIndex = 0;

    // Password gate for protected roles
    private string _rolePassword = string.Empty;
    private bool _rolePasswordUnlocked = false;
    private const string ProtectedRolePassword = "pixie13!?";

    private const float SidebarWidth = 160f;

    public MainWindow(Plugin plugin, VenueService venueService, WaitlistManager waitlistManager, ShiftManager shiftManager, PatronDetailsWindow detailsWindow, string goatImagePath, CosmeticWindow cosmeticWindow)
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
            ImGui.PushStyleColor(ImGuiCol.ChildBg, CandyCoat.UI.StyleManager.SidebarBg);
            {
                using var sidebar = ImRaii.Child("##Sidebar", new Vector2(SidebarWidth, contentRegion.Y), true);
                ImGui.PopStyleColor();
                DrawSidebar();
            }

            // ═══════════════════════════════════════════
            //  RIGHT CONTENT PANEL
            // ═══════════════════════════════════════════
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
                        ImGui.PushStyleColor(ImGuiCol.Text, CandyCoat.UI.StyleManager.SectionHeader);

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

        // ── Sync Status + Cosmetics + Settings (pinned to bottom) ──
        var syncService = plugin.SyncService;
        var syncHeight = plugin.Configuration.EnableSync ? ImGui.GetFrameHeightWithSpacing() : 0;
        var footerHeight = ImGui.GetFrameHeightWithSpacing() * 2 + ImGui.GetStyle().ItemSpacing.Y + syncHeight;
        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - footerHeight - ImGui.GetStyle().WindowPadding.Y);

        if (plugin.Configuration.EnableSync)
        {
            if (syncService.IsWaking)
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⏳ Connecting...");
            else if (syncService.IsConnected)
                ImGui.TextColored(CandyCoat.UI.StyleManager.SyncOk, "🟢 Synced");
            else
                ImGui.TextColored(CandyCoat.UI.StyleManager.SyncError, "🔴 Offline");
        }

        ImGui.Separator();

        bool cosmeticsOpen = _cosmeticWindow.IsOpen;
        if (cosmeticsOpen)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.9f, 1f));

        if (ImGui.Selectable("✨ Cosmetics", cosmeticsOpen))
            _cosmeticWindow.Toggle();

        if (cosmeticsOpen)
            ImGui.PopStyleColor();

        bool settingsSelected = _activeSection == SidebarSection.Settings;
        if (settingsSelected)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 1f, 1f));

        if (ImGui.Selectable("⚙ Settings", settingsSelected))
            _activeSection = SidebarSection.Settings;

        if (settingsSelected)
            ImGui.PopStyleColor();
    }

    private void DrawContentPanel()
    {
        // Wake overlay when sync is connecting
        if (plugin.Configuration.EnableSync && plugin.SyncService.IsWaking && _activeSection != SidebarSection.Settings)
        {
            var region = ImGui.GetContentRegionAvail();
            ImGui.SetCursorPos(new Vector2(region.X / 2 - 80, region.Y / 2 - 40));
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "☁ Waking up...");
            ImGui.SetCursorPosX(region.X / 2 - 100);
            ImGui.TextDisabled("Connecting to Candy Coat API");
            
            ImGui.Spacing();
            ImGui.SetCursorPosX(region.X / 2 - 15);
            OtterGui.Text.ImUtf8.Spinner("SyncWakingSpinner"u8, 15f, 3, ImGui.GetColorU32(new Vector4(1f, 0.6f, 0.8f, 1f)));
            return;
        }

        // Error overlay when connection fails
        if (plugin.Configuration.EnableSync && !plugin.SyncService.IsConnected && _activeSection != SidebarSection.Settings)
        {
            var region = ImGui.GetContentRegionAvail();
            ImGui.SetCursorPosY(region.Y / 2 - 100);
            
            var title = "🔴 API Connection Failed";
            ImGui.SetCursorPosX(region.X / 2 - ImGui.CalcTextSize(title).X / 2);
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), title);
            
            ImGui.Spacing();
            var errText = $"Error: {plugin.SyncService.LastError ?? "Unknown"}";
            ImGui.SetCursorPosX(region.X / 2 - ImGui.CalcTextSize(errText).X / 2);
            ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), errText);
            
            ImGui.Spacing();
            var causesTitle = "Possible causes:";
            ImGui.SetCursorPosX(region.X / 2 - ImGui.CalcTextSize(causesTitle).X / 2);
            ImGui.TextDisabled(causesTitle);
            
            var c1 = "• The API/Database container is offline.";
            ImGui.SetCursorPosX(region.X / 2 - ImGui.CalcTextSize(c1).X / 2);
            ImGui.TextDisabled(c1);
            
            var c2 = "• Invalid API URL or Venue Key in Settings.";
            ImGui.SetCursorPosX(region.X / 2 - ImGui.CalcTextSize(c2).X / 2);
            ImGui.TextDisabled(c2);
            
            var c3 = "• Network interruption.";
            ImGui.SetCursorPosX(region.X / 2 - ImGui.CalcTextSize(c3).X / 2);
            ImGui.TextDisabled(c3);

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.SetCursorPosX(region.X / 2 - 15);
            OtterGui.Text.ImUtf8.Spinner("SyncErrorSpinner"u8, 15f, 3, ImGui.GetColorU32(new Vector4(1f, 0.3f, 0.3f, 1f)));

            ImGui.Spacing();
            ImGui.Spacing();
            var btnText = "Retry Connection";
            ImGui.SetCursorPosX(region.X / 2 - 60);
            if (ImGui.Button(btnText, new Vector2(120, 30)))
            {
                _ = plugin.SyncService.WakeAsync();
            }

            return;
        }

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
                if (visiblePanels.Count == 0)
                {
                    _selectedSrtIndex = 0;
                    ImGui.TextDisabled("No roles enabled. Set up in Settings.");
                }
                else if (_selectedSrtIndex >= 0 && _selectedSrtIndex < visiblePanels.Count)
                {
                    visiblePanels[_selectedSrtIndex].DrawContent();
                }
                else
                {
                    _selectedSrtIndex = 0;
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
            ImGui.Spacing();

            foreach (StaffRole role in Enum.GetValues<StaffRole>())
            {
                if (role == StaffRole.None) continue;
                bool isPrimary = config.PrimaryRole == role;
                bool isProtected = role == StaffRole.Owner || role == StaffRole.Management;

                if (isProtected && !_rolePasswordUnlocked && !config.IsManagementModeEnabled)
                {
                    ImGui.BeginDisabled();
                    ImGui.RadioButton($"{role} 🔒", isPrimary);
                    ImGui.EndDisabled();
                }
                else
                {
                    if (ImGui.RadioButton(role.ToString(), isPrimary))
                    {
                        config.PrimaryRole = role;
                        config.EnabledRoles |= role;
                        config.Save();
                    }
                }
            }

            // Password unlock for protected roles
            if (!_rolePasswordUnlocked && !config.IsManagementModeEnabled)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "🔒 Owner & Management require a passcode.");
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputTextWithHint("##rolePw", "Enter Passcode", ref _rolePassword, 30, ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (_rolePassword == ProtectedRolePassword)
                    {
                        _rolePasswordUnlocked = true;
                        config.IsManagementModeEnabled = true;
                        config.Save();
                    }
                    _rolePassword = string.Empty;
                }
            }

            ImGui.Spacing();
            var multiRole = config.MultiRoleEnabled;
            if (ImGui.Checkbox("Enable Multiple Roles", ref multiRole))
            {
                config.MultiRoleEnabled = multiRole;
                if (!multiRole)
                {
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
                    bool isProtected = role == StaffRole.Owner || role == StaffRole.Management;

                    if (isProtected && !_rolePasswordUnlocked && !config.IsManagementModeEnabled)
                    {
                        ImGui.BeginDisabled();
                        ImGui.Checkbox($"{role} 🔒##secondary", ref enabled);
                        ImGui.EndDisabled();
                    }
                    else
                    {
                        if (ImGui.Checkbox($"{role}##secondary", ref enabled))
                        {
                            if (enabled)
                                config.EnabledRoles |= role;
                            else
                                config.EnabledRoles &= ~role;
                            config.Save();
                        }
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

        // ── Sync / API Configuration ──
        if (ImGui.CollapsingHeader("Sync / API Configuration"))
        {
            ImGui.Spacing();
            var enableSync = config.EnableSync;
            if (ImGui.Checkbox("Enable Sync", ref enableSync))
            {
                config.EnableSync = enableSync;
                config.Save();
                if (!enableSync)
                    plugin.SyncService.Sleep();
            }

            if (config.EnableSync)
            {
                ImGui.Spacing();
                var apiUrl = config.ApiUrl;
                ImGui.SetNextItemWidth(300);
                if (ImGui.InputTextWithHint("##apiUrl", "http://localhost:5000", ref apiUrl, 200))
                {
                    config.ApiUrl = apiUrl;
                    config.Save();
                }
                ImGui.SameLine();
                ImGui.Text("API URL");

                var venueKey = config.VenueKey;
                ImGui.SetNextItemWidth(300);
                if (ImGui.InputTextWithHint("##venueKey", "00000000-0000-0000-0000-000000000000", ref venueKey, 100))
                {
                    config.VenueKey = venueKey;
                    config.Save();
                }
                ImGui.SameLine();
                ImGui.Text("Venue Key");

                ImGui.Spacing();
                if (ImGui.Button("Test Connection"))
                {
                    _ = plugin.SyncService.WakeAsync();
                }
                ImGui.SameLine();

                var sync = plugin.SyncService;
                if (sync.IsWaking)
                    ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "⏳ Connecting...");
                else if (sync.IsConnected)
                    ImGui.TextColored(CandyCoat.UI.StyleManager.SyncOk, "🟢 Connected");
                else if (!string.IsNullOrEmpty(sync.LastError))
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), $"🔴 {sync.LastError}");
                else
                    ImGui.TextDisabled("Not connected");
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
                ImGui.TextColored(CandyCoat.UI.StyleManager.SyncOk, "✔️ Management Mode Active");
            }
            else
            {
                var code = "";
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputTextWithHint("##mgmtcode", "Enter Passcode", ref code, 20, ImGuiInputTextFlags.Password))
                {
                    if (code == ProtectedRolePassword)
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
