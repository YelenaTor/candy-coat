using System;

namespace CandyCoat.API.Models;

public class BookingEntity
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public string PatronName { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public int Gil { get; set; }
    public string State { get; set; } = "Active"; // Active, Inactive, CompletedPaid, CompletedUnpaid
    public string StaffName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(60);
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
