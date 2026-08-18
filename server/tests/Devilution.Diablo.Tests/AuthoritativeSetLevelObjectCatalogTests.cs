using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeSetLevelObjectCatalogTests
{
    [Fact]
    public void TsvLoaderResolvesStableSetLevelIdentityAndOrdersPlacements()
    {
        var catalog = AuthoritativeSetLevelObjectCatalog.LoadTsv(
            "set_level_objects.tsv",
            "placement_id\tset_level_id\tset_level_symbol\tobject_id\tposition_x\tposition_y\ttemplate_x\ttemplate_y\ttemplate_width\ttemplate_height\n"
                + "1002\t1\tSL_SKELKING\t2\t64\t59\t20\t14\t1\t2\n"
                + "1001\t1\tSL_SKELKING\t1\t64\t34\t20\t7\t3\t3\n");

        Assert.Equal([1001U, 1002U], catalog.Definitions.Select(placement => placement.PlacementId));
        var gate = Assert.Single(catalog.GetForSetLevel(1), placement => placement.ObjectId == 2);
        Assert.Equal("SL_SKELKING", gate.SetLevelSymbol);
        Assert.Equal((64, 59, 20, 14, 1, 2), (
            gate.PositionX,
            gate.PositionY,
            gate.TemplateX,
            gate.TemplateY,
            gate.TemplateWidth,
            gate.TemplateHeight));
    }

    [Fact]
    public void ValidationRejectsDuplicatePlacementIdentity()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelObjectCatalog([
            new(1001, 1, 1, 10, 10, 0, 0, 1, 1),
            new(1001, 1, 2, 10, 10, 0, 0, 1, 1),
        ]));
    }

}
