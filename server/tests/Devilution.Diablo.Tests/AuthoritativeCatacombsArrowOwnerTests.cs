using System.Reflection;
using System.Text.Json;
using Devilution.Engine;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralNativePopulationIntegrationTests;

namespace Devilution.Diablo.Tests;

/** Generated C2 owners retain flight independently of later floor preparation. */
public sealed class AuthoritativeCatacombsArrowOwnerTests
{
    [Fact(Explicit = true)]
    public void CaptureIsPureAndOneExactSourceMayOwnSeveralArrows()
    {
        using var fixture = CreateFixture();
        var build = fixture.CreateBuild(5, 5);
        var world = CreateWorld(fixture, build);
        var player = Participant(fixture, build, 0);
        var random = new DeterministicLegacyRandom(0x12345678);
        var processor = Processor(fixture, world, random);
        var first = LaunchAtPreparedRelease(fixture, world, processor, player);
        var source = world.CombatTargets[first.SourceEntityId];
        Assert.True(fixture.Activation.MonsterMissiles!.TryGet(source.NativeAi!, source.MonsterId, out var missile));
        var action = Assert.Single(world.CaptureNativeCatacombsRangedActions());
        Emit(processor, source, player, action, missile);
        Assert.Equal(2, world.CaptureNativeCatacombsArrows().Count);
        Assert.Equal(2, world.Projectiles.Count);
        var before = Snapshot(world);

        Assert.Equal(before, Snapshot(world));
        Assert.Equal(0x12345678U, random.State);
        world.ValidateNativePopulationState([player]);
    }

    [Fact(Explicit = true)]
    public void CaptureProjectsCurrentLiveSourceFactsWithoutChangingWorldOrRng()
    {
        using var fixture = CreateFixture();
        var build = fixture.CreateBuild(5, 5);
        var world = CreateWorld(fixture, build);
        var player = Participant(fixture, build, 0);
        var random = new DeterministicLegacyRandom(0x12345678);
        var projectile = LaunchAtPreparedRelease(fixture, world,
            Processor(fixture, world, random), player);
        var source = world.CombatTargets[projectile.SourceEntityId];
        var first = Assert.Single(world.CaptureNativeCatacombsArrows());
        var destination = Enumerable.Range(0, 8)
            .Select(direction => DeterministicGridDirections.GetDisplacement(
                (DeterministicGridDirection)direction))
            .Select(offset => (X: source.PositionX + offset.X, Y: source.PositionY + offset.Y))
            .First(tile => world.IsWalkable(source.LevelId, tile.X, tile.Y)
                && !world.IsOccupied(source.LevelId, tile.X, tile.Y, [player], source.EntityId));
        source.ApplyKnockback(destination.X, destination.Y);
        source.MonsterLevel++;
        source.MinDamage = 3;
        source.MaxDamage = 7;
        var actorBefore = JsonSerializer.Serialize(source.CaptureCohortState());
        var projectileBefore = JsonSerializer.Serialize(projectile);

        var live = Assert.Single(world.CaptureNativeCatacombsArrows());

        Assert.Equal((source.PositionX, source.PositionY),
            (live.SourcePositionX, live.SourcePositionY));
        Assert.Equal(source.MonsterLevel, live.SourceMonsterLevel);
        Assert.Equal(3, live.SourceMinimumDamage);
        Assert.Equal(7, live.SourceMaximumDamage);
        Assert.Equal(first.StartX, live.StartX);
        Assert.Equal(first.StartY, live.StartY);
        Assert.Equal(first.MissileDistance, live.MissileDistance);
        Assert.Equal(live, Assert.Single(world.CaptureNativeCatacombsArrows()));
        Assert.Equal(actorBefore, JsonSerializer.Serialize(source.CaptureCohortState()));
        Assert.Equal(projectileBefore, JsonSerializer.Serialize(projectile));
        Assert.Equal(0x12345678U, random.State);
        world.ValidateNativePopulationState([player]);
    }

    [Theory(Explicit = true)]
    [InlineData(0U)]
    [InlineData(2U)]
    public void PublishingAnotherC2FloorRetainsExistingFlightExactly(uint classId)
    {
        using var fixture = CreateFixture(classId);
        var first = fixture.CreateBuild(5, 5);
        var world = CreateWorld(fixture, first);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture, first, classId);
        LaunchAtPreparedRelease(fixture, world, Processor(fixture, world), player);
        var beforeArrows = JsonSerializer.Serialize(world.CaptureNativeCatacombsArrows());
        var beforeProjectiles = JsonSerializer.Serialize(world.Projectiles.Values);
        var next = fixture.CreateBuild(6, 6, world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet());
        var before = Snapshot(world);
        var admission = world.PrepareNativeCatacombsFloorAdmission(next, fixture.Items, fixture.Spells,
            fixture.Activation.MonsterMissiles!, [player]);
        Assert.Equal(before, Snapshot(world));

        world.CommitNativeCatacombsFloorAdmission(admission);

        Assert.True(world.OwnsNativePopulationLevel(6));
        Assert.Equal(beforeArrows, JsonSerializer.Serialize(world.CaptureNativeCatacombsArrows()));
        Assert.Equal(beforeProjectiles, JsonSerializer.Serialize(world.Projectiles.Values));
        world.ValidateNativePopulationState([player]);
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedLiveOrDetachedFlightRefusesPublicationBeforeAnyWrite(bool detached)
    {
        using var fixture = CreateFixture();
        var first = fixture.CreateBuild(5, 5);
        var world = CreateWorld(fixture, first);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture, first, 0);
        var processor = Processor(fixture, world);
        LaunchAtPreparedRelease(fixture, world, processor, player);
        foreach (var actor in world.CombatTargets.Values)
            actor.IsPetrified = true;
        var next = fixture.CreateBuild(6, 6, world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet());
        var admission = world.PrepareNativeCatacombsFloorAdmission(next, fixture.Items, fixture.Spells,
            fixture.Activation.MonsterMissiles!, [player]);
        if (detached)
            LaunchAtPreparedRelease(fixture, admission.Floor, Processor(fixture, admission.Floor),
                Participant(fixture, next, 0));
        else
            processor.AdvanceTick(1, [player]);
        var before = Snapshot(world);

        Assert.Throws<InvalidOperationException>(() => world.CommitNativeCatacombsFloorAdmission(admission));

        Assert.Equal(before, Snapshot(world));
        Assert.False(world.OwnsNativePopulationLevel(6));
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrReboundProjectileCannotBeCaptured(bool remove)
    {
        using var fixture = CreateFixture();
        var build = fixture.CreateBuild(5, 5);
        var world = CreateWorld(fixture, build);
        var player = Participant(fixture, build, 0);
        var projectile = LaunchAtPreparedRelease(fixture, world, Processor(fixture, world), player);
        if (remove)
            world.Projectiles.Remove(projectile.EntityId);
        else
            projectile.TargetX = projectile.TargetX == 111 ? 110 : projectile.TargetX + 1;

        Assert.Throws<InvalidDataException>(() => world.CaptureNativeCatacombsArrows());
    }

    private static DiabloWorldState CreateWorld(QualifiedCatacombsBuilderFixture fixture,
        AuthoritativeCatacombsNativePopulationBuild build)
        => DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells,
            fixture.Activation.MonsterMissiles!);

    private static AuthoritativePlayerState Participant(QualifiedCatacombsBuilderFixture fixture,
        AuthoritativeCatacombsNativePopulationBuild build, uint classId)
    {
        var hero = build.Ready.Request.FreshLevel.Request.Hero;
        var player = fixture.Activation.PlayerLoadouts.CreateTemplate("catacombs-arrow-owner", classId,
            null, null, null, startingLevelId: build.Policy.LevelId,
            startingPositionX: hero.PreEntryFuture.X, startingPositionY: hero.PreEntryFuture.Y).CreatePlayer();
        player.EntityId = 0x70000001;
        return player;
    }

    private static DiabloCombatCommandProcessor Processor(QualifiedCatacombsBuilderFixture fixture,
        DiabloWorldState world, DeterministicLegacyRandom? random = null)
        => new(world, fixture.Spells, new AuthoritativeCombatRules(10, 1, 100),
            random ?? new DeterministicLegacyRandom(0x12345678),
            monsterMissiles: fixture.Activation.MonsterMissiles!,
            monsterTypes: fixture.Activation.TrustedOptions!.MonsterTypes);

    /** Prepare a legitimate release without advancing arbitrary generated geometry/AI. */
    private static AuthoritativeProjectile LaunchAtPreparedRelease(QualifiedCatacombsBuilderFixture fixture,
        DiabloWorldState world, DiabloCombatCommandProcessor processor, AuthoritativePlayerState player)
    {
        var source = world.CombatTargets.Values.Where(value => value.LevelId == player.LevelId
                && value.NativeAi is "SkeletonRanged" or "GoatRanged"
                && value.NativeInitialization!.InitialState.Mode == 0)
            .OrderBy(value => value.NativeInitialization!.Binding.NativeSlot).First();
        Assert.True(fixture.Activation.MonsterMissiles!.TryGet(source.NativeAi!, source.MonsterId, out var missile));
        world.StartNativeCatacombsRangedAction(source, missile, player, 123);
        for (var tick = 0; tick < 256; tick++) {
            var step = world.AdvanceNativeCatacombsRangedAction(source, missile,
                player.FuturePositionX, player.FuturePositionY);
            if (step.Release) {
                var ids = world.Projectiles.Keys.ToHashSet();
                Emit(processor, source, player, step.Action, missile);
                return Assert.Single(world.Projectiles.Values, value => !ids.Contains(value.EntityId));
            }
            Assert.False(step.Completed, "The selected prepared Arrow owner must have a reachable effect frame.");
        }
        throw new InvalidOperationException("The prepared ranged release exceeded its bounded clock.");
    }

    private static void Emit(DiabloCombatCommandProcessor processor, AuthoritativeCombatTarget source,
        AuthoritativePlayerState player, AuthoritativeNativeCatacombsRangedAction action,
        AuthoritativeMonsterMissileDefinition missile)
    {
        var method = typeof(DiabloCombatCommandProcessor).GetMethod("CreateNativeCatacombsArrowProjectile",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(processor, [source, player, action, missile]);
    }

    private static string Snapshot(DiabloWorldState world)
        => JsonSerializer.Serialize(new {
            Geometry = world.LevelGeometry, Backing = world.World!.Levels,
            Actors = world.CombatTargets.Values.Select(value => value.CaptureCohortState()).ToArray(),
            Projectiles = world.Projectiles.Values.ToArray(),
            Arrows = world.CaptureNativeCatacombsArrows(),
            Ranged = world.CaptureNativeCatacombsRangedActions(), Items = world.CaptureCathedralItems(),
            Doors = world.CaptureNativeCatacombsDoorStates(), Phases = world.CaptureNativeCatacombsMonsterPhases(),
        });
}
