using System.Buffers.Binary;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeDunGeometryTests
{
    [Fact]
    public void MaterializerExpandsMegaTilesUsingNativeCollisionFlags()
    {
        var asset = AuthoritativeDunMapAsset.Load("synthetic.dun", CreateDun(1, 1, [7]));
        var pieces = new AuthoritativeDunPieceCatalog([
            new(7, 10, 11, 12, 13),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(10, AuthoritativeDunTileFlags.Solid),
            new(11, AuthoritativeDunTileFlags.BlockMissile),
            new(12, AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile),
            new(13, AuthoritativeDunTileFlags.None),
        ]);

        var level = AuthoritativeDunGeometryMaterializer.Materialize(
            101,
            asset,
            pieces,
            properties,
            "SL_SYNTHETIC");

        Assert.Equal(2, level.Width);
        Assert.Equal(2, level.Height);
        Assert.Equal([0, 2], level.BlockedCells);
        Assert.Equal([1, 2], level.MissileBlockedCells);
        Assert.Equal("SL_SYNTHETIC", level.StableSymbol);
    }

    [Fact]
    public void MaterializerRejectsMissingNativeCollisionInputs()
    {
        var asset = AuthoritativeDunMapAsset.Load("synthetic.dun", CreateDun(1, 1, [7]));
        var pieces = new AuthoritativeDunPieceCatalog([new(7, 10, 11, 12, 13)]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(10, AuthoritativeDunTileFlags.None),
        ]);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunGeometryMaterializer.Materialize(101, asset, pieces, properties));
    }

    [Fact]
    public void CatalogLoadersUseStrictExternalBooleanAndPieceColumns()
    {
        var pieces = AuthoritativeDunPieceCatalog.LoadTsv(
            "dun_pieces.tsv",
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "7\t10\t11\t12\t13\n");
        var properties = AuthoritativeDunTilePropertyCatalog.LoadTsv(
            "dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_missile\n10\ttrue\tfalse\n");

        Assert.Equal(7U, Assert.Single(pieces.Definitions).MegaTileId);
        Assert.Equal(AuthoritativeDunTileFlags.Solid, Assert.Single(properties.Definitions).Flags);
        Assert.Throws<InvalidDataException>(() => AuthoritativeDunTilePropertyCatalog.LoadTsv(
            "dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_missile\n10\tmaybe\tfalse\n"));
    }

    [Fact]
    public void TilLoaderReadsNativeLittleEndianMegaTileRecords()
    {
        var bytes = new byte[16];
        WriteWord(bytes, 0, 10);
        WriteWord(bytes, 2, 11);
        WriteWord(bytes, 4, 12);
        WriteWord(bytes, 6, 13);
        WriteWord(bytes, 8, 40);
        WriteWord(bytes, 10, 41);
        WriteWord(bytes, 12, 42);
        WriteWord(bytes, 14, 43);

        var catalog = AuthoritativeDunPieceCatalog.LoadTil("levels/l1data/l1.til", bytes);

        Assert.Equal(
            new AuthoritativeDunPieceDefinition(0, 10, 11, 12, 13),
            catalog.Definitions[0]);
        Assert.Equal(
            new AuthoritativeDunPieceDefinition(1, 40, 41, 42, 43),
            catalog.Definitions[1]);
    }

    [Fact]
    public void TilLoaderRejectsEmptyAndMisalignedAssets()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunPieceCatalog.LoadTil("empty.til", Array.Empty<byte>()));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunPieceCatalog.LoadTil("truncated.til", new byte[7]));
    }

    [Fact]
    public void SolLoaderPreservesNativeFlagsForEveryDunPiece()
    {
        var bytes = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        bytes[10] = (byte)(AuthoritativeDunTileFlags.Solid
            | AuthoritativeDunTileFlags.BlockLight
            | AuthoritativeDunTileFlags.BlockMissile);
        bytes[11] = (byte)(AuthoritativeDunTileFlags.TransparentLeft
            | AuthoritativeDunTileFlags.TransparentRight
            | AuthoritativeDunTileFlags.Trap);

        var catalog = AuthoritativeDunTilePropertyCatalog.LoadSol("levels/l1data/l1.sol", bytes);

        Assert.Equal(AuthoritativeDunTilePropertyCatalog.NativeTileCount, catalog.Definitions.Count);
        Assert.Equal(
            AuthoritativeDunTileFlags.Solid
                | AuthoritativeDunTileFlags.BlockLight
                | AuthoritativeDunTileFlags.BlockMissile,
            catalog.Definitions[10].Flags);
        Assert.Equal(
            AuthoritativeDunTileFlags.TransparentLeft
                | AuthoritativeDunTileFlags.TransparentRight
                | AuthoritativeDunTileFlags.Trap,
            catalog.Definitions[11].Flags);
        Assert.Equal(AuthoritativeDunTileFlags.None, catalog.Definitions[^1].Flags);
    }

    [Fact]
    public void SolLoaderRejectsWrongLengthAndUnsupportedFlags()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunTilePropertyCatalog.LoadSol(
                "short.sol",
                new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount - 1]));

        var bytes = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        bytes[12] = 0x40;
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunTilePropertyCatalog.LoadSol("unsupported.sol", bytes));
    }

    [Fact]
    public void NativeBundleLoaderComposesDunTilAndSolIntoAuthoritativeGeometry()
    {
        var dun = CreateDun(1, 1, [0]);
        var til = new byte[sizeof(ushort) * 4];
        WriteWord(til, 0, 20);
        WriteWord(til, 2, 21);
        WriteWord(til, 4, 22);
        WriteWord(til, 6, 23);
        var sol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        sol[20] = (byte)AuthoritativeDunTileFlags.Solid;
        sol[21] = (byte)AuthoritativeDunTileFlags.BlockMissile;
        sol[22] = (byte)(AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile);

        var level = AuthoritativeNativeDunGeometryLoader.Load(
            202,
            "levels/l1data/synthetic.dun",
            dun,
            "levels/l1data/l1.til",
            til,
            "levels/l1data/l1.sol",
            sol,
            "SL_SYNTHETIC");

        Assert.Equal([0, 2], level.BlockedCells);
        Assert.Equal([1, 2], level.MissileBlockedCells);
        Assert.Equal("SL_SYNTHETIC", level.StableSymbol);
    }

    [Fact]
    public void NativeBundleLoaderRejectsMissingMegaTileRecords()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunGeometryLoader.Load(
                202,
                "synthetic.dun",
                CreateDun(1, 1, [1]),
                "l1.til",
                new byte[sizeof(ushort) * 4],
                "l1.sol",
                new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount]));
    }

    [Fact]
    public void NativeBundleLoaderReadsSetLevelAssetsFromComposedContent()
    {
        var dun = CreateDun(1, 1, [0]);
        var til = new byte[sizeof(ushort) * 4];
        WriteWord(til, 0, 20);
        WriteWord(til, 2, 21);
        WriteWord(til, 4, 22);
        WriteWord(til, 6, 23);
        var sol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        sol[20] = (byte)AuthoritativeDunTileFlags.Solid;
        sol[21] = (byte)AuthoritativeDunTileFlags.BlockMissile;
        sol[22] = (byte)(AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/l1data/pre.dun", dun),
                new ContentAsset("levels/l1data/active.dun", dun),
                new ContentAsset("levels/l1data/l1.til", til),
                new ContentAsset("levels/l1data/l1.sol", sol),
            ]),
        ]));
        var definition = new AuthoritativeSetLevelMapDefinition(
            1,
            "levels/l1data/pre.dun",
            "levels/l1data/active.dun",
            "Cathedral",
            8,
            8) {
            SetLevelSymbol = "SL_SYNTHETIC",
            TileAssetPath = "levels/l1data/l1.til",
            SolAssetPath = "levels/l1data/l1.sol",
        };

        var level = AuthoritativeNativeDunGeometryLoader.Load(content, definition, 202, activeMap: true);

        Assert.Equal([0, 2], level.BlockedCells);
        Assert.Equal([1, 2], level.MissileBlockedCells);
        Assert.Equal("SL_SYNTHETIC", level.StableSymbol);
    }

    [Fact]
    public void SetLevelCatalogPublishesCompleteBundlesIntoReservedRuntimeLevels()
    {
        var dun = CreateDun(1, 1, [0]);
        var til = new byte[sizeof(ushort) * 4];
        WriteWord(til, 0, 20);
        WriteWord(til, 2, 21);
        WriteWord(til, 4, 22);
        WriteWord(til, 6, 23);
        var sol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        sol[20] = (byte)AuthoritativeDunTileFlags.Solid;
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/pre.dun", dun),
                new ContentAsset("levels/active.dun", dun),
                new ContentAsset("levels/map.til", til),
                new ContentAsset("levels/map.sol", sol),
            ]),
        ]));
        var catalog = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(2, "levels/pre.dun", "levels/active.dun", "Catacombs", 4, 5) {
                SetLevelSymbol = "SL_BONECHAMB",
                TileAssetPath = "levels/map.til",
                SolAssetPath = "levels/map.sol",
            },
        ]);

        var levels = catalog.LoadAvailableGeometry(content);

        Assert.Equal(2, levels.Count);
        Assert.Equal(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(2, activeMap: false),
            levels[0].LevelId);
        Assert.Equal(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(2, activeMap: true),
            levels[1].LevelId);
        Assert.All(levels, level => Assert.Equal("SL_BONECHAMB", level.StableSymbol));
    }

    [Fact]
    public void SetLevelCatalogRejectsPartiallySuppliedNativeBundles()
    {
        var catalog = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(1, "pre.dun", "active.dun", "Cathedral", 1, 1) {
                TileAssetPath = "map.til",
                SolAssetPath = "map.sol",
            },
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("pre.dun", new byte[] { 1 }),
            ]),
        ]));

        Assert.Throws<InvalidDataException>(() => catalog.LoadAvailableGeometry(content));
    }

    private static byte[] CreateDun(int megaWidth, int megaHeight, ushort[] tiles)
    {
        var megaCount = megaWidth * megaHeight;
        var dPieceCount = megaCount * 4;
        var words = new ushort[2 + megaCount + dPieceCount * 4];
        words[0] = (ushort)megaWidth;
        words[1] = (ushort)megaHeight;
        Array.Copy(tiles, 0, words, 2, tiles.Length);
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        return bytes;
    }

    private static void WriteWord(byte[] bytes, int offset, ushort value)
        => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
}
