using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;

namespace CandyCoat.UI;

/// <summary>
/// Loads 16×16 pixel-art PNG badge icons from {PluginDirectory}/Badges/.
/// Falls back to glyph rendering in CosmeticRenderer if a file is absent.
///
/// Expected files (place 16×16 PNGs):
///   Badges/Heart.png
///   Badges/Star.png
///   Badges/Crown.png
///   Badges/Moon.png
///   Badges/Diamond.png
/// </summary>
public class CosmeticBadgeManager : IDisposable
{
    /// <summary>Recommended source image size for pixel-art badges.</summary>
    public const float BadgeRenderSize = 16f;

    private readonly Dictionary<string, ISharedImmediateTexture> _textures = new();

    public CosmeticBadgeManager()
    {
        var badgeDir = Path.Combine(
            Plugin.PluginInterface.AssemblyLocation.Directory!.FullName,
            "Badges");

        foreach (var name in CosmeticRenderer.BadgeTemplates)
        {
            if (name == "None") continue;
            var path = Path.Combine(badgeDir, $"{name}.png");
            if (!File.Exists(path)) continue;

            try
            {
                _textures[name] = Plugin.TextureProvider.GetFromFile(path);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[CosmeticBadgeManager] Failed to load badge '{name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Returns the texture wrap for <paramref name="name"/> if the PNG is loaded
    /// and the texture is ready this frame. Returns null to signal glyph fallback.
    /// </summary>
    public IDalamudTextureWrap? TryGetWrap(string name)
    {
        if (!_textures.TryGetValue(name, out var tex)) return null;
        return tex.GetWrapOrDefault();
    }

    public void Dispose()
    {
        foreach (var tex in _textures.Values)
            if (tex is IDisposable d)
                d.Dispose();
        _textures.Clear();
    }
}
