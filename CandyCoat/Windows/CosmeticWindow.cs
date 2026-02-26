using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;
using CandyCoat.Windows.Tabs;

namespace CandyCoat.Windows;

public class CosmeticWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly CosmeticDrawerTab _tab;
    private DateTime _lastAutoRedraw = DateTime.MinValue;
    private const double AutoRedrawIntervalSeconds = 30.0;

    public CosmeticWindow(Plugin plugin, CosmeticFontManager fontManager, CosmeticBadgeManager badgeManager)
        : base("✨ Cosmetic Drawer##CandyCoatCosmetics", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 520),
            MaximumSize = new Vector2(800, 900),
        };
        _plugin = plugin;
        _tab = new CosmeticDrawerTab(plugin, fontManager, badgeManager);
    }

    public override void Draw()
    {
        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        var contentHeight = ImGui.GetContentRegionAvail().Y - footerHeight;

        if (ImGui.BeginChild("CosmeticContent", new Vector2(0, contentHeight), false))
            _tab.DrawContent();
        ImGui.EndChild();

        ImGui.Separator();

        if (ImGui.Button("↺ Re-draw"))
            ForcePush();

        ImGui.SameLine();

        var autoRedraw = _plugin.Configuration.CosmeticAutoRedraw;
        if (ImGui.Checkbox("Auto re-draw", ref autoRedraw))
        {
            _plugin.Configuration.CosmeticAutoRedraw = autoRedraw;
            _plugin.Configuration.Save();
        }

        // Auto re-draw timer — pushes every 30 s when enabled
        if (_plugin.Configuration.CosmeticAutoRedraw &&
            (DateTime.UtcNow - _lastAutoRedraw).TotalSeconds >= AutoRedrawIntervalSeconds)
        {
            ForcePush();
        }
    }

    private void ForcePush()
    {
        _lastAutoRedraw = DateTime.UtcNow;
        if (_plugin.SyncService.IsConnected)
            _ = _plugin.SyncService.PushCosmeticsAsync(_plugin.Configuration.CosmeticProfile);
    }

    public void Dispose() { }
}
