namespace CandyCoat.Windows.Tabs;

public interface ITab
{
    string Name { get; }
    void Draw();
    void DrawContent();
}
