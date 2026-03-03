using System;

namespace CandyCoat.Data;

[Serializable]
public class TellMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsOutgoing { get; set; }
    public bool IsRead { get; set; }
}
