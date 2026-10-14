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
        if (@object.NativeChestInteractable)
            return IsValidNativeChestInteraction(@object);
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

    /** Validates the additive capability without inferring chest authority from an ID. */
    public static bool IsValidNativeChestInteraction(ObjectSnapshot @object)
    {
        ArgumentNullException.ThrowIfNull(@object);
        return @object.NativeChestInteractable
            && @object.LevelId != 0
            && @object.NativeObjectId is >= 5 and <= 7
            && !@object.Activated
            && !@object.Destroyed
            && !@object.TrapArmed
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
            && @object.PresentationKind is ProtocolObjectPresentationKind.Unspecified
                or ProtocolObjectPresentationKind.Generic;
    }

    private static bool IsValidCathedralDoorInteraction(ObjectSnapshot @object)
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
            && !@object.TrapArmed
            && !@object.Breakable
            && !@object.IsShrine
            && !@object.ActivateOnOccupancy
            && @object.KeepVisibleAfterActivation
            && @object.AnimationFrame == door.AnimationFrame
            && @object.MissilePassable == door.MissilePassable;
    }
}
