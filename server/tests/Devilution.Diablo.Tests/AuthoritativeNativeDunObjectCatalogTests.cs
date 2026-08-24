using Devilution.Diablo;
using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunObjectCatalogTests
{
    [Fact]
    public void TsvLoaderParsesStableIdentityAndActiveVariant()
    {
        var catalog = AuthoritativeNativeDunObjectCatalog.LoadTsv(
            "native_dun_objects.tsv",
            "set_level_id\tset_level_symbol\tactive_map\tnative_object_id\tobject_id\tobject_symbol\ttemplate_entity_id\n"
                + "2\tSL_BONECHAMB\t1\t37\t4\tbone_door\t3001\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(2, definition.SetLevelId);
        Assert.True(definition.ActiveMap);
        Assert.Equal((ushort)37, definition.NativeObjectId);
        Assert.Equal(4U, definition.ObjectId);
        Assert.Equal(3001U, definition.TemplateEntityId);
        Assert.Equal("SL_BONECHAMB", definition.SetLevelSymbol);
        Assert.Equal("bone_door", definition.ObjectSymbol);
    }

    [Fact]
    public void TsvLoaderResolvesStableObjectSymbol()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "catacombs", 8),
            new StableContentId("object", "bone_door", 4),
        ]);
        var catalog = AuthoritativeNativeDunObjectCatalog.LoadTsv(
            "native_dun_objects.tsv",
            "level_id\tlevel_symbol\tnative_object_id\tobject_id\tobject_symbol\n"
                + "8\tcatacombs\t71\t4\tbone_door\n",
            stableIds);

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(4U, definition.ObjectId);
        Assert.Equal("bone_door", definition.ObjectSymbol);
    }

    [Fact]
    public void TsvLoaderRejectsMismatchedStableObjectSymbol()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "catacombs", 8),
            new StableContentId("object", "bone_door", 4),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunObjectCatalog.LoadTsv(
                "native_dun_objects.tsv",
                "level_id\tlevel_symbol\tnative_object_id\tobject_id\tobject_symbol\n"
                    + "8\tcatacombs\t71\t5\tbone_door\n",
                stableIds));
        Assert.Contains("maps object 5 to symbol 'bone_door'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TsvLoaderRejectsNativeObjectIdOutsideCompatibilityRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunObjectCatalog.LoadTsv(
                "native_dun_objects.tsv",
                "set_level_id\tactive_map\tnative_object_id\tobject_id\n"
                    + "2\t1\t109\t4\n"));
    }

    [Fact]
    public void TsvLoaderParsesOrdinaryLevelTarget()
    {
        var catalog = AuthoritativeNativeDunObjectCatalog.LoadTsv(
            "native_dun_objects.tsv",
            "level_id\tlevel_symbol\tnative_object_id\tobject_id\ttemplate_entity_id\n"
                + "8\tcatacombs\t71\t4001\t0\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(AuthoritativeNativeDunObjectTargetKind.Level, definition.TargetKind);
        Assert.Equal(8U, definition.LevelId);
        Assert.Equal("catacombs", definition.LevelSymbol);
        Assert.Equal(8U, definition.GetRuntimeLevelId());
    }

    [Fact]
    public void MaterializesMappedCellsWithDeterministicEntitiesAndTemplateBehavior()
    {
        var levelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            levelId,
            3,
            2,
            nativeObjectLayer: [0, 9, 0, 9, 0, 0]));
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(1, true, 9, 4, 3001),
        ]);
        var template = new AuthoritativeWorldObject(
            3001,
            4,
            1,
            0,
            0,
            questId: 3015,
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevel,
            effectAmount: 7);

        var objects = catalog.LoadAvailableObjects(world, templates: [template]);

        Assert.Equal(2, objects.Count);
        Assert.Equal(
            AuthoritativeNativeDunObjectCatalog.GetRuntimeEntityId(1, true, 1),
            objects[0].EntityId);
        Assert.Equal((1, 0), (objects[0].PositionX, objects[0].PositionY));
        Assert.Equal(
            AuthoritativeNativeDunObjectCatalog.GetRuntimeEntityId(1, true, 3),
            objects[1].EntityId);
        Assert.Equal((0, 1), (objects[1].PositionX, objects[1].PositionY));
        Assert.All(objects, @object => {
            Assert.Equal(3015U, @object.QuestId);
            Assert.Equal(AuthoritativeObjectEffectKind.GrantSpellLevel, @object.EffectKind);
            Assert.Equal(7, @object.EffectAmount);
        });
    }

    [Fact]
    public void UnmappedNativeCellsRemainInert()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            2,
            2,
            nativeObjectLayer: [11, 0, 12, 0]));
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(1, true, 11, 4, 0),
        ]);

        var objects = catalog.LoadAvailableObjects(world);

        var @object = Assert.Single(objects);
        Assert.Equal(4U, @object.ObjectId);
        Assert.Equal((0, 0), (@object.PositionX, @object.PositionY));
    }

    [Fact]
    public void MaterializesOrdinaryLevelCellsWithSeparateReservedEntityIds()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            8,
            2,
            2,
            nativeObjectLayer: [0, 71, 0, 0]));
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(0, false, 71, 4001, 0) {
                TargetKind = AuthoritativeNativeDunObjectTargetKind.Level,
                LevelId = 8,
            },
        ]);

        var @object = Assert.Single(catalog.LoadAvailableObjects(world));

        Assert.Equal(
            AuthoritativeNativeDunObjectCatalog.GetRuntimeEntityId(8, 1),
            @object.EntityId);
        Assert.Equal((1, 0), (@object.PositionX, @object.PositionY));
        Assert.Equal(4001U, @object.ObjectId);
    }

    [Fact]
    public void RejectsRowsThatTargetBothOrdinaryAndSetLevels()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunObjectCatalog.LoadTsv(
                "native_dun_objects.tsv",
                "set_level_id\tlevel_id\tactive_map\tnative_object_id\tobject_id\n"
                    + "2\t8\t1\t71\t4001\n"));
    }

    [Fact]
    public void RejectsDuplicateLayerMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(1, true, 9, 4, 0),
            new AuthoritativeNativeDunObjectDefinition(1, true, 9, 5, 0),
        ]));
    }

    [Fact]
    public void RejectsMappedLevelWithoutNativeObjectLayer()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true),
            2,
            2));
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(1, true, 9, 4, 0),
        ]);

        Assert.Throws<InvalidDataException>(() => catalog.LoadAvailableObjects(world));
    }
}
