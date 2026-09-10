using Devilution.Data;
using Devilution.Shared;

namespace Devilution.Diablo;

[Flags]
public enum DiabloPlayerClassFlags : uint
{
    None = 0,
    CriticalStrike = 1U << 0,
    DualWield = 1U << 1,
    IronSkin = 1U << 2,
    NaturalResistance = 1U << 3,
    TrapSense = 1U << 4,
    FlexibleBlock = 1U << 5,
    FastBlock = 1U << 6,
    HeavyHitThreshold = 1U << 7,
}

public enum DiabloPlayerClassAvailability
{
    BaseGame,
    Hellfire,
    HellfireHidden,
}

/** Native class formula used to resolve equipped physical damage modifiers. */
public enum AuthoritativePhysicalDamageFormula
{
    Standard,
    Rogue,
    Monk,
    Bard,
    Barbarian,
}

/** Complete deterministic class metadata mirrored from the native tables. */
public sealed record AuthoritativePlayerClassDefinition(
    uint ClassId,
    string Symbol,
    string DisplayName,
    DiabloPlayerClassAvailability Availability,
    bool IsDefault,
    DiabloPlayerClassFlags Flags,
    int BaseStrength,
    int BaseMagic,
    int BaseDexterity,
    int BaseVitality,
    int MaximumStrength,
    int MaximumMagic,
    int MaximumDexterity,
    int MaximumVitality,
    int BaseBlockChance,
    int BaseMagicToHit,
    int BaseMeleeToHit,
    int BaseRangedToHit,
    int LifeAdjustmentQ6,
    int ManaAdjustmentQ6,
    int LifePerLevelQ6,
    int ManaPerLevelQ6,
    int LifePerVitalityQ6,
    int ManaPerMagicQ6,
    int ItemLifePerVitalityQ6,
    int ItemManaPerMagicQ6)
{
    /** Native base-game spell-mana reduction, before the spell minimum. */
    public int SpellManaReductionPercent { get; init; }

    /** Native Hellfire spell-mana reduction, before the spell minimum. */
    public int HellfireSpellManaReductionPercent { get; init; }

    /** Native class multiplier applied to the self-Healing spell's Q6 result. */
    public int SelfHealingSpellPercent { get; init; } = 100;

    /** Native class multiplier applied when casting Heal Other. */
    public int HealOtherSpellPercent { get; init; } = 100;

    /** Native class multiplier applied when casting the Mana spell. */
    public int ManaSpellPercent { get; init; } = 100;

    /** Native melee target-armor reduction divisor; zero disables the rule. */
    public int MeleeArmorReductionDivisor { get; init; }

    /** Validated native class formula for equipped physical damage. */
    public AuthoritativePhysicalDamageFormula PhysicalDamageFormula { get; init; }

    /** Native Monk unarmed minimum-damage level divisor. */
    public int MonkUnarmedMinimumDamageLevelDivisor { get; init; } = 2;

    /** Native Monk unarmed maximum-damage level divisor. */
    public int MonkUnarmedMaximumDamageLevelDivisor { get; init; } = 1;

    /** Native Warrior Repair durability denominator offset; zero keeps legacy 9. */
    public int RepairAbilityDurabilityLevelOffset { get; init; }

    /** Native class critical-strike damage multiplier. */
    public int CriticalDamageMultiplier { get; init; } = 2;

    /** Native Critical Strike chance added per character level, in percent. */
    public int CriticalStrikeChancePerLevelPercent { get; init; } = 1;

    /** Native IronSkin armor bonus level divisor. */
    public int IronSkinLevelDivisor { get; init; } = 4;

    /** Native resistance percentage added per character level. */
    public int NaturalResistancePerLevelPercent { get; init; } = 100;

    /** Native HeavyHitThreshold bonus percentage over character level. */
    public int HeavyHitThresholdBonusPercent { get; init; } = 25;

    /** Native staff FastBlock frame skip. */
    public int FastBlockFrameSkip { get; init; } = 2;

    /** Native ranged-arrow speed bonus divisor per character level. */
    public int RangedArrowSpeedLevelDivisor { get; init; }

    /** Native inventory cursor graphic used for this class's PvP ear. */
    public int PvpEarCursorGraphic { get; init; }

    /** Native Monk armor bonus numerator for light armor per character level. */
    public int MonkLightArmorLevelMultiplier { get; init; }

    /** Native Monk armor bonus numerator for ordinary medium armor per level. */
    public int MonkMediumArmorLevelMultiplier { get; init; }

    /** Native Monk armor bonus numerator for unique medium armor per level. */
    public int MonkUniqueMediumArmorLevelMultiplier { get; init; }

    /** Native Monk armor bonus numerator for unique heavy armor per level. */
    public int MonkUniqueHeavyArmorLevelMultiplier { get; init; }

    /** Native Monk armor bonus denominator shared by the armor formulas. */
    public int MonkArmorLevelDivisor { get; init; }

    /** Calculates the class's native natural-resistance contribution. */
    public int CalculateNaturalResistanceBonus(uint characterLevel)
    {
        if (characterLevel == 0)
            throw new ArgumentOutOfRangeException(nameof(characterLevel));
        return checked((int)((ulong)characterLevel * (uint)NaturalResistancePerLevelPercent / 100));
    }

    /** Calculates the class's native heavy-hit threshold. */
    public int CalculateHeavyHitThreshold(uint characterLevel)
    {
        if (characterLevel == 0)
            throw new ArgumentOutOfRangeException(nameof(characterLevel));
        var level = checked((int)characterLevel);
        return checked(level + (int)((long)level * HeavyHitThresholdBonusPercent / 100));
    }

    public PlayerAttributesState CreateStartingAttributes()
    {
        return new PlayerAttributesState(
            new PlayerAttributeState(BaseStrength, BaseStrength),
            new PlayerAttributeState(BaseMagic, BaseMagic),
            new PlayerAttributeState(BaseDexterity, BaseDexterity),
            new PlayerAttributeState(BaseVitality, BaseVitality));
    }

    /** Mirrors native melee/ranged physical to-hit from class and attributes. */
    public int CalculatePhysicalToHit(
        uint characterLevel,
        int dexterity,
        bool ranged)
    {
        if (characterLevel == 0)
            throw new ArgumentOutOfRangeException(nameof(characterLevel));
        if (dexterity < 0)
            throw new ArgumentOutOfRangeException(nameof(dexterity));

        return checked(
            (int)characterLevel
            + (ranged ? dexterity : dexterity / 2)
            + (ranged ? BaseRangedToHit : BaseMeleeToHit));
    }

    /** Mirrors native `Player::calculateBaseLife` in fixed-point Q6 units. */
    public int CalculateBaseLifeQ6(uint characterLevel, int baseVitality)
    {
        ValidateResourceInputs(
            characterLevel,
            baseVitality,
            MaximumVitality,
            nameof(baseVitality));
        return checked(
            LifeAdjustmentQ6
            + LifePerLevelQ6 * (int)characterLevel
            + LifePerVitalityQ6 * baseVitality);
    }

    /** Mirrors native `Player::calculateBaseMana` in fixed-point Q6 units. */
    public int CalculateBaseManaQ6(uint characterLevel, int baseMagic)
    {
        ValidateResourceInputs(
            characterLevel,
            baseMagic,
            MaximumMagic,
            nameof(baseMagic));
        return checked(
            ManaAdjustmentQ6
            + ManaPerLevelQ6 * (int)characterLevel
            + ManaPerMagicQ6 * baseMagic);
    }

    private static void ValidateResourceInputs(
        uint characterLevel,
        int attribute,
        int maximumAttribute,
        string parameterName)
    {
        if (characterLevel is < 1 or > NativeCompatibilityLimits.MaximumNativeCharacterLevel)
            throw new ArgumentOutOfRangeException(nameof(characterLevel));
        if (attribute < 0 || attribute > maximumAttribute)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}

/** Validated stable-ID catalog for base-game and Hellfire player classes. */
public sealed class AuthoritativePlayerClassCatalog
{
    private static readonly IReadOnlyList<string> RequiredColumns = [
        "class_id",
        "symbol",
        "display_name",
        "availability",
        "is_default",
        "class_flags",
        "base_strength",
        "base_magic",
        "base_dexterity",
        "base_vitality",
        "maximum_strength",
        "maximum_magic",
        "maximum_dexterity",
        "maximum_vitality",
        "base_block_chance",
        "base_magic_to_hit",
        "base_melee_to_hit",
        "base_ranged_to_hit",
        "life_adjustment_q6",
        "mana_adjustment_q6",
        "life_per_level_q6",
        "mana_per_level_q6",
        "life_per_vitality_q6",
        "mana_per_magic_q6",
        "item_life_per_vitality_q6",
        "item_mana_per_magic_q6",
        "spell_mana_reduction_percent",
        "hellfire_spell_mana_reduction_percent",
        "self_healing_spell_percent",
        "heal_other_spell_percent",
        "pvp_ear_cursor_graphic",
        "monk_light_armor_level_multiplier",
        "monk_medium_armor_level_multiplier",
        "monk_unique_medium_armor_level_multiplier",
        "monk_unique_heavy_armor_level_multiplier",
        "monk_armor_level_divisor",
    ];

    private static readonly IReadOnlyList<string> ManaColumns = [
        ..RequiredColumns,
        "mana_spell_percent",
    ];

    private static readonly IReadOnlyList<string> CurrentColumns = [
        ..ManaColumns,
        "melee_armor_reduction_divisor",
    ];

    private static readonly IReadOnlyList<string> PhysicalDamageColumns = [
        ..CurrentColumns,
        "physical_damage_formula",
    ];

    private static readonly IReadOnlyList<string> PhysicalDamageTuningColumns = [
        ..PhysicalDamageColumns,
        "monk_unarmed_minimum_damage_level_divisor",
        "monk_unarmed_maximum_damage_level_divisor",
    ];

    private static readonly IReadOnlyList<string> RepairTuningColumns = [
        ..PhysicalDamageTuningColumns,
        "repair_ability_durability_level_offset",
    ];

    private static readonly IReadOnlyList<string> CriticalTuningColumns = [
        ..RepairTuningColumns,
        "critical_damage_multiplier",
    ];

    private static readonly IReadOnlyList<string> CriticalChanceTuningColumns = [
        ..CriticalTuningColumns,
        "critical_strike_chance_per_level_percent",
    ];

    private static readonly IReadOnlyList<string> DefenseTuningColumns = [
        ..CriticalChanceTuningColumns,
        "iron_skin_level_divisor",
    ];

    private static readonly IReadOnlyList<string> ClassDefenseTuningColumns = [
        ..DefenseTuningColumns,
        "natural_resistance_per_level_percent",
        "heavy_hit_threshold_bonus_percent",
        "fast_block_frame_skip",
    ];

    private static readonly IReadOnlyList<string> ClassProjectileTuningColumns = [
        ..ClassDefenseTuningColumns,
        "ranged_arrow_speed_level_divisor",
    ];

    private const DiabloPlayerClassFlags KnownFlags
        = DiabloPlayerClassFlags.CriticalStrike
            | DiabloPlayerClassFlags.DualWield
            | DiabloPlayerClassFlags.IronSkin
            | DiabloPlayerClassFlags.NaturalResistance
            | DiabloPlayerClassFlags.TrapSense
            | DiabloPlayerClassFlags.FlexibleBlock
            | DiabloPlayerClassFlags.FastBlock
            | DiabloPlayerClassFlags.HeavyHitThreshold;

    private readonly Dictionary<uint, AuthoritativePlayerClassDefinition> byId;
    private readonly Dictionary<string, AuthoritativePlayerClassDefinition> bySymbol;

    public AuthoritativePlayerClassCatalog(
        IEnumerable<AuthoritativePlayerClassDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        byId = new Dictionary<uint, AuthoritativePlayerClassDefinition>();
        bySymbol = new Dictionary<string, AuthoritativePlayerClassDefinition>(
            StringComparer.Ordinal);
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            Validate(definition);
            if (!byId.TryAdd(definition.ClassId, definition)) {
                throw new InvalidDataException(
                    $"Player class ID {definition.ClassId} is defined more than once.");
            }
            if (!bySymbol.TryAdd(definition.Symbol, definition)) {
                throw new InvalidDataException(
                    $"Player class symbol '{definition.Symbol}' is defined more than once.");
            }
        }
        if (byId.Count == 0)
            throw new InvalidDataException("The player-class catalog cannot be empty.");
        var orderedIds = byId.Keys.Order().ToArray();
        for (var index = 0; index < orderedIds.Length; index++) {
            if (orderedIds[index] != (uint)index) {
                throw new InvalidDataException(
                    "Player class IDs must be contiguous and start at zero.");
            }
        }
        var defaults = byId.Values.Where(definition => definition.IsDefault).ToArray();
        if (defaults.Length != 1) {
            throw new InvalidDataException(
                "The player-class catalog requires exactly one default class.");
        }
        Default = defaults[0];
    }

    public IReadOnlyList<AuthoritativePlayerClassDefinition> Definitions
        => byId.Values.OrderBy(definition => definition.ClassId).ToArray();

    public AuthoritativePlayerClassDefinition Default { get; }

    public bool TryGet(
        uint classId,
        out AuthoritativePlayerClassDefinition definition)
        => byId.TryGetValue(classId, out definition!);

    public AuthoritativePlayerClassDefinition GetRequired(uint classId)
    {
        return TryGet(classId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Player class ID {classId} is not defined.");
    }

    public AuthoritativePlayerClassDefinition Resolve(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return bySymbol.TryGetValue(symbol, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Player class '{symbol}' is not defined.");
    }

    /**
     * Rejects inferred class tuning at the current production content
     * boundary. Direct callers may continue to load older compatibility shapes.
     */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ClassProjectileTuningColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production player-class table '{table.SourcePath}' must use the complete ordered class tuning schema.");
        foreach (var row in table.Rows)
            foreach (var column in ClassProjectileTuningColumns)
                if (!column.Equals("class_flags", StringComparison.Ordinal))
                    _ = row.Required(column);
    }

    public static AuthoritativePlayerClassCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var hasClassProjectileTuning = table.Columns.SequenceEqual(
            ClassProjectileTuningColumns,
            StringComparer.Ordinal);
        var hasClassDefenseTuning = table.Columns.SequenceEqual(
            ClassDefenseTuningColumns,
            StringComparer.Ordinal)
            || hasClassProjectileTuning;
        var hasDefenseTuning = table.Columns.SequenceEqual(
            DefenseTuningColumns,
            StringComparer.Ordinal)
            || hasClassDefenseTuning;
        var hasCriticalChanceTuning = table.Columns.SequenceEqual(
            CriticalChanceTuningColumns,
            StringComparer.Ordinal)
            || hasDefenseTuning;
        var hasCriticalTuning = table.Columns.SequenceEqual(
            CriticalTuningColumns,
            StringComparer.Ordinal)
            || hasCriticalChanceTuning;
        var hasRepairTuning = hasCriticalChanceTuning
            || hasCriticalTuning
            || table.Columns.SequenceEqual(
            RepairTuningColumns,
            StringComparer.Ordinal);
        var hasPhysicalDamageTuning = table.Columns.SequenceEqual(
                PhysicalDamageTuningColumns,
                StringComparer.Ordinal)
            || hasRepairTuning;
        var hasPhysicalDamageFormula = table.Columns.SequenceEqual(
            PhysicalDamageColumns,
            StringComparer.Ordinal)
            || hasPhysicalDamageTuning;
        var hasMeleeArmorReduction = hasPhysicalDamageFormula
            || table.Columns.SequenceEqual(CurrentColumns, StringComparer.Ordinal);
        var hasManaSpellPercent = hasMeleeArmorReduction
            || table.Columns.SequenceEqual(ManaColumns, StringComparer.Ordinal);
        if (!hasManaSpellPercent
            && !hasRepairTuning
            && !table.Columns.SequenceEqual(RequiredColumns, StringComparer.Ordinal)) {
            throw new InvalidDataException(
                $"Player-class table '{sourcePath}' does not match the required ordered schema.");
        }
        return new AuthoritativePlayerClassCatalog(table.Rows.Select(row =>
            new AuthoritativePlayerClassDefinition(
                AuthoritativeClassIdentity.Resolve(
                    row,
                    stableIds,
                    "class_id",
                    "symbol"),
                row.Required("symbol"),
                row.Required("display_name"),
                ParseAvailability(row),
                row.RequiredBoolean("is_default"),
                ParseFlags(row),
                row.RequiredInt32("base_strength"),
                row.RequiredInt32("base_magic"),
                row.RequiredInt32("base_dexterity"),
                row.RequiredInt32("base_vitality"),
                row.RequiredInt32("maximum_strength"),
                row.RequiredInt32("maximum_magic"),
                row.RequiredInt32("maximum_dexterity"),
                row.RequiredInt32("maximum_vitality"),
                row.RequiredInt32("base_block_chance"),
                row.RequiredInt32("base_magic_to_hit"),
                row.RequiredInt32("base_melee_to_hit"),
                row.RequiredInt32("base_ranged_to_hit"),
                row.RequiredInt32("life_adjustment_q6"),
                row.RequiredInt32("mana_adjustment_q6"),
                row.RequiredInt32("life_per_level_q6"),
                row.RequiredInt32("mana_per_level_q6"),
                row.RequiredInt32("life_per_vitality_q6"),
                row.RequiredInt32("mana_per_magic_q6"),
                row.RequiredInt32("item_life_per_vitality_q6"),
                row.RequiredInt32("item_mana_per_magic_q6")) {
                SpellManaReductionPercent = row.RequiredInt32(
                    "spell_mana_reduction_percent"),
                HellfireSpellManaReductionPercent = row.RequiredInt32(
                    "hellfire_spell_mana_reduction_percent"),
                SelfHealingSpellPercent = row.RequiredInt32(
                    "self_healing_spell_percent"),
                HealOtherSpellPercent = row.RequiredInt32(
                    "heal_other_spell_percent"),
                PvpEarCursorGraphic = row.RequiredInt32(
                    "pvp_ear_cursor_graphic"),
                MonkLightArmorLevelMultiplier = row.RequiredInt32(
                    "monk_light_armor_level_multiplier"),
                MonkMediumArmorLevelMultiplier = row.RequiredInt32(
                    "monk_medium_armor_level_multiplier"),
                MonkUniqueMediumArmorLevelMultiplier = row.RequiredInt32(
                    "monk_unique_medium_armor_level_multiplier"),
                MonkUniqueHeavyArmorLevelMultiplier = row.RequiredInt32(
                    "monk_unique_heavy_armor_level_multiplier"),
                MonkArmorLevelDivisor = row.RequiredInt32(
                    "monk_armor_level_divisor"),
                ManaSpellPercent = hasManaSpellPercent
                    ? row.RequiredInt32("mana_spell_percent")
                    : 100,
                MeleeArmorReductionDivisor = hasMeleeArmorReduction
                    ? row.RequiredInt32("melee_armor_reduction_divisor")
                    : 0,
                PhysicalDamageFormula = hasPhysicalDamageFormula
                    ? ParsePhysicalDamageFormula(row)
                    : InferLegacyPhysicalDamageFormula(row),
                MonkUnarmedMinimumDamageLevelDivisor = hasPhysicalDamageTuning
                    ? row.RequiredInt32("monk_unarmed_minimum_damage_level_divisor")
                    : 2,
                MonkUnarmedMaximumDamageLevelDivisor = hasPhysicalDamageTuning
                    ? row.RequiredInt32("monk_unarmed_maximum_damage_level_divisor")
                    : 1,
                RepairAbilityDurabilityLevelOffset = hasRepairTuning
                    ? row.RequiredInt32("repair_ability_durability_level_offset")
                    : 0,
                CriticalDamageMultiplier = hasCriticalTuning
                    ? row.RequiredInt32("critical_damage_multiplier")
                    : 2,
                CriticalStrikeChancePerLevelPercent = hasCriticalChanceTuning
                    ? row.RequiredInt32("critical_strike_chance_per_level_percent")
                    : 1,
                IronSkinLevelDivisor = hasDefenseTuning
                    ? row.RequiredInt32("iron_skin_level_divisor")
                    : 4,
                NaturalResistancePerLevelPercent = hasClassDefenseTuning
                    ? row.RequiredInt32("natural_resistance_per_level_percent")
                    : 100,
                HeavyHitThresholdBonusPercent = hasClassDefenseTuning
                    ? row.RequiredInt32("heavy_hit_threshold_bonus_percent")
                    : 25,
                FastBlockFrameSkip = hasClassDefenseTuning
                    ? row.RequiredInt32("fast_block_frame_skip")
                    : 2,
                RangedArrowSpeedLevelDivisor = hasClassProjectileTuning
                    ? row.RequiredInt32("ranged_arrow_speed_level_divisor")
                    : InferLegacyRangedArrowSpeedLevelDivisor(row.Required("symbol")),
            }));
    }

    private static int InferLegacyRangedArrowSpeedLevelDivisor(string symbol)
    {
        return symbol switch {
            "rogue" => 4,
            "warrior" or "bard" => 8,
            _ => 0,
        };
    }

    private static void Validate(AuthoritativePlayerClassDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Symbol)
            || definition.Symbol.Any(character =>
                !(character is >= 'a' and <= 'z'
                    || character is >= '0' and <= '9'
                    || character == '_'))
            || string.IsNullOrWhiteSpace(definition.DisplayName)
            || !Enum.IsDefined(definition.Availability)
            || (definition.Flags & ~KnownFlags) != 0) {
            throw new InvalidDataException(
                $"Player class {definition.ClassId} has invalid identity metadata.");
        }
        ValidateAttribute(
            definition.ClassId,
            "strength",
            definition.BaseStrength,
            definition.MaximumStrength);
        ValidateAttribute(
            definition.ClassId,
            "magic",
            definition.BaseMagic,
            definition.MaximumMagic);
        ValidateAttribute(
            definition.ClassId,
            "dexterity",
            definition.BaseDexterity,
            definition.MaximumDexterity);
        ValidateAttribute(
            definition.ClassId,
            "vitality",
            definition.BaseVitality,
            definition.MaximumVitality);
        if (definition.BaseBlockChance is < 0 or > 100
            || definition.BaseMagicToHit is < 0 or > 100
            || definition.BaseMeleeToHit is < 0 or > 100
            || definition.BaseRangedToHit is < 0 or > 100
            || definition.SpellManaReductionPercent is < 0 or > 100
            || definition.HellfireSpellManaReductionPercent is < 0 or > 100
            || definition.SelfHealingSpellPercent is < 1 or > 400
            || definition.HealOtherSpellPercent is < 1 or > 400
            || definition.ManaSpellPercent is < 1 or > 400
            || !Enum.IsDefined(definition.PhysicalDamageFormula)
            || definition.MonkUnarmedMinimumDamageLevelDivisor <= 0
            || definition.MonkUnarmedMaximumDamageLevelDivisor <= 0
            || definition.RepairAbilityDurabilityLevelOffset < 0
            || definition.CriticalDamageMultiplier is < 1 or > 100
            || definition.CriticalStrikeChancePerLevelPercent is < 0 or > 100
            || definition.IronSkinLevelDivisor <= 0
            || definition.NaturalResistancePerLevelPercent is < 0 or > 100
            || definition.HeavyHitThresholdBonusPercent is < 0 or > 100
            || definition.FastBlockFrameSkip is < 0 or > 10
            || definition.RangedArrowSpeedLevelDivisor is < 0 or > 100
            || definition.MeleeArmorReductionDivisor < 0
            || definition.PvpEarCursorGraphic is < 0 or > byte.MaxValue) {
            throw new InvalidDataException(
                $"Player class {definition.ClassId} has an invalid percentage.");
        }
        var monkArmorValues = new[] {
            definition.MonkLightArmorLevelMultiplier,
            definition.MonkMediumArmorLevelMultiplier,
            definition.MonkUniqueMediumArmorLevelMultiplier,
            definition.MonkUniqueHeavyArmorLevelMultiplier,
        };
        if (monkArmorValues.Any(value => value < 0)
            || definition.MonkArmorLevelDivisor <= 0
            || monkArmorValues.Any(value => value > 10 * definition.MonkArmorLevelDivisor)) {
            throw new InvalidDataException(
                $"Player class {definition.ClassId} has invalid Monk armor tuning.");
        }
        var q6Values = new[] {
            definition.LifeAdjustmentQ6,
            definition.ManaAdjustmentQ6,
            definition.LifePerLevelQ6,
            definition.ManaPerLevelQ6,
            definition.LifePerVitalityQ6,
            definition.ManaPerMagicQ6,
            definition.ItemLifePerVitalityQ6,
            definition.ItemManaPerMagicQ6,
        };
        if (q6Values.Any(value => value is < short.MinValue or > short.MaxValue)) {
            throw new InvalidDataException(
                $"Player class {definition.ClassId} has a Q6 value outside the native range.");
        }
    }

    private static void ValidateAttribute(
        uint classId,
        string name,
        int @base,
        int maximum)
    {
        if (@base is < 0 or > byte.MaxValue
            || maximum is < 0 or > byte.MaxValue
            || @base > maximum) {
            throw new InvalidDataException(
                $"Player class {classId} has invalid {name} bounds.");
        }
    }

    private static DiabloPlayerClassAvailability ParseAvailability(TsvRow row)
    {
        var value = row.Required("availability");
        if (Enum.TryParse<DiabloPlayerClassAvailability>(
            value,
            ignoreCase: false,
            out var availability)
            && Enum.IsDefined(availability)
            && Enum.GetName(availability) == value) {
            return availability;
        }
        throw new InvalidDataException(
            $"Player-class row {row.LineNumber} contains unknown availability '{value}'.");
    }

    private static DiabloPlayerClassFlags ParseFlags(TsvRow row)
    {
        if (!row.TryGet("class_flags", out var value)
            || string.IsNullOrWhiteSpace(value)) {
            value = string.Empty;
        }
        var result = DiabloPlayerClassFlags.None;
        var names = value.Length == 0
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.TrimEntries);
        if (names.Any(string.IsNullOrWhiteSpace)
            || names.Distinct(StringComparer.Ordinal).Count() != names.Length) {
            throw new InvalidDataException(
                $"Player-class row {row.LineNumber} contains duplicate or empty flags.");
        }
        foreach (var name in names) {
            if (!Enum.TryParse<DiabloPlayerClassFlags>(name, ignoreCase: false, out var flag)
                || flag == DiabloPlayerClassFlags.None
                || !Enum.IsDefined(flag)
                || Enum.GetName(flag) != name) {
                throw new InvalidDataException(
                    $"Player-class row {row.LineNumber} contains unknown flag '{name}'.");
            }
            result |= flag;
        }
        // Older class overlays predate the declarative trait flags. Preserve
        // their native identities at the data boundary while the combat engine
        // consumes only the validated flag values above.
        var symbol = row.Required("symbol");
        if (string.Equals(symbol, "monk", StringComparison.Ordinal))
            result |= DiabloPlayerClassFlags.FastBlock;
        if (string.Equals(symbol, "barbarian", StringComparison.Ordinal))
            result |= DiabloPlayerClassFlags.HeavyHitThreshold;
        return result;
    }

    private static AuthoritativePhysicalDamageFormula ParsePhysicalDamageFormula(TsvRow row)
    {
        var value = row.Required("physical_damage_formula");
        if (Enum.TryParse<AuthoritativePhysicalDamageFormula>(
                value,
                ignoreCase: false,
                out var formula)
            && Enum.IsDefined(formula)
            && Enum.GetName(formula) == value) {
            return formula;
        }
        throw new InvalidDataException(
            $"Player-class row {row.LineNumber} contains unknown physical damage formula '{value}'.");
    }

    private static AuthoritativePhysicalDamageFormula InferLegacyPhysicalDamageFormula(TsvRow row)
    {
        return row.Required("symbol") switch {
            "rogue" => AuthoritativePhysicalDamageFormula.Rogue,
            "monk" => AuthoritativePhysicalDamageFormula.Monk,
            "bard" => AuthoritativePhysicalDamageFormula.Bard,
            "barbarian" => AuthoritativePhysicalDamageFormula.Barbarian,
            _ => AuthoritativePhysicalDamageFormula.Standard,
        };
    }
}
