using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeItemCursorCatalogTests
{
    [Fact]
    public void AcceptsCompleteProductionSchema()
    {
        var table = TsvTable.Parse(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nSHORT_SWORD\t64\n");

        AuthoritativeItemCursorCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void RejectsPartialProductionSchema()
    {
        var table = TsvTable.Parse(
            "item_cursor_graphics.tsv",
            "native_cursor_graphic\tsymbol\n64\tSHORT_SWORD\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCursorCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsCursorGraphicDefinitionsAfterValidation()
    {
        var catalog = AuthoritativeItemCursorCatalog.LoadTsv(
            "item_cursor_graphics.tsv",
            "symbol\tnative_cursor_graphic\nSHORT_SWORD\t64\n");

        Assert.Equal(64, catalog.Resolve("short_sword"));
    }
}
