using System;

namespace CandyCoat.API.Models;

public class VenueConfigEntity
{
    public string VenueId { get; set; } = string.Empty;
    public bool ManagerPwAdded { get; set; } = false;
    public string ManagerPw { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
