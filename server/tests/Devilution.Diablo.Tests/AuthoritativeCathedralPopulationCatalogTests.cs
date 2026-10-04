using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralPopulationCatalogTests
{
	[Fact]
	public void LoadsCompleteOrderedRawTablesAndPreservesDetachedAnimationMetadata()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralPopulationCatalog.Load(
			fixture.Monsters, fixture.Objects);

		Assert.Equal(138, catalog.Monsters.Count);
		Assert.Equal(109, catalog.Objects.Count);
		Assert.Equal(Enumerable.Range(0, 138), catalog.Monsters.Select(value => value.NativeMonsterId));
		Assert.Equal(Enumerable.Range(0, 109), catalog.Objects.Select(value => value.NativeType));
		Assert.Equal(7, catalog.Monsters[0].Frames[5]);
		Assert.Equal(0, catalog.Monsters[0].PopulationDefinition.Frames[5]);
		Assert.Equal((byte)63, catalog.Objects[108].Flags);
		Assert.Equal(64, catalog.Objects[108].AnimWidth);
	}

	[Fact]
	public void FingerprintAndOrderingAreStableAcrossIndependentLoads()
	{
		var fixture = CreateFixture();
		var first = AuthoritativeCathedralPopulationCatalog.Load(fixture.Monsters, fixture.Objects);
		var second = AuthoritativeCathedralPopulationCatalog.Load(fixture.Monsters, fixture.Objects);

		Assert.Equal(first.Fingerprint, second.Fingerprint);
		Assert.NotSame(first.Monsters, second.Monsters);
		Assert.NotSame(first.Monsters[0], second.Monsters[0]);
		Assert.Equal(first.Monsters.Select(value => value.NativeMonsterId),
			second.Monsters.Select(value => value.NativeMonsterId));
		var frames = Assert.IsAssignableFrom<IList<int>>(first.Monsters[0].Frames);
		Assert.Throws<NotSupportedException>(() => frames[0] = 99);
	}

	[Fact]
	public void FingerprintUsesCanonicalSha256AndChangesWithEitherTable()
	{
		var fixture = CreateFixture();
		var catalog = AuthoritativeCathedralPopulationCatalog.Load(fixture.Monsters, fixture.Objects);
		var canonical = "cathedral_population_monsters.tsv\n"
			+ fixture.Monsters.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n"
			+ "cathedral_population_objects.tsv\n"
			+ fixture.Objects.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
		Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))), catalog.Fingerprint);
		Assert.Matches("^[0-9A-F]{64}$", catalog.Fingerprint);
		var normalized = AuthoritativeCathedralPopulationCatalog.Load(
			"\uFEFF# ignored comment\r\n\r\n" + fixture.Monsters.Replace("\r\n", "\n", StringComparison.Ordinal),
			"# ignored comment\n" + fixture.Objects.Replace("\r\n", "\n", StringComparison.Ordinal));
		Assert.Equal(catalog.Fingerprint, normalized.Fingerprint);
		Assert.NotEqual(catalog.Fingerprint, AuthoritativeCathedralPopulationCatalog.Load(
			SetMonsterCell(fixture.Monsters, 0, "image", "101"), fixture.Objects).Fingerprint);
		Assert.NotEqual(catalog.Fingerprint, AuthoritativeCathedralPopulationCatalog.Load(
			fixture.Monsters, SetObjectCell(fixture.Objects, 0, "anim_delay", "6")).Fingerprint);
	}

	[Fact]
	public void PreservesSignedDisabledAnimationMetadataButRejectsNegativeActiveDimensions()
	{
		var fixture = CreateFixture();
		var contents = SetMonsterCell(SetMonsterCell(fixture.Monsters, 0, "frame_5", "-128"), 0, "rate_5", "-1");
		var catalog = AuthoritativeCathedralPopulationCatalog.Load(contents, fixture.Objects);
		Assert.Equal(-128, catalog.Monsters[0].Frames[5]);
		Assert.Equal(-1, catalog.Monsters[0].Rates[5]);
		Assert.Equal(0, catalog.Monsters[0].PopulationDefinition.Frames[5]);
		Assert.Equal(0, catalog.Monsters[0].PopulationDefinition.Rates[5]);
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationCatalog.Load(
			SetMonsterCell(contents, 0, "has_special", "1"), fixture.Objects));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationCatalog.Load(
			SetMonsterCell(contents, 0, "frame_0", "-1"), fixture.Objects));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationCatalog.Load(
			SetMonsterCell(contents, 0, "rate_0", "-1"), fixture.Objects));
	}

	[Fact]
	public void LoadTablesRequiresExplicitTableNamesAndExactSchemas()
	{
		var fixture = CreateFixture();
		var monsters = TsvTable.Parse(
			AuthoritativeCathedralPopulationCatalog.MonsterTableName, fixture.Monsters);
		var objects = TsvTable.Parse(
			AuthoritativeCathedralPopulationCatalog.ObjectTableName, fixture.Objects);
		var catalog = AuthoritativeCathedralPopulationCatalog.LoadTables(monsters, objects);
		Assert.Equal(138, catalog.Monsters.Count);

		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.LoadTables(
				TsvTable.Parse("other.tsv", fixture.Monsters), objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				fixture.Monsters.Replace("availability", "unknown", StringComparison.Ordinal),
				fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				fixture.Monsters.Replace("frame_5", "frame_6", StringComparison.Ordinal),
				fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				fixture.Monsters.Replace(
					"availability", "availability\textra", StringComparison.Ordinal),
				fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				fixture.Monsters,
				fixture.Objects.Replace("flags", "unknown_flags", StringComparison.Ordinal)));
	}

	[Fact]
	public void RequiresCompleteOrderedMonsterAndObjectIds()
	{
		var fixture = CreateFixture();

		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				SetMonsterCell(fixture.Monsters, 0, "native_monster_id", "1"),
				fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				SwapMonsterRows(fixture.Monsters, 0, 1), fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				RemoveLastDataRow(fixture.Monsters), fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				fixture.Monsters,
				SetObjectCell(fixture.Objects, 0, "native_object_id", "1")));
	}

	[Fact]
	public void EnforcesInvariantTypedRangesAndStrictBooleans()
	{
		var fixture = CreateFixture();
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				SetMonsterCell(fixture.Monsters, 0, "is_skeleton", "true"), fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				SetMonsterCell(fixture.Monsters, 0, "image", "1,5"), fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				SetMonsterCell(fixture.Monsters, 0, "frame_0", "128"), fixture.Objects));
		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				fixture.Monsters,
				SetObjectCell(fixture.Objects, 0, "flags", "64")));
	}

	[Fact]
	public void PreservesRawDungeonLevelsThroughSignedInt8AndRejectsOverflow()
	{
		var fixture = CreateFixture();
		var level26 = AuthoritativeCathedralPopulationCatalog.Load(
			SetMonsterCell(fixture.Monsters, 0, "max_dungeon_level", "26"),
			fixture.Objects);
		Assert.Equal(26, level26.Monsters[0].MaxDungeonLevel);

		var level127 = AuthoritativeCathedralPopulationCatalog.Load(
			SetMonsterCell(fixture.Monsters, 0, "max_dungeon_level", "127"),
			fixture.Objects);
		Assert.Equal(127, level127.Monsters[0].MaxDungeonLevel);

		Assert.Throws<InvalidDataException>(() =>
			AuthoritativeCathedralPopulationCatalog.Load(
				SetMonsterCell(fixture.Monsters, 0, "max_dungeon_level", "128"),
				fixture.Objects));
	}

	private static Fixture CreateFixture()
	{
		var monsters = new StringBuilder();
		monsters.AppendLine(string.Join('\t', AuthoritativeCathedralPopulationCatalog.MonsterColumns));
		for (var id = 0; id < 138; id++) {
			var skeleton = id is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27;
			var row = new string[] {
				id.ToString(), "1", (100 + id).ToString(), "0", "16", skeleton ? "1" : "0",
				"2", "4", "0", "0", "0", "1", "2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0",
				"0", "64", "1", "0", "0", "0", "0", "7", "1", "0", "0", "0", "0", "9",
			};
			monsters.AppendLine(string.Join('\t', row));
		}

		var objects = new StringBuilder();
		objects.AppendLine(string.Join('\t', AuthoritativeCathedralPopulationCatalog.ObjectColumns));
		for (var id = 0; id < 109; id++)
			objects.AppendLine(string.Join('\t', id, id == 108 ? 63 : 0, 5, 8, id == 108 ? 64 : 1, 0));
		return new(monsters.ToString().TrimEnd('\r', '\n'), objects.ToString().TrimEnd('\r', '\n'));
	}

	private static string SetMonsterCell(string contents, int dataRow, string column, string value)
		=> SetCell(contents, dataRow, column, value);

	private static string SetObjectCell(string contents, int dataRow, string column, string value)
		=> SetCell(contents, dataRow, column, value);

	private static string SetCell(string contents, int dataRow, string column, string value)
	{
		var lines = contents.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		var columns = lines[0].Split('\t');
		var columnIndex = Array.IndexOf(columns, column);
		Assert.True(columnIndex >= 0);
		var cells = lines[dataRow + 1].Split('\t');
		cells[columnIndex] = value;
		lines[dataRow + 1] = string.Join('\t', cells);
		return string.Join('\n', lines);
	}

	private static string SwapMonsterRows(string contents, int first, int second)
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

	private sealed record Fixture(string Monsters, string Objects);
}
