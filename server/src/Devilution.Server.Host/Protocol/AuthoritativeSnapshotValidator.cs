using Devilution.Protocol.V1;
using Devilution.Protocol;
using System.Collections.Generic;

namespace Devilution.Server.Protocol;

/** Validates adapter-produced snapshots before they cross the wire. */
public static class AuthoritativeSnapshotValidator
{
    public const int Sha256HexLength = 64;
    private const int NativeMaximumPortalCount = 4;
    private const int MaximumWorldMutationCount = 4096;

    public static void Validate(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSessionMetadata(snapshot);
        ValidatePlayerNames(snapshot);
        ValidatePlayerNativeRanges(snapshot);
        ValidateMonsterMovementTiming(snapshot);
        ValidateEntityIds(snapshot);
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
