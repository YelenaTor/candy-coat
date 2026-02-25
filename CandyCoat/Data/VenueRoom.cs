using System;

namespace CandyCoat.Data;

public enum RoomStatus
{
    Available,
    Occupied,
    Reserved,
    Maintenance
}

public class VenueRoom
{
    public string Name { get; set; } = string.Empty;
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string OccupiedBy { get; set; } = string.Empty;
    public string PatronName { get; set; } = string.Empty;
    public DateTime? OccupiedSince { get; set; }
}
