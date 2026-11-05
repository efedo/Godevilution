using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Warrior snapshots hide armed flags; an actual source-less wall missile proves a door fired. */
internal static class NativeCatacombsDoorTrapObservation
{
    internal sealed record Firing(uint Level, uint DoorEntity, uint TrapEntity, ulong Tick);

    internal static IEnumerable<Firing> Find(Snapshot snapshot)
    {
        foreach (var missile in snapshot.Projectiles) {
            if (missile.SpellId != 0 || missile.LevelId is < 5 or > 8)
                continue;
            var door = snapshot.Objects.FirstOrDefault(value => value.EntityId == missile.TargetEntityId);
            var source = snapshot.Objects.FirstOrDefault(value => value.EntityId == missile.SourceEntityId);
            if (door is { NativeObjectId: 42 or 43, MissilePassable: true }
                && door.LevelId == missile.LevelId && source is { NativeObjectId: 53 or 54 }
                && source.LevelId == missile.LevelId)
                yield return new(missile.LevelId, door.EntityId, source.EntityId, snapshot.Tick);
        }
    }
}
