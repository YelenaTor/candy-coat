using System.Numerics;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Toolbar entry that exposes the plugin settings panel inside the toolbar balloon.
/// Wraps <see cref="SettingsPanel"/> which uses a Una.Drawing placeholder node
/// and renders its UI via raw ImGui overlays.
/// </summary>
internal class SettingsEntry : IToolbarEntry
{
    private readonly SettingsPanel _settingsPanel;

    public SettingsEntry(SettingsPanel settingsPanel)
    {
        _settingsPanel = settingsPanel;
    }

    /// <inheritdoc/>
    public string Id => "settings";

    /// <inheritdoc/>
    public string Icon => "\uF013"; // FontAwesome cog

    /// <inheritdoc/>
    public string Label => "Settings";

    /// <inheritdoc/>
    public StaffRole Role => StaffRole.None;

    /// <inheritdoc/>
    public Node BuildPanel() => _settingsPanel.BuildNode();

    /// <inheritdoc/>
    public void DrawOverlays()
    {
        var region = ImGui.GetContentRegionAvail();
        _settingsPanel.DrawOverlays(region);
    }
}
