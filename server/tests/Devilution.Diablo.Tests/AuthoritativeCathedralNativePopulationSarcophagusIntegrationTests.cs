using System.Reflection;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    private const string SarcophagusCorpusDirectoryVariable = "GODEVILUTION_CATHEDRAL_SARCOPHAGUS_ORACLE_DIRECTORY";
    private const uint NativeMonsterEntityIdBaseValue = 0x09001000;
    private const uint NativeObjectEntityIdBaseValue = 0x09002000;
    private const string NativeSarcophagusSession = "native-sarcophagus-integration";

    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeSarcophagusOrdinaryCommandOpensLootAndEmptyBranchesAcrossV5ColdRestore(bool hasLoot)
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        if (setup is null)
            return;

        var simulation = setup.Activation.Simulation;
        var world = simulation.WorldState;
        var generated = GenerateNativePopulationState(setup.Activation, setup.Fixture);
        var supportedSarcophagi = world.Objects.Values
            .Where(value => value.LevelId == 1 && value.NativeObjectId == 48)
            .Where(world.CanOperateNativeSarcophagus)
            .ToArray();
        Assert.Equal(11, supportedSarcophagi.Length);
        foreach (var supported in supportedSarcophagi) {
            Assert.True(world.TryGetNativeSarcophagusOperationFacts(supported, out var facts));
            Assert.Equal(1000, facts.AnimationDelay);
        }

        var candidate = world.Objects.Values
            .Where(value => value.LevelId == 1 && value.NativeObjectId == 48)
            .Select(value => (Object: value, Slot: checked((int)(value.EntityId - NativeObjectEntityIdBaseValue))))
            .Where(value => (uint)value.Slot < generated.ObjectSlots.Count)
            .Select(value => (value.Object, Raw: generated.ObjectSlots[value.Slot]))
            .Where(value => world.CanOperateNativeSarcophagus(value.Object)
                && (value.Raw.Var1 <= 2) == hasLoot)
            .OrderBy(value => value.Raw.Slot)
            .FirstOrDefault();
        var selectedObject = candidate.Object
            ?? throw new InvalidOperationException("No native sarcophagus matches the selected loot branch.");
        var selectedRaw = candidate.Raw
            ?? throw new InvalidOperationException("The selected native sarcophagus has no raw population row.");
        Assert.Equal(hasLoot, selectedRaw.Var1 <= 2);
        Assert.InRange(selectedRaw.Var1, hasLoot ? 0 : 3, hasLoot ? 2 : 7);

        var start = FindAdjacentWalkableCell(world, selectedObject);
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var template = setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeSarcophagusSession,
            rogue.ClassId,
            goldOverride: null,
            lifeOverride: null,
            manaOverride: null,
            startingLevelId: 1,
            startingPositionX: start.X,
            startingPositionY: start.Y);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write(NativeSarcophagusSession, player => player.EntityId = 0x7E000101);
        var player = runtime.Read(NativeSarcophagusSession, value => value);
        Assert.Equal(1U, player.LevelId);
        Assert.Equal(1, Math.Max(Math.Abs(player.PositionX - selectedObject.PositionX),
            Math.Abs(player.PositionY - selectedObject.PositionY)));

        var codec = new DiabloCohortCodec();
        var legacy = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeCombatFormatVersion, legacy.FormatVersion);
        Assert.Null(legacy.NativeSarcophagi);
        var initialSarcophagi = world.CaptureNativeSarcophagi();
        Assert.NotNull(initialSarcophagi);
        var initialSarcophagusCount = initialSarcophagi!.Count;
        Assert.Equal(11, initialSarcophagusCount);
        var initialSarcophagus = Assert.Single(initialSarcophagi,
            value => value.ObjectSlot == selectedRaw.Slot);
        Assert.Equal(1000, initialSarcophagus.InitialAnimationDelay);
        Assert.Equal(selectedRaw.AnimCount, initialSarcophagus.InitialAnimationCount);
        Assert.Equal(selectedRaw.AnimFrame, initialSarcophagus.InitialAnimationFrame);
        var legacyBytes = codec.Serialize(legacy);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            setup.Activation.CohortCandidateFactory);

        var legacyCandidate = factory(codec.Deserialize(legacyBytes));
        var restoredRuntime = legacyCandidate.CreateRuntime();
        var restoredSimulation = legacyCandidate.Simulation;
        var restoredPlayer = restoredRuntime.Read(NativeSarcophagusSession, value => value);
        var restoredObject = Assert.Single(restoredSimulation.WorldState.Objects.Values,
            value => value.EntityId == selectedObject.EntityId);
        Assert.Equal(legacyBytes, codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)));
        var itemsBefore = JsonSerializer.Serialize(restoredSimulation.WorldState.CaptureCathedralItems());
        var worldItemCountBefore = restoredSimulation.WorldState.WorldItems.Values.Count(value => value.LevelId == 1);

        var operation = restoredSimulation.Execute(restoredPlayer,
            new DiabloOperateObjectIntent(restoredObject.EntityId), 1, [restoredPlayer]);
        Assert.True(operation.Succeeded, $"The native sarcophagus command was refused: {operation.Failure}.");
        restoredRuntime.AdvanceTo(1);

        var opened = restoredSimulation.WorldState.GetNativeSarcophagusState(selectedRaw.Slot);
        Assert.NotNull(opened);
        Assert.True(opened!.Opened);
        Assert.Equal(DiabloNativeSarcophagusActorState.None, opened.ActorState);
        Assert.True(restoredObject.Activated);
        Assert.Equal(checked((int)opened.AnimationFrame), restoredObject.AnimationFrame);
        var afterOpen = restoredSimulation.CaptureCohortState(restoredRuntime);
        Assert.Equal(DiabloCohortState.NativeSarcophagusFormatVersion, afterOpen.FormatVersion);
        Assert.NotNull(afterOpen.NativeSarcophagi);
        Assert.Equal(initialSarcophagusCount, afterOpen.NativeSarcophagi!.Count);
        var openedSarcophagus = Assert.Single(afterOpen.NativeSarcophagi,
            value => value.ObjectSlot == selectedRaw.Slot);
        Assert.Equal(1000, openedSarcophagus.InitialAnimationDelay);
        Assert.Equal(selectedRaw.AnimCount, openedSarcophagus.InitialAnimationCount);
        Assert.Equal(selectedRaw.AnimFrame, openedSarcophagus.InitialAnimationFrame);
        if (hasLoot) {
            Assert.NotEqual(itemsBefore, JsonSerializer.Serialize(restoredSimulation.WorldState.CaptureCathedralItems()));
            Assert.Equal(worldItemCountBefore + 1,
                restoredSimulation.WorldState.WorldItems.Values.Count(value => value.LevelId == 1));
        } else {
            Assert.Equal(itemsBefore, JsonSerializer.Serialize(restoredSimulation.WorldState.CaptureCathedralItems()));
            Assert.Equal(worldItemCountBefore,
                restoredSimulation.WorldState.WorldItems.Values.Count(value => value.LevelId == 1));
        }

        var openedBytes = codec.Serialize(afterOpen);
        var fresh = factory(codec.Deserialize(openedBytes));
        var freshRuntime = fresh.CreateRuntime();
        var freshSimulation = fresh.Simulation;
        Assert.Equal(openedBytes, codec.Serialize(freshSimulation.CaptureCohortState(freshRuntime)));
        restoredRuntime.AdvanceTo(6);
        freshRuntime.AdvanceTo(6);
        Assert.Equal(codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)),
            codec.Serialize(freshSimulation.CaptureCohortState(freshRuntime)));
        var unchanged = codec.Serialize(freshSimulation.CaptureCohortState(freshRuntime));
        var duplicate = freshSimulation.Execute(
            freshRuntime.Read(NativeSarcophagusSession, value => value),
            new DiabloOperateObjectIntent(restoredObject.EntityId), 7,
            [freshRuntime.Read(NativeSarcophagusSession, value => value)]);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, duplicate.Failure);
        Assert.Equal(unchanged, codec.Serialize(freshSimulation.CaptureCohortState(freshRuntime)));
    }

    [Fact(Explicit = true)]
    public void NativeSarcophagusLinkedReservationSpawnsAndCompletesAfterFreshCohortRestore()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: true, requireLinkedCorpus: true);
        if (setup is null)
            return;

        var generated = GenerateNativePopulationState(setup.Activation, setup.Fixture);
        var options = Assert.IsType<DiabloSimulationOptions>(setup.Factory!.CapturedOptions);
        var linkedRaw = ReadCapturedLinkedMonster();
        var originalWorld = setup.Activation.Simulation.WorldState;
        var activeObjectRows = generated.ActiveObjectIds.Take(generated.ActiveObjectCount)
            .Select(slot => generated.ObjectSlots[slot]).ToArray();
        var activeMonsterSlots = generated.ActiveMonsterIds.Take(generated.ActiveMonsterCount).ToHashSet();
        var linkedMonsterSlots = activeObjectRows
            .Where(value => value.Type == 48 && value.Var1 >= 8 || value.Type == 57 && value.Var2 >= 8)
            .Select(value => value.Type == 48 ? value.Var2 : value.Var4)
            .ToHashSet();
        var actorSlot = generated.ActiveMonsterIds.Skip(generated.ActiveMonsterCount)
            .Where(slot => slot is >= 4 and < AuthoritativeCathedralThemeRoomState.MonsterCapacity)
            .Where(slot => !activeMonsterSlots.Contains(slot) && !linkedMonsterSlots.Contains(slot)
                && !generated.MonsterPetrified[slot]
                && !generated.MonsterRefs.Any(reference => Math.Abs(reference) == slot + 1)
                && !generated.MonsterSlots.Any(monster => monster.LeaderRelation != 0 && monster.Leader == slot))
            .FirstOrDefault(-1);
        Assert.True(actorSlot >= 4, "The pinned population must retain an unreferenced raw slot for the controlled captured reservation.");

        var availableSarcophagus = generated.ActiveObjectIds.Take(generated.ActiveObjectCount)
            .Select(slot => generated.ObjectSlots[slot])
            .Where(value => value.Type == 48 && value.Var1 < 8 && !value.TrapFlag && value.SelectionRegion != 0)
            .Where(value => HasTwoOpenSpawnNeighbors(originalWorld, value.Position.X, value.Position.Y))
            .OrderBy(value => value.Slot)
            .First();
        var objectSlots = generated.ObjectSlots.ToArray();
        objectSlots[availableSarcophagus.Slot] = availableSarcophagus with { Var1 = 8, Var2 = actorSlot };
        var monsterSlots = generated.MonsterSlots.ToArray();
        monsterSlots[actorSlot] = linkedRaw with { Slot = actorSlot };
        var signedActorRef = actorSlot + 1;
        var monsterRefs = generated.MonsterRefs.Select(value => Math.Abs(value) == signedActorRef ? 0 : value).ToArray();
        var activeMonsterIds = generated.ActiveMonsterIds.ToArray();
        var displacedInactiveSlot = activeMonsterIds[generated.ActiveMonsterCount];
        var replacedSlotIndex = Array.IndexOf(activeMonsterIds, actorSlot);
        Assert.True(replacedSlotIndex >= generated.ActiveMonsterCount);
        activeMonsterIds[replacedSlotIndex] = displacedInactiveSlot;
        activeMonsterIds[generated.ActiveMonsterCount] = actorSlot;
        var controlledState = CopyNativePopulationState(
            generated, objectSlots, monsterSlots, monsterRefs, activeMonsterIds,
            checked(generated.ActiveMonsterCount + 1), checked(generated.MonsterTargetCount + 1));

        var metadata = setup.Fixture.PopulationMetadata;
        var depth = setup.Fixture.Profile.Levels.Single(value => value.Depth == 1);
        var rogue = setup.Activation.PlayerClasses.Definitions.Single(value => value.Symbol == "rogue");
        var playerTemplate = setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeSarcophagusSession, rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: 57, startingPositionY: 40);
        var hero = new AuthoritativeCathedralFreshLevelHeroContext(
            playerTemplate.BaseLifeMaximum,
            playerTemplate.BaseManaMaximum,
            8,
            setup.Activation.PlayerAnimations.GetRequired(rogue.ClassId).IdleFrames,
            new(57, 40),
            LevelChanging: true,
            LevelLoading: true);
        var request = AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
            metadata, 1, depth.DungeonSeed, 0x13579BDF,
            AuthoritativeCathedralEntry.Main, hero, depth.LevelSymbol);
        var runtimePath = RequiredEnvironment("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var runtimeCatalog = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
            AuthoritativeCathedralMonsterRuntimeCatalog.TableName, File.ReadAllText(runtimePath)));
        var bindings = controlledState.ActiveMonsterIds.Take(controlledState.ActiveMonsterCount)
            .Select(slot => {
                var nativeType = checked((uint)controlledState.MonsterSlots[slot].Type);
                Assert.True(options.MonsterTypes!.TryGetNative(nativeType, out var stableType));
                return new AuthoritativeCathedralMonsterRuntimeBinding(
                    slot, NativeMonsterEntityIdBaseValue + (uint)slot, stableType.MonsterId, 1);
            }).ToArray();
        var uniqueIdentityMap = metadata.UniqueMonsters.Definitions.ToDictionary(
            value => value.NativeUniqueId,
            value => "native-cathedral-unique-" + value.NativeUniqueId.ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
        var adapted = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptWithBindings(
            controlledState,
            runtimeCatalog,
            options.NativeMonsterMetadataFingerprint!,
            bindings,
            options.Difficulty,
            metadata.UniqueMonsters.Definitions,
            options.MonsterTypes,
            uniqueIdentityMap,
            metadata.Population.Monsters);
        Assert.Equal(AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton,
            Assert.Single(adapted.Records, value => value.Binding.NativeSlot == actorSlot).UnsupportedReason);

        var nativeObjects = BuildNativeObjectOrder(controlledState, originalWorld.Objects.Values.Where(value => value.LevelId == 1));
        var itemProfile = Assert.IsType<AuthoritativeCathedralItemRuntimeProfile>(options.NativeCathedralItems);
        var nativeLevel = options.World!.Levels.Single(value => value.LevelId == 1);
        var policy = new AuthoritativeCathedralNativePopulationPolicy(
            request, controlledState, adapted, nativeObjects, itemProfile,
            isDefaultStart: nativeLevel.IsDefaultStart,
            suppressMultiplayerDeathDrops: nativeLevel.SuppressMultiplayerDeathDrops);
        itemProfile = itemProfile with {
            GenerationFingerprint = policy.Fingerprint,
            InitialCheckpoint = itemProfile.InitialCheckpoint with { GenerationFingerprint = policy.Fingerprint },
        };
        var customWorld = new AuthoritativeWorld();
        customWorld.RestoreGeometryState(options.World.CaptureGeometryState());
        var customBackingLevel = customWorld.Levels.Single(value => value.LevelId == 1);
        customWorld.ReplaceLevel(AuthoritativeCathedralDoors.CopyLevel(customBackingLevel,
            special: controlledState.Special.Select(value => checked((ushort)value))));
        var customOptions = CopyOptions(options, customWorld, adapted.Records
            .Where(value => value.Target is not null).Select(value => value.Target!).ToArray(), policy, itemProfile);
        var simulation = DiabloSimulationFactory.Instance.Create(customOptions);
        var sarcophagus = Assert.Single(simulation.WorldState.Objects.Values,
            value => value.EntityId == NativeObjectEntityIdBaseValue + (uint)availableSarcophagus.Slot);
        var freeNeighbors = simulation.WorldState.GetNativeSarcophagusSpawnAvailability(sarcophagus, [])
            .Select((available, index) => (available, index))
            .Where(value => value.available && value.index > 0)
            .ToArray();
        Assert.True(freeNeighbors.Length >= 2);
        var playerStart = FindAdjacentWalkableCell(simulation.WorldState, sarcophagus,
            freeNeighbors.Select(value => value.index).ToHashSet());
        var runtime = simulation.CreateRuntime(setup.Activation.PlayerLoadouts.CreateTemplate(
            NativeSarcophagusSession, rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: playerStart.X, startingPositionY: playerStart.Y));
        runtime.Write(NativeSarcophagusSession, value => value.EntityId = 0x7E000102);
        var player = runtime.Read(NativeSarcophagusSession, value => value);
        Assert.Contains(true, simulation.WorldState.GetNativeSarcophagusSpawnAvailability(sarcophagus, [player]));
        var linkedEntityId = NativeMonsterEntityIdBaseValue + (uint)actorSlot;
        Assert.False(simulation.WorldState.CombatTargets.ContainsKey(linkedEntityId));
        Assert.True(simulation.WorldState.IsEntityIdOccupied(linkedEntityId));

        var codec = new DiabloCohortCodec();
        var oldCheckpoint = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeCombatFormatVersion, oldCheckpoint.FormatVersion);
        Assert.Null(oldCheckpoint.NativeSarcophagi);
        var oldBytes = codec.Serialize(oldCheckpoint);

        // A second owner instance closes every currently available spawn cell
        // with existing native actors. The operation must still open the
        // sarcophagus, retain its reservation, and consume no spawn-selection
        // RNG or item-pool state.
        var blockedCandidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            customOptions, codec.Deserialize(oldBytes));
        var blockedRuntime = blockedCandidate.CreateRuntime();
        var blockedSimulation = blockedCandidate.Simulation;
        var blockedWorld = blockedSimulation.WorldState;
        var blockedPlayer = blockedRuntime.Read(NativeSarcophagusSession, value => value);
        var blockedSarcophagus = blockedWorld.Objects[sarcophagus.EntityId];
        var spawnOffsets = new (int X, int Y)[] {
            (0, 0), (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
        };
        var availableSpawnCells = blockedWorld.GetNativeSarcophagusSpawnAvailability(
                blockedSarcophagus, [blockedPlayer])
            .Select((available, index) => (available, index))
            .Where(value => value.available)
            .Select(value => (X: blockedSarcophagus.PositionX + spawnOffsets[value.index].X,
                Y: blockedSarcophagus.PositionY + spawnOffsets[value.index].Y))
            .ToArray();
        Assert.NotEmpty(availableSpawnCells);
        var occupancyActors = blockedWorld.CombatTargets.Values
            .Where(value => value.EntityId != linkedEntityId && value.NativeInitializationPresent
                && !value.IsPlayerMinion && !value.IsPetrified && !value.IsMoving
                && value.NativeWalkCursor is null && value.NativeMeleeCursor is null
                && Math.Max(Math.Abs(value.PositionX - blockedSarcophagus.PositionX),
                    Math.Abs(value.PositionY - blockedSarcophagus.PositionY)) > 1
                && Math.Max(Math.Abs(value.FuturePositionX - blockedSarcophagus.PositionX),
                    Math.Abs(value.FuturePositionY - blockedSarcophagus.PositionY)) > 1)
            .Take(availableSpawnCells.Length)
            .ToArray();
        Assert.Equal(availableSpawnCells.Length, occupancyActors.Length);
        for (var index = 0; index < occupancyActors.Length; index++)
            occupancyActors[index].ApplyKnockback(availableSpawnCells[index].X, availableSpawnCells[index].Y);
        Assert.DoesNotContain(true,
            blockedWorld.GetNativeSarcophagusSpawnAvailability(blockedSarcophagus, [blockedPlayer]));
        Assert.True(blockedWorld.TryGetNativeSarcophagusOperationFacts(blockedSarcophagus, out var blockedFacts));
        var blockedItemsBefore = JsonSerializer.Serialize(blockedWorld.CaptureCathedralItems());
        var blockedWorldItemsBefore = JsonSerializer.Serialize(blockedWorld.WorldItems.Values
            .Where(value => value.LevelId == 1).OrderBy(value => value.EntityId).ToArray());

        var blockedOpen = blockedSimulation.Execute(blockedPlayer,
            new DiabloOperateObjectIntent(blockedSarcophagus.EntityId), 1, [blockedPlayer]);
        Assert.True(blockedOpen.Succeeded,
            $"A captured sarcophagus with all spawn cells occupied was refused: {blockedOpen.Failure}.");
        Assert.Equal(blockedFacts.RandomSeed, blockedSimulation.RandomState);
        Assert.True(blockedSarcophagus.Activated);
        Assert.Equal(DiabloNativeSarcophagusActorState.Reserved,
            blockedWorld.GetNativeSarcophagusState(availableSarcophagus.Slot)!.ActorState);
        Assert.True(blockedWorld.IsEntityIdOccupied(linkedEntityId));
        Assert.False(blockedWorld.CombatTargets.ContainsKey(linkedEntityId));
        Assert.Equal(blockedItemsBefore, JsonSerializer.Serialize(blockedWorld.CaptureCathedralItems()));
        Assert.Equal(blockedWorldItemsBefore, JsonSerializer.Serialize(blockedWorld.WorldItems.Values
            .Where(value => value.LevelId == 1).OrderBy(value => value.EntityId).ToArray()));

        blockedRuntime.AdvanceTo(1);
        var blockedBytes = codec.Serialize(blockedSimulation.CaptureCohortState(blockedRuntime));
        var blockedRestore = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            customOptions, codec.Deserialize(blockedBytes));
        var blockedRestoredRuntime = blockedRestore.CreateRuntime();
        var blockedRestoredSimulation = blockedRestore.Simulation;
        Assert.Equal(blockedBytes,
            codec.Serialize(blockedRestoredSimulation.CaptureCohortState(blockedRestoredRuntime)));
        Assert.Equal(DiabloNativeSarcophagusActorState.Reserved,
            blockedRestoredSimulation.WorldState.GetNativeSarcophagusState(availableSarcophagus.Slot)!.ActorState);
        Assert.False(blockedRestoredSimulation.WorldState.CombatTargets.ContainsKey(linkedEntityId));
        var blockedUnchanged = codec.Serialize(blockedRestoredSimulation.CaptureCohortState(blockedRestoredRuntime));
        var blockedDuplicatePlayer = blockedRestoredRuntime.Read(NativeSarcophagusSession, value => value);
        var blockedDuplicate = blockedRestoredSimulation.Execute(blockedDuplicatePlayer,
            new DiabloOperateObjectIntent(blockedSarcophagus.EntityId), 2, [blockedDuplicatePlayer]);
        Assert.False(blockedDuplicate.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, blockedDuplicate.Failure);
        Assert.Equal(blockedUnchanged,
            codec.Serialize(blockedRestoredSimulation.CaptureCohortState(blockedRestoredRuntime)));

        var oldCandidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(customOptions,
            codec.Deserialize(oldBytes));
        var restoredRuntime = oldCandidate.CreateRuntime();
        var restoredSimulation = oldCandidate.Simulation;
        var restoredPlayer = restoredRuntime.Read(NativeSarcophagusSession, value => value);
        var restoredSarcophagus = restoredSimulation.WorldState.Objects[sarcophagus.EntityId];
        Assert.Equal(oldBytes, codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)));
        var command = restoredSimulation.Execute(restoredPlayer,
            new DiabloOperateObjectIntent(restoredSarcophagus.EntityId), 1, [restoredPlayer]);
        Assert.True(command.Succeeded, $"Controlled captured linked sarcophagus command failed: {command.Failure}.");
        restoredRuntime.AdvanceTo(1);
        var emerging = restoredSimulation.WorldState.GetNativeSarcophagusState(availableSarcophagus.Slot);
        Assert.NotNull(emerging);
        Assert.Equal(DiabloNativeSarcophagusActorState.Emerging, emerging!.ActorState);
        Assert.Equal((actorSlot, linkedEntityId, linkedRaw.Type),
            (emerging.LinkedMonsterSlot, emerging.LinkedMonsterEntityId, emerging.LinkedMonsterTypeId));
        var actor = Assert.Single(restoredSimulation.WorldState.CombatTargets.Values,
            value => value.EntityId == linkedEntityId);
        Assert.Equal((emerging.SpawnPositionX, emerging.SpawnPositionY),
            (actor.PositionX, actor.PositionY));
        Assert.Equal((actor.PositionX, actor.PositionY), (actor.FuturePositionX, actor.FuturePositionY));
        Assert.Equal(linkedRaw.Type, actor.NativeInitialization!.Binding.NativeTypeId);

        var presentation = restoredSimulation.CapturePresentationSnapshot(restoredPlayer, 1);
        var protocolSnapshot = DiabloProtocolMapper.ToProtocolSnapshot(
            presentation,
            levelGeometry: restoredSimulation.WorldState.World!.Levels,
            spells: customOptions.Spells,
            spellBooks: customOptions.SpellBooks,
            items: customOptions.Items);
        var protocolSarcophagus = Assert.Single(protocolSnapshot.Objects,
            value => value.EntityId == restoredSarcophagus.EntityId);
        var presentationSarcophagus = Assert.Single(presentation.Objects,
            value => value.EntityId == restoredSarcophagus.EntityId);
        Assert.Equal(restoredSarcophagus.Activated, presentationSarcophagus.Activated);
        Assert.Equal(checked((int)emerging.AnimationFrame), presentationSarcophagus.AnimationFrame);
        Assert.False(presentation.NativeSarcophagusInteractableEntityIds.Contains(restoredSarcophagus.EntityId));
        Assert.Equal(48U, protocolSarcophagus.NativeObjectId);
        Assert.True(protocolSarcophagus.Activated);
        Assert.False(protocolSarcophagus.NativeSarcophagusInteractable);
        Assert.Equal(checked((uint)emerging.AnimationFrame), protocolSarcophagus.AnimationFrame);
        Assert.Equal(presentationSarcophagus.Activated, protocolSarcophagus.Activated);
        var protocolActor = Assert.Single(protocolSnapshot.Monsters,
            value => value.EntityId == linkedEntityId);
        var presentationActor = Assert.Single(presentation.Monsters,
            value => value.EntityId == linkedEntityId);
        Assert.Equal(actor.MonsterId, presentationActor.MonsterId);
        Assert.Equal((actor.LevelId, actor.PositionX, actor.PositionY),
            (presentationActor.LevelId, presentationActor.PositionX, presentationActor.PositionY));
        Assert.Equal((actor.HitPoints, actor.Direction, actor.NativeAi, actor.NativeInitializationPresent),
            (presentationActor.HitPoints, presentationActor.Direction, presentationActor.NativeAi,
                presentationActor.NativeInitializationPresent));
        Assert.Equal(actor.MonsterId, protocolActor.MonsterId);
        Assert.Equal((actor.LevelId, actor.PositionX, actor.PositionY),
            (protocolActor.LevelId, protocolActor.PositionX, protocolActor.PositionY));
        Assert.Equal((actor.HitPoints, actor.Direction, actor.NativeAi),
            (protocolActor.HitPoints, protocolActor.Direction, protocolActor.NativeAi));
        Assert.True(protocolActor.NativeInitializationPresent);
        Assert.Equal(presentationActor.MonsterId, protocolActor.MonsterId);
        Assert.Equal(presentationActor.Direction, protocolActor.Direction);
        Assert.Equal(presentationActor.NativeAi, protocolActor.NativeAi);
        Assert.Equal(presentationActor.NativeInitializationPresent, protocolActor.NativeInitializationPresent);

        var midState = restoredSimulation.CaptureCohortState(restoredRuntime);
        var midBytes = codec.Serialize(midState);
        Assert.Equal(DiabloCohortState.NativeSarcophagusFormatVersion, midState.FormatVersion);
        Assert.Equal(DiabloNativeSarcophagusActorState.Emerging, Assert.Single(midState.NativeSarcophagi!,
            value => value.ObjectSlot == availableSarcophagus.Slot).ActorState);
        var midCandidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(customOptions,
            codec.Deserialize(midBytes));
        var midRuntime = midCandidate.CreateRuntime();
        var midSimulation = midCandidate.Simulation;
        Assert.Equal(midBytes, codec.Serialize(midSimulation.CaptureCohortState(midRuntime)));

        var tampered = midState with {
            CombatTargets = midState.CombatTargets.Select(value => value.Monster.EntityId == linkedEntityId
                ? value with { Monster = value.Monster with { NativeAi = "Zombie" } }
                : value).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.CreateCohortCandidate(
            customOptions, tampered));
        Assert.Equal(midBytes, codec.Serialize(midSimulation.CaptureCohortState(midRuntime)));

        var specialFacts = policy.GetSarcophagusOperationFacts(availableSarcophagus.Slot);
        var effectiveSpecialRate = checked((ulong)Math.Max(1, specialFacts.NativeSpecialTicksPerFrame));
        var framesRemaining = specialFacts.NativeSpecialFrameCount - 1 - emerging.SpecialFrameIndex;
        Assert.True(framesRemaining >= 0);
        var ticksToLastSpecialFrame = checked((ulong)framesRemaining * effectiveSpecialRate
            - (ulong)emerging.SpecialTickCounter);
        var lastSpecialFrameTick = checked(midRuntime.LastAdvancedTick + ticksToLastSpecialFrame);
        restoredRuntime.AdvanceTo(lastSpecialFrameTick);
        midRuntime.AdvanceTo(lastSpecialFrameTick);
        var lastFrameState = restoredSimulation.WorldState.GetNativeSarcophagusState(availableSarcophagus.Slot);
        Assert.NotNull(lastFrameState);
        Assert.Equal(DiabloNativeSarcophagusActorState.Emerging, lastFrameState!.ActorState);
        Assert.Equal(specialFacts.NativeSpecialFrameCount - 1, lastFrameState.SpecialFrameIndex);
        Assert.Equal(0, lastFrameState.SpecialTickCounter);
        var lastFrameBytes = codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime));
        var lastFrameCandidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(customOptions,
            codec.Deserialize(lastFrameBytes));
        var lastFrameRuntime = lastFrameCandidate.CreateRuntime();
        Assert.Equal(lastFrameBytes, codec.Serialize(lastFrameCandidate.Simulation.CaptureCohortState(lastFrameRuntime)));

        var completeTick = checked(lastSpecialFrameTick + 1);
        restoredRuntime.AdvanceTo(completeTick);
        midRuntime.AdvanceTo(completeTick);
        lastFrameRuntime.AdvanceTo(completeTick);
        var completed = restoredSimulation.WorldState.GetNativeSarcophagusState(availableSarcophagus.Slot);
        Assert.NotNull(completed);
        Assert.Equal(DiabloNativeSarcophagusActorState.Active, completed!.ActorState);
        Assert.Equal(codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)),
            codec.Serialize(midSimulation.CaptureCohortState(midRuntime)));
        Assert.Equal(codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)),
            codec.Serialize(lastFrameCandidate.Simulation.CaptureCohortState(lastFrameRuntime)));

        var activeCandidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(customOptions,
            codec.Deserialize(codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime))));
        var activeRuntime = activeCandidate.CreateRuntime();
        var activeSimulation = activeCandidate.Simulation;
        var activeTarget = activeSimulation.WorldState.CombatTargets[linkedEntityId];
        activeTarget.DefeatForDialogue();
        activeRuntime.AdvanceTo(checked(activeRuntime.LastAdvancedTick
            + (ulong)Math.Max(1, activeTarget.DeathAnimationTicks + 2)));
        Assert.False(activeSimulation.WorldState.CombatTargets.ContainsKey(linkedEntityId));
        Assert.Contains(linkedEntityId, activeSimulation.WorldState.Corpses.Keys);
        Assert.Equal(DiabloNativeSarcophagusActorState.Dead,
            activeSimulation.WorldState.GetNativeSarcophagusState(availableSarcophagus.Slot)!.ActorState);
        var deadBytes = codec.Serialize(activeSimulation.CaptureCohortState(activeRuntime));
        var deadCandidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(customOptions,
            codec.Deserialize(deadBytes));
        Assert.Equal(deadBytes, codec.Serialize(deadCandidate.Simulation.CaptureCohortState(deadCandidate.CreateRuntime())));
    }

    private static SarcophagusTestSetup? CreateSarcophagusSetup(
        bool captureOptions,
        bool requireLinkedCorpus = false)
    {
        var metadataDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY");
        var runtimePath = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var footprintPath = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH");
        var linkedPath = Environment.GetEnvironmentVariable(SarcophagusCorpusDirectoryVariable);
        if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(runtimePath)
            || string.IsNullOrWhiteSpace(footprintPath)
            || requireLinkedCorpus && string.IsNullOrWhiteSpace(linkedPath)) {
            Assert.Skip("The exact external Cathedral metadata, runtime catalog, cursor footprints, and linked sarcophagus corpus are required.");
            return null;
        }

        var fixture = new NativeFixture(metadataDirectory, runtimePath, footprintPath);
        try {
            var factory = captureOptions ? new CapturingSarcophagusFactory() : null;
            var bootstrap = factory is null
                ? DiabloModule.Instance.CathedralNativePopulationContentBootstrap
                : new DiabloContentBootstrap(factory);
            var activation = bootstrap.ActivateNativePopulation(fixture.Content);
            return new(fixture, activation, factory);
        } catch {
            fixture.Dispose();
            throw;
        }
    }

    private static (int X, int Y) FindAdjacentWalkableCell(
        DiabloWorldState world,
        AuthoritativeWorldObject sarcophagus,
        IReadOnlySet<int>? allowedSpawnIndexes = null)
    {
        var offsets = new[] { (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0),
            (X: 1, Y: -1), (X: 1, Y: 1), (X: -1, Y: 1), (X: -1, Y: -1) };
        foreach (var offset in offsets) {
            var x = sarcophagus.PositionX + offset.X;
            var y = sarcophagus.PositionY + offset.Y;
            var index = Array.FindIndex(new[] {
                (X: 0, Y: 0), (X: -1, Y: -1), (X: 0, Y: -1), (X: 1, Y: -1),
                (X: -1, Y: 0), (X: 1, Y: 0), (X: -1, Y: 1), (X: 0, Y: 1), (X: 1, Y: 1),
            }, value => value == offset);
            if (allowedSpawnIndexes is not null && !allowedSpawnIndexes.Contains(index))
                continue;
            if (world.IsWalkable(sarcophagus.LevelId, x, y)
                && !world.IsOccupied(sarcophagus.LevelId, x, y, [])
                && !world.WorldItems.Values.Any(item => item.LevelId == sarcophagus.LevelId
                    && item.PositionX == x && item.PositionY == y)
                && !world.Corpses.Values.Any(corpse => corpse.LevelId == sarcophagus.LevelId
                    && corpse.PositionX == x && corpse.PositionY == y))
                return (x, y);
        }
        throw new InvalidOperationException("No ordinary adjacent player tile is available for the selected native sarcophagus.");
    }

    private static bool HasTwoOpenSpawnNeighbors(DiabloWorldState world, int x, int y)
    {
        var offsets = new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };
        return offsets.Count(offset => world.IsWalkable(1, x + offset.Item1, y + offset.Item2)
            && !world.IsOccupied(1, x + offset.Item1, y + offset.Item2, [])
            && !world.WorldItems.Values.Any(item => item.LevelId == 1
                && item.PositionX == x + offset.Item1 && item.PositionY == y + offset.Item2)) >= 2;
    }

    private static AuthoritativeCathedralThemeRoomState CopyNativePopulationState(
        AuthoritativeCathedralThemeRoomState source,
        IReadOnlyList<AuthoritativeCathedralPopulationObjectState> objectSlots,
        IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> monsterSlots,
        IReadOnlyList<int> monsterRefs,
        IReadOnlyList<int> activeMonsterIds,
        int activeMonsterCount,
        int monsterTargetCount)
        => new(
            objectSlots, source.ActiveObjectIds, source.AvailableObjectIds, source.ActiveObjectCount,
            monsterSlots, activeMonsterIds, activeMonsterCount, monsterTargetCount,
            monsterRefs, source.PlayerRefs, source.ItemSlots, source.ActiveItemIds, source.ActiveItemCount,
            source.ItemRefs, source.UniqueItemFlags, source.Pieces, source.Transparency, source.Special,
            source.PieceProperties, source.TileFlags, source.ObjectRefs, source.Light, source.PreLight,
            source.ActiveLightIds, source.ActiveLightCount, source.LightRecords, source.UpdateLighting,
            source.DisableLighting, source.Flags, source.Corpses, source.MonsterPetrified);

    private static AuthoritativeCathedralPopulationMonsterState ReadCapturedLinkedMonster()
    {
        var directory = RequiredEnvironment(SarcophagusCorpusDirectoryVariable);
        var path = Path.Combine(Path.GetFullPath(directory), "native-sarcophagus-operations.json");
        if (!File.Exists(path) || new FileInfo(path).Length is < 1 or > 64 * 1024 * 1024)
            throw new InvalidDataException("The bounded native linked-sarcophagus capture is unavailable.");
        using var corpus = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 64 });
        var operation = corpus.RootElement.GetProperty("cases").EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "spawn-var1-8-default-neighbors");
        var raw = JsonSerializer.Deserialize<AuthoritativeCathedralPopulationMonsterState>(
            operation.GetProperty("inputLinkedMonster").GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return raw ?? throw new InvalidDataException("The native linked-skeleton capture is empty.");
    }

    private static IReadOnlyList<AuthoritativeWorldObject> BuildNativeObjectOrder(
        AuthoritativeCathedralThemeRoomState state,
        IEnumerable<AuthoritativeWorldObject> objects)
    {
        var values = objects.ToArray();
        var bySlot = values.Where(value => value.CathedralDoor is null)
            .ToDictionary(value => checked((int)(value.EntityId - NativeObjectEntityIdBaseValue)));
        var doorsByPosition = values.Where(value => value.CathedralDoor is not null)
            .ToDictionary(value => (value.PositionX, value.PositionY));
        return state.ActiveObjectIds.Take(state.ActiveObjectCount).Select(slot => {
            var raw = state.ObjectSlots[slot];
            return raw.DoorFlag ? doorsByPosition[(raw.Position.X, raw.Position.Y)] : bySlot[slot];
        }).ToArray();
    }

    private static DiabloSimulationOptions CopyOptions(
        DiabloSimulationOptions source,
        AuthoritativeWorld world,
        IReadOnlyList<AuthoritativeCombatTarget> targets,
        AuthoritativeCathedralNativePopulationPolicy policy,
        AuthoritativeCathedralItemRuntimeProfile itemProfile)
    {
        var copy = new DiabloSimulationOptions(source.StoreCatalog, source.StoreRules);
        foreach (var property in typeof(DiabloSimulationOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (property.SetMethod is null)
                continue;
            property.SetValue(copy, property.GetValue(source));
        }
        typeof(DiabloSimulationOptions).GetProperty(nameof(DiabloSimulationOptions.World))!.SetValue(copy, world);
        typeof(DiabloSimulationOptions).GetProperty(nameof(DiabloSimulationOptions.CombatTargets))!.SetValue(copy, targets);
        typeof(DiabloSimulationOptions).GetProperty(nameof(DiabloSimulationOptions.NativePopulationPolicy))!.SetValue(copy, policy);
        typeof(DiabloSimulationOptions).GetProperty(nameof(DiabloSimulationOptions.NativeCathedralItems))!.SetValue(copy, itemProfile);
        return copy;
    }

    private static string RequiredEnvironment(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"The explicit native fixture requires {name}.");

    private sealed record SarcophagusTestSetup(
        NativeFixture Fixture,
        DiabloContentActivation Activation,
        CapturingSarcophagusFactory? Factory) : IDisposable
    {
        public void Dispose() => Fixture.Dispose();
    }

    private sealed class CapturingSarcophagusFactory : IDiabloCohortSimulationFactory
    {
        public DiabloSimulationOptions? CapturedOptions { get; private set; }

        public DiabloSimulation Create(DiabloSimulationOptions options)
        {
            CapturedOptions = options;
            return DiabloSimulationFactory.Instance.Create(options);
        }

        public DiabloCohortCandidate CreateCohortCandidate(DiabloSimulationOptions options, DiabloCohortState state)
            => DiabloSimulationFactory.Instance.CreateCohortCandidate(options, state);
    }
}
