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
    public void TsvLoaderAcceptsRawDunObjectIdBeyondPresentationRange()
    {
        var catalog = AuthoritativeNativeDunObjectCatalog.LoadTsv(
            "native_dun_objects.tsv",
            "set_level_id\tactive_map\tnative_object_id\tobject_id\n"
                + "2\t1\t109\t4\n");

        Assert.Equal((ushort)109, Assert.Single(catalog.Definitions).NativeObjectId);
    }

    [Fact]
    public void TsvLoaderRejectsRawDunObjectIdOutsideCompatibilityRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeDunObjectCatalog.LoadTsv(
                "native_dun_objects.tsv",
                "set_level_id\tactive_map\tnative_object_id\tobject_id\n"
                    + "2\t1\t147\t4\n"));
    }

    [Fact]
    public void ProductionValidationRejectsRowsThatTargetBothLevelKinds()
    {
        var table = TsvTable.Parse(
            "native_dun_objects.tsv",
            "set_level_id\tset_level_symbol\tlevel_id\tlevel_symbol\tactive_map\tnative_object_id\tobject_id\tobject_symbol\ttemplate_entity_id\n"
                + "2\tSL_BONECHAMB\t8\tcatacombs\t1\t71\t4\tbone_door\t3001\n");

        var exception = Assert.Throws<InvalidDataException>(
            () => AuthoritativeNativeDunObjectCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("must target exactly one set-level or ordinary level", exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionValidationRejectsActiveMapOnOrdinaryLevelRows()
    {
        var table = TsvTable.Parse(
            "native_dun_objects.tsv",
            "set_level_id\tset_level_symbol\tlevel_id\tlevel_symbol\tactive_map\tnative_object_id\tobject_id\tobject_symbol\ttemplate_entity_id\n"
                + "\t\t8\tcatacombs\t1\t71\t4\tbone_door\t3001\n");

        var exception = Assert.Throws<InvalidDataException>(
            () => AuthoritativeNativeDunObjectCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("cannot use active_map with an ordinary level target", exception.Message,
            StringComparison.Ordinal);
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

    [Fact]
    public void RejectsOrdinaryMappingForUnknownWorldLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(7, 2, 2));
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(0, false, 9, 4, 0) {
                TargetKind = AuthoritativeNativeDunObjectTargetKind.Level,
                LevelId = 8,
            },
        ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateWorld(world));

        Assert.Contains("unknown level 8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsDeferredSetLevelMappingWhenWorldDoesNotContainItsRuntimeLevel()
    {
        var world = new AuthoritativeWorld();
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(2, true, 9, 4, 0),
        ]);

        catalog.ValidateWorld(world);
    }

    [Fact]
    public void RejectsUnknownTemplateBeforeNativeGeometryIsLoaded()
    {
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(2, true, 9, 4, 3001),
        ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateTemplates([]));

        Assert.Contains("unknown template entity 3001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTemplateWithDifferentObjectIdentityBeforeGeometryIsLoaded()
    {
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(2, true, 9, 4, 3001),
        ]);
        var template = new AuthoritativeWorldObject(3001, 5, 0, 0, 0);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateTemplates([template]));

        Assert.Contains("uses object 5, expected 4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTemplateWithDifferentNativeObjectIdentityBeforeGeometryIsLoaded()
    {
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(2, true, 109, 4, 3001),
        ]);
        var template = new AuthoritativeWorldObject(
            3001,
            4,
            0,
            0,
            0,
            nativeObjectId: 5);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateTemplates([template]));

        Assert.Contains("uses native object 5, expected 4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforcesNativeObjectPoolCapacityPerLevel()
    {
        var catalog = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(0, false, 9, 4, 0) {
                TargetKind = AuthoritativeNativeDunObjectTargetKind.Level,
                LevelId = 8,
            },
        ]);
        static ushort[] ObjectLayer(int mappedCount)
            => Enumerable.Range(0, AuthoritativeLevel.NativeMaximumWidth * 2)
                .Select(cell => (ushort)(cell < mappedCount ? 9 : 0))
                .ToArray();

        var atCapacityWorld = new AuthoritativeWorld();
        atCapacityWorld.AddLevel(new AuthoritativeLevel(
            8,
            AuthoritativeLevel.NativeMaximumWidth,
            2,
            nativeObjectLayer: ObjectLayer(AuthoritativeLevel.NativeMaximumObjectCount)));
        var objects = catalog.LoadAvailableObjects(atCapacityWorld);
        Assert.Equal(AuthoritativeLevel.NativeMaximumObjectCount, objects.Count);

        var overCapacityWorld = new AuthoritativeWorld();
        overCapacityWorld.AddLevel(new AuthoritativeLevel(
            8,
            AuthoritativeLevel.NativeMaximumWidth,
            2,
            nativeObjectLayer: ObjectLayer(AuthoritativeLevel.NativeMaximumObjectCount + 1)));
        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.LoadAvailableObjects(overCapacityWorld));

        Assert.Contains("127-object capacity", exception.Message, StringComparison.Ordinal);

        var existingWorld = new AuthoritativeWorld();
        existingWorld.AddLevel(new AuthoritativeLevel(
            8,
            AuthoritativeLevel.NativeMaximumWidth,
            2,
            nativeObjectLayer: ObjectLayer(1)));
        var existingObjects = Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount)
            .Select(index => new AuthoritativeWorldObject(
                (uint)(5000 + index),
                4,
                8,
                index % AuthoritativeLevel.NativeMaximumWidth,
                index / AuthoritativeLevel.NativeMaximumWidth))
            .ToArray();
        var existingException = Assert.Throws<InvalidDataException>(() =>
            catalog.LoadAvailableObjects(existingWorld, existingObjects));

        Assert.Contains("127-object capacity", existingException.Message, StringComparison.Ordinal);
    }
}
