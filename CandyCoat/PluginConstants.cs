namespace CandyCoat;

internal static class PluginConstants
{
    /// <summary>Production API base URL — hardcoded for Sugar Venue deployment.</summary>
    public const string ProductionApiUrl = "https://145.241.101.66.nip.io";

    /// <summary>
    /// Venue key that identifies and authenticates the Sugar Venue plugin instance.
    /// Stays in binary for zero-disruption migration of existing Sugar staff.
    /// Validated against the Venues table server-side (DB is source of truth).
    /// </summary>
    public const string VenueKey = "sugar-venue-2026-master-13";

    /// <summary>
    /// Sugar's pre-seeded VenueId — deterministic from the key above via MD5.
    /// Computed as: new Guid(MD5.HashData(Encoding.UTF8.GetBytes(VenueKey)))
    /// Must match the value seeded in migration AddVenueRegistry.
    /// </summary>
    internal const string SugarVenueId = "7c303cf0-a169-49c3-186f-8bc93d58616c";
}
