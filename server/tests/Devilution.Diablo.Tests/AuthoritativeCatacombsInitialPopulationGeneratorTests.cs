using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Focused family-boundary and portable L2 initial-population checks. */
public sealed class AuthoritativeCatacombsInitialPopulationGeneratorTests
{
	[Theory]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	[InlineData(8)]
	public void GeneratesL2GolemsThemesAndObjectsWithItsExplicitFamily(int depth)
	{
		var parameters = CreateParameters(depth, populationSeed: 4);
		var result = AuthoritativeCatacombsInitialPopulationGenerator.Generate(parameters);

		Assert.Equal(AuthoritativeCatacombsInitialPopulationGenerator.Policy,
			"quest-disabled-original-diablo-catacombs-initial-population");
		Assert.Equal(4, result.Population.Monsters.Count);
		Assert.All(result.Population.Monsters, monster => {
			Assert.Equal(109, monster.Type);
			Assert.Equal(new AuthoritativeCathedralPopulationPoint(1, 0), monster.Tile);
		});
		Assert.Equal(parameters.Generation.Themes.Count, result.SelectedThemes.Count);
		Assert.Equal(parameters.Generation.Themes.Count, result.Themes.Themes.Count);
		Assert.Equal(parameters.Generation.Themes.Select(room => room.Transparency),
			result.Themes.Themes.Select(theme => theme.RegionId));
		Assert.DoesNotContain(result.Population.Objects, item => item.Type == 48);
		Assert.Contains(result.Population.Objects, item => item.Type is 5 or 6 or 7);
		Assert.True(result.Population.ActiveObjectCount > 4);
	}

	[Fact]
	public void L2DoorsAndTorchesUseCatacombsPiecesAndPermitTheNativePlacementRange()
	{
		var parameters = CreateParameters(5, populationSeed: 4, includeStaticFeatures: true);
		var result = AuthoritativeCatacombsInitialPopulationGenerator.Generate(parameters);

		var doors = result.Population.Objects.Where(item => item.Type is 42 or 43).ToArray();
		Assert.Equal(4, doors.Length);
		Assert.All(doors, door => {
			Assert.True(door.DoorFlag);
			Assert.False(door.MissilePassable);
			Assert.Equal(3, door.SelectionRegion);
		});
		Assert.Equal((ushort)537, result.Population.Pieces[Index(40, 40)]);
		Assert.Equal((byte)0, result.Population.Special[Index(40, 40)]);
		Assert.Equal((ushort)539, result.Population.Pieces[Index(50, 50)]);
		Assert.Equal((byte)0, result.Population.Special[Index(50, 50)]);
		Assert.Contains(result.Population.Objects, item => item.Type == 46 && item.Position == new AuthoritativeCathedralPopulationPoint(70, 70) && item.Var1 == -1);
		Assert.Contains(result.Population.Objects, item => item.Type == 47 && item.Position == new AuthoritativeCathedralPopulationPoint(72, 70) && item.Var1 == -1);
		Assert.Contains(result.Population.Objects, item => item.Type is 57 or 58);
	}

	[Theory]
	[InlineData(0x0000_0000u, 0, 250, 1, 5, 6)]
	[InlineData(0x0001_0000u, 1, 253, 4, 3, 4)]
	[InlineData(0x0002_0000u, 2, 256, 7, 1, 2)]
	public void L8StoryBookUsesTheExplicitCatacombsSpeechColumn(
		uint storySeed, int row, int messageId, int bookVar3, int animFrame, int bookVar4)
	{
		var parameters = CreateParameters(8, populationSeed: 2588, storySeed: storySeed);
		var result = AuthoritativeCatacombsInitialPopulationGenerator.Generate(parameters);
		var book = Assert.Single(result.Population.Objects, item => item.Type == 86);

		Assert.Equal(row, book.Var1);
		Assert.Equal(messageId, book.Var2);
		Assert.Equal(bookVar3, book.Var3);
		Assert.Equal((uint)animFrame, book.AnimFrame);
		Assert.Equal(bookVar4, book.Var4);
		Assert.Equal(6, result.Population.Objects.Count(item => item.Type == 87));
	}

	[Fact]
	public void HoldsOnlyTheGeneratedCoarseRoomInteriorAndPreservesInitialPopulatedCells()
	{
		var parameters = CreateParameters(5, populationSeed: 2588, initialPopulated: true);
		var result = AuthoritativeCatacombsInitialPopulationGenerator.Generate(parameters);
		var held = result.Themes.HeldPopulatedCells;
		var expected = new HashSet<int> { 0 };
		for (var y = 36; y < 44; y++)
			for (var x = 36; x < 44; x++)
				expected.Add(Index(x, y));

		Assert.Equal(expected.Order(), held.Select((value, index) => (value, index))
			.Where(pair => pair.value).Select(pair => pair.index).Order());
		Assert.Equal(65, held.Count(value => value));
	}

	[Fact]
	public void PublicCathedralPathsStayStrictAndInternalFamilyMismatchIsRejected()
	{
		var parameters = CreateParameters(5);

		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralBestiaryParameters(
			5, parameters.PopulationSeed, parameters.BestiaryParameters.Catalog));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralBestiaryGenerator.Generate(
			parameters.BestiaryParameters, default));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralBestiaryGenerator.Generate(
			parameters.BestiaryParameters, AuthoritativeNativeDungeonFamilyContext.Cathedral(1)));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralPopulationGenerator.Generate(
			parameters.ObjectTemplate, default));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeGenerator.Generate(
			parameters.ThemeTemplate));
	}

	[Fact]
	public void ValidatesMalformedGenerationBeforeCopyingAndDetachesAcceptedArrays()
	{
		var parameters = CreateParameters(5);
		var geometry = parameters.PreThemeGeometry;
		var nullThemes = parameters.Generation with { Themes = null! };

		Assert.Throws<InvalidDataException>(() => new AuthoritativeCatacombsInitialPopulationParameters(
			5, parameters.PopulationSeed, parameters.StorySeed, nullThemes, geometry,
			parameters.BestiaryParameters.Catalog, parameters.ObjectTemplate.Objects,
			parameters.ObjectTemplate.Player, parameters.ObjectTemplate.TrapPercent,
			parameters.ObjectTemplate.StoryMessages));
		var nullMegaTiles = parameters.Generation with { MegaTiles = null! };
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCatacombsInitialPopulationParameters(
			5, parameters.PopulationSeed, parameters.StorySeed, nullMegaTiles, geometry,
			parameters.BestiaryParameters.Catalog, parameters.ObjectTemplate.Objects,
			parameters.ObjectTemplate.Player, parameters.ObjectTemplate.TrapPercent,
			parameters.ObjectTemplate.StoryMessages));
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCatacombsInitialPopulationParameters(
			5, parameters.PopulationSeed, parameters.StorySeed, CreateGeneration(6), geometry,
			parameters.BestiaryParameters.Catalog, parameters.ObjectTemplate.Objects,
			parameters.ObjectTemplate.Player, parameters.ObjectTemplate.TrapPercent,
			parameters.ObjectTemplate.StoryMessages));
		var changedTransparency = parameters.Generation.NativeTransparency.ToArray();
		changedTransparency[0] = 1;
		var mismatchedTransparency = parameters.Generation with { NativeTransparency = changedTransparency };
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCatacombsInitialPopulationParameters(
			5, parameters.PopulationSeed, parameters.StorySeed, mismatchedTransparency, geometry,
			parameters.BestiaryParameters.Catalog, parameters.ObjectTemplate.Objects,
			parameters.ObjectTemplate.Player, parameters.ObjectTemplate.TrapPercent,
			parameters.ObjectTemplate.StoryMessages));

		var sourceMegaTiles = Enumerable.Repeat((ushort)12,
			AuthoritativeCatacombsGenerationResult.MegaWidth * AuthoritativeCatacombsGenerationResult.MegaHeight).ToArray();
		var sourceGeneration = CreateGeneration(5) with { MegaTiles = sourceMegaTiles };
		var detached = new AuthoritativeCatacombsInitialPopulationParameters(
			5, parameters.PopulationSeed, parameters.StorySeed, sourceGeneration, geometry,
			parameters.BestiaryParameters.Catalog, parameters.ObjectTemplate.Objects,
			parameters.ObjectTemplate.Player, parameters.ObjectTemplate.TrapPercent,
			parameters.ObjectTemplate.StoryMessages);
		sourceMegaTiles[0] = 0;
		Assert.Equal((ushort)12, detached.Generation.MegaTiles[0]);
	}

	private static AuthoritativeCatacombsInitialPopulationParameters CreateParameters(
		int depth,
		uint populationSeed = 2588,
		uint storySeed = 0x1357_9BDF,
		bool includeStaticFeatures = false,
		bool initialPopulated = false)
	{
		var generation = CreateGeneration(depth);
		var pieces = Enumerable.Repeat((ushort)130, AuthoritativeCathedralPopulationGeometry.CellCount).ToArray();
		if (includeStaticFeatures) {
			pieces[Index(40, 40)] = 12;
			pieces[Index(41, 40)] = 540;
			pieces[Index(50, 50)] = 16;
			pieces[Index(51, 50)] = 541;
			pieces[Index(70, 70)] = 0;
			pieces[Index(72, 70)] = 4;
		}
		var populated = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		if (initialPopulated) populated[0] = true;
		var geometry = CreateGeometry(depth, generation.NativeTransparency, pieces, populated);
		var monsters = CreateMonsters();
		var objects = Enumerable.Range(0, 109)
			.Select(nativeType => new AuthoritativeCathedralPopulationObjectDefinition(
				nativeType, 0, 0, 1, 96, 0))
			.ToArray();
		var player = new AuthoritativeCathedralPopulationPlayer(
			0, depth, false, false,
			new(75, 68), new(75, 68));
		return new(depth, populationSeed, storySeed, generation, geometry,
			monsters, objects, player, 0, [250, 253, 256], 100_000, 10_000);
	}

	private static AuthoritativeCatacombsGenerationResult CreateGeneration(int depth)
	{
		var transparency = new byte[AuthoritativeCathedralPopulationGeometry.CellCount];
		for (var y = 36; y < 44; y++)
			for (var x = 36; x < 44; x++)
				transparency[Index(x, y)] = 1;
		var mega = Enumerable.Repeat((ushort)12, AuthoritativeCatacombsGenerationResult.MegaWidth
			* AuthoritativeCatacombsGenerationResult.MegaHeight).ToArray();
		var room = new AuthoritativeCatacombsThemeRoom(10, 10, 5, 5, 1);
		return new(
			depth,
			AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs,
			AuthoritativeCatacombsEntry.Main,
			2588,
			null,
			2588,
			100,
			mega,
			mega,
			new bool[mega.Length],
			transparency,
			new byte[transparency.Length],
			[room],
			new(3, 4),
			new(5, 6),
			depth == 5 ? new AuthoritativeCathedralPoint(7, 8) : null,
			new(27, 28),
			new(16, 16),
			new(96, 96),
			default,
			default,
			1);
	}

	private static AuthoritativeCathedralPopulationGeometry CreateGeometry(
		int depth,
		IReadOnlyList<byte> transparency,
		IReadOnlyList<ushort>? pieces,
		bool initialPopulated)
	{
		var populated = new bool[AuthoritativeCathedralPopulationGeometry.CellCount];
		if (initialPopulated) populated[0] = true;
		return CreateGeometry(depth, transparency, pieces, populated);
	}

	private static AuthoritativeCathedralPopulationGeometry CreateGeometry(
		int depth,
		IReadOnlyList<byte> transparency,
		IReadOnlyList<ushort>? pieces,
		IReadOnlyList<bool> populated)
	{
		_ = depth;
		return new(
			pieces ?? Enumerable.Repeat((ushort)130, AuthoritativeCathedralPopulationGeometry.CellCount).ToArray(),
			new byte[AuthoritativeCathedralPopulationGeometry.CellCount],
			transparency,
			new byte[AuthoritativeCathedralPopulationGeometry.PieceCount],
			populated,
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount],
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount]);
	}

	private static IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> CreateMonsters()
	{
		var frames = Enumerable.Repeat(8, 6).ToArray();
		var rates = Enumerable.Repeat(4, 6).ToArray();
		return Array.AsReadOnly(Enumerable.Range(0, AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount)
			.Select(nativeId => new AuthoritativeCathedralRawMonsterDefinition(
				nativeId,
				AuthoritativeCathedralMonsterAvailability.Never,
				1,
				1,
				8,
				nativeId is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27,
				4,
				9,
				1,
				1,
				0,
				2,
				4,
				0,
				0,
				1,
				0,
				0,
				0,
				0,
				0,
				0,
				0,
				false,
				96,
				frames,
				rates))
			.ToArray());
	}

	private static int Index(int x, int y)
		=> x + y * AuthoritativeCathedralPopulationGeometry.Width;
}
