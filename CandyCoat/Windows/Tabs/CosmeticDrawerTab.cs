using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;

namespace CandyCoat.Windows.Tabs;

public class CosmeticDrawerTab
{
    private readonly Plugin plugin;
    private DateTime lastEditTime = DateTime.MinValue;

    public CosmeticDrawerTab(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void DrawContent()
    {
        var profile = plugin.Configuration.CosmeticProfile;
        
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), "Custom Nameplates & Overlays");
        ImGui.Spacing();
        ImGui.TextWrapped("Customize how your nameplate appears to other Sugar staff. These settings are instantly synced to all connected staff members.");
        ImGui.Spacing();
        ImGui.Separator();
        
        // ── Preview Panel ──
        ImGui.Spacing();
        ImGui.TextDisabled("Live Preview:");
        
        var drawList = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        var regionAvail = ImGui.GetContentRegionAvail();
        
        // Draw a dark mock background for the preview
        var previewRectMin = cursorPos;
        var previewRectMax = new Vector2(cursorPos.X + regionAvail.X, cursorPos.Y + 100);
        drawList.AddRectFilled(previewRectMin, previewRectMax, ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.08f, 1f)), 8f);
        
        ImGui.Dummy(new Vector2(regionAvail.X, 100)); // Claim the space
        
        // Mock Nameplate Logic
        var mockCenter = new Vector2(previewRectMin.X + regionAvail.X / 2, previewRectMin.Y + 50);
        var mockText = $"« {plugin.Configuration.CharacterName ?? "Your Name"} »";
        var textSize = ImGui.CalcTextSize(mockText);
        var textPos = new Vector2(mockCenter.X - textSize.X / 2, mockCenter.Y - textSize.Y / 2);
        
        // Draw Glow (Multiple offset renders)
        if (profile.EnableGlow)
        {
            var glowColor = ImGui.GetColorU32(profile.GlowColor);
            float offset = 2f + (float)Math.Sin(ImGui.GetTime() * 3f) * 1f; // Pulsing effect
            drawList.AddText(new Vector2(textPos.X - offset, textPos.Y), glowColor, mockText);
            drawList.AddText(new Vector2(textPos.X + offset, textPos.Y), glowColor, mockText);
            drawList.AddText(new Vector2(textPos.X, textPos.Y - offset), glowColor, mockText);
            drawList.AddText(new Vector2(textPos.X, textPos.Y + offset), glowColor, mockText);
        }
        
        // Draw Drop Shadow
        if (profile.EnableDropShadow)
        {
            drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 2), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f)), mockText);
        }
        
        // Base Text
        drawList.AddText(textPos, ImGui.GetColorU32(profile.BaseColor), mockText);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Controls ──
        bool changed = false;

        float columnWidth = regionAvail.X / 2 - 10;
        ImGui.BeginChild("CosmeticControls", new Vector2(0, 0), false);
        
        ImGui.Columns(2, "CosmeticColumns", false);
        ImGui.SetColumnWidth(0, columnWidth);
        
        // Left Column: Toggles
        ImGui.Text("Features");
        ImGui.Spacing();
        
        var enableGlow = profile.EnableGlow;
        if (ImGui.Checkbox("Enable Pulsing Glow", ref enableGlow)) { profile.EnableGlow = enableGlow; changed = true; }
        
        var enableShadow = profile.EnableDropShadow;
        if (ImGui.Checkbox("Enable Drop Shadow", ref enableShadow)) { profile.EnableDropShadow = enableShadow; changed = true; }
        
        var enableSfwNsfw = profile.EnableSfwNsfwTint;
        if (ImGui.Checkbox("Auto SFW/NSFW Tinting", ref enableSfwNsfw)) { profile.EnableSfwNsfwTint = enableSfwNsfw; changed = true; }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Tints nameplate blue if LFM (Looking for Meld), or red if LFP (Looking for Party).");
        
        var enableClock = profile.EnableClockInAlpha;
        if (ImGui.Checkbox("Clock-In Opacity Fade", ref enableClock)) { profile.EnableClockInAlpha = enableClock; changed = true; }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fades opacity to 30% when you are not officially clocked into a shift.");
        
        var enableIcons = profile.EnableRoleIcon;
        if (ImGui.Checkbox("Draw Role Icon", ref enableIcons)) { profile.EnableRoleIcon = enableIcons; changed = true; }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Draws a high-res class icon next to your name based on your active role.");
        
        ImGui.NextColumn();
        
        // Right Column: Colors
        ImGui.Text("Colors");
        ImGui.Spacing();
        
        var baseColor = profile.BaseColor;
        if (ImGui.ColorEdit4("Base Text Color", ref baseColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            profile.BaseColor = baseColor;
            changed = true;
        }
        
        if (profile.EnableGlow)
        {
            var glowColor = profile.GlowColor;
            if (ImGui.ColorEdit4("Glow Color", ref glowColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            {
                profile.GlowColor = glowColor;
                changed = true;
            }
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Asset Selectors");
        ImGui.Spacing();
        
        var iconTemplates = new[] { "Heart", "Star", "Crown" };
        int currentIconIndex = Array.IndexOf(iconTemplates, profile.RoleIconTemplate);
        if (currentIconIndex == -1) currentIconIndex = 0;
        
        if (ImGui.Combo("Role Icon", ref currentIconIndex, iconTemplates, iconTemplates.Length))
        {
            profile.RoleIconTemplate = iconTemplates[currentIconIndex];
            changed = true;
        }
        
        var bgTemplates = new[] { "None", "Pastel Gradient" };
        int currentBgIndex = Array.IndexOf(bgTemplates, profile.BackgroundTexture);
        if (currentBgIndex == -1) currentBgIndex = 0;
        
        if (ImGui.Combo("Background", ref currentBgIndex, bgTemplates, bgTemplates.Length))
        {
            profile.BackgroundTexture = bgTemplates[currentBgIndex];
            changed = true;
        }

        ImGui.Columns(1);
        ImGui.EndChild();

        // ── Debounce Save Logic ──
        if (changed)
        {
            profile.IsDirty = true;
            lastEditTime = DateTime.UtcNow;
            plugin.Configuration.Save(); // Save to local disk immediately for safety
        }

        // If 1.5 seconds have passed since the last edit and it's dirty, compress and push to Sync API
        if (profile.IsDirty && (DateTime.UtcNow - lastEditTime).TotalSeconds > 1.5)
        {
            profile.IsDirty = false;
            _ = plugin.SyncService.PushCosmeticsAsync(profile);
        }
    }
}
