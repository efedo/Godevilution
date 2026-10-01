using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** Strict external catalog for the original Diablo Cathedral depths one through four. */
public static class AuthoritativeCathedralDepthRuntimeCatalog
{
	public const string TableName = "native_cathedral_depths.tsv";
	public const string CatalogId = "native-cathedral-depths";
	public const string GeneratorId = "original-diablo.cathedral-depths";
	public const int GeneratorRevision = 1;

	private static readonly string[] RequiredColumns =
	{
		"level_id",
		"level_symbol",
		"depth",
		"generator_id",
		"generator_revision",
		"dungeon_seed",
		"entry",
		"policy",
	};

	public static AuthoritativeCathedralDepthRuntimeCatalogDefinition Load(
		string contents,
		StableContentIdCatalog stableIds)
	{
		ArgumentNullException.ThrowIfNull(contents);
		ArgumentNullException.ThrowIfNull(stableIds);

		var table = TsvTable.Parse(TableName, contents);
		if (!table.Columns.SequenceEqual(RequiredColumns, StringComparer.Ordinal))
			throw new InvalidDataException(
				$"{TableName} must have the exact ordered header: {string.Join(',', RequiredColumns)}.");
		if (table.Rows.Count != AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth)
			throw new InvalidDataException(
				$"{TableName} must contain exactly four ordered Cathedral depth rows.");

		var definitions = new List<AuthoritativeCathedralDepthRuntimeDefinition>(table.Rows.Count);
		var levelIds = new HashSet<uint>();
		var levelSymbols = new HashSet<string>(StringComparer.Ordinal);
		foreach (var (row, index) in table.Rows.Select((row, index) => (row, index)))
		{
			var expectedDepth = index + 1;
			var levelId = ParseUInt(row, "level_id");
			var levelSymbol = RequiredText(row, "level_symbol");
			var depth = ParseInt(row, "depth");
			if (depth != expectedDepth || levelId != (uint)expectedDepth)
				throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} must be ordered Cathedral depth {expectedDepth} with level id {expectedDepth}.");

			var generatorId = RequiredText(row, "generator_id");
			if (!string.Equals(generatorId, GeneratorId, StringComparison.Ordinal))
				throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} has unsupported generator_id '{generatorId}'.");
			var revision = ParseInt(row, "generator_revision");
			if (revision != GeneratorRevision)
				throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} has unsupported generator_revision {revision}.");

			var seed = ParseUInt(row, "dungeon_seed");
			var entryText = RequiredText(row, "entry");
			var entry = entryText switch
			{
				"main" => AuthoritativeCathedralEntry.Main,
				"previous" => AuthoritativeCathedralEntry.Previous,
				_ => throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} has unsupported entry '{entryText}'."),
			};
			var policyText = RequiredText(row, "policy");
			var policy = policyText switch
			{
				"quest-disabled-original-diablo" => AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
				_ => throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} has unsupported policy '{policyText}'."),
			};

			if (!levelIds.Add(levelId) || !levelSymbols.Add(levelSymbol))
				throw new InvalidDataException(
					$"{TableName} contains a duplicate level identity at row {row.LineNumber}.");
			if (!stableIds.TryResolve("level", levelSymbol, out var resolvedId) || resolvedId != levelId)
				throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} does not match the stable level identity for '{levelSymbol}'.");

			definitions.Add(new AuthoritativeCathedralDepthRuntimeDefinition(
				levelId,
				levelSymbol,
				depth,
				seed,
				entry,
				policy));
		}

		return new AuthoritativeCathedralDepthRuntimeCatalogDefinition(
			CatalogId,
			GeneratorId,
			GeneratorRevision,
			HashText(table.ToCanonicalTsv()),
			definitions);
	}

	private static string RequiredText(TsvRow row, string column)
	{
		var value = row.Required(column).Trim();
		if (value.Length == 0)
			throw new InvalidDataException($"{TableName} row {row.LineNumber} has a blank {column}.");
		return value;
	}

	private static uint ParseUInt(TsvRow row, string column)
	{
		var text = RequiredText(row, column);
		if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
			throw new InvalidDataException(
				$"{TableName} row {row.LineNumber} has an invalid {column} '{text}'.");
		return value;
	}

	private static int ParseInt(TsvRow row, string column)
	{
		var text = RequiredText(row, column);
		if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			throw new InvalidDataException(
				$"{TableName} row {row.LineNumber} has an invalid {column} '{text}'.");
		return value;
	}

	private static string HashText(string value)
		=> Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class AuthoritativeCathedralDepthRuntimeCatalogDefinition
{
	internal AuthoritativeCathedralDepthRuntimeCatalogDefinition(
		string catalogId,
		string generatorId,
		int generatorRevision,
		string catalogFingerprint,
		IEnumerable<AuthoritativeCathedralDepthRuntimeDefinition> definitions)
	{
		CatalogId = catalogId;
		GeneratorId = generatorId;
		GeneratorRevision = generatorRevision;
		CatalogFingerprint = catalogFingerprint;
		Definitions = new ReadOnlyCollection<AuthoritativeCathedralDepthRuntimeDefinition>(definitions.ToArray());
	}

	public string CatalogId { get; }
	public string GeneratorId { get; }
	public int GeneratorRevision { get; }
	public string CatalogFingerprint { get; }
	public IReadOnlyList<AuthoritativeCathedralDepthRuntimeDefinition> Definitions { get; }

	/** Generates every depth eagerly and returns a plan without mutating the world. */
	public AuthoritativeCathedralDepthGenerationPlan Prepare(
		AuthoritativeWorld world,
		AuthoritativeNativeDunGeometryCatalog geometry)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(geometry);
		if (Definitions.Count != AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth)
			throw new InvalidDataException("Native Cathedral depth generation requires exactly four definitions.");

		var geometryCatalogFingerprint = AuthoritativeCathedralLevel1RuntimeCatalog.ComputeGeometryCatalogFingerprint(geometry);
		var candidates = new List<AuthoritativeCathedralDepthGenerationCandidate>(Definitions.Count);
		for (var index = 0; index < Definitions.Count; index++)
		{
			var definition = Definitions[index];
			var expectedDepth = index + 1;
			if (definition.Depth != expectedDepth || definition.LevelId != (uint)expectedDepth)
				throw new InvalidDataException("Native Cathedral depth definitions must remain ordered depths 1 through 4.");

			var original = world.Levels.SingleOrDefault(level => level.LevelId == definition.LevelId);
			if (original is null)
				throw new InvalidDataException(
					$"Native Cathedral row targets unknown level id {definition.LevelId}.");
			if (!string.Equals(original.StableSymbol, definition.LevelSymbol, StringComparison.Ordinal))
				throw new InvalidDataException(
					$"Native Cathedral row level {definition.LevelId} has stable symbol '{definition.LevelSymbol}', but the world has '{original.StableSymbol}'.");

			var selected = AuthoritativeCathedralDepthGenerator.Generate(
				definition.Depth,
				definition.DungeonSeed,
				definition.Entry,
				definition.Policy);
			var mainEntry = new AuthoritativeCathedralPoint(
				selected.StairsUp.X * 2 + 19,
				selected.StairsUp.Y * 2 + 20);
			var previousEntry = new AuthoritativeCathedralPoint(
				selected.StairsDown.X * 2 + 19,
				selected.StairsDown.Y * 2 + 19);
			var materialized = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
				definition.LevelId,
				selected,
				geometry.Pieces,
				geometry.TileProperties,
				definition.LevelSymbol,
				original.IsDefaultStart,
				original.SuppressMultiplayerDeathDrops);

			AuthoritativeCathedralLevel1RuntimeCatalogDefinition.ValidateFineGeometry(materialized.Level);
			var routes = AuthoritativeCathedralLevel1RuntimeCatalogDefinition.FindRoutes(
				materialized.Level,
				mainEntry,
				previousEntry);
			var fineFingerprint = AuthoritativeCathedralLevel1RuntimeCatalog.ComputeFineFingerprint(materialized.Level);
			var identity = new AuthoritativeCathedralDepthGenerationIdentity(
				CatalogId,
				GeneratorId,
				GeneratorRevision,
				definition.LevelId,
				definition.LevelSymbol,
				definition.Depth,
				definition.Policy,
				definition.DungeonSeed,
				definition.Entry,
				selected.AttemptSeed,
				selected.FinalRandomState,
				AuthoritativeCathedralLevel1RuntimeCatalog.HashSequence("coarse", new[] {
					AuthoritativeCathedralLevel1RuntimeCatalog.HashSequence("mega", selected.MegaTiles),
					AuthoritativeCathedralLevel1RuntimeCatalog.HashSequence("transparency", selected.NativeTransparency),
				}),
				fineFingerprint,
				AuthoritativeCathedralLevel1RuntimeCatalog.HashRoutes(routes),
				AuthoritativeCathedralLevel1RuntimeCatalog.CombineCatalogFingerprints(
					CatalogFingerprint,
					geometryCatalogFingerprint));

			candidates.Add(new AuthoritativeCathedralDepthGenerationCandidate(
				original,
				materialized.Level,
				materialized.GenerationResult,
				routes,
				identity));
		}

		return new AuthoritativeCathedralDepthGenerationPlan(candidates);
	}
}

public sealed record AuthoritativeCathedralDepthRuntimeDefinition(
	uint LevelId,
	string LevelSymbol,
	int Depth,
	uint DungeonSeed,
	AuthoritativeCathedralEntry Entry,
	AuthoritativeCathedralDepthGenerationPolicy Policy);

/** Identity retained for saved-game and peer validation of one generated depth. */
public sealed record AuthoritativeCathedralDepthGenerationIdentity(
	string CatalogId,
	string GeneratorId,
	int GeneratorRevision,
	uint LevelId,
	string LevelSymbol,
	int Depth,
	AuthoritativeCathedralDepthGenerationPolicy Policy,
	uint DungeonSeed,
	AuthoritativeCathedralEntry Entry,
	uint AttemptSeed,
	uint FinalRandomState,
	string CoarseFingerprint,
	string FineFingerprint,
	string RouteFingerprint,
	string CatalogFingerprint);

public sealed class AuthoritativeCathedralDepthGenerationCandidate
{
	internal AuthoritativeCathedralDepthGenerationCandidate(
		AuthoritativeLevel originalLevel,
		AuthoritativeLevel level,
		AuthoritativeCathedralDepthResult result,
		AuthoritativeCathedralLevel1RoutePoints routes,
		AuthoritativeCathedralDepthGenerationIdentity identity)
	{
		OriginalLevel = originalLevel;
		Level = level;
		GenerationResult = result;
		Routes = routes;
		Identity = identity;
	}

	public AuthoritativeLevel OriginalLevel { get; }
	public AuthoritativeLevel Level { get; }
	public AuthoritativeCathedralDepthResult GenerationResult { get; }
	public AuthoritativeCathedralLevel1RoutePoints Routes { get; }
	public AuthoritativeCathedralDepthGenerationIdentity Identity { get; }
}

public sealed class AuthoritativeCathedralDepthGenerationPlan
{
	internal AuthoritativeCathedralDepthGenerationPlan(
		IEnumerable<AuthoritativeCathedralDepthGenerationCandidate> candidates)
	{
		Candidates = new ReadOnlyCollection<AuthoritativeCathedralDepthGenerationCandidate>(candidates.ToArray());
		if (Candidates.Count != AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth)
			throw new InvalidDataException("Native Cathedral depth generation plan requires exactly four candidates.");
	}

	public IReadOnlyList<AuthoritativeCathedralDepthGenerationCandidate> Candidates { get; }

	/** Applies all staged levels atomically, rolling back source instances on replacement failure. */
	public void Apply(AuthoritativeWorld world)
	{
		ArgumentNullException.ThrowIfNull(world);
		var current = new List<AuthoritativeLevel>(Candidates.Count);
		foreach (var candidate in Candidates)
		{
			var level = world.Levels.SingleOrDefault(item => item.LevelId == candidate.OriginalLevel.LevelId);
			if (!ReferenceEquals(level, candidate.OriginalLevel))
				throw new InvalidOperationException("Native Cathedral depth generation plan is stale and was not applied.");
			current.Add(level!);
		}

		try
		{
			foreach (var candidate in Candidates)
				world.ReplaceLevel(candidate.Level);
		}
		catch
		{
			foreach (var original in current)
				world.ReplaceLevel(original);
			throw;
		}
	}
}
