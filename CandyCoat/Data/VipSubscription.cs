using System;
using Newtonsoft.Json;

namespace CandyCoat.Data;

public class VipSubscription
{
    public Guid PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public VipTier Tier { get; set; }
    public VipDurationType DurationType { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.Now;
    public DateTime? ExpiresAt { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    public int PaidGil { get; set; } = 0;

    [JsonIgnore]
    public bool IsExpired =>
        DurationType == VipDurationType.Monthly
        && ExpiresAt.HasValue
        && ExpiresAt.Value < DateTime.Now;

    [JsonIgnore]
    public int DaysRemaining =>
        ExpiresAt.HasValue
            ? Math.Max(0, (int)(ExpiresAt.Value - DateTime.Now).TotalDays)
            : -1; // -1 = permanent / no expiry
}
