using System;

namespace CandyCoat.API.Models;

public class GlobalProfileEntity
{
    public string ProfileId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string HomeWorld { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string StaffData { get; set; } = "{}";
    public string PatronData { get; set; } = "{}";
    public string RegisteredVenues { get; set; } = "[]";
    public bool HasGlamourerIntegrated { get; set; } = false;
    public bool HasChatTwoIntegrated { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeen { get; set; }
}
