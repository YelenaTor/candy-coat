using System;
using System.Numerics;
using Newtonsoft.Json;

namespace CandyCoat.Data;

[Serializable]
public class CosmeticProfile
{
    // ─── Text ───
    public string FontName { get; set; } = "Default";
    public Vector4 BaseColor { get; set; } = new(1f, 1f, 1f, 1f);

    // ─── Glow ───
    public bool EnableGlow { get; set; } = true;
    public Vector4 GlowColor { get; set; } = new(1f, 0.6f, 0.8f, 0.5f);

    // ─── Gradient Text ───
    public bool EnableGradient { get; set; } = false;
    public Vector4 GradientColor1 { get; set; } = new(1f, 0.6f, 0.8f, 1f);
    public Vector4 GradientColor2 { get; set; } = new(0.6f, 0.8f, 1f, 1f);
    public GradientMode GradientMode { get; set; } = GradientMode.Static;
    public float GradientSpeed { get; set; } = 1f;

    // ─── Drop Shadow ───
    public bool EnableDropShadow { get; set; } = true;

    // ─── Outline ───
    public bool EnableOutline { get; set; } = false;
    public Vector4 OutlineColor { get; set; } = new(0f, 0f, 0f, 1f);
    public OutlineMode OutlineMode { get; set; } = OutlineMode.Hard;

    // ─── Background Pill ───
    public BackgroundStyle BackgroundStyle { get; set; } = BackgroundStyle.None;
    public Vector4 BackgroundColor1 { get; set; } = new(0.1f, 0.05f, 0.15f, 0.75f);
    public Vector4 BackgroundColor2 { get; set; } = new(0.25f, 0.1f, 0.3f, 0.75f);
    public float BackgroundPadding { get; set; } = 6f;

    // ─── Aura Ring ───
    public bool EnableAura { get; set; } = false;
    public Vector4 AuraColor { get; set; } = new(1f, 0.6f, 0.8f, 0.3f);
    public float AuraRadius { get; set; } = 40f;
    public float AuraThickness { get; set; } = 3f;

    // ─── Sparkles ───
    public bool EnableSparkles { get; set; } = false;
    public Vector4 SparkleColor { get; set; } = new(1f, 1f, 0.8f, 0.9f);
    public int SparkleCount { get; set; } = 8;
    public float SparkleSpeed { get; set; } = 1f;
    public float SparkleRadius { get; set; } = 30f;
    public SparkleStyle SparkleStyle { get; set; } = SparkleStyle.Orbital;

    // ─── Badges ───
    public bool EnableRoleIcon { get; set; } = true;
    public string RoleIconTemplate { get; set; } = "Heart";
    public string BadgeSlot2Template { get; set; } = "None";
    public BadgePosition BadgePosition { get; set; } = BadgePosition.Right;

    // ─── Adjustments ───
    public int FontSizeOverride { get; set; } = 30; // px
    public float OffsetX { get; set; } = 0f;        // screen-space pixel nudge
    public float OffsetY { get; set; } = 0f;

    // ─── Behavior ───
    public bool EnableSfwNsfwTint { get; set; } = true;
    public bool EnableClockInAlpha { get; set; } = true;

    [JsonIgnore]
    public bool IsDirty { get; set; } = false;
}
