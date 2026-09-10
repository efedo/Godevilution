using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterMissileCatalogTests
{
    [Fact]
    public void RejectsPartialMonsterMissileProductionSchema()
    {
        var table = TsvTable.Parse(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "GoatRanged\tArrow\tMonsterArrow\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeMonsterMissileCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsNativeMissileMappingsInStableIdentityOrder()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "Succubus\tBloodStar\tBloodStar\n"
                + "GoatRanged\tArrow\tTargeted\n");

        Assert.Equal(["GoatRanged", "Succubus"], catalog.Definitions.Select(definition => definition.NativeAi));
        Assert.True(catalog.TryGet("Succubus", out var succubus));
        Assert.Equal("BloodStar", succubus.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, succubus.ProjectileBehavior);
        Assert.Equal(AuthoritativeMonsterRangedAttackMode.Ranged, succubus.AttackMode);
        Assert.Equal(100, succubus.AvoidanceProbabilityPercent);
        Assert.True(catalog.TryGetByMissileId("BloodStar", out var byIdentity));
        Assert.Equal("Succubus", byIdentity.NativeAi);
        Assert.False(catalog.TryGetByMissileId("Unknown", out _));
    }

    [Fact]
    public void ValidatesReferencedMissileIdentitiesAgainstNativeCatalog()
    {
        var mappings = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "GoatRanged\tArrow\tMonsterArrow\n");
        var native = new AuthoritativeNativeMissileCatalog([
            new("Arrow", "AddArrow", "ProcessArrow", "", "", "Arrow", "Physical,Arrow", "Blockable", null),
        ]);
        mappings.ValidateMissileReferences(native);

        var missing = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "GoatRanged\tMissing\tMonsterArrow\n");
        Assert.Throws<InvalidDataException>(() => missing.ValidateMissileReferences(native));
    }

    [Fact]
    public void LoadsOptionalProjectileDamageRandomRange()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tprojectile_damage_random_range\n"
                + "Bat\tLightning\tMonsterMagic\t4\n");

        Assert.Equal(4, Assert.Single(catalog.Definitions).ProjectileDamageRandomRange);
    }

    [Fact]
    public void LoadsOptionalInitialTravelQuanta()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tinitial_travel_quanta\n"
                + "Magma\tMagmaBall\tMonsterMagic\t3\n");

        Assert.Equal(3, Assert.Single(catalog.Definitions).InitialTravelQuanta);
    }

    [Fact]
    public void LoadsOptionalProjectileCount()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tprojectile_count\n"
                + "Counselor\tChargedBolt\tMonsterChargedBolt\t3\n");

        Assert.Equal(3, Assert.Single(catalog.Definitions).ProjectileCount);
    }

    [Fact]
    public void LoadsOptionalSnakeNonPrimaryImpactBoundary()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tsuppress_non_primary_player_impact\n"
                + "Snake\tRhino\tSnakeCharge\ttrue\n");

        Assert.True(Assert.Single(catalog.Definitions).SuppressNonPrimaryPlayerImpact);
    }

    [Fact]
    public void RejectsSnakeNonPrimaryImpactBoundaryOnOtherBehaviors()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "GoatRanged",
                "Arrow",
                AuthoritativeProjectileBehavior.MonsterArrow) {
                SuppressNonPrimaryPlayerImpact = true,
            },
        ]));
    }

    [Fact]
    public void LoadsValidatedAdjacentAttackBand()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\t"
                + "ranged_avoidance_adjacent_attack_chance_base\t"
                + "ranged_avoidance_adjacent_attack_chance_intelligence_scale\t"
                + "ranged_avoidance_adjacent_attack_chance_divisor\n"
                + "Magma\tMagmaBall\tMonsterMagic\tRangedAvoidance\t"
                + "3000\t700\t2\n");

        var definition = Assert.Single(catalog.Definitions);
        Assert.Equal((3000, 700, 2), (
            definition.RangedAvoidanceAdjacentAttackChanceBase,
            definition.RangedAvoidanceAdjacentAttackChanceIntelligenceScale,
            definition.RangedAvoidanceAdjacentAttackChanceDivisor));
    }

    [Fact]
    public void ResolvesSymbolicMissileSpellIdsThroughStableContentIds()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "fireball", 12),
        ]);
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tspell_id\tspell_symbol\n"
                + "Counselor\tFireball\tFireball\t12\tfireball\n",
            stableIds);

        Assert.Equal(12U, Assert.Single(catalog.Definitions).SpellId);
    }

    [Fact]
    public void RejectsSymbolicMissileSpellIdNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "fireball", 12),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tspell_id\tspell_symbol\n"
                + "Counselor\tFireball\tFireball\t3\tfireball\n",
            stableIds));
    }

    [Fact]
    public void LoadsValidatedAvoidanceModeAndProbability()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
                "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tavoidance_probability_percent\tspell_id\tspawn_monster_id\tdamage_type\tprojectile_speed\tprojectile_lifetime\tprojectile_lifetime_base\tprojectile_lifetime_intelligence_scale\tpuddle_duration_random_range\tpuddle_duration_base\tpuddle_duration_intelligence_scale\tpuddle_damage_minimum_monster_level\tpuddle_damage_low\tpuddle_damage_high\tranged_avoidance_minimum_distance\tranged_avoidance_far_distance\tranged_avoidance_chance_base\tranged_avoidance_chance_intelligence_scale\tranged_avoidance_far_intelligence_offset\tprojectile_damage\tpuddle_impact_lifetime\tprojectile_impact_lifetime\tranged_avoidance_movement_chance_modulo\n"
                + "Acid\tAcid\tAcid\tRangedAvoidance\t50\t1\t117\tAcid\t8\t9\t20\t5\t15\t40\t40\t2\t1\t2\t2\t3\t500\t500\t1\t40\t4\t6\t8\n");

        Assert.True(catalog.TryGet("Acid", out var acid));
        Assert.Equal(AuthoritativeMonsterRangedAttackMode.RangedAvoidance, acid.AttackMode);
        Assert.Equal(50, acid.AvoidanceProbabilityPercent);
        Assert.Equal(1U, acid.SpellId);
        Assert.Equal(117U, acid.SpawnMonsterId);
        Assert.Equal(AuthoritativeDamageType.Acid, acid.DamageType);
        Assert.Equal(8, acid.ProjectileSpeed);
        Assert.Equal(9, acid.ProjectileLifetime);
        Assert.Equal(20, acid.ProjectileLifetimeBase);
        Assert.Equal(5, acid.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(15, acid.PuddleDurationRandomRange);
        Assert.Equal(40, acid.PuddleDurationBase);
        Assert.Equal(40, acid.PuddleDurationIntelligenceScale);
        Assert.Equal(2, acid.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(1, acid.PuddleDamageLow);
        Assert.Equal(2, acid.PuddleDamageHigh);
        Assert.Equal(40, acid.ProjectileDamage);
        Assert.Equal(4, acid.PuddleImpactLifetime);
        Assert.Equal(6, acid.ProjectileImpactLifetime);
        Assert.Equal((2, 3, 500, 500, 1), (
            acid.RangedAvoidanceMinimumDistance,
            acid.RangedAvoidanceFarDistance,
            acid.RangedAvoidanceChanceBase,
            acid.RangedAvoidanceChanceIntelligenceScale,
            acid.RangedAvoidanceFarIntelligenceOffset));
        Assert.Equal(8, acid.RangedAvoidanceMovementChanceModulo);
    }

    [Fact]
    public void LoadsValidatedRangedRetreatTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tranged_retreat_minimum_distance\tranged_retreat_chance_base\tranged_retreat_chance_intelligence_scale\n"
                + "AcidUnique\tAcid\tAcid\tRanged\t5\t70\t10\n");

        Assert.True(catalog.TryGet("AcidUnique", out var acidUnique));
        Assert.Equal((5, 70, 10), (
            acidUnique.RangedRetreatMinimumDistance,
            acidUnique.RangedRetreatChanceBase,
            acidUnique.RangedRetreatChanceIntelligenceScale));
    }

    [Fact]
    public void LoadsRangedAvoidanceMovementModuloWithoutOtherAvoidanceTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tranged_avoidance_movement_chance_modulo\n"
                + "Magma\tMagmaBall\tMonsterMagic\tRangedAvoidance\t5\n");

        Assert.True(catalog.TryGet("Magma", out var magma));
        Assert.Equal(5, magma.RangedAvoidanceMovementChanceModulo);
        Assert.Equal(0, magma.RangedAvoidanceMinimumDistance);
    }

    [Fact]
    public void LoadsRangedAvoidanceMovementBandTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tranged_avoidance_movement_chance_base\tranged_avoidance_movement_chance_intelligence_scale\tranged_avoidance_movement_fresh_chance_bonus\n"
                + "Magma\tMagmaBall\tMonsterMagic\tRangedAvoidance\t50\t10\t30\n");

        Assert.True(catalog.TryGet("Magma", out var magma));
        Assert.Equal((50, 10, 30), (
            magma.RangedAvoidanceMovementChanceBase,
            magma.RangedAvoidanceMovementChanceIntelligenceScale,
            magma.RangedAvoidanceMovementFreshChanceBonus));
    }

    [Fact]
    public void LoadsValidatedRangedPostAttackDelayTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tranged_delay_base\tranged_delay_random_range\n"
                + "Succubus\tBloodStar\tBloodStar\t3\t7\n");

        var succubus = Assert.Single(catalog.Definitions);
        Assert.Equal(3, succubus.RangedDelayBase);
        Assert.Equal(7, succubus.RangedDelayRandomRange);
    }

    [Fact]
    public void LoadsValidatedRangedAvoidanceRecoveryDelayTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tranged_avoidance_delay_base\tranged_avoidance_delay_random_range\n"
                + "Acid\tAcid\tAcid\tRangedAvoidance\t7\t11\n");

        var acid = Assert.Single(catalog.Definitions);
        Assert.Equal(7, acid.RangedAvoidanceDelayBase);
        Assert.Equal(11, acid.RangedAvoidanceDelayRandomRange);
    }

    [Fact]
    public void LoadsValidatedHorkDemonRecoveryDelayTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tspawn_monster_id\thork_demon_delay_base\thork_demon_delay_random_range\n"
                + "HorkDemon\tHorkSpawn\tHorkSpawn\t117\t12\t13\n");

        var horkDemon = Assert.Single(catalog.Definitions);
        Assert.Equal(12, horkDemon.HorkDemonDelayBase);
        Assert.Equal(13, horkDemon.HorkDemonDelayRandomRange);
    }

    [Fact]
    public void LoadsValidatedSnakeDelayBands()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tsnake_adjacent_delay_base\tsnake_adjacent_delay_intelligence_scale\tsnake_adjacent_delay_random_range\tsnake_distant_delay_base\tsnake_distant_delay_intelligence_scale\tsnake_distant_delay_random_range\n"
                + "Snake\tRhino\tSnakeCharge\t11\t2\t12\t16\t3\t14\n");

        var snake = Assert.Single(catalog.Definitions);
        Assert.Equal((11, 2, 12, 16, 3, 14), (
            snake.SnakeAdjacentDelayBase,
            snake.SnakeAdjacentDelayIntelligenceScale,
            snake.SnakeAdjacentDelayRandomRange,
            snake.SnakeDistantDelayBase,
            snake.SnakeDistantDelayIntelligenceScale,
            snake.SnakeDistantDelayRandomRange));
    }

    [Fact]
    public void LoadsValidatedSnakeDistantDelayAdmissionChance()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tsnake_distant_delay_chance_base\tsnake_distant_delay_chance_intelligence_scale\n"
                + "Snake\tRhino\tSnakeCharge\t41\t3\n");

        var snake = Assert.Single(catalog.Definitions);
        Assert.Equal((41, 3), (
            snake.SnakeDistantDelayChanceBase,
            snake.SnakeDistantDelayChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsPartialSnakeDistantDelayAdmissionChance()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Snake",
                "Rhino",
                AuthoritativeProjectileBehavior.SnakeCharge,
                SnakeDistantDelayChanceBase: 41),
        ]));
    }

    [Fact]
    public void LoadsValidatedSnakeAdjacentAttackAdmissionChance()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tsnake_adjacent_attack_chance_base\tsnake_adjacent_attack_chance_intelligence_scale\n"
                + "Snake\tRhino\tSnakeCharge\t20\t1\n");

        var snake = Assert.Single(catalog.Definitions);
        Assert.Equal((20, 1), (
            snake.SnakeAdjacentAttackChanceBase,
            snake.SnakeAdjacentAttackChanceIntelligenceScale));
    }

    [Fact]
    public void RejectsPartialSnakeAdjacentAttackAdmissionChance()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Snake",
                "Rhino",
                AuthoritativeProjectileBehavior.SnakeCharge,
                SnakeAdjacentAttackChanceBase: 20),
        ]));
    }

    [Fact]
    public void RejectsSnakeAdjacentAttackAdmissionChanceOnNonSnakeMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "GoatRanged",
                "Arrow",
                AuthoritativeProjectileBehavior.MonsterArrow,
                SnakeAdjacentAttackChanceBase: 20,
                SnakeAdjacentAttackChanceIntelligenceScale: 1),
        ]));
    }

    [Fact]
    public void LoadsValidatedMegaDelayTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmega_delay_base\tmega_delay_random_range\n"
                + "Mega\tInfernoControl\tInfernoController\t6\t12\n");

        var mega = Assert.Single(catalog.Definitions);
        Assert.Equal(6, mega.MegaDelayBase);
        Assert.Equal(12, mega.MegaDelayRandomRange);
    }

    [Fact]
    public void RejectsRangedPostAttackDelayOnAvoidanceMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Acid",
                "Acid",
                AuthoritativeProjectileBehavior.Acid,
                AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                RangedDelayBase: 1,
                RangedDelayRandomRange: 2),
        ]));
    }

    [Fact]
    public void RejectsRangedAvoidanceRecoveryDelayOnOrdinaryRangedMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                AuthoritativeMonsterRangedAttackMode.Ranged,
                RangedAvoidanceDelayBase: 1,
                RangedAvoidanceDelayRandomRange: 2),
        ]));
    }

    [Fact]
    public void RejectsHorkDemonRecoveryDelayOnNonHorkMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                HorkDemonDelayBase: 1,
                HorkDemonDelayRandomRange: 2),
        ]));
    }

    [Fact]
    public void RejectsSnakeDelayBandsOnNonSnakeMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                SnakeAdjacentDelayBase: 1,
                SnakeAdjacentDelayRandomRange: 2,
                SnakeDistantDelayBase: 1,
                SnakeDistantDelayRandomRange: 2),
        ]));
    }

    [Fact]
    public void RejectsMegaDelayTuningOnNonInfernoMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                MegaDelayBase: 1,
                MegaDelayRandomRange: 2),
        ]));
    }

    [Fact]
    public void LoadsValidatedInfernoAdmissionTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tinferno_maximum_distance\tinferno_adjacent_chance_base\tinferno_adjacent_chance_intelligence_scale\tinferno_ranged_far_distance\tinferno_ranged_chance_base\tinferno_ranged_chance_intelligence_scale\tinferno_ranged_far_intelligence_offset\tinferno_adjacent_choice_modulo\n"
                + "Mega\tInfernoControl\tInfernoController\t5\t40\t10\t3\t5\t5\t1\t2\n");

        Assert.True(catalog.TryGet("Mega", out var mega));
        Assert.Equal((5, 40, 10, 3, 5, 5, 1, 2), (
            mega.InfernoMaximumDistance,
            mega.InfernoAdjacentChanceBase,
            mega.InfernoAdjacentChanceIntelligenceScale,
            mega.InfernoRangedFarDistance,
            mega.InfernoRangedChanceBase,
            mega.InfernoRangedChanceIntelligenceScale,
            mega.InfernoRangedFarIntelligenceOffset,
            mega.InfernoAdjacentChoiceModulo));
    }

    [Fact]
    public void LegacyRowsDefaultToCompatibilitySpellIdentity()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "Succubus\tBloodStar\tBloodStar\n");

        Assert.True(catalog.TryGet("Succubus", out var succubus));
        Assert.Equal(0U, succubus.SpellId);
        Assert.Equal(0U, succubus.SpawnMonsterId);
        Assert.Null(succubus.DamageType);
        Assert.Equal(0, succubus.ProjectileSpeed);
        Assert.Equal(0, succubus.ProjectileLifetime);
        Assert.Equal(0, succubus.ProjectileLifetimeBase);
        Assert.Equal(0, succubus.ProjectileLifetimeIntelligenceScale);
        Assert.Equal(0, succubus.PuddleDurationRandomRange);
        Assert.Equal(0, succubus.PuddleDurationBase);
        Assert.Equal(0, succubus.PuddleDurationIntelligenceScale);
        Assert.Equal(0, succubus.PuddleDamageMinimumMonsterLevel);
        Assert.Equal(0, succubus.PuddleDamageLow);
        Assert.Equal(0, succubus.PuddleDamageHigh);
        Assert.Equal(0, succubus.PuddleImpactLifetime);
        Assert.Equal(0, succubus.ProjectileImpactLifetime);
        Assert.Equal(0, succubus.RangedDelayBase);
        Assert.Equal(0, succubus.RangedDelayRandomRange);
        Assert.Equal(0, succubus.RangedAvoidanceDelayBase);
        Assert.Equal(0, succubus.RangedAvoidanceDelayRandomRange);
        Assert.Equal(0, succubus.HorkDemonDelayBase);
        Assert.Equal(0, succubus.HorkDemonDelayRandomRange);
        Assert.Equal(0, succubus.SnakeAdjacentDelayBase);
        Assert.Equal(0, succubus.SnakeAdjacentDelayIntelligenceScale);
        Assert.Equal(0, succubus.SnakeAdjacentDelayRandomRange);
        Assert.Equal(0, succubus.SnakeDistantDelayBase);
        Assert.Equal(0, succubus.SnakeDistantDelayIntelligenceScale);
        Assert.Equal(0, succubus.SnakeDistantDelayRandomRange);
        Assert.Equal(0, succubus.MegaDelayBase);
        Assert.Equal(0, succubus.MegaDelayRandomRange);
        Assert.Equal(0, succubus.ChargeMinimumDistance);
        Assert.Equal(0, succubus.ChargeChanceBase);
        Assert.Equal(0, succubus.ChargeChanceIntelligenceScale);
        Assert.Equal(0, succubus.RangedAvoidanceMinimumDistance);
        Assert.Equal(0, succubus.RangedAvoidanceFarDistance);
        Assert.Equal(0, succubus.RangedAvoidanceChanceBase);
        Assert.Equal(0, succubus.RangedAvoidanceChanceIntelligenceScale);
        Assert.Equal(0, succubus.RangedAvoidanceFarIntelligenceOffset);
        Assert.Equal(0, succubus.RangedAvoidanceMovementChanceModulo);
        Assert.Equal(0, succubus.RangedAvoidanceMovementChanceBase);
        Assert.Equal(0, succubus.RangedAvoidanceMovementChanceIntelligenceScale);
        Assert.Equal(0, succubus.RangedAvoidanceMovementFreshChanceBonus);
        Assert.Equal(0, succubus.RangedRetreatMinimumDistance);
        Assert.Equal(0, succubus.RangedRetreatChanceBase);
        Assert.Equal(0, succubus.RangedRetreatChanceIntelligenceScale);
        Assert.Equal(0, succubus.InfernoMaximumDistance);
        Assert.Equal(0, succubus.InfernoAdjacentChanceBase);
        Assert.Equal(0, succubus.InfernoAdjacentChanceIntelligenceScale);
        Assert.Equal(0, succubus.InfernoRangedFarDistance);
        Assert.Equal(0, succubus.InfernoRangedChanceBase);
        Assert.Equal(0, succubus.InfernoRangedChanceIntelligenceScale);
        Assert.Equal(0, succubus.InfernoRangedFarIntelligenceOffset);
        Assert.Equal(0, succubus.InfernoAdjacentChoiceModulo);
    }

    [Fact]
    public void ValidatesConfiguredSpellReferences()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tspell_id\n"
                + "Succubus\tBloodStar\tBloodStar\t36\n");
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(36, 1, 0, 0, 0, 0),
        ]);

        catalog.ValidateSpellReferences(spells);
    }

    [Fact]
    public void RejectsUnknownConfiguredSpellReferences()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tspell_id\n"
                + "Succubus\tBloodStar\tBloodStar\t36\n");
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0),
        ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateSpellReferences(spells));

        Assert.Contains("unknown spell ID 36", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatesConfiguredMonsterSelectorReferences()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmonster_id\n"
                + "Bat\tRhino\tBatCharge\t42\n");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                42, 42, "Bat", 1, 16, 1, 1, 1, 1, 1, 1, 1, 1,
                false, false, false) { NativeAi = "Bat" },
        ]);

        catalog.ValidateMonsterSelectorReferences(monsterTypes);
    }

    [Fact]
    public void ResolvesStableSymbolsForMonsterSelectorAndSpawnReferences()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("monster", "bat", 42),
            new StableContentId("monster", "summoned", 117),
        ]);
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmonster_id\tmonster_symbol\tspawn_monster_id\tspawn_monster_symbol\n"
                + "Bat\tRhino\tBatCharge\t0\tbat\t0\tsummoned\n",
            stableIds);

        Assert.True(catalog.TryGet("Bat", 42, out var definition));
        Assert.Equal(117U, definition.SpawnMonsterId);
    }

    [Fact]
    public void RejectsUnknownConfiguredMonsterSelectorReferences()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmonster_id\n"
                + "Bat\tRhino\tBatCharge\t42\n");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1, 1, "Bat", 1, 16, 1, 1, 1, 1, 1, 1, 1, 1,
                false, false, false) { NativeAi = "Bat" },
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            catalog.ValidateMonsterSelectorReferences(monsterTypes));
        Assert.Contains("references unknown monster type 42", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatesConfiguredSpawnMonsterReferences()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tspawn_monster_id\n"
                + "HorkDemon\tHorkSpawn\tHorkSpawn\t117\n");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                117, 117, "Spawn", 1, 16, 1, 1, 1, 1, 1, 1, 1, 1,
                false, false, false),
        ]);

        catalog.ValidateSpawnMonsterReferences(monsterTypes);
    }

    [Fact]
    public void RejectsUnknownConfiguredSpawnMonsterReferences()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tspawn_monster_id\n"
                + "HorkDemon\tHorkSpawn\tHorkSpawn\t117\n");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1, 1, "Other", 1, 16, 1, 1, 1, 1, 1, 1, 1, 1,
                false, false, false),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            catalog.ValidateSpawnMonsterReferences(monsterTypes));
        Assert.Contains("references unknown spawn monster 117", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsExplicitMonsterArrowBehavior()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "GoatRanged\tArrow\tMonsterArrow\n");

        Assert.True(catalog.TryGet("GoatRanged", out var goatRanged));
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, goatRanged.ProjectileBehavior);
    }

    [Fact]
    public void LoadsValidatedBatChargeTuning()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmonster_id\tcharge_minimum_distance\tcharge_chance_base\tcharge_chance_intelligence_scale\n"
                + "Bat\tRhino\tBatCharge\t42\t6\t35\t5\n");

        Assert.True(catalog.TryGet("Bat", 42, out var batCharge));
        Assert.Equal((6, 35, 5), (
            batCharge.ChargeMinimumDistance,
            batCharge.ChargeChanceBase,
            batCharge.ChargeChanceIntelligenceScale));
    }

    [Fact]
    public void LoadsMonsterSpecificFamiliarLightningMapping()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmonster_id\tspell_id\tdamage_type\tprojectile_speed\tprojectile_lifetime\tprojectile_impact_lifetime\tpost_melee_missile\n"
                + "Bat\tFamiliarArc\tMonsterMagic\t43\t3\tLightning\t16\t2\t8\ttrue\n");

        Assert.True(catalog.TryGet("Bat", 43, out var familiar));
        Assert.Equal("FamiliarArc", familiar.MissileId);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, familiar.ProjectileBehavior);
        Assert.Equal(3U, familiar.SpellId);
        Assert.Equal(AuthoritativeDamageType.Lightning, familiar.DamageType);
        Assert.Equal((16, 2, 8), (
            familiar.ProjectileSpeed,
            familiar.ProjectileLifetime,
            familiar.ProjectileImpactLifetime));
        Assert.True(familiar.IsPostMeleeMissile);
    }

    [Fact]
    public void RejectsPostMeleeMissileForNonMagicProjectile()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "FamiliarArc",
                    AuthoritativeProjectileBehavior.BatCharge,
                    MonsterId: 43,
                    IsPostMeleeMissile: true),
            ]));
    }

    [Fact]
    public void LoadsValidatedSnakeChargeDistance()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tcharge_minimum_distance\n"
                + "Snake\tRhino\tSnakeCharge\t3\n");

        Assert.True(catalog.TryGet("Snake", out var snakeCharge));
        Assert.Equal(3, snakeCharge.ChargeMinimumDistance);
        Assert.Equal(0, snakeCharge.ChargeChanceBase);
        Assert.Equal(0, snakeCharge.ChargeChanceIntelligenceScale);
    }

    [Fact]
    public void LoadsValidatedSnakeDriftPattern()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tsnake_drift_pattern\n"
                + "Snake\tRhino\tSnakeCharge\t1, 1, 0, -1, -1, 0\n");

        Assert.True(catalog.TryGet("Snake", out var snakeCharge));
        Assert.Equal([1, 1, 0, -1, -1, 0], snakeCharge.SnakeDriftPattern);
    }

    [Fact]
    public void RejectsSnakeDriftPatternsWithTheWrongLength()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge) {
                    SnakeDriftPattern = [1, 0],
                },
            ]));
    }

    [Fact]
    public void RejectsAnOutOfRangeAvoidanceProbability()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "Acid",
                    AuthoritativeProjectileBehavior.Acid,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    0),
            ]));
    }

    [Fact]
    public void RejectsNegativeProjectileDamage()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Diablo",
                    "DiabloApocalypse",
                    AuthoritativeProjectileBehavior.ApocalypseController,
                ProjectileDamage: -1),
            ]));
    }

    [Fact]
    public void RejectsInvalidInitialTravelQuanta()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic) {
                    InitialTravelQuanta = 17,
                },
            ]));
    }

    [Fact]
    public void RejectsInvalidProjectileCount()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "ChargedBolt",
                    AuthoritativeProjectileBehavior.MonsterChargedBolt) {
                    ProjectileCount = 33,
                },
            ]));
    }

    [Fact]
    public void RejectsNegativeProjectileDamageRandomRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Lightning",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    ProjectileDamageRandomRange: -1),
            ]));
    }

    [Fact]
    public void RejectsNegativePuddleImpactLifetime()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "Acid",
                    AuthoritativeProjectileBehavior.Acid,
                    PuddleImpactLifetime: -1),
            ]));
    }

    [Fact]
    public void RejectsIncompleteAdjacentAttackTuning()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceAdjacentAttackChanceBase: 6000),
            ]));
    }

    [Fact]
    public void RejectsNegativeProjectileImpactLifetime()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "Acid",
                    AuthoritativeProjectileBehavior.Acid,
                    ProjectileImpactLifetime: -1),
            ]));
    }

    [Fact]
    public void RejectsAdjacentAttackChanceDivisorForNonAvoidanceMode()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.Ranged,
                    RangedAvoidanceAdjacentAttackChanceDivisor: 2),
            ]));
    }

    [Fact]
    public void RejectsNegativeAdjacentAttackChanceDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceAdjacentAttackChanceDivisor: -1),
            ]));
    }

    [Fact]
    public void RejectsInvalidChargeTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Bat",
                "Rhino",
                AuthoritativeProjectileBehavior.BatCharge,
                MonsterId: 42,
                ChargeMinimumDistance: 5,
                ChargeChanceBase: 33,
                ChargeChanceIntelligenceScale: 0),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                ChargeMinimumDistance: 5),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Snake",
                "Rhino",
                AuthoritativeProjectileBehavior.SnakeCharge,
                ChargeChanceBase: 33),
        ]));
    }

    [Fact]
    public void RejectsHorkSpawnWithoutValidatedSpawnIdentity()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "HorkDemon",
                "HorkSpawn",
                AuthoritativeProjectileBehavior.HorkSpawn),
        ]));
    }

    [Fact]
    public void RejectsPartialRangedAvoidanceTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Acid",
                "Acid",
                AuthoritativeProjectileBehavior.Acid,
                AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                RangedAvoidanceMinimumDistance: 2),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar,
                RangedAvoidanceMinimumDistance: 2),
        ]));
    }

    [Fact]
    public void RejectsRangedAvoidanceMovementModuloOnOrdinaryRangedMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar) {
                RangedAvoidanceMovementChanceModulo = 4,
            },
        ]));
    }

    [Fact]
    public void RejectsRangedAvoidanceMovementBandOnOrdinaryRangedMappings()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Succubus",
                "BloodStar",
                AuthoritativeProjectileBehavior.BloodStar) {
                RangedAvoidanceMovementChanceBase = 50,
                RangedAvoidanceMovementChanceIntelligenceScale = 10,
                RangedAvoidanceMovementFreshChanceBonus = 30,
            },
        ]));
    }

    [Fact]
    public void RejectsPartialRangedRetreatTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "AcidUnique",
                "Acid",
                AuthoritativeProjectileBehavior.Acid,
                RangedRetreatMinimumDistance: 4),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Acid",
                "Acid",
                AuthoritativeProjectileBehavior.Acid,
                AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                RangedRetreatMinimumDistance: 4,
                RangedRetreatChanceBase: 70,
                RangedRetreatChanceIntelligenceScale: 10),
        ]));
    }

    [Fact]
    public void RejectsPartialInfernoTuning()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Mega",
                "InfernoControl",
                AuthoritativeProjectileBehavior.InfernoController,
                InfernoMaximumDistance: 5),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Magma",
                "MagmaBall",
                AuthoritativeProjectileBehavior.MonsterMagic,
                InfernoMaximumDistance: 5,
                InfernoAdjacentChanceBase: 40,
                InfernoAdjacentChanceIntelligenceScale: 10,
                InfernoRangedFarDistance: 3,
                InfernoRangedChanceBase: 5,
                InfernoRangedChanceIntelligenceScale: 5,
                InfernoRangedFarIntelligenceOffset: 1,
                InfernoAdjacentChoiceModulo: 2),
        ]));
    }

    [Fact]
    public void RejectsAnUnknownDamageType()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tdamage_type\n"
                + "Succubus\tBloodStar\tBloodStar\tNotARealDamageType\n"));
    }

    [Fact]
    public void ResolvesMonsterSpecificRowsBeforeNativeAiWildcards()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\tmonster_id\n"
                + "Bat\tArrow\tTargeted\t0\n"
                + "Bat\tRhino\tBatCharge\t42\n");

        Assert.True(catalog.TryGet("Bat", 42, out var gloom));
        Assert.Equal("Rhino", gloom.MissileId);
        Assert.Equal(42U, gloom.MonsterId);
        Assert.True(catalog.TryGet("Bat", out var wildcard));
        Assert.Equal("Arrow", wildcard.MissileId);
    }

    [Fact]
    public void RejectsUnknownNativeAiIdentity()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "NotARealAi\tArrow\tTargeted\n"));
    }

    [Fact]
    public void RejectsDuplicateNativeAiIdentity()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
            "native_ai\tmissile_id\tprojectile_behavior\n"
                + "Succubus\tBloodStar\tBloodStar\n"
                + "Succubus\tBloodStar\tBloodStar\n"));
    }
}
