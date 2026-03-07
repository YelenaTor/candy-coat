using CandyCoat.Data;
using CandyCoat.Windows.SRT;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Thin adapter that wraps an <see cref="IToolboxPanel"/> as an <see cref="IToolbarEntry"/>,
/// allowing SRT panels to be displayed inside the screen-anchored toolbar balloon.
/// </summary>
public class SrtEntry : IToolbarEntry
{
    private readonly IToolboxPanel _panel;
    private readonly string        _icon;

    public SrtEntry(IToolboxPanel panel, string icon)
    {
        _panel = panel;
        _icon  = icon;
    }

    /// <inheritdoc/>
    public string Id => _panel.Name.ToLowerInvariant().Replace(" ", "-");

    /// <inheritdoc/>
    public string Icon => _icon;

    /// <inheritdoc/>
    public string Label => _panel.Name;

    /// <inheritdoc/>
    public StaffRole Role => _panel.Role;

    /// <inheritdoc/>
    public Node BuildPanel() => _panel.BuildNode();

    /// <inheritdoc/>
    public void DrawOverlays() => _panel.DrawOverlays();

    /// <inheritdoc/>
    public Node? BuildSettingsPanel() => _panel.BuildSettingsNode();

    /// <inheritdoc/>
    public void DrawSettingsOverlays() => _panel.DrawSettingsOverlays();
}
