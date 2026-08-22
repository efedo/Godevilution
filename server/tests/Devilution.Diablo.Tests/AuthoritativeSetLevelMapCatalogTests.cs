using Devilution.Diablo;
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
}
