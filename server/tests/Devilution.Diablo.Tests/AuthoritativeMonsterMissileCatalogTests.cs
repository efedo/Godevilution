using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterMissileCatalogTests
{
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
    }

    [Fact]
    public void LoadsValidatedAvoidanceModeAndProbability()
    {
        var catalog = AuthoritativeMonsterMissileCatalog.LoadTsv(
            "monster_missiles.tsv",
                "native_ai\tmissile_id\tprojectile_behavior\tattack_mode\tavoidance_probability_percent\tspell_id\tspawn_monster_id\tdamage_type\tprojectile_speed\tprojectile_lifetime\tprojectile_lifetime_base\tprojectile_lifetime_intelligence_scale\tpuddle_duration_random_range\tpuddle_duration_base\tpuddle_duration_intelligence_scale\tpuddle_damage_minimum_monster_level\tpuddle_damage_low\tpuddle_damage_high\tranged_avoidance_minimum_distance\tranged_avoidance_far_distance\tranged_avoidance_chance_base\tranged_avoidance_chance_intelligence_scale\tranged_avoidance_far_intelligence_offset\n"
                + "Acid\tAcid\tAcid\tRangedAvoidance\t50\t1\t117\tAcid\t8\t9\t20\t5\t15\t40\t40\t2\t1\t2\t2\t3\t500\t500\t1\n");

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
        Assert.Equal((2, 3, 500, 500, 1), (
            acid.RangedAvoidanceMinimumDistance,
            acid.RangedAvoidanceFarDistance,
            acid.RangedAvoidanceChanceBase,
            acid.RangedAvoidanceChanceIntelligenceScale,
            acid.RangedAvoidanceFarIntelligenceOffset));
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
        Assert.Equal(0, succubus.ChargeMinimumDistance);
        Assert.Equal(0, succubus.ChargeChanceBase);
        Assert.Equal(0, succubus.ChargeChanceIntelligenceScale);
        Assert.Equal(0, succubus.RangedAvoidanceMinimumDistance);
        Assert.Equal(0, succubus.RangedAvoidanceFarDistance);
        Assert.Equal(0, succubus.RangedAvoidanceChanceBase);
        Assert.Equal(0, succubus.RangedAvoidanceChanceIntelligenceScale);
        Assert.Equal(0, succubus.RangedAvoidanceFarIntelligenceOffset);
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
