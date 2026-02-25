using System;

namespace CandyCoat.API.Models;

public class RoomEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; } // Future: FK to venues table
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Available"; // Available, Occupied, Reserved, Maintenance
    public string OccupiedBy { get; set; } = string.Empty;
    public string PatronName { get; set; } = string.Empty;
    public DateTime? OccupiedSince { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
