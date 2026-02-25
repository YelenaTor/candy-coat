using System;

namespace CandyCoat.API.Models;

public class ServiceMenuEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; } // Future: FK to venues table
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public string Category { get; set; } = string.Empty; // Session, Drink, Game, Performance, Other
}
