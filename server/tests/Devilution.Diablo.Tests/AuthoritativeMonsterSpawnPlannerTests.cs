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
                BerserkImmune = true,
                FireResistance = 25,
                Knockback = true,
                DeathAnimationTicks = 9,
                ExperienceReward = 77,
                ToHit = 61,
                ToHitSpecial = 43,
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
        Assert.Equal(6, target.AggroRange);
        Assert.True(target.IsUndead);
        Assert.True(target.BerserkImmune);
        Assert.True(target.HasKnockback);
        Assert.Equal(9, target.DeathAnimationTicks);
        Assert.Equal(77U, target.ExperienceReward);
        Assert.Equal((61, 43), (target.ToHit, target.ToHitSpecial));
        Assert.Equal(2, target.MinDamage);
        Assert.Equal(3, target.MaxDamage);
    }

    [Fact]
    public void CarriesValidatedStaticDropPolicyIntoGeneratedTargets()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 4, 4, 2, 3),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                7,
                1,
                75,
                75,
                4,
                8,
                0,
                20,
                20,
                1,
                1,
                true),
        ]);
        var monsters = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\tdrop_item_id\tdrop_item_level\tdiablo\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\t1009\t6001\t25\t7\t1\t1\n",
            items,
            monsterTypes: types);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 4, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            types,
            generation,
            items,
            monsters);

        var first = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));
        var second = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));

        Assert.NotNull(first.Drop);
        Assert.Equal(
            AuthoritativeMonsterSpawnPlanner.FirstGeneratedDropEntityId,
            first.Drop!.EntityId);
        Assert.NotEqual(0U, first.Drop.ItemSeed);
        Assert.Equal(7, first.Drop.State.ItemIndex);
        Assert.Equal(first.Drop.State, second.Drop!.State);
        Assert.Equal(first.Drop.ItemSeed, second.Drop.ItemSeed);
        Assert.Equal(first.Drop.State.Value, checked((int)first.Drop.Price));
        Assert.True(first.IsDiablo);
        Assert.True(second.IsDiablo);
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
