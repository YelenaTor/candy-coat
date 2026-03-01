using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using System.Linq;
using CandyCoat.Windows;
using CandyCoat.Data;
using CandyCoat.Services;
using CandyCoat.IPC;
using CandyCoat.UI;

using ECommons;
using ECommons.DalamudServices;

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
    private MainWindow MainWindow { get; init; }

    public SessionManager SessionManager { get; init; }
    public VenueService VenueService { get; init; }
    public LocatorService LocatorService { get; init; }
    public TradeMonitorService TradeMonitorService { get; init; }
    public WaitlistManager WaitlistManager { get; init; }
    public ShiftManager ShiftManager { get; init; }
    public SyncService SyncService { get; init; }

    public CosmeticFontManager CosmeticFontManager { get; init; }
    public CosmeticBadgeManager CosmeticBadgeManager { get; init; }
    public CosmeticWindow CosmeticWindow { get; init; }

    private SessionWindow SessionWindow { get; init; }
    private SetupWindow SetupWindow { get; init; }
    public ProfileWindow ProfileWindow { get; init; }
    private PatronDetailsWindow PatronDetailsWindow { get; init; }
    private ChatTwoIpc ChatTwoIpc { get; init; }
    private NameplateRenderer NameplateRenderer { get; init; }

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // You might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

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
        var glamourerIpc = new GlamourerIpc();

        PatronDetailsWindow = new PatronDetailsWindow(this, glamourerIpc);
        ProfileWindow = new ProfileWindow(this);
        MainWindow = new MainWindow(this, VenueService, WaitlistManager, ShiftManager, PatronDetailsWindow, goatImagePath, CosmeticWindow, ProfileWindow);
        SessionWindow = new SessionWindow(SessionManager, PluginInterface.ConfigDirectory.FullName);

        WindowSystem.AddWindow(PatronDetailsWindow);
        WindowSystem.AddWindow(ProfileWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SessionWindow);
        WindowSystem.AddWindow(CosmeticWindow);

        // Initialize IPC
        ChatTwoIpc = new ChatTwoIpc((targetName) =>
        {
            SessionManager.StartCapture(targetName);
            SessionWindow.IsOpen = true;
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
        else
        {
            // Normal startup
            // MainWindow.IsOpen = true; // Or keep closed until command? 
            // Usually we don't auto-open main window on load unless configured.
        }

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
        ECommonsMain.Dispose();
        
        // Unregister all actions to not leak anythign during disposal of plugin
        Svc.ContextMenu.OnMenuOpened -= OnMenuOpened;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        
        WindowSystem.RemoveAllWindows();

        ChatTwoIpc?.Disable();
        ChatTwoIpc?.Dispose();
        NameplateRenderer.Dispose();
        
        MainWindow.Dispose();
        PatronDetailsWindow.Dispose();
        ProfileWindow?.Dispose();
        SessionWindow?.Dispose();
        SetupWindow?.Dispose();
        
        SessionManager?.Dispose();
        LocatorService?.Dispose();
        TradeMonitorService?.Dispose();
        SyncService?.Dispose();
        CosmeticFontManager?.Dispose();
        CosmeticBadgeManager?.Dispose();
        CosmeticWindow?.Dispose();

        CommandManager.RemoveHandler(MainCommandName);
    }

    private void OnMainCommand(string command, string args)
    {
        if (!Configuration.IsSetupComplete)
        {
            SetupWindow.IsOpen = true;
            return;
        }
        
        // In response to the slash command, toggle the display status of our main ui
        MainWindow.Toggle();

        // Wake/sleep sync service based on UI state
        if (MainWindow.IsOpen)
            _ = SyncService.WakeAsync().ContinueWith(
                t => Log.Error($"[CandyCoat] WakeAsync threw: {t.Exception}"),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        else
            SyncService.Sleep();
    }
    
    public void OnSetupComplete()
    {
        MainWindow.IsOpen = true;
    }

    public void ToggleMainUi()
    {
        if (!Configuration.IsSetupComplete)
        {
            SetupWindow.IsOpen = true;
            return;
        }
        MainWindow.Toggle();
    }

    public void ToggleConfigUi()
    {
        if (!Configuration.IsSetupComplete)
        {
            SetupWindow.IsOpen = true;
            return;
        }
        MainWindow.Toggle();
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
