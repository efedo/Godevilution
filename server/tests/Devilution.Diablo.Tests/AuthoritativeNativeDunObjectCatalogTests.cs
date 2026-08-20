using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunObjectCatalogTests
{
    [Fact]
    public void TsvLoaderParsesStableIdentityAndActiveVariant()
    {
        var catalog = AuthoritativeNativeDunObjectCatalog.LoadTsv(
            "native_dun_objects.tsv",
            "set_level_id\tset_level_symbol\tactive_map\tnative_object_id\tobject_id\ttemplate_entity_id\n"
                + "2\tSL_BONECHAMB\t1\t37\t4\t3001\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal(2, definition.SetLevelId);
        Assert.True(definition.ActiveMap);
        Assert.Equal((ushort)37, definition.NativeObjectId);
        Assert.Equal(4U, definition.ObjectId);
        Assert.Equal(3001U, definition.TemplateEntityId);
        Assert.Equal("SL_BONECHAMB", definition.SetLevelSymbol);
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
