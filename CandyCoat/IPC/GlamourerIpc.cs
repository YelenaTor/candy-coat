using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;

namespace CandyCoat.IPC;

public class GlamourerIpc : IDisposable
{
    public const string LabelGetDesignList = "Glamourer.GetDesignList";
    public const string LabelApplyDesign = "Glamourer.ApplyDesign";

    private readonly ICallGateSubscriber<Dictionary<Guid, string>> _getDesignListSubscriber;
    private readonly ICallGateSubscriber<Guid, int, uint, uint, int> _applyDesignSubscriber;
    private readonly ICallGateSubscriber<int> _apiVersionSubscriber;

    // Based on Glamourer Api Enums
    [Flags]
    public enum ApplyFlag : uint
    {
        Once = 1 << 0,
        Equipment = 1 << 1,
        Customization = 1 << 2,
        // ... (We only strictly need 0 (Manual) or Once for basic usage, but typing it helps)
    }

    public GlamourerIpc()
    {
        _getDesignListSubscriber = Svc.PluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>(LabelGetDesignList);
        _applyDesignSubscriber = Svc.PluginInterface.GetIpcSubscriber<Guid, int, uint, uint, int>(LabelApplyDesign);
        _apiVersionSubscriber = Svc.PluginInterface.GetIpcSubscriber<int>("Glamourer.ApiVersion");
    }

    public bool IsAvailable()
    {
        try
        {
            _apiVersionSubscriber.InvokeFunc();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Dictionary<Guid, string> GetDesignList()
    {
        try
        {
            return _getDesignListSubscriber.InvokeFunc();
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[GlamourerIpc] GetDesignList failed: {ex.Message}");
            return new Dictionary<Guid, string>();
        }
    }

    public void ApplyDesign(Guid designId)
    {
        try
        {
            _applyDesignSubscriber.InvokeFunc(designId, 0, 0, (uint)0);
            Svc.Log.Info($"[GlamourerIpc] Applied design {designId}.");
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[GlamourerIpc] ApplyDesign failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Subscribers don't strictly need disposal implies unregistration, 
        // but it's good practice if the interface changes.
    }
}
