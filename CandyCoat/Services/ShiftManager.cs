using System;
using System.Linq;
using System.Collections.Generic;
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

    public IEnumerable<Shift> ShiftHistory => _plugin.Configuration.StaffShifts
        .Where(s => !s.IsActive)
        .OrderByDescending(s => s.StartTime);
    
    public void ClockIn()
    {
        if (CurrentShift == null)
        {
            _plugin.Configuration.StaffShifts.Add(new Shift { StartTime = DateTime.UtcNow });
            _plugin.Configuration.Save();
            _plugin.SyncService.SendHeartbeatAsync();
        }
    }
    
    public void ClockOut()
    {
        var shift = CurrentShift;
        if (shift != null)
        {
            shift.EndTime = DateTime.UtcNow;
            _plugin.Configuration.Save();
        }
    }
}
