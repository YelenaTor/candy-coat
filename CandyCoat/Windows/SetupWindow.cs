using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using CandyCoat.IPC;
using CandyCoat.Data;

namespace CandyCoat.Windows;

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
    private bool _chatTwoDetected = false;

    // Step 3: Role Selection
    private StaffRole _selectedPrimaryRole = StaffRole.None;
    private StaffRole _selectedSecondaryRoles = StaffRole.None;
    private bool _multiRoleToggle = false;
    private string _setupRolePassword = string.Empty;
    private bool _setupRolePasswordUnlocked = false;
    private const string ProtectedRolePassword = "pixie13!?";

    public SetupWindow(Plugin plugin, GlamourerIpc glamourer, ChatTwoIpc chatTwo) : base("Candy Coat Setup##CandyCoatSetup")
    {
        _plugin = plugin;
        _glamourer = glamourer;
        _chatTwo = chatTwo;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 380),
            MaximumSize = new Vector2(650, 550)
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
                DrawStep3_RoleSelection();
                break;
            case 3:
                DrawStep4_Configuration();
                break;
            case 4:
                DrawStep5_Finish();
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

        if (_currentStep < 4)
        {
            var canProceed = _currentStep switch
            {
                0 => !string.IsNullOrWhiteSpace(_firstName) && !string.IsNullOrWhiteSpace(_lastName) && !string.IsNullOrWhiteSpace(_homeWorld),
                1 => _glamourerDetected,
                2 => _selectedPrimaryRole != StaffRole.None,
                3 => true,
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
            var player = Svc.Objects.LocalPlayer;
            if (player != null)
            {
                var nameParts = player.Name.ToString().Split(' ');
                if (nameParts.Length >= 2)
                {
                    _firstName = nameParts[0];
                    _lastName = nameParts[1];
                }
                _homeWorld = player.HomeWorld.ValueNullable?.Name.ToString() ?? "";
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
        _chatTwoDetected = _chatTwo.IsAvailable();
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
        if (_chatTwoDetected)
            ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), "Detected via IPC");
        else
            ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1f), "Not Detected (Optional)");

        ImGui.Spacing();
        if (ImGui.Button("Re-check Dependencies"))
        {
            CheckDependencies();
        }
    }

    private void DrawStep3_RoleSelection()
    {
        ImGui.TextWrapped("Step 3: Choose Your Role");
        ImGui.TextWrapped("Select your primary role at the venue. This determines which toolbox you'll see in the Sugar Role Toolbox (SRT).");
        ImGui.Spacing();

        // Role descriptions
        var roles = new (StaffRole Role, string Icon, string Desc)[]
        {
            (StaffRole.Sweetheart,  "♥",  "Entertainer / Companion"),
            (StaffRole.CandyHeart,  "💗", "Greeter / Welcome team"),
            (StaffRole.Bartender,   "🍸", "Bar / drink service"),
            (StaffRole.Gamba,       "🎲", "Gambling / games host"),
            (StaffRole.DJ,          "🎵", "Music / performance"),
            (StaffRole.Management,  "📋", "Staff oversight"),
            (StaffRole.Owner,       "👑", "Venue-wide admin"),
        };

        ImGui.Text("Primary Role:");
        ImGui.Spacing();

        foreach (var (role, icon, desc) in roles)
        {
            bool isSelected = _selectedPrimaryRole == role;
            bool isProtected = role == StaffRole.Owner || role == StaffRole.Management;

            if (isProtected && !_setupRolePasswordUnlocked)
            {
                ImGui.BeginDisabled();
                ImGui.Selectable($"  {icon}  {role} — {desc} 🔒##role{role}", false, ImGuiSelectableFlags.None, new Vector2(0, 24));
                ImGui.EndDisabled();
            }
            else
            {
                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.4f, 0.2f, 0.5f, 1f));

                if (ImGui.Selectable($"  {icon}  {role} — {desc}##role{role}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 24)))
                {
                    _selectedPrimaryRole = role;
                    _selectedSecondaryRoles |= role;
                }

                if (isSelected)
                    ImGui.PopStyleColor();
            }
        }

        // Password unlock for protected roles
        if (!_setupRolePasswordUnlocked)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "🔒 Owner & Management require a passcode.");
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputTextWithHint("##setupRolePw", "Enter Passcode", ref _setupRolePassword, 30, ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (_setupRolePassword == ProtectedRolePassword)
                    _setupRolePasswordUnlocked = true;
                _setupRolePassword = string.Empty;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Multi-role toggle
        ImGui.Checkbox("I regularly do more than one role", ref _multiRoleToggle);

        if (_multiRoleToggle && _selectedPrimaryRole != StaffRole.None)
        {
            ImGui.Indent();
            ImGui.TextDisabled("Select additional roles:");
            ImGui.Spacing();

            foreach (var (role, icon, desc) in roles)
            {
                if (role == _selectedPrimaryRole) continue;
                bool enabled = _selectedSecondaryRoles.HasFlag(role);
                bool isProtected = role == StaffRole.Owner || role == StaffRole.Management;

                if (isProtected && !_setupRolePasswordUnlocked)
                {
                    ImGui.BeginDisabled();
                    ImGui.Checkbox($"{icon} {role} 🔒##sec{role}", ref enabled);
                    ImGui.EndDisabled();
                }
                else
                {
                    if (ImGui.Checkbox($"{icon} {role}##sec{role}", ref enabled))
                    {
                        if (enabled)
                            _selectedSecondaryRoles |= role;
                        else
                            _selectedSecondaryRoles &= ~role;
                    }
                }
            }
            ImGui.Unindent();
        }
    }

    private void DrawStep4_Configuration()
    {
        ImGui.TextWrapped("Step 4: Initial Configuration");
        ImGui.Spacing();

        ImGui.TextWrapped("You can configure detailed settings later in the Settings panel.");
        
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

    private void DrawStep5_Finish()
    {
        ImGui.TextWrapped("You're all set!");
        ImGui.Spacing();
        ImGui.Text($"Character: {_firstName} {_lastName} @ {_homeWorld}");
        ImGui.Text($"Primary Role: {_selectedPrimaryRole}");
        if (_multiRoleToggle)
        {
            ImGui.Text($"Additional Roles: {_selectedSecondaryRoles & ~_selectedPrimaryRole}");
        }
        ImGui.Spacing();

        if (ImGui.Button("Finish & Launch Candy Coat"))
        {
            // Save Config
            _plugin.Configuration.CharacterName = $"{_firstName} {_lastName}";
            _plugin.Configuration.HomeWorld = _homeWorld;
            _plugin.Configuration.PrimaryRole = _selectedPrimaryRole;
            _plugin.Configuration.MultiRoleEnabled = _multiRoleToggle;
            _plugin.Configuration.EnabledRoles = _multiRoleToggle 
                ? (_selectedSecondaryRoles | _selectedPrimaryRole)
                : _selectedPrimaryRole;
            _plugin.Configuration.IsSetupComplete = true;
            _plugin.Configuration.Save();

            // Close Setup, Open Main
            this.IsOpen = false;
            _plugin.OnSetupComplete();
        }
    }
}
