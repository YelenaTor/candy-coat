using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using CandyCoat.Helpers;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep1_CharacterProfile
{
    private static readonly Vector4 DimGrey = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 Pink    = new(1f, 0.6f, 0.8f, 1f);
    private static readonly Vector4 Amber   = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Vector4 Red     = new(1f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Green   = new(0.2f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 PanelBg = new(0.12f, 0.08f, 0.16f, 1f);

    private bool    _hasAutoScanned       = false;
    private bool    _scanDetected         = false;
    private string? _validationError      = null;
    private string  _existingIdBuffer     = string.Empty;

    public void DrawContent(ref int step, WizardState state)
    {
        // ── Auto-scan on first draw ──
        if (!_hasAutoScanned)
        {
            TryScan(state);
            _hasAutoScanned = true;
        }

        ImGui.TextColored(DimGrey, "Step 1 of 4 — Character Profile");
        ImGui.Spacing();

        // ── Scan status + Retry ──
        if (_scanDetected)
            ImGui.TextColored(Green, "✔ Character detected");
        else
            ImGui.TextColored(Amber, "⚠ Character not detected");

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

        // ── Two-panel layout: New User | or | Existing User ──
        var avail  = ImGui.GetContentRegionAvail().X;
        const float DividerW = 30f;
        const float PanelH   = 150f;
        var panelW = (avail - DividerW) / 2f;

        // Left: New User
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelBg);
        using (ImRaii.Child("##newUserPanel", new Vector2(panelW, PanelH), true))
        {
            ImGui.PopStyleColor();
            DrawNewUserPanel(state);
        }

        // "or" divider
        ImGui.SameLine(0, 0);
        using (ImRaii.Child("##orDivider", new Vector2(DividerW, PanelH), false))
        {
            var ty = (PanelH - ImGui.GetTextLineHeight()) / 2f;
            var tx = (DividerW - ImGui.CalcTextSize("or").X) / 2f;
            ImGui.SetCursorPosY(ty);
            ImGui.SetCursorPosX(tx);
            ImGui.TextDisabled("or");
        }

        // Right: Existing User
        ImGui.SameLine(0, 0);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelBg);
        using (ImRaii.Child("##existUserPanel", new Vector2(panelW, PanelH), true))
        {
            ImGui.PopStyleColor();
            DrawExistingUserPanel(ref step, state);
        }

        // ── Validation error ──
        if (_validationError != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(Red, _validationError);
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

    // ─── Left panel: New User ───

    private void DrawNewUserPanel(WizardState state)
    {
        ImGui.TextColored(DimGrey, "New User");
        ImGui.Spacing();

        // Editable fields (auto-filled from scan, editable as fallback)
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

    // ─── Right panel: Existing User ───

    private void DrawExistingUserPanel(ref int step, WizardState state)
    {
        ImGui.TextColored(DimGrey, "Existing User");
        ImGui.Spacing();
        ImGui.TextWrapped("Already have a Profile ID? Enter it below.");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##existId", "silver-moon-4821", ref _existingIdBuffer, 50);

        ImGui.Spacing();

        // Require both a Profile ID and a detected character (so we have name/world)
        bool canContinue = !string.IsNullOrWhiteSpace(_existingIdBuffer) && _scanDetected;

        if (!canContinue) ImGui.BeginDisabled();
        if (ImGui.Button("Continue##useExistId", new Vector2(-1, 0)))
        {
            state.ProfileId   = _existingIdBuffer.Trim();
            state.IdGenerated = true;
            step = 2;
        }
        if (!canContinue) ImGui.EndDisabled();

        if (!string.IsNullOrWhiteSpace(_existingIdBuffer) && !_scanDetected)
        {
            ImGui.Spacing();
            ImGui.TextColored(Amber, "Retry Scan needed\nto detect character.");
        }
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

        var expectedName  = $"{state.FirstName.Trim()} {state.LastName.Trim()}";
        var actualName    = player.Name.ToString();
        var actualWorld   = player.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;

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
            step = 2;
    }
}
