using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCatacombsMonsterRuntimeAdapterTests
{
    [Fact]
    public void LegacyAdapterKeepsFrozenGargoyleAsNonStandAndDoesNotImportSpecialCursor()
    {
        var state = CreateState(CreateFrozenGargoyle());
        var (catalog, bestiary) = CreateMetadata();
        var legacy = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
            state, catalog, Fingerprint, Bindings(7), populationMonsters: bestiary);

        Assert.Null(Assert.Single(legacy.Records).Target);
        Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.NonStandMode,
            legacy.Records[0].UnsupportedReason);
        Assert.Equal(0, legacy.Records[0].Initialization.NativeSpecialAttackFrameCount);
        Assert.Equal(0, legacy.Records[0].Initialization.NativeSpecialAttackTicksPerFrame);
        Assert.Equal(0, legacy.Records[0].Initialization.CatacombsDepth);
    }

    [Fact]
    public void CatacombsAdapterAdmitsExactFrozenStatueAndBindsRawSpecialAnimationMetadata()
    {
        var state = CreateState(CreateFrozenGargoyle());
        var (catalog, bestiary) = CreateMetadata();
        var result = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, state, catalog, Fingerprint, Bindings(7), bestiary);

        var runtime = result.SharedRuntime;
        var record = Assert.Single(runtime.Records);
        Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None, record.UnsupportedReason);
        Assert.NotNull(record.Target);
        Assert.False(record.Target!.GargoyleSpecialActive);
        Assert.Equal((14, 2), (record.Initialization.NativeSpecialAttackFrameCount,
            record.Initialization.NativeSpecialAttackTicksPerFrame));
        Assert.Equal(5, record.Initialization.CatacombsDepth);
        Assert.Equal(new AuthoritativeNativeCatacombsMonsterPhase(
            0, 501, 7, 81, Fingerprint, 14, 2, 0, 0,
            AuthoritativeNativeCatacombsMonsterPhaseState.Frozen), Assert.Single(result.GargoylePhases));
    }

    [Theory]
    [InlineData(22)]
    [InlineData(23)]
    public void CatacombsAdapterAdmitsOnlyExactRangedSkeletonSpecialStand(int nativeType)
    {
        var state = CreateState(CreateRangedSkeletonSpecialStand(nativeType));
        var (catalog, bestiary) = CreateMetadata();

        var catacombs = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, state, catalog, Fingerprint, Bindings(7), bestiary);

        var record = Assert.Single(catacombs.SharedRuntime.Records);
        Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None, record.UnsupportedReason);
        Assert.NotNull(record.Target);
        Assert.Equal(3, record.InitialState.Ai);
        Assert.Equal((3, 2), (record.Initialization.NativeSpecialAttackFrameCount,
            record.Initialization.NativeSpecialAttackTicksPerFrame));
        Assert.Equal(new AuthoritativeNativeInitialSpecialStand(0, 501, 7, 3, 2, 1, 1),
            Assert.Single(catacombs.SharedRuntime.InitialSpecialStandActors));

        var firstEntry = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptLevelEntryWithBindings(
            state, catalog, Fingerprint, Bindings(7), bestiary);
        Assert.Null(Assert.Single(firstEntry.Records).Target);
        Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.NonStandMode,
            firstEntry.Records[0].UnsupportedReason);
        Assert.Empty(firstEntry.InitialSpecialStandActors);
    }

    [Fact]
    public void CatacombsRangedSkeletonSpecialStandRejectsBadCursorAndUntrustedAiOrEffectMetadata()
    {
        var (catalog, bestiary) = CreateMetadata();
        var valid = CreateRangedSkeletonSpecialStand();
        foreach (var malformed in new[] {
            CreateState(valid with { AnimCurrentFrame = 3 }),
            CreateState(valid with { AnimTickCounter = 2 }),
        }) {
            Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
                5, 7, malformed, catalog, Fingerprint, Bindings(7), bestiary));
        }

        var mismatchedAi = CreateMetadata(rangedSkeletonAi: 2);
        Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(valid), mismatchedAi.Catalog, Fingerprint, Bindings(7), mismatchedAi.Bestiary));

        var mismatchedFrames = CreateMetadata(rangedSkeletonFrames: 4);
        Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(valid), mismatchedFrames.Catalog, Fingerprint, Bindings(7), mismatchedFrames.Bestiary));

        var mismatchedRate = CreateMetadata(rangedSkeletonTicks: 3);
        Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(valid), mismatchedRate.Catalog, Fingerprint, Bindings(7), mismatchedRate.Bestiary));

        var specialEffect = CreateMetadata(rangedSkeletonSpecialEffect: 1);
        Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(valid), specialEffect.Catalog, Fingerprint, Bindings(7), specialEffect.Bestiary));
    }

    [Fact]
    public void CatacombsAdapterRejectsMalformedStatuesAndUnclassifiedActiveRows()
    {
        var (catalog, bestiary) = CreateMetadata();
        foreach (var bad in new[] { CreateFrozenGargoyle() with { Flags = 0 },
            CreateFrozenGargoyle() with { AnimCurrentFrame = 1 },
            CreateFrozenGargoyle() with { Future = new(11, 10) } }) {
            var state = CreateState(bad);
            Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
                5, 7, state, catalog, Fingerprint, Bindings(7), bestiary));
        }
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(CreateFrozenGargoyle() with { UniqueType = 0 }),
            catalog, Fingerprint, Bindings(7), bestiary));

        var unsupported = CreateState(CreateFrozenGargoyle() with { Mode = 3 });
        Assert.Throws<NotSupportedException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, unsupported, catalog, Fingerprint, Bindings(7), bestiary));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            4, 7, CreateState(CreateFrozenGargoyle()), catalog, Fingerprint, Bindings(7), bestiary));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(CreateFrozenGargoyle()), catalog, Fingerprint,
            [new(1, 501, 801, 7)], bestiary));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(CreateFrozenGargoyle()), catalog, Fingerprint,
            Bindings(8), bestiary));
    }

    [Fact]
    public void CatacombsAdapterAdmitsTrustedUniqueBatOverrideAndLeashedGargoyleMinion()
    {
        var leader = CreateFrozenGargoyle() with {
            Mode = 0,
            Ai = 11,
            UniqueType = 42,
            Flags = 0,
            Tile = new(20, 20),
            Future = new(20, 20),
            Old = new(20, 20),
            PackSize = 2,
        };
        var follower = CreateFrozenGargoyle() with {
            Slot = 1,
            Tile = new(21, 20),
            Future = new(21, 20),
            Old = new(21, 20),
            Leader = 0,
            LeaderRelation = (int)AuthoritativeMonsterLeaderRelation.Leashed,
            PackSize = 2,
        };
        var state = CreateState(leader, follower);
        var (catalog, bestiary) = CreateMetadata();
        var unique = new AuthoritativeCathedralUniqueDefinition(
            42, 81, 7, 100, 11, 5, 1, 2, 0,
            0, 0, 0, 0, 0, 0,
            AuthoritativeCathedralUniquePack.Independent, 0, 0, -1, false);
        var admission = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, state, catalog, Fingerprint, Bindings(7, count: 2), bestiary,
            uniqueDefinitions: [unique], uniqueIdentities: new Dictionary<int, string> {
                [42] = "test-gargoyle-bat-override",
            });

        var records = admission.SharedRuntime.Records;
        var admittedLeader = Assert.IsType<AuthoritativeCombatTarget>(records[0].Target);
        var admittedFollower = Assert.IsType<AuthoritativeCombatTarget>(records[1].Target);
        Assert.True(admittedLeader.IsUnique);
        Assert.Equal("Bat", admittedLeader.NativeAi);
        Assert.Equal("test-gargoyle-bat-override", admittedLeader.NativeInitialization!.Binding.UniqueIdentity);
        Assert.Equal(5, admittedLeader.NativeInitialization.CatacombsDepth);
        Assert.Equal(5, admittedFollower.NativeInitialization!.CatacombsDepth);
        var phase = Assert.Single(admission.GargoylePhases);
        Assert.Equal(1, phase.NativeSlot);

        var world = new DiabloWorldState(combatTargets: [admittedLeader, admittedFollower]);
        world.ConfigureNativeCatacombsMonsterPhases(admission);
        DiabloCombatCommandProcessor.BeginNativeMelee(admittedLeader, usesSpecialAttack: true);
        Assert.False(world.ApplyNativeCatacombsGargoyleFollowerWakeBeforeAi(admittedFollower));
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Frozen,
            world.NativeCatacombsMonsterPhase(admittedFollower.EntityId)!.State);
        DiabloCombatCommandProcessor.InterruptNativeMelee(admittedLeader);
        Assert.True(world.ApplyNativeCatacombsGargoyleFollowerWakeBeforeAi(admittedFollower));
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Playing,
            world.NativeCatacombsMonsterPhase(admittedFollower.EntityId)!.State);
    }

    [Fact]
    public void CatacombsUniqueBatOverrideDoesNotUseBaseGargoyleHealingRetreatOrMovementTuning()
    {
        var baseGargoyleCatalog = CreateGargoyleBaseTuningCatalog();
        var healingCase = CreateUniqueBatOverride(
            baseGargoyleCatalog, hitPoints: 640, positionX: 10, positionY: 10);
        Assert.Equal("Bat", healingCase.Target.NativeAi);
        Assert.Equal(5, healingCase.Target.NativeInitialization!.CatacombsDepth);
        Assert.Equal(100, Assert.Single(baseGargoyleCatalog.Definitions).GargoyleHealingHealthThresholdPercent);

        var healingProcessor = CreateProcessor(healingCase.World, baseGargoyleCatalog);
        healingProcessor.AdvanceTick(1, [CreateProcessorPlayer(positionX: 18, positionY: 10)]);

        // The unique retains its ordinary seven-Q6 regeneration, without Gargoyle healing.
        Assert.Equal(647, healingCase.Target.HitPointsQ6);
        Assert.False(healingCase.Target.GargoyleSpecialActive);
        Assert.Equal(-1, healingCase.Target.AvoidanceMovementTicks);

        var retreatCase = CreateUniqueBatOverride(
            baseGargoyleCatalog, hitPoints: 640, positionX: 10, positionY: 10);
        var retreatProcessor = CreateProcessor(retreatCase.World, baseGargoyleCatalog);
        retreatProcessor.AdvanceTick(1, [CreateProcessorPlayer(positionX: 11, positionY: 10)]);

        Assert.False(retreatCase.Target.IsMoving);
        Assert.Equal((10, 10), (retreatCase.Target.PositionX, retreatCase.Target.PositionY));
        Assert.Equal(647, retreatCase.Target.HitPointsQ6);

        var movementCase = CreateUniqueBatOverride(
            baseGargoyleCatalog, hitPoints: 6400, positionX: 10, positionY: 10);
        var movementProcessor = CreateProcessor(movementCase.World, baseGargoyleCatalog);
        movementProcessor.AdvanceTick(1, [CreateProcessorPlayer(positionX: 14, positionY: 10)]);

        Assert.Equal(-1, movementCase.Target.AvoidanceMovementTicks);
        Assert.Equal(-1, movementCase.Target.AvoidanceMovementIdleTicks);
        Assert.Equal(0, movementCase.Target.AvoidanceMovementMode);
    }

    [Fact]
    public void ProcessorWakesFollowerOnTheSameTickOrNextTickAccordingToEntityOrder()
    {
        var leaderFirst = CreateGargoylePair(leaderEntityId: 501, followerEntityId: 502);
        AdvanceLeaderToLastFrame(leaderFirst.World, leaderFirst.Leader);
        var leaderFirstProcessor = CreateProcessor(leaderFirst.World);
        leaderFirstProcessor.AdvanceTick(1, [CreateProcessorPlayer()]);
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Completed,
            leaderFirst.World.NativeCatacombsMonsterPhase(leaderFirst.Leader.EntityId)!.State);
        var leaderFirstLeaderPhase = leaderFirst.World.NativeCatacombsMonsterPhase(leaderFirst.Leader.EntityId)!;
        var leaderFirstFollowerPhase = leaderFirst.World.NativeCatacombsMonsterPhase(leaderFirst.Follower.EntityId)!;
        Assert.Equal(0, leaderFirst.Leader.ActiveForTicks);
        Assert.Equal(0, leaderFirst.Follower.ActiveForTicks);
        Assert.Equal((0, 0), (leaderFirstLeaderPhase.FrameIndex, leaderFirstLeaderPhase.TickCounter));
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Playing, leaderFirstFollowerPhase.State);
        Assert.Equal((0, 1), (leaderFirstFollowerPhase.FrameIndex, leaderFirstFollowerPhase.TickCounter));

        var followerFirst = CreateGargoylePair(leaderEntityId: 502, followerEntityId: 501);
        AdvanceLeaderToLastFrame(followerFirst.World, followerFirst.Leader);
        var followerFirstProcessor = CreateProcessor(followerFirst.World);
        followerFirstProcessor.AdvanceTick(1, [CreateProcessorPlayer()]);
        var followerFirstLeaderPhase = followerFirst.World.NativeCatacombsMonsterPhase(followerFirst.Leader.EntityId)!;
        var followerFirstFollowerPhase = followerFirst.World.NativeCatacombsMonsterPhase(followerFirst.Follower.EntityId)!;
        Assert.Equal(0, followerFirst.Leader.ActiveForTicks);
        Assert.Equal(0, followerFirst.Follower.ActiveForTicks);
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Completed, followerFirstLeaderPhase.State);
        Assert.Equal((0, 0), (followerFirstLeaderPhase.FrameIndex, followerFirstLeaderPhase.TickCounter));
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Frozen, followerFirstFollowerPhase.State);
        Assert.Equal((0, 0), (followerFirstFollowerPhase.FrameIndex, followerFirstFollowerPhase.TickCounter));

        followerFirstProcessor.AdvanceTick(2, [CreateProcessorPlayer()]);
        var wokenFollowerPhase = followerFirst.World.NativeCatacombsMonsterPhase(followerFirst.Follower.EntityId)!;
        Assert.Equal(0, followerFirst.Leader.ActiveForTicks);
        Assert.Equal(0, followerFirst.Follower.ActiveForTicks);
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Playing, wokenFollowerPhase.State);
        Assert.Equal((0, 1), (wokenFollowerPhase.FrameIndex, wokenFollowerPhase.TickCounter));
    }

    [Fact]
    public void ProcessorDoesNotWakeFrozenCatacombsPackAtTickStart()
    {
        var pair = CreateGargoylePair(leaderEntityId: 501, followerEntityId: 502);
        var processor = CreateProcessor(pair.World);

        processor.AdvanceTick(1, [CreateProcessorPlayer()]);

        foreach (var target in new[] { pair.Leader, pair.Follower }) {
            Assert.Equal(0, target.ActiveForTicks);
            var phase = pair.World.NativeCatacombsMonsterPhase(target.EntityId)!;
            Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Frozen, phase.State);
            Assert.Equal((0, 0), (phase.FrameIndex, phase.TickCounter));
        }
    }

    [Fact]
    public void GroupUnityCompletionHookWakesFrozenCatacombsGargoyleLeaderFromLeashedMember()
    {
        var leader = CreateFrozenGargoyle() with { PackSize = 1 };
        var member = CreateFrozenGargoyle() with {
            Slot = 1,
            Type = 1,
            Mode = 0,
            Ai = 0,
            Flags = 0,
            Tile = new(11, 10),
            Future = new(11, 10),
            Old = new(11, 10),
            Leader = 0,
            LeaderRelation = (int)AuthoritativeMonsterLeaderRelation.Leashed,
            PackSize = 0,
        };
        var (catalog, bestiary) = CreateMetadata();
        var admission = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(leader, member), catalog, Fingerprint,
            Bindings(7, count: 2), bestiary);
        var targets = admission.SharedRuntime.Records
            .Select(record => Assert.IsType<AuthoritativeCombatTarget>(record.Target))
            .ToArray();
        var world = new DiabloWorldState(combatTargets: targets);
        world.ConfigureNativeCatacombsMonsterPhases(admission);

        Assert.True(AuthoritativeMonsterGroupUnity.Apply(
            targets[1], world.CombatTargets, world.HasAiLineOfSight));
        Assert.True(world.ApplyNativeCatacombsGargoyleLeaderWakeAfterGroupUnity(targets[1]));
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Playing,
            world.NativeCatacombsMonsterPhase(targets[0].EntityId)!.State);
    }

    [Fact]
    public void WorldPhaseConsumesWakeHitRequiresActiveProximityAndPlaysZeroDamageSpecialToCompletion()
    {
        var (admission, world, target) = CreateWorld();
        Assert.False(world.TryWakeNativeCatacombsGargoyleByProximity(target, distance: 1, maximumDistance: 3));
        Assert.False(world.TryWakeNativeCatacombsGargoyleByProximity(target, distance: 3, maximumDistance: 3));
        Assert.True(world.TryWakeNativeCatacombsGargoyle(target));
        Assert.False(target.GargoyleSpecialActive);
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Playing,
            world.NativeCatacombsMonsterPhase(target.EntityId)!.State);

        var originalHitPoints = target.HitPointsQ6;
        for (var tick = 0; tick < 26; tick++)
            Assert.True(world.AdvanceNativeCatacombsGargoyle(target));
        Assert.False(world.AdvanceNativeCatacombsGargoyle(target));
        Assert.False(target.GargoyleSpecialActive);
        Assert.Equal(originalHitPoints, target.HitPointsQ6);
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Completed,
            Assert.Single(world.CaptureNativeCatacombsMonsterPhases()).State);
        target.GargoyleSpecialActive = true;
        Assert.False(world.AdvanceNativeCatacombsGargoyle(target));
        Assert.True(target.GargoyleSpecialActive);
        Assert.Empty(admission.SharedRuntime.InitialSpecialStandActors);
    }

    [Fact]
    public void WorldPhaseProximityWakeAndRestoreValidateOrderedBoundPhases()
    {
        var (admission, world, target) = CreateWorld(activeForTicks: 1);
        Assert.True(world.TryWakeNativeCatacombsGargoyleByProximity(target, distance: 2, maximumDistance: 3));
        Assert.False(world.TryWakeNativeCatacombsGargoyle(target));
        var saved = world.CaptureNativeCatacombsMonsterPhases();
        var restoredWorld = new DiabloWorldState(combatTargets: [target]);
        restoredWorld.ConfigureNativeCatacombsMonsterPhases(admission, saved);
        Assert.Equal(saved, restoredWorld.CaptureNativeCatacombsMonsterPhases());
        Assert.Throws<InvalidDataException>(() => new DiabloWorldState(combatTargets: [target])
            .ConfigureNativeCatacombsMonsterPhases(admission, [saved[0] with { EntityId = 999 }]));

        restoredWorld.InterruptNativeCatacombsGargoyle(target.EntityId);
        Assert.Equal(AuthoritativeNativeCatacombsMonsterPhaseState.Interrupted,
            Assert.Single(restoredWorld.CaptureNativeCatacombsMonsterPhases()).State);
    }

    [Fact]
    public void TerminalGargoylePhaseRestoresWithMovedCorpseButRejectsWrongEntityOrFloor()
    {
        var (admission, world, target) = CreateWorld(activeForTicks: 1);
        Assert.True(world.TryWakeNativeCatacombsGargoyleByProximity(target, distance: 2, maximumDistance: 3));
        for (var tick = 0; tick < 26; tick++)
            Assert.True(world.AdvanceNativeCatacombsGargoyle(target));
        Assert.False(world.AdvanceNativeCatacombsGargoyle(target));
        var saved = world.CaptureNativeCatacombsMonsterPhases();

        var movedCorpse = new AuthoritativeCorpse(target.EntityId, target.LevelId, 11, 10);
        var restored = new DiabloWorldState(corpses: [movedCorpse]);
        restored.ConfigureNativeCatacombsMonsterPhases(admission, saved);
        Assert.Equal(saved, restored.CaptureNativeCatacombsMonsterPhases());

        Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
                corpses: [new AuthoritativeCorpse(target.EntityId + 1, target.LevelId, 11, 10)])
            .ConfigureNativeCatacombsMonsterPhases(admission, saved));
        Assert.Throws<InvalidDataException>(() => new DiabloWorldState(
                corpses: [new AuthoritativeCorpse(target.EntityId, target.LevelId + 1, 11, 10)])
            .ConfigureNativeCatacombsMonsterPhases(admission, saved));
    }

    [Fact]
    public void SavedCatacombsGargoyleCursorRejectsOutOfRangeAndUnknownDisposition()
    {
        var phase = new AuthoritativeNativeCatacombsMonsterPhase(
            0, 501, 7, 81, Fingerprint, 14, 2, 0, 0,
            AuthoritativeNativeCatacombsMonsterPhaseState.Frozen);
        Assert.Throws<InvalidDataException>(() => (phase with { FrameIndex = 14 }).ValidateShape());
        Assert.Throws<InvalidDataException>(() => (phase with { TickCounter = 2 }).ValidateShape());
        Assert.Throws<InvalidDataException>(() => (phase with { State = (AuthoritativeNativeCatacombsMonsterPhaseState)99 }).ValidateShape());
    }

    [Fact]
    public void GargoylePhaseRegistriesRetainIndependentFloorsAndRefuseDuplicateConfiguration()
    {
        var (catalog, bestiary) = CreateMetadata();
        var first = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(5, 7,
            CreateState(CreateFrozenGargoyle()), catalog, Fingerprint, [new(0, 501, 801, 7)], bestiary);
        var second = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(6, 8,
            CreateState(CreateFrozenGargoyle()), catalog, Fingerprint, [new(0, 502, 801, 8)], bestiary);
        var world = new DiabloWorldState(combatTargets: [first.SharedRuntime.Records[0].Target!,
            second.SharedRuntime.Records[0].Target!]);
        world.ConfigureNativeCatacombsMonsterPhases(second);
        world.ConfigureNativeCatacombsMonsterPhases(first);
        Assert.Equal(new uint[] { 501, 502 }, world.CaptureNativeCatacombsMonsterPhases().Select(phase => phase.EntityId));
        Assert.Equal(first.GargoylePhases, world.CaptureNativeCatacombsMonsterPhases(7));
        Assert.Equal(second.GargoylePhases, world.CaptureNativeCatacombsMonsterPhases(8));
        Assert.Throws<InvalidOperationException>(() => world.ConfigureNativeCatacombsMonsterPhases(first));
        Assert.Equal(2, world.CaptureNativeCatacombsMonsterPhases().Count);
    }

    [Fact]
    public void LaterPhaseIdentityCollisionCannotPublishAnEarlierValidRow()
    {
        var (catalog, bestiary) = CreateMetadata();
        var first = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(5, 7,
            CreateState(CreateFrozenGargoyle()), catalog, Fingerprint, [new(0, 501, 801, 7)], bestiary);
        var incoming = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(6, 8,
            CreateState(CreateFrozenGargoyle(), CreateFrozenGargoyle() with {
                Slot = 1, Tile = new(11, 10), Future = new(11, 10), Old = new(11, 10),
            }),
            catalog, Fingerprint, [new(0, 502, 801, 8), new(1, 501, 801, 8)], bestiary);
        var world = new DiabloWorldState(combatTargets: [first.SharedRuntime.Records[0].Target!,
            incoming.SharedRuntime.Records[0].Target!]);
        world.ConfigureNativeCatacombsMonsterPhases(first);
        Assert.Throws<InvalidDataException>(() => world.ConfigureNativeCatacombsMonsterPhases(incoming));
        Assert.Equal(first.GargoylePhases, world.CaptureNativeCatacombsMonsterPhases());
        Assert.Empty(world.CaptureNativeCatacombsMonsterPhases(8));
    }

    [Fact]
    public void AConfiguredFloorWithNoGargoylesStillRefusesDuplicateConfiguration()
    {
        var (catalog, bestiary) = CreateMetadata();
        var admission = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(5, 7,
            CreateState(CreateRangedSkeletonSpecialStand()), catalog, Fingerprint, Bindings(7), bestiary);
        var world = new DiabloWorldState(combatTargets: [admission.SharedRuntime.Records[0].Target!]);
        world.ConfigureNativeCatacombsMonsterPhases(admission);
        Assert.Empty(world.CaptureNativeCatacombsMonsterPhases());
        Assert.Throws<InvalidOperationException>(() => world.ConfigureNativeCatacombsMonsterPhases(admission));
    }

    private static (AuthoritativeCatacombsMonsterRuntimeAdapterResult Admission, DiabloWorldState World,
        AuthoritativeCombatTarget Target) CreateWorld(int activeForTicks = 0)
    {
        var (catalog, bestiary) = CreateMetadata();
        var state = CreateState(CreateFrozenGargoyle() with { ActiveForTicks = activeForTicks });
        var admission = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, state, catalog, Fingerprint, Bindings(7), bestiary);
        var target = Assert.IsType<AuthoritativeCombatTarget>(Assert.Single(admission.SharedRuntime.Records).Target);
        var world = new DiabloWorldState(combatTargets: [target]);
        world.ConfigureNativeCatacombsMonsterPhases(admission);
        return (admission, world, target);
    }

    internal static (DiabloWorldState World, AuthoritativeCombatTarget Leader,
        AuthoritativeCombatTarget Follower) CreateGargoylePair(uint leaderEntityId, uint followerEntityId)
    {
        var leader = CreateFrozenGargoyle() with { PackSize = 1 };
        var follower = CreateFrozenGargoyle() with {
            Slot = 1,
            Tile = new(11, 10),
            Future = new(11, 10),
            Old = new(11, 10),
            Leader = 0,
            LeaderRelation = (int)AuthoritativeMonsterLeaderRelation.Leashed,
        };
        var (catalog, bestiary) = CreateMetadata();
        var admission = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(leader, follower), catalog, Fingerprint,
            [new(0, leaderEntityId, 801, 7), new(1, followerEntityId, 802, 7)], bestiary);
        var targets = admission.SharedRuntime.Records
            .Select(record => Assert.IsType<AuthoritativeCombatTarget>(record.Target))
            .ToArray();
        var backingWorld = new AuthoritativeWorld();
        backingWorld.AddLevel(new AuthoritativeLevel(
            7, 112, 112,
            nativeTransparency: new ushort[112 * 112],
            nativeVisionBlockedCells: []));
        var world = new DiabloWorldState(world: backingWorld, combatTargets: targets);
        world.ConfigureNativeCatacombsMonsterPhases(admission);
        return (world,
            targets.Single(target => target.EntityId == leaderEntityId),
            targets.Single(target => target.EntityId == followerEntityId));
    }

    internal static void AdvanceLeaderToLastFrame(DiabloWorldState world, AuthoritativeCombatTarget leader)
    {
        Assert.True(world.TryWakeNativeCatacombsGargoyle(leader));
        for (var tick = 0; tick < 26; tick++)
            Assert.True(world.AdvanceNativeCatacombsGargoyle(leader));
        Assert.Equal((13, 0), (world.NativeCatacombsMonsterPhase(leader.EntityId)!.FrameIndex,
            world.NativeCatacombsMonsterPhase(leader.EntityId)!.TickCounter));
    }

    internal static DiabloCombatCommandProcessor CreateProcessor(
        DiabloWorldState world,
        AuthoritativeMonsterTypeCatalog? monsterTypes = null)
        => new(world,
            new AuthoritativeSpellCatalog([new AuthoritativeSpellDefinition(1, 1, 1, 0, 0, 0)]),
            new AuthoritativeCombatRules(10, 1, 100),
            monsterTypes: monsterTypes);

    internal static AuthoritativePlayerState CreateProcessorPlayer(int positionX = 90, int positionY = 90)
        => new(0, 0, 50, 0, 0, PlayerAttributesState.Zero, [], Enumerable.Repeat(-1, 40).ToArray(), [],
            positionX, positionY, 50, 1, 7, 0) { EntityId = 1 };

    private static IReadOnlyList<AuthoritativeCathedralMonsterRuntimeBinding> Bindings(uint levelId, int count = 1)
        => Enumerable.Range(0, count)
            .Select(slot => new AuthoritativeCathedralMonsterRuntimeBinding(slot, (uint)(501 + slot),
                (uint)(801 + slot), levelId))
            .ToArray();

    private static AuthoritativeCathedralPopulationMonsterState CreateFrozenGargoyle()
        => new() {
            Slot = 0, Type = 81, LevelType = 5, Mode = 7, Goal = 1,
            UniqueType = byte.MaxValue, Direction = 0, Ai = 12, Intelligence = 5,
            ActiveForTicks = 0, Tile = new(10, 10), Future = new(10, 10), Old = new(10, 10),
            HitPoints = 6400, MaxHitPoints = 6400, AnimTicksPerFrame = 2,
            AnimTickCounter = 0, AnimNumberOfFrames = 14, AnimCurrentFrame = 0,
            Flags = (1U << 2) | (1U << 9), Leader = byte.MaxValue,
            RndItemSeed = 17, AiSeed = 19,
        };

    private static AuthoritativeCathedralPopulationMonsterState CreateRangedSkeletonSpecialStand(int nativeType = 22)
        => new() {
            Slot = 0, Type = nativeType, LevelType = 5, Mode = 11, Goal = 1,
            UniqueType = byte.MaxValue, Direction = 0, Ai = 3, Intelligence = 5,
            ActiveForTicks = 0, Tile = new(10, 10), Future = new(10, 10), Old = new(10, 10),
            HitPoints = 6400, MaxHitPoints = 6400, AnimTicksPerFrame = 2,
            AnimTickCounter = 1, AnimNumberOfFrames = 3, AnimCurrentFrame = 1,
            Flags = 0, Leader = byte.MaxValue, RndItemSeed = 17, AiSeed = 19,
        };

    private static (DiabloWorldState World, AuthoritativeCombatTarget Target) CreateUniqueBatOverride(
        AuthoritativeMonsterTypeCatalog monsterTypes,
        int hitPoints,
        int positionX,
        int positionY)
    {
        var (runtimeCatalog, bestiary) = CreateMetadata();
        var nativeState = CreateFrozenGargoyle() with {
            Mode = 0,
            Ai = 11,
            UniqueType = 42,
            Flags = 0,
            Tile = new(positionX, positionY),
            Future = new(positionX, positionY),
            Old = new(positionX, positionY),
            HitPoints = hitPoints,
            MaxHitPoints = 6400,
        };
        var unique = new AuthoritativeCathedralUniqueDefinition(
            42, 81, 7, 100, 11, 5, 1, 2, 0,
            0, 0, 0, 0, 0, 0,
            AuthoritativeCathedralUniquePack.Independent, 0, 0, -1, false);
        var admission = AuthoritativeCatacombsMonsterRuntimeAdapter.AdaptWithBindings(
            5, 7, CreateState(nativeState), runtimeCatalog, Fingerprint, Bindings(7), bestiary,
            uniqueDefinitions: [unique],
            monsterTypeCatalog: monsterTypes,
            uniqueIdentities: new Dictionary<int, string> { [42] = "test-gargoyle-bat-override" });
        var target = Assert.IsType<AuthoritativeCombatTarget>(
            Assert.Single(admission.SharedRuntime.Records).Target);
        var world = new DiabloWorldState(combatTargets: [target]);
        world.ConfigureNativeCatacombsMonsterPhases(admission);
        return (world, target);
    }

    private static AuthoritativeMonsterTypeCatalog CreateGargoyleBaseTuningCatalog()
        => new([
            new AuthoritativeMonsterTypeDefinition(
                801, 81, "Gargoyle", 1, 8, 10, 1, 100, 10, 1, 2, 0, 0,
                false, false, false) {
                NativeAi = "Gargoyle",
                Intelligence = 5,
                GargoyleSafeDistanceBase = 2,
                GargoyleSafeDistanceIntelligenceScale = 1,
                GargoyleAttackChanceBase = 23,
                GargoyleAttackChanceIntelligenceScale = 2,
                GargoyleMovementChanceBase = 100,
                GargoyleMovementChanceIntelligenceScale = 1,
                GargoyleHealingHealthThresholdPercent = 100,
                GargoyleHealDivisorMultiplier = 16,
                GargoyleHealDivisorRandomRange = 5,
                GargoyleHealDivisorRandomOffset = 4,
            },
        ]);

    private static AuthoritativeCathedralThemeRoomState CreateState(
        params AuthoritativeCathedralPopulationMonsterState[] activeMonsters)
    {
        ArgumentNullException.ThrowIfNull(activeMonsters);
        if (activeMonsters.Length == 0
            || !activeMonsters.Select(monster => monster.Slot).Order().SequenceEqual(Enumerable.Range(0, activeMonsters.Length)))
            throw new ArgumentException("The fixture requires active monsters in contiguous native slots.", nameof(activeMonsters));
        var monsters = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.MonsterCapacity)
            .Select(slot => new AuthoritativeCathedralPopulationMonsterState {
                Slot = slot, Type = 0, LevelType = 5, Goal = 1, UniqueType = byte.MaxValue,
                Direction = 0, AnimTicksPerFrame = 1, AnimNumberOfFrames = 1,
                HitPoints = 6400, MaxHitPoints = 6400, Leader = byte.MaxValue,
                Tile = new(slot + 1, 0), Future = new(slot + 1, 0),
            }).ToArray();
        foreach (var activeMonster in activeMonsters)
            monsters[activeMonster.Slot] = activeMonster;
        var objectSlots = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.ObjectCapacity)
            .Select(slot => new AuthoritativeCathedralPopulationObjectState { Slot = slot, Type = -1, BookMessage = -1 })
            .ToArray();
        var itemSlots = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.ItemCapacity)
            .Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
        var lights = Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.LightCapacity)
            .Select(light => new AuthoritativeCathedralEncounterLightState(light,
                new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
        return new AuthoritativeCathedralThemeRoomState(
            objectSlots, Enumerable.Range(0, 127).ToArray(), Enumerable.Range(0, 127).ToArray(), 0,
            monsters, Enumerable.Range(0, AuthoritativeCathedralThemeRoomState.MonsterCapacity).ToArray(),
            activeMonsters.Length, 1, new int[AuthoritativeCathedralThemeRoomState.CellCount],
            new int[AuthoritativeCathedralThemeRoomState.CellCount], itemSlots, Enumerable.Range(0, 127).ToArray(), 0,
            new int[AuthoritativeCathedralThemeRoomState.CellCount], new HashSet<int>(),
            new ushort[AuthoritativeCathedralThemeRoomState.CellCount], new sbyte[AuthoritativeCathedralThemeRoomState.CellCount],
            new byte[AuthoritativeCathedralThemeRoomState.CellCount], new byte[AuthoritativeCathedralPopulationGeometry.PieceCount],
            new byte[AuthoritativeCathedralThemeRoomState.CellCount], new int[AuthoritativeCathedralThemeRoomState.CellCount],
            new byte[AuthoritativeCathedralThemeRoomState.CellCount], new byte[AuthoritativeCathedralThemeRoomState.CellCount],
            Enumerable.Range(0, 32).ToArray(), 0, lights, false, false,
            monsterPetrified: new bool[AuthoritativeCathedralThemeRoomState.MonsterCapacity]);
    }

    private static (AuthoritativeCathedralMonsterRuntimeCatalog Catalog,
        IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> Bestiary) CreateMetadata(
            int rangedSkeletonAi = 3,
            int rangedSkeletonSpecialEffect = 0,
            int rangedSkeletonFrames = 3,
            int rangedSkeletonTicks = 2)
    {
        var runtime = new AuthoritativeCathedralMonsterRuntimeCatalog(
            Enumerable.Range(0, AuthoritativeCathedralMonsterRuntimeCatalog.NativeMonsterCount)
                .Select(id => new AuthoritativeCathedralMonsterRuntimeDefinition(
                    id, id == 81 ? "GARGOYLE" : $"MONSTER_{id:000}", id == 81 ? "Gargoyle" : $"Monster {id}",
                    10, 20, 4, 22, id is 22 or 23 ? rangedSkeletonSpecialEffect : 0,
                    1, 1, 0, 100, 0)));
        var bestiary = Enumerable.Range(0, AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount)
            .Select(id => new AuthoritativeCathedralRawMonsterDefinition(
                id, AuthoritativeCathedralMonsterAvailability.Retail, 0, 0, sbyte.MaxValue,
                isSkeleton: id is >= 8 and <= 11 or >= 20 and <= 27,
                minimumHitPoints: 1, maximumHitPoints: 2,
                ai: id == 81 ? 12 : id is 22 or 23 ? rangedSkeletonAi : 0, intelligence: 5, abilityFlags: 0,
                minDamage: 1, maxDamage: 1, minDamageSpecial: 0, maxDamageSpecial: 0,
                armorClass: 0, resistance: 0,
                reducePlayerStrength: 0, reducePlayerMagic: 0, reducePlayerDexterity: 0,
                reducePlayerVitality: 0, reducePlayerMaxHP: 0, reducePlayerMaxMana: 0,
                hasSpecial: id is 22 or 23, width: 1,
                frames: id == 81 ? new[] { 14, 14, 14, 10, 18, 14 }
                    : id is 22 or 23 ? new[] { 0, 0, 4, 0, 0, rangedSkeletonFrames }
                    : new[] { 0, 0, 4, 0, 0, 0 },
                rates: id == 81 ? new[] { 1, 1, 1, 1, 1, 2 }
                    : id is 22 or 23 ? new[] { 0, 0, 2, 0, 0, rangedSkeletonTicks }
                    : new[] { 0, 0, 2, 0, 0, 0 }))
            .ToArray();
        return (runtime, bestiary);
    }

    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
}
