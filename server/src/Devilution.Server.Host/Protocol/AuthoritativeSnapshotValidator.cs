using Devilution.Protocol.V1;
using System.Collections.Generic;

namespace Devilution.Server.Protocol;

/** Validates adapter-produced snapshots before they cross the wire. */
public static class AuthoritativeSnapshotValidator
{
    public const int Sha256HexLength = 64;

    public static void Validate(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSessionMetadata(snapshot);
        ValidatePlayerNames(snapshot);
        ValidateEntityIds(snapshot);
        if (snapshot.StateSha256.Length == 0 || IsSha256Hex(snapshot.StateSha256))
            return;

        throw new InvalidDataException(
            "The authoritative snapshot contains an invalid SHA-256 state hash.");
    }

    private static void ValidatePlayerNames(Snapshot snapshot)
    {
        foreach (var player in snapshot.Players) {
            if (player.CharacterName.Length > 31
                || player.CharacterName.Any(character => character is < (char)0x21 or > (char)0x7e)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid character name.");
            }
        }
    }

    private static void ValidateSessionMetadata(Snapshot snapshot)
    {
        if (!Enum.IsDefined(snapshot.Difficulty))
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid game difficulty.");

        var mutationIds = new HashSet<uint>();
        foreach (var mutationId in snapshot.ActiveWorldMutationIds) {
            if (mutationId == 0 || !mutationIds.Add(mutationId))
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate world-mutation identities.");
        }

        var portalIds = new HashSet<uint>();
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
