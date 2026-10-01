using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/// <summary>
/// The only external generator identity admitted by the opt-in Cathedral level-one runtime.
/// </summary>
public static class AuthoritativeCathedralLevel1RuntimeCatalog
{
	public const string TableName = "native_cathedral_level1.tsv";
	public const string GeneratorId = "original-diablo.cathedral-level1";
	public const int GeneratorRevision = 1;
	public const string CatalogId = "native-cathedral-level1";

	private static readonly string[] RequiredColumns =
	{
		"level_id",
		"level_symbol",
		"generator_id",
		"generator_revision",
		"dungeon_seed",
		"entry",
	};

	/// <summary>
	/// Parses the optional table. The caller must only invoke this for a table selected by an
	/// opt-in content profile; an absent table is represented by not calling this method.
	/// </summary>
	public static AuthoritativeCathedralLevel1RuntimeCatalogDefinition Load(
		string contents,
		StableContentIdCatalog stableIds)
	{
		ArgumentNullException.ThrowIfNull(contents);
		ArgumentNullException.ThrowIfNull(stableIds);

		var table = TsvTable.Parse(TableName, contents);
		if (table.Columns.Count != RequiredColumns.Length || !table.Columns.SequenceEqual(RequiredColumns, StringComparer.Ordinal))
			throw new InvalidDataException($"{TableName} must have the exact ordered header: {string.Join(',', RequiredColumns)}.");

		if (table.Rows.Count != 1)
			throw new InvalidDataException($"{TableName} must contain exactly one ordinary Cathedral level-one row.");

		var definitions = new List<AuthoritativeCathedralLevel1RuntimeDefinition>(table.Rows.Count);
		var levelIds = new HashSet<uint>();
		var levelSymbols = new HashSet<string>(StringComparer.Ordinal);
		foreach (var row in table.Rows)
		{
			var levelId = ParseUInt(row, "level_id");
			if (levelId != 1)
				throw new InvalidDataException($"{TableName} row {row.LineNumber} must target ordinary Cathedral level id 1.");
			var levelSymbol = RequiredText(row, "level_symbol");
			var generatorId = RequiredText(row, "generator_id");
			if (!string.Equals(generatorId, GeneratorId, StringComparison.Ordinal))
				throw new InvalidDataException($"{TableName} row {row.LineNumber} has unsupported generator_id '{generatorId}'.");

			var revision = ParseInt(row, "generator_revision");
			if (revision != GeneratorRevision)
				throw new InvalidDataException($"{TableName} row {row.LineNumber} has unsupported generator_revision {revision}.");

			var seed = ParseUInt(row, "dungeon_seed");
			var entryText = RequiredText(row, "entry");
			var entry = entryText switch
			{
				"main" => AuthoritativeCathedralEntry.Main,
				"previous" => AuthoritativeCathedralEntry.Previous,
				_ => throw new InvalidDataException($"{TableName} row {row.LineNumber} has unsupported entry '{entryText}'."),
			};

			if (!levelIds.Add(levelId) || !levelSymbols.Add(levelSymbol))
				throw new InvalidDataException($"{TableName} contains a duplicate level identity at row {row.LineNumber}.");
			if (!stableIds.TryResolve("level", levelSymbol, out var resolvedId) || resolvedId != levelId)
				throw new InvalidDataException($"{TableName} row {row.LineNumber} does not match the stable level identity for '{levelSymbol}'.");

			definitions.Add(new AuthoritativeCathedralLevel1RuntimeDefinition(levelId, levelSymbol, seed, entry));
		}

		return new AuthoritativeCathedralLevel1RuntimeCatalogDefinition(
			CatalogId,
			GeneratorId,
			GeneratorRevision,
			HashText(table.ToCanonicalTsv()),
			definitions);
	}

	private static string RequiredText(TsvRow row, string column)
	{
		var value = row.Required(column);
		if (string.IsNullOrWhiteSpace(value))
			throw new InvalidDataException($"{TableName} row {row.LineNumber} has a blank {column}.");
		return value;
	}

	private static uint ParseUInt(TsvRow row, string column)
	{
		var text = RequiredText(row, column);
		if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
			throw new InvalidDataException($"{TableName} row {row.LineNumber} has an invalid {column} '{text}'.");
		return value;
	}

	private static int ParseInt(TsvRow row, string column)
	{
		var text = RequiredText(row, column);
		if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			throw new InvalidDataException($"{TableName} row {row.LineNumber} has an invalid {column} '{text}'.");
		return value;
	}

	private static string HashText(string value)
		=> Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

	internal static string HashSequence<T>(string label, IEnumerable<T> values)
	{
		var builder = new StringBuilder(label);
		var count = 0;
		foreach (var value in values)
		{
			builder.Append('|').Append(count++).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
		}
		builder.Append("|count=").Append(count);
		return HashText(builder.ToString());
	}

	internal static string HashRoutes(AuthoritativeCathedralLevel1RoutePoints routes)
		=> HashSequence("routes", new[]
		{
			$"up:{routes.UpTrigger.X},{routes.UpTrigger.Y}",
			$"down:{routes.DownTrigger.X},{routes.DownTrigger.Y}",
			$"main:{routes.MainEntry.X},{routes.MainEntry.Y}",
			$"previous:{routes.PreviousEntry.X},{routes.PreviousEntry.Y}",
		});

	/// <summary>Computes the identity of the effective fine-grid geometry after materialization.</summary>
	internal static string ComputeFineFingerprint(AuthoritativeLevel level)
	{
		ArgumentNullException.ThrowIfNull(level);
		return HashSequence("fine-effective", new[]
		{
			HashSequence("pieces", level.NativePieceLayer),
			HashSequence("transparency", level.NativeTransparency),
			HashSequence("collision", level.BlockedCells),
			HashSequence("missile", level.MissileBlockedCells),
			HashSequence("vision", level.NativeVisionBlockedCells),
			HashSequence("piece-collision-provenance", level.NativePieceCollisionCells),
			HashSequence("piece-missile-provenance", level.NativePieceMissileBlockedCells),
			HashSequence("explicit-vision-provenance", new[] { level.HasExplicitNativeVisionBlockedCells }),
			HashSequence("piece-properties", level.NativePieceProperties
				.OrderBy(pair => pair.Key)
				.Select(pair => $"{pair.Key}:{(byte)pair.Value}")),
		});
	}

	/// <summary>Computes the semantic fingerprint of the external TIL/SOL catalog pair.</summary>
	internal static string ComputeGeometryCatalogFingerprint(AuthoritativeNativeDunGeometryCatalog geometry)
	{
		ArgumentNullException.ThrowIfNull(geometry);
		return HashSequence("native-geometry", new[]
		{
			HashSequence("pieces", geometry.Pieces.Definitions
				.Select(definition => $"{definition.MegaTileId}:{definition.NorthWestPieceId},{definition.NorthEastPieceId},{definition.SouthWestPieceId},{definition.SouthEastPieceId}")),
			HashSequence("properties", geometry.TileProperties.Definitions
				.Select(property => $"{property.PieceId}:{(byte)property.Flags}")),
		});
	}

	internal static string CombineCatalogFingerprints(
		string tableFingerprint,
		string geometryFingerprint)
		=> HashSequence("catalog", new[] { tableFingerprint, geometryFingerprint });
}

public sealed class AuthoritativeCathedralLevel1RuntimeCatalogDefinition
{
	internal AuthoritativeCathedralLevel1RuntimeCatalogDefinition(
		string catalogId,
		string generatorId,
		int generatorRevision,
		string catalogFingerprint,
		IEnumerable<AuthoritativeCathedralLevel1RuntimeDefinition> definitions)
	{
		CatalogId = catalogId;
		GeneratorId = generatorId;
		GeneratorRevision = generatorRevision;
		CatalogFingerprint = catalogFingerprint;
		Definitions = new ReadOnlyCollection<AuthoritativeCathedralLevel1RuntimeDefinition>(definitions.ToArray());
	}

	public string CatalogId { get; }
	public string GeneratorId { get; }
	public int GeneratorRevision { get; }
	public string CatalogFingerprint { get; }
	public IReadOnlyList<AuthoritativeCathedralLevel1RuntimeDefinition> Definitions { get; }

	/// <summary>
	/// Generates and validates every candidate before returning a plan. No world state is mutated.
	/// </summary>
	public AuthoritativeCathedralLevel1GenerationPlan Prepare(
		AuthoritativeWorld world,
		AuthoritativeNativeDunGeometryCatalog geometry)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(geometry);

		var candidates = new List<AuthoritativeCathedralLevel1GenerationCandidate>(Definitions.Count);
		var geometryCatalogFingerprint = AuthoritativeCathedralLevel1RuntimeCatalog.ComputeGeometryCatalogFingerprint(geometry);
		foreach (var definition in Definitions)
		{
			var original = world.Levels.SingleOrDefault(level => level.LevelId == definition.LevelId);
			if (original == null)
				throw new InvalidDataException($"Native Cathedral row targets unknown level id {definition.LevelId}.");
			if (!string.Equals(original.StableSymbol, definition.LevelSymbol, StringComparison.Ordinal))
				throw new InvalidDataException($"Native Cathedral row level {definition.LevelId} has stable symbol '{definition.LevelSymbol}', but the world has '{original.StableSymbol}'.");

			var selected = AuthoritativeCathedralLevel1Generator.Generate(definition.DungeonSeed, definition.Entry);
			var mainEntry = new AuthoritativeCathedralPoint(
				selected.StairsUp.X * 2 + 19,
				selected.StairsUp.Y * 2 + 20);
			var previousEntry = new AuthoritativeCathedralPoint(
				selected.StairsDown.X * 2 + 19,
				selected.StairsDown.Y * 2 + 19);
			var materialized = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
				definition.LevelId,
				selected,
				geometry.Pieces,
				geometry.TileProperties,
				definition.LevelSymbol,
				original.IsDefaultStart,
				original.SuppressMultiplayerDeathDrops);

			ValidateFineGeometry(materialized.Level);
			var routes = FindRoutes(materialized.Level, mainEntry, previousEntry);
			var fineFingerprint = AuthoritativeCathedralLevel1RuntimeCatalog.ComputeFineFingerprint(materialized.Level);
			var identity = new AuthoritativeCathedralLevel1GenerationIdentity(
				CatalogId,
				GeneratorId,
				GeneratorRevision,
				definition.LevelId,
				definition.LevelSymbol,
				definition.DungeonSeed,
				definition.Entry,
				selected.AttemptSeed,
				selected.FinalRandomState,
				AuthoritativeCathedralLevel1RuntimeCatalog.HashSequence("coarse", selected.MegaTiles),
				fineFingerprint,
				AuthoritativeCathedralLevel1RuntimeCatalog.HashRoutes(routes),
				AuthoritativeCathedralLevel1RuntimeCatalog.CombineCatalogFingerprints(
					CatalogFingerprint,
					geometryCatalogFingerprint));

			candidates.Add(new AuthoritativeCathedralLevel1GenerationCandidate(original, materialized.Level, selected, routes, identity));
		}

		return new AuthoritativeCathedralLevel1GenerationPlan(candidates);
	}

	internal static void ValidateFineGeometry(AuthoritativeLevel level)
	{
		if (level.NativeMegaTileLayer.Count != 0)
			throw new InvalidDataException("Native Cathedral level-one materialization must not contain a coarse mega-tile layer.");
		if (level.Width != 112 || level.Height != 112 || level.NativePieceLayer == null || level.NativePieceLayer.Count != 112 * 112)
			throw new InvalidDataException("Native Cathedral level-one materialization must be a complete 112x112 fine layer.");
		if (level.NativeTransparency == null || level.NativeTransparency.Count != 112 * 112)
			throw new InvalidDataException("Native Cathedral level-one materialization has incomplete transparency.");
	}

	internal static AuthoritativeCathedralLevel1RoutePoints FindRoutes(
		AuthoritativeLevel level,
		AuthoritativeCathedralPoint mainEntry,
		AuthoritativeCathedralPoint previousEntry)
	{
		var up = FindUniquePiece(level, 128, "up trigger");
		var down = FindUniquePiece(level, 114, "down trigger");
		foreach (var (name, point) in new[]
		{
			("up trigger", up),
			("down trigger", down),
			("main entry", mainEntry),
			("previous entry", previousEntry),
		})
		{
			if (!level.ContainsPosition(point.X, point.Y) || !level.IsWalkable(point.X, point.Y))
				throw new InvalidDataException($"Native Cathedral {name} is not an in-bounds walkable point.");
		}
		// Native InitL1Triggers identifies the trigger pieces independently of the
		// entry formulas. Do not impose synthetic pairwise-distinct rules here.
		return new AuthoritativeCathedralLevel1RoutePoints(up, down, mainEntry, previousEntry);
	}

	private static AuthoritativeCathedralPoint FindUniquePiece(AuthoritativeLevel level, ushort piece, string name)
	{
		var matches = new List<AuthoritativeCathedralPoint>();
		var layer = level.NativePieceLayer!;
		for (var y = 0; y < level.Height; y++)
		{
			for (var x = 0; x < level.Width; x++)
			{
				if (layer[y * level.Width + x] == piece)
					matches.Add(new AuthoritativeCathedralPoint(x, y));
			}
		}
		if (matches.Count != 1)
			throw new InvalidDataException($"Native Cathedral requires exactly one {name} piece {piece}, found {matches.Count}.");
		return matches[0];
	}
}

public sealed record AuthoritativeCathedralLevel1RuntimeDefinition(
	uint LevelId,
	string LevelSymbol,
	uint DungeonSeed,
	AuthoritativeCathedralEntry Entry);

public readonly record struct AuthoritativeCathedralLevel1RoutePoints(
	AuthoritativeCathedralPoint UpTrigger,
	AuthoritativeCathedralPoint DownTrigger,
	AuthoritativeCathedralPoint MainEntry,
	AuthoritativeCathedralPoint PreviousEntry);

public sealed record AuthoritativeCathedralLevel1GenerationIdentity(
	string CatalogId,
	string GeneratorId,
	int GeneratorRevision,
	uint LevelId,
	string LevelSymbol,
	uint DungeonSeed,
	AuthoritativeCathedralEntry Entry,
	uint AttemptSeed,
	uint FinalRandomState,
	string CoarseFingerprint,
	string FineFingerprint,
	string RouteFingerprint,
	string CatalogFingerprint);

public sealed class AuthoritativeCathedralLevel1GenerationCandidate
{
	internal AuthoritativeCathedralLevel1GenerationCandidate(
		AuthoritativeLevel originalLevel,
		AuthoritativeLevel level,
		AuthoritativeCathedralLevel1Result result,
		AuthoritativeCathedralLevel1RoutePoints routes,
		AuthoritativeCathedralLevel1GenerationIdentity identity)
	{
		OriginalLevel = originalLevel;
		Level = level;
		GenerationResult = result;
		Routes = routes;
		Identity = identity;
	}

	public AuthoritativeLevel OriginalLevel { get; }
	public AuthoritativeLevel Level { get; }
	public AuthoritativeCathedralLevel1Result GenerationResult { get; }
	public AuthoritativeCathedralLevel1RoutePoints Routes { get; }
	public AuthoritativeCathedralLevel1GenerationIdentity Identity { get; }
}

public sealed class AuthoritativeCathedralLevel1GenerationPlan
{
	internal AuthoritativeCathedralLevel1GenerationPlan(IEnumerable<AuthoritativeCathedralLevel1GenerationCandidate> candidates)
	{
		Candidates = new ReadOnlyCollection<AuthoritativeCathedralLevel1GenerationCandidate>(candidates.ToArray());
		if (Candidates.Count == 0)
			throw new InvalidDataException("Native Cathedral generation plan cannot be empty.");
	}

	public IReadOnlyList<AuthoritativeCathedralLevel1GenerationCandidate> Candidates { get; }

	/// <summary>
	/// Applies a fully staged plan only when every original level is still the immutable source
	/// observed during Prepare. A failed replacement is rolled back to those source instances.
	/// The bootstrap caller remains responsible for refreshing any WorldState projection and
	/// validating route tables after this AuthoritativeWorld replacement.
	/// </summary>
	public void Apply(AuthoritativeWorld world)
	{
		ArgumentNullException.ThrowIfNull(world);
		var current = new List<AuthoritativeLevel>(Candidates.Count);
		foreach (var candidate in Candidates)
		{
			var level = world.Levels.SingleOrDefault(item => item.LevelId == candidate.OriginalLevel.LevelId);
			if (!ReferenceEquals(level, candidate.OriginalLevel))
				throw new InvalidOperationException("Native Cathedral generation plan is stale and was not applied.");
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
