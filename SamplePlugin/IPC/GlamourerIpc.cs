using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;

namespace SamplePlugin.IPC;

public class GlamourerIpc : IDisposable
{
    public const string LabelGetDesignList = "Glamourer.GetDesignList";
    public const string LabelApplyDesign = "Glamourer.ApplyDesign";

    private readonly ICallGateSubscriber<Dictionary<Guid, string>> _getDesignListSubscriber;
    private readonly ICallGateSubscriber<Guid, int, uint, uint, int> _applyDesignSubscriber;

    public GlamourerIpc()
    {
        _getDesignListSubscriber = Svc.PluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>(LabelGetDesignList);
        _applyDesignSubscriber = Svc.PluginInterface.GetIpcSubscriber<Guid, int, uint, uint, int>(LabelApplyDesign);
    }

    public Dictionary<Guid, string> GetDesignList()
    {
        try
        {
            return _getDesignListSubscriber.InvokeFunc();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to get Glamourer design list: {ex.Message}");
            return new Dictionary<Guid, string>();
        }
    }

    public void ApplyDesign(Guid designId)
    {
        try
        {
            // Apply to LocalPlayer (Index 0)
            // ApplyFlag: 0 (Manual) or check Api.Enums
            // We'll use 0 for objectIndex (LocalPlayer)
            
            // Note: The signature in the provided API was:
            // GlamourerApiEc ApplyDesign(Guid designId, int objectIndex, uint key, ApplyFlag flags);
            // We need to map this to the IPC subscriber signature.
            // Be careful with Enum types in IPC, often passed as int/uint.
            
            _applyDesignSubscriber.InvokeFunc(designId, 0, 0, 0); 
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to apply Glamourer design: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Subscribers don't strictly need disposal implies unregistration, 
        // but it's good practice if the interface changes.
    }
}
