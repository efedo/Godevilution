using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeCatacombsArrowStateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void SameCellLaunchBindsFacingFallbackAndEffectiveDirection(int facing)
    {
        var state = CreateState(facing);

        state.ValidateShape();
        Assert.Equal((10, 10), (state.RequestedTargetX, state.RequestedTargetY));
        Assert.Equal((10, 10), (state.StartX, state.StartY));
        Assert.NotEqual((state.StartX, state.StartY), (state.DestinationX, state.DestinationY));
        Assert.Equal((state.DestinationX, state.DestinationY),
            AuthoritativeNativeCatacombsArrowState.ResolveEffectiveDestination(
                state.StartX, state.StartY, state.RequestedTargetX, state.RequestedTargetY,
                state.LaunchFacingDirection));
        Assert.Equal(state.Direction16, AuthoritativeNativeCatacombsArrowState.ResolveDirection16(
            state.StartX, state.StartY, state.DestinationX, state.DestinationY));

        Assert.Throws<InvalidDataException>(() => (state with {
            LaunchFacingDirection = (facing + 1) % 8,
        }).ValidateShape());
        Assert.Throws<InvalidDataException>(() => (state with {
            Direction16 = (state.Direction16 + 1) % 16,
        }).ValidateShape());
        Assert.Throws<InvalidDataException>(() => (state with {
            RequestedTargetX = state.StartX,
            RequestedTargetY = state.StartY + 2,
        }).ValidateShape());
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void PendingArrowRejectsFractionalTravelThatStaysInTheSameTile(int offsetX, int offsetY)
    {
        var state = CreateState(facing: 0, requestedTargetX: 20, requestedTargetY: 20);
        var velocity = DeterministicGridVelocity.CalculateNative(
            state.StartX, state.StartY, state.DestinationX, state.DestinationY, 32);
        var projectile = new AuthoritativeProjectile(
            state.ProjectileEntityId, state.SourceEntityId, state.TargetEntityId, 0, state.LevelId,
            state.StartX, state.StartY, state.DestinationX, state.DestinationY, 0,
            AuthoritativeDamageType.Physical, 0, 256, allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
            NativeMissileId = "Arrow",
            Phase = AuthoritativeProjectilePhase.Travel,
            Speed = 32,
            ImpactLifetime = 0,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            OriginalDirection = state.Direction16 / 2,
        };

        state.ValidateProjectile(projectile);
        projectile.TraveledX = offsetX;
        projectile.TraveledY = offsetY;
        Assert.Equal((state.StartX, state.StartY), (projectile.PositionX, projectile.PositionY));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
    }

    [Fact]
    public void PendingArrowRejectsHiddenOrCompetingProjectilePayloads()
    {
        var state = CreateState(facing: 0, requestedTargetX: 20, requestedTargetY: 20);
        var projectile = CreateProjectile(state);
        state.ValidateProjectile(projectile);

        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, damageQ6: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, damageMinimum: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, damageMaximum: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state,
            nativeWeaponArrow: new AuthoritativeNativeWeaponArrowState(0))));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state,
            nativeTrapArrow: new AuthoritativeNativeTrapArrowState(0, 0, 0))));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, activationDelayTicks: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, elementalFireDamage: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, elementalLightningDamage: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, attackHit: true)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, perilSelfDamageQ6: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, doppelgangerProc: true)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state,
            damageType: AuthoritativeDamageType.Fire)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, areaRadius: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, lastCollisionTargetEntityId: 9)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, effectiveSpellLevel: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state, wallLeftStopped: true)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state,
            wallRightDirection: 0)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state,
            wallHitDirection: 0)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state,
            elementalOriginX: 1)));
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(CreateProjectile(state,
            elementalOriginY: 1)));

        projectile.PathIndex = 1;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.PathTicks = 1;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.WallRightX = 1;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.WallRightY = 1;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.WallRightStopped = true;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.ApocalypseScanX = 1;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.ApocalypseScanY = 1;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.ApocalypseHit = true;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.RingOfFireLimitReached = true;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
        projectile = CreateProjectile(state);
        projectile.GuardianState = 1;
        Assert.Throws<InvalidDataException>(() => state.ValidateProjectile(projectile));
    }

    private static AuthoritativeProjectile CreateProjectile(
        AuthoritativeNativeCatacombsArrowState state,
        AuthoritativeDamageType damageType = AuthoritativeDamageType.Physical,
        int areaRadius = 0,
        int damageQ6 = 0,
        int damageMinimum = 0,
        int damageMaximum = 0,
        AuthoritativeNativeWeaponArrowState? nativeWeaponArrow = null,
        AuthoritativeNativeTrapArrowState? nativeTrapArrow = null,
        uint activationDelayTicks = 0,
        int elementalFireDamage = 0,
        int perilSelfDamageQ6 = 0,
        bool doppelgangerProc = false,
        uint lastCollisionTargetEntityId = 0,
        int effectiveSpellLevel = 0,
        bool wallLeftStopped = false,
        int elementalLightningDamage = 0,
        bool attackHit = false,
        int wallRightDirection = -1,
        int wallHitDirection = -1,
        int elementalOriginX = 0,
        int elementalOriginY = 0)
    {
        var velocity = DeterministicGridVelocity.CalculateNative(
            state.StartX, state.StartY, state.DestinationX, state.DestinationY, 32);
        return new AuthoritativeProjectile(
            state.ProjectileEntityId, state.SourceEntityId, state.TargetEntityId, 0, state.LevelId,
            state.StartX, state.StartY, state.DestinationX, state.DestinationY, 0,
            damageType, areaRadius, 256, allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
            NativeMissileId = "Arrow",
            Phase = AuthoritativeProjectilePhase.Travel,
            Speed = 32,
            ImpactLifetime = 0,
            VelocityX = velocity.X,
            VelocityY = velocity.Y,
            OriginalDirection = state.Direction16 / 2,
            DamageQ6 = damageQ6,
            DamageMinimum = damageMinimum,
            DamageMaximum = damageMaximum,
            NativeWeaponArrow = nativeWeaponArrow,
            NativeTrapArrow = nativeTrapArrow,
            ActivationDelayTicks = activationDelayTicks,
            ElementalFireDamage = elementalFireDamage,
            PerilSelfDamageQ6 = perilSelfDamageQ6,
            DoppelgangerProc = doppelgangerProc,
            LastCollisionTargetEntityId = lastCollisionTargetEntityId,
            EffectiveSpellLevel = effectiveSpellLevel,
            WallLeftStopped = wallLeftStopped,
            ElementalLightningDamage = elementalLightningDamage,
            AttackHit = attackHit,
            WallRightDirection = wallRightDirection,
            WallHitDirection = wallHitDirection,
            ElementalOriginX = elementalOriginX,
            ElementalOriginY = elementalOriginY,
        };
    }

    private static AuthoritativeNativeCatacombsArrowState CreateState(
        int facing,
        int requestedTargetX = 10,
        int requestedTargetY = 10)
    {
        const int startX = 10;
        const int startY = 10;
        var destination = AuthoritativeNativeCatacombsArrowState.ResolveEffectiveDestination(
            startX, startY, requestedTargetX, requestedTargetY, facing);
        return new AuthoritativeNativeCatacombsArrowState(
            ProjectileEntityId: 1,
            SourceEntityId: 2,
            NativeSlot: 0,
            ContentMonsterId: 1,
            NativeTypeId: 22,
            LevelId: 5,
            Depth: 5,
            TargetEntityId: 3,
            CharacterClassId: 0,
            NativeAi: "SkeletonRanged",
            MetadataFingerprint: new string('A', 64),
            PopulationFingerprint: new string('B', 64),
            MissileMappingFingerprint: new string('C', 64),
            StartX: startX,
            StartY: startY,
            RequestedTargetX: requestedTargetX,
            RequestedTargetY: requestedTargetY,
            LaunchFacingDirection: facing,
            DestinationX: destination.X,
            DestinationY: destination.Y,
            Direction16: AuthoritativeNativeCatacombsArrowState.ResolveDirection16(
                startX, startY, destination.X, destination.Y),
            MissileDistance: 0,
            LastCollisionTargetHash: 0,
            SourcePositionX: startX,
            SourcePositionY: startY,
            SourceMonsterLevel: 1,
            SourceToHit: 10,
            SourceMinimumDamage: 1,
            SourceMaximumDamage: 2,
            SourceIsPlayerMinion: false,
            SourceIsBerserked: false);
    }
}
