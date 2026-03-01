using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using CandyCoat.Data;

namespace CandyCoat.Services;

/// <summary>
/// Holds a single pending patron entry alert shown in the overlay.
/// </summary>
public class PatronAlertEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public Patron Patron { get; init; } = null!;
    public PatronTier Tier { get; init; }
    public float Distance { get; init; }
    public DateTime ArrivedAt { get; init; } = DateTime.UtcNow;
    public bool Dismissed { get; set; }
}

/// <summary>
/// Listens to LocatorService arrival events and dispatches alerts via
/// the in-game panel overlay, chat echo, or both — based on config.
/// Manages per-patron cooldowns and auto-dismiss timing.
/// </summary>
public class PatronAlertService : IDisposable
{
    private readonly Plugin _plugin;
    private readonly LocatorService _locator;

    private readonly List<PatronAlertEntry> _alerts = new();
    private readonly Dictionary<string, DateTime> _cooldowns = new();

    public IReadOnlyList<PatronAlertEntry> ActiveAlerts => _alerts;

    public PatronAlertService(Plugin plugin, LocatorService locator)
    {
        _plugin = plugin;
        _locator = locator;
        _locator.OnPatronArrived += HandlePatronArrived;
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (_alerts.Count == 0) return;
        var dismissAfter = TimeSpan.FromSeconds(_plugin.Configuration.AlertDismissSeconds);
        _alerts.RemoveAll(a => a.Dismissed || DateTime.UtcNow - a.ArrivedAt >= dismissAfter);
    }

    private void HandlePatronArrived(Patron patron, float distance)
    {
        if (!_plugin.Configuration.EnablePatronAlerts) return;
        if (!_plugin.Configuration.IsSetupComplete) return;

        bool isDanger = patron.Status is PatronStatus.Warning or PatronStatus.Blacklisted;

        // AlertOnRegularOnly suppresses alerts for non-tracked or neutral patrons,
        // but always allows danger-status alerts through.
        if (_plugin.Configuration.AlertOnRegularOnly && !isDanger &&
            patron.Status != PatronStatus.Regular)
            return;

        // Time-based cooldown — don't re-alert the same patron too often
        if (_cooldowns.TryGetValue(patron.Name, out var last) &&
            (DateTime.UtcNow - last).TotalMinutes < _plugin.Configuration.AlertCooldownMinutes)
            return;

        _cooldowns[patron.Name] = DateTime.UtcNow;

        var tier   = _plugin.Configuration.GetTier(patron);
        var entry  = new PatronAlertEntry { Patron = patron, Tier = tier, Distance = distance };
        var method = _plugin.Configuration.AlertMethod;

        if (method is PatronAlertMethod.Panel or PatronAlertMethod.Both)
            _alerts.Add(entry);

        if (method is PatronAlertMethod.Chat or PatronAlertMethod.Both)
            SendChatAlert(patron, tier, distance, isDanger);
    }

    private static void SendChatAlert(Patron patron, PatronTier tier, float distance, bool isDanger)
    {
        var message = isDanger
            ? BuildDangerSummary(patron, distance)
            : BuildCrmSummary(patron, tier, distance);

        Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
        {
            Type    = Dalamud.Game.Text.XivChatType.Echo,
            Message = message
        });
    }

    private static string BuildDangerSummary(Patron patron, float distance)
    {
        var label = patron.Status == PatronStatus.Blacklisted ? "[BLACKLIST]" : "[WARNING]";
        var sb = new StringBuilder();
        sb.Append($"[CandyCoat] {label} {patron.Name} is nearby! ({distance:F1}m)");
        if (!string.IsNullOrWhiteSpace(patron.Notes))
        {
            var note = patron.Notes.Split('\n')[0].Trim();
            if (note.Length > 80) note = note[..80] + "…";
            sb.Append($" — {note}");
        }
        return sb.ToString();
    }

    internal static string BuildCrmSummary(Patron patron, PatronTier tier, float distance)
    {
        var sb = new StringBuilder();
        sb.Append($"[CandyCoat] ♥ {patron.Name} [{tier}] is here! ({distance:F1}m) — ");
        sb.Append($"{patron.VisitCount} visit{(patron.VisitCount != 1 ? "s" : "")}");

        if (patron.TotalGilSpent > 0)
            sb.Append($" · {patron.TotalGilSpent:N0} Gil");

        if (!string.IsNullOrWhiteSpace(patron.FavoriteDrink))
            sb.Append($" · Drink: {patron.FavoriteDrink}");

        return sb.ToString();
    }

    public void Dismiss(Guid alertId)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
        if (alert != null) alert.Dismissed = true;
    }

    public void DismissAll() => _alerts.ForEach(a => a.Dismissed = true);

    public void Dispose()
    {
        _locator.OnPatronArrived -= HandlePatronArrived;
        Svc.Framework.Update -= OnFrameworkUpdate;
    }
}
