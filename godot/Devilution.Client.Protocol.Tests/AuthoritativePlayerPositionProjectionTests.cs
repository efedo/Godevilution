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
    public void PresentsAWalkAtItsAuthoritativeActionProgress()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                StartedAtTick = 10,
                EffectTick = 14,
                DirectionX = 1,
            },
        };

        var position = AuthoritativePlayerPositionProjection.ResolvePresentation(player, 12);

        Assert.Equal(4.5, position.X, precision: 6);
        Assert.Equal(5, position.Y, precision: 6);
    }

    [Fact]
    public void PresentsAWalkAtAFractionalPresentationTick()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                StartedAtTick = 10,
                EffectTick = 14,
                DirectionX = 1,
            },
        };

        var position = AuthoritativePlayerPositionProjection.ResolvePresentation(player, 10.5);

        Assert.Equal(4.125, position.X, precision: 6);
        Assert.Equal(5, position.Y, precision: 6);
    }

    [Fact]
    public void PresentsARemoteWalkThroughACoarseSnapshotBoundary()
    {
        var clock = new AuthoritativePresentationClock();
        var moving = new PlayerSnapshot {
            EntityId = 17,
            PositionX = 4,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                StartedAtTick = 100,
                EffectTick = 104,
                DirectionX = 1,
            },
        };

        var atStart = AuthoritativePlayerPositionProjection.ResolvePresentation(
            moving,
            clock.Advance(100, 0, 20));
        var betweenSnapshots = AuthoritativePlayerPositionProjection.ResolvePresentation(
            moving,
            clock.Advance(100, 0.025, 20));

        Assert.Equal(new ClientPresentationPosition(4, 5), atStart);
        Assert.Equal(new ClientPresentationPosition(4.125, 5), betweenSnapshots);

        var completed = moving.Clone();
        completed.PositionX = 5;
        completed.CurrentAction.EffectApplied = true;
        var atNextSnapshot = AuthoritativePlayerPositionProjection.ResolvePresentation(
            completed,
            clock.Advance(104, 0, 20));

        Assert.Equal(new ClientPresentationPosition(5, 5), atNextSnapshot);
    }

    [Fact]
    public void ClampsPresentationToTheReservedDestinationAtTheEffectTick()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                StartedAtTick = 10,
                EffectTick = 14,
                DirectionX = -1,
                DirectionY = 1,
            },
        };

        var position = AuthoritativePlayerPositionProjection.ResolvePresentation(player, 20);

        Assert.Equal(3, position.X, precision: 6);
        Assert.Equal(6, position.Y, precision: 6);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, -1)]
    [InlineData(1, -1)]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void InterpolatesEveryAdjacentWalkDirectionAtAuthoritativeBoundaries(
        int directionX,
        int directionY)
    {
        const int startX = 10;
        const int startY = 20;
        const ulong startedAtTick = 100;
        const ulong effectTick = 108;
        var player = new PlayerSnapshot {
            PositionX = startX,
            PositionY = startY,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                StartedAtTick = startedAtTick,
                EffectTick = effectTick,
                DirectionX = directionX,
                DirectionY = directionY,
            },
        };

        var atStart = AuthoritativePlayerPositionProjection.ResolvePresentation(
            player,
            startedAtTick);
        var atMidpoint = AuthoritativePlayerPositionProjection.ResolvePresentation(
            player,
            startedAtTick + 4);
        var atEffect = AuthoritativePlayerPositionProjection.ResolvePresentation(
            player,
            effectTick);
        var afterEffect = AuthoritativePlayerPositionProjection.ResolvePresentation(
            player,
            effectTick + 20);

        Assert.Equal(new ClientPresentationPosition(startX, startY), atStart);
        Assert.Equal(
            new ClientPresentationPosition(
                startX + directionX * 0.5,
                startY + directionY * 0.5),
            atMidpoint);
        Assert.Equal(
            new ClientPresentationPosition(
                startX + directionX,
                startY + directionY),
            atEffect);
        Assert.Equal(atEffect, afterEffect);
    }

    [Fact]
    public void DoesNotInterpolateActionsOtherThanAnInProgressWalk()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Attack,
                StartedAtTick = 10,
                EffectTick = 14,
                DirectionX = 1,
            },
        };

        var position = AuthoritativePlayerPositionProjection.ResolvePresentation(player, 12);

        Assert.Equal(new ClientPresentationPosition(4, 5), position);
    }

    [Fact]
    public void PrefersBoundedLocalPredictionWhileItIsAvailable()
    {
        var player = new PlayerSnapshot {
            PositionX = 4,
            PositionY = 5,
            CurrentAction = new PlayerActionSnapshot {
                Kind = PlayerActionKind.Move,
                StartedAtTick = 10,
                EffectTick = 14,
                DirectionX = 1,
            },
        };

        var position = AuthoritativePlayerPositionProjection.ResolvePresentation(
            player,
            12,
            new ClientPosition(8, 9));

        Assert.Equal(new ClientPresentationPosition(8, 9), position);
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
