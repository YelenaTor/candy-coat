using Una.Drawing;
using CandyCoat.Data;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// A single entry in the screen-anchored toolbar.
/// Produces a Una.Drawing node tree for the balloon panel content.
/// </summary>
public interface IToolbarEntry
{
    /// <summary>Unique string identifier (used for LastActiveEntryId config).</summary>
    string Id { get; }

    /// <summary>FontAwesome icon character string.</summary>
    string Icon { get; }

    /// <summary>Display label shown on the toolbar button and balloon tab strip.</summary>
    string Label { get; }

    /// <summary>Role gate. StaffRole.None = always visible regardless of EnabledRoles.</summary>
    StaffRole Role { get; }

    /// <summary>
    /// Returns the Una.Drawing node tree for this entry's balloon panel content.
    /// Cache the node tree internally — only rebuild when data changes.
    /// </summary>
    Node BuildPanel();

    /// <summary>
    /// Called inside the ghost ImGui window each frame while this entry's balloon is open.
    /// Place all ImGui input widgets here (InputText, Combo, Checkbox, etc.).
    /// </summary>
    void DrawOverlays();

    /// <summary>
    /// Optional settings content node. Return null if no settings section needed.
    /// </summary>
    Node? BuildSettingsPanel() => null;

    /// <summary>Optional settings ImGui overlays.</summary>
    void DrawSettingsOverlays() { }
}
