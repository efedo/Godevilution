using System.Text.Json;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralNativePopulationIntegrationTests;

namespace Devilution.Diablo.Tests;

/** Generated C2 floors remain detached until every mutable publication dependency is checked. */
public sealed class AuthoritativeCatacombsFloorAdmissionTests
{
    [Theory(Explicit = true)]
    [InlineData(0U)]
    [InlineData(2U)]
    public void SecondCatacombsFloorRetainsIndependentDoorsPhasesAndItemPools(uint classId)
    {
        using var fixture = CreateFixture(classId);
        var first = fixture.CreateBuild(5, 5);
        var world = DiabloWorldState.CreateNativeCatacombsFloor(first, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture.Activation, first, classId);
        var firstDoor = world.CaptureNativeCatacombsDoorStates().Single().Doors.First();
        Assert.True(world.TryOperateNativeCatacombsDoor(firstDoor.EntityId, [player]));
        var beforeDoors = JsonSerializer.Serialize(world.CaptureNativeCatacombsDoorStates().Single());
        var beforePhases = JsonSerializer.Serialize(world.CaptureNativeCatacombsMonsterPhases(5));
        StartRanged(world, fixture, player.EntityId);
        var beforeRanged = JsonSerializer.Serialize(world.CaptureNativeCatacombsRangedActions(5));
        var beforeItems = JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single());
        var history = world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet();
        var next = fixture.CreateBuild(6, 6, history);
        var before = Snapshot(world);
        var admission = world.PrepareNativeCatacombsFloorAdmission(next, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!, [player]);
        Assert.Equal(before, Snapshot(world));
        Assert.False(world.OwnsNativePopulationLevel(6));
        world.CommitNativeCatacombsFloorAdmission(admission);
        Assert.True(world.OwnsNativePopulationLevel(5));
        Assert.True(world.OwnsNativePopulationLevel(6));
        world.ValidateNativePopulationState([player]);
        Assert.Equal(beforeDoors, JsonSerializer.Serialize(world.CaptureNativeCatacombsDoorStates().Single(row => row.LevelId == 5)));
        Assert.Equal(beforePhases, JsonSerializer.Serialize(world.CaptureNativeCatacombsMonsterPhases(5)));
        Assert.Equal(beforeRanged, JsonSerializer.Serialize(world.CaptureNativeCatacombsRangedActions(5)));
        StartRanged(world, fixture, player.EntityId, levelId: 6);
        Assert.Single(world.CaptureNativeCatacombsRangedActions(6));
        Assert.Equal(beforeItems, JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single(row => row.LevelId == 5)));
        Assert.Equal(new uint[] { 5, 6 }, world.CaptureCathedralItems()!.Levels.Select(row => row.LevelId));
        Assert.Equal(new uint[] { 5, 6 }, world.CaptureNativeCatacombsDoorStates().Select(row => row.LevelId));
        Assert.Equal(next.Monsters.GargoylePhases, world.CaptureNativeCatacombsMonsterPhases(6));
        Assert.Throws<InvalidDataException>(() => world.CaptureCathedralItems()!.ValidateShape());
        Assert.Throws<InvalidDataException>(() => world.CaptureCathedralDoors());
        var published = Snapshot(world);
        Assert.Throws<InvalidOperationException>(() => world.CommitNativeCatacombsFloorAdmission(admission));
        Assert.Equal(published, Snapshot(world));
    }

    [Theory(Explicit = true)]
    [InlineData(0U)]
    [InlineData(2U)]
    public void CathedralAndCatacombsOwnersShareOneExplicitFamilyItemRegistry(uint classId)
    {
        using var fixture = CreateFixture(classId);
        var world = fixture.Activation.Simulation.WorldState;
        world.World!.AddLevel(new AuthoritativeLevel(5, 112, 112));
        var history = world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet();
        var build = fixture.CreateBuild(5, 5, history);
        var player = Participant(fixture.Activation, build, classId);
        player.LevelId = 1;
        var beforeDoors = JsonSerializer.Serialize(world.CaptureCathedralDoorObjects());
        var beforeItems = JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single());
        var before = Snapshot(world);
        var admission = world.PrepareNativeCatacombsFloorAdmission(build, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!, [player]);
        Assert.Equal(before, Snapshot(world));
        world.CommitNativeCatacombsFloorAdmission(admission);
        Assert.True(world.OwnsNativePopulationLevel(1));
        Assert.True(world.OwnsNativePopulationLevel(5));
        world.ValidateNativePopulationState([player]);
        Assert.Equal(new uint[] { 1, 5 }, world.CaptureCathedralItems()!.Levels.Select(row => row.LevelId));
        Assert.Equal(beforeDoors, JsonSerializer.Serialize(world.CaptureCathedralDoorObjects()));
        Assert.Equal(beforeItems, JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single(row => row.LevelId == 1)));
    }

    [Fact(Explicit = true)]
    public void ExtraInitialUniqueHistoryIsRejectedBeforePublication()
    {
        using var fixture = CreateFixture();
        var first = fixture.CreateBuild(5, 5);
        var world = DiabloWorldState.CreateNativeCatacombsFloor(first, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture.Activation, first, 0);
        var history = world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet();
        history.Add(Enumerable.Range(0, 128).First(flag => !history.Contains(flag)));
        var next = fixture.CreateBuild(6, 6, history);
        var before = Snapshot(world);
        Assert.Throws<InvalidDataException>(() => world.PrepareNativeCatacombsFloorAdmission(
            next, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!, [player]));
        Assert.Equal(before, Snapshot(world));
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedParticipantOrDetachedObjectCannotPublish(bool changeObject)
    {
        using var fixture = CreateFixture();
        var first = fixture.CreateBuild(5, 5);
        var world = DiabloWorldState.CreateNativeCatacombsFloor(first, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture.Activation, first, 0);
        var next = fixture.CreateBuild(6, 6, world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet());
        var admission = world.PrepareNativeCatacombsFloorAdmission(next, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!, [player]);
        if (changeObject)
            admission.Floor.Objects.Values.First().AnimationFrame++;
        else
            player.Gold++;
        var before = Snapshot(world);
        if (changeObject)
            Assert.Throws<InvalidDataException>(() => world.CommitNativeCatacombsFloorAdmission(admission));
        else
            Assert.Throws<InvalidOperationException>(() => world.CommitNativeCatacombsFloorAdmission(admission));
        Assert.Equal(before, Snapshot(world));
        Assert.False(world.OwnsNativePopulationLevel(6));
    }

    [Fact(Explicit = true)]
    public void ExistingActorChangeMakesAdmissionStaleWithoutReplacingGeometryOrItems()
    {
        using var fixture = CreateFixture();
        var first = fixture.CreateBuild(5, 5);
        var world = DiabloWorldState.CreateNativeCatacombsFloor(first, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture.Activation, first, 0);
        var next = fixture.CreateBuild(6, 6, world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet());
        var admission = world.PrepareNativeCatacombsFloorAdmission(next, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!, [player]);
        world.CombatTargets.Values.First(target => target.HitPointsQ6 > 128).ApplyDamageQ6(64);
        var before = Snapshot(world);
        Assert.Throws<InvalidOperationException>(() => world.CommitNativeCatacombsFloorAdmission(admission));
        Assert.Equal(before, Snapshot(world));
        Assert.False(world.OwnsNativePopulationLevel(6));
    }

    [Theory(Explicit = true)]
    [InlineData(0U)]
    [InlineData(2U)]
    public void LaterFloorBindsEarnedHeroResourcesInsteadOfCanonicalStarterValues(uint classId)
    {
        using var fixture = CreateFixture(classId);
        var first = fixture.CreateBuild(5, 5);
        var world = DiabloWorldState.CreateNativeCatacombsFloor(first, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture.Activation, first, classId);
        player.AdvanceLevel();
        var hero = first.Ready.Request.FreshLevel.Request.Hero with {
            MaximumLifeBase = player.BaseLifeMaximum, MaximumManaBase = player.BaseManaMaximum,
            LightRadius = player.LightRadius,
        };
        var next = fixture.CreateBuild(6, 6, world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet(), hero);
        var admission = world.PrepareNativeCatacombsFloorAdmission(next, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!, [player]);
        world.CommitNativeCatacombsFloorAdmission(admission);
        world.ValidateNativePopulationState([player]);
    }

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedLiveOrDetachedRangedClockCannotPublish(bool detached)
    {
        using var fixture = CreateFixture();
        var first = fixture.CreateBuild(5, 5);
        var world = DiabloWorldState.CreateNativeCatacombsFloor(first, fixture.Items, fixture.Spells,
            fixture.Activation.MonsterMissiles!);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture.Activation, first, 0);
        var live = StartRanged(world, fixture, player.EntityId);
        var next = fixture.CreateBuild(6, 6, world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet());
        var admission = world.PrepareNativeCatacombsFloorAdmission(next, fixture.Items, fixture.Spells,
            fixture.Activation.MonsterMissiles!, [player]);
        if (detached)
            StartRanged(admission.Floor, fixture, player.EntityId, levelId: 6);
        else {
            Assert.True(fixture.Activation.MonsterMissiles!.TryGet(live.NativeAi!, live.MonsterId, out var missile));
            world.AdvanceNativeCatacombsRangedAction(live, missile, null, null);
        }
        var before = Snapshot(world);
        Assert.Throws<InvalidOperationException>(() => world.CommitNativeCatacombsFloorAdmission(admission));
        Assert.Equal(before, Snapshot(world));
        Assert.False(world.OwnsNativePopulationLevel(6));
    }

    [Fact(Explicit = true)]
    public void TrustedRangedMappingChangesAndUnknownSpellsCannotEnterFloorOwners()
    {
        using var fixture = CreateFixture();
        var first = fixture.CreateBuild(5, 5);
        var missiles = fixture.Activation.MonsterMissiles!;
        var world = DiabloWorldState.CreateNativeCatacombsFloor(first, fixture.Items, fixture.Spells, missiles);
        world.AddLevel(new AuthoritativeLevel(6, 112, 112));
        var player = Participant(fixture.Activation, first, 0);
        var next = fixture.CreateBuild(6, 6, world.CaptureCathedralItems()!.UniqueItemFlags.ToHashSet());
        var changed = new AuthoritativeMonsterMissileCatalog(missiles.Definitions.Select(row =>
            row.NativeAi == "SkeletonRanged" ? row with { ProjectileSpeed = row.ProjectileSpeed + 1 } : row));
        var before = Snapshot(world);
        Assert.Throws<InvalidDataException>(() => world.PrepareNativeCatacombsFloorAdmission(
            next, fixture.Items, fixture.Spells, changed, [player]));
        var unknownSpell = new AuthoritativeMonsterMissileCatalog(missiles.Definitions.Select(row =>
            row.NativeAi == "Acid" ? row with { SpellId = uint.MaxValue } : row));
        Assert.Throws<InvalidDataException>(() => DiabloWorldState.CreateNativeCatacombsFloor(
            next, fixture.Items, fixture.Spells, unknownSpell));
        Assert.Equal(before, Snapshot(world));
        Assert.False(world.OwnsNativePopulationLevel(6));
    }

    private static AuthoritativeCombatTarget StartRanged(DiabloWorldState world,
        QualifiedCatacombsBuilderFixture fixture, uint targetEntityId, uint levelId = 5)
    {
        var target = world.CombatTargets.Values.First(value => value.LevelId == levelId
            && value.NativeAi == "SkeletonRanged" && value.NativeInitialization!.InitialState.Mode == 0);
        Assert.True(fixture.Activation.MonsterMissiles!.TryGet(target.NativeAi!, target.MonsterId, out var missile));
        var victim = AuthoritativeCatacombsMonsterRuntimeAdapterTests.CreateProcessorPlayer(
            target.PositionX + 4, target.PositionY);
        victim.EntityId = targetEntityId;
        victim.LevelId = levelId;
        world.StartNativeCatacombsRangedAction(target, missile, victim, attackSeed: 123);
        return target;
    }

    private static AuthoritativePlayerState Participant(DiabloContentActivation activation,
        AuthoritativeCatacombsNativePopulationBuild build, uint classId)
    {
        var hero = build.Ready.Request.FreshLevel.Request.Hero;
        var player = activation.PlayerLoadouts.CreateTemplate("catacombs-admission", classId,
            null, null, null, startingLevelId: build.Policy.LevelId,
            startingPositionX: hero.PreEntryFuture.X, startingPositionY: hero.PreEntryFuture.Y).CreatePlayer();
        player.EntityId = 0x70000001;
        return player;
    }

    private static string Snapshot(DiabloWorldState world)
        => JsonSerializer.Serialize(new {
            Backing = world.World!.Levels, Effective = world.LevelGeometry,
            Actors = world.CombatTargets.Values.Select(target => target.CaptureCohortState()).ToArray(),
            Objects = world.Objects.Values.Select(DiabloSimulation.CaptureObject).ToArray(),
            Items = world.CaptureCathedralItems(), Doors = world.CaptureNativeCatacombsDoorStates(),
            Phases = world.CaptureNativeCatacombsMonsterPhases(),
            Ranged = world.CaptureNativeCatacombsRangedActions(),
        });
}
