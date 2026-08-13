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
