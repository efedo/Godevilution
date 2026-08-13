using Devilution.Data;

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

    public int RollLegacy(AuthoritativeLegacyRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var low = Math.Min(Value1, Value2);
        var high = Math.Max(Value1, Value2);
        return low == high ? low : low + random.Next(high - low + 1);
    }
}

/** External unique item definition composed from a base item and normalized powers. */
public sealed record AuthoritativeUniqueItemDefinition(
    uint UniqueId,
    string Name,
    uint BaseItemId,
    int MinimumLevel,
    int Value,
    IReadOnlyList<AuthoritativeUniqueItemPower> Powers);

/** Deterministic unique item generation backed by the authoritative base catalog. */
public sealed class AuthoritativeUniqueItemCatalog
{
    private readonly Dictionary<uint, AuthoritativeUniqueItemDefinition> definitions;
    public bool UsesLegacyRandom { get; }

    public AuthoritativeUniqueItemCatalog(IEnumerable<AuthoritativeUniqueItemDefinition> definitions, bool usesLegacyRandom = false)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<uint, AuthoritativeUniqueItemDefinition>();
        foreach (var definition in definitions) {
            if (definition.UniqueId == 0 || string.IsNullOrWhiteSpace(definition.Name) || definition.BaseItemId == 0
                || definition.MinimumLevel < 0 || definition.Value < 0 || definition.Powers.Count > 6)
                throw new InvalidDataException("Unique item definitions contain invalid bounds.");
            if (definition.Powers.Any(power => string.IsNullOrWhiteSpace(power.Power)))
                throw new InvalidDataException($"Unique item {definition.UniqueId} contains an invalid power.");
            if (!this.definitions.TryAdd(definition.UniqueId, definition))
                throw new InvalidDataException($"Unique item {definition.UniqueId} is defined more than once.");
        }
        UsesLegacyRandom = usesLegacyRandom;
    }

    public IReadOnlyList<AuthoritativeUniqueItemDefinition> Definitions => definitions.Values.OrderBy(definition => definition.UniqueId).ToArray();

    public bool TryGet(uint uniqueId, out AuthoritativeUniqueItemDefinition definition) => definitions.TryGetValue(uniqueId, out definition!);

    public bool TrySelectForBase(
        uint baseItemId,
        int itemLevel,
        int chancePercent,
        AuthoritativeLegacyRandom random,
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
        var state = items.Generate(definition.BaseItemId, itemSeed) with {
            UniqueId = checked((int)uniqueId),
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
            Value = definition.Value,
            IdentifiedValue = definition.Value,
            Identified = true,
            Magical = 2,
        };
        if (UsesLegacyRandom) {
            var random = new AuthoritativeLegacyRandom(itemSeed);
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
        AuthoritativeLegacyRandom random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(random);
        state = state with {
            UniqueId = checked((int)definition.UniqueId),
            Value = definition.Value,
            IdentifiedValue = definition.Value,
            Identified = true,
            Magical = 2,
        };
        foreach (var power in definition.Powers)
            state = ApplyPower(state, power, power.RollLegacy(random));
        return state;
    }

    public static AuthoritativeUniqueItemCatalog LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows
            .GroupBy(row => row.RequiredUInt32("unique_id"))
            .Select(group => {
                var first = group.First();
                var powers = group.Select(row => new AuthoritativeUniqueItemPower(
                    row.Required("power"),
                    row.OptionalInt32("value1"),
                    row.OptionalInt32("value2"))).ToArray();
                if (group.Any(row => row.Required("name") != first.Required("name")
                    || row.RequiredUInt32("base_item_id") != first.RequiredUInt32("base_item_id")
                    || row.RequiredInt32("min_level") != first.RequiredInt32("min_level")
                    || row.RequiredInt32("value") != first.RequiredInt32("value")))
                    throw new InvalidDataException($"Unique item {group.Key} has inconsistent definition columns.");
                return new AuthoritativeUniqueItemDefinition(
                    group.Key,
                    first.Required("name"),
                    first.RequiredUInt32("base_item_id"),
                    first.RequiredInt32("min_level"),
                    first.RequiredInt32("value"),
                    powers);
            });
        return new AuthoritativeUniqueItemCatalog(definitions);
    }

    /** Loads the native unique_itemdat.tsv shape, resolving uniqueBaseItem symbols through the base catalog. */
    public static AuthoritativeUniqueItemCatalog LoadLegacyTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemCatalog items)
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
            return new AuthoritativeUniqueItemDefinition(
                checked((uint)index + 1),
                row.Required("name"),
                items.ResolveLegacySymbol(row.Required("uniqueBaseItem")),
                row.RequiredInt32("minLevel"),
                row.RequiredInt32("value"),
                powers);
        }).ToArray();
        return new AuthoritativeUniqueItemCatalog(definitions, usesLegacyRandom: true);
    }

    private static AuthoritativeItemState ApplyPower(AuthoritativeItemState state, AuthoritativeUniqueItemPower power, int value)
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
                Flags = state.Flags | 0x02000000U | 0x00000008U,
                FireMinDamage = power.Value1,
                FireMaxDamage = power.Value2,
                LightningMinDamage = 1,
                LightningMaxDamage = 0,
            },
            "ADDMANAAC" => state with {
                Flags = state.Flags | 0x00000020U | 0x00000010U,
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
                Flags = (state.Flags | 0x00000010U) & ~0x00000020U,
            },
            "LIGHTDAM" => state with {
                LightningMinDamage = power.Value1,
                LightningMaxDamage = power.Value2,
                FireMinDamage = 0,
                FireMaxDamage = 0,
                Flags = (state.Flags | 0x00000020U) & ~0x00000010U,
            },
            "FIRERES_CURSE" => state with { PlusFireResistance = state.PlusFireResistance - value },
            "LIGHTRES_CURSE" => state with { PlusLightningResistance = state.PlusLightningResistance - value },
            "MAGICRES_CURSE" => state with { PlusMagicResistance = state.PlusMagicResistance - value },
            // Native Hellfire IPL_FIREBALL marks both arrow channels so the bow
            // command selects the persistent FireballBow projectile.
            "FIREBALL" => state with {
                Flags = state.Flags | 0x02000000U | 0x00000008U,
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
            "SETDUR" => state with { Durability = value, MaxDurability = value },
            "INDESTRUCTIBLE" => state with { Durability = 255, MaxDurability = 255 },
            "SPELL" => state with { SpellId = power.Value1, Charges = power.Value2, MaxCharges = power.Value2 },
            "FIRE_ARROWS" => state with {
                Flags = (state.Flags | 0x00000008U | 0x00000010U) & ~0x02000000U,
                FireMinDamage = power.Value1,
                FireMaxDamage = power.Value2,
                LightningMinDamage = 0,
                LightningMaxDamage = 0,
            },
            "LIGHT_ARROWS" => state with {
                Flags = (state.Flags | 0x02000000U | 0x00000020U) & ~0x00000008U,
                LightningMinDamage = power.Value1,
                LightningMaxDamage = power.Value2,
                FireMinDamage = 0,
                FireMaxDamage = 0,
            },
            "MULT_ARROWS" => state with { Flags = state.Flags | 0x00000200U },
            "RNDARROWVEL" => state with { Flags = state.Flags | 0x00000004U },
            "RNDSTEALLIFE" => state with { Flags = state.Flags | 0x00000002U },
            "STEALMANA" => state with { Flags = state.Flags | (power.Value1 == 3 ? 0x00002000U : 0x00004000U) },
            "STEALLIFE" => state with { Flags = state.Flags | (power.Value1 == 3 ? 0x00008000U : 0x00010000U) },
            "DRAINLIFE" => state with { Flags = state.Flags | 0x00000040U },
            "NOMANA" => state with { Flags = state.Flags | 0x08000000U },
            "FASTATTACK" => state with { Flags = state.Flags | (power.Value1 switch { 1 => 0x00020000U, 2 => 0x00040000U, 3 => 0x00080000U, _ => 0x00100000U }) },
            "FASTRECOVER" => state with { Flags = state.Flags | (power.Value1 switch { 1 => 0x00200000U, 2 => 0x00400000U, _ => 0x00800000U }) },
            "FASTBLOCK" => state with { Flags = state.Flags | 0x01000000U },
            "KNOCKBACK" => state with { Flags = state.Flags | 0x00000800U },
            "THORNS" => state with { Flags = state.Flags | 0x04000000U },
            "3XDAMVDEM" => state with { Flags = state.Flags | 0x40000000U },
            "ABSHALFTRAP" => state with { Flags = state.Flags | 0x10000000U },
            "ALLRESZERO" => state with { Flags = state.Flags | 0x80000000U },
            "ACUNDEAD" => state with { HellfireDamageArmorFlags = state.HellfireDamageArmorFlags | (1 << 6) },
            "NOMINSTR" => state with { MinimumStrength = 0 },
            "ONEHAND" => state with { EquipLocation = 1 },
            _ => state,
        };
    }
}
