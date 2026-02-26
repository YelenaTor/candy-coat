using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;
using CandyCoat.Windows.Tabs;

namespace CandyCoat.Windows;

public class CosmeticWindow : Window, IDisposable
{
    private readonly CosmeticDrawerTab _tab;

    public CosmeticWindow(Plugin plugin, CosmeticFontManager fontManager, CosmeticBadgeManager badgeManager)
        : base("✨ Cosmetic Drawer##CandyCoatCosmetics", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 520),
            MaximumSize = new Vector2(800, 900),
        };
        _tab = new CosmeticDrawerTab(plugin, fontManager, badgeManager);
    }

    public override void Draw()
    {
        _tab.DrawContent();
    }

    public void Dispose() { }
}
