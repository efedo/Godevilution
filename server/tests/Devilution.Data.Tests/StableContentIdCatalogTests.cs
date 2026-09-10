using Devilution.Data;
using Xunit;

namespace Devilution.Data.Tests;

public sealed class StableContentIdCatalogTests
{
    [Fact]
    public void AcceptsCompleteProductionSchema()
    {
        var table = TsvTable.Parse(
            "content_ids.tsv",
            "kind\tsymbol\tnumeric_id\nmonster\tzombie\t1\n");

        StableContentIdCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void RejectsPartialProductionSchema()
    {
        var table = TsvTable.Parse(
            "content_ids.tsv",
            "numeric_id\tsymbol\n1\tzombie\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            StableContentIdCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvesSymbolsAndReverseIdsDeterministically()
    {
        var catalog = new StableContentIdCatalog([
            new StableContentId("spell", "haste", 2),
            new StableContentId("spell", "healing", 1),
        ]);

        Assert.Equal(1U, catalog.Resolve("spell", "healing"));
        Assert.Equal("haste", catalog.Resolve(2, "spell"));
        Assert.Equal([1U, 2U], catalog.Entries.Select(entry => entry.NumericId));
    }

    [Fact]
    public void AllowsNativeZeroForTheClassNamespace()
    {
        var catalog = new StableContentIdCatalog([
            new StableContentId("class", "warrior", 0),
        ]);

        Assert.Equal(0U, catalog.Resolve("class", "warrior"));
        Assert.Equal("warrior", catalog.Resolve(0, "class"));
    }

    [Fact]
    public void AllowsNativeZeroForTheLevelNamespace()
    {
        var catalog = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
        ]);

        Assert.Equal(0U, catalog.Resolve("level", "town"));
        Assert.Equal("town", catalog.Resolve(0, "level"));
    }

    [Fact]
    public void AllowsNativeZeroForTheSetLevelNamespace()
    {
        var catalog = new StableContentIdCatalog([
            new StableContentId("set_level", "SL_NONE", 0),
        ]);

        Assert.Equal(0U, catalog.Resolve("set_level", "SL_NONE"));
        Assert.Equal("SL_NONE", catalog.Resolve(0, "set_level"));
    }

    [Fact]
    public void RejectsDuplicateSymbolAndNumericId()
    {
        Assert.Throws<InvalidDataException>(() => new StableContentIdCatalog([
            new StableContentId("spell", "healing", 1),
            new StableContentId("spell", "healing", 2),
        ]));
        Assert.Throws<InvalidDataException>(() => new StableContentIdCatalog([
            new StableContentId("spell", "healing", 1),
            new StableContentId("spell", "haste", 1),
        ]));
    }

    [Fact]
    public void ValidatesReferencesInAStableNamespace()
    {
        var catalog = new StableContentIdCatalog([
            new StableContentId("level", "town", 1),
            new StableContentId("level", "dungeon", 2),
        ]);

        catalog.ValidateReferences("level", id => id is 1 or 2);
    }

    [Fact]
    public void RejectsUnknownReferencesInAStableNamespace()
    {
        var catalog = new StableContentIdCatalog([
            new StableContentId("portal", "cathedral_entrance", 1),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            catalog.ValidateReferences("portal", _ => false));
        Assert.Contains("Stable content portal symbol 'cathedral_entrance' references unknown ID 1", exception.Message, StringComparison.Ordinal);
    }
}
