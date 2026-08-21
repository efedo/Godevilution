using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativePlayerPositionProjectionTests
{
    [Fact]
    public void UsesTheOccupiedTileWhenNoWalkIsInProgress()
    {
        var player = new PlayerSnapshot { PositionX = 4, PositionY = 5 };

        Assert.Equal(
            new ClientPosition(4, 5),
            AuthoritativePlayerPositionProjection.Resolve(player));
    }

    [Fact]
    public void UsesTheReservedDestinationDuringAnAuthoritativeWalk()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CombatMode = PlayerCombatMode.WalkSideways,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                DirectionX = -1,
                DirectionY = 1,
            },
        };

        Assert.Equal(
            new ClientPosition(3, 6),
            AuthoritativePlayerPositionProjection.Resolve(player));
    }

    [Fact]
    public void UsesTheOccupiedTileAfterTheAuthoritativeWalkEffectApplies()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CombatMode = PlayerCombatMode.WalkSideways,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                DirectionX = -1,
                DirectionY = 1,
                EffectApplied = true,
            },
        };

        Assert.Equal(
            new ClientPosition(4, 5),
            AuthoritativePlayerPositionProjection.Resolve(player));
    }

    [Fact]
    public void DoesNotApplyAttackDirectionFieldsToPresentationPosition()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CombatMode = PlayerCombatMode.Attack,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Attack,
                DirectionX = 1,
                DirectionY = 1,
            },
        };

        Assert.Equal(
            new ClientPosition(4, 5),
            AuthoritativePlayerPositionProjection.Resolve(player));
    }

    [Fact]
    public void KeepsInterpolationForAOneTileAuthoritativeWalk()
    {
        var previous = new PlayerSnapshot {
            EntityId = 7,
            LevelId = 2,
            PositionX = 4,
            PositionY = 5,
        };
        var next = new PlayerSnapshot {
            EntityId = 7,
            LevelId = 2,
            PositionX = 4,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                DirectionX = 1,
                EffectApplied = false,
            },
        };

        Assert.False(AuthoritativePlayerPositionProjection.RequiresPresentationReset(previous, next));
    }

    [Fact]
    public void SnapsSameLevelLargeAuthoritativeRelocations()
    {
        var previous = new PlayerSnapshot {
            EntityId = 7,
            LevelId = 2,
            PositionX = 4,
            PositionY = 5,
        };
        var next = new PlayerSnapshot {
            EntityId = 7,
            LevelId = 2,
            PositionX = 20,
            PositionY = 15,
        };

        Assert.True(AuthoritativePlayerPositionProjection.RequiresPresentationReset(previous, next));
    }

    [Fact]
    public void SnapsAcrossExplicitTransitionActions()
    {
        var previous = new PlayerSnapshot {
            EntityId = 7,
            LevelId = 2,
            PositionX = 4,
            PositionY = 5,
        };
        var next = new PlayerSnapshot {
            EntityId = 7,
            LevelId = 2,
            PositionX = 5,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.RestartTown,
            },
        };

        Assert.True(AuthoritativePlayerPositionProjection.RequiresPresentationReset(previous, next));
    }
}
