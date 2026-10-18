using System.Reflection;
using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeDepthThreeAndFourObjectTransactionsRetainFreshCohort()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);

        const uint runSeed = 0x13579BDF;
        var initial = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, runSeed);
        var footprintTable = setup.Fixture.Content.GetRequiredTable(
            "native_cathedral_runtime/native-rogue-l1-inventory.tsv");
        var footprints = AuthoritativeCathedralNativeItemSidecars.LoadFootprints(
            TsvTable.Parse("native-rogue-l1-inventory.tsv", footprintTable.ToCanonicalTsv()));
        var fullSemantics = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
            setup.Fixture.PopulationMetadata.Items.Catalog,
            setup.Fixture.BaselineItems,
            initial.Simulation.Spells!,
            footprints,
            includeNativeJewelry: true,
            includeNativeDepthItems: true);
        var priorSemantics = setup.Fixture.Content.GetRequiredTable(
            "native_cathedral_runtime/native_item_semantics.tsv");
        var expectedAddedIndexes = new[] { 82, 100, 101, 109, 112, 113 };
        var priorIndexes = priorSemantics.Rows
            .Select(row => row.RequiredInt32("native_item_index")).ToHashSet();
        var addedIndexes = fullSemantics.Rows
            .Select(row => row.RequiredInt32("native_item_index"))
            .Where(index => !priorIndexes.Contains(index))
            .Order()
            .ToArray();
        Assert.Equal(expectedAddedIndexes, addedIndexes);
        var priorRows = priorSemantics.Rows
            .Select(row => string.Join('\t', priorSemantics.Columns.Select(column => column == "item_action_kind"
                    && row.RequiredInt32("native_item_index") is 27 or 91 or 98 or 102 ? "None" : row.Required(column))))
            .ToArray();
        var retainedRows = fullSemantics.Rows
            .Where(row => !expectedAddedIndexes.Contains(row.RequiredInt32("native_item_index")))
            .Select(row => string.Join('\t', fullSemantics.Columns.Select(column => row.Required(column))))
            .ToArray();
        Assert.Equal(priorRows, retainedRows);
        var fullRejuvenationRow = Assert.Single(fullSemantics.Rows,
            row => row.RequiredInt32("native_item_index") == 82);
        Assert.Equal("potion_full_rejuvenation_standard", fullRejuvenationRow.Required("stable_item_symbol"));
        Assert.Equal(20, fullRejuvenationRow.RequiredInt32("misc_id"));
        Assert.Equal("RestoreFullLifeAndMana", fullRejuvenationRow.Required("item_action_kind"));

        var content = ReplaceTable(setup.Fixture.Content,
            "native_cathedral_runtime/native_item_semantics.tsv",
            fullSemantics with { SourcePath = "native_cathedral_runtime/native_item_semantics.tsv" });
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(content, runSeed);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var firstStair = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        const string session = "native-depth-three-four-objects";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(session, rogue.ClassId,
            null, null, null, startingLevelId: 1,
            startingPositionX: firstStair.TriggerX, startingPositionY: firstStair.TriggerY));
        runtime.Write(session, player => player.EntityId = 0x70000001);
        var player = runtime.Read(session, value => value);
        var commands = (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(simulation)!;
        var codec = new DiabloCohortCodec();
        var coverage = new HashSet<string>(StringComparer.Ordinal);
        var operationCount = 0;
        var fullRejuvenationDefinition = simulation.Items!.Definitions.SingleOrDefault(row =>
            row.StableSymbol == "potion_full_rejuvenation_standard");
        Assert.NotNull(fullRejuvenationDefinition);
        Assert.Equal(82, fullRejuvenationDefinition!.NativeItemIndex);
        Assert.Equal(DiabloItemActionKind.RestoreFullLifeAndMana, fullRejuvenationDefinition.ItemActionKind);
        var fullRejuvenationUsed = false;

        for (uint sourceLevel = 1; sourceLevel < 4; sourceLevel++) {
            var stair = world.World!.WarpTargets.Single(row => row.SourceLevelId == sourceLevel
                && row.TargetLevelId == sourceLevel + 1);
            player.PositionX = stair.TriggerX;
            player.PositionY = stair.TriggerY;
            var entryTick = checked(runtime.LastAdvancedTick + 1);
            var entered = commands.ExecuteLevelTrigger(player, entryTick, [player]);
            Assert.True(entered.Succeeded, $"The native L{sourceLevel + 1} stair was refused.");
            Assert.Equal(sourceLevel + 1, player.LevelId);
            if (player.CurrentAction is { } entryAction) {
                runtime.AdvanceTo(entryAction.EndTick);
                Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
                Assert.Null(player.CurrentAction);
            }
            Assert.True(world.OwnsNativePopulationLevel(sourceLevel));
            Assert.True(world.OwnsNativePopulationLevel(sourceLevel + 1));
            if (player.LevelId is 3 or 4)
                ProcessLevel(player.LevelId);
        }

        void RunOperation(
            uint levelId,
            string label,
            AuthoritativeWorldObject target,
            int expectedAllocations,
            int? expectedCreateInfo,
            Func<ulong, bool> apply,
            Func<string>? refusalContext = null)
        {
            var adjacent = FindAdjacentWalkableCell(world, target);
            player.PositionX = adjacent.X;
            player.PositionY = adjacent.Y;

            var before = simulation.CaptureCohortState(runtime);
            var beforeItems = Assert.IsType<DiabloCathedralItemCheckpoint>(before.CathedralItems);
            var beforePool = beforeItems.Levels.Single(row => row.LevelId == levelId);
            var beforeTarget = Assert.Single(before.Objects, row => row.EntityId == target.EntityId);
            var beforeOtherObjects = JsonSerializer.Serialize(before.Objects
                .Where(row => row.EntityId != target.EntityId).ToArray());
            var beforeOtherItems = JsonSerializer.Serialize(before.WorldItems
                .Where(row => row.LevelId != levelId).ToArray());
            var beforeOtherActors = JsonSerializer.Serialize(before.CombatTargets
                .Where(row => row.Monster.LevelId != levelId).ToArray());
            var beforeOtherPools = JsonSerializer.Serialize(beforeItems.Levels
                .Where(row => row.LevelId != levelId).ToArray());
            var beforeOtherSarcs = JsonSerializer.Serialize((before.NativeSarcophagi ?? [])
                .Where(row => row.EntityId != target.EntityId).ToArray());
            var beforeOtherBarrels = JsonSerializer.Serialize((before.NativeBarrels ?? [])
                .Where(row => row.EntityId != target.EntityId).ToArray());
            var priorGroundIds = before.WorldItems.Select(row => row.EntityId).ToHashSet();
            var appliedTick = checked(runtime.LastAdvancedTick + 1);

            Assert.True(apply(appliedTick), $"Native L{levelId} {label} operation was refused. "
                + refusalContext?.Invoke());

            var after = simulation.CaptureCohortState(runtime);
            var afterPool = Assert.IsType<DiabloCathedralItemCheckpoint>(after.CathedralItems)
                .Levels.Single(row => row.LevelId == levelId);
            Assert.Equal(expectedAllocations, afterPool.ActiveItemCount - beforePool.ActiveItemCount);
            if (expectedCreateInfo is { } createInfo) {
                var newSlots = afterPool.ActiveItemIds
                    .Skip(beforePool.ActiveItemCount)
                    .Take(expectedAllocations)
                    .ToArray();
                Assert.Equal(expectedAllocations, newSlots.Length);
                Assert.All(newSlots, slot => Assert.Equal(createInfo,
                    (int)(afterPool.Items[slot].CreateInfo & 63)));
            }
            var newDrops = after.WorldItems.Where(row => row.LevelId == levelId
                && !priorGroundIds.Contains(row.EntityId)).ToArray();
            if (expectedCreateInfo is { } visibleCreateInfo)
                Assert.All(newDrops, row => Assert.Equal(visibleCreateInfo, (int)(row.State.CreateInfo & 63)));

            var afterTarget = Assert.Single(after.Objects, row => row.EntityId == target.EntityId);
            Assert.Equal(beforeTarget.EntityId, afterTarget.EntityId);
            Assert.Equal(beforeTarget.ObjectId, afterTarget.ObjectId);
            Assert.Equal(beforeTarget.LevelId, afterTarget.LevelId);
            Assert.Equal(beforeTarget.PositionX, afterTarget.PositionX);
            Assert.Equal(beforeTarget.PositionY, afterTarget.PositionY);
            Assert.Equal(beforeOtherObjects, JsonSerializer.Serialize(after.Objects
                .Where(row => row.EntityId != target.EntityId).ToArray()));
            Assert.Equal(beforeOtherItems, JsonSerializer.Serialize(after.WorldItems
                .Where(row => row.LevelId != levelId).ToArray()));
            Assert.Equal(beforeOtherActors, JsonSerializer.Serialize(after.CombatTargets
                .Where(row => row.Monster.LevelId != levelId).ToArray()));
            Assert.Equal(beforeOtherPools, JsonSerializer.Serialize(after.CathedralItems!.Levels
                .Where(row => row.LevelId != levelId).ToArray()));
            Assert.Equal(beforeOtherSarcs, JsonSerializer.Serialize((after.NativeSarcophagi ?? [])
                .Where(row => row.EntityId != target.EntityId).ToArray()));
            Assert.Equal(beforeOtherBarrels, JsonSerializer.Serialize((after.NativeBarrels ?? [])
                .Where(row => row.EntityId != target.EntityId).ToArray()));
            Assert.Equal(levelId, (uint)afterPool.Depth);
            operationCount++;
            coverage.Add($"L{levelId}:{label}");
        }

        void ProcessLevel(uint levelId)
        {
            Assert.Equal(levelId, player.LevelId);
            Assert.Equal((int)levelId, world.NativePopulationDepth(levelId));
            var objects = world.Objects.Values.Where(row => row.LevelId == levelId).ToArray();
            var chests = new List<(AuthoritativeWorldObject Object, int Branch, int Count)>();
            foreach (var chest in objects.Where(world.CanOperateNativeChest)) {
                if (world.TryGetNativeChestLootParameters(chest, out _, out _, out var count, out var branch)
                    && count > 0)
                    chests.Add((chest, branch, count));
            }

            foreach (var useful in new[] { true, false }) {
                var candidate = chests.FirstOrDefault(value => (value.Branch == 0) == useful);
                if (candidate.Object is null)
                    continue;
                var expectedCreateInfo = useful ? (int)levelId : checked((int)levelId * 2);
                var beforeFrame = candidate.Object.AnimationFrame;
                DiabloRuleFailure? chestFailure = null;
                RunOperation(levelId, useful ? "chest-useful" : "chest-random", candidate.Object,
                    candidate.Count, expectedCreateInfo, tick => {
                        var result = simulation.Execute(player,
                            new DiabloOperateObjectIntent(candidate.Object.EntityId), tick, [player]);
                        chestFailure = result.Failure;
                        return result.Succeeded;
                    }, () => $"failure={chestFailure}; player={player.LevelId}@{player.PositionX},{player.PositionY}/"
                        + $"{player.CombatMode}; chest={candidate.Object.EntityId}/{candidate.Object.NativeObjectId}/"
                        + $"{candidate.Object.LevelId}@{candidate.Object.PositionX},{candidate.Object.PositionY}/"
                        + $"activated={candidate.Object.Activated}; distance={DeterministicGridMetrics.ChebyshevDistance(candidate.Object.PositionX, candidate.Object.PositionY, player.PositionX, player.PositionY)}; "
                        + $"depth={world.NativePopulationDepth(levelId)}; count={candidate.Count}; branch={candidate.Branch}");
                Assert.True(candidate.Object.Activated);
                Assert.Equal(checked(beforeFrame + 2), candidate.Object.AnimationFrame);
            }

            var sarcFacts = objects.Where(world.CanOperateNativeSarcophagus)
                .Select(value => (Object: value, Facts: world.TryGetNativeSarcophagusOperationFacts(value, out var facts)
                    ? facts : null))
                .Where(value => value.Facts is not null)
                .ToArray();
            var lootSarc = sarcFacts.FirstOrDefault(value => value.Facts!.Var1 <= 2);
            if (lootSarc.Object is not null) {
                RunOperation(levelId, "sarcophagus-loot", lootSarc.Object, 1,
                    checked((int)levelId * 2), tick => simulation.Execute(player,
                        new DiabloOperateObjectIntent(lootSarc.Object.EntityId), tick, [player]).Succeeded);
                Assert.True(world.GetNativeSarcophagusState(levelId, lootSarc.Facts!.ObjectSlot)!.Opened);
            }
            var linkedSarc = sarcFacts.FirstOrDefault(value => value.Facts!.Var1 >= 8
                && value.Facts.LinkedMonster is not null
                && (lootSarc.Object is null || value.Object.EntityId != lootSarc.Object.EntityId));
            if (linkedSarc.Object is not null) {
                RunOperation(levelId, "sarcophagus-linked", linkedSarc.Object, 0, null,
                    tick => simulation.Execute(player,
                        new DiabloOperateObjectIntent(linkedSarc.Object.EntityId), tick, [player]).Succeeded);
                var state = world.GetNativeSarcophagusState(levelId, linkedSarc.Facts!.ObjectSlot)!;
                Assert.True(state.Opened);
                Assert.Contains(state.ActorState, new[] { DiabloNativeSarcophagusActorState.Reserved,
                    DiabloNativeSarcophagusActorState.Emerging, DiabloNativeSarcophagusActorState.Active });
            }

            var barrels = new List<(AuthoritativeWorldObject Object, AuthoritativeCathedralNativeBarrelFacts Facts)>();
            foreach (var barrel in objects.Where(world.CanBreakNativeBarrel)) {
                if (world.TryGetNativeBarrelFacts(barrel, out var facts))
                    barrels.Add((barrel, facts));
            }
            foreach (var useful in new[] { true, false }) {
                var candidate = barrels.FirstOrDefault(value => !value.Facts.IsExplosive
                    && value.Facts.Var2 <= 1 && (value.Facts.Var3 == 0) == useful);
                if (candidate.Object is null)
                    continue;
                var expectedCreateInfo = useful ? (int)levelId : checked((int)levelId * 2);
                RunOperation(levelId, useful ? "barrel-useful" : "barrel-random", candidate.Object,
                    1, expectedCreateInfo, tick => simulation.BreakNativeBarrelAtEffectBoundary(
                        player, candidate.Object.EntityId, tick, [player]).Succeeded);
                Assert.True(world.GetNativeBarrelState(levelId, candidate.Facts.ObjectSlot)!.Broken);
            }

            var linkedBarrel = barrels.FirstOrDefault(value => !value.Facts.IsExplosive
                && value.Facts.Var2 >= 8 && value.Facts.LinkedMonster is not null);
            if (linkedBarrel.Object is not null) {
                RunOperation(levelId, "barrel-linked", linkedBarrel.Object, 0, null,
                    tick => simulation.BreakNativeBarrelAtEffectBoundary(
                        player, linkedBarrel.Object.EntityId, tick, [player]).Succeeded);
                var state = world.GetNativeBarrelState(levelId, linkedBarrel.Facts.ObjectSlot)!;
                Assert.True(state.Broken);
                Assert.Contains(state.ActorState, new[] { DiabloNativeBarrelActorState.Reserved,
                    DiabloNativeBarrelActorState.Emerging, DiabloNativeBarrelActorState.Active });
            }

            ProcessFullRejuvenation(levelId);
        }

        void ProcessFullRejuvenation(uint levelId)
        {
            foreach (var drop in world.WorldItems.Values.Where(value => value.LevelId == levelId
                         && value.State.StableItemId == fullRejuvenationDefinition!.ItemId).ToArray()) {
                Assert.Equal(19, drop.State.MiscId);
                Assert.Equal(DiabloItemActionKind.RestoreFullLifeAndMana, drop.State.ItemActionKind);
                player.PositionX = drop.PositionX;
                player.PositionY = drop.PositionY;
                var pickupTick = checked(runtime.LastAdvancedTick + 1);
                Assert.True(simulation.Execute(player, new DiabloPickupWorldItemIntent(drop.EntityId),
                    pickupTick, [player]).Succeeded);
                var ownedInventory = player.Inventory.Select((item, index) => (Item: item, Index: index))
                    .FirstOrDefault(value => value.Item.State.StableItemId == drop.State.StableItemId
                        && value.Item.ItemSeed == drop.ItemSeed);
                var ownedBelt = player.Belt.FirstOrDefault(value => value.State.StableItemId == drop.State.StableItemId
                    && value.ItemSeed == drop.ItemSeed);
                DiabloPlayerItemReference useReference = ownedInventory.Item is not null
                    ? new(DiabloPlayerItemLocation.Inventory, checked((uint)ownedInventory.Index), drop.State.StableItemId)
                    : ownedBelt is not null
                        ? new(DiabloPlayerItemLocation.Belt, ownedBelt.Slot, drop.State.StableItemId)
                        : throw new InvalidOperationException(
                            "The native full rejuvenation pickup has no owned inventory projection.");
                player.Life = Math.Max(1, player.BaseLifeMaximum / 2);
                player.Mana = Math.Max(1, player.BaseManaMaximum / 2);
                var randomBeforeUse = simulation.CaptureCohortState(runtime).RandomState;
                var useTick = checked(runtime.LastAdvancedTick + 1);
                Assert.True(simulation.Execute(player, new DiabloUseConsumableIntent(useReference), useTick).Succeeded);
                Assert.Equal(player.BaseLifeMaximum, player.Life);
                Assert.Equal(player.BaseManaMaximum, player.Mana);
                Assert.DoesNotContain(player.Inventory, value => value.State.StableItemId == drop.State.StableItemId
                    && value.ItemSeed == drop.ItemSeed);
                Assert.DoesNotContain(player.Belt, value => value.State.StableItemId == drop.State.StableItemId
                    && value.ItemSeed == drop.ItemSeed);
                Assert.DoesNotContain(world.WorldItems.Values, value => value.EntityId == drop.EntityId);
                Assert.Equal(randomBeforeUse, simulation.CaptureCohortState(runtime).RandomState);
                fullRejuvenationUsed = true;
                break;
            }
        }

        Assert.Contains(coverage, value => value.StartsWith("L3:chest-", StringComparison.Ordinal));
        Assert.Contains(coverage, value => value.StartsWith("L4:chest-", StringComparison.Ordinal));
        Assert.Contains(coverage, value => value.StartsWith("L3:barrel-", StringComparison.Ordinal));
        Assert.Contains(coverage, value => value.StartsWith("L4:barrel-", StringComparison.Ordinal));
        Assert.True(coverage.Any(value => value.Contains("sarcophagus-", StringComparison.Ordinal)),
            "The pinned L3/L4 activation exposed no supported sarcophagus operation.");
        Assert.True(operationCount >= 5);
        Console.WriteLine("Native v10 depth-object branch coverage: " + string.Join(", ", coverage.OrderBy(value => value)));

        Console.WriteLine($"Native v10 L3/L4 full rejuvenation ground projection used: {fullRejuvenationUsed}.");

        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeDepthProgressionFormatVersion, captured.FormatVersion);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, captured.CathedralItems!.Levels.Select(row => row.LevelId));
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, captured.CathedralDoors!.DoorLevels.Select(row => row.LevelId));
        Assert.Equal(new uint[] { 2, 3, 4 }, captured.NativeProgression!.Entries.Select(row => row.LevelId));
        var bytes = codec.Serialize(captured);
        var coldCandidate = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var coldRuntime = coldCandidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(coldCandidate.Simulation.CaptureCohortState(coldRuntime)));
        var nextTick = checked(runtime.LastAdvancedTick + 1);
        runtime.AdvanceTo(nextTick);
        coldRuntime.AdvanceTo(nextTick);
        Assert.Equal(codec.Serialize(simulation.CaptureCohortState(runtime)),
            codec.Serialize(coldCandidate.Simulation.CaptureCohortState(coldRuntime)));
    }
}
