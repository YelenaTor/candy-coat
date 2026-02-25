using System;

namespace CandyCoat.API.Models;

public class PatronEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; } // Future: FK to venues table
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string Status { get; set; } = "Neutral"; // Neutral, Regular, VIP, Warning, Blacklisted
    public int VisitCount { get; set; } = 0;
    public int TotalGilSpent { get; set; } = 0;
    public string Notes { get; set; } = string.Empty;
    public string RpHooks { get; set; } = string.Empty;
    public string FavoriteDrink { get; set; } = string.Empty;
    public string Allergies { get; set; } = string.Empty;
    public string BlacklistReason { get; set; } = string.Empty;
    public DateTime? BlacklistDate { get; set; }
    public string BlacklistFlaggedBy { get; set; } = string.Empty;
    public DateTime? LastSeen { get; set; }
}
