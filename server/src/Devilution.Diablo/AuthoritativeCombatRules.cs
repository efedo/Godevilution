using Devilution.Data;

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
        int monsterChargeBaseToHit = 500)
    {
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
            || deathDropSearchRadius is < 1 or > DiabloCrawl.MaximumSupportedRadius
            || pvpEarItemId == 0
            || pvpEarNativeItemIndex < 0
            || pvpEarMiscId < 0) {
            throw new InvalidDataException("Combat player-death drop rules are invalid.");
        }
        BaseAttackDamage = baseAttackDamage;
        MinimumDamage = minimumDamage;
        DefeatExperience = defeatExperience;
        HitChancePercent = hitChancePercent;
        CriticalChancePercent = criticalChancePercent;
        CriticalMultiplier = criticalMultiplier;
        FriendlyFire = friendlyFire;
        GameVariant = gameVariant;
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
    }

    public int BaseAttackDamage { get; }

    public int MinimumDamage { get; }

    public uint DefeatExperience { get; }

    public int HitChancePercent { get; }

    public int CriticalChancePercent { get; }

    public int CriticalMultiplier { get; }

    /** Matches the native game-session option that can override friendly mode. */
    public bool FriendlyFire { get; }

    /** Selects native base-game or Hellfire-only deterministic behavior. */
    public DiabloGameVariant GameVariant { get; }

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
        var ear = items.GetRequired(PvpEarItemId);
        if (ear.NativeItemIndex != PvpEarNativeItemIndex
            || ear.MiscId != PvpEarMiscId) {
            throw new InvalidDataException(
                "Combat PvP-ear content must project native IDI_EAR identity.");
        }
    }

    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
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
        if (monsterToHit > 255 || monsterLevel < 0 || targetArmorClass < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterToHit));

        // DevilutionX's MonsterAttackPlayer computes to-hit as the content
        // baseline plus configured level delta and base bonus, then subtracts
        // player armor. The level-specific floor is the native GetMinHit() rule.
        var minimumHitChance = ResolveMonsterMinimumHitChance(
            targetLevelId,
            MonsterMeleeMinimumHitChance);
        var levelDelta = checked(
            MonsterToHitLevelScale * (monsterLevel - checked((int)targetCharacterLevel)));
        var hitChance = Math.Max(
            minimumHitChance,
            checked(monsterToHit + levelDelta + MonsterToHitBaseBonus - targetArmorClass));
        if (!ResolveAttackHit(Math.Clamp(hitChance, 0, 100), seed))
            return 0;
        return Math.Max(1, baseDamage);
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
        if (monsterToHit > 255
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

    public static AuthoritativeCombatRules LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var row = table.Rows.Single();
        return new AuthoritativeCombatRules(
            row.RequiredInt32("base_attack_damage"),
            row.RequiredInt32("minimum_damage"),
            row.RequiredUInt32("defeat_experience"),
            row.OptionalInt32("hit_chance_percent", 100),
            row.OptionalInt32("critical_chance_percent"),
            row.OptionalInt32("critical_multiplier", 2),
            row.OptionalInt32("friendly_fire", 1) != 0,
            ParseGameVariant(row),
            row.OptionalUInt32("restart_level_id"),
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
            row.OptionalUInt32("pvp_ear_item_id", 24),
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
            row.OptionalInt32("monster_charge_base_to_hit", 500));
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
