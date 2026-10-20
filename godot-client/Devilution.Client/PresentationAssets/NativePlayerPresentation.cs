using Devilution.Protocol.V1;

namespace Devilution.Client.PresentationAssets;

public enum NativePlayerPresentationTimingSource
{
    SnapshotTickLoop,
    AuthoritativeActionInterval,
    FirstFrameOnly,
    Unresolved,
}

/** Native-compatible player art selection projected from authoritative snapshots. */
public sealed record NativePlayerPresentationSelection(
    uint EntityId,
    uint ClassId,
    NativePlayerLocation Location,
    NativePlayerArmor Armor,
    NativePlayerWeapon Weapon,
    NativePlayerGraphic? Graphic,
    int DirectionIndex,
    int? FrameIndex,
    int? ActionFrame,
    NativePlayerPresentationTimingSource TimingSource,
    string? Diagnostic)
{
    public NativePlayerClipKey? ClipKey => Graphic is { } graphic
        ? new NativePlayerClipKey(ClassId, Location, Armor, Weapon, graphic)
        : null;
}

/** Pixel-space destination calculated from the tile anchor and native sprite centering rule. */
public readonly record struct NativePlayerDestinationRect(
    double X,
    double Y,
    double Width,
    double Height);

/** Primitive observation from the actual world draw, safe to retain without texture references. */
public sealed record NativePlayerDrawObservation(
    uint EntityId,
    string PackId,
    string ContentManifestHash,
    string? ClipKey,
    string? ResourcePath,
    bool IsLoaded,
    bool UsedFallback,
    int DirectionIndex,
    int? FrameIndex,
    int? ActionFrame,
    NativePlayerPresentationTimingSource TimingSource,
    int? RegionX,
    int? RegionY,
    int? RegionWidth,
    int? RegionHeight,
    double DestinationX,
    double DestinationY,
    double DestinationWidth,
    double DestinationHeight,
    double AnchorX,
    double AnchorY,
    string? Diagnostic,
    int? AnchorWidth = null);

/** Status and bounded resource summary for the independently optional native-player pack. */
internal sealed record NativePlayerPackObservation(
    string? ManifestPath,
    bool IsLoaded,
    string? PackId,
    string? ContentManifestHash,
    bool ContentIdentityMatched,
    IReadOnlyList<string> AcceptedContentManifestHashes,
    string? Diagnostic,
    IReadOnlyList<string> LoadedAtlasPaths,
    IReadOnlyList<string> MissingAtlasPaths);

/**
 * Projects native player class, equipment, scene and action state into a
 * client-only animation key. It never advances or mutates simulation state.
 */
public static class NativePlayerPresentation
{
    public const int NativeTileWidth = 32;
    public const int NativeDirectionCount = 8;
    private const uint LeftHandSlot = 4;
    private const uint RightHandSlot = 5;
    private const uint ChestSlot = 6;
    private const int WeaponItemClass = 1;
    private const int ItemTypeSword = 1;
    private const int ItemTypeAxe = 2;
    private const int ItemTypeBow = 3;
    private const int ItemTypeMace = 4;
    private const int ItemTypeShield = 5;
    private const int ItemTypeMediumArmor = 8;
    private const int ItemTypeHeavyArmor = 9;
    private const int ItemTypeStaff = 10;

    /**
     * Resolves supported base-class art from protocol-owned class, equipment,
     * mode and action fields. False means the snapshot has no supported native
     * base class and must remain on the caller's ordinary presentation path.
     */
    public static bool TryResolve(
        PlayerSnapshot player,
        NativePlayerCatalog catalog,
        NativePlayerLocation location,
        double currentTick,
        out NativePlayerPresentationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(catalog);
        selection = default!;
        if (player.CharacterClassId > 2 || !Enum.IsDefined(location))
            return false;

        var armor = ResolveArmor(player);
        var weapon = ResolveWeapon(player);
        var graphicResult = ResolveGraphic(player, location, catalog);
        if (graphicResult.Graphic == NativePlayerGraphic.Death)
            weapon = NativePlayerWeapon.Unarmed;
        else if (location == NativePlayerLocation.Town
            && graphicResult.Graphic is NativePlayerGraphic.Lightning or NativePlayerGraphic.Fire or NativePlayerGraphic.Magic) {
            weapon = weapon switch {
                NativePlayerWeapon.Sword or NativePlayerWeapon.Mace => NativePlayerWeapon.Unarmed,
                NativePlayerWeapon.SwordShield or NativePlayerWeapon.MaceShield => NativePlayerWeapon.UnarmedShield,
                _ => weapon,
            };
        }
        var direction = Enum.IsDefined(player.FacingDirection)
            ? (int)player.FacingDirection
            : -1;

        if (graphicResult.Graphic is not { } graphic || direction < 0) {
            selection = new NativePlayerPresentationSelection(
                player.EntityId,
                player.CharacterClassId,
                location,
                armor,
                weapon,
                graphicResult.Graphic,
                direction,
                null,
                null,
                NativePlayerPresentationTimingSource.Unresolved,
                graphicResult.Diagnostic ?? "The snapshot has an unsupported native direction.");
            return true;
        }

        var key = new NativePlayerClipKey(player.CharacterClassId, location, armor, weapon, graphic);
        if (!catalog.TryGetClip(key, out var clip)) {
            selection = new NativePlayerPresentationSelection(
                player.EntityId,
                player.CharacterClassId,
                location,
                armor,
                weapon,
                graphic,
                direction,
                null,
                null,
                NativePlayerPresentationTimingSource.Unresolved,
                $"Native player clip '{NativePlayerCatalog.FormatKey(key)}' is absent from the partial catalog.");
            return true;
        }

        var (frame, timing, timingDiagnostic) = ResolveFrameIndex(
            player,
            graphic,
            clip,
            currentTick);
        selection = new NativePlayerPresentationSelection(
            player.EntityId,
            player.CharacterClassId,
            location,
            armor,
            weapon,
            graphic,
            direction,
            frame,
            clip.ActionFrame,
            timing,
            graphicResult.Diagnostic ?? timingDiagnostic);
        return true;
    }

    /** Converts the tile-center anchor into the native sprite destination rect. */
    public static NativePlayerDestinationRect CalculateDestinationRect(
        double anchorX,
        double bottomY,
        int frameWidth,
        int frameHeight,
        int tileWidth = NativeTileWidth,
        int? anchorWidth = null)
    {
        if (frameWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));
        if (frameHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameHeight));
        if (tileWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileWidth));
        var nominalAnchorWidth = anchorWidth ?? frameWidth;
        if (nominalAnchorWidth <= 0 || nominalAnchorWidth > frameWidth)
            throw new ArgumentOutOfRangeException(nameof(anchorWidth));

        // Native subtracts (sprite width - tile width) / 2 from a tile-origin
        // coordinate. The nominal CL2 width determines the anchor; the padded
        // storage width remains the complete rendered footprint.
        var nativeSpriteOffset = (nominalAnchorWidth - tileWidth) / 2d;
        var tileOriginX = anchorX - tileWidth / 2d;
        return new NativePlayerDestinationRect(
            tileOriginX - nativeSpriteOffset,
            bottomY - frameHeight + 1,
            frameWidth,
            frameHeight);
    }

    private static NativePlayerArmor ResolveArmor(PlayerSnapshot player)
    {
        var armor = FindEquipped(player, ChestSlot);
        if (!IsUsable(player, armor))
            return NativePlayerArmor.Light;
        return armor!.State.ItemType switch {
            ItemTypeHeavyArmor => NativePlayerArmor.Heavy,
            ItemTypeMediumArmor => NativePlayerArmor.Medium,
            _ => NativePlayerArmor.Light,
        };
    }

    private static NativePlayerWeapon ResolveWeapon(PlayerSnapshot player)
    {
        var left = FindEquipped(player, LeftHandSlot);
        var right = FindEquipped(player, RightHandSlot);
        var hasShield = IsHoldingShield(left) || IsHoldingShield(right);
        var weapon = NativePlayerWeapon.Unarmed;
        if (IsUsableWeapon(player, left))
            weapon = MapWeaponType(left!.State.ItemType);
        if (IsUsableWeapon(player, right))
            weapon = MapWeaponType(right!.State.ItemType);

        return weapon switch {
            NativePlayerWeapon.Unarmed when hasShield => NativePlayerWeapon.UnarmedShield,
            NativePlayerWeapon.Sword when hasShield => NativePlayerWeapon.SwordShield,
            NativePlayerWeapon.Mace when hasShield => NativePlayerWeapon.MaceShield,
            _ => weapon,
        };
    }

    private static (NativePlayerGraphic? Graphic, string? Diagnostic) ResolveGraphic(
        PlayerSnapshot player,
        NativePlayerLocation location,
        NativePlayerCatalog catalog)
    {
        if (!Enum.IsDefined(player.CombatMode))
            return (null, $"Native player combat mode {(int)player.CombatMode} is unsupported.");
        if (player.CurrentAction is { } currentAction && !Enum.IsDefined(currentAction.Kind))
            return (null, $"Native player action kind {(int)currentAction.Kind} is unsupported.");

        NativePlayerGraphic graphic;
        var action = player.CurrentAction;
        if (player.Life <= 0 || player.CombatMode == PlayerCombatMode.Death
            || action?.Kind == PlayerActionKind.Death) {
            graphic = NativePlayerGraphic.Death;
        } else if (player.CombatMode == PlayerCombatMode.Block) {
            graphic = NativePlayerGraphic.Block;
        } else if (player.CombatMode == PlayerCombatMode.GotHit) {
            graphic = NativePlayerGraphic.Hit;
        } else if (player.CombatMode == PlayerCombatMode.Spell
            || action?.Kind == PlayerActionKind.Cast) {
            var spellId = action?.Cast?.SpellId ?? 0;
            if (spellId == 0 || !catalog.TryGetSpellGraphic(spellId, out graphic))
                return (null, $"Native spell graphic for stable spell id {spellId} is unresolved in pack '{catalog.PackId}'.");
        } else if (player.CombatMode is PlayerCombatMode.Attack or PlayerCombatMode.RangedAttack
            || action?.Kind == PlayerActionKind.Attack) {
            graphic = NativePlayerGraphic.Attack;
        } else if (player.CombatMode is PlayerCombatMode.WalkNorthwards
            or PlayerCombatMode.WalkSouthwards or PlayerCombatMode.WalkSideways
            || action?.Kind == PlayerActionKind.Move) {
            graphic = location == NativePlayerLocation.Town
                ? NativePlayerGraphic.TownWalk
                : NativePlayerGraphic.Walk;
        } else {
            graphic = location == NativePlayerLocation.Town
                ? NativePlayerGraphic.TownStand
                : NativePlayerGraphic.Stand;
        }

        return (graphic, null);
    }

    private static (int? Frame, NativePlayerPresentationTimingSource Timing, string? Diagnostic) ResolveFrameIndex(
        PlayerSnapshot player,
        NativePlayerGraphic graphic,
        NativePlayerClip clip,
        double currentTick)
    {
        var expectedKind = graphic switch {
            NativePlayerGraphic.TownWalk or NativePlayerGraphic.Walk => PlayerActionKind.Move,
            NativePlayerGraphic.Attack => PlayerActionKind.Attack,
            NativePlayerGraphic.Lightning or NativePlayerGraphic.Fire or NativePlayerGraphic.Magic => PlayerActionKind.Cast,
            NativePlayerGraphic.Death => PlayerActionKind.Death,
            _ => PlayerActionKind.Unspecified,
        };
        if (expectedKind != PlayerActionKind.Unspecified
            && player.CurrentAction is { } action
            && action.Kind == expectedKind
            && action.EndTick >= action.StartedAtTick
            && action.EndTick > 0) {
            var tick = double.IsFinite(currentTick) ? currentTick : action.StartedAtTick;
            var elapsedTicks = Math.Clamp(tick, action.StartedAtTick, action.EndTick) - action.StartedAtTick;
            var frame = (int)Math.Clamp(
                Math.Floor(elapsedTicks / clip.TicksPerFrame),
                0,
                clip.EffectiveAnimationFrames - 1);
            return (frame, NativePlayerPresentationTimingSource.AuthoritativeActionInterval, null);
        }

        if (graphic is NativePlayerGraphic.TownStand or NativePlayerGraphic.TownWalk
            or NativePlayerGraphic.Stand or NativePlayerGraphic.Walk) {
            var tick = double.IsFinite(currentTick) ? Math.Max(0, currentTick) : 0;
            var loopFrame = (int)(Math.Floor(tick / clip.TicksPerFrame) % clip.EffectiveAnimationFrames);
            return (loopFrame, NativePlayerPresentationTimingSource.SnapshotTickLoop, null);
        }

        return (
            0,
            NativePlayerPresentationTimingSource.FirstFrameOnly,
            $"The protocol has no native frame cursor or matching action interval for {NativePlayerCatalog.FormatGraphic(graphic)}; showing frame 0.");
    }

    private static EquippedItemSnapshot? FindEquipped(PlayerSnapshot player, uint slot)
        => player.Equipment.FirstOrDefault(item => item.Slot == slot);

    private static bool IsUsableWeapon(PlayerSnapshot player, EquippedItemSnapshot? item)
        => IsUsable(player, item)
            && item!.State.ItemClass == WeaponItemClass;

    private static bool IsUsable(PlayerSnapshot player, EquippedItemSnapshot? item)
    {
        if (item?.State is not { } state || state.Deleted || state.ItemType <= 0)
            return false;
        var attributes = player.Attributes;
        return (attributes?.Strength?.Current ?? 0) >= state.MinimumStrength
            && (attributes?.Magic?.Current ?? 0) >= state.MinimumMagic
            && (attributes?.Dexterity?.Current ?? 0) >= state.MinimumDexterity;
    }

    private static bool IsHoldingShield(EquippedItemSnapshot? item)
        => item?.State is { Deleted: false, ItemType: ItemTypeShield, StatFlag: true };

    private static NativePlayerWeapon MapWeaponType(int itemType)
        => itemType switch {
            ItemTypeSword => NativePlayerWeapon.Sword,
            ItemTypeAxe => NativePlayerWeapon.Axe,
            ItemTypeBow => NativePlayerWeapon.Bow,
            ItemTypeMace => NativePlayerWeapon.Mace,
            ItemTypeStaff => NativePlayerWeapon.Staff,
            _ => NativePlayerWeapon.Unarmed,
        };
}
