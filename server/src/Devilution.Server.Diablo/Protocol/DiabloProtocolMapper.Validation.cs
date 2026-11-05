using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Domain.Protocol;
using Devilution.Engine;
using Devilution.Server.Snapshots;
using AuthoritativeMonsterTypeCatalog = Devilution.Diablo.AuthoritativeMonsterTypeCatalog;
using AuthoritativeProjectileBehavior = Devilution.Diablo.AuthoritativeProjectileBehavior;

namespace Devilution.Server.Protocol;

/** Projection validation for objects, geometry and quests. */
internal static partial class DiabloProtocolMapper
{
    private static void ValidateProjectedWorldItemPositions(
        IReadOnlyList<DiabloWorldItemState> worldItems,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var group in worldItems.GroupBy(item => item.LevelId)) {
            if (group.Count() > AuthoritativeLevel.NativeMaximumItemCount)
                throw new InvalidDataException(
                    $"Authoritative world-item state for level {group.Key} exceeds the native "
                    + $"{AuthoritativeLevel.NativeMaximumItemCount}-item capacity.");
        }
        foreach (var item in worldItems) {
            ValidateProjectedEntityPosition(
                item.LevelId,
                item.PositionX,
                item.PositionY,
                levels,
                "world-item");
        }
    }

    private static void ValidateProjectedObjectPositions(
        IReadOnlyList<DiabloObjectState> objects,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var group in objects.GroupBy(@object => @object.LevelId)) {
            if (group.Count() > AuthoritativeLevel.NativeMaximumObjectCount)
                throw new InvalidDataException(
                    $"Authoritative object state for level {group.Key} exceeds the native "
                    + $"{AuthoritativeLevel.NativeMaximumObjectCount}-object capacity.");
        }
        foreach (var @object in objects) {
            ValidateProjectedEntityPosition(
                @object.LevelId,
                @object.PositionX,
                @object.PositionY,
                levels,
                "object");
        }
    }

    private static void ValidateProjectedObjectStates(
        IReadOnlyList<DiabloObjectState> objects,
        IReadOnlyList<DiabloQuestState> quests)
    {
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var @object in objects) {
            var transitionKind = @object.QuestTransitionKind;
            var rewardKind = @object.RewardKind;
            var followUpKind = @object.QuestSequenceFollowUpKind;
            var effectKind = @object.EffectKind;
            var transitionSetsVariable = transitionKind is
                AuthoritativeObjectQuestTransitionKind.SetVariable1
                or AuthoritativeObjectQuestTransitionKind.SetVariable2
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable2
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable1
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable2;
            var transitionValid = transitionKind == AuthoritativeObjectQuestTransitionKind.None
                || @object.QuestId != 0
                    && (transitionSetsVariable
                        ? @object.QuestTransitionAmount > 0
                        : @object.QuestTransitionAmount == 0);
            var rewardValid = rewardKind == AuthoritativeObjectRewardKind.None
                ? @object.RewardId == 0
                    && @object.RewardItemLevel == 0
                    && @object.RewardOffsetX == 0
                    && @object.RewardOffsetY == 0
                    && @object.RewardOriginKind == AuthoritativeObjectRewardOriginKind.Object
                    && @object.RewardSetLevelId == -1
                : @object.RewardId != 0;
            var producesProjectile = effectKind is
                    AuthoritativeObjectEffectKind.SpawnSpellProjectile
                    or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                || followUpKind == AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile;
            var followUpValid = followUpKind switch {
                AuthoritativeQuestSequenceFollowUpKind.None
                    => @object.QuestSequenceFollowUpAmount == 0
                        && @object.QuestSequenceFollowUpOffsetX == 0
                        && @object.QuestSequenceFollowUpOffsetY == 0,
                AuthoritativeQuestSequenceFollowUpKind.WorldMutation
                    => (effectKind == AuthoritativeObjectEffectKind.QuestSequence
                        || transitionKind != AuthoritativeObjectQuestTransitionKind.None)
                        && @object.QuestSequenceFollowUpAmount != 0
                        && @object.QuestSequenceFollowUpOffsetX == 0
                        && @object.QuestSequenceFollowUpOffsetY == 0,
                AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile
                    => (effectKind == AuthoritativeObjectEffectKind.QuestSequence
                        || effectKind == AuthoritativeObjectEffectKind.WorldMutation
                        || transitionKind != AuthoritativeObjectQuestTransitionKind.None)
                        && @object.QuestSequenceFollowUpAmount != 0,
                AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                    => (effectKind == AuthoritativeObjectEffectKind.QuestSequence
                        || transitionKind != AuthoritativeObjectQuestTransitionKind.None)
                        && @object.QuestSequenceFollowUpAmount != 0,
                _ => false,
            };

            if (@object.ObjectId == 0
                || @object.EffectAmount < 0
                || effectKind is < AuthoritativeObjectEffectKind.None
                    or > AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile
                || @object.QuestId != 0 && !questIds.Contains(@object.QuestId)
                || transitionKind is < AuthoritativeObjectQuestTransitionKind.None
                    or > AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable2
                || @object.QuestTransitionAmount is < 0 or > MaximumNativeQuestByte
                || @object.QuestTransitionRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || @object.QuestTransitionRequiredVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || !transitionValid
                || rewardKind is < AuthoritativeObjectRewardKind.None
                    or > AuthoritativeObjectRewardKind.UniqueItem
                || @object.RewardItemLevel is < 0 or > MaximumNativeQuestByte
                || @object.RewardOffsetX is < MinimumNativeRewardOffset or > MaximumNativeRewardOffset
                || @object.RewardOffsetY is < MinimumNativeRewardOffset or > MaximumNativeRewardOffset
                || @object.RewardOriginKind is < AuthoritativeObjectRewardOriginKind.Object
                    or > AuthoritativeObjectRewardOriginKind.SetLevelOrigin
                || @object.RewardOriginKind == AuthoritativeObjectRewardOriginKind.Object
                    && @object.RewardSetLevelId != -1
                || @object.RewardOriginKind == AuthoritativeObjectRewardOriginKind.SetLevelOrigin
                    && @object.RewardSetLevelId is < 0 or > AuthoritativeProtocolLimits.MaximumNativeSetLevelId
                || !rewardValid
                || @object.ProjectileTargetOffsetX is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || @object.ProjectileTargetOffsetY is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || !producesProjectile
                    && (@object.ProjectileTargetOffsetX != 0
                        || @object.ProjectileTargetOffsetY != 0)
                || followUpKind is < AuthoritativeQuestSequenceFollowUpKind.None
                    or > AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster
                || @object.QuestSequenceFollowUpOffsetX is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || @object.QuestSequenceFollowUpOffsetY is < MinimumNativeObjectOffset or > MaximumNativeObjectOffset
                || !followUpValid
                || @object.ProjectileSourceKind is < AuthoritativeObjectProjectileSourceKind.Object
                    or > AuthoritativeObjectProjectileSourceKind.ActivatingPlayer
                || !producesProjectile && @object.ProjectileSourceKind
                    != AuthoritativeObjectProjectileSourceKind.Object
                || @object.NativeObjectId > AuthoritativeLevel.NativeMaximumObjectId
                || !Enum.IsDefined(@object.PresentationKind)
                || @object.DialogueMessageId is < 0 or > short.MaxValue
                || !AuthoritativeWorldObject.IsValidPresentation(
                    @object.HideWhenActivated,
                    @object.AnimationFrame,
                    @object.ActivatedAnimationFrame)
                || @object.Breakable && @object.MissilePassable
                || @object.Destroyed && !@object.Breakable) {
                throw new InvalidDataException(
                    "Authoritative object state is outside the shared protocol range.");
            }
        }
    }

    /** Validates the optional typed Cathedral door projection without changing the legacy object shape. */
    private static Dictionary<uint, DiabloCathedralDoorObjectState> ValidateProjectedCathedralDoorObjects(
        IReadOnlyList<DiabloCathedralDoorObjectState> doors,
        IReadOnlyList<DiabloObjectState> objects,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(doors);
        var objectByEntity = new Dictionary<uint, DiabloObjectState>();
        foreach (var @object in objects) {
            if (!objectByEntity.TryAdd(@object.EntityId, @object)) {
                throw new InvalidDataException(
                    "Authoritative object projection contains duplicate entity IDs.");
            }
        }
        var byEntity = new Dictionary<uint, DiabloCathedralDoorObjectState>();
        foreach (var door in doors) {
            if (door is null || door.EntityId == 0 || !byEntity.TryAdd(door.EntityId, door)
                || !objectByEntity.TryGetValue(door.EntityId, out var @object)) {
                throw new InvalidDataException(
                    "Authoritative Cathedral door projection contains an unknown or duplicate entity ID.");
            }
            ValidateCathedralDoorFields(door, levels);
            if (door.LevelId != @object.LevelId
                || door.PositionX != @object.PositionX
                || door.PositionY != @object.PositionY
                || door.NativeObjectId != @object.NativeObjectId
                || @object.Activated
                || @object.Destroyed
                || @object.EffectKind != AuthoritativeObjectEffectKind.None
                || @object.QuestId != 0
                || @object.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
                || @object.RewardKind != AuthoritativeObjectRewardKind.None
                || @object.QuestSequenceFollowUpKind != AuthoritativeQuestSequenceFollowUpKind.None
                || @object.DialogueMessageId != 0
                || @object.Breakable
                || @object.IsShrine
                || @object.ActivateOnOccupancy
                || @object.HideWhenActivated
                || @object.AnimationFrame != door.AnimationFrame
                || @object.MissilePassable != door.MissilePassable) {
                throw new InvalidDataException(
                    "Authoritative Cathedral door projection conflicts with its object metadata.");
            }
        }

        return byEntity;
    }

    private static void ValidateProjectedCathedralDoorCheckpoint(
        DiabloCathedralDoorCheckpoint? checkpoint,
        IReadOnlyList<DiabloCathedralDoorObjectState> doors)
    {
        if (checkpoint is null)
            return;

        checkpoint.ValidateShape();
        var checkpointDoors = checkpoint.DoorLevels
            .SelectMany(level => level.Doors)
            .ToArray();
        if (checkpointDoors.Length != doors.Count
            || checkpointDoors.Length != checkpointDoors.Distinct().Count()
            || doors.Count != doors.Distinct().Count()
            || !checkpointDoors.ToHashSet().SetEquals(doors)) {
            throw new InvalidDataException(
                "Authoritative Cathedral door checkpoint does not match its detached object projection.");
        }
    }

    private static void ValidateProjectedCathedralDoorSpecialLayers(
        IEnumerable<DiabloCathedralDoorObjectState> doors,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        foreach (var door in doors) {
            if (!levels.TryGetValue(door.LevelId, out var level)
                || level.NativeSpecialLayer.Count == 0
                || door.PositionX < 0
                || door.PositionY < 0
                || door.PositionX >= level.Width
                || door.PositionY >= level.Height) {
                continue;
            }
            var cell = checked(door.PositionY * level.Width + door.PositionX);
            var expected = door.DoorState == 0
                ? 0
                : door.NativeObjectId == 1 ? 7 : 8;
            if (level.NativeSpecialLayer[cell] != expected) {
                throw new InvalidDataException(
                    "Authoritative Cathedral door conflicts with its projected native special layer.");
            }
        }
    }

    private static void ValidateCathedralDoorFields(
        DiabloCathedralDoorObjectState door,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        ValidateProjectedEntityPosition(
            door.LevelId,
            door.PositionX,
            door.PositionY,
            levels,
            "Cathedral door");
        var closed = door.DoorState == 0;
        var baselineFrame = door.NativeObjectId == 1 ? 1 : 2;
        if (door.NativeObjectId is < 1 or > 2
            || door.DoorState is < 0 or > 2
            || door.CachedPiece is <= 0
            || door.CachedPiece > checked((int)AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1)
            || door.CachedNeighborPiece is <= 0
            || door.CachedNeighborPiece > checked((int)AuthoritativeProtocolLimits.NativeMaximumDunPieceId + 1)
            || door.AnimationFrame is < 0 or > byte.MaxValue
            || door.AnimationFrame < baselineFrame
            || (door.AnimationFrame - baselineFrame) % 2 != 0
            || closed && door.AnimationFrame > 253
            || !closed && door.AnimationFrame < baselineFrame + 2
            || door.SelectionRegion != (closed ? 3 : 2)
            || door.PreDraw != !closed
            || door.MissilePassable != !closed
            || door.Solid) {
            throw new InvalidDataException(
                "Authoritative Cathedral door projection contains invalid native fields.");
        }
    }

    private static CathedralDoorSnapshot ToProtocolCathedralDoor(
        DiabloCathedralDoorObjectState door)
        => new() {
            EntityId = door.EntityId,
            LevelId = door.LevelId,
            PositionX = door.PositionX,
            PositionY = door.PositionY,
            NativeObjectId = checked((uint)door.NativeObjectId),
            DoorState = door.DoorState,
            CachedPiece = checked((uint)door.CachedPiece),
            CachedNeighborPiece = checked((uint)door.CachedNeighborPiece),
            AnimationFrame = checked((uint)door.AnimationFrame),
            SelectionRegion = checked((uint)door.SelectionRegion),
            PreDraw = door.PreDraw,
            MissilePassable = door.MissilePassable,
            Solid = door.Solid,
        };

    private static void ValidateProjectedQuestStates(
        IReadOnlyList<DiabloQuestState> quests)
    {
        if (quests.Count > MaximumSnapshotQuestCount) {
            throw new InvalidDataException(
                "Authoritative quest state exceeds the shared protocol collection limit.");
        }

        var questIds = new HashSet<uint>();
        var nativeQuestIds = new HashSet<uint>();
        foreach (var quest in quests) {
            if (quest.QuestId == 0
                || !questIds.Add(quest.QuestId)
                || quest.NativeQuestId.HasValue
                    && (quest.NativeQuestId.Value > AuthoritativeQuestTypeCatalog.MaximumNativeQuestId
                        || !nativeQuestIds.Add(quest.NativeQuestId.Value))
                || quest.RequiredProgress == 0
                || quest.Progress > quest.RequiredProgress
                || quest.Unavailable && (quest.Completed || quest.Progress != 0)
                || quest.Variable1 is < 0 or > MaximumNativeQuestByte
                || quest.Variable2 is < 0 or > MaximumNativeQuestByte
                || quest.MessageId is < 0 or > short.MaxValue
                || quest.TurnInTownerId is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInAlternateMessageId is < 0 or > short.MaxValue
                || quest.TurnInCompletionVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInCompletionRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInCompletionVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInRequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || quest.TurnInVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || !NativeTextLimits.IsIdentityWithinLimit(quest.CompletionUniqueIdentity)) {
                throw new InvalidDataException(
                    "Authoritative quest state is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedPortalStates(
        IReadOnlyList<DiabloPortalState> portals)
    {
        if (portals.Count > AuthoritativePortalRules.NativeMaximumPortalCount)
            throw new InvalidDataException(
                $"Authoritative portal state exceeds the native {AuthoritativePortalRules.NativeMaximumPortalCount}-portal capacity.");
        var portalIds = new HashSet<uint>();
        foreach (var portal in portals) {
            if (portal.PortalId == 0
                || !portalIds.Add(portal.PortalId)
                || portal.Bidirectional
                    && (portal.OwnerEntityId == 0 || portal.RemainingTicks == 0)
                || !portal.Bidirectional
                    && (portal.OwnerEntityId != 0 || portal.RemainingTicks != 0)) {
                throw new InvalidDataException(
                    "Authoritative portal state is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedWarpTargets(
        IReadOnlyList<AuthoritativeWarpTarget> warpTargets,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels,
        IReadOnlyList<DiabloQuestState> quests)
    {
        foreach (var group in warpTargets.GroupBy(warp => warp.SourceLevelId)) {
            if (group.Count() > NativeMaximumWarpTriggerCount)
                throw new InvalidDataException(
                    $"Authoritative warp targets contain more than {NativeMaximumWarpTriggerCount} "
                    + $"triggers on level {group.Key}, exceeding the native trigger capacity.");
        }
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        var warpIds = new HashSet<uint>();
        foreach (var warp in warpTargets) {
            var hasSourceLevel = levels.TryGetValue(warp.SourceLevelId, out var sourceLevel);
            if (warp.WarpId == 0
                || !warpIds.Add(warp.WarpId)
                || !hasSourceLevel
                || warp.TriggerX < 0
                || warp.TriggerY < 0
                || warp.TriggerX >= sourceLevel!.Width
                || warp.TriggerY >= sourceLevel.Height
                || !levels.TryGetValue(warp.TargetLevelId, out var targetLevel)
                || warp.TargetX < 0
                || warp.TargetY < 0
                || warp.TargetX >= targetLevel.Width
                || warp.TargetY >= targetLevel.Height
                || warp.RequiredQuestId != 0
                    && !questIds.Contains(warp.RequiredQuestId)
                || warp.RequiredQuestId == 0 && warp.RequiredQuestCompleted
                || warp.StableSymbol.Any(character => character is < (char)0x21 or > (char)0x7e)) {
                throw new InvalidDataException(
                    "Authoritative warp target is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedQuestTownerInteractions(
        IReadOnlyList<AuthoritativeQuestTownerInteractionDefinition> interactions,
        IReadOnlyList<DiabloQuestState> quests,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels)
    {
        if (interactions.Count > MaximumQuestTownerInteractionCount) {
            throw new InvalidDataException(
                "Authoritative quest/towner interactions exceed the shared protocol limit.");
        }

        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        var interactionIds = new HashSet<uint>();
        foreach (var interaction in interactions) {
            var hasLevel = levels.TryGetValue(interaction.LevelId, out var level);
            if (interaction.InteractionId == 0
                || !interactionIds.Add(interaction.InteractionId)
                || interaction.QuestId == 0
                || !questIds.Contains(interaction.QuestId)
                || levels.Count > 0
                    && (!hasLevel
                        || interaction.PositionX >= level!.Width
                        || interaction.PositionY >= level.Height)
                || interaction.TownerId is < 0 or > MaximumNativeQuestByte
                || interaction.LevelId > ushort.MaxValue
                || interaction.RequiredVisitedLevel1 > ushort.MaxValue
                || interaction.RequiredVisitedLevel2 > ushort.MaxValue
                || interaction.RequiredItemId > ushort.MaxValue
                || interaction.PositionX < 0
                || interaction.PositionY < 0
                || interaction.Kind is < AuthoritativeQuestTownerInteractionKind.Activate
                    or > AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest
                || interaction.MessageId is <= 0 or > short.MaxValue
                || interaction.RequiredVariable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || interaction.RequiredVariable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || interaction.Variable1 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte
                || interaction.Variable2 is < MinimumNativeOptionalQuestByte or > MaximumNativeQuestByte) {
                throw new InvalidDataException(
                    "Authoritative quest/towner interaction is outside the shared protocol range.");
            }
        }
    }

    private static void ValidateProjectedWorldMutationIds(
        IReadOnlyList<uint> mutationIds)
    {
        if (mutationIds.Count > DiabloWorldState.MaximumWorldStateEntries
            || mutationIds.Any(mutationId => mutationId == 0)
            || mutationIds.Distinct().Count() != mutationIds.Count) {
            throw new InvalidDataException(
                "Authoritative active world mutations contain invalid or duplicate identities.");
        }
    }

    private static void ValidateProjectedMaterializedProceduralLevelIds(
        IReadOnlyList<uint> levelIds)
    {
        if (levelIds.Count > DiabloWorldState.MaximumWorldStateEntries
            || levelIds.Distinct().Count() != levelIds.Count) {
            throw new InvalidDataException(
                "Authoritative materialized procedural levels contain duplicate identities.");
        }
    }

    private static void ValidateProjectedMaterializedProceduralLevelIds(
        Google.Protobuf.Collections.RepeatedField<uint> levelIds)
    {
        if (levelIds.Count > DiabloWorldState.MaximumWorldStateEntries
            || levelIds.Distinct().Count() != levelIds.Count) {
            throw new InvalidDataException(
                "Authoritative materialized procedural levels contain duplicate identities.");
        }
    }

    private static void ValidateProjectedSetLevelObjectOrigins(
        IReadOnlyList<DiabloSetLevelObjectOriginState> origins,
        IReadOnlyDictionary<uint, AuthoritativeLevel>? levels = null)
    {
        if (origins.Count > DiabloWorldState.MaximumWorldStateEntries
            || origins.Any(origin => origin.SetLevelId is < 0 or > AuthoritativeProtocolLimits.MaximumNativeSetLevelId)
            || origins.Select(origin => origin.SetLevelId).Distinct().Count() != origins.Count) {
            throw new InvalidDataException(
                "Authoritative set-level object origins contain invalid or duplicate identities.");
        }
        if (levels is null)
            return;
        foreach (var origin in origins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height) {
                throw new InvalidDataException(
                    $"Authoritative set-level object origin {origin.SetLevelId} is outside projected level {origin.LevelId}.");
            }
        }
    }

    private static void ValidateProjectedSetLevelObjectOrigins(
        Google.Protobuf.Collections.RepeatedField<SetLevelObjectOriginSnapshot> origins,
        IReadOnlyDictionary<uint, LevelGeometrySnapshot>? levels = null)
    {
        if (origins.Count > DiabloWorldState.MaximumWorldStateEntries
            || origins.Any(origin => origin.SetLevelId is < 0 or > AuthoritativeProtocolLimits.MaximumNativeSetLevelId)
            || origins.Select(origin => origin.SetLevelId).Distinct().Count() != origins.Count) {
            throw new InvalidDataException(
                "Authoritative set-level object origins contain invalid or duplicate identities.");
        }
        if (levels is null)
            return;
        foreach (var origin in origins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || level.Width <= 0
                || level.Height <= 0
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height) {
                throw new InvalidDataException(
                    $"Authoritative set-level object origin {origin.SetLevelId} is outside projected level {origin.LevelId}.");
            }
        }
    }

    private static void ValidateProjectedActiveStoreSlots(
        IEnumerable<uint> storeSlots)
    {
        var uniqueSlots = new HashSet<uint>();
        foreach (var storeSlot in storeSlots) {
            if (!uniqueSlots.Add(storeSlot)) {
                throw new InvalidDataException(
                    "Authoritative active-store stock contains duplicate slots.");
            }
        }
    }

    private static void ValidateProjectedEntityPosition(
        uint levelId,
        int positionX,
        int positionY,
        IReadOnlyDictionary<uint, AuthoritativeLevel> levels,
        string entityKind)
    {
        if (!AuthoritativeProtocolLimits.IsNativeTileCoordinate(
                positionX,
                positionY)
            || levels.TryGetValue(levelId, out var level)
                && (positionX >= level.Width || positionY >= level.Height)) {
            throw new InvalidDataException(
                $"Authoritative {entityKind} position is outside the projected level geometry.");
        }
    }

}
