using System;

namespace CandyCoat.Data;

public class WaitlistEntry
{
    public string PatronName { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.Now;
    
    public TimeSpan TimeWaited => DateTime.Now - AddedAt;
}
