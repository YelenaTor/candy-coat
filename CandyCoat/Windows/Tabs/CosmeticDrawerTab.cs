using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.UI;

namespace CandyCoat.Windows.Tabs;

public class CosmeticDrawerTab
{
    private readonly Plugin _plugin;
    private readonly CosmeticFontManager _fontManager;
    private readonly CosmeticBadgeManager _badgeManager;
    private DateTime _lastEditTime = DateTime.MinValue;

    private static readonly string[] GradientModeNames  = { "Static", "Sweep", "Rainbow" };
    private static readonly string[] SparkleStyleNames  = { "Orbital", "Rising", "Burst" };
    private static readonly string[] BackgroundStyleNames = { "None", "Solid", "Gradient", "Shimmer" };
    private static readonly string[] OutlineModeNames   = { "Hard", "Soft" };
    private static readonly string[] BadgePositionNames = { "Left", "Right", "Above" };

    public CosmeticDrawerTab(Plugin plugin, CosmeticFontManager fontManager, CosmeticBadgeManager badgeManager)
    {
        _plugin = plugin;
        _fontManager = fontManager;
        _badgeManager = badgeManager;
    }

    public void DrawContent()
    {
        var profile = _plugin.Configuration.CosmeticProfile;

        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Custom Nameplates & Overlays");
        ImGui.Spacing();
        ImGui.TextWrapped("Customize how your nameplate appears to other Sugar staff. Changes sync automatically.");
        ImGui.Spacing();
        ImGui.Separator();

        // ── Live Preview ──────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextDisabled("Live Preview");

        var drawList    = ImGui.GetWindowDrawList();
        var cursorPos   = ImGui.GetCursorScreenPos();
        var regionAvail = ImGui.GetContentRegionAvail();

        const float previewHeight = 120f;
        var previewMin = cursorPos;
        var previewMax = new Vector2(cursorPos.X + regionAvail.X, cursorPos.Y + previewHeight);
        drawList.AddRectFilled(previewMin, previewMax,
            ImGui.GetColorU32(new Vector4(0.05f, 0.04f, 0.08f, 1f)), 8f);

        ImGui.Dummy(new Vector2(regionAvail.X, previewHeight));

        var previewCenter = new Vector2(previewMin.X + regionAvail.X / 2f, previewMin.Y + previewHeight / 2f);
        var previewText   = $"« {(_plugin.Configuration.CharacterName is { Length: > 0 } n ? n : "Your Name")} »";
        int previewSeed   = _plugin.Configuration.CharacterName?.GetHashCode() ?? 0;

        CosmeticRenderer.Render(drawList, profile, previewText, previewCenter, 1f, _fontManager, _badgeManager, previewSeed);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        bool changed = false;

        using var child = ImRaii.Child("CosmeticControls", new Vector2(0, 0), false);

        // ── Text & Colors ─────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Text & Colors", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            ImGui.Spacing();

            // Font
            var availFonts = _fontManager.AvailableFonts;
            int fontIdx = Array.IndexOf(availFonts, profile.FontName);
            if (fontIdx < 0) fontIdx = 0;
            ImGui.SetNextItemWidth(160);
            if (ImGui.Combo("Font##font", ref fontIdx, availFonts, availFonts.Length))
            { profile.FontName = availFonts[fontIdx]; changed = true; }
            if (ImGui.IsItemHovered())
            {
                var dir = _fontManager.FontDirectory;
                var exists = System.IO.Directory.Exists(dir);
                var count = _fontManager.AvailableFonts.Length - 1; // exclude "Default"
                ImGui.SetTooltip($"Fonts dir: {dir}\nExists: {exists}  |  Loaded: {count} font(s)\nDrop .ttf/.otf files there to add fonts.");
            }

            ImGui.Spacing();

            // Base color
            var baseColor = profile.BaseColor;
            if (ImGui.ColorEdit4("Base Color##base", ref baseColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            { profile.BaseColor = baseColor; changed = true; }

            ImGui.Spacing();

            // Glow
            var enableGlow = profile.EnableGlow;
            if (ImGui.Checkbox("Pulsing Glow##glow", ref enableGlow)) { profile.EnableGlow = enableGlow; changed = true; }
            if (profile.EnableGlow)
            {
                ImGui.SameLine();
                var glowColor = profile.GlowColor;
                if (ImGui.ColorEdit4("##glowcol", ref glowColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                { profile.GlowColor = glowColor; changed = true; }
            }

            ImGui.Spacing();

            // Gradient
            var enableGrad = profile.EnableGradient;
            if (ImGui.Checkbox("Gradient Text##grad", ref enableGrad)) { profile.EnableGradient = enableGrad; changed = true; }
            if (profile.EnableGradient)
            {
                ImGui.Indent();
                int gradMode = (int)profile.GradientMode;
                ImGui.SetNextItemWidth(120);
                if (ImGui.Combo("Mode##gradmode", ref gradMode, GradientModeNames, GradientModeNames.Length))
                { profile.GradientMode = (GradientMode)gradMode; changed = true; }

                if (profile.GradientMode != GradientMode.Rainbow)
                {
                    ImGui.SameLine();
                    var gc1 = profile.GradientColor1;
                    if (ImGui.ColorEdit4("##gc1", ref gc1, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                    { profile.GradientColor1 = gc1; changed = true; }
                    ImGui.SameLine();
                    var gc2 = profile.GradientColor2;
                    if (ImGui.ColorEdit4("##gc2", ref gc2, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                    { profile.GradientColor2 = gc2; changed = true; }
                }

                if (profile.GradientMode != GradientMode.Static)
                {
                    var gspeed = profile.GradientSpeed;
                    ImGui.SetNextItemWidth(150);
                    if (ImGui.SliderFloat("Speed##gspeed", ref gspeed, 0.1f, 5f))
                    { profile.GradientSpeed = gspeed; changed = true; }
                }
                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Unindent();
        }

        // ── Effects ───────────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Effects", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            ImGui.Spacing();

            // Drop Shadow
            var enableShadow = profile.EnableDropShadow;
            if (ImGui.Checkbox("Drop Shadow##shadow", ref enableShadow)) { profile.EnableDropShadow = enableShadow; changed = true; }

            ImGui.Spacing();

            // Outline
            var enableOutline = profile.EnableOutline;
            if (ImGui.Checkbox("Outline##outline", ref enableOutline)) { profile.EnableOutline = enableOutline; changed = true; }
            if (profile.EnableOutline)
            {
                ImGui.Indent();
                int outlineMode = (int)profile.OutlineMode;
                ImGui.SetNextItemWidth(100);
                if (ImGui.Combo("Mode##outlinemode", ref outlineMode, OutlineModeNames, OutlineModeNames.Length))
                { profile.OutlineMode = (OutlineMode)outlineMode; changed = true; }
                ImGui.SameLine();
                var outlineColor = profile.OutlineColor;
                if (ImGui.ColorEdit4("##outlinecol", ref outlineColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                { profile.OutlineColor = outlineColor; changed = true; }
                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Aura
            var enableAura = profile.EnableAura;
            if (ImGui.Checkbox("Aura Ring##aura", ref enableAura)) { profile.EnableAura = enableAura; changed = true; }
            if (profile.EnableAura)
            {
                ImGui.Indent();
                var auraColor = profile.AuraColor;
                if (ImGui.ColorEdit4("Color##auracol", ref auraColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                { profile.AuraColor = auraColor; changed = true; }

                var auraRadius = profile.AuraRadius;
                ImGui.SetNextItemWidth(150);
                if (ImGui.SliderFloat("Radius##aurarad", ref auraRadius, 10f, 100f))
                { profile.AuraRadius = auraRadius; changed = true; }

                var auraThick = profile.AuraThickness;
                ImGui.SetNextItemWidth(150);
                if (ImGui.SliderFloat("Thickness##aurathick", ref auraThick, 0.5f, 8f))
                { profile.AuraThickness = auraThick; changed = true; }
                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Sparkles
            var enableSparkles = profile.EnableSparkles;
            if (ImGui.Checkbox("Sparkles##sparkles", ref enableSparkles)) { profile.EnableSparkles = enableSparkles; changed = true; }
            if (profile.EnableSparkles)
            {
                ImGui.Indent();
                int sparkleStyle = (int)profile.SparkleStyle;
                ImGui.SetNextItemWidth(110);
                if (ImGui.Combo("Style##sparklestyle", ref sparkleStyle, SparkleStyleNames, SparkleStyleNames.Length))
                { profile.SparkleStyle = (SparkleStyle)sparkleStyle; changed = true; }
                ImGui.SameLine();
                var sparkleColor = profile.SparkleColor;
                if (ImGui.ColorEdit4("##sparklecol", ref sparkleColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                { profile.SparkleColor = sparkleColor; changed = true; }

                var sparkleCount = profile.SparkleCount;
                ImGui.SetNextItemWidth(150);
                if (ImGui.SliderInt("Count##sparklecount", ref sparkleCount, 1, 32))
                { profile.SparkleCount = sparkleCount; changed = true; }

                var sparkleSpeed = profile.SparkleSpeed;
                ImGui.SetNextItemWidth(150);
                if (ImGui.SliderFloat("Speed##sparklespeed", ref sparkleSpeed, 0.1f, 5f))
                { profile.SparkleSpeed = sparkleSpeed; changed = true; }

                var sparkleRadius = profile.SparkleRadius;
                ImGui.SetNextItemWidth(150);
                if (ImGui.SliderFloat("Radius##sparkleradius", ref sparkleRadius, 5f, 80f))
                { profile.SparkleRadius = sparkleRadius; changed = true; }
                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Unindent();
        }

        // ── Background ────────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Background", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            ImGui.Spacing();

            int bgStyle = (int)profile.BackgroundStyle;
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("Style##bgstyle", ref bgStyle, BackgroundStyleNames, BackgroundStyleNames.Length))
            { profile.BackgroundStyle = (BackgroundStyle)bgStyle; changed = true; }

            if (profile.BackgroundStyle != BackgroundStyle.None)
            {
                var bc1 = profile.BackgroundColor1;
                if (ImGui.ColorEdit4("Color 1##bc1", ref bc1, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                { profile.BackgroundColor1 = bc1; changed = true; }

                if (profile.BackgroundStyle is BackgroundStyle.Gradient or BackgroundStyle.Shimmer)
                {
                    var bc2 = profile.BackgroundColor2;
                    if (ImGui.ColorEdit4("Color 2##bc2", ref bc2, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                    { profile.BackgroundColor2 = bc2; changed = true; }
                }

                var bgPad = profile.BackgroundPadding;
                ImGui.SetNextItemWidth(150);
                if (ImGui.SliderFloat("Padding##bgpad", ref bgPad, 0f, 20f))
                { profile.BackgroundPadding = bgPad; changed = true; }
            }

            ImGui.Spacing();
            ImGui.Unindent();
        }

        // ── Badges ────────────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Badges", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            ImGui.Spacing();

            // Role icon (slot 1, always Left)
            var enableIcon = profile.EnableRoleIcon;
            if (ImGui.Checkbox("Role Icon (Slot 1)##roleicon", ref enableIcon)) { profile.EnableRoleIcon = enableIcon; changed = true; }
            if (profile.EnableRoleIcon)
            {
                ImGui.Indent();
                int iconIdx = Array.IndexOf(CosmeticRenderer.BadgeTemplates, profile.RoleIconTemplate);
                if (iconIdx < 1) iconIdx = 1;
                ImGui.SetNextItemWidth(110);
                if (ImGui.Combo("Icon##roletemplate", ref iconIdx, CosmeticRenderer.BadgeTemplates, CosmeticRenderer.BadgeTemplates.Length))
                { profile.RoleIconTemplate = CosmeticRenderer.BadgeTemplates[iconIdx]; changed = true; }
                ImGui.SameLine();
                ImGui.TextDisabled("(always Left)");
                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Badge slot 2
            int badge2Idx = Array.IndexOf(CosmeticRenderer.BadgeTemplates, profile.BadgeSlot2Template);
            if (badge2Idx < 0) badge2Idx = 0;
            ImGui.SetNextItemWidth(110);
            if (ImGui.Combo("Badge Slot 2##badge2", ref badge2Idx, CosmeticRenderer.BadgeTemplates, CosmeticRenderer.BadgeTemplates.Length))
            { profile.BadgeSlot2Template = CosmeticRenderer.BadgeTemplates[badge2Idx]; changed = true; }

            if (profile.BadgeSlot2Template != "None")
            {
                ImGui.Indent();
                int badgePos = (int)profile.BadgePosition;
                ImGui.SetNextItemWidth(100);
                if (ImGui.Combo("Position##badgepos", ref badgePos, BadgePositionNames, BadgePositionNames.Length))
                { profile.BadgePosition = (BadgePosition)badgePos; changed = true; }
                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Unindent();
        }

        // ── Adjustments ───────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Adjustments"))
        {
            ImGui.Indent();
            ImGui.Spacing();

            var fontSz = profile.FontSizeOverride;
            ImGui.SetNextItemWidth(160);
            if (ImGui.SliderInt("Font Size##adjfontsize", ref fontSz, 8, 40))
            { profile.FontSizeOverride = fontSz; changed = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Overrides the rendered font size. Default is 20.");

            ImGui.Spacing();

            var offX = profile.OffsetX;
            ImGui.SetNextItemWidth(160);
            if (ImGui.SliderFloat("Offset X##adjoffx", ref offX, -200f, 200f, "%.0f px"))
            { profile.OffsetX = offX; changed = true; }

            var offY = profile.OffsetY;
            ImGui.SetNextItemWidth(160);
            if (ImGui.SliderFloat("Offset Y##adjoffy", ref offY, -200f, 200f, "%.0f px"))
            { profile.OffsetY = offY; changed = true; }

            ImGui.Spacing();
            ImGui.Unindent();
        }

        // ── Behavior ──────────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Behavior"))
        {
            ImGui.Indent();
            ImGui.Spacing();

            var enableSfwNsfw = profile.EnableSfwNsfwTint;
            if (ImGui.Checkbox("Auto SFW/NSFW Tinting##sfwnsfw", ref enableSfwNsfw)) { profile.EnableSfwNsfwTint = enableSfwNsfw; changed = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Blue tint when LFM (SFW). Red tint when LFP (NSFW).");

            var enableClock = profile.EnableClockInAlpha;
            if (ImGui.Checkbox("Clock-In Opacity Fade##clockalpha", ref enableClock)) { profile.EnableClockInAlpha = enableClock; changed = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fades nameplate to 30% when not clocked in.");

            ImGui.Spacing();
            ImGui.Unindent();
        }

        // ── Debounce save / sync push ─────────────────────────────────────────
        if (changed)
        {
            profile.IsDirty = true;
            _lastEditTime = DateTime.UtcNow;
            _plugin.Configuration.Save();
        }

        if (profile.IsDirty && (DateTime.UtcNow - _lastEditTime).TotalSeconds > 1.5)
        {
            profile.IsDirty = false;
            _ = _plugin.SyncService.PushCosmeticsAsync(profile);
        }
    }
}
