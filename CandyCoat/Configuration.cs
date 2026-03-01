using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using CandyCoat.Data;

namespace CandyCoat;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsSetupComplete { get; set; } = false;
    public bool IsManagementModeEnabled { get; set; } = false;
    public List<Booking> Bookings { get; set; } = new();
    public List<MacroTemplate> Macros { get; set; } = new();
    public List<Patron> Patrons { get; set; } = new();
    public List<Shift> StaffShifts { get; set; } = new();
    public Dictionary<string, int> DailyEarnings { get; set; } = new();
    public string CharacterName { get; set; } = string.Empty;
    public string HomeWorld { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string UserMode  { get; set; } = string.Empty;
    public bool EnableGlamourer { get; set; } = true;
    public bool EnableChatTwo { get; set; } = true;
    public bool IsConfigWindowMovable { get; set; } = true;
    public CosmeticProfile CosmeticProfile { get; set; } = new();

    // SRT Role Config
    public StaffRole PrimaryRole { get; set; } = StaffRole.None;
    public StaffRole EnabledRoles { get; set; } = StaffRole.None;
    public bool MultiRoleEnabled { get; set; } = false;

    // Venue-wide (Owner configures)
    public List<ServiceMenuItem> ServiceMenu { get; set; } = new();
    public List<VenueRoom> Rooms { get; set; } = new();
    public string VenueName { get; set; } = string.Empty;

    // Per-role data
    public List<PatronNote> PatronNotes { get; set; } = new();
    public List<EarningsEntry> Earnings { get; set; } = new();

    // Gamba presets
    public List<GambaGamePreset> GambaPresets { get; set; } = new();

    // Per-role Quick-Tell templates
    public List<Data.MacroTemplate> SweetheartMacros { get; set; } = new();
    public List<Data.MacroTemplate> CandyHeartMacros { get; set; } = new();
    public List<Data.MacroTemplate> BartenderMacros { get; set; } = new();

    // Role cosmetic defaults (Owner-configurable)
    public Dictionary<StaffRole, RoleDefaultCosmetic> RoleDefaults { get; set; } = new();

    // Patron loyalty tier thresholds (Owner-configurable)
    public int RegularTierVisits { get; set; } = 3;
    public int EliteTierVisits { get; set; } = 10;
    public int RegularTierGil { get; set; } = 100_000;
    public int EliteTierGil { get; set; } = 1_000_000;

    // Setup wizard progress (persisted so users can resume mid-setup)
    public int SetupWizardStep { get; set; } = 0;

    // Sync API settings
    public string ApiUrl { get; set; } = string.Empty;
    public string VenueKey { get; set; } = string.Empty;
    public string VenueId { get; set; } = string.Empty; // Future: multi-venue
    public bool EnableSync { get; set; } = false;
    public bool CosmeticAutoRedraw { get; set; } = false;
    public DateTime LastSyncTimestamp { get; set; } = DateTime.MinValue;

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public PatronTier GetTier(Patron patron)
    {
        if (patron.VisitCount >= EliteTierVisits || patron.TotalGilSpent >= EliteTierGil)
            return PatronTier.Elite;
        if (patron.VisitCount >= RegularTierVisits || patron.TotalGilSpent >= RegularTierGil)
            return PatronTier.Regular;
        return PatronTier.Guest;
    }
}
