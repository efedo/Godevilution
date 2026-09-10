using Devilution.Data;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeUniqueMonsterPlacementCatalogTests
{
    [Fact]
    public void LoadsValidatesAndMaterializesAuthoredGeneratedPlacement()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 6, 6));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 3) with { NativeAi = "SkeletonMelee" },
        ]);
        var uniqueMonsters = AuthoritativeUniqueMonsterCatalog.LoadTsv(
            "unique_monsters.tsv",
            "entity_id\tmonster_id\tidentity\tnative_ai\n"
                + "0\t2\tGenerated Defiler\tSkeletonMelee\n");
        var placements = AuthoritativeUniqueMonsterPlacementCatalog.LoadTsv(
            "unique_monster_placements.tsv",
            "placement_id\tunique_identity\tmonster_id\tlevel_id\tentity_id\tposition_x\tposition_y\tseed\tdirection\n"
                + "7\tGenerated Defiler\t2\t1\t77\t2\t3\t99\t3\n");
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            monsterTypes,
            generation,
            uniqueMonsters: uniqueMonsters);

        placements.Validate(world, monsterTypes, uniqueMonsters);
        var target = Assert.Single(placements.Materialize(
            planner,
            uniqueMonsters,
            multiplayer: false));

        Assert.Equal((77U, 1U, 2U, 2, 3), (
            target.EntityId,
            target.LevelId,
            target.MonsterId,
            target.PositionX,
            target.PositionY));
        Assert.True(target.IsUnique);
        Assert.Equal("Generated Defiler", target.UniqueIdentity);
        Assert.Equal(3, target.Direction);
    }

    [Fact]
    public void MaterializesValidatedLeashedPackMembers()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 12, 12));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 3) with { NativeAi = "SkeletonMelee", Intelligence = 17 },
            CreateType(3, 1, 3) with { NativeAi = "SkeletonMelee", Intelligence = 3 },
        ]);
        var uniqueMonsters = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 2, "Generated Defiler", "SkeletonMelee"),
        ]);
        var placements = new AuthoritativeUniqueMonsterPlacementCatalog([
            new AuthoritativeUniqueMonsterPlacementDefinition(
                7,
                "Generated Defiler",
                2,
                1,
                77,
                6,
                6,
                99),
        ]);
        var packs = new AuthoritativeUniqueMonsterPackCatalog([
            new AuthoritativeUniqueMonsterPackDefinition(
                3,
                7,
                3,
                3,
                AuthoritativeUniqueMonsterPackKind.Leashed),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            monsterTypes,
            generation,
            uniqueMonsters: uniqueMonsters);

        packs.Validate(placements, monsterTypes);
        var targets = placements.Materialize(
            planner,
            uniqueMonsters,
            multiplayer: false,
            packs);

        Assert.Equal(4, targets.Count);
        var leader = Assert.Single(targets, target => target.EntityId == 77);
        Assert.Equal(3, leader.PackSize);
        var members = targets.Where(target => target.EntityId != leader.EntityId).ToArray();
        Assert.Equal(3, members.Length);
        Assert.All(members, member => {
            Assert.False(member.IsUnique);
            Assert.Equal(leader.EntityId, member.LeaderEntityId);
            Assert.Equal(AuthoritativeMonsterLeaderRelation.Leashed, member.LeaderRelation);
            Assert.Equal(leader.Intelligence, member.Intelligence);
            Assert.Equal(member.MaxHitPoints, member.HitPoints);
            Assert.Equal(10, member.MaxHitPoints);
            Assert.True(Math.Max(
                Math.Abs(member.PositionX - leader.PositionX),
                Math.Abs(member.PositionY - leader.PositionY)) < AuthoritativeMonsterGroupUnity.ReunionDistance);
        });
        Assert.Equal(
            members.Length,
            members.Select(member => (member.PositionX, member.PositionY)).Distinct().Count());
    }

    [Fact]
    public void RejectsPackThatReferencesUnknownPlacementOrMonster()
    {
        var placements = new AuthoritativeUniqueMonsterPlacementCatalog([
            new AuthoritativeUniqueMonsterPlacementDefinition(
                7,
                "Generated Defiler",
                2,
                1,
                77,
                6,
                6,
                99),
        ]);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([CreateType(2, 1, 3)]);
        var unknownPlacement = new AuthoritativeUniqueMonsterPackCatalog([
            new AuthoritativeUniqueMonsterPackDefinition(1, 8, 2, 1),
        ]);
        Assert.Throws<InvalidDataException>(() => unknownPlacement.Validate(placements, monsterTypes));

        var unknownMonster = new AuthoritativeUniqueMonsterPackCatalog([
            new AuthoritativeUniqueMonsterPackDefinition(1, 7, 3, 1),
        ]);
        Assert.Throws<InvalidDataException>(() => unknownMonster.Validate(placements, monsterTypes));
    }

    [Fact]
    public void LoadsPackContractFromTsv()
    {
        var packs = AuthoritativeUniqueMonsterPackCatalog.LoadTsv(
            "unique_monster_packs.tsv",
            "pack_id\tplacement_id\tminion_monster_id\tpack_size\tpack_kind\n"
                + "3\t7\t2\t8\tLEASHED\n");

        var pack = Assert.Single(packs.Definitions);
        Assert.Equal((3U, 7U, 2U, 8), (
            pack.PackId,
            pack.PlacementId,
            pack.MinionMonsterId,
            pack.PackSize));
        Assert.Equal(
            AuthoritativeUniqueMonsterPackKind.Leashed,
            pack.Kind);
    }

    [Fact]
    public void RejectsPlacementThatOverlapsAnExistingEncounter()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([CreateType(2, 1, 1)]);
        var uniqueMonsters = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 2, "Generated Defiler"),
        ]);
        var placements = new AuthoritativeUniqueMonsterPlacementCatalog([
            new AuthoritativeUniqueMonsterPlacementDefinition(
                1,
                "Generated Defiler",
                2,
                1,
                77,
                1,
                1,
                99),
        ]);
        var existing = new AuthoritativeCombatTarget(
            12,
            1,
            1,
            10,
            levelId: 1,
            monsterId: 2);

        Assert.Throws<InvalidDataException>(() => placements.Validate(
            world,
            monsterTypes,
            uniqueMonsters,
            [existing]));
    }

    [Fact]
    public void RejectsPlacementThatReusesAnExistingEntityId()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([CreateType(2, 1, 1)]);
        var uniqueMonsters = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 2, "Generated Defiler"),
        ]);
        var placements = new AuthoritativeUniqueMonsterPlacementCatalog([
            new AuthoritativeUniqueMonsterPlacementDefinition(
                1,
                "Generated Defiler",
                2,
                1,
                77,
                1,
                1,
                99),
        ]);
        var existing = new AuthoritativeCombatTarget(
            77,
            2,
            2,
            10,
            levelId: 1,
            monsterId: 2);

        Assert.Throws<InvalidDataException>(() => placements.Validate(
            world,
            monsterTypes,
            uniqueMonsters,
            [existing]));
    }

    [Fact]
    public void RejectsPlacementThatReusesAReservedEntityId()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([CreateType(2, 1, 1)]);
        var uniqueMonsters = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 2, "Generated Defiler"),
        ]);
        var placements = new AuthoritativeUniqueMonsterPlacementCatalog([
            new AuthoritativeUniqueMonsterPlacementDefinition(
                1,
                "Generated Defiler",
                2,
                1,
                77,
                1,
                1,
                99),
        ]);

        Assert.Throws<InvalidDataException>(() => placements.Validate(
            world,
            monsterTypes,
            uniqueMonsters,
            reservedEntityIds: [77U]));
    }

    [Fact]
    public void RejectsPlacementOnAReservedWorldCell()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([CreateType(2, 1, 1)]);
        var uniqueMonsters = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(0, 2, "Generated Defiler"),
        ]);
        var placements = new AuthoritativeUniqueMonsterPlacementCatalog([
            new AuthoritativeUniqueMonsterPlacementDefinition(
                1,
                "Generated Defiler",
                2,
                1,
                77,
                1,
                1,
                99),
        ]);

        Assert.Throws<InvalidDataException>(() => placements.Validate(
            world,
            monsterTypes,
            uniqueMonsters,
            reservedPositions: [(1U, 1, 1)]));
    }

    [Fact]
    public void ComposedContentActivationMaterializesTheAuthoredPlacement()
    {
        var contentRoot = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(contentRoot, "server", "content", "base"));
        var uniqueTable = basePack.Tables.Single(
            table => table.SourcePath == "unique_monsters.tsv");
        var placementTable = basePack.Tables.Single(
            table => table.SourcePath == "unique_monster_placements.tsv");
        var packTable = TsvTable.Parse(
            "unique_monster_packs.tsv",
            "pack_id\tplacement_id\tminion_monster_id\tminion_monster_symbol\tpack_size\tpack_kind\n"
                + "1\t1\t1\tbase_monster\t2\tleashed");
        var uniqueHeader = uniqueTable.ToCanonicalTsv().Split('\n')[0];
        var placementHeader = placementTable.ToCanonicalTsv().Split('\n')[0];
        var overlay = new ContentPack(
            "placement.test",
            "0.1.0",
            [
                TsvTable.Parse(
                    uniqueTable.SourcePath,
                    uniqueHeader
                        + "\n0\t1\tbase_monster\tGenerated Zombie\tZombie\tnone\t0\tnone\tnone\tnone\tnone"),
                TsvTable.Parse(
                    placementTable.SourcePath,
                    placementHeader
                        + "\n1\tGenerated Zombie\t1\tbase_monster\t1\tcathedral\t100\t10\t10\t99\t0\t0\t0\t "),
                packTable,
            ],
            ["devilution.base"],
            [
                new ContentTableOverlay(
                    "unique_monsters.tsv",
                    ["identity"]),
                new ContentTableOverlay(
                    "unique_monster_placements.tsv",
                    ["placement_id"]),
            ],
            DiabloContentSchema.Current);
        var manifest = new ContentManifest(
            "placement-test",
            "1",
            [basePack, overlay]);

        var activation = DiabloModule.Instance.ContentBootstrap.Activate(
            ComposedContent.Compose(manifest),
            randomSeed: 99);
        var target = Assert.Single(
            activation.Simulation.WorldState.CombatTargets.Values,
            candidate => candidate.EntityId == 100);

        Assert.True(target.IsUnique);
        Assert.Equal("Generated Zombie", target.UniqueIdentity);
        Assert.Equal((1U, 10, 10), (
            target.LevelId,
            target.PositionX,
            target.PositionY));
        Assert.Single(activation.UniqueMonsterPlacements!.Definitions);
        Assert.Single(activation.UniqueMonsterPacks!.Definitions);
        Assert.Equal(2, activation.Simulation.WorldState.CombatTargets.Values.Count(
            candidate => candidate.LeaderEntityId == target.EntityId));
    }

    private static AuthoritativeMonsterTypeDefinition CreateType(
        uint monsterId,
        int minimumLevel,
        int maximumLevel)
    {
        return new AuthoritativeMonsterTypeDefinition(
            monsterId,
            monsterId,
            $"Monster {monsterId}",
            minimumLevel,
            maximumLevel,
            1,
            10,
            10,
            0,
            2,
            2,
            0,
            0,
            false,
            false,
            false);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
