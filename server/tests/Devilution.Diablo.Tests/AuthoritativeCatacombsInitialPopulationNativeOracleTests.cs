using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Explicit, archive-free comparisons of the managed Catacombs initial-population
 * phases against the separately captured native v2 stages. Expected worlds are
 * comparison-only; generator, metadata, and prepared-player inputs are rebuilt
 * from qualified source tables and the fixed case matrix.
 */
public sealed class AuthoritativeCatacombsInitialPopulationNativeOracleTests
{
	private const string ReferenceDirectoryVariable = "GODEVILUTION_NATIVE_CATACOMBS_POPULATION_REFERENCE_DIRECTORY";
	private const string SourceMetadataVariable = "GODEVILUTION_NATIVE_CATACOMBS_SOURCE_METADATA";
	private const string GlobalMetadataVariable = "GODEVILUTION_NATIVE_CATACOMBS_GLOBAL_METADATA_DIRECTORY";
	private const string StorySourceVariable = "GODEVILUTION_NATIVE_CATACOMBS_STORY_SOURCE_TABLE";
	private const string OracleFormat = "godevilution-native-catacombs-population";
	private const string CaseFormat = "godevilution-catacombs-population-case";
	private const string Policy = "quest-disabled-original-diablo-single-player-normal";
	private const uint StorySeed = 0x1357_9BDF;
	private const int Width = AuthoritativeCathedralPopulationGeometry.Width;
	private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
	private const int MaximumManifestBytes = 64 * 1024;
	private const int MaximumCaseBytes = 12 * 1024 * 1024;

	private static readonly JsonSerializerOptions JsonOptions = new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};
	private static readonly IReadOnlyList<CaseSpec> CaseMatrix = BuildCaseMatrix();
	private static readonly Lazy<OracleCorpus> LoadedCorpus = new(LoadCorpus);

	public static TheoryData<string, string, int, int, string, uint, bool> Cases
	{
		get {
			var data = new TheoryData<string, string, int, int, string, uint, bool>();
			foreach (var row in CaseMatrix)
				data.Add(row.File, row.CharacterClass, row.CharacterClassId, row.Depth, row.Entry, row.Seed, row.RetainedUniqueHistory);
			return data;
		}
	}

	[Theory(Explicit = true)]
	[MemberData(nameof(Cases))]
	public void CatacombsInitialPopulationMatchesNativeStages(
		string file, string characterClass, int characterClassId, int depth, string entry, uint seed, bool retainedUniqueHistory)
	{
		var spec = CaseMatrix.Single(row => row.File == file);
		Assert.Equal(spec, new CaseSpec(file, characterClass, characterClassId, depth, entry, seed, retainedUniqueHistory));
		var corpus = LoadedCorpus.Value;
		Assert.Equal(32, corpus.ManifestCases.Count);
		var manifestCase = corpus.ManifestCases.Single(row => row.File == file);
		Assert.Equal(characterClass, manifestCase.CharacterClass);
		Assert.Equal(characterClassId, manifestCase.CharacterClassId);
		Assert.Equal(depth, manifestCase.LevelId);
		Assert.Equal(entry, manifestCase.Entry);
		Assert.Equal(seed, manifestCase.InputSeed);
		Assert.Equal(retainedUniqueHistory, manifestCase.RetainedUniqueHistory);

		using var document = JsonDocument.Parse(ReadBoundedRegularFile(
			Path.Combine(corpus.ReferenceDirectory, file), MaximumCaseBytes));
		var root = document.RootElement;
		Assert.Equal(CaseFormat, RequiredString(root, "format"));
		Assert.Equal(2, RequiredInt(root, "version"));
		Assert.Equal(Policy, RequiredString(root, "policy"));
		Assert.Equal(characterClass, RequiredString(root, "characterClass"));
		Assert.Equal(characterClassId, RequiredInt(root, "characterClassId"));
		Assert.Equal(depth, RequiredInt(root, "levelId"));
		Assert.Equal(entry, RequiredString(root, "entry"));
		Assert.Equal(seed, RequiredUInt(root, "inputSeed"));
		Assert.Equal(StorySeed, RequiredUInt(root, "storySeed"));
		Assert.Equal(retainedUniqueHistory, RequiredBool(root, "retainedUniqueHistory"));
		Assert.Equal("post-InitObjects,post-InitMonsters,post-InitItems", corpus.CapturedStages);
		if (file.Contains("ai-eligibility-coverage", StringComparison.Ordinal))
			Assert.Equal("native-ai-eligibility-coverage", RequiredString(root, "casePurpose"));

		var generation = AuthoritativeCatacombsGenerator.Generate(depth, seed, ToEntry(entry));
		Assert.Equal(seed, generation.InputSeed);
		Assert.Equal(RequiredUInt(root, "attemptSeed"), generation.AttemptSeed);
		var rngBoundaries = RequiredObject(root, "rngBoundaries");
		Assert.Equal(RequiredUInt(rngBoundaries, "afterCreateLevel"), generation.FinalRandomState);
		var clearedMonsterPool = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(seed);
		Assert.Equal(RequiredUInt(rngBoundaries, "afterPopulationReset"), clearedMonsterPool.FinalRandomState);
		Assert.Equal(clearedMonsterPool.FinalRandomState, RequiredUInt(rngBoundaries, "beforeCreateLevel"));
		var materialized = AuthoritativeCatacombsGeometryMaterializer.Materialize(
			checked((uint)depth), generation, corpus.Metadata.Geometry.Pieces,
			corpus.Metadata.EffectivePieceProperties, $"native-catacombs-l{depth}");
		var worldBefore = RequiredObject(root, "worldBeforePopulation");
		var initialPopulated = DeriveTriggerPopulation(materialized.Triggers);
		CompareMaterializedGeometry(worldBefore, materialized, generation, corpus.Metadata, initialPopulated);

		var playerInput = RequiredObject(root, "playerGenerationInput");
		var preEntry = RequiredObject(root, "playerPreEntryContext");
		var future = ReadPoint(RequiredObject(preEntry, "future"));
		var player = new AuthoritativeCathedralPopulationPlayer(
			0,
			depth,
			RequiredBool(playerInput, "active"),
			RequiredBool(playerInput, "levelChanging"),
			new(generation.EntryPosition.X, generation.EntryPosition.Y),
			future);
		var trapPercent = corpus.Metadata.DepthRules.Single(rule => rule.Depth == depth).TrapPercent;
		var geometry = new AuthoritativeCathedralPopulationGeometry(
			materialized.Level.NativePieceLayer,
			materialized.Level.NativeSpecialLayer.Select(value => checked((byte)value)).ToArray(),
			generation.NativeTransparency,
			corpus.Metadata.EffectivePieceProperties,
			initialPopulated,
			new bool[CellCount],
			new bool[CellCount]);
		var parameters = new AuthoritativeCatacombsInitialPopulationParameters(
			depth,
			seed,
			RequiredUInt(root, "storySeed"),
			generation,
			geometry,
			corpus.Metadata.Population.Monsters,
			corpus.Metadata.Population.Objects,
			player,
			trapPercent,
			corpus.Metadata.StoryMessages);
		var actual = AuthoritativeCatacombsInitialPopulationGenerator.Generate(parameters);

		CompareBestiary(root, corpus.Metadata, actual);
		CompareThemes(root, worldBefore, generation, actual, initialPopulated);
		CompareObjectStage(root, worldBefore, actual, corpus.Metadata, clearedMonsterPool.Monsters);
	}

	/** Shared corpus qualification for complete-phase tests; native worlds remain comparison-only. */
	internal static (string Directory, AuthoritativeCatacombsPopulationMetadata Metadata) QualifiedInputs()
		=> (LoadedCorpus.Value.ReferenceDirectory, LoadedCorpus.Value.Metadata);

	internal static JsonDocument OpenQualifiedCase(string file)
	{
		if (!CaseMatrix.Any(row => row.File == file))
			throw new InvalidDataException("A fixed Catacombs oracle matrix filename is required.");
		return JsonDocument.Parse(ReadBoundedRegularFile(
			Path.Combine(LoadedCorpus.Value.ReferenceDirectory, file), MaximumCaseBytes));
	}

	internal static JsonDocument OpenQualifiedSource()
		=> JsonDocument.Parse(ReadBoundedRegularFile(
			RequiredFile(SourceMetadataVariable, 2 * 1024 * 1024), 2 * 1024 * 1024));

	private static void CompareMaterializedGeometry(
		JsonElement worldBefore,
		AuthoritativeCatacombsGeometry materialized,
		AuthoritativeCatacombsGenerationResult generation,
		AuthoritativeCatacombsPopulationMetadata metadata,
		IReadOnlyList<bool> initialPopulated)
	{
		var tiles = RequiredArray(worldBefore, "tiles");
		Assert.Equal(CellCount, tiles.GetArrayLength());
		Assert.Equal(AuthoritativeCatacombsGenerationResult.FineWidth * AuthoritativeCatacombsGenerationResult.FineHeight,
			generation.NativeTileFlags.Count);
		Assert.All(generation.NativeTileFlags, value => Assert.Equal((byte)0, value));
		for (var index = 0; index < CellCount; index++) {
			var tile = tiles[index];
			Assert.Equal(11, tile.GetArrayLength());
			Assert.Equal((int)materialized.Level.NativePieceLayer[index], tile[0].GetInt32());
			Assert.Equal((int)materialized.Level.NativeSpecialLayer[index], tile[1].GetInt32());
			Assert.Equal((int)unchecked((sbyte)generation.NativeTransparency[index]), tile[2].GetInt32());
			Assert.Equal(initialPopulated[index] ? 8 : 0, tile[3].GetInt32());
			for (var channel = 4; channel <= 8; channel++)
				Assert.Equal(0, tile[channel].GetInt32());
			Assert.Equal(15, tile[9].GetInt32());
			Assert.Equal(0, tile[10].GetInt32());
		}
		CompareByteArray(RequiredArray(worldBefore, "sol"), metadata.EffectivePieceProperties,
			"worldBeforePopulation.sol");
		Assert.Equal(0, RequiredInt(worldBefore, "activeObjectCount"));
		Assert.Equal(0, RequiredInt(worldBefore, "activeMonsterCount"));
		Assert.Equal(0, RequiredInt(worldBefore, "activeItemCount"));
	}

	private static void CompareBestiary(
		JsonElement root,
		AuthoritativeCatacombsPopulationMetadata metadata,
		AuthoritativeCatacombsInitialPopulationResult actual)
	{
		var expected = RequiredArray(root, "bestiary");
		Assert.Equal(actual.Bestiary.Entries.Count, expected.GetArrayLength());
		Assert.Equal(RequiredUInt(RequiredObject(root, "rngBoundaries"), "afterGetLevelMTypes"),
			actual.Bestiary.FinalRandomState);
		for (var index = 0; index < expected.GetArrayLength(); index++) {
			var row = expected[index];
			var selected = actual.Bestiary.Entries[index];
			var raw = metadata.Population.Monsters.Single(value => value.NativeMonsterId == selected.NativeType);
			Assert.Equal(index, RequiredInt(row, "index"));
			Assert.Equal(selected.Index, RequiredInt(row, "index"));
			Assert.Equal(selected.NativeType, RequiredInt(row, "monsterType"));
			Assert.Equal(selected.PlaceFlags, RequiredInt(row, "placeFlags"));
			Assert.Equal(selected.CorpseId, RequiredInt(row, "corpseId"));
			Assert.Equal(raw.Image, RequiredInt(row, "imageCost"));
			Assert.Equal(raw.Ai, RequiredInt(row, "nativeAi"));
			Assert.Equal((int)raw.Availability, RequiredInt(row, "availability"));
			Assert.Equal(raw.MinDungeonLevel, RequiredInt(row, "minimumDungeonLevel"));
			Assert.Equal(raw.MaxDungeonLevel, RequiredInt(row, "maximumDungeonLevel"));
			CompareIntArray(RequiredArray(row, "sourceFrames"), raw.Frames, $"bestiary[{index}].sourceFrames");
			CompareIntArray(RequiredArray(row, "sourceRates"), raw.Rates, $"bestiary[{index}].sourceRates");
			var animations = RequiredArray(row, "animations");
			Assert.Equal(6, animations.GetArrayLength());
			for (var animation = 0; animation < 6; animation++) {
				var expectedAnimation = animations[animation];
				Assert.Equal(3, expectedAnimation.GetArrayLength());
				Assert.Equal(selected.Widths[animation], expectedAnimation[0].GetInt32());
				Assert.Equal(selected.Frames[animation], expectedAnimation[1].GetInt32());
				Assert.Equal(selected.Rates[animation], expectedAnimation[2].GetInt32());
			}
		}
	}

	private static void CompareThemes(
		JsonElement root,
		JsonElement worldBefore,
		AuthoritativeCatacombsGenerationResult generation,
		AuthoritativeCatacombsInitialPopulationResult actual,
		IReadOnlyList<bool> initialPopulated)
	{
		var expected = RequiredObject(root, "themesBeforePopulation");
		var selected = RequiredArray(expected, "selected");
		Assert.Equal(actual.Themes.Themes.Count, selected.GetArrayLength());
		Assert.Equal(actual.SelectedThemes.Count, generation.Themes.Count);
		Assert.Equal(generation.Themes.Count, RequiredInt(expected, "roomCount"));
		for (var index = 0; index < selected.GetArrayLength(); index++) {
			var pair = selected[index];
			Assert.Equal(2, pair.GetArrayLength());
			var theme = actual.Themes.Themes[index];
			Assert.Equal(theme.ThemeType, pair[0].GetInt32());
			Assert.Equal(theme.RegionId, pair[1].GetInt32());
			Assert.Equal(theme.ThemeType, actual.SelectedThemes[index].ThemeType);
			Assert.Equal(generation.Themes[index], actual.SelectedThemes[index].Room);
		}

		var rooms = RequiredArray(expected, "rooms");
		Assert.Equal(generation.Themes.Count, rooms.GetArrayLength());
		for (var index = 0; index < rooms.GetArrayLength(); index++) {
			var row = rooms[index];
			var position = ReadPoint(RequiredObject(row, "position"));
			var size = RequiredArray(row, "size");
			Assert.Equal(2, size.GetArrayLength());
			var room = generation.Themes[index];
			Assert.Equal(room.X, position.X);
			Assert.Equal(room.Y, position.Y);
			Assert.Equal(room.Width, size[0].GetInt32());
			Assert.Equal(room.Height, size[1].GetInt32());
			Assert.Equal(room.Transparency, RequiredInt(row, "transparency"));
		}

		Assert.Equal(RequiredUInt(RequiredObject(root, "rngBoundaries"), "afterInitThemes"),
			actual.Themes.FinalRandomState);
		Assert.Equal(actual.Themes.PlacementOrigin.X, RequiredInt(worldBefore, "themeX"));
		Assert.Equal(actual.Themes.PlacementOrigin.Y, RequiredInt(worldBefore, "themeY"));
		Assert.Equal(actual.Themes.PlacementVariant, RequiredInt(worldBefore, "themeVar1"));
		var afterHold = RequiredObject(root, "rngAfterPopulationStages");
		Assert.Equal(RequiredUInt(root, "inputSeed"), RequiredUInt(afterHold, "afterHoldThemeRooms"));
		Assert.Equal(RequiredUInt(worldBefore, "randomState"), RequiredUInt(afterHold, "afterHoldThemeRooms"));
		CompareExactJson(RequiredObject(worldBefore, "flags"),
			JsonSerializer.SerializeToElement(actual.Themes.Flags, JsonOptions), "initial native theme flags");
		CompareExactJson(RequiredObject(RequiredObject(root, "worldAfterInitialObjects"), "flags"),
			JsonSerializer.SerializeToElement(actual.Themes.Flags, JsonOptions), "post-object native theme flags");

		var beforeTiles = RequiredArray(worldBefore, "tiles");
		var afterTiles = RequiredArray(RequiredObject(root, "worldAfterInitialObjects"), "tiles");
		Assert.Equal(CellCount, actual.Themes.HeldPopulatedCells.Count);
		for (var index = 0; index < CellCount; index++) {
			Assert.Equal(initialPopulated[index] ? 8 : 0, beforeTiles[index][3].GetInt32());
			Assert.Equal(actual.Themes.HeldPopulatedCells[index] ? 8 : 0, afterTiles[index][3].GetInt32());
		}
	}

	private static void CompareObjectStage(
		JsonElement root,
		JsonElement worldBefore,
		AuthoritativeCatacombsInitialPopulationResult actual,
		AuthoritativeCatacombsPopulationMetadata metadata,
		IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> clearedMonsterSlots)
	{
		var worldAfter = RequiredObject(root, "worldAfterInitialObjects");
		var population = actual.Population;
		var stages = RequiredObject(root, "rngAfterPopulationStages");
		Assert.Equal(RequiredUInt(stages, "afterInitGolems"), population.RandomAfterGolems);
		Assert.Equal(RequiredUInt(stages, "afterInitObjects"), population.FinalRandomState);
		Assert.Equal(RequiredUInt(worldAfter, "randomState"), population.FinalRandomState);
		Assert.Equal(RequiredUInt(root, "inputSeed"), RequiredUInt(worldBefore, "randomState"));
		Assert.Equal(population.ActiveObjectCount, RequiredInt(worldAfter, "activeObjectCount"));
		CompareIntArray(RequiredArray(worldAfter, "activeObjectIds"), population.ActiveObjectIds, "activeObjectIds");
		CompareIntArray(RequiredArray(worldAfter, "availableObjectIds"), population.AvailableObjectIds, "availableObjectIds");
		CompareIntArray(JsonSerializer.SerializeToElement(RequiredArray(worldAfter, "tiles").EnumerateArray().Select(tile => tile[7].GetInt32()).ToArray()),
			population.TileObjectRefs, "tile object reference channel");

		var expectedObjects = RequiredArray(worldAfter, "objects");
		Assert.Equal(AuthoritativeCathedralPopulationParameters.ObjectCapacity, expectedObjects.GetArrayLength());
		Assert.Equal(expectedObjects.GetArrayLength(), population.Objects.Count);
		for (var slot = 0; slot < expectedObjects.GetArrayLength(); slot++)
			CompareExactJson(expectedObjects[slot], JsonSerializer.SerializeToElement(population.Objects[slot], JsonOptions),
				$"initial object slot {slot}");

		var beforeMonsters = RequiredArray(worldBefore, "monsters");
		var expectedMonsters = RequiredArray(worldAfter, "monsters");
		Assert.Equal(AuthoritativeCathedralPopulationParameters.MonsterCapacity, expectedMonsters.GetArrayLength());
		Assert.Equal(beforeMonsters.GetArrayLength(), expectedMonsters.GetArrayLength());
		Assert.Equal(expectedMonsters.GetArrayLength(), clearedMonsterSlots.Count);
		Assert.Equal(0, RequiredInt(worldBefore, "activeMonsterCount"));
		var nativeSlotIds = Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity).ToArray();
		CompareIntArray(RequiredArray(worldBefore, "activeMonsterIds"), nativeSlotIds, "cleared activeMonsterIds");
		for (var slot = 0; slot < beforeMonsters.GetArrayLength(); slot++)
			CompareExactJson(beforeMonsters[slot], JsonSerializer.SerializeToElement(clearedMonsterSlots[slot], JsonOptions),
				$"cleared monster slot {slot}");

		var activeMonsterCount = RequiredInt(worldAfter, "activeMonsterCount");
		Assert.Equal(population.Monsters.Count, activeMonsterCount);
		var expectedActiveIds = RequiredArray(worldAfter, "activeMonsterIds");
		Assert.Equal(AuthoritativeCathedralPopulationParameters.MonsterCapacity, expectedActiveIds.GetArrayLength());
		CompareIntArray(expectedActiveIds, nativeSlotIds, "activeMonsterIds");
		CompareIntArrayPrefix(expectedActiveIds, population.ActiveMonsterIds, activeMonsterCount, "activeMonsterIds prefix");
		var expectedMonsterSlots = clearedMonsterSlots.Select(value => value with { }).ToArray();
		foreach (var monster in population.Monsters) {
			Assert.InRange(monster.Slot, 0, expectedMonsterSlots.Length - 1);
			expectedMonsterSlots[monster.Slot] = monster with { };
		}
		var monstersBySlot = population.Monsters.ToDictionary(value => value.Slot);
		for (var slot = 0; slot < expectedMonsters.GetArrayLength(); slot++) {
			CompareExactJson(expectedMonsters[slot], JsonSerializer.SerializeToElement(expectedMonsterSlots[slot], JsonOptions),
				$"initial monster slot {slot}");
			if (!monstersBySlot.ContainsKey(slot))
				CompareExactJson(beforeMonsters[slot], expectedMonsters[slot], $"untouched monster slot {slot}");
		}
		Assert.Equal(RequiredInt(worldBefore, "monsterTargetCount"), RequiredInt(worldAfter, "monsterTargetCount"));
		CompareExactJson(RequiredArray(worldBefore, "monsterPetrified"),
			RequiredArray(worldAfter, "monsterPetrified"), "monster petrification flags");

		Assert.Equal(RequiredInt(worldBefore, "activeItemCount"), RequiredInt(worldAfter, "activeItemCount"));
		CompareExactJson(RequiredArray(worldBefore, "activeItemIds"), RequiredArray(worldAfter, "activeItemIds"), "active item IDs");
		CompareExactJson(RequiredArray(worldBefore, "items"), RequiredArray(worldAfter, "items"), "untouched item slots");
		CompareExactJson(RequiredArray(worldBefore, "uniqueFlags"), RequiredArray(worldAfter, "uniqueFlags"), "unique item history");
		CompareExactJson(RequiredObject(worldBefore, "lightAllocator"), RequiredObject(worldAfter, "lightAllocator"), "light allocator");
		foreach (var name in new[] { "themeX", "themeY", "themeVar1" })
			Assert.Equal(RequiredInt(worldBefore, name), RequiredInt(worldAfter, name));

		CompareByteArray(RequiredArray(worldAfter, "sol"), metadata.EffectivePieceProperties, "post-object SOL");
		CompareAllTileChannels(worldBefore, worldAfter, population, actual.Themes.HeldPopulatedCells);
	}

	private static void CompareAllTileChannels(
		JsonElement worldBefore,
		JsonElement worldAfter,
		AuthoritativeCathedralPopulationResult population,
		IReadOnlyList<bool> heldPopulated)
	{
		var beforeTiles = RequiredArray(worldBefore, "tiles");
		var afterTiles = RequiredArray(worldAfter, "tiles");
		Assert.Equal(CellCount, beforeTiles.GetArrayLength());
		Assert.Equal(CellCount, afterTiles.GetArrayLength());
		var expectedLight = Enumerable.Repeat((byte)15, CellCount).ToArray();
		var expectedPreLight = new byte[CellCount];
		AuthoritativeCathedralInitialObjectLighting.ApplyCatacombs(expectedLight, expectedPreLight, population);
		for (var cell = 0; cell < CellCount; cell++) {
			var before = beforeTiles[cell];
			var after = afterTiles[cell];
			Assert.Equal(11, before.GetArrayLength());
			Assert.Equal(11, after.GetArrayLength());
			Assert.Equal((int)population.Pieces[cell], after[0].GetInt32());
			Assert.Equal((int)population.Special[cell], after[1].GetInt32());
			Assert.Equal((int)unchecked((sbyte)population.Transparency[cell]), after[2].GetInt32());
			var expectedFlags = (before[3].GetInt32() & ~8) | (heldPopulated[cell] ? 8 : 0);
			Assert.Equal(expectedFlags, after[3].GetInt32());
			Assert.Equal(before[4].GetInt32(), after[4].GetInt32());
			Assert.Equal(before[5].GetInt32(), after[5].GetInt32());
			Assert.Equal(before[6].GetInt32(), after[6].GetInt32());
			Assert.Equal(population.TileObjectRefs[cell], after[7].GetInt32());
			Assert.Equal(before[8].GetInt32(), after[8].GetInt32());
			Assert.Equal(expectedLight[cell], after[9].GetByte());
			Assert.Equal(expectedPreLight[cell], after[10].GetByte());
		}
	}

	private static IReadOnlyList<bool> DeriveTriggerPopulation(IReadOnlyList<AuthoritativeCatacombsTrigger> triggers)
	{
		var populated = new bool[CellCount];
		foreach (var trigger in triggers) {
			if ((uint)trigger.X >= Width || (uint)trigger.Y >= Width)
				throw new InvalidDataException("A generated Catacombs trigger escaped the fine grid.");
			for (var dy = -2; dy <= 2; dy++) {
				for (var dx = -2; dx <= 2; dx++) {
					var x = trigger.X + dx;
					var y = trigger.Y + dy;
					if ((uint)x >= Width || (uint)y >= Width)
						throw new InvalidDataException("A native Freeupstairs trigger mask crossed the fine-grid edge.");
					populated[x + y * Width] = true;
				}
			}
		}
		return Array.AsReadOnly(populated);
	}

	private static OracleCorpus LoadCorpus()
	{
		var directory = RequiredDirectory(ReferenceDirectoryVariable);
		var statusPath = Path.Combine(directory, "capture-status.json");
		using (var status = JsonDocument.Parse(ReadBoundedRegularFile(statusPath, 4096)))
			Assert.Equal("complete", RequiredString(status.RootElement, "status"));
		using var manifestDocument = JsonDocument.Parse(ReadBoundedRegularFile(
			Path.Combine(directory, "manifest.json"), MaximumManifestBytes));
		var manifest = manifestDocument.RootElement;
		Assert.Equal(OracleFormat, RequiredString(manifest, "format"));
		Assert.Equal(2, RequiredInt(manifest, "version"));
		Assert.Equal(Policy, RequiredString(manifest, "policy"));
		Assert.Equal(32, RequiredInt(manifest, "caseCount"));
		var capturedStages = RequiredString(manifest, "capturedStages");
		Assert.Equal("post-InitObjects,post-InitMonsters,post-InitItems", capturedStages);
		Assert.Equal(5, RequiredInt(manifest, "catacombsFirstLevel"));
		Assert.Equal(8, RequiredInt(manifest, "catacombsLastLevel"));
		var manifestCases = RequiredArray(manifest, "cases").EnumerateArray().Select(row => new ManifestCase(
			RequiredString(row, "file"),
			RequiredString(row, "characterClass"),
			RequiredInt(row, "characterClassId"),
			RequiredInt(row, "levelId"),
			RequiredString(row, "entry"),
			RequiredUInt(row, "inputSeed"),
			RequiredBool(row, "retainedUniqueHistory"))).ToArray();
		Assert.Equal(CaseMatrix.Count, manifestCases.Length);
		Assert.Equal(CaseMatrix.Select(row => row.File).Order(StringComparer.Ordinal),
			manifestCases.Select(row => row.File).Order(StringComparer.Ordinal));
		foreach (var expected in CaseMatrix) {
			var found = manifestCases.Single(row => row.File == expected.File);
			Assert.Equal(expected.CharacterClass, found.CharacterClass);
			Assert.Equal(expected.CharacterClassId, found.CharacterClassId);
			Assert.Equal(expected.Depth, found.LevelId);
			Assert.Equal(expected.Entry, found.Entry);
			Assert.Equal(expected.Seed, found.InputSeed);
			Assert.Equal(expected.RetainedUniqueHistory, found.RetainedUniqueHistory);
		}
		var casesDirectory = Path.Combine(directory, "cases");
		var actualCaseFiles = Directory.EnumerateFiles(casesDirectory, "*.json", SearchOption.TopDirectoryOnly)
			.Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
		var expectedCaseFiles = CaseMatrix.Select(row => Path.GetFileName(row.File)).Order(StringComparer.Ordinal).ToArray();
		Assert.Equal(expectedCaseFiles, actualCaseFiles);

		var sourceMetadata = RequiredFile(SourceMetadataVariable, 2 * 1024 * 1024);
		var globalMetadata = RequiredDirectory(GlobalMetadataVariable);
		var storySource = RequiredFile(StorySourceVariable, 16 * 1024 * 1024);
		var tables = CatacombsPopulationMetadataCompilerTests.BuildTablesFromQualifiedInputs(
			sourceMetadata, globalMetadata, storySource);
		var metadata = AuthoritativeCatacombsPopulationMetadata.Load(new ContentPack(
			AuthoritativeCatacombsPopulationMetadata.CatalogId,
			AuthoritativeCatacombsPopulationMetadata.SchemaVersion,
			tables,
			schema: new ContentSchemaIdentity(
				AuthoritativeCatacombsPopulationMetadata.SchemaId,
				AuthoritativeCatacombsPopulationMetadata.SchemaVersion)));
		return new(directory, capturedStages, manifestCases, metadata);
	}

	private static IReadOnlyList<CaseSpec> BuildCaseMatrix()
	{
		var rows = new List<CaseSpec>(32);
		foreach (var seed in new uint[] { 2588, 743271966 }) {
			foreach (var depth in Enumerable.Range(5, 4)) {
				foreach (var entry in new[] { "main", "previous" })
					rows.Add(new($"cases/warrior-cold-l{depth}-{entry}-{seed}.json", "warrior", 0, depth, entry, seed, false));
			}
		}
		foreach (var depth in Enumerable.Range(5, 4))
			rows.Add(new($"cases/sorcerer-cold-l{depth}-main-2588.json", "sorcerer", 2, depth, "main", 2588, false));
		rows.Add(new("cases/warrior-town-warp-down-l5-town-warp-down-2588.json", "warrior", 0, 5, "town-warp-down", 2588, false));
		rows.Add(new("cases/sorcerer-town-warp-down-l5-town-warp-down-2588.json", "sorcerer", 2, 5, "town-warp-down", 2588, false));
		foreach (var characterClass in new[] { (Name: "warrior", Id: 0), (Name: "sorcerer", Id: 2) }) {
			foreach (var depth in Enumerable.Range(5, 4))
				rows.Add(new($"cases/{characterClass.Name}-retained-history-l{depth}-main-2588.json",
					characterClass.Name, characterClass.Id, depth, "main", 2588, true));
		}
		rows.Add(new("cases/warrior-ai-eligibility-coverage-l8-main-0.json", "warrior", 0, 8, "main", 0, false));
		rows.Add(new("cases/warrior-ai-eligibility-coverage-l8-main-1.json", "warrior", 0, 8, "main", 1, false));
		Assert.Equal(32, rows.Count);
		Assert.Equal(rows.Count, rows.Select(row => row.File).Distinct(StringComparer.Ordinal).Count());
		return Array.AsReadOnly(rows.ToArray());
	}

	private static AuthoritativeCatacombsEntry ToEntry(string value)
		=> value switch {
			"main" => AuthoritativeCatacombsEntry.Main,
			"previous" => AuthoritativeCatacombsEntry.Previous,
			"town-warp-down" => AuthoritativeCatacombsEntry.TownWarpDown,
			_ => throw new InvalidDataException($"Unsupported native Catacombs entry '{value}'."),
		};

	private static void CompareIntArray(JsonElement expected, IEnumerable<int> actual, string context)
	{
		var values = expected.EnumerateArray().Select(value => value.GetInt32()).ToArray();
		Assert.True(values.SequenceEqual(actual), $"Mismatch in {context}.");
	}

	private static void CompareIntArrayPrefix(JsonElement expected, IEnumerable<int> actual, int count, string context)
	{
		var values = expected.EnumerateArray().Take(count).Select(value => value.GetInt32()).ToArray();
		Assert.True(values.SequenceEqual(actual), $"Mismatch in {context}.");
	}

	private static void CompareByteArray(JsonElement expected, IEnumerable<byte> actual, string context)
	{
		var values = expected.EnumerateArray().Select(value => value.GetByte()).ToArray();
		Assert.True(values.SequenceEqual(actual), $"Mismatch in {context}.");
	}

	private static void CompareExactJson(JsonElement expected, JsonElement actual, string context)
	{
		Assert.Equal(expected.ValueKind, actual.ValueKind);
		switch (expected.ValueKind) {
		case JsonValueKind.Object: {
			var expectedProperties = expected.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal).ToArray();
			var actualProperties = actual.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal).ToArray();
			Assert.Equal(expectedProperties, actualProperties);
			foreach (var name in expectedProperties)
				CompareExactJson(expected.GetProperty(name), actual.GetProperty(name), $"{context}.{name}");
			break;
		}
		case JsonValueKind.Array:
			Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
			for (var index = 0; index < expected.GetArrayLength(); index++)
				CompareExactJson(expected[index], actual[index], $"{context}[{index}]");
			break;
		case JsonValueKind.String:
			Assert.Equal(expected.GetString(), actual.GetString());
			break;
		case JsonValueKind.Number:
			Assert.Equal(expected.GetDecimal(), actual.GetDecimal());
			break;
		case JsonValueKind.True:
		case JsonValueKind.False:
			Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
			break;
		case JsonValueKind.Null:
			break;
		default:
			throw new InvalidDataException($"Unexpected JSON value in {context}.");
		}
	}

	private static byte[] ReadTileByteColumn(JsonElement tiles, int column)
	{
		var values = new byte[tiles.GetArrayLength()];
		for (var index = 0; index < values.Length; index++)
			values[index] = tiles[index][column].GetByte();
		return values;
	}

	private static AuthoritativeCathedralPopulationPoint ReadPoint(JsonElement value)
		=> new(RequiredInt(value, "x"), RequiredInt(value, "y"));

	private static JsonElement RequiredObject(JsonElement value, string name)
	{
		var result = value.GetProperty(name);
		Assert.Equal(JsonValueKind.Object, result.ValueKind);
		return result;
	}

	private static JsonElement RequiredArray(JsonElement value, string name)
	{
		var result = value.GetProperty(name);
		Assert.Equal(JsonValueKind.Array, result.ValueKind);
		return result;
	}

	private static string RequiredString(JsonElement value, string name)
		=> value.GetProperty(name).GetString() ?? throw new InvalidDataException($"'{name}' cannot be null.");

	private static int RequiredInt(JsonElement value, string name)
		=> value.GetProperty(name).GetInt32();

	private static uint RequiredUInt(JsonElement value, string name)
		=> value.GetProperty(name).GetUInt32();

	private static bool RequiredBool(JsonElement value, string name)
		=> value.GetProperty(name).GetBoolean();

	private static string RequiredDirectory(string variable)
	{
		var value = Environment.GetEnvironmentVariable(variable);
		Assert.False(string.IsNullOrWhiteSpace(value), $"Set {variable} to the explicit native test input directory.");
		Assert.True(Directory.Exists(value), $"The directory from {variable} does not exist.");
		return Path.GetFullPath(value!);
	}

	private static string RequiredFile(string variable, long maximumBytes)
	{
		var value = Environment.GetEnvironmentVariable(variable);
		Assert.False(string.IsNullOrWhiteSpace(value), $"Set {variable} to the explicit native source input.");
		var path = Path.GetFullPath(value!);
		var info = new FileInfo(path);
		Assert.True(info.Exists, $"The file from {variable} does not exist.");
		Assert.InRange(info.Length, 1, maximumBytes);
		return path;
	}

	private static byte[] ReadBoundedRegularFile(string path, int maximumBytes)
	{
		var info = new FileInfo(path);
		Assert.True(info.Exists, $"Native oracle file is missing: {path}");
		Assert.False((info.Attributes & FileAttributes.Directory) != 0, $"Native oracle path is not a file: {path}");
		Assert.InRange(info.Length, 1, maximumBytes);
		return File.ReadAllBytes(path);
	}

	private sealed record CaseSpec(
		string File, string CharacterClass, int CharacterClassId, int Depth, string Entry, uint Seed, bool RetainedUniqueHistory);
	private sealed record ManifestCase(
		string File, string CharacterClass, int CharacterClassId, int LevelId, string Entry, uint InputSeed, bool RetainedUniqueHistory);
	private sealed record OracleCorpus(
		string ReferenceDirectory,
		string CapturedStages,
		IReadOnlyList<ManifestCase> ManifestCases,
		AuthoritativeCatacombsPopulationMetadata Metadata);
}
