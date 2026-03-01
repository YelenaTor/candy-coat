using System.Numerics;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep3_RoleSelection
{
    private const string ProtectedRolePassword = "pixie13!?";

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

    public void DrawContent(ref int step, WizardState state)
    {
        var dimGrey = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var amber   = new Vector4(1f, 0.8f, 0.2f, 1f);

        ImGui.TextColored(dimGrey, "Step 3 of 4 — Role Selection");
        ImGui.Spacing();

        // Warning banner
        ImGui.PushStyleColor(ImGuiCol.Text, amber);
        ImGui.TextWrapped("⚠ Role setup will change in a future update.");
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.TextWrapped("Select your primary role at the venue. This determines which toolbox panels you'll see in the Sugar Role Toolbox (SRT).");
        ImGui.Spacing();

        ImGui.Text("Primary Role:");
        ImGui.Spacing();

        foreach (var (role, icon, desc) in Roles)
        {
            bool isSelected  = state.SelectedPrimaryRole == role;
            bool isProtected = role == StaffRole.Owner || role == StaffRole.Management;

            if (isProtected && !state.RolePasswordUnlocked)
            {
                ImGui.BeginDisabled();
                ImGui.Selectable($"  {icon}  {role} — {desc} 🔒##role{role}", false,
                    ImGuiSelectableFlags.None, new Vector2(0, 24));
                ImGui.EndDisabled();
            }
            else
            {
                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.4f, 0.2f, 0.5f, 1f));

                if (ImGui.Selectable($"  {icon}  {role} — {desc}##role{role}", isSelected,
                    ImGuiSelectableFlags.None, new Vector2(0, 24)))
                {
                    state.SelectedPrimaryRole    = role;
                    state.SelectedSecondaryRoles |= role;
                }

                if (isSelected)
                    ImGui.PopStyleColor();
            }
        }

        // Password unlock
        if (!state.RolePasswordUnlocked)
        {
            ImGui.Spacing();
            ImGui.TextColored(amber, "🔒 Owner & Management require a passcode.");
            ImGui.SetNextItemWidth(160);
            var pw = state.RolePasswordBuffer;
            if (ImGui.InputTextWithHint("##wizRolePw", "Enter Passcode", ref pw, 30,
                ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (pw == ProtectedRolePassword)
                    state.RolePasswordUnlocked = true;
                pw = string.Empty;
            }
            state.RolePasswordBuffer = pw;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

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
                bool enabled     = state.SelectedSecondaryRoles.HasFlag(role);
                bool isProtected = role == StaffRole.Owner || role == StaffRole.Management;

                if (isProtected && !state.RolePasswordUnlocked)
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
