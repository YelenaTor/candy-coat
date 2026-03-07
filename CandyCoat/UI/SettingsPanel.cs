using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using Una.Drawing;

namespace CandyCoat.UI;

/// <summary>
/// Encapsulates the Settings content panel for the Una.Drawing phase 6 refactor.
/// BuildNode() returns a placeholder node that fills the content area.
/// DrawOverlays() renders the full settings UI as raw ImGui on top.
/// </summary>
public class SettingsPanel
{
    private readonly Plugin _plugin;

    // Manager password UI state
    private string _mgrPwBuffer    = string.Empty;
    private bool   _mgrPwSetResult = false;

    private const string ProtectedRolePassword = "pixie13!?";

    public SettingsPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    /// <summary>
    /// Returns a Una.Drawing placeholder node sized to fill the content area.
    /// The actual settings UI is drawn via DrawOverlays().
    /// </summary>
    public Node BuildNode()
    {
        return UdtHelper.CreateFromTemplate("settings-panel.xml", "settings-layout");
    }

    /// <summary>
    /// Draws the full settings UI as raw ImGui, positioned over the content area.
    /// Must be called after _rootNode.Render() so it appears on top of the Una.Drawing layer.
    /// </summary>
    public void DrawOverlays() => DrawOverlays(ImGui.GetContentRegionAvail());

    public void DrawOverlays(Vector2 region)
    {
        // The balloon ghost window is already positioned at the balloon origin —
        // no sidebar offset needed here.
        var contentPos  = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        var contentSize = new Vector2(region.X, region.Y);

        ImGui.SetNextWindowPos(contentPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(contentSize, ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.10f, 0.08f, 0.13f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,  new Vector4(0.10f, 0.08f, 0.13f, 1f));
        ImGui.SetNextWindowBgAlpha(1f);

        bool open = true;
        ImGui.Begin("##SettingsOverlay", ref open,
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoBringToFrontOnFocus);

        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Settings");
        ImGui.Separator();
        ImGui.Spacing();
        DrawSettings();

        ImGui.End();
        ImGui.PopStyleColor(2);
    }

    // ─── Settings sections ───────────────────────────────────────────────────

    private void DrawSettings()
    {
        var config = _plugin.Configuration;

        // ── Role Management ──
        if (ImGui.CollapsingHeader("Role Management", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            ImGui.Text("Primary Role:");

            var allRoles   = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Where(Enum.GetValues<StaffRole>(), r => r != StaffRole.None));
            var roleLabels = System.Array.ConvertAll(allRoles, r => r.ToString());

            int primaryIdx = System.Array.IndexOf(allRoles, config.PrimaryRole);
            if (primaryIdx < 0) primaryIdx = 0;

            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("##primaryRoleSettings", ref primaryIdx, roleLabels, roleLabels.Length))
            {
                var chosen       = allRoles[primaryIdx];
                bool mgmtLocked  = chosen == StaffRole.Management && string.IsNullOrEmpty(config.ManagerPassword);
                bool ownerLocked = chosen == StaffRole.Owner && !config.IsManagementModeEnabled;

                if (!mgmtLocked && !ownerLocked)
                {
                    config.PrimaryRole   = chosen;
                    config.EnabledRoles |= chosen;
                    config.Save();
                }
            }

            if (string.IsNullOrEmpty(config.ManagerPassword))
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f),
                    "\u26a0 Management role requires a Manager Password (set by Owner).");
            }

            ImGui.Spacing();
            var multiRole = config.MultiRoleEnabled;
            if (ImGui.Checkbox("Enable Multiple Roles", ref multiRole))
            {
                config.MultiRoleEnabled = multiRole;
                if (!multiRole) config.EnabledRoles = config.PrimaryRole;
                config.Save();
            }

            if (config.MultiRoleEnabled)
            {
                ImGui.Indent();
                foreach (StaffRole role in Enum.GetValues<StaffRole>())
                {
                    if (role == StaffRole.None || role == config.PrimaryRole) continue;
                    bool enabled     = config.EnabledRoles.HasFlag(role);
                    bool mgmtLocked  = role == StaffRole.Management && string.IsNullOrEmpty(config.ManagerPassword);
                    bool ownerLocked = role == StaffRole.Owner && !config.IsManagementModeEnabled;

                    if (mgmtLocked || ownerLocked)
                    {
                        ImGui.BeginDisabled();
                        ImGui.Checkbox($"{role} \ud83d\udd12##secondary", ref enabled);
                        ImGui.EndDisabled();
                    }
                    else
                    {
                        if (ImGui.Checkbox($"{role}##secondary", ref enabled))
                        {
                            if (enabled) config.EnabledRoles |=  role;
                            else         config.EnabledRoles &= ~role;
                            config.Save();
                        }
                    }
                }
                ImGui.Unindent();
            }

            // ── Set Manager Password (Owner only) ──
            if (config.IsManagementModeEnabled)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "\ud83d\udd11 Set Manager Password");
                ImGui.TextDisabled("Controls who can be assigned the Management role.");
                ImGui.Spacing();
                ImGui.SetNextItemWidth(200);
                ImGui.InputTextWithHint("##mgrPwInput", "New password...", ref _mgrPwBuffer, 50,
                    ImGuiInputTextFlags.Password);
                ImGui.SameLine();
                if (ImGui.Button("Save##saveMgrPw"))
                {
                    config.ManagerPassword = _mgrPwBuffer.Trim();
                    config.Save();
                    _plugin.SyncService.UpsertVenueConfigAsync(config.ManagerPassword);
                    _mgrPwBuffer    = string.Empty;
                    _mgrPwSetResult = true;
                }
                if (_mgrPwSetResult)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.4f, 1f), "\u2714 Saved");
                }
            }

            ImGui.Spacing();
        }

        // ── Integrations ──
        if (ImGui.CollapsingHeader("Integrations", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Spacing();
            var enableGlam = config.EnableGlamourer;
            if (ImGui.Checkbox("Enable Glamourer Integration", ref enableGlam))
            { config.EnableGlamourer = enableGlam; config.Save(); }

            var enableChat = config.EnableChatTwo;
            if (ImGui.Checkbox("Enable ChatTwo Integration", ref enableChat))
            { config.EnableChatTwo = enableChat; config.Save(); }

            ImGui.Spacing();
        }

        // ── Custom Macros ──
        if (ImGui.CollapsingHeader("Custom Macros"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Create Quick-Tells that appear on patron profiles. Use {name} to insert their first name.");
            if (ImGui.Button("Add New Macro"))
            {
                config.Macros.Add(new MacroTemplate { Title = "New Macro", Text = "Hello {name}!" });
                config.Save();
            }
            ImGui.Spacing();
            for (int i = 0; i < config.Macros.Count; i++)
            {
                var m     = config.Macros[i];
                var title = m.Title;
                var text  = m.Text;
                ImGui.PushID($"Macro{i}");
                if (ImGui.InputText("Title", ref title, 50))          { m.Title = title; config.Save(); }
                if (ImGui.InputTextMultiline("Text", ref text, 500,
                    new Vector2(-1, 60)))                              { m.Text  = text;  config.Save(); }
                if (ImGui.Button("Delete"))
                {
                    config.Macros.RemoveAt(i);
                    config.Save();
                    ImGui.PopID();
                    break;
                }
                ImGui.Separator();
                ImGui.PopID();
            }
            ImGui.Spacing();
        }

        // ── Management Access ──
        if (ImGui.CollapsingHeader("Management Access"))
        {
            ImGui.Spacing();
            if (config.IsManagementModeEnabled)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.65f, 1.0f), "\u2714\ufe0f Management Mode Active");
            }
            else
            {
                var code = "";
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputTextWithHint("##mgmtcode", "Enter Passcode", ref code, 20,
                    ImGuiInputTextFlags.Password))
                {
                    if (code == ProtectedRolePassword)
                    { config.IsManagementModeEnabled = true; config.Save(); }
                }
                ImGui.SameLine();
                ImGui.TextDisabled("(Locked)");
            }
            ImGui.Spacing();
        }

        // ── Patron Alerts ──
        if (ImGui.CollapsingHeader("Patron Alerts"))
        {
            ImGui.Spacing();
            var enableAlerts = config.EnablePatronAlerts;
            if (ImGui.Checkbox("Enable Patron Entry Alerts", ref enableAlerts))
            { config.EnablePatronAlerts = enableAlerts; config.Save(); }
            ImGui.TextDisabled("Shows an overlay when a tracked patron enters the instance.");
            ImGui.Spacing();

            if (config.EnablePatronAlerts)
            {
                ImGui.Indent();
                ImGui.Text("Alert Method:");
                ImGui.SameLine();
                var methodIdx = (int)config.AlertMethod;
                ImGui.SetNextItemWidth(120);
                if (ImGui.Combo("##alertMethod", ref methodIdx, new[] { "Panel", "Chat", "Both" }, 3))
                { config.AlertMethod = (PatronAlertMethod)methodIdx; config.Save(); }
                ImGui.TextDisabled("Panel = on-screen card \u00b7 Chat = echo message \u00b7 Both = panel + chat");
                ImGui.Spacing();

                var regularOnly = config.AlertOnRegularOnly;
                if (ImGui.Checkbox("Only alert for Regular / Elite patrons", ref regularOnly))
                { config.AlertOnRegularOnly = regularOnly; config.Save(); }
                ImGui.TextDisabled("Danger-status patrons always alert regardless.");
                ImGui.Spacing();

                var targetBtn = config.EnableTargetOnAlertClick;
                if (ImGui.Checkbox("Show 'Target' button on panel alerts", ref targetBtn))
                { config.EnableTargetOnAlertClick = targetBtn; config.Save(); }
                ImGui.Spacing();

                var cooldown = config.AlertCooldownMinutes;
                ImGui.SetNextItemWidth(80);
                if (ImGui.InputInt("Cooldown (minutes)##alertCooldown", ref cooldown, 1))
                { config.AlertCooldownMinutes = System.Math.Max(1, cooldown); config.Save(); }
                ImGui.TextDisabled("Minimum time before re-alerting for the same patron.");
                ImGui.Spacing();

                var dismissSecs = config.AlertDismissSeconds;
                ImGui.SetNextItemWidth(80);
                if (ImGui.InputInt("Auto-dismiss after (seconds)##alertDismiss", ref dismissSecs, 1))
                { config.AlertDismissSeconds = System.Math.Max(3, dismissSecs); config.Save(); }
                ImGui.Unindent();
            }
            ImGui.Spacing();
        }

        // ── Candy Tells ──
        if (ImGui.CollapsingHeader("Candy Tells"))
        {
            ImGui.Spacing();
            var suppressInGame = config.TellSuppressInGame;
            if (ImGui.Checkbox("Suppress tells from in-game chat", ref suppressInGame))
            { config.TellSuppressInGame = suppressInGame; config.Save(); }
            ImGui.TextDisabled("Removes incoming /tell messages from the main chat window.");
            ImGui.Spacing();

            var autoOpen = config.TellAutoOpen;
            if (ImGui.Checkbox("Auto-open Tells window on incoming message", ref autoOpen))
            { config.TellAutoOpen = autoOpen; config.Save(); }
            ImGui.Spacing();

            var maxMsgs = config.TellHistoryMaxMessages;
            ImGui.SetNextItemWidth(100);
            if (ImGui.SliderInt("Max messages per conversation##tellMax", ref maxMsgs, 50, 500))
            { config.TellHistoryMaxMessages = System.Math.Max(50, maxMsgs); config.Save(); }
            ImGui.Spacing();

            if (ImGui.Button("Clear all conversation history##clearTells"))
            {
                config.TellHistory.Clear();
                config.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(Cannot be undone)");
            ImGui.Spacing();
        }

        // ── Support & Feedback ──
        if (ImGui.CollapsingHeader("Support & Feedback"))
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Thank you for helping us improve Candy Coat! <3");
            ImGui.BulletText("Bugs & Crashes: Report via Discord (DM me) or GitHub Issues.");
            ImGui.BulletText("Suggestions: Use the #\ud83c\udf70-staff-bot-testing channel on Discord.");
            ImGui.Spacing();
            if (ImGui.Button("Open GitHub Issues"))
                ECommons.GenericHelpers.ShellStart("https://github.com/YelenaTor/candy-coat/issues");
            ImGui.SameLine();
            if (ImGui.Button("Copy Discord Link"))
                ImGui.SetClipboardText("https://discord.gg/your-discord-link");
            ImGui.Spacing();
        }
    }
}
