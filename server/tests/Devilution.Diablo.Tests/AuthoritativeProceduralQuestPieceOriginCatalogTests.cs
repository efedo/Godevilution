using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeProceduralQuestPieceOriginCatalogTests
{
    [Fact]
    public void LoadTsvResolvesIndependentOriginAndLevelSymbols()
    {
        var stableIds = StableContentIdCatalog.LoadTsv(
            "content_ids.tsv",
            "kind\tsymbol\tnumeric_id\n"
                + "procedural_origin\thalls_book\t2201\n"
                + "level\tcathedral\t17\n");
        var catalog = AuthoritativeProceduralQuestPieceOriginCatalog.LoadTsv(
            "procedural_quest_piece_origins.tsv",
            "origin_id\torigin_symbol\tlevel_id\tlevel_symbol\tfootprint_width\tfootprint_height\tselection_seed\n"
                + "2201\thalls_book\t17\tcathedral\t4\t4\t1234\n",
            stableIds);

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal((2201U, 17U, "halls_book", "cathedral"),
            (definition.OriginId, definition.LevelId, definition.OriginSymbol, definition.LevelSymbol));
    }

    [Fact]
    public void RejectsDuplicateOriginIdentities()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeProceduralQuestPieceOriginCatalog([
                new AuthoritativeProceduralQuestPieceOriginDefinition(2201, 17, 4, 4, 1),
                new AuthoritativeProceduralQuestPieceOriginDefinition(2201, 17, 4, 4, 2),
            ]));
    }
}
