using System.Text;
using Devilution.Data;
using Devilution.Engine;

namespace Devilution.Diablo;

/** One normalized unique-item modifier row. */
public sealed record AuthoritativeUniqueItemPower(string Power, int Value1, int Value2)
{
    public int Roll(uint seed)
    {
        var low = Math.Min(Value1, Value2);
        var high = Math.Max(Value1, Value2);
        if (low == high)
            return low;
        return low + (int)(unchecked(seed * 1664525U + 1013904223U) % (uint)(high - low + 1));
    }

    public int RollLegacy(DeterministicLegacyRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var low = Math.Min(Value1, Value2);
        var high = Math.Max(Value1, Value2);
        return low == high ? low : low + random.Next(high - low + 1);
    }

    /** Preserves the pre-engine overload for existing Diablo callers. */
    public int RollLegacy(AuthoritativeLegacyRandom random)
        => RollLegacy((DeterministicLegacyRandom)random);
}

/** External unique item definition composed from a base item and normalized powers. */
public sealed record AuthoritativeUniqueItemDefinition(
    uint UniqueId,
    string Name,
    uint BaseItemId,
    int MinimumLevel,
    int Value,
    IReadOnlyList<AuthoritativeUniqueItemPower> Powers)
{
    /** Stable content symbol retained alongside the numeric compatibility ID. */
    public string StableSymbol { get; init; } = string.Empty;

    /** Optional legacy cursor symbol; an empty value preserves the base item cursor. */
    public string CursorGraphic { get; init; } = string.Empty;

    /** Optional native cursor projection resolved through the active cursor catalog. */
    public int? NativeCursorGraphic { get; init; }
}

/** Deterministic unique item generation backed by the authoritative base catalog. */
public sealed class AuthoritativeUniqueItemCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "unique_id",
        "unique_symbol",
        "name",
        "base_item_id",
        "base_item_symbol",
        "min_level",
        "value",
        "power",
        "value1",
        "value2",
        "cursor_graphic",
    ];

    private static readonly IReadOnlySet<string> SupportedPowerNames =
        new HashSet<string>(StringComparer.Ordinal) {
            "DAM", "DAMP", "DAMMOD", "TOHIT_DAMP", "TOHIT_DAMP_CURSE", "SETDAM",
            "TOHIT_CURSE", "DAMP_CURSE", "TOHIT", "ACP", "SETAC", "ACP_CURSE",
            "AC_CURSE", "STR", "STR_CURSE", "MAG", "MAG_CURSE", "DEX", "DEX_CURSE",
            "VIT", "VIT_CURSE", "ATTRIBS", "ATTRIBS_CURSE", "LIFE", "LIFE_CURSE",
            "MANA", "MANA_CURSE", "MANATOLIFE", "LIFETOMANA", "GETHIT", "GETHIT_CURSE",
            "FIRERES", "LIGHTRES", "MAGICRES", "ALLRES", "ADDACLIFE", "ADDMANAAC",
            "LIGHT", "LIGHT_CURSE", "TARGAC", "FIREDAM", "LIGHTDAM", "FIRERES_CURSE",
            "LIGHTRES_CURSE", "MAGICRES_CURSE", "DEVASTATION", "PERIL", "JESTERS",
            "DOPPELGANGER", "DECAY", "FIREBALL", "SPLLVLADD", "DUR", "SETDUR",
            "INDESTRUCTIBLE", "SPELL", "CHARGES", "FIRE_ARROWS", "LIGHT_ARROWS", "MULT_ARROWS",
            "RNDARROWVEL", "RNDSTEALLIFE", "STEALMANA", "STEALLIFE", "DRAINLIFE",
            "NOMANA", "FASTATTACK", "FASTRECOVER", "FASTBLOCK", "KNOCKBACK", "THORNS",
            "3XDAMVDEM", "ABSHALFTRAP", "ALLRESZERO", "ACDEMON", "ACUNDEAD", "NOMINSTR",
            "ONEHAND", "DUR_CURSE", "CRYSTALLINE",
        };
    private readonly Dictionary<uint, AuthoritativeUniqueItemDefinition> definitions;
    private readonly AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog;
    private readonly AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog;
    public bool UsesLegacyRandom { get; }

    public AuthoritativeUniqueItemCatalog(
        IEnumerable<AuthoritativeUniqueItemDefinition> definitions,
        bool usesLegacyRandom = false,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.nativeItemHellfireEffectCatalog = nativeItemHellfireEffectCatalog;
        this.nativeItemEffectCatalog = nativeItemEffectCatalog;
        this.definitions = new Dictionary<uint, AuthoritativeUniqueItemDefinition>();
        foreach (var definition in definitions) {
            if (definition.UniqueId == 0 || !IsValidPresentationText(definition.Name) || definition.BaseItemId == 0
                || definition.MinimumLevel < 0 || definition.Value < 0 || definition.Powers.Count > 6
                || !IsValidCursorSymbol(definition.CursorGraphic)
                || definition.NativeCursorGraphic is < 0 or > byte.MaxValue)
                throw new InvalidDataException("Unique item definitions contain invalid bounds.");
            if (definition.Powers.Any(power => string.IsNullOrWhiteSpace(power.Power)))
                throw new InvalidDataException($"Unique item {definition.UniqueId} contains an invalid power.");
            if (!this.definitions.TryAdd(definition.UniqueId, definition))
                throw new InvalidDataException($"Unique item {definition.UniqueId} is defined more than once.");
        }
        UsesLegacyRandom = usesLegacyRandom;
    }

    public IReadOnlyList<AuthoritativeUniqueItemDefinition> Definitions => definitions.Values.OrderBy(definition => definition.UniqueId).ToArray();

    /** Rejects partial normalized unique-item rows at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production unique-item table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows)
            foreach (var column in ProductionColumns)
                _ = row.Required(column);
    }

    /** Rejects unique powers that have no authoritative C# application handler. */
    public void ValidatePowers()
    {
        foreach (var definition in Definitions) {
            foreach (var power in definition.Powers) {
                if (!SupportedPowerNames.Contains(power.Power))
                    throw new InvalidDataException(
                        $"Unique item {definition.UniqueId} uses unsupported power '{power.Power}'.");
            }
        }
    }

    public bool TryGet(uint uniqueId, out AuthoritativeUniqueItemDefinition definition) => definitions.TryGetValue(uniqueId, out definition!);

    /** Validates unique-item base identities against the active item catalog. */
    public void ValidateBaseItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var definition in Definitions) {
            if (!items.TryGet(definition.BaseItemId, out _))
                throw new InvalidDataException(
                    $"Unique item {definition.UniqueId} references unknown base item {definition.BaseItemId}.");
        }
    }

    public bool TrySelectForBase(
        uint baseItemId,
        int itemLevel,
        int chancePercent,
        DeterministicLegacyRandom random,
        out AuthoritativeUniqueItemDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(random);
        definition = null;
        if (random.Next(100) > chancePercent)
            return false;
        var candidates = definitions.Values
            .Where(candidate => candidate.BaseItemId == baseItemId && candidate.MinimumLevel <= itemLevel)
            .OrderBy(candidate => candidate.UniqueId)
            .ToArray();
        if (candidates.Length == 0)
            return false;
        random.Discard(1);
        definition = candidates[^1];
        return true;
    }

    /** Preserves the pre-engine overload for existing Diablo callers. */
    public bool TrySelectForBase(
        uint baseItemId,
        int itemLevel,
        int chancePercent,
        AuthoritativeLegacyRandom random,
        out AuthoritativeUniqueItemDefinition? definition)
        => TrySelectForBase(baseItemId, itemLevel, chancePercent,
            (DeterministicLegacyRandom)random, out definition);

    public AuthoritativeItemState Generate(AuthoritativeItemCatalog items, uint uniqueId, uint itemSeed, int itemLevel)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!TryGet(uniqueId, out var definition))
            throw new InvalidDataException($"Unique item {uniqueId} is not present in the authoritative catalog.");
        if (itemLevel < definition.MinimumLevel)
            throw new InvalidDataException($"Unique item {uniqueId} requires item level {definition.MinimumLevel}.");
        // Legacy unique generation starts from the base item and applies only
        // the unique's declared powers; random prefix/suffix allocation is a
        // separate magic-item path.
        var state = items.Generate(definition.BaseItemId, itemSeed);
        state = state with {
            UniqueId = checked((int)uniqueId),
            BaseName = definition.Name,
            IdentifiedName = definition.Name,
            NativeCursorGraphic = definition.NativeCursorGraphic ?? state.NativeCursorGraphic,
            Value = definition.Value,
            IdentifiedValue = definition.Value,
            Identified = true,
            Magical = 2,
        };
        return ApplyDefinition(state, definition, itemSeed);
    }

    public AuthoritativeItemState ApplyDefinition(
        AuthoritativeItemState state,
        AuthoritativeUniqueItemDefinition definition,
        uint itemSeed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        state = state with {
            UniqueId = checked((int)definition.UniqueId),
            BaseName = definition.Name,
            IdentifiedName = definition.Name,
            NativeCursorGraphic = definition.NativeCursorGraphic ?? state.NativeCursorGraphic,
            Value = definition.Value,
            IdentifiedValue = definition.Value,
            Identified = true,
            Magical = 2,
        };
        if (UsesLegacyRandom) {
            var random = new DeterministicLegacyRandom(itemSeed);
            foreach (var power in definition.Powers)
                state = ApplyPower(state, power, power.RollLegacy(random));
        } else {
            foreach (var (power, index) in definition.Powers.Select((power, index) => (power, index)))
                state = ApplyPower(state, power, power.Roll(itemSeed ^ (uint)(0xC2B2AE35 + index * 97)));
        }
        return state;
    }

    public AuthoritativeItemState ApplyDefinition(
        AuthoritativeItemState state,
        AuthoritativeUniqueItemDefinition definition,
        DeterministicLegacyRandom random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(random);
        state = state with {
            UniqueId = checked((int)definition.UniqueId),
            BaseName = definition.Name,
            IdentifiedName = definition.Name,
            NativeCursorGraphic = definition.NativeCursorGraphic ?? state.NativeCursorGraphic,
            Value = definition.Value,
            IdentifiedValue = definition.Value,
            Identified = true,
            Magical = 2,
        };
        foreach (var power in definition.Powers)
            state = ApplyPower(state, power, power.RollLegacy(random));
        return state;
    }

    /** Preserves the pre-engine overload for existing Diablo callers. */
    public AuthoritativeItemState ApplyDefinition(
        AuthoritativeItemState state,
        AuthoritativeUniqueItemDefinition definition,
        AuthoritativeLegacyRandom random)
        => ApplyDefinition(state, definition, (DeterministicLegacyRandom)random);

    public static AuthoritativeUniqueItemCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemCursorCatalog? cursorCatalog = null,
        StableContentIdCatalog? stableIds = null,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows
            .Select(row => (Row: row, UniqueId: AuthoritativeUniqueItemIdentity.Resolve(row, stableIds)))
            .GroupBy(entry => entry.UniqueId)
            .Select(group => {
                var first = group.First();
                var powers = group.Select(row => new AuthoritativeUniqueItemPower(
                    row.Row.Required("power"),
                    row.Row.OptionalInt32("value1"),
                    row.Row.OptionalInt32("value2"))).ToArray();
                if (group.Any(row => row.Row.Required("name") != first.Row.Required("name")
                    || AuthoritativeUniqueItemIdentity.ReadSymbol(row.Row)
                        != AuthoritativeUniqueItemIdentity.ReadSymbol(first.Row)
                    || AuthoritativeItemIdentity.Resolve(row.Row, stableIds, "base_item_id", "base_item_symbol")
                        != AuthoritativeItemIdentity.Resolve(
                            first.Row,
                            stableIds,
                            "base_item_id",
                            "base_item_symbol")
                    || row.Row.RequiredInt32("min_level") != first.Row.RequiredInt32("min_level")
                    || row.Row.RequiredInt32("value") != first.Row.RequiredInt32("value")
                    || OptionalCursorSymbol(row.Row, "cursor_graphic")
                        != OptionalCursorSymbol(first.Row, "cursor_graphic")))
                    throw new InvalidDataException($"Unique item {group.Key} has inconsistent definition columns.");
                return new AuthoritativeUniqueItemDefinition(
                    group.Key,
                    first.Row.Required("name"),
                    AuthoritativeItemIdentity.Resolve(
                        first.Row,
                        stableIds,
                        "base_item_id",
                        "base_item_symbol"),
                    first.Row.RequiredInt32("min_level"),
                    first.Row.RequiredInt32("value"),
                    powers) {
                        StableSymbol = AuthoritativeUniqueItemIdentity.ReadSymbol(first.Row),
                        CursorGraphic = OptionalCursorSymbol(first.Row, "cursor_graphic"),
                        NativeCursorGraphic = ParseOptionalCursorGraphic(first.Row, cursorCatalog),
                    };
            });
        return new AuthoritativeUniqueItemCatalog(
            definitions,
            nativeItemHellfireEffectCatalog: nativeItemHellfireEffectCatalog,
            nativeItemEffectCatalog: nativeItemEffectCatalog);
    }

    private static bool IsValidPresentationText(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Contains('\0')
            && Encoding.UTF8.GetByteCount(value) < 64;
    }

    private static bool IsValidCursorSymbol(string value)
    {
        return value is not null
            && (value.Length == 0
                || value.All(character => character is >= 'A' and <= 'Z'
                    or >= '0' and <= '9'
                    or '_'));
    }

    private static string OptionalCursorSymbol(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || value.Equals("-", StringComparison.Ordinal))
            return string.Empty;
        return value.Trim();
    }

    private static int? ParseOptionalCursorGraphic(
        TsvRow row,
        AuthoritativeItemCursorCatalog? cursorCatalog)
    {
        var symbol = OptionalCursorSymbol(row, "cursor_graphic");
        return string.IsNullOrEmpty(symbol) || cursorCatalog is null
            ? null
            : cursorCatalog.Resolve(symbol);
    }

    private static string LegacyCursorSymbol(TsvRow row)
    {
        if (!row.TryGet("cursorGraphic", out var value)
            || string.IsNullOrWhiteSpace(value)
            || value.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return value.Trim();
    }

    /** Loads the native unique_itemdat.tsv shape, resolving uniqueBaseItem symbols through the base catalog. */
    public static AuthoritativeUniqueItemCatalog LoadLegacyTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemCatalog items,
        AuthoritativeItemCursorCatalog? cursorCatalog = null,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select((row, index) => {
            var powers = Enumerable.Range(0, 6)
                .Select(powerIndex => {
                    var powerName = $"power{powerIndex}";
                    if (!row.TryGet(powerName, out var power) || string.IsNullOrWhiteSpace(power))
                        return null;
                    return new AuthoritativeUniqueItemPower(
                        power,
                        row.OptionalInt32($"{powerName}.value1"),
                        row.OptionalInt32($"{powerName}.value2"));
                })
                .Where(power => power is not null)
                .Cast<AuthoritativeUniqueItemPower>()
                .ToArray();
            var cursorGraphic = LegacyCursorSymbol(row);
            return new AuthoritativeUniqueItemDefinition(
                checked((uint)index + 1),
                row.Required("name"),
                items.ResolveLegacySymbol(row.Required("uniqueBaseItem")),
                row.RequiredInt32("minLevel"),
                row.RequiredInt32("value"),
                powers) {
                    CursorGraphic = cursorGraphic,
                    NativeCursorGraphic = string.IsNullOrEmpty(cursorGraphic) || cursorCatalog is null
                        ? null
                        : cursorCatalog.Resolve(cursorGraphic),
                };
        }).ToArray();
        return new AuthoritativeUniqueItemCatalog(
            definitions,
            usesLegacyRandom: true,
            nativeItemHellfireEffectCatalog: nativeItemHellfireEffectCatalog,
            nativeItemEffectCatalog: nativeItemEffectCatalog);
    }

    private AuthoritativeItemState ApplyPower(
        AuthoritativeItemState state,
        AuthoritativeUniqueItemPower power,
        int value)
    {
        return power.Power switch {
            "DAM" or "DAMP" => state with { PlusDamage = state.PlusDamage + value },
            "DAMMOD" => state with { PlusDamageModifier = state.PlusDamageModifier + value },
            "TOHIT_DAMP" => state with { PlusToHit = state.PlusToHit + value, PlusDamage = state.PlusDamage + value },
            "TOHIT_DAMP_CURSE" => state with { PlusToHit = state.PlusToHit - value, PlusDamage = state.PlusDamage - value },
            "SETDAM" => state with {
                MinDamage = power.Value1,
                MaxDamage = power.Value2,
            },
            "TOHIT_CURSE" => state with { PlusToHit = state.PlusToHit - value },
            "DAMP_CURSE" => state with { PlusDamage = state.PlusDamage - value },
            "TOHIT" => state with { PlusToHit = state.PlusToHit + value },
            "ACP" => state with { PlusArmorClass = state.PlusArmorClass + value },
            "SETAC" => state with { ArmorClass = value },
            "ACP_CURSE" or "AC_CURSE" => state with { PlusArmorClass = state.PlusArmorClass - value },
            "STR" => state with { PlusStrength = state.PlusStrength + value },
            "STR_CURSE" => state with { PlusStrength = state.PlusStrength - value },
            "MAG" => state with { PlusMagic = state.PlusMagic + value },
            "MAG_CURSE" => state with { PlusMagic = state.PlusMagic - value },
            "DEX" => state with { PlusDexterity = state.PlusDexterity + value },
            "DEX_CURSE" => state with { PlusDexterity = state.PlusDexterity - value },
            "VIT" => state with { PlusVitality = state.PlusVitality + value },
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
            "LIFE" => state with { PlusHitPoints = state.PlusHitPoints + (value << 6) },
            "LIFE_CURSE" => state with { PlusHitPoints = state.PlusHitPoints - (value << 6) },
            "MANA" => state with { PlusMana = state.PlusMana + (value << 6) },
            "MANA_CURSE" => state with { PlusMana = state.PlusMana - (value << 6) },
            "MANATOLIFE" => state with { ManaToLifePercent = state.ManaToLifePercent + value },
            "LIFETOMANA" => state with { LifeToManaPercent = state.LifeToManaPercent + value },
            "GETHIT" => state with { PlusGetHit = state.PlusGetHit - value },
            "GETHIT_CURSE" => state with { PlusGetHit = state.PlusGetHit + value },
            "FIRERES" => state with { PlusFireResistance = state.PlusFireResistance + value },
            "LIGHTRES" => state with { PlusLightningResistance = state.PlusLightningResistance + value },
            "MAGICRES" => state with { PlusMagicResistance = state.PlusMagicResistance + value },
            "ALLRES" => state with {
                PlusFireResistance = Math.Max(state.PlusFireResistance + value, 0),
                PlusLightningResistance = Math.Max(state.PlusLightningResistance + value, 0),
                PlusMagicResistance = Math.Max(state.PlusMagicResistance + value, 0),
            },
            "ADDACLIFE" => state with {
                Flags = state.Flags
                    | ResolveItemEffectFlag(
                        "LightningArrows",
                        AuthoritativeEquipmentElementalDamageRules.LightningArrowsFlag)
                    | ResolveItemEffectFlag(
                        "FireArrows",
                        AuthoritativeEquipmentElementalDamageRules.FireArrowsFlag),
                FireMinDamage = power.Value1,
                FireMaxDamage = power.Value2,
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
                FireMinDamage = power.Value1,
                FireMaxDamage = power.Value2,
                LightningMinDamage = 2,
                LightningMaxDamage = 0,
            },
            "LIGHT" => state with { PlusLight = state.PlusLight + power.Value1 },
            "LIGHT_CURSE" => state with { PlusLight = state.PlusLight - power.Value1 },
            "TARGAC" => state with { PlusEnemyArmorClass = state.PlusEnemyArmorClass + value },
            "FIREDAM" => state with {
                FireMinDamage = power.Value1,
                FireMaxDamage = power.Value2,
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
                LightningMinDamage = power.Value1,
                LightningMaxDamage = power.Value2,
                FireMinDamage = 0,
                FireMaxDamage = 0,
                Flags = (state.Flags | ResolveItemEffectFlag(
                        "LightningDamage",
                        AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag))
                    & ~ResolveItemEffectFlag(
                        "FireDamage",
                        AuthoritativeEquipmentElementalDamageRules.FireDamageFlag),
            },
            "FIRERES_CURSE" => state with { PlusFireResistance = state.PlusFireResistance - value },
            "LIGHTRES_CURSE" => state with { PlusLightningResistance = state.PlusLightningResistance - value },
            "MAGICRES_CURSE" => state with { PlusMagicResistance = state.PlusMagicResistance - value },
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
            // Native Hellfire IPL_FIREBALL marks both arrow channels so the bow
            // command selects the persistent FireballBow projectile.
            "FIREBALL" => state with {
                Flags = state.Flags
                    | ResolveItemEffectFlag(
                        "LightningArrows",
                        AuthoritativeEquipmentElementalDamageRules.LightningArrowsFlag)
                    | ResolveItemEffectFlag(
                        "FireArrows",
                        AuthoritativeEquipmentElementalDamageRules.FireArrowsFlag),
                FireMinDamage = power.Value1,
                FireMaxDamage = power.Value2,
                LightningMinDamage = 0,
                LightningMaxDamage = 0,
            },
            "SPLLVLADD" => state with { SpellLevelAdd = value },
            "DUR" => state with {
                Durability = state.MaxDurability + state.MaxDurability * value / 100,
                MaxDurability = state.MaxDurability + state.MaxDurability * value / 100,
            },
            "DUR_CURSE" => state with {
                Durability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100),
                MaxDurability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100),
            },
            "CRYSTALLINE" => state with {
                PlusDamage = state.PlusDamage + 140 + value * 2,
                Durability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100),
                MaxDurability = Math.Max(1, state.MaxDurability - state.MaxDurability * value / 100),
            },
            "SETDUR" => state with { Durability = value, MaxDurability = value },
            "INDESTRUCTIBLE" => state with { Durability = 255, MaxDurability = 255 },
            "SPELL" => state with {
                SpellId = power.Value1,
                Charges = power.Value2,
                MaxCharges = power.Value2,
                SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            },
            "CHARGES" => state with {
                Charges = state.Charges * Math.Max(1, power.Value1),
                MaxCharges = state.MaxCharges * Math.Max(1, power.Value1),
            },
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
                FireMinDamage = power.Value1,
                FireMaxDamage = power.Value2,
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
                LightningMinDamage = power.Value1,
                LightningMaxDamage = power.Value2,
                FireMinDamage = 0,
                FireMaxDamage = 0,
            },
            "MULT_ARROWS" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "MultipleArrows",
                    AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag),
            },
            "RNDARROWVEL" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "RandomArrowVelocity",
                    AuthoritativeEquipmentAttackEffectRules.RandomArrowVelocityFlag),
            },
            "RNDSTEALLIFE" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "RandomStealLife",
                    AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag),
            },
            "STEALMANA" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    power.Value1 == 3 ? "StealMana3Percent" : "StealMana5Percent",
                    power.Value1 == 3 ? 0x00002000U : 0x00004000U),
            },
            "STEALLIFE" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    power.Value1 == 3 ? "StealLife3Percent" : "StealLife5Percent",
                    power.Value1 == 3 ? 0x00008000U : 0x00010000U),
            },
            "DRAINLIFE" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "DrainLife",
                    AuthoritativeEquipmentIncomingDamageRules.DrainLifeFlag),
            },
            "NOMANA" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "NoMana",
                    AuthoritativeEquipmentAttackEffectRules.NoManaFlag),
            },
            "FASTATTACK" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    power.Value1 switch {
                        1 => "FastAttack1",
                        2 => "FastAttack2",
                        3 => "FastAttack3",
                        _ => "FastAttack4",
                    },
                    power.Value1 switch {
                        1 => 0x00020000U,
                        2 => 0x00040000U,
                        3 => 0x00080000U,
                        _ => 0x00100000U,
                    }),
            },
            "FASTRECOVER" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    power.Value1 switch {
                        1 => "FastRecover1",
                        2 => "FastRecover2",
                        _ => "FastRecover3",
                    },
                    power.Value1 switch {
                        1 => 0x00200000U,
                        2 => 0x00400000U,
                        _ => 0x00800000U,
                    }),
            },
            "FASTBLOCK" => state with {
                Flags = state.Flags | ResolveItemEffectFlag("FastBlock", 0x01000000U),
            },
            "KNOCKBACK" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "Knockback",
                    AuthoritativeEquipmentAttackEffectRules.KnockbackFlag),
            },
            "THORNS" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "Thorns",
                    AuthoritativeEquipmentIncomingDamageRules.ThornsFlag),
            },
            "3XDAMVDEM" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "TripleDemonDamage",
                    AuthoritativeEquipmentAttackEffectRules.TripleDemonDamageFlag),
            },
            "ABSHALFTRAP" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "HalfTrapDamage",
                    AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag),
            },
            "ALLRESZERO" => state with {
                Flags = state.Flags | ResolveItemEffectFlag(
                    "ZeroResistance",
                    AuthoritativeEquipmentResistanceRules.ZeroResistanceFlag),
            },
            "ACDEMON" => state with {
                HellfireDamageArmorFlags = state.HellfireDamageArmorFlags
                    | ResolveHellfireEffectFlag("ACDEMON", 1 << 5),
            },
            "ACUNDEAD" => state with {
                HellfireDamageArmorFlags = state.HellfireDamageArmorFlags
                    | ResolveHellfireEffectFlag("ACUNDEAD", 1 << 6),
            },
            "NOMINSTR" => state with { MinimumStrength = 0 },
            "ONEHAND" => state with { EquipLocation = 1 },
            _ => state,
        };
    }

    private int ResolveHellfireEffectFlag(string symbol, int fallback)
    {
        return nativeItemHellfireEffectCatalog is not null
            && nativeItemHellfireEffectCatalog.TryGetFlag(symbol, out var nativeFlag)
            ? nativeFlag
            : fallback;
    }

    private uint ResolveItemEffectFlag(string symbol, uint fallback)
    {
        return nativeItemEffectCatalog is not null
            && nativeItemEffectCatalog.TryGetFlag(symbol, out var nativeFlag)
            ? nativeFlag
            : fallback;
    }
}
