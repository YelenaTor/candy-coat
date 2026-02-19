using System;
using System.Collections.Generic;

namespace CandyCoat.Data;

public class Patron
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string RpHooks { get; set; } = string.Empty;
    public List<Guid> QuickSwitchDesignIds { get; set; } = new();
    public DateTime LastSeen { get; set; } = DateTime.Now;
}
