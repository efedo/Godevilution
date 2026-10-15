using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeLinkedBarrelUsesOrderedReservationAndColdRestoresItsLifecycle()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: true, requireLinkedCorpus: true);
        if (setup is null)
            return;

        var fixture = CreateLinkedBarrelFixture(setup);
        var simulation = fixture.Simulation;
        var world = simulation.WorldState;
        var barrel = world.Objects[NativeObjectEntityIdBaseValue + (uint)fixture.BarrelSlot];
        Assert.True(world.TryGetNativeBarrelFacts(barrel, out var facts));
        Assert.Equal(AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType, facts.NativeTypeId);
        Assert.Equal(8, facts.Var2);
        Assert.Equal(fixture.LinkedMonsterSlot, facts.Var4);
        Assert.NotNull(facts.LinkedMonster);
        Assert.True(facts.NativeSpecialFrameCount > 0);

        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            "native-linked-barrel-owner", fixture.Rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: fixture.PlayerStart.X, startingPositionY: fixture.PlayerStart.Y));
        runtime.Write("native-linked-barrel-owner", value => value.EntityId = 0x7E000301);
        var player = runtime.Read("native-linked-barrel-owner", value => value);
        var codec = new DiabloCohortCodec();
        var initial = simulation.CaptureCohortState(runtime);
        var v6 = initial with {
            FormatVersion = DiabloCohortState.NativeSarcophagusFormatVersion,
            NativeSarcophagi = world.CaptureNativeSarcophagi(),
            NativeBarrels = null,
        };
        v6.ValidateShape();
        var v6Bytes = codec.Serialize(v6);

        DiabloCohortCandidate Restore(DiabloCohortState state)
            => DiabloSimulationFactory.Instance.CreateCohortCandidate(fixture.Options, state);

        var centerCandidate = Restore(codec.Deserialize(v6Bytes));
        var centerRuntime = centerCandidate.CreateRuntime();
        var centerSimulation = centerCandidate.Simulation;
        var centerWorld = centerSimulation.WorldState;
        var centerPlayer = centerRuntime.Read("native-linked-barrel-owner", value => value);
        var centerBarrel = centerWorld.Objects[barrel.EntityId];
        var centerAvailability = centerWorld.GetNativeBarrelSpawnAvailability(centerBarrel, [centerPlayer]);
        Assert.True(centerAvailability[0], "The broken barrel's center should be available when no actor or player occupies it.");
        Assert.True(centerAvailability.Skip(1).Count(value => value) >= 2,
            "The controlled raw barrel needs multiple available neighboring spawn cells.");
        var centerBreak = centerSimulation.BreakNativeBarrelAtEffectBoundary(
            centerPlayer, centerBarrel.EntityId, checked(centerRuntime.LastAdvancedTick + 1), [centerPlayer]);
        Assert.True(centerBreak.Succeeded, $"Native linked center spawn was refused: {centerBreak.Failure}.");
        Assert.Equal(facts.RandomSeed, centerSimulation.RandomState);
        var centerState = centerWorld.GetNativeBarrelState(fixture.BarrelSlot)!;
        Assert.True(centerState.Broken);
        Assert.Equal(DiabloNativeBarrelActorState.Emerging, centerState.ActorState);
        Assert.Equal((facts.PositionX, facts.PositionY, 1),
            (centerState.SpawnPositionX, centerState.SpawnPositionY, centerState.SpawnDirection));
        var linkedEntityId = NativeMonsterEntityIdBaseValue + (uint)fixture.LinkedMonsterSlot;
        var centerActor = Assert.Single(centerWorld.CombatTargets.Values, value => value.EntityId == linkedEntityId);
        Assert.Equal(facts.LinkedMonster!.Binding, centerActor.NativeInitialization!.Binding);

        var centerCheckpoint = centerSimulation.CaptureCohortState(centerRuntime);
        Assert.Equal(DiabloCohortState.NativeBarrelFormatVersion, centerCheckpoint.FormatVersion);
        var centerBytes = codec.Serialize(centerCheckpoint);
        var centerRestored = Restore(codec.Deserialize(centerBytes));
        var centerRestoredRuntime = centerRestored.CreateRuntime();
        Assert.Equal(centerBytes, codec.Serialize(centerRestored.Simulation.CaptureCohortState(centerRestoredRuntime)));
        Assert.Equal(DiabloNativeBarrelActorState.Emerging,
            centerRestored.Simulation.WorldState.GetNativeBarrelState(fixture.BarrelSlot)!.ActorState);

        var neighborCandidate = Restore(codec.Deserialize(v6Bytes));
        var neighborRuntime = neighborCandidate.CreateRuntime();
        var neighborSimulation = neighborCandidate.Simulation;
        var neighborWorld = neighborSimulation.WorldState;
        var neighborPlayer = neighborRuntime.Read("native-linked-barrel-owner", value => value);
        var neighborBarrel = neighborWorld.Objects[barrel.EntityId];
        var initialNeighborAvailability = neighborWorld.GetNativeBarrelSpawnAvailability(neighborBarrel, [neighborPlayer]);
        Assert.True(initialNeighborAvailability[0]);
        var centerOccupier = FindBarrelOccupants(neighborWorld, linkedEntityId, neighborBarrel, 1)[0];
        centerOccupier.ApplyKnockback(facts.PositionX, facts.PositionY);
        var neighborAvailability = neighborWorld.GetNativeBarrelSpawnAvailability(neighborBarrel, [neighborPlayer]);
        Assert.False(neighborAvailability[0]);
        var availableNeighbors = neighborAvailability.Skip(1).Select((available, index) => (available, index: index + 1))
            .Where(value => value.available).Select(value => value.index).ToArray();
        Assert.True(availableNeighbors.Length >= 2,
            "Blocking center must leave multiple source-ordered neighbor choices.");
        var directionOrder = new[] { 4, 5, 6, 3, 7, 2, 1, 0 };
        var directionX = new[] { 1, 0, -1, -1, -1, 0, 1, 1 };
        var directionY = new[] { 1, 1, 1, 0, -1, -1, -1, 0 };
        var neighborDirections = availableNeighbors.Select(index => directionOrder[index - 1]).ToArray();
        var selectionRandom = new AuthoritativeLegacyRandom(facts.RandomSeed);
        var selectionDraw = selectionRandom.GenerateRnd(15);
        var selectedIndex = unchecked((int)(unchecked((ulong)(long)selectionDraw)
            % (ulong)neighborDirections.Length));
        var expectedDirection = neighborDirections[selectedIndex];
        var expectedX = checked(facts.PositionX + directionX[expectedDirection]);
        var expectedY = checked(facts.PositionY + directionY[expectedDirection]);
        var neighborBreak = neighborSimulation.BreakNativeBarrelAtEffectBoundary(
            neighborPlayer, neighborBarrel.EntityId, checked(neighborRuntime.LastAdvancedTick + 1), [neighborPlayer]);
        Assert.True(neighborBreak.Succeeded, $"Native linked neighbor spawn was refused: {neighborBreak.Failure}.");
        Assert.Equal(selectionRandom.State, neighborSimulation.RandomState);
        var neighborState = neighborWorld.GetNativeBarrelState(fixture.BarrelSlot)!;
        Assert.Equal(DiabloNativeBarrelActorState.Emerging, neighborState.ActorState);
        Assert.Equal((expectedX, expectedY, (expectedDirection + 4) % 8),
            (neighborState.SpawnPositionX, neighborState.SpawnPositionY, neighborState.SpawnDirection));
        var neighborBytes = codec.Serialize(neighborSimulation.CaptureCohortState(neighborRuntime));
        var neighborRestore = Restore(codec.Deserialize(neighborBytes));
        Assert.Equal(neighborBytes, codec.Serialize(neighborRestore.Simulation.CaptureCohortState(
            neighborRestore.CreateRuntime())));

        var blockedCandidate = Restore(codec.Deserialize(v6Bytes));
        var blockedRuntime = blockedCandidate.CreateRuntime();
        var blockedSimulation = blockedCandidate.Simulation;
        var blockedWorld = blockedSimulation.WorldState;
        var blockedPlayer = blockedRuntime.Read("native-linked-barrel-owner", value => value);
        var blockedBarrel = blockedWorld.Objects[barrel.EntityId];
        var blockedAvailability = blockedWorld.GetNativeBarrelSpawnAvailability(blockedBarrel, [blockedPlayer]);
        var blockedCells = blockedAvailability.Select((available, index) => (available, index))
            .Where(value => value.available)
            .Select(value => value.index).ToArray();
        Assert.NotEmpty(blockedCells);
        var blockers = FindBarrelOccupants(blockedWorld, linkedEntityId, blockedBarrel, blockedCells.Length);
        var offsets = new (int X, int Y)[] {
            (0, 0), (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
        };
        for (var index = 0; index < blockedCells.Length; index++) {
            var offset = offsets[blockedCells[index]];
            blockers[index].ApplyKnockback(facts.PositionX + offset.X, facts.PositionY + offset.Y);
        }
        Assert.DoesNotContain(true, blockedWorld.GetNativeBarrelSpawnAvailability(blockedBarrel, [blockedPlayer]));
        var fullBlock = blockedSimulation.BreakNativeBarrelAtEffectBoundary(
            blockedPlayer, blockedBarrel.EntityId, checked(blockedRuntime.LastAdvancedTick + 1), [blockedPlayer]);
        Assert.True(fullBlock.Succeeded, $"A fully blocked linked barrel should still break: {fullBlock.Failure}.");
        Assert.Equal(facts.RandomSeed, blockedSimulation.RandomState);
        var blockedState = blockedWorld.GetNativeBarrelState(fixture.BarrelSlot)!;
        Assert.True(blockedState.Broken);
        Assert.Equal(DiabloNativeBarrelActorState.Reserved, blockedState.ActorState);
        Assert.False(blockedWorld.CombatTargets.ContainsKey(linkedEntityId));
        Assert.True(blockedWorld.IsEntityIdOccupied(linkedEntityId));
        var blockedBytes = codec.Serialize(blockedSimulation.CaptureCohortState(blockedRuntime));
        var blockedRestore = Restore(codec.Deserialize(blockedBytes));
        Assert.Equal(blockedBytes, codec.Serialize(blockedRestore.Simulation.CaptureCohortState(
            blockedRestore.CreateRuntime())));

        var specialRate = Math.Max(1, facts.NativeSpecialTicksPerFrame);
        var framesRemaining = facts.NativeSpecialFrameCount - 1 - centerState.SpecialFrameIndex;
        var ticksToFinalFrame = checked((ulong)(framesRemaining * specialRate - centerState.SpecialTickCounter));
        var finalFrameTick = checked(centerRuntime.LastAdvancedTick + ticksToFinalFrame);
        centerRuntime.AdvanceTo(finalFrameTick);
        var finalFrame = centerWorld.GetNativeBarrelState(fixture.BarrelSlot)!;
        Assert.Equal(DiabloNativeBarrelActorState.Emerging, finalFrame.ActorState);
        Assert.Equal(facts.NativeSpecialFrameCount - 1, finalFrame.SpecialFrameIndex);
        Assert.Equal(0, finalFrame.SpecialTickCounter);
        var finalFrameBytes = codec.Serialize(centerSimulation.CaptureCohortState(centerRuntime));
        var finalFrameRestore = Restore(codec.Deserialize(finalFrameBytes));
        Assert.Equal(finalFrameBytes, codec.Serialize(finalFrameRestore.Simulation.CaptureCohortState(
            finalFrameRestore.CreateRuntime())));
        centerRuntime.AdvanceTo(checked(finalFrameTick + 1));
        Assert.Equal(DiabloNativeBarrelActorState.Active,
            centerWorld.GetNativeBarrelState(fixture.BarrelSlot)!.ActorState);

        // Exercise petrification while still Emerging from the earlier fresh checkpoint.
        var petrifiedState = codec.Deserialize(centerBytes) with {
            CombatTargets = codec.Deserialize(centerBytes).CombatTargets.Select(value =>
                value.Monster.EntityId == linkedEntityId
                    ? value with { Monster = value.Monster with { Petrified = true } }
                    : value).ToArray(),
        };
        var petrifiedCandidate = Restore(petrifiedState);
        var petrifiedRuntime = petrifiedCandidate.CreateRuntime();
        var petrifiedSimulation = petrifiedCandidate.Simulation;
        var petrifiedActor = petrifiedSimulation.WorldState.CombatTargets[linkedEntityId];
        var beforePetrifyCursor = petrifiedSimulation.WorldState.GetNativeBarrelState(fixture.BarrelSlot)!;
        petrifiedRuntime.AdvanceTo(checked(petrifiedRuntime.LastAdvancedTick + 3));
        var paused = petrifiedSimulation.WorldState.GetNativeBarrelState(fixture.BarrelSlot)!;
        Assert.Equal(DiabloNativeBarrelActorState.Emerging, paused.ActorState);
        Assert.Equal((beforePetrifyCursor.SpecialFrameIndex, beforePetrifyCursor.SpecialTickCounter),
            (paused.SpecialFrameIndex, paused.SpecialTickCounter));
        var pausedBytes = codec.Serialize(petrifiedSimulation.CaptureCohortState(petrifiedRuntime));
        var pausedRestore = Restore(codec.Deserialize(pausedBytes));
        Assert.Equal(pausedBytes, codec.Serialize(pausedRestore.Simulation.CaptureCohortState(
            pausedRestore.CreateRuntime())));
        var interruptedCandidate = Restore(codec.Deserialize(centerBytes));
        var interruptedSimulation = interruptedCandidate.Simulation;
        var interruptedRuntime = interruptedCandidate.CreateRuntime();
        var interruptedActor = interruptedSimulation.WorldState.CombatTargets[linkedEntityId];
        interruptedActor.RestoreNativeMeleeCursor(new AuthoritativeNativeMeleeCursor(0, 0) { HitRecovery = true });
        interruptedRuntime.AdvanceTo(checked(interruptedRuntime.LastAdvancedTick + 1));
        var interrupted = interruptedSimulation.WorldState.GetNativeBarrelState(fixture.BarrelSlot)!;
        Assert.Equal(DiabloNativeBarrelActorState.Active, interrupted.ActorState);
        Assert.Equal((0, 0), (interrupted.SpecialFrameIndex, interrupted.SpecialTickCounter));
        var interruptedBytes = codec.Serialize(interruptedSimulation.CaptureCohortState(interruptedRuntime));
        var interruptedRestore = Restore(codec.Deserialize(interruptedBytes));
        Assert.Equal(interruptedBytes, codec.Serialize(interruptedRestore.Simulation.CaptureCohortState(
            interruptedRestore.CreateRuntime())));

        var deathCandidate = Restore(codec.Deserialize(centerBytes));
        var deathSimulation = deathCandidate.Simulation;
        var deathRuntime = deathCandidate.CreateRuntime();
        var deathActor = deathSimulation.WorldState.CombatTargets[linkedEntityId];
        deathActor.DefeatForDialogue();
        deathRuntime.AdvanceTo(checked(deathRuntime.LastAdvancedTick + 1));
        Assert.False(deathSimulation.WorldState.CombatTargets.ContainsKey(linkedEntityId));
        Assert.Contains(linkedEntityId, deathSimulation.WorldState.Corpses.Keys);
        Assert.Equal(DiabloNativeBarrelActorState.Dead,
            deathSimulation.WorldState.GetNativeBarrelState(fixture.BarrelSlot)!.ActorState);
        var deadBytes = codec.Serialize(deathSimulation.CaptureCohortState(deathRuntime));
        var deadRestore = Restore(codec.Deserialize(deadBytes));
        Assert.Equal(deadBytes, codec.Serialize(deadRestore.Simulation.CaptureCohortState(deadRestore.CreateRuntime())));
    }

    private static LinkedBarrelTestFixture CreateLinkedBarrelFixture(SarcophagusTestSetup setup)
    {
        var generated = GenerateNativePopulationState(setup.Activation, setup.Fixture);
        var options = Assert.IsType<DiabloSimulationOptions>(setup.Factory!.CapturedOptions);
        var sourceWorld = setup.Activation.Simulation.WorldState;
        var activeObjects = generated.ActiveObjectIds.Take(generated.ActiveObjectCount)
            .Select(slot => generated.ObjectSlots[slot]).ToArray();
        var activeMonsterSlots = generated.ActiveMonsterIds.Take(generated.ActiveMonsterCount).ToHashSet();
        var reservedMonsterSlots = activeObjects
            .Where(value => value.Type == 48 && value.Var1 >= 8 || value.Type == 57 && value.Var2 >= 8)
            .Select(value => value.Type == 48 ? value.Var2 : value.Var4)
            .ToHashSet();
        var actorSlot = generated.ActiveMonsterIds.Skip(generated.ActiveMonsterCount)
            .Where(slot => slot is >= 4 and < AuthoritativeCathedralThemeRoomState.MonsterCapacity)
            .Where(slot => !activeMonsterSlots.Contains(slot) && !reservedMonsterSlots.Contains(slot)
                && !generated.MonsterPetrified[slot]
                && !generated.MonsterRefs.Any(reference => Math.Abs(reference) == slot + 1)
                && !generated.MonsterSlots.Any(monster => monster.LeaderRelation != 0 && monster.Leader == slot))
            .FirstOrDefault(-1);
        Assert.True(actorSlot >= 4,
            "The pinned native population must retain an unreferenced pre-spawn slot for the controlled barrel reservation.");

        var linkedRaw = ReadCapturedLinkedMonster();
        var barrel = activeObjects
            .Where(value => value.Type == AuthoritativeCathedralBarrelOperationGenerator.OrdinaryBarrelType
                && value.Var1 == 0 && value.Var2 is >= 0 and < 8 && value.Var3 is >= 0 and <= 2
                && value.SelectionRegion != 0 && !value.TrapFlag && value.SolidFlag && value.BreakFlag >= 0
                && value.AnimFlag == 0 && value.AnimDelay >= 1 && value.AnimCount == 0
                && value.AnimLength is > 0 and <= byte.MaxValue && value.AnimFrame <= value.AnimLength
                && sourceWorld.IsWalkable(1, value.Position.X, value.Position.Y))
            .Where(value => HasTwoOpenSpawnNeighbors(sourceWorld, value.Position.X, value.Position.Y))
            .Where(value => !sourceWorld.CombatTargets.Values.Any(target => target.LevelId == 1
                && target.HitPointsQ6 > 0
                && Math.Max(Math.Abs(target.PositionX - value.Position.X),
                    Math.Abs(target.PositionY - value.Position.Y)) <= 1))
            .OrderBy(value => value.Slot)
            .FirstOrDefault();
        Assert.NotNull(barrel);

        var objectSlots = generated.ObjectSlots.ToArray();
        objectSlots[barrel!.Slot] = barrel with { Var2 = 8, Var4 = actorSlot };
        var monsterSlots = generated.MonsterSlots.ToArray();
        monsterSlots[actorSlot] = linkedRaw with { Slot = actorSlot };
        var actorReference = actorSlot + 1;
        var monsterRefs = generated.MonsterRefs.Select(value => Math.Abs(value) == actorReference ? 0 : value).ToArray();
        var activeMonsterIds = generated.ActiveMonsterIds.ToArray();
        var inactiveReplacement = activeMonsterIds[generated.ActiveMonsterCount];
        var actorIndex = Array.IndexOf(activeMonsterIds, actorSlot);
        Assert.True(actorIndex >= generated.ActiveMonsterCount);
        activeMonsterIds[actorIndex] = inactiveReplacement;
        activeMonsterIds[generated.ActiveMonsterCount] = actorSlot;
        var controlledState = CopyNativePopulationState(generated, objectSlots, monsterSlots, monsterRefs,
            activeMonsterIds, checked(generated.ActiveMonsterCount + 1), checked(generated.MonsterTargetCount + 1));

        var metadata = setup.Fixture.PopulationMetadata;
        var depth = setup.Fixture.Profile.Levels.Single(value => value.Depth == 1);
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var template = setup.Activation.PlayerLoadouts.CreateTemplate("native-linked-barrel-owner", rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: 57, startingPositionY: 40);
        var hero = new AuthoritativeCathedralFreshLevelHeroContext(template.BaseLifeMaximum,
            template.BaseManaMaximum, 8, setup.Activation.PlayerAnimations.GetRequired(rogue.ClassId).IdleFrames,
            new(57, 40), LevelChanging: true, LevelLoading: true);
        var request = AuthoritativeCathedralFreshLevelGenerator.CreateRequest(metadata, 1, depth.DungeonSeed,
            0x13579BDF, AuthoritativeCathedralEntry.Main, hero, depth.LevelSymbol);
        var runtimePath = RequiredEnvironment("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var runtimeCatalog = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
            AuthoritativeCathedralMonsterRuntimeCatalog.TableName, File.ReadAllText(runtimePath)));
        var bindings = controlledState.ActiveMonsterIds.Take(controlledState.ActiveMonsterCount).Select(slot => {
            var nativeType = checked((uint)controlledState.MonsterSlots[slot].Type);
            Assert.True(options.MonsterTypes!.TryGetNative(nativeType, out var stableType));
            return new AuthoritativeCathedralMonsterRuntimeBinding(slot,
                NativeMonsterEntityIdBaseValue + (uint)slot, stableType.MonsterId, 1);
        }).ToArray();
        var uniqueIdentityMap = metadata.UniqueMonsters.Definitions.ToDictionary(value => value.NativeUniqueId,
            value => "native-cathedral-unique-" + value.NativeUniqueId.ToString("D3",
                System.Globalization.CultureInfo.InvariantCulture));
        var adapted = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(controlledState,
            runtimeCatalog, options.NativeMonsterMetadataFingerprint!, bindings, options.Difficulty,
            metadata.UniqueMonsters.Definitions, options.MonsterTypes, uniqueIdentityMap, metadata.Population.Monsters);
        Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton,
            Assert.Single(adapted.Records, value => value.Binding.NativeSlot == actorSlot).UnsupportedReason);

        var nativeObjects = BuildNativeObjectOrder(controlledState,
            sourceWorld.Objects.Values.Where(value => value.LevelId == 1));
        var itemProfile = Assert.IsType<AuthoritativeCathedralItemRuntimeProfile>(options.NativeCathedralItems);
        var nativeLevel = options.World!.Levels.Single(value => value.LevelId == 1);
        var policy = new AuthoritativeCathedralNativePopulationPolicy(request, controlledState, adapted,
            nativeObjects, itemProfile, isDefaultStart: nativeLevel.IsDefaultStart,
            suppressMultiplayerDeathDrops: nativeLevel.SuppressMultiplayerDeathDrops);
        itemProfile = itemProfile with {
            GenerationFingerprint = policy.Fingerprint,
            InitialCheckpoint = itemProfile.InitialCheckpoint with { GenerationFingerprint = policy.Fingerprint },
        };
        var customWorld = new AuthoritativeWorld();
        customWorld.RestoreGeometryState(options.World.CaptureGeometryState());
        var backingLevel = customWorld.Levels.Single(value => value.LevelId == 1);
        customWorld.ReplaceLevel(AuthoritativeCathedralDoors.CopyLevel(backingLevel,
            special: controlledState.Special.Select(value => checked((ushort)value))));
        var customOptions = CopyOptions(options, customWorld,
            adapted.Records.Where(value => value.Target is not null).Select(value => value.Target!).ToArray(),
            policy, itemProfile);
        var simulation = DiabloSimulationFactory.Instance.Create(customOptions);
        var objectEntityId = NativeObjectEntityIdBaseValue + (uint)barrel.Slot;
        var barrelObject = simulation.WorldState.Objects[objectEntityId];
        Assert.True(simulation.WorldState.CanBreakNativeBarrel(barrelObject));
        Assert.True(simulation.WorldState.TryGetNativeBarrelFacts(barrelObject, out var facts));
        Assert.Equal(actorSlot, facts.Var4);
        var playerStart = FindAdjacentWalkableCell(simulation.WorldState, barrelObject);
        return new(simulation, customOptions, rogue, barrel.Slot, actorSlot, playerStart);
    }

    private static AuthoritativeCombatTarget[] FindBarrelOccupants(
        DiabloWorldState world,
        uint reservedEntityId,
        AuthoritativeWorldObject barrel,
        int count)
    {
        var result = world.CombatTargets.Values
            .Where(value => value.EntityId != reservedEntityId && value.NativeInitializationPresent
                && !value.IsPlayerMinion && !value.IsPetrified && !value.IsMoving
                && value.NativeWalkCursor is null && value.NativeMeleeCursor is null
                && value.HitPointsQ6 > 0
                && Math.Max(Math.Abs(value.PositionX - barrel.PositionX), Math.Abs(value.PositionY - barrel.PositionY)) > 1
                && Math.Max(Math.Abs(value.FuturePositionX - barrel.PositionX), Math.Abs(value.FuturePositionY - barrel.PositionY)) > 1)
            .Take(count).ToArray();
        Assert.Equal(count, result.Length);
        return result;
    }

    private sealed record LinkedBarrelTestFixture(
        DiabloSimulation Simulation,
        DiabloSimulationOptions Options,
        AuthoritativePlayerClassDefinition Rogue,
        int BarrelSlot,
        int LinkedMonsterSlot,
        (int X, int Y) PlayerStart);
}
