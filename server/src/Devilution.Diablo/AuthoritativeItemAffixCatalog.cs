using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** One external prefix or suffix candidate used by deterministic item generation. */
public sealed record AuthoritativeItemAffix(
    string Name,
    string Power,
    int Value1,
    int Value2,
    int MinimumLevel,
    IReadOnlySet<string> ItemTags,
    int Chance,
    bool Useful)
{
    public string Alignment { get; init; } = "Any";

    public int MinimumValue { get; init; }

    public int MaximumValue { get; init; }

    public int ValueMultiplier { get; init; }

    public bool HasLegacyValueRange { get; init; }

    public int Roll(uint seed)
    {
        var low = Math.Min(Value1, Value2);
        var high = Math.Max(Value1, Value2);
        if (low == high)
            return low;
        var value = unchecked(seed * 1664525U + 1013904223U);
        return low + (int)(value % (uint)(high - low + 1));
    }

    public bool AppliesTo(int itemLevel, IReadOnlySet<string> itemTags)
    {
        return itemLevel >= MinimumLevel && Chance > 0
            && (ItemTags.Count == 0 || ItemTags.Overlaps(itemTags));
    }

    public int RollLegacy(DeterministicLegacyRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var low = Math.Min(Value1, Value2);
        var high = Math.Max(Value1, Value2);
        var raw = low == high ? low : low + random.Next(high - low + 1);
        if (!HasLegacyValueRange)
            return raw;
        if (MinimumValue == 0 && MaximumValue == 0 && ValueMultiplier == 0)
            return raw;
        if (Value1 == Value2 || MinimumValue == MaximumValue)
            return MinimumValue;
        return MinimumValue + (MaximumValue - MinimumValue) * (100 * (raw - Value1) / (Value2 - Value1)) / 100;
    }

    /** Preserves the pre-engine overload for existing Diablo callers. */
    public int RollLegacy(AuthoritativeLegacyRandom random)
        => RollLegacy((DeterministicLegacyRandom)random);

    public int RollLegacy(uint seed)
    {
        var raw = Roll(seed);
        if (!HasLegacyValueRange)
            return raw;
        if (MinimumValue == 0 && MaximumValue == 0 && ValueMultiplier == 0)
            return raw;
        if (Value1 == Value2 || MinimumValue == MaximumValue)
            return MinimumValue;
        return MinimumValue + (MaximumValue - MinimumValue) * (100 * (raw - Value1) / (Value2 - Value1)) / 100;
    }
}

/** External affix tables and deterministic modifier application for generated items. */
public sealed class AuthoritativeItemAffixCatalog
{
    private static readonly IReadOnlySet<string> SupportedPowerNames =
        new HashSet<string>(StringComparer.Ordinal) {
            "DAM", "DAMP", "DAMP_CURSE", "DAMMOD", "TOHIT", "TOHIT_CURSE", "TOHIT_DAMP",
            "TOHIT_DAMP_CURSE", "ACP", "ACP_CURSE", "AC_CURSE", "SETAC", "STR", "MAG",
            "DEX", "VIT", "STR_CURSE", "MAG_CURSE", "DEX_CURSE", "VIT_CURSE", "ATTRIBS",
            "ATTRIBS_CURSE", "FIRERES", "LIGHTRES", "MAGICRES", "FIRERES_CURSE",
            "LIGHTRES_CURSE", "MAGICRES_CURSE", "ALLRES", "LIFE", "LIFE_CURSE", "MANA",
            "MANA_CURSE", "GETHIT", "GETHIT_CURSE", "LIGHT", "LIGHT_CURSE", "SPLLVLADD",
            "CHARGES", "SPELL", "DUR", "DUR_CURSE", "CRYSTALLINE", "FIREDAM", "LIGHTDAM", "DEVASTATION",
            "PERIL", "JESTERS", "DOPPELGANGER", "DECAY", "INDESTRUCTIBLE", "FIRE_ARROWS",
            "LIGHT_ARROWS", "MULT_ARROWS", "THORNS", "NOMANA", "ABSHALFTRAP", "KNOCKBACK",
            "3XDAMVDEM", "ALLRESZERO", "STEALMANA", "STEALLIFE", "TARGAC", "FASTATTACK",
            "FASTRECOVER", "FASTBLOCK", "RNDARROWVEL", "SETDAM", "SETDUR", "NOMINSTR",
            "ONEHAND", "DRAINLIFE", "RNDSTEALLIFE", "ADDACLIFE", "ADDMANAAC",
        };
    private readonly IReadOnlyList<AuthoritativeItemAffix> prefixes;
    private readonly IReadOnlyList<AuthoritativeItemAffix> suffixes;
    private readonly AuthoritativeItemGenerationRules generationRules;
    private readonly AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog;
    private readonly AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog;

    public bool UsesLegacyRandom { get; }

    public AuthoritativeItemAffixCatalog(
        IEnumerable<AuthoritativeItemAffix> prefixes,
        IEnumerable<AuthoritativeItemAffix> suffixes,
        AuthoritativeItemGenerationRules? generationRules = null,
        bool usesLegacyRandom = false,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null)
    {
        this.prefixes = Validate(prefixes, "prefix");
        this.suffixes = Validate(suffixes, "suffix");
        this.generationRules = generationRules ?? AuthoritativeItemGenerationRules.Default;
        this.nativeItemEffectCatalog = nativeItemEffectCatalog;
        this.nativeItemHellfireEffectCatalog = nativeItemHellfireEffectCatalog;
        UsesLegacyRandom = usesLegacyRandom;
    }

    public IReadOnlyList<AuthoritativeItemAffix> Prefixes => prefixes;
    public IReadOnlyList<AuthoritativeItemAffix> Suffixes => suffixes;
    public AuthoritativeItemGenerationRules GenerationRules => generationRules;

    /** Rejects affix powers that have no authoritative C# application handler. */
    public void ValidatePowers()
    {
        foreach (var affix in prefixes.Concat(suffixes)) {
            if (!SupportedPowerNames.Contains(affix.Power))
                throw new InvalidDataException(
                    $"Affix '{affix.Name}' uses unsupported power '{affix.Power}'.");
        }
    }

    public AuthoritativeItemState Apply(
        AuthoritativeItemState state,
        uint itemSeed,
        int itemLevel,
        IReadOnlySet<string> itemTags)
    {
        if (UsesLegacyRandom)
            return Apply(state, new DeterministicLegacyRandom(itemSeed), itemLevel, itemTags);

        var result = state;
        var allocatePrefix = Percent(itemSeed ^ 0x9E3779B9U, generationRules.PrefixPercent);
        var allocateSuffix = Percent(itemSeed ^ 0x85EBCA6BU, generationRules.SuffixPercent);
        if (!allocatePrefix && !allocateSuffix) {
            if (Percent(itemSeed ^ 0xD1B54A32U, 50))
                allocatePrefix = true;
            else
                allocateSuffix = true;
        }
        var onlyGood = Percent(itemSeed ^ 0x94D049BBU, generationRules.OnlyGoodChance);
        var prefix = allocatePrefix
            ? Select(prefixes, itemSeed ^ 0x9E3779B9U, itemLevel, itemTags, "Any", onlyGood)
            : null;
        var suffix = allocateSuffix
            ? Select(suffixes, itemSeed ^ 0x85EBCA6BU, itemLevel, itemTags, prefix?.Alignment ?? "Any", onlyGood)
            : null;
        if (prefix is not null)
            result = ApplyPower(result, prefix, prefix.RollLegacy(itemSeed ^ 0xA341316CU), true);
        if (suffix is not null)
            result = ApplyPower(result, suffix, suffix.RollLegacy(itemSeed ^ 0xC8013EA4U), false);
        return result;
    }

    public AuthoritativeItemState Apply(
        AuthoritativeItemState state,
        DeterministicLegacyRandom random,
        int itemLevel,
        IReadOnlySet<string> itemTags)
    {
        if (!UsesLegacyRandom)
            throw new InvalidOperationException("The legacy random affix path is not enabled for this catalog.");
        return ApplyLegacy(state, random, itemLevel, itemTags);
    }

    /** Preserves the pre-engine overload for existing Diablo callers. */
    public AuthoritativeItemState Apply(
        AuthoritativeItemState state,
        AuthoritativeLegacyRandom random,
        int itemLevel,
        IReadOnlySet<string> itemTags)
        => Apply(state, (DeterministicLegacyRandom)random, itemLevel, itemTags);

    private AuthoritativeItemState ApplyLegacy(
        AuthoritativeItemState state,
        DeterministicLegacyRandom random,
        int itemLevel,
        IReadOnlySet<string> itemTags)
    {
        ArgumentNullException.ThrowIfNull(random);
        var allocatePrefix = random.Next(100) < generationRules.PrefixPercent;
        var allocateSuffix = random.Next(100) < generationRules.SuffixPercent;
        if (!allocatePrefix && !allocateSuffix) {
            if (random.Next(2) == 0)
                allocatePrefix = true;
            else
                allocateSuffix = true;
        }
        var onlyGood = random.Next(100) < generationRules.OnlyGoodChance;
        var prefix = allocatePrefix
            ? SelectLegacy(prefixes, random, itemLevel, itemTags, "Any", onlyGood)
            : null;
        var suffix = allocateSuffix
            ? SelectLegacy(suffixes, random, itemLevel, itemTags, prefix?.Alignment ?? "Any", onlyGood)
            : null;
        var result = state;
        if (prefix is not null)
            result = ApplyLegacyPower(result, prefix, random, true);
        if (suffix is not null)
            result = ApplyLegacyPower(result, suffix, random, false);
        return RecalculateItemValue(result with { Magical = prefix is not null || suffix is not null ? 1 : result.Magical });
    }

    private AuthoritativeItemState ApplyLegacyPower(
        AuthoritativeItemState state,
        AuthoritativeItemAffix affix,
        DeterministicLegacyRandom random,
        bool isPrefix)
    {
        if (affix.Power == "TOHIT_DAMP") {
            _ = affix.RollLegacy(random);
            var damage = affix.RollLegacy(random);
            var toHit = CalculateToHitBonus(affix.Value1, random);
            return ApplyAffixValueMetadata(state with {
                PlusDamage = state.PlusDamage + damage,
                PlusToHit = state.PlusToHit + toHit,
            }, affix, isPrefix, damage);
        }
        if (affix.Power == "TOHIT_DAMP_CURSE") {
            var damage = affix.RollLegacy(random);
            var toHit = CalculateToHitBonus(affix.Value1, random);
            return ApplyAffixValueMetadata(state with {
                PlusDamage = state.PlusDamage - damage,
                PlusToHit = state.PlusToHit + toHit,
            }, affix, isPrefix, damage);
        }
        return ApplyPower(state, affix, affix.RollLegacy(random), isPrefix);
    }

    private static int CalculateToHitBonus(int level, DeterministicLegacyRandom random)
    {
        var (minimum, maximum, sign) = level switch {
            -50 => (6, 10, -1),
            -25 => (1, 5, -1),
            20 => (1, 5, 1),
            36 => (6, 10, 1),
            51 => (11, 15, 1),
            66 => (16, 20, 1),
            81 => (21, 30, 1),
            96 => (31, 40, 1),
            111 => (41, 50, 1),
            126 => (51, 75, 1),
            151 => (76, 100, 1),
            _ => throw new InvalidDataException($"Unsupported TOHIT_DAMP level {level}.")
        };
        return sign * (minimum + random.Next(maximum - minimum + 1));
    }

    public static AuthoritativeItemAffixCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemGenerationRules? generationRules = null,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var usesLegacyRandom = table.Rows.Any(row => row.OptionalBoolean("legacy_random"));
        return new AuthoritativeItemAffixCatalog(
            table.Rows.Where(row => !row.OptionalBoolean("is_suffix")).Select(row => Parse(row)),
            table.Rows.Where(row => row.OptionalBoolean("is_suffix")).Select(row => Parse(row)),
            generationRules,
            usesLegacyRandom: usesLegacyRandom,
            nativeItemEffectCatalog: nativeItemEffectCatalog,
            nativeItemHellfireEffectCatalog: nativeItemHellfireEffectCatalog);
    }

    /** Loads the native legacy prefix and suffix table shapes without rewriting their columns. */
    public static AuthoritativeItemAffixCatalog LoadLegacyTsv(
        string prefixSourcePath,
        string prefixContents,
        string suffixSourcePath,
        string suffixContents,
        AuthoritativeItemGenerationRules? generationRules = null,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null)
    {
        var prefixes = TsvTable.Parse(prefixSourcePath, prefixContents).Rows.Select(ParseLegacy);
        var suffixes = TsvTable.Parse(suffixSourcePath, suffixContents).Rows.Select(ParseLegacy);
        return new AuthoritativeItemAffixCatalog(
            prefixes,
            suffixes,
            generationRules,
            usesLegacyRandom: true,
            nativeItemEffectCatalog: nativeItemEffectCatalog,
            nativeItemHellfireEffectCatalog: nativeItemHellfireEffectCatalog);
    }

    private static AuthoritativeItemAffix Parse(TsvRow row)
    {
        var tags = row.TryGet("item_tags", out var itemTags) && !string.IsNullOrWhiteSpace(itemTags)
            ? itemTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legacyRandom = row.OptionalBoolean("legacy_random");
        return new AuthoritativeItemAffix(
            row.Required("name"),
            row.Required("power"),
            row.OptionalInt32("value1"),
            row.OptionalInt32("value2"),
            row.OptionalInt32("min_level"),
            tags,
            row.OptionalInt32("chance", 1),
            row.OptionalBoolean("useful", true)) {
                Alignment = row.TryGet("alignment", out var alignment)
                    && !string.IsNullOrWhiteSpace(alignment)
                    ? alignment
                    : "Any",
                MinimumValue = row.OptionalInt32("min_value"),
                MaximumValue = row.OptionalInt32("max_value"),
                ValueMultiplier = row.OptionalInt32("value_multiplier"),
                HasLegacyValueRange = legacyRandom
                    || row.OptionalBoolean("legacy_value_range"),
            };
    }

    private static AuthoritativeItemAffix ParseLegacy(TsvRow row)
    {
        var tags = row.TryGet("itemTypes", out var itemTypes) && !string.IsNullOrWhiteSpace(itemTypes)
            ? itemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new AuthoritativeItemAffix(
            row.Required("name"),
            row.Required("power"),
            row.OptionalInt32("power.value1"),
            row.OptionalInt32("power.value2"),
            row.OptionalInt32("minLevel"),
            tags,
            row.OptionalInt32("chance", 1),
            row.OptionalBoolean("useful", true)) {
                Alignment = row.TryGet("alignment", out var alignment) && !string.IsNullOrWhiteSpace(alignment) ? alignment : "Any",
                MinimumValue = row.OptionalInt32("minVal"),
                MaximumValue = row.OptionalInt32("maxVal"),
                ValueMultiplier = row.OptionalInt32("multVal"),
                HasLegacyValueRange = true,
            };
    }

    private static bool Percent(uint seed, int percentage)
    {
        return unchecked(seed * 1664525U + 1013904223U) % 100U < percentage;
    }

    private static IReadOnlyList<AuthoritativeItemAffix> Validate(IEnumerable<AuthoritativeItemAffix> values, string kind)
    {
        var result = values?.ToArray() ?? throw new ArgumentNullException(nameof(values));
        if (result.Any(value => string.IsNullOrWhiteSpace(value.Name) || string.IsNullOrWhiteSpace(value.Power)
            || value.MinimumLevel < 0 || value.Chance < 0))
            throw new InvalidDataException($"The {kind} affix catalog contains invalid rows.");
        return result;
    }

    private static AuthoritativeItemAffix? Select(
        IReadOnlyList<AuthoritativeItemAffix> values,
        uint seed,
        int itemLevel,
        IReadOnlySet<string> itemTags,
        string blockedAlignment,
        bool onlyGood)
    {
        var candidates = values.Where(value => value.AppliesTo(itemLevel, itemTags)
            && (!onlyGood || value.Useful)
            && !Opposes(value.Alignment, blockedAlignment)).ToArray();
        var totalChance = candidates.Sum(value => value.Chance);
        if (totalChance <= 0)
            return null;
        var roll = (int)(unchecked(seed * 1103515245U + 12345U) % (uint)totalChance);
        foreach (var candidate in candidates) {
            if (roll < candidate.Chance)
                return candidate;
            roll -= candidate.Chance;
        }
        return candidates[^1];
    }

    private static AuthoritativeItemAffix? SelectLegacy(
        IReadOnlyList<AuthoritativeItemAffix> values,
        DeterministicLegacyRandom random,
        int itemLevel,
        IReadOnlySet<string> itemTags,
        string blockedAlignment,
        bool onlyGood)
    {
        var candidates = values.Where(value => value.AppliesTo(itemLevel, itemTags)
            && (!onlyGood || value.Useful)
            && !Opposes(value.Alignment, blockedAlignment)).ToArray();
        var totalChance = candidates.Sum(value => value.Chance);
        if (totalChance <= 0)
            return null;
        var roll = random.Next(totalChance);
        foreach (var candidate in candidates) {
            if (roll < candidate.Chance)
                return candidate;
            roll -= candidate.Chance;
        }
        return candidates[^1];
    }

    private static bool Opposes(string left, string right)
    {
        return (left.Equals("Good", StringComparison.OrdinalIgnoreCase) && right.Equals("Evil", StringComparison.OrdinalIgnoreCase))
            || (left.Equals("Evil", StringComparison.OrdinalIgnoreCase) && right.Equals("Good", StringComparison.OrdinalIgnoreCase));
    }

    private static AuthoritativeItemState RecalculateItemValue(AuthoritativeItemState state)
    {
        var multiplier = state.ValueMultiply1 + state.ValueMultiply2;
        var value = multiplier > 0
            ? checked(multiplier * state.Value)
            : multiplier < 0
                ? state.Value / multiplier
                : 0;
        value = checked(state.ValueAdd1 + state.ValueAdd2 + value);
        return state with { IdentifiedValue = Math.Max(value, 1) };
    }

    private AuthoritativeItemState ApplyPower(
        AuthoritativeItemState state,
        AuthoritativeItemAffix affix,
        int value,
        bool isPrefix)
    {
        var result = affix.Power switch {
            "DAM" or "DAMP" => state with { PlusDamage = state.PlusDamage + value },
            "DAMP_CURSE" => state with { PlusDamage = state.PlusDamage - value },
            "DAMMOD" => state with { PlusDamageModifier = state.PlusDamageModifier + value },
            "TOHIT" => state with { PlusToHit = state.PlusToHit + value },
            "TOHIT_CURSE" => state with { PlusToHit = state.PlusToHit - value },
            "TOHIT_DAMP" => state with { PlusToHit = state.PlusToHit + value, PlusDamage = state.PlusDamage + value },
            "TOHIT_DAMP_CURSE" => state with { PlusToHit = state.PlusToHit - value, PlusDamage = state.PlusDamage - value },
            "ACP" => state with { PlusArmorClass = state.PlusArmorClass + value },
            "ACP_CURSE" or "AC_CURSE" => state with { PlusArmorClass = state.PlusArmorClass - value },
            "SETAC" => state with { ArmorClass = value },
            "STR" => state with { PlusStrength = state.PlusStrength + value },
            "MAG" => state with { PlusMagic = state.PlusMagic + value },
            "DEX" => state with { PlusDexterity = state.PlusDexterity + value },
            "VIT" => state with { PlusVitality = state.PlusVitality + value },
            "STR_CURSE" => state with { PlusStrength = state.PlusStrength - value },
            "MAG_CURSE" => state with { PlusMagic = state.PlusMagic - value },
            "DEX_CURSE" => state with { PlusDexterity = state.PlusDexterity - value },
            "VIT_CURSE" => state with { PlusVitality = state.PlusVitality - value },
            "ATTRIBS" => state with {
                PlusStrength = state.PlusStrength + value,
                PlusMagic = state.PlusMagic + value,
                PlusDexterity = state.PlusDexterity + value,
                PlusVitality = state.PlusVitality + value,
            },
            "ATTRIBS_CURSE" => state with {
                PlusStrength = state.PlusStrength - value,
                PlusMagic = state.PlusMagic - value,
                PlusDexterity = state.PlusDexterity - value,
                PlusVitality = state.PlusVitality - value,
            },
            "FIRERES" => state with { PlusFireResistance = state.PlusFireResistance + value },
            "LIGHTRES" => state with { PlusLightningResistance = state.PlusLightningResistance + value },
            "MAGICRES" => state with { PlusMagicResistance = state.PlusMagicResistance + value },
            "FIRERES_CURSE" => state with { PlusFireResistance = state.PlusFireResistance - value },
            "LIGHTRES_CURSE" => state with { PlusLightningResistance = state.PlusLightningResistance - value },
            "MAGICRES_CURSE" => state with { PlusMagicResistance = state.PlusMagicResistance - value },
            "ALLRES" => state with {
                PlusFireResistance = Math.Max(state.PlusFireResistance + value, 0),
                PlusLightningResistance = Math.Max(state.PlusLightningResistance + value, 0),
                PlusMagicResistance = Math.Max(state.PlusMagicResistance + value, 0),
            },
            "LIFE" => state with { PlusHitPoints = state.PlusHitPoints + (value << 6) },
            "LIFE_CURSE" => state with { PlusHitPoints = state.PlusHitPoints - (value << 6) },
            "MANA" => state with { PlusMana = state.PlusMana + (value << 6) },
            "MANA_CURSE" => state with { PlusMana = state.PlusMana - (value << 6) },
            "GETHIT" => state with { PlusGetHit = state.PlusGetHit - value },
            "GETHIT_CURSE" => state with { PlusGetHit = state.PlusGetHit + value },
            "LIGHT" => state with { PlusLight = state.PlusLight + affix.Value1 },
            "LIGHT_CURSE" => state with { PlusLight = state.PlusLight - affix.Value1 },
            "SPLLVLADD" => state with { SpellLevelAdd = state.SpellLevelAdd + value },
            "CHARGES" => state with { Charges = state.Charges * Math.Max(1, affix.Value1), MaxCharges = state.MaxCharges * Math.Max(1, affix.Value1) },
            "SPELL" => state with {
                SpellId = affix.Value1,
                Charges = affix.Value2,
                MaxCharges = affix.Value2,
                SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            },
            "DUR" => state with { Durability = state.MaxDurability + state.MaxDurability * value / 100, MaxDurability = state.MaxDurability + state.MaxDurability * value / 100 },
            "CRYSTALLINE" => state with {
                PlusDamage = state.PlusDamage + 140 + value * 2,
                Durability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100),
                MaxDurability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100),
            },
            "DUR_CURSE" => state with { Durability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100), MaxDurability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100) },
            "FIREDAM" => state with {
                FireMinDamage = affix.Value1,
                FireMaxDamage = affix.Value2,
                LightningMinDamage = 0,
                LightningMaxDamage = 0,
                Flags = (state.Flags | ResolveItemEffectFlag(
                        "FireDamage",
                        AuthoritativeEquipmentElementalDamageRules.FireDamageFlag))
                    & ~ResolveItemEffectFlag(
                        "LightningDamage",
                        AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag),
            },
            "LIGHTDAM" => state with {
                LightningMinDamage = affix.Value1,
                LightningMaxDamage = affix.Value2,
                FireMinDamage = 0,
                FireMaxDamage = 0,
                Flags = (state.Flags | ResolveItemEffectFlag(
                        "LightningDamage",
                        AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag))
                    & ~ResolveItemEffectFlag(
                        "FireDamage",
                        AuthoritativeEquipmentElementalDamageRules.FireDamageFlag),
            },
            "DEVASTATION" => state with {
                HellfireDamageArmorFlags = state.HellfireDamageArmorFlags
                    | ResolveHellfireEffectFlag(
                        "DEVASTATION",
                        AuthoritativeEquipmentIncomingDamageRules.DevastationFlag),
            },
            "PERIL" => state with {
                HellfireDamageArmorFlags = state.HellfireDamageArmorFlags
                    | ResolveHellfireEffectFlag(
                        "PERIL",
                        AuthoritativeEquipmentIncomingDamageRules.PerilFlag),
            },
            "JESTERS" => state with {
                HellfireDamageArmorFlags = state.HellfireDamageArmorFlags
                    | ResolveHellfireEffectFlag(
                        "JESTERS",
                        AuthoritativeEquipmentIncomingDamageRules.JestersFlag),
            },
            "DOPPELGANGER" => state with {
                HellfireDamageArmorFlags = state.HellfireDamageArmorFlags
                    | ResolveHellfireEffectFlag(
                        "DOPPELGANGER",
                        AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag),
            },
            "DECAY" => state with {
                HellfireDamageArmorFlags = state.HellfireDamageArmorFlags
                    | ResolveHellfireEffectFlag(
                        "DECAY",
                        AuthoritativeEquipmentIncomingDamageRules.DecayFlag),
                PlusDamage = state.PlusDamage + value,
            },
            "INDESTRUCTIBLE" => state with { Durability = 255, MaxDurability = 255 },
            "FIRE_ARROWS" => state with {
                Flags = (state.Flags
                    | ResolveItemEffectFlag(
                        "FireArrows",
                        AuthoritativeEquipmentElementalDamageRules.FireArrowsFlag)
                    | ResolveItemEffectFlag(
                        "FireDamage",
                        AuthoritativeEquipmentElementalDamageRules.FireDamageFlag))
                    & ~ResolveItemEffectFlag(
                        "LightningArrows",
                        AuthoritativeEquipmentElementalDamageRules.LightningArrowsFlag),
                FireMinDamage = affix.Value1,
                FireMaxDamage = affix.Value2,
                LightningMinDamage = 0,
                LightningMaxDamage = 0,
            },
            "LIGHT_ARROWS" => state with {
                Flags = (state.Flags
                    | ResolveItemEffectFlag(
                        "LightningArrows",
                        AuthoritativeEquipmentElementalDamageRules.LightningArrowsFlag)
                    | ResolveItemEffectFlag(
                        "LightningDamage",
                        AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag))
                    & ~ResolveItemEffectFlag(
                        "FireArrows",
                        AuthoritativeEquipmentElementalDamageRules.FireArrowsFlag),
                LightningMinDamage = affix.Value1,
                LightningMaxDamage = affix.Value2,
                FireMinDamage = 0,
                FireMaxDamage = 0,
            },
            "MULT_ARROWS" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "MultipleArrows",
                    AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag),
            },
            "THORNS" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "Thorns",
                    AuthoritativeEquipmentIncomingDamageRules.ThornsFlag),
            },
            "NOMANA" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "NoMana",
                    AuthoritativeEquipmentAttackEffectRules.NoManaFlag),
            },
            "ABSHALFTRAP" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "HalfTrapDamage",
                    AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag),
            },
            "KNOCKBACK" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "Knockback",
                    AuthoritativeEquipmentAttackEffectRules.KnockbackFlag),
            },
            "3XDAMVDEM" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "TripleDemonDamage",
                    AuthoritativeEquipmentAttackEffectRules.TripleDemonDamageFlag),
            },
            "ALLRESZERO" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "ZeroResistance",
                    AuthoritativeEquipmentResistanceRules.ZeroResistanceFlag),
            },
            "STEALMANA" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    affix.Value1 == 3 ? "StealMana3Percent" : "StealMana5Percent",
                    affix.Value1 == 3 ? 0x00002000U : 0x00004000U),
            },
            "STEALLIFE" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    affix.Value1 == 3 ? "StealLife3Percent" : "StealLife5Percent",
                    affix.Value1 == 3 ? 0x00008000U : 0x00010000U),
            },
            "TARGAC" => state with { PlusEnemyArmorClass = state.PlusEnemyArmorClass + value },
            "FASTATTACK" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    affix.Value1 switch {
                        1 => "FastAttack1",
                        2 => "FastAttack2",
                        3 => "FastAttack3",
                        _ => "FastAttack4",
                    },
                    affix.Value1 switch {
                        1 => 0x00020000U,
                        2 => 0x00040000U,
                        3 => 0x00080000U,
                        _ => 0x00100000U,
                    }),
            },
            "FASTRECOVER" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    affix.Value1 switch {
                        1 => "FastRecover1",
                        2 => "FastRecover2",
                        _ => "FastRecover3",
                    },
                    affix.Value1 switch {
                        1 => 0x00200000U,
                        2 => 0x00400000U,
                        _ => 0x00800000U,
                    }),
            },
            "FASTBLOCK" => state with {
                Flags = state.Flags | ResolveItemEffectFlag("FastBlock", 0x01000000U),
            },
            "RNDARROWVEL" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "RandomArrowVelocity",
                    AuthoritativeEquipmentAttackEffectRules.RandomArrowVelocityFlag),
            },
            "SETDAM" => state with { MinDamage = affix.Value1, MaxDamage = affix.Value2 },
            "SETDUR" => state with { Durability = affix.Value1, MaxDurability = affix.Value1 },
            "NOMINSTR" => state with { MinimumStrength = 0 },
            "ONEHAND" => state with { EquipLocation = 1 },
            "DRAINLIFE" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "DrainLife",
                    AuthoritativeEquipmentIncomingDamageRules.DrainLifeFlag),
            },
            "RNDSTEALLIFE" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "RandomStealLife",
                    AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag),
            },
            "ADDACLIFE" => state with {
                Flags = state.Flags
                    | ResolveItemEffectFlag(
                        "LightningArrows",
                        AuthoritativeEquipmentElementalDamageRules.LightningArrowsFlag)
                    | ResolveItemEffectFlag(
                        "FireArrows",
                        AuthoritativeEquipmentElementalDamageRules.FireArrowsFlag),
                FireMinDamage = affix.Value1,
                FireMaxDamage = affix.Value2,
                LightningMinDamage = 1,
                LightningMaxDamage = 0,
            },
            "ADDMANAAC" => state with {
                Flags = state.Flags
                    | ResolveItemEffectFlag(
                        "LightningDamage",
                        AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag)
                    | ResolveItemEffectFlag(
                        "FireDamage",
                        AuthoritativeEquipmentElementalDamageRules.FireDamageFlag),
                FireMinDamage = affix.Value1,
                FireMaxDamage = affix.Value2,
                LightningMinDamage = 2,
                LightningMaxDamage = 0,
            },
            _ => state,
        };
        return ApplyAffixValueMetadata(result, affix, isPrefix, value);
    }

    private static AuthoritativeItemState ApplyAffixValueMetadata(
        AuthoritativeItemState result,
        AuthoritativeItemAffix affix,
        bool isPrefix,
        int value)
    {
        return result with {
            PrefixPower = isPrefix ? StablePowerId(affix.Power) : result.PrefixPower,
            SuffixPower = isPrefix ? result.SuffixPower : StablePowerId(affix.Power),
            ValueAdd1 = result.ValueAdd1 == 0 && result.ValueMultiply1 == 0 ? value : result.ValueAdd1,
            ValueMultiply1 = result.ValueAdd1 == 0 && result.ValueMultiply1 == 0 ? affix.ValueMultiplier : result.ValueMultiply1,
            ValueAdd2 = result.ValueAdd1 != 0 && result.ValueAdd2 == 0 ? value : result.ValueAdd2,
            ValueMultiply2 = result.ValueAdd1 != 0 && result.ValueAdd2 == value ? affix.ValueMultiplier : result.ValueMultiply2,
        };
    }

    private uint ResolveItemEffectFlag(string symbol, uint fallback)
    {
        return nativeItemEffectCatalog is not null
            && nativeItemEffectCatalog.TryGetFlag(symbol, out var nativeFlag)
            ? nativeFlag
            : fallback;
    }

    private int ResolveHellfireEffectFlag(string symbol, int fallback)
    {
        return nativeItemHellfireEffectCatalog is not null
            && nativeItemHellfireEffectCatalog.TryGetFlag(symbol, out var nativeFlag)
            ? nativeFlag
            : fallback;
    }

    private static int StablePowerId(string power)
    {
        unchecked {
            var hash = 17;
            foreach (var character in power)
                hash = hash * 31 + character;
            return Math.Abs(hash == int.MinValue ? int.MaxValue : hash);
        }
    }
}
