using System;

namespace CandyCoat.API.Models;

public class EarningsEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; } // Future: FK to venues table
    public string Role { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Session, Drink, Tip, GamePayout
    public string PatronName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Amount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
