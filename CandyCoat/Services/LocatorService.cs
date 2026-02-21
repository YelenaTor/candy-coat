using System;
using System.Collections.Generic;
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
            // Objects list contains many things, filter by name
            var playerName = player.Name.ToString();
            var patron = _plugin.Configuration.Patrons.Find(p => p.Name == playerName);
            
            if (patron != null && patron.Status != PatronStatus.Neutral)
            {
                var distance = Vector3.Distance(localPlayer.Position, player.Position);
                newCache.Add((patron, distance));
                currentNearbyNames.Add(playerName);
                
                if (patron.Status is PatronStatus.Warning or PatronStatus.Blacklisted)
                {
                    if (!_alertedPatrons.Contains(playerName))
                    {
                        _alertedPatrons.Add(playerName);
                        var alertLevel = patron.Status == PatronStatus.Blacklisted ? "[BLACKLIST]" : "[WARNING]";
                        Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                        {
                            Type = Dalamud.Game.Text.XivChatType.Echo,
                            Message = $"[CandyCoat] {alertLevel} {playerName} is nearby! ({distance:F1}m). Notes: {patron.Notes}"
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

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
    }
}
