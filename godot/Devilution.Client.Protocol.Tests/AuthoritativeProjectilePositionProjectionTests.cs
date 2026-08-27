using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeProjectilePositionProjectionTests
{
    [Fact]
    public void KeepsTravelInterpolationWithinOneAuthoritativePhase()
    {
        var previous = new ProjectileSnapshot {
            EntityId = 7,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
            Behavior = 2,
            Phase = 0,
        };
        var next = previous.Clone();
        next.PositionX = 5;

        Assert.False(
            AuthoritativeProjectilePositionProjection.RequiresPresentationReset(previous, next));
    }

    [Fact]
    public void SnapsAcrossProjectilePhaseChanges()
    {
        var previous = new ProjectileSnapshot {
            EntityId = 7,
            LevelId = 1,
            Phase = 0,
        };
        var next = previous.Clone();
        next.Phase = 1;

        Assert.True(
            AuthoritativeProjectilePositionProjection.RequiresPresentationReset(previous, next));
    }

    [Fact]
    public void SnapsAcrossProjectileLevelChanges()
    {
        var previous = new ProjectileSnapshot {
            EntityId = 7,
            LevelId = 1,
        };
        var next = previous.Clone();
        next.LevelId = 2;

        Assert.True(
            AuthoritativeProjectilePositionProjection.RequiresPresentationReset(previous, next));
    }

    [Fact]
    public void SnapsWhenProjectilePresentationIdentityChanges()
    {
        var previous = new ProjectileSnapshot {
            EntityId = 7,
            LevelId = 1,
            Behavior = 2,
        };
        var next = previous.Clone();
        next.Behavior = 16;

        Assert.True(
            AuthoritativeProjectilePositionProjection.RequiresPresentationReset(previous, next));
    }
}
