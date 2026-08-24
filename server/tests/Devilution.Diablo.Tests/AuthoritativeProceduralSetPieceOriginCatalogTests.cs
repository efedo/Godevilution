using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeProceduralSetPieceOriginCatalogTests
{
    [Fact]
    public void PlansAnAlignedWalkableOriginDeterministically()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 24, 24));
        var catalog = new AuthoritativeProceduralSetPieceOriginCatalog([
            new AuthoritativeProceduralSetPieceOriginDefinition(6, 17, 4, 6, 1234),
        ]);

        var first = catalog.Plan(world, 99);
        var second = catalog.Plan(world, 99);
        var planned = Assert.Single(first);
        var origin = planned.Value;

        Assert.Equal(first, second);
        Assert.Equal(6, planned.Key);
        Assert.Equal(0, origin.PositionX % 2);
        Assert.Equal(0, origin.PositionY % 2);
        for (var y = origin.PositionY; y < origin.PositionY + 6; y++)
            for (var x = origin.PositionX; x < origin.PositionX + 4; x++)
                Assert.True(world.IsWalkable(17, x, y));
    }

    [Fact]
    public void DefersRulesForUnavailableLevels()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(18, 24, 24));
        var catalog = new AuthoritativeProceduralSetPieceOriginCatalog([
            new AuthoritativeProceduralSetPieceOriginDefinition(6, 17, 4, 4, 1),
        ]);

        Assert.Empty(catalog.Plan(world));
    }

    [Fact]
    public void RejectsRulesWithNoWalkableFootprint()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 8, 8, blockedCells: Enumerable.Range(0, 64)));
        var catalog = new AuthoritativeProceduralSetPieceOriginCatalog([
            new AuthoritativeProceduralSetPieceOriginDefinition(6, 17, 2, 2, 1),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.Plan(world));
    }

    [Fact]
    public void LoadsNumericAndSymbolicMetadataWithoutChangingThePlannerContract()
    {
        const string tsv = "set_level_id\tset_level_symbol\tlevel_id\tlevel_symbol\tfootprint_width\tfootprint_height\tselection_seed\talignment\n"
            + "6\tSL_ARENA_CHURCH\t17\thellfire_level_17\t4\t4\t77\t2\n";

        var catalog = AuthoritativeProceduralSetPieceOriginCatalog.LoadTsv(
            "procedural_set_piece_origins.tsv",
            tsv);

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(6, definition.SetLevelId);
        Assert.Equal(17U, definition.LevelId);
        Assert.Equal("SL_ARENA_CHURCH", definition.SetLevelSymbol);
        Assert.Equal("hellfire_level_17", definition.LevelSymbol);
    }
}
