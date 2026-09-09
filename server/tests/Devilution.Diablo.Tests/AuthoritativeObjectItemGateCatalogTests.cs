using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeObjectItemGateCatalogTests
{
    [Fact]
    public void LoadsStableItemRequirementsAndNormalizesByEntity()
    {
        var catalog = AuthoritativeObjectItemGateCatalog.LoadTsv(
            "object_item_gates.tsv",
            "entity_id\titem_id\titem_symbol\tconsume_item\n"
                + "2024\t2143\tblood_stone\ttrue\n"
                + "2023\t2143\tblood_stone\tfalse\n");

        Assert.Equal([2023U, 2024U], catalog.Definitions.Select(definition => definition.EntityId));
        Assert.False(catalog.Definitions[0].ConsumeItem);
        Assert.True(catalog.Definitions[1].ConsumeItem);
    }

    [Fact]
    public void RejectsDuplicateObjectEntities()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeObjectItemGateCatalog([
            new AuthoritativeObjectItemGateDefinition(2023, 2143),
            new AuthoritativeObjectItemGateDefinition(2023, 2143),
        ]));
    }
}
