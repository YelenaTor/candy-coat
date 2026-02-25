using System;

namespace CandyCoat.API.Models;

public class StaffEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; } // Future: FK to venues table
    public string CharacterName { get; set; } = string.Empty;
    public string HomeWorld { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // StaffRole.ToString()
    public bool IsOnline { get; set; } = false;
    public bool IsDnd { get; set; } = false;
    public DateTime? ShiftStart { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
}
