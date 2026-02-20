using System;

namespace CandyCoat.Data;

public class Shift
{
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public int GilEarned { get; set; } = 0;
    
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.Now - StartTime;
    public bool IsActive => !EndTime.HasValue;
}
