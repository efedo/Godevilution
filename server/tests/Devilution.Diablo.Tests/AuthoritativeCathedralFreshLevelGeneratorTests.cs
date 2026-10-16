using Devilution.Data;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Focused tests for cold-storage generation in native load order. */
public sealed class AuthoritativeCathedralFreshLevelGeneratorTests
{
    [Fact]
    public void GeneratedDepthCarriesNativeNextTransparencyValue()
    {
        var result = AuthoritativeCathedralDepthGenerator.Generate(
            1, 2588, AuthoritativeCathedralEntry.Main);

        Assert.InRange(result.NextTransparencyValue, 1, 127);
        Assert.NotEqual(0, result.NextTransparencyValue);
    }

    [Fact]
    public void CreateRequestRetainsIndependentDungeonAndStorySeeds()
    {
        var metadata = SyntheticMetadata();
        var hero = Hero();
        var request = AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
            metadata, 4, 2588, 0x80000000, AuthoritativeCathedralEntry.Previous, hero);

        Assert.Equal((uint)2588, request.DungeonSeed);
        Assert.Equal(0x80000000U, request.StorySeed);
        Assert.Equal(AuthoritativeCathedralEntry.Previous, request.Entry);
        Assert.Equal(hero, request.Hero);
    }

    [Fact]
    public void InvalidHeroBoundsAreRejectedBeforeFreshGeneration()
    {
        var metadata = SyntheticMetadata();
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
                metadata, 1, 1, 2, AuthoritativeCathedralEntry.Main,
                Hero() with { MaximumLifeBase = 0 }));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
                metadata, 1, 1, 2, AuthoritativeCathedralEntry.Main,
                Hero() with { LightRadius = 1 }));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
                metadata, 1, 1, 2, AuthoritativeCathedralEntry.Main,
                Hero() with { IdleFrames = 0 }));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
                metadata, 1, 1, 2, AuthoritativeCathedralEntry.Main,
                Hero() with { PreEntryFuture = new(112, 0) }));
    }

    [Theory(Explicit = true)]
    [InlineData(1, 2588U, "main")]
    [InlineData(2, 743271966U, "previous")]
    [InlineData(3, 0x80000000U, "main")]
    [InlineData(4, 0U, "previous")]
    public void FreshGenerationDerivesGeometryTriggersPoolsAndPlayerLight(
        int depth, uint seed, string entry)
    {
        var metadata = LoadMetadata();
        var result = AuthoritativeCathedralFreshLevelGenerator.Generate(
            metadata, depth, seed, 0x12345678, ParseEntry(entry), Hero());

        Assert.Equal(112 * 112, result.PreThemePopulated.Count);
        Assert.Equal(result.PreThemePopulated,
            result.InitialPopulation.PreThemeGeometry.Populated);
        Assert.NotEmpty(result.TriggerPositions);
        Assert.Equal(15, result.InitialLight[0]);
        Assert.All(result.InitialLight, value => Assert.Equal((byte)15, value));
        Assert.All(result.InitialPreLight, value => Assert.Equal((byte)0, value));
        Assert.Equal(1, result.LightAllocator.ActiveLightCount);
        Assert.True(result.LightAllocator.UpdateLighting);
        Assert.Equal(0, result.LightAllocator.ActiveLightSlots[0]);
        var playerLight = result.LightAllocator.LightRecords[0];
        var generatedPlayer = result.InitialPopulation.ObjectTemplate.Player;
        Assert.Equal(generatedPlayer.Tile, playerLight.Tile);
        Assert.Equal(generatedPlayer.Tile, playerLight.OldTile);
        Assert.Equal(Hero().LightRadius, playerLight.Radius);
        Assert.Equal(Hero().LightRadius, playerLight.OldRadius);
        Assert.True(playerLight.HasChanged);
        Assert.False(playerLight.IsInvalid);
        Assert.Equal(seed, result.FullPopulationRequest.MonsterClearRandomState);
        Assert.Equal(result.Population.ClearedMonsterPool.FinalRandomState,
            result.MonsterClearFinalRandomState);
        Assert.Equal(200, result.Population.ClearedMonsterPool.Monsters.Count);
        Assert.Equal(128, result.FullPopulationRequest.InitialItemSlots.Count);
        Assert.Equal(127, result.FullPopulationRequest.InitialActiveItemIds.Count);
        Assert.Empty(result.FullPopulationRequest.InitialUniqueItemFlags);
        Assert.Equal(generatedPlayer.Tile, result.VisionRecords[0].Tile);
        Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 0), result.VisionRecords[0].OldTile);
        Assert.Equal(Hero().LightRadius, result.VisionRecords[0].Radius);
        Assert.Equal(0, result.VisionRecords[0].OldRadius);
        Assert.False(result.VisionRecords[0].HasChanged);
        Assert.Equal(new[] { true, false, false, false }, result.VisionActive);
        Assert.True(result.UpdateVision);
        Assert.Equal(Hero().IdleFrames, result.PlayerPreparation.IdleFrames);
        Assert.Equal(4, result.PlayerPreparation.TicksPerFrame);
        Assert.Equal(result.Population.InitialPopulation.Themes.FinalRandomState,
            result.PlayerPreparation.RandomStateBefore);
        Assert.Equal(Hero().PreEntryFuture, generatedPlayer.Future);
        Assert.True(generatedPlayer.LevelChanging);
        Assert.Equal(depth, generatedPlayer.Level);
        Assert.Equal(result.Geometry.GenerationResult.NextTransparencyValue, result.NextTransparencyValue);
        Assert.InRange(result.NextTransparencyValue, 2, 127);
    }

    [Fact]
    public void FirstEntryRequestDetachesGlobalUniqueHistoryWithoutChangingLegacyFreshRequests()
    {
        var metadata = SyntheticMetadata();
        var history = new HashSet<int> { 0, 127 };
        var entry = AuthoritativeCathedralFreshLevelGenerator.CreateEntryRequest(
            metadata, 2, 2588, 0x13579BDF, AuthoritativeCathedralEntry.Main, Hero(), history);
        history.Clear();
        history.Add(64);
        Assert.Equal(new[] { 0, 127 }, entry.InitialUniqueItemFlags.Order());
        Assert.Empty(AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
            metadata, 2, 2588, 0x13579BDF, AuthoritativeCathedralEntry.Main, Hero()).InitialUniqueItemFlags);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void FirstEntryRejectsOutOfRangeUniqueHistoryBeforeGenerating(int flag)
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralFreshLevelGenerator.CreateEntryRequest(
            SyntheticMetadata(), 2, 2588, 0x13579BDF, AuthoritativeCathedralEntry.Main, Hero(),
            new HashSet<int> { flag }));
    }

    [Fact(Explicit = true)]
    public void FirstL2EntryPreservesAlreadyGeneratedUniqueItemsInTheActualPopulation()
    {
        var request = AuthoritativeCathedralFreshLevelGenerator.CreateEntryRequest(
            LoadMetadata(), 2, 2588, 0x13579BDF, AuthoritativeCathedralEntry.Main,
            Hero() with { PreEntryFuture = new(49, 48) }, new HashSet<int> { 0, 127 });
        var generated = AuthoritativeCathedralFreshLevelGenerator.Generate(request);
        Assert.NotEmpty(generated.Population.State.ActiveMonsterIds
            .Take(generated.Population.State.ActiveMonsterCount));
        Assert.Equal(new[] { 0, 127 }, generated.FullPopulationRequest.InitialUniqueItemFlags.Order());
        Assert.Contains(0, generated.Population.State.UniqueItemFlags);
        Assert.Contains(127, generated.Population.State.UniqueItemFlags);
    }

    [Fact(Explicit = true)]
    public void ActualL2EntryRetainsAllSixEmergingSkeletonsAndItsUniqueMonster()
    {
        var metadata = LoadMetadata();
        var runtime = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
            AuthoritativeCathedralMonsterRuntimeCatalog.TableName, File.ReadAllText(
                Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_TABLE_PATH")!)));
        var request = AuthoritativeCathedralFreshLevelGenerator.CreateEntryRequest(metadata,
            2, 2588, 0x13579BDF, AuthoritativeCathedralEntry.Main,
            new(2880, 1440, 8, 8, new(49, 48), true), new HashSet<int>());
        var state = AuthoritativeCathedralFreshLevelGenerator.Generate(request).Population.State;
        var bindings = state.ActiveMonsterIds.Take(state.ActiveMonsterCount).Select(slot =>
            new AuthoritativeCathedralMonsterRuntimeBinding(slot, (uint)(0x09011000 + slot),
                (uint)(0x10000 + state.MonsterSlots[slot].Type), 2)).ToArray();
        var fingerprint = AuthoritativeCathedralMonsterRuntimeAdapter.ComputeMetadataFingerprint(
            metadata.Fingerprint, metadata.UniqueMonsters.Fingerprint, runtime);
        var imported = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptLevelEntryWithBindings(
            state, runtime, fingerprint, bindings, metadata.Population.Monsters,
            uniqueDefinitions: metadata.UniqueMonsters.Definitions,
            uniqueIdentities: metadata.UniqueMonsters.Definitions.ToDictionary(row => row.NativeUniqueId,
                row => "native-cathedral-unique-" + row.NativeUniqueId.ToString("D3")));
        Assert.Equal(152, imported.Records.Count);
        Assert.Equal(144, imported.Records.Count(row => row.LiveTargetCreated));
        Assert.Equal(new[] { 122, 123, 124, 125, 126, 127 },
            imported.InitialSpecialStandActors.Select(row => row.NativeSlot));
        foreach (var phase in imported.InitialSpecialStandActors) {
            var original = state.MonsterSlots[phase.NativeSlot];
            var target = Assert.IsType<AuthoritativeCombatTarget>(imported.Records.Single(
                row => row.Binding.NativeSlot == phase.NativeSlot).Target);
            Assert.Equal(original, target.NativeInitialization!.InitialState);
            Assert.Equal(11, original.Mode);
            Assert.Equal(original.AnimCurrentFrame, phase.FrameIndex);
            Assert.Equal(original.AnimTickCounter, phase.TickCounter);
            Assert.Equal(original.AnimNumberOfFrames, phase.FrameCount);
            Assert.Equal(original.AnimTicksPerFrame, phase.TicksPerFrame);
        }
        Assert.True(imported.Records.Single(row => row.Binding.NativeSlot == 7).Target!.IsUnique);
        Assert.All(imported.Records.Where(row => !row.LiveTargetCreated), row =>
            Assert.Contains(row.UnsupportedReason, new[] {
                AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DormantGolem,
                AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton }));
    }

    private static AuthoritativeCathedralFreshLevelHeroContext Hero()
        => new(6400, 3200, 8, 8, new(75, 68), true);

    [Fact]
    public void HeroEntryContextIsExplicitAndBoundedWithoutOriginalAssets()
    {
        var metadata = SyntheticMetadata();
        foreach (var hero in new[] {
            Hero() with { MaximumManaBase = -1 }, Hero() with { LightRadius = 16 },
            Hero() with { IdleFrames = 128 }, Hero() with { PreEntryFuture = new(-1, 68) },
            Hero() with { LevelLoading = false },
        }) Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralFreshLevelGenerator.CreateRequest(
            metadata, 1, 0, 1, AuthoritativeCathedralEntry.Main, hero));
        foreach (var changing in new[] { false, true }) {
            var hero = Hero() with { LevelChanging = changing, PreEntryFuture = new(0, 111), IdleFrames = 1, LightRadius = 2 };
            var request = AuthoritativeCathedralFreshLevelGenerator.CreateRequest(metadata, 1, 0, 1,
                AuthoritativeCathedralEntry.Main, hero);
            Assert.Equal(hero, request.Hero);
        }
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralFreshLevelGenerator.CreateRequest(metadata,
            1, 0, 1, (AuthoritativeCathedralEntry)99, Hero()));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralFreshLevelGenerator.CreateRequest(metadata,
            1, 0, 1, AuthoritativeCathedralEntry.Main, Hero(), maximumPlacementAttempts: 0));
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralFreshLevelGenerator.CreateRequest(metadata,
            1, 0, 1, AuthoritativeCathedralEntry.Main, Hero(), stableLevelSymbol: ""));
    }

    [Fact]
    public void MaterializersPreserveCounterWithoutAliasingCallerLayers()
    {
        var metadata = SyntheticMetadata();
        var generated = AuthoritativeCathedralDepthGenerator.Generate(2, 2588, AuthoritativeCathedralEntry.Main);
        var coarse = generated.MegaTiles.ToArray();
        var transparency = generated.NativeTransparency.ToArray();
        var owned = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(2,
            generated with { MegaTiles = coarse, NativeTransparency = transparency }, metadata.Geometry.Pieces,
            metadata.Geometry.TileProperties, "counter-proof");
        Assert.Equal(generated.NextTransparencyValue, owned.GenerationResult.NextTransparencyValue);
        Assert.InRange(owned.GenerationResult.NextTransparencyValue, 2, 127);
        Array.Clear(coarse); Array.Clear(transparency);
        Assert.Equal(generated.MegaTiles, owned.GenerationResult.MegaTiles);
        Assert.Equal(generated.NativeTransparency, owned.GenerationResult.NativeTransparency);
    }

    private static AuthoritativeCathedralPopulationMetadata SyntheticMetadata()
        => AuthoritativeCathedralPopulationMetadata.Load(new ContentPack("test.cathedral-fresh", "1.0.0",
            CathedralPopulationMetadataCompilerTests.CreateCompleteTables(),
            schema: new ContentSchemaIdentity(AuthoritativeCathedralPopulationMetadata.SchemaId,
                AuthoritativeCathedralPopulationMetadata.SchemaVersion)));

    private static AuthoritativeCathedralEntry ParseEntry(string value)
        => value == "main" ? AuthoritativeCathedralEntry.Main : AuthoritativeCathedralEntry.Previous;

    private static AuthoritativeCathedralPopulationMetadata LoadMetadata()
        => CathedralPopulationMetadataCompiler.LoadDirectory(
            AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
                "GODEVILUTION_CATHEDRAL_POPULATION_METADATA_PACK_DIRECTORY"));
}
