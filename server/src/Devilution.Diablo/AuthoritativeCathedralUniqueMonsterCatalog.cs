using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/**
 * Strict detached copy of the 100 native UniqueMonstersData rows used by the
 * original-Diablo Cathedral encounter generator. This is raw source metadata,
 * not an authored runtime pack or an activation policy.
 */
public sealed class AuthoritativeCathedralUniqueMonsterCatalog
{
	public const string TableName = "cathedral_population_uniques.tsv";
	public const int UniqueCatalogCount = 100;
	public const int MaximumNativeMonsterId = 137;

	private static readonly int[] QuestOnlyNativeIdsArray = [0, 2, 3, 7, 8];

	private static readonly string[] ColumnsArray = [
		"native_unique_id", "monster_type", "dungeon_level", "maximum_hit_points",
		"ai", "intelligence", "min_damage", "max_damage", "resistance",
		"reduce_player_strength", "reduce_player_magic", "reduce_player_dexterity",
		"reduce_player_vitality", "reduce_player_max_hp", "reduce_player_max_mana",
		"monster_pack", "custom_to_hit", "custom_armor_class", "talk_message",
	];

	/** Exact ordered source columns; callers cannot mutate the schema. */
	public static IReadOnlyList<string> Columns
		=> Array.AsReadOnly(ColumnsArray);

	/** Native source IDs whose rows are quest-only in ordinary single-player mode. */
	public static IReadOnlyList<int> QuestOnlyNativeIds
		=> Array.AsReadOnly(QuestOnlyNativeIdsArray);

	private AuthoritativeCathedralUniqueMonsterCatalog(
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition> definitions,
		string fingerprint)
	{
		Definitions = Array.AsReadOnly(definitions.ToArray());
		Fingerprint = fingerprint;
	}

	/** Complete detached source-order unique metadata. */
	public IReadOnlyList<AuthoritativeCathedralUniqueDefinition> Definitions { get; }

	/** Uppercase SHA-256 of the table identity and canonical TSV schema/values. */
	public string Fingerprint { get; }

	/** Parses the exact external table without accessing native files or MPQs. */
	public static AuthoritativeCathedralUniqueMonsterCatalog Load(string contents)
	{
		ArgumentNullException.ThrowIfNull(contents);
		return LoadTable(TsvTable.Parse(TableName, contents));
	}

	/** Alias for callers that name external content loaders explicitly. */
	public static AuthoritativeCathedralUniqueMonsterCatalog LoadTsv(string contents)
		=> Load(contents);

	/** Loads an already parsed table and retains no references to its rows. */
	public static AuthoritativeCathedralUniqueMonsterCatalog LoadTable(TsvTable table)
	{
		ArgumentNullException.ThrowIfNull(table);
		ValidateTableIdentity(table);
		if (table.Rows.Count != UniqueCatalogCount)
			throw new InvalidDataException(
				$"{TableName} requires exactly {UniqueCatalogCount} ordered native rows.");

		var definitions = new AuthoritativeCathedralUniqueDefinition[UniqueCatalogCount];
		for (var index = 0; index < definitions.Length; index++)
			definitions[index] = ParseDefinition(table.Rows[index], index);

		var canonical = TableName + "\n" + table.ToCanonicalTsv();
		var fingerprint = Convert.ToHexString(
			SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
		return new AuthoritativeCathedralUniqueMonsterCatalog(definitions, fingerprint);
	}

	private static AuthoritativeCathedralUniqueDefinition ParseDefinition(
		TsvRow row, int expectedId)
	{
		try {
			var nativeUniqueId = RequiredUInt(row, "native_unique_id", UniqueCatalogCount - 1);
			if (nativeUniqueId != (uint)expectedId)
				throw new InvalidDataException(
					$"{TableName} must list native unique IDs in exact 0..99 order.");

			var monsterType = RequiredUInt(row, "monster_type", MaximumNativeMonsterId);
			var dungeonLevel = RequiredUInt(row, "dungeon_level", byte.MaxValue);
			var maximumHitPoints = RequiredUInt(row, "maximum_hit_points", ushort.MaxValue);
			var ai = RequiredSigned(row, "ai");
			if (ai is < -1 or > 39)
				throw new InvalidDataException(
					$"Column 'ai' on TSV row {row.LineNumber} is outside native range -1..39.");

			var monsterPack = RequiredUInt(row, "monster_pack", (uint)AuthoritativeCathedralUniquePack.Leashed);
			var talkMessage = RequiredSigned(row, "talk_message");
			if (talkMessage is < -1 or > short.MaxValue)
				throw new InvalidDataException(
					$"Column 'talk_message' on TSV row {row.LineNumber} must be TEXT_NONE (-1) or a nonnegative signed int16 speech identity.");

			return new AuthoritativeCathedralUniqueDefinition(
				(int)nativeUniqueId,
				(int)monsterType,
				(int)dungeonLevel,
				(int)maximumHitPoints,
				ai,
				(int)RequiredUInt(row, "intelligence", byte.MaxValue),
				(int)RequiredUInt(row, "min_damage", byte.MaxValue),
				(int)RequiredUInt(row, "max_damage", byte.MaxValue),
				(int)RequiredUInt(row, "resistance", ushort.MaxValue),
				(int)RequiredUInt(row, "reduce_player_strength", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_magic", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_dexterity", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_vitality", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_max_hp", byte.MaxValue),
				(int)RequiredUInt(row, "reduce_player_max_mana", byte.MaxValue),
				(AuthoritativeCathedralUniquePack)monsterPack,
				(int)RequiredUInt(row, "custom_to_hit", byte.MaxValue),
				(int)RequiredUInt(row, "custom_armor_class", byte.MaxValue),
				talkMessage,
				Array.IndexOf(QuestOnlyNativeIdsArray, expectedId) >= 0);
		} catch (InvalidDataException exception) {
			throw new InvalidDataException(
				$"{TableName} row {row.LineNumber} is invalid: {exception.Message}", exception);
		}
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

	private static void ValidateTableIdentity(TsvTable table)
	{
		if (!string.Equals(table.SourcePath, TableName, StringComparison.Ordinal))
			throw new InvalidDataException(
				$"Expected TSV table '{TableName}', got '{table.SourcePath}'.");
		if (!table.Columns.SequenceEqual(ColumnsArray, StringComparer.Ordinal))
			throw new InvalidDataException(
				$"Production table '{TableName}' must use its exact ordered schema.");
	}
}
