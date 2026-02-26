using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;

namespace CandyCoat.UI;

/// <summary>
/// Shared nameplate rendering logic used by both NameplateRenderer (world overlay)
/// and CosmeticDrawerTab (live preview panel).
/// </summary>
public static class CosmeticRenderer
{
    /// <summary>Badge template options for UI combos.</summary>
    public static readonly string[] BadgeTemplates =
        { "None", "Heart", "Star", "Crown", "Moon", "Diamond" };

    /// <summary>
    /// Renders a full cosmetic nameplate at <paramref name="center"/>.
    /// Pushes a custom font if <paramref name="fontManager"/> has one loaded for the profile.
    /// <paramref name="characterSeed"/> drives deterministic sparkle positions (use name hash).
    /// </summary>
    public static void Render(
        ImDrawListPtr drawList,
        CosmeticProfile profile,
        string text,
        Vector2 center,
        float alphaMult,
        CosmeticFontManager? fontManager = null,
        CosmeticBadgeManager? badgeManager = null,
        int characterSeed = 0)
    {
        IDisposable? fontScope = null;
        fontManager?.TryPushFont(profile.FontName, out fontScope);

        // Only honour the size override when a custom font was actually pushed.
        // Scaling the default ImGui font (rasterised at ~13 px) up to 20 px via
        // the explicit AddText(font, size, …) overload produces a blurry result.
        float fontSize = fontScope != null && profile.FontSizeOverride > 0
            ? profile.FontSizeOverride
            : ImGui.GetFontSize();

        try
        {
            RenderCore(drawList, profile, text, center, alphaMult, badgeManager, characterSeed, fontSize);
        }
        finally
        {
            fontScope?.Dispose();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Core (called with font already pushed)
    // ────────────────────────────────────────────────────────────────────────

    private static void RenderCore(
        ImDrawListPtr drawList,
        CosmeticProfile profile,
        string text,
        Vector2 center,
        float alphaMult,
        CosmeticBadgeManager? badgeManager,
        int characterSeed,
        float fontSize)
    {
        var textSize = MeasureText(text, fontSize);
        var textPos  = new Vector2(center.X - textSize.X / 2f, center.Y - textSize.Y / 2f);

        var baseColor    = ApplyAlpha(profile.BaseColor,    alphaMult);
        var glowColor    = ApplyAlpha(profile.GlowColor,    alphaMult);
        var outlineColor = ApplyAlpha(profile.OutlineColor, alphaMult);

        // 1. Aura ring (behind everything)
        if (profile.EnableAura)
            DrawAura(drawList, center, profile, alphaMult);

        // 2. Background pill
        if (profile.BackgroundStyle != BackgroundStyle.None)
            DrawBackground(drawList, textPos, textSize, profile, alphaMult);

        // 3. Outline (under glow + main text)
        if (profile.EnableOutline)
            DrawOutline(drawList, textPos, text, outlineColor, profile.OutlineMode, fontSize);

        // 4. Pulsing glow
        if (profile.EnableGlow)
            DrawGlow(drawList, textPos, text, glowColor, fontSize);

        // 5. Drop shadow
        if (profile.EnableDropShadow)
            AddText(drawList, fontSize,
                new Vector2(textPos.X + 1, textPos.Y + 2),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f * alphaMult)),
                text);

        // 6. Main text — gradient or solid
        if (profile.EnableGradient)
            DrawGradientText(drawList, textPos, text, profile, alphaMult, fontSize);
        else
            AddText(drawList, fontSize, textPos, ImGui.GetColorU32(baseColor), text);

        // 7. Sparkle particles
        if (profile.EnableSparkles)
            DrawSparkles(drawList, center, textSize, profile, alphaMult, characterSeed);

        // 8. Role icon badge (always Left)
        if (profile.EnableRoleIcon)
            DrawBadge(drawList, textPos, textSize, profile.RoleIconTemplate, BadgePosition.Left, alphaMult, badgeManager);

        // 9. Badge slot 2
        if (profile.BadgeSlot2Template != "None")
            DrawBadge(drawList, textPos, textSize, profile.BadgeSlot2Template, profile.BadgePosition, alphaMult, badgeManager);
    }

    // ─── Aura ───────────────────────────────────────────────────────────────

    private static void DrawAura(ImDrawListPtr drawList, Vector2 center, CosmeticProfile profile, float alphaMult)
    {
        float t      = (float)ImGui.GetTime();
        float pulse  = MathF.Sin(t * 2f) * 4f;
        float pulse2 = MathF.Sin(t * 2f + MathF.PI) * 4f;

        var col  = ApplyAlpha(profile.AuraColor, alphaMult);
        var col2 = ApplyAlpha(new Vector4(
            profile.AuraColor.X, profile.AuraColor.Y, profile.AuraColor.Z,
            profile.AuraColor.W * 0.4f), alphaMult);

        drawList.AddCircle(center, profile.AuraRadius + pulse,  ImGui.GetColorU32(col),  64, profile.AuraThickness);
        drawList.AddCircle(center, profile.AuraRadius * 1.25f + pulse2, ImGui.GetColorU32(col2), 64, profile.AuraThickness * 0.6f);
    }

    // ─── Background ─────────────────────────────────────────────────────────

    private static void DrawBackground(
        ImDrawListPtr drawList,
        Vector2 textPos, Vector2 textSize,
        CosmeticProfile profile, float alphaMult)
    {
        float pad = profile.BackgroundPadding;
        var min = new Vector2(textPos.X - pad,              textPos.Y - pad);
        var max = new Vector2(textPos.X + textSize.X + pad, textPos.Y + textSize.Y + pad);

        switch (profile.BackgroundStyle)
        {
            case BackgroundStyle.Solid:
                drawList.AddRectFilled(min, max,
                    ImGui.GetColorU32(ApplyAlpha(profile.BackgroundColor1, alphaMult)), 6f);
                break;

            case BackgroundStyle.Gradient:
            {
                uint c1 = ImGui.GetColorU32(ApplyAlpha(profile.BackgroundColor1, alphaMult));
                uint c2 = ImGui.GetColorU32(ApplyAlpha(profile.BackgroundColor2, alphaMult));
                drawList.AddRectFilledMultiColor(min, max, c1, c2, c2, c1);
                break;
            }

            case BackgroundStyle.Shimmer:
            {
                float s   = (MathF.Sin((float)ImGui.GetTime() * 1.5f) + 1f) * 0.5f;
                uint left  = ImGui.GetColorU32(ApplyAlpha(LerpColor(profile.BackgroundColor1, profile.BackgroundColor2, s),       alphaMult));
                uint right = ImGui.GetColorU32(ApplyAlpha(LerpColor(profile.BackgroundColor1, profile.BackgroundColor2, 1f - s),  alphaMult));
                drawList.AddRectFilledMultiColor(min, max, left, right, right, left);
                break;
            }
        }
    }

    // ─── Glow ───────────────────────────────────────────────────────────────

    private static void DrawGlow(ImDrawListPtr drawList, Vector2 textPos, string text, Vector4 glowColor, float fontSize)
    {
        float offset = 2f + MathF.Sin((float)ImGui.GetTime() * 3f) * 1f;
        uint col = ImGui.GetColorU32(glowColor);
        AddText(drawList, fontSize, new Vector2(textPos.X - offset, textPos.Y), col, text);
        AddText(drawList, fontSize, new Vector2(textPos.X + offset, textPos.Y), col, text);
        AddText(drawList, fontSize, new Vector2(textPos.X,          textPos.Y - offset), col, text);
        AddText(drawList, fontSize, new Vector2(textPos.X,          textPos.Y + offset), col, text);
    }

    // ─── Outline ────────────────────────────────────────────────────────────

    private static void DrawOutline(
        ImDrawListPtr drawList, Vector2 textPos,
        string text, Vector4 outlineColor, OutlineMode mode, float fontSize)
    {
        uint col = ImGui.GetColorU32(outlineColor);

        if (mode == OutlineMode.Hard)
        {
            // 8-directional 1px offset
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                AddText(drawList, fontSize, new Vector2(textPos.X + dx, textPos.Y + dy), col, text);
            }
        }
        else // Soft
        {
            const float o = 1.5f;
            AddText(drawList, fontSize, new Vector2(textPos.X - o, textPos.Y), col, text);
            AddText(drawList, fontSize, new Vector2(textPos.X + o, textPos.Y), col, text);
            AddText(drawList, fontSize, new Vector2(textPos.X, textPos.Y - o), col, text);
            AddText(drawList, fontSize, new Vector2(textPos.X, textPos.Y + o), col, text);
        }
    }

    // ─── Gradient text ──────────────────────────────────────────────────────

    private static void DrawGradientText(
        ImDrawListPtr drawList, Vector2 textPos,
        string text, CosmeticProfile profile, float alphaMult, float fontSize)
    {
        float t     = (float)ImGui.GetTime() * profile.GradientSpeed;
        float charX = textPos.X;
        int   len   = text.Length;

        for (int i = 0; i < len; i++)
        {
            var   ch   = text[i].ToString();
            float frac = len > 1 ? (float)i / (len - 1) : 0f;

            Vector4 col = profile.GradientMode switch
            {
                GradientMode.Rainbow =>
                    HsvToRgb(((frac + t * 0.15f) % 1f + 1f) % 1f, 0.9f, 1f, profile.BaseColor.W),
                GradientMode.Sweep =>
                    LerpColor(profile.GradientColor1, profile.GradientColor2,
                        ((frac + t * 0.15f) % 1f + 1f) % 1f),
                _ => // Static
                    LerpColor(profile.GradientColor1, profile.GradientColor2, frac),
            };

            AddText(drawList, fontSize, new Vector2(charX, textPos.Y),
                ImGui.GetColorU32(ApplyAlpha(col, alphaMult)), ch);
            charX += MeasureText(ch, fontSize).X;
        }
    }

    // ─── Sparkles ───────────────────────────────────────────────────────────

    private static void DrawSparkles(
        ImDrawListPtr drawList, Vector2 center, Vector2 textSize,
        CosmeticProfile profile, float alphaMult, int seed)
    {
        float t      = (float)ImGui.GetTime();
        int   count  = Math.Clamp(profile.SparkleCount, 1, 32);
        float spread = profile.SparkleRadius;

        for (int i = 0; i < count; i++)
        {
            float phase     = DeterministicFloat(seed, i, 1234567) * MathF.PI * 2f;
            float speedMult = 0.7f + DeterministicFloat(seed, i, 7654321) * 0.6f;
            float baseAngle = (float)i / count * MathF.PI * 2f;
            float size      = 1.5f + DeterministicFloat(seed, i, 3141592) * 2f;
            float xFrac     = DeterministicFloat(seed, i, 9876543) - 0.5f;

            float alpha  = MathF.Sin(t * speedMult * profile.SparkleSpeed + phase) * 0.5f + 0.5f;
            uint  col    = ImGui.GetColorU32(ApplyAlpha(
                new Vector4(profile.SparkleColor.X, profile.SparkleColor.Y,
                            profile.SparkleColor.Z, profile.SparkleColor.W * alpha),
                alphaMult));

            Vector2 pos = profile.SparkleStyle switch
            {
                SparkleStyle.Orbital => new Vector2(
                    center.X + MathF.Cos(baseAngle + t * 0.5f * profile.SparkleSpeed) * spread,
                    center.Y + MathF.Sin(baseAngle + t * 0.5f * profile.SparkleSpeed) * spread * 0.35f),

                SparkleStyle.Rising => new Vector2(
                    center.X + xFrac * textSize.X * 1.5f,
                    center.Y - ((t * 20f * profile.SparkleSpeed + phase * 10f) % (spread * 2f))),

                SparkleStyle.Burst => new Vector2(
                    center.X + MathF.Cos(baseAngle) * (spread * alpha),
                    center.Y + MathF.Sin(baseAngle) * (spread * alpha * 0.4f)),

                _ => center,
            };

            drawList.AddCircleFilled(pos, size, col);
        }
    }

    // ─── Badge ──────────────────────────────────────────────────────────────

    private static void DrawBadge(
        ImDrawListPtr drawList,
        Vector2 textPos, Vector2 textSize,
        string template, BadgePosition position, float alphaMult,
        CosmeticBadgeManager? badgeManager)
    {
        if (template == "None") return;

        const float iconSize = CosmeticBadgeManager.BadgeRenderSize;
        const float gap      = 4f;

        Vector2 iconMin = position switch
        {
            BadgePosition.Above => new Vector2(
                textPos.X + textSize.X / 2f - iconSize / 2f,
                textPos.Y - iconSize - gap),
            BadgePosition.Right => new Vector2(
                textPos.X + textSize.X + gap,
                textPos.Y + (textSize.Y - iconSize) / 2f),
            _ => new Vector2( // Left
                textPos.X - iconSize - gap,
                textPos.Y + (textSize.Y - iconSize) / 2f),
        };
        var iconMax = new Vector2(iconMin.X + iconSize, iconMin.Y + iconSize);

        // PNG path — tint carries alpha so transparency is preserved
        var wrap = badgeManager?.TryGetWrap(template);
        if (wrap != null)
        {
            uint tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alphaMult));
            drawList.AddImage(wrap.Handle, iconMin, iconMax,
                new Vector2(0, 0), new Vector2(1, 1), tint);
            return;
        }

        // Glyph fallback when no PNG is present
        drawList.AddRectFilled(iconMin, iconMax,
            ImGui.GetColorU32(new Vector4(0.12f, 0.08f, 0.18f, 0.85f * alphaMult)), 3f);

        (string glyph, Vector4 color) = template switch
        {
            "Heart"   => ("♥", new Vector4(1f,   0.35f, 0.55f, alphaMult)),
            "Star"    => ("★", new Vector4(1f,   0.9f,  0.2f,  alphaMult)),
            "Crown"   => ("♛", new Vector4(1f,   0.8f,  0.1f,  alphaMult)),
            "Moon"    => ("☽", new Vector4(0.7f, 0.85f, 1f,    alphaMult)),
            "Diamond" => ("◆", new Vector4(0.4f, 0.85f, 1f,    alphaMult)),
            _         => ("•", new Vector4(1f,   1f,    1f,    alphaMult)),
        };

        var gs = ImGui.CalcTextSize(glyph);
        drawList.AddText(
            new Vector2(iconMin.X + (iconSize - gs.X) / 2f, iconMin.Y + (iconSize - gs.Y) / 2f),
            ImGui.GetColorU32(color), glyph);
    }

    // ─── Utilities ──────────────────────────────────────────────────────────

    /// <summary>Renders text at an explicit font size using the currently-pushed font.</summary>
    private static void AddText(ImDrawListPtr dl, float size, Vector2 pos, uint col, string text) =>
        dl.AddText(ImGui.GetFont(), size, pos, col, text);

    /// <summary>Measures text, scaling proportionally when size differs from the active font size.</summary>
    private static Vector2 MeasureText(string text, float size)
    {
        var raw = ImGui.CalcTextSize(text);
        float defaultSize = ImGui.GetFontSize();
        return defaultSize > 0f ? raw * (size / defaultSize) : raw;
    }

    private static Vector4 ApplyAlpha(Vector4 c, float a) =>
        new(c.X, c.Y, c.Z, c.W * a);

    public static Vector4 LerpColor(Vector4 a, Vector4 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Vector4(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t);
    }

    private static Vector4 HsvToRgb(float h, float s, float v, float a)
    {
        float h6 = h * 6f;
        int   i  = (int)h6;
        float f  = h6 - i;
        float p  = v * (1f - s);
        float q  = v * (1f - s * f);
        float tv = v * (1f - s * (1f - f));
        return i switch
        {
            0 => new Vector4(v,  tv,  p,  a),
            1 => new Vector4(q,  v,   p,  a),
            2 => new Vector4(p,  v,   tv, a),
            3 => new Vector4(p,  q,   v,  a),
            4 => new Vector4(tv, p,   v,  a),
            _ => new Vector4(v,  p,   q,  a),
        };
    }

    private static float DeterministicFloat(int seed, int index, int prime)
    {
        int h = Math.Abs(seed * prime + index * 1234567);
        return (h % 10000) / 10000f;
    }
}
