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
            "placement_id\tset_level_id\tset_level_symbol\tobject_id\tposition_x\tposition_y\ttemplate_x\ttemplate_y\ttemplate_width\ttemplate_height\ttemplate_entity_id\n"
                + "1002\t1\tSL_SKELKING\t2\t64\t59\t20\t14\t1\t2\t2002\n"
                + "1001\t1\tSL_SKELKING\t1\t64\t34\t20\t7\t3\t3\t0\n");

        Assert.Equal([1001U, 1002U], catalog.Definitions.Select(placement => placement.PlacementId));
        var gate = Assert.Single(catalog.GetForSetLevel(1), placement => placement.ObjectId == 2);
        Assert.Equal("SL_SKELKING", gate.SetLevelSymbol);
        Assert.Equal(2002U, gate.TemplateEntityId);
        Assert.Equal((64, 59, 20, 14, 1, 2), (
            gate.PositionX,
            gate.PositionY,
            gate.TemplateX,
            gate.TemplateY,
            gate.TemplateWidth,
            gate.TemplateHeight));
    }

    [Fact]
    public void TsvLoaderAcceptsGeneratedSetPieceOffsets()
    {
        var catalog = AuthoritativeSetLevelObjectCatalog.LoadTsv(
            "set_level_objects.tsv",
            "placement_id\tset_level_id\tset_level_symbol\tobject_id\tposition_x\tposition_y\ttemplate_x\ttemplate_y\ttemplate_width\ttemplate_height\tplacement_mode\n"
                + "1101\t6\tSL_BLIND\t4\t9\t24\t0\t0\t2\t2\tset_level_origin\n");

        var placement = Assert.Single(catalog.Definitions);
        Assert.Equal(
            AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
            placement.PlacementMode);
        Assert.Equal((9, 24), (placement.PositionX, placement.PositionY));
    }

    [Fact]
    public void ValidationRejectsDuplicatePlacementIdentity()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSetLevelObjectCatalog([
            new(1001, 1, 1, 10, 10, 0, 0, 1, 1),
            new(1001, 1, 2, 10, 10, 0, 0, 1, 1),
        ]));
    }

    [Fact]
    public void MaterializesPlacementsForLoadedActiveSetLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            8,
            8));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1001, 1, 2, 4, 5, 0, 0, 1, 1) {
                SetLevelSymbol = "SL_SKELKING",
            },
        ]);

        var objects = catalog.LoadAvailableObjects(world);

        var @object = Assert.Single(objects);
        Assert.Equal(
            AuthoritativeSetLevelObjectCatalog.RuntimeObjectEntityIdBase + 1001,
            @object.EntityId);
        Assert.Equal(2U, @object.ObjectId);
        Assert.Equal(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            @object.LevelId);
        Assert.Equal(4, @object.PositionX);
        Assert.Equal(5, @object.PositionY);
    }

    [Fact]
    public void EnforcesNativeObjectPoolCapacityPerLevel()
    {
        var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            levelId,
            AuthoritativeLevel.NativeMaximumWidth,
            2));

        static AuthoritativeSetLevelObjectPlacement Placement(int index)
            => new(
                (uint)(1000 + index),
                1,
                2,
                index % AuthoritativeLevel.NativeMaximumWidth,
                index / AuthoritativeLevel.NativeMaximumWidth,
                0,
                0,
                1,
                1);

        var exactCatalog = new AuthoritativeSetLevelObjectCatalog(
            Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount)
                .Select(Placement));
        Assert.Equal(
            AuthoritativeLevel.NativeMaximumObjectCount,
            exactCatalog.LoadAvailableObjects(world).Count);

        var overflowCatalog = new AuthoritativeSetLevelObjectCatalog(
            Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount + 1)
                .Select(Placement));
        var overflow = Assert.Throws<InvalidDataException>(
            () => overflowCatalog.LoadAvailableObjects(world));
        Assert.Contains("127-object capacity", overflow.Message, StringComparison.Ordinal);

        var existing = Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount)
            .Select(index => new AuthoritativeWorldObject(
                (uint)(9000 + index),
                2,
                levelId,
                0,
                0));
        var existingOccupancy = Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeSetLevelObjectCatalog([Placement(0)])
                .LoadAvailableObjects(world, existing));
        Assert.Contains("127-object capacity", existingOccupancy.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterializesGeneratedSetPiecePlacementFromSuppliedOrigin()
    {
        var levelId = 17U;
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(levelId, 32, 32));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, 9, 24, 0, 0, 2, 2) {
                PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
            },
        ]);

        var objects = catalog.LoadAvailableObjects(
            world,
            setLevelOrigins: new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
                [6] = new(levelId, 12, 7),
            });

        var @object = Assert.Single(objects);
        Assert.Equal((21, 31), (@object.PositionX, @object.PositionY));
        Assert.Equal(levelId, @object.LevelId);
    }

    [Fact]
    public void DefersGeneratedSetPiecePlacementUntilItsOriginExists()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, 9, 24, 0, 0, 2, 2) {
                PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
            },
        ]);

        Assert.Empty(catalog.LoadAvailableObjects(world));
        Assert.Empty(catalog.LoadAvailableObjects(
            world,
            setLevelOrigins: new Dictionary<int, AuthoritativeSetLevelObjectOrigin>()));
    }

    [Fact]
    public void RejectsUnknownGeneratedPlacementMode()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeSetLevelObjectCatalog.LoadTsv(
            "set_level_objects.tsv",
            "placement_id\tset_level_id\tset_level_symbol\tobject_id\tposition_x\tposition_y\ttemplate_x\ttemplate_y\ttemplate_width\ttemplate_height\tplacement_mode\n"
                + "1101\t6\tSL_BLIND\t4\t9\t24\t0\t0\t2\t2\tfuture_mode\n"));
    }

    [Fact]
    public void MaterializesAuthoredTemplateBehaviorAtPlacement()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(2, activeMap: true),
            8,
            8));
        var catalog = AuthoritativeSetLevelObjectCatalog.LoadTsv(
            "set_level_objects.tsv",
            "placement_id\tset_level_id\tset_level_symbol\tobject_id\tposition_x\tposition_y\ttemplate_x\ttemplate_y\ttemplate_width\ttemplate_height\ttemplate_entity_id\n"
                + "2001\t2\tSL_BONECHAMB\t4\t4\t5\t0\t0\t1\t1\t3001\n");
        var template = new AuthoritativeWorldObject(
            3001,
            4,
            1,
            1,
            1,
            questId: 3015,
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevel,
            effectAmount: 7,
            questTransitionKind: AuthoritativeObjectQuestTransitionKind.CompleteQuest,
            rewardKind: AuthoritativeObjectRewardKind.None);

        var @object = Assert.Single(catalog.LoadAvailableObjects(world, templates: [template]));

        Assert.Equal(4U, @object.ObjectId);
        Assert.Equal(3015U, @object.QuestId);
        Assert.Equal(AuthoritativeObjectEffectKind.GrantSpellLevel, @object.EffectKind);
        Assert.Equal(7, @object.EffectAmount);
        Assert.Equal(AuthoritativeObjectQuestTransitionKind.CompleteQuest, @object.QuestTransitionKind);
        Assert.Equal((4, 5), (@object.PositionX, @object.PositionY));
    }

    [Fact]
    public void RejectsTemplateWithDifferentNativeObjectIdentity()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            8,
            8));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1001, 1, 2, 4, 5, 0, 0, 1, 1) {
                TemplateEntityId = 3001,
            },
        ]);
        var template = new AuthoritativeWorldObject(3001, 9, 1, 1, 1);

        Assert.Throws<InvalidDataException>(() => catalog.LoadAvailableObjects(world, templates: [template]));
    }

    [Fact]
    public void LeavesPlacementsMetadataOnlyUntilActiveGeometryIsLoaded()
    {
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1001, 1, 2, 4, 5, 0, 0, 1, 1),
        ]);

        Assert.Empty(catalog.LoadAvailableObjects(new AuthoritativeWorld()));
    }

    [Fact]
    public void RejectsRuntimePlacementEntityCollision()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            8,
            8));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1001, 1, 2, 4, 5, 0, 0, 1, 1),
        ]);
        var existing = new AuthoritativeWorldObject(
            AuthoritativeSetLevelObjectCatalog.RuntimeObjectEntityIdBase + 1001,
            9,
            1,
            1,
            1);

        Assert.Throws<InvalidDataException>(() => catalog.LoadAvailableObjects(world, [existing]));
    }

    [Fact]
    public void RejectsPlacementOutsideLoadedActiveLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            2,
            2));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1001, 1, 2, 4, 5, 0, 0, 1, 1),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.LoadAvailableObjects(world));
    }

    [Fact]
    public void RejectsUnknownTemplateBeforeSetLevelGeometryIsLoaded()
    {
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1001, 1, 2, 4, 5, 0, 0, 1, 1) {
                TemplateEntityId = 3001,
            },
        ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateTemplates([]));

        Assert.Contains("unknown template entity 3001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTemplateWithDifferentObjectIdentityBeforeSetLevelGeometryIsLoaded()
    {
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1001, 1, 2, 4, 5, 0, 0, 1, 1) {
                TemplateEntityId = 3001,
            },
        ]);
        var template = new AuthoritativeWorldObject(3001, 9, 0, 0, 0);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateTemplates([template]));

        Assert.Contains("uses object 9, expected 2", exception.Message, StringComparison.Ordinal);
    }

}
