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
        var footerHeight = ImGui.GetFrameHeightWithSpacing() * 2 + ImGui.GetStyle().ItemSpacing.Y * 2;
        var contentHeight = ImGui.GetContentRegionAvail().Y - footerHeight;

        if (ImGui.BeginChild("CosmeticContent", new Vector2(0, contentHeight), false))
            _tab.DrawContent();
        ImGui.EndChild();

        ImGui.Separator();

        // Row 1 — On/Off toggle
        var enabled = _plugin.Configuration.EnableNameplateCosmetics;
        if (ImGui.Checkbox("Enable Candy Coat Nameplates", ref enabled))
        {
            _plugin.Configuration.EnableNameplateCosmetics = enabled;
            _plugin.Configuration.Save();
            // Force game nameplate system to reprocess all handlers so
            // our suppression (or restoration) kicks in immediately.
            Plugin.NamePlateGui.RequestRedraw();
        }

        ImGui.Separator();

        // Row 2 — Re-draw controls (local client-side refresh only)
        if (ImGui.Button("↺ Re-draw"))
            ForceLocalRedraw();

        ImGui.SameLine();

        var autoRedraw = _plugin.Configuration.CosmeticAutoRedraw;
        if (ImGui.Checkbox("Auto re-draw", ref autoRedraw))
        {
            _plugin.Configuration.CosmeticAutoRedraw = autoRedraw;
            _plugin.Configuration.Save();
        }

        // Auto re-draw timer — local redraw every 30 s when enabled
        if (_plugin.Configuration.CosmeticAutoRedraw &&
            (DateTime.UtcNow - _lastAutoRedraw).TotalSeconds >= AutoRedrawIntervalSeconds)
        {
            ForceLocalRedraw();
        }
    }

    private void ForceLocalRedraw()
    {
        _lastAutoRedraw = DateTime.UtcNow;
        Plugin.NamePlateGui.RequestRedraw();
    }

    public void Dispose() { }
}
