using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using ECommons.DalamudServices;
using SamplePlugin.IPC;

namespace SamplePlugin.Windows;

public class SetupWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly GlamourerIpc _glamourer;
    private readonly ChatTwoIpc _chatTwo;

    private int _currentStep = 0;
    
    // Step 1: Identity
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _homeWorld = string.Empty;

    // Step 2: Dependencies
    private bool _glamourerDetected = false;
    private bool _chatTwoDetected = false; // ChatTwo detection is hard, maybe manual check?

    public SetupWindow(Plugin plugin, GlamourerIpc glamourer, ChatTwoIpc chatTwo) : base("Candy Coat Setup##CandyCoatSetup")
    {
        _plugin = plugin;
        _glamourer = glamourer;
        _chatTwo = chatTwo;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(600, 450)
        };

        Flags |= ImGuiWindowFlags.NoCollapse;
        Flags |= ImGuiWindowFlags.NoResize;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Welcome to Candy Coat!");
        ImGui.Separator();
        ImGui.Spacing();

        switch (_currentStep)
        {
            case 0:
                DrawStep1_Identity();
                break;
            case 1:
                DrawStep2_Dependencies();
                break;
            case 2:
                DrawStep3_Configuration();
                break;
            case 3:
                DrawStep4_Finish();
                break;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Footer Navigation
        if (_currentStep > 0)
        {
            if (ImGui.Button("Back"))
            {
                _currentStep--;
            }
            ImGui.SameLine();
        }

        if (_currentStep < 3)
        {
            var canProceed = _currentStep switch
            {
                0 => !string.IsNullOrWhiteSpace(_firstName) && !string.IsNullOrWhiteSpace(_lastName) && !string.IsNullOrWhiteSpace(_homeWorld),
                1 => _glamourerDetected, // Glamourer is required? User said "Glamourer*needed"
                2 => true,
                _ => false
            };
            
            if (!canProceed) ImGui.BeginDisabled();
            if (ImGui.Button("Next"))
            {
                _currentStep++;
                if (_currentStep == 1) CheckDependencies();
            }
            if (!canProceed) ImGui.EndDisabled();
        }
    }

    private void DrawStep1_Identity()
    {
        ImGui.TextWrapped("Step 1: Who are you?");
        ImGui.TextWrapped("Candy Coat needs your character name and world to properly track sessions and bookings.");
        ImGui.Spacing();

        if (ImGui.Button("Detect Current Character"))
        {
            var player = Svc.ClientState.LocalPlayer;
            if (player != null)
            {
                var nameParts = player.Name.ToString().Split(' ');
                if (nameParts.Length >= 2)
                {
                    _firstName = nameParts[0];
                    _lastName = nameParts[1];
                }
                _homeWorld = player.HomeWorld.GameData?.Name.ToString() ?? "";
            }
        }
        
        ImGui.Spacing();
        ImGui.InputText("First Name", ref _firstName, 50);
        ImGui.InputText("Last Name", ref _lastName, 50);
        ImGui.InputText("World", ref _homeWorld, 50);
    }

    private void CheckDependencies()
    {
        _glamourerDetected = _glamourer.IsAvailable();
        // ChatTwo detection logic or assume optional
        // Since ChatTwoIpc.IsAvailable returns false for now, let's just not block on it.
        // Or we can try to rely on user check.
        _chatTwoDetected = false; 
    }

    private void DrawStep2_Dependencies()
    {
        ImGui.TextWrapped("Step 2: Required Plugins");
        ImGui.Spacing();

        ImGui.Text("Glamourer (Required):");
        ImGui.SameLine();
        if (_glamourerDetected)
            ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), "Detected via IPC");
        else
            ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "Not Detected! (Please install/enable Glamourer)");

        ImGui.Spacing();
        ImGui.Text("ChatTwo (Optional):");
        ImGui.SameLine();
        ImGui.TextDisabled("Detection unavailable (Optional)");

        ImGui.Spacing();
        if (ImGui.Button("Re-check Dependencies"))
        {
            CheckDependencies();
        }
    }

    private void DrawStep3_Configuration()
    {
        ImGui.TextWrapped("Step 3: Initial Configuration");
        ImGui.Spacing();

        ImGui.TextWrapped("You can configure detailed settings later in the main menu.");
        
        var enableGlam = _plugin.Configuration.EnableGlamourer;
        if (ImGui.Checkbox("Enable Glamourer Integration", ref enableGlam))
        {
            _plugin.Configuration.EnableGlamourer = enableGlam;
        }

        var enableChat = _plugin.Configuration.EnableChatTwo;
        if (ImGui.Checkbox("Enable ChatTwo Integration", ref enableChat))
        {
            _plugin.Configuration.EnableChatTwo = enableChat;
        }
    }

    private void DrawStep4_Finish()
    {
        ImGui.TextWrapped("You're all set!");
        ImGui.Spacing();
        ImGui.Text($"Character: {_firstName} {_lastName} @ {_homeWorld}");
        ImGui.Spacing();

        if (ImGui.Button("Finish & Launch Candy Coat"))
        {
            // Save Config
            _plugin.Configuration.CharacterName = $"{_firstName} {_lastName}";
            _plugin.Configuration.HomeWorld = _homeWorld;
            _plugin.Configuration.IsSetupComplete = true;
            _plugin.Configuration.Save();

            // Close Setup, Open Main
            this.IsOpen = false;
            _plugin.OnSetupComplete();
        }
    }
}
