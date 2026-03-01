using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep3_RoleSelection
{
    private const string OwnerPassword = "pixie13!?";

    private static readonly (StaffRole Role, string Icon, string Desc)[] Roles =
    [
        (StaffRole.Sweetheart,  "♥",  "Entertainer / Companion"),
        (StaffRole.CandyHeart,  "💗", "Greeter / Welcome team"),
        (StaffRole.Bartender,   "🍸", "Bar / drink service"),
        (StaffRole.Gamba,       "🎲", "Gambling / games host"),
        (StaffRole.DJ,          "🎵", "Music / performance"),
        (StaffRole.Management,  "📋", "Staff oversight"),
        (StaffRole.Owner,       "👑", "Venue-wide admin"),
    ];

    private static readonly string[] ComboLabels = Array.ConvertAll(Roles,
        r => $"{r.Icon}  {r.Role} — {r.Desc}");

    // Pending protected-role unlock
    private int       _comboIndex      = 0;
    private bool      _comboInitDone   = false;
    private StaffRole _pendingRole     = StaffRole.None;
    private string    _pendingPwBuffer = string.Empty;
    private bool      _pendingPwError  = false;
    private bool      _mgmtNoPassword  = false;

    public void DrawContent(ref int step, WizardState state, Plugin plugin)
    {
        var dimGrey = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var amber   = new Vector4(1f, 0.8f, 0.2f, 1f);
        var red     = new Vector4(1f, 0.3f, 0.3f, 1f);

        ImGui.TextColored(dimGrey, "Step 4 of 5 — Role Selection");
        ImGui.Spacing();
        ImGui.TextWrapped("Select your primary role at the venue. This determines which toolbox panels you'll see in the Sugar Role Toolbox (SRT).");
        ImGui.Spacing();

        // ── Sync combo index from state (only when no pending unlock in progress) ──
        if (!_comboInitDone || _pendingRole == StaffRole.None)
        {
            _comboInitDone = true;
            _comboIndex = 0;
            for (int i = 0; i < Roles.Length; i++)
            {
                if (Roles[i].Role == state.SelectedPrimaryRole)
                { _comboIndex = i; break; }
            }
        }

        ImGui.Text("Primary Role:");
        ImGui.SetNextItemWidth(320);

        int prevIndex = _comboIndex;
        if (ImGui.Combo("##primaryRole", ref _comboIndex, ComboLabels, ComboLabels.Length))
        {
            var chosen = Roles[_comboIndex].Role;
            _mgmtNoPassword = false;
            _pendingRole    = StaffRole.None;
            _pendingPwError = false;

            if (chosen == StaffRole.Management)
            {
                if (string.IsNullOrEmpty(plugin.Configuration.ManagerPassword))
                {
                    _mgmtNoPassword = true;
                    _comboIndex     = prevIndex; // revert — no password set yet
                }
                else
                {
                    _pendingRole     = StaffRole.Management;
                    _pendingPwBuffer = string.Empty;
                }
            }
            else if (chosen == StaffRole.Owner)
            {
                _pendingRole     = StaffRole.Owner;
                _pendingPwBuffer = string.Empty;
            }
            else
            {
                state.SelectedPrimaryRole    = chosen;
                state.SelectedSecondaryRoles |= chosen;
            }
        }

        // ── Management locked message ──
        if (_mgmtNoPassword)
        {
            ImGui.Spacing();
            ImGui.TextColored(amber, "\u26a0 Management requires a Manager Password to be set by an Owner first.");
        }

        // ── Pending password prompt ──
        if (_pendingRole != StaffRole.None)
        {
            ImGui.Spacing();
            ImGui.TextColored(amber, $"\uD83D\uDD12 Enter password to unlock {_pendingRole}:");
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputText("##pendingPw", ref _pendingPwBuffer, 30,
                ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                var expected = _pendingRole == StaffRole.Owner
                    ? OwnerPassword
                    : plugin.Configuration.ManagerPassword;

                if (_pendingPwBuffer == expected)
                {
                    state.SelectedPrimaryRole    = _pendingRole;
                    state.SelectedSecondaryRoles |= _pendingRole;
                    _pendingPwError  = false;
                    _pendingRole     = StaffRole.None;
                }
                else
                {
                    _pendingPwError = true;
                }
                _pendingPwBuffer = string.Empty;
            }

            if (_pendingPwError)
                ImGui.TextColored(red, "Incorrect password.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Multi-role ──
        var multiRole = state.MultiRoleToggle;
        if (ImGui.Checkbox("I regularly do more than one role", ref multiRole))
            state.MultiRoleToggle = multiRole;

        if (state.MultiRoleToggle && state.SelectedPrimaryRole != StaffRole.None)
        {
            ImGui.Indent();
            ImGui.TextDisabled("Select additional roles:");
            ImGui.Spacing();

            foreach (var (role, icon, desc) in Roles)
            {
                if (role == state.SelectedPrimaryRole) continue;

                bool enabled = state.SelectedSecondaryRoles.HasFlag(role);
                bool mgmtLocked = role == StaffRole.Management
                               && string.IsNullOrEmpty(plugin.Configuration.ManagerPassword);
                bool ownerLocked = role == StaffRole.Owner;

                if (mgmtLocked || ownerLocked)
                {
                    ImGui.BeginDisabled();
                    ImGui.Checkbox($"{icon} {role} \uD83D\uDD12##sec{role}", ref enabled);
                    ImGui.EndDisabled();
                }
                else
                {
                    if (ImGui.Checkbox($"{icon} {role}##sec{role}", ref enabled))
                    {
                        if (enabled)
                            state.SelectedSecondaryRoles |= role;
                        else
                            state.SelectedSecondaryRoles &= ~role;
                    }
                }
            }
            ImGui.Unindent();
        }
    }
}
