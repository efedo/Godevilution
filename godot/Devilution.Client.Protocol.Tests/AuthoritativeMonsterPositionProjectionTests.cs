using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeMonsterPositionProjectionTests
{
    [Fact]
    public void UsesTheAuthoritativeFutureTileWhileMoving()
    {
        var monster = new MonsterSnapshot {
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = true,
        };

        Assert.Equal(
            new ClientPresentationPosition(5, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster));
    }

    [Fact]
    public void UsesTheOccupiedTileWhenStationary()
    {
        var monster = new MonsterSnapshot {
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = false,
        };

        Assert.Equal(
            new ClientPresentationPosition(4, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster));
    }

    [Fact]
    public void InterpolatesAnAuthoritativeWalkWindow()
    {
        var monster = new MonsterSnapshot {
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = true,
            MovementStartedTick = 12,
            MovementEffectTick = 14,
        };

        Assert.Equal(
            new ClientPresentationPosition(4, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, 12));
        Assert.Equal(
            new ClientPresentationPosition(4.5, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, 13));
        Assert.Equal(
            new ClientPresentationPosition(5, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, 14));
    }

    [Fact]
    public void FallsBackToTheFutureTileWhenTimingIsOmitted()
    {
        var monster = new MonsterSnapshot {
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = true,
        };

        Assert.Equal(
            new ClientPresentationPosition(5, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, 12));
    }

    [Fact]
    public void InterpolatesAtFractionalPresentationTicks()
    {
        var monster = new MonsterSnapshot {
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = true,
            MovementStartedTick = 12,
            MovementEffectTick = 14,
        };

        Assert.Equal(
            new ClientPresentationPosition(4.25, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, 12.5));
        Assert.Equal(
            new ClientPresentationPosition(5, 5),
            AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, 20.25));
    }

    [Fact]
    public void WidenedCoordinateDeltaKeepsExtremeInterpolationNumericallyStable()
    {
        var monster = new MonsterSnapshot {
            PositionX = int.MinValue,
            PositionY = int.MaxValue,
            FuturePositionX = int.MaxValue,
            FuturePositionY = int.MinValue,
            Moving = true,
            MovementStartedTick = 12,
            MovementEffectTick = 14,
        };

        var position = AuthoritativeMonsterPositionProjection.ResolvePresentation(monster, 13);

        Assert.Equal(-0.5, position.X);
        Assert.Equal(-0.5, position.Y);
    }

    [Fact]
    public void KeepsAOneTileMoveOnTheInterpolationPath()
    {
        var previous = new MonsterSnapshot {
            EntityId = 7,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
        };
        var next = new MonsterSnapshot {
            EntityId = 7,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
            FuturePositionX = 5,
            FuturePositionY = 5,
            Moving = true,
        };

        Assert.False(
            AuthoritativeMonsterPositionProjection.RequiresPresentationReset(previous, next));
    }

    [Fact]
    public void SnapsLargeSameLevelRelocations()
    {
        var previous = new MonsterSnapshot {
            EntityId = 7,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
        };
        var next = new MonsterSnapshot {
            EntityId = 7,
            LevelId = 1,
            PositionX = 10,
            PositionY = 5,
        };

        Assert.True(
            AuthoritativeMonsterPositionProjection.RequiresPresentationReset(previous, next));
    }

    [Fact]
    public void SnapsAcrossMonsterLevelChanges()
    {
        var previous = new MonsterSnapshot {
            EntityId = 7,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
        };
        var next = previous.Clone();
        next.LevelId = 2;

        Assert.True(
            AuthoritativeMonsterPositionProjection.RequiresPresentationReset(previous, next));
    }
}
