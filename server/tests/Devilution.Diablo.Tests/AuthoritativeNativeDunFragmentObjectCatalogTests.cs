using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunFragmentObjectCatalogTests
{
    [Fact]
    public void AppliesObjectPositionsRelativeToTheFragmentAnchor()
    {
        var catalog = AuthoritativeNativeDunFragmentObjectCatalog.LoadTsv(
            "native_dun_fragment_objects.tsv",
            "placement_id\tfragment_id\tentity_id\tanchor_entity_id\toffset_x\toffset_y\n"
                + "1\t7\t10\t9\t-2\t3\n");
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(7, 24, "fragment.dun", 9, -2, -2),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var anchor = new AuthoritativeWorldObject(9, 6003, 24, 4, 4);
        var target = new AuthoritativeWorldObject(10, 6001, 24, 0, 0);

        catalog.Apply(world, [anchor, target], fragments);

        Assert.Equal((2, 7), (target.PositionX, target.PositionY));
    }

    [Fact]
    public void RejectsPlacementsThatReferenceAnUnknownFragment()
    {
        var catalog = new AuthoritativeNativeDunFragmentObjectCatalog([
            new AuthoritativeNativeDunFragmentObjectPlacement(1, 7, 10, 9, -2, 3),
        ]);
        var fragments = new AuthoritativeNativeDunFragmentCatalog([]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(
            world,
            [
                new AuthoritativeWorldObject(9, 6003, 24, 4, 4),
                new AuthoritativeWorldObject(10, 6001, 24, 0, 0),
            ],
            fragments));
        Assert.Contains("unknown fragment 7", exception.Message, StringComparison.Ordinal);
    }
}
