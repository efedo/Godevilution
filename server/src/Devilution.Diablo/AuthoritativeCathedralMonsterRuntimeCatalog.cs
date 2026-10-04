using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/// <summary>
/// The source metadata needed by the live monster runtime but intentionally
/// omitted from the population catalog.  Values retain the native widths;
/// runtime calculations perform the same explicit unsigned narrowing as the
/// original code.
/// </summary>
public sealed record AuthoritativeCathedralMonsterRuntimeDefinition
{
	public AuthoritativeCathedralMonsterRuntimeDefinition(
		int nativeMonsterId,
		string stableSymbol,
		string name,
		int level,
		int toHit,
		int animationFrameNum,
		int toHitSpecial,
		int animationFrameNumSpecial,
		int monsterClass,
		int selectionRegion,
		int treasure,
		int experience,
		int resistanceHell)
	{
		NativeMonsterId = nativeMonsterId;
		StableSymbol = stableSymbol ?? throw new ArgumentNullException(nameof(stableSymbol));
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Level = level;
		ToHit = toHit;
		AnimationFrameNum = animationFrameNum;
		ToHitSpecial = toHitSpecial;
		AnimationFrameNumSpecial = animationFrameNumSpecial;
		MonsterClass = monsterClass;
		SelectionRegion = selectionRegion;
		Treasure = treasure;
		Experience = experience;
		ResistanceHell = resistanceHell;
	}

	public int NativeMonsterId { get; }
	public string StableSymbol { get; }
	public string Name { get; }
	public int Level { get; }
	public int ToHit { get; }
	public int AnimationFrameNum { get; }
	public int ToHitSpecial { get; }
	public int AnimationFrameNumSpecial { get; }
	public int MonsterClass { get; }
	public int SelectionRegion { get; }
	public int Treasure { get; }
	public int Experience { get; }
	public int ResistanceHell { get; }
}

/// <summary>
/// Detached, ordered raw MonsterData runtime metadata for the original
/// Cathedral population.  This catalog does not select monsters, spawn loot,
/// or replace the existing 19-table population metadata.
/// </summary>
public sealed class AuthoritativeCathedralMonsterRuntimeCatalog
{
	public const string SchemaId = "godevilution.cathedral-monster-runtime-metadata";
	public const string SchemaVersion = "1.0.0";
	public const string TableName = "cathedral_population_monster_runtime.tsv";
	public const int NativeMonsterCount = 138;

	public static IReadOnlyList<string> Columns { get; } = new ReadOnlyCollection<string>(new[]
	{
		"native_monster_id",
		"stable_symbol",
		"name",
		"level",
		"to_hit",
		"anim_frame_num",
		"to_hit_special",
		"anim_frame_num_special",
		"monster_class",
		"selection_region",
		"treasure",
		"experience",
		"resistance_hell",
	});

	private const int TreasureMask = 0x0FFF;
	private const int TreasureNoDrop = 0x4000;
	private const int TreasureUnique = 0x8000;
	private readonly ReadOnlyCollection<AuthoritativeCathedralMonsterRuntimeDefinition> definitions;
	private readonly ReadOnlyDictionary<int, AuthoritativeCathedralMonsterRuntimeDefinition> byId;

	public AuthoritativeCathedralMonsterRuntimeCatalog(IEnumerable<AuthoritativeCathedralMonsterRuntimeDefinition> definitions)
	{
		if (definitions is null)
			throw new ArgumentNullException(nameof(definitions));

		var detached = definitions
			.Select(Clone)
			.ToArray();
		ValidateDefinitions(detached);
		this.definitions = Array.AsReadOnly(detached);
		byId = new ReadOnlyDictionary<int, AuthoritativeCathedralMonsterRuntimeDefinition>(detached.ToDictionary(row => row.NativeMonsterId));
		Fingerprint = ComputeFingerprint(BuildTable(detached));
	}

	public IReadOnlyList<AuthoritativeCathedralMonsterRuntimeDefinition> Definitions => definitions;
	public string Fingerprint { get; }

	public AuthoritativeCathedralMonsterRuntimeDefinition this[int nativeMonsterId] => Get(nativeMonsterId);

	public AuthoritativeCathedralMonsterRuntimeDefinition Get(int nativeMonsterId)
	{
		if (!byId.TryGetValue(nativeMonsterId, out var definition))
			throw new ArgumentOutOfRangeException(nameof(nativeMonsterId));
		return definition;
	}

	public static AuthoritativeCathedralMonsterRuntimeCatalog LoadTables(IEnumerable<TsvTable> tables)
	{
		if (tables is null)
			throw new ArgumentNullException(nameof(tables));

		var materialized = tables.ToArray();
		if (materialized.Length != 1 || materialized[0] is null)
			throw new InvalidDataException($"Expected exactly one {TableName} table.");
		return LoadTable(materialized[0]);
	}

	/** Loads exactly this standalone schema; assets, dependencies and overlays are not accepted. */
	public static AuthoritativeCathedralMonsterRuntimeCatalog Load(ContentPack pack)
	{
		ArgumentNullException.ThrowIfNull(pack);
		if (pack.Schema is null
			|| !string.Equals(pack.Schema.Id, SchemaId, StringComparison.Ordinal)
			|| pack.Schema.Version.ToString() != SchemaVersion
			|| pack.Tables.Count != 1
			|| !string.Equals(pack.Tables[0].SourcePath, TableName, StringComparison.Ordinal)
			|| pack.Assets.Count != 0
			|| pack.Dependencies.Count != 0
			|| pack.Overlays.Count != 0
			|| pack.DependencyConstraints.Count != 0)
			throw new InvalidDataException($"Content pack schema must be {SchemaId}@{SchemaVersion}.");
		return LoadTable(pack.Tables[0]);
	}

	public static AuthoritativeCathedralMonsterRuntimeCatalog LoadTable(TsvTable table)
	{
		if (table is null)
			throw new ArgumentNullException(nameof(table));
		ValidateTableShape(table);

		var rows = new List<AuthoritativeCathedralMonsterRuntimeDefinition>(NativeMonsterCount);
		foreach (var row in table.Rows)
		{
			rows.Add(new AuthoritativeCathedralMonsterRuntimeDefinition(
				ReadInt(row, "native_monster_id"),
				ReadText(row, "stable_symbol", allowEmpty: false),
				ReadText(row, "name", allowEmpty: true),
				ReadInt(row, "level"),
				ReadInt(row, "to_hit"),
				ReadInt(row, "anim_frame_num"),
				ReadInt(row, "to_hit_special"),
				ReadInt(row, "anim_frame_num_special"),
				ReadInt(row, "monster_class"),
				ReadInt(row, "selection_region"),
				ReadInt(row, "treasure"),
				ReadInt(row, "experience"),
				ReadInt(row, "resistance_hell")));
		}
		return new AuthoritativeCathedralMonsterRuntimeCatalog(rows);
	}

	public static IReadOnlyList<TsvTable> CreateTables(AuthoritativeCathedralMonsterRuntimeCatalog catalog)
	{
		if (catalog is null)
			throw new ArgumentNullException(nameof(catalog));
		return new[] { catalog.CreateTable() };
	}

	public ContentPack CreateContentPack(string id = "cathedral-monster-runtime", string version = SchemaVersion)
	{
		return new ContentPack(
			id,
			version,
			CreateTables(this),
			schema: new ContentSchemaIdentity(SchemaId, SchemaVersion));
	}

	public TsvTable CreateTable() => BuildTable(definitions);

	/// <summary>
	/// Returns the native effective level.  Native <c>int8_t</c> base levels are
	/// converted to unsigned before difficulty adjustments, including for raw
	/// negative source values.
	/// </summary>
	public uint GetEffectiveLevel(int nativeMonsterId, DiabloDifficulty difficulty, AuthoritativeCathedralUniqueDefinition? unique = null)
	{
		ValidateDifficulty(difficulty);
		ValidateUnique(unique);
		var definition = Get(nativeMonsterId);
		uint level = unchecked((uint)definition.Level);
		if (unique is not null)
			level = unique.DungeonLevel != 0 ? unchecked((uint)unique.DungeonLevel * 2u) : unchecked(level + 5u);

		return difficulty switch
		{
			DiabloDifficulty.Normal => level,
			DiabloDifficulty.Nightmare => unchecked(level + 15u),
			DiabloDifficulty.Hell => unchecked(level + 30u),
			_ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
		};
	}

	/// <summary>
	/// Returns the native effective to-hit value.  A non-special player minion
	/// uses its golem to-hit value and does not receive difficulty bonuses.
	/// </summary>
	public uint GetEffectiveToHit(
		int nativeMonsterId,
		DiabloDifficulty difficulty,
		bool special = false,
		bool playerMinion = false,
		int golemToHit = 0,
		AuthoritativeCathedralUniqueDefinition? unique = null)
	{
		ValidateDifficulty(difficulty);
		ValidateUnique(unique);
		if (golemToHit < 0 || golemToHit > ushort.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(golemToHit));
		var definition = Get(nativeMonsterId);
		if (playerMinion && !special)
			return (uint)golemToHit;

		uint value = (uint)(special ? definition.ToHitSpecial : definition.ToHit);
		if (unique is not null && unique.CustomToHit != 0)
			value = (uint)unique.CustomToHit;
		return difficulty switch
		{
			DiabloDifficulty.Normal => value,
			DiabloDifficulty.Nightmare => checked(value + 85u),
			DiabloDifficulty.Hell => checked(value + 120u),
			_ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
		};
	}

	/// <summary>Returns the native experience value before player-share division.</summary>
	public uint GetEffectiveExperience(int nativeMonsterId, DiabloDifficulty difficulty, bool unique = false)
	{
		ValidateDifficulty(difficulty);
		uint value = (uint)Get(nativeMonsterId).Experience;
		value = difficulty switch
		{
			DiabloDifficulty.Normal => value,
			DiabloDifficulty.Nightmare => checked(2u * (value + 1000u)),
			DiabloDifficulty.Hell => checked(4u * (value + 1000u)),
			_ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
		};
		return unique ? checked(value * 2u) : value;
	}

	/// <summary>
	/// Applies the fixed-point HP initialization and native difficulty scaling
	/// to a random whole-number HP result from the existing population catalog.
	/// </summary>
	public static int ScaleInitialHitPoints(
		int randomWholeHitPoints,
		DiabloGameMode gameMode,
		DiabloDifficulty difficulty,
		DiabloGameVariant gameVariant)
	{
		if (randomWholeHitPoints is < 0 or > ushort.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(randomWholeHitPoints));
		ValidateDifficulty(difficulty);
		if (gameMode is not DiabloGameMode.SinglePlayer and not DiabloGameMode.Multiplayer)
			throw new ArgumentOutOfRangeException(nameof(gameMode));
		if (gameVariant is not DiabloGameVariant.Diablo and not DiabloGameVariant.Hellfire)
			throw new ArgumentOutOfRangeException(nameof(gameVariant));

		var value = checked(randomWholeHitPoints << 6);
		if (gameMode == DiabloGameMode.SinglePlayer)
			value = Math.Max(value / 2, 64);

		var bonus = gameVariant == DiabloGameVariant.Hellfire
			? gameMode == DiabloGameMode.Multiplayer ? 100 : 50
			: 100;
		return difficulty switch
		{
			DiabloDifficulty.Normal => value,
			DiabloDifficulty.Nightmare => checked(3 * value + (gameVariant == DiabloGameVariant.Hellfire ? bonus : 100) * 64),
			DiabloDifficulty.Hell => checked(4 * value + (gameVariant == DiabloGameVariant.Hellfire ? bonus * 2 : 200) * 64),
			_ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
		};
	}

	public bool HasNoDrop(int nativeMonsterId) => (Get(nativeMonsterId).Treasure & TreasureNoDrop) != 0;
	public bool HasUniqueTreasure(int nativeMonsterId) => (Get(nativeMonsterId).Treasure & TreasureUnique) != 0;
	public int GetTreasureItemMask(int nativeMonsterId) => Get(nativeMonsterId).Treasure & TreasureMask;

	private static AuthoritativeCathedralMonsterRuntimeDefinition Clone(AuthoritativeCathedralMonsterRuntimeDefinition source)
	{
		if (source is null)
			throw new ArgumentException("Runtime metadata cannot contain null rows.", nameof(source));
		return new AuthoritativeCathedralMonsterRuntimeDefinition(
			source.NativeMonsterId,
			source.StableSymbol,
			source.Name,
			source.Level,
			source.ToHit,
			source.AnimationFrameNum,
			source.ToHitSpecial,
			source.AnimationFrameNumSpecial,
			source.MonsterClass,
			source.SelectionRegion,
			source.Treasure,
			source.Experience,
			source.ResistanceHell);
	}

	private static void ValidateDefinitions(IReadOnlyList<AuthoritativeCathedralMonsterRuntimeDefinition> rows)
	{
		if (rows.Count != NativeMonsterCount)
			throw new InvalidDataException($"Expected {NativeMonsterCount} runtime monster rows, got {rows.Count}.");

		for (var index = 0; index < rows.Count; index++)
		{
			var row = rows[index] ?? throw new InvalidDataException($"Runtime monster row {index} is null.");
			if (row.NativeMonsterId != index)
				throw new InvalidDataException($"Runtime monster row {index} has native ID {row.NativeMonsterId}.");
			ValidateText(row.StableSymbol, nameof(row.StableSymbol), allowEmpty: false);
			ValidateText(row.Name, nameof(row.Name), allowEmpty: true);
			RequireRange(row.Level, sbyte.MinValue, sbyte.MaxValue, $"row {index} level");
			RequireRange(row.ToHit, byte.MinValue, byte.MaxValue, $"row {index} to-hit");
			RequireRange(row.AnimationFrameNum, sbyte.MinValue, sbyte.MaxValue, $"row {index} animation frame");
			RequireRange(row.ToHitSpecial, byte.MinValue, byte.MaxValue, $"row {index} special to-hit");
			RequireRange(row.AnimationFrameNumSpecial, sbyte.MinValue, sbyte.MaxValue, $"row {index} special animation frame");
			RequireRange(row.MonsterClass, 0, 2, $"row {index} monster class");
			RequireRange(row.SelectionRegion, 0, 7, $"row {index} selection region");
			RequireRange(row.Treasure, ushort.MinValue, ushort.MaxValue, $"row {index} treasure");
			RequireRange(row.Experience, ushort.MinValue, ushort.MaxValue, $"row {index} experience");
			RequireRange(row.ResistanceHell, byte.MinValue, byte.MaxValue, $"row {index} Hell resistance");
		}
	}

	private static void ValidateTableShape(TsvTable table)
	{
		if (!string.Equals(table.SourcePath, TableName, StringComparison.Ordinal))
			throw new InvalidDataException($"Expected table {TableName}, got {table.SourcePath}.");
		if (table.Columns.Count != Columns.Count || !table.Columns.SequenceEqual(Columns, StringComparer.Ordinal))
			throw new InvalidDataException("Runtime monster table columns are not the canonical ordered schema.");
		if (table.Rows.Count != NativeMonsterCount)
			throw new InvalidDataException($"Expected {NativeMonsterCount} runtime monster rows, got {table.Rows.Count}.");

		var expected = Columns.ToHashSet(StringComparer.Ordinal);
		foreach (var row in table.Rows)
		{
			if (row.Values.Count != expected.Count || !row.Values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
				throw new InvalidDataException("Runtime monster row has missing or unknown columns.");
		}
	}

	private static TsvTable BuildTable(IEnumerable<AuthoritativeCathedralMonsterRuntimeDefinition> rows)
	{
		var values = rows.Select(row => new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["native_monster_id"] = row.NativeMonsterId.ToString(CultureInfo.InvariantCulture),
			["stable_symbol"] = row.StableSymbol,
			["name"] = row.Name,
			["level"] = row.Level.ToString(CultureInfo.InvariantCulture),
			["to_hit"] = row.ToHit.ToString(CultureInfo.InvariantCulture),
			["anim_frame_num"] = row.AnimationFrameNum.ToString(CultureInfo.InvariantCulture),
			["to_hit_special"] = row.ToHitSpecial.ToString(CultureInfo.InvariantCulture),
			["anim_frame_num_special"] = row.AnimationFrameNumSpecial.ToString(CultureInfo.InvariantCulture),
			["monster_class"] = row.MonsterClass.ToString(CultureInfo.InvariantCulture),
			["selection_region"] = row.SelectionRegion.ToString(CultureInfo.InvariantCulture),
			["treasure"] = row.Treasure.ToString(CultureInfo.InvariantCulture),
			["experience"] = row.Experience.ToString(CultureInfo.InvariantCulture),
			["resistance_hell"] = row.ResistanceHell.ToString(CultureInfo.InvariantCulture),
		}).ToArray();
		var detachedRows = values.Select((row, index) => new TsvRow(
			index + 2,
			new ReadOnlyDictionary<string, string>(Columns.ToDictionary(
				column => column,
				column => row[column],
				StringComparer.Ordinal)))).ToArray();
		return new TsvTable(
			TableName,
			new ReadOnlyCollection<string>(Columns.ToArray()),
			new ReadOnlyCollection<TsvRow>(detachedRows));
	}

	private static string ComputeFingerprint(TsvTable table)
	{
		var canonical = $"{SchemaId}\n{SchemaVersion}\n{table.ToCanonicalTsv()}";
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
	}

	private static int ReadInt(TsvRow row, string column)
	{
		var text = ReadText(row, column, allowEmpty: false);
		if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
			throw new InvalidDataException($"Column {column} is not an invariant integer.");
		return value;
	}

	private static string ReadText(TsvRow row, string column, bool allowEmpty)
	{
		if (!row.Values.TryGetValue(column, out var value) || value is null)
			throw new InvalidDataException($"Missing runtime monster column {column}.");
		ValidateText(value, column, allowEmpty);
		return value;
	}

	private static void ValidateText(string value, string column, bool allowEmpty)
	{
		if (!allowEmpty && value.Length == 0)
			throw new InvalidDataException($"Runtime monster column {column} cannot be empty.");
		foreach (var character in value)
		{
			if (character == '\0' || character == '\t' || character == '\r' || character == '\n' || char.IsControl(character))
				throw new InvalidDataException($"Runtime monster column {column} contains an unsupported control character.");
		}
		try
		{
			_ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetBytes(value);
		}
		catch (EncoderFallbackException exception)
		{
			throw new InvalidDataException($"Runtime monster column {column} is not valid UTF-8.", exception);
		}
	}

	private static void RequireRange(int value, int minimum, int maximum, string description)
	{
		if (value < minimum || value > maximum)
			throw new InvalidDataException($"{description} {value} is outside [{minimum}, {maximum}].");
	}

	private static void ValidateDifficulty(DiabloDifficulty difficulty)
	{
		if (difficulty is not DiabloDifficulty.Normal and not DiabloDifficulty.Nightmare and not DiabloDifficulty.Hell)
			throw new ArgumentOutOfRangeException(nameof(difficulty));
	}

	private static void ValidateUnique(AuthoritativeCathedralUniqueDefinition? unique)
	{
		if (unique is null)
			return;
		if (unique.DungeonLevel is < 0 or > byte.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(unique), "Unique mlevel must fit the native uint8 field.");
		if (unique.CustomToHit is < 0 or > byte.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(unique), "Unique custom to-hit must fit the native uint8 field.");
	}
}
