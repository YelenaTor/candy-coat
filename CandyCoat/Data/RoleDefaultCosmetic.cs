using System;
using System.Numerics;

namespace CandyCoat.Data;

/// <summary>
/// Per-role cosmetic defaults set by the Owner.
/// When a staff member has no personal cosmetic profile, these defaults are used.
/// They also blend onto existing profiles (badge + glow) when the profile has none configured.
/// </summary>
[Serializable]
public class RoleDefaultCosmetic
{
    public bool Enabled { get; set; } = false;
    public string BadgeTemplate { get; set; } = "Heart";
    public Vector4 GlowColor { get; set; } = new(1f, 0.6f, 0.8f, 0.5f);
}
