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
    private const int NativeEarItemIndex = 23;
    private const int NativeEarMiscId = 44;

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
        uint pvpEarItemId = 24)
    {
        if (baseAttackDamage <= 0 || minimumDamage <= 0 || minimumDamage > baseAttackDamage
            || hitChancePercent is < 0 or > 100 || criticalChancePercent is < 0 or > 100 || criticalMultiplier < 1)
            throw new InvalidDataException("Combat damage bounds are invalid.");
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
            || pvpEarItemId == 0) {
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

    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var ear = items.GetRequired(PvpEarItemId);
        if (ear.NativeItemIndex != NativeEarItemIndex
            || ear.MiscId != NativeEarMiscId) {
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
        if (baseDamage <= 0)
            return 0;
        var hitRoll = unchecked(seed * 1664525U + 1013904223U) % 100U;
        if (hitRoll >= HitChancePercent)
            return 0;
        var damage = baseDamage;
        var criticalRoll = unchecked(seed * 1103515245U + 12345U) % 100U;
        if (criticalRoll < CriticalChancePercent)
            damage = checked(damage * CriticalMultiplier);
        return Math.Max(0, Math.Max(MinimumDamage, damage - Math.Max(0, armorClass)));
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
            row.OptionalUInt32("pvp_ear_item_id", 24));
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
