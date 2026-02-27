using System;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;

namespace CandyCoat.Services;

public class TradeMonitorService : IDisposable
{
    private readonly Plugin _plugin;
    
    // Regex for: "You trade X Gil to Name."
    private static readonly Regex OutgoingTradeRegex = new(@"You trade (?<amount>[\d,]+) Gil to (?<name>.+)\.", RegexOptions.Compiled);
    // Regex for: "Name trades you X Gil."
    private static readonly Regex IncomingTradeRegex = new(@"(?<name>.+) trades you (?<amount>[\d,]+) Gil\.", RegexOptions.Compiled);

    // Fired after each incoming trade: (cleanPatronName, amount, wasLinkedToBooking)
    public event Action<string, int, bool>? OnTradeDetected;

    public TradeMonitorService(Plugin plugin)
    {
        _plugin = plugin;
        Svc.Chat.ChatMessage += OnChatMessage;
    }

    private static bool TryParseGilAmount(string raw, out int amount) =>
        int.TryParse(raw.Replace(",", ""), out amount);

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        // System messages for trades are usually System messages (type 57 or something similar)
        if (type != XivChatType.SystemMessage) return;

        var text = message.TextValue;

        var incomingMatch = IncomingTradeRegex.Match(text);
        if (incomingMatch.Success)
        {
            if (TryParseGilAmount(incomingMatch.Groups["amount"].Value, out int amount))
                HandleTrade(incomingMatch.Groups["name"].Value, amount);
            return;
        }

        var outgoingMatch = OutgoingTradeRegex.Match(text);
        if (outgoingMatch.Success)
        {
            // For venues, you rarely pay the patron, but capturing it just in case
            if (TryParseGilAmount(outgoingMatch.Groups["amount"].Value, out int amount))
                Svc.Log.Info($"[CandyCoat] Outgoing trade detected to {outgoingMatch.Groups["name"].Value} for {amount} Gil.");
        }
    }

    private void HandleTrade(string patronName, int amount)
    {
        Svc.Log.Info($"[CandyCoat] Detected incoming trade from {patronName} for {amount} Gil.");

        // Clean up cross-world suffix if present (e.g., Name Surname@World -> Name Surname)
        var cleanName = patronName.Split('@')[0].Trim();

        // 1. Update Patron TotalGilSpent
        var patron = _plugin.Configuration.Patrons.Find(p => p.Name.StartsWith(cleanName, StringComparison.OrdinalIgnoreCase));
        if (patron != null)
        {
            patron.TotalGilSpent += amount;
        }

        // 2. Update Daily Earnings
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (!_plugin.Configuration.DailyEarnings.ContainsKey(today))
        {
            _plugin.Configuration.DailyEarnings[today] = 0;
        }
        _plugin.Configuration.DailyEarnings[today] += amount;

        // 3. Update Shift Earnings (if active)
        var activeShift = _plugin.Configuration.StaffShifts.Find(s => s.IsActive);
        if (activeShift != null)
        {
            activeShift.GilEarned += amount;
        }

        bool linkedToBooking = false;
        foreach (var booking in _plugin.Configuration.Bookings)
        {
            if (booking.State == Data.BookingState.Active && booking.Gil > 0)
            {
                if (booking.PatronName.StartsWith(cleanName, StringComparison.OrdinalIgnoreCase))
                {
                    if (amount >= booking.Gil)
                    {
                        booking.State = Data.BookingState.CompletedPaid;
                        linkedToBooking = true;
                        Svc.Log.Info($"[CandyCoat] Automatically marked booking for {booking.PatronName} as Completed (Paid).");
                        break;
                    }
                }
            }
        }

        _plugin.Configuration.Save();
        OnTradeDetected?.Invoke(cleanName, amount, linkedToBooking);
    }

    public void Dispose()
    {
        Svc.Chat.ChatMessage -= OnChatMessage;
    }
}
