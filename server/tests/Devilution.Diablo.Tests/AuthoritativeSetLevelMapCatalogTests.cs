using Devilution.Diablo;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeSetLevelMapCatalogTests
{
    [Fact]
    public void TsvLoaderResolvesStableIdentityAndPreservesNativeEntryPoint()
    {
        var catalog = AuthoritativeSetLevelMapCatalog.LoadTsv(
            "set_level_maps.tsv",
            "set_level_id\tset_level_symbol\tpre_map_asset\tactive_map_asset\ttil_asset\tsol_asset\ttransparency_asset\tdungeon_type\tspawn_x\tspawn_y\n"
                + "5\tSL_VILEBETRAYER\tlevels/l1data/vile1.dun\tlevels/l1data/vile2.dun\tlevels/l1data/l1.til\tlevels/l1data/l1.sol\tlevels/l1data/vile1t.dun\tCathedral\t35\t36\n");

        var map = Assert.Single(catalog.Definitions);
        Assert.Equal(5, map.SetLevelId);
        Assert.Equal("SL_VILEBETRAYER", map.SetLevelSymbol);
        Assert.Equal("levels/l1data/vile1.dun", map.PreMapAssetPath);
        Assert.Equal("levels/l1data/vile2.dun", map.ActiveMapAssetPath);
        Assert.Equal("levels/l1data/l1.til", map.TileAssetPath);
        Assert.Equal("levels/l1data/l1.sol", map.SolAssetPath);
        Assert.Equal("levels/l1data/vile1t.dun", map.TransparencyAssetPath);
        Assert.Equal((35, 36), (map.SpawnX, map.SpawnY));
    }

    [Fact]
    public void ValidationRejectsTraversalAndDuplicateMapDefinitions()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelMapCatalog([
            new(1, "levels/l1data/../bad.dun", "levels/l1data/sklkng2.dun", "Cathedral", 1, 1),
        ]));

        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelMapCatalog([
            new(1, "levels/l1data/sklkng1.dun", "levels/l1data/sklkng2.dun", "Cathedral", 1, 1),
            new(1, "levels/l1data/sklkng1.dun", "levels/l1data/sklkng2.dun", "Cathedral", 2, 2),
        ]));

        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelMapCatalog([
            new(1, "levels/l1data/sklkng1.dun", "levels/l1data/sklkng2.dun", "Cathedral", 1, 1) {
                TileAssetPath = "levels/l1data/../bad.til",
            },
        ]));

        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelMapCatalog([
            new(1, "levels/l1data/sklkng1.dun", "levels/l1data/sklkng2.dun", "Cathedral", 1, 1) {
                TileAssetPath = "levels/l1data/l1.til",
            },
        ]));

        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelMapCatalog([
            new(1, "levels/l1data/sklkng1.dun", "levels/l1data/sklkng2.dun", "Cathedral", 1, 1) {
                SolAssetPath = "levels/l1data/l1.bin",
            },
        ]));

        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelMapCatalog([
            new(1, "levels/l1data/sklkng1.dun", "levels/l1data/sklkng2.dun", "Cathedral", 1, 1) {
                TransparencyAssetPath = "levels/l1data/../bad.dun",
            },
        ]));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void ActiveArenaRuntimeLevelsAreRecognized(int setLevelId)
    {
        Assert.True(AuthoritativeSetLevelMapCatalog.IsArenaRuntimeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(setLevelId, activeMap: true)));
        Assert.False(AuthoritativeSetLevelMapCatalog.IsArenaRuntimeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(setLevelId, activeMap: false)));
    }

    [Fact]
    public void NonArenaRuntimeLevelsAreNotRecognized()
    {
        Assert.False(AuthoritativeSetLevelMapCatalog.IsArenaRuntimeLevel(0));
        Assert.False(AuthoritativeSetLevelMapCatalog.IsArenaRuntimeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(5, activeMap: true)));
    }

    [Fact]
    public void DeclaredDunAssetWithoutItsPairIsRejectedEvenWhenTileAssetsAreOmitted()
    {
        var catalog = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(
                1,
                "levels/pre.dun",
                "levels/active.dun",
                "Cathedral",
                1,
                1),
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/pre.dun", new byte[] { 1 }),
            ]),
        ]));

        var exception = Assert.Throws<InvalidDataException>(() => catalog.ValidateAssets(content));

        Assert.Contains("incomplete native asset bundle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparencyMayBeShippedBeforeDeferredMapGeometry()
    {
        var catalog = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(
                1,
                "levels/l1data/sklkng1.dun",
                "levels/l1data/sklkng2.dun",
                "Cathedral",
                83,
                44) {
                TileAssetPath = "levels/l1data/l1.til",
                SolAssetPath = "levels/l1data/l1.sol",
                TransparencyAssetPath = "levels/l1data/sklkngt.dun",
            },
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/l1data/sklkngt.dun", MinimalDun(1)),
            ]),
        ]));

        catalog.ValidateAssets(content);

        Assert.Empty(catalog.LoadAvailableGeometry(content));
    }

    [Fact]
    public void SharedExternalGeometryMaterializesSetLevelMapsWithoutBinaryPairs()
    {
        var catalog = new AuthoritativeSetLevelMapCatalog([
            new AuthoritativeSetLevelMapDefinition(
                1,
                "levels/pre.dun",
                "levels/active.dun",
                "Cathedral",
                0,
                0),
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("levels/pre.dun", MinimalDun(0)),
                new ContentAsset("levels/active.dun", MinimalDun(0)),
            ]),
        ]));
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            "native_dun_piece_definitions.tsv",
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "0\t1\t1\t1\t1\n",
            "native_dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_light\tblock_missile\n"
                + "1\tfalse\tfalse\tfalse\n");

        catalog.ValidateAssets(content, geometry);
        var levels = catalog.LoadAvailableGeometry(content, geometry);

        Assert.Equal(2, levels.Count);
        Assert.All(levels, level => {
            Assert.Equal(2, level.Width);
            Assert.Equal(2, level.Height);
            Assert.Equal([1, 1, 1, 1], level.NativePieceLayer);
        });
    }

    private static byte[] MinimalDun(ushort transparencyValue)
    {
        // A 1x1 mega-tile DUN has two header words, one tile word, and four
        // four-cell d-piece layers (reserved, monster, object, transparency).
        var words = new ushort[19];
        words[0] = 1;
        words[1] = 1;
        words[^1] = transparencyValue;
        var bytes = new byte[words.Length * sizeof(ushort)];
        Buffer.BlockCopy(words, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
