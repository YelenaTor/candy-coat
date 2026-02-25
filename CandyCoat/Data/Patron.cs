using System;
using System.Collections.Generic;

namespace CandyCoat.Data;

public class Patron
{
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public PatronStatus Status { get; set; } = PatronStatus.Neutral;
    public int TotalGilSpent { get; set; } = 0;
    public string Notes { get; set; } = string.Empty;
    public string RpHooks { get; set; } = string.Empty;
    public string FavoriteDrink { get; set; } = string.Empty;
    public string Allergies { get; set; } = string.Empty;
    public List<Guid> QuickSwitchDesignIds { get; set; } = new();
    public DateTime LastSeen { get; set; } = DateTime.Now;
    public DateTime LastVisitDate { get; set; } = DateTime.Now;
    public int VisitCount { get; set; } = 0;
    public string BlacklistReason { get; set; } = string.Empty;
    public DateTime? BlacklistDate { get; set; }
    public string BlacklistFlaggedBy { get; set; } = string.Empty;
}
