using System.Reflection;
using System.Text.Json;
using Devilution.Engine;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    public void NativePepinPurchasesSurviveFreshProgressionCohort(bool pinned)
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var content = setup!.Fixture.Content;
        if (pinned) {
            var sidecars = AuthoritativeCathedralNativeItemSidecars.Load(
                content.GetRequiredTable("native_cathedral_runtime/native-rogue-l1-inventory.tsv") with { SourcePath = "native-rogue-l1-inventory.tsv" },
                content.GetRequiredTable("native_cathedral_runtime/native_item_semantics.tsv") with { SourcePath = "native_item_semantics.tsv" });
            var stock = AuthoritativeCathedralNativePopulationBuilder.PrepareNativeProfileStoreTable(
                content.GetRequiredTable("stores.tsv"), setup.Fixture.BaselineItems, sidecars,
                nativeProgression: true);
            var marker = AuthoritativeCathedralNativePopulationBuilder.CreateNativeProgressionStoreCapabilityTable();
            var packs = content.Manifest.Packs.Select(pack => new Devilution.Data.ContentPack(
                pack.Id, pack.Version,
                pack.Tables.Select(table => table.SourcePath == "stores.tsv" ? stock : table)
                    .Concat(pack.Tables.Any(table => table.SourcePath == "stores.tsv") ? [marker] : []).ToArray(),
                pack.Dependencies, pack.Overlays, pack.Schema, pack.DependencyConstraints, pack.Assets));
            content = Devilution.Data.ComposedContent.Compose(new Devilution.Data.ContentManifest(
                content.Manifest.Id, content.Manifest.Version, packs));
        }
        var activation = DiabloModule.Instance.CathedralNativeProgressionContentBootstrap
            .ActivateNativeProgression(content, 0x13579BDF);
        var simulation = activation.Simulation;
        Assert.True(activation.TownerGreetings!.TryGet(1, out var pepin));
        var adjacent = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: pepin.PositionX + offset.X, Y: pepin.PositionY + offset.Y))
            .First(cell => simulation.WorldState.IsWalkable(0, cell.X, cell.Y)
                && !simulation.WorldState.IsOccupied(0, cell.X, cell.Y, []));
        const string session = "native-pepin-cold";
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        // A controlled test wallet exercises both purchases; app acceptance earns its own Gold.
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, 500, null, null, startingLevelId: 0,
            startingPositionX: adjacent.X, startingPositionY: adjacent.Y));
        runtime.Write(session, player => player.EntityId = 0x70000001);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);
        Assert.True(simulation.Execute(player, new DiabloOpenStoreIntent(4), 1).Succeeded);
        var beforeStock = simulation.CaptureCohortState(runtime).Stores.Single(row => row.StoreId == 4);
        var firstPrice = beforeStock.Items!.Single(row => row.StoreSlot == 0).Price;
        var expectedRandom = new DeterministicLegacyRandom(simulation.RandomState);
        var firstSeed = pinned ? unchecked((uint)expectedRandom.AdvanceRndSeed()) : 0;
        Assert.True(simulation.Execute(player, new DiabloPurchaseIntent(4, 0), 1).Succeeded);
        Assert.Equal(500U - firstPrice, player.Gold);
        Assert.Equal(expectedRandom.State, simulation.RandomState);
        if (pinned) {
            Assert.True(player.Inventory.Any(row => row.ItemSeed == firstSeed && row.State.StableItemId == 2180)
                || player.Belt.Any(row => row.ItemSeed == firstSeed && row.State.StableItemId == 2180));
            Assert.Equal(JsonSerializer.Serialize(beforeStock),
                JsonSerializer.Serialize(simulation.CaptureCohortState(runtime).Stores.Single(row => row.StoreId == 4)));
        } else {
            Assert.DoesNotContain(simulation.CaptureCohortState(runtime).Stores.Single(row => row.StoreId == 4).Items!,
                row => row.StoreSlot == 0);
        }
        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var fresh = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        var freshRuntime = fresh.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime)));
        var restored = freshRuntime.Read(session, value => value);
        var restoredStock = fresh.Simulation.CaptureCohortState(freshRuntime).Stores.Single(row => row.StoreId == 4);
        var slot = pinned ? 0U : 1U;
        var price = restoredStock.Items!.Single(row => row.StoreSlot == slot).Price;
        var gold = restored.Gold;
        var expectedResumed = new DeterministicLegacyRandom(fresh.Simulation.RandomState);
        var resumedSeed = pinned ? unchecked((uint)expectedResumed.AdvanceRndSeed()) : 0;
        Assert.True(fresh.Simulation.Execute(restored, new DiabloPurchaseIntent(4, slot), 1).Succeeded);
        Assert.Equal(gold - price, restored.Gold);
        Assert.Equal(expectedResumed.State, fresh.Simulation.RandomState);
        if (pinned) {
            Assert.True(restored.Inventory.Any(row => row.ItemSeed == resumedSeed && row.State.StableItemId == 2180)
                || restored.Belt.Any(row => row.ItemSeed == resumedSeed && row.State.StableItemId == 2180));
            Assert.Equal(JsonSerializer.Serialize(restoredStock),
                JsonSerializer.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime).Stores.Single(row => row.StoreId == 4)));
        } else {
            Assert.Empty(fresh.Simulation.CaptureCohortState(freshRuntime).Stores.Single(row => row.StoreId == 4).Items!);
        }
        var secondBytes = codec.Serialize(fresh.Simulation.CaptureCohortState(freshRuntime));
        var secondFresh = activation.CohortCandidateFactory!(codec.Deserialize(secondBytes));
        Assert.Equal(secondBytes, codec.Serialize(secondFresh.Simulation.CaptureCohortState(secondFresh.CreateRuntime())));
    }

    [Fact(Explicit = true)]
    public void NativeSemanticsRetainLegacyPinsAndExplicitProgressionRows()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        Assert.NotNull(setup);
        Assert.True(setup!.Fixture.Content.TryGetTable("native_cathedral_runtime/native_item_semantics.tsv", out var legacy));
        static string Digest(Devilution.Data.TsvTable table) => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(table.ToCanonicalTsv())));
        Assert.Equal(110, legacy.Rows.Count);
        Assert.Equal("45A6194CA54BBB16D905B101DB495E08093D3746963B29535E1D8BC38098A2EB", Digest(legacy));
        var footprints = AuthoritativeCathedralNativeItemSidecars.LoadFootprints(Devilution.Data.TsvTable.Parse(
            "native-rogue-l1-inventory.tsv", File.ReadAllText(
                Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_NATIVE_CURSOR_FOOTPRINT_TABLE_PATH")!)));
        var progression = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
            setup.Fixture.PopulationMetadata.Items.Catalog, setup.Fixture.BaselineItems,
            setup.Activation.Simulation.Spells, footprints, includeNativeJewelry: true);
        Assert.Equal(115, progression.Rows.Count);
        Assert.Equal("E77394CFB1B290E19FA1FCC2A85EF3EA7BC7A721D77F889226AE397355A73C8D", Digest(progression));
    }

    [Fact(Explicit = true)]
    public void NativeQueuedPotionAfterLethalHitPreservesDeathAndValidProjection()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false);
        Assert.NotNull(setup);
        var activation = setup!.Activation;
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        const string session = "native-queued-potion-death";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(session, rogue.ClassId,
            null, null, null, startingLevelId: 1, startingPositionX: 77, startingPositionY: 46));
        runtime.Write(session, value => value.EntityId = 0x70000001);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);
        var potion = player.Belt.First(row => row.State.ItemActionKind == DiabloItemActionKind.RestorePartialLife);
        // The intent is formed alive, then reaches authoritative execution after a lethal native impact.
        var queued = new DiabloUseConsumableIntent(new DiabloPlayerItemReference(
            DiabloPlayerItemLocation.Belt, potion.Slot, potion.State.StableItemId));
        player.Life = 64;
        var trap = world.CaptureNativeTraps()!.First();
        var random = new DeterministicLegacyRandom(1);
        var combat = new DiabloCombatCommandProcessor(world, simulation.Spells, simulation.CombatRules,
            random, simulation.PlayerClasses, simulation.PlayerAnimations, useNativePlayerActionTiming: true,
            items: simulation.Items);
        Assert.True(combat.ResolveNativeTrapArrowPlayerHit(trap.Facts.TrapEntityId, player, 0, 1, [player], []));
        Assert.Equal(0, player.Life);
        Assert.Equal(DiabloPlayerCombatMode.Death, player.CombatMode);
        Assert.Equal(DiabloPlayerActionKind.Death, player.CurrentAction!.Kind);
        var cursor = player.CurrentAction;
        var codec = new DiabloCohortCodec();
        var before = codec.Serialize(simulation.CaptureCohortState(runtime));
        var randomBefore = random.State;
        var refused = simulation.Execute(player, queued, 1);
        Assert.False(refused.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, refused.Failure);
        Assert.Same(cursor, player.CurrentAction);
        Assert.Contains(player.Belt, row => row.ItemSeed == potion.ItemSeed && row.State.StableItemId == potion.State.StableItemId);
        Assert.Equal(randomBefore, random.State);
        Assert.Equal(before, codec.Serialize(simulation.CaptureCohortState(runtime)));
        var protocol = Devilution.Server.Protocol.DiabloProtocolMapper.ToProtocolSnapshot(
            simulation.CapturePresentationSnapshot(player, 1), levelGeometry: world.World!.Levels,
            spells: simulation.Spells, items: simulation.Items);
        Devilution.Server.Protocol.AuthoritativeSnapshotValidator.Validate(protocol);
        Assert.Equal(0, protocol.Players.Single().Life);
        Assert.Equal(8, (int)protocol.Players.Single().CombatMode);
    }

    [Theory(Explicit = true)]
    [InlineData(0U, 25, 2156045615U)]
    [InlineData(1U, 27, 2867233980U)]
    [InlineData(5U, 24, 1417020144U)]
    public void NativeL2UsefulContentsKeepPortalSelectionAndExactRng(uint seed, int itemIndex, uint finalSeed)
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        // Literal Borland-LCG vectors for native SetupAllUseful: potion draw,
        // L2 FlipCoin(3), then GetItemAttrs' armor draw, including range one.
        var result = AuthoritativeCathedralThemeItemGenerator.GenerateUseful(
            new(), seed, 2, setup!.Fixture.PopulationMetadata.Items.Catalog,
            new(2, CurrentDungeonLevel: 2), null, null);
        Assert.Equal(itemIndex, result.SelectedNativeItemIndex);
        Assert.Equal(finalSeed, result.FinalRandomState);
        Assert.Equal(2, result.Item.CreateInfo & 63);
        Assert.Equal(itemIndex == 27 ? 7 : 0, result.Item.SpellId);
    }

    [Fact(Explicit = true)]
    public void NativeL2ObjectLootAndTrapDepthSurviveFreshCohort()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeProgressionContentBootstrap
            .ActivateNativeProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var stair = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        const string session = "native-l2-interactions";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(session, rogue.ClassId,
            null, null, null, startingLevelId: 1, startingPositionX: stair.TriggerX, startingPositionY: stair.TriggerY));
        runtime.Write(session, value => value.EntityId = 0x70000001);
        var player = runtime.Read(session, value => value);
        var commands = (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(simulation)!;
        Assert.True(commands.ExecuteLevelTrigger(player, 0, [player]).Succeeded);
        Assert.Equal(2U, player.LevelId);
        runtime.AdvanceTo(player.CurrentAction!.EndTick);
        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        var l1Pool = JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single(row => row.LevelId == 1));

        void Approach(AuthoritativeWorldObject value)
        {
            var tile = FindAdjacentWalkableCell(world, value);
            player.PositionX = tile.X;
            player.PositionY = tile.Y;
        }
        void CheckNewLoot(HashSet<uint> oldIds, int generationLevel)
        {
            var drops = world.WorldItems.Values.Where(row => row.LevelId == 2 && !oldIds.Contains(row.EntityId)).ToArray();
            Assert.NotEmpty(drops);
            Assert.All(drops, row => Assert.Equal(generationLevel, (int)(row.State.CreateInfo & 63)));
            var state = simulation.CaptureCohortState(runtime);
            Assert.Equal(2, state.CathedralItems!.Levels.Single(row => row.LevelId == 2).Depth);
            Assert.Equal(l1Pool, JsonSerializer.Serialize(state.CathedralItems.Levels.Single(row => row.LevelId == 1)));
        }

        var chests = world.Objects.Values.Where(row => row.LevelId == 2 && world.CanOperateNativeChest(row)
                && world.TryGetNativeChestLootParameters(row, out _, out _, out var count, out var itemBranch)
                && count > 0).ToArray();
        Assert.NotEmpty(chests);
        foreach (var chest in chests.Take(2)) {
            Assert.True(world.TryGetNativeChestLootParameters(chest, out _, out _, out _, out var branch));
            Approach(chest);
            var oldIds = world.WorldItems.Keys.ToHashSet();
            var result = simulation.Execute(player, new DiabloOperateObjectIntent(chest.EntityId), 1, [player]);
            Assert.True(result.Succeeded, $"L2 chest branch {branch}: {result.Failure}");
            Assert.True(chest.Activated);
            CheckNewLoot(oldIds, branch == 0 ? 2 : 4);
        }
        var sarcophagus = world.Objects.Values.First(row => row.LevelId == 2 && world.CanOperateNativeSarcophagus(row)
            && world.TryGetNativeSarcophagusOperationFacts(row, out var facts) && facts.Var1 <= 2);
        Approach(sarcophagus);
        var beforeSarc = world.WorldItems.Keys.ToHashSet();
        Assert.True(simulation.Execute(player, new DiabloOperateObjectIntent(sarcophagus.EntityId), 1, [player]).Succeeded);
        CheckNewLoot(beforeSarc, 4);
        var barrels = world.Objects.Values.Where(row => row.LevelId == 2 && world.CanBreakNativeBarrel(row)
            && world.TryGetNativeBarrelFacts(row, out var facts) && !facts.IsExplosive && facts.Var2 <= 1).ToArray();
        Assert.NotEmpty(barrels);
        foreach (var barrel in barrels.Take(2)) {
            Assert.True(world.TryGetNativeBarrelFacts(barrel, out var facts));
            Approach(barrel);
            var oldIds = world.WorldItems.Keys.ToHashSet();
            Assert.True(simulation.BreakNativeBarrelAtEffectBoundary(player, barrel.EntityId, 1, [player]).Succeeded);
            CheckNewLoot(oldIds, facts.Var3 == 0 ? 2 : 4);
        }

        // This seed retains unsupported L2 wall-trap variants. The admitted
        // Arrow source is on L1; source-less impact scaling belongs to the victim's owner.
        Assert.DoesNotContain(world.CaptureNativeTraps()!, row => row.Facts.LevelId == 2);
        var trap = world.CaptureNativeTraps()!.First(row => row.Facts.LevelId == 1);
        var codec = new DiabloCohortCodec();
        var unsupported = world.Objects.Values.First(row => row.LevelId == 2 && row.TrapArmed
            && !world.CanOperateNativeTrapTrigger(row));
        Approach(unsupported);
        var refusedBytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var refusedRandom = new DeterministicLegacyRandom(1);
        Assert.Equal(DiabloNativeTrapDisarmResult.NotApplicable,
            world.TryDisarmNativeTrapTrigger(player, unsupported, refusedRandom));
        Assert.Equal(1U, refusedRandom.State);
        Assert.False(simulation.Execute(player, new DiabloOperateObjectIntent(unsupported.EntityId), 3, [player]).Succeeded);
        Assert.Equal(refusedBytes, codec.Serialize(simulation.CaptureCohortState(runtime)));
        var bytes = codec.Serialize(simulation.CaptureCohortState(runtime));
        var cold = activation.CohortCandidateFactory!(codec.Deserialize(bytes));
        Assert.Equal(bytes, codec.Serialize(cold.Simulation.CaptureCohortState(cold.CreateRuntime())));

        // Resolve source-less Arrow boundaries against the actual L2 owner.
        var victim = new AuthoritativePlayerState(0, 0, 2880, 1440, 1440,
            new(new(20, 20), new(10, 10), new(0, 0), new(0, 0)), [], [], [],
            player.PositionX, player.PositionY, 2880, 1, 2, 0, rogue.ClassId,
            items: simulation.Items) { EntityId = 0x70000002 };
        var random = new DeterministicLegacyRandom(1);
        var processor = new DiabloCombatCommandProcessor(world, simulation.Spells, simulation.CombatRules,
            random, simulation.PlayerClasses, simulation.PlayerAnimations, useNativePlayerActionTiming: true,
            items: simulation.Items);
        var expected = new DeterministicLegacyRandom(1);
        expected.GenerateRnd(100);
        var damage = expected.RandomIntBetween(128, 256);
        expected.AdvanceRndSeed();
        var life = victim.Life;
        Assert.True(processor.ResolveNativeTrapArrowPlayerHit(trap.Facts.TrapEntityId, victim, 0, 1, [victim], []));
        Assert.Equal(damage, life - victim.Life);
        Assert.Equal(expected.State, random.State);
        var monster = world.CombatTargets.Values.First(row => row.LevelId == 2 && row.NativeAi == "Zombie"
            && row.HitPointsQ6 > 512 && !row.IsUnique);
        monster.IsPetrified = true;
        expected = new(1);
        expected.GenerateRnd(100);
        damage = expected.RandomIntBetween(2, 4) * 64;
        expected.AdvanceRndSeed();
        random.Restore(1);
        var hp = monster.HitPointsQ6;
        var hit = typeof(DiabloCombatCommandProcessor).GetMethod("ResolveNativeTrapArrowMonsterHit",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)hit.Invoke(processor, [player, monster, 0, new List<DiabloGameplayEvent>()])!);
        Assert.Equal(damage, hp - monster.HitPointsQ6);
        Assert.Equal(expected.State, random.State);
    }
}
