using System;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using CandyCoat.Data;
using ECommons.DalamudServices;

namespace CandyCoat.Services;

public class TellService : IDisposable
{
    private readonly Plugin _plugin;
    private TellConversation? _selectedConversation;

    public event Action? OnTellReceived;

    public TellConversation? SelectedConversation => _selectedConversation;

    public TellService(Plugin plugin)
    {
        _plugin = plugin;
        Svc.Chat.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Svc.Chat.ChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type == XivChatType.TellIncoming)
        {
            var playerName = sender.TextValue;
            AddMessage(playerName, message.TextValue, isOutgoing: false);

            if (_plugin.Configuration.TellSuppressInGame)
                isHandled = true;

            if (_plugin.Configuration.TellAutoOpen)
            {
                _selectedConversation = GetOrCreateConversation(playerName);
                _plugin.TellWindow.IsOpen = true;
            }

            OnTellReceived?.Invoke();
        }
        else if (type == XivChatType.TellOutgoing)
        {
            // For outgoing tells, the game puts the recipient's name in sender
            var playerName = sender.TextValue;
            AddMessage(playerName, message.TextValue, isOutgoing: true);
            OnTellReceived?.Invoke();
        }
    }

    private void AddMessage(string playerName, string content, bool isOutgoing)
    {
        var cfg = _plugin.Configuration;
        var conv = GetOrCreateConversation(playerName);

        conv.Messages.Add(new TellMessage
        {
            Sender = isOutgoing ? cfg.CharacterName : playerName,
            Content = content,
            IsOutgoing = isOutgoing,
            IsRead = isOutgoing
        });

        // Trim oldest messages to stay within per-conversation cap
        while (conv.Messages.Count > cfg.TellHistoryMaxMessages)
            conv.Messages.RemoveAt(0);

        cfg.Save();
    }

    public TellConversation GetOrCreateConversation(string playerName)
    {
        var cfg = _plugin.Configuration;
        var conv = cfg.TellHistory.FirstOrDefault(c =>
            string.Equals(c.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));

        if (conv == null)
        {
            conv = new TellConversation { PlayerName = playerName };
            cfg.TellHistory.Add(conv);
            cfg.Save();
        }

        return conv;
    }

    public void SelectConversation(string playerName)
    {
        _selectedConversation = GetOrCreateConversation(playerName);
    }

    public void SelectConversation(TellConversation conv)
    {
        _selectedConversation = conv;
    }

    public void ClearSelection()
    {
        _selectedConversation = null;
    }

    public void SendTell(string playerName, string message)
    {
        // The TellOutgoing chat hook will capture this automatically when the game processes it
        Plugin.CommandManager.ProcessCommand($"/tell {playerName} {message}");
    }
}
