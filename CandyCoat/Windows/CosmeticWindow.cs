using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;
using CandyCoat.Windows.Tabs;
using Una.Drawing;

namespace CandyCoat.Windows;

public class CosmeticWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly CosmeticDrawerTab _tab;
    private Node? _root;
    private DateTime _lastAutoRedraw = DateTime.MinValue;
    private const double AutoRedrawIntervalSeconds = 30.0;

    public CosmeticWindow(Plugin plugin, CosmeticFontManager fontManager, CosmeticBadgeManager badgeManager)
        : base("Cosmetic Drawer##CandyCoatCosmetics", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 520),
            MaximumSize = new Vector2(800, 900),
        };
        _plugin = plugin;
        _tab = new CosmeticDrawerTab(plugin, fontManager, badgeManager);
    }

    public void Dispose()
    {
        _root?.Dispose();
        _root = null;
    }

    private void BuildRoot()
    {
        _root?.Dispose();

        // Content area grows to fill available space
        var contentSpacer = CandyUI.InputSpacer("cosmetic-content-spacer", 0, 0);
        contentSpacer.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow);
        var contentCard = CandyUI.Card("cosmetic-content-card", contentSpacer);
        contentCard.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow);

        // Footer row 1: enable nameplate checkbox spacer (full width)
        var enableSpacer = CandyUI.InputSpacer("cosmetic-enable-spacer", 0, 24);
        enableSpacer.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit);

        // Footer row 2: re-draw button + auto re-draw checkbox
        var redrawSpacer    = CandyUI.InputSpacer("cosmetic-redraw-spacer", 70, 24);
        var autoRedrawSpacer = CandyUI.InputSpacer("cosmetic-autoredraw-spacer", 0, 24);
        autoRedrawSpacer.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit);

        _root = CandyUI.Column("cosmetic-root", 4,
            contentCard,
            CandyUI.Separator("cosmetic-sep1"),
            enableSpacer,
            CandyUI.Separator("cosmetic-sep2"),
            CandyUI.Row("cosmetic-redraw-row", 8, redrawSpacer, autoRedrawSpacer)
        );
    }

    public override void Draw()
    {
        if (_root == null) BuildRoot();

        var region = ImGui.GetContentRegionAvail();
        _root!.Style.Size = new Size((int)region.X, (int)region.Y);

        var pos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _root.Render(ImGui.GetWindowDrawList(), pos);
        ImGui.Dummy(region);

        DrawOverlays();
    }

    private void DrawOverlays()
    {
        // Una.Drawing bounds are screen-space; subtract window origin for ImGui cursor coords.
        var origin = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();

        // Content area — render the cosmetic tab in the growing card spacer
        var contentSpacer = _root!.QuerySelector("#cosmetic-content-spacer");
        if (contentSpacer != null)
        {
            var r = contentSpacer.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            if (ImGui.BeginChild("CosmeticContent", new Vector2(r.Width, r.Height), false))
                _tab.DrawContent();
            ImGui.EndChild();
        }

        // Row 1 — Enable nameplate checkbox
        var enableSpacer = _root!.QuerySelector("#cosmetic-enable-spacer");
        if (enableSpacer != null)
        {
            var r = enableSpacer.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            var enabled = _plugin.Configuration.EnableNameplateCosmetics;
            if (ImGui.Checkbox("Enable Candy Coat Nameplates", ref enabled))
            {
                _plugin.Configuration.EnableNameplateCosmetics = enabled;
                _plugin.Configuration.Save();
                Plugin.NamePlateGui.RequestRedraw();
            }
        }

        // Row 2 — Re-draw button
        var redrawSpacer = _root!.QuerySelector("#cosmetic-redraw-spacer");
        if (redrawSpacer != null)
        {
            var r = redrawSpacer.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            if (ImGui.Button("Re-draw"))
                ForceLocalRedraw();
        }

        // Row 2 — Auto re-draw checkbox
        var autoRedrawSpacer = _root!.QuerySelector("#cosmetic-autoredraw-spacer");
        if (autoRedrawSpacer != null)
        {
            var r = autoRedrawSpacer.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            var autoRedraw = _plugin.Configuration.CosmeticAutoRedraw;
            if (ImGui.Checkbox("Auto re-draw", ref autoRedraw))
            {
                _plugin.Configuration.CosmeticAutoRedraw = autoRedraw;
                _plugin.Configuration.Save();
            }
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
}
