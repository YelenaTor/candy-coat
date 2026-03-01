using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;
using CandyCoat.Windows.SetupWizard;

namespace CandyCoat.Windows;

public class SetupWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    private int _currentStep = 0;
    private readonly WizardState _state = new();

    private readonly SetupStep0_Welcome          _step0 = new();
    private readonly SetupStep1_CharacterProfile _step1 = new();
    private readonly SetupStep2_ModeSelection    _step2 = new();
    private readonly SetupStep3_RoleSelection    _step3 = new();
    private readonly SetupStep4_Finish           _step4 = new();

    public SetupWindow(Plugin plugin) : base("Candy Coat Setup##CandyCoatSetup")
    {
        _plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 420),
            MaximumSize = new Vector2(700, 560)
        };

        Flags |= ImGuiWindowFlags.NoCollapse;
        Flags |= ImGuiWindowFlags.NoResize;
    }

    public void Dispose() { }

    public override void Draw()
    {
        StyleManager.PushStyles();
        try
        {
            switch (_currentStep)
            {
                case 0: _step0.DrawContent(ref _currentStep, _state); break;
                case 1: _step1.DrawContent(ref _currentStep, _state); break;
                case 2: _step2.DrawContent(ref _currentStep, _state); break;
                case 3: DrawStep3WithNav(); break;
                case 4: DrawStep4WithNav(); break;
            }
        }
        finally
        {
            StyleManager.PopStyles();
        }
    }

    // Step 3 and 4 have a Back button drawn by the outer shell
    private void DrawStep3WithNav()
    {
        _step3.DrawContent(ref _currentStep, _state);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Back##step3back"))
            _currentStep = 2;

        bool canProceed = _state.SelectedPrimaryRole != CandyCoat.Data.StaffRole.None;
        ImGui.SameLine();
        if (!canProceed) ImGui.BeginDisabled();
        if (ImGui.Button("Next##step3next"))
            _currentStep = 4;
        if (!canProceed) ImGui.EndDisabled();
    }

    private void DrawStep4WithNav()
    {
        _step4.DrawContent(ref _currentStep, _state, _plugin, this);

        ImGui.Spacing();
        if (ImGui.Button("Back##step4back"))
            _currentStep = 3;
    }
}
