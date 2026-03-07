using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using System.Linq;
using System.Collections.Generic;
using CandyCoat.Windows;
using CandyCoat.Windows.SRT;
using CandyCoat.Windows.Tabs;
using CandyCoat.Data;
using CandyCoat.Services;
using CandyCoat.IPC;
using CandyCoat.UI;
using CandyCoat.UI.Toolbar;

using ECommons;
using ECommons.DalamudServices;
using Una.Drawing;

namespace CandyCoat;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;

    private const string MainCommandName = "/candy";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("CandyCoat");
    public ToolbarService ToolbarService { get; init; }

    public SessionManager SessionManager { get; init; }
    public VenueService VenueService { get; init; }
    public LocatorService LocatorService { get; init; }
    public TradeMonitorService TradeMonitorService { get; init; }
    public WaitlistManager WaitlistManager { get; init; }
    public ShiftManager ShiftManager { get; init; }
    public SyncService SyncService { get; init; }
    public PatronAlertService PatronAlertService { get; init; }
    public TellService TellService { get; init; }
    public TellWindow TellWindow { get; init; }

    public CosmeticFontManager CosmeticFontManager { get; init; }
    public CosmeticBadgeManager CosmeticBadgeManager { get; init; }
    public CosmeticWindow CosmeticWindow { get; init; }

    public SessionWindow SessionWindow { get; init; }
    private SetupWindow SetupWindow { get; init; }
    public ProfileWindow ProfileWindow { get; init; }
    private PatronDetailsWindow PatronDetailsWindow { get; init; }
    private PatronAlertOverlay PatronAlertOverlay { get; init; }
    private SrtFeatureWindow SrtFeatureWindow { get; init; }
    private ChatTwoIpc ChatTwoIpc { get; init; }
    private NameplateRenderer NameplateRenderer { get; init; }

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);
        DrawingLib.Setup(PluginInterface);
        CandyTheme.Apply();

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var needsProfileSync = MigrateConfig();

        CosmeticFontManager = new CosmeticFontManager();
        CosmeticBadgeManager = new CosmeticBadgeManager();
        CosmeticWindow = new CosmeticWindow(this, CosmeticFontManager, CosmeticBadgeManager);

        // Initialize Services
        SessionManager = new SessionManager();
        VenueService = new VenueService(this);
        LocatorService = new LocatorService(this);
        TradeMonitorService = new TradeMonitorService(this);
        WaitlistManager = new WaitlistManager();
        ShiftManager = new ShiftManager(this);
        SyncService = new SyncService(this);

        // One-time profile upsert for existing Sugar installs that just got VenueId backfilled
        if (needsProfileSync && Configuration.IsSetupComplete && !string.IsNullOrEmpty(Configuration.ProfileId))
        {
            SyncService.UpsertProfileAsync(
                Configuration.ProfileId,
                Configuration.CharacterName,
                Configuration.HomeWorld,
                Configuration.UserMode,
                Configuration.VenueId);
        }

        PatronAlertService = new PatronAlertService(this, LocatorService);
        TellService = new TellService(this);
        var glamourerIpc = new GlamourerIpc();

        PatronDetailsWindow = new PatronDetailsWindow(this, glamourerIpc);
        ProfileWindow = new ProfileWindow(this);
        SrtFeatureWindow = new SrtFeatureWindow(Configuration);
        SessionWindow = new SessionWindow(SessionManager, PluginInterface.ConfigDirectory.FullName);
        PatronAlertOverlay = new PatronAlertOverlay(this, PatronAlertService);
        TellWindow = new TellWindow(this);

        WindowSystem.AddWindow(PatronDetailsWindow);
        WindowSystem.AddWindow(ProfileWindow);
        WindowSystem.AddWindow(SrtFeatureWindow);
        WindowSystem.AddWindow(SessionWindow);
        WindowSystem.AddWindow(CosmeticWindow);
        WindowSystem.AddWindow(PatronAlertOverlay);
        WindowSystem.AddWindow(TellWindow);

        // Build toolbar entries
        var bookingsTab = new BookingsTab(this, VenueService);
        bookingsTab.OnPatronSelected += p => { if (p != null) PatronDetailsWindow.OpenForPatron(p); };

        var locatorTab = new LocatorTab(this, VenueService);
        locatorTab.OnPatronSelected += p => { if (p != null) PatronDetailsWindow.OpenForPatron(p); };

        var overviewTabs = new List<ITab>
        {
            new OverviewTab(this),
            bookingsTab,
            locatorTab,
            new SessionTab(this),
            new WaitlistTab(WaitlistManager),
            new StaffTab(ShiftManager),
        };

        var srtPanels = new List<(IToolboxPanel Panel, string Icon)>
        {
            (new SweetheartPanel(this), "\uF004"),
            (new CandyHeartPanel(this), "\uF0A0"),
            (new BartenderPanel(this), "\uF000"),
            (new GambaPanel(this), "\uF11B"),
            (new DJPanel(this), "\uF001"),
            (new ManagementPanel(this), "\uF0E8"),
            (new OwnerPanel(this), "\uF521"),
            (new GreeterPanel(this), "\uF2B9"),
        };

        var entries = new List<IToolbarEntry>();
        entries.Add(new OverviewEntry(overviewTabs));
        foreach (var (panel, icon) in srtPanels)
            entries.Add(new SrtEntry(panel, icon));
        entries.Add(new SettingsEntry(new SettingsPanel(this)));

        ToolbarService = new ToolbarService(PluginInterface, Configuration);
        ToolbarService.SetEntries(entries);

        // Initialize IPC
        ChatTwoIpc = new ChatTwoIpc(
            onStartCapture: (targetName) =>
            {
                SessionManager.StartCapture(targetName);
                SessionWindow.IsOpen = true;
            },
            onOpenTells: (targetName) =>
            {
                TellService.GetOrCreateConversation(targetName);
                TellService.SelectConversation(targetName);
                TellWindow.IsOpen = true;
            });
        ChatTwoIpc.Enable();

        // Initialize Setup Window
        SetupWindow = new SetupWindow(this);
        WindowSystem.AddWindow(SetupWindow);

        NameplateRenderer = new NameplateRenderer(this, CosmeticFontManager, CosmeticBadgeManager);

        // Startup Logic
        if (!Configuration.IsSetupComplete)
        {
            SetupWindow.IsOpen = true;
        }
        // Toolbar renders automatically via UiBuilder.Draw; no explicit open needed.

        CommandManager.AddHandler(MainCommandName, new CommandInfo(OnMainCommand)
        {
            HelpMessage = "Open Candy Coat's main interface."
        });

        // Context Menu
        Svc.ContextMenu.OnMenuOpened += OnMenuOpened;

        // Tell the UI system that we want our windows to be drawn throught he window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // This adds a button to the plugin installer entry of this plugin which allows
        // Add a simple log message to indicate Candy Coat loaded correctly
        Log.Information($"[CandyCoat] Started successfully.");
    }

    public void Dispose()
    {
        // 1. Unregister all hooks and event handlers first
        Svc.ContextMenu.OnMenuOpened -= OnMenuOpened;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        // 2. Remove windows from window system
        WindowSystem.RemoveAllWindows();

        // 3. Dispose IPC (before ECommons since it may use Svc)
        ChatTwoIpc?.Disable();
        ChatTwoIpc?.Dispose();

        // 4. Dispose all windows and services that hold Una.Drawing nodes
        ToolbarService?.Dispose();
        NameplateRenderer?.Dispose();
        PatronDetailsWindow?.Dispose();
        ProfileWindow?.Dispose();
        SessionWindow?.Dispose();
        SetupWindow?.Dispose();
        TellService?.Dispose();
        TellWindow?.Dispose();
        SessionManager?.Dispose();
        LocatorService?.Dispose();
        TradeMonitorService?.Dispose();
        SyncService?.Dispose();
        PatronAlertService?.Dispose();
        CosmeticFontManager?.Dispose();
        CosmeticBadgeManager?.Dispose();
        CosmeticWindow?.Dispose();

        CommandManager.RemoveHandler(MainCommandName);

        // 5. DrawingLib last — after all nodes are disposed
        DrawingLib.Dispose();

        // 6. ECommons last of all
        ECommonsMain.Dispose();
    }

    private void OnMainCommand(string command, string args)
    {
        if (!Configuration.IsSetupComplete) { SetupWindow.IsOpen = true; return; }
        // Toolbar is always visible; /candy is a no-op once setup is complete.
    }
    
    /// <summary>
    /// Backfills permanent constants into config on every load so existing installs
    /// never need manual entry and are always pointing at the production API.
    /// Returns true if VenueId was just backfilled (triggers a one-time profile upsert).
    /// </summary>
    private bool MigrateConfig()
    {
        var cfg = Configuration;
        bool dirty = false;
        bool didSetVenueId = false;

        if (string.IsNullOrEmpty(cfg.ApiUrl))
            { cfg.ApiUrl = PluginConstants.ProductionApiUrl; dirty = true; }

        if (string.IsNullOrEmpty(cfg.VenueKey))
            { cfg.VenueKey = PluginConstants.VenueKey; dirty = true; }

        if (string.IsNullOrEmpty(cfg.VenueName))
            { cfg.VenueName = "Sugar"; dirty = true; }

        // Backfill VenueId for existing Sugar installations
        if (string.IsNullOrEmpty(cfg.VenueId) && cfg.VenueKey == PluginConstants.VenueKey)
        {
            cfg.VenueId = PluginConstants.SugarVenueId;
            dirty = true;
            didSetVenueId = true;
        }

        if (dirty) cfg.Save();
        return didSetVenueId;
    }

    public void OnSetupComplete()
    {
        // Toolbar is always visible; nothing to open explicitly.
    }

    public void ToggleMainUi()
    {
        if (!Configuration.IsSetupComplete)
        {
            SetupWindow.IsOpen = true;
        }
    }

    public void ToggleConfigUi()
    {
        if (!Configuration.IsSetupComplete)
        {
            SetupWindow.IsOpen = true;
        }
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        // For players, we check the target manager
        if (Svc.Targets.Target is not IPlayerCharacter pc)
            return;

        var name = pc.Name.ToString();

        var item = new MenuItem
        {
            Name = "Add as Regular",
            OnClicked = _ =>
            {
                var patron = EnsurePatronExists(name);
                patron.Status = PatronStatus.Regular;

                if (pc.HomeWorld.IsValid)
                    patron.World = pc.HomeWorld.Value.Name.ToString();
                else if (Svc.PlayerState.HomeWorld.IsValid)
                    patron.World = Svc.PlayerState.HomeWorld.Value.Name.ToString();

                Configuration.Save();
            }
        };

        args.AddMenuItem(item);
    }

    public Patron EnsurePatronExists(string name) => VenueService.EnsurePatronExists(name);
}
