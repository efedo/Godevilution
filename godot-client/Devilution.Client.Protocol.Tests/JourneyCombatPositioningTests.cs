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
}
