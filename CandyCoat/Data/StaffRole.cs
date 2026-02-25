using System;

namespace CandyCoat.Data;

[Flags]
public enum StaffRole
{
    None        = 0,
    Sweetheart  = 1 << 0,  // Entertainers / Companions
    CandyHeart  = 1 << 1,  // Greeters / Welcome team
    Bartender   = 1 << 2,  // Bar / drink service
    Gamba       = 1 << 3,  // Gambling / games
    DJ          = 1 << 4,  // Music / performance
    Management  = 1 << 5,  // Staff oversight
    Owner       = 1 << 6,  // Venue-wide admin
}
