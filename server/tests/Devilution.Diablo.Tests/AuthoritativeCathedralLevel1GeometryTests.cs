using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralLevel1GeometryTests
{
    [Fact]
    public void MaterializesNativePaddingInteriorTransparencyAndEntry()
    {
        var transparency = new ushort[80 * 80];
        transparency[0] = 17;
        transparency[^1] = 23;
        var result = CreateResult(
            Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(),
            transparency,
            entry: new AuthoritativeCathedralPoint(73, 74));

        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            result,
            CreatePieces(),
            CreateProperties(),
            stableSymbol: "cathedral",
            isDefaultStart: false);

        var level = geometry.Level;
        Assert.Equal(112, level.Width);
        Assert.Equal(112, level.Height);
        Assert.Empty(level.NativeMegaTileLayer);
        Assert.Equal(40 * 40, geometry.GenerationResult.MegaTiles.Count);
        Assert.Equal((ushort)1, geometry.GenerationResult.GetMegaTile(0, 0));
        Assert.Equal((ushort)1, geometry.GenerationResult.GetMegaTile(39, 39));
        Assert.Equal((ushort)1, level.NativePieceLayer[0]);
        Assert.Equal((ushort)4, level.NativePieceLayer[^1]);
        Assert.Equal((ushort)11, level.NativePieceLayer[16 * 112 + 16]);
        Assert.Equal((ushort)14, level.NativePieceLayer[95 * 112 + 95]);
        Assert.Equal((ushort)17, level.NativeTransparency[16 * 112 + 16]);
        Assert.Equal((ushort)23, level.NativeTransparency[95 * 112 + 95]);
        Assert.Equal((ushort)0, level.NativeTransparency[15 * 112 + 16]);
        Assert.Equal(73, level.StartPositionX);
        Assert.Equal(74, level.StartPositionY);
        Assert.Equal("cathedral", level.StableSymbol);
    }

    [Fact]
    public void InteriorPiecesReplaceExteriorDirtCollisionAndMissileFlags()
    {
        var pieces = new AuthoritativeDunPieceCatalog([
            new AuthoritativeDunPieceDefinition(0, 11, 11, 11, 11),
            new AuthoritativeDunPieceDefinition(21, 1, 1, 1, 1),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new AuthoritativeDunTileProperty(1, AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile),
            new AuthoritativeDunTileProperty(11, AuthoritativeDunTileFlags.None),
        ]);
        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            CreateResult(Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(), new ushort[80 * 80]),
            pieces,
            properties,
            isDefaultStart: true);

        var level = geometry.Level;
        var interiorCell = 16 * 112 + 16;
        var exteriorCell = 15 * 112 + 16;
        Assert.Equal((ushort)11, level.NativePieceLayer[interiorCell]);
        Assert.DoesNotContain(interiorCell, level.BlockedCells);
        Assert.DoesNotContain(interiorCell, level.MissileBlockedCells);
        Assert.Contains(exteriorCell, level.BlockedCells);
        Assert.Contains(exteriorCell, level.MissileBlockedCells);
    }

    [Fact]
    public void ExplicitVisionFlagsIncludeNativePieceZero()
    {
        var pieces = new AuthoritativeDunPieceCatalog([
            new AuthoritativeDunPieceDefinition(0, 0, 0, 0, 0),
            new AuthoritativeDunPieceDefinition(21, 1, 1, 1, 1),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new AuthoritativeDunTileProperty(0, AuthoritativeDunTileFlags.BlockLight),
            new AuthoritativeDunTileProperty(1, AuthoritativeDunTileFlags.None),
        ]);
        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            CreateResult(Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(), new ushort[80 * 80]),
            pieces,
            properties,
            isDefaultStart: true);

        var interiorCell = 16 * 112 + 16;
        Assert.Equal((ushort)0, geometry.Level.NativePieceLayer[interiorCell]);
        Assert.DoesNotContain(interiorCell, geometry.Level.BlockedCells);
        Assert.Contains(interiorCell, geometry.Level.NativeVisionBlockedCells);
    }

    [Fact]
    public void UsesOneBasedMegaIdsAndNativeQuadrantOrderAndFlags()
    {
        var megaTiles = Enumerable.Repeat<ushort>(2, 40 * 40).ToArray();
        var result = CreateResult(megaTiles, new ushort[80 * 80], entry: new AuthoritativeCathedralPoint(60, 60));
        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            result,
            CreatePieces(),
            CreateProperties(),
            isDefaultStart: false);

        var level = geometry.Level;
        var origin = 16 * 112 + 16;
        Assert.Equal((ushort)31, level.NativePieceLayer[origin]);
        Assert.Equal((ushort)32, level.NativePieceLayer[origin + 1]);
        Assert.Equal((ushort)33, level.NativePieceLayer[origin + 112]);
        Assert.Equal((ushort)34, level.NativePieceLayer[origin + 113]);
        Assert.Equal((ushort)2, geometry.GenerationResult.GetMegaTile(0, 0));
        Assert.Contains(origin, level.BlockedCells);
        Assert.Contains(origin + 1, level.MissileBlockedCells);
        Assert.Contains(origin + 112, level.NativeVisionBlockedCells);
        Assert.True(level.NativePieceProperties[34].HasFlag(AuthoritativeDunTileFlags.Trap));
        Assert.True(level.NativePieceProperties[33].HasFlag(AuthoritativeDunTileFlags.BlockLight));
    }

    [Fact]
    public void PreservesDefaultStartDeathDropAndGenerationMetadata()
    {
        var result = CreateResult(
            Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(),
            new ushort[80 * 80],
            entry: new AuthoritativeCathedralPoint(20, 20));
        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            result,
            CreatePieces(),
            CreateProperties((11, AuthoritativeDunTileFlags.None)),
            stableSymbol: "cathedral",
            isDefaultStart: true,
            suppressMultiplayerDeathDrops: true);

        Assert.True(geometry.Level.IsDefaultStart);
        Assert.True(geometry.Level.SuppressMultiplayerDeathDrops);
        Assert.Equal(result.InputSeed, geometry.GenerationResult.InputSeed);
        Assert.Equal(result.AttemptSeed, geometry.GenerationResult.AttemptSeed);
        Assert.Equal(result.FinalRandomState, geometry.GenerationResult.FinalRandomState);
        Assert.Equal(result.StairsUp, geometry.GenerationResult.StairsUp);
        Assert.Equal(result.StairsDown, geometry.GenerationResult.StairsDown);
        Assert.Equal(result.EntryPosition, geometry.GenerationResult.EntryPosition);
    }

    [Fact]
    public void RejectsMalformedTransparencyAndStairCoordinates()
    {
        var valid = CreateResult(
            Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(),
            new ushort[80 * 80]);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid with {
                    NativeTransparency = [.. valid.NativeTransparency.Take(6399), (ushort)256],
                },
                CreatePieces(),
                CreateProperties(),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid with { StairsUp = new AuthoritativeCathedralPoint(-1, 0) },
                CreatePieces(),
                CreateProperties(),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid with { StairsDown = new AuthoritativeCathedralPoint(40, 0) },
                CreatePieces(),
                CreateProperties(),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid,
                CreatePieces(),
                CreateProperties(),
                isDefaultStart: true));
    }

    [Fact]
    public void AppliesNativeCathedralSolCorrectionsWithoutChangingCatalog()
    {
        var properties = CreateProperties((9, AuthoritativeDunTileFlags.None));
        var pieces = new AuthoritativeDunPieceCatalog([
            new AuthoritativeDunPieceDefinition(0, 9, 9, 9, 9),
            new AuthoritativeDunPieceDefinition(21, 1, 1, 1, 1),
        ]);
        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            CreateResult(Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(), new ushort[80 * 80]),
            pieces,
            properties,
            isDefaultStart: false);

        var level = geometry.Level;
        Assert.True(level.NativePieceProperties[9].HasFlag(AuthoritativeDunTileFlags.BlockLight));
        Assert.True(level.NativePieceProperties[9].HasFlag(AuthoritativeDunTileFlags.BlockMissile));
        Assert.False(properties.Definitions.Single(property => property.PieceId == 9).Flags
            .HasFlag(AuthoritativeDunTileFlags.BlockLight));
    }

    [Theory]
    [InlineData(9U, true, true)]
    [InlineData(15U, true, true)]
    [InlineData(16U, true, true)]
    [InlineData(20U, true, true)]
    [InlineData(21U, true, true)]
    [InlineData(27U, false, true)]
    [InlineData(28U, false, true)]
    [InlineData(51U, true, true)]
    [InlineData(56U, true, true)]
    [InlineData(58U, true, true)]
    [InlineData(61U, true, true)]
    [InlineData(63U, true, true)]
    [InlineData(65U, true, true)]
    [InlineData(72U, true, true)]
    [InlineData(208U, true, true)]
    [InlineData(247U, true, true)]
    [InlineData(253U, true, true)]
    [InlineData(257U, true, true)]
    [InlineData(323U, true, true)]
    [InlineData(403U, true, false)]
    [InlineData(24U, true, false)]
    [InlineData(450U, true, true)]
    public void AppliesEveryNativeCathedralSolCorrection(
        uint pieceId,
        bool blocksLight,
        bool blocksMissile)
    {
        var properties = CreateProperties((pieceId, AuthoritativeDunTileFlags.None));
        var pieces = new AuthoritativeDunPieceCatalog([
            new AuthoritativeDunPieceDefinition(0, pieceId, pieceId, pieceId, pieceId),
            new AuthoritativeDunPieceDefinition(21, 1, 1, 1, 1),
        ]);
        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            CreateResult(Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(), new ushort[80 * 80]),
            pieces,
            properties,
            isDefaultStart: false);

        var flags = geometry.Level.NativePieceProperties[checked((ushort)pieceId)];
        Assert.Equal(blocksLight, flags.HasFlag(AuthoritativeDunTileFlags.BlockLight));
        Assert.Equal(blocksMissile, flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile));
    }

    [Fact]
    public void RejectsInvalidShapesCoordinatesAndCrossCatalogReferences()
    {
        var valid = CreateResult(
            Enumerable.Repeat<ushort>(1, 40 * 40).ToArray(),
            new ushort[80 * 80]);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid with { MegaTiles = [1] },
                CreatePieces(),
                CreateProperties(),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid with { EntryPosition = new AuthoritativeCathedralPoint(112, 0) },
                CreatePieces(),
                CreateProperties(),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid with { MegaTiles = [.. valid.MegaTiles.Take(1599), (ushort)0] },
                CreatePieces(),
                CreateProperties(),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid,
                new AuthoritativeDunPieceCatalog([
                    new AuthoritativeDunPieceDefinition(0, 7, 7, 7, 7),
                ]),
                CreateProperties(),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid,
                CreatePieces(),
                new AuthoritativeDunTilePropertyCatalog([
                    new AuthoritativeDunTileProperty(1, AuthoritativeDunTileFlags.None),
                    new AuthoritativeDunTileProperty(2, AuthoritativeDunTileFlags.None),
                    new AuthoritativeDunTileProperty(3, AuthoritativeDunTileFlags.None),
                    new AuthoritativeDunTileProperty(4, AuthoritativeDunTileFlags.None),
                ]),
                isDefaultStart: false));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
                1,
                valid,
                CreatePieces(),
                new AuthoritativeDunTilePropertyCatalog([
                    new AuthoritativeDunTileProperty(1, AuthoritativeDunTileFlags.None),
                ]),
                isDefaultStart: false));
    }

    [Fact]
    public void MaterializationCopiesInputLayers()
    {
        var megaTiles = Enumerable.Repeat<ushort>(1, 40 * 40).ToArray();
        var transparency = new ushort[80 * 80];
        transparency[0] = 7;
        var result = CreateResult(megaTiles, transparency, entry: new AuthoritativeCathedralPoint(20, 20));
        var geometry = AuthoritativeCathedralLevel1GeometryMaterializer.Materialize(
            1,
            result,
            CreatePieces(),
            CreateProperties(),
            isDefaultStart: false);

        var level = geometry.Level;
        megaTiles[0] = 2;
        transparency[0] = 99;
        Assert.Equal((ushort)1, geometry.GenerationResult.GetMegaTile(0, 0));
        Assert.Equal((ushort)7, level.NativeTransparency[16 * 112 + 16]);
    }

    private static AuthoritativeCathedralLevel1Result CreateResult(
        IReadOnlyList<ushort> megaTiles,
        IReadOnlyList<ushort> transparency,
        AuthoritativeCathedralPoint? entry = null)
        => new(
            1,
            1,
            1,
            megaTiles,
            transparency,
            new AuthoritativeCathedralPoint(1, 1),
            new AuthoritativeCathedralPoint(2, 2),
            entry ?? new AuthoritativeCathedralPoint(20, 20));

    private static AuthoritativeDunPieceCatalog CreatePieces()
        => new([
            new AuthoritativeDunPieceDefinition(0, 11, 12, 13, 14),
            new AuthoritativeDunPieceDefinition(1, 31, 32, 33, 34),
            new AuthoritativeDunPieceDefinition(21, 1, 2, 3, 4),
        ]);

    private static AuthoritativeDunTilePropertyCatalog CreateProperties(
        params (uint PieceId, AuthoritativeDunTileFlags Flags)[] overrides)
    {
        var flags = new Dictionary<uint, AuthoritativeDunTileFlags> {
            [1] = AuthoritativeDunTileFlags.None,
            [2] = AuthoritativeDunTileFlags.None,
            [3] = AuthoritativeDunTileFlags.None,
            [4] = AuthoritativeDunTileFlags.None,
            [11] = AuthoritativeDunTileFlags.Solid,
            [12] = AuthoritativeDunTileFlags.BlockMissile,
            [13] = AuthoritativeDunTileFlags.BlockLight,
            [14] = AuthoritativeDunTileFlags.Trap,
            [31] = AuthoritativeDunTileFlags.Solid,
            [32] = AuthoritativeDunTileFlags.BlockMissile,
            [33] = AuthoritativeDunTileFlags.BlockLight,
            [34] = AuthoritativeDunTileFlags.Trap,
        };
        foreach (var (pieceId, pieceFlags) in overrides)
            flags[pieceId] = pieceFlags;
        return new AuthoritativeDunTilePropertyCatalog(
            flags.Select(pair => new AuthoritativeDunTileProperty(pair.Key, pair.Value)));
    }
}
