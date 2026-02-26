using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using ECommons.DalamudServices;
using CandyCoat.Data;

namespace CandyCoat.UI;

public class NameplateRenderer : IDisposable
{
    private readonly Plugin _plugin;
    private readonly CosmeticFontManager _fontManager;
    private readonly CosmeticBadgeManager _badgeManager;

    public NameplateRenderer(Plugin plugin, CosmeticFontManager fontManager, CosmeticBadgeManager badgeManager)
    {
        _plugin = plugin;
        _fontManager = fontManager;
        _badgeManager = badgeManager;
        Svc.PluginInterface.UiBuilder.Draw += DrawNameplates;
        Plugin.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
    }

    private void OnNamePlateUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!_plugin.Configuration.EnableSync) return;

        foreach (var handler in handlers)
        {
            var pc = handler.PlayerCharacter;
            if (pc == null) continue;

            var name  = pc.Name.ToString();
            var world = pc.HomeWorld.Value.Name.ToString();
            var hash  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{name}@{world}"));

            bool hasProfile =
                (_plugin.SyncService.IsConnected && _plugin.SyncService.Cosmetics.ContainsKey(hash)) ||
                (name == _plugin.Configuration.CharacterName && world == _plugin.Configuration.HomeWorld);

            if (!hasProfile) continue;

            handler.RemoveField(NamePlateStringField.Name);
            handler.RemoveField(NamePlateStringField.Title);
            handler.RemoveField(NamePlateStringField.FreeCompanyTag);
            handler.RemoveField(NamePlateStringField.StatusPrefix);
            handler.NameIconId   = 0;
            handler.MarkerIconId = 0;
        }
    }

    private void DrawNameplates()
    {
        if (!_plugin.Configuration.EnableSync)
            return;

        var drawList = ImGui.GetBackgroundDrawList();
        var localName  = _plugin.Configuration.CharacterName;
        var localWorld = _plugin.Configuration.HomeWorld;

        foreach (var obj in Svc.Objects)
        {
            if (obj is not IPlayerCharacter pc) continue;

            var name  = pc.Name.ToString();
            var world = pc.HomeWorld.Value.Name.ToString();
            var hash  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{name}@{world}"));

            // Synced profile takes priority when connected
            CosmeticProfile? profile = null;
            if (_plugin.SyncService.IsConnected)
                _plugin.SyncService.Cosmetics.TryGetValue(hash, out profile);

            // Local player falls back to their own configured profile
            if (profile == null && name == localName && world == localWorld)
                profile = _plugin.Configuration.CosmeticProfile;

            if (profile == null) continue;

            var staffMatch  = _plugin.SyncService.OnlineStaff.Find(s => s.CharacterName == name && s.HomeWorld == world);
            bool isClockedIn = staffMatch?.ShiftStart != null;

            var headPos = new Vector3(pc.Position.X, pc.Position.Y + 2.3f, pc.Position.Z);
            if (!Svc.GameGui.WorldToScreen(headPos, out var screenPos))
                continue;

            screenPos = new Vector2(screenPos.X + profile.OffsetX, screenPos.Y + profile.OffsetY);

            float alphaMult = profile.EnableClockInAlpha && !isClockedIn ? 0.3f : 1f;

            // SFW/NSFW tint: clone profile so the cached original is never mutated
            var renderProfile = profile;
            if (profile.EnableSfwNsfwTint && pc.OnlineStatus.RowId != 0)
            {
                renderProfile = ShallowClone(profile);
                renderProfile.BaseColor = pc.OnlineStatus.RowId switch
                {
                    25 => new Vector4(0.4f, 0.8f, 1f,   profile.BaseColor.W), // SFW — blue
                    22 => new Vector4(1f,   0.3f, 0.3f, profile.BaseColor.W), // NSFW — red
                    _  => profile.BaseColor,
                };
            }

            CosmeticRenderer.Render(
                drawList, renderProfile,
                $"« {name} »",
                screenPos,
                alphaMult,
                _fontManager,
                _badgeManager,
                name.GetHashCode());
        }
    }

    private static CosmeticProfile ShallowClone(CosmeticProfile p) => new()
    {
        FontName           = p.FontName,
        BaseColor          = p.BaseColor,
        EnableGlow         = p.EnableGlow,
        GlowColor          = p.GlowColor,
        EnableGradient     = p.EnableGradient,
        GradientColor1     = p.GradientColor1,
        GradientColor2     = p.GradientColor2,
        GradientMode       = p.GradientMode,
        GradientSpeed      = p.GradientSpeed,
        EnableDropShadow   = p.EnableDropShadow,
        EnableOutline      = p.EnableOutline,
        OutlineColor       = p.OutlineColor,
        OutlineMode        = p.OutlineMode,
        BackgroundStyle    = p.BackgroundStyle,
        BackgroundColor1   = p.BackgroundColor1,
        BackgroundColor2   = p.BackgroundColor2,
        BackgroundPadding  = p.BackgroundPadding,
        EnableAura         = p.EnableAura,
        AuraColor          = p.AuraColor,
        AuraRadius         = p.AuraRadius,
        AuraThickness      = p.AuraThickness,
        EnableSparkles     = p.EnableSparkles,
        SparkleColor       = p.SparkleColor,
        SparkleCount       = p.SparkleCount,
        SparkleSpeed       = p.SparkleSpeed,
        SparkleRadius      = p.SparkleRadius,
        SparkleStyle       = p.SparkleStyle,
        EnableRoleIcon     = p.EnableRoleIcon,
        RoleIconTemplate   = p.RoleIconTemplate,
        BadgeSlot2Template = p.BadgeSlot2Template,
        BadgePosition      = p.BadgePosition,
        EnableSfwNsfwTint  = p.EnableSfwNsfwTint,
        EnableClockInAlpha = p.EnableClockInAlpha,
        FontSizeOverride   = p.FontSizeOverride,
        OffsetX            = p.OffsetX,
        OffsetY            = p.OffsetY,
    };

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= DrawNameplates;
        Plugin.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}
