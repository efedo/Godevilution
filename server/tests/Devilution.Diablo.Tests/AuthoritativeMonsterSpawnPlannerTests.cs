using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterSpawnPlannerTests
{
    [Fact]
    public void PlansWalkableLevelValidSpawnsDeterministically()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            1,
            5,
            5,
            blockedCells: [0, 1, 2]));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 3, 5, 1, 2),
            CreateType(3, 1, 2, 7, 9, 2, 4),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 2, 200, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var first = planner.Plan(1, seed: 0x1234, multiplayer: false, maximumMonsters: 8);
        var second = planner.Plan(1, seed: 0x1234, multiplayer: false, maximumMonsters: 8);

        Assert.Equal(8, first.Count);
        Assert.Equal(
            first.Select(spawn => (
                spawn.Definition.MonsterId,
                spawn.PositionX,
                spawn.PositionY,
                spawn.HitPoints,
                spawn.AttackDamage)),
            second.Select(spawn => (
                spawn.Definition.MonsterId,
                spawn.PositionX,
                spawn.PositionY,
                spawn.HitPoints,
                spawn.AttackDamage)));
        Assert.All(first, spawn => {
            Assert.True(world.IsWalkable(spawn.LevelId, spawn.PositionX, spawn.PositionY));
            Assert.InRange(spawn.HitPoints, spawn.Definition.MinimumHitPoints, spawn.Definition.MaximumHitPoints);
            Assert.InRange(spawn.AttackDamage, spawn.Definition.MinimumDamage, spawn.Definition.MaximumDamage);
        });
        Assert.Equal(8, first.Select(spawn => (spawn.PositionX, spawn.PositionY)).Distinct().Count());
    }

    [Fact]
    public void AppliesMultiplayerDensityAndRejectsUnknownLevels()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([CreateType(2, 1, 1, 1, 1, 1, 1)]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 4, 200, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        Assert.Equal(8, planner.Plan(1, 2, multiplayer: true, maximumMonsters: 20).Count);
        Assert.Throws<InvalidDataException>(
            () => planner.Plan(2, 2, multiplayer: false, maximumMonsters: 1));
    }

    [Fact]
    public void ReturnsNoSpawnsWhenNoTypeMatchesTheLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([CreateType(2, 3, 4, 1, 1, 1, 1)]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 4, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        Assert.Empty(planner.Plan(1, 2, multiplayer: false, maximumMonsters: 20));
    }

    [Fact]
    public void ExcludesReservedWalkableCellsFromDeterministicPlacements()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 3, 3));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 1, 1, 1, 1),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);
        var reserved = new HashSet<(int X, int Y)> { (0, 0), (1, 1) };

        var spawns = planner.Plan(
            1,
            seed: 7,
            multiplayer: false,
            maximumMonsters: 20,
            reservedPositions: reserved);

        Assert.Equal(7, spawns.Count);
        Assert.DoesNotContain(spawns, spawn => reserved.Contains((spawn.PositionX, spawn.PositionY)));
    }

    [Fact]
    public void ConvertsPlacementsIntoAuthoritativeCombatTargets()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 4, 4, 2, 3) with {
                ArmorClass = 7,
                Undead = true,
                FireResistance = 25,
                Knockback = true,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 4, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var target = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));

        Assert.Equal(AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId, target.EntityId);
        Assert.Equal(1U, target.LevelId);
        Assert.Equal(2U, target.MonsterId);
        Assert.Equal(4, target.HitPoints);
        Assert.Equal(4, target.MaxHitPoints);
        Assert.Equal(7, target.ArmorClass);
        Assert.Equal(25, target.FireResistance);
        Assert.True(target.IsUndead);
        Assert.True(target.HasKnockback);
        Assert.Equal(2, target.MinDamage);
        Assert.Equal(3, target.MaxDamage);
    }

    private static AuthoritativeMonsterTypeDefinition CreateType(
        uint monsterId,
        int minimumLevel,
        int maximumLevel,
        int minimumHitPoints,
        int maximumHitPoints,
        int minimumDamage,
        int maximumDamage)
    {
        return new AuthoritativeMonsterTypeDefinition(
            monsterId,
            monsterId,
            $"Monster {monsterId}",
            minimumLevel,
            maximumLevel,
            1,
            minimumHitPoints,
            maximumHitPoints,
            0,
            minimumDamage,
            maximumDamage,
            0,
            0,
            false,
            false,
            false);
    }
}
