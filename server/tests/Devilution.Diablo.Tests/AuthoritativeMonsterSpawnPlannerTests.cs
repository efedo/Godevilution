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
                spawn.AttackDamage,
                spawn.Direction)),
            second.Select(spawn => (
                spawn.Definition.MonsterId,
                spawn.PositionX,
                spawn.PositionY,
                spawn.HitPoints,
                spawn.AttackDamage,
                spawn.Direction)));
        Assert.All(first, spawn => {
            Assert.True(world.IsWalkable(spawn.LevelId, spawn.PositionX, spawn.PositionY));
            Assert.InRange(spawn.HitPoints, spawn.Definition.MinimumHitPoints, spawn.Definition.MaximumHitPoints);
            Assert.InRange(spawn.AttackDamage, spawn.Definition.MinimumDamage, spawn.Definition.MaximumDamage);
            Assert.InRange(spawn.Direction, 0, 7);
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
    public void ExcludesSummonedAndBossIdentitiesFromOrdinarySpawns()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 5, 5));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 1, 1, 1, 1),
            CreateType(109, 1, 1, 1, 1, 1, 1) with { NativeAi = "Golem" },
            CreateType(110, 1, 1, 1, 1, 1, 1) with { Diablo = true },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var spawns = planner.Plan(1, seed: 17, multiplayer: false, maximumMonsters: 20);

        Assert.NotEmpty(spawns);
        Assert.All(spawns, spawn => {
            Assert.Equal(2U, spawn.Definition.MonsterId);
            Assert.NotEqual("Golem", spawn.Definition.NativeAi);
            Assert.False(spawn.Definition.Diablo);
        });
    }

    [Fact]
    public void ContentSpawnRoleOverridesLegacyIdentityFallback()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 5, 5));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 1, 1, 1, 1) with { Diablo = true },
            CreateType(3, 1, 1, 1, 1, 1, 1),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var policies = new AuthoritativeMonsterSpawnPolicyCatalog([
            new AuthoritativeMonsterSpawnPolicyDefinition(
                2,
                AuthoritativeMonsterSpawnRole.Ordinary),
            new AuthoritativeMonsterSpawnPolicyDefinition(
                3,
                AuthoritativeMonsterSpawnRole.Quest),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            types,
            generation,
            spawnPolicies: policies);

        var spawns = planner.Plan(1, seed: 17, multiplayer: false, maximumMonsters: 8);

        Assert.NotEmpty(spawns);
        Assert.All(spawns, spawn => Assert.Equal(2U, spawn.Definition.MonsterId));
    }

    [Fact]
    public void HalvesGeneratedMonsterHealthInSinglePlayer()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 8, 10, 1, 1),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 4, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var singlePlayer = Assert.Single(
            planner.PlanCombatTargets(1, 3, multiplayer: false, maximumMonsters: 1));
        var multiplayer = Assert.Single(
            planner.PlanCombatTargets(1, 3, multiplayer: true, maximumMonsters: 1));

        Assert.Equal(
            (multiplayer.HitPoints / 2, multiplayer.MaxHitPoints / 2),
            (singlePlayer.HitPoints, singlePlayer.MaxHitPoints));
        Assert.InRange(multiplayer.HitPoints, 8, 10);
        Assert.Equal(10, multiplayer.MaxHitPoints);
    }

    [Fact]
    public void InitializesNativeGargoyleSpecialLock()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(81, 1, 1, 8, 8, 1, 1) with { NativeAi = "Gargoyle" },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var target = Assert.Single(
            planner.PlanCombatTargets(1, 3, multiplayer: false, maximumMonsters: 1));

        Assert.True(target.GargoyleSpecialActive);
    }

    [Fact]
    public void ConsumesNativeStandAnimationRandomBeforeProceduralHealth()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 1, 1));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 1, 100, 1, 1) with {
                StandAnimationFrames = 8,
                StandAnimationTicksPerFrame = 4,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var target = Assert.Single(
            planner.PlanCombatTargets(1, 3, multiplayer: true, maximumMonsters: 1));
        var random = new AuthoritativeLegacyRandom(3 ^ 1U);
        random.Next(8);
        random.Next(4 - 1);
        random.Next(8 - 1);
        var expectedHitPoints = random.Next(100) + 1;
        var expectedMonsterItemSeed = random.AdvanceSeed();
        var expectedMonsterAiSeed = random.AdvanceSeed();

        Assert.Equal(expectedHitPoints, target.HitPoints);
        Assert.Equal(expectedMonsterItemSeed, target.MonsterItemSeed);
        Assert.Equal(expectedMonsterAiSeed, target.MonsterAiSeed);
    }

    [Fact]
    public void ReservesNativeMonsterSlotsForRuntimeSummons()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 40, 40));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 1, 1, 1, 1),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var spawns = planner.Plan(
            1,
            seed: 2,
            multiplayer: false,
            maximumMonsters: 500);

        Assert.Equal(
            AuthoritativeMonsterSpawnPlanner.NativeOrdinaryLevelMonsterCapacity,
            spawns.Count);
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
                NativeAi = "SkeletonRanged",
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 4, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var spawn = Assert.Single(
            planner.Plan(1, seed: 4, multiplayer: false, maximumMonsters: 1));
        var target = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));

        Assert.Equal(AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId, target.EntityId);
        Assert.Equal(1U, target.LevelId);
        Assert.Equal(2U, target.MonsterId);
        Assert.Equal(2, target.HitPoints);
        Assert.Equal(2, target.MaxHitPoints);
        Assert.Equal(7, target.ArmorClass);
        Assert.Equal(25, target.FireResistance);
        Assert.Equal(6, target.AggroRange);
        Assert.True(target.IsUndead);
        Assert.True(target.BerserkImmune);
        Assert.True(target.HasKnockback);
        Assert.Equal(9, target.DeathAnimationTicks);
        Assert.Equal(77U, target.ExperienceReward);
        Assert.Equal((61, 43), (target.ToHit, target.ToHitSpecial));
        Assert.Equal("SkeletonRanged", target.NativeAi);
        Assert.Equal(2, target.MinDamage);
        Assert.Equal(3, target.MaxDamage);
        Assert.Equal(spawn.Direction, target.Direction);
    }

    [Fact]
    public void AppliesUniqueIdentitySelectorToGeneratedUniqueEncounter()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 4, 4, 2, 3) with { NativeAi = "SkeletonMelee" },
        ]);
        var monsters = new AuthoritativeMonsterCatalog(
            [],
            [new AuthoritativeMonsterDropDefinition(2, 0, 0, 0, false, true, false)]);
        var uniqueMonsters = new AuthoritativeUniqueMonsterCatalog([
            new AuthoritativeUniqueMonsterDefinition(
                0,
                2,
                "Generated Defiler",
                "SkeletonMelee"),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            types,
            generation,
            monsterCatalog: monsters,
            uniqueMonsters: uniqueMonsters);

        var target = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));

        Assert.True(target.IsUnique);
        Assert.Equal("Generated Defiler", target.UniqueIdentity);
    }

    [Fact]
    public void LeavesGeneratedUniqueSelectorUnmatchedUntilItsEncounterAppears()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 4, 4, 2, 3),
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            types,
            generation,
            uniqueMonsters: new AuthoritativeUniqueMonsterCatalog([
                new AuthoritativeUniqueMonsterDefinition(0, 99, "Absent", "SkeletonMelee"),
            ]));

        var target = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));

        Assert.Equal(2U, target.MonsterId);
        Assert.Empty(target.UniqueIdentity);
    }

    [Fact]
    public void CreatesOnlyLevelValidSkeletonMeleeSummons()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 4, 6, 2, 3) with { NativeAi = "SkeletonMelee" },
            CreateType(3, 1, 1, 8, 8, 4, 4) with { NativeAi = "Zombie" },
            CreateType(4, 2, 2, 9, 9, 5, 5) with { NativeAi = "SkeletonMelee" },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var summon = planner.CreateSkeletonSummon(
            levelId: 1,
            seed: 7,
            positionX: 2,
            positionY: 2,
            entityId: 99,
            direction: 5);

        Assert.NotNull(summon);
        Assert.Equal(99U, summon!.EntityId);
        Assert.Equal(2U, summon.MonsterId);
        Assert.Equal("SkeletonMelee", summon.NativeAi);
        Assert.Equal(5, summon.Direction);
        Assert.InRange(summon.HitPoints, 2, 3);
        Assert.InRange(summon.MinDamage, 2, 3);
        Assert.InRange(summon.MaxDamage, 2, 3);
        Assert.Null(planner.CreateSkeletonSummon(
            levelId: 2,
            seed: 7,
            positionX: 2,
            positionY: 2,
            entityId: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => planner.CreateSkeletonSummon(
            levelId: 1,
            seed: 7,
            positionX: 2,
            positionY: 2,
            entityId: 101,
            direction: 8));
    }

    [Fact]
    public void SkeletonSummonsConsumeNativeSelectionInitializationAndSeedDraws()
    {
        const uint seed = 0x12345678;
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 10, 20, 2, 4) with {
                StandAnimationFrames = 8,
                StandAnimationTicksPerFrame = 4,
                NativeAi = "SkeletonMelee",
            },
            CreateType(3, 1, 1, 30, 40, 5, 7) with {
                StandAnimationFrames = 6,
                StandAnimationTicksPerFrame = 3,
                NativeAi = "SkeletonMelee",
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var expected = new AuthoritativeLegacyRandom(seed);
        var summonSeed = expected.State;
        var candidates = types.GetAvailableForDungeonLevel(1)
            .OrderBy(definition => definition.MonsterId)
            .ToArray();
        var expectedDefinition = candidates[expected.Next(candidates.Length)];
        expected.Next(expectedDefinition.StandAnimationTicksPerFrame - 1);
        expected.Next(expectedDefinition.StandAnimationFrames - 1);
        var expectedHitPoints = expected.Next(
            expectedDefinition.MaximumHitPoints
                - expectedDefinition.MinimumHitPoints + 1)
            + expectedDefinition.MinimumHitPoints;
        var expectedItemSeed = expected.AdvanceSeed();
        var expectedAiSeed = expected.AdvanceSeed();

        var summon = planner.CreateSkeletonSummon(
            levelId: 1,
            seed: seed,
            positionX: 2,
            positionY: 2,
            entityId: 99,
            multiplayer: true);

        Assert.NotNull(summon);
        Assert.Equal(expectedDefinition.MonsterId, summon!.MonsterId);
        Assert.Equal(expectedHitPoints, summon.HitPoints);
        var expectedAttackDamage = expectedDefinition.MinimumDamage
            + (int)((summonSeed / 0x100U)
                % (uint)(expectedDefinition.MaximumDamage
                    - expectedDefinition.MinimumDamage + 1));
        Assert.Equal(expectedAttackDamage, summon.AttackDamage);
        Assert.Equal(expectedItemSeed, summon.MonsterItemSeed);
        Assert.Equal(expectedAiSeed, summon.MonsterAiSeed);
    }

    [Fact]
    public void CreatesSkeletonMeleeSummonsFromCompleteTuningUnderAnAlternateLabel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(5, 1, 1, 4, 4, 2, 2) with {
                NativeAi = "Fallen",
                SkeletonMeleeDelayChanceBase = 35,
                SkeletonMeleeDelayChanceIntelligenceScale = 4,
                SkeletonMeleeAttackChanceBase = 20,
                SkeletonMeleeAttackChanceIntelligenceScale = 2,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var summon = planner.CreateSkeletonSummon(
            levelId: 1,
            seed: 7,
            positionX: 2,
            positionY: 2,
            entityId: 99,
            direction: 3);

        Assert.NotNull(summon);
        Assert.Equal(5U, summon!.MonsterId);
        Assert.Equal("Fallen", summon.NativeAi);
    }

    [Fact]
    public void UsesExplicitSkeletonSummonCandidateBeforeNativeAiFallback()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 4, 4, 2, 2) with {
                NativeAi = "SkeletonMelee",
                SkeletonSummonCandidate = false,
            },
            CreateType(3, 1, 1, 4, 4, 3, 3) with {
                NativeAi = "Zombie",
                SkeletonSummonCandidate = true,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var summon = planner.CreateSkeletonSummon(
            levelId: 1,
            seed: 0,
            positionX: 2,
            positionY: 2,
            entityId: 99);

        Assert.NotNull(summon);
        Assert.Equal(3U, summon!.MonsterId);
        Assert.Equal("Zombie", summon.NativeAi);
    }

    [Fact]
    public void CreatesSummonsFromValidatedNativeMonsterIdentity()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(18, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                119,
                117,
                "Hork Spawn",
                18,
                19,
                22,
                30,
                30,
                25,
                10,
                25,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "SkeletonMelee",
                AggroRange = 6,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(18, 18, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(world, types, generation);

        var summon = planner.CreateNativeMonsterSummon(
            levelId: 18,
            nativeMonsterId: 117,
            seed: 7,
            positionX: 2,
            positionY: 2,
            entityId: 99,
            direction: 3);

        Assert.NotNull(summon);
        Assert.Equal((119U, 117U), (summon!.MonsterId, types.Definitions[0].NativeMonsterId));
        Assert.Equal("SkeletonMelee", summon.NativeAi);
        Assert.Equal(3, summon.Direction);
        Assert.Equal((15, 10), (summon.HitPoints, summon.MinDamage));
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

    [Fact]
    public void GeneratesDynamicSpellBookForAProceduralWorldDrop()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var types = new AuthoritativeMonsterTypeCatalog([
            CreateType(2, 1, 1, 4, 4, 2, 3),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);
        var monsters = new AuthoritativeMonsterCatalog(
            [],
            [new AuthoritativeMonsterDropDefinition(2, 114, 0, 1, false, false, false)]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 4, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            types,
            generation,
            items,
            monsters,
            books);

        var first = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));
        var second = Assert.Single(
            planner.PlanCombatTargets(1, seed: 4, multiplayer: false, maximumMonsters: 1));

        Assert.NotNull(first.Drop);
        Assert.Equal(first.Drop.State, second.Drop!.State);
        Assert.Equal(7, first.Drop!.State.SpellId);
        Assert.Equal(DiabloItemActionKind.ReadBook, first.Drop.State.ItemActionKind);
        Assert.Equal("Book of Firebolt", first.Drop.State.BaseName);
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
