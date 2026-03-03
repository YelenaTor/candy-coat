using System.Numerics;
using CandyCoat.Data;

namespace CandyCoat.UI;

public static class VipColours
{
    public static Vector4 GetTierColour(VipTier tier) => tier switch
    {
        VipTier.Bronze   => new Vector4(0.80f, 0.50f, 0.20f, 1f),
        VipTier.Silver   => new Vector4(0.75f, 0.75f, 0.80f, 1f),
        VipTier.Gold     => new Vector4(1.00f, 0.82f, 0.15f, 1f),
        VipTier.Platinum => new Vector4(0.80f, 0.60f, 1.00f, 1f),
        _                => Vector4.One,
    };
}
