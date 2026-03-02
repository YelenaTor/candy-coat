using CandyCoat.Data;

namespace CandyCoat.Windows.SRT;

public interface IToolboxPanel
{
    string Name { get; }
    StaffRole Role { get; }
    void DrawContent();   // Feature panel (live/operational UI)
    void DrawSettings();  // Settings panel (config/macros/thresholds)
}
