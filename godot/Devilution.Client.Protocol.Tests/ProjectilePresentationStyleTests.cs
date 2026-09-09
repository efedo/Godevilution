using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Client.Protocol;
using Xunit;
using ProtocolProjectilePresentationKind = Devilution.Protocol.V1.ProjectilePresentationKind;
using ProtocolItemPresentationKind = Devilution.Protocol.V1.ItemPresentationKind;
using ProtocolObjectPresentationKind = Devilution.Protocol.V1.ObjectPresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed class ProjectilePresentationStyleTests
{
    [Fact]
    public void UsesStableProtocolBehaviorIdentities()
    {
        Assert.Equal(59, (int)AuthoritativeProjectileBehavior.MonsterArrow);
        Assert.Equal(
            (int)AuthoritativeProjectileBehavior.MonsterArrow,
            AuthoritativeProtocolLimits.MaximumProjectileBehavior);
        Assert.Equal(
            (int)ProtocolProjectilePresentationKind.NovaBall,
            AuthoritativeProtocolLimits.MaximumProjectilePresentationKind);
        Assert.Equal(
            (int)NativeProjectileVisual.MonsterMagmaBall,
            AuthoritativeProtocolLimits.MaximumNativeProjectileVisual);
        Assert.Equal(
            (int)StatusEffectBehavior.Etherealize,
            AuthoritativeProtocolLimits.MaximumStatusEffectBehavior);
        Assert.Equal(
            (int)ProtocolItemPresentationKind.Ear,
            AuthoritativeProtocolLimits.MaximumItemPresentationKind);
        Assert.Equal(
            (int)ItemSpellSourceKind.ConsumableItem,
            AuthoritativeProtocolLimits.MaximumItemSpellSourceKind);
        Assert.Equal(
            (int)ItemActionKind.ReadQuestNote,
            AuthoritativeProtocolLimits.MaximumItemActionKind);
        Assert.Equal(
            (int)SpellTargetingKind.WorldEntity,
            AuthoritativeProtocolLimits.MaximumSpellTargetingKind);
        Assert.Equal(
            (int)ProtocolObjectPresentationKind.Trap,
            AuthoritativeProtocolLimits.MaximumObjectPresentationKind);
        Assert.Equal(9, AuthoritativeProtocolLimits.MaximumObjectEffectKind);
        Assert.Equal(8, AuthoritativeProtocolLimits.MaximumObjectQuestTransitionKind);
        Assert.Equal(2, AuthoritativeProtocolLimits.MaximumObjectRewardKind);
        Assert.Equal(1, AuthoritativeProtocolLimits.MaximumObjectRewardOriginKind);
        Assert.Equal(3, AuthoritativeProtocolLimits.MaximumObjectQuestSequenceFollowUpKind);
        Assert.Equal(1, AuthoritativeProtocolLimits.MaximumObjectProjectileSourceKind);
        Assert.Equal(4, AuthoritativeProtocolLimits.MaximumQuestTownerInteractionKind);
        Assert.Equal(
            ProjectilePresentationKind.Fire,
            ProjectilePresentationStyle.Resolve(
                (int)AuthoritativeProjectileBehavior.Firebolt,
                phase: 0));
    }

    [Fact]
    public void UsesSharedAuthoritativeCollectionCaps()
    {
        Assert.Equal(1024 * 1024, AuthoritativeProtocolLimits.MaximumEnvelopeBytes);
        Assert.Equal(4096, AuthoritativeProtocolLimits.MaximumEventBatchCount);
        Assert.Equal(4096, AuthoritativeProtocolLimits.MaximumWorldMutationCount);
        Assert.Equal(4096, AuthoritativeProtocolLimits.MaximumSnapshotQuestCount);
        Assert.Equal(4096, AuthoritativeProtocolLimits.MaximumSetLevelObjectOriginCount);
        Assert.Equal(4096, AuthoritativeProtocolLimits.MaximumMaterializedProceduralLevelCount);
    }

    [Fact]
    public void UsesSharedNativeCompatibilityCaps()
    {
        Assert.Equal(8, AuthoritativeProtocolLimits.MaximumNativeSetLevelId);
        Assert.Equal(2, AuthoritativeProtocolLimits.MaximumMonsterLeaderRelation);
        Assert.Equal(255, AuthoritativeProtocolLimits.MaximumNativeQuestByte);
        Assert.Equal(-1, AuthoritativeProtocolLimits.MinimumNativeOptionalQuestByte);
        Assert.Equal(-16, AuthoritativeProtocolLimits.MinimumNativeRewardOffset);
        Assert.Equal(16, AuthoritativeProtocolLimits.MaximumNativeRewardOffset);
        Assert.Equal(-32, AuthoritativeProtocolLimits.MinimumNativeObjectOffset);
        Assert.Equal(32, AuthoritativeProtocolLimits.MaximumNativeObjectOffset);
        Assert.Equal(-1, AuthoritativeProtocolLimits.MinimumNativeDirection);
        Assert.Equal(1, AuthoritativeProtocolLimits.MaximumNativeDirection);
        Assert.Equal(4, AuthoritativeProtocolLimits.NativeMaximumPlayerCount);
        Assert.Equal(40, AuthoritativeProtocolLimits.NativeMaximumInventoryCount);
        Assert.Equal(7, AuthoritativeProtocolLimits.NativeMaximumEquipmentCount);
        Assert.Equal(8, AuthoritativeProtocolLimits.NativeMaximumBeltCount);
        Assert.Equal(40, AuthoritativeProtocolLimits.NativeMaximumInventoryGridCount);
        Assert.Equal(10, AuthoritativeProtocolLimits.NativeInventoryWidth);
        Assert.Equal(4, AuthoritativeProtocolLimits.NativeInventoryHeight);
        Assert.Equal(100, AuthoritativeProtocolLimits.NativeStashPageCount);
        Assert.Equal(100, AuthoritativeProtocolLimits.NativeStashCellCount);
        Assert.Equal(10, AuthoritativeProtocolLimits.NativeStashGridWidth);
        Assert.Equal(10, AuthoritativeProtocolLimits.NativeStashGridHeight);
        Assert.Equal(0, AuthoritativeProtocolLimits.MinimumNativeTileCoordinate);
        Assert.Equal(255, AuthoritativeProtocolLimits.MaximumNativeTileCoordinate);
        Assert.Equal(200, AuthoritativeProtocolLimits.NativeMaximumMonsterCount);
        Assert.Equal(127, AuthoritativeProtocolLimits.NativeMaximumObjectCount);
        Assert.Equal(127, AuthoritativeProtocolLimits.NativeMaximumItemCount);
        Assert.Equal(7, AuthoritativeProtocolLimits.NativeMaximumWarpTriggerCount);
        Assert.Equal(4, AuthoritativeProtocolLimits.NativeMaximumPortalCount);
        Assert.Equal(112, AuthoritativeProtocolLimits.NativeMaximumWidth);
        Assert.Equal(112, AuthoritativeProtocolLimits.NativeMaximumHeight);
        Assert.Equal(40, AuthoritativeProtocolLimits.NativeMaximumMegaWidth);
        Assert.Equal(40, AuthoritativeProtocolLimits.NativeMaximumMegaHeight);
        Assert.Equal((uint)146, AuthoritativeProtocolLimits.NativeMaximumDunObjectLayerId);
        Assert.Equal((uint)1378, AuthoritativeProtocolLimits.NativeMaximumDunPieceId);
        Assert.Equal((uint)108, AuthoritativeProtocolLimits.NativeMaximumObjectId);
        Assert.Equal((uint)23, AuthoritativeProtocolLimits.NativeMaximumQuestId);
        Assert.Equal(256, AuthoritativeProtocolLimits.MaximumNativeVisitedLevelCount);
        Assert.Equal(1024, AuthoritativeProtocolLimits.MaximumNativeMonsterDeathAnimationTicks);
        Assert.Equal((uint)1_000_000, AuthoritativeProtocolLimits.MaximumNativeMonsterExperienceReward);
        Assert.Equal(255, AuthoritativeProtocolLimits.MaximumNativeMonsterToHit);
        Assert.Equal(255, AuthoritativeProtocolLimits.MaximumNativeMonsterIntelligence);
        Assert.Equal(7, AuthoritativeProtocolLimits.MaximumNativeMonsterDirection);
        Assert.Equal(255, AuthoritativeProtocolLimits.MaximumNativeMonsterPackSize);
        Assert.Equal(255, AuthoritativeProtocolLimits.MaximumNativeMonsterActiveTicks);
        Assert.Equal(127, AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationFrame);
        Assert.Equal(127, AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationTick);
        Assert.Equal(4096, AuthoritativeProtocolLimits.MaximumNativeMonsterFallenRallyTicks);
        Assert.Equal(-100, AuthoritativeProtocolLimits.MinimumNativeMonsterResistance);
        Assert.Equal(100, AuthoritativeProtocolLimits.MaximumNativeMonsterResistance);
        Assert.Equal((uint)255, AuthoritativeProtocolLimits.MaximumStableSpellId);
        Assert.Equal((uint)51, AuthoritativeProtocolLimits.MaximumNativeSpellId);
        Assert.Equal((uint)15, AuthoritativeProtocolLimits.MaximumNativeSpellLevel);
        Assert.Equal(51, AuthoritativeProtocolLimits.MaximumMemorizedSpellCount);
        Assert.Equal((uint)50, AuthoritativeProtocolLimits.MaximumNativeCharacterLevel);
        Assert.Equal((uint)255, AuthoritativeProtocolLimits.MaximumNativeDiabloKillLevel);
        Assert.Equal((uint)65_535, AuthoritativeProtocolLimits.MaximumNativeReflectCharges);
        Assert.Equal(2, AuthoritativeProtocolLimits.MinimumNativeLightRadius);
        Assert.Equal(15, AuthoritativeProtocolLimits.MaximumNativeLightRadius);
        Assert.Equal(1024, AuthoritativeProtocolLimits.MaximumQuestTownerInteractionCount);
        Assert.Equal(256, AuthoritativeProtocolLimits.MaximumTownerGreetingCount);
        Assert.Equal(128, AuthoritativeProtocolLimits.MaximumStatusEffectCount);
    }

    [Fact]
    public void NativeTileCoordinateContractIncludesWireBoundariesOnly()
    {
        Assert.True(AuthoritativeProtocolLimits.IsNativeTileCoordinate(0, 0));
        Assert.True(AuthoritativeProtocolLimits.IsNativeTileCoordinate(255, 255));
        Assert.False(AuthoritativeProtocolLimits.IsNativeTileCoordinate(-1, 0));
        Assert.False(AuthoritativeProtocolLimits.IsNativeTileCoordinate(0, -1));
        Assert.False(AuthoritativeProtocolLimits.IsNativeTileCoordinate(256, 0));
        Assert.False(AuthoritativeProtocolLimits.IsNativeTileCoordinate(0, 256));
    }

    [Fact]
    public void UsesSharedProjectileStateCaps()
    {
        Assert.Equal(2, AuthoritativeProtocolLimits.MaximumMonsterDialogueState);
        Assert.Equal(4, AuthoritativeProtocolLimits.MaximumProjectileDamageType);
        Assert.Equal(1, AuthoritativeProtocolLimits.MaximumProjectilePhase);
    }

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
