using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCombatRulesTests
{
    [Fact]
    public void ValidatesUniqueMonsterExperienceMultiplier()
    {
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            uniqueExperienceMultiplier: 3);

        Assert.Equal(3, rules.UniqueExperienceMultiplier);
        Assert.Equal(3, rules.MultipleArrowsCount);
        Assert.Equal(2, rules.MultipleArrowsDelayTicks);
        Assert.Equal(1, rules.MultipleArrowsSpreadDistance);
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            uniqueExperienceMultiplier: 0));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            multipleArrowsCount: 0));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            multipleArrowsCount: 4));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            multipleArrowsDelayTicks: 3));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            multipleArrowsSpreadDistance: 9));
    }

    [Fact]
    public void LoadsExternalCombatConstants()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\thit_chance_percent\tcritical_chance_percent\tcritical_multiplier\tfriendly_fire\tgame_variant\trestart_level_id\trestart_position_x\trestart_position_y\trestart_life\trestart_life_scale\trestart_transition_ticks\tdeath_gold_divisor\tdeath_gold_maximum_stack\tdeath_gold_item_type\tdeath_gold_item_index\tdeath_drop_search_radius\n12\t2\t250\t80\t25\t3\t0\tHellfire\t4\t75\t68\t1\t64\t2\t3\t2500\t11\t0\t25\n");

        Assert.Equal(12, rules.BaseAttackDamage);
        Assert.Equal(2, rules.MinimumDamage);
        Assert.Equal(250U, rules.DefeatExperience);
        Assert.Equal(80, rules.HitChancePercent);
        Assert.Equal(25, rules.CriticalChancePercent);
        Assert.Equal(3, rules.CriticalMultiplier);
        Assert.False(rules.FriendlyFire);
        Assert.Equal(DiabloGameVariant.Hellfire, rules.GameVariant);
        Assert.Equal((4U, 75, 68), (
            rules.RestartLevelId,
            rules.RestartPositionX,
            rules.RestartPositionY));
        Assert.Equal((1, 64, 2U), (
            rules.RestartLife,
            rules.RestartLifeScale,
            rules.RestartTransitionTicks));
        Assert.Equal((3, 2500, 11, 0, 25), (
            rules.DeathGoldDivisor,
            rules.DeathGoldMaximumStack,
            rules.DeathGoldItemType,
            rules.DeathGoldItemIndex,
            rules.DeathDropSearchRadius));
        Assert.Equal(24U, rules.PvpEarItemId);
        Assert.Equal(2, rules.PvpBlockLevelScale);
        Assert.Equal(2, rules.EquipmentFastBlockFrameSkip);
        Assert.Equal((1, 2, 3), (
            rules.FastHitRecoveryFrameSkip,
            rules.FasterHitRecoveryFrameSkip,
            rules.FastestHitRecoveryFrameSkip));
        Assert.Equal((2, 5, 95), (
            rules.PlayerMagicHitCharacterLevelScale,
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance));
        Assert.Equal(2, rules.MonsterRegenerationLevelDivisor);
        Assert.Equal(2, rules.MonsterPlayerBlockLevelScale);
        Assert.Equal(3, rules.MultipleArrowsCount);
        Assert.Equal(2, rules.MultipleArrowsDelayTicks);
        Assert.Equal(1, rules.MultipleArrowsSpreadDistance);
    }

    [Fact]
    public void LoadsConfiguredMultipleArrowsRules()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tmultiple_arrows_count\tmultiple_arrows_delay_ticks\tmultiple_arrows_spread_distance\n"
            + "10\t1\t100\t2\t1\t0\n");

        Assert.Equal(2, rules.MultipleArrowsCount);
        Assert.Equal(1, rules.MultipleArrowsDelayTicks);
        Assert.Equal(0, rules.MultipleArrowsSpreadDistance);
    }

    [Fact]
    public void DefaultsLegacyTablesToDiabloAndRejectsUnknownVariants()
    {
        var legacy = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\n10\t1\t100\n");
        var invalid =
            "base_attack_damage\tminimum_damage\tdefeat_experience\tgame_variant\n10\t1\t100\tExpansion\n";

        Assert.Equal(DiabloGameVariant.Diablo, legacy.GameVariant);
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv("combat.tsv", invalid));
    }

    [Fact]
    public void ProductionValidationRejectsOmittedCombatTuning()
    {
        var table = TsvTable.Parse(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tgame_variant\n"
                + "10\t1\t100\tDiablo\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.ValidateExternalizedProduction(table));

        Assert.Contains("missing required columns", exception.Message, StringComparison.Ordinal);
        Assert.Contains("critical_chance_percent", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsConfiguredMonsterHitChanceFloors()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tmonster_melee_minimum_hit_chance\tmonster_ranged_minimum_hit_chance\tmonster_minimum_hit_chance_level_14\tmonster_minimum_hit_chance_level_15\tmonster_minimum_hit_chance_level_16\tmonster_to_hit_level_scale\tmonster_to_hit_base_bonus\tmonster_ranged_distance_penalty\tmonster_magic_minimum_hit_chance\tmonster_magic_to_hit_monster_level_scale\tmonster_magic_to_hit_player_level_scale\tmonster_magic_to_hit_base_bonus\tmonster_magic_distance_penalty\tmonster_charge_base_to_hit\tmonster_acid_puddle_minimum_hit_chance\tmonster_acid_puddle_base_hit_chance\tmaximum_resistance\trandom_steal_life_divisor\tsteal_life_3_percent\tsteal_life_5_percent\tsteal_mana_3_percent\tsteal_mana_5_percent\thellfire_armor_bonus_against_demons\thellfire_armor_bonus_against_undead\n10\t1\t100\t50\t60\t70\t80\t90\t3\t40\t5\t55\t6\t7\t45\t8\t501\t65\t75\t61\t10\t7\t9\t11\t13\t17\t19\n");

        Assert.Equal((50, 60, 70, 80, 90, 3, 40, 5, 55, 6, 7, 45, 8, 501, 65, 75, 61, 10, 7, 9, 11, 13, 17, 19), (
            rules.MonsterMeleeMinimumHitChance,
            rules.MonsterRangedMinimumHitChance,
            rules.MonsterMinimumHitChanceLevel14,
            rules.MonsterMinimumHitChanceLevel15,
            rules.MonsterMinimumHitChanceLevel16,
            rules.MonsterToHitLevelScale,
            rules.MonsterToHitBaseBonus,
            rules.MonsterRangedDistancePenalty,
            rules.MonsterMagicMinimumHitChance,
            rules.MonsterMagicToHitMonsterLevelScale,
            rules.MonsterMagicToHitPlayerLevelScale,
            rules.MonsterMagicToHitBaseBonus,
            rules.MonsterMagicDistancePenalty,
            rules.MonsterChargeBaseToHit,
            rules.MonsterAcidPuddleMinimumHitChance,
            rules.MonsterAcidPuddleBaseHitChance,
            rules.MaximumResistance,
            rules.RandomStealLifeDivisor,
            rules.StealLife3Percent,
            rules.StealLife5Percent,
            rules.StealMana3Percent,
            rules.StealMana5Percent,
            rules.HellfireArmorBonusAgainstDemons,
            rules.HellfireArmorBonusAgainstUndead));
        Assert.Equal(
            10,
            rules.ResolveMonsterAttackDamage(
                10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 30,
                seed: 0));
        Assert.Equal(
            0,
            new AuthoritativeCombatRules(10, 1, 100).ResolveMonsterAttackDamage(
                10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 30,
                seed: 0));
        Assert.Equal(
            10,
            rules.ResolveMonsterRangedAttackDamage(
                10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 14,
                targetArmorClass: 30,
                distance: 0,
                seed: 0));
        Assert.Equal(
            55,
            rules.ResolveMonsterMagicProjectileHitChance(
                monsterLevel: 3,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                distance: 2));
        Assert.Equal(
            70,
            rules.ResolveMonsterMagicProjectileHitChance(
                monsterLevel: 0,
                targetCharacterLevel: 1,
                targetLevelId: 14,
                distance: 100));
        Assert.Equal(
            50,
            rules.ResolveMonsterChargeHitChance(
                monsterLevel: 3,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 500));
        Assert.Equal(
            75,
            rules.ResolveMonsterAcidPuddleHitChance(targetLevelId: 1));
        Assert.Equal(
            75,
            rules.ResolveMonsterAcidPuddleHitChance(targetLevelId: 14));
    }

    [Fact]
    public void UsesConfiguredMonsterAccuracyFormulaTerms()
    {
        var levelScaled = new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterMeleeMinimumHitChance: 0,
            monsterRangedMinimumHitChance: 0,
            monsterMinimumHitChanceLevel14: 0,
            monsterMinimumHitChanceLevel15: 0,
            monsterMinimumHitChanceLevel16: 0,
            monsterToHitLevelScale: 10,
            monsterToHitBaseBonus: 30,
            monsterRangedDistancePenalty: 2);
        Assert.Equal(
            10,
            levelScaled.ResolveMonsterAttackDamage(
                10,
                monsterToHit: 1,
                monsterLevel: 3,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 20,
                seed: 0));
        Assert.Equal(
            0,
            new AuthoritativeCombatRules(10, 1, 100).ResolveMonsterAttackDamage(
                10,
                monsterToHit: 1,
                monsterLevel: 3,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 20,
                seed: 0));

        var rangedDistance = new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterMeleeMinimumHitChance: 0,
            monsterRangedMinimumHitChance: 0,
            monsterMinimumHitChanceLevel14: 0,
            monsterMinimumHitChanceLevel15: 0,
            monsterMinimumHitChanceLevel16: 0,
            monsterToHitLevelScale: 2,
            monsterToHitBaseBonus: 30,
            monsterRangedDistancePenalty: 10);
        Assert.Equal(
            0,
            rangedDistance.ResolveMonsterRangedAttackDamage(
                10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 0,
                distance: 1,
                seed: 0));
        Assert.Equal(
            10,
            new AuthoritativeCombatRules(10, 1, 100).ResolveMonsterRangedAttackDamage(
                10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 0,
                distance: 1,
                seed: 0));
    }

    [Fact]
    public void LoadsExternalDifficultyTuningWithLegacyFallbacks()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tdifficulty_nightmare_level_offset\tdifficulty_hell_damage_bonus\tdifficulty_experience_bonus\n"
            + "10\t1\t100\t17\t9\t1200\n");

        Assert.Equal((17, 30, 85, 120, 50, 80), (
            rules.DifficultyRules.NightmareLevelOffset,
            rules.DifficultyRules.HellLevelOffset,
            rules.DifficultyRules.NightmareToHitBonus,
            rules.DifficultyRules.HellToHitBonus,
            rules.DifficultyRules.NightmareArmorBonus,
            rules.DifficultyRules.HellArmorBonus));
        Assert.Equal((2, 2, 4, 9, 1200U), (
            rules.DifficultyRules.NightmareDamageMultiplier,
            rules.DifficultyRules.NightmareDamageBonus,
            rules.DifficultyRules.HellDamageMultiplier,
            rules.DifficultyRules.HellDamageBonus,
            rules.DifficultyRules.ExperienceBonus));
        Assert.Equal((3, 4, 100, 50, 100, 200, 100, 200), (
            rules.DifficultyRules.NightmareHealthMultiplier,
            rules.DifficultyRules.HellHealthMultiplier,
            rules.DifficultyRules.NightmareDiabloHealthBonus,
            rules.DifficultyRules.NightmareHellfireSinglePlayerHealthBonus,
            rules.DifficultyRules.NightmareHellfireMultiplayerHealthBonus,
            rules.DifficultyRules.HellDiabloHealthBonus,
            rules.DifficultyRules.HellHellfireSinglePlayerHealthBonus,
            rules.DifficultyRules.HellHellfireMultiplayerHealthBonus));
    }

    [Fact]
    public void LoadsConfiguredMaximumPrimaryAttribute()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tmaximum_attribute\n"
            + "10\t1\t100\t321\n");

        Assert.Equal(321, rules.MaximumAttribute);
    }

    [Fact]
    public void LoadsConfiguredBarbarianWeaponStrengthDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_weapon_strength_damage_divisor\n"
            + "10\t1\t100\t51\n");

        Assert.Equal(51, rules.BarbarianWeaponStrengthDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredRogueStrengthDexterityDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\trogue_strength_dexterity_damage_divisor\n"
            + "10\t1\t100\t201\n");

        Assert.Equal(201, rules.RogueStrengthDexterityDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredMonkStaffUnarmedDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tmonk_staff_unarmed_damage_divisor\n"
            + "10\t1\t100\t151\n");

        Assert.Equal(151, rules.MonkStaffUnarmedDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredMonkWeaponDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tmonk_weapon_damage_divisor\n"
            + "10\t1\t100\t301\n");

        Assert.Equal(301, rules.MonkWeaponDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredBardSwordDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tbard_sword_strength_dexterity_damage_divisor\n"
            + "10\t1\t100\t151\n");

        Assert.Equal(151, rules.BardSwordStrengthDexterityDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredBardBowDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tbard_bow_strength_dexterity_damage_divisor\n"
            + "10\t1\t100\t251\n");

        Assert.Equal(251, rules.BardBowStrengthDexterityDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredBardDefaultDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tbard_default_strength_damage_divisor\n"
            + "10\t1\t100\t101\n");

        Assert.Equal(101, rules.BardDefaultStrengthDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredStandardDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tstandard_strength_damage_divisor\n"
            + "10\t1\t100\t101\n");

        Assert.Equal(101, rules.StandardStrengthDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredBarbarianBowDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_bow_strength_damage_divisor\n"
            + "10\t1\t100\t301\n");

        Assert.Equal(301, rules.BarbarianBowStrengthDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredBarbarianDefaultDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_default_strength_damage_divisor\n"
            + "10\t1\t100\t101\n");

        Assert.Equal(101, rules.BarbarianDefaultStrengthDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredBarbarianVitalityDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_vitality_damage_divisor\n"
            + "10\t1\t100\t101\n");

        Assert.Equal(101, rules.BarbarianVitalityDamageDivisor);
    }

    [Fact]
    public void LoadsConfiguredThornsDamageBounds()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tthorns_damage_minimum\tthorns_damage_maximum\n"
            + "10\t1\t100\t7\t11\n");

        Assert.Equal((7, 11), (rules.ThornsDamageMinimum, rules.ThornsDamageMaximum));
    }

    [Fact]
    public void LoadsConfiguredHellfireDecayTuning()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_decay_damage_reduction\thellfire_decay_removal_threshold\n"
            + "10\t1\t100\t7\t21\n");

        Assert.Equal((7, 21), (
            rules.HellfireDecayDamageReduction,
            rules.HellfireDecayRemovalThreshold));
    }

    [Fact]
    public void LoadsConfiguredHellfireDevastationTuning()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_devastation_chance_percent\thellfire_devastation_damage_multiplier\n"
            + "10\t1\t100\t17\t4\n");

        Assert.Equal((17, 4), (
            rules.HellfireDevastationChancePercent,
            rules.HellfireDevastationDamageMultiplier));
    }

    [Fact]
    public void LoadsConfiguredHellfireJestersTuning()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_jesters_roll_maximum\thellfire_jesters_expansion_threshold\thellfire_jesters_expansion_multiplier\n"
            + "10\t1\t100\t80\t40\t2\n");

        Assert.Equal((80, 40, 2), (
            rules.HellfireJestersRollMaximum,
            rules.HellfireJestersExpansionThreshold,
            rules.HellfireJestersExpansionMultiplier));
    }

    [Fact]
    public void LoadsConfiguredHellfirePerilAndDoppelgangerTuning()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_peril_damage_multiplier\thellfire_doppelganger_chance_percent\n"
            + "10\t1\t100\t3\t27\n");

        Assert.Equal((3, 27), (
            rules.HellfirePerilDamageMultiplier,
            rules.HellfireDoppelgangerChancePercent));
    }

    [Fact]
    public void LoadsConfiguredTripleDemonDamageMultiplier()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\ttriple_demon_damage_multiplier\n"
            + "10\t1\t100\t7\n");

        Assert.Equal(7, rules.TripleDemonDamageMultiplier);
    }

    [Fact]
    public void RejectsInvalidMaximumPrimaryAttribute()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(10, 1, 100, maximumAttribute: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tmaximum_attribute\n"
                + "10\t1\t100\t10001\n"));
    }

    [Fact]
    public void RejectsInvalidBarbarianWeaponStrengthDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_weapon_strength_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidRogueStrengthDexterityDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\trogue_strength_dexterity_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidMonkStaffUnarmedDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tmonk_staff_unarmed_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidMonkWeaponDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tmonk_weapon_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidBardSwordDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tbard_sword_strength_dexterity_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidBardBowDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tbard_bow_strength_dexterity_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidBardDefaultDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tbard_default_strength_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidStandardDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tstandard_strength_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidBarbarianBowDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_bow_strength_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidBarbarianDefaultDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_default_strength_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidBarbarianVitalityDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tbarbarian_vitality_damage_divisor\n"
                + "10\t1\t100\t0\n"));
    }

    [Fact]
    public void RejectsInvalidThornsDamageBounds()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\tthorns_damage_minimum\tthorns_damage_maximum\n"
                + "10\t1\t100\t0\t3\n"));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                thornsDamageMinimum: 4,
                thornsDamageMaximum: 3));
    }

    [Fact]
    public void RejectsInvalidHellfireDecayTuning()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_decay_damage_reduction\n"
                + "10\t1\t100\t0\n"));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                hellfireDecayRemovalThreshold: 0));
    }

    [Fact]
    public void RejectsInvalidHellfireDevastationTuning()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_devastation_chance_percent\n"
                + "10\t1\t100\t101\n"));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                hellfireDevastationDamageMultiplier: 0));
    }

    [Fact]
    public void RejectsInvalidHellfireJestersTuning()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_jesters_roll_maximum\thellfire_jesters_expansion_threshold\n"
                + "10\t1\t100\t10\t11\n"));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                hellfireJestersExpansionMultiplier: 0));
    }

    [Fact]
    public void RejectsInvalidHellfirePerilAndDoppelgangerTuning()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\thellfire_peril_damage_multiplier\n"
                + "10\t1\t100\t0\n"));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                hellfireDoppelgangerChancePercent: 101));
    }

    [Fact]
    public void RejectsInvalidTripleDemonDamageMultiplier()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCombatRules.LoadTsv(
                "combat.tsv",
                "base_attack_damage\tminimum_damage\tdefeat_experience\ttriple_demon_damage_multiplier\n"
                + "10\t1\t100\t0\n"));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                tripleDemonDamageMultiplier: 101));
    }

    [Fact]
    public void LoadsAndValidatesPvpBlockLevelScale()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tpvp_block_level_scale\n"
            + "10\t1\t100\t4\n");

        Assert.Equal(4, rules.PvpBlockLevelScale);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                pvpBlockLevelScale: 65));
    }

    [Fact]
    public void LoadsAndValidatesPlayerMagicAccuracyTuning()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\t"
            + "player_magic_hit_character_level_scale\tplayer_magic_minimum_hit_chance\t"
            + "player_magic_maximum_hit_chance\n"
            + "10\t1\t100\t4\t17\t88\n");

        Assert.Equal((4, 17, 88), (
            rules.PlayerMagicHitCharacterLevelScale,
            rules.PlayerMagicMinimumHitChance,
            rules.PlayerMagicMaximumHitChance));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                playerMagicHitCharacterLevelScale: 65));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                playerMagicMinimumHitChance: 90,
                playerMagicMaximumHitChance: 89));
    }

    [Fact]
    public void LoadsAndValidatesMonsterRegenerationLevelDivisor()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\t"
            + "monster_regeneration_level_divisor\n"
            + "10\t1\t100\t4\n");

        Assert.Equal(4, rules.MonsterRegenerationLevelDivisor);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterRegenerationLevelDivisor: 0));
    }

    [Fact]
    public void LoadsAndValidatesMonsterPlayerBlockLevelScale()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\t"
            + "monster_player_block_level_scale\n"
            + "10\t1\t100\t4\n");

        Assert.Equal(4, rules.MonsterPlayerBlockLevelScale);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterPlayerBlockLevelScale: 65));
    }

    [Fact]
    public void LoadsAndValidatesEquipmentFastBlockFrameSkip()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tequipment_fast_block_frame_skip\n"
            + "10\t1\t100\t4\n");

        Assert.Equal(4, rules.EquipmentFastBlockFrameSkip);
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                equipmentFastBlockFrameSkip: 11));
    }

    [Fact]
    public void LoadsAndValidatesHitRecoveryTierSkips()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tfast_hit_recovery_frame_skip\t"
            + "faster_hit_recovery_frame_skip\tfastest_hit_recovery_frame_skip\n"
            + "10\t1\t100\t2\t4\t6\n");

        Assert.Equal((2, 4, 6), (
            rules.FastHitRecoveryFrameSkip,
            rules.FasterHitRecoveryFrameSkip,
            rules.FastestHitRecoveryFrameSkip));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                fastHitRecoveryFrameSkip: 3,
                fasterHitRecoveryFrameSkip: 2));
    }

    [Fact]
    public void LoadsAndValidatesMeleeAttackSpeedTierSkips()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\t"
            + "quick_attack_frame_skip_with_first_frame\tquick_attack_frame_skip_without_first_frame\t"
            + "fast_attack_frame_skip_with_first_frame\tfast_attack_frame_skip_without_first_frame\t"
            + "faster_attack_frame_skip_with_first_frame\tfaster_attack_frame_skip_without_first_frame\t"
            + "fastest_attack_frame_skip_with_first_frame\tfastest_attack_frame_skip_without_first_frame\n"
            + "10\t1\t100\t2\t1\t4\t2\t6\t3\t8\t4\n");

        Assert.Equal((2, 1, 4, 2, 6, 3, 8, 4), (
            rules.QuickAttackFrameSkipWithFirstFrame,
            rules.QuickAttackFrameSkipWithoutFirstFrame,
            rules.FastAttackFrameSkipWithFirstFrame,
            rules.FastAttackFrameSkipWithoutFirstFrame,
            rules.FasterAttackFrameSkipWithFirstFrame,
            rules.FasterAttackFrameSkipWithoutFirstFrame,
            rules.FastestAttackFrameSkipWithFirstFrame,
            rules.FastestAttackFrameSkipWithoutFirstFrame));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                fastestAttackFrameSkipWithFirstFrame: 2,
                fastestAttackFrameSkipWithoutFirstFrame: 3));
    }

    [Fact]
    public void LoadsRangedAttackFrameTiming()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\t"
            + "ranged_attack_frame_skip_with_first_frame\t"
            + "ranged_attack_frame_skip_without_first_frame\t"
            + "ranged_fast_attack_frame_skip_bonus\n"
            + "10\t1\t100\t3\t1\t2\n");

        Assert.Equal((3, 1, 2), (
            rules.RangedAttackFrameSkipWithFirstFrame,
            rules.RangedAttackFrameSkipWithoutFirstFrame,
            rules.RangedFastAttackFrameSkipBonus));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                rangedFastAttackFrameSkipBonus: 65));
    }

    [Fact]
    public void UsesConfiguredMonsterMagicAccuracyFormulaTerms()
    {
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterMagicMinimumHitChance: 0,
            monsterMagicToHitMonsterLevelScale: 10,
            monsterMagicToHitPlayerLevelScale: 3,
            monsterMagicToHitBaseBonus: 4,
            monsterMagicDistancePenalty: 5);

        Assert.Equal(
            21,
            rules.ResolveMonsterMagicProjectileHitChance(
                monsterLevel: 3,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                distance: 2));
        Assert.Equal(
            0,
            rules.ResolveMonsterMagicProjectileHitChance(
                monsterLevel: 0,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                distance: 2));
        Assert.Equal(
            20,
            rules.ResolveMonsterMagicProjectileHitChance(
                monsterLevel: 0,
                targetCharacterLevel: 1,
                targetLevelId: 14,
                distance: 100));
        Assert.Equal(
            28,
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMeleeMinimumHitChance: 0,
                monsterMinimumHitChanceLevel14: 0,
                monsterMinimumHitChanceLevel15: 0,
                monsterMinimumHitChanceLevel16: 0,
                monsterToHitLevelScale: 3,
                monsterToHitBaseBonus: 4,
                monsterChargeBaseToHit: 20)
                .ResolveMonsterChargeHitChance(
                    monsterLevel: 3,
                    targetCharacterLevel: 1,
                    targetLevelId: 1,
                    targetArmorClass: 2));
    }

    [Fact]
    public void RejectsInvalidDamageBounds()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(1, 2, 0));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterMeleeMinimumHitChance: 101));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterToHitLevelScale: 65));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterMagicToHitBaseBonus: 256));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterChargeBaseToHit: 1001));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            monsterAcidPuddleBaseHitChance: 101));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            maximumResistance: 101));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            randomStealLifeDivisor: 0));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            hellfireArmorBonusAgainstDemons: -1));
    }

    [Fact]
    public void ValidatesRestartDestinationAgainstWorldGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(4, 80, 80, [68 * 80 + 75]));
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            restartLevelId: 4,
            restartPositionX: 75,
            restartPositionY: 68);

        Assert.Throws<InvalidDataException>(() => rules.ValidateWorld(world));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            restartLifeScale: 65));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(
            10,
            1,
            100,
            deathGoldDivisor: 1));
    }

    [Fact]
    public void ValidatesPvpEarNativeIdentityFromTheExternalItemCatalog()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var valid = new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 23, miscId: 44),
        ]);
        var wrongItemIndex = new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 24, miscId: 44),
        ]);
        var wrongMiscId = new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 23, miscId: 43),
        ]);

        rules.ValidateItems(valid);
        Assert.Throws<InvalidDataException>(() => rules.ValidateItems(wrongItemIndex));
        Assert.Throws<InvalidDataException>(() => rules.ValidateItems(wrongMiscId));
    }

    [Fact]
    public void RejectsUnknownPvpEarItemReference()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100, pvpEarItemId: 9999);
        var items = new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 23, miscId: 44),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() => rules.ValidateItems(items));
        Assert.Contains("references unknown item 9999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsPvpEarNativeProjectionFromCombatContent()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tpvp_ear_native_item_index\tpvp_ear_misc_id\r\n"
                + "10\t1\t100\t99\t77\r\n");

        Assert.Equal(99, rules.PvpEarNativeItemIndex);
        Assert.Equal(77, rules.PvpEarMiscId);
        rules.ValidateItems(new AuthoritativeItemCatalog([
            CreateEarDefinition(nativeItemIndex: 99, miscId: 77),
        ]));
    }

    [Fact]
    public void ResolvesStablePvpEarItemSymbol()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "ear", 24),
        ]);
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tpvp_ear_item_id\tpvp_ear_item_symbol\n"
                + "10\t1\t100\t24\tear\n",
            stableIds: stableIds);

        Assert.Equal(24U, rules.PvpEarItemId);
    }

    [Fact]
    public void ResolvesStableRestartLevelSymbol()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
        ]);
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\trestart_level_id\trestart_level_symbol\n"
                + "10\t1\t100\t0\ttown\n",
            stableIds: stableIds);

        Assert.Equal(0U, rules.RestartLevelId);
    }

    [Fact]
    public void RejectsStablePvpEarItemNumericMismatch()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("item", "ear", 24),
        ]);

        Assert.Throws<InvalidDataException>(() => AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\tpvp_ear_item_id\tpvp_ear_item_symbol\n"
                + "10\t1\t100\t25\tear\n",
            stableIds: stableIds));
    }

    [Fact]
    public void ResolvesArmorAndElementalResistanceDeterministically()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var target = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            armorClass: 3,
            fireResistance: 50,
            acidResistance: 75);

        Assert.Equal(7, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Physical, target));
        Assert.Equal(5, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Fire, target));
        Assert.Equal(2, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Acid, target));
    }

    [Fact]
    public void ResolvesDeterministicMissAndCriticalAttackOutcomes()
    {
        var target = new AuthoritativeCombatTarget(9, 0, 0, 100, armorClass: 0);
        var misses = new AuthoritativeCombatRules(10, 1, 100, hitChancePercent: 0);
        Assert.Equal(0, misses.ResolveAttackDamage(10, target, 1));

        var criticals = new AuthoritativeCombatRules(10, 1, 100, criticalChancePercent: 100, criticalMultiplier: 3);
        Assert.Equal(30, criticals.ResolveAttackDamage(10, target, 1));
    }

    [Fact]
    public void ResolvesAutonomousMonsterAttacksThroughTheSameRules()
    {
        var rules = new AuthoritativeCombatRules(3, 1, 100, hitChancePercent: 100, criticalChancePercent: 100, criticalMultiplier: 2);

        Assert.Equal(4, rules.ResolveAttackDamage(3, armorClass: 2, seed: 1));
    }

    [Fact]
    public void ResolvesNativeMonsterAccuracyWithoutSubtractingPlayerArmorFromDamage()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);

        Assert.Equal(
            10,
            rules.ResolveMonsterAttackDamage(
                baseDamage: 10,
                monsterToHit: 100,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 50,
                seed: 0));
        Assert.Equal(
            0,
            rules.ResolveMonsterAttackDamage(
                baseDamage: 10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 100,
                seed: 0));
    }

    [Fact]
    public void ResolvesNativeMonsterArrowAccuracyWithDistancePenalty()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);

        Assert.Equal(
            10,
            rules.ResolveMonsterRangedAttackDamage(
                baseDamage: 10,
                monsterToHit: 100,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 50,
                distance: 2,
                seed: 0));
        Assert.Equal(
            0,
            rules.ResolveMonsterRangedAttackDamage(
                baseDamage: 10,
                monsterToHit: 1,
                monsterLevel: 1,
                targetCharacterLevel: 1,
                targetLevelId: 1,
                targetArmorClass: 100,
                distance: 2,
                seed: 0));
    }

    [Fact]
    public void PhysicalHitChanceSubtractsArmorAndClampsToNativeBounds()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);

        Assert.Equal(5, rules.ResolvePhysicalHitChance(20, targetArmorClass: 30));
        Assert.Equal(65, rules.ResolvePhysicalHitChance(70, targetArmorClass: 10, armorPiercingBonus: 5));
        Assert.Equal(95, rules.ResolvePhysicalHitChance(200, targetArmorClass: 0));
        Assert.Equal(57, rules.ResolvePhysicalHitChance(
            70,
            targetArmorClass: 16,
            armorPiercingBonus: 1,
            barbarianMelee: true));
    }

    [Fact]
    public void PhysicalHitChanceAcceptsAContentDeclaredMeleeArmorDivisor()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);

        Assert.Equal(
            57,
            rules.ResolvePhysicalHitChance(
                70,
                targetArmorClass: 16,
                armorPiercingBonus: 1,
                meleeArmorReductionDivisor: 8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            rules.ResolvePhysicalHitChance(
                70,
                targetArmorClass: 16,
                armorPiercingBonus: 1,
                meleeArmorReductionDivisor: -1));
    }

    [Fact]
    public void HellfirePhysicalHitChanceDoesNotApplyArmorPiercingBonus()
    {
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            gameVariant: DiabloGameVariant.Hellfire);

        Assert.Equal(62, rules.ResolvePhysicalHitChance(70, targetArmorClass: 10, armorPiercingBonus: 1));
        Assert.Equal(70, rules.ResolvePhysicalHitChance(70, targetArmorClass: 10, armorPiercingBonus: 50));
    }

    private static AuthoritativeItemDefinition CreateEarDefinition(
        int nativeItemIndex,
        int miscId)
    {
        return new AuthoritativeItemDefinition(
            24,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            false) {
                NativeItemIndex = nativeItemIndex,
                MiscId = miscId,
            };
    }
}
