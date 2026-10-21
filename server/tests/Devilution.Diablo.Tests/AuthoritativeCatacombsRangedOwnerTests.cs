using System.Text.Json;
using Devilution.Engine;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralNativePopulationIntegrationTests;

namespace Devilution.Diablo.Tests;

/** Actual generated owners retain their clock without allowing a replacement actor or missile row. */
public sealed class AuthoritativeCatacombsRangedOwnerTests
{
    [Fact(Explicit = true)]
    public void CaptureIsPureAndChangedMissileOrDetachedActorCannotAdvanceOwnedAction()
    {
        using var fixture = CreateFixture();
        var build = fixture.CreateBuild(5, 5);
        var missiles = fixture.Activation.MonsterMissiles!;
        var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, missiles);
        var target = RangedActor(world);
        var victim = Victim(fixture, target, 0);
        Assert.True(missiles.TryGet(target.NativeAi!, target.MonsterId, out var missile));
        var changed = missile with { ProjectileSpeed = missile.ProjectileSpeed + 1 };
        Assert.Throws<InvalidDataException>(() => world.StartNativeCatacombsRangedAction(target, changed, victim, 123));
        Assert.Empty(world.CaptureNativeCatacombsRangedActions());
        world.StartNativeCatacombsRangedAction(target, missile, victim, 123);
        var captured = JsonSerializer.Serialize(world.CaptureNativeCatacombsRangedActions());
        var actor = JsonSerializer.Serialize(target.CaptureCohortState());
        Assert.Equal(captured, JsonSerializer.Serialize(world.CaptureNativeCatacombsRangedActions()));
        Assert.Equal(actor, JsonSerializer.Serialize(target.CaptureCohortState()));
        Assert.Throws<InvalidDataException>(() => world.AdvanceNativeCatacombsRangedAction(target, changed, null, null));
        var clone = new AuthoritativeCombatTarget(target.EntityId, target.PositionX, target.PositionY,
            100, levelId: target.LevelId, monsterId: target.MonsterId, nativeAi: target.NativeAi);
        Assert.Throws<InvalidDataException>(() => world.TryGetNativeCatacombsRangedAction(clone, out _));
        Assert.Throws<InvalidDataException>(() => world.ConfigureNativeCatacombsRangedActions(build.Policy.Bindings, missiles));
        Assert.Equal(captured, JsonSerializer.Serialize(world.CaptureNativeCatacombsRangedActions()));
    }

    [Fact(Explicit = true)]
    public void DetachedConfigurationBindsSavedActionsToTrustedMappingBeforePublication()
    {
        using var fixture = CreateFixture();
        var missiles = fixture.Activation.MonsterMissiles!;
        var build = fixture.CreateBuild(5, 5);
        var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, missiles);
        var target = RangedActor(world);
        var victim = Victim(fixture, target, 0);
        Assert.True(missiles.TryGet(target.NativeAi!, target.MonsterId, out var missile));
        world.StartNativeCatacombsRangedAction(target, missile, victim, 123);
        world.AdvanceNativeCatacombsRangedAction(target, missile, null, null);
        var saved = Assert.Single(world.CaptureNativeCatacombsRangedActions());
        var detachedBuild = fixture.CreateBuild(5, 5);
        var restored = DiabloWorldState.CreateNativeCatacombsFloor(detachedBuild, fixture.Items,
            fixture.Spells, missiles, [saved]);
        Assert.Equal(saved, Assert.Single(restored.CaptureNativeCatacombsRangedActions()));
        var forged = saved with { MissileMappingFingerprint = new string('A', 64) };
        Assert.Throws<InvalidDataException>(() => DiabloWorldState.CreateNativeCatacombsFloor(
            detachedBuild, fixture.Items, fixture.Spells, missiles, [forged]));
        Assert.Throws<InvalidDataException>(() => DiabloWorldState.CreateNativeCatacombsFloor(
            detachedBuild, fixture.Items, fixture.Spells, missiles, [saved, saved]));
        Assert.Equal(saved, Assert.Single(world.CaptureNativeCatacombsRangedActions()));
        Assert.Equal(saved, Assert.Single(restored.CaptureNativeCatacombsRangedActions()));
    }

    [Theory(Explicit = true)]
    [InlineData(0U)]
    [InlineData(2U)]
    public void ProcessorPausesOffFloorAndPetrifiedClocksThenResumesWithoutRerollingAi(uint classId)
    {
        using var fixture = CreateFixture(classId);
        var build = fixture.CreateBuild(5, 5);
        var missiles = fixture.Activation.MonsterMissiles!;
        var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, missiles);
        var target = RangedActor(world);
        var victim = Victim(fixture, target, classId);
        Assert.True(missiles.TryGet(target.NativeAi!, target.MonsterId, out var missile));
        world.StartNativeCatacombsRangedAction(target, missile, victim, 123);
        foreach (var other in world.CombatTargets.Values.Where(value => value.EntityId != target.EntityId))
            other.IsPetrified = true;
        var random = new DeterministicLegacyRandom(0x12345678);
        var processor = new DiabloCombatCommandProcessor(world, fixture.Spells,
            new AuthoritativeCombatRules(10, 1, 100), random,
            monsterMissiles: missiles, monsterTypes: fixture.Activation.TrustedOptions!.MonsterTypes);
        var before = Assert.Single(world.CaptureNativeCatacombsRangedActions());
        victim.LevelId = 0;
        processor.AdvanceTick(1, [victim]);
        Assert.Equal(before, Assert.Single(world.CaptureNativeCatacombsRangedActions()));
        Assert.Equal(0x12345678U, random.State);
        victim.LevelId = 5;
        target.IsPetrified = true;
        processor.AdvanceTick(2, [victim]);
        Assert.Equal(before, Assert.Single(world.CaptureNativeCatacombsRangedActions()));
        Assert.Equal(0x12345678U, random.State);
        target.IsPetrified = false;
        processor.AdvanceTick(3, [victim]);
        Assert.Equal(before.Advance().Action, Assert.Single(world.CaptureNativeCatacombsRangedActions()));
        Assert.Empty(world.Projectiles);
        Assert.Equal(0x12345678U, random.State);
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessorCancelsPendingReleaseWhenActorDiesOrEntersHitRecovery(bool dead)
    {
        using var fixture = CreateFixture();
        var build = fixture.CreateBuild(5, 5);
        var missiles = fixture.Activation.MonsterMissiles!;
        var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, missiles);
        var target = RangedActor(world);
        var victim = Victim(fixture, target, 0);
        Assert.True(missiles.TryGet(target.NativeAi!, target.MonsterId, out var missile));
        world.StartNativeCatacombsRangedAction(target, missile, victim, 123);
        foreach (var other in world.CombatTargets.Values.Where(value => value.EntityId != target.EntityId))
            other.IsPetrified = true;
        target.RestoreActiveForTicks(0);
        if (dead)
            target.ApplyDamageQ6(target.HitPointsQ6);
        else
            target.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(0, 0) { HitRecovery = true });
        var processor = new DiabloCombatCommandProcessor(world, fixture.Spells,
            new AuthoritativeCombatRules(10, 1, 100), new DeterministicLegacyRandom(0x12345678),
            monsterMissiles: missiles, monsterTypes: fixture.Activation.TrustedOptions!.MonsterTypes);

        processor.AdvanceTick(1, [victim]);

        Assert.False(world.HasNativeCatacombsRangedAction(target.EntityId));
        Assert.Empty(world.CaptureNativeCatacombsRangedActions());
        Assert.DoesNotContain(world.Projectiles.Values, value => value.SourceEntityId == target.EntityId);
    }

    private static AuthoritativeCombatTarget RangedActor(DiabloWorldState world)
        => world.CombatTargets.Values.First(value => value.NativeAi == "SkeletonRanged"
            && value.NativeInitialization!.InitialState.Mode == 0 && !value.IsUnique);

    private static AuthoritativePlayerState Victim(QualifiedCatacombsBuilderFixture fixture,
        AuthoritativeCombatTarget target, uint classId)
    {
        var victim = fixture.Activation.PlayerLoadouts.CreateTemplate("ranged-owner", classId, null, null, null,
            startingLevelId: target.LevelId, startingPositionX: target.PositionX + 5,
            startingPositionY: target.PositionY).CreatePlayer();
        victim.EntityId = 0x70000001;
        return victim;
    }
}
