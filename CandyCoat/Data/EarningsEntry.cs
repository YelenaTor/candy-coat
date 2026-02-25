using System;

namespace CandyCoat.Data;

public enum EarningsType
{
    Session,
    Drink,
    Tip,
    GamePayout,
    Other
}

public class EarningsEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public StaffRole Role { get; set; }
    public EarningsType Type { get; set; }
    public string PatronName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Amount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
