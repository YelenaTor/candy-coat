using CandyCoat.Data;
using Una.Drawing;

namespace CandyCoat.Windows.SRT;

public interface IToolboxPanel
{
    string Name { get; }
    StaffRole Role { get; }
    void DrawContent();   // Feature panel (live/operational UI)
    void DrawSettings();  // Settings panel (config/macros/thresholds)

    Node BuildNode();
    Node BuildSettingsNode();
    void DrawOverlays() { }
    void DrawSettingsOverlays() { }
    void Dispose() { }
}
