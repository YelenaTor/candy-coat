using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ImGuiNET;
using ECommons.DalamudServices;

namespace SamplePlugin.IPC;

public class ChatTwoIpc : IDisposable
{
    // This is used to register your plugin with the IPC. It will return an ID
    // that you should save for later.
    private ICallGateSubscriber<string> Register { get; }
    // This is used to unregister your plugin from the IPC. You should call this
    // when your plugin is unloaded.
    private ICallGateSubscriber<string, object?> Unregister { get; }
    // You should subscribe to this event in order to receive a notification
    // when Chat 2 is loaded or updated, so you can re-register.
    private ICallGateSubscriber<object?> Available { get; }
    // Subscribe to this to draw your custom context menu items.
    private ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> Invoke { get; }

    // The registration ID.
    private string? _id;
    
    // Callback action when menu item is clicked
    private readonly Action<string> _onStartCapture;

    public ChatTwoIpc(Action<string> onStartCapture)
    {
        var inter = Svc.PluginInterface;
        this.Register = inter.GetIpcSubscriber<string>("ChatTwo.Register");
        this.Unregister = inter.GetIpcSubscriber<string, object?>("ChatTwo.Unregister");
        this.Invoke = inter.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>("ChatTwo.Invoke");
        this.Available = inter.GetIpcSubscriber<object?>("ChatTwo.Available");
        
        this._onStartCapture = onStartCapture;
    }

    public void Enable() {
        // When Chat 2 becomes available (if it loads after this plugin) or when
        // Chat 2 is updated, register automatically.
        this.Available.Subscribe(OnAvailable);
        // Register if Chat 2 is already loaded.
        DoRegister();

        // Listen for context menu events.
        this.Invoke.Subscribe(this.Integration);
    }
    
    private void OnAvailable()
    {
        DoRegister();
    }

    private void DoRegister() {
        // Register and save the registration ID.
        try
        {
            this._id = this.Register.InvokeFunc();
        }
        catch (Exception)
        {
            // ChatTwo might not be present
        }
    }

    public void Disable() {
        if (this._id != null) {
            try 
            {
                this.Unregister.InvokeAction(this._id);
            }
            catch { /* Ignore */ }
            this._id = null;
        }

        this.Invoke.Unsubscribe(this.Integration);
        this.Available.Unsubscribe(OnAvailable);
    }
    
    public void Dispose()
    {
        Disable();
    }

    private void Integration(string id, PlayerPayload? sender, ulong contentId, Payload? payload, SeString? senderString, SeString? content) {
        // Make sure the ID is the same as the saved registration ID.
        if (id != this._id) {
            return;
        }

        // Draw your custom menu items here.
        if (sender != null && !string.IsNullOrEmpty(sender.PlayerName))
        {
            if (ImGui.Selectable("Start Candy Session")) {
                var name = sender.PlayerName;
                // If cross world, it might contain world name? PlayerPayload generally separates them.
                // kept simple for now.
                _onStartCapture?.Invoke(name);
            }
        }
    }
}
