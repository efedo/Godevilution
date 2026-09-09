using System.Buffers.Binary;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunFragmentCatalogTests
{
    [Fact]
    public void RejectsAnchorIdentitiesThatExceedTheNativeUtf8ByteLimit()
    {
        var identity = new string('é', 65);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentDefinition(
            1,
            24,
            "fragment.dun",
            0,
            0,
            0,
            AnchorUniqueIdentity: identity).Validate());
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentObjectPlacement(
            1,
            1,
            2,
            0,
            0,
            0) { AnchorUniqueIdentity = identity }.Validate());
    }

    [Fact]
    public void RejectsDeferredFragmentForUnknownWorldLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                25,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Snotspill"),
        ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateWorld(world));

        Assert.Contains("unknown level 25", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsUniqueAnchorDeferralWhenTargetLevelIsKnown()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Snotspill"),
        ]);

        catalog.ValidateWorld(world);
    }

    [Fact]
    public void LoadsAndAppliesAnAnchorRelativeMegaTileFragment()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t9\t-2\t-2\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8, blockedCells: [0]));
        var anchor = new AuthoritativeWorldObject(9, 6003, 24, 4, 4);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        catalog.Apply(content, world, [anchor]);

        var level = Assert.Single(world.Levels);
        Assert.Equal([0], level.BlockedCells);
        Assert.Equal(16, level.NativeMegaTileLayer.Count);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void LoadsAndAppliesAProceduralQuestOriginFragment()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\tanchor_procedural_origin_id\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t\t-2\t-2\t2201\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        catalog.Apply(
            content,
            world,
            [],
            proceduralQuestPieceOrigins: new Dictionary<uint, AuthoritativeProceduralQuestPieceOrigin> {
                [2201] = new(24, 4, 4),
            });

        var level = Assert.Single(world.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void ResolvesProceduralQuestOriginFragmentSymbol()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("procedural_origin", "halls_book", 2201),
        ]);
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\tanchor_procedural_origin_symbol\n"
                + "1\t24\tfragment.dun\t\t-2\t-2\thalls_book\n",
            stableIds);

        Assert.Equal(
            2201U,
            Assert.Single(catalog.Definitions).AnchorProceduralQuestPieceOriginId);
    }

    [Fact]
    public void ComposesValidatedNativeCollisionWhenTileDefinitionsAreSupplied()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\ttile_asset_path\tsol_asset_path\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t9\t-2\t-2\tfragment.til\tfragment.sol\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8, blockedCells: [0]));
        var anchor = new AuthoritativeWorldObject(9, 6003, 24, 4, 4);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
                new ContentAsset("fragment.til", CreateTil()),
                new ContentAsset("fragment.sol", CreateSol()),
            ]),
        ]));

        catalog.Apply(content, world, [anchor]);

        var level = Assert.Single(world.Levels);
        Assert.Equal([0, 18, 26], level.BlockedCells);
        Assert.Equal([0, 19, 26], level.MissileBlockedCells);
        Assert.Equal(AuthoritativeDunTileFlags.Solid, level.NativePieceProperties[1]);
        Assert.Equal([18, 26], level.NativePieceCollisionCells);
        Assert.Equal([19, 26], level.NativePieceMissileBlockedCells);
    }

    [Fact]
    public void ComposesNativeDPieceLayerFromFragmentData()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\ttile_asset_path\tsol_asset_path\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t9\t-2\t-2\tfragment.til\tfragment.sol\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var anchor = new AuthoritativeWorldObject(9, 6003, 24, 4, 4);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
                new ContentAsset("fragment.til", CreateTil()),
                new ContentAsset("fragment.sol", CreateSol()),
            ]),
        ]));

        catalog.Apply(content, world, [anchor]);

        var level = Assert.Single(world.Levels);
        Assert.Equal(64, level.NativePieceLayer.Count);
        Assert.Equal((ushort)1, level.NativePieceLayer[2 * 8 + 2]);
        Assert.Equal((ushort)4, level.NativePieceLayer[3 * 8 + 3]);
    }

    [Fact]
    public void ComposesNativeMonsterLayerFromFragmentData()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_offset_x\tanchor_offset_y\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t9\t-2\t-2\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var anchor = new AuthoritativeWorldObject(9, 6003, 24, 4, 4);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42, monsterId: 155)),
            ]),
        ]));

        catalog.Apply(content, world, [anchor]);

        var level = Assert.Single(world.Levels);
        Assert.Equal(64, level.NativeMonsterLayer.Count);
        Assert.Equal((ushort)155, level.NativeMonsterLayer[2 * 8 + 2]);
    }

    [Fact]
    public void AppliesAFragmentRelativeToAUniqueEncounter()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_unique_identity\tanchor_offset_x\tanchor_offset_y\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t\tSnotspill\t-2\t-2\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var target = new AuthoritativeCombatTarget(
            0x10000000,
            4,
            4,
            100,
            levelId: 24,
            unique: true,
            uniqueIdentity: "Snotspill");
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        catalog.Apply(content, world, [], [target]);

        var level = Assert.Single(world.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void AppliesAFragmentFromAValidatedCoordinateAnchor()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_unique_identity\tanchor_position_x\tanchor_position_y\tanchor_offset_x\tanchor_offset_y\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t0\t\t4\t4\t-2\t-2\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        catalog.Apply(content, world, []);

        var level = Assert.Single(world.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void AppliesAFragmentFromAPublishedSetLevelOrigin()
    {
        var catalog = AuthoritativeNativeDunFragmentCatalog.LoadTsv(
            "native_dun_fragments.tsv",
            "fragment_id\tlevel_id\tlevel_symbol\tasset_path\tanchor_entity_id\tanchor_set_level_id\tanchor_offset_x\tanchor_offset_y\n"
                + "1\t24\thellfire_level_24\tfragment.dun\t0\t6\t-2\t-2\n");
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        catalog.Apply(
            content,
            world,
            [],
            setLevelOrigins: new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
                [6] = new(24, 4, 4),
            });

        var level = Assert.Single(world.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void RejectsUnpublishedSetLevelOrigin()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorSetLevelId: 6),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(content, world, []));

        Assert.Contains("unpublished set-level origin 6", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulationDefersUniqueAnchoredFragmentsUntilTheEncounterExists()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Snotspill"),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var target = new AuthoritativeCombatTarget(
            0x10000000,
            4,
            4,
            100,
            levelId: 24,
            unique: true,
            uniqueIdentity: "Snotspill");
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42, 11)),
            ]),
        ]));
        var nativeObjectTemplate = new AuthoritativeWorldObject(100, 97, 0, 0, 0);
        var nativeDunObjects = new AuthoritativeNativeDunObjectCatalog([
            new AuthoritativeNativeDunObjectDefinition(0, false, 11, 97, 100) {
                TargetKind = AuthoritativeNativeDunObjectTargetKind.Level,
                LevelId = 24,
            },
        ]);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                CombatTargets = [target],
                NativeDunFragments = catalog,
                NativeDunObjects = nativeDunObjects,
                NativeObjectTemplates = [nativeObjectTemplate],
                Content = content,
            });

        var level = Assert.Single(simulation.WorldState.World!.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
        var mappedObject = Assert.Single(
            simulation.WorldState.Objects.Values,
            @object => @object.ObjectId == 97);
        Assert.Equal((target.PositionX - 2, target.PositionY - 2), (
            mappedObject.PositionX,
            mappedObject.PositionY));
    }

    [Fact]
    public void SimulationDefersSetLevelOriginFragmentUntilOriginPublication()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorSetLevelId: 6),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                NativeDunFragments = catalog,
                Content = content,
            });

        Assert.Empty(simulation.WorldState.World!.Levels.Single().NativeMegaTileLayer);
        simulation.PublishSetLevelObjectOrigin(6, new AuthoritativeSetLevelObjectOrigin(24, 4, 4));

        Assert.Equal((ushort)42, simulation.WorldState.World.Levels.Single().NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void DeferredFragmentRetainsSharedExternalGeometryAfterOriginPublication()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorSetLevelId: 6),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            "native_dun_piece_definitions.tsv",
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "42\t1\t2\t3\t4\n",
            "native_dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_light\tblock_missile\n"
                + "1\ttrue\tfalse\tfalse\n"
                + "2\tfalse\tfalse\ttrue\n"
                + "3\ttrue\tfalse\ttrue\n"
                + "4\tfalse\tfalse\tfalse\n");
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                NativeDunFragments = catalog,
                NativeDunGeometry = geometry,
                Content = content,
            });

        simulation.PublishSetLevelObjectOrigin(6, new AuthoritativeSetLevelObjectOrigin(24, 4, 4));

        var level = Assert.Single(simulation.WorldState.World!.Levels);
        Assert.Equal([1, 2, 3, 4], level.NativePieceLayer.Skip(2 * level.Width + 2).Take(2).Concat(
            level.NativePieceLayer.Skip(3 * level.Width + 2).Take(2)));
        Assert.Equal([18, 26], level.BlockedCells);
        Assert.Equal([19, 26], level.MissileBlockedCells);
    }

    [Fact]
    public void SnapshotRestoreRepublishesSetLevelOriginFragmentWithoutPreloadedOrigin()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorSetLevelId: 6),
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));
        var geometry = CreateExternalGeometry();
        var fragmentObjects = new AuthoritativeNativeDunFragmentObjectCatalog([
            new AuthoritativeNativeDunFragmentObjectPlacement(1, 1, 900, 0, 1, 1) {
                AnchorSetLevelId = 6,
                TemplateEntityId = 100,
            },
        ]);
        var template = new AuthoritativeWorldObject(100, 97, 24, 0, 0);
        var sourceWorld = new AuthoritativeWorld();
        sourceWorld.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = sourceWorld,
                Content = content,
                NativeDunFragments = catalog,
                NativeDunGeometry = geometry,
                NativeDunFragmentObjects = fragmentObjects,
                NativeObjectTemplates = [template],
            });
        var player = new AuthoritativePlayerState(
            0,
            0,
            10,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            4,
            4,
            10,
            1,
            24,
            0);

        source.PublishSetLevelObjectOrigin(
            6,
            new AuthoritativeSetLevelObjectOrigin(24, 4, 4));
        var sourceObject = Assert.Single(source.WorldState.Objects.Values);
        Assert.Equal((5, 5), (sourceObject.PositionX, sourceObject.PositionY));
        var snapshot = source.CaptureSnapshot(player, 3);

        var replacementWorld = new AuthoritativeWorld();
        replacementWorld.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = replacementWorld,
                Content = content,
                NativeDunFragments = catalog,
                NativeDunGeometry = geometry,
                NativeDunFragmentObjects = fragmentObjects,
                NativeObjectTemplates = [template],
            });

        replacement.RestoreSnapshot(player, snapshot);

        var restoredLevel = Assert.Single(replacement.WorldState.World!.Levels);
        Assert.Equal((ushort)42, restoredLevel.NativeMegaTileLayer[1 * 4 + 1]);
        Assert.Equal([18, 26], restoredLevel.BlockedCells);
        Assert.Equal([19, 26], restoredLevel.MissileBlockedCells);
        Assert.Equal(
            new AuthoritativeSetLevelObjectOrigin(24, 4, 4),
            replacement.SetLevelObjectOrigins[6]);
        var restoredObject = Assert.Single(replacement.WorldState.Objects.Values);
        Assert.Equal(900U, restoredObject.EntityId);
        Assert.Equal((5, 5), (restoredObject.PositionX, restoredObject.PositionY));
    }

    [Fact]
    public void SnapshotRestoreRepublishesProceduralQuestOriginFragmentWithoutPreloadedOrigin()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorProceduralQuestPieceOriginId: 2201),
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));
        var geometry = CreateExternalGeometry();
        var fragmentObjects = new AuthoritativeNativeDunFragmentObjectCatalog([
            new AuthoritativeNativeDunFragmentObjectPlacement(1, 1, 900, 0, 1, 1) {
                AnchorProceduralQuestPieceOriginId = 2201,
                TemplateEntityId = 100,
            },
        ]);
        var template = new AuthoritativeWorldObject(100, 97, 24, 0, 0);
        var sourceWorld = new AuthoritativeWorld();
        sourceWorld.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = sourceWorld,
                Content = content,
                NativeDunFragments = catalog,
                NativeDunGeometry = geometry,
                NativeDunFragmentObjects = fragmentObjects,
                NativeObjectTemplates = [template],
            });
        var player = new AuthoritativePlayerState(
            0,
            0,
            10,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            4,
            4,
            10,
            1,
            24,
            0);

        source.PublishProceduralQuestPieceOrigin(
            2201,
            new AuthoritativeProceduralQuestPieceOrigin(24, 4, 4));
        var sourceObject = Assert.Single(source.WorldState.Objects.Values);
        Assert.Equal((5, 5), (sourceObject.PositionX, sourceObject.PositionY));
        var snapshot = source.CaptureSnapshot(player, 3);

        var replacementWorld = new AuthoritativeWorld();
        replacementWorld.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = replacementWorld,
                Content = content,
                NativeDunFragments = catalog,
                NativeDunGeometry = geometry,
                NativeDunFragmentObjects = fragmentObjects,
                NativeObjectTemplates = [template],
            });

        replacement.RestoreSnapshot(player, snapshot);

        var restoredLevel = Assert.Single(replacement.WorldState.World!.Levels);
        Assert.Equal((ushort)42, restoredLevel.NativeMegaTileLayer[1 * 4 + 1]);
        Assert.Equal([18, 26], restoredLevel.BlockedCells);
        Assert.Equal([19, 26], restoredLevel.MissileBlockedCells);
        Assert.Equal(
            new AuthoritativeProceduralQuestPieceOrigin(24, 4, 4),
            replacement.ProceduralQuestPieceOriginLocations[2201]);
        var restoredObject = Assert.Single(replacement.WorldState.Objects.Values);
        Assert.Equal(900U, restoredObject.EntityId);
        Assert.Equal((5, 5), (restoredObject.PositionX, restoredObject.PositionY));
    }

    [Fact]
    public void FailedSnapshotRestoreRollsBackLateFragmentObjectConflict()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorSetLevelId: 6),
        ]);
        var fragmentObjects = new AuthoritativeNativeDunFragmentObjectCatalog([
            new AuthoritativeNativeDunFragmentObjectPlacement(1, 1, 900, 0, 1, 1) {
                AnchorSetLevelId = 6,
                TemplateEntityId = 100,
            },
        ]);
        var template = new AuthoritativeWorldObject(100, 97, 24, 0, 0);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Content = content,
                NativeDunFragments = catalog,
                NativeDunFragmentObjects = fragmentObjects,
                NativeObjectTemplates = [template],
            });
        var player = new AuthoritativePlayerState(
            0,
            0,
            10,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            4,
            4,
            10,
            1,
            24,
            0);

        simulation.PublishSetLevelObjectOrigin(
            6,
            new AuthoritativeSetLevelObjectOrigin(24, 4, 4));
        var valid = simulation.CaptureSnapshot(player, 3);
        simulation.RestoreSnapshot(player, valid);
        var invalid = valid with {
            Objects = [valid.Objects.Single() with { LevelId = 0 }],
        };

        Assert.Throws<InvalidDataException>(() => simulation.RestoreSnapshot(player, invalid));

        var restoredObject = Assert.Single(simulation.WorldState.Objects.Values);
        Assert.Equal(97U, restoredObject.ObjectId);
        Assert.Equal((ushort)42, simulation.WorldState.World!.Levels.Single().NativeMegaTileLayer[1 * 4 + 1]);
        Assert.Equal(
            new AuthoritativeSetLevelObjectOrigin(24, 4, 4),
            simulation.SetLevelObjectOrigins[6]);
    }

    [Fact]
    public void ValidatesDeferredFragmentAssetsBeforeEncounterMaterialization()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "missing.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Snotspill"),
        ]);
        var content = ComposedContent.Compose(new ContentManifest(
            "rules",
            "1",
            [new ContentPack("base", "1", [])]));

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateAssets(content));
        Assert.Contains("missing.dun", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatesDeferredFragmentCollisionBundleBeforeEncounterMaterialization()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                -2,
                -2,
                "fragment.til",
                "fragment.sol",
                AnchorUniqueIdentity: "Snotspill"),
        ]);
        var content = ComposedContent.Compose(new ContentManifest(
            "rules",
            "1",
            [new ContentPack("base", "1", [], assets: [
                // Tile 43 is outside this deliberately short .til bundle.
                new ContentAsset("fragment.dun", CreateDun(43)),
                new ContentAsset("fragment.til", CreateTil()),
                new ContentAsset("fragment.sol", CreateSol()),
            ])]));

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateAssets(content));
        Assert.Contains("unknown mega-tile 43", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppliesDeferredUniqueFragmentAfterProceduralLevelEntrySpawn()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            1,
            8,
            8,
            blockedCells: Enumerable.Range(0, 64)
                .Where(cell => cell % 8 % 2 != 0 || cell / 8 % 2 != 0)));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2,
                2,
                "Skeleton",
                1,
                1,
                1,
                10,
                10,
                1,
                1,
                1,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "SkeletonMelee",
            },
        ]);
        var monsterCatalog = new AuthoritativeMonsterCatalog(
            [],
            [new AuthoritativeMonsterDropDefinition(2, 0, 0, 0, false, true, false)]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            monsterTypes,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
            ]),
            monsterCatalog: monsterCatalog,
            uniqueMonsters: new AuthoritativeUniqueMonsterCatalog([
                new AuthoritativeUniqueMonsterDefinition(0, 2, "Generated", "SkeletonMelee"),
            ]));
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                7,
                1,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Generated"),
        ]);
        var fragmentObjects = new AuthoritativeNativeDunFragmentObjectCatalog([
            new AuthoritativeNativeDunFragmentObjectPlacement(1, 7, 900, 0, -2, -2) {
                AnchorUniqueIdentity = "Generated",
                TemplateEntityId = 100,
            },
        ]);
        var template = new AuthoritativeWorldObject(100, 97, 0, 0, 0);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Content = content,
                NativeDunFragments = fragments,
                NativeDunFragmentObjects = fragmentObjects,
                NativeObjectTemplates = [template],
                MonsterTypes = monsterTypes,
                MonsterSpawnPlanner = planner,
                ProceduralMonsterSpawns = new DiabloMonsterSpawnConfiguration(1, 1),
            });
        var player = new AuthoritativePlayerState(
            0,
            0,
            10,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            10,
            1,
            1,
            0);

        var result = simulation.Execute(
            player,
            new DiabloSetFriendlyModeIntent(true),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var target = Assert.Single(simulation.WorldState.CombatTargets.Values);
        Assert.Equal("Generated", target.UniqueIdentity);
        Assert.InRange(target.PositionX, 2, 7);
        Assert.InRange(target.PositionY, 2, 7);
        var level = Assert.Single(simulation.WorldState.World!.Levels);
        var originX = target.PositionX - 2;
        var originY = target.PositionY - 2;
        var megaWidth = (level.Width + 1) / 2;
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[(originY / 2) * megaWidth + originX / 2]);
        var generatedObject = Assert.Single(
            simulation.WorldState.Objects.Values,
            @object => @object.EntityId == 900);
        Assert.Equal(97U, generatedObject.ObjectId);
        Assert.Equal((target.PositionX - 2, target.PositionY - 2), (
            generatedObject.PositionX,
            generatedObject.PositionY));

        var snapshot = simulation.CaptureSnapshot(player, 2);
        var restoredWorld = new AuthoritativeWorld();
        restoredWorld.AddLevel(new AuthoritativeLevel(
            1,
            8,
            8,
            blockedCells: Enumerable.Range(0, 64)
                .Where(cell => cell % 8 % 2 != 0 || cell / 8 % 2 != 0)));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = restoredWorld,
                Content = content,
                NativeDunFragments = fragments,
                NativeDunFragmentObjects = fragmentObjects,
                NativeObjectTemplates = [template],
            });

        replacement.RestoreSnapshot(player, snapshot);

        var restoredLevel = Assert.Single(replacement.WorldState.World!.Levels);
        Assert.Equal((ushort)42, restoredLevel.NativeMegaTileLayer[(originY / 2) * megaWidth + originX / 2]);
        var restoredObject = Assert.Single(
            replacement.WorldState.Objects.Values,
            @object => @object.EntityId == 900);
        Assert.Equal(97U, restoredObject.ObjectId);
        Assert.Equal((target.PositionX - 2, target.PositionY - 2), (
            restoredObject.PositionX,
            restoredObject.PositionY));
    }

    [Fact]
    public void AppliesUniqueFragmentAfterNativeDunMonsterMaterialization()
    {
        var nativeMonsterLayer = new ushort[64];
        nativeMonsterLayer[4 * 8 + 4] = 1;
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            1,
            8,
            8,
            nativeMonsterLayer: nativeMonsterLayer));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2,
                2,
                "Skeleton",
                1,
                1,
                1,
                10,
                10,
                1,
                1,
                1,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "SkeletonMelee",
            },
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            monsterTypes,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
            ]),
            uniqueMonsters: new AuthoritativeUniqueMonsterCatalog([
                new AuthoritativeUniqueMonsterDefinition(0, 2, "Generated", "SkeletonMelee"),
            ]));
        var nativeDunMonsters = new AuthoritativeNativeDunMonsterCatalog([
            new AuthoritativeNativeDunMonsterDefinition(1, 2, 2),
        ]);
        var fragments = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                7,
                1,
                "fragment.dun",
                0,
                -2,
                -2,
                AnchorUniqueIdentity: "Generated"),
        ]);
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Content = content,
                NativeDunMonsters = nativeDunMonsters,
                NativeDunFragments = fragments,
                MonsterTypes = monsterTypes,
                MonsterSpawnPlanner = planner,
            });

        var target = Assert.Single(
            simulation.WorldState.CombatTargets.Values,
            candidate => candidate.UniqueIdentity == "Generated");
        Assert.True(target.IsUnique);
        var level = Assert.Single(simulation.WorldState.World!.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
    }

    [Fact]
    public void RejectsAnIncompleteNativeCollisionAssetPair()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                9,
                -2,
                -2,
                "fragment.til",
                null).Validate());
        Assert.Contains(".til/.sol asset pair", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMissingOrAmbiguousFragmentAnchors()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentDefinition(
            1,
            24,
            "fragment.dun",
            0,
            -2,
            -2).Validate());
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentDefinition(
            1,
            24,
            "fragment.dun",
            9,
            -2,
            -2,
            AnchorUniqueIdentity: "Snotspill").Validate());
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentDefinition(
            1,
            24,
            "fragment.dun",
            0,
            -2,
            -2,
            AnchorPositionX: 4).Validate());
        Assert.Throws<InvalidDataException>(() => new AuthoritativeNativeDunFragmentDefinition(
            1,
            24,
            "fragment.dun",
            9,
            -2,
            -2,
            AnchorPositionX: 4,
            AnchorPositionY: 4).Validate());
    }

    [Fact]
    public void RejectsAnOddAnchorRelativeDunOrigin()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(1, 24, "fragment.dun", 9, -1, -2),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(
            content,
            world,
            [new AuthoritativeWorldObject(9, 6003, 24, 4, 4)]));
        Assert.Contains("even d-piece origin", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsCoordinateAnchorsOutsideTheTargetLevel()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(
                1,
                24,
                "fragment.dun",
                0,
                0,
                0,
                AnchorPositionX: 8,
                AnchorPositionY: 4),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        var exception = Assert.Throws<InvalidDataException>(() => catalog.Apply(
            content,
            world,
            []));

        Assert.Contains("coordinate anchor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishesMultipleFragmentReplacementsAtomically()
    {
        var catalog = new AuthoritativeNativeDunFragmentCatalog([
            new AuthoritativeNativeDunFragmentDefinition(1, 24, "fragment.dun", 9, -2, -2),
            new AuthoritativeNativeDunFragmentDefinition(2, 24, "fragment.dun", 10, -1, -2),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(24, 8, 8));
        var content = ComposedContent.Compose(new ContentManifest("rules", "1", [
            new ContentPack("base", "1", [], assets: [
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        Assert.Throws<InvalidDataException>(() => catalog.Apply(
            content,
            world,
            [
                new AuthoritativeWorldObject(9, 6003, 24, 4, 4),
                new AuthoritativeWorldObject(10, 6004, 24, 4, 4),
            ]));

        var level = Assert.Single(world.Levels);
        Assert.Empty(level.NativeMegaTileLayer);
        Assert.Empty(level.NativeObjectLayer);
    }

    private static byte[] CreateDun(
        ushort tileId,
        ushort objectId = 0,
        ushort monsterId = 0)
    {
        var words = new ushort[19];
        words[0] = 1;
        words[1] = 1;
        words[2] = tileId;
        words[7] = monsterId;
        words[11] = objectId;
        var bytes = new byte[words.Length * sizeof(ushort)];
        for (var index = 0; index < words.Length; index++) {
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                words[index]);
        }
        return bytes;
    }

    private static byte[] CreateTil()
    {
        var bytes = new byte[43 * sizeof(ushort) * 4];
        var offset = 42 * sizeof(ushort) * 4;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(offset + sizeof(ushort), sizeof(ushort)),
            2);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(offset + sizeof(ushort) * 2, sizeof(ushort)),
            3);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(offset + sizeof(ushort) * 3, sizeof(ushort)),
            4);
        return bytes;
    }

    private static byte[] CreateSol()
    {
        var bytes = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        bytes[1] = (byte)AuthoritativeDunTileFlags.Solid;
        bytes[2] = (byte)AuthoritativeDunTileFlags.BlockMissile;
        bytes[3] = (byte)(AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockMissile);
        return bytes;
    }

    private static AuthoritativeNativeDunGeometryCatalog CreateExternalGeometry()
    {
        return AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            "native_dun_piece_definitions.tsv",
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + "42\t1\t2\t3\t4\n",
            "native_dun_tile_properties.tsv",
            "piece_id\tsolid\tblock_light\tblock_missile\n"
                + "1\ttrue\tfalse\tfalse\n"
                + "2\tfalse\tfalse\ttrue\n"
                + "3\ttrue\tfalse\ttrue\n"
                + "4\tfalse\tfalse\tfalse\n");
    }
}
