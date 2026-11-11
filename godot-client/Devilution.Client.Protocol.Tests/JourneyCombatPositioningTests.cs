using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneyCombatPositioningTests
{
    [Fact]
    public void ThirteenMonsterSwarmFallsBackToVisitedClearedChokeRatherThanUnexploredRoom()
    {
        var positioning = new JourneyCombatPositioning();
        var state = new JourneyCombatSafety.Observation(1, 6, 100, 9000, 11776, 0, 0, 0, true, true, 200);
        positioning.Observe(state, 3, 4);
        var player = new PlayerSnapshot { EntityId = 1, LevelId = 6, PositionX = 4, PositionY = 4 };
        var geometry = new LevelGeometrySnapshot { LevelId = 6, Width = 12, Height = 12 };
        geometry.BlockedCells.Add(new[] { 3 * 12 + 3, 5 * 12 + 3 }); // Narrow ground behind the player.
        var snapshot = new Snapshot { Players = { player }, LevelGeometry = { geometry } };
        for (uint monster = 0; monster < 13; monster++) snapshot.Monsters.Add(new MonsterSnapshot {
            EntityId = monster + 2, LevelId = 6, Alive = true, PositionX = 7 + (int)monster % 3, PositionY = 3 + (int)monster / 3,
        });
        state = state with { Nearby = 13 };
        positioning.Observe(state, 4, 4);
        Assert.True(positioning.ShouldHold(state));
        Assert.False(new JourneyCombatSafety().Observe(state).Retreat);
        bool Open(int x, int y) => x >= 0 && y >= 0 && x < 12 && y < 12 && !geometry.BlockedCells.Contains(y * 12 + x);
        Assert.Equal((3, 4), positioning.CoverStep(snapshot, player, geometry, Open));
        player.PositionX = 3;
        // Holding this choke beats reversing direction back into the thirteen monsters.
        Assert.Null(positioning.CoverStep(snapshot, player, geometry, Open));
    }

    [Fact]
    public void CoverNeverCutsABlockedDiagonalOrStepsOntoAStairTrigger()
    {
        var positioning = new JourneyCombatPositioning();
        var state = new JourneyCombatSafety.Observation(1, 4, 100, 3000, 10000, 0, 0, 0, true, true);
        positioning.Observe(state, 1, 1);
        var player = new PlayerSnapshot { EntityId = 1, LevelId = 4, PositionX = 2, PositionY = 2 };
        var geometry = new LevelGeometrySnapshot { LevelId = 4, Width = 5, Height = 5 };
        var snapshot = new Snapshot { Players = { player }, Monsters = {
            new MonsterSnapshot { EntityId = 2, LevelId = 4, Alive = true, PositionX = 3, PositionY = 2 },
        } };
        Assert.Null(positioning.CoverStep(snapshot, player, geometry, (x, y) => !(x == 1 && y == 2)));
        geometry.WarpTriggers.Add(new WarpTriggerSnapshot { TriggerX = 1, TriggerY = 1 });
        Assert.Null(positioning.CoverStep(snapshot, player, geometry, (_, _) => true));
        positioning.Reset();
        geometry.WarpTriggers.Clear();
        Assert.Null(positioning.CoverStep(snapshot, player, geometry, (_, _) => true));
    }

    private static JourneyCombatSafety.Observation ArcherState()
        => new(1, 6, 100, 9000, 11776, 3, 0, 0, true, true, 200);

    [Fact]
    public void StationaryArchersCannotConsumeTheMovementBudgetWithoutTryingAClearPath()
    {
        var positioning = new JourneyCombatPositioning();
        var state = ArcherState();
        var snapshot = new Snapshot { Tick = state.Tick,
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 6, PositionX = 2, PositionY = 4, Life = state.Life, LifeMaximum = state.MaximumLife } },
            LevelGeometry = { new LevelGeometrySnapshot { LevelId = 6, Width = 12, Height = 12 } },
        };
        for (uint index = 0; index < 3; index++) snapshot.Monsters.Add(new MonsterSnapshot {
            EntityId = index + 2, LevelId = 6, Alive = true, PositionX = 7, PositionY = (int)index + 3,
            HitPoints = 64, MaxHitPoints = 64, HitPointsQ6 = 4096, MaxHitPointsQ6 = 4096,
        });
        var model = new AuthoritativeClientModel();
        var pauses = 0; var moves = 0;
        // Even a search with just one movement remaining gets its real collision/pathfinding attempt.
        for (var work = 0; work < 4 && moves == 0; work++) {
            positioning.Observe(state, 2, 4);
            snapshot.Tick = state.Tick;
            model.Apply(new(Snapshot: snapshot));
            if (positioning.ShouldHold(state)) {
                pauses++;
                state = state with { Tick = state.Tick + 5 };
                continue;
            }
            model.TrackPredictedMoveTo(1, 6, 4);
            moves++;
        }
        Assert.Equal(2, pauses);
        Assert.Equal(1, moves);
        Assert.Equal(new ClientPosition(3, 4), model.LocalMovementTarget);
        // Re-entering the melee helper cannot replenish holding for the same stationary group.
        for (var retry = 0; retry < 20; retry++) {
            state = state with { Tick = state.Tick + 5 };
            positioning.Observe(state, 2, 4);
            Assert.False(positioning.ShouldHold(state));
        }
    }

    [Fact]
    public void EnemiesThatStopClosingReleaseApproachAfterOneHold()
    {
        var positioning = new JourneyCombatPositioning(); var state = ArcherState();
        Assert.True(positioning.ShouldHold(state, 5));
        state = state with { Tick = 105 };
        Assert.False(positioning.ShouldHold(state, 5));
        Assert.False(positioning.ShouldHold(state with { Tick = 110 }, 4));
    }

    [Fact]
    public void ClosingMeleeGetsOnlyTwoPositioningOpportunities()
    {
        var positioning = new JourneyCombatPositioning(); var state = ArcherState();
        Assert.True(positioning.ShouldHold(state, 6));
        Assert.True(positioning.ShouldHold(state with { Tick = 105 }, 5));
        Assert.False(positioning.ShouldHold(state with { Tick = 110 }, 4));
    }

    [Fact]
    public void DamageFromRangeReleasesApproachEvenWhenEnemiesAreStillClosing()
    {
        var positioning = new JourneyCombatPositioning(); var state = ArcherState();
        Assert.True(positioning.ShouldHold(state, 5));
        Assert.False(positioning.ShouldHold(state with { Tick = 105, Life = 8500 }, 4));
    }

    [Theory]
    [InlineData(2, 0, 6U)]
    [InlineData(3, 1, 6U)]
    [InlineData(0, 0, 0U)]
    public void OnlyEndingTheSwarmOrPreparingInTownRearmsHolding(int nearby, int potions, uint level)
    {
        var positioning = new JourneyCombatPositioning(); var state = ArcherState();
        Assert.True(positioning.ShouldHold(state));
        Assert.True(positioning.ShouldHold(state));
        Assert.False(positioning.ShouldHold(state));
        positioning.Observe(state with { Tick = 105, Nearby = nearby, HealingItems = potions, Level = level }, 2, 4);
        state = state with { Tick = 110 };
        positioning.Observe(state, 2, 4);
        Assert.True(positioning.ShouldHold(state));
    }
}
