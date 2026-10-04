using System.Collections.ObjectModel;
using System.Globalization;

using Devilution.Data;
using Devilution.Diablo;

using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralMonsterRuntimeCatalogTests
{
	[Fact]
	public void CompleteCatalogRoundTripsInNativeOrderAndDetachesCallerRows()
	{
		var rows = CreateDefinitions();
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(rows);
		rows[0] = Definition(0, level: 31, name: "changed-after-load");

		var roundTrip = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTables(
			AuthoritativeCathedralMonsterRuntimeCatalog.CreateTables(catalog));

		Assert.Equal(138, roundTrip.Definitions.Count);
		Assert.Equal(Enumerable.Range(0, 138), roundTrip.Definitions.Select(row => row.NativeMonsterId));
		Assert.Equal(sbyte.MinValue, roundTrip[0].Level);
		Assert.Equal("MONSTER_000", roundTrip[0].StableSymbol);
		Assert.Equal(catalog.Fingerprint, roundTrip.Fingerprint);
		Assert.All(catalog.Fingerprint, character => Assert.True(character is >= '0' and <= '9' or >= 'A' and <= 'F'));
		Assert.NotEqual("changed-after-load", catalog[0].Name);
		Assert.Equal(AuthoritativeCathedralMonsterRuntimeCatalog.TableName,
			AuthoritativeCathedralMonsterRuntimeCatalog.CreateTables(catalog)[0].SourcePath);
	}

	[Fact]
	public void EmptyUnavailableNativeSourceNameIsPreservedWithoutInventedText()
	{
		var rows = CreateDefinitions();
		rows[112] = Definition(112, name: string.Empty);
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(rows);
		Assert.Empty(AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(catalog.CreateTable())[112].Name);
	}

	[Fact]
	public void ContentPackLoadRequiresStandaloneSchemaAndRoundTrips()
	{
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions());
		var pack = catalog.CreateContentPack();
		var loaded = AuthoritativeCathedralMonsterRuntimeCatalog.Load(pack);

		Assert.Equal(catalog.Fingerprint, loaded.Fingerprint);
		Assert.Equal("1.0.0", pack.Schema!.Version.ToString());
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.Load(
			new ContentPack(pack.Id, pack.Version, pack.Tables,
				dependencies: ["other"], schema: pack.Schema)));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.Load(
			new ContentPack(pack.Id, pack.Version, pack.Tables,
				schema: new ContentSchemaIdentity(AuthoritativeCathedralMonsterRuntimeCatalog.SchemaId, "1.1.0"))));
	}

	[Fact]
	public void CanonicalFingerprintIsCultureIndependentAndSensitiveToRawCells()
	{
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions());
		var previous = CultureInfo.CurrentCulture;
		try {
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			var same = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(catalog.CreateTable());
			Assert.Equal(catalog.Fingerprint, same.Fingerprint);
		} finally {
			CultureInfo.CurrentCulture = previous;
		}

		var changedRows = catalog.CreateTable().Rows.Select((row, index) =>
			index == 3 ? ReplaceCell(row, "experience", "54321") : row).ToArray();
		var changedTable = new TsvTable(
			AuthoritativeCathedralMonsterRuntimeCatalog.TableName,
			AuthoritativeCathedralMonsterRuntimeCatalog.Columns,
			changedRows);
		Assert.NotEqual(catalog.Fingerprint,
			AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(changedTable).Fingerprint);
	}

	[Fact]
	public void LoaderRejectsMissingUnknownAndReorderedSchemaCells()
	{
		var table = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions()).CreateTable();
		var missing = ReplaceValues(table.Rows[0], values => {
			values.Remove("name");
			return values;
		});
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(
			new TsvTable(table.SourcePath, table.Columns,
				table.Rows.Select((row, index) => index == 0 ? missing : row).ToArray())));

		var unknown = ReplaceValues(table.Rows[0], values => {
			values["unexpected"] = "1";
			return values;
		});
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(
			new TsvTable(table.SourcePath, table.Columns,
				table.Rows.Select((row, index) => index == 0 ? unknown : row).ToArray())));

		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(
			new TsvTable(table.SourcePath, table.Columns.Reverse().ToArray(), table.Rows)));
	}

	[Theory]
	[InlineData("level", "-129")]
	[InlineData("level", "128")]
	[InlineData("to_hit", "256")]
	[InlineData("anim_frame_num", "-129")]
	[InlineData("anim_frame_num_special", "128")]
	[InlineData("monster_class", "3")]
	[InlineData("selection_region", "8")]
	[InlineData("treasure", "65536")]
	[InlineData("experience", "65536")]
	[InlineData("resistance_hell", "256")]
	public void LoaderRejectsNativeWidthAndSemanticViolations(string column, string value)
	{
		var table = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions()).CreateTable();
		var changedRows = table.Rows.Select((row, index) =>
			index == 0 ? ReplaceCell(row, column, value) : row).ToArray();

		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(
			new TsvTable(table.SourcePath, table.Columns, changedRows)));
	}

	[Fact]
	public void LoaderPreservesRawTreasureBitsAndRejectsControlText()
	{
		var table = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions()).CreateTable();
		var treasure = ReplaceCell(table.Rows[0], "treasure", "65535");
		var rawTreasureCatalog = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(
			new TsvTable(table.SourcePath, table.Columns,
				table.Rows.Select((row, index) => index == 0 ? treasure : row).ToArray()));
		Assert.Equal(ushort.MaxValue, rawTreasureCatalog[0].Treasure);

		var control = ReplaceCell(table.Rows[0], "name", "bad\tname");
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(
			new TsvTable(table.SourcePath, table.Columns,
				table.Rows.Select((row, index) => index == 0 ? control : row).ToArray())));

		var invalidUnicode = CreateDefinitions();
		invalidUnicode[0] = Definition(0, name: "\uD800");
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralMonsterRuntimeCatalog(invalidUnicode));
	}

	[Fact]
	public void EffectiveLevelUsesNativeUnsignedBaseAndUniqueRules()
	{
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions());
		var uniqueFallback = Unique(dungeonLevel: 0);
		var uniqueLevel = Unique(dungeonLevel: 7);

		Assert.Equal(4294967168U, catalog.GetEffectiveLevel(0, DiabloDifficulty.Normal));
		Assert.Equal(4294967183U, catalog.GetEffectiveLevel(0, DiabloDifficulty.Nightmare));
		Assert.Equal(unchecked((uint)(sbyte.MinValue + 5)), catalog.GetEffectiveLevel(0, DiabloDifficulty.Normal, uniqueFallback));
		Assert.Equal(14u + 15u, catalog.GetEffectiveLevel(0, DiabloDifficulty.Nightmare, uniqueLevel));
	}

	[Fact]
	public void EffectiveToHitCoversDifficultyUniqueSpecialAndGolemBranches()
	{
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions());
		var unique = Unique(customToHit: 33);

		Assert.Equal(10u, catalog.GetEffectiveToHit(10, DiabloDifficulty.Normal));
		Assert.Equal(95u, catalog.GetEffectiveToHit(10, DiabloDifficulty.Nightmare));
		Assert.Equal(130u, catalog.GetEffectiveToHit(10, DiabloDifficulty.Hell));
		Assert.Equal(33u, catalog.GetEffectiveToHit(10, DiabloDifficulty.Normal, unique: unique));
		Assert.Equal(22u, catalog.GetEffectiveToHit(10, DiabloDifficulty.Normal, special: true));
		Assert.Equal(501u, catalog.GetEffectiveToHit(10, DiabloDifficulty.Hell, playerMinion: true, golemToHit: 501));
		Assert.Throws<ArgumentOutOfRangeException>(() => catalog.GetEffectiveToHit(
			10, DiabloDifficulty.Normal, playerMinion: true, golemToHit: 65536));
		Assert.Throws<ArgumentOutOfRangeException>(() => catalog.GetEffectiveLevel(
			10, DiabloDifficulty.Normal, Unique(dungeonLevel: 256)));
		Assert.Throws<ArgumentOutOfRangeException>(() => catalog.GetEffectiveToHit(
			10, DiabloDifficulty.Normal, unique: Unique(customToHit: 256)));
	}

	[Fact]
	public void EffectiveExperienceUsesNativeDifficultyBonusAndUniqueMultiplier()
	{
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions());

		Assert.Equal(200u, catalog.GetEffectiveExperience(10, DiabloDifficulty.Normal));
		Assert.Equal(2400u, catalog.GetEffectiveExperience(10, DiabloDifficulty.Nightmare));
		Assert.Equal(4800u, catalog.GetEffectiveExperience(10, DiabloDifficulty.Hell));
		Assert.Equal(9600u, catalog.GetEffectiveExperience(10, DiabloDifficulty.Hell, unique: true));
	}

	[Fact]
	public void InitialHitPointsRetainNativeQ6AndDifficultyVariantOrder()
	{
		Assert.Equal(704, AuthoritativeCathedralMonsterRuntimeCatalog.ScaleInitialHitPoints(
			11, DiabloGameMode.Multiplayer, DiabloDifficulty.Normal, DiabloGameVariant.Diablo));
		Assert.Equal(352, AuthoritativeCathedralMonsterRuntimeCatalog.ScaleInitialHitPoints(
			11, DiabloGameMode.SinglePlayer, DiabloDifficulty.Normal, DiabloGameVariant.Diablo));
		Assert.Equal(3 * 352 + 100 * 64, AuthoritativeCathedralMonsterRuntimeCatalog.ScaleInitialHitPoints(
			11, DiabloGameMode.SinglePlayer, DiabloDifficulty.Nightmare, DiabloGameVariant.Diablo));
		Assert.Equal(ushort.MaxValue << 6, AuthoritativeCathedralMonsterRuntimeCatalog.ScaleInitialHitPoints(
			ushort.MaxValue, DiabloGameMode.Multiplayer, DiabloDifficulty.Normal, DiabloGameVariant.Diablo));
		Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.ScaleInitialHitPoints(
			-1, DiabloGameMode.SinglePlayer, DiabloDifficulty.Normal, DiabloGameVariant.Diablo));
		Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCathedralMonsterRuntimeCatalog.ScaleInitialHitPoints(
			ushort.MaxValue + 1, DiabloGameMode.SinglePlayer, DiabloDifficulty.Normal, DiabloGameVariant.Diablo));
	}

	[Fact]
	public void TreasureFlagsRemainRawAndAreExposedWithoutLootSelection()
	{
		var catalog = new AuthoritativeCathedralMonsterRuntimeCatalog(CreateDefinitions());
		Assert.True(catalog.HasNoDrop(0));
		Assert.True(catalog.HasUniqueTreasure(0));
		Assert.Equal(7, catalog.GetTreasureItemMask(0));
	}

	private static List<AuthoritativeCathedralMonsterRuntimeDefinition> CreateDefinitions()
		=> Enumerable.Range(0, AuthoritativeCathedralMonsterRuntimeCatalog.NativeMonsterCount)
			.Select(index => Definition(index,
				level: index == 0 ? sbyte.MinValue : index == 1 ? sbyte.MaxValue : 10,
				name: $"Monster_{index:000}",
				toHit: index == 10 ? 10 : 20,
				animationFrameNum: index == 2 ? sbyte.MinValue : 4,
				toHitSpecial: 22,
				animationFrameNumSpecial: index == 3 ? sbyte.MaxValue : 5,
				monsterClass: index % 3,
				selectionRegion: index % 8,
				treasure: index == 0 ? 0xC007 : 0,
				experience: index == 10 ? 200 : 100,
				resistanceHell: index == 4 ? byte.MaxValue : 12))
			.ToList();

	private static AuthoritativeCathedralMonsterRuntimeDefinition Definition(
		int nativeMonsterId,
		int level = 10,
		string? name = null,
		int toHit = 20,
		int animationFrameNum = 4,
		int toHitSpecial = 22,
		int animationFrameNumSpecial = 5,
		int monsterClass = 0,
		int selectionRegion = 0,
		int treasure = 0,
		int experience = 100,
		int resistanceHell = 12)
		=> new(
			nativeMonsterId,
			$"MONSTER_{nativeMonsterId:000}",
			name ?? $"Monster_{nativeMonsterId:000}",
			level,
			toHit,
			animationFrameNum,
			toHitSpecial,
			animationFrameNumSpecial,
			monsterClass,
			selectionRegion,
			treasure,
			experience,
			resistanceHell);

	private static AuthoritativeCathedralUniqueDefinition Unique(int dungeonLevel = 0, int customToHit = 0)
		=> new(
			NativeUniqueId: 0,
			MonsterType: 10,
			DungeonLevel: dungeonLevel,
			MaximumHitPoints: 100,
			Ai: 0,
			Intelligence: 0,
			MinDamage: 1,
			MaxDamage: 2,
			Resistance: 0,
			ReducePlayerStrength: 0,
			ReducePlayerMagic: 0,
			ReducePlayerDexterity: 0,
			ReducePlayerVitality: 0,
			ReducePlayerMaxHP: 0,
			ReducePlayerMaxMana: 0,
			MonsterPack: AuthoritativeCathedralUniquePack.None,
			CustomToHit: customToHit,
			CustomArmorClass: 0,
			TalkMessage: -1,
			QuestOnly: false);

	private static TsvRow ReplaceCell(TsvRow source, string column, string value)
		=> ReplaceValues(source, values => {
			values[column] = value;
			return values;
		});

	private static TsvRow ReplaceValues(TsvRow source, Func<Dictionary<string, string>, Dictionary<string, string>> mutate)
	{
		var values = source.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
		return new TsvRow(source.LineNumber,
			new ReadOnlyDictionary<string, string>(mutate(values)));
	}
}
