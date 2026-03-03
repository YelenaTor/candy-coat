using System;

namespace CandyCoat.API.Models;

public class VenueEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VenueKey { get; set; } = string.Empty;      // unique — auth secret
    public string VenueName { get; set; } = string.Empty;
    public string OwnerProfileId { get; set; } = string.Empty; // "" = no owner yet
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
