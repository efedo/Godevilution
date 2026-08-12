using Devilution.Data;
using Xunit;

namespace Devilution.Data.Tests;

public sealed class StableContentIdCatalogTests
{
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
}
