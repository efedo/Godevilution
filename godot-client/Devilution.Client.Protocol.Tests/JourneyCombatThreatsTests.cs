using Devilution.Client.Acceptance;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneyCombatThreatsTests
{
    internal static Snapshot RangedPack(int life = 3858)
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 6, Width = 112, Height = 112 };
        // Synthetic opaque vision, so a projected damage source must override the ordinary visibility filter.
        geometry.NativeVisionBlockedCells.Add(Enumerable.Range(0, 112 * 112));
        var snapshot = new Snapshot { Tick = 40000,
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 6, PositionX = 66, PositionY = 25,
                Life = life, LifeMaximum = 11776, LightRadius = 10, CombatMode = PlayerCombatMode.Stand } },
            LevelGeometry = { geometry } };
        var pack = new[] { (151326736U, 69, 29, 21), (151326759U, 70, 22, 21), (151326769U, 63, 21, 24),
            (151326779U, 70, 23, 23), (151326810U, 73, 26, 21), (151326811U, 74, 27, 20), (151326812U, 71, 29, 24) };
        foreach (var (id, x, y, hp) in pack) snapshot.Monsters.Add(new MonsterSnapshot {
            EntityId = id, LevelId = 6, PositionX = x, PositionY = y, Alive = true, MonsterId = 46,
            HitPoints = hp, MaxHitPoints = hp, HitPointsQ6 = hp * 64, MaxHitPointsQ6 = hp * 64,
        });
        return snapshot;
    }

    internal static GameEvent Hit(uint source, uint target = 1)
        => new() { Damage = new DamageEvent { SourceEntityId = source, TargetEntityId = target, AmountQ6 = 100 } };

    [Fact]
    public void CounterFireChoosesTheNearestReachableSourceInsteadOfAnIsolatedCloserShooter()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 6, Width = 10, Height = 10 };
        // A synthetic sealed cell holds the nearer shooter. The other has an ordinary open approach.
        foreach (var (x, y) in new[] { (3, 3), (4, 3), (5, 3), (3, 4), (5, 4), (3, 5), (4, 5), (5, 5) })
            geometry.BlockedCells.Add(y * 10 + x);
        var snapshot = new Snapshot { Tick = 100,
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 6, PositionX = 2, PositionY = 4,
                Life = 5190, LifeMaximum = 11776 } },
            LevelGeometry = { geometry },
            Monsters = {
                new MonsterSnapshot { EntityId = 10, LevelId = 6, Alive = true, PositionX = 4, PositionY = 4 },
                new MonsterSnapshot { EntityId = 20, LevelId = 6, Alive = true, PositionX = 7, PositionY = 4 },
            } };
        foreach (var monster in snapshot.Monsters) snapshot.Projectiles.Add(new ProjectileSnapshot {
            SourceEntityId = monster.EntityId, TargetEntityId = 1, LevelId = 6, RemainingTicks = 20, NativeMissileId = "Arrow" });
        var threats = new JourneyCombatThreats();
        Assert.Equal(10U, threats.Observe(snapshot, snapshot.Players[0], [], 0, 0).RangedTarget);
        bool Reachable(MonsterSnapshot monster) => NativeObjectApproachPath.Find(snapshot, geometry, new ObjectSnapshot {
            PositionX = monster.PositionX, PositionY = monster.PositionY, LevelId = 6 }, pickup: true) is not null;
        Assert.False(Reachable(snapshot.Monsters[0]));
        Assert.True(Reachable(snapshot.Monsters[1]));
        Assert.Equal(20U, threats.ClosestReachableShooter(snapshot, snapshot.Players[0], Reachable));
        Assert.Equal(0U, threats.ClosestReachableShooter(snapshot, snapshot.Players[0], _ => false));
    }

    [Theory]
    [InlineData(3858)]
    [InlineData(3128)]
    public void RecordedLowLifePackCountsItsRangedSourcesAndFightsInsteadOfRetreatingForever(int life)
    {
        var snapshot = RangedPack(life); var player = snapshot.Players[0];
        var events = snapshot.Monsters.Select(monster => Hit(monster.EntityId)).ToArray();
        var threats = new JourneyCombatThreats();
        Assert.Equal(default, threats.Observe(snapshot, player, [], 0, 0));
        var counts = threats.Observe(snapshot, player, events, 0, (ulong)events.Length);
        Assert.Equal(new JourneyCombatThreats.Counts(7, 0, 7, 151326736), counts);
        var state = new JourneyCombatSafety.Observation(1, 6, snapshot.Tick, life, 11776, counts.Nearby, counts.Adjacent,
            0, true, true, 200, RangedAttackers: counts.Ranged, RangedTarget: counts.RangedTarget);
        var policy = new JourneyCombatSafety(); var decision = policy.Observe(state);
        Assert.False(decision.Drink);
        Assert.False(decision.Retreat);
        Assert.True(decision.StopWalking);
        Assert.Equal(7, decision.Attackers);
        Assert.False(new JourneyCombatPositioning().BeginSearch().TryHold(state));
        Assert.True(threats.IsRangedAttacker(decision.RangedTarget));
    }

    [Fact]
    public async Task TravelDefendsTheDamagingShooterWithoutSpendingRetreatWorkOrOvertakingAPendingCommand()
    {
        var state = new JourneyCombatSafety.Observation(1, 6, 40000, 3858, 11776, 7, 0, 0, true, true,
            200, RangedAttackers: 7, RangedTarget: 151326736);
        var policy = new JourneyCombatSafety(); var fights = 0;
        Assert.True(await policy.CheckpointAsync(JourneySafetyActivity.Travel, () => state,
            () => throw new InvalidOperationException("No potion remains."), () => {
                Assert.False(policy.Observe(state).Retreat);
                Assert.Equal(151326736U, policy.Observe(state).RangedTarget);
                fights++;
                state = state with { Nearby = 0, RangedAttackers = 0, RangedTarget = 0 };
                return Task.CompletedTask;
            }));
        Assert.Equal(1, fights);
        state = state with { CommandsAdmitted = false, Nearby = 7, RangedAttackers = 7, RangedTarget = 151326736 };
        Assert.False(await policy.CheckpointAsync(JourneySafetyActivity.Combat, () => state,
            () => throw new InvalidOperationException("Pending receipt."),
            () => throw new InvalidOperationException("Pending receipt.")));
        Assert.True(policy.Observe(state with { CommandsAdmitted = true, Adjacent = 3 }).Retreat);
    }

    [Fact]
    public void IncomingDynamicDamageArrowAndItsLaterHitCountTheSourceOnce()
    {
        var snapshot = RangedPack(); var player = snapshot.Players[0]; var source = snapshot.Monsters[0];
        snapshot.Projectiles.Add(new ProjectileSnapshot { EntityId = 800, SourceEntityId = source.EntityId,
            TargetEntityId = 1, LevelId = 6, RemainingTicks = 20, NativeMissileId = "Arrow" });
        var threats = new JourneyCombatThreats();
        Assert.Equal(1, threats.Observe(snapshot, player, [], 0, 0).Ranged);
        Assert.Equal(1, threats.Observe(snapshot, player, [Hit(source.EntityId)], 0, 1).Ranged);
        Assert.Equal(source.EntityId, threats.Observe(snapshot, player, [Hit(source.EntityId)], 0, 1).RangedTarget);
    }

    [Fact]
    public void ClosingOnTheShooterDoesNotRetreatBeforeTheFirstMeleeStrikeOrDoubleCountIt()
    {
        var snapshot = RangedPack(); var player = snapshot.Players[0]; var shooter = snapshot.Monsters[0];
        snapshot.Monsters.Clear(); snapshot.Monsters.Add(shooter);
        var events = new[] { Hit(shooter.EntityId) }; var threats = new JourneyCombatThreats();
        Assert.Equal(1, threats.Observe(snapshot, player, events, 0, 1).Ranged);
        player.PositionX = shooter.PositionX - 1; player.PositionY = shooter.PositionY; snapshot.Tick++;
        var counts = threats.Observe(snapshot, player, events, 0, 1);
        Assert.Equal(new JourneyCombatThreats.Counts(1, 1, 0, shooter.EntityId), counts);
        var decision = new JourneyCombatSafety().Observe(new(1, 6, snapshot.Tick, player.Life, player.LifeMaximum,
            counts.Nearby, counts.Adjacent, 0, true, true, 200, RangedAttackers: counts.Ranged, RangedTarget: counts.RangedTarget));
        Assert.False(decision.Retreat);
        Assert.Equal(1, decision.Attackers);
        Assert.True(threats.IsRangedAttacker(shooter.EntityId));
    }

    [Fact]
    public void AnOrdinaryMeleeHitDoesNotInventARangedTarget()
    {
        var snapshot = RangedPack(); var player = snapshot.Players[0]; var melee = snapshot.Monsters[0];
        snapshot.Monsters.Clear(); snapshot.Monsters.Add(melee);
        player.PositionX = melee.PositionX - 1; player.PositionY = melee.PositionY;
        var counts = new JourneyCombatThreats().Observe(snapshot, player, [Hit(melee.EntityId)], 0, 1);
        Assert.Equal(new JourneyCombatThreats.Counts(1, 1, 0, 0), counts);
    }

    [Fact]
    public void ACompletedKillDoesNotRequestRetreatFromHistoricalDamageAlone()
    {
        var snapshot = RangedPack(4500); var player = snapshot.Players[0]; var shooter = snapshot.Monsters[0];
        snapshot.Monsters.Clear(); snapshot.Monsters.Add(shooter);
        var threats = new JourneyCombatThreats(); var policy = new JourneyCombatSafety();
        var events = new[] { Hit(shooter.EntityId) };
        JourneyCombatSafety.Observation Observe()
        {
            var counts = threats.Observe(snapshot, player, events, 0, 1);
            return new(1, 6, snapshot.Tick, player.Life, player.LifeMaximum, counts.Nearby, counts.Adjacent,
                0, true, true, 200, RangedAttackers: counts.Ranged, RangedTarget: counts.RangedTarget);
        }
        policy.Observe(Observe());
        // The final hit and the shooter's death can publish together, before its corpse conversion.
        player.Life = 3858; shooter.Alive = false; snapshot.Tick++;
        var decision = policy.Observe(Observe());
        Assert.True(decision.LossPerTick > 0);
        Assert.Equal(0, decision.Attackers);
        Assert.False(decision.Retreat);
    }

    [Theory]
    [InlineData("other-victim")]
    [InlineData("expired")]
    [InlineData("other-level")]
    public void OnlyALiveIncomingMissileForThisPlayerIdentifiesARangedSource(string change)
    {
        var snapshot = RangedPack();
        var arrow = new ProjectileSnapshot { EntityId = 800, SourceEntityId = snapshot.Monsters[0].EntityId,
            TargetEntityId = 1, LevelId = 6, RemainingTicks = 20, NativeMissileId = "Arrow" };
        switch (change) {
        case "other-victim": arrow.TargetEntityId = 2; break;
        case "expired": arrow.RemainingTicks = 0; break;
        case "other-level": arrow.LevelId = 5; break;
        }
        snapshot.Projectiles.Add(arrow);
        Assert.Equal(0, new JourneyCombatThreats().Observe(snapshot, snapshot.Players[0], [], 0, 0).Ranged);
    }

    [Fact]
    public void RetainedOldEventsCannotRearmExpiredDamageAndNewEventsCan()
    {
        var snapshot = RangedPack(); var player = snapshot.Players[0]; var source = snapshot.Monsters[0].EntityId;
        var events = new[] { Hit(source) }; var threats = new JourneyCombatThreats();
        Assert.Equal(1, threats.Observe(snapshot, player, events, 0, 1).Ranged);
        snapshot.Tick += 80;
        Assert.Equal(1, threats.Observe(snapshot, player, events, 0, 1).Ranged);
        snapshot.Tick++;
        Assert.Equal(0, threats.Observe(snapshot, player, events, 0, 1).Ranged);
        Assert.Equal(1, threats.Observe(snapshot, player, [Hit(source)], 1, 2).Ranged);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("level")]
    [InlineData("rollback")]
    [InlineData("event-reset")]
    public void ChangedAuthorityContextDoesNotReuseOldDamageEvidence(string change)
    {
        var snapshot = RangedPack(); var player = snapshot.Players[0]; var events = new[] { Hit(snapshot.Monsters[0].EntityId) };
        var threats = new JourneyCombatThreats();
        Assert.Equal(1, threats.Observe(snapshot, player, events, 0, 1).Ranged);
        var end = 1UL;
        switch (change) {
        case "player": player.EntityId = 2; break;
        case "level": player.LevelId = 5; break;
        case "rollback": snapshot.Tick--; break;
        case "event-reset": end = 0; events = []; break;
        }
        Assert.Equal(0, threats.Observe(snapshot, player, events, 0, end).Ranged);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("minion")]
    [InlineData("off-level")]
    [InlineData("other-victim")]
    [InlineData("no-damage")]
    [InlineData("trap-source")]
    public void OnlyPublishedHostileDamageSourcesCountAsRangedAttackers(string change)
    {
        var snapshot = RangedPack(); var monster = snapshot.Monsters[0];
        var hit = Hit(monster.EntityId);
        switch (change) {
        case "dead": monster.Alive = false; break;
        case "minion": monster.IsPlayerMinion = true; break;
        case "off-level": monster.LevelId = 5; break;
        case "other-victim": hit.Damage.TargetEntityId = 2; break;
        case "no-damage": hit.Damage.AmountQ6 = 0; break;
        case "trap-source": hit.Damage.SourceEntityId = 9999; break;
        }
        Assert.Equal(0, new JourneyCombatThreats().Observe(snapshot, snapshot.Players[0], [hit], 0, 1).Ranged);
    }
}
