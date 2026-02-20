using System;
using System.Linq;
using CandyCoat.Data;

namespace CandyCoat.Services;

public class ShiftManager
{
    private readonly Plugin _plugin;
    
    public ShiftManager(Plugin plugin)
    {
        _plugin = plugin;
    }
    
    public Shift? CurrentShift => _plugin.Configuration.StaffShifts.LastOrDefault(s => s.IsActive);
    
    public void ClockIn()
    {
        if (CurrentShift == null)
        {
            _plugin.Configuration.StaffShifts.Add(new Shift { StartTime = DateTime.Now });
            _plugin.Configuration.Save();
        }
    }
    
    public void ClockOut()
    {
        var shift = CurrentShift;
        if (shift != null)
        {
            shift.EndTime = DateTime.Now;
            _plugin.Configuration.Save();
        }
    }
}
