using System.Globalization;

using Devilution.Data;

using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralAutomapMetadataTests
{
	[Fact]
	public void CompleteTableRetainsNativeOrdinalsAndExplicitColdTile()
	{
		var metadata = Load();

		Assert.Equal(256, metadata.Definitions.Count);
		Assert.Equal(Enumerable.Range(0, 256), metadata.Definitions.Select(tile => (int)tile.TileId));
		Assert.Equal(AuthoritativeCathedralAutomapTileType.None, metadata.Get(0).Type);
		Assert.Equal(AuthoritativeCathedralAutomapTileFlags.None, metadata.Get(0).Flags);
		Assert.Equal(55, (byte)metadata.Get(255).Type);
		Assert.Equal(60, Enum.GetValues<AuthoritativeCathedralAutomapTileType>().Length);
		Assert.Equal(59, (byte)AuthoritativeCathedralAutomapTileType.PentagramOpen);
		Assert.Equal(0x15, (byte)AuthoritativeCathedralAutomapTileFlags.VerticalPassage);
		Assert.Equal(0x2A, (byte)AuthoritativeCathedralAutomapTileFlags.HorizontalPassage);
	}

	[Fact]
	public void CreateTableAndCanonicalTextRoundTripWithoutCallerAliases()
	{
		var source = Definitions();
		var table = AuthoritativeCathedralAutomapMetadata.CreateTable(source);
		var metadata = AuthoritativeCathedralAutomapMetadata.ParseTsv(table.ToCanonicalTsv());

		Assert.Equal(table.ToCanonicalTsv(), metadata.ToCanonicalTsv());
		Assert.Equal(metadata.Fingerprint, AuthoritativeCathedralAutomapMetadata.Load(
			new ContentPack("fixture", "1.0.0", [table],
				schema: new ContentSchemaIdentity(
					AuthoritativeCathedralAutomapMetadata.SchemaId,
					AuthoritativeCathedralAutomapMetadata.SchemaVersion))).Fingerprint);
	}

	[Theory]
	[InlineData("header")]
	[InlineData("missing")]
	[InlineData("order")]
	[InlineData("duplicate")]
	[InlineData("type")]
	[InlineData("flags")]
	public void MalformedTablesAreRejectedBeforeCatalogConstruction(string mutation)
	{
		var lines = ValidText().TrimEnd('\n').Split('\n').ToArray();
		lines[0] = mutation == "header" ? "id\tflags\ttype" : lines[0];
		if (mutation == "missing")
			lines = lines[..^1];
		if (mutation == "order")
			lines[1] = "1\t0\t0";
		if (mutation == "duplicate")
			lines[3] = "1\t0\t0";
		if (mutation == "type")
			lines[2] = "1\t60\t0";
		if (mutation == "flags")
			lines[1] = "0\t0\t256";

		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralAutomapMetadata.Load(
			string.Join('\n', lines) + "\n"));
	}

	[Fact]
	public void TileZeroCannotBeUsedAsAnOpaqueTailOrSemanticOverride()
	{
		var lines = ValidText().TrimEnd('\n').Split('\n').ToArray();
		lines[1] = "0\t2\t64";
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralAutomapMetadata.ParseTsv(
			string.Join('\n', lines) + "\n"));
	}

	[Fact]
	public void VerticalAndCrossNeighborsMatchNativeSetAutomapView()
	{
		var metadata = Load();
		var coarse = new ushort[1600];
		coarse[4 + 5 * 40] = 1; // vertical, non-solid
		coarse[3 + 5 * 40] = 2; // dirt to the west
		coarse[10 + 10 * 40] = 3; // solid cross
		coarse[10 + 11 * 40] = 2; // solid cross's south-west dirt
		coarse[11 + 10 * 40] = 2; // solid cross's south-east dirt
		var exploration = new byte[1600];

		metadata.ProjectExplorationAtMap(coarse, 4, 5, 3, exploration);
		metadata.ProjectExplorationAtMap(coarse, 10, 10, 4, exploration);

		Assert.Equal(3, exploration[4 + 5 * 40]);
		Assert.Equal(3, exploration[3 + 5 * 40]);
		Assert.Equal(4, exploration[10 + 10 * 40]);
		Assert.Equal(4, exploration[10 + 11 * 40]);
		Assert.Equal(4, exploration[11 + 10 * 40]);
	}

	[Fact]
	public void CornerArchCorrectionPreventsAResolvedDiamondFromReceivingCornerPropagation()
	{
		var definitions = Definitions();
		definitions[1] = new(1, AuthoritativeCathedralAutomapTileType.Cross,
			AuthoritativeCathedralAutomapTileFlags.Dirt
			| AuthoritativeCathedralAutomapTileFlags.VerticalArch);
		definitions[2] = new(2, AuthoritativeCathedralAutomapTileType.Corner,
			AuthoritativeCathedralAutomapTileFlags.Dirt);
		definitions[3] = new(3, AuthoritativeCathedralAutomapTileType.None,
			AuthoritativeCathedralAutomapTileFlags.HorizontalArch);
		definitions[4] = new(4, AuthoritativeCathedralAutomapTileType.None,
			AuthoritativeCathedralAutomapTileFlags.VerticalArch);
		var metadata = AuthoritativeCathedralAutomapMetadata.Load(
			AuthoritativeCathedralAutomapMetadata.ToCanonicalTsv(definitions));
		var coarse = new ushort[1600];
		coarse[10 + 10 * 40] = 1;
		coarse[10 + 11 * 40] = 2;
		coarse[9 + 11 * 40] = 5;
		coarse[10 + 10 * 40] = 1;
		var exploration = new byte[1600];

		metadata.ProjectExplorationAtMap(coarse, 10, 10, 4, exploration);

		Assert.Equal(4, exploration[10 + 10 * 40]);
		Assert.Equal(0, exploration[10 + 11 * 40]);
	}

	[Fact]
	public void FinePositionUsesNativeSixteenTileOriginAndReturnedProjectionIsDetached()
	{
		var metadata = Load();
		var coarse = new ushort[1600];
		coarse[7 + 8 * 40] = 1;

		var result = metadata.ProjectExploration(coarse, 30, 32, 2);

		Assert.Equal(2, result[7 + 8 * 40]);
		Assert.IsAssignableFrom<IReadOnlyList<byte>>(result);
		Assert.Equal(0, result[0]);
	}

	[Fact]
	public void ProjectionRejectsNonNativeBoundsAndNeverPartiallyWrites()
	{
		var metadata = Load();
		var coarse = new ushort[1600];
		var exploration = Enumerable.Repeat((byte)1, 1600).ToArray();
		var before = exploration.ToArray();

		Assert.Throws<ArgumentException>(() => metadata.ProjectExploration(coarse, 0, 0, 1, new byte[1599]));
		Assert.Throws<InvalidDataException>(() => metadata.ProjectExploration(coarse, 0, 0, 5, exploration));
		coarse[0] = 512;
		Assert.Throws<InvalidDataException>(() => metadata.ProjectExploration(coarse, 0, 0, 1, exploration));
		Assert.Equal(before, exploration);

		coarse[0] = 0;
		metadata.ProjectExploration(coarse, -100, -100, 4, exploration);
		Assert.Equal(before, exploration);
	}

	[Fact]
	public void CompleteNativeTypeAndFlagWidthsAreRetained()
	{
		var definitions = Definitions();
		definitions[255] = new(255, AuthoritativeCathedralAutomapTileType.PentagramOpen,
			(AuthoritativeCathedralAutomapTileFlags)255);
		var metadata = AuthoritativeCathedralAutomapMetadata.Load(
			AuthoritativeCathedralAutomapMetadata.CreateTable(definitions).ToCanonicalTsv());
		Assert.Equal(definitions[255], metadata.Definitions[255]);
		definitions[255] = new(255, (AuthoritativeCathedralAutomapTileType)60, 0);
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralAutomapMetadata.CreateTable(definitions));
	}

	[Fact]
	public void ConstructedPackCannotHideUndeclaredRowValues()
	{
		var metadata = Load();
		var original = metadata.Tables[0];
		var rows = original.Rows.ToArray();
		var values = rows[1].Values.ToDictionary(pair => pair.Key, pair => pair.Value);
		values["hidden"] = "unhashed";
		rows[1] = new TsvRow(rows[1].LineNumber, values);
		var pack = new ContentPack("fixture", "1.0.0", [new TsvTable(original.SourcePath, original.Columns, rows)],
			schema: new ContentSchemaIdentity(AuthoritativeCathedralAutomapMetadata.SchemaId, AuthoritativeCathedralAutomapMetadata.SchemaVersion));
		Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralAutomapMetadata.Load(pack));
	}

	[Fact]
	public void InvalidUtf8RefusesBeforeParsing()
		=> Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralAutomapMetadata.LoadUtf8(new byte[] { 0xC3, 0x28 }));

	private static AuthoritativeCathedralAutomapMetadata Load()
		=> AuthoritativeCathedralAutomapMetadata.Load(ValidText());

	private static string ValidText()
		=> AuthoritativeCathedralAutomapMetadata.ToCanonicalTsv(Definitions());

	private static List<AuthoritativeCathedralAutomapTileDefinition> Definitions()
	{
		var definitions = Enumerable.Range(0, 256)
			.Select(id => new AuthoritativeCathedralAutomapTileDefinition(
				(byte)id, AuthoritativeCathedralAutomapTileType.None,
				AuthoritativeCathedralAutomapTileFlags.None))
			.ToList();
		definitions[1] = new(1, AuthoritativeCathedralAutomapTileType.Vertical,
			AuthoritativeCathedralAutomapTileFlags.None);
		definitions[2] = new(2, AuthoritativeCathedralAutomapTileType.Corner,
			AuthoritativeCathedralAutomapTileFlags.Dirt);
		definitions[3] = new(3, AuthoritativeCathedralAutomapTileType.Cross,
			AuthoritativeCathedralAutomapTileFlags.Dirt);
		definitions[4] = new(4, AuthoritativeCathedralAutomapTileType.Corner,
			AuthoritativeCathedralAutomapTileFlags.Dirt);
		definitions[5] = new(5, AuthoritativeCathedralAutomapTileType.None,
			AuthoritativeCathedralAutomapTileFlags.HorizontalArch);
		definitions[6] = new(6, AuthoritativeCathedralAutomapTileType.None,
			AuthoritativeCathedralAutomapTileFlags.VerticalArch);
		definitions[255] = new(255, AuthoritativeCathedralAutomapTileType.VerticalBridgeLava,
			AuthoritativeCathedralAutomapTileFlags.Stairs);
		return definitions;
	}
}
