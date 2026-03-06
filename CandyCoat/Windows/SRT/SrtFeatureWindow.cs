using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace CandyCoat.Windows.SRT;

public class SrtFeatureWindow : Window
{
    private IToolboxPanel? _panel;
    private readonly Configuration _cfg;

    public SrtFeatureWindow(Configuration cfg)
        : base("##CCSrtFeature", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _cfg = cfg;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void SetPanel(IToolboxPanel? panel)
    {
        _panel = panel;
    }

    public override void Draw()
    {
        if (_panel == null)
        {
            ImGui.TextDisabled("No role selected.");
            return;
        }

        var icon = MainWindow.PanelIcon(_panel.Role);

        // Title line with flush-right Attach button
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), $"{icon}  {_panel.Name} \u2014 Features");
        ImGui.SameLine();
        var attachLabel = "\ud83d\udccc Attach";
        var attachW = ImGui.CalcTextSize(attachLabel).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X - attachW - 4f);
        if (ImGui.SmallButton(attachLabel))
        {
            _cfg.SrtFeatureAttached = true;
            _cfg.Save();
            IsOpen = false;
        }

        ImGui.Separator();
        ImGui.Spacing();

        _panel.DrawContent();
    }
}
