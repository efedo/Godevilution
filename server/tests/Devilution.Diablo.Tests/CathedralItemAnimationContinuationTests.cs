using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Exercises native item animation through live ticks and a strict cohort codec restore. */
public sealed class CathedralItemAnimationContinuationTests
{
    private const string MonsterFingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void LootCreatedInsideOrderedTickAdvancesOnceAndStopsOnItsLastFrame()
    {
        var profile = CreateProfile(CreateCheckpoint(1));
        var target = Target(9, 7, 40, 40, 1, experienceReward: 1);
        var options = Options(profile, [target]);
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(Template());
        runtime.Write("hero", hero => {
            hero.EntityId = 1;
            hero.LevelId = 1;
            hero.PositionX = 40;
            hero.PositionY = 39;
        });

        var result = Assert.Single(runtime.ExecuteTick<DiabloCombatIntentResult>(
            1, [AttackCommand(simulation, 1, 9)]));

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var worldItem = Assert.Single(simulation.WorldState.WorldItems.Values);
        var runtimeLevel = simulation.WorldState.CaptureCathedralItems()!.Levels.Single(level => level.LevelId == 1);
        var slot = Array.IndexOf(runtimeLevel.SlotEntityBindings.Select(binding => binding.EntityId).ToArray(), worldItem.EntityId);
        Assert.InRange(slot, 0, AuthoritativeCathedralItemRuntime.PoolCapacity - 1);
        Assert.True(runtimeLevel.Items[slot].AnimFlag);
        Assert.Equal((0, 1, false), (
            runtimeLevel.Items[slot].Animation.TickCounterOfCurrentFrame,
            runtimeLevel.Items[slot].Animation.CurrentFrame,
            runtimeLevel.Items[slot].Animation.IsPetrified));
        Assert.Equal(1U, runtime.Read("hero", hero => hero.CharacterLevel));

        runtime.AdvanceTo(2);
        var afterSecondTick = simulation.WorldState.CaptureCathedralItems()!.Levels.Single(level => level.LevelId == 1);
        Assert.Equal(2, afterSecondTick.Items[slot].Animation.CurrentFrame);
        runtime.AdvanceTo(2);
        Assert.Equal(afterSecondTick.Items[slot], simulation.WorldState.CaptureCathedralItems()!.Levels.Single(level => level.LevelId == 1).Items[slot]);

        runtime.AdvanceTo(3);
        var completed = simulation.WorldState.CaptureCathedralItems()!.Levels.Single(level => level.LevelId == 1).Items[slot];
        Assert.Equal((3, false, 1), (completed.Animation.CurrentFrame, completed.AnimFlag, completed.SelectionRegion));
        runtime.AdvanceTo(4);
        Assert.Equal(completed, simulation.WorldState.CaptureCathedralItems()!.Levels.Single(level => level.LevelId == 1).Items[slot]);
    }

    [Fact]
    public void CodecRestoredMidAnimationContinuesThroughPickupEquipAndASecondLevelKill()
    {
        var profile = CreateProfile(CreateCheckpoint(2, animatedGroundItem: true));
        var first = Target(9, 7, 40, 40, 1, experienceReward: 1);
        var second = Target(10, 8, 40, 40, 2);
        var options = Options(profile, [first, second]);
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(Template());
        runtime.Write("hero", hero => {
            hero.EntityId = 1;
            hero.LevelId = 1;
            hero.PositionX = 40;
            hero.PositionY = 39;
            hero.Experience = 999;
        });

        var killed = Assert.Single(runtime.ExecuteTick<DiabloCombatIntentResult>(
            1, [AttackCommand(simulation, 1, first.EntityId)]));
        Assert.True(killed.Succeeded, killed.Failure?.ToString());
        var dropped = Assert.Single(simulation.WorldState.WorldItems.Values, item => item.LevelId == 1 && item.EntityId != 101);
        var codec = new DiabloCohortCodec();
        var serialized = codec.Serialize(simulation.CaptureCohortState(runtime));
        var decoded = codec.Deserialize(serialized);
        Assert.Equal(4, decoded.FormatVersion);

        var candidate = DiabloSimulation.CreateCohortCandidate(options, decoded);
        var restoredRuntime = candidate.CreateRuntime();
        var restoredSimulation = candidate.Simulation;
        Assert.Equal(serialized, codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(1, GroundItem(simulation, 1, dropped.EntityId).Animation.CurrentFrame);

        runtime.AdvanceTo(2);
        restoredRuntime.AdvanceTo(2);
        Assert.Equal(2, GroundItem(simulation, 1, dropped.EntityId).Animation.CurrentFrame);
        Assert.Equal(2, GroundItem(restoredSimulation, 1, dropped.EntityId).Animation.CurrentFrame);
        Assert.Equal(2, GroundItem(simulation, 1, 101).Animation.CurrentFrame);

        runtime.Write("hero", hero => {
            hero.PositionX = dropped.PositionX - 1;
            hero.PositionY = dropped.PositionY;
        });
        restoredRuntime.Write("hero", hero => {
            hero.PositionX = dropped.PositionX - 1;
            hero.PositionY = dropped.PositionY;
        });

        DiabloWorldIntentResult sourcePickup = default!;
        runtime.Write("hero", hero => sourcePickup = simulation.Execute(
            hero, new DiabloPickupWorldItemIntent(dropped.EntityId), 3, [hero]));
        DiabloWorldIntentResult restoredPickup = default!;
        restoredRuntime.Write("hero", hero => restoredPickup = restoredSimulation.Execute(
            hero, new DiabloPickupWorldItemIntent(dropped.EntityId), 3, [hero]));
        Assert.Equal(sourcePickup.Succeeded, restoredPickup.Succeeded);
        Assert.True(sourcePickup.Succeeded, sourcePickup.Failure?.ToString());
        var sourcePlayer = runtime.Read("hero", hero => hero);
        var restoredPlayer = restoredRuntime.Read("hero", hero => hero);
        Assert.Equal(1002U, Assert.Single(sourcePlayer.Inventory).State.StableItemId);
        Assert.Equal(1002U, Assert.Single(restoredPlayer.Inventory).State.StableItemId);

        var itemReference = new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 1002);
        DiabloIntentResult sourceEquip = default!;
        runtime.Write("hero", hero => sourceEquip = simulation.Execute(
            hero, new DiabloAutoEquipItemIntent(itemReference), 3));
        DiabloIntentResult restoredEquip = default!;
        restoredRuntime.Write("hero", hero => restoredEquip = restoredSimulation.Execute(
            hero, new DiabloAutoEquipItemIntent(itemReference), 3));
        Assert.Equal(sourceEquip, restoredEquip);
        Assert.True(sourceEquip.Succeeded, sourceEquip.Failure?.ToString());
        sourcePlayer = runtime.Read("hero", hero => hero);
        restoredPlayer = restoredRuntime.Read("hero", hero => hero);
        Assert.Equal(1002U, Assert.Single(sourcePlayer.Equipment).State.StableItemId);
        Assert.Equal(1002U, Assert.Single(restoredPlayer.Equipment).State.StableItemId);

        runtime.Write("hero", hero => {
            hero.LevelId = 2;
            hero.PositionX = 40;
            hero.PositionY = 39;
        });
        restoredRuntime.Write("hero", hero => {
            hero.LevelId = 2;
            hero.PositionX = 40;
            hero.PositionY = 39;
        });
        runtime.AdvanceTo(3);
        restoredRuntime.AdvanceTo(3);
        Assert.Equal(2, GroundItem(simulation, 1, 101).Animation.CurrentFrame);
        Assert.Equal(2, GroundItem(restoredSimulation, 1, 101).Animation.CurrentFrame);

        sourcePlayer = runtime.Read("hero", hero => hero);
        var postPickupEquipBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var postPickupEquipCandidate = DiabloSimulation.CreateCohortCandidate(
            options, codec.Deserialize(postPickupEquipBytes));
        var postPickupEquipRuntime = postPickupEquipCandidate.CreateRuntime();
        var postPickupEquipSimulation = postPickupEquipCandidate.Simulation;
        Assert.Equal(postPickupEquipBytes,
            codec.Serialize(postPickupEquipSimulation.CaptureCohortState(postPickupEquipRuntime)));
        var coldRestoredPlayer = postPickupEquipRuntime.Read("hero", hero => hero);
        Assert.Equal(2U, coldRestoredPlayer.LevelId);
        Assert.Empty(sourcePlayer.Inventory);
        Assert.Empty(coldRestoredPlayer.Inventory);
        Assert.Equal(Assert.Single(sourcePlayer.Equipment), Assert.Single(coldRestoredPlayer.Equipment));

        var secondKill = Assert.Single(runtime.ExecuteTick<DiabloCombatIntentResult>(
            4, [AttackCommand(simulation, 4, second.EntityId)]));
        var restoredSecondKill = Assert.Single(postPickupEquipRuntime.ExecuteTick<DiabloCombatIntentResult>(
            4, [AttackCommand(postPickupEquipSimulation, 4, second.EntityId)]));
        Assert.Equal(secondKill.Succeeded, restoredSecondKill.Succeeded);
        Assert.Equal(secondKill.Failure, restoredSecondKill.Failure);
        Assert.Equal(secondKill.Events, restoredSecondKill.Events);
        Assert.True(secondKill.Succeeded, secondKill.Failure?.ToString());
        var secondDrop = Assert.Single(simulation.WorldState.WorldItems.Values, item => item.LevelId == 2 && item.EntityId != 102);
        Assert.Equal(1, GroundItem(simulation, 2, secondDrop.EntityId).Animation.CurrentFrame);
        Assert.Equal(1, GroundItem(postPickupEquipSimulation, 2, secondDrop.EntityId).Animation.CurrentFrame);

        sourcePlayer = runtime.Read("hero", hero => hero);
        restoredPlayer = postPickupEquipRuntime.Read("hero", hero => hero);
        var sourceState = simulation.CaptureCohortState(runtime);
        var restoredState = postPickupEquipSimulation.CaptureCohortState(postPickupEquipRuntime);
        Assert.Equal(codec.Serialize(sourceState), codec.Serialize(restoredState));
        Assert.Equal(1000U, sourcePlayer.Experience);
        Assert.Equal(2U, sourcePlayer.CharacterLevel);
        Assert.Equal(Assert.Single(sourcePlayer.Equipment).State,
            Assert.Single(restoredPlayer.Equipment).State);
    }

    [Fact]
    public void PetrifiedGroundItemsAdvanceForDeadParticipantsButDormantLevelsFreeze()
    {
        var profile = CreateProfile(CreateCheckpoint(2, animatedGroundItem: true));
        var simulation = DiabloSimulationFactory.Instance.Create(Options(profile, []));
        var living = Template().CreatePlayer();
        living.EntityId = 1;
        living.LevelId = 1;
        living.PositionX = 20;
        living.PositionY = 20;
        var dead = Template().CreatePlayer();
        dead.EntityId = 2;
        dead.LevelId = 2;
        dead.Life = 0;

        simulation.AdvanceTick(1, [living, dead]);
        var first = simulation.WorldState.CaptureCathedralItems()!;
        Assert.Equal(1, first.Levels.Single(level => level.LevelId == 1).Items[0].Animation.CurrentFrame);
        var deadLevelItem = first.Levels.Single(level => level.LevelId == 2).Items[0];
        Assert.Equal(1, deadLevelItem.Animation.CurrentFrame);
        Assert.True(deadLevelItem.Animation.IsPetrified);

        simulation.AdvanceTick(2, [living]);
        var second = simulation.WorldState.CaptureCathedralItems()!;
        Assert.Equal(2, second.Levels.Single(level => level.LevelId == 1).Items[0].Animation.CurrentFrame);
        Assert.Equal(deadLevelItem, second.Levels.Single(level => level.LevelId == 2).Items[0]);
    }

    [Fact]
    public void DeathLootPlacementReservesMovingPlayerAndVictimCurrentAndFutureTiles()
    {
        var profile = CreateProfile(CreateCheckpoint(1));
        var victim = Target(9, 7, 40, 40, 1, moving: true, futurePositionX: 41, futurePositionY: 40);
        var simulation = DiabloSimulationFactory.Instance.Create(Options(profile, [victim]));
        var runtime = simulation.CreateRuntime(Template());

        AuthoritativeCathedralItemDeathTransaction? prepared = null;
        runtime.Write("hero", hero => {
            hero.EntityId = 1;
            hero.LevelId = 1;
            hero.PositionX = 39;
            hero.PositionY = 39;
            hero.CurrentAction = new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Move, 1, 2, 2, 5) { DirectionX = -1 };
            hero.CombatMode = DiabloPlayerCombatMode.WalkSideways;
            hero.CombatModeEndTick = 5;
            Assert.True(hero.IsMoving);
            Assert.Equal((38, 39), (hero.FuturePositionX, hero.FuturePositionY));
            Assert.True(victim.IsMoving);

            var death = simulation.WorldState.PrepareCathedralDeathLoot(victim, hero, [hero]);
            Assert.NotNull(death);
            prepared = death;
        });

        Assert.NotNull(prepared);
        var transaction = prepared!;
        Assert.True(transaction.Loot.Dropped);
        Assert.True(transaction.Loot.PlacementSucceeded);
        var playerRefs = transaction.Loot.WorldState.PlayerRefs;
        var monsterRefs = transaction.Loot.WorldState.MonsterRefs;
        static int Cell(int x, int y) => y * AuthoritativeCathedralItemRuntime.Width + x;
        Assert.Equal(1, playerRefs[Cell(39, 39)]);
        Assert.Equal(-1, playerRefs[Cell(38, 39)]);
        var victimMarker = victim.NativeInitialization!.InitialState.Slot + 1;
        Assert.Equal(victimMarker, monsterRefs[Cell(40, 40)]);
        Assert.Equal(-victimMarker, monsterRefs[Cell(41, 40)]);
        Assert.DoesNotContain(
            (transaction.Loot.Placement.X, transaction.Loot.Placement.Y),
            new[] { (39, 39), (38, 39), (40, 40), (41, 40) });

        // The prepared placement retains the mover's future reservation even
        // though real death processing clears that reservation after preparation.
        victim.CancelMovement();
        Assert.False(victim.IsMoving);
        Assert.Equal(-victimMarker, transaction.Loot.WorldState.MonsterRefs[Cell(41, 40)]);
    }

    [Fact]
    public void MagicRockUsesItsTwoSelectionFrameLoopsAndRejectsShortMetadata()
    {
        var profile = CreateProfile(CreateMagicRockCheckpoint(20), magicRockFrameCount: 20);
        var simulation = DiabloSimulationFactory.Instance.Create(Options(profile, []));
        var hero = Template().CreatePlayer();
        hero.EntityId = 1;
        hero.LevelId = 1;
        simulation.AdvanceTick(1, [hero]);
        var items = simulation.WorldState.CaptureCathedralItems()!.Levels.Single().Items;
        Assert.Equal((0, 1, true), (items[0].Animation.CurrentFrame, items[0].SelectionRegion, items[0].Animation.IsPetrified));
        Assert.Equal((10, 2, true), (items[1].Animation.CurrentFrame, items[1].SelectionRegion, items[1].Animation.IsPetrified));
        Assert.True(items[0].AnimFlag);
        Assert.True(items[1].AnimFlag);

        var malformedProfile = CreateProfile(CreateMagicRockCheckpoint(19), magicRockFrameCount: 19);
        var malformed = DiabloSimulationFactory.Instance.Create(Options(malformedProfile, []));
        Assert.Throws<InvalidDataException>(() => malformed.AdvanceTick(1, [hero]));
        Assert.Equal(19, malformed.WorldState.CaptureCathedralItems()!.Levels.Single().Items[0].Animation.NumberOfFrames);
    }

    [Fact]
    public void LastAvailablePoolSlotCanBePickedUpAndReused()
    {
        var full = AuthoritativeCathedralItemRuntimeTestData.FullPoolCheckpoint();
        var checkpoint = full with { Levels = [full.Levels.Single() with { ActiveItemCount = 126 }] };
        var profile = CreateProfile(checkpoint);
        var first = Target(9, 7, 40, 40, 1);
        var second = Target(10, 8, 41, 40, 1);
        var options = Options(profile, [first, second]);
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(Template());
        runtime.Write("hero", hero => {
            hero.EntityId = 1;
            hero.LevelId = 1;
            hero.PositionX = 40;
            hero.PositionY = 39;
        });

        var firstKill = Assert.Single(runtime.ExecuteTick<DiabloCombatIntentResult>(
            1, [AttackCommand(simulation, 1, first.EntityId)]));
        Assert.True(firstKill.Succeeded, firstKill.Failure?.ToString());
        var firstDrop = Assert.Single(simulation.WorldState.WorldItems.Values);
        runtime.Write("hero", hero => {
            hero.PositionX = firstDrop.PositionX - 1;
            hero.PositionY = firstDrop.PositionY;
        });
        var firstLevel = simulation.WorldState.CaptureCathedralItems()!.Levels.Single();
        Assert.Equal(127, firstLevel.ActiveItemCount);
        var firstSlot = Array.FindIndex(firstLevel.SlotEntityBindings.ToArray(), binding => binding.EntityId == firstDrop.EntityId);
        Assert.Equal(126, firstSlot);

        DiabloWorldIntentResult pickup = default!;
        runtime.Write("hero", hero => pickup = simulation.Execute(
            hero, new DiabloPickupWorldItemIntent(firstDrop.EntityId), 2, [hero]));
        Assert.True(pickup.Succeeded, pickup.Failure?.ToString());
        Assert.Equal(126, simulation.WorldState.CaptureCathedralItems()!.Levels.Single().ActiveItemCount);
        runtime.Write("hero", hero => {
            hero.PositionX = 40;
            hero.PositionY = 39;
        });
        var secondKill = Assert.Single(runtime.ExecuteTick<DiabloCombatIntentResult>(
            3, [AttackCommand(simulation, 3, second.EntityId)]));
        Assert.True(secondKill.Succeeded, secondKill.Failure?.ToString());
        var secondDrop = Assert.Single(simulation.WorldState.WorldItems.Values);
        var finalLevel = simulation.WorldState.CaptureCathedralItems()!.Levels.Single();
        var secondSlot = Array.FindIndex(finalLevel.SlotEntityBindings.ToArray(), binding => binding.EntityId == secondDrop.EntityId);
        Assert.Equal(firstSlot, secondSlot);
        Assert.Equal(127, finalLevel.ActiveItemCount);
    }

    [Fact]
    public void FullPoolCursorPickupDropsHeldItemAtEmptyOriginAndColdRestoresBothIdentities()
    {
        const int cursorSlot = AuthoritativeCathedralItemRuntime.ActiveCapacity - 1;
        const uint initialEntityId = 150;
        var full = AuthoritativeCathedralItemRuntimeTestData.FullPoolCheckpoint();
        var fullLevel = full.Levels.Single();
        var rawCursorItem = AuthoritativeCathedralItemRuntimeTestData.Item(1, 30, 30) with {
            Seed = 7311,
            CreateInfo = 0x8123,
            Identified = true,
            BaseName = "Cursor-origin sword",
            IdentifiedName = "Cursor-origin sword",
        };
        var items = fullLevel.Items.ToArray();
        items[cursorSlot] = rawCursorItem;
        var refs = fullLevel.SignedItemRefs.ToArray();
        refs[30 * AuthoritativeCathedralItemRuntime.Width + 30] = cursorSlot + 1;
        var bindings = fullLevel.SlotEntityBindings.ToArray();
        bindings[cursorSlot] = new(cursorSlot, initialEntityId);
        var checkpoint = full with {
            Levels = [fullLevel with { Items = items, SignedItemRefs = refs, SlotEntityBindings = bindings }],
        };
        checkpoint.ValidateShape();

        var profile = CreateProfile(checkpoint);
        var options = Options(profile, []);
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(Template());
        runtime.Write("hero", hero => {
            hero.EntityId = 1;
            hero.LevelId = 1;
            hero.PositionX = 29;
            hero.PositionY = 30;
        });

        var initialPlayer = runtime.Read("hero", hero => hero);
        Assert.Empty(initialPlayer.Inventory);
        Assert.Null(initialPlayer.HeldItem);
        var heroOrigin = (initialPlayer.PositionX, initialPlayer.PositionY);
        var beforePickup = simulation.WorldState.CaptureCathedralItems()!.Levels.Single();
        Assert.Equal(127, beforePickup.ActiveItemCount);
        Assert.Equal(Enumerable.Range(0, AuthoritativeCathedralItemRuntime.ActiveCapacity), beforePickup.ActiveItemIds);
        Assert.Equal(rawCursorItem, beforePickup.Items[cursorSlot]);
        var sourceWorldItem = Assert.Single(simulation.WorldState.WorldItems.Values);
        Assert.Equal(initialEntityId, sourceWorldItem.EntityId);
        Assert.DoesNotContain(simulation.WorldState.WorldItems.Values,
            item => item.PositionX == heroOrigin.PositionX && item.PositionY == heroOrigin.PositionY);
        var heldState = sourceWorldItem.State with { CreateInfo = 0x0456U };
        var heldBeforePickup = new OwnedStoreItem(
            0, 0, 8811, sourceWorldItem.Price, 0, heldState);
        runtime.Write("hero", hero => hero.HeldItem = heldBeforePickup);
        Assert.Equal(heldBeforePickup, runtime.Read("hero", hero => hero.HeldItem));

        DiabloWorldIntentResult pickup = default!;
        runtime.Write("hero", hero => pickup = simulation.Execute(
            hero, new DiabloPickupWorldItemIntent(initialEntityId), 1, [hero]));
        Assert.True(pickup.Succeeded, pickup.Failure?.ToString());
        var pickedItem = runtime.Read("hero", hero => Assert.Single(hero.Inventory));
        Assert.Equal((uint)7311, pickedItem.ItemSeed);
        Assert.Equal(1002U, pickedItem.State.StableItemId);
        Assert.Equal(sourceWorldItem.Price, pickedItem.Price);
        var expectedPickedState = sourceWorldItem.State with {
            CreateInfo = sourceWorldItem.State.CreateInfo & ~0x8000U,
            StatFlag = true,
        };
        Assert.Equal(expectedPickedState, pickedItem.State);
        Assert.Null(runtime.Read("hero", hero => hero.HeldItem));
        var droppedHeldItem = Assert.Single(simulation.WorldState.WorldItems.Values);
        Assert.Equal(heroOrigin, (droppedHeldItem.PositionX, droppedHeldItem.PositionY));
        Assert.Equal(8811U, droppedHeldItem.ItemSeed);
        Assert.Equal(heldState with { PositionX = heroOrigin.PositionX, PositionY = heroOrigin.PositionY },
            droppedHeldItem.State);
        Assert.NotEqual(initialEntityId, droppedHeldItem.EntityId);

        var afterPickup = simulation.WorldState.CaptureCathedralItems()!.Levels.Single();
        Assert.Equal(127, afterPickup.ActiveItemCount);
        Assert.Equal(Enumerable.Range(0, AuthoritativeCathedralItemRuntime.ActiveCapacity), afterPickup.ActiveItemIds);
        Assert.Equal(cursorSlot, Array.FindIndex(afterPickup.SlotEntityBindings.ToArray(),
            binding => binding.EntityId == droppedHeldItem.EntityId));
        Assert.Equal(8811U, afterPickup.Items[cursorSlot].Seed);
        Assert.Equal((ushort)0x0456, afterPickup.Items[cursorSlot].CreateInfo);
        Assert.Equal(heldState.BaseName, afterPickup.Items[cursorSlot].BaseName);
        Assert.Equal(heldState.Identified, afterPickup.Items[cursorSlot].Identified);
        Assert.Equal(checkpoint.UniqueItemFlags, simulation.WorldState.CaptureCathedralItems()!.UniqueItemFlags);

        var codec = new DiabloCohortCodec();
        var afterPickupBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var candidate = DiabloSimulation.CreateCohortCandidate(options, codec.Deserialize(afterPickupBytes));
        var restoredRuntime = candidate.CreateRuntime();
        var restoredSimulation = candidate.Simulation;
        Assert.Equal(afterPickupBytes, codec.Serialize(restoredSimulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(pickedItem, restoredRuntime.Read("hero", hero => Assert.Single(hero.Inventory)));
        var restoredHeldDrop = Assert.Single(restoredSimulation.WorldState.WorldItems.Values);
        Assert.Equal(droppedHeldItem.EntityId, restoredHeldDrop.EntityId);
        Assert.Equal(droppedHeldItem.LevelId, restoredHeldDrop.LevelId);
        Assert.Equal(droppedHeldItem.PositionX, restoredHeldDrop.PositionX);
        Assert.Equal(droppedHeldItem.PositionY, restoredHeldDrop.PositionY);
        Assert.Equal(droppedHeldItem.ItemSeed, restoredHeldDrop.ItemSeed);
        Assert.Equal(droppedHeldItem.Price, restoredHeldDrop.Price);
        Assert.Equal(droppedHeldItem.State, restoredHeldDrop.State);
        var restoredLevel = restoredSimulation.WorldState.CaptureCathedralItems()!.Levels.Single();
        Assert.Equal(127, restoredLevel.ActiveItemCount);
        Assert.Equal(afterPickup.ActiveItemIds, restoredLevel.ActiveItemIds);
        Assert.Equal(afterPickup.Items[cursorSlot], restoredLevel.Items[cursorSlot]);
    }

    private static AuthoritativeCathedralItemRuntimeProfile CreateProfile(
        DiabloCathedralItemCheckpoint checkpoint,
        int? magicRockFrameCount = null)
    {
        var source = CathedralItemLiveSimulationTests.Profile();
        var catalog = magicRockFrameCount is null
            ? CreateGuaranteedSwordCatalog()
            : CreateMagicRockCatalog(magicRockFrameCount.Value);
        var bindings = source.ItemBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (magicRockFrameCount is not null)
            bindings[76] = new(76, 1076, "magic_rock");
        var levelProfiles = checkpoint.Levels.Select(level => source.LevelProfiles[0] with {
            LevelId = level.LevelId,
            Depth = level.Depth,
        }).ToArray();
        return source with {
            InitialCheckpoint = checkpoint,
            ItemCatalog = catalog,
            ItemBindings = bindings,
            LevelProfiles = levelProfiles,
        };
    }

    private static AuthoritativeCathedralThemeItemCatalog CreateGuaranteedSwordCatalog()
    {
        var source = AuthoritativeCathedralMonsterLootGeneratorTests.Catalog();
        var items = source.Items.Select(item => item.NativeItemIndex == 1
            ? item with { DropRate = 1, Available = true }
            : item with { DropRate = 0, Available = false }).ToArray();
        return new(items, source.Prefixes, source.Suffixes, source.Uniques, source.Spells,
            source.CursorAnimations, source.GlyphWidths, source.Cursors, source.AlwaysMagicMiscIds,
            source.Rules with { NoDropPercent = 0, GoldPercent = 0 }, source.ReplacementGlyphWidth);
    }

    private static AuthoritativeCathedralThemeItemCatalog CreateMagicRockCatalog(int frameCount)
    {
        var source = AuthoritativeCathedralThemeItemMetadataTests.CreateCatalog();
        var animations = source.CursorAnimations.ToDictionary(pair => pair.Key, pair => pair.Value);
        animations[76] = new(frameCount);
        var items = source.Items.Select(item => item.CursorGraphic == 76
            ? item with { AnimationFrameCount = frameCount, AnimationTicksPerFrame = 1 }
            : item).ToArray();
        return new(items, source.Prefixes, source.Suffixes, source.Uniques, source.Spells,
            animations, source.GlyphWidths, source.Cursors, source.AlwaysMagicMiscIds,
            source.Rules, source.ReplacementGlyphWidth);
    }

    private static DiabloSimulationOptions Options(
        AuthoritativeCathedralItemRuntimeProfile profile,
        IReadOnlyList<AuthoritativeCombatTarget> targets)
    {
        var definitions = profile.ItemBindings.Values.Select(binding => {
            var native = profile.ItemCatalog.Items.Single(item => item.NativeItemIndex == binding.NativeItemIndex);
            var tags = binding.NativeItemIndex switch {
                0 => new HashSet<string>(["Gold", "Currency"], StringComparer.OrdinalIgnoreCase),
                1 => new HashSet<string>(["Weapon", "OneHanded"], StringComparer.OrdinalIgnoreCase),
                2 => new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            };
            return new AuthoritativeItemDefinition(binding.StableItemId, native.ItemType, native.Value,
                native.Value, native.MinimumDamage, native.MaximumDamage, native.MinimumArmorClass,
                native.Durability, native.Durability, binding.InventoryWidth, binding.InventoryHeight, true) {
                    NativeItemIndex = binding.NativeItemIndex,
                    StableSymbol = binding.StableSymbol,
                    ItemActionKind = binding.ItemActionKind ?? DiabloItemActionKind.None,
                    EquipLocation = binding.NativeItemIndex == 1 ? 1 : native.EquipLocation,
                    GenerationTags = tags,
                    Name = native.Name,
                    IdentifiedName = native.Name,
                    ItemClass = native.ItemClass,
                    MiscId = native.MiscId,
                    SpellId = native.SpellId,
                    Flags = native.Flags,
                    MinimumStrength = native.MinimumStrength,
                    MinimumMagic = native.MinimumMagic,
                    MinimumDexterity = native.MinimumDexterity,
                };
        }).Append(new AuthoritativeItemDefinition(24, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, false) {
            NativeItemIndex = 23,
            MiscId = 44,
        }).ToArray();

        return new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            FallbackWidth = 112,
            FallbackHeight = 112,
            RandomSeed = 71,
            CombatTargets = targets,
            CombatRules = new(100, 1, 10),
            NativeMonsterMetadataFingerprint = MonsterFingerprint,
            NativeCathedralItems = profile,
            SuppressInitialWorldScaling = true,
            Items = new(definitions),
            Spells = new([new AuthoritativeSpellDefinition(101, 1, 0, 0, 0, 0) { NativeSpellId = 1 }]),
            Experience = new(new[] {
                new AuthoritativeExperienceLevel(1, 1000),
                new AuthoritativeExperienceLevel(2, 2000),
            }),
        };
    }

    private static DiabloCathedralItemCheckpoint CreateCheckpoint(int levelCount, bool animatedGroundItem = false)
    {
        var empty = AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint();
        var template = empty.Levels.Single();
        var levels = new DiabloCathedralItemLevelCheckpoint[levelCount];
        for (var index = 0; index < levelCount; index++) {
            var level = template with { LevelId = checked((uint)index + 1), Depth = index + 1 };
            if (animatedGroundItem) {
                var items = level.Items.ToArray();
                var entityId = checked((uint)101 + (uint)index);
                var x = 30 + index;
                items[0] = AuthoritativeCathedralItemRuntimeTestData.Item(1, x, 30) with {
                    AnimFlag = true,
                    Animation = GroundAnimation(4, 0, petrified: true),
                    SelectionRegion = 1,
                };
                var refs = level.SignedItemRefs.ToArray();
                refs[30 * AuthoritativeCathedralItemRuntime.Width + x] = 1;
                var bindings = level.SlotEntityBindings.ToArray();
                bindings[0] = new(0, entityId);
                level = level with {
                    Items = items,
                    ActiveItemCount = 1,
                    SignedItemRefs = refs,
                    SlotEntityBindings = bindings,
                };
            }
            levels[index] = level;
        }
        return empty with { Levels = levels };
    }

    private static DiabloCathedralItemCheckpoint CreateMagicRockCheckpoint(int frameCount)
    {
        var empty = AuthoritativeCathedralItemRuntimeTestData.EmptyCheckpoint();
        var level = empty.Levels.Single();
        var items = level.Items.ToArray();
        items[0] = MagicRock(25, 25, frameCount, 9, 1);
        items[1] = MagicRock(26, 25, frameCount, 18, 2);
        var refs = level.SignedItemRefs.ToArray();
        refs[25 * AuthoritativeCathedralItemRuntime.Width + 25] = 1;
        refs[25 * AuthoritativeCathedralItemRuntime.Width + 26] = 2;
        var bindings = level.SlotEntityBindings.ToArray();
        bindings[0] = new(0, 201);
        bindings[1] = new(1, 202);
        level = level with { Items = items, ActiveItemCount = 2, SignedItemRefs = refs, SlotEntityBindings = bindings };
        return empty with { Levels = [level] };
    }

    private static AuthoritativeCathedralInitialItemState MagicRock(int x, int y, int frameCount, int frame, int selectionRegion)
        => AuthoritativeCathedralItemRuntimeTestData.Item(76, x, y) with {
            CursorGraphic = 76,
            AnimFlag = true,
            Animation = GroundAnimation(frameCount, frame, petrified: true),
            SelectionRegion = selectionRegion,
        };

    private static AuthoritativeCathedralInitialItemAnimation GroundAnimation(
        int frameCount,
        int frame,
        bool petrified = false)
        => new() {
            TicksPerFrame = 1,
            TickCounterOfCurrentFrame = 0,
            NumberOfFrames = frameCount,
            CurrentFrame = frame,
            IsPetrified = petrified,
        };

    private static DiabloPlayerTemplate Template()
        => DiabloPlayerTemplate.CreateDefault(1000, 1000, 0, 0);

    private static AuthoritativeCombatTarget Target(
        uint entityId,
        int slot,
        int x,
        int y,
        uint levelId,
        uint experienceReward = 0,
        bool moving = false,
        int? futurePositionX = null,
        int? futurePositionY = null)
    {
        var futureX = futurePositionX ?? x;
        var futureY = futurePositionY ?? y;
        var target = new AuthoritativeCombatTarget(entityId, x, y, 1, maxHitPoints: 1,
            monsterId: 90, levelId: levelId, experienceReward: experienceReward,
            moving: moving, futurePositionX: futureX, futurePositionY: futureY);
        var raw = new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot,
            Type = 0,
            LevelType = 1,
            UniqueType = 255,
            Leader = 255,
            Tile = new(x, y),
            Future = new(futureX, futureY),
            Old = new(x, y),
            LightId = -1,
            HitPoints = 64,
            MaxHitPoints = 64,
            RndItemSeed = checked((uint)(2588 + slot)),
            AiSeed = 0,
        };
        target.RestoreNativeInitialization(new(MonsterFingerprint, raw, 0, 0, 1, 1,
            new(slot, 0, 255, entityId, 90, levelId, string.Empty), false),
            checked((uint)(2588 + slot)), 0);
        return target;
    }

    private static SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, DiabloCombatIntentResult>
        AttackCommand(DiabloSimulation simulation, ulong tick, uint targetEntityId)
        => new("hero", (player, participants) => {
            var result = simulation.Execute(player, new DiabloAttackIntent(targetEntityId), tick, participants);
            return new SimulationStepResult<DiabloCombatIntentResult, DiabloGameplayEvent>(result, result.Events);
        });

    private static AuthoritativeCathedralInitialItemState GroundItem(
        DiabloSimulation simulation,
        uint levelId,
        uint entityId)
    {
        var level = simulation.WorldState.CaptureCathedralItems()!.Levels.Single(value => value.LevelId == levelId);
        var slot = Array.FindIndex(level.SlotEntityBindings.ToArray(), binding => binding.EntityId == entityId);
        Assert.InRange(slot, 0, AuthoritativeCathedralItemRuntime.PoolCapacity - 1);
        return level.Items[slot];
    }
}
