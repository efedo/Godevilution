using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralDepthGeneratorTests
{
	[Theory]
	[InlineData(1, 533)]
	[InlineData(2, 693)]
	[InlineData(3, 761)]
	[InlineData(4, 761)]
	public void SupportedDepthsExposeNativeMinimumAreas(int depth, int minimumArea)
	{
		Assert.Equal(minimumArea, AuthoritativeCathedralDepthGenerator.MinimumAreaForDepth(depth));
	}

	[Theory]
	[InlineData(2588U, AuthoritativeCathedralEntry.Main)]
	[InlineData(2588U, AuthoritativeCathedralEntry.Previous)]
	[InlineData(743271966U, AuthoritativeCathedralEntry.Main)]
	[InlineData(743271966U, AuthoritativeCathedralEntry.Previous)]
	[InlineData(0U, AuthoritativeCathedralEntry.Main)]
	[InlineData(0U, AuthoritativeCathedralEntry.Previous)]
	[InlineData(1U, AuthoritativeCathedralEntry.Main)]
	[InlineData(1U, AuthoritativeCathedralEntry.Previous)]
	[InlineData(0x80000000U, AuthoritativeCathedralEntry.Main)]
	[InlineData(0x80000000U, AuthoritativeCathedralEntry.Previous)]
	[InlineData(uint.MaxValue, AuthoritativeCathedralEntry.Main)]
	[InlineData(uint.MaxValue, AuthoritativeCathedralEntry.Previous)]
	public void DepthOneCompatibilityFacadeMatchesTheSharedDepthAlgorithm(uint seed, AuthoritativeCathedralEntry entry)
	{
		var legacy = AuthoritativeCathedralLevel1Generator.Generate(seed, entry);
		var depth = AuthoritativeCathedralDepthGenerator.Generate(1, seed, entry);

		Assert.Equal(1, depth.Depth);
		Assert.Equal(AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs, depth.Policy);
		Assert.Equal(legacy.InputSeed, depth.InputSeed);
		Assert.Equal(legacy.AttemptSeed, depth.AttemptSeed);
		Assert.Equal(legacy.FinalRandomState, depth.FinalRandomState);
		Assert.Equal(legacy.MegaTiles, depth.MegaTiles);
		Assert.Equal(legacy.NativeTransparency, depth.NativeTransparency);
		Assert.Equal(legacy.StairsUp, depth.StairsUp);
		Assert.Equal(legacy.StairsDown, depth.StairsDown);
		Assert.Equal(legacy.EntryPosition, depth.EntryPosition);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	public void SupportedDepthsAreDeterministicAndRetainEntrySpecificMetadata(int depth)
	{
		var main = AuthoritativeCathedralDepthGenerator.Generate(depth, 2588, AuthoritativeCathedralEntry.Main);
		var mainAgain = AuthoritativeCathedralDepthGenerator.Generate(depth, 2588, AuthoritativeCathedralEntry.Main);
		var previous = AuthoritativeCathedralDepthGenerator.Generate(depth, 2588, AuthoritativeCathedralEntry.Previous);

		Assert.Equal(depth, main.Depth);
		Assert.Equal(AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs, main.Policy);
		Assert.Equal(40 * 40, main.MegaTiles.Count);
		Assert.Equal(80 * 80, main.NativeTransparency.Count);
		Assert.Equal(main.AttemptSeed, mainAgain.AttemptSeed);
		Assert.Equal(main.FinalRandomState, mainAgain.FinalRandomState);
		Assert.Equal(main.MegaTiles, mainAgain.MegaTiles);
		Assert.Equal(main.NativeTransparency, mainAgain.NativeTransparency);
		Assert.Equal(main.StairsUp, mainAgain.StairsUp);
		Assert.Equal(main.StairsDown, mainAgain.StairsDown);
		Assert.NotEqual(main.EntryPosition, previous.EntryPosition);
	}

	[Fact]
	public void RejectsUnsupportedDepthPolicyAndEntry()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			AuthoritativeCathedralDepthGenerator.Generate(0, 1, AuthoritativeCathedralEntry.Main));
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			AuthoritativeCathedralDepthGenerator.Generate(5, 1, AuthoritativeCathedralEntry.Main));
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			AuthoritativeCathedralDepthGenerator.Generate(
				1,
				1,
				AuthoritativeCathedralEntry.Main,
				(AuthoritativeCathedralDepthGenerationPolicy)999));
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			AuthoritativeCathedralDepthGenerator.Generate(
				1,
				1,
				(AuthoritativeCathedralEntry)999));
	}

	[Fact]
	public void DepthMaterializerCopiesLayersAndRetainsDepthMetadata()
	{
		var megaTiles = Enumerable.Repeat<ushort>(1, 40 * 40).ToArray();
		var transparency = new ushort[80 * 80];
		transparency[0] = 17;
		var result = new AuthoritativeCathedralDepthResult(
			4,
			AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
			2588,
			11,
			22,
			megaTiles,
			transparency,
			new AuthoritativeCathedralPoint(1, 1),
			new AuthoritativeCathedralPoint(2, 2),
			new AuthoritativeCathedralPoint(73, 74));

		var geometry = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
			4,
			result,
			CreatePieces(),
			CreateProperties(),
			"cathedral_depth_4");

		megaTiles[0] = 2;
		transparency[0] = 99;

		Assert.Equal(4, geometry.GenerationResult.Depth);
		Assert.Equal(result.Policy, geometry.GenerationResult.Policy);
		Assert.Equal((ushort)1, geometry.GenerationResult.GetMegaTile(0, 0));
		Assert.Equal((ushort)17, geometry.GenerationResult.GetTransparency(0, 0));
		Assert.Equal((112, 112), (geometry.Level.Width, geometry.Level.Height));
		Assert.Equal("cathedral_depth_4", geometry.Level.StableSymbol);
		Assert.Equal((73, 74), (geometry.Level.StartPositionX, geometry.Level.StartPositionY));
	}

	[Fact]
	public void DepthMaterializerRejectsUnsupportedPolicyAndDepth()
	{
		var result = CreateResult(2);
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
				2,
				result with { Depth = 5 },
				CreatePieces(),
				CreateProperties(),
				"cathedral_depth_5"));
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
				2,
				result with { Policy = (AuthoritativeCathedralDepthGenerationPolicy)999 },
				CreatePieces(),
				CreateProperties(),
				"cathedral_depth_2"));
	}

	[Theory]
	[InlineData(true, 1599)]
	[InlineData(true, 1601)]
	[InlineData(false, 6399)]
	[InlineData(false, 6401)]
	public void DepthMaterializerRejectsMalformedLayerDimensions(bool megaLayer, int length)
	{
		var result = CreateResult(3);
		result = megaLayer
			? result with { MegaTiles = Enumerable.Repeat<ushort>(1, length).ToArray() }
			: result with { NativeTransparency = new ushort[length] };
		Assert.Throws<InvalidDataException>(() => Materialize(result));
	}

	[Fact]
	public void DepthMaterializerRejectsNullLayersAndInvalidNativeValues()
	{
		var result = CreateResult(2);
		Assert.Throws<ArgumentNullException>(() => Materialize(result with { MegaTiles = null! }));
		Assert.Throws<ArgumentNullException>(() => Materialize(result with { NativeTransparency = null! }));
		Assert.Throws<InvalidDataException>(() => Materialize(result with { MegaTiles = new ushort[40 * 40] }));
		Assert.Throws<InvalidDataException>(() => Materialize(result with {
			MegaTiles = Enumerable.Repeat<ushort>(256, 40 * 40).ToArray(),
		}));
		Assert.Throws<InvalidDataException>(() => Materialize(result with {
			NativeTransparency = Enumerable.Repeat<ushort>(256, 80 * 80).ToArray(),
		}));
	}

	[Fact]
	public void DepthResultGettersRejectNegativeAndExclusiveUpperBounds()
	{
		var result = CreateResult(4);
		foreach (var coordinate in new[] { -1, 40 }) {
			Assert.Throws<ArgumentOutOfRangeException>(() => result.GetMegaTile(coordinate, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => result.GetMegaTile(0, coordinate));
		}
		foreach (var coordinate in new[] { -1, 80 }) {
			Assert.Throws<ArgumentOutOfRangeException>(() => result.GetTransparency(coordinate, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => result.GetTransparency(0, coordinate));
		}
	}

	private static AuthoritativeCathedralDepthGeometry Materialize(AuthoritativeCathedralDepthResult result)
		=> AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
			(uint)result.Depth, result, CreatePieces(), CreateProperties(), $"cathedral_depth_{result.Depth}");

	private static AuthoritativeCathedralDepthResult CreateResult(int depth)
		=> new(
			depth,
			AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs,
			1,
			1,
			1,
			Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(),
			new ushort[80 * 80],
			new AuthoritativeCathedralPoint(1, 1),
			new AuthoritativeCathedralPoint(2, 2),
			new AuthoritativeCathedralPoint(20, 20));

	private static AuthoritativeDunPieceCatalog CreatePieces()
		=> new([
			new AuthoritativeDunPieceDefinition(0, 11, 12, 13, 14),
			new AuthoritativeDunPieceDefinition(21, 1, 2, 3, 4),
		]);

	private static AuthoritativeDunTilePropertyCatalog CreateProperties()
		=> new([
			new AuthoritativeDunTileProperty(1, AuthoritativeDunTileFlags.None),
			new AuthoritativeDunTileProperty(2, AuthoritativeDunTileFlags.None),
			new AuthoritativeDunTileProperty(3, AuthoritativeDunTileFlags.None),
			new AuthoritativeDunTileProperty(4, AuthoritativeDunTileFlags.None),
			new AuthoritativeDunTileProperty(11, AuthoritativeDunTileFlags.None),
			new AuthoritativeDunTileProperty(12, AuthoritativeDunTileFlags.None),
			new AuthoritativeDunTileProperty(13, AuthoritativeDunTileFlags.None),
			new AuthoritativeDunTileProperty(14, AuthoritativeDunTileFlags.None),
		]);
}
