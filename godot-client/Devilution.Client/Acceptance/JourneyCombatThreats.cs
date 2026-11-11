using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Recent authoritative hits and incoming missiles identify shooters beyond melee or vision range. */
internal sealed class JourneyCombatThreats
{
    private const ulong DamageMemoryTicks = 80;
    internal readonly record struct Counts(int Nearby, int Adjacent, int Ranged, uint RangedTarget);
    private readonly Dictionary<uint, ulong> sources = [];
    private readonly HashSet<uint> ranged = [];
    private uint actor, level;
    private ulong tick, eventSequence;
    private bool initialized;

    internal void Reset() { sources.Clear(); ranged.Clear(); initialized = false; eventSequence = 0; }
    internal bool IsRangedAttacker(uint entity) => ranged.Contains(entity);

    internal Counts Observe(Snapshot snapshot, PlayerSnapshot player, IReadOnlyList<GameEvent> events,
        ulong firstSequence, ulong lastSequence)
    {
        if (!initialized || actor != player.EntityId || level != player.LevelId || snapshot.Tick < tick
            || lastSequence < eventSequence) {
            sources.Clear();
            eventSequence = initialized ? lastSequence : firstSequence;
        }
        initialized = true; actor = player.EntityId; level = player.LevelId; tick = snapshot.Tick;
        var first = (int)Math.Min((ulong)events.Count, eventSequence > firstSequence ? eventSequence - firstSequence : 0);
        for (var index = first; index < events.Count; index++) {
            if (events[index].Damage is not { } hit || hit.TargetEntityId != actor || hit.Amount <= 0 && hit.AmountQ6 <= 0) continue;
            var source = snapshot.Monsters.FirstOrDefault(monster => monster.EntityId == hit.SourceEntityId);
            if (source is not null && (Distance(player, source) > 1 || sources.ContainsKey(source.EntityId)))
                sources[source.EntityId] = tick;
        }
        eventSequence = lastSequence;
        // Native C2 Arrow publishes zero generic damage; its live monster owns the impact roll.
        foreach (var missile in snapshot.Projectiles)
            if (missile.TargetEntityId == actor && AuthoritativeLevelVisibility.IsVisible(missile.LevelId, level)
                && missile.RemainingTicks > 0)
                sources[missile.SourceEntityId] = tick;
        ranged.Clear();
        if (level == 0) return default;
        var geometry = snapshot.LevelGeometry.FirstOrDefault(value => value.LevelId == level);
        var nearby = 0; var adjacent = 0; var distantShooters = 0;
        MonsterSnapshot? target = null; long targetDistance = long.MaxValue;
        foreach (var monster in snapshot.Monsters) {
            if (!monster.Alive || monster.IsPlayerMinion || !AuthoritativeLevelVisibility.IsVisible(monster.LevelId, level)) continue;
            var distance = Distance(player, monster);
            var shooting = sources.TryGetValue(monster.EntityId, out var seen) && tick >= seen && tick - seen <= DamageMemoryTicks;
            if (!shooting && (distance > 6 || distance > 1 && !AuthoritativeNativeVisionVisibility.IsVisible(
                    monster.LevelId, monster.PositionX, monster.PositionY, level, geometry,
                    player.PositionX, player.PositionY, player.LightRadius))) continue;
            nearby++;
            if (distance <= 1) adjacent++;
            if (!shooting) continue;
            ranged.Add(monster.EntityId);
            if (distance > 1) distantShooters++; // Adjacent shooters already contribute to the melee count.
            if (target is null || distance < targetDistance || distance == targetDistance
                && (monster.HitPointsQ6 < target.HitPointsQ6 || monster.HitPointsQ6 == target.HitPointsQ6 && monster.EntityId < target.EntityId)) {
                target = monster; targetDistance = distance;
            }
        }
        return new(nearby, adjacent, distantShooters, target?.EntityId ?? 0);
    }

    private static long Distance(PlayerSnapshot player, MonsterSnapshot monster)
    {
        var distance = Math.Max(Math.Abs((long)player.PositionX - monster.PositionX), Math.Abs((long)player.PositionY - monster.PositionY));
        return monster.Moving ? Math.Min(distance,
            Math.Max(Math.Abs((long)player.PositionX - monster.FuturePositionX), Math.Abs((long)player.PositionY - monster.FuturePositionY))) : distance;
    }
}
