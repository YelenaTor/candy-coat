using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using CandyCoat.Data;

namespace CandyCoat.Services;

public class LocatorService : IDisposable
{
    private readonly Plugin _plugin;
    private int _frameCount = 0;
    private const int ScanIntervalFrames = 60; // Approx 1 second at 60fps
    private readonly HashSet<string> _alertedPatrons = new();

    // Cached state for the UI to read from
    public List<(Data.Patron Patron, float Distance)> NearbyRegulars { get; private set; } = new();

    public LocatorService(Plugin plugin)
    {
        _plugin = plugin;
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        _frameCount++;
        if (_frameCount >= ScanIntervalFrames)
        {
            _frameCount = 0;
            ScanForPatrons();
        }
    }

    private void ScanForPatrons()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            NearbyRegulars.Clear();
            _alertedPatrons.Clear();
            return;
        }

        var newCache = new List<(Data.Patron, float)>();
        var currentNearbyNames = new HashSet<string>();

        foreach (var player in Svc.Objects)
        {
            if (player is not Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter) continue;
            var playerName = player.Name.ToString();
            var patron = _plugin.Configuration.Patrons.Find(p => p.Name == playerName);
            
            if (patron != null && patron.Status != PatronStatus.Neutral)
            {
                var distance = Vector3.Distance(localPlayer.Position, player.Position);
                newCache.Add((patron, distance));
                currentNearbyNames.Add(playerName);
                
                if (!_alertedPatrons.Contains(playerName))
                {
                    _alertedPatrons.Add(playerName);

                    if (patron.Status is PatronStatus.Warning or PatronStatus.Blacklisted)
                    {
                        var alertLevel = patron.Status == PatronStatus.Blacklisted ? "[BLACKLIST]" : "[WARNING]";
                        Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                        {
                            Type = Dalamud.Game.Text.XivChatType.Echo,
                            Message = $"[CandyCoat] {alertLevel} {playerName} is nearby! ({distance:F1}m). Notes: {patron.Notes}"
                        });
                    }
                    else if (patron.Status == PatronStatus.Regular)
                    {
                        Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                        {
                            Type = Dalamud.Game.Text.XivChatType.Echo,
                            Message = BuildCrmSummary(patron, distance)
                        });
                    }
                }
                
                // Update LastSeen
                patron.LastSeen = DateTime.Now;
            }
        }

        // Clean up alerted patrons who left so we can alert again if they return
        _alertedPatrons.RemoveWhere(name => !currentNearbyNames.Contains(name));

        NearbyRegulars = newCache;
    }

    private static string BuildCrmSummary(Data.Patron patron, float distance)
    {
        var parts = new System.Text.StringBuilder();
        parts.Append($"[CandyCoat] ♥ {patron.Name} is here! ({distance:F1}m) — ");
        parts.Append($"{patron.VisitCount} visit{(patron.VisitCount != 1 ? "s" : "")}");

        if (patron.TotalGilSpent > 0)
            parts.Append($" · {patron.TotalGilSpent:N0} Gil spent");

        if (!string.IsNullOrWhiteSpace(patron.FavoriteDrink))
            parts.Append($" · Drink: {patron.FavoriteDrink}");

        var lastNote = patron.Notes?.Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
                                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (!string.IsNullOrWhiteSpace(lastNote))
        {
            var preview = lastNote.Length > 60 ? lastNote[..60] + "…" : lastNote;
            parts.Append($" · Note: {preview}");
        }

        if (patron.LastVisitDate != default && patron.LastVisitDate != patron.LastSeen)
            parts.Append($" · Last: {patron.LastVisitDate:MM/dd}");

        return parts.ToString();
    }

    public int GetNearbyCount()
    {
        int count = 0;
        foreach (var obj in Svc.Objects)
        {
            if (obj is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter)
                count++;
        }
        return count;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
    }
}
