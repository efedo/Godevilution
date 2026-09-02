using Devilution.Protocol.V1;
using Devilution.Protocol;
using Devilution.Domain.Protocol;
using System.Collections.Generic;

namespace Devilution.Server.Protocol;

/** Validates adapter-produced snapshots before they cross the wire. */
public static class AuthoritativeSnapshotValidator
{
    public const int Sha256HexLength = 64;
    private const int NativeMaximumPlayerCount = 4;
    private const int NativeMaximumMonsterCount = 200;
    private const int NativeMaximumObjectCount = 127;
    private const int NativeMaximumItemCount = 127;
    private const int NativeMaximumWarpTriggerCount = 7;
    private const int NativeMaximumPortalCount = 4;
    private const int NativeMaximumWidth = 112;
    private const int NativeMaximumHeight = 112;
    private const int NativeMaximumMegaWidth = 40;
    private const int NativeMaximumMegaHeight = 40;
    private const uint NativeMaximumDunObjectLayerId = 146;
    private const uint NativeMaximumDunPieceId = 1378;
    private const uint NativeMaximumQuestId = 23;
    private const int MaximumQuestCount = 4096;
    private const int MaximumQuestTownerInteractionCount = 1024;
    private const int MaximumSetLevelObjectOriginCount = 4096;
    private const int MaximumMaterializedProceduralLevelCount = 4096;
    private const int MaximumWorldMutationCount = 4096;

    public static void Validate(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSessionMetadata(snapshot);
        ValidatePlayerNames(snapshot);
        ValidatePlayerPoolCapacity(snapshot);
        ValidatePlayerNativeRanges(snapshot);
        ValidateQuests(snapshot);
        ValidateQuestTownerInteractions(snapshot);
        ValidateLevelGeometry(snapshot);
        ValidateSetLevelObjectOrigins(snapshot);
        ValidateMaterializedProceduralLevels(snapshot);
        ValidateEntityPositions(snapshot);
        ValidateMonsterMovementTiming(snapshot);
        ValidateEntityIds(snapshot);
        ValidateMonsterPoolCapacity(snapshot);
        ValidateObjectPoolCapacity(snapshot);
        ValidateWorldItemPoolCapacity(snapshot);
        if (snapshot.StateSha256.Length == 0 || IsSha256Hex(snapshot.StateSha256))
            return;

        throw new InvalidDataException(
            "The authoritative snapshot contains an invalid SHA-256 state hash.");
    }

    private static void ValidatePlayerNames(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            if (!AuthoritativePlayerNameRules.IsValidShape(player.CharacterName)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid character name.");
            }
        }
    }

    private static void ValidatePlayerNativeRanges(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            if (!AuthoritativeLevelIdRules.IsSupported(player.LevelId)
                || player.VisitedLevelIds.Count > 256
                || player.VisitedLevelIds.Any(levelId => !AuthoritativeLevelIdRules.IsRegularLevel(levelId))
                || player.VisitedLevelIds.Distinct().Count() != player.VisitedLevelIds.Count
                || player.VisitedLevelIds.Count > 0
                    && AuthoritativeLevelIdRules.IsRegularLevel(player.LevelId)
                    && !player.VisitedLevelIds.Contains(player.LevelId)
                || !Enum.IsDefined(player.FacingDirection)
                || !Enum.IsDefined(player.CombatMode)
                || !Enum.IsDefined(player.AbilityKind)
                || !Enum.IsDefined(player.AbilityTargetingKind)
                || player.DiabloKillLevel > byte.MaxValue)
                throw new InvalidDataException(
                    "The authoritative snapshot contains player state outside the native range.");
        }
    }

    private static void ValidateSessionMetadata(Snapshot snapshot)
    {
        if (!Enum.IsDefined(snapshot.Difficulty))
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid game difficulty.");

        var mutationIds = new HashSet<uint>();
        if (snapshot.ActiveWorldMutationIds.Count > MaximumWorldMutationCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many active world mutations.");
        foreach (var mutationId in snapshot.ActiveWorldMutationIds) {
            if (mutationId == 0 || !mutationIds.Add(mutationId))
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate world-mutation identities.");
        }

        var portalIds = new HashSet<uint>();
        if (snapshot.Portals.Count > NativeMaximumPortalCount)
            throw new InvalidDataException(
                $"The authoritative snapshot contains more than {NativeMaximumPortalCount} portals, exceeding the native portal capacity.");
        foreach (var portal in snapshot.Portals) {
            if (portal.PortalId == 0
                || !portalIds.Add(portal.PortalId)
                || portal.Bidirectional
                    && (portal.OwnerEntityId == 0 || portal.RemainingTicks == 0)
                || !portal.Bidirectional
                    && (portal.OwnerEntityId != 0 || portal.RemainingTicks != 0)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid portal route state.");
            }
        }
    }

    private static void ValidateEntityIds(Snapshot snapshot)
    {
        var entityIds = new HashSet<uint>();
        foreach (var player in snapshot.Players)
            ValidateEntityId(player.EntityId, entityIds);
        foreach (var monster in snapshot.Monsters)
            ValidateEntityId(monster.EntityId, entityIds);
        foreach (var worldItem in snapshot.WorldItems)
            ValidateEntityId(worldItem.EntityId, entityIds);
        foreach (var @object in snapshot.Objects)
            ValidateEntityId(@object.EntityId, entityIds);
        foreach (var projectile in snapshot.Projectiles)
            ValidateEntityId(projectile.EntityId, entityIds);
        foreach (var corpse in snapshot.Corpses)
            ValidateEntityId(corpse.EntityId, entityIds);
    }

    private static void ValidatePlayerPoolCapacity(Snapshot snapshot)
    {
        if (snapshot.Players.Count > NativeMaximumPlayerCount)
            throw new InvalidDataException(
                $"The authoritative snapshot contains more than {NativeMaximumPlayerCount} "
                + "players, exceeding the native player pool capacity.");
    }

    private static void ValidateQuests(Snapshot snapshot)
    {
        if (snapshot.Quests.Count > MaximumQuestCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quests.");

        var questIds = new HashSet<uint>();
        var nativeQuestIds = new HashSet<uint>();
        foreach (var quest in snapshot.Quests) {
            if (quest.QuestId == 0
                || !questIds.Add(quest.QuestId)
                || quest.HasNativeQuestId
                    && (quest.NativeQuestId > NativeMaximumQuestId
                        || !nativeQuestIds.Add(quest.NativeQuestId))
                || quest.RequiredProgress == 0
                || quest.Progress > quest.RequiredProgress
                || quest.Unavailable && (quest.Completed || quest.Progress != 0)
                || quest.Variable1 is < 0 or > byte.MaxValue
                || quest.Variable2 is < 0 or > byte.MaxValue
                || quest.MessageId is < 0 or > short.MaxValue
                || quest.TurnInTownerId is < -1 or > byte.MaxValue
                || quest.TurnInAlternateMessageId is < 0 or > short.MaxValue
                || quest.TurnInCompletionVariable2 is < -1 or > byte.MaxValue
                || quest.TurnInCompletionRequiredVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInCompletionVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInRequiredVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInVariable1 is < -1 or > byte.MaxValue
                || !NativeTextLimits.IsIdentityWithinLimit(quest.CompletionUniqueIdentity)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid quest state.");
            }
        }
    }

    private static void ValidateQuestTownerInteractions(Snapshot snapshot)
    {
        if (snapshot.QuestTownerInteractions.Count > MaximumQuestTownerInteractionCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quest/towner interactions.");

        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            if (!levels.TryAdd(level.LevelId, level))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
        }

        var interactionIds = new HashSet<uint>();
        foreach (var interaction in snapshot.QuestTownerInteractions) {
            if (interaction.InteractionId == 0
                || !interactionIds.Add(interaction.InteractionId)
                || interaction.QuestId == 0
                || !questIds.Contains(interaction.QuestId)
                || levels.Count > 0
                    && (!levels.TryGetValue(interaction.LevelId, out var level)
                        || level.Width > 0 && interaction.PositionX >= level.Width
                        || level.Height > 0 && interaction.PositionY >= level.Height)
                || interaction.TownerId is < 0 or > byte.MaxValue
                || interaction.LevelId > ushort.MaxValue
                || interaction.RequiredVisitedLevel1 > ushort.MaxValue
                || interaction.RequiredVisitedLevel2 > ushort.MaxValue
                || interaction.RequiredItemId > ushort.MaxValue
                || interaction.PositionX < 0
                || interaction.PositionY < 0
                || interaction.Kind is < 0 or > 4
                || !interaction.HasMessageId
                || interaction.MessageId is <= 0 or > short.MaxValue
                || interaction.HasRequiredVariable1
                    && interaction.RequiredVariable1 is < 0 or > byte.MaxValue
                || interaction.HasRequiredVariable2
                    && interaction.RequiredVariable2 is < 0 or > byte.MaxValue
                || interaction.HasVariable1
                    && interaction.Variable1 is < 0 or > byte.MaxValue
                || interaction.HasVariable2
                    && interaction.Variable2 is < 0 or > byte.MaxValue) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid quest/towner interaction.");
            }
        }
    }

    private static void ValidateMonsterPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.Monsters.GroupBy(monster => monster.LevelId)) {
            if (group.Count() > NativeMaximumMonsterCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumMonsterCount} "
                    + $"monsters on level {group.Key}, exceeding the native monster capacity.");
        }
    }

    private static void ValidateObjectPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.Objects.GroupBy(@object => @object.LevelId)) {
            if (group.Count() > NativeMaximumObjectCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumObjectCount} "
                    + $"objects on level {group.Key}, exceeding the native object capacity.");
        }
    }

    private static void ValidateWorldItemPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.WorldItems.GroupBy(item => item.LevelId)) {
            if (group.Count() > NativeMaximumItemCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumItemCount} "
                    + $"world items on level {group.Key}, exceeding the native item capacity.");
        }
    }

    private static void ValidateLevelGeometry(Snapshot snapshot)
    {
        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            var coarseWidth = (level.Width + 1L) / 2;
            var coarseHeight = (level.Height + 1L) / 2;
            if (level.Width < 0
                || level.Height < 0
                || level.Width > NativeMaximumWidth
                || level.Height > NativeMaximumHeight
                || level.NativeTransparency.Any(value => value > byte.MaxValue)
                || level.NativeObjectLayer.Any(value => value > NativeMaximumDunObjectLayerId)
                || level.NativeMegaTileLayer.Any(value => value > byte.MaxValue)
                || level.NativePieceLayer.Any(value => value > NativeMaximumDunPieceId)
                || level.NativeMegaTileLayer.Count != 0
                    && (coarseWidth > NativeMaximumMegaWidth
                        || coarseHeight > NativeMaximumMegaHeight))
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid level geometry dimensions.");

            if (level.Width > 0 && level.Height > 0) {
                var cellCount = checked((long)level.Width * level.Height);
                if (!AreValidCells(level.BlockedCells, cellCount)
                    || !AreValidCells(level.MissileBlockedCells, cellCount)
                    || !AreValidCells(level.NativeVisionBlockedCells, cellCount)
                    || level.NativeTransparency.Count != 0
                        && level.NativeTransparency.Count != cellCount
                    || level.NativeObjectLayer.Count != 0
                        && level.NativeObjectLayer.Count != cellCount
                    || level.NativeMegaTileLayer.Count != 0
                        && level.NativeMegaTileLayer.Count != coarseWidth * coarseHeight
                    || level.NativePieceLayer.Count != 0
                        && level.NativePieceLayer.Count != cellCount)
                    throw new InvalidDataException(
                        "The authoritative snapshot contains invalid level geometry cells.");
            }

            if (!levels.TryAdd(level.LevelId, level))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
        }

        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        var warpIds = new HashSet<uint>();
        foreach (var level in snapshot.LevelGeometry) {
            if (level.WarpTriggers.Count > NativeMaximumWarpTriggerCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumWarpTriggerCount} "
                    + $"warp triggers on level {level.LevelId}, exceeding the native trigger capacity.");

            foreach (var warp in level.WarpTriggers) {
                var hasTargetLevel = levels.TryGetValue(warp.TargetLevelId, out var targetLevel);
                if (warp.WarpId == 0
                    || !warpIds.Add(warp.WarpId)
                    || warp.TriggerX < 0
                    || warp.TriggerY < 0
                    || level.Width > 0 && warp.TriggerX >= level.Width
                    || level.Height > 0 && warp.TriggerY >= level.Height
                    || !hasTargetLevel
                    || warp.TargetX < 0
                    || warp.TargetY < 0
                    || targetLevel!.Width > 0 && warp.TargetX >= targetLevel.Width
                    || targetLevel.Height > 0 && warp.TargetY >= targetLevel.Height
                    || warp.RequiredQuestId != 0
                        && !questIds.Contains(warp.RequiredQuestId)
                    || warp.RequiredQuestId == 0 && warp.RequiredQuestCompleted
                    || warp.RequiredQuestId == 0 && !warp.Available
                    || warp.StableSymbol.Any(character => character is < (char)0x21 or > (char)0x7e))
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid warp trigger.");
            }
        }
    }

    private static bool AreValidCells(IList<int> cells, long cellCount)
    {
        if (cells.Count > cellCount)
            return false;

        var uniqueCells = new HashSet<int>();
        foreach (var cell in cells) {
            if (cell < 0 || cell >= cellCount || !uniqueCells.Add(cell))
                return false;
        }
        return true;
    }

    private static void ValidateSetLevelObjectOrigins(Snapshot snapshot)
    {
        if (snapshot.SetLevelObjectOrigins.Count > MaximumSetLevelObjectOriginCount
            || snapshot.SetLevelObjectOrigins.Any(origin => origin.SetLevelId is < 0 or > 8)
            || snapshot.SetLevelObjectOrigins
                .Select(origin => origin.SetLevelId)
                .Distinct()
                .Count() != snapshot.SetLevelObjectOrigins.Count)
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid or duplicate set-level object origins.");
        if (snapshot.LevelGeometry.Count == 0)
            return;

        var levels = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        foreach (var origin in snapshot.SetLevelObjectOrigins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains a set-level object origin outside level {origin.LevelId}.");
        }
    }

    private static void ValidateMaterializedProceduralLevels(Snapshot snapshot)
    {
        if (snapshot.MaterializedProceduralLevelIds.Count > MaximumMaterializedProceduralLevelCount
            || snapshot.MaterializedProceduralLevelIds
                .Distinct()
                .Count() != snapshot.MaterializedProceduralLevelIds.Count)
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate materialized procedural levels.");
    }

    private static void ValidateEntityPositions(Snapshot snapshot)
    {
        var levels = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        bool IsNativePosition(int x, int y)
            => x is >= 0 and <= byte.MaxValue
                && y is >= 0 and <= byte.MaxValue;

        bool IsInBounds(uint levelId, int x, int y)
        {
            if (!IsNativePosition(x, y))
                return false;
            if (!levels.TryGetValue(levelId, out var level)
                || level.Width == 0
                || level.Height == 0)
                return true;
            return x < level.Width && y < level.Height;
        }

        bool IsValidMonster(MonsterSnapshot monster)
            => IsInBounds(monster.LevelId, monster.PositionX, monster.PositionY)
                && (!monster.Moving
                    || IsInBounds(
                        monster.LevelId,
                        monster.FuturePositionX,
                        monster.FuturePositionY));

        if (snapshot.Players.Any(player =>
                !IsInBounds(player.LevelId, player.PositionX, player.PositionY))
            || snapshot.Monsters.Any(monster => !IsValidMonster(monster))
            || snapshot.WorldItems.Any(item =>
                !IsInBounds(item.LevelId, item.PositionX, item.PositionY))
            || snapshot.Objects.Any(@object =>
                !IsInBounds(@object.LevelId, @object.PositionX, @object.PositionY))
            || snapshot.Projectiles.Any(projectile =>
                !IsInBounds(projectile.LevelId, projectile.PositionX, projectile.PositionY))
            || snapshot.Corpses.Any(corpse =>
                !IsInBounds(corpse.LevelId, corpse.PositionX, corpse.PositionY))
            || snapshot.Portals.Any(portal =>
                !IsInBounds(
                    portal.SourceLevelId,
                    portal.SourcePositionX,
                    portal.SourcePositionY)
                || !IsInBounds(
                    portal.DestinationLevelId,
                    portal.DestinationPositionX,
                    portal.DestinationPositionY)))
            throw new InvalidDataException(
                "The authoritative snapshot contains an entity outside projected level geometry.");
    }

    private static void ValidateMonsterMovementTiming(Snapshot snapshot)
    {
        foreach (var monster in snapshot.Monsters) {
            if ((!monster.Moving
                    && (monster.MovementStartedTick != 0
                        || monster.MovementEffectTick != 0))
                || (monster.MovementStartedTick != 0
                    || monster.MovementEffectTick != 0)
                    && monster.MovementEffectTick <= monster.MovementStartedTick) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid monster movement timing.");
            }
        }
    }

    private static void ValidateEntityId(uint entityId, HashSet<uint> entityIds)
    {
        if (entityId == 0 || !entityIds.Add(entityId))
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate or zero entity IDs.");
    }

    private static bool IsSha256Hex(string value)
    {
        if (value.Length != Sha256HexLength)
            return false;

        foreach (var character in value) {
            if (!((character >= '0' && character <= '9')
                  || (character >= 'a' && character <= 'f')
                  || (character >= 'A' && character <= 'F')))
                return false;
        }
        return true;
    }
}
