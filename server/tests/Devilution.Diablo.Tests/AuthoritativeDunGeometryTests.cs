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
        Assert.Equal(
            AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile,
            level.NativePieceProperties[12]);
        Assert.Equal([10, 11, 12, 13], level.NativePieceLayer);
        Assert.Equal(new ushort[] { 0, 0, 0, 0 }, level.NativeMonsterLayer);
        Assert.Equal([0, 2], level.NativePieceCollisionCells);
        Assert.Equal([1, 2], level.NativePieceMissileBlockedCells);
        Assert.Equal("SL_SYNTHETIC", level.StableSymbol);
    }

    [Fact]
    public void MaterializerPreservesNativeTransparencyLayer()
    {
        var asset = AuthoritativeDunMapAsset.Load(
            "synthetic.dun",
            CreateDun(1, 1, [7], transparencyLayer: [13, 14, 15, 16]));
        var pieces = new AuthoritativeDunPieceCatalog([
            new(7, 10, 11, 12, 13),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(10, AuthoritativeDunTileFlags.None),
            new(11, AuthoritativeDunTileFlags.None),
            new(12, AuthoritativeDunTileFlags.None),
            new(13, AuthoritativeDunTileFlags.None),
        ]);

        var level = AuthoritativeDunGeometryMaterializer.Materialize(
            101,
            asset,
            pieces,
            properties);

        Assert.Equal(new ushort[] { 13, 14, 15, 16 }, level.NativeTransparency);
    }

    [Fact]
    public void MaterializerPreservesNativeSignedTransparencyWireBytes()
    {
        var asset = AuthoritativeDunMapAsset.Load(
            "signed-transparency.dun",
            CreateDun(1, 1, [7], transparencyLayer: [0xFFFF, 0xFF80, 0x0080, 0x0100]));
        var pieces = new AuthoritativeDunPieceCatalog([
            new(7, 10, 11, 12, 13),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(10, AuthoritativeDunTileFlags.None),
            new(11, AuthoritativeDunTileFlags.None),
            new(12, AuthoritativeDunTileFlags.None),
            new(13, AuthoritativeDunTileFlags.None),
        ]);

        var level = AuthoritativeDunGeometryMaterializer.Materialize(
            101,
            asset,
            pieces,
            properties);

        Assert.Equal(new ushort[] { 255, 128, 128, 0 }, level.NativeTransparency);
    }

    [Fact]
    public void MaterializerPreservesNativeMonsterLayer()
    {
        var asset = AuthoritativeDunMapAsset.Load(
            "synthetic.dun",
            CreateDun(1, 1, [7], monsterLayer: [1, 0, 155, 2]));
        var pieces = new AuthoritativeDunPieceCatalog([
            new(7, 10, 11, 12, 13),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(10, AuthoritativeDunTileFlags.None),
            new(11, AuthoritativeDunTileFlags.None),
            new(12, AuthoritativeDunTileFlags.None),
            new(13, AuthoritativeDunTileFlags.None),
        ]);

        var level = AuthoritativeDunGeometryMaterializer.Materialize(
            101,
            asset,
            pieces,
            properties);

        Assert.Equal(new ushort[] { 1, 0, 155, 2 }, level.NativeMonsterLayer);
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
    public void TilePropertyTsvLoaderPreservesAllNativeSolFlags()
    {
        var catalog = AuthoritativeDunTilePropertyCatalog.LoadTsv(
            "dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n"
                + "10\t1\ttrue\t0\tfalse\t1\tfalse\ttrue\n");

        Assert.Equal(
            AuthoritativeDunTileFlags.Solid
                | AuthoritativeDunTileFlags.BlockLight
                | AuthoritativeDunTileFlags.TransparentLeft
                | AuthoritativeDunTileFlags.Trap,
            Assert.Single(catalog.Definitions).Flags);
        Assert.Throws<InvalidDataException>(() => AuthoritativeDunTilePropertyCatalog.LoadTsv(
            "dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_missile\ttrap\n10\tfalse\tfalse\tmaybe\n"));
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
    public void NativeDunCatalogsEnforceMaxTilesPieceRange()
    {
        var maximumPieceId = (uint)AuthoritativeDunTilePropertyCatalog.NativeTileCount - 1;
        var pieces = new AuthoritativeDunPieceCatalog([
            new(0, maximumPieceId, maximumPieceId, maximumPieceId, maximumPieceId),
        ]);
        var properties = new AuthoritativeDunTilePropertyCatalog([
            new(maximumPieceId, AuthoritativeDunTileFlags.None),
        ]);

        Assert.Equal(maximumPieceId, pieces.Definitions[0].NorthWestPieceId);
        Assert.Equal(maximumPieceId, properties.Definitions[0].PieceId);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeDunPieceCatalog([
            new(0, (uint)AuthoritativeDunTilePropertyCatalog.NativeTileCount, 0, 0, 0),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeDunTilePropertyCatalog([
            new((uint)AuthoritativeDunTilePropertyCatalog.NativeTileCount, AuthoritativeDunTileFlags.None),
        ]));
        Assert.Throws<InvalidDataException>(() => AuthoritativeDunPieceCatalog.LoadTsv(
            "dun_pieces.tsv",
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "0\t1379\t0\t0\t0\n"));
        Assert.Throws<InvalidDataException>(() => AuthoritativeDunTilePropertyCatalog.LoadTsv(
            "dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_missile\n1379\tfalse\tfalse\n"));
    }

    [Fact]
    public void TilLoaderRejectsPieceOutsideMaxTilesRange()
    {
        var bytes = new byte[sizeof(ushort) * 4];
        WriteWord(bytes, 0, AuthoritativeDunTilePropertyCatalog.NativeTileCount);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeDunPieceCatalog.LoadTil("out-of-range.til", bytes));
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
    public void NativeBundleLoaderPreservesObjectLayerForExplicitMappings()
    {
        var dun = CreateDun(1, 1, [0], objectLayer: [109, 0, 0, 19]);
        var til = new byte[sizeof(ushort) * 4];
        var sol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];

        var level = AuthoritativeNativeDunGeometryLoader.Load(
            202,
            "synthetic.dun",
            dun,
            "l1.til",
            til,
            "l1.sol",
            sol);

        Assert.Equal([109, 0, 0, 19], level.NativeObjectLayer);
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
            new AuthoritativeSetLevelMapDefinition(2, "levels/pre.dun", "levels/active.dun", "Catacombs", 1, 1) {
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

    [Fact]
    public void SetLevelCatalogPreflightsPartiallySuppliedNativeBundles()
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

        var exception = Assert.Throws<InvalidDataException>(() => catalog.ValidateAssets(content));

        Assert.Contains("incomplete native asset bundle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetLevelCatalogPreflightsCompleteBundlesWithoutWorldActivation()
    {
        var til = new byte[sizeof(ushort) * 4];
        var sol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        var catalog = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(1, "pre.dun", "active.dun", "Cathedral", 1, 1) {
                TileAssetPath = "map.til",
                SolAssetPath = "map.sol",
            },
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("pre.dun", CreateDun(1, 1, [43])),
                new ContentAsset("active.dun", CreateDun(1, 1, [43])),
                new ContentAsset("map.til", til),
                new ContentAsset("map.sol", sol),
            ]),
        ]));

        var exception = Assert.Throws<InvalidDataException>(() => catalog.ValidateAssets(content));

        Assert.Contains("unknown mega-tile 43", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetLevelCatalogRejectsBlockedActiveMapSpawnPoint()
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
                new ContentAsset("pre.dun", dun),
                new ContentAsset("active.dun", dun),
                new ContentAsset("map.til", til),
                new ContentAsset("map.sol", sol),
            ]),
        ]));
        var catalog = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(1, "pre.dun", "active.dun", "Cathedral", 0, 0) {
                TileAssetPath = "map.til",
                SolAssetPath = "map.sol",
            },
        ]);

        var exception = Assert.Throws<InvalidDataException>(() => catalog.LoadAvailableGeometry(content));
        Assert.Contains("spawn point", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetLevelCatalogValidatesOptionalTransparencyDimensions()
    {
        var dun = CreateDun(1, 1, [0]);
        var til = new byte[sizeof(ushort) * 4];
        WriteWord(til, 0, 20);
        WriteWord(til, 2, 21);
        WriteWord(til, 4, 22);
        WriteWord(til, 6, 23);
        var sol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("pre.dun", dun),
                new ContentAsset("active.dun", dun),
                new ContentAsset("map.til", til),
                new ContentAsset("map.sol", sol),
                new ContentAsset("transparency.dun", dun),
                new ContentAsset("transparency-wide.dun", CreateDun(2, 1, [0, 0])),
            ]),
        ]));
        var valid = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(1, "pre.dun", "active.dun", "Cathedral", 1, 1) {
                TileAssetPath = "map.til",
                SolAssetPath = "map.sol",
                TransparencyAssetPath = "transparency.dun",
            },
        ]);

        var levels = valid.LoadAvailableGeometry(content);
        Assert.Equal(2, levels.Count);
        var active = Assert.Single(
            levels,
            level => level.LevelId
                == AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true));
        Assert.Equal(4, active.NativeTransparency.Count);
        Assert.All(active.NativeTransparency, value => Assert.Equal((ushort)0, value));

        var invalid = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(1, "pre.dun", "active.dun", "Cathedral", 1, 1) {
                TileAssetPath = "map.til",
                SolAssetPath = "map.sol",
                TransparencyAssetPath = "transparency-wide.dun",
            },
        ]);
        var exception = Assert.Throws<InvalidDataException>(
            () => invalid.LoadAvailableGeometry(content));
        Assert.Contains("transparency dimensions", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateDun(
        int megaWidth,
        int megaHeight,
        ushort[] tiles,
        ushort[]? objectLayer = null,
        ushort[]? transparencyLayer = null,
        ushort[]? monsterLayer = null)
    {
        var megaCount = megaWidth * megaHeight;
        var dPieceCount = megaCount * 4;
        var words = new ushort[2 + megaCount + dPieceCount * 4];
        words[0] = (ushort)megaWidth;
        words[1] = (ushort)megaHeight;
        Array.Copy(tiles, 0, words, 2, tiles.Length);
        if (objectLayer is not null)
            Array.Copy(objectLayer, 0, words, 2 + megaCount + dPieceCount * 2, objectLayer.Length);
        if (transparencyLayer is not null)
            Array.Copy(transparencyLayer, 0, words, 2 + megaCount + dPieceCount * 3, transparencyLayer.Length);
        if (monsterLayer is not null)
            Array.Copy(monsterLayer, 0, words, 2 + megaCount + dPieceCount, monsterLayer.Length);
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
