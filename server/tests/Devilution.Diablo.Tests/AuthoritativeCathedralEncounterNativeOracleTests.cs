using System.Globalization;
using System.Text;
using System.Text.Json;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Explicit, opt-in comparisons for the ordinary Cathedral InitMonsters phase.
 * The native encounter sidecar is deliberately external and contains inputs
 * captured before InitMonsters plus complete expected post-state.  The managed
 * initial-population input is rebuilt through the production raw-table loader;
 * selected bestiary and final-state fields are never used as generation input.
 */
public sealed class AuthoritativeCathedralEncounterNativeOracleTests
{
	private const string EncounterDirectoryVariable = "GODEVILUTION_CATHEDRAL_ENCOUNTER_ORACLE_DIRECTORY";
	private const string PopulationDirectoryVariable = "GODEVILUTION_CATHEDRAL_POPULATION_INPUT_DIRECTORY";
	private const string PopulationOracleDirectoryVariable = "GODEVILUTION_CATHEDRAL_POPULATION_ORACLE_DIRECTORY";
	private const string EncounterFileName = "native-encounter-inputs.json";
	private const string EncounterFormat = "godevilution-cathedral-l1-encounters";
	private const string EncounterPolicy = "ordinary-original-diablo-post-initial-objects-initmonsters";
	private const string PopulationFormat = "godevilution-cathedral-initial-inputs";
	private const string PopulationPolicy = "ordinary-original-diablo-fresh-initial-objects";
	private const string PopulationOracleFormat = "godevilution-cathedral-l1-population-oracle";
	private const string PopulationOraclePolicy = "no-quest-original-diablo-initgolems-initobjects";
	private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
	private const int PieceCount = AuthoritativeCathedralPopulationGeometry.PieceCount;
	private const int MonsterCapacity = AuthoritativeCathedralPopulationParameters.MonsterCapacity;
	private const int LightCapacity = AuthoritativeCathedralEncounterLightAllocator.MaximumLights;
	private const int MaxInputBytes = 128 * 1024 * 1024;
	private const int MaxMetadataBytes = 4 * 1024 * 1024;
	private const int MaxDepth = 128;

	public static TheoryData<int, uint, string> Cases
	{
		get {
			var result = new TheoryData<int, uint, string>();
			foreach (var depth in Enumerable.Range(1, 4))
				foreach (var seed in new uint[] { 2588, 743271966 })
					foreach (var entry in new[] { "main", "previous" })
						result.Add(depth, seed, entry);
			return result;
		}
	}

	[Theory(Explicit = true)]
	[MemberData(nameof(Cases))]
	public void EncounterMatchesNativeOracle(int depth, uint inputSeed, string entry)
	{
		var encounterDirectory = RequiredExternalDirectory(EncounterDirectoryVariable);
		var populationDirectory = RequiredExternalDirectory(PopulationDirectoryVariable);
		var populationOracleDirectory = RequiredExternalDirectory(PopulationOracleDirectoryVariable);
		var encounterEntries = RegularEntries(encounterDirectory);
		Assert.Equal(new[] { EncounterFileName }, encounterEntries.Select(value => Path.GetFileName(value)!).ToArray());
		var encounterPath = Path.Combine(encounterDirectory, EncounterFileName);
		var encounterRoot = ParseFile(encounterPath, MaxInputBytes);
		var corpus = EncounterCorpus.Load(encounterRoot, populationDirectory, populationOracleDirectory);
		var snapshot = $"cathedral-population-depth-{depth}-{inputSeed}-{entry}.json";
		var encounterCase = corpus.Cases.Single(value => value.Snapshot == snapshot);
		var populationCase = corpus.PopulationCases.Single(value => value.Snapshot == snapshot);
		Assert.Equal(depth, encounterCase.Depth);
		Assert.Equal(inputSeed, encounterCase.InputSeed);
		Assert.Equal(entry, encounterCase.Entry);

		var composed = ComposePopulation(corpus, populationCase);
		Assert.Equal(populationCase.Input.PopulatedCells, composed.Result.Themes.HeldPopulatedCells);
		var pre = encounterCase.PreState;
		Assert.Equal(MonsterCapacity, pre.TotalMonsters);
		Assert.Equal(composed.Population.FinalRandomState, pre.RandomState);
		Assert.Equal(composed.Population.Monsters.Count, pre.ActiveMonsterCount);
		Assert.Equal(Enumerable.Range(0, pre.ActiveMonsterCount), composed.Population.ActiveMonsterIds);
		Assert.Equal(Enumerable.Range(0, MonsterCapacity), pre.ActiveMonsterIds);
		Assert.Equal(composed.Population.Monsters, pre.Monsters.Take(pre.ActiveMonsterCount));
		Assert.Equal(populationCase.Input.MonsterTileRefs, pre.MonsterTileRefs);
		Assert.Equal(populationCase.Input.PlayerTileRefs, pre.PlayerTileRefs);
		Assert.Equal(composed.Population.TileObjectRefs, pre.ObjectTileRefs);
		Assert.Equal(composed.Population.TileObjectRefs, encounterCase.ExpectedState.ObjectTileRefs);
		Assert.Equal(pre.PlayerTileRefs, encounterCase.ExpectedState.PlayerTileRefs);

		var freshPool = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(encounterCase.PreClearRandomState);
		Assert.Equal(encounterCase.PostClearRandomState, freshPool.FinalRandomState);
		var slots = freshPool.Monsters.ToArray();
		foreach (var monster in composed.Population.Monsters)
			slots[monster.Slot] = monster;
		Assert.Equal(pre.Monsters, slots);
		var tileFlags = BuildInitialTileFlags(encounterCase.PreThemeTileFlags, composed.Result.Themes.HeldPopulatedCells);
		Assert.Equal(pre.TileFlags, tileFlags);
		var visibility = tileFlags.Select(value => (value & 0x02) != 0).ToArray();
		var lit = tileFlags.Select(value => (value & 0x40) != 0).ToArray();
		var scatterTypes = composed.Bestiary.Entries
			.Where(value => (value.PlaceFlags & AuthoritativeCathedralBestiaryGenerator.PlaceScatter) != 0)
			.Select(value => value.Index).ToArray();
		var lightAllocator = CreateLightAllocator(pre.LightAllocator);
		var populationParameters = BuildEncounterPopulationParameters(corpus, populationCase, composed);
		var encounterParameters = new AuthoritativeCathedralEncounterParameters(
			populationParameters, composed.Population, pre.MonsterTileRefs, visibility,
			populationCase.Input.Prerequisites.TriggerPositions, scatterTypes, corpus.UniqueDefinitions,
			encounterCase.DensityDivisor, lightAllocator, initialVisibleTransparencyRegions: pre.VisibleTransparencyRegions,
			initialMonsterSlots: slots, initialActiveMonsterIds: pre.ActiveMonsterIds,
			initialActiveMonsterCount: pre.ActiveMonsterCount, initialLit: lit, initialTileFlags: tileFlags,
			composedBestiary: composed.Bestiary);
		var actual = AuthoritativeCathedralEncounterGenerator.Generate(encounterParameters);
		CompareState(actual, encounterCase.ExpectedState);
	}

	private static byte[] BuildInitialTileFlags(int[] preThemeTileFlags, IReadOnlyList<bool> heldPopulatedCells)
	{
		if (preThemeTileFlags.Length != CellCount || heldPopulatedCells.Count != CellCount)
			throw new InvalidDataException("Pre-theme tile flags and held cells must cover the complete dungeon.");
		const byte populated = 1 << 3;
		return preThemeTileFlags.Select((value, index) => {
			if (value is < 0 or > byte.MaxValue)
				throw new InvalidDataException("Pre-theme tile flags must fit uint8.");
			return heldPopulatedCells[index]
				? (byte)(value | populated)
				: (byte)(value & ~populated);
		}).ToArray();
	}

	private static ComposedPopulation ComposePopulation(EncounterCorpus corpus, PopulationCase populationCase)
	{
		var input = populationCase.Input;
		var prereq = input.Prerequisites ?? throw new InvalidDataException("Population v2 prerequisites are required.");
		var catalog = AuthoritativeCathedralPopulationCatalog.Load(
			BuildMonsterTsv(corpus.RawMonsters), BuildObjectTsv(corpus.Objects));
		var preTheme = new AuthoritativeCathedralPopulationGeometry(
			ToUShort(input.Pieces, CellCount, "pieces"), ToByte(input.Special, CellCount, "special"),
			ToByte(input.Transparency, CellCount, "transparency"), ToByte(input.PieceProperties, PieceCount, "pieceProperties"),
			prereq.PopulatedCells, ToOccupancy(input.PlayerTileRefs), ToOccupancy(input.MonsterTileRefs));
		var player = new AuthoritativeCathedralPopulationPlayer(
			input.Player.Id, input.Player.Level, input.Player.Active, input.Player.LevelChanging,
			input.Player.Tile, input.Player.Future);
		var parameters = new AuthoritativeCathedralInitialPopulationParameters(
			input.Depth, input.PopulationSeed, input.StorySeed, preTheme,
			catalog.Monsters, catalog.Objects, player, input.TrapPercent, corpus.StoryMessages,
			prereq.NextTransparencyValue, prereq.TriggerPositions);
		var result = AuthoritativeCathedralInitialPopulationGenerator.Generate(parameters);
		return new(result, catalog, player, prereq);
	}

	private static AuthoritativeCathedralPopulationParameters BuildEncounterPopulationParameters(
		EncounterCorpus corpus, PopulationCase populationCase, ComposedPopulation composed)
	{
		var input = populationCase.Input;
		var geometry = new AuthoritativeCathedralPopulationGeometry(
			ToUShort(input.Pieces, CellCount, "pieces"), ToByte(input.Special, CellCount, "special"),
			ToByte(input.Transparency, CellCount, "transparency"), ToByte(input.PieceProperties, PieceCount, "pieceProperties"),
			composed.Result.Themes.HeldPopulatedCells, ToOccupancy(input.PlayerTileRefs), ToOccupancy(input.MonsterTileRefs));
		return new AuthoritativeCathedralPopulationParameters(
			input.Depth, input.PopulationSeed, input.StorySeed, geometry, composed.Catalog.Objects,
			composed.Result.Bestiary.PopulationDefinitions, composed.Player, input.TrapPercent,
			corpus.StoryMessages);
	}

	private static void CompareState(AuthoritativeCathedralEncounterResult actual, EncounterState expected)
	{
		Assert.Equal(expected.RandomState, actual.FinalRandomState);
		Assert.Equal(expected.TotalMonsters, actual.TotalMonsters);
		Assert.Equal(expected.ActiveMonsterCount, actual.ActiveMonsterCount);
		Assert.Equal(expected.ActiveMonsterIds, actual.ActiveMonsterIds);
		Assert.Equal(expected.MonsterTileRefs, actual.MonsterTileRefs);
		Assert.Equal(expected.TileFlags, actual.TileFlags);
		Assert.Equal(expected.VisibleTransparencyRegions, actual.VisibleTransparencyRegions);
		Assert.Equal(expected.LightAllocator.ActiveLightSlots, actual.ActiveLightSlots);
		Assert.Equal(expected.LightAllocator.ActiveLightCount, actual.ActiveLightCount);
		Assert.Equal(expected.LightAllocator.UpdateLighting, actual.UpdateLighting);
		Assert.Equal(expected.Monsters, actual.MonsterSlots);
		Assert.Equal(expected.TileFlags.Select(value => (value & 0x02) != 0), actual.Visibility);
		Assert.Equal(expected.TileFlags.Select(value => (value & 0x40) != 0), actual.Lit);
		Assert.Equal(expected.Monsters.Take(expected.ActiveMonsterCount), actual.Monsters);
		Assert.All(actual.MonsterSlots, value => Assert.InRange(value.Slot, 0, MonsterCapacity - 1));
		Assert.Equal(expected.LightAllocator.Lights, actual.LightRecords);
		Assert.Equal(expected.LightAllocator.DisableLighting, actual.DisableLighting);
	}

	private static AuthoritativeCathedralEncounterLightAllocator CreateLightAllocator(LightAllocatorInput input)
		=> new(input.ActiveLightSlots, input.ActiveLightCount, input.Lights,
			input.UpdateLighting, input.DisableLighting);

	private static string BuildMonsterTsv(IReadOnlyList<RawMonster> values)
	{
		var columns = AuthoritativeCathedralPopulationCatalog.MonsterColumns;
		var builder = new StringBuilder().AppendLine(string.Join('\t', columns));
		foreach (var value in values.OrderBy(value => value.NativeMonsterId)) {
			var fields = new List<string> {
				value.NativeMonsterId.ToString(CultureInfo.InvariantCulture), value.Availability.ToString(CultureInfo.InvariantCulture),
				value.Image.ToString(CultureInfo.InvariantCulture), value.MinimumDungeonLevel.ToString(CultureInfo.InvariantCulture),
				value.MaximumDungeonLevel.ToString(CultureInfo.InvariantCulture), IsSkeleton(value.NativeMonsterId) ? "1" : "0",
				value.MinHp.ToString(CultureInfo.InvariantCulture), value.MaxHp.ToString(CultureInfo.InvariantCulture),
				value.Ai.ToString(CultureInfo.InvariantCulture), value.Intelligence.ToString(CultureInfo.InvariantCulture),
				value.AbilityFlags.ToString(CultureInfo.InvariantCulture), value.MinDamage.ToString(CultureInfo.InvariantCulture),
				value.MaxDamage.ToString(CultureInfo.InvariantCulture), value.MinDamageSpecial.ToString(CultureInfo.InvariantCulture),
				value.MaxDamageSpecial.ToString(CultureInfo.InvariantCulture), value.ArmorClass.ToString(CultureInfo.InvariantCulture),
				value.Resistance.ToString(CultureInfo.InvariantCulture), value.ReducePlayerStrength.ToString(CultureInfo.InvariantCulture),
				value.ReducePlayerMagic.ToString(CultureInfo.InvariantCulture), value.ReducePlayerDexterity.ToString(CultureInfo.InvariantCulture),
				value.ReducePlayerVitality.ToString(CultureInfo.InvariantCulture), value.ReducePlayerMaxHP.ToString(CultureInfo.InvariantCulture),
				value.ReducePlayerMaxMana.ToString(CultureInfo.InvariantCulture), value.HasSpecial ? "1" : "0",
				value.Width.ToString(CultureInfo.InvariantCulture),
			};
			fields.AddRange(value.Frames.Select(number => number.ToString(CultureInfo.InvariantCulture)));
			fields.AddRange(value.Rates.Select(number => number.ToString(CultureInfo.InvariantCulture)));
			builder.AppendLine(string.Join('\t', fields));
		}
		return builder.ToString();
	}

	private static string BuildObjectTsv(IReadOnlyList<ObjectMetadata> values)
	{
		var builder = new StringBuilder().AppendLine(string.Join('\t', AuthoritativeCathedralPopulationCatalog.ObjectColumns));
		foreach (var value in values.OrderBy(value => value.NativeObjectId))
			builder.AppendLine(string.Join("\t", value.NativeObjectId.ToString(CultureInfo.InvariantCulture),
				value.Flags.ToString(CultureInfo.InvariantCulture), value.AnimDelay.ToString(CultureInfo.InvariantCulture),
				value.AnimLength.ToString(CultureInfo.InvariantCulture), value.AnimWidth.ToString(CultureInfo.InvariantCulture),
				value.SelectionRegion.ToString(CultureInfo.InvariantCulture)));
		return builder.ToString();
	}

	private static bool IsSkeleton(int type)
		=> type is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27;

	private static JsonElement ParseFile(string path, int maxBytes)
		=> JsonDocument.Parse(ReadBoundedRegularFile(path, maxBytes), new JsonDocumentOptions {
			CommentHandling = JsonCommentHandling.Disallow, MaxDepth = MaxDepth,
		}).RootElement.Clone();

	private static JsonElement ParseText(string text)
		=> JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = MaxDepth }).RootElement.Clone();

	private static byte[] ReadBoundedRegularFile(string path, int maxBytes)
	{
		var fullPath = Path.GetFullPath(path);
		if (!File.Exists(fullPath) || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
			throw new InvalidDataException($"Required regular oracle file is missing or redirected: {fullPath}");
		var info = new FileInfo(fullPath);
		if (info.Length <= 0 || info.Length > maxBytes)
			throw new InvalidDataException($"Oracle file is outside its bounded size: {fullPath}");
		return File.ReadAllBytes(fullPath);
	}

	private static string RequiredExternalDirectory(string variable)
	{
		var value = Environment.GetEnvironmentVariable(variable);
		if (string.IsNullOrWhiteSpace(value))
			throw new InvalidDataException($"{variable} must point to an external oracle directory.");
		var path = Path.GetFullPath(value);
		var repository = DiscoverRepositoryRoot();
		var repositoryPrefix = repository.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!Directory.Exists(path) || string.Equals(path, repository, StringComparison.OrdinalIgnoreCase)
			|| path.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException($"{variable} must point outside the repository.");
		for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
			if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
				throw new InvalidDataException($"{variable} has a reparse-point ancestor.");
		return path;
	}

	private static string DiscoverRepositoryRoot()
	{
		for (var current = new DirectoryInfo(Path.GetFullPath(Environment.CurrentDirectory)); current is not null; current = current.Parent) {
			var gitPath = Path.Combine(current.FullName, ".git");
			if (Directory.Exists(gitPath) || File.Exists(gitPath))
				return current.FullName.TrimEnd(Path.DirectorySeparatorChar);
		}
		throw new InvalidDataException("Unable to discover the repository root for oracle path validation.");
	}

	private static IReadOnlyList<string> RegularEntries(string directory)
	{
		var entries = Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
		foreach (var entry in entries)
			if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
				throw new InvalidDataException($"Oracle entry is a reparse point: {entry}");
		return entries;
	}

	private static void ValidateCaseScope(string snapshot, int depth, uint seed, string entry)
	{
		if (depth is < 1 or > 4 || entry is not ("main" or "previous"))
			throw new InvalidDataException("Cathedral oracle cases must use depths 1..4 and main/previous entries.");
		var expected = $"cathedral-population-depth-{depth}-{seed}-{entry}.json";
		if (!string.Equals(snapshot, expected, StringComparison.Ordinal))
			throw new InvalidDataException("Cathedral oracle case snapshot name does not match its depth, seed, and entry.");
	}

	private static void RequireCompleteCaseNames(IReadOnlyList<string> names)
	{
		var expected = (from depth in Enumerable.Range(1, 4)
			from seed in new uint[] { 2588, 743271966 }
			from entry in new[] { "main", "previous" }
			select $"cathedral-population-depth-{depth}-{seed}-{entry}.json").OrderBy(value => value).ToArray();
		if (names.Count != 16 || names.Distinct(StringComparer.Ordinal).Count() != 16
			|| !names.OrderBy(value => value).SequenceEqual(expected))
			throw new InvalidDataException("Cathedral oracle sidecars must contain the exact sixteen cases.");
	}

	private static void RequirePermutation(IReadOnlyList<int> values, int count, string context)
	{
		if (values.Count != count || !values.OrderBy(value => value).SequenceEqual(Enumerable.Range(0, count)))
			throw new InvalidDataException($"{context} must be a complete 0..{count - 1} permutation.");
	}

	private static void ValidateMonsterReferences(int[] references, int activeCount, IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> monsters)
	{
		var seen = new HashSet<int>();
		for (var index = 0; index < references.Length; index++) {
			if (references[index] == 0)
				continue;
			var slot = Math.Abs(references[index]) - 1;
			if (slot < 0 || slot >= activeCount || !seen.Add(slot))
				throw new InvalidDataException("Monster tile references must identify each active slot at most once.");
			var monster = monsters[slot];
			if (monster.Tile == new AuthoritativeCathedralPopulationPoint(0, 0)
				|| (monster.Type == AuthoritativeCathedralPopulationParameters.GolemNativeType
					&& monster.Tile == new AuthoritativeCathedralPopulationPoint(1, 0)))
				throw new InvalidDataException("Off-map Golem and skeleton reservations cannot occupy a dungeon tile reference.");
			var expected = new AuthoritativeCathedralPopulationPoint(index % AuthoritativeCathedralPopulationGeometry.Width,
				index / AuthoritativeCathedralPopulationGeometry.Width);
			if (monster.Tile != expected)
				throw new InvalidDataException("Monster tile references must match their complete monster state.");
		}
	}

	[Fact]
	public void ReaderRejectsDuplicateAndExtraProperties()
	{
		Assert.Throws<InvalidDataException>(() => RequireProperties(ParseText("{\"value\":1,\"value\":2}"), "duplicate", "value"));
		Assert.Throws<InvalidDataException>(() => RequireProperties(ParseText("{\"value\":1,\"extra\":2}"), "extra", "value"));
	}

	[Fact]
	public void ReaderRejectsFloatStringAndNegativeUnsignedScalars()
	{
		Assert.Throws<InvalidDataException>(() => Int(ParseText("{\"value\":1.5}"), "value"));
		Assert.Throws<InvalidDataException>(() => Bool(ParseText("{\"value\":\"true\"}"), "value"));
		Assert.Throws<InvalidDataException>(() => UInt(ParseText("{\"value\":-1}"), "value"));
	}

	[Fact]
	public void ReaderRejectsMalformedPoolPermutationsAndBooleanDimensions()
	{
		Assert.Throws<InvalidDataException>(() => RequirePermutation(new int[MonsterCapacity], MonsterCapacity, "monster slots"));
		var lights = Enumerable.Range(0, LightCapacity).ToArray();
		lights[^1] = lights[^2];
		Assert.Throws<InvalidDataException>(() => RequirePermutation(lights, LightCapacity, "light slots"));
		Assert.Throws<InvalidDataException>(() => RequiredBoolArray(ParseText("{\"values\":[true]}"), "values", "populated cells"));
	}

	[Fact]
	public void ReaderRejectsCaseScopeAndDuplicateReplacementNames()
	{
		Assert.Throws<InvalidDataException>(() => ValidateCaseScope("cathedral-population-depth-5-2588-main.json", 5, 2588, "main"));
		Assert.Throws<InvalidDataException>(() => ValidateCaseScope("cathedral-population-depth-1-2588-main.json", 1, 743271966, "main"));
		Assert.Throws<InvalidDataException>(() => ValidateCaseScope("cathedral-population-depth-1-2588-main.json", 1, 2588, "previous"));
		var names = (from depth in Enumerable.Range(1, 4)
			from seed in new uint[] { 2588, 743271966 }
			from entry in new[] { "main", "previous" }
			select $"cathedral-population-depth-{depth}-{seed}-{entry}.json").ToArray();
		names[^1] = names[^2];
		Assert.Throws<InvalidDataException>(() => RequireCompleteCaseNames(names));
	}

	[Fact]
	public void ReaderRejectsNegativeRangeSharedAndOffMapMonsterReferences()
	{
		var invalidRange = new int[CellCount];
		invalidRange[0] = -MonsterCapacity - 1;
		Assert.Throws<InvalidDataException>(() => EncounterCorpus.CheckedRefs(invalidRange));
		var shared = new int[CellCount];
		shared[10] = 1;
		shared[11] = 1;
		var sharedMonsters = Enumerable.Range(0, MonsterCapacity).Select(slot => new AuthoritativeCathedralPopulationMonsterState {
			Slot = slot, Type = 1, Tile = new(slot == 0 ? 10 : slot == 1 ? 11 : 20, slot < 2 ? 0 : 1),
		}).ToArray();
		Assert.Throws<InvalidDataException>(() => ValidateMonsterReferences(shared, 2, sharedMonsters));
		var offMap = new int[CellCount];
		offMap[1] = 1;
		var offMapMonsters = sharedMonsters.ToArray();
		offMapMonsters[0] = new() { Slot = 0, Type = AuthoritativeCathedralPopulationParameters.GolemNativeType, Tile = new(1, 0) };
		Assert.Throws<InvalidDataException>(() => ValidateMonsterReferences(offMap, 1, offMapMonsters));
		offMap[1] = 0;
		offMap[0] = 1;
		offMapMonsters[0] = new() { Slot = 0, Type = 8, Tile = new(0, 0) };
		Assert.Throws<InvalidDataException>(() => ValidateMonsterReferences(offMap, 1, offMapMonsters));
	}

	private static void RequireProperties(JsonElement value, string context, params string[] expected)
	{
		if (value.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException($"{context} must be an object.");
		var actual = new HashSet<string>(StringComparer.Ordinal);
		foreach (var property in value.EnumerateObject())
			if (!actual.Add(property.Name)) throw new InvalidDataException($"{context} contains duplicate '{property.Name}'.");
		if (!actual.SetEquals(expected))
			throw new InvalidDataException($"{context} has an unexpected property set.");
	}

	private static JsonElement Property(JsonElement value, string name)
		=> value.TryGetProperty(name, out var property) ? property : throw new InvalidDataException($"Missing property '{name}'.");

	private static string String(JsonElement value, string name)
		=> Property(value, name).ValueKind == JsonValueKind.String ? Property(value, name).GetString()! : throw new InvalidDataException($"'{name}' must be a string.");

	private static int Int(JsonElement value, string name)
		=> Property(value, name).TryGetInt32(out var result) ? result : throw new InvalidDataException($"'{name}' must be int32.");

	private static uint UInt(JsonElement value, string name)
		=> Property(value, name).TryGetUInt32(out var result) ? result : throw new InvalidDataException($"'{name}' must be uint32.");

	private static bool Bool(JsonElement value, string name)
		=> Property(value, name).ValueKind switch {
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => throw new InvalidDataException($"'{name}' must be bool."),
		};

	private static JsonElement Array(JsonElement value, string name)
		=> Property(value, name).ValueKind == JsonValueKind.Array ? Property(value, name) : throw new InvalidDataException($"'{name}' must be array.");

	private static int[] IntArray(JsonElement value, string name)
		=> Array(value, name).EnumerateArray().Select((item, index) => item.TryGetInt32(out var result) ? result : throw new InvalidDataException($"'{name}[{index}]' must be int32.")).ToArray();

	private static uint[] UIntArray(JsonElement value, string name)
		=> Array(value, name).EnumerateArray().Select((item, index) => item.TryGetUInt32(out var result) ? result : throw new InvalidDataException($"'{name}[{index}]' must be uint32.")).ToArray();

	private static string[] StringArray(JsonElement value, string name)
		=> Array(value, name).EnumerateArray().Select((item, index) => item.ValueKind == JsonValueKind.String ? item.GetString()! : throw new InvalidDataException($"'{name}[{index}]' must be string.")).ToArray();

	private static bool[] BoolArray(JsonElement value, string name)
		=> Array(value, name).EnumerateArray().Select((item, index) => item.ValueKind switch {
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => throw new InvalidDataException($"'{name}[{index}]' must be bool."),
		}).ToArray();

	private static bool[] RequiredBoolArray(JsonElement value, string name, string context)
	{
		var result = BoolArray(value, name);
		if (result.Length != CellCount)
			throw new InvalidDataException($"{context} requires exactly {CellCount} boolean values.");
		return result;
	}

	private static AuthoritativeCathedralPopulationPoint Point(JsonElement value)
	{
		RequireProperties(value, "point", "x", "y");
		var point = new AuthoritativeCathedralPopulationPoint(Int(value, "x"), Int(value, "y"));
		if (point.X is < 0 or > byte.MaxValue || point.Y is < 0 or > byte.MaxValue)
			throw new InvalidDataException("Native point coordinates must fit uint8.");
		return point;
	}

	private static AuthoritativeCathedralPopulationPoint SignedOffset(JsonElement value)
	{
		RequireProperties(value, "offset", "x", "y");
		var offset = new AuthoritativeCathedralPopulationPoint(Int(value, "x"), Int(value, "y"));
		if (offset.X is < sbyte.MinValue or > sbyte.MaxValue || offset.Y is < sbyte.MinValue or > sbyte.MaxValue)
			throw new InvalidDataException("Native light offsets must fit signed int8.");
		return offset;
	}

	private static ushort[] ToUShort(int[] values, int count, string context)
	{
		if (values.Length != count || values.Any(value => value is < 0 or > ushort.MaxValue))
			throw new InvalidDataException($"{context} has invalid length or range.");
		return values.Select(value => (ushort)value).ToArray();
	}

	private static byte[] ToByte(int[] values, int count, string context)
	{
		if (values.Length != count || values.Any(value => value is < 0 or > byte.MaxValue))
			throw new InvalidDataException($"{context} has invalid length or range.");
		return values.Select(value => (byte)value).ToArray();
	}

	private static bool[] ToOccupancy(int[] values)
	{
		if (values.Length != CellCount || values.Any(value => value is < -MonsterCapacity or > MonsterCapacity))
			throw new InvalidDataException("Tile reference array has invalid length or range.");
		return values.Select(value => value != 0).ToArray();
	}

	private sealed record ComposedPopulation(
		AuthoritativeCathedralInitialPopulationResult Result,
		AuthoritativeCathedralPopulationCatalog Catalog,
		AuthoritativeCathedralPopulationPlayer Player,
		Prerequisites Prerequisites)
	{
		public AuthoritativeCathedralPopulationResult Population => Result.Population;
		public AuthoritativeCathedralBestiaryResult Bestiary => Result.Bestiary;
	}

	private sealed class EncounterCorpus
	{
		private EncounterCorpus(IReadOnlyList<AuthoritativeCathedralUniqueDefinition> uniqueDefinitions, IReadOnlyList<EncounterCase> cases,
			IReadOnlyList<PopulationCase> populationCases, IReadOnlyList<RawMonster> rawMonsters,
			IReadOnlyList<ObjectMetadata> objects, int[] storyMessages)
		{
			UniqueDefinitions = uniqueDefinitions;
			Cases = cases;
			PopulationCases = populationCases;
			RawMonsters = rawMonsters;
			Objects = objects;
			StoryMessages = storyMessages;
		}

		public IReadOnlyList<AuthoritativeCathedralUniqueDefinition> UniqueDefinitions { get; }
		public IReadOnlyList<EncounterCase> Cases { get; }
		public IReadOnlyList<PopulationCase> PopulationCases { get; }
		public IReadOnlyList<RawMonster> RawMonsters { get; }
		public IReadOnlyList<ObjectMetadata> Objects { get; }
		public int[] StoryMessages { get; }

		public static EncounterCorpus Load(JsonElement encounterRoot, string populationDirectory, string oracleDirectory)
		{
			RequireProperties(encounterRoot, "encounter sidecar", "format", "version", "policy", "caseCount", "uniqueDefinitions", "cases");
			if (String(encounterRoot, "format") != EncounterFormat || Int(encounterRoot, "version") != 1 || String(encounterRoot, "policy") != EncounterPolicy || Int(encounterRoot, "caseCount") != 16)
				throw new InvalidDataException("Encounter sidecar metadata does not match the ordinary Cathedral contract.");
			var uniqueDefinitions = Array(encounterRoot, "uniqueDefinitions").EnumerateArray().Select(ParseUnique).ToArray();
			if (uniqueDefinitions.Length != 100 || !uniqueDefinitions.Select(value => value.NativeUniqueId).SequenceEqual(Enumerable.Range(0, 100)))
				throw new InvalidDataException("Encounter unique definitions must be complete ordered native rows.");
			var cases = Array(encounterRoot, "cases").EnumerateArray().Select(ParseEncounterCase).ToArray();
			RequireCompleteCaseNames(cases.Select(value => value.Snapshot).ToArray());
			var population = LoadPopulation(populationDirectory, oracleDirectory);
			if (!cases.Select(value => value.Snapshot).OrderBy(value => value).SequenceEqual(population.Cases.Select(value => value.Snapshot).OrderBy(value => value)))
				throw new InvalidDataException("Encounter and population sidecars must cover the same snapshots.");
			foreach (var encounterCase in cases) {
				var populationCase = population.Cases.Single(value => value.Snapshot == encounterCase.Snapshot);
				if (populationCase.Input.Depth != encounterCase.Depth || populationCase.Input.PopulationSeed != encounterCase.InputSeed)
					throw new InvalidDataException("Encounter and population case depth/seed metadata disagree.");
			}
			return new(uniqueDefinitions, cases, population.Cases, population.RawMonsters, population.Objects, population.StoryMessages);
		}

		private static EncounterCase ParseEncounterCase(JsonElement value)
		{
			RequireProperties(value, "encounter case", "snapshot", "depth", "inputSeed", "entry", "densityDivisor", "preClearRandomState", "postClearRandomState", "preThemeTileFlags", "preState", "expectedState");
			var preTheme = IntArray(value, "preThemeTileFlags");
			if (preTheme.Length != CellCount || preTheme.Any(number => number is < 0 or > byte.MaxValue))
				throw new InvalidDataException("preThemeTileFlags must be a complete byte array.");
			ValidateCaseScope(String(value, "snapshot"), Int(value, "depth"), UInt(value, "inputSeed"), String(value, "entry"));
			return new(String(value, "snapshot"), Int(value, "depth"), UInt(value, "inputSeed"), String(value, "entry"), Int(value, "densityDivisor"),
				UInt(value, "preClearRandomState"), UInt(value, "postClearRandomState"), preTheme,
				ParseState(Property(value, "preState")), ParseState(Property(value, "expectedState")));
		}

		private static AuthoritativeCathedralUniqueDefinition ParseUnique(JsonElement value)
		{
			RequireProperties(value, "unique definition", "nativeUniqueId", "monsterType", "dungeonLevel", "maximumHitPoints", "ai", "intelligence", "minDamage", "maxDamage", "resistance", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana", "monsterPack", "customToHit", "customArmorClass", "talkMessage", "questOnly");
			var id = Int(value, "nativeUniqueId");
			var questOnly = id is 0 or 2 or 3 or 7 or 8;
			var byteFields = new[] { "intelligence", "minDamage", "maxDamage", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana", "customToHit", "customArmorClass" };
			if (Bool(value, "questOnly") != questOnly || Int(value, "monsterPack") is < 0 or > 2
				|| Int(value, "monsterType") is < 0 or >= 138 || Int(value, "dungeonLevel") is < 0 or > byte.MaxValue
				|| Int(value, "maximumHitPoints") is < 0 or > ushort.MaxValue || Int(value, "ai") is < -1 or > 39
				|| Int(value, "resistance") is < 0 or > ushort.MaxValue || Int(value, "talkMessage") is < -1 or > short.MaxValue
				|| byteFields.Any(name => Int(value, name) is < 0 or > byte.MaxValue))
				throw new InvalidDataException("Unique quest suppression or pack metadata is invalid.");
			return new(id, Int(value, "monsterType"), Int(value, "dungeonLevel"), Int(value, "maximumHitPoints"), Int(value, "ai"), Int(value, "intelligence"), Int(value, "minDamage"), Int(value, "maxDamage"), Int(value, "resistance"), Int(value, "reducePlayerStrength"), Int(value, "reducePlayerMagic"), Int(value, "reducePlayerDexterity"), Int(value, "reducePlayerVitality"), Int(value, "reducePlayerMaxHP"), Int(value, "reducePlayerMaxMana"), (AuthoritativeCathedralUniquePack)Int(value, "monsterPack"), Int(value, "customToHit"), Int(value, "customArmorClass"), Int(value, "talkMessage"), questOnly);
		}

		private static EncounterState ParseState(JsonElement value)
		{
			RequireProperties(value, "encounter state", "randomState", "totalMonsters", "activeMonsterCount", "activeMonsterIds", "monsters", "monsterTileRefs", "playerTileRefs", "objectTileRefs", "tileFlags", "visibleTransparencyRegions", "lightAllocator");
			var activeIds = IntArray(value, "activeMonsterIds");
			RequirePermutation(activeIds, MonsterCapacity, "Encounter active-monster IDs");
			var activeMonsterCount = Int(value, "activeMonsterCount");
			if (activeMonsterCount is < 0 or > MonsterCapacity)
				throw new InvalidDataException("Encounter active-monster count is outside the native pool.");
			var monsters = Array(value, "monsters").EnumerateArray().Select(ParseMonster).ToArray();
			if (monsters.Length != MonsterCapacity || !monsters.Select(monster => monster.Slot).SequenceEqual(Enumerable.Range(0, MonsterCapacity)))
				throw new InvalidDataException("Encounter monster slots must be complete and ordered.");
			var tileFlags = IntArray(value, "tileFlags");
			if (tileFlags.Length != CellCount || tileFlags.Any(number => number is < 0 or > byte.MaxValue))
				throw new InvalidDataException("Encounter tile flags must be a complete byte array.");
			var visible = BoolArray(value, "visibleTransparencyRegions");
			if (visible.Length != 256)
				throw new InvalidDataException("Encounter transparency visibility must have 256 entries.");
			var totalMonsters = Int(value, "totalMonsters");
			if (totalMonsters is < 0 or > MonsterCapacity)
				throw new InvalidDataException("Encounter total monster count is outside the native pool.");
			var monsterTileRefs = CheckedRefs(IntArray(value, "monsterTileRefs"));
			ValidateMonsterReferences(monsterTileRefs, activeMonsterCount, monsters);
			return new(UInt(value, "randomState"), totalMonsters, activeMonsterCount, activeIds, monsters,
				monsterTileRefs, CheckedRefs(IntArray(value, "playerTileRefs")), CheckedRefs(IntArray(value, "objectTileRefs")),
				tileFlags.Select(number => (byte)number).ToArray(), visible, ParseLightAllocator(Property(value, "lightAllocator")));
		}

		internal static int[] CheckedRefs(int[] values)
		{
			if (values.Length != CellCount || values.Any(value => value is < -MonsterCapacity or > MonsterCapacity))
				throw new InvalidDataException("Encounter tile references have invalid length or range.");
			return values;
		}

		private static AuthoritativeCathedralPopulationMonsterState ParseMonster(JsonElement value)
		{
			RequireProperties(value, "encounter monster", MonsterProperties);
			return new() {
				Slot = Int(value, "slot"), Type = Int(value, "type"), LevelType = Int(value, "levelType"), Mode = Int(value, "mode"), Goal = Int(value, "goal"), GoalVar1 = Int(value, "goalVar1"), GoalVar2 = Int(value, "goalVar2"), GoalVar3 = Int(value, "goalVar3"), Var1 = Int(value, "var1"), Var2 = Int(value, "var2"), Var3 = Int(value, "var3"), PathCount = Int(value, "pathCount"), Tile = Point(Property(value, "tile")), Future = Point(Property(value, "future")), Old = Point(Property(value, "old")), Last = Point(Property(value, "last")), Direction = Int(value, "direction"), Enemy = Int(value, "enemy"), EnemyPosition = Point(Property(value, "enemyPosition")), Invalid = Bool(value, "invalid"), Ai = Int(value, "ai"), Intelligence = Int(value, "intelligence"), ActiveForTicks = Int(value, "activeForTicks"), UniqueType = Int(value, "uniqueType"), UniqueTranslation = Int(value, "uniqueTranslation"), CorpseId = Int(value, "corpseId"), WhoHit = Int(value, "whoHit"), MinDamage = Int(value, "minDamage"), MaxDamage = Int(value, "maxDamage"), MinDamageSpecial = Int(value, "minDamageSpecial"), MaxDamageSpecial = Int(value, "maxDamageSpecial"), ArmorClass = Int(value, "armorClass"), Resistance = Int(value, "resistance"), TalkMessage = Int(value, "talkMessage"), Leader = Int(value, "leader"), LeaderRelation = Int(value, "leaderRelation"), PackSize = Int(value, "packSize"), Flags = UInt(value, "flags"), RndItemSeed = UInt(value, "rndItemSeed"), AiSeed = UInt(value, "aiSeed"), LightId = Int(value, "lightId"), HitPoints = Int(value, "hitPoints"), MaxHitPoints = Int(value, "maxHitPoints"), AnimTicksPerFrame = Int(value, "animTicksPerFrame"), AnimTickCounter = Int(value, "animTickCounter"), AnimNumberOfFrames = Int(value, "animNumberOfFrames"), AnimCurrentFrame = Int(value, "animCurrentFrame"), Temp = Point(Property(value, "temp")), GolemToHit = Int(value, "golemToHit"), ReducePlayerStrength = Int(value, "reducePlayerStrength"), ReducePlayerMagic = Int(value, "reducePlayerMagic"), ReducePlayerDexterity = Int(value, "reducePlayerDexterity"), ReducePlayerVitality = Int(value, "reducePlayerVitality"), ReducePlayerMaxHP = Int(value, "reducePlayerMaxHP"), ReducePlayerMaxMana = Int(value, "reducePlayerMaxMana"),
			};
		}

		private static LightAllocatorInput ParseLightAllocator(JsonElement value)
		{
			RequireProperties(value, "light allocator", "activeLightSlots", "activeLightCount", "updateLighting", "disableLighting", "lights");
			var slots = IntArray(value, "activeLightSlots");
			RequirePermutation(slots, LightCapacity, "Active light slots");
			var activeLightCount = Int(value, "activeLightCount");
			if (activeLightCount is < 0 or > LightCapacity)
				throw new InvalidDataException("Active light count is outside the native light pool.");
			var lights = Array(value, "lights").EnumerateArray().Select(ParseLight).ToArray();
			if (lights.Length != LightCapacity)
				throw new InvalidDataException("Light records must contain all 32 slots.");
			RequirePermutation(lights.Select(light => light.LightId).ToArray(), LightCapacity, "Light records");
			return new(slots, activeLightCount, Bool(value, "updateLighting"), Bool(value, "disableLighting"), lights);
		}

		private static AuthoritativeCathedralEncounterLightState ParseLight(JsonElement value)
		{
			RequireProperties(value, "light", "slot", "tile", "offset", "old", "radius", "oldRadius", "invalid", "changed");
			var slot = Int(value, "slot");
			var offset = SignedOffset(Property(value, "offset"));
			var radius = Int(value, "radius");
			var oldRadius = Int(value, "oldRadius");
			if (slot is < 0 or >= LightCapacity || radius is < 0 or > byte.MaxValue || oldRadius is < 0 or > byte.MaxValue)
				throw new InvalidDataException("Native light metadata is outside its typed range.");
			return new(slot, Point(Property(value, "tile")), Point(Property(value, "old")), offset.X, offset.Y, radius, oldRadius, Bool(value, "invalid"), Bool(value, "changed"));
		}

		private static PopulationCorpus LoadPopulation(string populationDirectory, string oracleDirectory)
		{
			var inputEntries = RegularEntries(populationDirectory).ToArray();
			if (inputEntries.Length != 1 || !string.Equals(Path.GetFileName(inputEntries[0]), "native-population-inputs.json", StringComparison.Ordinal))
				throw new InvalidDataException("Population input directory must contain exactly native-population-inputs.json.");
			var oracleEntries = RegularEntries(oracleDirectory).ToArray();
			if (oracleEntries.Length != 17 || !oracleEntries.Any(value => string.Equals(Path.GetFileName(value), "metadata.json", StringComparison.Ordinal)))
				throw new InvalidDataException("Population oracle directory must contain metadata.json and sixteen snapshots.");
			var metadataPath = Path.Combine(oracleDirectory, "metadata.json");
			var metadata = ParseFile(metadataPath, MaxMetadataBytes);
			RequireProperties(metadata, "population metadata", "format", "version", "policy", "nativeWidth", "nativeHeight", "rowMajor", "headless", "soundInitialized", "game", "gameMode", "difficulty", "playerLifeQ6", "caseCount", "depths", "seeds", "entries", "storySeed", "objectFields", "monsterFields", "rngBoundaries", "nativeObjectTypes", "nativeObjectCatalog");
			if (String(metadata, "format") != PopulationOracleFormat || Int(metadata, "version") != 1 || String(metadata, "policy") != PopulationOraclePolicy || Int(metadata, "caseCount") != 16)
				throw new InvalidDataException("Population metadata does not match the native corpus.");
			if (Int(metadata, "nativeWidth") != 112 || Int(metadata, "nativeHeight") != 112 || !Bool(metadata, "rowMajor")
				|| !Bool(metadata, "headless") || Bool(metadata, "soundInitialized") || String(metadata, "game") != "diablo"
				|| String(metadata, "gameMode") != "singleplayer" || String(metadata, "difficulty") != "normal"
				|| Int(metadata, "playerLifeQ6") != 64 || UInt(metadata, "storySeed") != 324508639U
				|| !IntArray(metadata, "depths").SequenceEqual(new[] { 1, 2, 3, 4 })
				|| !UIntArray(metadata, "seeds").SequenceEqual(new uint[] { 2588, 743271966 })
				|| !StringArray(metadata, "entries").SequenceEqual(new[] { "main", "previous" }))
				throw new InvalidDataException("Population metadata does not match the ordinary Cathedral scope.");
			var objects = Array(metadata, "nativeObjectCatalog").EnumerateArray().Select(ParseObject).ToArray();
			if (objects.Length != 109 || !objects.Select(value => value.NativeObjectId).SequenceEqual(Enumerable.Range(0, 109)))
				throw new InvalidDataException("Population object metadata must be complete and ordered.");
			var root = ParseFile(inputEntries[0], MaxInputBytes);
			RequireProperties(root, "population sidecar", "format", "version", "policy", "caseCount", "levelGenerationTableLoaded", "storyMessages", "constants", "monsterData", "cases", "freshBestiarySlots");
			if (String(root, "format") != PopulationFormat || Int(root, "version") != 2 || String(root, "policy") != PopulationPolicy || Int(root, "caseCount") != 16 || !Bool(root, "levelGenerationTableLoaded") || !Bool(root, "freshBestiarySlots"))
				throw new InvalidDataException("Population sidecar is not the required v2 input corpus.");
			var story = IntArray(root, "storyMessages");
			if (story.Length != 3) throw new InvalidDataException("Three story messages are required.");
			ValidatePopulationConstants(Property(root, "constants"));
			var raw = Array(root, "monsterData").EnumerateArray().Select(ParseRawMonster).ToArray();
			if (raw.Length != 138 || !raw.Select(value => value.NativeMonsterId).SequenceEqual(Enumerable.Range(0, 138)))
				throw new InvalidDataException("Raw monster data must be complete and ordered.");
			var cases = Array(root, "cases").EnumerateArray().Select(ParsePopulationCase).ToArray();
			if (cases.Length != 16) throw new InvalidDataException("Sixteen population cases are required.");
			RequireCompleteCaseNames(cases.Select(value => value.Snapshot).ToArray());
			return new(cases, raw, objects, story);
		}

		private static ObjectMetadata ParseObject(JsonElement value)
		{
			RequireProperties(value, "native object", "nativeObjectId", "graphicIndex", "minLevel", "maxLevel", "levelType", "theme", "quest", "flags", "animDelay", "animLength", "animWidth", "selectionRegion", "behavior");
			return new(Int(value, "nativeObjectId"), Int(value, "flags"), Int(value, "animDelay"), Int(value, "animLength"), Int(value, "animWidth"), Int(value, "selectionRegion"));
		}

		private static RawMonster ParseRawMonster(JsonElement value)
		{
			RequireProperties(value, "raw monster", "nativeMonsterId", "name", "availability", "image", "minDungeonLevel", "maxDungeonLevel", "level", "minHp", "maxHp", "ai", "intelligence", "minDamage", "maxDamage", "minDamageSpecial", "maxDamageSpecial", "armorClass", "resistance", "resistanceHell", "abilityFlags", "hasSpecial", "width", "frames", "rates", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana");
			var frames = IntArray(value, "frames");
			var rates = IntArray(value, "rates");
			if (frames.Length != 6 || rates.Length != 6) throw new InvalidDataException("Raw monster animations require six values.");
			return new(Int(value, "nativeMonsterId"), String(value, "name"), Int(value, "availability"), Int(value, "image"), Int(value, "minDungeonLevel"), Int(value, "maxDungeonLevel"), Int(value, "minHp"), Int(value, "maxHp"), Int(value, "ai"), Int(value, "intelligence"), UInt(value, "abilityFlags"), Bool(value, "hasSpecial"), Int(value, "width"), frames, rates, Int(value, "minDamage"), Int(value, "maxDamage"), Int(value, "minDamageSpecial"), Int(value, "maxDamageSpecial"), Int(value, "armorClass"), Int(value, "resistance"), Int(value, "reducePlayerStrength"), Int(value, "reducePlayerMagic"), Int(value, "reducePlayerDexterity"), Int(value, "reducePlayerVitality"), Int(value, "reducePlayerMaxHP"), Int(value, "reducePlayerMaxMana"));
		}

		private static PopulationCase ParsePopulationCase(JsonElement value)
		{
			RequireProperties(value, "population case", "snapshot", "input", "expectedOutput");
			var expectedOutput = Property(value, "expectedOutput");
			RequireProperties(expectedOutput, "population expected output", "pieces", "special", "transparency", "monsterSupplemental", "themes");
			if (IntArray(expectedOutput, "pieces").Length != CellCount || IntArray(expectedOutput, "special").Length != CellCount || IntArray(expectedOutput, "transparency").Length != CellCount)
				throw new InvalidDataException("Population expected geometry arrays must cover the complete dungeon.");
			foreach (var supplemental in Array(expectedOutput, "monsterSupplemental").EnumerateArray()) {
				RequireProperties(supplemental, "population monster supplemental", "slot", "temp", "golemToHit", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana");
				_ = Point(Property(supplemental, "temp"));
			}
			foreach (var theme in Array(expectedOutput, "themes").EnumerateArray())
				RequireProperties(theme, "population selected theme", "type", "region");
			var input = Property(value, "input");
			RequireProperties(input, "population input", "depth", "populationSeed", "storySeed", "pieces", "special", "transparency", "pieceProperties", "populatedCells", "playerTileRefs", "monsterTileRefs", "freshMonsterSlots", "player", "trapPercent", "orderedBestiary", "prerequisites");
			if (!Bool(input, "freshMonsterSlots")) throw new InvalidDataException("Population cases require fresh monster slots.");
			_ = BoolArray(input, "populatedCells");
			foreach (var bestiary in Array(input, "orderedBestiary").EnumerateArray()) {
				RequireProperties(bestiary, "ordered bestiary entry", "index", "type", "skeleton", "placeFlags", "corpseId", "frames", "rates", "widths");
				foreach (var name in new[] { "frames", "rates", "widths" })
					if (IntArray(bestiary, name).Length != 6)
						throw new InvalidDataException("Ordered bestiary animation arrays require six values.");
			}
			var player = Property(input, "player");
			RequireProperties(player, "population player", "id", "level", "active", "levelChanging", "tile", "future");
			var prerequisite = Property(input, "prerequisites");
			RequireProperties(prerequisite, "population prerequisites", "nextTransparencyValue", "populatedCells", "triggerPositions");
			var prereq = new Prerequisites(Int(prerequisite, "nextTransparencyValue"), RequiredBoolArray(prerequisite, "populatedCells", "prerequisites.populatedCells"), Array(prerequisite, "triggerPositions").EnumerateArray().Select(Point).ToArray());
			var snapshot = String(value, "snapshot");
			var depth = Int(input, "depth");
			var populationSeed = UInt(input, "populationSeed");
			ValidateCaseScope(snapshot, depth, populationSeed, snapshot.Split('-')[^1].Replace(".json", string.Empty, StringComparison.Ordinal));
			return new(snapshot, new(depth, populationSeed, UInt(input, "storySeed"), IntArray(input, "pieces"), IntArray(input, "special"), IntArray(input, "transparency"), IntArray(input, "pieceProperties"), RequiredBoolArray(input, "populatedCells", "input.populatedCells"), IntArray(input, "playerTileRefs"), IntArray(input, "monsterTileRefs"), new(Int(player, "id"), Int(player, "level"), Bool(player, "active"), Bool(player, "levelChanging"), Point(Property(player, "tile")), Point(Property(player, "future"))), Int(input, "trapPercent"), prereq));
		}

		private static void ValidatePopulationConstants(JsonElement value)
		{
			var names = new[] { "golemNativeType", "southDirection", "standMode", "normalGoal", "specialAttackMode", "noUniqueType", "noLeader", "noLight", "textNone", "monsterNoEnemyFlag", "golemFlag", "berserkFlag", "targetsMonsterFlag", "lockAnimationFlag", "allowSpecialFlag", "tileSolidFlag", "tileTrapFlag", "arrow", "firebolt", "lightningControl", "maxObjects", "maxMonsters" };
			RequireProperties(value, "population constants", names);
			var expected = new[] { 109, 0, 0, 1, 7, 255, 255, -1, -1, 1024, 32, 2048, 16, 2, 4, 1, 128, 0, 1, 7, 127, 200 };
			for (var index = 0; index < names.Length; index++)
				if (Int(value, names[index]) != expected[index])
					throw new InvalidDataException($"Population constant '{names[index]}' does not match the native contract.");
		}
	}

	private static readonly string[] MonsterProperties = [
		"slot", "type", "levelType", "mode", "goal", "goalVar1", "goalVar2", "goalVar3", "var1", "var2", "var3", "pathCount", "tile", "future", "old", "last", "direction", "enemy", "enemyPosition", "invalid", "ai", "intelligence", "activeForTicks", "uniqueType", "uniqueTranslation", "corpseId", "whoHit", "minDamage", "maxDamage", "minDamageSpecial", "maxDamageSpecial", "armorClass", "resistance", "talkMessage", "leader", "leaderRelation", "packSize", "flags", "rndItemSeed", "aiSeed", "lightId", "hitPoints", "maxHitPoints", "animTicksPerFrame", "animTickCounter", "animNumberOfFrames", "animCurrentFrame", "temp", "golemToHit", "reducePlayerStrength", "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP", "reducePlayerMaxMana",
	];

	private sealed record LightAllocatorInput(int[] ActiveLightSlots, int ActiveLightCount, bool UpdateLighting, bool DisableLighting, AuthoritativeCathedralEncounterLightState[] Lights);
	private sealed record EncounterState(uint RandomState, int TotalMonsters, int ActiveMonsterCount, int[] ActiveMonsterIds, AuthoritativeCathedralPopulationMonsterState[] Monsters, int[] MonsterTileRefs, int[] PlayerTileRefs, int[] ObjectTileRefs, byte[] TileFlags, bool[] VisibleTransparencyRegions, LightAllocatorInput LightAllocator);
	private sealed record EncounterCase(string Snapshot, int Depth, uint InputSeed, string Entry, int DensityDivisor, uint PreClearRandomState, uint PostClearRandomState, int[] PreThemeTileFlags, EncounterState PreState, EncounterState ExpectedState);
	private sealed record RawMonster(int NativeMonsterId, string Name, int Availability, int Image, int MinimumDungeonLevel, int MaximumDungeonLevel, int MinHp, int MaxHp, int Ai, int Intelligence, uint AbilityFlags, bool HasSpecial, int Width, int[] Frames, int[] Rates, int MinDamage, int MaxDamage, int MinDamageSpecial, int MaxDamageSpecial, int ArmorClass, int Resistance, int ReducePlayerStrength, int ReducePlayerMagic, int ReducePlayerDexterity, int ReducePlayerVitality, int ReducePlayerMaxHP, int ReducePlayerMaxMana);
	private sealed record ObjectMetadata(int NativeObjectId, int Flags, int AnimDelay, int AnimLength, int AnimWidth, int SelectionRegion);
	private sealed record Prerequisites(int NextTransparencyValue, bool[] PopulatedCells, AuthoritativeCathedralPopulationPoint[] TriggerPositions);
	private sealed record PopulationInput(int Depth, uint PopulationSeed, uint StorySeed, int[] Pieces, int[] Special, int[] Transparency, int[] PieceProperties, bool[] PopulatedCells, int[] PlayerTileRefs, int[] MonsterTileRefs, PlayerInput Player, int TrapPercent, Prerequisites Prerequisites);
	private sealed record PlayerInput(int Id, int Level, bool Active, bool LevelChanging, AuthoritativeCathedralPopulationPoint Tile, AuthoritativeCathedralPopulationPoint Future);
	private sealed record PopulationCase(string Snapshot, PopulationInput Input);
	private sealed record PopulationCorpus(IReadOnlyList<PopulationCase> Cases, IReadOnlyList<RawMonster> RawMonsters, IReadOnlyList<ObjectMetadata> Objects, int[] StoryMessages);
}
