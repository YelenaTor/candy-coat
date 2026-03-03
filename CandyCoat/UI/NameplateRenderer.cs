using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using CandyCoat.Data;
using CandyCoat.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CandyCoat.UI;

public class NameplateRenderer : IDisposable
{
    private readonly Plugin _plugin;
    private readonly CosmeticFontManager _fontManager;
    private readonly CosmeticBadgeManager _badgeManager;
    private readonly Dictionary<string, NameplateAnchor> _nameplateAnchors = new(StringComparer.Ordinal);
    private bool _nativeAnchorSubscriptionActive;
    private int _lastAnchorCleanupFrame;

    private const int AnchorFreshFrameBudget = 6;
    private const int AnchorStaleFrameBudget = 180;
    private const float LegacyBaseYOffset = 30f;

    public NameplateRenderer(Plugin plugin, CosmeticFontManager fontManager, CosmeticBadgeManager badgeManager)
    {
        _plugin = plugin;
        _fontManager = fontManager;
        _badgeManager = badgeManager;
        Svc.PluginInterface.UiBuilder.Draw += DrawNameplates;
        Plugin.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
        try
        {
            Plugin.NamePlateGui.OnDataUpdate += OnDataUpdate;
            _nativeAnchorSubscriptionActive = true;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[NameplateRenderer] OnDataUpdate unavailable, using fallback placement only: {ex.Message}");
            _nativeAnchorSubscriptionActive = false;
        }
    }

    private void OnDataUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!_nativeAnchorSubscriptionActive)
            return;

        int frame = ImGui.GetFrameCount();
        foreach (var handler in handlers)
        {
            var pc = handler.PlayerCharacter;
            if (pc == null) continue;

            if (!TryReadNativeAnchor(handler, out var anchor))
                continue;

            var hash = BuildCharacterHash(pc.Name.ToString(), pc.HomeWorld.Value.Name.ToString());
            _nameplateAnchors[hash] = new NameplateAnchor(anchor, frame);
        }

        CleanupStaleAnchors(frame);
    }

    private void OnNamePlateUpdate(
        INamePlateUpdateContext context,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!_plugin.Configuration.EnableNameplateCosmetics)
            return;

        var localName = _plugin.Configuration.CharacterName;
        var localWorld = _plugin.Configuration.HomeWorld;
        var staffLookup = BuildStaffLookup();

        foreach (var handler in handlers)
        {
            var pc = handler.PlayerCharacter;
            if (pc == null) continue;

            var name  = pc.Name.ToString();
            var world = pc.HomeWorld.Value.Name.ToString();
            var hash  = BuildCharacterHash(name, world);

            if (!TryResolveRenderDecision(name, world, hash, localName, localWorld, staffLookup, out _, out _))
                continue;

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
        if (!_plugin.Configuration.EnableNameplateCosmetics)
            return;

        var drawList = ImGui.GetBackgroundDrawList();
        var localName  = _plugin.Configuration.CharacterName;
        var localWorld = _plugin.Configuration.HomeWorld;
        var staffLookup = BuildStaffLookup();

        foreach (var obj in Svc.Objects)
        {
            if (obj is not IPlayerCharacter pc) continue;

            var name  = pc.Name.ToString();
            var world = pc.HomeWorld.Value.Name.ToString();
            var hash  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{name}@{world}"));
            var isLocalCharacter = name == localName && world == localWorld;

            if (!TryResolveRenderDecision(name, world, hash, localName, localWorld, staffLookup, out var profile, out var staffMatch))
                continue;

            if (!TryResolveAnchor(pc, hash, out var anchorCenter, out var isLegacyAnchor))
                continue;

            // LegacyBaseYOffset only compensates for the dual-projection fallback, which
            // anchors at computed head-height above feet and needs a small downward nudge.
            // Native and world-position anchors already land at the nameplate position.
            var screenPos = new Vector2(
                MathF.Round(anchorCenter.X + profile.OffsetX),
                MathF.Round(anchorCenter.Y + profile.OffsetY + (isLegacyAnchor ? LegacyBaseYOffset : 0f)));

            bool? isClockedIn = ResolveClockedInState(isLocalCharacter, staffMatch);
            float alphaMult = profile.EnableClockInAlpha && isClockedIn == false ? 0.3f : 1f;

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

            // Blend role badge + glow onto profiles that have no badge configured
            if (staffMatch != null
                && renderProfile.RoleIconTemplate == "None"
                && Enum.TryParse<Data.StaffRole>(staffMatch.Role, true, out var matchRole)
                && _plugin.Configuration.RoleDefaults.TryGetValue(matchRole, out var roleDefault)
                && roleDefault.Enabled)
            {
                if (ReferenceEquals(renderProfile, profile))
                    renderProfile = ShallowClone(renderProfile);
                renderProfile.RoleIconTemplate = roleDefault.BadgeTemplate;
                if (!renderProfile.EnableGlow)
                {
                    renderProfile.EnableGlow = true;
                    renderProfile.GlowColor = roleDefault.GlowColor;
                }
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

    private void CleanupStaleAnchors(int frame)
    {
        if (frame - _lastAnchorCleanupFrame < 60)
            return;

        _lastAnchorCleanupFrame = frame;
        List<string>? staleKeys = null;
        foreach (var kvp in _nameplateAnchors)
        {
            if (frame - kvp.Value.Frame > AnchorStaleFrameBudget)
            {
                staleKeys ??= [];
                staleKeys.Add(kvp.Key);
            }
        }

        if (staleKeys == null)
            return;

        foreach (var key in staleKeys)
            _nameplateAnchors.Remove(key);
    }

    private bool TryResolveRenderDecision(
        string name,
        string world,
        string hash,
        string localName,
        string localWorld,
        Dictionary<string, SyncedStaff> staffLookup,
        out CosmeticProfile profile,
        out SyncedStaff? staffMatch)
    {
        staffLookup.TryGetValue(BuildStaffKey(name, world), out staffMatch);

        if (_plugin.SyncService.Cosmetics.TryGetValue(hash, out var syncedProfile) && syncedProfile != null)
        {
            profile = syncedProfile;
            return true;
        }

        if (name == localName && world == localWorld)
        {
            profile = _plugin.Configuration.CosmeticProfile;
            return true;
        }

        if (staffMatch != null
            && Enum.TryParse<Data.StaffRole>(staffMatch.Role, true, out var smRole)
            && _plugin.Configuration.RoleDefaults.TryGetValue(smRole, out var rd)
            && rd.Enabled)
        {
            profile = BuildRoleDefaultProfile(rd);
            return true;
        }

        profile = null!;
        return false;
    }

    private static string BuildCharacterHash(string name, string world) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{name}@{world}"));

    private static string BuildStaffKey(string name, string world) => $"{name}@{world}";

    private Dictionary<string, SyncedStaff> BuildStaffLookup()
    {
        var lookup = new Dictionary<string, SyncedStaff>(StringComparer.Ordinal);
        foreach (var staff in _plugin.SyncService.OnlineStaff)
        {
            if (string.IsNullOrWhiteSpace(staff.CharacterName) || string.IsNullOrWhiteSpace(staff.HomeWorld))
                continue;
            lookup[BuildStaffKey(staff.CharacterName, staff.HomeWorld)] = staff;
        }

        return lookup;
    }

    private bool TryResolveAnchor(IPlayerCharacter pc, string hash, out Vector2 center, out bool isLegacyAnchor)
    {
        if (_nativeAnchorSubscriptionActive && TryGetNativeAnchor(hash, out center))
        { isLegacyAnchor = false; return true; }
        if (TryGetNameplateWorldAnchor(pc, out center))
        { isLegacyAnchor = false; return true; }
        isLegacyAnchor = true;
        return TryGetDualProjectionAnchor(pc, out center);
    }

    private bool TryGetNativeAnchor(string hash, out Vector2 center)
    {
        center = default;
        if (!_nameplateAnchors.TryGetValue(hash, out var anchor))
            return false;

        var age = ImGui.GetFrameCount() - anchor.Frame;
        if (age > AnchorFreshFrameBudget)
            return false;

        center = anchor.Center;
        return true;
    }

    private static unsafe bool TryReadNativeAnchor(INamePlateUpdateHandler handler, out Vector2 center)
    {
        center = default;
        if (handler.NamePlateObjectAddress == 0)
            return false;

        var nameplateObject = (AddonNamePlate.NamePlateObject*)handler.NamePlateObjectAddress;
        if (nameplateObject == null || nameplateObject->RootComponentNode == null)
            return false;

        var node = (AtkResNode*)nameplateObject->RootComponentNode;
        if (node == null)
            return false;
        if (node->ScreenX == 0f && node->ScreenY == 0f)
            return false;

        center = new Vector2(node->ScreenX + node->Width / 2f, node->ScreenY + node->Height / 2f);
        return true;
    }

    private static unsafe bool TryGetNameplateWorldAnchor(IPlayerCharacter pc, out Vector2 center)
    {
        center = default;
        var gameObject = pc.Struct();
        if (gameObject == null)
            return false;

        FFXIVClientStructs.FFXIV.Common.Math.Vector3 worldPos = default;
        gameObject->GetNamePlateWorldPosition(&worldPos);

        // Guard: GetNamePlateWorldPosition writes (0,0,0) when the nameplate is not
        // loaded or the character is culled (common when zoomed out). WorldToScreen
        // happily succeeds on (0,0,0) and returns the world-origin projected to screen,
        // which ends up in the top-left — reject it before it poisons the draw call.
        if (worldPos.X == 0f && worldPos.Y == 0f && worldPos.Z == 0f)
            return false;

        // Sanity: nameplate should be within ~2 units horizontally and above the feet.
        // Mounted/large races may push it to ~4 units up, so allow up to 6 for safety.
        if (MathF.Abs(worldPos.X - pc.Position.X) > 2f || MathF.Abs(worldPos.Z - pc.Position.Z) > 2f)
            return false;
        if (worldPos.Y < pc.Position.Y || worldPos.Y > pc.Position.Y + 6f)
            return false;

        if (!Svc.GameGui.WorldToScreen(new Vector3(worldPos.X, worldPos.Y, worldPos.Z), out var screenPos))
            return false;

        center = new Vector2(screenPos.X, screenPos.Y);
        return true;
    }

    private static bool TryGetDualProjectionAnchor(IPlayerCharacter pc, out Vector2 center)
    {
        center = default;
        if (!Svc.GameGui.WorldToScreen(pc.Position, out var feetScreen))
            return false;
        if (!Svc.GameGui.WorldToScreen(new Vector3(pc.Position.X, pc.Position.Y + 1f, pc.Position.Z), out var oneUnitUp))
            return false;

        var pxPerUnit = feetScreen.Y - oneUnitUp.Y;
        var pixelLift = pxPerUnit * 1.7f;
        center = new Vector2(feetScreen.X, feetScreen.Y - pixelLift);
        return true;
    }

    private bool? ResolveClockedInState(bool isLocalCharacter, SyncedStaff? staffMatch)
    {
        if (isLocalCharacter)
            return _plugin.ShiftManager.CurrentShift != null;
        if (staffMatch != null)
            return staffMatch.ShiftStart != null;
        return null;
    }

    private static CosmeticProfile BuildRoleDefaultProfile(Data.RoleDefaultCosmetic rd) => new()
    {
        EnableGlow         = true,
        GlowColor          = rd.GlowColor,
        RoleIconTemplate   = rd.BadgeTemplate,
        EnableRoleIcon     = true,
        EnableDropShadow   = true,
        FontSizeOverride   = 30,
    };

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
        if (_nativeAnchorSubscriptionActive)
            Plugin.NamePlateGui.OnDataUpdate -= OnDataUpdate;
    }

    private readonly record struct NameplateAnchor(Vector2 Center, int Frame);
}
