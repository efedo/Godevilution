using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class ProcessorCohortStateTests
{
    [Fact]
    public void WorldCommandAllocatorRoundTripPreservesIndependentCursorsAndHoles()
    {
        var processor = new DiabloWorldCommandProcessor(new DiabloWorldState());
        var expected = new DiabloWorldCommandAllocatorState(
            uint.MaxValue,
            0,
            0x10000017);

        processor.RestoreCohortAllocators(expected);

        var captured = processor.CaptureCohortAllocators();
        Assert.Equal(expected.NextWorldItemEntityId, captured.NextWorldItemEntityId);
        Assert.Equal(expected.NextAuthoredProjectileEntityId, captured.NextAuthoredProjectileEntityId);
        Assert.Equal(expected.NextGeneratedMonsterEntityId, captured.NextGeneratedMonsterEntityId);
        processor.AssertCohortCaptureBoundary();
    }

    [Fact]
    public void CombatAllocatorRoundTripPreservesIndependentCursorsAndExhaustion()
    {
        var processor = new DiabloCombatCommandProcessor(
            new DiabloWorldState(),
            DiabloSimulationFactory.Instance.Create(
                new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules)).Spells,
            new AuthoritativeCombatRules(10, 1, 100));
        var expected = new DiabloCombatCommandAllocatorState(
            0x40000023,
            uint.MaxValue,
            0);

        processor.RestoreCohortAllocators(expected);

        var captured = processor.CaptureCohortAllocators();
        Assert.Equal(expected.NextWorldItemEntityId, captured.NextWorldItemEntityId);
        Assert.Equal(expected.NextMonsterEntityId, captured.NextMonsterEntityId);
        Assert.Equal(expected.NextProjectileEntityId, captured.NextProjectileEntityId);
    }

    [Fact]
    public void WorldInfrastructureCaptureSortsAndPreservesNullablePortalClock()
    {
        var world = new DiabloWorldState(
            fallbackWidth: 5,
            fallbackHeight: 4,
            fallbackBlockedCells: [8, 1],
            fallbackMissileBlockedCells: [9, 2]);

        var emptyClock = world.CaptureCohortInfrastructure();
        Assert.Equal([1, 8], emptyClock.BlockedCells);
        Assert.Equal([2, 9], emptyClock.MissileBlockedCells);
        Assert.Null(emptyClock.LastPortalTick);

        world.AdvancePortals(0);
        var zeroClock = world.CaptureCohortInfrastructure();
        Assert.Equal((ulong)0, zeroClock.LastPortalTick);

        world.RestoreCohortInfrastructure(emptyClock);
        Assert.Null(world.CaptureCohortInfrastructure().LastPortalTick);
    }

    [Fact]
    public void WorldInfrastructureRestoresMutationProjectionAfterFinalBackingStateIsInstalled()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(0, 4, 4));
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                0,
                blockedCellsAdd: [5],
                missileBlockedCellsAdd: [6]),
        ]);
        var source = new DiabloWorldState(
            geometry,
            worldMutations: mutations,
            activeWorldMutationIds: [7]);
        var captured = source.CaptureCohortInfrastructure();
        var candidate = new DiabloWorldState(
            geometry,
            worldMutations: mutations,
            deferCohortInitialization: true);

        candidate.RestoreCohortInfrastructure(captured);

        Assert.False(candidate.IsWalkable(0, 1, 1));
        Assert.True(candidate.IsMissileGeometryBlocked(0, 2, 1));
        Assert.Equal([7U], candidate.ActiveWorldMutationIds);
    }

    [Fact]
    public void WorldInfrastructureRejectsMalformedCollectionsBeforeChangingState()
    {
        var world = new DiabloWorldState(
            fallbackWidth: 4,
            fallbackHeight: 4,
            fallbackBlockedCells: [1],
            fallbackMissileBlockedCells: [2]);
        var original = world.CaptureCohortInfrastructure();

        Assert.Throws<InvalidDataException>(() => world.RestoreCohortInfrastructure(
            new DiabloWorldInfrastructureState(
                4,
                4,
                [2, 1],
                [2],
                [],
                null,
                [])));
        Assert.Throws<InvalidDataException>(() => world.RestoreCohortInfrastructure(
            new DiabloWorldInfrastructureState(
                4,
                4,
                [1],
                [16],
                [],
                null,
                [])));
        Assert.Throws<InvalidDataException>(() => world.RestoreCohortInfrastructure(
            new DiabloWorldInfrastructureState(
                4,
                4,
                [1],
                [2],
                [3, 2],
                null,
                [])));
        Assert.Throws<InvalidDataException>(() => world.RestoreCohortInfrastructure(
            new DiabloWorldInfrastructureState(
                4,
                4,
                [1],
                [2],
                [3],
                null,
                [])));
        Assert.Throws<InvalidDataException>(() => world.RestoreCohortInfrastructure(
            new DiabloWorldInfrastructureState(
                5,
                4,
                [1],
                [2],
                [],
                null,
                [])));

        var unchanged = world.CaptureCohortInfrastructure();
        Assert.Equal(original.FallbackWidth, unchanged.FallbackWidth);
        Assert.Equal(original.FallbackHeight, unchanged.FallbackHeight);
        Assert.Equal(original.BlockedCells, unchanged.BlockedCells);
        Assert.Equal(original.MissileBlockedCells, unchanged.MissileBlockedCells);
        Assert.Equal(original.ActiveWorldMutationIds, unchanged.ActiveWorldMutationIds);
        Assert.Equal(original.LastPortalTick, unchanged.LastPortalTick);
    }

    [Fact]
    public void DeferredWorldConstructionLeavesDerivedProjectionUntilRestore()
    {
        var sourceGeometry = new AuthoritativeWorld();
        sourceGeometry.AddLevel(new AuthoritativeLevel(0, 3, 3));
        var captured = new DiabloWorldState(sourceGeometry)
            .CaptureCohortInfrastructure();
        var candidateGeometry = new AuthoritativeWorld();
        candidateGeometry.AddLevel(new AuthoritativeLevel(0, 3, 3));
        var world = new DiabloWorldState(
            candidateGeometry,
            deferCohortInitialization: true);

        Assert.Empty(world.LevelGeometry);
        world.RestoreCohortInfrastructure(captured);

        Assert.Single(world.LevelGeometry);
        Assert.True(world.IsWalkable(0, 1, 1));
    }

    [Fact]
    public void DeferredWorldResolverAttachmentDoesNotProjectBeforeRestore()
    {
        var sourceGeometry = new AuthoritativeWorld();
        sourceGeometry.AddLevel(new AuthoritativeLevel(0, 3, 3));
        var captured = new DiabloWorldState(sourceGeometry)
            .CaptureCohortInfrastructure();
        var candidateGeometry = new AuthoritativeWorld();
        candidateGeometry.AddLevel(new AuthoritativeLevel(0, 3, 3));
        var world = new DiabloWorldState(
            candidateGeometry,
            deferCohortInitialization: true);

        world.SetSetLevelOriginResolver(_ => new AuthoritativeSetLevelObjectOrigin(1, 1, 1));
        world.SetProceduralQuestPieceOriginResolver(
            _ => new AuthoritativeProceduralQuestPieceOrigin(0, 1, 1));

        Assert.Empty(world.LevelGeometry);
        world.RestoreCohortInfrastructure(captured);

        Assert.Single(world.LevelGeometry);
    }

    [Fact]
    public void DeferredRestoreIgnoresInactiveAnchoredMutationFootprints()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(0, 3, 3));
        var target = new AuthoritativeCombatTarget(
            7,
            2,
            2,
            10,
            unique: true,
            uniqueIdentity: "roaming-unique");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                11,
                0,
                geometryRectanglesAdd: [
                    new AuthoritativeWorldMutationRectangle(0, 0, 2, 2),
                ],
                anchorUniqueIdentity: "roaming-unique"),
        ]);
        var world = new DiabloWorldState(
            geometry,
            combatTargets: [target],
            worldMutations: mutations,
            deferCohortInitialization: true);
        var captured = new DiabloWorldState(geometry)
            .CaptureCohortInfrastructure();

        world.RestoreCohortInfrastructure(captured);

        Assert.True(world.IsWalkable(0, 2, 2));
        Assert.Empty(world.ActiveWorldMutationIds);
    }

    [Fact]
    public void CohortCaptureRetainsCachedAnchorProjectionUntilExplicitRefresh()
    {
        var sourceGeometry = new AuthoritativeWorld();
        sourceGeometry.AddLevel(new AuthoritativeLevel(0, 5, 5));
        var sourceTarget = new AuthoritativeCombatTarget(
            7,
            1,
            1,
            10,
            unique: true,
            uniqueIdentity: "roaming-unique");
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                12,
                0,
                geometryRectanglesAdd: [
                    new AuthoritativeWorldMutationRectangle(0, 0, 1, 1),
                ],
                anchorUniqueIdentity: "roaming-unique"),
        ]);
        var source = new DiabloWorldState(
            sourceGeometry,
            combatTargets: [sourceTarget],
            worldMutations: mutations,
            activeWorldMutationIds: [12]);
        sourceTarget.PositionX = 3;
        sourceTarget.PositionY = 3;
        var captured = source.CaptureCohortInfrastructure();

        var candidateGeometry = new AuthoritativeWorld();
        candidateGeometry.AddLevel(new AuthoritativeLevel(0, 5, 5));
        var candidateTarget = new AuthoritativeCombatTarget(
            7,
            3,
            3,
            10,
            unique: true,
            uniqueIdentity: "roaming-unique");
        var candidate = new DiabloWorldState(
            candidateGeometry,
            combatTargets: [candidateTarget],
            worldMutations: mutations,
            deferCohortInitialization: true);

        candidate.RestoreCohortInfrastructure(captured);

        Assert.False(candidate.IsWalkable(0, 1, 1));
        Assert.True(candidate.IsWalkable(0, 3, 3));
        candidate.RefreshProjectedGeometry();
        Assert.True(candidate.IsWalkable(0, 1, 1));
        Assert.False(candidate.IsWalkable(0, 3, 3));
    }

    [Fact]
    public void EffectiveProjectionRestoreRejectsMalformedCellsAndBackingPolicyChanges()
    {
        var sourceGeometry = new AuthoritativeWorld();
        sourceGeometry.AddLevel(new AuthoritativeLevel(0, 3, 3));
        var captured = new DiabloWorldState(sourceGeometry)
            .CaptureCohortInfrastructure();
        var candidateGeometry = new AuthoritativeWorld();
        candidateGeometry.AddLevel(new AuthoritativeLevel(0, 3, 3));
        var candidate = new DiabloWorldState(
            candidateGeometry,
            deferCohortInitialization: true);
        var level = Assert.Single(captured.EffectiveLevels!);

        Assert.Throws<InvalidDataException>(() => candidate.RestoreCohortInfrastructure(
            captured with {
                EffectiveLevels = [level with { BlockedCells = [1, 1] }],
            }));
        Assert.Throws<InvalidDataException>(() => candidate.RestoreCohortInfrastructure(
            captured with {
                EffectiveLevels = [level with { NativeVisionBlockedCells = [1] }],
            }));
        Assert.Throws<InvalidDataException>(() => candidate.RestoreCohortInfrastructure(
            captured with {
                EffectiveLevels = [level with { IsDefaultStart = true }],
            }));

        Assert.Empty(candidate.LevelGeometry);
    }
}
