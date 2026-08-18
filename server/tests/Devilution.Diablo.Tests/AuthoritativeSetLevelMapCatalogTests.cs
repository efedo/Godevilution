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
            "set_level_id\tset_level_symbol\tpre_map_asset\tactive_map_asset\tdungeon_type\tspawn_x\tspawn_y\n"
                + "5\tSL_VILEBETRAYER\tlevels/l1data/vile1.dun\tlevels/l1data/vile2.dun\tCathedral\t35\t36\n");

        var map = Assert.Single(catalog.Definitions);
        Assert.Equal(5, map.SetLevelId);
        Assert.Equal("SL_VILEBETRAYER", map.SetLevelSymbol);
        Assert.Equal("levels/l1data/vile1.dun", map.PreMapAssetPath);
        Assert.Equal("levels/l1data/vile2.dun", map.ActiveMapAssetPath);
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
    }
}
