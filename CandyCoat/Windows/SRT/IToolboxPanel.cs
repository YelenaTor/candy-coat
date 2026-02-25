using CandyCoat.Data;

namespace CandyCoat.Windows.SRT;

public interface IToolboxPanel
{
    string Name { get; }
    StaffRole Role { get; }
    void DrawContent();
}
