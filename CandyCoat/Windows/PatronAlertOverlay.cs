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
using Una.Drawing;

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

    // Una.Drawing root — rebuilt when alert count changes
    private Node? _root;
    private int   _builtAlertCount = -1;

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

    public void Dispose()
    {
        _root?.Dispose();
        _root = null;
    }

    private void BuildRoot(int alertCount)
    {
        _root?.Dispose();
        _builtAlertCount = alertCount;

        if (alertCount == 0)
        {
            _root = CandyUI.Column("alert-root", 0);
            return;
        }

        // Spacer column — one InputSpacer per alert card.
        // Actual card rendering is done via ImGui overlays in DrawOverlays().
        var children = new Node[alertCount];
        for (int i = 0; i < alertCount; i++)
        {
            bool showTarget = _plugin.Configuration.EnableTargetOnAlertClick;
            int  rows       = showTarget ? 3 : 2;
            // Approximate card height: 22px per row + 12px padding
            var cardSpacer = CandyUI.InputSpacer($"alert-card-spacer-{i}", (int)CardWidth, rows * 22 + 12);
            children[i] = cardSpacer;
        }

        _root = CandyUI.Column("alert-root", 6, children);
    }

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

        if (_root == null || _builtAlertCount != alerts.Count)
            BuildRoot(alerts.Count);

        var region = ImGui.GetContentRegionAvail();
        _root!.Style.Size = new Size((int)region.X, (int)region.Y);

        var pos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _root.Render(ImGui.GetWindowDrawList(), pos);
        ImGui.Dummy(region);

        DrawOverlays(alerts);
    }

    private void DrawOverlays(System.Collections.Generic.List<PatronAlertEntry> alerts)
    {
        ImGui.SetCursorPos(new Vector2(0, 0));

        for (int i = 0; i < alerts.Count; i++)
        {
            DrawCard(alerts[i]);
            if (i < alerts.Count - 1)
                ImGui.Spacing();
        }
    }

    private void DrawCard(PatronAlertEntry alert)
    {
        var patron    = alert.Patron;
        var vip       = patron.ActiveVip;
        bool isDanger = patron.Status is PatronStatus.Warning or PatronStatus.Blacklisted;
        bool hasActiveVip  = !isDanger && vip != null && !vip.IsExpired;
        bool hasExpiredVip = !isDanger && vip != null && vip.IsExpired;

        var cardBg = isDanger
            ? new Vector4(0.30f, 0.05f, 0.05f, 1f)
            : hasActiveVip
                ? new Vector4(0.25f, 0.18f, 0.04f, 0.95f)
                : hasExpiredVip
                    ? new Vector4(0.16f, 0.12f, 0.18f, 0.95f)
                    : new Vector4(0.14f, 0.08f, 0.20f, 1f);

        bool showTarget = _plugin.Configuration.EnableTargetOnAlertClick;
        float rowH      = ImGui.GetFrameHeightWithSpacing();
        float padY      = ImGui.GetStyle().WindowPadding.Y;
        float cardH     = rowH * (showTarget ? 3f : 2f) + padY * 2f;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg);
        using var card = ImRaii.Child($"##AC{alert.Id}", new Vector2(CardWidth, cardH), true,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.PopStyleColor();

        if (!card) return;

        // ── Row 1: icon + name + tier/VIP badge + dismiss ──────────────────
        Vector4 nameCol;
        string icon;
        string tierLabel;

        if (isDanger)
        {
            nameCol = patron.Status == PatronStatus.Blacklisted
                ? new Vector4(1f, 0.25f, 0.25f, 1f)
                : new Vector4(1f, 0.78f, 0.2f, 1f);
            icon      = patron.Status == PatronStatus.Blacklisted ? "!!" : "!";
            tierLabel = patron.Status.ToString().ToUpperInvariant();
        }
        else if (hasActiveVip)
        {
            nameCol   = VipColours.GetTierColour(vip!.Tier);
            icon      = "[VIP]";
            tierLabel = vip.PackageName;
        }
        else if (hasExpiredVip)
        {
            nameCol   = new Vector4(0.55f, 0.50f, 0.58f, 1f);
            icon      = "[x]";
            tierLabel = "VIP EXPIRED";
        }
        else
        {
            nameCol = alert.Tier == PatronTier.Elite
                ? new Vector4(1f, 0.85f, 0.2f, 1f)
                : new Vector4(1f, 0.65f, 0.85f, 1f);
            icon      = alert.Tier == PatronTier.Elite ? "*" : "+";
            tierLabel = alert.Tier.ToString();
        }

        ImGui.TextColored(nameCol, $"{icon} {patron.Name}");
        ImGui.SameLine();
        ImGui.TextDisabled($"[{tierLabel}]");

        // Dismiss button — right-aligned
        float dismissX = ImGui.GetWindowWidth()
                       - ImGui.CalcTextSize("x").X
                       - ImGui.GetStyle().FramePadding.X * 2f
                       - ImGui.GetStyle().WindowPadding.X;
        ImGui.SameLine(dismissX);
        if (ImGui.SmallButton($"x##D{alert.Id}"))
            _alertService.Dismiss(alert.Id);

        // ── Row 2: distance · visits · VIP status or drink ─────────────────
        ImGui.TextDisabled($"{alert.Distance:F0}m  ·  {patron.VisitCount} visit{(patron.VisitCount != 1 ? "s" : "")}");

        if (hasActiveVip)
        {
            ImGui.SameLine();
            var daysRemaining = vip!.DaysRemaining;
            var daysText = daysRemaining == -1
                ? "· Permanent"
                : $"· {daysRemaining} day{(daysRemaining != 1 ? "s" : "")} left";
            ImGui.TextDisabled(daysText);
        }
        else if (hasExpiredVip)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "· EXPIRED");
        }
        else if (!string.IsNullOrWhiteSpace(patron.FavoriteDrink))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"· {patron.FavoriteDrink}");
        }

        // ── Row 3 (optional): Target button ────────────────────────────────
        if (showTarget)
        {
            if (ImGui.SmallButton($"[>] Target##{alert.Id}"))
            {
                var obj = Svc.Objects.FirstOrDefault(o => o.Name.ToString() == patron.Name);
                if (obj != null)
                    Svc.Targets.Target = obj;
            }
        }
    }
}
