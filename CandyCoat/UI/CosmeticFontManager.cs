using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Interface.ManagedFontAtlas;
using ECommons.DalamudServices;

namespace CandyCoat.UI;

/// <summary>
/// Manages custom TTF font handles for the Cosmetic Drawer.
/// Place .ttf files at: {PluginDirectory}/Fonts/{Name}.ttf
///   e.g.  Fonts/Script.ttf   → selectable as "Script"
///         Fonts/Bold.ttf     → selectable as "Bold"
///         Fonts/Pixel.ttf    → selectable as "Pixel"
/// </summary>
public class CosmeticFontManager : IDisposable
{
    private readonly Dictionary<string, IFontHandle> _handles = new();

    /// <summary>Font names shown in the UI. "Default" uses the standard ImGui font.</summary>
    public static readonly string[] AvailableFonts = { "Default", "Script", "Bold", "Pixel" };

    public CosmeticFontManager()
    {
        var fontDir = Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.Directory!.FullName,
            "Fonts");

        RegisterFont(fontDir, "Script", "Script.ttf", 20f);
        RegisterFont(fontDir, "Bold",   "Bold.ttf",   20f);
        RegisterFont(fontDir, "Pixel",  "Pixel.ttf",  16f);
    }

    private void RegisterFont(string fontDir, string name, string filename, float sizePx)
    {
        var path = Path.Combine(fontDir, filename);
        if (!File.Exists(path)) return;

        try
        {
            _handles[name] = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk =>
                    tk.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx })));
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[CosmeticFontManager] Failed to register font '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Pushes the named font onto the ImGui font stack if it is loaded and available.
    /// Returns true and a disposable scope that pops the font when disposed.
    /// Returns false (scope = null) for "Default" or unloaded fonts.
    /// </summary>
    public bool TryPushFont(string fontName, out IDisposable? scope)
    {
        if (fontName == "Default"
            || !_handles.TryGetValue(fontName, out var handle)
            || !handle.Available)
        {
            scope = null;
            return false;
        }

        scope = handle.Push();
        return true;
    }

    public void Dispose()
    {
        foreach (var h in _handles.Values)
            h.Dispose();
        _handles.Clear();
    }
}
