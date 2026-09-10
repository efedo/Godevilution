using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** Native ruleset variant selected by validated Diablo content. */
public enum DiabloGameVariant
{
    Diablo,
    Hellfire,
}

/** Externalized constants for the initial authoritative combat boundary. */
public sealed class AuthoritativeCombatRules
{
    private static readonly string[] ExternalizedProductionColumns = [
        "base_attack_damage",
        "minimum_damage",
        "defeat_experience",
        "hit_chance_percent",
        "critical_chance_percent",
        "critical_multiplier",
        "friendly_fire",
        "game_variant",
        "restart_level_id",
        "restart_level_symbol",
        "restart_position_x",
        "restart_position_y",
        "restart_life",
        "restart_life_scale",
        "restart_transition_ticks",
        "death_gold_divisor",
        "death_gold_maximum_stack",
        "death_gold_item_type",
        "death_gold_item_index",
        "death_drop_search_radius",
        "pvp_ear_item_id",
        "pvp_ear_item_symbol",
        "monster_melee_minimum_hit_chance",
        "monster_ranged_minimum_hit_chance",
        "monster_minimum_hit_chance_level_14",
        "monster_minimum_hit_chance_level_15",
        "monster_minimum_hit_chance_level_16",
        "monster_to_hit_level_scale",
        "monster_to_hit_base_bonus",
        "monster_ranged_distance_penalty",
        "monster_magic_minimum_hit_chance",
        "monster_magic_to_hit_monster_level_scale",
        "monster_magic_to_hit_player_level_scale",
        "monster_magic_to_hit_base_bonus",
        "monster_magic_distance_penalty",
        "monster_charge_base_to_hit",
        "monster_acid_puddle_minimum_hit_chance",
        "monster_acid_puddle_base_hit_chance",
        "maximum_resistance",
        "random_steal_life_divisor",
        "steal_life_3_percent",
        "steal_life_5_percent",
        "steal_mana_3_percent",
        "steal_mana_5_percent",
        "hellfire_armor_bonus_against_demons",
        "hellfire_armor_bonus_against_undead",
        "pvp_ear_native_item_index",
        "pvp_ear_misc_id",
        "maximum_attribute",
        "barbarian_weapon_strength_damage_divisor",
        "rogue_strength_dexterity_damage_divisor",
        "monk_staff_unarmed_damage_divisor",
        "monk_weapon_damage_divisor",
        "bard_sword_strength_dexterity_damage_divisor",
        "bard_bow_strength_dexterity_damage_divisor",
        "bard_default_strength_damage_divisor",
        "standard_strength_damage_divisor",
        "barbarian_bow_strength_damage_divisor",
        "barbarian_default_strength_damage_divisor",
        "barbarian_vitality_damage_divisor",
        "thorns_damage_minimum",
        "thorns_damage_maximum",
        "hellfire_decay_damage_reduction",
        "hellfire_decay_removal_threshold",
        "hellfire_devastation_chance_percent",
        "hellfire_devastation_damage_multiplier",
        "hellfire_jesters_roll_maximum",
        "hellfire_jesters_expansion_threshold",
        "hellfire_jesters_expansion_multiplier",
        "hellfire_peril_damage_multiplier",
        "hellfire_doppelganger_chance_percent",
        "triple_demon_damage_multiplier",
        "pvp_block_level_scale",
        "equipment_fast_block_frame_skip",
        "fast_hit_recovery_frame_skip",
        "faster_hit_recovery_frame_skip",
        "fastest_hit_recovery_frame_skip",
        "quick_attack_frame_skip_with_first_frame",
        "quick_attack_frame_skip_without_first_frame",
        "fast_attack_frame_skip_with_first_frame",
        "fast_attack_frame_skip_without_first_frame",
        "faster_attack_frame_skip_with_first_frame",
        "faster_attack_frame_skip_without_first_frame",
        "fastest_attack_frame_skip_with_first_frame",
        "fastest_attack_frame_skip_without_first_frame",
        "player_magic_hit_character_level_scale",
        "player_magic_minimum_hit_chance",
        "player_magic_maximum_hit_chance",
        "monster_regeneration_level_divisor",
        "monster_player_block_level_scale",
        "difficulty_nightmare_level_offset",
        "difficulty_hell_level_offset",
        "difficulty_nightmare_to_hit_bonus",
        "difficulty_hell_to_hit_bonus",
        "difficulty_nightmare_armor_bonus",
        "difficulty_hell_armor_bonus",
        "difficulty_nightmare_damage_multiplier",
        "difficulty_nightmare_damage_bonus",
        "difficulty_hell_damage_multiplier",
        "difficulty_hell_damage_bonus",
        "difficulty_nightmare_experience_multiplier",
        "difficulty_hell_experience_multiplier",
        "difficulty_experience_bonus",
        "difficulty_nightmare_health_multiplier",
        "difficulty_hell_health_multiplier",
        "difficulty_nightmare_diablo_health_bonus",
        "difficulty_nightmare_hellfire_single_player_health_bonus",
        "difficulty_nightmare_hellfire_multiplayer_health_bonus",
        "difficulty_hell_diablo_health_bonus",
        "difficulty_hell_hellfire_single_player_health_bonus",
        "difficulty_hell_hellfire_multiplayer_health_bonus",
        "ranged_attack_frame_skip_with_first_frame",
        "ranged_attack_frame_skip_without_first_frame",
        "ranged_fast_attack_frame_skip_bonus",
        "unique_experience_multiplier",
        "multiple_arrows_count",
        "multiple_arrows_delay_ticks",
        "multiple_arrows_spread_distance",
    ];

    public AuthoritativeCombatRules(
        int baseAttackDamage,
        int minimumDamage,
        uint defeatExperience,
        int hitChancePercent = 100,
        int criticalChancePercent = 0,
        int criticalMultiplier = 2,
        bool friendlyFire = true,
        DiabloGameVariant gameVariant = DiabloGameVariant.Diablo,
        uint restartLevelId = 0,
        int restartPositionX = 0,
        int restartPositionY = 0,
        int restartLife = 1,
        int restartLifeScale = 64,
        uint restartTransitionTicks = 1,
        int deathGoldDivisor = 2,
        int deathGoldMaximumStack = 5000,
        int deathGoldItemType = 11,
        int deathGoldItemIndex = 0,
        int deathDropSearchRadius = 50,
        uint pvpEarItemId = 24,
        int monsterMeleeMinimumHitChance = 15,
        int monsterRangedMinimumHitChance = 10,
        int monsterMinimumHitChanceLevel14 = 20,
        int monsterMinimumHitChanceLevel15 = 25,
        int monsterMinimumHitChanceLevel16 = 30,
        int monsterToHitLevelScale = 2,
        int monsterToHitBaseBonus = 30,
        int monsterRangedDistancePenalty = 2,
        int randomStealLifeDivisor = 8,
        int stealLife3Percent = 3,
        int stealLife5Percent = 5,
        int stealMana3Percent = 3,
        int stealMana5Percent = 5,
        int hellfireArmorBonusAgainstDemons = 40,
        int hellfireArmorBonusAgainstUndead = 20,
        int pvpEarNativeItemIndex = 23,
        int pvpEarMiscId = 44,
        int monsterMagicMinimumHitChance = 10,
        int monsterMagicToHitMonsterLevelScale = 2,
        int monsterMagicToHitPlayerLevelScale = 2,
        int monsterMagicToHitBaseBonus = 40,
        int monsterMagicDistancePenalty = 2,
        int monsterChargeBaseToHit = 500,
        int monsterAcidPuddleMinimumHitChance = 10,
        int monsterAcidPuddleBaseHitChance = 40,
        int maximumResistance = 75,
        int maximumAttribute = AuthoritativeEquipmentAttributeRules.NativeMaximumAttribute,
        int barbarianWeaponStrengthDamageDivisor = 75,
        int rogueStrengthDexterityDamageDivisor = 200,
        int monkStaffUnarmedDamageDivisor = 150,
        int monkWeaponDamageDivisor = 300,
        int bardSwordStrengthDexterityDamageDivisor = 150,
        int bardBowStrengthDexterityDamageDivisor = 250,
        int bardDefaultStrengthDamageDivisor = 100,
        int standardStrengthDamageDivisor = 100,
        int barbarianBowStrengthDamageDivisor = 300,
        int barbarianDefaultStrengthDamageDivisor = 100,
        int barbarianVitalityDamageDivisor = 100,
        int thornsDamageMinimum = 1,
        int thornsDamageMaximum = 3,
        int hellfireDecayDamageReduction = 5,
        int hellfireDecayRemovalThreshold = 100,
        int hellfireDevastationChancePercent = 5,
        int hellfireDevastationDamageMultiplier = 3,
        int hellfireJestersRollMaximum = 200,
        int hellfireJestersExpansionThreshold = 100,
        int hellfireJestersExpansionMultiplier = 5,
        int hellfirePerilDamageMultiplier = 2,
        int hellfireDoppelgangerChancePercent = 10,
        int tripleDemonDamageMultiplier = 3,
        int pvpBlockLevelScale = 2,
        int equipmentFastBlockFrameSkip = 2,
        int fastHitRecoveryFrameSkip = 1,
        int fasterHitRecoveryFrameSkip = 2,
        int fastestHitRecoveryFrameSkip = 3,
        int quickAttackFrameSkipWithFirstFrame = 1,
        int quickAttackFrameSkipWithoutFirstFrame = 0,
        int fastAttackFrameSkipWithFirstFrame = 2,
        int fastAttackFrameSkipWithoutFirstFrame = 1,
        int fasterAttackFrameSkipWithFirstFrame = 3,
        int fasterAttackFrameSkipWithoutFirstFrame = 2,
        int fastestAttackFrameSkipWithFirstFrame = 4,
        int fastestAttackFrameSkipWithoutFirstFrame = 2,
        int playerMagicHitCharacterLevelScale = 2,
        int playerMagicMinimumHitChance = 5,
        int playerMagicMaximumHitChance = 95,
        int monsterRegenerationLevelDivisor = 2,
        int monsterPlayerBlockLevelScale = 2,
        DiabloDifficultyRules? difficultyRules = null,
        int? rangedAttackFrameSkipWithFirstFrame = null,
        int? rangedAttackFrameSkipWithoutFirstFrame = null,
        int? rangedFastAttackFrameSkipBonus = null,
        uint townLevelId = AuthoritativePortalRules.TownLevelId,
        int uniqueExperienceMultiplier = 2,
        int multipleArrowsCount = 3,
        int multipleArrowsDelayTicks = 2,
        int multipleArrowsSpreadDistance = 1)
    {
        var resolvedRangedAttackFrameSkipWithFirstFrame = rangedAttackFrameSkipWithFirstFrame
            ?? (gameVariant == DiabloGameVariant.Hellfire ? 0 : 1);
        var resolvedRangedAttackFrameSkipWithoutFirstFrame = rangedAttackFrameSkipWithoutFirstFrame
            ?? 0;
        var resolvedRangedFastAttackFrameSkipBonus = rangedFastAttackFrameSkipBonus
            ?? (gameVariant == DiabloGameVariant.Hellfire ? 0 : 1);
        if (baseAttackDamage <= 0 || minimumDamage <= 0 || minimumDamage > baseAttackDamage
            || hitChancePercent is < 0 or > 100 || criticalChancePercent is < 0 or > 100 || criticalMultiplier < 1)
            throw new InvalidDataException("Combat damage bounds are invalid.");
        if (monsterMeleeMinimumHitChance is < 0 or > 100
            || monsterRangedMinimumHitChance is < 0 or > 100
            || monsterMinimumHitChanceLevel14 is < 0 or > 100
            || monsterMinimumHitChanceLevel15 is < 0 or > 100
            || monsterMinimumHitChanceLevel16 is < 0 or > 100) {
            throw new InvalidDataException("Monster hit-chance floors are invalid.");
        }
        if (monsterToHitLevelScale is < 0 or > 64
            || monsterToHitBaseBonus is < 0 or > 255
            || monsterRangedDistancePenalty is < 0 or > 64) {
            throw new InvalidDataException("Monster accuracy formula tuning is invalid.");
        }
        if (monsterMagicMinimumHitChance is < 0 or > 100
            || monsterMagicToHitMonsterLevelScale is < 0 or > 64
            || monsterMagicToHitPlayerLevelScale is < 0 or > 64
            || monsterMagicToHitBaseBonus is < 0 or > 255
            || monsterMagicDistancePenalty is < 0 or > 64) {
            throw new InvalidDataException("Monster magic accuracy formula tuning is invalid.");
        }
        if (monsterChargeBaseToHit is < 0 or > 1000) {
            throw new InvalidDataException("Monster charge accuracy tuning is invalid.");
        }
        if (monsterAcidPuddleMinimumHitChance is < 0 or > 100
            || monsterAcidPuddleBaseHitChance is < 0 or > 100) {
            throw new InvalidDataException("Acid puddle accuracy tuning is invalid.");
        }
        if (maximumResistance is < 0 or > 100) {
            throw new InvalidDataException("Maximum resistance tuning is invalid.");
        }
        AuthoritativeEquipmentAttributeRules.ValidateMaximumAttribute(maximumAttribute);
        if (barbarianWeaponStrengthDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Barbarian weapon damage tuning is invalid.");
        }
        if (rogueStrengthDexterityDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Rogue weapon damage tuning is invalid.");
        }
        if (monkStaffUnarmedDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Monk staff/unarmed damage tuning is invalid.");
        }
        if (monkWeaponDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Monk weapon damage tuning is invalid.");
        }
        if (bardSwordStrengthDexterityDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Bard sword damage tuning is invalid.");
        }
        if (bardBowStrengthDexterityDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Bard bow damage tuning is invalid.");
        }
        if (bardDefaultStrengthDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Bard default damage tuning is invalid.");
        }
        if (standardStrengthDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Standard damage tuning is invalid.");
        }
        if (barbarianBowStrengthDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Barbarian bow damage tuning is invalid.");
        }
        if (barbarianDefaultStrengthDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Barbarian default damage tuning is invalid.");
        }
        if (barbarianVitalityDamageDivisor is < 1 or > 10_000) {
            throw new InvalidDataException("Barbarian vitality damage tuning is invalid.");
        }
        if (thornsDamageMinimum <= 0
            || thornsDamageMaximum < thornsDamageMinimum
            || thornsDamageMaximum > 10_000) {
            throw new InvalidDataException("Thorns damage tuning is invalid.");
        }
        if (hellfireDecayDamageReduction <= 0
            || hellfireDecayDamageReduction > 10_000
            || hellfireDecayRemovalThreshold <= 0
            || hellfireDecayRemovalThreshold > 10_000) {
            throw new InvalidDataException("Hellfire Decay tuning is invalid.");
        }
        if (hellfireDevastationChancePercent is < 0 or > 100
            || hellfireDevastationDamageMultiplier is < 1 or > 100) {
            throw new InvalidDataException("Hellfire Devastation tuning is invalid.");
        }
        if (hellfireJestersRollMaximum <= 0
            || hellfireJestersRollMaximum > 10_000
            || hellfireJestersExpansionThreshold < 0
            || hellfireJestersExpansionThreshold > hellfireJestersRollMaximum
            || hellfireJestersExpansionMultiplier is < 1 or > 100) {
            throw new InvalidDataException("Hellfire Jesters tuning is invalid.");
        }
        if (hellfirePerilDamageMultiplier is < 1 or > 100
            || hellfireDoppelgangerChancePercent is < 0 or > 100) {
            throw new InvalidDataException("Hellfire Peril/Doppelganger tuning is invalid.");
        }
        if (tripleDemonDamageMultiplier is < 1 or > 100) {
            throw new InvalidDataException("Triple Demon damage tuning is invalid.");
        }
        if (uniqueExperienceMultiplier is < 1 or > 100) {
            throw new InvalidDataException("Unique-monster experience tuning is invalid.");
        }
        if (multipleArrowsCount is < 1 or > 3) {
            throw new InvalidDataException("Multiple Arrows count tuning is invalid.");
        }
        if (multipleArrowsDelayTicks is < 0 or > 2) {
            throw new InvalidDataException("Multiple Arrows delay tuning is invalid.");
        }
        if (multipleArrowsSpreadDistance is < 0 or > 8) {
            throw new InvalidDataException("Multiple Arrows spread tuning is invalid.");
        }
        if (pvpBlockLevelScale is < 0 or > 64) {
            throw new InvalidDataException("PvP block level tuning is invalid.");
        }
        if (playerMagicHitCharacterLevelScale is < 0 or > 64
            || playerMagicMinimumHitChance is < 0 or > 100
            || playerMagicMaximumHitChance is < 0 or > 100
            || playerMagicMinimumHitChance > playerMagicMaximumHitChance) {
            throw new InvalidDataException("Player magic accuracy tuning is invalid.");
        }
        if (monsterRegenerationLevelDivisor is < 1 or > 64) {
            throw new InvalidDataException("Monster regeneration tuning is invalid.");
        }
        if (resolvedRangedAttackFrameSkipWithFirstFrame is < 0 or > 64
            || resolvedRangedAttackFrameSkipWithoutFirstFrame is < 0 or > 64
            || resolvedRangedFastAttackFrameSkipBonus is < 0 or > 64) {
            throw new InvalidDataException("Ranged attack frame timing tuning is invalid.");
        }
        if (monsterPlayerBlockLevelScale is < 0 or > 64) {
            throw new InvalidDataException("Monster player-block tuning is invalid.");
        }
        DifficultyRules = (difficultyRules ?? DiabloDifficultyRules.NativeDefaults).Validate();
        if (equipmentFastBlockFrameSkip is < 0 or > 10) {
            throw new InvalidDataException("Equipment FastBlock tuning is invalid.");
        }
        if (fastHitRecoveryFrameSkip is < 0 or > 10
            || fasterHitRecoveryFrameSkip < fastHitRecoveryFrameSkip
            || fastestHitRecoveryFrameSkip < fasterHitRecoveryFrameSkip
            || fastestHitRecoveryFrameSkip > 10) {
            throw new InvalidDataException("Hit recovery tier tuning is invalid.");
        }
        if (quickAttackFrameSkipWithFirstFrame is < 0 or > 10
            || quickAttackFrameSkipWithoutFirstFrame is < 0
            || quickAttackFrameSkipWithoutFirstFrame > quickAttackFrameSkipWithFirstFrame
            || fastAttackFrameSkipWithFirstFrame is < 0 or > 10
            || fastAttackFrameSkipWithoutFirstFrame is < 0
            || fastAttackFrameSkipWithoutFirstFrame > fastAttackFrameSkipWithFirstFrame
            || fasterAttackFrameSkipWithFirstFrame is < 0 or > 10
            || fasterAttackFrameSkipWithoutFirstFrame is < 0
            || fasterAttackFrameSkipWithoutFirstFrame > fasterAttackFrameSkipWithFirstFrame
            || fastestAttackFrameSkipWithFirstFrame is < 0 or > 10
            || fastestAttackFrameSkipWithoutFirstFrame is < 0
            || fastestAttackFrameSkipWithoutFirstFrame > fastestAttackFrameSkipWithFirstFrame) {
            throw new InvalidDataException("Melee attack-speed tier tuning is invalid.");
        }
        if (randomStealLifeDivisor <= 0
            || stealLife3Percent is < 0 or > 100
            || stealLife5Percent is < 0 or > 100
            || stealMana3Percent is < 0 or > 100
            || stealMana5Percent is < 0 or > 100) {
            throw new InvalidDataException("Equipment attack-effect tuning is invalid.");
        }
        if (hellfireArmorBonusAgainstDemons < 0
            || hellfireArmorBonusAgainstUndead < 0) {
            throw new InvalidDataException("Hellfire armor bonus tuning is invalid.");
        }
        if (!Enum.IsDefined(gameVariant))
            throw new InvalidDataException("Combat game variant is invalid.");
        if (restartPositionX < 0 || restartPositionY < 0
            || restartLife <= 0
            || restartLifeScale is <= 0 or > 64
            || restartTransitionTicks is 0 or > 64) {
            throw new InvalidDataException("Combat restart-in-town rules are invalid.");
        }
        if (deathGoldDivisor < 2
            || deathGoldMaximumStack <= 0
            || deathGoldItemType < 0
            || deathGoldItemIndex < 0
            || deathDropSearchRadius is < 1 or > DeterministicGridCrawl.MaximumSupportedRadius
            || pvpEarItemId == 0
            || pvpEarNativeItemIndex < 0
            || pvpEarMiscId < 0) {
            throw new InvalidDataException("Combat player-death drop rules are invalid.");
        }
        BaseAttackDamage = baseAttackDamage;
        MinimumDamage = minimumDamage;
        DefeatExperience = defeatExperience;
        UniqueExperienceMultiplier = uniqueExperienceMultiplier;
        MultipleArrowsCount = multipleArrowsCount;
        MultipleArrowsDelayTicks = multipleArrowsDelayTicks;
        MultipleArrowsSpreadDistance = multipleArrowsSpreadDistance;
        HitChancePercent = hitChancePercent;
        CriticalChancePercent = criticalChancePercent;
        CriticalMultiplier = criticalMultiplier;
        FriendlyFire = friendlyFire;
        GameVariant = gameVariant;
        TownLevelId = townLevelId;
        RestartLevelId = restartLevelId;
        RestartPositionX = restartPositionX;
        RestartPositionY = restartPositionY;
        RestartLife = restartLife;
        RestartLifeScale = restartLifeScale;
        RestartTransitionTicks = restartTransitionTicks;
        DeathGoldDivisor = deathGoldDivisor;
        DeathGoldMaximumStack = deathGoldMaximumStack;
        DeathGoldItemType = deathGoldItemType;
        DeathGoldItemIndex = deathGoldItemIndex;
        DeathDropSearchRadius = deathDropSearchRadius;
        PvpEarItemId = pvpEarItemId;
        PvpEarNativeItemIndex = pvpEarNativeItemIndex;
        PvpEarMiscId = pvpEarMiscId;
        MonsterMeleeMinimumHitChance = monsterMeleeMinimumHitChance;
        MonsterRangedMinimumHitChance = monsterRangedMinimumHitChance;
        MonsterMinimumHitChanceLevel14 = monsterMinimumHitChanceLevel14;
        MonsterMinimumHitChanceLevel15 = monsterMinimumHitChanceLevel15;
        MonsterMinimumHitChanceLevel16 = monsterMinimumHitChanceLevel16;
        MonsterToHitLevelScale = monsterToHitLevelScale;
        MonsterToHitBaseBonus = monsterToHitBaseBonus;
        MonsterRangedDistancePenalty = monsterRangedDistancePenalty;
        RandomStealLifeDivisor = randomStealLifeDivisor;
        StealLife3Percent = stealLife3Percent;
        StealLife5Percent = stealLife5Percent;
        StealMana3Percent = stealMana3Percent;
        StealMana5Percent = stealMana5Percent;
        HellfireArmorBonusAgainstDemons = hellfireArmorBonusAgainstDemons;
        HellfireArmorBonusAgainstUndead = hellfireArmorBonusAgainstUndead;
        MonsterMagicMinimumHitChance = monsterMagicMinimumHitChance;
        MonsterMagicToHitMonsterLevelScale = monsterMagicToHitMonsterLevelScale;
        MonsterMagicToHitPlayerLevelScale = monsterMagicToHitPlayerLevelScale;
        MonsterMagicToHitBaseBonus = monsterMagicToHitBaseBonus;
        MonsterMagicDistancePenalty = monsterMagicDistancePenalty;
        MonsterChargeBaseToHit = monsterChargeBaseToHit;
        MonsterAcidPuddleMinimumHitChance = monsterAcidPuddleMinimumHitChance;
        MonsterAcidPuddleBaseHitChance = monsterAcidPuddleBaseHitChance;
        MaximumResistance = maximumResistance;
        MaximumAttribute = maximumAttribute;
        BarbarianWeaponStrengthDamageDivisor = barbarianWeaponStrengthDamageDivisor;
        RogueStrengthDexterityDamageDivisor = rogueStrengthDexterityDamageDivisor;
        MonkStaffUnarmedDamageDivisor = monkStaffUnarmedDamageDivisor;
        MonkWeaponDamageDivisor = monkWeaponDamageDivisor;
        BardSwordStrengthDexterityDamageDivisor = bardSwordStrengthDexterityDamageDivisor;
        BardBowStrengthDexterityDamageDivisor = bardBowStrengthDexterityDamageDivisor;
        BardDefaultStrengthDamageDivisor = bardDefaultStrengthDamageDivisor;
        StandardStrengthDamageDivisor = standardStrengthDamageDivisor;
        BarbarianBowStrengthDamageDivisor = barbarianBowStrengthDamageDivisor;
        BarbarianDefaultStrengthDamageDivisor = barbarianDefaultStrengthDamageDivisor;
        BarbarianVitalityDamageDivisor = barbarianVitalityDamageDivisor;
        ThornsDamageMinimum = thornsDamageMinimum;
        ThornsDamageMaximum = thornsDamageMaximum;
        HellfireDecayDamageReduction = hellfireDecayDamageReduction;
        HellfireDecayRemovalThreshold = hellfireDecayRemovalThreshold;
        HellfireDevastationChancePercent = hellfireDevastationChancePercent;
        HellfireDevastationDamageMultiplier = hellfireDevastationDamageMultiplier;
        HellfireJestersRollMaximum = hellfireJestersRollMaximum;
        HellfireJestersExpansionThreshold = hellfireJestersExpansionThreshold;
        HellfireJestersExpansionMultiplier = hellfireJestersExpansionMultiplier;
        HellfirePerilDamageMultiplier = hellfirePerilDamageMultiplier;
        HellfireDoppelgangerChancePercent = hellfireDoppelgangerChancePercent;
        TripleDemonDamageMultiplier = tripleDemonDamageMultiplier;
        PvpBlockLevelScale = pvpBlockLevelScale;
        EquipmentFastBlockFrameSkip = equipmentFastBlockFrameSkip;
        FastHitRecoveryFrameSkip = fastHitRecoveryFrameSkip;
        FasterHitRecoveryFrameSkip = fasterHitRecoveryFrameSkip;
        FastestHitRecoveryFrameSkip = fastestHitRecoveryFrameSkip;
        QuickAttackFrameSkipWithFirstFrame = quickAttackFrameSkipWithFirstFrame;
        QuickAttackFrameSkipWithoutFirstFrame = quickAttackFrameSkipWithoutFirstFrame;
        FastAttackFrameSkipWithFirstFrame = fastAttackFrameSkipWithFirstFrame;
        FastAttackFrameSkipWithoutFirstFrame = fastAttackFrameSkipWithoutFirstFrame;
        FasterAttackFrameSkipWithFirstFrame = fasterAttackFrameSkipWithFirstFrame;
        FasterAttackFrameSkipWithoutFirstFrame = fasterAttackFrameSkipWithoutFirstFrame;
        FastestAttackFrameSkipWithFirstFrame = fastestAttackFrameSkipWithFirstFrame;
        FastestAttackFrameSkipWithoutFirstFrame = fastestAttackFrameSkipWithoutFirstFrame;
        PlayerMagicHitCharacterLevelScale = playerMagicHitCharacterLevelScale;
        PlayerMagicMinimumHitChance = playerMagicMinimumHitChance;
        PlayerMagicMaximumHitChance = playerMagicMaximumHitChance;
        MonsterRegenerationLevelDivisor = monsterRegenerationLevelDivisor;
        MonsterPlayerBlockLevelScale = monsterPlayerBlockLevelScale;
        RangedAttackFrameSkipWithFirstFrame = resolvedRangedAttackFrameSkipWithFirstFrame;
        RangedAttackFrameSkipWithoutFirstFrame = resolvedRangedAttackFrameSkipWithoutFirstFrame;
        RangedFastAttackFrameSkipBonus = resolvedRangedFastAttackFrameSkipBonus;
    }

    public int BaseAttackDamage { get; }

    public int MinimumDamage { get; }

    public uint DefeatExperience { get; }

    /** Native multiplier applied to experience awarded by unique monsters. */
    public int UniqueExperienceMultiplier { get; }

    /** Native number of weapon arrows emitted by the Multiple Arrows effect. */
    public int MultipleArrowsCount { get; }

    /** Native delay before each follow-up Multiple Arrows missile begins travel. */
    public int MultipleArrowsDelayTicks { get; }

    /** Native perpendicular cell offset applied to follow-up Multiple Arrows missiles. */
    public int MultipleArrowsSpreadDistance { get; }

    public int HitChancePercent { get; }

    public int CriticalChancePercent { get; }

    public int CriticalMultiplier { get; }

    /** Matches the native game-session option that can override friendly mode. */
    public bool FriendlyFire { get; }

    /** Selects native base-game or Hellfire-only deterministic behavior. */
    public DiabloGameVariant GameVariant { get; }

    /** External world identity used by town portal and warp admission. */
    public uint TownLevelId { get; }

    public uint RestartLevelId { get; }

    public int RestartPositionX { get; }

    public int RestartPositionY { get; }

    public int RestartLife { get; }

    public int RestartLifeScale { get; }

    public uint RestartTransitionTicks { get; }

    /** Integer divisor applied independently to retained and dropped death gold. */
    public int DeathGoldDivisor { get; }

    public int DeathGoldMaximumStack { get; }

    /** Native `ItemType::Gold` value projected by compatibility clients. */
    public int DeathGoldItemType { get; }

    /** Native `IDI_GOLD` index projected by compatibility clients. */
    public int DeathGoldItemIndex { get; }

    public int DeathDropSearchRadius { get; }

    /** Stable content item ID for the native `IDI_EAR` definition. */
    public uint PvpEarItemId { get; }

    /** Native item-index projection required for compatibility PvP ears. */
    public int PvpEarNativeItemIndex { get; }

    /** Native miscellaneous-ID projection required for compatibility PvP ears. */
    public int PvpEarMiscId { get; }

    /** Native minimum hit chance for ordinary monster melee attacks. */
    public int MonsterMeleeMinimumHitChance { get; }

    /** Native minimum hit chance for ordinary monster ranged attacks. */
    public int MonsterRangedMinimumHitChance { get; }

    /** Native minimum hit chance on level 14, regardless of attack type. */
    public int MonsterMinimumHitChanceLevel14 { get; }

    /** Native minimum hit chance on level 15, regardless of attack type. */
    public int MonsterMinimumHitChanceLevel15 { get; }

    /** Native minimum hit chance on level 16, regardless of attack type. */
    public int MonsterMinimumHitChanceLevel16 { get; }

    /** Native level-difference multiplier in monster accuracy formulas. */
    public int MonsterToHitLevelScale { get; }

    /** Native flat accuracy bonus in monster accuracy formulas. */
    public int MonsterToHitBaseBonus { get; }

    /** Native per-tile accuracy penalty for monster ranged attacks. */
    public int MonsterRangedDistancePenalty { get; }

    /** Native minimum hit chance for ordinary monster magic projectiles. */
    public int MonsterMagicMinimumHitChance { get; }

    /** Native monster-level multiplier in monster magic projectile accuracy. */
    public int MonsterMagicToHitMonsterLevelScale { get; }

    /** Native player-level multiplier in monster magic projectile accuracy. */
    public int MonsterMagicToHitPlayerLevelScale { get; }

    /** Native flat accuracy bonus in monster magic projectile accuracy. */
    public int MonsterMagicToHitBaseBonus { get; }

    /** Native per-tile accuracy penalty for monster magic projectiles. */
    public int MonsterMagicDistancePenalty { get; }

    /** Native base to-hit value passed to a monster Snake charge attack. */
    public int MonsterChargeBaseToHit { get; }

    /** Native minimum hit chance for persistent Acid Puddle damage. */
    public int MonsterAcidPuddleMinimumHitChance { get; }

    /** Native fixed hit chance used by persistent Acid Puddle damage. */
    public int MonsterAcidPuddleBaseHitChance { get; }

    /** Native maximum elemental resistance percentage. */
    public int MaximumResistance { get; }

    /** Maximum effective primary attribute after equipment bonuses. */
    public int MaximumAttribute { get; }

    /** Native Barbarian axe/mace strength-to-damage divisor. */
    public int BarbarianWeaponStrengthDamageDivisor { get; }

    /** Native Rogue strength-plus-dexterity damage divisor. */
    public int RogueStrengthDexterityDamageDivisor { get; }

    /** Native Monk staff/unarmed strength-plus-dexterity damage divisor. */
    public int MonkStaffUnarmedDamageDivisor { get; }

    /** Native Monk weapon strength-plus-dexterity damage divisor. */
    public int MonkWeaponDamageDivisor { get; }

    /** Native Bard sword strength-plus-dexterity damage divisor. */
    public int BardSwordStrengthDexterityDamageDivisor { get; }

    /** Native Bard bow strength-plus-dexterity damage divisor. */
    public int BardBowStrengthDexterityDamageDivisor { get; }

    /** Native Bard fallback strength damage divisor without a sword or bow. */
    public int BardDefaultStrengthDamageDivisor { get; }

    /** Native standard strength damage divisor for non-specialized classes. */
    public int StandardStrengthDamageDivisor { get; }

    /** Native Barbarian bow strength damage divisor. */
    public int BarbarianBowStrengthDamageDivisor { get; }

    /** Native Barbarian fallback strength damage divisor. */
    public int BarbarianDefaultStrengthDamageDivisor { get; }

    /** Native Barbarian vitality damage divisor for weaponless physical attacks. */
    public int BarbarianVitalityDamageDivisor { get; }

    /** Native minimum damage rolled by a successful Thorns retaliation. */
    public int ThornsDamageMinimum { get; }

    /** Native maximum damage rolled by a successful Thorns retaliation. */
    public int ThornsDamageMaximum { get; }

    /** Native damage-bonus reduction applied by each Hellfire Decay proc. */
    public int HellfireDecayDamageReduction { get; }

    /** Positive native damage-bonus threshold that removes a Decay weapon. */
    public int HellfireDecayRemovalThreshold { get; }

    /** Native post-hit proc chance for Hellfire Devastation. */
    public int HellfireDevastationChancePercent { get; }

    /** Native damage multiplier applied by a successful Devastation proc. */
    public int HellfireDevastationDamageMultiplier { get; }

    /** Native inclusive upper bound for the Hellfire Jesters damage roll. */
    public int HellfireJestersRollMaximum { get; }

    /** Native threshold at which Jesters expands its upper-half roll. */
    public int HellfireJestersExpansionThreshold { get; }

    /** Native multiplier applied to the upper-half Jesters roll. */
    public int HellfireJestersExpansionMultiplier { get; }

    /** Native final-damage multiplier applied while Hellfire Peril is active. */
    public int HellfirePerilDamageMultiplier { get; }

    /** Native post-hit proc chance for Hellfire Doppelganger. */
    public int HellfireDoppelgangerChancePercent { get; }

    /** Native damage multiplier applied by the Triple Demon Damage effect. */
    public int TripleDemonDamageMultiplier { get; }

    /** Native level-difference scale used by player-versus-player block chance. */
    public int PvpBlockLevelScale { get; }

    /** Native frame skip applied by equipment carrying the FastBlock flag. */
    public int EquipmentFastBlockFrameSkip { get; }

    /** Native skip for equipment carrying the Fast Hit Recovery flag. */
    public int FastHitRecoveryFrameSkip { get; }

    /** Native skip for equipment carrying the Faster Hit Recovery flag. */
    public int FasterHitRecoveryFrameSkip { get; }

    /** Native skip for equipment carrying the Fastest Hit Recovery flag. */
    public int FastestHitRecoveryFrameSkip { get; }

    /** Native melee attack-speed tier skips when the first animation frame is included. */
    public int QuickAttackFrameSkipWithFirstFrame { get; }

    public int FastAttackFrameSkipWithFirstFrame { get; }

    public int FasterAttackFrameSkipWithFirstFrame { get; }

    public int FastestAttackFrameSkipWithFirstFrame { get; }

    /** Native melee attack-speed tier skips after the first animation frame. */
    public int QuickAttackFrameSkipWithoutFirstFrame { get; }

    public int FastAttackFrameSkipWithoutFirstFrame { get; }

    public int FasterAttackFrameSkipWithoutFirstFrame { get; }

    public int FastestAttackFrameSkipWithoutFirstFrame { get; }

    /** Native character-level penalty in direct player spell magic-hit rolls. */
    public int PlayerMagicHitCharacterLevelScale { get; }

    /** Native lower bound in direct player spell magic-hit rolls. */
    public int PlayerMagicMinimumHitChance { get; }

    /** Native upper bound in direct player spell magic-hit rolls. */
    public int PlayerMagicMaximumHitChance { get; }

    /** Native divisor applied to monster level for higher-level regeneration. */
    public int MonsterRegenerationLevelDivisor { get; }

    /** Native level-difference scale for monster attacks that can be blocked. */
    public int MonsterPlayerBlockLevelScale { get; }

    /** Native ranged-attack frame skip when the animation includes its first frame. */
    public int RangedAttackFrameSkipWithFirstFrame { get; }

    /** Native ranged-attack frame skip when the animation starts after its first frame. */
    public int RangedAttackFrameSkipWithoutFirstFrame { get; }

    /** Additional native ranged frame skip contributed by a Fast Attack flag. */
    public int RangedFastAttackFrameSkipBonus { get; }

    /** Native difficulty level, accuracy, armor, damage, health, and XP modifiers. */
    public DiabloDifficultyRules DifficultyRules { get; }

    /** Native divisor for the random life-steal equipment effect. */
    public int RandomStealLifeDivisor { get; }

    /** Native percentage for the life-steal-3 equipment effect. */
    public int StealLife3Percent { get; }

    /** Native percentage for the life-steal-5 equipment effect. */
    public int StealLife5Percent { get; }

    /** Native percentage for the mana-steal-3 equipment effect. */
    public int StealMana3Percent { get; }

    /** Native percentage for the mana-steal-5 equipment effect. */
    public int StealMana5Percent { get; }

    /** Native Hellfire armor bonus against demon-class monsters. */
    public int HellfireArmorBonusAgainstDemons { get; }

    /** Native Hellfire armor bonus against undead-class monsters. */
    public int HellfireArmorBonusAgainstUndead { get; }

    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!items.TryGet(PvpEarItemId, out var ear))
            throw new InvalidDataException(
                $"Combat PvP-ear content references unknown item {PvpEarItemId}.");
        if (ear.NativeItemIndex != PvpEarNativeItemIndex
            || ear.MiscId != PvpEarMiscId) {
            throw new InvalidDataException(
                "Combat PvP-ear content must project native IDI_EAR identity.");
        }
    }

    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.ContainsLevel(TownLevelId)) {
            throw new InvalidDataException(
                $"Combat rules reference unknown town level {TownLevelId}.");
        }
        if (!world.IsWalkable(RestartLevelId, RestartPositionX, RestartPositionY)) {
            throw new InvalidDataException(
                "Combat restart-in-town destination must be a walkable world position.");
        }
    }

    public int ResolveAttackDamage(int baseDamage, AuthoritativeCombatTarget target, uint seed)
    {
        return ResolveAttackDamage(baseDamage, target.ArmorClass, seed);
    }

    public int ResolveAttackDamage(int baseDamage, int armorClass, uint seed)
    {
        return ResolveAttackDamage(baseDamage, armorClass, HitChancePercent, seed);
    }

    /** Resolves a physical player attack with an equipment-adjusted hit chance. */
    public int ResolveAttackDamage(
        int baseDamage,
        int armorClass,
        int hitChancePercent,
        uint seed)
    {
        if (baseDamage <= 0)
            return 0;
        if (hitChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(hitChancePercent));
        if (!ResolveAttackHit(hitChancePercent, seed))
            return 0;
        var damage = baseDamage;
        var criticalRoll = unchecked(seed * 1103515245U + 12345U) % 100U;
        if (criticalRoll < CriticalChancePercent)
            damage = checked(damage * CriticalMultiplier);
        return Math.Max(0, Math.Max(MinimumDamage, damage - Math.Max(0, armorClass)));
    }

    /** Resolves a native monster's normal physical hit against a player. */
    public int ResolveMonsterAttackHitChance(
        int monsterToHit,
        int monsterLevel,
        uint targetCharacterLevel,
        uint targetLevelId,
        int targetArmorClass)
    {
        // Nightmare/Hell add native accuracy bonuses after the byte-sized
        // content value; the final hit chance is clamped below.
        if (monsterToHit < 0 || monsterLevel < 0 || targetArmorClass < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterToHit));

        // DevilutionX's MonsterAttackPlayer computes to-hit as the content
        // baseline plus configured level delta and base bonus, then subtracts
        // player armor. The level-specific floor is the native GetMinHit() rule.
        var minimumHitChance = ResolveMonsterMinimumHitChance(
            targetLevelId,
            MonsterMeleeMinimumHitChance);
        var levelDelta = checked(
            MonsterToHitLevelScale * (monsterLevel - checked((int)targetCharacterLevel)));
        return Math.Clamp(
            Math.Max(
                minimumHitChance,
                checked(monsterToHit + levelDelta + MonsterToHitBaseBonus - targetArmorClass)),
            0,
            100);
    }

    /** Resolves the damage value after native MonsterAttackPlayer hit/block rolls. */
    public int ResolveMonsterAttackDamageAfterHit(int baseDamage)
    {
        return baseDamage <= 0 ? 0 : Math.Max(1, baseDamage);
    }

    /** Resolves a native monster's normal physical hit against a player. */
    public int ResolveMonsterAttackDamage(
        int baseDamage,
        int monsterToHit,
        int monsterLevel,
        uint targetCharacterLevel,
        uint targetLevelId,
        int targetArmorClass,
        uint seed)
    {
        if (baseDamage <= 0 || monsterToHit <= 0)
            return 0;
        var hitChance = ResolveMonsterAttackHitChance(
            monsterToHit,
            monsterLevel,
            targetCharacterLevel,
            targetLevelId,
            targetArmorClass);
        if (!ResolveAttackHit(hitChance, seed))
            return 0;
        return ResolveMonsterAttackDamageAfterHit(baseDamage);
    }

    /** Resolves a native monster arrow hit, including its distance penalty. */
    public int ResolveMonsterRangedAttackDamage(
        int baseDamage,
        int monsterToHit,
        int monsterLevel,
        uint targetCharacterLevel,
        uint targetLevelId,
        int targetArmorClass,
        int distance,
        uint seed)
    {
        if (baseDamage <= 0 || monsterToHit <= 0)
            return 0;
        // Nightmare/Hell add native accuracy bonuses after the byte-sized
        // content value; the final hit chance is clamped below.
        if (monsterToHit < 0
            || monsterLevel < 0
            || targetArmorClass < 0
            || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterToHit));

        var minimumHitChance = ResolveMonsterMinimumHitChance(
            targetLevelId,
            MonsterRangedMinimumHitChance);
        var levelDelta = checked(
            MonsterToHitLevelScale * (monsterLevel - checked((int)targetCharacterLevel)));
        var hitChance = Math.Max(
            minimumHitChance,
            checked(monsterToHit
                + levelDelta
                + MonsterToHitBaseBonus
                - (MonsterRangedDistancePenalty * distance)
                - targetArmorClass));
        if (!ResolveAttackHit(Math.Clamp(hitChance, 0, 100), seed))
            return 0;
        return Math.Max(1, baseDamage);
    }

    /** Resolves the native admission chance for a monster-owned magic projectile. */
    public int ResolveMonsterMagicProjectileHitChance(
        int monsterLevel,
        uint targetCharacterLevel,
        uint targetLevelId,
        int distance)
    {
        if (monsterLevel < 0 || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterLevel));

        var levelDelta = checked(
            MonsterMagicToHitMonsterLevelScale * monsterLevel
            - MonsterMagicToHitPlayerLevelScale * checked((int)targetCharacterLevel));
        var hitChance = Math.Max(
            ResolveMonsterMinimumHitChance(targetLevelId, MonsterMagicMinimumHitChance),
            checked(MonsterMagicToHitBaseBonus
                + levelDelta
                - (MonsterMagicDistancePenalty * distance)));
        return Math.Clamp(hitChance, 0, 100);
    }

    /** Resolves the native special-hit admission chance for a Snake charge. */
    public int ResolveMonsterChargeHitChance(
        int monsterLevel,
        uint targetCharacterLevel,
        uint targetLevelId,
        int targetArmorClass)
    {
        if (monsterLevel < 0 || targetArmorClass < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterLevel));

        var levelDelta = checked(
            MonsterToHitLevelScale * (monsterLevel - checked((int)targetCharacterLevel)));
        var hitChance = Math.Max(
            ResolveMonsterMinimumHitChance(targetLevelId, MonsterMeleeMinimumHitChance),
            checked(MonsterChargeBaseToHit
                + levelDelta
                + MonsterToHitBaseBonus
                - targetArmorClass));
        return Math.Clamp(hitChance, 0, 100);
    }

    /** Resolves the native persistent Acid Puddle player-hit admission chance. */
    public int ResolveMonsterAcidPuddleHitChance(uint targetLevelId)
    {
        return Math.Clamp(
            Math.Max(
                ResolveMonsterMinimumHitChance(
                    targetLevelId,
                    MonsterAcidPuddleMinimumHitChance),
                MonsterAcidPuddleBaseHitChance),
            0,
            100);
    }

    /** Resolves the native hit roll independently from post-armor damage. */
    public bool ResolveAttackHit(int hitChancePercent, uint seed)
    {
        if (hitChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(hitChancePercent));
        var hitRoll = unchecked(seed * 1664525U + 1013904223U) % 100U;
        return hitRoll < hitChancePercent;
    }

    private int ResolveMonsterMinimumHitChance(uint targetLevelId, int fallback)
    {
        return targetLevelId switch {
            14 => MonsterMinimumHitChanceLevel14,
            15 => MonsterMinimumHitChanceLevel15,
            16 => MonsterMinimumHitChanceLevel16,
            _ => fallback,
        };
    }

    /** Mirrors native physical hit chance after armor and piercing rules. */
    public int ResolvePhysicalHitChance(
        int attackerToHit,
        int targetArmorClass,
        int armorPiercingBonus = 0,
        bool barbarianMelee = false)
        => ResolvePhysicalHitChance(
            attackerToHit,
            targetArmorClass,
            armorPiercingBonus,
            barbarianMelee ? 8 : 0);

    /** Resolves physical hit chance with a content-declared melee armor divisor. */
    public int ResolvePhysicalHitChance(
        int attackerToHit,
        int targetArmorClass,
        int armorPiercingBonus,
        int meleeArmorReductionDivisor)
    {
        var effectiveArmor = targetArmorClass;
        if (meleeArmorReductionDivisor < 0)
            throw new ArgumentOutOfRangeException(nameof(meleeArmorReductionDivisor));
        if (armorPiercingBonus > 0) {
            if (GameVariant == DiabloGameVariant.Hellfire) {
                var armorPiercingShift = armorPiercingBonus - 1;
                effectiveArmor = armorPiercingShift > 0
                    ? effectiveArmor >> armorPiercingShift
                    : effectiveArmor - effectiveArmor / 4;
            }
            if (meleeArmorReductionDivisor > 0)
                effectiveArmor -= targetArmorClass / meleeArmorReductionDivisor;
        }
        effectiveArmor = Math.Max(0, effectiveArmor);
        var piercing = GameVariant == DiabloGameVariant.Diablo
            ? armorPiercingBonus
            : 0;
        return Math.Clamp(
            checked(attackerToHit + piercing - effectiveArmor),
            5,
            95);
    }

    public int ResolveDamage(int baseDamage, int armorClass, AuthoritativeDamageType damageType, AuthoritativeCombatTarget target)
    {
        if (baseDamage <= 0)
            return 0;
        var postArmor = damageType == AuthoritativeDamageType.Physical
            ? Math.Max(MinimumDamage, baseDamage - Math.Max(0, armorClass))
            : baseDamage;
        var resistance = damageType switch {
            AuthoritativeDamageType.Fire => target.FireResistance,
            AuthoritativeDamageType.Lightning => target.LightningResistance,
            AuthoritativeDamageType.Magic => target.MagicResistance,
            AuthoritativeDamageType.Acid => target.AcidResistance,
            _ => 0,
        };
        return Math.Max(0, postArmor * (100 - Math.Clamp(resistance, -100, 100)) / 100);
    }

    /**
     * Rejects omitted combat tuning at the current production content
     * boundary. Direct callers may continue to load partial compatibility rows.
     */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Rows.Count != 1)
            throw new InvalidDataException(
                $"Production combat table '{table.SourcePath}' must contain exactly one row.");

        var missingColumns = ExternalizedProductionColumns
            .Where(column => !table.Columns.Contains(column, StringComparer.Ordinal))
            .ToArray();
        if (missingColumns.Length != 0)
            throw new InvalidDataException(
                $"Production combat table '{table.SourcePath}' is missing required columns: "
                + string.Join(", ", missingColumns));

        var row = table.Rows[0];
        foreach (var column in ExternalizedProductionColumns)
            _ = row.Required(column);
    }

    public static AuthoritativeCombatRules LoadTsv(
        string sourcePath,
        string contents,
        uint townLevelId = AuthoritativePortalRules.TownLevelId,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var row = table.Rows.Single();
        var hasPvpEarItemId = row.TryGet("pvp_ear_item_id", out var pvpEarItemValue)
            && !string.IsNullOrWhiteSpace(pvpEarItemValue);
        var hasPvpEarItemSymbol = row.TryGet("pvp_ear_item_symbol", out var pvpEarItemSymbol)
            && !string.IsNullOrWhiteSpace(pvpEarItemSymbol);
        var pvpEarItemId = hasPvpEarItemId || hasPvpEarItemSymbol
            ? AuthoritativeItemIdentity.ResolveOptional(
                row,
                stableIds,
                "pvp_ear_item_id",
                "pvp_ear_item_symbol")
            : 24U;
        return new AuthoritativeCombatRules(
            row.RequiredInt32("base_attack_damage"),
            row.RequiredInt32("minimum_damage"),
            row.RequiredUInt32("defeat_experience"),
            row.OptionalInt32("hit_chance_percent", 100),
            row.OptionalInt32("critical_chance_percent"),
            row.OptionalInt32("critical_multiplier", 2),
            row.OptionalBoolean("friendly_fire", true),
            ParseGameVariant(row),
            AuthoritativeLevelIdentity.ResolveOptional(
                row,
                stableIds,
                "restart_level_id",
                "restart_level_symbol"),
            row.OptionalInt32("restart_position_x"),
            row.OptionalInt32("restart_position_y"),
            row.OptionalInt32("restart_life", 1),
            row.OptionalInt32("restart_life_scale", 64),
            row.OptionalUInt32("restart_transition_ticks", 1),
            row.OptionalInt32("death_gold_divisor", 2),
            row.OptionalInt32("death_gold_maximum_stack", 5000),
            row.OptionalInt32("death_gold_item_type", 11),
            row.OptionalInt32("death_gold_item_index"),
            row.OptionalInt32("death_drop_search_radius", 50),
            pvpEarItemId,
            row.OptionalInt32("monster_melee_minimum_hit_chance", 15),
            row.OptionalInt32("monster_ranged_minimum_hit_chance", 10),
            row.OptionalInt32("monster_minimum_hit_chance_level_14", 20),
            row.OptionalInt32("monster_minimum_hit_chance_level_15", 25),
            row.OptionalInt32("monster_minimum_hit_chance_level_16", 30),
            row.OptionalInt32("monster_to_hit_level_scale", 2),
            row.OptionalInt32("monster_to_hit_base_bonus", 30),
            row.OptionalInt32("monster_ranged_distance_penalty", 2),
            row.OptionalInt32("random_steal_life_divisor", 8),
            row.OptionalInt32("steal_life_3_percent", 3),
            row.OptionalInt32("steal_life_5_percent", 5),
            row.OptionalInt32("steal_mana_3_percent", 3),
            row.OptionalInt32("steal_mana_5_percent", 5),
            row.OptionalInt32("hellfire_armor_bonus_against_demons", 40),
            row.OptionalInt32("hellfire_armor_bonus_against_undead", 20),
            row.OptionalInt32("pvp_ear_native_item_index", 23),
            row.OptionalInt32("pvp_ear_misc_id", 44),
            row.OptionalInt32("monster_magic_minimum_hit_chance", 10),
            row.OptionalInt32("monster_magic_to_hit_monster_level_scale", 2),
            row.OptionalInt32("monster_magic_to_hit_player_level_scale", 2),
            row.OptionalInt32("monster_magic_to_hit_base_bonus", 40),
            row.OptionalInt32("monster_magic_distance_penalty", 2),
            row.OptionalInt32("monster_charge_base_to_hit", 500),
            row.OptionalInt32("monster_acid_puddle_minimum_hit_chance", 10),
            row.OptionalInt32("monster_acid_puddle_base_hit_chance", 40),
            row.OptionalInt32("maximum_resistance", 75),
            row.OptionalInt32("maximum_attribute", AuthoritativeEquipmentAttributeRules.NativeMaximumAttribute),
            row.OptionalInt32("barbarian_weapon_strength_damage_divisor", 75),
            row.OptionalInt32("rogue_strength_dexterity_damage_divisor", 200),
            row.OptionalInt32("monk_staff_unarmed_damage_divisor", 150),
            row.OptionalInt32("monk_weapon_damage_divisor", 300),
            row.OptionalInt32("bard_sword_strength_dexterity_damage_divisor", 150),
            row.OptionalInt32("bard_bow_strength_dexterity_damage_divisor", 250),
            row.OptionalInt32("bard_default_strength_damage_divisor", 100),
            row.OptionalInt32("standard_strength_damage_divisor", 100),
            row.OptionalInt32("barbarian_bow_strength_damage_divisor", 300),
            row.OptionalInt32("barbarian_default_strength_damage_divisor", 100),
            row.OptionalInt32("barbarian_vitality_damage_divisor", 100),
            row.OptionalInt32("thorns_damage_minimum", 1),
            row.OptionalInt32("thorns_damage_maximum", 3),
            row.OptionalInt32("hellfire_decay_damage_reduction", 5),
            row.OptionalInt32("hellfire_decay_removal_threshold", 100),
            row.OptionalInt32("hellfire_devastation_chance_percent", 5),
            row.OptionalInt32("hellfire_devastation_damage_multiplier", 3),
            row.OptionalInt32("hellfire_jesters_roll_maximum", 200),
            row.OptionalInt32("hellfire_jesters_expansion_threshold", 100),
            row.OptionalInt32("hellfire_jesters_expansion_multiplier", 5),
            row.OptionalInt32("hellfire_peril_damage_multiplier", 2),
            row.OptionalInt32("hellfire_doppelganger_chance_percent", 10),
            row.OptionalInt32("triple_demon_damage_multiplier", 3),
            row.OptionalInt32("pvp_block_level_scale", 2),
            row.OptionalInt32("equipment_fast_block_frame_skip", 2),
            row.OptionalInt32("fast_hit_recovery_frame_skip", 1),
            row.OptionalInt32("faster_hit_recovery_frame_skip", 2),
            row.OptionalInt32("fastest_hit_recovery_frame_skip", 3),
            row.OptionalInt32("quick_attack_frame_skip_with_first_frame", 1),
            row.OptionalInt32("quick_attack_frame_skip_without_first_frame"),
            row.OptionalInt32("fast_attack_frame_skip_with_first_frame", 2),
            row.OptionalInt32("fast_attack_frame_skip_without_first_frame", 1),
            row.OptionalInt32("faster_attack_frame_skip_with_first_frame", 3),
            row.OptionalInt32("faster_attack_frame_skip_without_first_frame", 2),
            row.OptionalInt32("fastest_attack_frame_skip_with_first_frame", 4),
            row.OptionalInt32("fastest_attack_frame_skip_without_first_frame", 2),
            row.OptionalInt32("player_magic_hit_character_level_scale", 2),
            row.OptionalInt32("player_magic_minimum_hit_chance", 5),
            row.OptionalInt32("player_magic_maximum_hit_chance", 95),
            row.OptionalInt32("monster_regeneration_level_divisor", 2),
            row.OptionalInt32("monster_player_block_level_scale", 2),
            DiabloDifficultyRules.LoadTsv(row),
            row.OptionalInt32("ranged_attack_frame_skip_with_first_frame", 1),
            row.OptionalInt32("ranged_attack_frame_skip_without_first_frame"),
            row.OptionalInt32("ranged_fast_attack_frame_skip_bonus", 1),
            townLevelId,
            row.OptionalInt32("unique_experience_multiplier", 2),
            row.OptionalInt32("multiple_arrows_count", 3),
            row.OptionalInt32("multiple_arrows_delay_ticks", 2),
            row.OptionalInt32("multiple_arrows_spread_distance", 1));
    }

    private static DiabloGameVariant ParseGameVariant(TsvRow row)
    {
        var value = row.TryGet("game_variant", out var configured)
            && !string.IsNullOrWhiteSpace(configured)
            ? configured
            : nameof(DiabloGameVariant.Diablo);
        if (Enum.TryParse<DiabloGameVariant>(value, ignoreCase: false, out var variant)
            && Enum.IsDefined(variant)
            && Enum.GetName(variant) == value) {
            return variant;
        }
        throw new InvalidDataException(
            $"Combat table row {row.LineNumber} contains unknown game variant '{value}'.");
    }
}
