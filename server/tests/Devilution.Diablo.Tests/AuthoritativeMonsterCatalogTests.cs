using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterCatalogTests
{
    [Fact]
    public void ReturnsTypesAvailableAtTheRequestedDungeonLevelInStableIdOrder()
    {
        var catalog = new AuthoritativeMonsterTypeCatalog([
            CreateType(20, minimumLevel: 3, maximumLevel: 8),
            CreateType(10, minimumLevel: 1, maximumLevel: 4),
            CreateType(30, minimumLevel: 8, maximumLevel: 16),
        ]);

        Assert.Equal(
            [10U, 20U],
            catalog.GetAvailableForDungeonLevel(3)
                .Select(definition => definition.MonsterId));
        Assert.Equal(
            [20U, 30U],
            catalog.GetAvailableForDungeonLevel(8)
                .Select(definition => definition.MonsterId));
        Assert.Empty(catalog.GetAvailableForDungeonLevel(17));
    }

    [Fact]
    public void RejectsNegativeDungeonLevels()
    {
        var catalog = new AuthoritativeMonsterTypeCatalog([CreateType(1, 1, 1)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => catalog.GetAvailableForDungeonLevel(-1));
    }

    [Fact]
    public void RejectsOutOfRangeAggroRadius()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { AggroRange = 65 },
        ]));
    }

    [Fact]
    public void RejectsOutOfRangeDeathAnimationTicks()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { DeathAnimationTicks = 1025 },
        ]));
    }

    [Fact]
    public void RejectsOutOfRangeExperienceReward()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { ExperienceReward = 1_000_001 },
        ]));
    }

    [Fact]
    public void RejectsOutOfRangeMonsterAccuracy()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { ToHit = 256 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { ToHitSpecial = 256 },
        ]));
    }

    [Fact]
    public void RejectsOutOfRangeMonsterIntelligence()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { Intelligence = 256 },
        ]));
    }

    [Fact]
    public void RejectsUnknownNativeAiIdentity()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with { NativeAi = "NotARealAi" },
        ]));
    }

    [Fact]
    public void RejectsInvalidFallenFearTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Fallen",
                FallenFearRadius = 4,
                FallenFearLevelOffset = 0,
                FallenFearMinimumTicks = 2,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                FallenFearRadius = 4,
            },
        ]));
    }

    [Fact]
    public void LoadsFallenFearTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tfallen_fear_radius\tfallen_fear_level_offset\tfallen_fear_minimum_ticks\n"
                + "2\t0\tFallen\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tFallen\t3\t7\t2\n");

        var fallen = Assert.Single(catalog.Definitions);
        Assert.Equal((3, 7, 2), (
            fallen.FallenFearRadius,
            fallen.FallenFearLevelOffset,
            fallen.FallenFearMinimumTicks));
    }

    [Fact]
    public void RejectsInvalidSneakTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Sneak",
                SneakDistanceBase = 5,
                SneakDistanceIntelligenceScale = 0,
                SneakRetreatReleaseDistance = 3,
                SneakRetreatTicks = 9,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                SneakDistanceBase = 5,
            },
        ]));
    }

    [Fact]
    public void LoadsSneakTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tsneak_distance_base\tsneak_distance_intelligence_scale\tsneak_retreat_release_distance\tsneak_retreat_ticks\n"
                + "2\t0\tSneak\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tSneak\t6\t2\t4\t11\n");

        var sneak = Assert.Single(catalog.Definitions);
        Assert.Equal((6, 2, 4, 11), (
            sneak.SneakDistanceBase,
            sneak.SneakDistanceIntelligenceScale,
            sneak.SneakRetreatReleaseDistance,
            sneak.SneakRetreatTicks));
    }

    [Fact]
    public void RejectsInvalidBatTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Bat",
                BatAttackChanceBase = 8,
                BatAttackChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                BatAttackChanceBase = 8,
            },
        ]));
    }

    [Fact]
    public void LoadsBatTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tbat_attack_chance_base\tbat_attack_chance_intelligence_scale\n"
                + "2\t0\tBat\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tBat\t9\t5\n");

        var bat = Assert.Single(catalog.Definitions);
        Assert.Equal((9, 5), (
            bat.BatAttackChanceBase,
            bat.BatAttackChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidSkeletonMeleeTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "SkeletonMelee",
                SkeletonMeleeDelayChanceBase = 35,
                SkeletonMeleeDelayChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                SkeletonMeleeDelayChanceBase = 35,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "SkeletonMelee",
                SkeletonMeleeAttackChanceBase = 20,
                SkeletonMeleeAttackChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                SkeletonMeleeAttackChanceBase = 20,
            },
        ]));
    }

    [Fact]
    public void LoadsSkeletonMeleeTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tskeleton_melee_delay_chance_base\tskeleton_melee_delay_chance_intelligence_scale\n"
                + "2\t0\tSkeleton\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\tSkeletonMelee\t32\t3\n");

        var skeleton = Assert.Single(catalog.Definitions);
        Assert.Equal((32, 3), (
            skeleton.SkeletonMeleeDelayChanceBase,
            skeleton.SkeletonMeleeDelayChanceIntelligenceScale));
    }

    [Fact]
    public void LoadsSkeletonMeleeAttackTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tskeleton_melee_attack_chance_base\tskeleton_melee_attack_chance_intelligence_scale\n"
                + "2\t0\tSkeleton\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\tSkeletonMelee\t21\t3\n");

        var skeleton = Assert.Single(catalog.Definitions);
        Assert.Equal((21, 3), (
            skeleton.SkeletonMeleeAttackChanceBase,
            skeleton.SkeletonMeleeAttackChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidGoatMeleeTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "GoatMelee",
                GoatMeleeMovementChanceBase = 28,
                GoatMeleeMovementChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                GoatMeleeMovementChanceBase = 28,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "GoatMelee",
                GoatMeleeAttackChanceBase = 23,
                GoatMeleeAttackChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                GoatMeleeAttackChanceBase = 23,
            },
        ]));
    }

    [Fact]
    public void LoadsGoatMeleeTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tgoat_melee_movement_chance_base\tgoat_melee_movement_chance_intelligence_scale\n"
                + "2\t0\tGoat\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tGoatMelee\t31\t3\n");

        var goat = Assert.Single(catalog.Definitions);
        Assert.Equal((31, 3), (
            goat.GoatMeleeMovementChanceBase,
            goat.GoatMeleeMovementChanceIntelligenceScale));
    }

    [Fact]
    public void LoadsGoatMeleeAttackTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tgoat_melee_attack_chance_base\tgoat_melee_attack_chance_intelligence_scale\n"
                + "2\t0\tGoat\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tGoatMelee\t24\t3\n");

        var goat = Assert.Single(catalog.Definitions);
        Assert.Equal((24, 3), (
            goat.GoatMeleeAttackChanceBase,
            goat.GoatMeleeAttackChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidFatTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Fat",
                FatMovementChanceBase = 20,
                FatMovementChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                FatMovementChanceBase = 20,
            },
        ]));
    }

    [Fact]
    public void LoadsFatTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tfat_movement_chance_base\tfat_movement_chance_intelligence_scale\n"
                + "2\t0\tOverlord\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tFat\t22\t5\n");

        var fat = Assert.Single(catalog.Definitions);
        Assert.Equal((22, 5), (
            fat.FatMovementChanceBase,
            fat.FatMovementChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidZombieTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Zombie",
                ZombieActionChanceBase = 10,
                ZombieActionChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                ZombieActionChanceBase = 10,
            },
        ]));
    }

    [Fact]
    public void LoadsZombieTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tzombie_action_chance_base\tzombie_action_chance_intelligence_scale\n"
                + "2\t0\tZombie\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\tZombie\t12\t3\n");

        var zombie = Assert.Single(catalog.Definitions);
        Assert.Equal((12, 3), (
            zombie.ZombieActionChanceBase,
            zombie.ZombieActionChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidZombieMovementTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Zombie",
                ZombieMovementDistanceMinimum = 2,
                ZombieMovementDistanceBase = 4,
                ZombieMovementDistanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                ZombieMovementDistanceMinimum = 2,
            },
        ]));
    }

    [Fact]
    public void LoadsZombieMovementTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tzombie_movement_distance_minimum\tzombie_movement_distance_base\tzombie_movement_distance_intelligence_scale\n"
                + "2\t0\tZombie\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\tZombie\t3\t5\t2\n");

        var zombie = Assert.Single(catalog.Definitions);
        Assert.Equal((3, 5, 2), (
            zombie.ZombieMovementDistanceMinimum,
            zombie.ZombieMovementDistanceBase,
            zombie.ZombieMovementDistanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidSkeletonKingSummonTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "SkeletonKing",
                SkeletonKingSummonMinimumDistance = 3,
                SkeletonKingSummonChanceBase = 35,
                SkeletonKingSummonChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                SkeletonKingSummonChanceBase = 35,
            },
        ]));
    }

    [Fact]
    public void LoadsSkeletonKingSummonTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tskeleton_king_summon_minimum_distance\tskeleton_king_summon_chance_base\tskeleton_king_summon_chance_intelligence_scale\n"
                + "2\t0\tSkeleton King\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t1\t0\tSkeletonKing\t4\t37\t5\n");

        var king = Assert.Single(catalog.Definitions);
        Assert.Equal((4, 37, 5), (
            king.SkeletonKingSummonMinimumDistance,
            king.SkeletonKingSummonChanceBase,
            king.SkeletonKingSummonChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidHorkDemonTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "HorkDemon",
                HorkDemonAttackChanceBase = 28,
                HorkDemonAttackChanceIntelligenceScale = 2,
                HorkDemonSpawnMinimumDistance = 3,
                HorkDemonSpawnChanceBase = 43,
                HorkDemonSpawnChanceIntelligenceScale = 2,
                HorkDemonMovementChanceBase = 33,
                HorkDemonMovementChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                HorkDemonAttackChanceBase = 28,
            },
        ]));
    }

    [Fact]
    public void LoadsHorkDemonTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\thork_demon_attack_chance_base\thork_demon_attack_chance_intelligence_scale\thork_demon_spawn_minimum_distance\thork_demon_spawn_chance_base\thork_demon_spawn_chance_intelligence_scale\thork_demon_movement_chance_base\thork_demon_movement_chance_intelligence_scale\n"
                + "2\t0\tHork Demon\t18\t19\t27\t120\t160\t80\t20\t35\t0\t0\t0\t0\t0\tHorkDemon\t29\t3\t4\t45\t3\t34\t3\n");

        var horkDemon = Assert.Single(catalog.Definitions);
        Assert.Equal((29, 3, 4, 45, 3, 34, 3), (
            horkDemon.HorkDemonAttackChanceBase,
            horkDemon.HorkDemonAttackChanceIntelligenceScale,
            horkDemon.HorkDemonSpawnMinimumDistance,
            horkDemon.HorkDemonSpawnChanceBase,
            horkDemon.HorkDemonSpawnChanceIntelligenceScale,
            horkDemon.HorkDemonMovementChanceBase,
            horkDemon.HorkDemonMovementChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidGargoyleTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Gargoyle",
                GargoyleSafeDistanceBase = 2,
                GargoyleSafeDistanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                GargoyleSafeDistanceBase = 2,
            },
        ]));
    }

    [Fact]
    public void LoadsGargoyleTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tgargoyle_safe_distance_base\tgargoyle_safe_distance_intelligence_scale\n"
                + "2\t0\tGargoyle\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tGargoyle\t3\t2\n");

        var gargoyle = Assert.Single(catalog.Definitions);
        Assert.Equal((3, 2), (
            gargoyle.GargoyleSafeDistanceBase,
            gargoyle.GargoyleSafeDistanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidSkeletonRangedTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "SkeletonRanged",
                SkeletonRangedRetreatMinimumDistance = 4,
                SkeletonRangedRetreatChanceBase = 13,
                SkeletonRangedRetreatChanceIntelligenceScale = 2,
                SkeletonRangedAttackChanceBase = 3,
                SkeletonRangedAttackChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                SkeletonRangedAttackChanceBase = 3,
            },
        ]));
    }

    [Fact]
    public void LoadsSkeletonRangedTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\tskeleton_ranged_retreat_minimum_distance\tskeleton_ranged_retreat_chance_base\tskeleton_ranged_retreat_chance_intelligence_scale\tskeleton_ranged_attack_chance_base\tskeleton_ranged_attack_chance_intelligence_scale\n"
                + "2\t0\tSkeleton Bow\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\tSkeletonRanged\t5\t15\t3\t4\t3\n");

        var skeletonRanged = Assert.Single(catalog.Definitions);
        Assert.Equal((5, 15, 3, 4, 3), (
            skeletonRanged.SkeletonRangedRetreatMinimumDistance,
            skeletonRanged.SkeletonRangedRetreatChanceBase,
            skeletonRanged.SkeletonRangedRetreatChanceIntelligenceScale,
            skeletonRanged.SkeletonRangedAttackChanceBase,
            skeletonRanged.SkeletonRangedAttackChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsInvalidRhinoTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                NativeAi = "Rhino",
                RhinoChargeMinimumDistance = 5,
                RhinoChargeChanceBase = 43,
                RhinoChargeChanceIntelligenceScale = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            CreateType(1, 1, 1) with {
                RhinoChargeChanceBase = 43,
            },
        ]));
    }

    [Fact]
    public void LoadsRhinoTuningFromMonsterTypes()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\trhino_charge_minimum_distance\trhino_charge_chance_base\trhino_charge_chance_intelligence_scale\n"
                + "2\t0\tHorned Demon\t1\t2\t1\t4\t7\t10\t2\t5\t5\t32\t0\t0\t0\tRhino\t6\t45\t3\n");

        var rhino = Assert.Single(catalog.Definitions);
        Assert.Equal((6, 45, 3), (
            rhino.RhinoChargeMinimumDistance,
            rhino.RhinoChargeChanceBase,
            rhino.RhinoChargeChanceIntelligenceScale));
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
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            false,
            false,
            false);
    }

    [Fact]
    public void LoadsNativeMonsterTypesInStableIdentityOrder()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tdeath_animation_ticks\texperience_reward\tto_hit\tto_hit_special\n"
                + "2\t0\tZombie\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\t16\t54\t10\t0\n"
                + "3\t1\tGhoul\t2\t3\t2\t7\t11\t10\t3\t10\t0\t0\t1\t1\t0\t16\t58\t10\t0\n");

        Assert.Equal([2U, 3U], catalog.Definitions.Select(definition => definition.MonsterId));
        var zombie = catalog.Definitions[0];
        Assert.Equal(0U, zombie.NativeMonsterId);
        Assert.Equal("Zombie", zombie.Name);
        Assert.Equal((4, 7, 10, 2, 5), (
            zombie.MinimumHitPoints,
            zombie.MaximumHitPoints,
            zombie.ArmorClass,
            zombie.MinimumDamage,
            zombie.MaximumDamage));
        Assert.Equal(6, zombie.AggroRange);
        Assert.Equal(16, zombie.DeathAnimationTicks);
        Assert.Equal(54U, zombie.ExperienceReward);
        Assert.Equal((10, 0), (zombie.ToHit, zombie.ToHitSpecial));
        Assert.Equal("Zombie", zombie.NativeAi);
        Assert.True(zombie.StoneCurseImmune);
        Assert.True(zombie.Undead);
        Assert.False(zombie.Demon);
        Assert.True(catalog.TryGetNative(0, out var nativeZombie));
        Assert.Same(zombie, nativeZombie);
    }

    [Fact]
    public void LoadsAndInheritsDemonCapability()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tdemon\n"
                + "2\t0\tDemon Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t1\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.True(Assert.Single(catalog.Targets).IsDemon);
    }

    [Fact]
    public void LoadsAndInheritsNativeAiIdentity()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tnative_ai\n"
                + "2\t0\tRanged Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\tSkeletonRanged\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal("SkeletonRanged", Assert.Single(catalog.Targets).NativeAi);
    }

    [Fact]
    public void LoadsAndInheritsNativeIntelligence()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tintelligence\n"
                + "2\t0\tIntelligent Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t3\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal(3, Assert.Single(catalog.Targets).Intelligence);
    }

    [Fact]
    public void LoadsAndInheritsAnimalCapability()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tanimal\n"
                + "2\t0\tFallen One\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t1\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.True(Assert.Single(catalog.Targets).IsAnimal);
    }

    [Fact]
    public void LoadsAndInheritsKnockbackCapability()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tknockback\n"
                + "2\t0\tKnockback Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t1\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.True(Assert.Single(catalog.Targets).HasKnockback);
    }

    [Fact]
    public void DoesNotReverseResolveAmbiguousNativeMonsterIdentities()
    {
        var catalog = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false),
            new AuthoritativeMonsterTypeDefinition(
                3, 0, "Ghoul", 2, 3, 2, 7, 11, 10, 3, 10, 0, 0, true, true, false),
        ]);

        Assert.False(catalog.TryGetNative(0, out _));
    }

    [Fact]
    public void LoadsNativeMonsterTypeResistances()
    {
        var catalog = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tfire_resistance\tlightning_resistance\tmagic_resistance\n"
                + "2\t0\tZombie\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t1\t1\t0\t0\t0\t100\n");

        var zombie = Assert.Single(catalog.Definitions);
        Assert.Equal((0, 0, 100), (
            zombie.FireResistance,
            zombie.LightningResistance,
            zombie.MagicResistance));
    }

    [Fact]
    public void LoadsAndInheritsAcidResistance()
    {
        var types = AuthoritativeMonsterTypeCatalog.LoadTsv(
            "monster_types.tsv",
            "monster_id\tnative_monster_id\tname\tminimum_dungeon_level\tmaximum_dungeon_level\tmonster_level\tminimum_hit_points\tmaximum_hit_points\tarmor_class\tminimum_damage\tmaximum_damage\tminimum_special_damage\tmaximum_special_damage\tstone_curse_immune\tundead\tberserk_immune\tacid_resistance\n"
                + "2\t0\tAcid Beast\t1\t2\t1\t4\t7\t10\t2\t5\t0\t0\t0\t0\t0\t100\n");

        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal(100, Assert.Single(catalog.Targets).AcidResistance);
    }

    [Fact]
    public void EncounterUsesTypeResistanceWhenNoInstanceOverrideExists()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false) {
                MagicResistance = 100,
            },
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal(100, Assert.Single(catalog.Targets).MagicResistance);
    }

    [Fact]
    public void ExplicitEncounterResistanceOverridesTypeResistance()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false) {
                MagicResistance = 100,
            },
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tmagic_resistance\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\t2\t25\n",
            monsterTypes: types);

        Assert.Equal(25, Assert.Single(catalog.Targets).MagicResistance);
    }

    [Fact]
    public void RejectsMonsterTypeResistanceOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false) {
                MagicResistance = 101,
            },
        ]));
    }

    [Fact]
    public void EncounterUsesTypeCombatAndCapabilityDefaultsWhenInstanceFieldsAreOmitted()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 3, 4, 7, 10, 2, 5, 6, 9, true, true, true),
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\n",
            monsterTypes: types);

        var target = Assert.Single(catalog.Targets);
        Assert.Equal(10, target.ArmorClass);
        Assert.Equal(3, target.MonsterLevel);
        Assert.Equal((2, 5, 6, 9), (
            target.MinDamage,
            target.MaxDamage,
            target.MinDamageSpecial,
            target.MaxDamageSpecial));
        Assert.True(target.StoneCurseImmune);
        Assert.True(target.IsUndead);
        Assert.True(target.BerserkImmune);
    }

    [Fact]
    public void EncounterRowsCanRequireAnExternalMonsterType()
    {
        var types = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2, 0, "Zombie", 1, 2, 1, 4, 7, 10, 2, 5, 0, 0, true, true, false),
        ]);
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t2\t1\t1\t0\t10\t10\t2\n",
            monsterTypes: types);

        Assert.Equal(2U, Assert.Single(catalog.Targets).MonsterId);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterCatalog.LoadTsv(
                "monsters.tsv",
                header + "9\t3\t1\t1\t0\t10\t10\t2\n",
                monsterTypes: types));
    }

    [Fact]
    public void LoadsExternalEncounterDefinitionsInStableEntityOrder()
    {
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\n"
                + "9\t1\t2\t4\t5\t20\t25\t3\n");

        var target = Assert.Single(catalog.Targets);
        Assert.Equal(9U, target.EntityId);
        Assert.Equal(1U, target.MonsterId);
        Assert.Equal(2U, target.LevelId);
        Assert.Equal(25, target.MaxHitPoints);
        Assert.False(target.StoneCurseImmune);
    }

    [Fact]
    public void LoadsAndValidatesStoneCurseImmunity()
    {
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tstone_curse_immune\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t1\t2\t4\t5\t20\t25\t3\t1\n");

        Assert.True(Assert.Single(catalog.Targets).StoneCurseImmune);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterCatalog.LoadTsv(
                "monsters.tsv",
                header + "9\t1\t2\t4\t5\t20\t25\t3\t2\n"));
    }

    [Fact]
    public void LoadsAndValidatesUndeadCapability()
    {
        const string header = "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tundead\n";
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            header + "9\t1\t2\t4\t5\t20\t25\t3\t1\n");

        Assert.True(Assert.Single(catalog.Targets).IsUndead);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterCatalog.LoadTsv(
                "monsters.tsv",
                header + "9\t1\t2\t4\t5\t20\t25\t3\t2\n"));
    }

    [Fact]
    public void RejectsDuplicateMonsterEntityIds()
    {
        var targets = new[] {
            new AuthoritativeCombatTarget(9, 0, 0, 10),
            new AuthoritativeCombatTarget(9, 1, 0, 10),
        };

        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterCatalog(targets));
    }

    [Fact]
    public void LoadsOptionalDropDefinitions()
    {
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1009\t6001\t25\n");

        var drop = Assert.Single(catalog.Targets).Drop;
        Assert.NotNull(drop);
        Assert.Equal(6001U, drop.ItemSeed);
    }

    [Fact]
    public void GeneratesCatalogBackedDropState()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ]);
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\tdrop_item_id\tdrop_item_level\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1009\t6001\t25\t7\t1\n",
            items);

        Assert.Equal(7, Assert.Single(catalog.Targets).Drop!.State.ItemIndex);
        Assert.Equal(4, catalog.Targets[0].Drop!.State.MinDamage);
    }

    [Fact]
    public void GeneratesUniqueCatalogBackedDropState()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ember Blade", 7, 1, 3650, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\tdrop_unique_item_id\tdrop_item_level\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1009\t6001\t3650\t1001\t1\n",
            items,
            uniques);

        Assert.Equal(1001, Assert.Single(catalog.Targets).Drop!.State.UniqueId);
    }

    [Fact]
    public void ExposesValidatedProceduralDropPolicyByMonsterType()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdrop_item_entity_id\tdrop_item_seed\tdrop_item_price\tdrop_item_id\tdrop_item_level\tdrop_only_good\tunique\tdiablo\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1009\t6001\t75\t7\t3\t1\t1\t1\n",
            items);

        var definition = Assert.Single(catalog.DropDefinitions);
        Assert.Equal(1U, definition.MonsterId);
        Assert.Equal(7U, definition.DropItemId);
        Assert.Equal(3, definition.ItemLevel);
        Assert.True(definition.OnlyGood);
        Assert.True(definition.MonsterUnique);
        Assert.True(definition.MonsterDiablo);
        Assert.True(catalog.TryGetDropDefinition(1, out var resolved));
        Assert.Same(definition, resolved);
    }

    [Fact]
    public void ExposesDiabloOnlyProceduralEncounterPolicyWithoutAnItemCatalog()
    {
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tlevel_id\tposition_x\tposition_y\thit_points\tmax_hit_points\tarmor_class\tdiablo\n"
                + "9\t1\t1\t1\t0\t10\t10\t2\t1\n");

        var definition = Assert.Single(catalog.DropDefinitions);
        Assert.False(definition.HasDrop);
        Assert.True(definition.MonsterDiablo);
    }
}
