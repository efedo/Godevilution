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
    public void AppliesObjectPositionsRelativeToAUniqueEncounterAnchor()
    {
        var catalog = AuthoritativeNativeDunFragmentObjectCatalog.LoadTsv(
            "native_dun_fragment_objects.tsv",
            "placement_id\tfragment_id\tentity_id\tanchor_entity_id\toffset_x\toffset_y\tanchor_unique_identity\n"
                + "1\t7\t10\t0\t-2\t3\tSnotspill\n");
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                7,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Snotspill"),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 12));
        var target = new AuthoritativeWorldObject(10, 6001, 24, 0, 0);
        var anchor = new AuthoritativeCombatTarget(
            11,
            4,
            5,
            10,
            levelId: 24,
            unique: true,
            uniqueIdentity: "Snotspill");

        catalog.Apply(world, [target], fragments, [anchor]);

        Assert.Equal((2, 8), (target.PositionX, target.PositionY));
    }

    [Fact]
    public void MaterializesAUniqueAnchoredBehaviorTemplate()
    {
        var catalog = AuthoritativeNativeDunFragmentObjectCatalog.LoadTsv(
            "native_dun_fragment_objects.tsv",
            "placement_id\tfragment_id\tentity_id\tanchor_entity_id\toffset_x\toffset_y\tanchor_unique_identity\ttemplate_entity_id\n"
                + "1\t7\t10\t0\t-2\t3\tSnotspill\t100\n");
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                7,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Snotspill"),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 12));
        var template = new AuthoritativeWorldObject(100, 97, 0, 0, 0);
        var anchor = new AuthoritativeCombatTarget(
            11,
            4,
            5,
            10,
            levelId: 24,
            unique: true,
            uniqueIdentity: "Snotspill");

        var generated = catalog.Materialize(world, [], [template], fragments, [anchor]);

        var result = Assert.Single(generated);
        Assert.Equal((10U, 97U, 24U, 2, 8), (
            result.EntityId,
            result.ObjectId,
            result.LevelId,
            result.PositionX,
            result.PositionY));
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

    [Fact]
    public void RejectsMissingBehaviorTemplatesBeforeMaterialization()
    {
        var catalog = new AuthoritativeNativeDunFragmentObjectCatalog([
            new AuthoritativeNativeDunFragmentObjectPlacement(1, 7, 10, 9, -2, 3) {
                TemplateEntityId = 100,
            },
        ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateTemplates([]));

        Assert.Contains("unknown template entity 100", exception.Message, StringComparison.Ordinal);
    }
}
