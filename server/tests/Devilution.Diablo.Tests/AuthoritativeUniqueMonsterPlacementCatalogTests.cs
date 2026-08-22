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
}
