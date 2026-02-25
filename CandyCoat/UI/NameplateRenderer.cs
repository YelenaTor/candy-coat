using System;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using CandyCoat.Data;

namespace CandyCoat.UI;

public class NameplateRenderer : IDisposable
{
    private readonly Plugin _plugin;

    public NameplateRenderer(Plugin plugin)
    {
        _plugin = plugin;
        Svc.PluginInterface.UiBuilder.Draw += DrawNameplates;
    }

    private void DrawNameplates()
    {
        if (!_plugin.Configuration.EnableSync || !_plugin.SyncService.IsConnected)
            return;

        var drawList = ImGui.GetBackgroundDrawList();

        foreach (var obj in Svc.Objects)
        {
            if (obj is not IPlayerCharacter pc) continue;
            
            // Reconstruct the hash
            var name = pc.Name.ToString();
            var world = pc.HomeWorld.Value.Name.ToString();
            var hash = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{name}@{world}"));

            // Check if this player has synced cosmetics
            if (!_plugin.SyncService.Cosmetics.TryGetValue(hash, out var profile))
                continue;

            // Optional: Also check if they are actually staff
            var staffMatch = _plugin.SyncService.OnlineStaff.Find(s => s.CharacterName == name && s.HomeWorld == world);
            bool isClockedIn = staffMatch?.ShiftStart != null;

            // Project 3D coordinate to 2D Screen
            var headPos = new Vector3(pc.Position.X, pc.Position.Y + 2.1f, pc.Position.Z);
            if (!Svc.GameGui.WorldToScreen(headPos, out var screenPos))
                continue;

            RenderCosmeticProfile(drawList, screenPos, pc, profile, isClockedIn);
        }
    }

    private void RenderCosmeticProfile(ImDrawListPtr drawList, Vector2 screenPos, IPlayerCharacter pc, CosmeticProfile profile, bool isClockedIn)
    {
        var text = $"« {pc.Name} »";
        var textSize = ImGui.CalcTextSize(text);
        var textPos = new Vector2(screenPos.X - textSize.X / 2, screenPos.Y - textSize.Y / 2);

        // Alpha / Greyscale Fade if not clocked in
        float alphaMult = 1.0f;
        if (profile.EnableClockInAlpha && !isClockedIn)
        {
            alphaMult = 0.3f;
        }

        // Base & Glow Colors
        var baseColorV = profile.BaseColor;
        var glowColorV = profile.GlowColor;

        // Auto SFW/NSFW Tinting (25 = Looking for Meld, 22 = Looking for Party)
        if (profile.EnableSfwNsfwTint && pc.OnlineStatus.RowId != 0)
        {
            uint statusId = pc.OnlineStatus.RowId;
            if (statusId == 25) // SFW - Meld
            {
                baseColorV = new Vector4(0.4f, 0.8f, 1f, baseColorV.W); // Light blue tint
            }
            else if (statusId == 22) // NSFW - Party
            {
                baseColorV = new Vector4(1f, 0.3f, 0.3f, baseColorV.W); // Red tint
            }
        }

        // Apply Opacity Fading
        baseColorV.W *= alphaMult;
        glowColorV.W *= alphaMult;
        
        uint baseColor = ImGui.GetColorU32(baseColorV);
        uint glowColor = ImGui.GetColorU32(glowColorV);

        // Pulsing Glow
        if (profile.EnableGlow)
        {
            float offset = 2f + (float)Math.Sin(ImGui.GetTime() * 3f) * 1f;
            drawList.AddText(new Vector2(textPos.X - offset, textPos.Y), glowColor, text);
            drawList.AddText(new Vector2(textPos.X + offset, textPos.Y), glowColor, text);
            drawList.AddText(new Vector2(textPos.X, textPos.Y - offset), glowColor, text);
            drawList.AddText(new Vector2(textPos.X, textPos.Y + offset), glowColor, text);
        }

        // Drop shadow
        if (profile.EnableDropShadow)
        {
            drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 2), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.8f * alphaMult)), text);
        }

        // Center Text
        drawList.AddText(textPos, baseColor, text);

        // Role Icon (Placeholder logic for drawing an icon next to name)
        if (profile.EnableRoleIcon)
        {
            // Calculate a position slightly to the left of the nameplate
            var iconPosMin = new Vector2(textPos.X - 20, textPos.Y);
            var iconPosMax = new Vector2(textPos.X - 4, textPos.Y + 16);
            
            // Given we don't have actual bundled assets loaded right now, we draw a mock marker icon
            drawList.AddRectFilled(iconPosMin, iconPosMax, ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 0.8f * alphaMult)), 4f);
            drawList.AddText(new Vector2(iconPosMin.X + 2, iconPosMin.Y + 1), ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.2f, 1f * alphaMult)), "★");
        }
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= DrawNameplates;
    }
}
