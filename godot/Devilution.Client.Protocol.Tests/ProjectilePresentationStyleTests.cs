using Devilution.Protocol.V1;
using Devilution.Client.Protocol;
using Xunit;
using ProtocolProjectilePresentationKind = Devilution.Protocol.V1.ProjectilePresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed class ProjectilePresentationStyleTests
{
    [Theory]
    [InlineData(2, 0, ProjectilePresentationKind.Fire)]
    [InlineData(2, 1, ProjectilePresentationKind.FireImpact)]
    [InlineData(16, 0, ProjectilePresentationKind.Fire)]
    [InlineData(16, 1, ProjectilePresentationKind.FireImpact)]
    [InlineData(32, 0, ProjectilePresentationKind.Fire)]
    [InlineData(33, 0, ProjectilePresentationKind.FireImpact)]
    [InlineData(35, 0, ProjectilePresentationKind.Fire)]
    [InlineData(35, 1, ProjectilePresentationKind.Fire)]
    [InlineData(36, 0, ProjectilePresentationKind.Guardian)]
    [InlineData(34, 0, ProjectilePresentationKind.Hidden)]
    [InlineData(5, 0, ProjectilePresentationKind.Flash)]
    [InlineData(6, 0, ProjectilePresentationKind.Flash)]
    [InlineData(7, 0, ProjectilePresentationKind.Flash)]
    [InlineData(20, 0, ProjectilePresentationKind.BloodStar)]
    [InlineData(50, 0, ProjectilePresentationKind.MonsterMagic)]
    [InlineData(57, 0, ProjectilePresentationKind.HorkSpawn)]
    [InlineData(21, 0, ProjectilePresentationKind.BoneSpirit)]
    [InlineData(31, 0, ProjectilePresentationKind.NovaBall)]
    [InlineData(56, 0, ProjectilePresentationKind.Lightning)]
    [InlineData(51, 0, ProjectilePresentationKind.Acid)]
    [InlineData(59, 0, ProjectilePresentationKind.Arrow)]
    [InlineData(1, 0, ProjectilePresentationKind.Lightning)]
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
    [InlineData(23, 2, ProjectilePresentationKind.Lightning)]
    [InlineData(23, 0, ProjectilePresentationKind.Fire)]
    public void ResolvesLegacyWallFallbackByDamageType(
        int behavior,
        int damageType,
        ProjectilePresentationKind expected)
    {
        Assert.Equal(expected, ProjectilePresentationStyle.Resolve(behavior, 0, damageType));
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

    [Theory]
    [InlineData(NativeProjectileVisual.MonsterLightning, ProjectilePresentationKind.MonsterLightning)]
    [InlineData(NativeProjectileVisual.MonsterOrangeFlare, ProjectilePresentationKind.MonsterOrangeFlare)]
    [InlineData(NativeProjectileVisual.MonsterBlueFlare, ProjectilePresentationKind.MonsterBlueFlare)]
    [InlineData(NativeProjectileVisual.MonsterRedFlare, ProjectilePresentationKind.MonsterRedFlare)]
    [InlineData(NativeProjectileVisual.MonsterYellowFlare, ProjectilePresentationKind.MonsterYellowFlare)]
    [InlineData(NativeProjectileVisual.MonsterBlueFlare2, ProjectilePresentationKind.MonsterBlueFlare2)]
    [InlineData(NativeProjectileVisual.MonsterMagmaBall, ProjectilePresentationKind.MonsterMagmaBall)]
    public void ResolvesContentOwnedMonsterMagicNativeVisuals(
        NativeProjectileVisual nativeVisual,
        ProjectilePresentationKind expected)
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 50,
            PresentationKind = ProtocolProjectilePresentationKind.MonsterMagic,
            NativeVisual = nativeVisual,
        };

        Assert.Equal(expected, ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void PrefersServerOwnedNativeVisualOverLegacyMissileIdentity()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 50,
            PresentationKind = ProtocolProjectilePresentationKind.MonsterMagic,
            NativeVisual = NativeProjectileVisual.MonsterOrangeFlare,
            NativeMissileId = "BlueFlare2",
        };

        Assert.Equal(
            ProjectilePresentationKind.MonsterOrangeFlare,
            ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void UsesLegacyNativeMissileIdentityWhenServerVisualIsAbsent()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 50,
            PresentationKind = ProtocolProjectilePresentationKind.MonsterMagic,
            NativeMissileId = "BlueFlare2",
        };

        Assert.Equal(
            ProjectilePresentationKind.MonsterBlueFlare2,
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
    public void ResolvesServerOwnedGuardianPresentationMetadata()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 999,
            PresentationKind = ProtocolProjectilePresentationKind.Guardian,
        };

        Assert.Equal(
            ProjectilePresentationKind.Guardian,
            ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void ResolvesServerOwnedFlashPresentationMetadata()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 999,
            PresentationKind = ProtocolProjectilePresentationKind.Flash,
        };

        Assert.Equal(
            ProjectilePresentationKind.Flash,
            ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void ResolvesServerOwnedHiddenPresentationMetadata()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 999,
            PresentationKind = ProtocolProjectilePresentationKind.Hidden,
        };

        Assert.Equal(
            ProjectilePresentationKind.Hidden,
            ProjectilePresentationStyle.Resolve(projectile));
    }

    [Fact]
    public void ResolvesServerOwnedNovaBallPresentationMetadata()
    {
        var projectile = new ProjectileSnapshot {
            Behavior = 999,
            PresentationKind = ProtocolProjectilePresentationKind.NovaBall,
        };

        Assert.Equal(
            ProjectilePresentationKind.NovaBall,
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

        projectile = new ProjectileSnapshot {
            Behavior = 23,
            DamageType = 2,
        };

        Assert.Equal(
            ProjectilePresentationKind.Lightning,
            ProjectilePresentationStyle.Resolve(projectile));
    }
}
