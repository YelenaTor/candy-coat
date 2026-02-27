using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.ManagedFontAtlas;
using ECommons.DalamudServices;

namespace CandyCoat.UI;

/// <summary>
/// Manages custom font handles for the Cosmetic Drawer.
/// Drop any .ttf or .otf file into {PluginDirectory}/Fonts/ and it will appear
/// in the font selector automatically, registered under its filename (no extension).
/// </summary>
public class CosmeticFontManager : IDisposable
{
    private readonly Dictionary<string, IFontHandle> _handles = new();

    /// <summary>Font names shown in the UI. Always starts with "Default"; rest are discovered from disk.</summary>
    public string[] AvailableFonts { get; private set; } = ["Default"];

    public CosmeticFontManager()
    {
        var fontDir = Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.Directory!.FullName,
            "Fonts");

        Svc.Log.Information($"[CosmeticFontManager] Font dir: {fontDir} | Exists: {Directory.Exists(fontDir)}");

        if (Directory.Exists(fontDir))
        {
            var files = Directory.GetFiles(fontDir, "*.ttf")
                .Concat(Directory.GetFiles(fontDir, "*.otf"))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Svc.Log.Information($"[CosmeticFontManager] Found {files.Count} font file(s).");
            foreach (var file in files)
                RegisterFont(file, Path.GetFileNameWithoutExtension(file), 30f);
        }

        AvailableFonts = new[] { "Default" }
            .Concat(_handles.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private void RegisterFont(string path, string name, float sizePx)
    {
        try
        {
            _handles[name] = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk =>
                    tk.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx })));
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[CosmeticFontManager] Failed to register font '{name}': {ex}");
        }
        Svc.Log.Information($"[CosmeticFontManager] Registered font '{name}' from {path}");
    }

    /// <summary>
    /// Pushes the named font onto the ImGui font stack if loaded and available.
    /// Returns true + a disposable scope that pops the font.
    /// Returns false (scope = null) for "Default" or any unloaded font.
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
