using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;

namespace SamplePlugin.Services;

public class SessionMessage
{
    public DateTime Timestamp { get; set; }
    public string Sender { get; set; } = string.Empty;
    public SeString Content { get; set; } = SeString.Empty;
    public bool IsMe { get; set; } // True if I sent it, False if Target sent it
}

public class SessionManager : IDisposable
{
    public bool IsCapturing { get; private set; } = false;
    public string TargetName { get; private set; } = string.Empty;
    public List<SessionMessage> Messages { get; private set; } = new();

    public event Action? OnMessageAdded;

    public SessionManager()
    {
        Svc.Chat.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Svc.Chat.ChatMessage -= OnChatMessage;
    }

    public void StartCapture(string targetName)
    {
        TargetName = targetName;
        IsCapturing = true;
        Messages.Clear();
        Svc.Log.Info($"[CandyCoat] Started session capture with {TargetName}");
    }

    public void StopCapture()
    {
        IsCapturing = false;
        Svc.Log.Info($"[CandyCoat] Stopped session capture.");
    }

    public void Clear()
    {
        Messages.Clear();
        OnMessageAdded?.Invoke();
    }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!IsCapturing) return;

        var senderName = sender.TextValue;
        var localName = Svc.ClientState.LocalPlayer?.Name.ToString();

        // 1. Check if it's the target speaking
        // Note: senderName might contain cross-world markers, need robust check if strictly needed,
        // but explicit string match is usually okay for same-world.
        if (senderName == TargetName)
        {
            AddMessage(senderName, message, false);
            return;
        }

        // 2. Check if it's ME speaking
        // When we speak, it usually comes through as XivChatType.Standard (Say), Party, TellOutgoing, etc.
        // We want to capture what *we* say too.
        if (senderName == localName)
        {
            AddMessage(localName, message, true);
            return;
        }
    }

    private void AddMessage(string sender, SeString content, bool isMe)
    {
        Messages.Add(new SessionMessage
        {
            Timestamp = DateTime.Now,
            Sender = sender,
            Content = content,
            IsMe = isMe
        });
        OnMessageAdded?.Invoke();
    }
}
