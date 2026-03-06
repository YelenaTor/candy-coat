using Una.Drawing;

namespace CandyCoat.Windows.Tabs;

public interface ITab
{
    string Name { get; }
    void Draw();
    void DrawContent();

    Node BuildNode();
    void DrawOverlays() { }
    void Dispose() { }
}
