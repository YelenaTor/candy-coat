using CandyCoat.Data;

namespace CandyCoat.Windows.SRT;

internal static class SrtPanelHelper
{
    internal static string PanelIcon(StaffRole role) => role switch
    {
        StaffRole.Sweetheart  => "\u2665",
        StaffRole.CandyHeart  => "\u2601",
        StaffRole.Bartender   => "\ud83c\udf78",
        StaffRole.Gamba       => "\ud83c\udfb2",
        StaffRole.DJ          => "\u266c",
        StaffRole.Management  => "\ud83d\udccb",
        StaffRole.Owner       => "\u2605",
        StaffRole.Greeter     => "\ud83d\udea8",
        _                     => "\u25cf",
    };
}
