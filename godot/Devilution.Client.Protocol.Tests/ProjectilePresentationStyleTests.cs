using Devilution.Protocol.V1;
using Devilution.Client.Protocol;
using Xunit;
using ProtocolProjectilePresentationKind = Devilution.Protocol.V1.ProjectilePresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed class ProjectilePresentationStyleTests
{
    [Theory]
    [InlineData(16, 0, ProjectilePresentationKind.Fire)]
    [InlineData(16, 1, ProjectilePresentationKind.FireImpact)]
    [InlineData(32, 0, ProjectilePresentationKind.Fire)]
    [InlineData(33, 0, ProjectilePresentationKind.FireImpact)]
    [InlineData(20, 0, ProjectilePresentationKind.BloodStar)]
    [InlineData(50, 0, ProjectilePresentationKind.MonsterMagic)]
    [InlineData(57, 0, ProjectilePresentationKind.HorkSpawn)]
    [InlineData(21, 0, ProjectilePresentationKind.BoneSpirit)]
    [InlineData(31, 0, ProjectilePresentationKind.Lightning)]
    [InlineData(56, 0, ProjectilePresentationKind.Lightning)]
    [InlineData(51, 0, ProjectilePresentationKind.Acid)]
    [InlineData(59, 0, ProjectilePresentationKind.Arrow)]
    [InlineData(9, 0, ProjectilePresentationKind.Teleport)]
    [InlineData(15, 0, ProjectilePresentationKind.Phasing)]
    public void ResolvesAuthoritativeProjectileFamilies(
        int behavior,
        int phase,
        ProjectilePresentationKind expected)
    {
        Assert.Equal(expected, ProjectilePresentationStyle.Resolve(behavior, phase));
    }

    [Theory]
    [InlineData(11, ProjectilePresentationKind.Beam)]
    [InlineData(13, ProjectilePresentationKind.StoneCurse)]
    [InlineData(999, ProjectilePresentationKind.Generic)]
    public void KeepsSpecialAndUnknownBehaviorsDeterministic(
        int behavior,
        ProjectilePresentationKind expected)
    {
        Assert.Equal(expected, ProjectilePresentationStyle.Resolve(behavior, 0));
    }

    [Fact]
    public void PrefersServerOwnedPresentationMetadata()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 999,
            PresentationKind = ProtocolProjectilePresentationKind.Fire,
        };

        Assert.Equal(
            ProjectilePresentationKind.Fire,
            ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void ResolvesServerOwnedMonsterMagicPresentationMetadata()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 999,
            PresentationKind = ProtocolProjectilePresentationKind.MonsterMagic,
        };

        Assert.Equal(
            ProjectilePresentationKind.MonsterMagic,
            ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void ResolvesServerOwnedHorkSpawnPresentationMetadata()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 999,
            PresentationKind = ProtocolProjectilePresentationKind.HorkSpawn,
        };

        Assert.Equal(
            ProjectilePresentationKind.HorkSpawn,
            ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void FallsBackToBehaviorMetadataForOlderSnapshots()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 16,
            Phase = 1,
        };

        Assert.Equal(
            ProjectilePresentationKind.FireImpact,
            ProjectilePresentationStyle.Resolve(projectile));
    }
}
