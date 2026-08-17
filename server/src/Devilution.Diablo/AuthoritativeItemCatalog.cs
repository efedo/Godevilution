using System.Text;
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
    /** Stable content symbol retained alongside the numeric compatibility ID. */
    public string StableSymbol { get; init; } = string.Empty;

    /** Explicit content-owned classification; null preserves legacy native fallback. */
    public DiabloItemSpellSourceKind? SpellSourceKind { get; init; }

    /** Explicit content-owned item action; None preserves legacy fallback. */
    public DiabloItemActionKind ItemActionKind { get; init; }

    public IReadOnlySet<string> GenerationTags { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public int MinArmorClass { get; init; } = -1;

    public int MaxArmorClass { get; init; } = -1;

    public int EquipLocation { get; init; }

    public int ItemClass { get; init; }

    public int MiscId { get; init; }

    public int SpellId { get; init; }

    public uint Flags { get; init; }

    /** Native Hellfire secondary damage/armor effects supplied by content. */
    public int HellfireDamageArmorFlags { get; init; }

    public int MinimumStrength { get; init; }

    public int MinimumMagic { get; init; }

    public int MinimumDexterity { get; init; }

    /** Native `_item_indexes` value used only by the compatibility client. */
    public int NativeItemIndex { get; init; } = checked((int)ItemId);

    public int Charges { get; init; }

    public int MaxCharges { get; init; }

    /** Optional content-owned display name projected through item state. */
    public string Name { get; init; } = string.Empty;

    /** Optional identified display name; empty uses `Name`. */
    public string IdentifiedName { get; init; } = string.Empty;

    /** Optional native cursor projection for compatibility clients. */
    public int? NativeCursorGraphic { get; init; }

    /** Optional legacy cursor symbol resolved through the active cursor catalog. */
    public string CursorGraphic { get; init; } = string.Empty;

    /** Native minimum monster level for random item drops. */
    public int MinimumMonsterLevel { get; init; }

    /** Native weighted drop rate; zero excludes the item from random selection. */
    public int DropRate { get; init; }

    /** Optional native iUsable flag for cursor-consumable item actions. */
    public bool? Usable { get; init; }

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
            SpellSourceKind = SpellSourceKind,
            ItemActionKind = ItemActionKind,
            Flags = Flags,
            HellfireDamageArmorFlags = HellfireDamageArmorFlags,
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
            BaseName = Name,
            IdentifiedName = string.IsNullOrEmpty(IdentifiedName) ? Name : IdentifiedName,
            NativeCursorGraphic = NativeCursorGraphic,
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
                || definition.Charges < 0 || definition.MaxCharges < definition.Charges
                || (definition.SpellSourceKind is not null
                    && !Enum.IsDefined(definition.SpellSourceKind.Value))
                || !Enum.IsDefined(definition.ItemActionKind)
                || (definition.SpellSourceKind == DiabloItemSpellSourceKind.ChargedItem
                    && (definition.SpellId <= 0 || definition.MaxCharges <= 0))
                || (definition.SpellSourceKind == DiabloItemSpellSourceKind.ConsumableItem
                    && definition.SpellId <= 0)
                || definition.SpellId < 0
                || !IsValidPresentationText(definition.Name)
                || !IsValidPresentationText(definition.IdentifiedName)
                || !IsValidCursorSymbol(definition.CursorGraphic)
                || definition.MinimumMonsterLevel < 0
                || definition.DropRate is < 0 or > byte.MaxValue
                || definition.HellfireDamageArmorFlags is < 0 or > 0x7F
                || definition.MinArmorClass < -1
                || definition.MaxArmorClass < -1
                || (definition.MinArmorClass == -1) != (definition.MaxArmorClass == -1)
                || (definition.MinArmorClass >= 0
                    && definition.MaxArmorClass < definition.MinArmorClass)
                || definition.NativeCursorGraphic is < 0 or > byte.MaxValue)
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

    /** Validates item spell identities against the active spell catalog. */
    public void ValidateSpellReferences(AuthoritativeSpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        foreach (var definition in Definitions.Where(definition => definition.SpellId != 0)) {
            if (!spells.TryGet(checked((uint)definition.SpellId), out _))
                throw new InvalidDataException(
                    $"Item {definition.ItemId} references unknown spell {definition.SpellId}.");
        }
    }

    private static bool IsValidPresentationText(string value)
    {
        return value is not null
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

    /** Resolves a content definition from the native item index retained in state. */
    public bool TryGetByNativeItemIndex(
        int nativeItemIndex,
        out AuthoritativeItemDefinition definition)
    {
        var matches = definitions.Values
            .Where(item => item.NativeItemIndex == nativeItemIndex)
            .ToArray();
        if (matches.Length == 0) {
            definition = null!;
            return false;
        }

        // Several legacy rows intentionally share an index. Only expose the
        // lookup when their content-owned tags agree; otherwise the caller
        // must retain the native item-type fallback.
        var firstTags = matches[0].GenerationTags;
        if (matches.Any(match => !firstTags.SetEquals(match.GenerationTags))) {
            definition = null!;
            return false;
        }

        definition = matches[0];
        return true;
    }

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

    /** Returns whether a catalog item is eligible for a random monster drop at the supplied level. */
    public bool CanDropAtMonsterLevel(uint itemId, int monsterLevel)
    {
        if (!TryGet(itemId, out var definition))
            throw new InvalidDataException($"Item {itemId} is not present in the authoritative catalog.");
        if (monsterLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterLevel));
        return monsterLevel == 0 || monsterLevel >= definition.MinimumMonsterLevel;
    }

    /** Generates a monster drop using the native magic-quality probability sequence. */
    public AuthoritativeItemState GenerateDrop(
        uint itemId,
        uint itemSeed,
        int monsterLevel,
        bool onlyGood = false,
        int? uniqueChancePercent = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer)
    {
        if (!TryGet(itemId, out var definition))
            throw new InvalidDataException($"Item {itemId} is not present in the authoritative catalog.");
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        if (spellBooks is not null && spellBooks.IsBookTemplate(this, itemId))
            return spellBooks.GenerateRandom(
                this,
                itemSeed,
                monsterLevel,
                gameMode,
                itemId);
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
        AuthoritativeUniqueItemCatalog? uniqueItems = null,
        AuthoritativeItemCursorCatalog? cursorCatalog = null,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var definitions = table.Rows.Select(row => new AuthoritativeItemDefinition(
            AuthoritativeItemIdentity.Resolve(row, stableIds),
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
            row.OptionalBoolean("identified")) {
                StableSymbol = AuthoritativeItemIdentity.ReadSymbol(row),
                GenerationTags = row.TryGet("item_tags", out var tags) && !string.IsNullOrWhiteSpace(tags)
                    ? tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                EquipLocation = row.OptionalInt32("equip_location"),
                ItemClass = row.OptionalInt32("item_class"),
                MiscId = row.OptionalInt32("misc_id"),
                SpellId = ResolveSpellId(row, stableIds),
                SpellSourceKind = ParseSpellSourceKind(row),
                ItemActionKind = ParseItemActionKind(row),
                Flags = ParseNormalizedFlags(row),
                HellfireDamageArmorFlags = ParseNormalizedHellfireEffects(row),
                MinArmorClass = row.OptionalInt32("min_armor_class", -1),
                MaxArmorClass = row.OptionalInt32("max_armor_class", -1),
                MinimumStrength = row.OptionalInt32("min_strength"),
                MinimumMagic = row.OptionalInt32("min_magic"),
                MinimumDexterity = row.OptionalInt32("min_dexterity"),
                NativeItemIndex = row.OptionalInt32(
                    "native_item_index",
                    checked((int)row.RequiredUInt32("item_id"))),
                Charges = row.OptionalInt32("charges"),
                MaxCharges = row.OptionalInt32("max_charges"),
                Name = OptionalPresentationText(row, "name"),
                IdentifiedName = OptionalPresentationText(row, "identified_name"),
                CursorGraphic = OptionalPresentationText(row, "cursor_graphic"),
                MinimumMonsterLevel = row.OptionalInt32("min_monster_level"),
                DropRate = row.OptionalInt32("drop_rate"),
                Usable = row.OptionalNullableBoolean("usable"),
                NativeCursorGraphic = ParseOptionalCursorGraphic(row, cursorCatalog),
            }).ToArray();
        return new AuthoritativeItemCatalog(definitions, affixes, uniqueItems);
    }

    private static int ResolveSpellId(TsvRow row, StableContentIdCatalog? stableIds)
    {
        var numericId = row.OptionalInt32("spell_id");
        if (!row.TryGet("spell_symbol", out var symbol)
            || string.IsNullOrWhiteSpace(symbol))
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Item row {row.LineNumber} requires stable spell IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("spell", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Item row {row.LineNumber} references unknown stable spell symbol '{symbol}'.",
                exception);
        }
        if (resolvedId > int.MaxValue)
            throw new InvalidDataException(
                $"Item row {row.LineNumber} resolves spell symbol '{symbol}' beyond the item spell-ID range.");
        if (numericId != 0 && numericId != (int)resolvedId)
            throw new InvalidDataException(
                $"Item row {row.LineNumber} maps spell {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return checked((int)resolvedId);
    }

    internal static DiabloItemSpellSourceKind? ParseSpellSourceKind(TsvRow row)
    {
        if (!row.TryGet("spell_source_kind", out var value))
            return null;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"Column 'spell_source_kind' is empty on TSV row {row.LineNumber}.");
        if (Enum.TryParse<DiabloItemSpellSourceKind>(value, ignoreCase: true, out var kind)
            && Enum.IsDefined(kind))
            return kind;
        throw new InvalidDataException(
            $"TSV row {row.LineNumber} contains unknown item spell source kind '{value}'.");
    }

    internal static DiabloItemActionKind ParseItemActionKind(TsvRow row)
    {
        if (!row.TryGet("item_action_kind", out var value)
            || string.IsNullOrWhiteSpace(value))
            return DiabloItemActionKind.None;
        if (Enum.TryParse<DiabloItemActionKind>(value, ignoreCase: true, out var kind)
            && Enum.IsDefined(kind))
            return kind;
        throw new InvalidDataException(
            $"TSV row {row.LineNumber} contains unknown item action kind '{value}'.");
    }

    /** Loads the shipped itemdat.tsv shape and assigns stable IDs by explicit row order. */
    public static AuthoritativeItemCatalog LoadLegacyTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemAffixCatalog? affixes = null,
        AuthoritativeUniqueItemCatalog? uniqueItems = null,
        AuthoritativeSpellCatalog? spellCatalog = null,
        AuthoritativeItemCursorCatalog? cursorCatalog = null)
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
                    SpellId = ParseLegacySpellId(OptionalText(row, "spell"), spellCatalog),
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
                    Name = OptionalText(row, "name"),
                    IdentifiedName = OptionalText(row, "shortName"),
                    CursorGraphic = LegacyCursorSymbol(row),
                    MinimumMonsterLevel = row.OptionalInt32("minMonsterLevel"),
                    DropRate = row.OptionalInt32("dropRate"),
                    Usable = ParseLegacyUsable(row),
                    NativeCursorGraphic = ParseLegacyCursorGraphic(row, cursorCatalog),
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

    private static string OptionalPresentationText(TsvRow row, string column)
    {
        var value = OptionalText(row, column);
        return value == "-" ? string.Empty : value;
    }

    private static bool ParseLegacyUsable(TsvRow row)
    {
        return row.OptionalBoolean("usable");
    }

    private static int? ParseOptionalCursorGraphic(
        TsvRow row,
        AuthoritativeItemCursorCatalog? cursorCatalog)
    {
        var resolved = default(int?);
        if (row.TryGet("cursor_graphic", out var symbol)
            && !string.IsNullOrWhiteSpace(symbol)
            && !symbol.Equals("-", StringComparison.Ordinal)) {
            // Direct table consumers may not have loaded the optional
            // compatibility catalog yet; activation resolves the symbol when
            // the catalog is present and otherwise retains legacy fallback.
            if (cursorCatalog is not null)
                resolved = cursorCatalog.Resolve(symbol);
        }
        if (row.TryGet("native_cursor_graphic", out var value)
            && !string.IsNullOrWhiteSpace(value)) {
            var numeric = row.OptionalInt32("native_cursor_graphic");
            if (resolved is not null && resolved.Value != numeric) {
                throw new InvalidDataException(
                    $"TSV row {row.LineNumber} contains conflicting cursor graphic values.");
            }
            resolved = numeric;
        }
        return resolved;
    }

    private static string LegacyCursorSymbol(TsvRow row)
    {
        if (!row.TryGet("cursorGraphic", out var value)
            || string.IsNullOrWhiteSpace(value)
            || value.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return value;
    }

    private static int? ParseLegacyCursorGraphic(
        TsvRow row,
        AuthoritativeItemCursorCatalog? cursorCatalog)
    {
        var symbol = LegacyCursorSymbol(row);
        return string.IsNullOrEmpty(symbol) || cursorCatalog is null
            ? null
            : cursorCatalog.Resolve(symbol);
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
            "SCROLL" => 21,
            "SCROLLT" => 22,
            "RUNEF" => 47,
            "RUNEL" => 48,
            "GR_RUNEL" => 49,
            "GR_RUNEF" => 50,
            "RUNES" => 51,
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

    private static int ParseLegacySpellId(
        string value,
        AuthoritativeSpellCatalog? spellCatalog = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Null", StringComparison.OrdinalIgnoreCase))
            return 0;
        var nativeSpellNames = new[] {
            "Null", "Firebolt", "Healing", "Lightning", "Flash", "Identify", "FireWall", "TownPortal",
            "StoneCurse", "Infravision", "Phasing", "ManaShield", "Fireball", "Guardian", "ChainLightning",
            "FlameWave", "DoomSerpents", "BloodRitual", "Nova", "Invisibility", "Inferno", "Golem", "Rage",
            "Teleport", "Apocalypse", "Etherealize", "ItemRepair", "StaffRecharge", "TrapDisarm", "Elemental",
            "ChargedBolt", "HolyBolt", "Resurrect", "Telekinesis", "HealOther", "BloodStar", "BoneSpirit",
            "Mana", "Magi", "Jester", "LightningWall", "Immolation", "Warp", "Reflect", "Berserk", "RingOfFire",
            "Search", "RuneOfFire", "RuneOfLight", "RuneOfNova", "RuneOfImmolation", "RuneOfStone",
        };
        var nativeSpellId = Array.FindIndex(
            nativeSpellNames,
            spell => spell.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (nativeSpellId < 0)
            return 0;
        if (spellCatalog is not null
            && spellCatalog.TryGetByNativeId(checked((uint)nativeSpellId), out var definition)) {
            return checked((int)definition.SpellId);
        }
        if (spellCatalog is not null) {
            throw new InvalidDataException(
                $"Legacy spell '{value}' (native spell ID {nativeSpellId}) is not mapped by the active spell catalog.");
        }
        return nativeSpellId;
    }

    private static uint ParseNormalizedFlags(TsvRow row)
    {
        var flags = row.OptionalUInt32("flags");
        if (!row.TryGet("special_effects", out var value)
            || string.IsNullOrWhiteSpace(value)) {
            return flags;
        }
        return flags | ParseSpecialEffects(
            value,
            rejectUnknown: true,
            lineNumber: row.LineNumber);
    }

    private static int ParseNormalizedHellfireEffects(TsvRow row)
    {
        if (!row.TryGet("hellfire_effects", out var value)
            || string.IsNullOrWhiteSpace(value)) {
            return 0;
        }

        var flags = 0;
        foreach (var effect in value.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var flag = effect.ToUpperInvariant() switch {
                "DEVASTATION" => AuthoritativeEquipmentIncomingDamageRules.DevastationFlag,
                "DECAY" => AuthoritativeEquipmentIncomingDamageRules.DecayFlag,
                "PERIL" => AuthoritativeEquipmentIncomingDamageRules.PerilFlag,
                "JESTERS" => AuthoritativeEquipmentIncomingDamageRules.JestersFlag,
                "DOPPELGANGER" => AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag,
                "ACAGAINSTDEMONS" or "ACDEMON" => AuthoritativeEquipmentIncomingDamageRules.AgainstDemonsFlag,
                "ACAGAINSTUNDEAD" or "ACUNDEAD" => AuthoritativeEquipmentIncomingDamageRules.AgainstUndeadFlag,
                _ => 0,
            };
            if (flag == 0) {
                throw new InvalidDataException(
                    $"TSV row {row.LineNumber} contains unknown Hellfire item effect '{effect}'.");
            }
            flags |= flag;
        }
        return flags;
    }

    private static uint ParseLegacyFlags(string value)
    {
        return ParseSpecialEffects(value, rejectUnknown: false, lineNumber: null);
    }

    private static uint ParseSpecialEffects(
        string value,
        bool rejectUnknown,
        int? lineNumber)
    {
        var flags = 0U;
        foreach (var effect in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            uint? flag = effect.ToUpperInvariant() switch {
                "RANDOMSTEALLIFE" => 0x00000002U,
                "RANDOMARROWVELOCITY" => AuthoritativeEquipmentAttackEffectRules.RandomArrowVelocityFlag,
                "FIREARROWS" => AuthoritativeEquipmentElementalDamageRules.FireArrowsFlag,
                "FIREDAMAGE" => AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
                "LIGHTNINGDAMAGE" => AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                "DRAINLIFE" => AuthoritativeEquipmentIncomingDamageRules.DrainLifeFlag,
                "LIGHTNINGARROWS" => AuthoritativeEquipmentElementalDamageRules.LightningArrowsFlag,
                "MULTIPLE ARROWS" or "MULTIPLEARROWS" => AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag,
                "KNOCKBACK" => 0x00000800U,
                "THORNS" => AuthoritativeEquipmentIncomingDamageRules.ThornsFlag,
                "NOMANA" => 0x08000000U,
                "HALFTRAPDAMAGE" => AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag,
                "TRIPLEDEMONDAMAGE" => 0x40000000U,
                "ZERORESISTANCE" => 0x80000000U,
                _ => null,
            };
            if (flag is null) {
                if (rejectUnknown) {
                    throw new InvalidDataException(
                        $"TSV row {lineNumber} contains unknown item special effect '{effect}'.");
                }
                continue;
            }
            flags |= flag.Value;
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
