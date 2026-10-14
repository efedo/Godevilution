using System.Text.Json;
using Devilution.Data;
using Devilution.Domain.Inventory;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Stores;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Exercises the opt-in native L1 owner against the real metadata-backed activation path. */
public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact]
    public void SparseObjectSlotsRequireExplicitOptInAndRejectDuplicateRangeAndFootprintErrors()
    {
        var baseProfile = AuthoritativeCathedralItemRuntimeTestData.Profile();
        var rawObjectRefs = new int[AuthoritativeCathedralItemRuntime.CellCount];
        rawObjectRefs[4 * AuthoritativeCathedralItemRuntime.Width + 4] = 127;
        var binding = new AuthoritativeCathedralItemObjectBinding(
            126, 0x70000001, 1, 4, 4, InitialSolid: false, InitialSelectionRegion: 0);

        AuthoritativeCathedralItemRuntimeProfile ProfileFor(
            IReadOnlyList<AuthoritativeCathedralItemObjectBinding> bindings,
            IReadOnlyList<int> objectRefs,
            bool? preserveSparse = null)
        {
            var level = baseProfile.LevelProfiles.Single() with {
                InitialObjectRefs = objectRefs,
                ObjectBindings = bindings,
            };
            if (preserveSparse is { } enabled)
                level = level with { PreserveSparseNativeObjectSlots = enabled };
            return baseProfile with { LevelProfiles = [level] };
        }

        void RejectWithoutMutating(AuthoritativeCathedralItemRuntimeProfile profile)
        {
            var sourceRefs = profile.LevelProfiles.Single().InitialObjectRefs.ToArray();
            var sourceBindings = profile.LevelProfiles.Single().ObjectBindings.ToArray();
            Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralItemRuntime(profile));
            Assert.Equal(sourceRefs, profile.LevelProfiles.Single().InitialObjectRefs.ToArray());
            Assert.Equal(sourceBindings, profile.LevelProfiles.Single().ObjectBindings.ToArray());
        }

        var defaultProfile = ProfileFor([binding], rawObjectRefs);
        Assert.False(defaultProfile.LevelProfiles.Single().PreserveSparseNativeObjectSlots);
        RejectWithoutMutating(defaultProfile);

        var sparseProfile = ProfileFor([binding], rawObjectRefs, preserveSparse: true);
        var sparseRuntime = new AuthoritativeCathedralItemRuntime(sparseProfile);
        Assert.True(sparseRuntime.HasLevel(1));

        RejectWithoutMutating(ProfileFor(
            [binding, binding with { EntityId = 0x70000002 }], rawObjectRefs, preserveSparse: true));
        RejectWithoutMutating(ProfileFor(
            [binding with { NativeObjectSlot = 127 }], rawObjectRefs, preserveSparse: true));
        var unboundReference = rawObjectRefs.ToArray();
        unboundReference[0] = -2;
        RejectWithoutMutating(ProfileFor([binding], unboundReference, preserveSparse: true));
    }

    [Fact(Explicit = true)]
    public void NativeGoldStacksProjectAndColdRestoreOnlyWithTheNativePopulationCatalog()
    {
        var metadataDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY");
        var runtimeTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var footprintTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH");
        if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(runtimeTablePath)
            || string.IsNullOrWhiteSpace(footprintTablePath)) {
            Assert.Skip("The exact external Cathedral metadata, runtime catalog, and cursor footprint paths are required.");
            return;
        }

        using var fixture = new NativeFixture(metadataDirectory, runtimeTablePath, footprintTablePath);
        var activation = DiabloModule.Instance.CathedralNativePopulationContentBootstrap
            .ActivateNativePopulation(fixture.Content);
        var simulation = activation.Simulation;
        var rules = simulation.CombatRules;
        var nativeItems = Assert.IsType<AuthoritativeItemCatalog>(simulation.Items);
        var nativeGold = Assert.IsType<AuthoritativeNativeGoldStackMetadata>(nativeItems.NativeGoldStackMetadata);
        var goldDefinition = nativeItems.GetRequired(nativeGold.StableItemId);
        Assert.Null(fixture.BaselineItems.NativeGoldStackMetadata);
        Assert.Equal(11, nativeGold.NativeItemType);
        Assert.Equal(4, nativeGold.ItemClass);
        Assert.Equal(7, nativeGold.EquipLocation);
        Assert.Equal(nativeGold.NativeItemIndex, goldDefinition.NativeItemIndex);
        Assert.Equal(nativeGold.NativeItemType, goldDefinition.ItemType);
        Assert.Equal(nativeGold.ItemClass, goldDefinition.ItemClass);
        Assert.Equal(nativeGold.EquipLocation, goldDefinition.EquipLocation);
        Assert.Equal(nativeGold.Flags, goldDefinition.Flags);
        Assert.Equal(nativeGold.MiscId, goldDefinition.MiscId);
        Assert.Equal(nativeGold.SpellId, goldDefinition.SpellId);
        Assert.Equal(nativeGold.BaseName, goldDefinition.Name);
        Assert.Equal(nativeGold.IdentifiedName, goldDefinition.IdentifiedName);
        Assert.Equal(nativeGold.InventoryWidth, goldDefinition.InventoryWidth);
        Assert.Equal(nativeGold.InventoryHeight, goldDefinition.InventoryHeight);

        var legacyCreatedState = DiabloGoldInventory.CreateState(1001, rules, fixture.BaselineItems);
        var legacyCreated = DiabloGoldInventory.CreateStack(0x71000001, 1001, rules, fixture.BaselineItems);
        var legacyDefinition = fixture.BaselineItems.Definitions.FirstOrDefault(item =>
            item.GenerationTags.Contains("Gold") || item.GenerationTags.Contains("Currency"));
        var expectedLegacyCreatedState = AuthoritativeItemState.Empty with {
            ItemType = legacyDefinition?.ItemType ?? rules.DeathGoldItemType,
            StableItemId = legacyDefinition?.ItemId ?? 0,
            Value = 1001,
            IdentifiedValue = 1001,
            ItemIndex = legacyDefinition?.NativeItemIndex ?? rules.DeathGoldItemIndex,
            StatFlag = true,
        };
        Assert.Equal(expectedLegacyCreatedState, legacyCreatedState);
        Assert.Equal(legacyCreatedState, legacyCreated.State);
        Assert.Null(legacyCreated.State.NativeCursorGraphic);
        var legacyWithCursor = legacyCreated with {
            State = legacyCreated.State with { NativeCursorGraphic = 17 },
        };
        var legacyRevalued = DiabloGoldInventory.WithValue(
            legacyWithCursor, 2500, rules, fixture.BaselineItems);
        Assert.Equal(legacyWithCursor.State with { Value = 2500, IdentifiedValue = 2500 }, legacyRevalued.State);
        Assert.Equal((int?)17, legacyRevalued.State.NativeCursorGraphic);
        Assert.Equal(legacyCreatedState.BaseName, legacyRevalued.State.BaseName);
        Assert.Equal(legacyCreatedState.InventoryWidth, legacyRevalued.State.InventoryWidth);
        Assert.Equal(legacyCreatedState.InventoryHeight, legacyRevalued.State.InventoryHeight);

        var thresholds = new uint[] { 1000, 1001, 2499, 2500 };
        int ExpectedCursor(uint value) => value <= 1000
            ? nativeGold.GoldSmallCursor
            : value >= 2500 ? nativeGold.GoldLargeCursor : nativeGold.GoldMediumCursor;

        for (var index = 0; index < thresholds.Length; index++) {
            var value = thresholds[index];
            var stack = DiabloGoldInventory.CreateStack(
                checked(0x71000010U + (uint)index), value, rules, nativeItems);
            Assert.True(DiabloGoldInventory.IsValidStack(stack, rules, nativeItems));
            Assert.Equal(nativeGold.StableItemId, stack.State.StableItemId);
            Assert.Equal(nativeGold.NativeItemIndex, stack.State.ItemIndex);
            Assert.Equal(nativeGold.NativeItemType, stack.State.ItemType);
            Assert.Equal(nativeGold.EquipLocation, stack.State.EquipLocation);
            Assert.Equal(nativeGold.ItemClass, stack.State.ItemClass);
            Assert.Equal(nativeGold.Flags, stack.State.Flags);
            Assert.Equal(nativeGold.MiscId, stack.State.MiscId);
            Assert.Equal(nativeGold.SpellId, stack.State.SpellId);
            Assert.Equal(nativeGold.BaseName, stack.State.BaseName);
            Assert.Equal(nativeGold.IdentifiedName, stack.State.IdentifiedName);
            Assert.Equal(nativeGold.InventoryWidth, stack.State.InventoryWidth);
            Assert.Equal(nativeGold.InventoryHeight, stack.State.InventoryHeight);
            Assert.Equal((int?)ExpectedCursor(value), stack.State.NativeCursorGraphic);
            Assert.Equal(checked((int)value), stack.State.Value);
            Assert.Equal(checked((int)value), stack.State.IdentifiedValue);
        }

        var reusableStack = DiabloGoldInventory.CreateStack(0x71000020, 1000, rules, nativeItems);
        foreach (var value in thresholds) {
            var revalued = DiabloGoldInventory.WithValue(reusableStack, value, rules, nativeItems);
            Assert.True(DiabloGoldInventory.IsValidStack(revalued, rules, nativeItems));
            Assert.Equal(reusableStack.ItemSeed, revalued.ItemSeed);
            Assert.Equal(value, revalued.Price);
            Assert.Equal(checked((int)value), revalued.State.Value);
            Assert.Equal(checked((int)value), revalued.State.IdentifiedValue);
            Assert.Equal((int?)ExpectedCursor(value), revalued.State.NativeCursorGraphic);
            Assert.Equal(nativeGold.EquipLocation, revalued.State.EquipLocation);
            Assert.Equal(nativeGold.ItemClass, revalued.State.ItemClass);
            Assert.Equal(nativeGold.Flags, revalued.State.Flags);
            Assert.Equal(nativeGold.MiscId, revalued.State.MiscId);
            Assert.Equal(nativeGold.SpellId, revalued.State.SpellId);
            Assert.Equal(nativeGold.BaseName, revalued.State.BaseName);
            Assert.Equal(nativeGold.IdentifiedName, revalued.State.IdentifiedName);
            Assert.Equal(nativeGold.InventoryWidth, revalued.State.InventoryWidth);
            Assert.Equal(nativeGold.InventoryHeight, revalued.State.InventoryHeight);
        }

        var admittedStack = DiabloGoldInventory.CreateStack(0x71000030, 1001, rules, nativeItems);
        var malformedStacks = new (string Field, OwnedStoreItem Stack)[] {
            ("base name", admittedStack with {
                State = admittedStack.State with { BaseName = "forged Gold" },
            }),
            ("identified name", admittedStack with {
                State = admittedStack.State with { IdentifiedName = "forged Gold" },
            }),
            ("cursor", admittedStack with {
                State = admittedStack.State with {
                    NativeCursorGraphic = nativeGold.CursorFor(admittedStack.Price) + 1,
                },
            }),
            ("width", admittedStack with {
                State = admittedStack.State with { InventoryWidth = nativeGold.InventoryWidth + 1 },
            }),
            ("height", admittedStack with {
                State = admittedStack.State with { InventoryHeight = nativeGold.InventoryHeight + 1 },
            }),
            ("stable item binding", admittedStack with {
                State = admittedStack.State with { StableItemId = nativeGold.StableItemId + 1 },
            }),
            ("native item binding", admittedStack with {
                State = admittedStack.State with { ItemIndex = nativeGold.NativeItemIndex + 1 },
            }),
            ("raw item class", admittedStack with {
                State = admittedStack.State with { ItemClass = nativeGold.ItemClass + 1 },
            }),
            ("raw equip location", admittedStack with {
                State = admittedStack.State with { EquipLocation = nativeGold.EquipLocation + 1 },
            }),
        };
        foreach (var (field, malformed) in malformedStacks) {
            var before = malformed;
            var refusal = Record.Exception(() => DiabloGoldInventory.WithValue(
                malformed, 1000, rules, nativeItems));
            Assert.NotNull(refusal);
            Assert.True(refusal is ArgumentException or InvalidDataException,
                $"Malformed {field} was rejected with an unexpected exception: {refusal.GetType().FullName}.");
            Assert.Equal(before, malformed);
        }

        var admittedStackBefore = admittedStack;
        var admittedStackRevalued = DiabloGoldInventory.WithValue(admittedStack, 1000, rules, nativeItems);
        Assert.Equal(1000U, admittedStackRevalued.Price);
        Assert.Equal(admittedStackBefore, admittedStack);

        var rogue = activation.PlayerClasses.Definitions.Single(definition => definition.Symbol == "rogue");
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-gold-cold-capture", rogue.ClassId,
            goldOverride: 0, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: 57, startingPositionY: 40);
        var runtime = simulation.CreateRuntime(template);
        const string sessionId = "native-gold-cold-capture";
        runtime.Write(sessionId, player => player.EntityId = 0x70000021);
        var playerState = runtime.Read(sessionId, player => player);
        // Mirrors the partial-stack DropInventoryGold branch: seed the 37-Gold world state and retain 38.
        var deathSplitSource = DiabloGoldInventory.CreateStack(0x71000020, 75, rules, nativeItems);
        const uint splitGoldValue = 37;
        const uint splitGoldSeed = 0x71000021;
        var retainedGold = DiabloGoldInventory.WithValue(
            deathSplitSource, deathSplitSource.Price - splitGoldValue, rules, nativeItems);
        var droppedGoldState = DiabloGoldInventory.CreateState(splitGoldValue, rules, nativeItems);
        Assert.Equal(75U, retainedGold.Price + splitGoldValue);
        Assert.Equal(nativeGold.StableItemId, droppedGoldState.StableItemId);
        Assert.Equal(nativeGold.NativeItemIndex, droppedGoldState.ItemIndex);
        Assert.Equal((int?)nativeGold.GoldSmallCursor, droppedGoldState.NativeCursorGraphic);

        var splitPosition = FindEmptyAdjacentCell(simulation.WorldState, playerState);
        const uint splitGoldEntityId = 0x71000032;
        simulation.WorldState.AddWorldItem(new AuthoritativeWorldItem(
            splitGoldEntityId,
            playerState.LevelId,
            splitPosition.X,
            splitPosition.Y,
            splitGoldSeed,
            splitGoldValue,
            droppedGoldState));
        var projectedSplit = simulation.WorldState.WorldItems[splitGoldEntityId];
        Assert.Equal(37U, projectedSplit.Price);
        Assert.Equal(splitGoldSeed, projectedSplit.ItemSeed);
        Assert.Equal(nativeGold.StableItemId, projectedSplit.State.StableItemId);
        Assert.Equal((int?)nativeGold.GoldSmallCursor, projectedSplit.State.NativeCursorGraphic);

        var position = FindEmptyAdjacentCell(simulation.WorldState, playerState);
        var worldStack = DiabloGoldInventory.CreateStack(0x71000022, 1000, rules, nativeItems);
        var freshWorldStack = DiabloGoldInventory.WithValue(worldStack, 2500, rules, nativeItems);
        const uint worldGoldEntityId = 0x71000033;
        // SetWorldItem revaluation is intentionally outside this proof: this checks fresh native
        // materialization of an owned, already-revalued stack.
        simulation.WorldState.AddWorldItem(new AuthoritativeWorldItem(
            worldGoldEntityId,
            playerState.LevelId,
            position.X,
            position.Y,
            freshWorldStack.ItemSeed,
            freshWorldStack.Price,
            freshWorldStack.State));
        var projectedGold = simulation.WorldState.WorldItems[worldGoldEntityId];
        Assert.Equal(2500U, projectedGold.Price);
        Assert.Equal(freshWorldStack.ItemSeed, projectedGold.ItemSeed);
        Assert.Equal(nativeGold.StableItemId, projectedGold.State.StableItemId);
        Assert.Equal((int?)nativeGold.GoldLargeCursor, projectedGold.State.NativeCursorGraphic);

        var codec = new DiabloCohortCodec();
        var encoded = codec.Serialize(simulation.CaptureCohortState(runtime));
        var candidateFactory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            activation.CohortCandidateFactory);
        var candidate = candidateFactory(codec.Deserialize(encoded));
        var restoredRuntime = candidate.CreateRuntime();
        var restoredGold = Assert.Single(candidate.Simulation.WorldState.WorldItems.Values,
            item => item.EntityId == worldGoldEntityId);
        var restoredSplit = Assert.Single(candidate.Simulation.WorldState.WorldItems.Values,
            item => item.EntityId == splitGoldEntityId);
        Assert.Equal(projectedSplit.LevelId, restoredSplit.LevelId);
        Assert.Equal(projectedSplit.PositionX, restoredSplit.PositionX);
        Assert.Equal(projectedSplit.PositionY, restoredSplit.PositionY);
        Assert.Equal(projectedSplit.ItemSeed, restoredSplit.ItemSeed);
        Assert.Equal(projectedSplit.Price, restoredSplit.Price);
        Assert.Equal(projectedSplit.State, restoredSplit.State);
        Assert.Equal(projectedGold.LevelId, restoredGold.LevelId);
        Assert.Equal(projectedGold.PositionX, restoredGold.PositionX);
        Assert.Equal(projectedGold.PositionY, restoredGold.PositionY);
        Assert.Equal(projectedGold.ItemSeed, restoredGold.ItemSeed);
        Assert.Equal(projectedGold.Price, restoredGold.Price);
        Assert.Equal(projectedGold.State, restoredGold.State);
        Assert.Equal(encoded, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
    }

    [Fact(Explicit = true)]
    public void NativeL1PolicyRetainsDoorsObjectsActorsAndRejectsForeignStateAcrossCodecRestore()
    {
        var metadataDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY");
        var runtimeTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var footprintTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH");
        if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(runtimeTablePath)
            || string.IsNullOrWhiteSpace(footprintTablePath)) {
            Assert.Skip("The exact external Cathedral metadata, runtime catalog, and cursor footprint paths are required.");
            return;
        }

        using var fixture = new NativeFixture(metadataDirectory, runtimeTablePath, footprintTablePath);
        var activation = DiabloModule.Instance.CathedralNativePopulationContentBootstrap
            .ActivateNativePopulation(fixture.Content);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.True(world.HasNativeCathedralItems);
        Assert.True(world.HasCathedralDoors);

        var itemCheckpoint = Assert.IsType<DiabloCathedralItemCheckpoint>(world.CaptureCathedralItems());
        var l1Items = Assert.Single(itemCheckpoint.Levels);
        Assert.Equal(AuthoritativeCathedralInitialItemGenerator.PoolCapacity, l1Items.Items.Count);
        Assert.Equal(AuthoritativeCathedralInitialItemGenerator.ActiveCapacity, l1Items.ActiveItemIds.Count);
        Assert.Equal(AuthoritativeCathedralInitialItemGenerator.PoolCapacity, l1Items.SlotEntityBindings.Count);

        var nativeObjects = world.Objects.Values.Where(value => value.LevelId == 1).ToArray();
        Assert.NotEmpty(nativeObjects);
        var nativeDoors = nativeObjects.Where(value => value.CathedralDoor is not null).ToArray();
        Assert.Contains(nativeDoors, value => value.CathedralDoor!.Kind == AuthoritativeCathedralDoorKind.Left);
        Assert.Contains(nativeDoors, value => value.CathedralDoor!.Kind == AuthoritativeCathedralDoorKind.Right);
        Assert.All(nativeDoors, value => Assert.Contains(value.ObjectId, new uint[] { 1, 2 }));

        var generatedState = GenerateNativePopulationState(activation, fixture);
        var activeObjectSlots = generatedState.ActiveObjectIds.Take(generatedState.ActiveObjectCount).ToArray();
        Assert.Contains(126, activeObjectSlots);
        Assert.Equal(new[] { 0, 126, 125 }, activeObjectSlots.Take(3));
        Assert.False(Enumerable.Range(0, activeObjectSlots.Length).SequenceEqual(activeObjectSlots));
        Assert.Equal(activeObjectSlots.Length, activeObjectSlots.Distinct().Count());
        var initialDoors = Assert.IsType<DiabloCathedralDoorCheckpoint>(world.CaptureCathedralDoors());
        AssertNativeObjectProjection(world, generatedState, nativeObjects, activeObjectSlots, initialDoors);

        var ordinaryObject = Assert.Single(nativeObjects.Where(value => value.CathedralDoor is null)
            .OrderBy(value => value.EntityId).Take(1));
        Assert.False(world.CanOperateNativeObject(ordinaryObject));
        Assert.False(world.CanBreakWorldObject(ordinaryObject));
        Assert.All(nativeObjects, value => Assert.False(world.CanBreakWorldObject(value)));
        var refusedBreakSlot = activeObjectSlots.FirstOrDefault(slot =>
            !generatedState.ObjectSlots[slot].DoorFlag && generatedState.ObjectSlots[slot].BreakFlag != 0);
        Assert.NotEqual(0, generatedState.ObjectSlots[refusedBreakSlot].BreakFlag);
        var refusedBreakObject = Assert.Single(nativeObjects,
            value => value.EntityId == 0x09002000U + (uint)refusedBreakSlot);
        Assert.False(refusedBreakObject.Breakable);
        Assert.False(world.CanBreakWorldObject(refusedBreakObject));
        Assert.Contains(nativeObjects, value => world.IsOccupied(1, value.PositionX, value.PositionY, []));
        var blockingObject = Assert.Single(nativeObjects.Where(value => !value.MissilePassable)
            .OrderBy(value => value.EntityId).Take(1));
        Assert.True(world.TryGetMissileBlockingObject(1, blockingObject.PositionX, blockingObject.PositionY,
            out var blockingProjection));
        Assert.Equal(blockingObject.EntityId, blockingProjection!.EntityId);

        var freeDoor = nativeDoors.FirstOrDefault(door => !world.CombatTargets.Values.Any(target =>
            target.LevelId == 1 && (target.PositionX, target.PositionY) == (door.PositionX, door.PositionY)
            || target.LevelId == 1 && target.IsMoving
                && (target.FuturePositionX, target.FuturePositionY) == (door.PositionX, door.PositionY))
            && !world.WorldItems.Values.Any(item => item.LevelId == 1
                && (item.PositionX, item.PositionY) == (door.PositionX, door.PositionY))
            && !world.Corpses.Values.Any(corpse => corpse.LevelId == 1
                && (corpse.PositionX, corpse.PositionY) == (door.PositionX, door.PositionY)));
        Assert.NotNull(freeDoor);
        Assert.True(world.TryOperateCathedralDoor(freeDoor!.EntityId, []));
        var operatedDoors = Assert.IsType<DiabloCathedralDoorCheckpoint>(world.CaptureCathedralDoors());
        Assert.False(initialDoors.DoorLevels.SelectMany(level => level.Doors)
            .SequenceEqual(operatedDoors.DoorLevels.SelectMany(level => level.Doors)));

        var rogue = activation.PlayerClasses.Definitions.Single(definition => definition.Symbol == "rogue");
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-population-policy-test", rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: 57, startingPositionY: 40);
        const string sessionId = "native-population-policy-test";
        var runtime = simulation.CreateRuntime(template);
        runtime.Write(sessionId, player => player.EntityId = 0x70000001);
        var playerState = runtime.Read(sessionId, player => player);
        Assert.Equal(rogue.ClassId, playerState.CharacterClassId);
        Assert.Equal(1U, playerState.LevelId);

        var reservedIds = Enumerable.Range(0, 4).Select(index => 0x09001000U + (uint)index).ToArray();
        Assert.All(reservedIds, entityId => {
            Assert.True(world.IsEntityIdOccupied(entityId));
            Assert.False(world.CombatTargets.ContainsKey(entityId));
        });

        // The remote object fails range validation before its unsupported action policy is reached;
        // direct capability checks above cover the policy decision itself.
        var processor = new DiabloWorldCommandProcessor(world, combatRules: simulation.CombatRules);
        var ordinaryOperation = processor.Execute(playerState,
            new DiabloOperateObjectIntent(ordinaryObject.EntityId), 0, [playerState]);
        Assert.False(ordinaryOperation.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, ordinaryOperation.Failure);
        Assert.True(world.AllowsNativePopulationLevel(0));
        Assert.True(world.AllowsNativePopulationLevel(1));
        Assert.Contains(world.World!.WarpTargets,
            target => target.SourceLevelId == 1 && target.TargetLevelId == 2);
        Assert.False(world.AllowsNativePopulationLevel(2));
        var codec = new DiabloCohortCodec();
        var beforeWarpDryRun = codec.Serialize(simulation.CaptureCohortState(runtime));
        var warp = processor.ExecuteWarp(playerState, 0, [playerState], applyEffects: false);
        Assert.True(warp.Succeeded,
            $"The Warp dry-run could not find the nearest available Town exit: {warp.Failure}.");
        Assert.Equal(beforeWarpDryRun, codec.Serialize(simulation.CaptureCohortState(runtime)));

        const uint foreignPortalId = 0xF0000001;
        world.Portals.Add(foreignPortalId, new AuthoritativePortal(foreignPortalId, 1,
            playerState.PositionX, playerState.PositionY, 2, 40, 40) {
                OwnerEntityId = playerState.EntityId,
                Bidirectional = true,
                RemainingTicks = 10,
                StableSymbol = "native-population-invalid-transition",
            });
        try {
            var portalResult = processor.Execute(playerState, new DiabloUsePortalIntent(foreignPortalId),
                0, [playerState]);
            Assert.False(portalResult.Succeeded);
            Assert.Equal(DiabloRuleFailure.NotAllowed, portalResult.Failure);
        } finally {
            world.Portals.Remove(foreignPortalId);
        }

        // Drive one generated actor through its normal terminal disposition before capture.
        var defeated = world.CombatTargets.Values.First(target => target.LevelId == 1
            && target.HitPoints > 0 && !target.IsPetrified && !target.IsPlayerMinion);
        var defeatedId = defeated.EntityId;
        defeated.DefeatForDialogue();
        runtime.AdvanceTo(checked((ulong)Math.Max(1, defeated.DeathAnimationTicks + 2)));
        Assert.False(world.CombatTargets.ContainsKey(defeatedId));
        Assert.True(world.Corpses.ContainsKey(defeatedId));

        var captured = simulation.CaptureCohortState(runtime);
        var encoded = codec.Serialize(captured);
        var decoded = codec.Deserialize(encoded);
        var candidateFactory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            activation.CohortCandidateFactory);
        var candidate = candidateFactory(decoded);
        var restoredRuntime = candidate.CreateRuntime();
        var restored = candidate.Simulation.WorldState;
        Assert.Equal(encoded, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        var restoredState = candidate.Simulation.CaptureCohortState(restoredRuntime);
        Assert.Equal(JsonSerializer.Serialize(captured.Objects), JsonSerializer.Serialize(restoredState.Objects));
        Assert.Equal(JsonSerializer.Serialize(captured.Corpses), JsonSerializer.Serialize(restoredState.Corpses));
        Assert.Equal(JsonSerializer.Serialize(captured.CathedralItems),
            JsonSerializer.Serialize(restoredState.CathedralItems));
        var restoredDoors = Assert.IsType<DiabloCathedralDoorCheckpoint>(restored.CaptureCathedralDoors());
        Assert.Equal(operatedDoors.DoorLevels.SelectMany(level => level.Doors),
            restoredDoors.DoorLevels.SelectMany(level => level.Doors));
        Assert.Equal(operatedDoors.EffectiveLevels.Select(level => level.NativePieceLayer),
            restoredDoors.EffectiveLevels.Select(level => level.NativePieceLayer));
        var restoredGeneratedState = GenerateNativePopulationState(activation, fixture);
        var restoredActiveObjectSlots = restoredGeneratedState.ActiveObjectIds
            .Take(restoredGeneratedState.ActiveObjectCount).ToArray();
        Assert.Equal(activeObjectSlots, restoredActiveObjectSlots);
        var restoredNativeObjects = restored.Objects.Values.Where(value => value.LevelId == 1).ToArray();
        AssertNativeObjectProjection(restored, restoredGeneratedState, restoredNativeObjects,
            restoredActiveObjectSlots, restoredDoors);
        var restoredRawBreakObject = restoredNativeObjects.Single(value =>
            value.EntityId == 0x09002000U + (uint)refusedBreakSlot);
        Assert.False(restoredRawBreakObject.Breakable);
        Assert.False(restored.CanBreakWorldObject(restoredRawBreakObject));
        Assert.Equal(generatedState.ObjectSlots[refusedBreakSlot].BreakFlag,
            restoredGeneratedState.ObjectSlots[refusedBreakSlot].BreakFlag);

        var unchanged = codec.Serialize(simulation.CaptureCohortState(runtime));
        var factory = activation.CohortCandidateFactory!;
        void RejectWithoutPublishing(DiabloCohortState invalid)
        {
            var error = Record.Exception(() => factory(invalid));
            Assert.NotNull(error);
            Assert.True(error is InvalidDataException or NotSupportedException,
                $"Expected a native-population refusal, got {error.GetType().FullName}.");
            Assert.Equal(unchanged, codec.Serialize(simulation.CaptureCohortState(runtime)));
            Assert.Equal(operatedDoors.DoorLevels.SelectMany(level => level.Doors),
                world.CaptureCathedralDoors()!.DoorLevels.SelectMany(level => level.Doors));
        }

        RejectWithoutPublishing(captured with {
            Configuration = captured.Configuration with {
                NativeMonsterMetadataFingerprint = DifferentFingerprint(
                    captured.Configuration.NativeMonsterMetadataFingerprint!),
            },
        });
        RejectWithoutPublishing(captured with {
            Configuration = captured.Configuration with {
                CathedralItemGenerationFingerprint = DifferentFingerprint(
                    captured.Configuration.CathedralItemGenerationFingerprint!),
            },
        });

        var foreignObject = captured.Objects[0] with { EntityId = 0xF0000002 };
        RejectWithoutPublishing(captured with {
            Objects = captured.Objects.Append(foreignObject).OrderBy(value => value.EntityId).ToArray(),
        });
        var nativeActor = captured.CombatTargets.First(target => target.NativeInitialization is not null);
        var foreignActor = nativeActor with {
            Monster = nativeActor.Monster with {
                EntityId = 0xF0000003,
                NativeInitializationPresent = false,
            },
            NativeInitialization = null,
        };
        RejectWithoutPublishing(captured with {
            CombatTargets = captured.CombatTargets.Append(foreignActor)
                .OrderBy(value => value.Monster.EntityId).ToArray(),
        });

        var participant = Assert.Single(captured.Participants);
        var alternateNativeAi = nativeActor.Monster.NativeAi == "Zombie" ? "SkeletonMelee" : "Zombie";
        var changedNativeAi = nativeActor with {
            Monster = nativeActor.Monster with { NativeAi = alternateNativeAi },
        };
        RejectWithoutPublishing(captured with {
            CombatTargets = captured.CombatTargets.Select(target =>
                target.Monster.EntityId == nativeActor.Monster.EntityId ? changedNativeAi : target).ToArray(),
        });
        var changedMinionDisposition = nativeActor with {
            Monster = nativeActor.Monster with {
                IsPlayerMinion = true,
                OwnerPlayerId = participant.EntityId,
            },
        };
        RejectWithoutPublishing(captured with {
            CombatTargets = captured.CombatTargets.Select(target =>
                target.Monster.EntityId == nativeActor.Monster.EntityId ? changedMinionDisposition : target).ToArray(),
        });

        var warrior = activation.PlayerClasses.Definitions.Single(definition => definition.Symbol == "warrior");
        RejectWithoutPublishing(captured with {
            Participants = [participant with {
                Player = participant.Player with { CharacterClassId = warrior.ClassId },
                PlayerClassDefinition = warrior,
            }],
        });
        RejectWithoutPublishing(captured with {
            Participants = [participant with {
                Player = participant.Player with { LevelId = 2 },
            }],
        });
        RejectWithoutPublishing(captured with {
            Participants = [participant with {
                EntityId = reservedIds[0],
                Player = participant.Player with { EntityId = reservedIds[0] },
            }],
        });

        var geometry = Assert.IsType<DiabloWorldGeometryState>(captured.Geometry);
        var geometryLevels = geometry.Levels!.ToArray();
        var l1GeometryIndex = Array.FindIndex(geometryLevels, level => level.LevelId == 1);
        var l1Geometry = geometryLevels[l1GeometryIndex];
        var changedPieces = l1Geometry.NativePieceLayer!.ToArray();
        changedPieces[0] = checked((ushort)(changedPieces[0] + 1));
        geometryLevels[l1GeometryIndex] = l1Geometry with { NativePieceLayer = changedPieces };
        RejectWithoutPublishing(captured with {
            Geometry = geometry with { Levels = geometryLevels },
        });

        var foreignPortal = new DiabloCohortPortalState(0xF0000004, 1,
            participant.Player.PositionX, participant.Player.PositionY,
            2, 40, 40, -1, false, string.Empty,
            "native-population-invalid-transition", 10, participant.EntityId, true);
        RejectWithoutPublishing(captured with {
            Portals = captured.Portals.Append(foreignPortal).OrderBy(value => value.PortalId).ToArray(),
        });

        // A native L1 owner accepts only Town and its owned L1. These are
        // otherwise well-formed, in-bounds entities on authored Cathedral levels.
        var foreignLevelActor = foreignActor with {
            Monster = foreignActor.Monster with {
                EntityId = 0xF0000010,
                LevelId = 2,
                PositionX = 40,
                PositionY = 40,
            },
        };
        RejectWithoutPublishing(captured with {
            CombatTargets = captured.CombatTargets.Append(foreignLevelActor)
                .OrderBy(value => value.Monster.EntityId).ToArray(),
        });

        var fixtureEquipment = participant.Player.Equipment.First();
        var foreignLevelItem = new DiabloWorldItemState(
            0xF0000011, 2, 40, 40, fixtureEquipment.ItemSeed, 0, fixtureEquipment.State);
        RejectWithoutPublishing(captured with {
            WorldItems = captured.WorldItems.Append(foreignLevelItem)
                .OrderBy(value => value.EntityId).ToArray(),
        });

        var foreignLevelObject = captured.Objects[0] with {
            EntityId = 0xF0000012,
            LevelId = 2,
            PositionX = 40,
            PositionY = 40,
        };
        RejectWithoutPublishing(captured with {
            Objects = captured.Objects.Append(foreignLevelObject)
                .OrderBy(value => value.EntityId).ToArray(),
        });

        var nativeCorpse = Assert.Single(captured.Corpses, corpse => corpse.LevelId == 1);
        var foreignLevelCorpse = nativeCorpse with {
            EntityId = 0xF0000013,
            LevelId = 2,
            PositionX = 40,
            PositionY = 40,
        };
        RejectWithoutPublishing(captured with {
            Corpses = captured.Corpses.Append(foreignLevelCorpse)
                .OrderBy(value => value.EntityId).ToArray(),
        });

        var foreignLevelProjectile = new DiabloProjectileState(
            0xF0000014, participant.EntityId, nativeActor.Monster.EntityId,
            0, 2, 40, 40, 41, 40, 0, AuthoritativeDamageType.Physical, 0, 1) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                Phase = AuthoritativeProjectilePhase.Travel,
                StartX = 40,
                StartY = 40,
                Speed = 16,
                ImpactLifetime = 1,
                ResistantDamageDivisor = 1,
            };
        RejectWithoutPublishing(captured with {
            Projectiles = captured.Projectiles.Append(foreignLevelProjectile)
                .OrderBy(value => value.EntityId).ToArray(),
        });

        Assert.Throws<NotSupportedException>(() => DiabloModule.Instance.CathedralNativePopulationContentBootstrap
            .ActivateNativePopulation(fixture.Content, gameMode: DiabloGameMode.Multiplayer));
    }

    [Fact(Explicit = true)]
    public void NativeRoguePurchasesDropsRestoresAndFiresItsBow()
    {
        var metadataDirectory = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY");
        var runtimeTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH");
        var footprintTablePath = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH");
        if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(runtimeTablePath)
            || string.IsNullOrWhiteSpace(footprintTablePath)) {
            Assert.Skip("The exact external Cathedral metadata, runtime catalog, and cursor footprint paths are required.");
            return;
        }

        using var fixture = new NativeFixture(metadataDirectory, runtimeTablePath, footprintTablePath);
        var activation = DiabloModule.Instance.CathedralNativePopulationContentBootstrap
            .ActivateNativePopulation(fixture.Content);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(definition => definition.Symbol == "rogue");
        var smith = Assert.Single(activation.TownerGreetings!.Definitions,
            definition => definition.TownerId == 0);
        var approach = new[] {
            (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0),
            (X: 1, Y: -1), (X: 1, Y: 1), (X: -1, Y: 1), (X: -1, Y: -1),
        }.Select(offset => (X: smith.PositionX + offset.X, Y: smith.PositionY + offset.Y))
            .First(position => world.IsWalkable(smith.LevelId, position.X, position.Y)
                && !world.IsOccupied(smith.LevelId, position.X, position.Y,
                    Array.Empty<AuthoritativePlayerState>()));
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-population-shop-route",
            rogue.ClassId,
            goldOverride: 100,
            lifeOverride: null,
            manaOverride: null,
            startingLevelId: smith.LevelId,
            startingPositionX: approach.X,
            startingPositionY: approach.Y);
        var runtime = simulation.CreateRuntime(template);
        const string sessionId = "native-population-shop-route";
        runtime.Write(sessionId, player => player.EntityId = 0x70000011);
        var playerState = runtime.Read(sessionId, player => player);
        Assert.Equal(rogue.ClassId, playerState.CharacterClassId);
        Assert.Equal(100U, playerState.Gold);
        Assert.Null(playerState.HeldItem);
        var startingBow = Assert.Single(playerState.Equipment,
            item => item.State.StableItemId == 103);
        var startingBowDefinition = simulation.Items!.GetRequired(103);
        Assert.Equal("short_bow_starting", startingBowDefinition.StableSymbol);
        Assert.Equal(startingBowDefinition.Name, startingBow.State.BaseName);

        var store = Assert.Single(simulation.CaptureCohortState(runtime).Stores,
            value => value.StoreId == 1);
        var bucklerOffer = Assert.Single(store.Items!, item => item.State.StableItemId == 101);
        Assert.Equal((1U, 43U, 25U), (bucklerOffer.StoreSlot, bucklerOffer.ItemSeed, bucklerOffer.Price));
        const ulong firstTick = 1;
        Assert.True(simulation.Execute(playerState, new DiabloOpenStoreIntent(1), firstTick).Succeeded);
        Assert.True(simulation.Execute(playerState,
            new DiabloPurchaseIntent(1, bucklerOffer.StoreSlot), firstTick).Succeeded);
        Assert.True(simulation.Execute(playerState, new DiabloCloseStoreIntent(), firstTick).Succeeded);
        runtime.AdvanceTo(firstTick);
        Assert.Equal(75U, playerState.Gold);
        var purchasedBuckler = Assert.Single(playerState.Inventory,
            item => item.State.StableItemId == 101);
        Assert.Equal((1U, 1U, 43U, 25U, firstTick), (
            purchasedBuckler.StoreId,
            purchasedBuckler.StoreSlot,
            purchasedBuckler.ItemSeed,
            purchasedBuckler.Price,
            purchasedBuckler.PurchasedAtTick));

        var entrance = Assert.Single(world.World!.WarpTargets,
            target => target.SourceLevelId == smith.LevelId && target.TargetLevelId == 1);
        MoveToLevelTrigger(simulation, runtime, playerState, entrance);
        Assert.Equal(1U, playerState.LevelId);
        Assert.Null(playerState.CurrentAction);
        Assert.Equal(100U, playerState.Gold + bucklerOffer.Price);

        // Preview real generated loot without mutating the pool, then use ordinary
        // ranged commands to earn one deterministic non-Gold drop and its XP.
        var poolBeforePreview = JsonSerializer.Serialize(world.CaptureCathedralItems());
        var itemSeedsBeforePreview = world.CombatTargets.Values.OrderBy(target => target.EntityId)
            .Select(target => (target.EntityId, target.MonsterItemSeed)).ToArray();
        var worldItemIdsBeforePreview = world.WorldItems.Keys.Order().ToArray();
        var playerExperienceBeforePreview = playerState.Experience;
        var lootCandidates = new List<(
            AuthoritativeCombatTarget Target,
            AuthoritativeCathedralItemDeathTransaction Preview,
            bool Equipable,
            long Distance)>();
        foreach (var target in world.CombatTargets.Values
            .Where(target => target.LevelId == playerState.LevelId
                && target.HitPoints > 0
                && target.ExperienceReward > 0
                && !target.IsPetrified
                && !target.IsPlayerMinion
                && !target.IsUnique
                && target.NativeInitialization is not null)) {
            AuthoritativeCathedralItemDeathTransaction? preview;
            try {
                preview = world.PrepareCathedralDeathLoot(target, playerState, [playerState]);
            } catch (NotSupportedException) {
                // Special-treasure actors remain outside the ordinary loot compositor.
                continue;
            }
            var preparedPreview = preview
                ?? throw new InvalidOperationException("The native death-loot preview was unexpectedly absent.");
            Assert.Equal(0U, preparedPreview.EntityId);
            if (preparedPreview is not { RequiresWorldItem: true, Projection: not null }
                || preparedPreview.Loot.Item is not { ItemType: not 11 })
                continue;
            var itemState = preparedPreview.Projection.State;
            var equipmentKind = AuthoritativeItemClassification.GetEquipmentSlotKind(
                itemState, simulation.Items!);
            var equipable = equipmentKind != DiabloEquipmentSlotKind.Unknown && itemState.StatFlag;
            lootCandidates.Add((target, preparedPreview, equipable,
                DeterministicGridMetrics.ChebyshevDistance(
                    target.PositionX, target.PositionY, playerState.PositionX, playerState.PositionY)));
        }
        Assert.Equal(poolBeforePreview, JsonSerializer.Serialize(world.CaptureCathedralItems()));
        Assert.Equal(itemSeedsBeforePreview, world.CombatTargets.Values.OrderBy(target => target.EntityId)
            .Select(target => (target.EntityId, target.MonsterItemSeed)).ToArray());
        Assert.Equal(worldItemIdsBeforePreview, world.WorldItems.Keys.Order().ToArray());
        Assert.Equal(playerExperienceBeforePreview, playerState.Experience);
        Assert.NotEmpty(lootCandidates);
        var selectedLoot = lootCandidates.OrderByDescending(candidate => candidate.Equipable)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Target.HitPoints)
            .ThenBy(candidate => candidate.Target.EntityId)
            .First();
        if (lootCandidates.Any(candidate => candidate.Equipable))
            Assert.True(selectedLoot.Equipable);
        var rangedTarget = selectedLoot.Target;
        var lootPreview = selectedLoot.Preview;
        Assert.Equal(rangedTarget.MonsterItemSeed, lootPreview.Loot.InitialRandomState);
        Assert.NotEqual(11, lootPreview.Loot.Item!.ItemType);
        Assert.True(lootPreview.RequiresWorldItem);

        var fightEvents = new List<DiabloGameplayEvent>();
        var shotDiagnostics = new List<string>();
        var clearedThreatEntityIds = new List<uint>();
        var threatShotDiagnostics = new List<string>();
        var threatShotsByEntity = new Dictionary<uint, int>();
        var recentProjectileTicks = new Queue<string>();
        var shotArrowIdsAtStart = new HashSet<uint>();
        var observedShotArrowIds = new HashSet<uint>();
        var captureProjectileTicks = false;
        AuthoritativeCombatTarget? projectileTraceTarget = null;
        AuthoritativeCathedralItemDeathTransaction? immediatelyBeforeDeathPreview = null;
        var healingPotionUses = new List<string>();

        string DescribeMissileBlockage(
            int levelId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            uint targetEntityId)
        {
            var x = startX;
            var y = startY;
            var maximumSteps = Math.Max(Math.Abs(targetX - startX), Math.Abs(targetY - startY));
            for (var step = 0; step < maximumSteps; step++) {
                var nextX = x + Math.Sign((long)targetX - x);
                var nextY = y + Math.Sign((long)targetY - y);
                var interveningTarget = world.CombatTargets.Values
                    .Where(candidate => candidate.EntityId != targetEntityId
                        && candidate.HitPointsQ6 > 0
                        && (candidate.LevelId == 0 || candidate.LevelId == (uint)levelId)
                        && ((candidate.PositionX == nextX && candidate.PositionY == nextY)
                            || (candidate.IsMoving && candidate.IsPetrified
                                && candidate.FuturePositionX == nextX
                                && candidate.FuturePositionY == nextY)))
                    .OrderBy(candidate => candidate.EntityId)
                    .FirstOrDefault();
                if (interveningTarget is not null)
                    return $"({nextX},{nextY}) target-collision entity={interveningTarget.EntityId}/"
                        + $"ai={interveningTarget.NativeAi}/hpQ6={interveningTarget.HitPointsQ6}";
                var missileBlocked = world.IsMissileBlocked((uint)levelId, nextX, nextY);
                var sightBlocked = !world.HasMissileLineOfSight((uint)levelId, x, y, nextX, nextY);
                if (missileBlocked || sightBlocked) {
                    var cause = missileBlocked ? "missile" : "sight";
                    if (world.TryGetMissileBlockingObject((uint)levelId, nextX, nextY, out var blocker))
                        return $"({nextX},{nextY}) {cause}-block object={blocker!.EntityId}/native={blocker.NativeObjectId}";
                    return $"({nextX},{nextY}) {cause}-block geometry";
                }
                x = nextX;
                y = nextY;
            }
            return "none";
        }

        string DescribeWeaponArrows()
        {
            var arrows = world.Projectiles.Values
                .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                    && projectile.SourceEntityId == playerState.EntityId)
                .OrderBy(projectile => projectile.EntityId)
                .ToArray();
            return arrows.Length == 0 ? "none" : string.Join(";", arrows.Select(projectile =>
                $"id={projectile.EntityId},target={projectile.TargetEntityId},"
                    + $"at=({projectile.PositionX},{projectile.PositionY}),"
                    + $"goal=({projectile.TargetX},{projectile.TargetY}),phase={projectile.Phase},"
                    + $"remaining={projectile.RemainingTicks},delay={projectile.ActivationDelayTicks},"
                    + $"hit={projectile.AttackHit},damageQ6={projectile.DamageQ6},speed={projectile.Speed},"
                    + $"path={projectile.PathIndex}/{projectile.PathTicks},"
                    + $"blocked={DescribeMissileBlockage((int)projectile.LevelId,
                        projectile.PositionX, projectile.PositionY, projectile.TargetX, projectile.TargetY,
                        projectile.TargetEntityId)}"));
        }

        string DescribeRangedHitModel(int distance, AuthoritativeCombatTarget target)
        {
            var usableEquipment = playerState.Equipment.Select(item => item.State)
                .Where(item => item.StatFlag && (item.Magical == 0 || item.Identified))
                .ToArray();
            var hitBonus = usableEquipment.Sum(item => item.PlusToHit);
            var armorPiercing = usableEquipment.Sum(item => item.PlusEnemyArmorClass);
            var classToHit = rogue.CalculatePhysicalToHit(
                playerState.CharacterLevel,
                playerState.Attributes.Dexterity.Current,
                ranged: true);
            var effectiveArmor = checked(target.ArmorClass
                + simulation.CombatRules.DifficultyRules.GetArmorBonus(simulation.Difficulty));
            var distancePenalty = checked(distance * distance / 2);
            var chance = simulation.CombatRules.ResolvePhysicalHitChance(
                checked(classToHit + hitBonus - distancePenalty),
                effectiveArmor,
                armorPiercing);
            return $"distance={distance},level={playerState.CharacterLevel},dex={playerState.Attributes.Dexterity.Current},"
                + $"baseRangedToHit={rogue.BaseRangedToHit},classToHit={classToHit},equipmentHitBonus={hitBonus},"
                + $"targetArmor={target.ArmorClass},difficultyArmorBonus="
                + $"{simulation.CombatRules.DifficultyRules.GetArmorBonus(simulation.Difficulty)},"
                + $"effectiveArmor={effectiveArmor},distancePenalty={distancePenalty},"
                + $"armorPiercing={armorPiercing},resolvedChance={chance}";
        }

        void UseHealingPotionIfNeeded()
        {
            var maximumLifeQ6 = Math.Max(playerState.LifeMaximum, 0);
            if (playerState.Life <= 0 || maximumLifeQ6 == 0
                || (long)playerState.Life * 3 > (long)maximumLifeQ6 * 2)
                return;

            var potion = playerState.Belt
                .Where(item => item.State.StableItemId == 105)
                .OrderBy(item => item.Slot)
                .FirstOrDefault();
            if (potion is null)
                return;

            var lifeBefore = playerState.Life;
            var actionBefore = playerState.CurrentAction;
            var appliedTick = checked(runtime.LastAdvancedTick + 1);
            var result = simulation.Execute(playerState,
                new DiabloUseConsumableIntent(new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Belt,
                    potion.Slot,
                    potion.State.StableItemId)),
                appliedTick);
            Assert.True(result.Succeeded,
                $"The Rogue could not use its carried healing potion: {result.Failure}.");
            Assert.True(playerState.Life > lifeBefore,
                "The accepted carried healing potion did not restore life.");
            Assert.Equal(actionBefore, playerState.CurrentAction);
            Assert.DoesNotContain(playerState.Belt,
                item => item.Slot == potion.Slot && item.State.StableItemId == potion.State.StableItemId);
            healingPotionUses.Add(
                $"tick={appliedTick},slot={potion.Slot},life={lifeBefore}->{playerState.Life},"
                    + $"action={actionBefore?.Kind.ToString() ?? "idle"}");

            runtime.AdvanceTo(appliedTick);
            fightEvents.AddRange(result.Events);
            fightEvents.AddRange(runtime.DrainEvents(sessionId));
        }

        void AdvanceCombatTo(ulong endTick)
        {
            while (runtime.LastAdvancedTick < endTick) {
                if (rangedTarget.HitPoints > 0)
                    immediatelyBeforeDeathPreview = world.PrepareCathedralDeathLoot(
                        rangedTarget, playerState, [playerState]);
                var tick = checked(runtime.LastAdvancedTick + 1);
                var arrowsBefore = captureProjectileTicks ? DescribeWeaponArrows() : string.Empty;
                runtime.AdvanceTo(tick);
                var tickEvents = runtime.DrainEvents(sessionId).ToArray();
                fightEvents.AddRange(tickEvents);
                if (captureProjectileTicks) {
                    var traceTarget = projectileTraceTarget ?? rangedTarget;
                    foreach (var arrow in world.Projectiles.Values.Where(projectile =>
                        projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                        && projectile.SourceEntityId == playerState.EntityId
                        && !shotArrowIdsAtStart.Contains(projectile.EntityId)))
                        observedShotArrowIds.Add(arrow.EntityId);
                    var events = tickEvents.Length == 0 ? "none" : string.Join(",",
                        tickEvents.Select(value =>
                            $"{value.Kind}:{value.SourceEntityId}>{value.TargetEntityId}:{value.Amount}/q6={value.AmountQ6}"));
                    recentProjectileTicks.Enqueue(
                        $"tick={tick},hero=({playerState.PositionX},{playerState.PositionY})/life={playerState.Life},"
                            + $"target={traceTarget.EntityId}@({traceTarget.PositionX},{traceTarget.PositionY})/"
                            + $"hp={traceTarget.HitPoints}/q6={traceTarget.HitPointsQ6}/"
                            + $"maxQ6={traceTarget.MaxHitPointsQ6}/armor={traceTarget.ArmorClass}/"
                            + $"ai={traceTarget.NativeAi}/hitModel=[{DescribeRangedHitModel(checked((int)
                                DeterministicGridMetrics.ChebyshevDistance(playerState.PositionX,
                                    playerState.PositionY, traceTarget.PositionX, traceTarget.PositionY)), traceTarget)}],"
                            + $"arrowsBefore=[{arrowsBefore}],arrowsAfter=[{DescribeWeaponArrows()}],events=[{events}]");
                    while (recentProjectileTicks.Count > 12)
                        recentProjectileTicks.Dequeue();
                }
                UseHealingPotionIfNeeded();
            }
        }

        void AdvanceCombatAndClearThreats(ulong endTick)
        {
            AdvanceCombatTo(endTick);
            ClearImmediateThreats(rangedTarget.EntityId,
                (rangedTarget.PositionX, rangedTarget.PositionY));
        }

        var navigationActionsRemaining = 256;
        var activePoolBeforePreFightThreatClear = Assert.IsType<DiabloCathedralItemCheckpoint>(
            world.CaptureCathedralItems()).Levels.Single().ActiveItemCount;
        var worldItemIdsBeforePreFightThreatClear = world.WorldItems.Keys.ToHashSet();
        ClearImmediateThreats(rangedTarget.EntityId,
            (rangedTarget.PositionX, rangedTarget.PositionY));
        var preFightThreatDrops = world.WorldItems.Values
            .Where(item => !worldItemIdsBeforePreFightThreatClear.Contains(item.EntityId))
            .OrderBy(item => item.EntityId)
            .ToArray();
        var activePoolAfterPreFightThreatClear = Assert.IsType<DiabloCathedralItemCheckpoint>(
            world.CaptureCathedralItems()).Levels.Single().ActiveItemCount;
        Assert.Equal(activePoolBeforePreFightThreatClear + preFightThreatDrops.Length,
            activePoolAfterPreFightThreatClear);
        Assert.True(playerState.Life > 0,
            $"The Rogue could not safely clear nearby attackers before the primary loot fight; "
                + $"life={playerState.Life}/cleared=[{string.Join(",", clearedThreatEntityIds)}]/"
                + $"potions=[{string.Join("|", healingPotionUses)}]/"
                + $"shots=[{string.Join(" | ", threatShotDiagnostics.TakeLast(12))}]");

        var playerExperienceBeforeFight = playerState.Experience;
        var fightEventsBeforePrimaryFight = fightEvents.Count;
        var targetInitialPosition = (rangedTarget.PositionX, rangedTarget.PositionY);
        var targetInitialHitPoints = rangedTarget.HitPoints;
        var playerInitialBowDurability = Assert.Single(playerState.Equipment,
            item => item.State.StableItemId == startingBow.State.StableItemId
                && item.ItemSeed == startingBow.ItemSeed).State.Durability;
        var worldItemIdsBeforeFight = world.WorldItems.Keys.ToHashSet();
        Assert.True(MoveToRangedLineOfSight(simulation, runtime, world, playerState,
                rangedTarget, ref navigationActionsRemaining, AdvanceCombatAndClearThreats),
            $"The Rogue could not reach a firing position through bounded ordinary movement and door commands; "
                + $"preFightCleared=[{string.Join(",", clearedThreatEntityIds)}]/"
                + $"preFightPotions=[{string.Join("|", healingPotionUses)}].");

        var targetHitPointsAtCombatStart = rangedTarget.HitPoints;
        var acceptedRangedAttacks = 0;
        var combatIterations = 0;
        while (rangedTarget.HitPoints > 0 && acceptedRangedAttacks < 48 && combatIterations < 256) {
            combatIterations++;
            ClearImmediateThreats(rangedTarget.EntityId,
                (rangedTarget.PositionX, rangedTarget.PositionY));
            Assert.True(playerState.Life > 0,
                $"The Rogue died before the selected native loot target after clearing nearby attackers; "
                    + $"life={playerState.Life}/potions=[{string.Join("|", healingPotionUses)}]/"
                    + $"cleared=[{string.Join(",", clearedThreatEntityIds)}].");
            Assert.True(MoveToRangedLineOfSight(simulation, runtime, world, playerState,
                    rangedTarget, ref navigationActionsRemaining, AdvanceCombatAndClearThreats),
                "The moving target left its close, projectile-clear firing lane and no bounded movement/door route restored it.");
            if (acceptedRangedAttacks == 0) {
                // Threats can be killed while the Rogue reaches the first firing lane. Start the
                // primary-target baseline only after that ordinary combat and repositioning.
                playerExperienceBeforeFight = playerState.Experience;
                fightEventsBeforePrimaryFight = fightEvents.Count;
                targetInitialPosition = (rangedTarget.PositionX, rangedTarget.PositionY);
                targetInitialHitPoints = rangedTarget.HitPoints;
                playerInitialBowDurability = Assert.Single(playerState.Equipment,
                    item => item.State.StableItemId == startingBow.State.StableItemId
                        && item.ItemSeed == startingBow.ItemSeed).State.Durability;
                worldItemIdsBeforeFight = world.WorldItems.Keys.ToHashSet();
            }

            var attackTick = checked(runtime.LastAdvancedTick + 1);
            var hitPointsBeforeAttack = rangedTarget.HitPoints;
            var hitPointsQ6BeforeAttack = rangedTarget.HitPointsQ6;
            var attackResult = simulation.Execute(playerState,
                new DiabloAttackIntent(rangedTarget.EntityId), attackTick, [playerState]);
            Assert.True(attackResult.Succeeded,
                $"The native Rogue bow attack was refused: {attackResult.Failure}.");
            var attack = Assert.IsType<AuthoritativePlayerActionState>(playerState.CurrentAction);
            Assert.Equal(DiabloPlayerActionKind.Attack, attack.Kind);
            Assert.Equal(rangedTarget.EntityId, attack.TargetEntityId);
            Assert.Equal(DiabloPlayerCombatMode.RangedAttack, playerState.CombatMode);
            acceptedRangedAttacks++;
            var eventsBeforeAttack = fightEvents.Count;
            shotArrowIdsAtStart = world.Projectiles.Values.Where(projectile =>
                projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                && projectile.SourceEntityId == playerState.EntityId)
                .Select(projectile => projectile.EntityId).ToHashSet();
            observedShotArrowIds.Clear();
            var playerPositionAtAttack = (playerState.PositionX, playerState.PositionY);
            var targetPositionAtAttack = (rangedTarget.PositionX, rangedTarget.PositionY);
            var distanceAtAttack = checked((int)DeterministicGridMetrics.ChebyshevDistance(
                playerPositionAtAttack.PositionX,
                playerPositionAtAttack.PositionY,
                targetPositionAtAttack.PositionX,
                targetPositionAtAttack.PositionY));
            captureProjectileTicks = true;
            AdvanceCombatTo(attack.EndTick);
            var actionEndTick = runtime.LastAdvancedTick;
            var hitPointsAtActionEnd = rangedTarget.HitPoints;
            var hitPointsQ6AtActionEnd = rangedTarget.HitPointsQ6;
            var arrowsAtActionEnd = DescribeWeaponArrows();

            // A target may move while an arrow is in flight. Let this shot settle
            // before admitting the next real command, while retaining each tick's
            // non-mutating pre-effect loot preview for the lethal tick.
            var flightWaitTicks = 0;
            for (; rangedTarget.HitPoints > 0 && flightWaitTicks < 32
                && world.Projectiles.Values.Any(projectile =>
                    projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                    && projectile.SourceEntityId == playerState.EntityId
                    && projectile.TargetEntityId == rangedTarget.EntityId); flightWaitTicks++)
                AdvanceCombatTo(checked(runtime.LastAdvancedTick + 1));
            captureProjectileTicks = false;
            var attackEvents = fightEvents.Skip(eventsBeforeAttack).ToArray();
            var targetDamage = attackEvents.Where(value => value.Kind == DiabloGameplayEventKind.Damage
                && value.TargetEntityId == rangedTarget.EntityId).Sum(value => value.Amount);
            var shotExperience = attackEvents.Where(value => value.Kind == DiabloGameplayEventKind.Experience
                && value.TargetEntityId == playerState.EntityId).Sum(value => value.Amount);
            shotDiagnostics.Add(
                $"shot={acceptedRangedAttacks},tick={attack.StartedAtTick}/{attack.EffectTick}/{attack.EndTick},"
                    + $"heroAt={playerPositionAtAttack},targetAt={targetPositionAtAttack},"
                    + $"hitModelAtCommand=[{DescribeRangedHitModel(distanceAtAttack, rangedTarget)}],"
                    + $"hp={hitPointsBeforeAttack}->{hitPointsAtActionEnd}->{rangedTarget.HitPoints},"
                    + $"hpQ6={hitPointsQ6BeforeAttack}->{hitPointsQ6AtActionEnd}->{rangedTarget.HitPointsQ6},"
                    + $"actionEndAdvanced={actionEndTick},flightWaitTicks={flightWaitTicks},"
                    + $"arrowIdsObserved=[{string.Join(",", observedShotArrowIds.Order())}],"
                    + $"arrowsAtActionEnd=[{arrowsAtActionEnd}],arrowsAfterFlight=[{DescribeWeaponArrows()}],"
                    + $"damageThisShot={targetDamage},xpThisShot={shotExperience}");

            if (rangedTarget.HitPoints < hitPointsBeforeAttack)
                Assert.Contains(fightEvents, gameplayEvent =>
                    gameplayEvent.Kind == DiabloGameplayEventKind.Damage
                    && gameplayEvent.TargetEntityId == rangedTarget.EntityId);
        }
        Assert.True(rangedTarget.HitPoints == 0,
            $"The Rogue did not defeat its generated loot target within the bounded combat/reposition loop; "
                + $"target={rangedTarget.EntityId}/monster={rangedTarget.MonsterId}/"
                + $"native={rangedTarget.NativeInitialization?.InitialState.Type}/slot="
                + $"{rangedTarget.NativeInitialization?.InitialState.Slot}/ai={rangedTarget.NativeAi},"
                + $"targetInitial={targetInitialPosition}/hp={targetInitialHitPoints},"
                + $"targetCombatStartHp={targetHitPointsAtCombatStart}/current=({rangedTarget.PositionX},"
                + $"{rangedTarget.PositionY})/hp={rangedTarget.HitPoints}/hpQ6={rangedTarget.HitPointsQ6}/"
                + $"maxHpQ6={rangedTarget.MaxHitPointsQ6}/armor={rangedTarget.ArmorClass}/"
                + $"moving={rangedTarget.IsMoving}/petrified={rangedTarget.IsPetrified}/"
                + $"future=({rangedTarget.FuturePositionX},{rangedTarget.FuturePositionY}),"
                + $"hero=({playerState.PositionX},{playerState.PositionY})/life={playerState.Life},"
                + $"bowDurability={playerInitialBowDurability}->"
                + $"{playerState.Equipment.FirstOrDefault(item =>
                    item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed)?.State.Durability},"
                + $"heroTargetMissileBlock={DescribeMissileBlockage((int)playerState.LevelId,
                    playerState.PositionX, playerState.PositionY, rangedTarget.PositionX, rangedTarget.PositionY,
                    rangedTarget.EntityId)},"
                + $"arrowsLive={world.Projectiles.Values.Count(projectile =>
                    projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                    && projectile.SourceEntityId == playerState.EntityId)},"
                + $"allProjectileCount={world.Projectiles.Count},gold={playerState.Gold},"
                + $"xp={playerExperienceBeforeFight}->{playerState.Experience},"
                + $"damageEvents={fightEvents.Count(value => value.Kind == DiabloGameplayEventKind.Damage
                    && value.TargetEntityId == rangedTarget.EntityId)}/xpEvents={fightEvents.Count(value =>
                    value.Kind == DiabloGameplayEventKind.Experience && value.TargetEntityId == playerState.EntityId)},"
                + $"healingEvents=[{string.Join(",", fightEvents.Where(value =>
                    value.Kind == DiabloGameplayEventKind.Healing
                    && value.TargetEntityId == rangedTarget.EntityId).Select(value =>
                    $"{value.SourceEntityId}>{value.TargetEntityId}:{value.Amount}/q6={value.AmountQ6}"))}],"
                + $"lastEvents=[{string.Join(",", fightEvents.TakeLast(12).Select(value =>
                    $"{value.Kind}:{value.SourceEntityId}>{value.TargetEntityId}:{value.Amount}/q6={value.AmountQ6}"))}],"
                + $"navigationRemaining={navigationActionsRemaining},attacks={acceptedRangedAttacks},"
                + $"hitModelNow=[{DescribeRangedHitModel(checked((int)DeterministicGridMetrics.ChebyshevDistance(
                    playerState.PositionX, playerState.PositionY,
                    rangedTarget.PositionX, rangedTarget.PositionY)), rangedTarget)}],"
                + $"iterations={combatIterations},shots=[{string.Join(" | ", shotDiagnostics.TakeLast(8))}],"
                + $"healingPotions=[{string.Join(" | ", healingPotionUses)}],"
                + $"recentProjectileTicks=[{string.Join(" | ", recentProjectileTicks)}].");
        Assert.True(rangedTarget.ExperienceReward > 0);
        Assert.True(playerState.Experience > playerExperienceBeforeFight);
        Assert.Contains(fightEvents.Skip(fightEventsBeforePrimaryFight), gameplayEvent =>
            gameplayEvent.Kind == DiabloGameplayEventKind.Experience
            && gameplayEvent.TargetEntityId == playerState.EntityId
            && gameplayEvent.Amount > 0);

        for (var deathTick = 0; world.CombatTargets.ContainsKey(rangedTarget.EntityId) && deathTick < 32;
            deathTick++)
            AdvanceCombatTo(checked(runtime.LastAdvancedTick + 1));
        Assert.False(world.CombatTargets.ContainsKey(rangedTarget.EntityId));
        Assert.True(world.Corpses.ContainsKey(rangedTarget.EntityId));

        var earnedDrop = Assert.Single(world.WorldItems.Values, item =>
            !worldItemIdsBeforeFight.Contains(item.EntityId)
            && item.LevelId == playerState.LevelId
            && item.State.ItemIndex == lootPreview.Loot.Item!.ItemIndex
            && item.ItemSeed == lootPreview.Loot.ItemSeed
            && item.State.StableItemId == lootPreview.Projection!.Binding.StableItemId);
        Assert.NotEqual(11, earnedDrop.State.ItemType);
        Assert.Equal(lootPreview.Loot.Item!.ItemIndex, earnedDrop.State.ItemIndex);
        Assert.Equal(lootPreview.Loot.ItemSeed, earnedDrop.ItemSeed);
        Assert.Equal(lootPreview.Projection!.Binding.StableItemId, earnedDrop.State.StableItemId);
        Assert.NotNull(immediatelyBeforeDeathPreview);
        Assert.Equal(0U, immediatelyBeforeDeathPreview!.EntityId);
        Assert.True(immediatelyBeforeDeathPreview!.RequiresWorldItem);
        Assert.Equal(earnedDrop.State.ItemIndex, immediatelyBeforeDeathPreview.Loot.Item!.ItemIndex);
        Assert.Equal(earnedDrop.ItemSeed, immediatelyBeforeDeathPreview.Loot.ItemSeed);
        var threatDropsBeforePostKillClear = world.WorldItems.Values
            .Where(item => !worldItemIdsBeforePreFightThreatClear.Contains(item.EntityId)
                && item.EntityId != earnedDrop.EntityId)
            .OrderBy(item => item.EntityId)
            .ToArray();

        AuthoritativeCombatTarget? FindImmediateThreat(
            uint? excludedEntityId,
            (int X, int Y)? focusPosition)
        {
            var incomingDamageRecency = fightEvents
                .Select((value, index) => (value, index))
                .Where(entry => entry.value.Kind == DiabloGameplayEventKind.Damage
                    && entry.value.TargetEntityId == playerState.EntityId
                    && entry.value.AmountQ6 > 0)
                .GroupBy(entry => entry.value.SourceEntityId)
                .ToDictionary(group => group.Key, group => group.Max(entry => entry.index));
            return world.CombatTargets.Values
                .Where(target => target.EntityId != excludedEntityId
                    && target.LevelId == playerState.LevelId
                    && target.HitPoints > 0
                    && !target.IsPetrified
                    && !target.IsPlayerMinion
                    && !target.IsUnique
                    && target.NativeInitialization is not null)
                .Where(target => DeterministicGridMetrics.ChebyshevDistance(
                        target.PositionX, target.PositionY, playerState.PositionX, playerState.PositionY) <= 4
                    || (focusPosition is { } focus
                        && DeterministicGridMetrics.ChebyshevDistance(
                            target.PositionX, target.PositionY, focus.X, focus.Y) <= 3))
                .OrderByDescending(target => incomingDamageRecency.GetValueOrDefault(target.EntityId, -1))
                .ThenBy(target => DeterministicGridMetrics.ChebyshevDistance(
                    target.PositionX, target.PositionY, playerState.PositionX, playerState.PositionY))
                .ThenBy(target => target.EntityId)
                .FirstOrDefault();
        }

        void ClearImmediateThreats(
            uint? excludedEntityId,
            (int X, int Y)? focusPosition)
        {
            const int maximumThreats = 8;
            const int maximumShotsPerThreat = 16;
            while (clearedThreatEntityIds.Count < maximumThreats) {
                var threat = FindImmediateThreat(excludedEntityId, focusPosition);
                if (threat is null)
                    return;
                var threatId = threat.EntityId;
                var initialHpQ6 = threat.HitPointsQ6;
                var initialHeroLife = playerState.Life;
                var potionUsesBeforeThreat = healingPotionUses.Count;
                var eventsAtThreatStart = fightEvents.Count;
                var damageEventsBeforeThreat = fightEvents.Count(value =>
                    value.Kind == DiabloGameplayEventKind.Damage
                    && value.TargetEntityId == playerState.EntityId);
                var shots = threatShotsByEntity.GetValueOrDefault(threatId);
                while (shots < maximumShotsPerThreat
                    && world.CombatTargets.TryGetValue(threatId, out var currentThreat)
                    && currentThreat.HitPoints > 0) {
                    Assert.True(playerState.Life > 0,
                        $"The Rogue died before clearing an immediate loot-route threat {threatId}; "
                            + $"initialLife={initialHeroLife}/currentLife={playerState.Life}/"
                            + $"hero=({playerState.PositionX},{playerState.PositionY})/"
                            + $"threat=({currentThreat.PositionX},{currentThreat.PositionY})/"
                            + $"hpQ6={currentThreat.HitPointsQ6}/ai={currentThreat.NativeAi}/"
                            + $"potionsBefore={potionUsesBeforeThreat}/potions=[{string.Join("|", healingPotionUses)}]/"
                            + $"attacks={shots}/damageEventsToHero={damageEventsBeforeThreat}/"
                            + $"recentShots=[{string.Join(" | ", threatShotDiagnostics.TakeLast(12))}]");
                    Assert.True(MoveToRangedLineOfSight(simulation, runtime, world, playerState,
                            currentThreat, ref navigationActionsRemaining, AdvanceCombatTo,
                            preferredMinimumFiringDistance: 3,
                            allowImmediateAdjacentCounterattack: true),
                        $"The Rogue could not reach a clear bow lane for immediate threat {threatId}; "
                            + $"initialLife={initialHeroLife}/hero=({playerState.PositionX},{playerState.PositionY})/"
                            + $"life={playerState.Life}/potionsBefore={potionUsesBeforeThreat}/"
                            + $"potions=[{string.Join("|", healingPotionUses)}],"
                            + $"threat=({currentThreat.PositionX},{currentThreat.PositionY})/"
                            + $"future=({currentThreat.FuturePositionX},{currentThreat.FuturePositionY})/"
                            + $"hpQ6={currentThreat.HitPointsQ6},"
                            + $"block={DescribeMissileBlockage((int)playerState.LevelId,
                                playerState.PositionX, playerState.PositionY, currentThreat.PositionX,
                                currentThreat.PositionY, threatId)}");
                    Assert.True(playerState.Life > 0,
                        $"The Rogue died while repositioning before attacking threat {threatId}; "
                            + $"initialLife={initialHeroLife}/currentLife={playerState.Life}/"
                            + $"potionsBefore={potionUsesBeforeThreat}/potions=[{string.Join("|", healingPotionUses)}]/"
                            + $"threat=({currentThreat.PositionX},{currentThreat.PositionY})/hpQ6={currentThreat.HitPointsQ6}/"
                            + $"events=[{string.Join("|", fightEvents.TakeLast(12).Select(value =>
                                $"{value.Kind}:{value.SourceEntityId}>{value.TargetEntityId}:{value.Amount}/q6={value.AmountQ6}"))}]");

                    var priorityThreat = FindImmediateThreat(excludedEntityId, focusPosition);
                    if (priorityThreat is not null && priorityThreat.EntityId != threatId)
                        break;

                    var attackTick = checked(runtime.LastAdvancedTick + 1);
                    var attack = simulation.Execute(playerState,
                        new DiabloAttackIntent(threatId), attackTick, [playerState]);
                    Assert.True(attack.Succeeded,
                        $"The Rogue's ordinary bow attack against immediate threat {threatId} was refused: "
                            + $"{attack.Failure}; initialLife={initialHeroLife}/life={playerState.Life}/"
                            + $"potionsBefore={potionUsesBeforeThreat}/potions=[{string.Join("|", healingPotionUses)}]/"
                            + $"hero=({playerState.PositionX},"
                            + $"{playerState.PositionY})/threat=({currentThreat.PositionX},"
                            + $"{currentThreat.PositionY})/hpQ6={currentThreat.HitPointsQ6}/"
                            + $"events=[{string.Join("|", fightEvents.Skip(eventsAtThreatStart).Select(value =>
                                $"{value.Kind}:{value.SourceEntityId}>{value.TargetEntityId}:{value.Amount}/q6={value.AmountQ6}"))}]");
                    var action = Assert.IsType<AuthoritativePlayerActionState>(playerState.CurrentAction);
                    Assert.Equal(DiabloPlayerActionKind.Attack, action.Kind);
                    Assert.Equal(DiabloPlayerCombatMode.RangedAttack, playerState.CombatMode);
                    var hpQ6Before = currentThreat.HitPointsQ6;
                    var eventsBefore = fightEvents.Count;
                    shotArrowIdsAtStart = world.Projectiles.Values.Where(projectile =>
                        projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                        && projectile.SourceEntityId == playerState.EntityId)
                        .Select(projectile => projectile.EntityId).ToHashSet();
                    observedShotArrowIds.Clear();
                    projectileTraceTarget = currentThreat;
                    captureProjectileTicks = true;
                    AdvanceCombatTo(action.EndTick);
                    var flightWaitTicks = 0;
                    for (; flightWaitTicks < 32 && currentThreat.HitPoints > 0
                        && world.Projectiles.Values.Any(projectile =>
                            projectile.Behavior == AuthoritativeProjectileBehavior.WeaponArrow
                            && projectile.SourceEntityId == playerState.EntityId
                            && projectile.TargetEntityId == threatId); flightWaitTicks++)
                        AdvanceCombatTo(checked(runtime.LastAdvancedTick + 1));
                    captureProjectileTicks = false;
                    projectileTraceTarget = null;
                    var threatEvents = fightEvents.Skip(eventsBefore).ToArray();
                    var damageToThreatQ6 = threatEvents.Where(value =>
                            value.Kind == DiabloGameplayEventKind.Damage && value.TargetEntityId == threatId)
                        .Sum(value => value.AmountQ6);
                    var damageToHeroQ6 = threatEvents.Where(value =>
                            value.Kind == DiabloGameplayEventKind.Damage
                            && value.TargetEntityId == playerState.EntityId)
                        .Sum(value => value.AmountQ6);
                    threatShotDiagnostics.Add(
                        $"threat={threatId}/ai={currentThreat.NativeAi}/shot={shots + 1}/"
                            + $"initialLife={initialHeroLife}/potionsBefore={potionUsesBeforeThreat}/"
                            + $"tick={action.StartedAtTick}/{action.EffectTick}/{action.EndTick}/"
                            + $"hero=({playerState.PositionX},{playerState.PositionY})/"
                            + $"target=({currentThreat.PositionX},{currentThreat.PositionY})/"
                            + $"hpQ6={hpQ6Before}->{currentThreat.HitPointsQ6}/"
                            + $"arrowsCreated=[{string.Join(",", observedShotArrowIds.Order())}]/"
                            + $"arrowState=[{DescribeWeaponArrows()}]/damageThreatQ6={damageToThreatQ6}/"
                            + $"damageHeroQ6={damageToHeroQ6}/events={threatEvents.Length}/"
                            + $"flight={flightWaitTicks}/currentAction={playerState.CurrentAction?.Kind.ToString() ?? "none"}/"
                            + $"life={playerState.Life}/events=[{string.Join("|", threatEvents.Select(value =>
                                $"{value.Kind}:{value.SourceEntityId}>{value.TargetEntityId}:{value.Amount}/q6={value.AmountQ6}"))}]/"
                            + $"recentTicks=[{string.Join("|", recentProjectileTicks.TakeLast(8))}]");
                    shots++;
                    threatShotsByEntity[threatId] = shots;
                    Assert.True(playerState.Life > 0,
                        $"The Rogue died during an ordinary bow attack on immediate threat {threatId}; "
                            + $"initialLife={initialHeroLife}/currentLife={playerState.Life}/"
                            + $"potionsBefore={potionUsesBeforeThreat}/potions=[{string.Join("|", healingPotionUses)}]/"
                            + $"attackCount={shots}/currentAction={playerState.CurrentAction?.Kind.ToString() ?? "none"}/"
                            + $"threatShots=[{string.Join(" | ", threatShotDiagnostics.TakeLast(12))}]");
                    UseHealingPotionIfNeeded();

                    var newlyDamagingAttacker = threatEvents
                        .Where(value => value.Kind == DiabloGameplayEventKind.Damage
                            && value.TargetEntityId == playerState.EntityId
                            && value.SourceEntityId != threatId
                            && value.AmountQ6 > 0)
                        .Select(value => world.CombatTargets.TryGetValue(value.SourceEntityId, out var candidate)
                            ? candidate
                            : null)
                        .LastOrDefault(candidate => candidate is not null
                            && candidate.HitPoints > 0
                            && candidate.LevelId == playerState.LevelId
                            && candidate.NativeInitialization is not null
                            && !candidate.IsPetrified
                            && !candidate.IsPlayerMinion
                            && !candidate.IsUnique);
                    if (newlyDamagingAttacker is not null)
                        break;
                }

                if (world.CombatTargets.TryGetValue(threatId, out var survivingThreat)
                    && survivingThreat.HitPoints > 0) {
                    Assert.True(shots < maximumShotsPerThreat,
                        $"The immediate loot-route threat exceeded its cumulative bow-shot budget: "
                            + $"entity={threatId}/ai={threat.NativeAi}/hpQ6={survivingThreat.HitPointsQ6}/"
                            + $"shots={shots}/{maximumShotsPerThreat}/initialHeroLife={initialHeroLife}/"
                            + $"hero=({playerState.PositionX},{playerState.PositionY})/life={playerState.Life}/"
                            + $"potions=[{string.Join("|", healingPotionUses)}]/"
                            + $"recentShots=[{string.Join("|", threatShotDiagnostics.TakeLast(16))}]");
                    continue;
                }

                Assert.False(world.CombatTargets.ContainsKey(threatId),
                    $"The immediate loot-route threat survived its bounded ordinary bow attacks: "
                        + $"entity={threatId}/ai={threat.NativeAi}/initialHpQ6={initialHpQ6}/shots={shots}/"
                        + $"initialHeroLife={initialHeroLife}/hero=({playerState.PositionX},{playerState.PositionY})/"
                        + $"life={playerState.Life}/potionUsesBefore={potionUsesBeforeThreat}/"
                        + $"potions=[{string.Join("|", healingPotionUses)}]/"
                        + $"recentShots=[{string.Join("|", threatShotDiagnostics.TakeLast(16))}]");
                Assert.Contains(world.Corpses.Values, corpse => corpse.EntityId == threatId);
                clearedThreatEntityIds.Add(threatId);
            }

            Assert.Null(FindImmediateThreat(excludedEntityId, focusPosition));
        }

        void ClearThreatsAroundLootDrop() => ClearImmediateThreats(
            excludedEntityId: null,
            focusPosition: (earnedDrop.PositionX, earnedDrop.PositionY));

        var activePoolBeforeThreatClear = Assert.IsType<DiabloCathedralItemCheckpoint>(
            world.CaptureCathedralItems()).Levels.Single().ActiveItemCount;
        var worldItemIdsBeforeThreatClear = world.WorldItems.Keys.ToHashSet();
        ClearImmediateThreats(excludedEntityId: null,
            focusPosition: (earnedDrop.PositionX, earnedDrop.PositionY));
        UseHealingPotionIfNeeded();
        MoveAdjacentToWorldItem(simulation, runtime, world, playerState, earnedDrop, sessionId,
            AdvanceCombatTo, fightEvents, ClearThreatsAroundLootDrop);
        var unclaimedThreatDrops = world.WorldItems.Values
            .Where(item => !worldItemIdsBeforeThreatClear.Contains(item.EntityId))
            .OrderBy(item => item.EntityId)
            .ToArray();
        var activePoolAfterThreatClear = Assert.IsType<DiabloCathedralItemCheckpoint>(
            world.CaptureCathedralItems()).Levels.Single().ActiveItemCount;
        Assert.Equal(activePoolBeforeThreatClear + unclaimedThreatDrops.Length, activePoolAfterThreatClear);
        var expectedActivePoolAfterEarnedPickup = activePoolAfterThreatClear - 1;
        // Healing remains conditional; correct native attack cadence may need no potion.
        var earnedPickupTick = checked(runtime.LastAdvancedTick + 1);
        var earnedPickup = simulation.Execute(playerState,
            new DiabloPickupWorldItemIntent(earnedDrop.EntityId), earnedPickupTick, [playerState]);
        Assert.True(earnedPickup.Succeeded,
            $"The generated non-Gold loot could not be picked up: {earnedPickup.Failure}.");
        runtime.AdvanceTo(earnedPickupTick);
        Assert.DoesNotContain(world.WorldItems.Values, item => item.EntityId == earnedDrop.EntityId);
        var carriedItems = new List<(uint Seed, AuthoritativeItemState State)>();
        carriedItems.AddRange(playerState.Inventory.Select(item => (item.ItemSeed, item.State)));
        carriedItems.AddRange(playerState.Equipment.Select(item => (item.ItemSeed, item.State)));
        carriedItems.AddRange(playerState.Belt.Select(item => (item.ItemSeed, item.State)));
        if (playerState.HeldItem is { } heldEarnedItem)
            carriedItems.Add((heldEarnedItem.ItemSeed, heldEarnedItem.State));
        var carriedEarnedItem = Assert.Single(carriedItems,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.Seed == earnedDrop.ItemSeed);
        var earnedDefinition = simulation.Items!.GetRequired(earnedDrop.State.StableItemId);
        var expectedPickupState = earnedDrop.State with {
            StatFlag = playerState.Attributes.Strength.Current >= earnedDefinition.MinimumStrength
                && playerState.Attributes.Magic.Current >= earnedDefinition.MinimumMagic
                && playerState.Attributes.Dexterity.Current >= earnedDefinition.MinimumDexterity,
        };
        Assert.True(expectedPickupState.StatFlag,
            $"The earned native item does not meet the Rogue's ordinary equipment requirements: "
                + $"symbol={earnedDefinition.StableSymbol}/itemIndex={earnedDrop.State.ItemIndex}/"
                + $"equip={earnedDrop.State.EquipLocation}/requirements={earnedDefinition.MinimumStrength},"
                + $"{earnedDefinition.MinimumMagic},{earnedDefinition.MinimumDexterity}/"
                + $"attributes={playerState.Attributes.Strength.Current},{playerState.Attributes.Magic.Current},"
                + $"{playerState.Attributes.Dexterity.Current}/groundStatFlag={earnedDrop.State.StatFlag}");
        Assert.Equal(expectedPickupState, carriedEarnedItem.State);
        Assert.Equal(earnedDrop.ItemSeed, carriedEarnedItem.Seed);
        var earnedPoolAfterPickup = Assert.IsType<DiabloCathedralItemCheckpoint>(world.CaptureCathedralItems());
        Assert.Equal(expectedActivePoolAfterEarnedPickup,
            Assert.Single(earnedPoolAfterPickup.Levels).ActiveItemCount);

        var itemInventoryIndex = playerState.Inventory.FindIndex(item =>
            item.State.StableItemId == purchasedBuckler.State.StableItemId
            && item.ItemSeed == purchasedBuckler.ItemSeed);
        Assert.True(itemInventoryIndex >= 0);
        var dropPosition = FindEmptyAdjacentCell(world, playerState);
        var dropTick = checked(runtime.LastAdvancedTick + 1);
        var dropped = simulation.Execute(playerState,
            new DiabloDropItemIntent(new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                checked((uint)itemInventoryIndex),
                purchasedBuckler.State.StableItemId),
                dropPosition.X,
                dropPosition.Y),
            dropTick,
            [playerState]);
        Assert.True(dropped.Succeeded, $"The purchased native item could not be dropped: {dropped.Failure}.");
        runtime.AdvanceTo(dropTick);
        var droppedItem = Assert.Single(world.WorldItems.Values,
            item => item.State.StableItemId == purchasedBuckler.State.StableItemId
                && item.ItemSeed == purchasedBuckler.ItemSeed);
        var poolWithDroppedItem = Assert.IsType<DiabloCathedralItemCheckpoint>(world.CaptureCathedralItems());
        var droppedLevel = Assert.Single(poolWithDroppedItem.Levels);
        var droppedSlot = Assert.Single(droppedLevel.SlotEntityBindings,
            binding => binding.EntityId == droppedItem.EntityId).Slot;
        var droppedRaw = droppedLevel.Items[droppedSlot];
        var expectedGroundPrice = checked((uint)Math.Max(0,
            droppedRaw.Identified ? droppedRaw.IdentifiedValue : droppedRaw.Value));
        Assert.Equal(1U, droppedItem.LevelId);
        Assert.Equal((dropPosition.X, dropPosition.Y), (droppedItem.PositionX, droppedItem.PositionY));
        Assert.Equal((dropPosition.X, dropPosition.Y),
            (droppedItem.State.PositionX, droppedItem.State.PositionY));
        Assert.Equal(purchasedBuckler.State.StableItemId, droppedItem.State.StableItemId);
        Assert.Equal(purchasedBuckler.ItemSeed, droppedItem.ItemSeed);
        Assert.Equal(droppedRaw.ItemIndex, droppedItem.State.ItemIndex);
        Assert.Equal(droppedRaw.Value, droppedItem.State.Value);
        Assert.Equal(droppedRaw.IdentifiedValue, droppedItem.State.IdentifiedValue);
        Assert.Equal(droppedRaw.Durability, droppedItem.State.Durability);
        Assert.Equal(droppedRaw.MaxDurability, droppedItem.State.MaxDurability);
        Assert.Equal(expectedGroundPrice, droppedItem.Price);
        Assert.Equal(30U, droppedItem.Price);
        Assert.Equal(AuthoritativeCathedralItemRuntime.PoolCapacity,
            droppedLevel.Items.Count);
        Assert.Equal(expectedActivePoolAfterEarnedPickup + 1,
            droppedLevel.ActiveItemCount);

        var pickupTick = checked(runtime.LastAdvancedTick + 1);
        var pickup = simulation.Execute(playerState,
            new DiabloPickupWorldItemIntent(droppedItem.EntityId), pickupTick, [playerState]);
        Assert.True(pickup.Succeeded, $"The purchased native item could not be picked up: {pickup.Failure}.");
        runtime.AdvanceTo(pickupTick);
        Assert.DoesNotContain(world.WorldItems.Values, item => item.EntityId == droppedItem.EntityId);
        var returnedBuckler = Assert.Single(playerState.Inventory,
            item => item.State.StableItemId == purchasedBuckler.State.StableItemId
                && item.ItemSeed == purchasedBuckler.ItemSeed);
        Assert.Equal(purchasedBuckler.State.StableItemId, returnedBuckler.State.StableItemId);
        Assert.Equal(purchasedBuckler.ItemSeed, returnedBuckler.ItemSeed);
        var expectedReturnedBucklerState = droppedItem.State with {
            CreateInfo = droppedItem.State.CreateInfo & ~0x8000U,
            StatFlag = playerState.Attributes.Strength.Current >= droppedItem.State.MinimumStrength
                && playerState.Attributes.Magic.Current >= droppedItem.State.MinimumMagic
                && playerState.Attributes.Dexterity.Current >= droppedItem.State.MinimumDexterity,
        };
        Assert.Equal(expectedReturnedBucklerState, returnedBuckler.State);
        Assert.Equal(expectedGroundPrice, returnedBuckler.Price);
        var poolAfterPickup = Assert.IsType<DiabloCathedralItemCheckpoint>(world.CaptureCathedralItems());
        Assert.Equal(expectedActivePoolAfterEarnedPickup,
            Assert.Single(poolAfterPickup.Levels).ActiveItemCount);

        var earnedEquipmentKind = AuthoritativeItemClassification.GetEquipmentSlotKind(
            carriedEarnedItem.State, simulation.Items!);
        Assert.NotEqual(DiabloEquipmentSlotKind.Unknown, earnedEquipmentKind);
        Assert.True(carriedEarnedItem.State.StatFlag);
        var earnedInventoryIndex = playerState.Inventory.FindIndex(item =>
            item.State.StableItemId == earnedDrop.State.StableItemId
            && item.ItemSeed == earnedDrop.ItemSeed);
        Assert.True(earnedInventoryIndex >= 0,
            "The selected earned equipment was not in inventory for the ordinary equip command.");
        var equipTick = checked(runtime.LastAdvancedTick + 1);
        var equip = simulation.Execute(playerState,
            new DiabloAutoEquipItemIntent(new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                checked((uint)earnedInventoryIndex),
                earnedDrop.State.StableItemId)),
            equipTick);
        Assert.True(equip.Succeeded, $"The earned native equipment could not be equipped: {equip.Failure}.");
        runtime.AdvanceTo(equipTick);
        var earnedAtCheckpoint = Assert.Single(playerState.Equipment,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        Assert.Equal(expectedPickupState, earnedAtCheckpoint.State);
        Assert.DoesNotContain(playerState.Inventory,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        var bucklerAtCheckpoint = Assert.Single(playerState.Inventory,
            item => item.State.StableItemId == purchasedBuckler.State.StableItemId
                && item.ItemSeed == purchasedBuckler.ItemSeed);
        var bowAtCheckpointEquipment = playerState.Equipment.SingleOrDefault(
            item => item.State.StableItemId == startingBow.State.StableItemId
                && item.ItemSeed == startingBow.ItemSeed);
        var bowAtCheckpointInventory = playerState.Inventory.SingleOrDefault(
            item => item.State.StableItemId == startingBow.State.StableItemId
                && item.ItemSeed == startingBow.ItemSeed);
        Assert.NotEqual(bowAtCheckpointEquipment is not null, bowAtCheckpointInventory is not null);
        var codec = new DiabloCohortCodec();
        var cathedralDoorsAtCheckpoint = world.Objects.Values
            .Where(value => value.LevelId == 1 && value.CathedralDoor is not null)
            .OrderBy(value => value.EntityId)
            .Select(value => (value.EntityId, value.CathedralDoor))
            .ToArray();
        var captured = simulation.CaptureCohortState(runtime);
        var encoded = codec.Serialize(captured);
        var decoded = codec.Deserialize(encoded);
        var factory = Assert.IsAssignableFrom<Func<DiabloCohortState, DiabloCohortCandidate>>(
            activation.CohortCandidateFactory);
        var candidate = factory(decoded);
        var restoredRuntime = candidate.CreateRuntime();
        var restoredSimulation = candidate.Simulation;
        var restoredWorld = restoredSimulation.WorldState;
        var restoredPlayer = restoredRuntime.Read(sessionId, player => player);
        Assert.Equal(encoded, codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(75U, restoredPlayer.Gold);
        Assert.Equal(playerState.Experience, restoredPlayer.Experience);
        Assert.Equal(bucklerAtCheckpoint, Assert.Single(restoredPlayer.Inventory,
            item => item.State.StableItemId == bucklerAtCheckpoint.State.StableItemId
                && item.ItemSeed == bucklerAtCheckpoint.ItemSeed));
        Assert.Equal(earnedAtCheckpoint, Assert.Single(restoredPlayer.Equipment,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed));
        if (bowAtCheckpointEquipment is not null) {
            Assert.Equal(bowAtCheckpointEquipment, Assert.Single(restoredPlayer.Equipment,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed));
            Assert.DoesNotContain(restoredPlayer.Inventory,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed);
        } else {
            Assert.Equal(bowAtCheckpointInventory, Assert.Single(restoredPlayer.Inventory,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed));
            Assert.DoesNotContain(restoredPlayer.Equipment,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed);
        }
        var restoredCarriedItems = new List<(uint Seed, AuthoritativeItemState State)>();
        restoredCarriedItems.AddRange(restoredPlayer.Inventory.Select(item => (item.ItemSeed, item.State)));
        restoredCarriedItems.AddRange(restoredPlayer.Equipment.Select(item => (item.ItemSeed, item.State)));
        restoredCarriedItems.AddRange(restoredPlayer.Belt.Select(item => (item.ItemSeed, item.State)));
        if (restoredPlayer.HeldItem is { } restoredHeldItem)
            restoredCarriedItems.Add((restoredHeldItem.ItemSeed, restoredHeldItem.State));
        var restoredEarnedItem = Assert.Single(restoredCarriedItems,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.Seed == earnedDrop.ItemSeed);
        Assert.Equal(expectedPickupState, restoredEarnedItem.State);
        Assert.Contains(restoredWorld.Corpses.Values, corpse => corpse.EntityId == rangedTarget.EntityId);
        Assert.DoesNotContain(restoredWorld.CombatTargets.Values,
            target => target.EntityId == rangedTarget.EntityId);
        foreach (var clearedThreatId in clearedThreatEntityIds) {
            Assert.Contains(restoredWorld.Corpses.Values, corpse => corpse.EntityId == clearedThreatId);
            Assert.DoesNotContain(restoredWorld.CombatTargets.Values,
                target => target.EntityId == clearedThreatId);
        }
        foreach (var preFightThreatDrop in threatDropsBeforePostKillClear) {
            var restoredThreatDrop = Assert.Single(restoredWorld.WorldItems.Values,
                item => item.EntityId == preFightThreatDrop.EntityId);
            Assert.Equal((preFightThreatDrop.LevelId, preFightThreatDrop.PositionX,
                preFightThreatDrop.PositionY, preFightThreatDrop.ItemSeed,
                preFightThreatDrop.Price, preFightThreatDrop.State), (
                restoredThreatDrop.LevelId, restoredThreatDrop.PositionX,
                restoredThreatDrop.PositionY, restoredThreatDrop.ItemSeed,
                restoredThreatDrop.Price, restoredThreatDrop.State));
        }
        Assert.Equal(cathedralDoorsAtCheckpoint, restoredWorld.Objects.Values
            .Where(value => value.LevelId == 1 && value.CathedralDoor is not null)
            .OrderBy(value => value.EntityId)
            .Select(value => (value.EntityId, value.CathedralDoor))
            .ToArray());
        Assert.DoesNotContain(restoredWorld.WorldItems.Values,
            item => item.EntityId == droppedItem.EntityId);
        Assert.Equal(expectedActivePoolAfterEarnedPickup,
            Assert.Single(Assert.IsType<DiabloCathedralItemCheckpoint>(
                restoredWorld.CaptureCathedralItems()).Levels).ActiveItemCount);
        foreach (var threatDrop in unclaimedThreatDrops) {
            var restoredDrop = Assert.Single(restoredWorld.WorldItems.Values,
                item => item.EntityId == threatDrop.EntityId);
            Assert.Equal((threatDrop.LevelId, threatDrop.PositionX, threatDrop.PositionY,
                threatDrop.ItemSeed, threatDrop.Price, threatDrop.State), (
                restoredDrop.LevelId, restoredDrop.PositionX, restoredDrop.PositionY,
                restoredDrop.ItemSeed, restoredDrop.Price, restoredDrop.State));
        }

        Assert.Null(restoredPlayer.ActiveStoreId);
        Assert.False(restoredPlayer.StashOpen);
        Assert.Null(restoredPlayer.HeldItem);
        var smithTownReturn = Assert.Single(restoredWorld.World!.WarpTargets,
            target => target.SourceLevelId == 1 && target.TargetLevelId == smith.LevelId);
        void AdvanceTownReturn(ulong endTick)
        {
            void UseRestoredHealingPotionIfNeeded()
            {
                var maximumLifeQ6 = Math.Max(restoredPlayer.LifeMaximum, 0);
                if (restoredPlayer.Life <= 0 || maximumLifeQ6 == 0
                    || (long)restoredPlayer.Life * 3 > (long)maximumLifeQ6 * 2)
                    return;
                var potion = restoredPlayer.Belt
                    .Where(item => item.State.StableItemId == 105)
                    .OrderBy(item => item.Slot)
                    .FirstOrDefault();
                if (potion is null)
                    return;
                var actionBefore = restoredPlayer.CurrentAction;
                var lifeBefore = restoredPlayer.Life;
                var useTick = checked(restoredRuntime.LastAdvancedTick + 1);
                var use = restoredSimulation.Execute(restoredPlayer,
                    new DiabloUseConsumableIntent(new DiabloPlayerItemReference(
                        DiabloPlayerItemLocation.Belt, potion.Slot, potion.State.StableItemId)), useTick);
                Assert.True(use.Succeeded,
                    $"The restored Rogue could not use an ordinary carried healing potion: {use.Failure}.");
                Assert.True(restoredPlayer.Life > lifeBefore);
                Assert.Equal(actionBefore, restoredPlayer.CurrentAction);
                restoredRuntime.AdvanceTo(useTick);
            }

            while (restoredRuntime.LastAdvancedTick < endTick) {
                restoredRuntime.AdvanceTo(checked(restoredRuntime.LastAdvancedTick + 1));
                _ = restoredRuntime.DrainEvents(sessionId);
                UseRestoredHealingPotionIfNeeded();
            }
        }
        MoveToLevelTrigger(restoredSimulation, restoredRuntime, restoredPlayer, smithTownReturn,
            AdvanceTownReturn);
        Assert.Equal(smith.LevelId, restoredPlayer.LevelId);
        var cathedralItemCheckpointAtTownArrival = JsonSerializer.Serialize(
            restoredWorld.CaptureCathedralItems());
        var cathedralWorldItemsAtTownArrival = restoredWorld.WorldItems.Values
            .Where(item => item.LevelId == 1)
            .OrderBy(item => item.EntityId)
            .Select(item => (item.EntityId, item.LevelId, item.PositionX, item.PositionY,
                item.ItemSeed, item.Price, item.State))
            .ToArray();
        var cathedralCorpsesAtTownArrival = restoredWorld.Corpses.Values
            .Where(corpse => corpse.LevelId == 1)
            .OrderBy(corpse => corpse.EntityId)
            .Select(corpse => (corpse.EntityId, corpse.LevelId, corpse.PositionX, corpse.PositionY))
            .ToArray();
        var cathedralDoorsAtTownArrival = restoredWorld.Objects.Values
            .Where(value => value.LevelId == 1 && value.CathedralDoor is not null)
            .OrderBy(value => value.EntityId)
            .Select(value => (value.EntityId, value.CathedralDoor))
            .ToArray();

        var barmaid = Assert.Single(restoredSimulation.TownerGreetings!.Definitions,
            definition => definition.TownerId == 7);
        MoveAdjacentToTowner(restoredSimulation, restoredRuntime, restoredPlayer, barmaid);
        var openStashTick = checked(restoredRuntime.LastAdvancedTick + 1);
        var openStash = restoredSimulation.Execute(restoredPlayer,
            new DiabloOpenStashIntent(), openStashTick);
        Assert.True(openStash.Succeeded, $"The returned Rogue could not open the Town stash: {openStash.Failure}.");
        restoredRuntime.AdvanceTo(openStashTick);
        Assert.True(restoredPlayer.StashOpen);
        var stashGoldBefore = restoredPlayer.StashGold;
        var capeAtTown = Assert.Single(restoredPlayer.Equipment,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        var stashPage = uint.MaxValue;
        var stashCell = -1;
        for (uint page = 0; page < checked((uint)DiabloStashLayout.PageCount) && stashCell < 0; page++) {
            var pageCells = DiabloStashLayout.CreateEmptyPage();
            foreach (var existing in restoredPlayer.Stash.Where(item => item.Page == page))
                DiabloStashLayout.Fill(pageCells, existing.Cell,
                    existing.State.InventoryWidth, existing.State.InventoryHeight);
            for (var cell = 0; cell < DiabloStashLayout.CellCount; cell++) {
                if (!DiabloStashLayout.CanPlace(pageCells, checked((uint)cell),
                        capeAtTown.State.InventoryWidth, capeAtTown.State.InventoryHeight))
                    continue;
                stashPage = page;
                stashCell = cell;
                break;
            }
        }
        Assert.InRange(stashPage, 0U, checked((uint)DiabloStashLayout.PageCount - 1));
        Assert.InRange(stashCell, 0, DiabloStashLayout.CellCount - 1);
        var stashCapeTick = checked(restoredRuntime.LastAdvancedTick + 1);
        var stashCape = restoredSimulation.Execute(restoredPlayer,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Equipment,
                    capeAtTown.Slot,
                    capeAtTown.State.StableItemId),
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    checked((uint)stashCell),
                    capeAtTown.State.StableItemId,
                    stashPage)),
            stashCapeTick);
        Assert.True(stashCape.Succeeded, $"The earned Cape could not be deposited in the Town stash: {stashCape.Failure}.");
        restoredRuntime.AdvanceTo(stashCapeTick);
        var stashedCape = Assert.Single(restoredPlayer.Stash,
            item => item.Page == stashPage && item.Cell == checked((uint)stashCell)
                && item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        Assert.Equal(earnedAtCheckpoint.State, stashedCape.State);
        Assert.DoesNotContain(restoredPlayer.Equipment,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);

        var inventoryCell = InventoryGridLayout.FindFirstPlacement(
            restoredPlayer.InventoryGrid, stashedCape.State.InventoryWidth, stashedCape.State.InventoryHeight);
        Assert.True(inventoryCell >= 0, "The returned Cape did not fit the Rogue's ordinary inventory grid.");
        var withdrawCapeTick = checked(restoredRuntime.LastAdvancedTick + 1);
        var withdrawCape = restoredSimulation.Execute(restoredPlayer,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    stashedCape.Cell,
                    stashedCape.State.StableItemId,
                    stashedCape.Page),
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    checked((uint)inventoryCell),
                    stashedCape.State.StableItemId)),
            withdrawCapeTick);
        Assert.True(withdrawCape.Succeeded,
            $"The earned Cape could not be withdrawn from the Town stash: {withdrawCape.Failure}.");
        restoredRuntime.AdvanceTo(withdrawCapeTick);
        Assert.DoesNotContain(restoredPlayer.Stash,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        var withdrawnCape = Assert.Single(restoredPlayer.Inventory,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        Assert.Equal(earnedAtCheckpoint.State, withdrawnCape.State);
        var withdrawnCapeIndex = restoredPlayer.Inventory.FindIndex(item =>
            item.State.StableItemId == earnedDrop.State.StableItemId
            && item.ItemSeed == earnedDrop.ItemSeed);
        Assert.True(withdrawnCapeIndex >= 0);
        var reequipCapeTick = checked(restoredRuntime.LastAdvancedTick + 1);
        var reequipCape = restoredSimulation.Execute(restoredPlayer,
            new DiabloAutoEquipItemIntent(new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                checked((uint)withdrawnCapeIndex),
                earnedDrop.State.StableItemId)),
            reequipCapeTick);
        Assert.True(reequipCape.Succeeded, $"The withdrawn Cape could not be re-equipped: {reequipCape.Failure}.");
        restoredRuntime.AdvanceTo(reequipCapeTick);
        var capeAfterRoundTrip = Assert.Single(restoredPlayer.Equipment,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        Assert.Equal(earnedAtCheckpoint.State, capeAfterRoundTrip.State);
        Assert.Equal(stashGoldBefore, restoredPlayer.StashGold);
        Assert.Equal(75U, restoredPlayer.Gold);
        Assert.True(restoredPlayer.StashOpen);
        var closeStashTick = checked(restoredRuntime.LastAdvancedTick + 1);
        var closeStash = restoredSimulation.Execute(restoredPlayer,
            new DiabloCloseStashIntent(), closeStashTick);
        Assert.True(closeStash.Succeeded, $"The Town stash session did not close: {closeStash.Failure}.");
        restoredRuntime.AdvanceTo(closeStashTick);
        Assert.False(restoredPlayer.StashOpen);
        var stashAfterTownRoundTrip = restoredPlayer.Stash.ToArray();

        var finalTownCheckpoint = restoredSimulation.CaptureCohortState(restoredRuntime);
        var finalTownBytes = codec.Serialize(finalTownCheckpoint);
        var finalTownCandidate = factory(codec.Deserialize(finalTownBytes));
        var finalTownRuntime = finalTownCandidate.CreateRuntime();
        var finalTownSimulation = finalTownCandidate.Simulation;
        var finalTownPlayer = finalTownRuntime.Read(sessionId, player => player);
        var finalTownWorld = finalTownSimulation.WorldState;
        Assert.Equal(finalTownBytes,
            codec.Serialize(finalTownSimulation.CaptureCohortState(finalTownRuntime)));
        Assert.Equal(smith.LevelId, finalTownPlayer.LevelId);
        Assert.Equal(75U, finalTownPlayer.Gold);
        Assert.Equal(stashGoldBefore, finalTownPlayer.StashGold);
        Assert.Null(finalTownPlayer.ActiveStoreId);
        Assert.False(finalTownPlayer.StashOpen);
        Assert.Null(finalTownPlayer.HeldItem);
        Assert.Equal(capeAfterRoundTrip, Assert.Single(finalTownPlayer.Equipment,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed));
        if (bowAtCheckpointEquipment is not null) {
            Assert.Equal(bowAtCheckpointEquipment, Assert.Single(finalTownPlayer.Equipment,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed));
            Assert.DoesNotContain(finalTownPlayer.Inventory,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed);
        } else {
            Assert.Equal(bowAtCheckpointInventory, Assert.Single(finalTownPlayer.Inventory,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed));
            Assert.DoesNotContain(finalTownPlayer.Equipment,
                item => item.State.StableItemId == startingBow.State.StableItemId
                    && item.ItemSeed == startingBow.ItemSeed);
        }
        Assert.Equal(bucklerAtCheckpoint, Assert.Single(finalTownPlayer.Inventory,
            item => item.State.StableItemId == purchasedBuckler.State.StableItemId
                && item.ItemSeed == purchasedBuckler.ItemSeed));
        Assert.DoesNotContain(finalTownPlayer.Stash,
            item => item.State.StableItemId == earnedDrop.State.StableItemId
                && item.ItemSeed == earnedDrop.ItemSeed);
        Assert.Equal(stashAfterTownRoundTrip, finalTownPlayer.Stash);
        Assert.Contains(finalTownWorld.Corpses.Values, corpse => corpse.EntityId == rangedTarget.EntityId);
        Assert.DoesNotContain(finalTownWorld.CombatTargets.Values,
            target => target.EntityId == rangedTarget.EntityId);
        foreach (var clearedThreatId in clearedThreatEntityIds) {
            Assert.Contains(finalTownWorld.Corpses.Values, corpse => corpse.EntityId == clearedThreatId);
            Assert.DoesNotContain(finalTownWorld.CombatTargets.Values,
                target => target.EntityId == clearedThreatId);
        }
        Assert.Equal(cathedralDoorsAtTownArrival, finalTownWorld.Objects.Values
            .Where(value => value.LevelId == 1 && value.CathedralDoor is not null)
            .OrderBy(value => value.EntityId)
            .Select(value => (value.EntityId, value.CathedralDoor))
            .ToArray());
        Assert.DoesNotContain(finalTownWorld.WorldItems.Values,
            item => item.EntityId == droppedItem.EntityId);
        Assert.Equal(expectedActivePoolAfterEarnedPickup,
            Assert.Single(Assert.IsType<DiabloCathedralItemCheckpoint>(
                finalTownWorld.CaptureCathedralItems()).Levels).ActiveItemCount);
        foreach (var threatDrop in unclaimedThreatDrops) {
            var finalDrop = Assert.Single(finalTownWorld.WorldItems.Values,
                item => item.EntityId == threatDrop.EntityId);
            Assert.Equal((threatDrop.LevelId, threatDrop.PositionX, threatDrop.PositionY,
                threatDrop.ItemSeed, threatDrop.Price, threatDrop.State), (
                finalDrop.LevelId, finalDrop.PositionX, finalDrop.PositionY,
                finalDrop.ItemSeed, finalDrop.Price, finalDrop.State));
        }
        Assert.Equal(cathedralItemCheckpointAtTownArrival,
            JsonSerializer.Serialize(finalTownWorld.CaptureCathedralItems()));
        Assert.Equal(cathedralWorldItemsAtTownArrival, finalTownWorld.WorldItems.Values
            .Where(item => item.LevelId == 1)
            .OrderBy(item => item.EntityId)
            .Select(item => (item.EntityId, item.LevelId, item.PositionX, item.PositionY,
                item.ItemSeed, item.Price, item.State))
            .ToArray());
        Assert.Equal(cathedralCorpsesAtTownArrival, finalTownWorld.Corpses.Values
            .Where(corpse => corpse.LevelId == 1)
            .OrderBy(corpse => corpse.EntityId)
            .Select(corpse => (corpse.EntityId, corpse.LevelId, corpse.PositionX, corpse.PositionY))
            .ToArray());
    }

    private static void MoveToLevelTrigger(
        DiabloSimulation simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime,
        AuthoritativePlayerState player,
        AuthoritativeWarpTarget trigger,
        Action<ulong>? advanceTo = null)
    {
        void Advance(ulong tick)
        {
            if (advanceTo is null)
                runtime.AdvanceTo(tick);
            else
                advanceTo(tick);
        }

        for (var step = 0; step < 128 && player.LevelId == trigger.SourceLevelId; step++) {
            var before = (player.LevelId, player.PositionX, player.PositionY);
            var tick = checked(runtime.LastAdvancedTick + 1);
            var result = simulation.Execute(player,
                new DiabloMoveToIntent(trigger.TriggerX, trigger.TriggerY), tick, [player]);
            Assert.True(result.Succeeded,
                $"Movement toward the level trigger {trigger.SourceLevelId}->{trigger.TargetLevelId} was refused: "
                    + $"{result.Failure}.");
            var action = player.CurrentAction;
            Assert.NotNull(action);
            Advance(action!.EndTick);
            for (var completion = 0; player.CurrentAction is { } pending; completion++) {
                Assert.True(completion < 3, "A native level-entry action did not settle after its bounded completion.");
                Assert.True(pending.EndTick > runtime.LastAdvancedTick,
                    "A completed movement action remained attached to the player.");
                Advance(pending.EndTick);
            }
            Assert.True(player.LevelId != before.LevelId
                || (player.PositionX, player.PositionY) != (before.PositionX, before.PositionY),
                "A successful movement command made no progress toward its level trigger.");
        }

        Assert.Equal(trigger.TargetLevelId, player.LevelId);
    }

    private static void MoveAdjacentToTowner(
        DiabloSimulation simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime,
        AuthoritativePlayerState player,
        AuthoritativeTownerGreetingDefinition greeting)
    {
        Assert.Equal(greeting.LevelId, player.LevelId);
        var world = simulation.WorldState;
        bool IsOpenAdjacentCell(int x, int y)
            => DeterministicGridMetrics.ChebyshevDistance(x, y, greeting.PositionX, greeting.PositionY) == 1
                && world.IsWalkable(greeting.LevelId, x, y)
                && !world.IsOccupied(greeting.LevelId, x, y, [player], player.EntityId);

        for (var actionCount = 0; actionCount < 256
            && DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX, player.PositionY, greeting.PositionX, greeting.PositionY) > 1;
            actionCount++) {
            var step = DeterministicGridNavigation.FindStepToGoal(
                player.PositionX,
                player.PositionY,
                maximumDepth: 256,
                IsOpenAdjacentCell,
                (x, y) => world.IsWalkable(greeting.LevelId, x, y),
                (x, y) => world.IsOccupied(greeting.LevelId, x, y, [player], player.EntityId));
            Assert.True(step is not null,
                $"The Rogue could not find an ordinary route to Town resident {greeting.TownerId}: "
                    + $"player=({player.PositionX},{player.PositionY})/resident=({greeting.PositionX},"
                    + $"{greeting.PositionY})/level={player.LevelId}.");
            var before = (player.PositionX, player.PositionY);
            var tick = checked(runtime.LastAdvancedTick + 1);
            var result = simulation.Execute(player,
                new DiabloMoveToIntent(step!.Value.X, step.Value.Y), tick, [player]);
            Assert.True(result.Succeeded,
                $"The Rogue's ordinary Town movement was refused: {result.Failure}.");
            var action = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
            Assert.Equal(DiabloPlayerActionKind.Move, action.Kind);
            runtime.AdvanceTo(action.EndTick);
            for (var completion = 0; player.CurrentAction is { } pending; completion++) {
                Assert.True(completion < 3,
                    "The Town movement action did not settle after bounded completion.");
                Assert.True(pending.EndTick > runtime.LastAdvancedTick);
                runtime.AdvanceTo(pending.EndTick);
            }
            Assert.NotEqual(before, (player.PositionX, player.PositionY));
        }

        Assert.Equal(greeting.LevelId, player.LevelId);
        Assert.InRange(DeterministicGridMetrics.ChebyshevDistance(
            player.PositionX, player.PositionY, greeting.PositionX, greeting.PositionY), 0L, 1L);
    }

    private static void MoveAdjacentToWorldItem(
        DiabloSimulation simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime,
        DiabloWorldState world,
        AuthoritativePlayerState player,
        AuthoritativeWorldItem item,
        string sessionId,
        Action<ulong>? advanceTo = null,
        IReadOnlyList<DiabloGameplayEvent>? observedEvents = null,
        Action? afterSettledAction = null)
    {
        const int maximumActions = 256;
        const int maximumConsecutiveNoProgress = 16;
        var consecutiveNoProgress = 0;
        var lastAttempt = "none";

        bool IsOpenAdjacentCell(int x, int y)
            => DeterministicGridMetrics.ChebyshevDistance(x, y, item.PositionX, item.PositionY) == 1
                && world.IsWalkable(item.LevelId, x, y)
                && !world.IsOccupied(item.LevelId, x, y, [player], player.EntityId)
                && !world.WorldItems.Values.Any(candidate => candidate.LevelId == item.LevelId
                    && candidate.PositionX == x && candidate.PositionY == y);

        string DescribeState()
        {
            var itemDefinition = simulation.Items?.Definitions.FirstOrDefault(
                definition => definition.ItemId == item.State.StableItemId);
            var actors = string.Join(";", world.CombatTargets.Values
                .Where(target => target.HitPointsQ6 > 0
                    && (target.LevelId == 0 || target.LevelId == player.LevelId))
                .OrderBy(target => target.EntityId)
                .Select(target => $"{target.EntityId}@{target.LevelId}:({target.PositionX},{target.PositionY})"
                    + $"->({target.FuturePositionX},{target.FuturePositionY})/moving={target.IsMoving}/"
                    + $"hpQ6={target.HitPointsQ6}"));
            var doors = string.Join(";", world.Objects.Values
                .Where(value => value.LevelId == player.LevelId && value.CathedralDoor is not null)
                .OrderBy(value => value.EntityId)
                .Select(value => $"{value.EntityId}@({value.PositionX},{value.PositionY})/"
                    + $"{value.CathedralDoor!.Status}/walk={world.IsWalkable(value.LevelId,
                        value.PositionX, value.PositionY)}"));
            var events = observedEvents is null ? "unavailable" : string.Join(";", observedEvents.TakeLast(8)
                .Select(value => $"{value.Kind}:{value.SourceEntityId}>{value.TargetEntityId}:"
                    + $"{value.Amount}/q6={value.AmountQ6}"));
            return $"item={item.EntityId}@({item.PositionX},{item.PositionY})/"
                + $"nativeIndex={item.State.ItemIndex}/stableId={item.State.StableItemId}/"
                + $"symbol={itemDefinition?.StableSymbol ?? "unknown"}/name={item.State.BaseName}/"
                + $"class={item.State.ItemClass}/equip={item.State.EquipLocation}/"
                + $"requirements={item.State.MinimumStrength},{item.State.MinimumMagic},{item.State.MinimumDexterity}/"
                + $"statFlag={item.State.StatFlag},"
                + $"hero=({player.PositionX},{player.PositionY})/life={player.Life}/"
                + $"action={player.CurrentAction?.Kind.ToString() ?? "idle"}/tick={runtime.LastAdvancedTick},"
                + $"lastAttempt={lastAttempt},actors=[{actors}],doors=[{doors}],events=[{events}]";
        }

        void Advance(ulong tick)
        {
            if (advanceTo is null)
                runtime.AdvanceTo(tick);
            else
                advanceTo(tick);
        }

        void SettleAction()
        {
            for (var completion = 0; player.CurrentAction is { } pending; completion++) {
                Assert.True(completion < 3,
                    $"A native item approach did not settle after its bounded completion: {DescribeState()}");
                Assert.True(pending.EndTick > runtime.LastAdvancedTick,
                    $"A completed native item approach remained attached to the player: {DescribeState()}");
                Advance(pending.EndTick);
            }
        }

        for (var actionCount = 0; actionCount < maximumActions
            && DeterministicGridMetrics.ChebyshevDistance(
                player.PositionX, player.PositionY, item.PositionX, item.PositionY) > 1; actionCount++) {
            Assert.True(player.Life > 0, $"The Rogue died approaching its earned item: {DescribeState()}");
            var step = DeterministicGridNavigation.FindStepToGoal(
                player.PositionX,
                player.PositionY,
                maximumDepth: 256,
                IsOpenAdjacentCell,
                (x, y) => world.IsWalkable(item.LevelId, x, y),
                (x, y) => world.IsOccupied(item.LevelId, x, y, [player], player.EntityId));

            if (step is { } nextCell) {
                var before = (player.PositionX, player.PositionY);
                var tick = checked(runtime.LastAdvancedTick + 1);
                var result = simulation.Execute(player,
                    new DiabloMoveToIntent(nextCell.X, nextCell.Y), tick, [player]);
                Assert.True(result.Succeeded,
                    $"The Rogue could not approach its earned item: {result.Failure}; {DescribeState()}");
                var action = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
                Assert.Equal(DiabloPlayerActionKind.Move, action.Kind);
                lastAttempt = $"MoveTo:{before}->{nextCell}/ends={action.EndTick}";
                Advance(action.EndTick);
                SettleAction();
                _ = runtime.DrainEvents(sessionId);
                afterSettledAction?.Invoke();

                if ((player.PositionX, player.PositionY) == before) {
                    consecutiveNoProgress++;
                    Assert.True(consecutiveNoProgress < maximumConsecutiveNoProgress,
                        $"Repeated settled moves lost their destination to current occupancy: {DescribeState()}");
                    continue;
                }

                consecutiveNoProgress = 0;
                continue;
            }

            var closedDoors = world.Objects.Values
                .Where(value => value.LevelId == player.LevelId
                    && value.CathedralDoor?.Status == AuthoritativeCathedralDoorStatus.Closed)
                .OrderBy(value => DeterministicGridMetrics.ChebyshevDistance(
                    player.PositionX, player.PositionY, value.PositionX, value.PositionY))
                .ThenBy(value => value.PositionY)
                .ThenBy(value => value.PositionX)
                .ToArray();
            var adjacentDoor = closedDoors.FirstOrDefault(door =>
                DeterministicGridMetrics.ChebyshevDistance(
                    player.PositionX, player.PositionY, door.PositionX, door.PositionY) <= 1);
            if (adjacentDoor is not null) {
                var operationTick = checked(runtime.LastAdvancedTick + 1);
                var operation = simulation.Execute(player,
                    new DiabloOperateObjectIntent(adjacentDoor.EntityId), operationTick, [player]);
                Assert.True(operation.Succeeded,
                    $"The Rogue could not open the native door on its item route: {operation.Failure}; "
                        + DescribeState());
                lastAttempt = $"OperateDoor:{adjacentDoor.EntityId}@{operationTick}";
                Advance(operationTick);
                SettleAction();
                Assert.NotEqual(AuthoritativeCathedralDoorStatus.Closed,
                    world.Objects[adjacentDoor.EntityId].CathedralDoor!.Status);
                consecutiveNoProgress = 0;
                _ = runtime.DrainEvents(sessionId);
                afterSettledAction?.Invoke();
                continue;
            }

            var doorRoute = closedDoors
                .Select(door => (Door: door, Step: DeterministicGridNavigation.FindStepToGoal(
                    player.PositionX,
                    player.PositionY,
                    maximumDepth: 256,
                    (x, y) => DeterministicGridMetrics.ChebyshevDistance(
                        x, y, door.PositionX, door.PositionY) == 1,
                    (x, y) => world.IsWalkable(player.LevelId, x, y),
                    (x, y) => world.IsOccupied(player.LevelId, x, y, [player], player.EntityId))))
                .FirstOrDefault(candidate => candidate.Step is not null);

            if (doorRoute.Door is not null && doorRoute.Step is { } doorStep) {
                var before = (player.PositionX, player.PositionY);
                var moveTick = checked(runtime.LastAdvancedTick + 1);
                var move = simulation.Execute(player,
                    new DiabloMoveToIntent(doorStep.X, doorStep.Y), moveTick, [player]);
                Assert.True(move.Succeeded,
                    $"The Rogue could not approach the native door on its item route: {move.Failure}; "
                        + DescribeState());
                var action = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
                lastAttempt = $"MoveToDoor:{before}->{doorStep}/ends={action.EndTick}";
                Advance(action.EndTick);
                SettleAction();
                if ((player.PositionX, player.PositionY) == before)
                    consecutiveNoProgress++;
                else
                    consecutiveNoProgress = 0;
                _ = runtime.DrainEvents(sessionId);
                afterSettledAction?.Invoke();
                Assert.True(consecutiveNoProgress < maximumConsecutiveNoProgress,
                    $"Repeated settled moves lost their destination on the route to a door: {DescribeState()}");
                continue;
            }

            // A live actor may occupy the only current route tile and move during the next tick.
            // Wait one real tick, then recompute from current world occupancy.
            lastAttempt = "WaitForCurrentOccupancyToChange";
            Advance(checked(runtime.LastAdvancedTick + 1));
            _ = runtime.DrainEvents(sessionId);
            afterSettledAction?.Invoke();
            consecutiveNoProgress++;
            Assert.True(consecutiveNoProgress < maximumConsecutiveNoProgress,
                $"No reachable item route emerged under current actor occupancy: {DescribeState()}");
        }

        Assert.True(DeterministicGridMetrics.ChebyshevDistance(
            player.PositionX, player.PositionY, item.PositionX, item.PositionY) <= 1,
            $"The Rogue did not reach a legal adjacent cell for the generated item pickup after bounded replanning: "
                + DescribeState());
    }

    private static bool MoveToRangedLineOfSight(
        DiabloSimulation simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime,
        DiabloWorldState world,
        AuthoritativePlayerState player,
        AuthoritativeCombatTarget target,
        ref int remainingActions,
        Action<ulong> advanceTo,
        int preferredMinimumFiringDistance = 2,
        bool allowImmediateAdjacentCounterattack = false)
    {
        const int maximumFiringDistance = 4;
        if (preferredMinimumFiringDistance is < 1 or > maximumFiringDistance)
            throw new ArgumentOutOfRangeException(nameof(preferredMinimumFiringDistance));
        var fallbackMinimumFiringDistance = preferredMinimumFiringDistance - 1;

        bool IsClearFiringTile(int x, int y, int minimumFiringDistance)
        {
            var distance = DeterministicGridMetrics.ChebyshevDistance(
                x, y, target.PositionX, target.PositionY);
            return (x != target.PositionX || y != target.PositionY)
                && distance >= minimumFiringDistance
                && distance <= maximumFiringDistance
                && world.HasMissileLineOfSight(player.LevelId,
                    x, y, target.PositionX, target.PositionY)
                && HasClearWeaponArrowPath(world, player.LevelId,
                    x, y, target.PositionX, target.PositionY, target.EntityId);
        }

        while (remainingActions > 0) {
            if (target.HitPoints <= 0 || target.LevelId != player.LevelId)
                return false;
            if (allowImmediateAdjacentCounterattack
                && DeterministicGridMetrics.ChebyshevDistance(
                    player.PositionX, player.PositionY, target.PositionX, target.PositionY) == 1
                && IsClearFiringTile(player.PositionX, player.PositionY, minimumFiringDistance: 1))
                return true;
            if (IsClearFiringTile(player.PositionX, player.PositionY,
                    minimumFiringDistance: preferredMinimumFiringDistance))
                return true;

            var firingStep = DeterministicGridNavigation.FindStepToGoal(
                player.PositionX,
                player.PositionY,
                maximumDepth: 256,
                (x, y) => IsClearFiringTile(x, y,
                    minimumFiringDistance: preferredMinimumFiringDistance),
                (x, y) => world.IsWalkable(player.LevelId, x, y),
                (x, y) => world.IsOccupied(player.LevelId, x, y, [player], player.EntityId));
            if (firingStep is null) {
                if (IsClearFiringTile(player.PositionX, player.PositionY,
                        minimumFiringDistance: fallbackMinimumFiringDistance))
                    return true;
                firingStep = DeterministicGridNavigation.FindStepToGoal(
                    player.PositionX,
                    player.PositionY,
                    maximumDepth: 256,
                    (x, y) => IsClearFiringTile(x, y,
                        minimumFiringDistance: fallbackMinimumFiringDistance),
                    (x, y) => world.IsWalkable(player.LevelId, x, y),
                    (x, y) => world.IsOccupied(player.LevelId, x, y, [player], player.EntityId));
            }
            if (firingStep is { } step) {
                remainingActions--;
                if (!MoveOneCell(simulation, runtime, world, player, step, advanceTo))
                    return false;
                continue;
            }

            var closedDoors = world.Objects.Values
                .Where(value => value.LevelId == player.LevelId
                    && value.CathedralDoor?.Status == AuthoritativeCathedralDoorStatus.Closed)
                .OrderBy(value => DeterministicGridMetrics.ChebyshevDistance(
                    player.PositionX, player.PositionY, value.PositionX, value.PositionY))
                .ThenBy(value => value.PositionY)
                .ThenBy(value => value.PositionX)
                .ToArray();
            if (closedDoors.Length == 0)
                return false;

            var adjacentDoor = closedDoors.FirstOrDefault(door =>
                DeterministicGridMetrics.ChebyshevDistance(
                    player.PositionX, player.PositionY, door.PositionX, door.PositionY) <= 1);
            if (adjacentDoor is not null) {
                var operationTick = checked(runtime.LastAdvancedTick + 1);
                var operation = simulation.Execute(player,
                    new DiabloOperateObjectIntent(adjacentDoor.EntityId), operationTick, [player]);
                if (!operation.Succeeded)
                    return false;
                remainingActions--;
                advanceTo(operationTick);
                continue;
            }

            var doorStep = DeterministicGridNavigation.FindStepToGoal(
                player.PositionX,
                player.PositionY,
                maximumDepth: 256,
                (x, y) => closedDoors.Any(door =>
                    DeterministicGridMetrics.ChebyshevDistance(x, y, door.PositionX, door.PositionY) <= 1
                    && (x != door.PositionX || y != door.PositionY)),
                (x, y) => world.IsWalkable(player.LevelId, x, y),
                (x, y) => world.IsOccupied(player.LevelId, x, y, [player], player.EntityId));
            if (doorStep is not { } reachableDoorStep)
                return false;
            remainingActions--;
            if (!MoveOneCell(simulation, runtime, world, player, reachableDoorStep, advanceTo))
                return false;
        }

        return false;
    }

    private static bool HasClearWeaponArrowPath(
        DiabloWorldState world,
        uint levelId,
        int startX,
        int startY,
        int targetX,
        int targetY,
        uint targetEntityId)
    {
        var x = startX;
        var y = startY;
        var maximumSteps = Math.Max(Math.Abs(targetX - startX), Math.Abs(targetY - startY));
        for (var step = 0; step < maximumSteps; step++) {
            var nextX = x + Math.Sign((long)targetX - x);
            var nextY = y + Math.Sign((long)targetY - y);
            if (world.IsMissileBlocked(levelId, nextX, nextY)
                || !world.HasMissileLineOfSight(levelId, x, y, nextX, nextY))
                return false;
            if (world.CombatTargets.Values.Any(candidate => candidate.EntityId != targetEntityId
                    && candidate.HitPointsQ6 > 0
                    && (candidate.LevelId == 0 || candidate.LevelId == levelId)
                    && ((candidate.PositionX == nextX && candidate.PositionY == nextY)
                        || (candidate.IsMoving && candidate.IsPetrified
                            && candidate.FuturePositionX == nextX
                            && candidate.FuturePositionY == nextY))))
                return false;
            x = nextX;
            y = nextY;
        }
        return true;
    }

    private static bool MoveOneCell(
        DiabloSimulation simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> runtime,
        DiabloWorldState world,
        AuthoritativePlayerState player,
        (int X, int Y) destination,
        Action<ulong> advanceTo)
    {
        if (!world.IsWalkable(player.LevelId, destination.X, destination.Y)
            || world.IsOccupied(player.LevelId, destination.X, destination.Y, [player], player.EntityId))
            return false;

        var before = (player.PositionX, player.PositionY);
        var moveTick = checked(runtime.LastAdvancedTick + 1);
        var move = simulation.Execute(player,
            new DiabloMoveToIntent(destination.X, destination.Y), moveTick, [player]);
        if (!move.Succeeded)
            return false;

        advanceTo(moveTick);
        for (var completion = 0; player.CurrentAction is { } pending; completion++) {
            if (completion >= 3 || pending.EndTick <= runtime.LastAdvancedTick)
                return false;
            advanceTo(pending.EndTick);
        }

        return (player.PositionX, player.PositionY) != before;
    }

    private static (int X, int Y) FindEmptyAdjacentCell(
        DiabloWorldState world,
        AuthoritativePlayerState player)
    {
        var offsets = new[] {
            (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0),
            (X: 1, Y: -1), (X: 1, Y: 1), (X: -1, Y: 1), (X: -1, Y: -1),
        };
        return offsets.Select(offset => (X: player.PositionX + offset.X, Y: player.PositionY + offset.Y))
            .First(position => world.IsWalkable(player.LevelId, position.X, position.Y)
                && !world.IsOccupied(player.LevelId, position.X, position.Y,
                    [player], player.EntityId)
                && !world.WorldItems.Values.Any(item => item.LevelId == player.LevelId
                    && item.PositionX == position.X && item.PositionY == position.Y));
    }

    private static string DifferentFingerprint(string value)
        => value == new string('A', 64) ? new string('B', 64) : new string('A', 64);

    private static AuthoritativeCathedralThemeRoomState GenerateNativePopulationState(
        DiabloContentActivation activation,
        NativeFixture fixture)
    {
        var rogue = activation.PlayerClasses.Definitions.Single(definition => definition.Symbol == "rogue");
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-cathedral-population-profile", rogue.ClassId,
            goldOverride: null, lifeOverride: null, manaOverride: null,
            startingLevelId: 1, startingPositionX: 57, startingPositionY: 40);
        var hero = new AuthoritativeCathedralFreshLevelHeroContext(
            template.BaseLifeMaximum,
            template.BaseManaMaximum,
            8,
            activation.PlayerAnimations.GetRequired(rogue.ClassId).IdleFrames,
            new(57, 40),
            LevelChanging: true,
            LevelLoading: true);
        var depth = fixture.Profile.Levels.Single(level => level.Depth == 1);
        var request = AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
            fixture.PopulationMetadata,
            depth: 1,
            dungeonSeed: depth.DungeonSeed,
            storySeed: 0x13579BDF,
            AuthoritativeCathedralEntry.Main,
            hero,
            depth.LevelSymbol);
        return AuthoritativeCathedralFreshLevelGenerator.Generate(request).Population.State;
    }

    private static void AssertNativeObjectProjection(
        DiabloWorldState world,
        AuthoritativeCathedralThemeRoomState state,
        IReadOnlyList<AuthoritativeWorldObject> projections,
        IReadOnlyList<int> activeSlots,
        DiabloCathedralDoorCheckpoint expectedCurrentDoors)
    {
        const uint objectEntityIdBase = 0x09002000;
        var projectionsBySlot = projections.Where(value => value.CathedralDoor is null)
            .ToDictionary(value => checked((int)(value.EntityId - objectEntityIdBase)));
        var doorsByPosition = projections.Where(value => value.CathedralDoor is not null)
            .ToDictionary(value => (value.PositionX, value.PositionY));
        var expectedDoorsByEntityId = expectedCurrentDoors.DoorLevels.SelectMany(level => level.Doors)
            .ToDictionary(value => value.EntityId);
        var entitiesBySlot = new Dictionary<int, AuthoritativeWorldObject>();
        var expectedProjectionOrder = new List<uint>(activeSlots.Count);

        foreach (var slot in activeSlots) {
            var raw = state.ObjectSlots[slot];
            var projection = raw.DoorFlag
                ? doorsByPosition[(raw.Position.X, raw.Position.Y)]
                : projectionsBySlot[slot];
            Assert.Equal(raw.Type, checked((int)projection.NativeObjectId));
            Assert.Equal(raw.Position.X, projection.PositionX);
            Assert.Equal(raw.Position.Y, projection.PositionY);
            Assert.Equal(raw.DoorFlag && raw.BreakFlag != 0, projection.Breakable);
            Assert.Equal(raw.TrapFlag, projection.TrapArmed);
            Assert.False(projection.Activated);
            Assert.False(projection.Destroyed);
            expectedProjectionOrder.Add(projection.EntityId);
            entitiesBySlot.Add(slot, projection);

            if (raw.DoorFlag) {
                Assert.NotNull(projection.CathedralDoor);
                Assert.Equal((AuthoritativeCathedralDoorKind)raw.Type, projection.CathedralDoor!.Kind);
                var currentDoor = expectedDoorsByEntityId[projection.EntityId];
                Assert.Equal(currentDoor.EntityId, projection.EntityId);
                Assert.Equal(currentDoor.LevelId, projection.LevelId);
                Assert.Equal((AuthoritativeCathedralDoorStatus)currentDoor.DoorState,
                    projection.CathedralDoor.Status);
                Assert.Equal(currentDoor.MissilePassable, projection.MissilePassable);
                Assert.Equal(currentDoor.AnimationFrame, projection.AnimationFrame);
            } else {
                Assert.Null(projection.CathedralDoor);
                Assert.Equal(raw.MissilePassable, projection.MissilePassable);
                Assert.Equal(checked((int)raw.AnimFrame), projection.AnimationFrame);
            }
        }

        Assert.Equal(expectedProjectionOrder.Count, projections.Count);
        Assert.Equal(expectedProjectionOrder.Order(), projections.Select(value => value.EntityId).Order());

        var footprintCount = 0;
        for (var index = 0; index < state.ObjectRefs.Count; index++) {
            var signedReference = state.ObjectRefs[index];
            if (signedReference == 0)
                continue;
            footprintCount++;
            var slot = checked(Math.Abs(signedReference) - 1);
            Assert.True(entitiesBySlot.TryGetValue(slot, out var projection),
                $"Raw object footprint {index} refers to inactive slot {slot}.");
            var x = index % AuthoritativeCathedralThemeRoomState.Width;
            var y = index / AuthoritativeCathedralThemeRoomState.Width;
            var raw = state.ObjectSlots[slot];
            if (raw.SolidFlag)
                Assert.True(world.IsOccupied(1, x, y, []));
            var missilePassable = raw.DoorFlag
                ? expectedDoorsByEntityId[projection!.EntityId].MissilePassable
                : raw.MissilePassable;
            if (missilePassable) {
                Assert.False(world.TryGetMissileBlockingObject(1, x, y, out _));
            } else {
                Assert.True(world.TryGetMissileBlockingObject(1, x, y, out var blocker));
                Assert.Equal(projection!.EntityId, blocker!.EntityId);
            }
        }
        Assert.True(footprintCount > 0);

        var authoritativeLevel = Assert.Single(world.World!.Levels, level => level.LevelId == 1);
        var presentationLevel = Assert.Single(world.CapturePresentationLevelGeometry(),
            level => level.LevelId == 1);
        var expectedStaticObjectCells = authoritativeLevel.StaticObjectBlockedCells.ToHashSet();
        for (var index = 0; index < state.ObjectRefs.Count; index++) {
            var signedReference = state.ObjectRefs[index];
            if (signedReference != 0 && state.ObjectSlots[Math.Abs(signedReference) - 1].SolidFlag)
                expectedStaticObjectCells.Add(index);
        }
        Assert.Equal(expectedStaticObjectCells.Order(), presentationLevel.StaticObjectBlockedCells);

        const int reportedX = 75;
        const int reportedY = 58;
        var reportedIndex = reportedY * AuthoritativeCathedralThemeRoomState.Width + reportedX;
        var reportedReference = state.ObjectRefs[reportedIndex];
        Assert.NotEqual(0, reportedReference);
        var reportedSlot = Math.Abs(reportedReference) - 1;
        Assert.Contains(reportedSlot, activeSlots);
        var reportedRawObject = state.ObjectSlots[reportedSlot];
        Assert.Equal(48, reportedRawObject.Type);
        Assert.Equal((75, 59), (reportedRawObject.Position.X, reportedRawObject.Position.Y));
        Assert.True(reportedRawObject.SolidFlag);
        var reportedObject = Assert.Single(projections,
            value => value.EntityId == 0x09002000U + (uint)reportedSlot);
        Assert.Equal(150994993U, reportedObject.ObjectId);
        Assert.True(world.IsOccupied(1, reportedX, reportedY, []));
        Assert.Contains(reportedIndex, presentationLevel.StaticObjectBlockedCells);
        Assert.DoesNotContain(reportedIndex, authoritativeLevel.StaticObjectBlockedCells);

        var nonSolidReferenceIndex = Enumerable.Range(0, state.ObjectRefs.Count).First(index => {
            var signedReference = state.ObjectRefs[index];
            return signedReference != 0
                && !state.ObjectSlots[Math.Abs(signedReference) - 1].SolidFlag
                && !authoritativeLevel.StaticObjectBlockedCells.Contains(index);
        });
        Assert.DoesNotContain(nonSolidReferenceIndex, presentationLevel.StaticObjectBlockedCells);
    }

    private sealed class NativeFixture : IDisposable
    {
        private readonly string output = Path.Combine(Path.GetTempPath(),
            "godevilution-native-population-" + Guid.NewGuid().ToString("N"));

        public ComposedContent Content { get; }
        public AuthoritativeCathedralPopulationMetadata PopulationMetadata { get; }
        public CathedralDepthPlaytestProfile Profile { get; }
        public AuthoritativeItemCatalog BaselineItems { get; private set; } = null!;

        public NativeFixture(string metadataDirectory, string runtimeTablePath, string footprintTablePath)
        {
            PopulationMetadata = CathedralPopulationMetadataCompiler.LoadDirectory(metadataDirectory);
            var metadataPack = PopulationMetadata.ToContentPack();
            var pieces = metadataPack.Tables.Single(table =>
                table.SourcePath == AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath).ToCanonicalTsv();
            var properties = metadataPack.Tables.Single(table =>
                table.SourcePath == AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath).ToCanonicalTsv();
            var runtimeTable = TsvTable.Parse(AuthoritativeCathedralMonsterRuntimeCatalog.TableName,
                File.ReadAllText(runtimeTablePath));
            var runtime = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(runtimeTable);
            var seeds = new uint[] { 2588, 2588, 1, uint.MaxValue };
            Profile = CathedralDepthPlaytestCompiler.Compile(new CathedralDepthPlaytestCompilerOptions(
                LoadBasePack(), output, pieces, properties, seeds) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                });
            var generated = AddNativeDoorCatalog(ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, "content")));
            var runtimePack = runtime.CreateContentPack();
            var baselineContent = ComposedContent.Compose(new ContentManifest(
                generated.Id, generated.Version, [generated]));
            var baselineActivation = DiabloModule.Instance.CathedralDoorContentBootstrap
                .ActivateDoors(baselineContent);
            BaselineItems = baselineActivation.Simulation.Items!;
            var footprintTable = TsvTable.Parse("native-rogue-l1-inventory.tsv",
                File.ReadAllText(footprintTablePath));
            var footprints = AuthoritativeCathedralNativeItemSidecars.LoadFootprints(footprintTable);
            var semanticsTable = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
                PopulationMetadata.Items.Catalog,
                baselineActivation.Simulation.Items!,
                baselineActivation.Simulation.Spells,
                footprints);
            var itemSidecars = AuthoritativeCathedralNativeItemSidecars.Load(footprintTable, semanticsTable);
            var storesTable = generated.Tables.Single(table => table.SourcePath == "stores.tsv");
            var preparedStores = AuthoritativeCathedralNativePopulationBuilder.PrepareNativeProfileStoreTable(
                storesTable, baselineActivation.Simulation.Items!, itemSidecars);
            generated = new ContentPack(generated.Id, generated.Version, generated.Tables.Select(table =>
                table.SourcePath == storesTable.SourcePath ? preparedStores : table).ToArray(),
                generated.Dependencies, generated.Overlays, generated.Schema,
                generated.DependencyConstraints, generated.Assets);
            var runtimeTables = runtimePack.Tables.Append(footprintTable).Append(semanticsTable).ToArray();
            Assert.Equal(3, runtimeTables.Length);
            var populationSidecar = new ContentPack(metadataPack.Id, metadataPack.Version,
                metadataPack.Tables.Select(table => table with {
                    SourcePath = "native_cathedral_population/" + table.SourcePath,
                }).ToArray());
            var runtimeSidecar = new ContentPack(runtimePack.Id, runtimePack.Version,
                runtimeTables.Select(table => table with {
                    SourcePath = "native_cathedral_runtime/" + table.SourcePath,
                }).ToArray());
            Content = ComposedContent.Compose(new ContentManifest(generated.Id, generated.Version,
                [generated, populationSidecar, runtimeSidecar]));
            Assert.Equal(seeds, Profile.DepthSeeds);
        }

        private static ContentPack AddNativeDoorCatalog(ContentPack pack)
        {
            const uint leftId = 1;
            const uint rightId = 2;
            const string leftSymbol = "l1_left_door";
            const string rightSymbol = "l1_right_door";
            var ids = pack.Tables.Single(table => table.SourcePath == "content_ids.tsv");
            var rightRows = ids.Rows.Where(row => row.Required("kind") == "object"
                && (row.Required("symbol") == rightSymbol || row.Required("numeric_id") == rightId.ToString())).ToArray();
            if (rightRows.Length != 0 && (rightRows.Length != 1
                    || rightRows[0].Required("symbol") != rightSymbol
                    || rightRows[0].RequiredUInt32("numeric_id") != rightId))
                throw new InvalidDataException("The native right-door identity conflicts with the base stable-ID catalog.");
            var updatedIds = rightRows.Length == 1 ? ids : TsvTable.Parse(ids.SourcePath,
                ids.ToCanonicalTsv().TrimEnd('\r', '\n') + $"\nobject\t{rightSymbol}\t{rightId}\n");

            var objectTable = pack.Tables.Single(table => table.SourcePath == "objects.tsv");
            if (objectTable.Rows.Any(row => row.RequiredUInt32("entity_id") == 2003))
                throw new InvalidDataException("Native right-door template entity 2003 is already in use.");
            var leftTemplate = objectTable.Rows.Single(row => row.RequiredUInt32("entity_id") == 2001);
            var rightTemplate = leftTemplate.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            rightTemplate["entity_id"] = "2003";
            rightTemplate["object_id"] = rightId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            rightTemplate["object_symbol"] = rightSymbol;
            rightTemplate["level_id"] = "0";
            rightTemplate["level_symbol"] = string.Empty;
            rightTemplate["position_x"] = "0";
            rightTemplate["position_y"] = "0";
            rightTemplate["template_only"] = "1";
            rightTemplate["native_object_id"] = "0";
            var updatedObjects = TsvTable.Parse(objectTable.SourcePath,
                objectTable.ToCanonicalTsv().TrimEnd('\r', '\n') + "\n"
                + string.Join('\t', objectTable.Columns.Select(column => rightTemplate[column])) + "\n");

            var doorRows = new List<string> {
                "level_id\tlevel_symbol\tleft_object_id\tleft_object_symbol\tright_object_id\tright_object_symbol",
            };
            foreach (var (levelId, symbol) in new[] {
                (1U, "cathedral"), (2U, "cathedral_depth_2"),
                (3U, "cathedral_depth_3"), (4U, "cathedral_depth_4"),
            })
                doorRows.Add($"{levelId}\t{symbol}\t{leftId}\t{leftSymbol}\t{rightId}\t{rightSymbol}");
            var doors = TsvTable.Parse(AuthoritativeCathedralDoorCatalog.TableName,
                string.Join('\n', doorRows) + "\n");
            var tables = pack.Tables.Select(table => table.SourcePath == ids.SourcePath ? updatedIds
                : table.SourcePath == objectTable.SourcePath ? updatedObjects : table).Append(doors).ToArray();
            return new ContentPack(pack.Id, pack.Version, tables, pack.Dependencies, pack.Overlays,
                pack.Schema, pack.DependencyConstraints, pack.Assets);
        }

        private static ContentPack LoadBasePack()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return ContentPackLoader.LoadManifestDirectory(Path.Combine(directory.FullName, "server", "content", "base"));
            throw new DirectoryNotFoundException("Could not locate the repository root.");
        }

        public void Dispose()
        {
            var target = Path.GetFullPath(output);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!target.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(target).StartsWith("godevilution-native-population-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe native population fixture cleanup target.");
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
        }
    }
}
