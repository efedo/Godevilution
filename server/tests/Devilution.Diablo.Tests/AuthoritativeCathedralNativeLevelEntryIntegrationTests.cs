using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeProgressionStairDryRunAndFreshCohortRetainBothFloorOwners()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        if (setup is null) return;
        var activation = DiabloModule.Instance.CathedralNativeProgressionContentBootstrap
            .ActivateNativeProgression(setup.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var stair = world.World!.WarpTargets.Single(row => row.SourceLevelId == 1 && row.TargetLevelId == 2);
        var template = activation.PlayerLoadouts.CreateTemplate("native-two-floor", rogue.ClassId,
            null, null, null, startingLevelId: 1,
            startingPositionX: stair.TriggerX, startingPositionY: stair.TriggerY);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("native-two-floor", player => player.EntityId = 0x70000001);
        var player = runtime.Read("native-two-floor", value => value);
        var commands = (DiabloWorldCommandProcessor)typeof(DiabloSimulation)
            .GetField("worldCommands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulation)!;
        var codec = new Devilution.Server.Stores.DiabloCohortCodec();
        var before = codec.Serialize(simulation.CaptureCohortState(runtime));
        Assert.True(commands.ExecuteLevelTrigger(player, 0, [player], applyEffects: false).Succeeded);
        Assert.Equal(before, codec.Serialize(simulation.CaptureCohortState(runtime)));
        Assert.False(world.OwnsNativePopulationLevel(2));
        Assert.True(commands.ExecuteLevelTrigger(player, 0, [player]).Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.True(world.OwnsNativePopulationLevel(1));
        Assert.True(world.OwnsNativePopulationLevel(2));
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeProgressionFormatVersion, captured.FormatVersion);
        var entry = Assert.Single(captured.NativeProgression!.Entries);
        Assert.Equal(new AuthoritativeCathedralPopulationPoint(stair.TriggerX, stair.TriggerY), entry.Hero.PreEntryFuture);
        Assert.Equal(6, captured.NativeProgression.InitialSpecialStand.Count);
        var bytes = codec.Serialize(captured);
        var decoded = codec.Deserialize(bytes);
        Assert.Throws<InvalidDataException>(() => codec.Serialize(captured with {
            FormatVersion = DiabloCohortState.NativeTrapFormatVersion,
        }));
        Assert.Throws<InvalidDataException>(() => activation.CohortCandidateFactory!(captured with {
            NativeProgression = captured.NativeProgression with {
                Entries = [entry with { Hero = entry.Hero with { MaximumLifeBase = entry.Hero.MaximumLifeBase + 64 } }],
            },
        }));
        var difficulty = typeof(DiabloSimulationOptions).GetProperty(nameof(DiabloSimulationOptions.Difficulty))!;
        try {
            difficulty.SetValue(activation.TrustedOptions!, DiabloDifficulty.Hell);
            Assert.Throws<InvalidDataException>(() => activation.CohortCandidateFactory!(decoded));
        } finally {
            difficulty.SetValue(activation.TrustedOptions!, DiabloDifficulty.Normal);
        }
        var candidate = activation.CohortCandidateFactory!(decoded);
        var restoredRuntime = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        var l1Actors = System.Text.Json.JsonSerializer.Serialize(captured.CombatTargets.Where(row => row.Monster.LevelId == 1));
        runtime.AdvanceTo(1);
        restoredRuntime.AdvanceTo(1);
        var advanced = simulation.CaptureCohortState(runtime);
        var unique = world.CombatTargets.Values.Single(target => target.LevelId == 2 && target.IsUnique);
        var itemPoolBefore = System.Text.Json.JsonSerializer.Serialize(world.CaptureCathedralItems());
        var uniqueLoot = world.PrepareCathedralDeathLoot(unique, player, [player]);
        Assert.NotNull(uniqueLoot);
        Assert.True(uniqueLoot!.Loot.Dropped);
        Assert.True(uniqueLoot.RequiresWorldItem);
        Assert.Equal(uniqueLoot.Loot.SelectedNativeItemIndex, uniqueLoot.Projection!.Raw.ItemIndex);
        Assert.Equal(uniqueLoot.Projection.Binding.StableItemId, uniqueLoot.Projection.State.StableItemId);
        Assert.Equal(itemPoolBefore, System.Text.Json.JsonSerializer.Serialize(world.CaptureCathedralItems()));
        var advancedBytes = codec.Serialize(advanced);
        Assert.Equal(advancedBytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restoredRuntime)));
        Assert.Equal(l1Actors, System.Text.Json.JsonSerializer.Serialize(advanced.CombatTargets.Where(row => row.Monster.LevelId == 1)));
        Assert.NotEqual(System.Text.Json.JsonSerializer.Serialize(captured.NativeProgression.InitialSpecialStand),
            System.Text.Json.JsonSerializer.Serialize(advanced.NativeProgression!.InitialSpecialStand));
        var secondCold = activation.CohortCandidateFactory!(codec.Deserialize(advancedBytes));
        var secondRuntime = secondCold.CreateRuntime();
        Assert.Equal(advancedBytes, codec.Serialize(secondCold.Simulation.CaptureCohortState(secondRuntime)));
    }

    [Fact(Explicit = true)]
    public void FirstL2OwnerBindsCompleteNativePopulationWithoutCollidingWithL1()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: true, progressionSemantics: true);
        if (setup is null) return;
        var options = setup.Factory!.CapturedOptions!;
        var activation = setup.Activation;
        var metadata = setup.Fixture.PopulationMetadata;
        var identity = options.CathedralDepthWorldIdentity!.Levels.Single(row => row.LevelId == 2);
        var original = options.World!.Levels.Single(row => row.LevelId == 2);
        var coarse = AuthoritativeCathedralDepthGenerator.Generate(2, identity.DungeonSeed,
            identity.Entry, identity.Policy);
        var materialized = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(2, coarse,
            metadata.Geometry.Pieces, metadata.Geometry.TileProperties, identity.LevelSymbol);
        var routes = AuthoritativeCathedralLevel1RuntimeCatalogDefinition.FindRoutes(materialized.Level,
            new(coarse.StairsUp.X * 2 + 19, coarse.StairsUp.Y * 2 + 20),
            new(coarse.StairsDown.X * 2 + 19, coarse.StairsDown.Y * 2 + 19));
        var candidate = new AuthoritativeCathedralDepthGenerationCandidate(original,
            materialized.Level, coarse, routes, identity);
        var runtime = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
            AuthoritativeCathedralMonsterRuntimeCatalog.TableName, File.ReadAllText(
                Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH")!)));
        Assert.True(setup.Fixture.Content.TryGetTable("native_cathedral_runtime/native-rogue-l1-inventory.tsv", out var footprints));
        Assert.True(setup.Fixture.Content.TryGetTable("native_cathedral_runtime/native_item_semantics.tsv", out var semantics));
        var footprintTable = footprints with { SourcePath = "native-rogue-l1-inventory.tsv" };
        var semanticTable = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
            metadata.Items.Catalog, options.Items!, options.Spells!,
            AuthoritativeCathedralNativeItemSidecars.LoadFootprints(footprintTable), includeNativeJewelry: true);
        var sidecars = AuthoritativeCathedralNativeItemSidecars.Load(footprintTable, semanticTable);
        var effectiveItems = AuthoritativeCathedralNativePopulationBuilder.ApplyItemSidecars(
            metadata.Items.Catalog, options.Items!, options.Spells!, sidecars);
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        var player = activation.PlayerLoadouts.CreateTemplate("first-l2-entry", rogue.ClassId,
            null, null, null, startingLevelId: 1, startingPositionX: 49, startingPositionY: 48);
        var carried = player.Inventory.Select(row => row.State).Concat(player.Equipment.Select(row => row.State))
            .Concat(player.Belt.Select(row => row.State)).ToArray();
        var request = AuthoritativeCathedralFreshLevelGenerator.CreateEntryRequest(metadata, 2,
            identity.DungeonSeed, 0x13579BDF, identity.Entry,
            new(player.BaseLifeMaximum, player.BaseManaMaximum, 8,
                activation.PlayerAnimations.GetRequired(rogue.ClassId).IdleFrames,
                new(player.PositionX, player.PositionY), true), new HashSet<int>(), identity.LevelSymbol);
        var built = AuthoritativeCathedralNativePopulationBuilder.Build(metadata, runtime, candidate,
            activation.PlayerClasses, activation.PlayerAnimations, activation.PlayerLoadouts,
            options.MonsterTypes!, effectiveItems, options.Spells!,
            options.CathedralDoors!.Definitions.Single() with { LevelId = 2 },
            options.StoreCatalog.CaptureAll().SelectMany(store => store.Items!).Select(row => row.State).ToArray(),
            sidecars, entryRequest: request, carriedItems: carried);
        Assert.Equal(2U, built.Policy.LevelId);
        Assert.Equal(2, built.Policy.Depth);
        Assert.Equal(152, built.Monsters.Records.Count);
        Assert.Equal(144, built.Monsters.Records.Count(row => row.LiveTargetCreated));
        Assert.Equal(6, built.Policy.InitialSpecialStandActors.Count);
        Assert.Single(built.Monsters.Records, row => row.Target?.IsUnique == true);
        Assert.Equal(102, built.Objects.Count);
        Assert.Equal(21, built.Items.InitialCheckpoint.Levels.Single().ActiveItemCount);
        var l1Ids = options.NativePopulationPolicy!.BoundEntityIds.ToHashSet();
        Assert.All(built.Policy.BoundEntityIds, entity => Assert.DoesNotContain(entity, l1Ids));
        Assert.NotEqual(options.NativePopulationPolicy.Fingerprint, built.Policy.Fingerprint);
        _ = new AuthoritativeCathedralItemRuntime(built.Items);
        built.Policy.ValidateItemProfile(built.Items);

        var world = activation.Simulation.WorldState;
        var l1Items = System.Text.Json.JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single());
        var l1Barrels = world.CaptureNativeBarrels()!.ToArray();
        var l1Sarcophagi = world.CaptureNativeSarcophagi()!.ToArray();
        var l1Traps = world.CaptureNativeTraps()!.ToArray();
        var admission = world.PrepareNativeFloorAdmission(built,
            options.CathedralDoors.Definitions.Single() with { LevelId = 2 }, effectiveItems, options.Spells!, []);
        Assert.False(world.OwnsNativePopulationLevel(2));
        var beforeRefusal = System.Text.Json.JsonSerializer.Serialize(new {
            Geometry = world.CaptureCohortInfrastructure(),
            Objects = world.Objects.Values.Select(DiabloSimulation.CaptureObject).ToArray(),
            Actors = world.CombatTargets.Values.Select(value => value.CaptureCohortState()).ToArray(),
            Items = world.CaptureCathedralItems(), Sarcophagi = world.CaptureNativeSarcophagi(),
            Barrels = world.CaptureNativeBarrels(), Traps = world.CaptureNativeTraps(),
        });
        var foreign = world.CombatTargets.Values.First();
        admission.Floor.CombatTargets.Add(foreign.EntityId, foreign);
        Assert.Throws<InvalidOperationException>(() => world.CommitNativeFloorAdmission(admission));
        Assert.Equal(beforeRefusal, System.Text.Json.JsonSerializer.Serialize(new {
            Geometry = world.CaptureCohortInfrastructure(),
            Objects = world.Objects.Values.Select(DiabloSimulation.CaptureObject).ToArray(),
            Actors = world.CombatTargets.Values.Select(value => value.CaptureCohortState()).ToArray(),
            Items = world.CaptureCathedralItems(), Sarcophagi = world.CaptureNativeSarcophagi(),
            Barrels = world.CaptureNativeBarrels(), Traps = world.CaptureNativeTraps(),
        }));
        admission.Floor.CombatTargets.Remove(foreign.EntityId);
        var poisoned = world.PrepareNativeFloorAdmission(built,
            options.CathedralDoors.Definitions.Single() with { LevelId = 2 }, effectiveItems, options.Spells!, []);
        var removed = poisoned.Floor.WorldItems.Values.First();
        Assert.True(poisoned.Items.RemoveWorldItem(removed.LevelId, removed.EntityId, out _));
        Assert.Throws<InvalidOperationException>(() => world.CommitNativeFloorAdmission(poisoned));
        Assert.False(world.OwnsNativePopulationLevel(2));
        Assert.Equal(l1Items, System.Text.Json.JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single()));
        world.CommitNativeFloorAdmission(admission);
        Assert.True(world.OwnsNativePopulationLevel(1));
        Assert.True(world.OwnsNativePopulationLevel(2));
        world.ValidateNativePopulationState([]);
        Assert.Equal(l1Items, System.Text.Json.JsonSerializer.Serialize(world.CaptureCathedralItems()!.Levels.Single(row => row.LevelId == 1)));
        Assert.Equal(l1Barrels, world.CaptureNativeBarrels()!.Where(row => l1Barrels.Any(old => old.EntityId == row.EntityId)));
        Assert.Equal(l1Sarcophagi, world.CaptureNativeSarcophagi()!.Where(row => l1Sarcophagi.Any(old => old.EntityId == row.EntityId)));
        Assert.Equal(l1Traps, world.CaptureNativeTraps()!.Where(row => row.Facts.LevelId == 1));
        Assert.Equal(built.Policy.InitialSpecialStandActors, world.CaptureNativeInitialSpecialStand());
        Assert.Throws<InvalidOperationException>(() => world.CommitNativeFloorAdmission(admission));
    }
}
