using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using CandyCoat.Data;
using CandyCoat.Services;
using CandyCoat.UI;

namespace CandyCoat.Windows;

/// <summary>
/// Non-modal overlay that renders stacked patron entry alert cards
/// in the top-right of the screen. Each card auto-dismisses after
/// AlertDismissSeconds and has an optional "Target" button.
/// </summary>
public class PatronAlertOverlay : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly PatronAlertService _alertService;

    private const float CardWidth = 310f;

    public PatronAlertOverlay(Plugin plugin, PatronAlertService alertService)
        : base("##CandyCoatAlerts",
            ImGuiWindowFlags.NoTitleBar       |
            ImGuiWindowFlags.NoResize         |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoScrollbar      |
            ImGuiWindowFlags.NoCollapse)
    {
        _plugin       = plugin;
        _alertService = alertService;
        IsOpen             = true;
        RespectCloseHotkey = false;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        var display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(new Vector2(display.X - CardWidth - 20f, 60f), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.90f);
    }

    public override void Draw()
    {
        if (!_plugin.Configuration.EnablePatronAlerts) return;
        if (!_plugin.Configuration.IsSetupComplete) return;

        var alerts = _alertService.ActiveAlerts
            .Where(a => !a.Dismissed)
            .ToList();

        if (alerts.Count == 0) return;

        StyleManager.PushStyles();
        try
        {
            for (int i = 0; i < alerts.Count; i++)
            {
                DrawCard(alerts[i]);
                if (i < alerts.Count - 1)
                    ImGui.Spacing();
            }
        }
        finally
        {
            StyleManager.PopStyles();
        }
    }

    private void DrawCard(PatronAlertEntry alert)
    {
        var patron   = alert.Patron;
        bool isDanger = patron.Status is PatronStatus.Warning or PatronStatus.Blacklisted;

        var cardBg = isDanger
            ? new Vector4(0.30f, 0.05f, 0.05f, 1f)
            : new Vector4(0.14f, 0.08f, 0.20f, 1f);

        bool showTarget = _plugin.Configuration.EnableTargetOnAlertClick;
        float rowH     = ImGui.GetFrameHeightWithSpacing();
        float padY     = ImGui.GetStyle().WindowPadding.Y;
        float cardH    = rowH * (showTarget ? 3f : 2f) + padY * 2f;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg);
        using var card = ImRaii.Child($"##AC{alert.Id}", new Vector2(CardWidth, cardH), true,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.PopStyleColor();

        if (!card) return;

        // ── Row 1: icon + name + tier badge + dismiss ──────────────────
        Vector4 nameCol = isDanger
            ? (patron.Status == PatronStatus.Blacklisted
                ? new Vector4(1f, 0.25f, 0.25f, 1f)
                : new Vector4(1f, 0.78f, 0.2f, 1f))
            : (alert.Tier == PatronTier.Elite
                ? new Vector4(1f, 0.85f, 0.2f, 1f)
                : new Vector4(1f, 0.65f, 0.85f, 1f));

        string icon = isDanger
            ? (patron.Status == PatronStatus.Blacklisted ? "🚫" : "⚠")
            : (alert.Tier == PatronTier.Elite ? "★" : "♥");

        string tierLabel = isDanger
            ? patron.Status.ToString().ToUpperInvariant()
            : alert.Tier.ToString();

        ImGui.TextColored(nameCol, $"{icon} {patron.Name}");
        ImGui.SameLine();
        ImGui.TextDisabled($"[{tierLabel}]");

        // Dismiss button — right-aligned
        float dismissX = ImGui.GetWindowWidth()
                       - ImGui.CalcTextSize("✕").X
                       - ImGui.GetStyle().FramePadding.X * 2f
                       - ImGui.GetStyle().WindowPadding.X;
        ImGui.SameLine(dismissX);
        if (ImGui.SmallButton($"✕##D{alert.Id}"))
            _alertService.Dismiss(alert.Id);

        // ── Row 2: distance · visits · drink ───────────────────────────
        ImGui.TextDisabled($"{alert.Distance:F0}m  ·  {patron.VisitCount} visit{(patron.VisitCount != 1 ? "s" : "")}");

        if (!string.IsNullOrWhiteSpace(patron.FavoriteDrink))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"· 🍹 {patron.FavoriteDrink}");
        }

        // ── Row 3 (optional): Target button ────────────────────────────
        if (showTarget)
        {
            if (ImGui.SmallButton($"👁 Target##{alert.Id}"))
            {
                var obj = Svc.Objects.FirstOrDefault(o => o.Name.ToString() == patron.Name);
                if (obj != null)
                    Svc.Targets.Target = obj;
            }
        }
    }
}
