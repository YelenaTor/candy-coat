using System;
using System.Collections.Generic;
using System.Linq;
using CandyCoat.Data;

namespace CandyCoat.Services;

public class VenueService
{
    private readonly Plugin _plugin;

    public VenueService(Plugin plugin)
    {
        _plugin = plugin;
    }

    public Booking AddBooking(string patronName, string service, string room, int gil)
    {
        var booking = new Booking
        {
            PatronName = patronName,
            Service = service,
            Room = room,
            Gil = gil,
            Timestamp = DateTime.Now,
            State = BookingState.Active,
            Duration = TimeSpan.FromMinutes(60)
        };

        _plugin.Configuration.Bookings.Add(booking);
        EnsurePatronExists(patronName);
        _plugin.Configuration.Save();
        return booking;
    }

    public void UpdateBookingState(Booking booking, BookingState newState)
    {
        booking.State = newState;
        _plugin.Configuration.Save();
    }

    public void RemoveBooking(Booking booking)
    {
        _plugin.Configuration.Bookings.Remove(booking);
        _plugin.Configuration.Save();
    }

    public Patron EnsurePatronExists(string name)
    {
        var patron = _plugin.Configuration.Patrons.FirstOrDefault(p => p.Name == name);
        if (patron == null)
        {
            patron = new Patron { Name = name, World = "Unknown" };
            _plugin.Configuration.Patrons.Add(patron);
            _plugin.Configuration.Save();
        }
        return patron;
    }

    public void TrackPatron(string name)
    {
        var patron = EnsurePatronExists(name);
        patron.Status = PatronStatus.Regular;
        _plugin.Configuration.Save();
    }

    public void UntrackPatron(Patron patron)
    {
        patron.Status = PatronStatus.Neutral;
        // Optionally remove if no longer needed, but keeping for history usually better
        _plugin.Configuration.Save();
    }
}
