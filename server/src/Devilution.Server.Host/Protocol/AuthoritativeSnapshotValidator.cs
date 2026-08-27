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
        ValidateEntityIds(snapshot);
        if (snapshot.StateSha256.Length == 0 || IsSha256Hex(snapshot.StateSha256))
            return;

        throw new InvalidDataException(
            "The authoritative snapshot contains an invalid SHA-256 state hash.");
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
