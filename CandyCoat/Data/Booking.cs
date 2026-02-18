using System;

namespace SamplePlugin.Data;

public enum BookingState
{
    Active,
    Inactive,
    CompletedPaid,
    CompletedUnpaid
}

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PatronName { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public int Gil { get; set; }
    public BookingState State { get; set; } = BookingState.Active;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
