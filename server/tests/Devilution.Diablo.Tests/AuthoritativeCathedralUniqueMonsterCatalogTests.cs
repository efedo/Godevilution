using System.Security.Cryptography;
using System.Text;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralUniqueMonsterCatalogTests
{
	[Fact]
	public void LoadsAllOrderedRowsAndDerivesNativeQuestOnlyPolicy()
	{
		var catalog = AuthoritativeCathedralUniqueMonsterCatalog.Load(CreateFixture());

		Assert.Equal(100, catalog.Definitions.Count);
		Assert.Equal(Enumerable.Range(0, 100),
			catalog.Definitions.Select(definition => definition.NativeUniqueId));
		Assert.Equal(new[] { 0, 2, 3, 7, 8 },
			catalog.Definitions.Where(definition => definition.QuestOnly)
				.Select(definition => definition.NativeUniqueId));
		Assert.DoesNotContain(catalog.Definitions.Where(definition => !definition.QuestOnly),
			definition => new[] { 0, 2, 3, 7, 8 }.Contains(definition.NativeUniqueId));
	}

	[Fact]
	public void PreservesRawDisabledRowsAndNativeBoundaryValues()
	{
		var contents = SetRow(CreateFixture(), 0, new Dictionary<string, string> {
			["monster_type"] = "137",
			["dungeon_level"] = "255",
			["maximum_hit_points"] = "65535",
			["ai"] = "-1",
			["intelligence"] = "255",
			["min_damage"] = "255",
			["max_damage"] = "255",
			["resistance"] = "65535",
			["reduce_player_strength"] = "255",
			["reduce_player_magic"] = "255",
			["reduce_player_dexterity"] = "255",
			["reduce_player_vitality"] = "255",
			["reduce_player_max_hp"] = "255",
			["reduce_player_max_mana"] = "255",
			["monster_pack"] = "2",
			["custom_to_hit"] = "255",
			["custom_armor_class"] = "255",
			["talk_message"] = "-1",
		});

		var definition = AuthoritativeCathedralUniqueMonsterCatalog.Load(contents).Definitions[0];
		Assert.Equal(137, definition.MonsterType);
		Assert.Equal(255, definition.DungeonLevel);
		Assert.Equal(ushort.MaxValue, definition.MaximumHitPoints);
		Assert.Equal(-1, definition.Ai);
		Assert.Equal(ushort.MaxValue, definition.Resistance);
		Assert.Equal(AuthoritativeCathedralUniquePack.Leashed, definition.MonsterPack);
		Assert.Equal(-1, definition.TalkMessage);
		Assert.True(definition.QuestOnly);
	}

	[Fact]
	public void IgnoresBomCommentsAndLineEndingStyleForCanonicalFingerprint()
	{
		var fixture = CreateFixture();
		var expected = AuthoritativeCathedralUniqueMonsterCatalog.Load(fixture);
		var windows = fixture.Replace("\n", "\r\n", StringComparison.Ordinal);
		var decorated = "\uFEFF# source comment\r\n\r\n" + windows;

		var actual = AuthoritativeCathedralUniqueMonsterCatalog.Load(decorated);
		Assert.Equal(expected.Fingerprint, actual.Fingerprint);
		Assert.Equal(expected.Definitions, actual.Definitions);
	}

	[Fact]
	public void FingerprintUsesIndependentCanonicalSha256Formula()
	{
		var fixture = CreateFixture();
		var table = TsvTable.Parse(
			AuthoritativeCathedralUniqueMonsterCatalog.TableName, fixture);
		var catalog = AuthoritativeCathedralUniqueMonsterCatalog.LoadTable(table);
		var canonical = AuthoritativeCathedralUniqueMonsterCatalog.TableName + "\n"
			+ fixture + "\n";

		Assert.Equal(
			Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
			catalog.Fingerprint);
		Assert.Matches("^[0-9A-F]{64}$", catalog.Fingerprint);
		Assert.NotEqual(catalog.Fingerprint,
			AuthoritativeCathedralUniqueMonsterCatalog.Load(
				SetCell(fixture, 0, "maximum_hit_points", "101")).Fingerprint);
	}

	[Fact]
	public void DetachesDefinitionsAndRequiresExactTableIdentitySchemaAndRowCount()
	{
		var fixture = CreateFixture();
		var table = TsvTable.Parse(
			AuthoritativeCathedralUniqueMonsterCatalog.TableName, fixture);
		var catalog = AuthoritativeCathedralUniqueMonsterCatalog.LoadTable(table);

		Assert.NotSame(table.Rows, catalog.Definitions);
		Assert.NotSame(catalog.Definitions, AuthoritativeCathedralUniqueMonsterCatalog.Load(fixture).Definitions);
		var definitions = Assert.IsAssignableFrom<IList<AuthoritativeCathedralUniqueDefinition>>(
			catalog.Definitions);
		Assert.Throws<NotSupportedException>(() => definitions[0] = definitions[1]);

		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.LoadTable(
			TsvTable.Parse("other.tsv", fixture)));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.Load(
			fixture.Replace("monster_type", "unknown", StringComparison.Ordinal)));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.Load(
			fixture.Replace("talk_message", "talk_message\tquest_only", StringComparison.Ordinal)));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.Load(
			RemoveLastDataRow(fixture)));
	}

	[Fact]
	public void RequiresExactSourceOrderAndRejectsDuplicateOrMissingIds()
	{
		var fixture = CreateFixture();

		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.Load(
			SetCell(fixture, 0, "native_unique_id", "1")));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.Load(
			SwapRows(fixture, 0, 1)));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.Load(
			SetCell(fixture, 99, "native_unique_id", "98")));
	}

	[Fact]
	public void RejectsInvalidNumbersNativeReferencesAndRawRanges()
	{
		var fixture = CreateFixture();
		var invalidCells = new[] {
			("native_unique_id", "-1"),
			("native_unique_id", "100"),
			("monster_type", "-1"),
			("monster_type", "138"),
			("dungeon_level", "256"),
			("maximum_hit_points", "65536"),
			("ai", "-2"),
			("ai", "40"),
			("intelligence", "256"),
			("min_damage", "256"),
			("max_damage", "256"),
			("resistance", "65536"),
			("reduce_player_strength", "256"),
			("reduce_player_magic", "256"),
			("reduce_player_dexterity", "256"),
			("reduce_player_vitality", "256"),
			("reduce_player_max_hp", "256"),
			("reduce_player_max_mana", "256"),
			("monster_pack", "3"),
			("custom_to_hit", "256"),
			("custom_armor_class", "256"),
			("talk_message", "-32769"),
			("talk_message", "-32768"),
			("talk_message", "-2"),
			("talk_message", "32768"),
			("talk_message", "1.0"),
		};

		foreach (var (column, value) in invalidCells)
			Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralUniqueMonsterCatalog.Load(
				SetCell(fixture, 0, column, value)));
	}

	private static string CreateFixture()
	{
		var contents = new StringBuilder();
		contents.Append(string.Join('\t', AuthoritativeCathedralUniqueMonsterCatalog.Columns)).Append('\n');
		for (var id = 0; id < AuthoritativeCathedralUniqueMonsterCatalog.UniqueCatalogCount; id++) {
			var row = new[] {
				id.ToString(), (id % 138).ToString(), (id % 5).ToString(), (100 + id).ToString(),
				(id % 40).ToString(), (id % 256).ToString(), (id % 256).ToString(),
				((id + 1) % 256).ToString(), (id * 17 % 65536).ToString(),
				(id % 256).ToString(), ((id + 1) % 256).ToString(), ((id + 2) % 256).ToString(),
				((id + 3) % 256).ToString(), ((id + 4) % 256).ToString(), ((id + 5) % 256).ToString(),
				(id % 3).ToString(), (id % 256).ToString(), ((id + 1) % 256).ToString(),
				(id == 0 ? "-1" : id.ToString()),
			};
			contents.Append(string.Join('\t', row)).Append('\n');
		}
		return contents.ToString().TrimEnd('\n');
	}

	private static string SetCell(string contents, int dataRow, string column, string value)
	{
		var lines = contents.Split('\n');
		var columns = lines[0].Split('\t');
		var columnIndex = Array.IndexOf(columns, column);
		Assert.True(columnIndex >= 0);
		var cells = lines[dataRow + 1].Split('\t');
		cells[columnIndex] = value;
		lines[dataRow + 1] = string.Join('\t', cells);
		return string.Join('\n', lines);
	}

	private static string SetRow(string contents, int dataRow, IReadOnlyDictionary<string, string> replacements)
	{
		var lines = contents.Split('\n');
		var columns = lines[0].Split('\t');
		var cells = lines[dataRow + 1].Split('\t');
		foreach (var (column, value) in replacements) {
			var columnIndex = Array.IndexOf(columns, column);
			Assert.True(columnIndex >= 0);
			cells[columnIndex] = value;
		}
		lines[dataRow + 1] = string.Join('\t', cells);
		return string.Join('\n', lines);
	}

	private static string SwapRows(string contents, int first, int second)
	{
		var lines = contents.Split('\n');
		(lines[first + 1], lines[second + 1]) = (lines[second + 1], lines[first + 1]);
		return string.Join('\n', lines);
	}

	private static string RemoveLastDataRow(string contents)
	{
		var lines = contents.Split('\n');
		return string.Join('\n', lines[..^1]);
	}
}
