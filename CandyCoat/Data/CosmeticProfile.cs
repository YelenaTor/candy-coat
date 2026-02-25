using System;
using System.Numerics;
using Newtonsoft.Json;

namespace CandyCoat.Data;

[Serializable]
public class CosmeticProfile
{
    public string FontName { get; set; } = "Default";
    public Vector4 BaseColor { get; set; } = new Vector4(1f, 1f, 1f, 1f);
    public Vector4 GlowColor { get; set; } = new Vector4(1f, 0.6f, 0.8f, 0.5f);
    
    public bool EnableGlow { get; set; } = true;
    public bool EnableDropShadow { get; set; } = true;
    public bool EnableRoleIcon { get; set; } = true;
    public string RoleIconTemplate { get; set; } = "Heart";
    public string BackgroundTexture { get; set; } = "None";
    
    public bool EnableSfwNsfwTint { get; set; } = true;
    public bool EnableClockInAlpha { get; set; } = true;
    
    [JsonIgnore]
    public bool IsDirty { get; set; } = false;
}
