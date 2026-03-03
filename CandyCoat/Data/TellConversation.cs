using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CandyCoat.Data;

[Serializable]
public class TellConversation
{
    public string PlayerName { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<TellMessage> Messages { get; set; } = new();

    [JsonIgnore]
    public DateTime LastActivity => Messages.LastOrDefault()?.Timestamp ?? DateTime.MinValue;

    [JsonIgnore]
    public int UnreadCount => Messages.Count(m => !m.IsRead && !m.IsOutgoing);
}
