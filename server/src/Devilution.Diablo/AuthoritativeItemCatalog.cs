using Devilution.Data;

namespace Devilution.Diablo;

/** Declarative base item definition used by authoritative item generation. */
public sealed record AuthoritativeItemDefinition(
    uint ItemId,
    int ItemType,
    int Value,
    int IdentifiedValue,
    int MinDamage,
    int MaxDamage,
    int ArmorClass,
    int Durability,
    int MaxDurability,
    int InventoryWidth,
    int InventoryHeight,
    bool Identified)
{
    public IReadOnlySet<string> GenerationTags { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public int MinArmorClass { get; init; } = -1;

    public int MaxArmorClass { get; init; } = -1;

    public int EquipLocation { get; init; }

    public int ItemClass { get; init; }

    public int MiscId { get; init; }

    public int SpellId { get; init; }

    public uint Flags { get; init; }

    public int MinimumStrength { get; init; }

    public int MinimumMagic { get; init; }

    public int MinimumDexterity { get; init; }

    /** Native `_item_indexes` value used only by the compatibility client. */
    public int NativeItemIndex { get; init; } = checked((int)ItemId);

    public int Charges { get; init; }

    public int MaxCharges { get; init; }

    public AuthoritativeItemState Generate(uint itemSeed)
    {
        return Generate(itemSeed, new AuthoritativeLegacyRandom(itemSeed));
    }

    public AuthoritativeItemState Generate(uint itemSeed, AuthoritativeLegacyRandom random)
    {
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        ArgumentNullException.ThrowIfNull(random);
        return AuthoritativeItemState.Empty with {
            CreateInfo = itemSeed,
            ItemType = ItemType,
            EquipLocation = EquipLocation,
            ItemClass = ItemClass,
            MiscId = MiscId,
            SpellId = SpellId,
            Flags = Flags,
            MinimumStrength = MinimumStrength,
            MinimumMagic = MinimumMagic,
            MinimumDexterity = MinimumDexterity,
            ItemIndex = NativeItemIndex,
            Value = Value,
            IdentifiedValue = IdentifiedValue,
            MinDamage = MinDamage,
            MaxDamage = MaxDamage,
            ArmorClass = MinArmorClass >= 0 && MaxArmorClass >= MinArmorClass
                ? MinArmorClass + random.Next(MaxArmorClass - MinArmorClass + 1)
                : ArmorClass,
            Charges = Charges,
            MaxCharges = MaxCharges,
            Durability = Durability,
            MaxDurability = MaxDurability,
            InventoryWidth = InventoryWidth,
            InventoryHeight = InventoryHeight,
            Identified = Identified,
        };
    }
}

/** Validated external item definitions keyed by stable numeric content ID. */
public sealed class AuthoritativeItemCatalog
{
    private readonly Dictionary<uint, AuthoritativeItemDefinition> definitions;
    private readonly AuthoritativeItemAffixCatalog? affixes;
    private AuthoritativeUniqueItemCatalog? uniqueItems;
    private readonly Dictionary<string, uint> legacySymbols = new(StringComparer.OrdinalIgnoreCase);

    public AuthoritativeItemCatalog(
        IEnumerable<AuthoritativeItemDefinition> definitions,
        AuthoritativeItemAffixCatalog? affixes = null,
        AuthoritativeUniqueItemCatalog? uniqueItems = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<uint, AuthoritativeItemDefinition>();
        foreach (var definition in definitions) {
            if (definition.ItemId == 0 || definition.ItemType < 0 || definition.Value < 0 || definition.IdentifiedValue < 0
                || definition.MinDamage < 0 || definition.MaxDamage < definition.MinDamage || definition.ArmorClass < 0
                || definition.Durability < 0 || definition.MaxDurability < definition.Durability
                || definition.InventoryWidth <= 0 || definition.InventoryHeight <= 0
                || definition.NativeItemIndex < -1
                || definition.Charges < 0 || definition.MaxCharges < definition.Charges)
                throw new InvalidDataException("Item definitions contain invalid bounds.");
            if (!this.definitions.TryAdd(definition.ItemId, definition))
                throw new InvalidDataException($"Item {definition.ItemId} is defined more than once.");
        }
        if (this.definitions.Count == 0)
            throw new InvalidDataException("The item catalog cannot be empty.");
        this.affixes = affixes;
        this.uniqueItems = uniqueItems;
    }

    public IReadOnlyList<AuthoritativeItemDefinition> Definitions => definitions.Values.OrderBy(definition => definition.ItemId).ToArray();

    public bool TryGet(uint itemId, out AuthoritativeItemDefinition definition) => definitions.TryGetValue(itemId, out definition!);

    public AuthoritativeItemDefinition GetRequired(uint itemId)
    {
        return TryGet(itemId, out var definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Item {itemId} is not present in the authoritative catalog.");
    }

    public uint ResolveLegacySymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol) || !legacySymbols.TryGetValue(symbol, out var itemId))
            throw new InvalidDataException($"Legacy item '{symbol}' is not present in the authoritative catalog.");
        return itemId;
    }

    public void AttachUniqueCatalog(AuthoritativeUniqueItemCatalog uniqueCatalog)
    {
        uniqueItems = uniqueCatalog ?? throw new ArgumentNullException(nameof(uniqueCatalog));
    }

    public AuthoritativeItemState Generate(uint itemId, uint itemSeed)
        => Generate(itemId, itemSeed, 0);

    public AuthoritativeItemState Generate(uint itemId, uint itemSeed, int itemLevel)
    {
        if (!TryGet(itemId, out var definition))
            throw new InvalidDataException($"Item {itemId} is not present in the authoritative catalog.");
        var useLegacyRandom = affixes?.UsesLegacyRandom == true;
        var random = useLegacyRandom ? new AuthoritativeLegacyRandom(itemSeed) : null;
        var state = useLegacyRandom ? definition.Generate(itemSeed, random!) : definition.Generate(itemSeed);
        return affixes is null || itemLevel <= 0
            ? state
            : useLegacyRandom
                ? affixes.Apply(state, random!, itemLevel, definition.GenerationTags)
                : affixes.Apply(state, itemSeed, itemLevel, definition.GenerationTags);
    }

    /** Generates a monster drop using the native magic-quality probability sequence. */
    public AuthoritativeItemState GenerateDrop(
        uint itemId,
        uint itemSeed,
        int monsterLevel,
        bool onlyGood = false,
        int? uniqueChancePercent = null)
    {
        if (!TryGet(itemId, out var definition))
            throw new InvalidDataException($"Item {itemId} is not present in the authoritative catalog.");
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        if (affixes is null || !affixes.UsesLegacyRandom || monsterLevel <= 0)
            return definition.Generate(itemSeed);

        var random = new AuthoritativeLegacyRandom(itemSeed);
        var state = definition.Generate(itemSeed, random);
        var rules = affixes.GenerationRules;
        var isAlwaysMagic = rules.AlwaysMagicMisc.Overlaps(definition.GenerationTags);
        var isMagic = isAlwaysMagic
            || random.Next(100) <= rules.MagicChanceBase
            || random.Next(100) <= monsterLevel * rules.MagicChancePerLevel
            || onlyGood;
        if (!isMagic)
            return state;
        if (uniqueItems is not null
            && uniqueItems.TrySelectForBase(
                itemId,
                monsterLevel,
                uniqueChancePercent ?? rules.UniqueChanceNormal,
                random,
                out var uniqueDefinition))
            return uniqueItems.ApplyDefinition(state, uniqueDefinition!, random);
        var result = affixes.Apply(state, random, monsterLevel, definition.GenerationTags);
        if (result.Magical != 2 && result.MaxDurability > 0 && result.MaxDurability != 255)
            result = result with {
                Durability = random.Next(result.MaxDurability / 2) + result.MaxDurability / 4 + 1,
            };
        return result;
    }

    public AuthoritativeItemState GenerateUnique(uint uniqueId, uint itemSeed, int itemLevel)
    {
        if (uniqueItems is null)
            throw new InvalidDataException("No unique item catalog is loaded.");
        return uniqueItems.Generate(this, uniqueId, itemSeed, itemLevel);
    }

    public static AuthoritativeItemCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemAffixCatalog? affixes = null,
        AuthoritativeUniqueItemCatalog? uniqueItems = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row => new AuthoritativeItemDefinition(
            row.RequiredUInt32("item_id"),
            row.RequiredInt32("item_type"),
            row.RequiredInt32("value"),
            row.RequiredInt32("identified_value"),
            row.OptionalInt32("min_damage"),
            row.OptionalInt32("max_damage"),
            row.OptionalInt32("armor_class"),
            row.OptionalInt32("durability"),
            row.OptionalInt32("max_durability"),
            Math.Max(1, row.OptionalInt32("inventory_width", 1)),
            Math.Max(1, row.OptionalInt32("inventory_height", 1)),
            row.OptionalInt32("identified") != 0) {
                GenerationTags = row.TryGet("item_tags", out var tags) && !string.IsNullOrWhiteSpace(tags)
                    ? tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                EquipLocation = row.OptionalInt32("equip_location"),
                ItemClass = row.OptionalInt32("item_class"),
                MiscId = row.OptionalInt32("misc_id"),
                SpellId = row.OptionalInt32("spell_id"),
                Flags = row.OptionalUInt32("flags"),
                MinimumStrength = row.OptionalInt32("min_strength"),
                MinimumMagic = row.OptionalInt32("min_magic"),
                MinimumDexterity = row.OptionalInt32("min_dexterity"),
                NativeItemIndex = row.OptionalInt32(
                    "native_item_index",
                    checked((int)row.RequiredUInt32("item_id"))),
                Charges = row.OptionalInt32("charges"),
                MaxCharges = row.OptionalInt32("max_charges"),
            }).ToArray();
        return new AuthoritativeItemCatalog(definitions, affixes, uniqueItems);
    }

    /** Loads the shipped itemdat.tsv shape and assigns stable IDs by explicit row order. */
    public static AuthoritativeItemCatalog LoadLegacyTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemAffixCatalog? affixes = null,
        AuthoritativeUniqueItemCatalog? uniqueItems = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var rows = table.Rows.ToArray();
        var definitions = rows.Select((row, index) => {
            var itemType = ParseLegacyItemType(row.Required("itemType"));
            var armorMin = row.OptionalInt32("minArmor");
            var armorMax = row.OptionalInt32("maxArmor", armorMin);
            return new AuthoritativeItemDefinition(
                checked((uint)index + 1),
                itemType,
                row.OptionalInt32("value"),
                row.OptionalInt32("value"),
                row.OptionalInt32("minDamage"),
                row.OptionalInt32("maxDamage"),
                armorMax,
                row.OptionalInt32("durability"),
                row.OptionalInt32("durability"),
                1,
                1,
                false) {
                    GenerationTags = LegacyTags(row.Required("itemType")),
                    MinArmorClass = armorMin,
                    MaxArmorClass = armorMax,
                    EquipLocation = ParseLegacyEquipLocation(row.Required("equipType")),
                    ItemClass = ParseLegacyItemClass(row.Required("class")),
                    MiscId = ParseLegacyMiscId(OptionalText(row, "miscId")),
                    SpellId = ParseLegacySpellId(OptionalText(row, "spell")),
                    Flags = ParseLegacyFlags(OptionalText(row, "specialEffects")),
                    MinimumStrength = row.OptionalInt32("minStrength"),
                    MinimumMagic = row.OptionalInt32("minMagic"),
                    MinimumDexterity = row.OptionalInt32("minDexterity"),
                    NativeItemIndex = index,
                    Charges = ParseLegacyMiscId(OptionalText(row, "miscId")) == 24
                        ? 40
                        : 0,
                    MaxCharges = ParseLegacyMiscId(OptionalText(row, "miscId")) == 24
                        ? 40
                        : 0,
                };
        }).ToArray();
        var catalog = new AuthoritativeItemCatalog(definitions, affixes, uniqueItems);
        foreach (var (row, index) in rows.Select((row, index) => (row, index))) {
            var numericId = checked((uint)index + 1);
            var symbols = new List<string>();
            if (row.TryGet("id", out var id) && !string.IsNullOrWhiteSpace(id))
                symbols.Add(id);
            if (!string.IsNullOrWhiteSpace(id) && id.StartsWith("IDI_", StringComparison.OrdinalIgnoreCase))
                symbols.Add(id[4..]);
            if (row.TryGet("cursorGraphic", out var cursor) && !string.IsNullOrWhiteSpace(cursor))
                symbols.Add(cursor);
            if (row.TryGet("uniqueBaseItem", out var uniqueBase) && !string.IsNullOrWhiteSpace(uniqueBase)
                && !uniqueBase.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                symbols.Add(uniqueBase);
            if (symbols.Count == 0)
                symbols.Add($"legacy_{index + 1}");
            foreach (var symbol in symbols)
                catalog.legacySymbols.TryAdd(symbol, numericId);
        }
        return catalog;
    }

    private static int ParseLegacyItemType(string value)
    {
        return value.ToLowerInvariant() switch {
            "misc" => 0,
            "weapon" or "sword" => 1,
            "axe" => 2,
            "bow" => 3,
            "mace" => 4,
            "shield" => 5,
            "lightarmor" or "light armor" => 6,
            "helm" => 7,
            "mediumarmor" or "medium armor" => 8,
            "heavyarmor" or "heavy armor" => 9,
            "staff" => 10,
            "gold" => 11,
            "ring" => 12,
            "amulet" => 13,
            _ => -1,
        };
    }

    private static string OptionalText(TsvRow row, string column)
    {
        return row.TryGet(column, out var value) ? value : string.Empty;
    }

    private static int ParseLegacyItemClass(string value)
    {
        return value.ToLowerInvariant() switch {
            "weapon" => 1,
            "armor" => 2,
            "misc" => 3,
            "gold" => 4,
            "quest" => 5,
            _ => 0,
        };
    }

    private static int ParseLegacyEquipLocation(string value)
    {
        return value.ToLowerInvariant() switch {
            "one-handed" or "onehanded" => 1,
            "two-handed" or "twohanded" => 2,
            "armor" => 3,
            "helm" => 4,
            "ring" => 5,
            "amulet" => 6,
            "belt" => 8,
            "unequippable" => 7,
            _ => 0,
        };
    }

    private static int ParseLegacyMiscId(string value)
    {
        return value.ToUpperInvariant() switch {
            "STAFF" => 24,
            "BOOK" => 25,
            "RING" => 26,
            "AMULET" => 27,
            "UNIQUE" => 28,
            "SCROLL" => 22,
            "SCROLLT" => 23,
            "HEAL" => 3,
            "FULLHEAL" => 2,
            "MANA" => 6,
            "FULLMANA" => 7,
            "REJUV" => 19,
            "FULLREJUV" => 20,
            "EAR" => 44,
            "MAPOFDOOM" => 43,
            "SPECELIX" => 45,
            "OILBSMTH" => 37,
            _ => 0,
        };
    }

    private static int ParseLegacySpellId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Null", StringComparison.OrdinalIgnoreCase))
            return 0;
        var spells = new[] {
            "Null", "Firebolt", "Healing", "Lightning", "Flash", "Identify", "FireWall", "TownPortal",
            "StoneCurse", "Infravision", "Phasing", "ManaShield", "Fireball", "Guardian", "ChainLightning",
            "FlameWave", "DoomSerpents", "BloodRitual", "Nova", "Invisibility", "Inferno", "Golem", "Rage",
            "Teleport", "Apocalypse", "Etherealize", "ItemRepair", "StaffRecharge", "TrapDisarm", "Elemental",
            "ChargedBolt", "HolyBolt", "Resurrect", "Telekinesis", "HealOther", "BloodStar", "BoneSpirit",
            "Mana", "Magi", "Jester", "LightningWall", "Immolation", "Warp", "Reflect", "Berserk", "RingOfFire",
            "Search", "RuneOfFire", "RuneOfLight", "RuneOfNova", "RuneOfImmolation", "RuneOfStone",
        };
        var index = Array.FindIndex(spells, spell => spell.Equals(value, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : 0;
    }

    private static uint ParseLegacyFlags(string value)
    {
        var flags = 0U;
        foreach (var effect in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            flags |= effect.ToUpperInvariant() switch {
                "RANDOMSTEALLIFE" => 0x00000002U,
                "RANDOMARROWVELOCITY" => 0x00000004U,
                "FIREARROWS" => 0x00000008U,
                "FIREDAMAGE" => 0x00000010U,
                "LIGHTNINGDAMAGE" => 0x00000020U,
                "DRAINLIFE" => 0x00000040U,
                "MULTIPLE ARROWS" or "MULTIPLEARROWS" => 0x00000200U,
                "KNOCKBACK" => 0x00000800U,
                "THORNS" => 0x04000000U,
                "NOMANA" => 0x08000000U,
                "HALFTRAPDAMAGE" => 0x10000000U,
                "TRIPLEDEMONDAMAGE" => 0x40000000U,
                "ZERORESISTANCE" => 0x80000000U,
                _ => 0U,
            };
        }
        return flags;
    }

    private static IReadOnlySet<string> LegacyTags(string itemType)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        switch (itemType.ToLowerInvariant()) {
        case "sword":
        case "axe":
        case "mace":
            tags.Add("Weapon");
            break;
        case "bow":
            tags.Add("Bow");
            break;
        case "staff":
            tags.Add("Staff");
            break;
        case "shield":
            tags.Add("Shield");
            break;
        case "armor":
        case "helm":
        case "light armor":
        case "medium armor":
        case "heavy armor":
            tags.Add("Armor");
            break;
        case "misc":
            tags.Add("Misc");
            break;
        case "ring":
            tags.Add("Misc");
            tags.Add("Ring");
            break;
        case "amulet":
            tags.Add("Misc");
            tags.Add("Amulet");
            break;
        }
        return tags;
    }
}
