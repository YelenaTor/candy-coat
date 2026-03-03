using System;
using System.Collections.Generic;

namespace CandyCoat.Data;

public class VipPackageDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public VipTier Tier { get; set; } = VipTier.Bronze;
    public VipDurationType DurationType { get; set; } = VipDurationType.Monthly;
    public int PriceGil { get; set; } = 0;
    public List<string> Perks { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
