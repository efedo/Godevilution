using Xunit;

namespace Devilution.Diablo.Tests;

/** Restore bounds for the actor-owned native ranged AI state and projectile-owned monster missiles. */
public sealed class AuthoritativeNativeMonsterMissileStateTests
{
    [Fact]
    public void AiVarsAcceptTheSourceRangesAndRejectEverythingElse()
    {
        var target = CreateTarget();
        var valid = new AuthoritativeNativeAiVars(17, short.MaxValue, true, 4, short.MaxValue, 1);
        target.RestoreNativeAiVars(valid);
        Assert.Equal(valid, target.NativeAiVars);
        foreach (var invalid in new[] {
                     valid with { Var1 = -1 }, valid with { Var1 = 18 },
                     valid with { Var2 = -1 }, valid with { Var2 = short.MaxValue + 1 },
                     valid with { Goal = 0 }, valid with { Goal = 3 }, valid with { Goal = 5 },
                     valid with { GoalVar1 = -1 }, valid with { GoalVar1 = short.MaxValue + 1 },
                     valid with { GoalVar2 = 2 },
                 })
            Assert.Throws<InvalidDataException>(() => target.RestoreNativeAiVars(invalid));
        Assert.Equal(valid, target.NativeAiVars);
        target.RestoreNativeAiVars(null);
        Assert.Null(target.NativeAiVars);
    }

    [Fact]
    public void RangedCursorRejectsHitRecoveryFadesAndOrphanDamage()
    {
        var target = CreateTarget();
        var ranged = new AuthoritativeNativeMeleeCursor(0, 0) { RangedMissile = "Acid", UsesSpecialAttack = true };
        target.RestoreNativeMeleeCursor(ranged);
        Assert.Equal(ranged, target.NativeMeleeCursor);
        foreach (var invalid in new[] {
                     ranged with { UsesSpecialAttack = false, HitRecovery = true },
                     ranged with { UsesSpecialAttack = false, Fade = AuthoritativeNativeFade.Out },
                     ranged with { RangedMissile = "" },
                     ranged with { RangedMissile = new string('A', 65) },
                     ranged with { RangedDamage = -1 },
                     ranged with { RangedDamage = 10_001 },
                     new AuthoritativeNativeMeleeCursor(0, 0) { RangedDamage = 4 },
                 })
            Assert.Throws<InvalidDataException>(() => target.RestoreNativeMeleeCursor(invalid));
    }

    [Theory]
    [InlineData(AuthoritativeNativeMonsterMissileKind.Acid)]
    [InlineData(AuthoritativeNativeMonsterMissileKind.AcidSplat)]
    [InlineData(AuthoritativeNativeMonsterMissileKind.AcidPuddle)]
    [InlineData(AuthoritativeNativeMonsterMissileKind.MagmaBall)]
    [InlineData(AuthoritativeNativeMonsterMissileKind.MagmaBallExplosion)]
    [InlineData(AuthoritativeNativeMonsterMissileKind.Lightning)]
    public void MissileStateBindsItsKindToTheProjectile(AuthoritativeNativeMonsterMissileKind kind)
    {
        var state = AuthoritativeNativeMonsterMissileState.Create(kind, CreateTarget());
        var projectile = CreateProjectile(kind.ToString());
        state.ValidateShape(projectile);
        Assert.Equal(kind.ToString(), state.MissileId);

        Assert.Throws<InvalidDataException>(() => state.ValidateShape(CreateProjectile("Arrow")));
        Assert.Throws<InvalidDataException>(() => (state with { MissileDistance = 1 }).ValidateShape(projectile));
        Assert.Throws<InvalidDataException>(() => (state with { LastCollisionTargetHash = 256 }).ValidateShape(projectile));
        Assert.Throws<InvalidDataException>(() => (state with { SourceIntelligence = 256 }).ValidateShape(projectile));
        Assert.Throws<InvalidDataException>(() => (state with {
            SourceMinimumDamage = 5, SourceMaximumDamage = 4,
        }).ValidateShape(projectile));
        Assert.Throws<InvalidDataException>(() => (state with { Kind = (AuthoritativeNativeMonsterMissileKind)99 })
            .ValidateShape(projectile));
        var ending = state with { Ending = true };
        if (kind == AuthoritativeNativeMonsterMissileKind.AcidPuddle)
            ending.ValidateShape(projectile);
        else
            Assert.Throws<InvalidDataException>(() => ending.ValidateShape(projectile));
    }

    [Fact]
    public void RefreshTakesTheLiveSourceFactsAndKeepsTheClock()
    {
        var source = CreateTarget();
        var state = AuthoritativeNativeMonsterMissileState.Create(AuthoritativeNativeMonsterMissileKind.AcidPuddle, source)
            with { Ending = true, LastCollisionTargetHash = -3 };
        var refreshed = state.Refresh(CreateTarget(intelligence: 3));
        Assert.Equal(3, refreshed.SourceIntelligence);
        Assert.True(refreshed.Ending);
        Assert.Equal(-3, refreshed.LastCollisionTargetHash);
    }

    private static AuthoritativeProjectile CreateProjectile(string missileId)
        => new(0x80000001, 900, 0, 0, 5, 50, 50, 51, 51, 1, AuthoritativeDamageType.Acid, 0, 8, allowZeroSpellId: true) {
            NativeMissileId = missileId,
        };

    private static AuthoritativeCombatTarget CreateTarget(int intelligence = 1)
    {
        var state = new AuthoritativeCathedralPopulationMonsterState {
            Slot = 0, Type = 46, Ai = 19, Goal = 1, UniqueType = 255,
            HitPoints = 6400, MaxHitPoints = 6400, Leader = 255,
            Tile = new(50, 50), Future = new(50, 50), Old = new(50, 50),
        };
        var initialization = new AuthoritativeNativeMonsterInitialization(new string('A', 64), state, 0, 3,
            7, 7, new(0, 46, 255, 900, 46, 5, ""), false) {
            CatacombsDepth = 5,
            NativeAttackFrameCount = 8, NativeAttackTicksPerFrame = 1,
            NativeSpecialAttackFrameCount = 10, NativeSpecialAttackTicksPerFrame = 1,
            NativeHitRecoveryFrameCount = 4, NativeHitRecoveryTicksPerFrame = 1,
        };
        var target = new AuthoritativeCombatTarget(900, 50, 50, 100, maxHitPoints: 100,
            levelId: 5, monsterId: 46, nativeAi: "Acid", intelligence: intelligence);
        target.RestoreNativeInitialization(initialization, 0, 0);
        return target;
    }
}
