using Devilution.Protocol;
using Devilution.Protocol.V1;
using ProtocolObjectPresentationKind = Devilution.Protocol.V1.ObjectPresentationKind;

namespace Devilution.Client.Protocol;

/** Resolves visible authored objects without treating presentation-only objects as commands. */
public static class AuthoritativeObjectSelector
{
    /** Selects the first visible active authored object for generic UI actions. */
    public static ObjectSnapshot? SelectFirst(Snapshot? snapshot)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var geometry = snapshot!.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return snapshot!.Objects
            .Where(@object => IsSelectable(@object, player, geometry, lightRadius))
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    /** Selects an active authored object at the local player's exact tile. */
    public static ObjectSnapshot? SelectAt(Snapshot? snapshot, int x, int y)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var geometry = snapshot!.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return snapshot!.Objects
            .Where(@object => IsSelectable(@object, player, geometry, lightRadius)
                && @object.PositionX == x
                && @object.PositionY == y)
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    /** Selects an explicitly declared native barrel as a melee target at one tile. */
    public static ObjectSnapshot? SelectNativeMeleeBarrelAt(
        Snapshot? snapshot, int x, int y, uint? localPlayerEntityId = null)
    {
        var player = snapshot?.Players.FirstOrDefault(value => value.EntityId == localPlayerEntityId)
            ?? snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var geometry = snapshot!.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return snapshot!.Objects
            .Where(@object => @object.NativeBarrelMeleeBreakable
                && @object.EntityId != 0
                && IsValidNativeBarrelMeleeBreakable(@object)
                && AuthoritativeObjectPresentationVisibility.IsVisible(
                    @object,
                    player.LevelId,
                    geometry,
                    player.PositionX,
                    player.PositionY,
                    lightRadius)
                && !@object.Activated
                && !@object.Destroyed
                && @object.PositionX == x
                && @object.PositionY == y)
            .OrderBy(@object => @object.EntityId)
            .FirstOrDefault();
    }

    private static bool IsSelectable(
        ObjectSnapshot @object,
        PlayerSnapshot player,
        LevelGeometrySnapshot? geometry,
        int lightRadius)
        => @object.EntityId != 0
            && AuthoritativeObjectPresentationVisibility.IsVisible(
                @object,
                player.LevelId,
                geometry,
                player.PositionX,
                player.PositionY,
                lightRadius)
            && !@object.Activated
            && !@object.Destroyed
            && HasAuthoritativeInteraction(@object);

    /** Returns whether the server declared behavior that can be submitted as an intent. */
    public static bool HasAuthoritativeInteraction(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        if (@object.NativeTrapInteractable)
            return IsValidNativeTrapInteraction(@object);
        if (@object.NativeBarrelMeleeBreakable)
            return false;
        if (@object.NativeChestInteractable)
            return IsValidNativeChestInteraction(@object);
        if (@object.NativeSarcophagusInteractable)
            return IsValidNativeSarcophagusInteraction(@object);
        if (@object.CathedralDoor is not null)
            return IsValidCathedralDoorInteraction(@object);
        return @object.EffectKind != 0
            || @object.QuestId != 0
            || @object.QuestTransitionKind != 0
            || @object.RewardKind != 0
            || @object.QuestSequenceFollowUpKind != 0
            || @object.DialogueMessageId != 0
            || @object.TrapArmed;
    }

    /** Validates the explicit native L1 trap capability without inferring authority from armed state alone. */
    public static bool IsValidNativeTrapInteraction(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        if (!@object.NativeTrapInteractable
            || !@object.TrapArmed
            || @object.NativeChestInteractable
            || @object.NativeSarcophagusInteractable
            || @object.NativeBarrelMeleeBreakable
            || @object.NativeObjectId is not (1 or 2 or 5 or 6 or 7)
            || @object.LevelId == 0
            || @object.Activated
            || @object.Destroyed)
            return false;

        return @object.NativeObjectId is 1 or 2
            ? @object.CathedralDoor is not null
                && IsValidCathedralDoorInteraction(@object, allowArmedNativeTrap: true)
            : @object.CathedralDoor is null
                && IsPlainNativeInteraction(@object, allowArmedTrap: true);
    }

    /** Validates the additive capability without inferring chest authority from an ID. */
    public static bool IsValidNativeChestInteraction(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        return @object.NativeChestInteractable
            && !@object.NativeSarcophagusInteractable
            && !@object.NativeBarrelMeleeBreakable
            && !@object.NativeTrapInteractable
            && @object.NativeObjectId is >= 5 and <= 7
            && IsPlainNativeInteraction(@object);
    }

    /** Requires the explicit sarcophagus capability and a consistent closed native shape. */
    public static bool IsValidNativeSarcophagusInteraction(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        return @object.NativeSarcophagusInteractable
            && !@object.NativeChestInteractable
            && !@object.NativeBarrelMeleeBreakable
            && !@object.NativeTrapInteractable
            && @object.NativeObjectId == 48
            && IsPlainNativeInteraction(@object);
    }

    /** Validates the explicit capability without making barrels generic UI interactions. */
    public static bool IsValidNativeBarrelMeleeBreakable(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        return @object.NativeBarrelMeleeBreakable
            && !@object.NativeChestInteractable
            && !@object.NativeSarcophagusInteractable
            && !@object.NativeTrapInteractable
            && @object.NativeObjectId is 57 or 58
            && IsPlainNativeInteraction(@object);
    }

    private static bool IsPlainNativeInteraction(ObjectSnapshot @object, bool allowArmedTrap = false)
        => @object.LevelId != 0
            && !@object.Activated
            && !@object.Destroyed
            && (allowArmedTrap || !@object.TrapArmed)
            && !@object.Breakable
            && !@object.IsShrine
            && !@object.ActivateOnOccupancy
            && @object.CathedralDoor is null
            && @object.EffectKind == 0
            && @object.EffectAmount == 0
            && @object.QuestId == 0
            && @object.QuestTransitionKind == 0
            && @object.QuestTransitionAmount == 0
            && @object.QuestTransitionRequiredVariable1 is -1 or 0
            && @object.QuestTransitionRequiredVariable2 is -1 or 0
            && @object.RewardKind == 0
            && @object.RewardId == 0
            && @object.RewardItemLevel == 0
            && @object.RewardOffsetX == 0
            && @object.RewardOffsetY == 0
            && @object.RewardOriginKind == 0
            && @object.RewardSetLevelId == 0
            && @object.ProjectileTargetOffsetX == 0
            && @object.ProjectileTargetOffsetY == 0
            && @object.QuestSequenceFollowUpKind == 0
            && @object.QuestSequenceFollowUpAmount == 0
            && @object.QuestSequenceFollowUpOffsetX == 0
            && @object.QuestSequenceFollowUpOffsetY == 0
            && @object.ProjectileSourceKind == 0
            && @object.DialogueMessageId == 0
            && @object.ActivatedAnimationFrame == 0
            && (@object.PresentationKind is ProtocolObjectPresentationKind.Unspecified
                    or ProtocolObjectPresentationKind.Generic
                || allowArmedTrap && @object.PresentationKind == ProtocolObjectPresentationKind.Trap);

    private static bool IsValidCathedralDoorInteraction(
        ObjectSnapshot @object,
        bool allowArmedNativeTrap = false)
    {
        var door = @object.CathedralDoor!;
        var closed = door.DoorState == 0;
        var baselineFrame = door.NativeObjectId == 1 ? 1U : 2U;
        return door.EntityId == @object.EntityId
            && door.LevelId == @object.LevelId
            && door.PositionX == @object.PositionX
            && door.PositionY == @object.PositionY
            && door.NativeObjectId == @object.NativeObjectId
            && door.NativeObjectId is 1 or 2
            && door.DoorState is >= 0 and <= 2
            && door.CachedPiece > 0
            && door.CachedPiece <= AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1
            && door.CachedNeighborPiece > 0
            && door.CachedNeighborPiece <= AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1
            && door.AnimationFrame <= byte.MaxValue
            && door.AnimationFrame >= baselineFrame
            && (door.AnimationFrame - baselineFrame) % 2 == 0
            && (closed
                ? door.AnimationFrame <= 253
                : door.AnimationFrame >= baselineFrame + 2)
            && door.SelectionRegion == (closed ? 3U : 2U)
            && door.PreDraw == !closed
            && door.MissilePassable == !closed
            && !door.Solid
            && !@object.Activated
            && !@object.Destroyed
            && @object.EffectKind == 0
            && @object.QuestId == 0
            && @object.QuestTransitionKind == 0
            && @object.RewardKind == 0
            && @object.QuestSequenceFollowUpKind == 0
            && @object.DialogueMessageId == 0
            && (allowArmedNativeTrap || !@object.TrapArmed)
            && !@object.Breakable
            && !@object.IsShrine
            && !@object.ActivateOnOccupancy
            && @object.KeepVisibleAfterActivation
            && @object.AnimationFrame == door.AnimationFrame
            && @object.MissilePassable == door.MissilePassable;
    }
}
