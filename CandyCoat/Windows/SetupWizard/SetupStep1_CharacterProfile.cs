using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using CandyCoat.Helpers;
using CandyCoat.Services;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep1_CharacterProfile
{
    private static readonly Vector4 DimGrey = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 Pink    = new(1f, 0.6f, 0.8f, 1f);
    private static readonly Vector4 Amber   = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Vector4 Red     = new(1f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Green   = new(0.2f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 PanelBg = new(0.12f, 0.08f, 0.16f, 1f);

    private bool    _hasAutoScanned  = false;
    private bool    _scanDetected    = false;
    private string? _validationError = null;

    // Existing profile lookup state machine
    private string   _existingIdBuffer = string.Empty;
    private enum LookupState { Idle, InProgress, NotFound, Error }
    private LookupState _lookupState = LookupState.Idle;
    private Task<GlobalProfileLookupResult?>? _lookupTask;
    private string _lookupError = string.Empty;

    public void DrawContent(ref int step, WizardState state, Plugin plugin, SetupWindow window)
    {
        // ── Poll async lookup task ──
        if (_lookupState == LookupState.InProgress && _lookupTask?.IsCompleted == true)
        {
            if (_lookupTask.IsFaulted)
            {
                _lookupState = LookupState.Error;
                _lookupError = _lookupTask.Exception?.InnerException?.Message ?? "Connection failed";
            }
            else
            {
                var result = _lookupTask.Result;
                if (result != null)
                {
                    ApplyLookupResult(result, plugin, window);
                    return; // window closing — skip further draw
                }
                _lookupState = LookupState.NotFound;
            }
            _lookupTask = null;
        }

        // ── Auto-scan on first draw ──
        if (!_hasAutoScanned)
        {
            TryScan(state);
            _hasAutoScanned = true;
        }

        ImGui.TextColored(DimGrey, "Step 2 of 5 — Character Profile");
        ImGui.Spacing();

        // ── Scan status + Retry ──
        if (_scanDetected)
            ImGui.TextColored(Green, "\u2714 Character detected");
        else
            ImGui.TextColored(Amber, "\u26a0 Character not detected");

        ImGui.SameLine();
        if (ImGui.SmallButton("Retry Scan"))
        {
            _validationError = null;
            TryScan(state);
        }

        ImGui.Spacing();

        // ── If ID is already confirmed, show the confirmation panel ──
        if (state.IdGenerated)
        {
            DrawIdConfirmed(ref step, state);
            return;
        }

        // ── New User section ──
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelBg);
        using (ImRaii.Child("##newUserPanel", new Vector2(-1, 165), true))
        {
            ImGui.PopStyleColor();
            DrawNewUserPanel(state);
        }

        // ── Validation error ──
        if (_validationError != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(Red, _validationError);
        }

        ImGui.Spacing();

        // ── "or" separator ──
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("Already have a Profile ID?");
        ImGui.Spacing();

        // ── Existing Profile ID section ──
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelBg);
        using (ImRaii.Child("##existUserPanel", new Vector2(-1, 80), true))
        {
            ImGui.PopStyleColor();
            DrawExistingUserPanel(plugin, window, state);
        }
    }

    // ─── Scan ───

    private void TryScan(WizardState state)
    {
        var player = Svc.Objects.LocalPlayer;
        if (player == null)
        {
            _scanDetected = false;
            return;
        }

        var parts = player.Name.ToString().Split(' ', 2);
        state.FirstName = parts.Length >= 1 ? parts[0] : string.Empty;
        state.LastName  = parts.Length >= 2 ? parts[1] : string.Empty;
        state.HomeWorld = player.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;

        _scanDetected = !string.IsNullOrEmpty(state.FirstName)
                     && !string.IsNullOrEmpty(state.LastName)
                     && !string.IsNullOrEmpty(state.HomeWorld);
    }

    // ─── New User panel ───

    private void DrawNewUserPanel(WizardState state)
    {
        ImGui.TextColored(DimGrey, "New User");
        ImGui.Spacing();

        var fn = state.FirstName;
        ImGui.TextDisabled("First");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##fn", ref fn, 30)) state.FirstName = fn;

        var ln = state.LastName;
        ImGui.TextDisabled("Last");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##ln", ref ln, 30)) state.LastName = ln;

        var hw = state.HomeWorld;
        ImGui.TextDisabled("World");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##hw", ref hw, 30)) state.HomeWorld = hw;

        ImGui.Spacing();

        bool allFilled = !string.IsNullOrWhiteSpace(state.FirstName)
                      && !string.IsNullOrWhiteSpace(state.LastName)
                      && !string.IsNullOrWhiteSpace(state.HomeWorld);

        if (!allFilled) ImGui.BeginDisabled();
        if (ImGui.Button("Confirm Details##newConfirm", new Vector2(-1, 0)))
            ValidateAndConfirm(state);
        if (!allFilled) ImGui.EndDisabled();
    }

    // ─── Existing User panel ───

    private void DrawExistingUserPanel(Plugin plugin, SetupWindow window, WizardState state)
    {
        ImGui.SetNextItemWidth(-70);
        ImGui.InputTextWithHint("##existId", "silver-moon-4821", ref _existingIdBuffer, 50);

        ImGui.SameLine();

        bool lookupReady = !string.IsNullOrWhiteSpace(_existingIdBuffer)
                        && _lookupState != LookupState.InProgress;

        if (!lookupReady) ImGui.BeginDisabled();
        if (ImGui.Button("Use This Profile##useExist"))
            StartLookup(plugin);
        if (!lookupReady) ImGui.EndDisabled();

        ImGui.Spacing();

        switch (_lookupState)
        {
            case LookupState.InProgress:
                ImGui.TextColored(Amber, "Looking up profile...");
                break;
            case LookupState.NotFound:
                ImGui.TextColored(Red, "Profile not found. Check the ID and try again.");
                break;
            case LookupState.Error:
                ImGui.TextColored(Red, $"Error: {_lookupError}");
                break;
        }
    }

    // ─── Start lookup task ───

    private void StartLookup(Plugin plugin)
    {
        _lookupState = LookupState.InProgress;
        _lookupError = string.Empty;
        _lookupTask  = SyncService.LookupProfileAsync(PluginConstants.ProductionApiUrl, _existingIdBuffer.Trim());
    }

    // ─── Apply lookup result and skip to dashboard ───

    private static void ApplyLookupResult(GlobalProfileLookupResult result, Plugin plugin, SetupWindow window)
    {
        var cfg = plugin.Configuration;
        cfg.ProfileId       = result.ProfileId;
        cfg.CharacterName   = result.CharacterName;
        cfg.HomeWorld       = result.HomeWorld;
        cfg.UserMode        = result.Mode;
        cfg.EnableGlamourer = result.HasGlamourerIntegrated;
        cfg.EnableChatTwo   = result.HasChatTwoIntegrated;
        cfg.IsSetupComplete = true;
        cfg.Save();

        window.IsOpen = false;
        plugin.OnSetupComplete();
    }

    // ─── Validation ───

    private void ValidateAndConfirm(WizardState state)
    {
        _validationError = null;

        var player = Svc.Objects.LocalPlayer;
        if (player == null)
        {
            _validationError = "No character detected. Please log in and retry.";
            return;
        }

        var expectedName = $"{state.FirstName.Trim()} {state.LastName.Trim()}";
        var actualName   = player.Name.ToString();
        var actualWorld  = player.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;

        if (!string.Equals(expectedName, actualName, StringComparison.OrdinalIgnoreCase)
         || !string.Equals(state.HomeWorld.Trim(), actualWorld, StringComparison.OrdinalIgnoreCase))
        {
            _validationError = "Details don't match your current character. Use Retry Scan.";
            return;
        }

        state.ProfileId   = ProfileIdHelper.Generate();
        state.IdGenerated = true;
    }

    // ─── ID Confirmed panel ───

    private void DrawIdConfirmed(ref int step, WizardState state)
    {
        ImGui.Spacing();
        ImGui.TextColored(DimGrey, "Your Profile ID:");
        ImGui.TextColored(Pink, state.ProfileId);

        ImGui.Spacing();

        if (ImGui.SmallButton("Copy##idCopy"))
            ImGui.SetClipboardText(state.ProfileId);

        ImGui.SameLine();

        if (ImGui.SmallButton("Save as .txt##idSave"))
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CandyCoat_ProfileId.txt");
                File.WriteAllText(path, state.ProfileId);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[CandyCoat] Save failed: {ex.Message}");
            }
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Amber);
        ImGui.TextWrapped("Keep this ID safe. You cannot use Candy Coat without it.");
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Spacing();

        if (ImGui.Button("Continue##step1continue", new Vector2(120, 0)))
            step = 3;
    }
}
