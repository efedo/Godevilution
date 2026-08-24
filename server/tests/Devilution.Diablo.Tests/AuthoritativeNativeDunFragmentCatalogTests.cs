using System.Buffers.Binary;
using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeDunFragmentCatalogTests
{
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
                new ContentAsset("fragment.dun", CreateDun(42)),
            ]),
        ]));

        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                CombatTargets = [target],
                NativeDunFragments = catalog,
                Content = content,
            });

        var level = Assert.Single(simulation.WorldState.World!.Levels);
        Assert.Equal((ushort)42, level.NativeMegaTileLayer[1 * 4 + 1]);
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

    private static byte[] CreateDun(ushort tileId)
    {
        var words = new ushort[19];
        words[0] = 1;
        words[1] = 1;
        words[2] = tileId;
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
}
