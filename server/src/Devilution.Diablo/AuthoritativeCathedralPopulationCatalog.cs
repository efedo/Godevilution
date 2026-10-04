using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/**
 * Detached, opt-in raw native metadata for the Cathedral initial-population
 * phase. These tables are deliberately separate from authored runtime packs:
 * they preserve the ordered native records consumed by GetLevelMTypes and
 * InitObjects/InitMonsters without reading MPQs or native installation state.
 */
public sealed class AuthoritativeCathedralPopulationCatalog
{
	public const string MonsterTableName = "cathedral_population_monsters.tsv";
	public const string ObjectTableName = "cathedral_population_objects.tsv";
	public const int MaximumRawDungeonLevel = sbyte.MaxValue;
	public const int ObjectCatalogCount = 109;

	private static readonly string[] MonsterColumnsArray = [
		"native_monster_id", "availability", "image", "min_dungeon_level",
		"max_dungeon_level", "is_skeleton", "minimum_hit_points",
		"maximum_hit_points", "ai", "intelligence", "ability_flags",
		"min_damage", "max_damage", "min_damage_special", "max_damage_special",
		"armor_class", "resistance", "reduce_player_strength",
		"reduce_player_magic", "reduce_player_dexterity", "reduce_player_vitality",
		"reduce_player_max_hp", "reduce_player_max_mana", "has_special", "width",
		"frame_0", "frame_1", "frame_2", "frame_3", "frame_4", "frame_5",
		"rate_0", "rate_1", "rate_2", "rate_3", "rate_4", "rate_5",
	];

	private static readonly string[] ObjectColumnsArray = [
		"native_object_id", "flags", "anim_delay", "anim_length", "anim_width",
		"selection_region",
	];

	/** Exact ordered monster-table columns; callers cannot mutate the schema. */
	public static IReadOnlyList<string> MonsterColumns
		=> Array.AsReadOnly(MonsterColumnsArray);

	/** Exact ordered object-table columns; callers cannot mutate the schema. */
	public static IReadOnlyList<string> ObjectColumns
		=> Array.AsReadOnly(ObjectColumnsArray);

	private AuthoritativeCathedralPopulationCatalog(
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> monsters,
		IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objects,
		string fingerprint)
	{
		Monsters = Array.AsReadOnly(monsters.ToArray());
		Objects = Array.AsReadOnly(objects.ToArray());
		Fingerprint = fingerprint;
	}

	public IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> Monsters { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> Objects { get; }
	public string Fingerprint { get; }

	/** Parses both required tables from canonical TSV content. */
	public static AuthoritativeCathedralPopulationCatalog Load(
		string monsterContents, string objectContents)
	{
		ArgumentNullException.ThrowIfNull(monsterContents);
		ArgumentNullException.ThrowIfNull(objectContents);
		return LoadTables(
			TsvTable.Parse(MonsterTableName, monsterContents),
			TsvTable.Parse(ObjectTableName, objectContents));
	}

	/** Alias for callers that name external content loaders explicitly. */
	public static AuthoritativeCathedralPopulationCatalog LoadTsv(
		string monsterContents, string objectContents)
		=> Load(monsterContents, objectContents);

	/** Loads already parsed tables, retaining no references to their rows. */
	public static AuthoritativeCathedralPopulationCatalog LoadTables(
		TsvTable monsterTable, TsvTable objectTable)
	{
		ArgumentNullException.ThrowIfNull(monsterTable);
		ArgumentNullException.ThrowIfNull(objectTable);
		ValidateTableIdentity(monsterTable, MonsterTableName, MonsterColumnsArray);
		ValidateTableIdentity(objectTable, ObjectTableName, ObjectColumnsArray);

		var monsters = new AuthoritativeCathedralRawMonsterDefinition[
			AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount];
		if (monsterTable.Rows.Count != monsters.Length)
			throw new InvalidDataException(
				$"{MonsterTableName} requires exactly {monsters.Length} ordered native rows.");
		for (var index = 0; index < monsters.Length; index++)
			monsters[index] = ParseMonster(monsterTable.Rows[index], index);

		var objects = new AuthoritativeCathedralPopulationObjectDefinition[ObjectCatalogCount];
		if (objectTable.Rows.Count != objects.Length)
			throw new InvalidDataException(
				$"{ObjectTableName} requires exactly {objects.Length} ordered native rows.");
		for (var index = 0; index < objects.Length; index++)
			objects[index] = ParseObject(objectTable.Rows[index], index);

		var canonical = MonsterTableName + "\n" + monsterTable.ToCanonicalTsv()
			+ ObjectTableName + "\n" + objectTable.ToCanonicalTsv();
		var fingerprint = Convert.ToHexString(
			SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
		return new AuthoritativeCathedralPopulationCatalog(monsters, objects, fingerprint);
	}

	private static AuthoritativeCathedralRawMonsterDefinition ParseMonster(
		TsvRow row, int expectedId)
	{
		try {
			var nativeMonsterId = RequiredUInt(row, "native_monster_id", 137);
			if (nativeMonsterId != (uint)expectedId)
				throw new InvalidDataException(
					$"{MonsterTableName} must list native monster IDs in exact 0..137 order.");

			var availability = RequiredUInt(row, "availability", 2);
			var minDungeonLevel = RequiredUInt(
				row, "min_dungeon_level", MaximumRawDungeonLevel);
			var maxDungeonLevel = RequiredUInt(
				row, "max_dungeon_level", MaximumRawDungeonLevel);
			if (maxDungeonLevel < minDungeonLevel)
				throw new InvalidDataException(
					$"{MonsterTableName} row {row.LineNumber} has a descending dungeon-level range.");

			var frames = Enumerable.Range(0, 6)
				.Select(index => RequiredInt8(row, $"frame_{index}"))
				.ToArray();
			var rates = Enumerable.Range(0, 6)
				.Select(index => RequiredInt8(row, $"rate_{index}"))
				.ToArray();
			return new AuthoritativeCathedralRawMonsterDefinition(
				(int)nativeMonsterId,
				(AuthoritativeCathedralMonsterAvailability)availability,
				(int)RequiredUInt(row, "image", ushort.MaxValue),
				(int)minDungeonLevel,
				(int)maxDungeonLevel,
				RequiredBinary(row, "is_skeleton"),
				(int)RequiredUInt(row, "minimum_hit_points", ushort.MaxValue),
				(int)RequiredUInt(row, "maximum_hit_points", ushort.MaxValue),
				RequiredAi(row),
				(int)RequiredUInt(row, "intelligence", byte.MaxValue),
				RequiredUInt(row, "ability_flags", ushort.MaxValue),
				(int)RequiredUInt(row, "min_damage", byte.MaxValue),
				(int)RequiredUInt(row, "max_damage", byte.MaxValue),
				(int)RequiredUInt(row, "min_damage_special", byte.MaxValue),
				(int)RequiredUInt(row, "max_damage_special", byte.MaxValue),
				(int)RequiredUInt(row, "armor_class", byte.MaxValue),
				(int)RequiredUInt(row, "resistance", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_strength", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_magic", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_dexterity", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_vitality", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_max_hp", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_max_mana", byte.MaxValue),
				RequiredBinary(row, "has_special"),
				(int)RequiredUInt(row, "width", ushort.MaxValue),
				frames,
				rates);
		} catch (InvalidDataException exception) {
			throw new InvalidDataException(
				$"{MonsterTableName} row {row.LineNumber} is invalid: {exception.Message}",
				exception);
		}
	}

	private static AuthoritativeCathedralPopulationObjectDefinition ParseObject(
		TsvRow row, int expectedId)
	{
		try {
			var nativeObjectId = RequiredUInt(row, "native_object_id", ObjectCatalogCount - 1);
			if (nativeObjectId != (uint)expectedId)
				throw new InvalidDataException(
					$"{ObjectTableName} must list native object IDs in exact 0..108 order.");
			return new AuthoritativeCathedralPopulationObjectDefinition(
				(int)nativeObjectId,
				(byte)RequiredUInt(row, "flags", 63),
				(byte)RequiredUInt(row, "anim_delay", byte.MaxValue),
				(byte)RequiredUInt(row, "anim_length", byte.MaxValue),
				(byte)RequiredUInt(row, "anim_width", byte.MaxValue),
				(int)RequiredUInt(row, "selection_region", 7));
		} catch (InvalidDataException exception) {
			throw new InvalidDataException(
				$"{ObjectTableName} row {row.LineNumber} is invalid: {exception.Message}",
				exception);
		}
	}

	private static int RequiredAi(TsvRow row)
	{
		var value = RequiredSigned(row, "ai");
		if (value is < -1 or > 39)
			throw new InvalidDataException(
				$"Column 'ai' on TSV row {row.LineNumber} is outside native range -1..39.");
		return value;
	}

	private static bool RequiredBinary(TsvRow row, string column)
	{
		var value = row.Required(column);
		return value switch {
			"0" => false,
			"1" => true,
			_ => throw new InvalidDataException(
				$"Column '{column}' on TSV row {row.LineNumber} must be exactly 0 or 1."),
		};
	}

	private static uint RequiredUInt(TsvRow row, string column, uint maximum)
	{
		var value = row.Required(column);
		if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
			|| result > maximum)
			throw new InvalidDataException(
				$"Column '{column}' on TSV row {row.LineNumber} must be an invariant unsigned value in range 0..{maximum}.");
		return result;
	}

	private static int RequiredSigned(TsvRow row, string column)
	{
		var value = row.Required(column);
		if (!int.TryParse(value, NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture, out var result))
			throw new InvalidDataException(
				$"Column '{column}' on TSV row {row.LineNumber} must be an invariant signed integer.");
		return result;
	}

	private static int RequiredInt8(TsvRow row, string column)
	{
		var result = RequiredSigned(row, column);
		if (result is < sbyte.MinValue or > sbyte.MaxValue)
			throw new InvalidDataException(
				$"Column '{column}' on TSV row {row.LineNumber} must fit signed int8 range.");
		return result;
	}

	private static void ValidateTableIdentity(
		TsvTable table, string expectedName, IReadOnlyList<string> expectedColumns)
	{
		if (!string.Equals(table.SourcePath, expectedName, StringComparison.Ordinal))
			throw new InvalidDataException(
				$"Expected TSV table '{expectedName}', got '{table.SourcePath}'.");
		if (!table.Columns.SequenceEqual(expectedColumns, StringComparer.Ordinal))
			throw new InvalidDataException(
				$"Production table '{expectedName}' must use its exact ordered schema.");
	}
}
