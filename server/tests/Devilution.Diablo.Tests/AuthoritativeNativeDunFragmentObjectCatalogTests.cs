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
    public void InheritsAFragmentCoordinateAnchorForObjectPlacement()
    {
        var catalog = AuthoritativeNativeDunFragmentObjectCatalog.LoadTsv(
            "native_dun_fragment_objects.tsv",
            "placement_id\tfragment_id\tentity_id\tanchor_entity_id\toffset_x\toffset_y\n"
                + "1\t7\t10\t0\t-2\t3\n");
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                7,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorPositionX: 4,
                AnchorPositionY: 4),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var target = new AuthoritativeWorldObject(10, 6001, 24, 0, 0);

        catalog.Apply(world, [target], fragments);

        Assert.Equal((2, 7), (target.PositionX, target.PositionY));
    }

    [Fact]
    public void MaterializesASetLevelOriginAnchoredBehaviorTemplate()
    {
        var catalog = AuthoritativeNativeDunFragmentObjectCatalog.LoadTsv(
            "native_dun_fragment_objects.tsv",
            "placement_id\tfragment_id\tentity_id\tanchor_entity_id\toffset_x\toffset_y\ttemplate_entity_id\n"
                + "1\t7\t10\t0\t1\t2\t100\n");
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                7,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorSetLevelId: 6),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var template = new AuthoritativeWorldObject(100, 97, 0, 0, 0);

        var generated = catalog.Materialize(
            world,
            [],
            [template],
            fragments,
            setLevelOrigins: new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
                [6] = new(24, 2, 2),
            });

        var result = Assert.Single(generated);
        Assert.Equal((10U, 97U, 24U, 3, 4), (
            result.EntityId,
            result.ObjectId,
            result.LevelId,
            result.PositionX,
            result.PositionY));
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
    public void EnforcesNativeObjectPoolCapacityForGeneratedObjectsPerLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                7,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorPositionX: 2,
                AnchorPositionY: 2),
        ]);
        var template = new AuthoritativeWorldObject(100, 97, 0, 0, 0);

        static AuthoritativeNativeDunFragmentObjectPlacement Placement(int index)
            => new(
                (uint)(index + 1),
                7,
                (uint)(index + 1000),
                0,
                0,
                0) {
                TemplateEntityId = 100,
            };

        var exactCatalog = new AuthoritativeNativeDunFragmentObjectCatalog(
            Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount)
                .Select(Placement));
        Assert.Equal(
            AuthoritativeLevel.NativeMaximumObjectCount,
            exactCatalog.Materialize(world, [], [template], fragments).Count);

        var overflowCatalog = new AuthoritativeNativeDunFragmentObjectCatalog(
            Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount + 1)
                .Select(Placement));
        var overflow = Assert.Throws<InvalidDataException>(() =>
            overflowCatalog.Materialize(world, [], [template], fragments));
        Assert.Contains("127-object capacity", overflow.Message, StringComparison.Ordinal);

        var existing = Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount)
            .Select(index => new AuthoritativeWorldObject(
                (uint)(9000 + index),
                97,
                24,
                0,
                0));
        var existingOccupancy = Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeDunFragmentObjectCatalog([Placement(0)])
                .Materialize(world, existing, [template], fragments));
        Assert.Contains("127-object capacity", existingOccupancy.Message, StringComparison.Ordinal);
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
