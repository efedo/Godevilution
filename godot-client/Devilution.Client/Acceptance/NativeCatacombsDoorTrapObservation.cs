using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** A live wall missile or its retained Fired link proves firing even when snapshot cadence skips the missile. */
internal static class NativeCatacombsDoorTrapObservation
{
    internal sealed record Firing(uint Level, uint DoorEntity, uint TrapEntity, ulong Tick);

    /** One observed closed-door baseline, bound later to that opening command's applied tick. */
    internal sealed class Opening
    {
        private readonly ObjectSnapshot door;
        private readonly ulong baselineTick;
        private readonly HashSet<uint> alreadyFired;

        internal Opening(Snapshot before, uint doorEntity)
        {
            door = before.Objects.Single(value => value.EntityId == doorEntity).Clone();
            baselineTick = before.Tick;
            alreadyFired = before.Objects.Where(value => value.NativeTrapFiredTriggerEntityId == doorEntity)
                .Select(value => value.EntityId).Concat(before.Projectiles.Where(value => value.SpellId == 0
                    && value.TargetEntityId == doorEntity).Select(value => value.SourceEntityId)).ToHashSet();
        }

        internal Firing? Find(Snapshot after, ulong openedTick, ulong deadlineTick)
        {
            if (door.MissilePassable || openedTick < baselineTick || deadlineTick < openedTick || after.Tick < openedTick)
                return null;
            var current = after.Objects.FirstOrDefault(value => value.EntityId == door.EntityId);
            if (current is null || !current.MissilePassable || current.LevelId != door.LevelId
                || current.PositionX != door.PositionX || current.PositionY != door.PositionY || current.NativeObjectId != door.NativeObjectId)
                return null;
            return NativeCatacombsDoorTrapObservation.Find(after).FirstOrDefault(value => value.DoorEntity == door.EntityId
                && !alreadyFired.Contains(value.TrapEntity) && value.Tick >= openedTick && value.Tick < deadlineTick
                && value.Tick <= after.Tick && after.Objects.Any(source => source.EntityId == value.TrapEntity
                    && source.NativeTrapFiredTriggerEntityId == door.EntityId && source.HasNativeTrapFiredTick));
        }
    }

    internal sealed record Opened(Opening Baseline, ulong AppliedTick, ulong DeadlineTick)
    {
        internal Firing? Find(Snapshot after) => Baseline.Find(after, AppliedTick, DeadlineTick);
    }

    internal static IEnumerable<Firing> Find(Snapshot snapshot)
    {
        var observed = new HashSet<(uint Source, uint Trigger)>();
        foreach (var source in snapshot.Objects.Where(value => value.NativeTrapFiredTriggerEntityId != 0 && value.HasNativeTrapFiredTick)) {
            var door = snapshot.Objects.FirstOrDefault(value => value.EntityId == source.NativeTrapFiredTriggerEntityId);
            if (source.NativeObjectId is 53 or 54 && source.LevelId is >= 5 and <= 8
                && door is { NativeObjectId: 42 or 43, MissilePassable: true } && door.LevelId == source.LevelId
                && observed.Add((source.EntityId, door.EntityId)))
                yield return new(source.LevelId, door.EntityId, source.EntityId, source.NativeTrapFiredTick);
        }
        foreach (var missile in snapshot.Projectiles) {
            if (missile.SpellId != 0 || missile.LevelId is < 5 or > 8)
                continue;
            var door = snapshot.Objects.FirstOrDefault(value => value.EntityId == missile.TargetEntityId);
            var source = snapshot.Objects.FirstOrDefault(value => value.EntityId == missile.SourceEntityId);
            if (door is { NativeObjectId: 42 or 43, MissilePassable: true }
                && door.LevelId == missile.LevelId && source is { NativeObjectId: 53 or 54 }
                && source.LevelId == missile.LevelId && observed.Add((source.EntityId, door.EntityId)))
                yield return new(missile.LevelId, door.EntityId, source.EntityId,
                    source.HasNativeTrapFiredTick ? source.NativeTrapFiredTick : snapshot.Tick);
        }
    }
}
