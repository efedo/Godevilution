using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Explicit, archive-free managed/native comparisons for ordinary Cathedral
 * GetLevelMTypes/InitThemes/HoldThemeRooms + InitGolems/InitObjects for version 2;
 * retained version 1 covers only InitGolems/InitObjects. Snapshots and sidecars are
 * deliberately external: they are produced with licensed local game data.
 */
public sealed class AuthoritativeCathedralInitialObjectNativeOracleTests
{
	private const string OracleDirectoryVariable = "GODEVILUTION_CATHEDRAL_POPULATION_ORACLE_DIRECTORY";
	private const string InputDirectoryVariable = "GODEVILUTION_CATHEDRAL_POPULATION_INPUT_DIRECTORY";
	private const string InputFormat = "godevilution-cathedral-initial-inputs";
	private const string InputPolicy = "ordinary-original-diablo-fresh-initial-objects";
	private const string SnapshotFormat = "godevilution-cathedral-l1-population-oracle";
	private const string SnapshotPolicy = "no-quest-original-diablo-initgolems-initobjects";
	private const int Version = 1;
	private const int Width = AuthoritativeCathedralPopulationGeometry.Width;
	private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
	private const int PieceCount = AuthoritativeCathedralPopulationGeometry.PieceCount;
	private const int MaxObjectBytes = 4 * 1024 * 1024;
	private const int MaxInputBytes = 32 * 1024 * 1024;

	private static readonly JsonSerializerOptions JsonOptions = new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};
	private static readonly Lazy<Corpus> LoadedCorpus = new(Corpus.Load);

	private static readonly string[] SnapshotProperties = [
		"format", "version", "policy", "depth", "inputSeed", "geometrySeed", "populationSeed", "storySeed", "entry",
		"attemptSeed", "rngAfterCreateLevel", "rngAfterPopulationReset", "rngAfterGetLevelMTypes", "rngAfterInitThemes",
		"rngAfterInitGolems", "rngAfterInitObjects", "entryPosition", "stairsUp", "stairsDown", "triggers",
		"populatedCellsBeforeHoldThemeRooms", "populatedCellsAfterHoldThemeRooms", "pieces", "transparency", "solProperties",
		"activeObjectCount", "activeObjectIds", "availableObjectIds", "tileObjectRefs", "objects", "activeMonsterCount",
		"activeMonsterIds", "monsters", "bestiary", "playerContext",
	];

	private static readonly string[] ObjectProperties = [
		"slot", "type", "position", "applyLighting", "animFlag", "animDelay", "animCount", "animLength", "animFrame",
		"animWidth", "deleteFlag", "breakFlag", "solidFlag", "missilePassable", "selectionRegion", "preFlag", "trapFlag",
		"doorFlag", "lightId", "rndSeed", "var1", "var2", "var3", "var4", "var5", "var6", "bookMessage", "var8",
	];

	private static readonly string[] MonsterProperties = [
		"slot", "type", "levelType", "mode", "goal", "goalVar1", "goalVar2", "goalVar3", "var1", "var2", "var3",
		"pathCount", "tile", "future", "old", "last", "direction", "enemy", "enemyPosition", "invalid", "ai",
		"intelligence", "activeForTicks", "uniqueType", "uniqueTranslation", "corpseId", "whoHit", "minDamage", "maxDamage",
		"minDamageSpecial", "maxDamageSpecial", "armorClass", "resistance", "talkMessage", "leader", "leaderRelation", "packSize",
		"flags", "rndItemSeed", "aiSeed", "lightId", "hitPoints", "maxHitPoints", "animTicksPerFrame", "animTickCounter",
		"animNumberOfFrames", "animCurrentFrame",
	];

	private static readonly string[] SupplementalProperties = [
		"slot", "temp", "golemToHit", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity",
		"reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana",
	];

	public static TheoryData<int, uint, string> Cases
	{
		get {
			var result = new TheoryData<int, uint, string>();
			foreach (var depth in Enumerable.Range(1, 4)) {
				foreach (var seed in new uint[] { 2588, 743271966 }) {
					result.Add(depth, seed, "main");
					result.Add(depth, seed, "previous");
				}
			}
			return result;
		}
	}

	[Theory(Explicit = true)]
	[MemberData(nameof(Cases))]
	public void InitialObjectsMatchNativeOracle(int depth, uint populationSeed, string entry)
	{
		var corpus = LoadedCorpus.Value;
		var caseName = $"cathedral-population-depth-{depth}-{populationSeed}-{entry}.json";
		var inputCase = corpus.Cases.Single(value => value.Snapshot == caseName);
		var snapshotPath = Path.Combine(corpus.OracleDirectory, caseName);
		using var snapshot = ParseJson(ReadBoundedRegularFile(snapshotPath, MaxObjectBytes));
		var root = snapshot.RootElement;
		RequireProperties(root, "snapshot", SnapshotProperties);
		Assert.Equal(SnapshotFormat, RequiredString(root, "format"));
		Assert.Equal(Version, RequiredInt(root, "version"));
		Assert.Equal(SnapshotPolicy, RequiredString(root, "policy"));
		Assert.Equal(depth, RequiredInt(root, "depth"));
		Assert.Equal(populationSeed, RequiredUInt(root, "inputSeed"));
		Assert.Equal(populationSeed, RequiredUInt(root, "geometrySeed"));
		Assert.Equal(populationSeed, RequiredUInt(root, "populationSeed"));
		Assert.Equal(populationSeed, RequiredUInt(root, "rngAfterPopulationReset"));
		Assert.Equal(inputCase.Input.StorySeed, RequiredUInt(root, "storySeed"));
		Assert.Equal(entry, RequiredString(root, "entry"));

		Assert.Equal(inputCase.Input.Depth, depth);
		Assert.Equal(inputCase.Input.PopulationSeed, populationSeed);
		Assert.Equal(inputCase.Input.Depth == 1 ? 10 : 15, inputCase.Input.TrapPercent);
		Assert.Equal(inputCase.Input.StorySeed, RequiredUInt(root, "storySeed"));
		Assert.Equal(inputCase.Input.OrderedBestiary.Count, RequiredArray(root, "bestiary").GetArrayLength());
		CompareBestiary(inputCase.Input.OrderedBestiary, RequiredArray(root, "bestiary"));
		ComparePlayer(inputCase.Input.Player, RequiredObject(root, "playerContext"));
		CompareIntArray(RequiredArray(root, "pieces"), inputCase.Input.Pieces, "snapshot pre-phase pieces");
		CompareIntArray(RequiredArray(root, "transparency"), inputCase.Input.Transparency, "snapshot pre-phase transparency");
		CompareIntArray(RequiredArray(root, "solProperties"), inputCase.Input.PieceProperties, "snapshot pre-phase SOL");

		var parameters = BuildParameters(corpus, inputCase, root, out var composedPopulation);
		var actual = composedPopulation ?? AuthoritativeCathedralPopulationGenerator.Generate(parameters);

		Assert.Equal(RequiredUInt(root, "rngAfterInitGolems"), actual.RandomAfterGolems);
		Assert.Equal(RequiredUInt(root, "rngAfterInitObjects"), actual.FinalRandomState);
		Assert.Equal(RequiredInt(root, "activeObjectCount"), actual.ActiveObjectCount);
		Assert.Equal(RequiredInt(root, "activeMonsterCount"), actual.Monsters.Count);
		CompareIntArray(RequiredArray(root, "activeObjectIds"), actual.ActiveObjectIds, "activeObjectIds");
		CompareIntArray(RequiredArray(root, "availableObjectIds"), actual.AvailableObjectIds, "availableObjectIds");
		CompareIntArray(RequiredArray(root, "tileObjectRefs"), actual.TileObjectRefs, "tileObjectRefs");
		CompareIntArray(RequiredArray(root, "activeMonsterIds"), actual.ActiveMonsterIds, "activeMonsterIds");

		var expectedObjects = RequiredArray(root, "objects");
		Assert.Equal(actual.ActiveObjectCount, expectedObjects.GetArrayLength());
		for (var index = 0; index < expectedObjects.GetArrayLength(); index++) {
			var slot = actual.ActiveObjectIds[index];
			Assert.InRange(slot, 0, AuthoritativeCathedralPopulationParameters.ObjectCapacity - 1);
			var expected = expectedObjects[index];
			RequireProperties(expected, $"snapshot.objects[{index}]", ObjectProperties);
			var serialized = JsonSerializer.SerializeToElement(actual.Objects[slot], JsonOptions);
			CompareExactJson(expected, serialized, $"object slot {slot}");
		}

		var expectedMonsters = RequiredArray(root, "monsters");
		Assert.Equal(actual.Monsters.Count, expectedMonsters.GetArrayLength());
		for (var index = 0; index < expectedMonsters.GetArrayLength(); index++) {
			var slot = actual.ActiveMonsterIds[index];
			Assert.InRange(slot, 0, AuthoritativeCathedralPopulationParameters.MonsterCapacity - 1);
			var expected = expectedMonsters[index];
			RequireProperties(expected, $"snapshot.monsters[{index}]", MonsterProperties);
			var serialized = JsonSerializer.SerializeToElement(actual.Monsters.Single(value => value.Slot == slot), JsonOptions);
			CompareSelectedProperties(expected, serialized, MonsterProperties, $"monster slot {slot}");
		}

		CompareUShortArray(RequiredArray(inputCase.ExpectedOutput, "pieces"), actual.Pieces, "sidecar.expectedOutput.pieces");
		CompareByteArray(RequiredArray(inputCase.ExpectedOutput, "special"), actual.Special, "sidecar.expectedOutput.special");
		CompareByteArray(RequiredArray(inputCase.ExpectedOutput, "transparency"), actual.Transparency, "sidecar.expectedOutput.transparency");
		CompareSupplemental(inputCase.ExpectedOutput, actual);
	}

	private static AuthoritativeCathedralPopulationParameters BuildParameters(Corpus corpus, OracleCase inputCase,
		JsonElement snapshot, out AuthoritativeCathedralPopulationResult? composedPopulation)
	{
		composedPopulation = null;
		var input = inputCase.Input;
		IReadOnlyList<bool> populationReservations = RequireBoolArray(input.PopulatedCells, CellCount, "input.populatedCells");
		Assert.True(input.FreshMonsterSlots, "The oracle requires fresh monster slots.");

		var objects = corpus.ObjectCatalog
			.OrderBy(value => value.NativeObjectId)
			.Select(value => new AuthoritativeCathedralPopulationObjectDefinition(
				value.NativeObjectId, CheckedByte(value.Flags, "object flags"), CheckedByte(value.AnimDelay, "object animDelay"),
				CheckedByte(value.AnimLength, "object animLength"), CheckedByte(value.AnimWidth, "object animWidth"), value.SelectionRegion))
			.ToArray();
		Assert.Equal(AuthoritativeCathedralPopulationParameters.ObjectCapacity - 18, objects.Length);

		var rawById = corpus.RawMonsterData.ToDictionary(value => value.NativeMonsterId);
		var bestiary = input.OrderedBestiary.Select(value => {
			Assert.True(rawById.TryGetValue(value.Type, out var raw), $"Missing raw monster data for native type {value.Type}.");
			Assert.Equal(value.Skeleton, IsNativeSkeleton(value.Type));
			var animationCount = raw!.HasSpecial ? 6 : 5;
			for (var animation = 0; animation < 6; animation++) {
				var expectedFrame = animation < animationCount ? raw.Frames[animation] : 0;
				var expectedRate = expectedFrame != 0 ? raw.Rates[animation] : 0;
				var expectedWidth = expectedFrame != 0 ? raw.Width : 0;
				Assert.Equal(expectedFrame, value.Frames[animation]);
				Assert.Equal(expectedRate, value.Rates[animation]);
				Assert.Equal(expectedWidth, value.Widths[animation]);
			}
			return new AuthoritativeCathedralPopulationMonsterDefinition(
				value.Type, value.Skeleton, raw!.MinHp, raw.MaxHp, raw.Ai, raw.Intelligence, raw.AbilityFlags,
				raw.MinDamage, raw.MaxDamage, raw.MinDamageSpecial, raw.MaxDamageSpecial, raw.ArmorClass, raw.Resistance,
				raw.ReducePlayerStrength, raw.ReducePlayerMagic, raw.ReducePlayerDexterity, raw.ReducePlayerVitality,
				raw.ReducePlayerMaxHP, raw.ReducePlayerMaxMana, value.Frames, value.Rates);
		}).ToArray();
		Assert.Equal(109, bestiary[0].NativeType);
		if (corpus.InputVersion == 2) {
			var prerequisites = input.Prerequisites ?? throw new InvalidDataException("Version 2 requires pre-theme prerequisites.");
			var preThemeGeometry = new AuthoritativeCathedralPopulationGeometry(
				ToUShortArray(input.Pieces, CellCount, "input.pieces"), ToByteArray(input.Special, CellCount, "input.special"),
				ToByteArray(input.Transparency, CellCount, "input.transparency"),
				ToByteArray(input.PieceProperties, PieceCount, "input.pieceProperties"),
				RequireBoolArray(prerequisites.PopulatedCells, CellCount, "prerequisites.populatedCells"),
				ToOccupancy(input.PlayerTileRefs, "input.playerTileRefs"), ToOccupancy(input.MonsterTileRefs, "input.monsterTileRefs"));
			var composed = AuthoritativeCathedralInitialPopulationGenerator.Generate(new(
				input.Depth, input.PopulationSeed, input.StorySeed, preThemeGeometry,
				corpus.RawMonsterData.Select(ToRawDefinition).ToArray(), objects,
				new(input.Player.Id, input.Player.Level, input.Player.Active, input.Player.LevelChanging,
					input.Player.Tile, input.Player.Future), input.TrapPercent, corpus.StoryMessages,
				prerequisites.NextTransparencyValue, prerequisites.TriggerPositions));
			composedPopulation = composed.Population;
			var generatedBestiary = composed.Bestiary;
			Assert.Equal(RequiredUInt(snapshot, "rngAfterGetLevelMTypes"), generatedBestiary.FinalRandomState);
			Assert.Equal(input.OrderedBestiary.Count, generatedBestiary.Entries.Count);
			for (var index = 0; index < generatedBestiary.Entries.Count; index++) {
				var actual = generatedBestiary.Entries[index];
				var expected = input.OrderedBestiary[index];
				Assert.Equal(expected.Index, actual.Index);
				Assert.Equal(expected.Type, actual.NativeType);
				Assert.Equal(expected.PlaceFlags, actual.PlaceFlags);
				Assert.Equal(expected.CorpseId, actual.CorpseId);
				Assert.Equal(expected.Skeleton, actual.PopulationDefinition.IsSkeleton);
				Assert.Equal(expected.Frames, actual.Frames);
				Assert.Equal(expected.Rates, actual.Rates);
				Assert.Equal(expected.Widths, actual.Widths);
			}
			// Native ordered output is comparison-only; subsequent generation uses the managed selection.
			bestiary = generatedBestiary.PopulationDefinitions.ToArray();
			var expectedTriggers = RequiredArray(snapshot, "triggers").EnumerateArray()
				.Select(trigger => ParsePoint(RequiredObject(trigger, "position"))).ToArray();
			Assert.Equal(expectedTriggers, prerequisites.TriggerPositions);
			var generatedThemes = composed.Themes;
			Assert.Equal(RequiredUInt(snapshot, "rngAfterInitThemes"), generatedThemes.FinalRandomState);
			CompareIntArray(RequiredArray(snapshot, "populatedCellsBeforeHoldThemeRooms"),
				Enumerable.Range(0, CellCount).Where(index => prerequisites.PopulatedCells[index]).ToArray(), "pre-theme populated cells");
			CompareIntArray(RequiredArray(snapshot, "populatedCellsAfterHoldThemeRooms"),
				Enumerable.Range(0, CellCount).Where(index => generatedThemes.HeldPopulatedCells[index]).ToArray(), "managed held populated cells");
			Assert.Equal(input.PopulatedCells, generatedThemes.HeldPopulatedCells);
			var expectedThemes = RequiredArray(inputCase.ExpectedOutput, "themes");
			Assert.Equal(expectedThemes.GetArrayLength(), generatedThemes.Themes.Count);
			for (var index = 0; index < generatedThemes.Themes.Count; index++) {
				RequireProperties(expectedThemes[index], "selected theme", "type", "region");
				Assert.Equal(RequiredInt(expectedThemes[index], "type"), generatedThemes.Themes[index].ThemeType);
				Assert.Equal(RequiredInt(expectedThemes[index], "region"), generatedThemes.Themes[index].RegionId);
			}
			populationReservations = generatedThemes.HeldPopulatedCells;
		}
		// Population restarts from its independent seed, not the after-theme RNG state.
		var geometry = new AuthoritativeCathedralPopulationGeometry(
			ToUShortArray(input.Pieces, CellCount, "input.pieces"),
			ToByteArray(input.Special, CellCount, "input.special"),
			ToByteArray(input.Transparency, CellCount, "input.transparency"),
			ToByteArray(input.PieceProperties, PieceCount, "input.pieceProperties"), populationReservations,
			ToOccupancy(input.PlayerTileRefs, "input.playerTileRefs"),
			ToOccupancy(input.MonsterTileRefs, "input.monsterTileRefs"));

		return new AuthoritativeCathedralPopulationParameters(
			input.Depth, input.PopulationSeed, input.StorySeed, geometry, objects, bestiary,
			new AuthoritativeCathedralPopulationPlayer(
				input.Player.Id, input.Player.Level, input.Player.Active, input.Player.LevelChanging,
				input.Player.Tile, input.Player.Future), input.TrapPercent, corpus.StoryMessages);
	}

	private static AuthoritativeCathedralRawMonsterDefinition ToRawDefinition(RawMonster raw)
		=> new(raw.NativeMonsterId, (AuthoritativeCathedralMonsterAvailability)raw.Availability,
			raw.Image, raw.MinimumDungeonLevel, raw.MaximumDungeonLevel, IsNativeSkeleton(raw.NativeMonsterId),
			raw.MinHp, raw.MaxHp, raw.Ai, raw.Intelligence, raw.AbilityFlags,
			raw.MinDamage, raw.MaxDamage, raw.MinDamageSpecial, raw.MaxDamageSpecial, raw.ArmorClass, raw.Resistance,
			raw.ReducePlayerStrength, raw.ReducePlayerMagic, raw.ReducePlayerDexterity, raw.ReducePlayerVitality,
			raw.ReducePlayerMaxHP, raw.ReducePlayerMaxMana, raw.HasSpecial, raw.Width, raw.Frames, raw.Rates);

	private static bool IsNativeSkeleton(int type)
		=> type is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27;

	private static void CompareSupplemental(JsonElement expectedOutput, AuthoritativeCathedralPopulationResult actual)
	{
		var expected = RequiredArray(expectedOutput, "monsterSupplemental");
		Assert.Equal(actual.Monsters.Count, expected.GetArrayLength());
		Assert.Equal(actual.Monsters.Count, actual.Monsters.Select(value => value.Slot).Distinct().Count());
		Assert.Equal(Enumerable.Range(0, actual.Monsters.Count), actual.Monsters.Select(value => value.Slot));
		for (var index = 0; index < expected.GetArrayLength(); index++) {
			RequireProperties(expected[index], $"monsterSupplemental[{index}]", SupplementalProperties);
			var slot = RequiredInt(expected[index], "slot");
			Assert.Equal(actual.ActiveMonsterIds[index], slot);
			var monster = actual.Monsters.Single(value => value.Slot == slot);
			var serialized = JsonSerializer.SerializeToElement(monster, JsonOptions);
			CompareSelectedProperties(expected[index], serialized, SupplementalProperties, $"monster supplemental slot {slot}");
		}
	}

	private static void CompareBestiary(IReadOnlyList<BestiaryInput> input, JsonElement snapshot)
	{
		var expectedProperties = new[] { "index", "type", "skeleton", "placeFlags", "corpseId", "frames", "rates", "widths" };
		for (var index = 0; index < input.Count; index++) {
			RequireProperties(snapshot[index], $"snapshot.bestiary[{index}]", expectedProperties);
			Assert.Equal(input[index].Index, RequiredInt(snapshot[index], "index"));
			Assert.Equal(input[index].Type, RequiredInt(snapshot[index], "type"));
			Assert.Equal(input[index].Skeleton, RequiredBool(snapshot[index], "skeleton"));
			Assert.Equal(input[index].PlaceFlags, RequiredInt(snapshot[index], "placeFlags"));
			Assert.Equal(input[index].CorpseId, RequiredInt(snapshot[index], "corpseId"));
			CompareIntArray(RequiredArray(snapshot[index], "frames"), input[index].Frames, $"bestiary[{index}].frames");
			CompareIntArray(RequiredArray(snapshot[index], "rates"), input[index].Rates, $"bestiary[{index}].rates");
			CompareIntArray(RequiredArray(snapshot[index], "widths"), input[index].Widths, $"bestiary[{index}].widths");
		}
	}

	private static void ComparePlayer(PlayerInput player, JsonElement snapshot)
	{
		RequireProperties(snapshot, "snapshot.playerContext", "id", "class", "level", "active", "levelChanging", "tile", "future", "old");
		Assert.Equal(player.Id, RequiredInt(snapshot, "id"));
		Assert.Equal(player.Level, RequiredInt(snapshot, "level"));
		Assert.Equal(player.Active, RequiredBool(snapshot, "active"));
		Assert.Equal(player.LevelChanging, RequiredBool(snapshot, "levelChanging"));
		Assert.Equal(player.Tile, ParsePoint(RequiredObject(snapshot, "tile")));
		Assert.Equal(player.Future, ParsePoint(RequiredObject(snapshot, "future")));
	}

	private static void CompareSelectedProperties(JsonElement expected, JsonElement actual, IReadOnlyList<string> properties, string context)
	{
		foreach (var property in properties)
			CompareExactJson(RequiredProperty(expected, property), RequiredProperty(actual, property), $"{context}.{property}");
	}

	private static void CompareExactJson(JsonElement expected, JsonElement actual, string context)
	{
		Assert.Equal(expected.ValueKind, actual.ValueKind);
		if (expected.ValueKind == JsonValueKind.Object) {
			var expectedNames = expected.EnumerateObject().Select(value => value.Name).OrderBy(value => value).ToArray();
			var actualNames = actual.EnumerateObject().Select(value => value.Name).OrderBy(value => value).ToArray();
			Assert.Equal(expectedNames, actualNames);
			foreach (var name in expectedNames)
				CompareExactJson(expected.GetProperty(name), actual.GetProperty(name), $"{context}.{name}");
			return;
		}
		if (expected.ValueKind == JsonValueKind.Array) {
			Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
			for (var index = 0; index < expected.GetArrayLength(); index++)
				CompareExactJson(expected[index], actual[index], $"{context}[{index}]");
			return;
		}
		Assert.Equal(expected.ToString(), actual.ToString());
	}

	private static void RequireProperties(JsonElement value, string context, params string[] properties)
	{
		if (value.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException($"{context} must be a JSON object.");
		var expected = properties.OrderBy(name => name).ToArray();
		var actual = value.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray();
		if (!expected.SequenceEqual(actual))
			throw new InvalidDataException($"{context} has an unexpected or missing field.");
	}

	private static JsonElement RequiredProperty(JsonElement value, string name)
	{
		if (!value.TryGetProperty(name, out var property))
			throw new InvalidDataException($"Missing JSON property '{name}'.");
		return property;
	}

	private static JsonElement RequiredObject(JsonElement value, string name)
	{
		var result = RequiredProperty(value, name);
		if (result.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException($"JSON property '{name}' must be an object.");
		return result;
	}

	private static JsonElement RequiredArray(JsonElement value, string name)
	{
		var result = RequiredProperty(value, name);
		if (result.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"JSON property '{name}' must be an array.");
		return result;
	}

	private static string RequiredString(JsonElement value, string name)
		=> RequiredProperty(value, name).ValueKind == JsonValueKind.String
			? RequiredProperty(value, name).GetString()!
			: throw new InvalidDataException($"JSON property '{name}' must be a string.");

	private static int RequiredInt(JsonElement value, string name)
		=> RequiredProperty(value, name).TryGetInt32(out var result)
			? result
			: throw new InvalidDataException($"JSON property '{name}' must be an Int32.");

	private static uint RequiredUInt(JsonElement value, string name)
		=> RequiredProperty(value, name).TryGetUInt32(out var result)
			? result
			: throw new InvalidDataException($"JSON property '{name}' must be a UInt32.");

	private static bool RequiredBool(JsonElement value, string name)
		=> RequiredProperty(value, name).ValueKind switch {
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => throw new InvalidDataException($"JSON property '{name}' must be Boolean."),
		};

	private static void CompareIntArray(JsonElement expected, IReadOnlyList<int> actual, string context)
	{
		Assert.Equal(actual.Count, expected.GetArrayLength());
		for (var index = 0; index < actual.Count; index++)
			Assert.Equal(actual[index], RequiredIntValue(expected[index], context));
	}

	private static void CompareUShortArray(JsonElement expected, IReadOnlyList<ushort> actual, string context)
	{
		Assert.Equal(actual.Count, expected.GetArrayLength());
		for (var index = 0; index < actual.Count; index++)
			Assert.Equal(actual[index], checked((ushort)RequiredIntValue(expected[index], context)));
	}

	private static void CompareByteArray(JsonElement expected, IReadOnlyList<byte> actual, string context, bool required = true)
	{
		if (expected.ValueKind != JsonValueKind.Array) {
			if (required) throw new InvalidDataException($"{context} must be an array.");
			return;
		}
		Assert.Equal(actual.Count, expected.GetArrayLength());
		for (var index = 0; index < actual.Count; index++)
			Assert.Equal(actual[index], checked((byte)RequiredIntValue(expected[index], context)));
	}

	private static void CompareExpectedIntArray(JsonElement expected, IReadOnlyList<int> actual, string context)
	{
		Assert.Equal(actual.Count, expected.GetArrayLength());
		for (var index = 0; index < actual.Count; index++)
			Assert.Equal(actual[index], RequiredIntValue(expected[index], context));
	}

	private static void CompareExpectedUIntArray(JsonElement expected, IReadOnlyList<uint> actual, string context)
	{
		Assert.Equal(actual.Count, expected.GetArrayLength());
		for (var index = 0; index < actual.Count; index++)
			Assert.Equal(actual[index], checked((uint)RequiredIntValue(expected[index], context)));
	}

	private static void CompareExpectedStringArray(JsonElement expected, IReadOnlyList<string> actual, string context)
	{
		Assert.Equal(actual.Count, expected.GetArrayLength());
		for (var index = 0; index < actual.Count; index++) {
			if (expected[index].ValueKind != JsonValueKind.String)
				throw new InvalidDataException($"{context} must contain strings.");
			Assert.Equal(actual[index], expected[index].GetString());
		}
	}

	private static void ValidateSnapshots(string oracleDirectory, IReadOnlyList<string> names, out int wallTrapCount)
	{
		wallTrapCount = 0;
		foreach (var name in names) {
			using var document = ParseJson(ReadBoundedRegularFile(Path.Combine(oracleDirectory, name), MaxObjectBytes));
			var root = document.RootElement;
			RequireProperties(root, $"snapshot {name}", SnapshotProperties);
			var objects = RequiredArray(root, "objects");
			foreach (var value in objects.EnumerateArray()) {
				RequireProperties(value, $"snapshot {name} object", ObjectProperties);
				var type = RequiredInt(value, "type");
				if (type is 53 or 54)
					wallTrapCount++;
			}
		}
	}

	private static int RequiredIntValue(JsonElement value, string context)
		=> value.TryGetInt32(out var result)
			? result
			: throw new InvalidDataException($"{context} must contain Int32 values.");

	private static int[] RequiredIntArray(JsonElement value, string context)
	{
		if (value.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"{context} must be an array.");
		return value.EnumerateArray().Select(item => RequiredIntValue(item, context)).ToArray();
	}

	private static bool[] RequiredBoolArray(JsonElement value, string context)
	{
		if (value.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"{context} must be an array.");
		return value.EnumerateArray().Select(item => item.ValueKind switch {
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => throw new InvalidDataException($"{context} must contain Boolean values."),
		}).ToArray();
	}

	private static JsonDocument ParseJson(byte[] bytes)
		=> JsonDocument.Parse(bytes, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 128 });

	private static byte[] ReadBoundedRegularFile(string path, int limit)
	{
		var fullPath = Path.GetFullPath(path);
		if (!File.Exists(fullPath))
			throw new FileNotFoundException($"Required native oracle file is missing: {fullPath}", fullPath);
		if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
			throw new InvalidDataException($"Refusing a reparse-point oracle file: {fullPath}");
		var length = new FileInfo(fullPath).Length;
		if (length is <= 0 or > int.MaxValue || length > limit)
			throw new InvalidDataException($"Native oracle file exceeds its bounded size: {fullPath}");
		return File.ReadAllBytes(fullPath);
	}

	private static string RequiredDirectory(string variable)
	{
		var path = Environment.GetEnvironmentVariable(variable);
		if (string.IsNullOrWhiteSpace(path))
			throw new InvalidDataException($"{variable} must point to an external native oracle directory.");
		var fullPath = Path.GetFullPath(path);
		if (!Directory.Exists(fullPath) || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
			throw new InvalidDataException($"{variable} is not a regular directory: {fullPath}");
		return fullPath;
	}

	private static AuthoritativeCathedralPopulationPoint ParsePoint(JsonElement value)
	{
		RequireProperties(value, "point", "x", "y");
		return new(RequiredInt(value, "x"), RequiredInt(value, "y"));
	}

	private static ushort[] ToUShortArray(int[] values, int count, string context)
	{
		if (values.Length != count) throw new InvalidDataException($"{context} requires exactly {count} values.");
		return values.Select(value => checked((ushort)value)).ToArray();
	}

	private static byte[] ToByteArray(int[] values, int count, string context)
	{
		if (values.Length != count) throw new InvalidDataException($"{context} requires exactly {count} values.");
		return values.Select(value => checked((byte)value)).ToArray();
	}

	private static bool[] RequireBoolArray(bool[] values, int count, string context)
	{
		if (values.Length != count) throw new InvalidDataException($"{context} requires exactly {count} values.");
		return values.ToArray();
	}

	private static bool[] ToOccupancy(int[] values, string context)
	{
		if (values.Length != CellCount) throw new InvalidDataException($"{context} requires exactly {CellCount} values.");
		return values.Select(value => value != 0).ToArray();
	}

	private static byte CheckedByte(int value, string context)
		=> value is >= byte.MinValue and <= byte.MaxValue
			? (byte)value
			: throw new InvalidDataException($"{context} is outside byte range.");

	private sealed class Corpus
	{
		private Corpus(string oracleDirectory, int[] storyMessages, IReadOnlyList<ObjectMetadata> objectCatalog,
			IReadOnlyList<RawMonster> rawMonsterData, IReadOnlyList<OracleCase> cases, int inputVersion)
		{
			OracleDirectory = oracleDirectory;
			StoryMessages = storyMessages;
			ObjectCatalog = objectCatalog;
			RawMonsterData = rawMonsterData;
			Cases = cases;
			InputVersion = inputVersion;
		}

		public string OracleDirectory { get; }
		public int[] StoryMessages { get; }
		public IReadOnlyList<ObjectMetadata> ObjectCatalog { get; }
		public IReadOnlyList<RawMonster> RawMonsterData { get; }
		public IReadOnlyList<OracleCase> Cases { get; }
		public int InputVersion { get; }

		public static Corpus Load()
		{
			var oracleDirectory = RequiredDirectory(OracleDirectoryVariable);
			var inputDirectory = RequiredDirectory(InputDirectoryVariable);
			var oracleEntries = Directory.EnumerateFileSystemEntries(oracleDirectory, "*", SearchOption.TopDirectoryOnly).ToArray();
			if (oracleEntries.Length != 17)
				throw new InvalidDataException("The native oracle directory must contain metadata.json and exactly sixteen snapshots.");
			var inputEntries = Directory.EnumerateFileSystemEntries(inputDirectory, "*", SearchOption.TopDirectoryOnly).ToArray();
			if (inputEntries.Length != 1 || !string.Equals(Path.GetFileName(inputEntries[0]), "native-population-inputs.json", StringComparison.Ordinal))
				throw new InvalidDataException("The input directory must contain exactly native-population-inputs.json.");

			using var metadata = ParseJson(ReadBoundedRegularFile(Path.Combine(oracleDirectory, "metadata.json"), MaxObjectBytes));
			var metadataRoot = metadata.RootElement;
			RequireProperties(metadataRoot, "metadata", "format", "version", "policy", "nativeWidth", "nativeHeight", "rowMajor",
				"headless", "soundInitialized", "game", "gameMode", "difficulty", "playerLifeQ6", "caseCount", "depths", "seeds",
				"entries", "storySeed", "objectFields", "monsterFields", "rngBoundaries", "nativeObjectTypes", "nativeObjectCatalog");
			Assert.Equal(Version, RequiredInt(metadataRoot, "version"));
			Assert.Equal(SnapshotFormat, RequiredString(metadataRoot, "format"));
			Assert.Equal(SnapshotPolicy, RequiredString(metadataRoot, "policy"));
			Assert.Equal(Width, RequiredInt(metadataRoot, "nativeWidth"));
			Assert.Equal(Width, RequiredInt(metadataRoot, "nativeHeight"));
			Assert.True(RequiredBool(metadataRoot, "rowMajor"));
			Assert.True(RequiredBool(metadataRoot, "headless"));
			Assert.False(RequiredBool(metadataRoot, "soundInitialized"));
			Assert.Equal("diablo", RequiredString(metadataRoot, "game"));
			Assert.Equal("singleplayer", RequiredString(metadataRoot, "gameMode"));
			Assert.Equal("normal", RequiredString(metadataRoot, "difficulty"));
			Assert.Equal(64, RequiredInt(metadataRoot, "playerLifeQ6"));
			Assert.Equal(16, RequiredInt(metadataRoot, "caseCount"));
			CompareExpectedIntArray(RequiredArray(metadataRoot, "depths"), [1, 2, 3, 4], "metadata.depths");
			CompareExpectedUIntArray(RequiredArray(metadataRoot, "seeds"), [2588U, 743271966U], "metadata.seeds");
			CompareExpectedStringArray(RequiredArray(metadataRoot, "entries"), ["main", "previous"], "metadata.entries");
			Assert.Equal(324508639U, RequiredUInt(metadataRoot, "storySeed"));
			CompareExpectedStringArray(RequiredArray(metadataRoot, "objectFields"), [
				"type", "position", "applyLighting", "animFlag", "animDelay", "animCount", "animLength", "animFrame", "animWidth",
				"deleteFlag", "breakFlag", "solidFlag", "missilePassable", "selectionRegion", "preFlag", "trapFlag", "doorFlag",
				"lightId", "rndSeed", "var1", "var2", "var3", "var4", "var5", "var6", "bookMessage", "var8"], "metadata.objectFields");
			CompareExpectedStringArray(RequiredArray(metadataRoot, "monsterFields"), MonsterProperties.Skip(1).ToArray(), "metadata.monsterFields");
			CompareExpectedStringArray(RequiredArray(metadataRoot, "rngBoundaries"), [
				"rngAfterCreateLevel", "rngAfterPopulationReset", "rngAfterGetLevelMTypes", "rngAfterInitThemes", "rngAfterInitGolems", "rngAfterInitObjects"], "metadata.rngBoundaries");
			var nativeObjectTypes = RequiredObject(metadataRoot, "nativeObjectTypes");
			RequireProperties(nativeObjectTypes, "metadata.nativeObjectTypes", "sarcophagus", "barrel");
			Assert.Equal(48, RequiredInt(nativeObjectTypes, "sarcophagus"));
			Assert.Equal(57, RequiredInt(nativeObjectTypes, "barrel"));
			var objectCatalog = ParseObjectCatalog(RequiredArray(metadataRoot, "nativeObjectCatalog"));
		Assert.Equal(109, objectCatalog.Count);

		using var sidecar = ParseJson(ReadBoundedRegularFile(inputEntries[0], MaxInputBytes));
		var sidecarRoot = sidecar.RootElement;
		var inputVersion = RequiredInt(sidecarRoot, "version");
		Assert.InRange(inputVersion, 1, 2);
		var sidecarProperties = new[] { "format", "version", "policy", "caseCount", "levelGenerationTableLoaded", "storyMessages", "constants", "monsterData", "cases" };
		RequireProperties(sidecarRoot, "sidecar", inputVersion == 2 ? [.. sidecarProperties, "freshBestiarySlots"] : sidecarProperties);
		Assert.Equal(InputFormat, RequiredString(sidecarRoot, "format"));
		if (inputVersion == 2)
			Assert.True(RequiredBool(sidecarRoot, "freshBestiarySlots"));
		Assert.Equal(InputPolicy, RequiredString(sidecarRoot, "policy"));
		Assert.Equal(16, RequiredInt(sidecarRoot, "caseCount"));
		Assert.True(RequiredBool(sidecarRoot, "levelGenerationTableLoaded"));
		var storyMessages = RequiredIntArray(RequiredArray(sidecarRoot, "storyMessages"), "storyMessages");
		Assert.Equal(3, storyMessages.Length);
		ValidateConstants(RequiredObject(sidecarRoot, "constants"));
		var rawMonsterData = ParseRawMonsterData(RequiredArray(sidecarRoot, "monsterData"));
		var cases = ParseCases(RequiredArray(sidecarRoot, "cases"), inputVersion);
		Assert.Equal(16, cases.Count);
		var expectedNames = (from depth in Enumerable.Range(1, 4)
			from seed in new uint[] { 2588, 743271966 }
			from entry in new[] { "main", "previous" }
			select $"cathedral-population-depth-{depth}-{seed}-{entry}.json").OrderBy(value => value).ToArray();
		Assert.Equal(expectedNames, cases.Select(value => value.Snapshot).OrderBy(value => value).ToArray());
		if (inputVersion == 2) {
			var themeCount = 0;
			foreach (var oracleCase in cases) {
				var themes = RequiredArray(oracleCase.ExpectedOutput, "themes");
				Assert.InRange(themes.GetArrayLength(), 0, 50);
				var regions = new HashSet<int>();
				foreach (var theme in themes.EnumerateArray()) {
					RequireProperties(theme, "expected theme", "type", "region");
					Assert.InRange(RequiredInt(theme, "type"), 0, 16);
					var region = RequiredInt(theme, "region");
					Assert.InRange(region, 0, 127);
					Assert.True(regions.Add(region), "Native theme regions must be unique.");
				}
				themeCount += themes.GetArrayLength();
			}
			Assert.True(themeCount > 0, "Version 2 must exercise native theme selection and reservation.");
		}
		// Native NUM_DEFAULT_MTYPES is distinct from the 200 runtime monster slots.
		Assert.Equal(138, rawMonsterData.Count);
		Assert.Equal(Enumerable.Range(0, 138), rawMonsterData.Select(value => value.NativeMonsterId));
		Assert.All(cases, value => Assert.Equal(RequiredUInt(metadataRoot, "storySeed"), value.Input.StorySeed));
		ValidateSnapshots(oracleDirectory, expectedNames, out var wallTrapCount);
		Assert.True(wallTrapCount > 0, "The sixteen native expected snapshots must exercise at least one wall trap.");
		return new(oracleDirectory, storyMessages, objectCatalog, rawMonsterData, cases, inputVersion);
		}

		private static IReadOnlyList<ObjectMetadata> ParseObjectCatalog(JsonElement values)
		{
			var result = new List<ObjectMetadata>();
			foreach (var value in values.EnumerateArray()) {
				RequireProperties(value, "nativeObjectCatalog entry", "nativeObjectId", "graphicIndex", "minLevel", "maxLevel", "levelType", "theme", "quest", "flags", "animDelay", "animLength", "animWidth", "selectionRegion", "behavior");
				result.Add(new(
					RequiredInt(value, "nativeObjectId"), RequiredInt(value, "flags"), RequiredInt(value, "animDelay"),
					RequiredInt(value, "animLength"), RequiredInt(value, "animWidth"), RequiredInt(value, "selectionRegion")));
			}
			if (!result.Select(value => value.NativeObjectId).SequenceEqual(Enumerable.Range(0, result.Count)))
				throw new InvalidDataException("The native object catalog must be ordered and complete.");
			return result;
		}

		private static IReadOnlyList<RawMonster> ParseRawMonsterData(JsonElement values)
		{
			var result = new List<RawMonster>();
			foreach (var value in values.EnumerateArray()) {
				RequireProperties(value, "monsterData entry", "nativeMonsterId", "name", "availability", "image", "minDungeonLevel", "maxDungeonLevel", "level", "minHp", "maxHp", "ai", "intelligence", "minDamage", "maxDamage", "minDamageSpecial", "maxDamageSpecial", "armorClass", "resistance", "resistanceHell", "abilityFlags", "hasSpecial", "width", "frames", "rates", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana");
				var frames = RequiredIntArray(RequiredArray(value, "frames"), "monsterData.frames");
				var rates = RequiredIntArray(RequiredArray(value, "rates"), "monsterData.rates");
				if (frames.Length != 6 || rates.Length != 6)
					throw new InvalidDataException("Every raw native monster animation array must contain six entries.");
				result.Add(new(
					RequiredInt(value, "nativeMonsterId"), RequiredInt(value, "minHp"), RequiredInt(value, "maxHp"), RequiredInt(value, "ai"),
					RequiredInt(value, "intelligence"), checked((uint)RequiredInt(value, "abilityFlags")), RequiredBool(value, "hasSpecial"), RequiredInt(value, "width"),
					frames, rates,
					RequiredInt(value, "minDamage"),
					RequiredInt(value, "maxDamage"), RequiredInt(value, "minDamageSpecial"), RequiredInt(value, "maxDamageSpecial"),
					RequiredInt(value, "armorClass"), RequiredInt(value, "resistance"), RequiredInt(value, "reducePlayerStrength"),
					RequiredInt(value, "reducePlayerMagic"), RequiredInt(value, "reducePlayerDexterity"), RequiredInt(value, "reducePlayerVitality"),
					RequiredInt(value, "reducePlayerMaxHP"), RequiredInt(value, "reducePlayerMaxMana"),
					RequiredInt(value, "availability"), RequiredInt(value, "image"),
					RequiredInt(value, "minDungeonLevel"), RequiredInt(value, "maxDungeonLevel")));
			}
			return result;
		}

		private static IReadOnlyList<OracleCase> ParseCases(JsonElement values, int inputVersion)
		{
			var result = new List<OracleCase>();
			foreach (var value in values.EnumerateArray()) {
				RequireProperties(value, "sidecar case", "snapshot", "input", "expectedOutput");
				var input = RequiredObject(value, "input");
				var inputProperties = new[] { "depth", "populationSeed", "storySeed", "pieces", "special", "transparency", "pieceProperties", "populatedCells", "playerTileRefs", "monsterTileRefs", "freshMonsterSlots", "player", "trapPercent", "orderedBestiary" };
				RequireProperties(input, "sidecar input", inputVersion == 2 ? [.. inputProperties, "prerequisites"] : inputProperties);
				PrerequisitesInput? prerequisites = null;
				if (inputVersion == 2) {
					var preTheme = RequiredObject(input, "prerequisites");
					RequireProperties(preTheme, "sidecar prerequisites", "nextTransparencyValue", "populatedCells", "triggerPositions");
					prerequisites = new(RequiredInt(preTheme, "nextTransparencyValue"),
						RequiredBoolArray(RequiredArray(preTheme, "populatedCells"), "prerequisites.populatedCells"),
						RequiredArray(preTheme, "triggerPositions").EnumerateArray().Select(ParsePoint).ToArray());
				}
				var expectedOutput = RequiredObject(value, "expectedOutput");
				var outputProperties = new[] { "pieces", "special", "transparency", "monsterSupplemental" };
				RequireProperties(expectedOutput, "sidecar expectedOutput", inputVersion == 2 ? [.. outputProperties, "themes"] : outputProperties);
				var player = RequiredObject(input, "player");
				RequireProperties(player, "sidecar input.player", "id", "level", "active", "levelChanging", "tile", "future");
				var orderedBestiary = new List<BestiaryInput>();
				foreach (var entry in RequiredArray(input, "orderedBestiary").EnumerateArray()) {
					RequireProperties(entry, "orderedBestiary entry", "index", "type", "skeleton", "placeFlags", "corpseId", "frames", "rates", "widths");
					var frames = RequiredIntArray(RequiredArray(entry, "frames"), "frames");
					var rates = RequiredIntArray(RequiredArray(entry, "rates"), "rates");
					var widths = RequiredIntArray(RequiredArray(entry, "widths"), "widths");
					if (frames.Length != 6 || rates.Length != 6 || widths.Length != 6)
						throw new InvalidDataException("Every native pre-phase bestiary animation array must contain six entries.");
					if (frames.Concat(rates).Any(animation => animation is < 0 or > 127))
						throw new InvalidDataException("Native pre-phase bestiary frames and rates must fit the managed animation contract.");
					orderedBestiary.Add(new(
						RequiredInt(entry, "index"), RequiredInt(entry, "type"), RequiredBool(entry, "skeleton"), RequiredInt(entry, "placeFlags"),
						RequiredInt(entry, "corpseId"), frames, rates, widths));
				}
				result.Add(new(
					RequiredString(value, "snapshot"),
					new(
						RequiredInt(input, "depth"), RequiredUInt(input, "populationSeed"), RequiredUInt(input, "storySeed"),
						RequiredIntArray(RequiredArray(input, "pieces"), "pieces"), RequiredIntArray(RequiredArray(input, "special"), "special"),
						RequiredIntArray(RequiredArray(input, "transparency"), "transparency"), RequiredIntArray(RequiredArray(input, "pieceProperties"), "pieceProperties"),
						RequiredBoolArray(RequiredArray(input, "populatedCells"), "populatedCells"), RequiredIntArray(RequiredArray(input, "playerTileRefs"), "playerTileRefs"),
						RequiredIntArray(RequiredArray(input, "monsterTileRefs"), "monsterTileRefs"), RequiredBool(input, "freshMonsterSlots"),
						new(RequiredInt(player, "id"), RequiredInt(player, "level"), RequiredBool(player, "active"), RequiredBool(player, "levelChanging"),
							ParsePoint(RequiredObject(player, "tile")), ParsePoint(RequiredObject(player, "future"))),
						RequiredInt(input, "trapPercent"), orderedBestiary, prerequisites),
					expectedOutput.Clone()));
			}
			return result;
		}

		private static void ValidateConstants(JsonElement value)
		{
			var names = new[] { "golemNativeType", "southDirection", "standMode", "normalGoal", "specialAttackMode", "noUniqueType", "noLeader", "noLight", "textNone", "monsterNoEnemyFlag", "golemFlag", "berserkFlag", "targetsMonsterFlag", "lockAnimationFlag", "allowSpecialFlag", "tileSolidFlag", "tileTrapFlag", "arrow", "firebolt", "lightningControl", "maxObjects", "maxMonsters" };
			RequireProperties(value, "sidecar constants", names);
			var expected = new[] { 109, 0, 0, 1, 7, 255, 255, -1, -1, 1024, 32, 2048, 16, 2, 4, 1, 128, 0, 1, 7, 127, 200 };
			for (var index = 0; index < names.Length; index++)
				Assert.Equal(expected[index], RequiredInt(value, names[index]));
		}
	}

	private sealed record ObjectMetadata(int NativeObjectId, int Flags, int AnimDelay, int AnimLength, int AnimWidth, int SelectionRegion);
	private sealed record RawMonster(int NativeMonsterId, int MinHp, int MaxHp, int Ai, int Intelligence, uint AbilityFlags, bool HasSpecial, int Width, int[] Frames, int[] Rates, int MinDamage, int MaxDamage, int MinDamageSpecial, int MaxDamageSpecial, int ArmorClass, int Resistance, int ReducePlayerStrength, int ReducePlayerMagic, int ReducePlayerDexterity, int ReducePlayerVitality, int ReducePlayerMaxHP, int ReducePlayerMaxMana, int Availability, int Image, int MinimumDungeonLevel, int MaximumDungeonLevel);
	private sealed record BestiaryInput(int Index, int Type, bool Skeleton, int PlaceFlags, int CorpseId, int[] Frames, int[] Rates, int[] Widths);
	private sealed record InputData(int Depth, uint PopulationSeed, uint StorySeed, int[] Pieces, int[] Special, int[] Transparency, int[] PieceProperties, bool[] PopulatedCells, int[] PlayerTileRefs, int[] MonsterTileRefs, bool FreshMonsterSlots, PlayerInput Player, int TrapPercent, IReadOnlyList<BestiaryInput> OrderedBestiary, PrerequisitesInput? Prerequisites);
	private sealed record PrerequisitesInput(int NextTransparencyValue, bool[] PopulatedCells, AuthoritativeCathedralPopulationPoint[] TriggerPositions);
	private sealed record PlayerInput(int Id, int Level, bool Active, bool LevelChanging, AuthoritativeCathedralPopulationPoint Tile, AuthoritativeCathedralPopulationPoint Future);
	private sealed record OracleCase(string Snapshot, InputData Input, JsonElement ExpectedOutput);
}
