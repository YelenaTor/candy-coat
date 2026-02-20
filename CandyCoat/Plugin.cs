using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using CandyCoat.Windows;

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

    private const string MainCommandName = "/candy";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("CandyCoat");
    private MainWindow MainWindow { get; init; }

    public CandyCoat.Services.SessionManager SessionManager { get; init; }
    public CandyCoat.Services.VenueService VenueService { get; init; }
    public CandyCoat.Services.LocatorService LocatorService { get; init; }
    public CandyCoat.Services.TradeMonitorService TradeMonitorService { get; init; }
    public CandyCoat.Services.WaitlistManager WaitlistManager { get; init; }
    public CandyCoat.Services.ShiftManager ShiftManager { get; init; }

    private CandyCoat.Windows.SessionWindow SessionWindow { get; init; }
    private CandyCoat.Windows.SetupWindow SetupWindow { get; init; }
    private CandyCoat.Windows.PatronDetailsWindow PatronDetailsWindow { get; init; }
    private CandyCoat.IPC.ChatTwoIpc ChatTwoIpc { get; init; }

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // You might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        // Initialize Services
        SessionManager = new CandyCoat.Services.SessionManager();
        VenueService = new CandyCoat.Services.VenueService(this);
        LocatorService = new CandyCoat.Services.LocatorService(this);
        TradeMonitorService = new CandyCoat.Services.TradeMonitorService(this);
        WaitlistManager = new CandyCoat.Services.WaitlistManager();
        ShiftManager = new CandyCoat.Services.ShiftManager(this);
        var glamourerIpc = new CandyCoat.IPC.GlamourerIpc();

        PatronDetailsWindow = new CandyCoat.Windows.PatronDetailsWindow(this, glamourerIpc);
        MainWindow = new MainWindow(this, VenueService, WaitlistManager, ShiftManager, PatronDetailsWindow, goatImagePath);
        SessionWindow = new CandyCoat.Windows.SessionWindow(SessionManager);
        
        WindowSystem.AddWindow(PatronDetailsWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SessionWindow);

        // Initialize IPC
        ChatTwoIpc = new CandyCoat.IPC.ChatTwoIpc((targetName) => 
        {
            SessionManager.StartCapture(targetName);
            SessionWindow.IsOpen = true;
        });
        ChatTwoIpc.Enable();
        
        // Initialize Setup Window (needs IPCs)
        SetupWindow = new CandyCoat.Windows.SetupWindow(this, glamourerIpc, ChatTwoIpc);
        WindowSystem.AddWindow(SetupWindow);

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

        // Tell the UI system that we want our windows to be drawn throught he window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // Add a simple log message to indicate Candy Coat loaded correctly
        Log.Information($"[CandyCoat] Started successfully.");
    }

    public void Dispose()
    {
        ECommonsMain.Dispose();
        
        // Unregister all actions to not leak anythign during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ChatTwoIpc?.Disable();
        ChatTwoIpc?.Dispose();
        
        MainWindow.Dispose();
        PatronDetailsWindow.Dispose();
        SessionWindow?.Dispose();
        SetupWindow?.Dispose();
        
        SessionManager?.Dispose();
        LocatorService?.Dispose();
        TradeMonitorService?.Dispose();

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
    }
    
    public void OnSetupComplete()
    {
        MainWindow.IsOpen = true;
    }

    public void ToggleMainUi() => MainWindow.Toggle();
}
