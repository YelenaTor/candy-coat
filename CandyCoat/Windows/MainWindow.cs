using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using CandyCoat.Services;
using CandyCoat.Windows.Tabs;

namespace CandyCoat.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;
    private readonly VenueService venueService;
    private readonly PatronDetailsWindow detailsWindow;

    private readonly List<ITab> tabs = new();

    public MainWindow(Plugin plugin, VenueService venueService, WaitlistManager waitlistManager, ShiftManager shiftManager, PatronDetailsWindow detailsWindow, string goatImagePath)
        : base("Candy Coat - Sugar##CandyCoatMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
        this.venueService = venueService;
        this.detailsWindow = detailsWindow;

        // Initialize Tabs
        var bookingsTab = new BookingsTab(plugin, venueService);
        bookingsTab.OnPatronSelected += OnPatronSelected;

        var locatorTab = new LocatorTab(plugin, venueService);
        locatorTab.OnPatronSelected += OnPatronSelected;

        tabs.Add(new OverviewTab(plugin));
        tabs.Add(bookingsTab);
        tabs.Add(locatorTab);
        tabs.Add(new SessionTab(plugin));
        tabs.Add(new WaitlistTab(waitlistManager));
        tabs.Add(new StaffTab(shiftManager));
        tabs.Add(new SettingsTab(plugin));
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
        // Navigation logic if external triggers are added
    }

    public override void Draw()
    {
        CandyCoat.UI.StyleManager.PushStyles();
        
        try 
        {
            using var tabBar = ImRaii.TabBar("CandyCoatTabBar");
            if (!tabBar) return;

            foreach (var tab in tabs)
            {
                tab.Draw();
            }
        }
        finally
        {
            CandyCoat.UI.StyleManager.PopStyles();
        }
    }
}
