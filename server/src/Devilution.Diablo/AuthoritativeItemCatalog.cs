using System.Text;
using Devilution.Data;
using Devilution.Engine;

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

    /** Content-owned magnitude for the item action, defaulting to one native unit. */
    public int ItemActionAmount { get; init; } = 1;

    /** Optional content-owned dialogue identity emitted by a non-consuming item action. */
    public int ItemActionMessageId { get; init; }

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

    /** Optional native item-level threshold for spell-book templates; -1 retains the legacy fallback. */
    public int BookTemplateMinimumItemLevel { get; init; } = -1;

    /** Native weighted drop rate; zero excludes the item from random selection. */
    public int DropRate { get; init; }

    /** Optional native iUsable flag for cursor-consumable item actions. */
    public bool? Usable { get; init; }

    /** Optional content-owned arena-only admission; null preserves native fallback. */
    public bool? ArenaOnly { get; init; }

    public AuthoritativeItemState Generate(uint itemSeed)
    {
        return Generate(itemSeed, new DeterministicLegacyRandom(itemSeed));
    }

    public AuthoritativeItemState Generate(uint itemSeed, DeterministicLegacyRandom random)
    {
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        ArgumentNullException.ThrowIfNull(random);
        return AuthoritativeItemState.Empty with {
            // Native creation flags are uint16_t. Small legacy fixtures retain
            // their historical value; large authoritative seeds use the
            // canonical no-flag value while the full seed remains item identity.
            CreateInfo = itemSeed <= ushort.MaxValue ? itemSeed : 0,
            ItemType = ItemType,
            EquipLocation = EquipLocation,
            ItemClass = ItemClass,
            MiscId = MiscId,
            SpellId = SpellId,
            SpellSourceKind = SpellSourceKind,
            ItemActionKind = ItemActionKind,
            StableItemId = ItemId,
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

    /** Preserves the pre-engine overload for existing Diablo callers. */
    public AuthoritativeItemState Generate(uint itemSeed, AuthoritativeLegacyRandom random)
        => Generate(itemSeed, (DeterministicLegacyRandom)random);
}

/** Complete native Gold-stack identity enabled only on an explicit population overlay. */
internal sealed record AuthoritativeNativeGoldStackMetadata(
    int NativeItemIndex,
    uint StableItemId,
    int NativeItemType,
    int EquipLocation,
    int ItemClass,
    uint Flags,
    int MiscId,
    int SpellId,
    string BaseName,
    string IdentifiedName,
    int InventoryWidth,
    int InventoryHeight,
    int GoldSmallCursor,
    int GoldMediumCursor,
    int GoldLargeCursor)
{
    public AuthoritativeItemState CompleteNewStack(
        AuthoritativeItemState state,
        uint value,
        AuthoritativeCombatRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        if (value == 0 || value > rules.DeathGoldMaximumStack
            || rules.DeathGoldItemIndex != NativeItemIndex
            || rules.DeathGoldItemType != NativeItemType
            || state.StableItemId != StableItemId
            || state.ItemIndex != NativeItemIndex
            || state.ItemType != NativeItemType)
            throw new InvalidDataException("Native Gold state does not match its admitted item binding.");
        return state with {
            EquipLocation = EquipLocation,
            ItemClass = ItemClass,
            Flags = Flags,
            Value = checked((int)value),
            IdentifiedValue = checked((int)value),
            MiscId = MiscId,
            SpellId = SpellId,
            InventoryWidth = InventoryWidth,
            InventoryHeight = InventoryHeight,
            BaseName = BaseName,
            IdentifiedName = IdentifiedName,
            NativeCursorGraphic = CursorFor(value),
        };
    }

    public void ValidateExistingStack(
        AuthoritativeItemState state,
        uint price,
        AuthoritativeCombatRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.DeathGoldItemIndex != NativeItemIndex
            || rules.DeathGoldItemType != NativeItemType
            || state.StableItemId != StableItemId
            || state.ItemIndex != NativeItemIndex
            || state.ItemType != NativeItemType
            || state.EquipLocation != EquipLocation
            || state.ItemClass != ItemClass
            || state.Flags != Flags
            || state.MiscId != MiscId
            || state.SpellId != SpellId
            || state.Value <= 0
            || state.Value > rules.DeathGoldMaximumStack
            || state.IdentifiedValue != state.Value
            || price != checked((uint)state.Value)
            || !state.StatFlag
            || state.InventoryWidth != InventoryWidth
            || state.InventoryHeight != InventoryHeight
            || !string.Equals(state.BaseName, BaseName, StringComparison.Ordinal)
            || !string.Equals(state.IdentifiedName, IdentifiedName, StringComparison.Ordinal)
            || state.NativeCursorGraphic != CursorFor(checked((uint)state.Value)))
            throw new InvalidDataException("Native Gold stack metadata does not match its admitted representation.");
    }

    public int CursorFor(uint value)
        => value >= 2500 ? GoldLargeCursor : value <= 1000 ? GoldSmallCursor : GoldMediumCursor;
}

/** Validated external item definitions keyed by stable numeric content ID. */
public sealed class AuthoritativeItemCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "item_id",
        "item_symbol",
        "item_type",
        "value",
        "identified_value",
        "min_damage",
        "max_damage",
        "armor_class",
        "durability",
        "max_durability",
        "inventory_width",
        "inventory_height",
        "identified",
        "item_tags",
        "equip_location",
        "item_class",
        "misc_id",
        "spell_id",
        "spell_symbol",
        "flags",
        "min_strength",
        "min_magic",
        "min_dexterity",
        "native_item_index",
        "charges",
        "max_charges",
        "spell_source_kind",
        "name",
        "identified_name",
        "cursor_graphic",
        "min_monster_level",
        "drop_rate",
        "usable",
        "special_effects",
        "hellfire_effects",
        "min_armor_class",
        "max_armor_class",
        "item_action_kind",
        "item_action_amount",
        "book_template_minimum_item_level",
        "arena_only",
        "item_action_message_id",
    ];

    private static readonly IReadOnlyList<string> ProductionRequiredColumns = [
        "item_id",
        "item_symbol",
        "item_type",
        "value",
        "identified_value",
        "native_item_index",
        "spell_source_kind",
        "name",
        "identified_name",
        "cursor_graphic",
        "item_action_kind",
        "item_action_amount",
    ];

    private readonly Dictionary<uint, AuthoritativeItemDefinition> definitions;
    private readonly AuthoritativeItemAffixCatalog? affixes;
    private AuthoritativeUniqueItemCatalog? uniqueItems;
    private readonly Dictionary<string, uint> legacySymbols = new(StringComparer.OrdinalIgnoreCase);

    /** Non-null only on the explicit, validated native population overlay. */
    internal AuthoritativeNativeGoldStackMetadata? NativeGoldStackMetadata { get; private set; }

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
                || definition.ItemActionAmount <= 0
                || definition.ItemActionMessageId is < 0 or > short.MaxValue
                || definition.ItemActionMessageId != 0
                    && definition.ItemActionKind == DiabloItemActionKind.None
                || (definition.SpellSourceKind == DiabloItemSpellSourceKind.ChargedItem
                    && (definition.SpellId <= 0 || definition.MaxCharges <= 0))
                || (definition.SpellSourceKind == DiabloItemSpellSourceKind.ConsumableItem
                    && definition.SpellId <= 0)
                || definition.SpellId < 0
                || !IsValidPresentationText(definition.Name)
                || !IsValidPresentationText(definition.IdentifiedName)
                || !IsValidCursorSymbol(definition.CursorGraphic)
                || definition.MinimumMonsterLevel < 0
                || definition.BookTemplateMinimumItemLevel is < -1 or 0 or > 255
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

    /** Rejects partial normalized item rows at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production item table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows)
            foreach (var column in ProductionRequiredColumns)
                _ = row.Required(column);
    }

    public bool TryGet(uint itemId, out AuthoritativeItemDefinition definition) => definitions.TryGetValue(itemId, out definition!);

    /** Resolves an item state through stable content identity before native compatibility identity. */
    public bool TryGetByState(
        AuthoritativeItemState state,
        out AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.StableItemId != 0 && TryGet(state.StableItemId, out definition!))
            return true;
        return TryGetByNativeItemIndex(state.ItemIndex, out definition!);
    }

    /** Matches one item state to a definition through stable identity before native fallback. */
    public bool IsStateForDefinition(
        AuthoritativeItemState state,
        AuthoritativeItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        return state.StableItemId != 0
            ? state.StableItemId == definition.ItemId
            : state.ItemIndex == definition.NativeItemIndex;
    }

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

    /** Resolves presentation metadata only when every matching misc-ID row agrees. */
    private bool TryGetPresentationDefinitionByMiscId(
        int miscId,
        out AuthoritativeItemDefinition definition)
    {
        definition = null!;
        if (miscId <= 0)
            return false;

        var matches = definitions.Values
            .Where(item => item.MiscId == miscId)
            .ToArray();
        if (matches.Length == 0)
            return false;

        var first = matches[0];
        if (matches.Any(match => !string.Equals(match.Name, first.Name, StringComparison.Ordinal)
                || !string.Equals(match.IdentifiedName, first.IdentifiedName, StringComparison.Ordinal)
                || match.NativeCursorGraphic != first.NativeCursorGraphic))
            return false;

        definition = first;
        return true;
    }

    /** Resolves an unambiguous content-owned spell-source kind by misc ID. */
    public bool TryGetSpellSourceKindByMiscId(
        int miscId,
        out DiabloItemSpellSourceKind sourceKind)
    {
        sourceKind = DiabloItemSpellSourceKind.None;
        if (miscId <= 0)
            return false;

        var kinds = definitions.Values
            .Where(definition => definition.MiscId == miscId
                && definition.SpellSourceKind is { } kind)
            .Select(definition => definition.SpellSourceKind!.Value)
            .Distinct()
            .ToArray();
        if (kinds.Length != 1)
            return false;

        sourceKind = kinds[0];
        return true;
    }

    /** Resolves unambiguous charged-item capacity and spell metadata by misc ID. */
    public bool TryGetChargedItemMetadataByMiscId(
        int miscId,
        out int maximumCharges,
        out int spellId)
    {
        maximumCharges = 0;
        spellId = 0;
        if (miscId <= 0)
            return false;

        var matches = definitions.Values
            .Where(definition => definition.MiscId == miscId
                && definition.SpellSourceKind == DiabloItemSpellSourceKind.ChargedItem)
            .ToArray();
        if (matches.Length == 0)
            return false;

        var chargeValues = matches.Select(definition => definition.MaxCharges).Distinct().ToArray();
        var spellValues = matches.Select(definition => definition.SpellId).Distinct().ToArray();
        if (chargeValues.Length != 1 || spellValues.Length != 1
            || chargeValues[0] <= 0 || spellValues[0] <= 0)
            return false;

        maximumCharges = chargeValues[0];
        spellId = spellValues[0];
        return true;
    }

    /** Resolves an unambiguous consumable-item spell identity by misc ID. */
    public bool TryGetConsumableItemSpellIdByMiscId(int miscId, out int spellId)
    {
        spellId = 0;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId
                && definition.SpellSourceKind == DiabloItemSpellSourceKind.ConsumableItem)
            .Select(definition => definition.SpellId)
            .Distinct()
            .ToArray();
        if (values.Length != 1 || values[0] <= 0)
            return false;

        spellId = values[0];
        return true;
    }

    /** Resolves an unambiguous authored usability flag by misc ID. */
    public bool TryGetUsableByMiscId(int miscId, out bool usable)
    {
        usable = true;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId
                && definition.Usable.HasValue)
            .Select(definition => definition.Usable!.Value)
            .Distinct()
            .ToArray();
        if (values.Length != 1)
            return false;

        usable = values[0];
        return true;
    }

    /** Resolves unambiguous authored durability metadata by misc ID. */
    public bool TryGetDurabilityMetadataByMiscId(
        int miscId,
        out int durability,
        out int maximumDurability)
    {
        durability = 0;
        maximumDurability = 0;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId)
            .Select(definition => (definition.Durability, definition.MaxDurability))
            .Distinct()
            .ToArray();
        if (values.Length != 1 || values[0].MaxDurability <= 0)
            return false;

        durability = values[0].Durability;
        maximumDurability = values[0].MaxDurability;
        return true;
    }

    /** Resolves unambiguous authored base damage metadata by misc ID. */
    public bool TryGetBaseDamageByMiscId(
        int miscId,
        out int minimumDamage,
        out int maximumDamage)
    {
        minimumDamage = 0;
        maximumDamage = 0;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId)
            .Select(definition => (definition.MinDamage, definition.MaxDamage))
            .Distinct()
            .ToArray();
        if (values.Length != 1 || values[0].MaxDamage <= 0)
            return false;

        minimumDamage = values[0].MinDamage;
        maximumDamage = values[0].MaxDamage;
        return true;
    }

    /** Resolves unambiguous authored service-value metadata by misc ID. */
    public bool TryGetValueMetadataByMiscId(
        int miscId,
        out int value,
        out int identifiedValue)
    {
        value = 0;
        identifiedValue = 0;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId)
            .Select(definition => (definition.Value, definition.IdentifiedValue))
            .Distinct()
            .ToArray();
        if (values.Length != 1
            || values[0].Value <= 0 && values[0].IdentifiedValue <= 0)
            return false;

        value = values[0].Value;
        identifiedValue = values[0].IdentifiedValue;
        return true;
    }

    /** Resolves unambiguous fixed authored armor metadata by misc ID. */
    public bool TryGetFixedArmorClassByMiscId(int miscId, out int armorClass)
    {
        armorClass = 0;
        if (miscId <= 0)
            return false;

        var matches = definitions.Values
            .Where(definition => definition.MiscId == miscId)
            .ToArray();
        if (matches.Length == 0
            || matches.Any(definition => definition.MinArmorClass >= 0
                && definition.MaxArmorClass > definition.MinArmorClass))
            return false;

        var values = matches
            .Select(definition => definition.MinArmorClass >= 0
                ? definition.MinArmorClass
                : definition.ArmorClass)
            .Distinct()
            .ToArray();
        if (values.Length != 1 || values[0] <= 0)
            return false;

        armorClass = values[0];
        return true;
    }

    /** Resolves an unambiguous authored armor range by misc ID. */
    public bool TryGetArmorClassRangeByMiscId(
        int miscId,
        out int minimumArmorClass,
        out int maximumArmorClass)
    {
        minimumArmorClass = 0;
        maximumArmorClass = 0;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId)
            .Select(definition => (
                Minimum: definition.MinArmorClass >= 0
                    ? definition.MinArmorClass
                    : definition.ArmorClass,
                Maximum: definition.MaxArmorClass >= 0
                    ? definition.MaxArmorClass
                    : definition.ArmorClass))
            .Distinct()
            .ToArray();
        if (values.Length != 1
            || values[0].Minimum < 0
            || values[0].Maximum < values[0].Minimum)
            return false;

        minimumArmorClass = values[0].Minimum;
        maximumArmorClass = values[0].Maximum;
        return true;
    }

    /** Resolves unambiguous authored spell-book metadata by misc ID. */
    public bool TryGetBookMetadataByMiscId(
        int miscId,
        out int spellId,
        out int minimumMagic)
    {
        spellId = 0;
        minimumMagic = 0;
        if (miscId <= 0)
            return false;

        var matches = definitions.Values
            .Where(definition => definition.MiscId == miscId
                && definition.SpellId > 0
                && (definition.ItemActionKind == DiabloItemActionKind.ReadBook
                    || definition.GenerationTags.Contains(
                        "Book",
                        StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        var values = matches
            .Select(definition => (definition.SpellId, definition.MinimumMagic))
            .Distinct()
            .ToArray();
        if (values.Length != 1)
            return false;

        spellId = values[0].SpellId;
        minimumMagic = values[0].MinimumMagic;
        return true;
    }

    /** Resolves unambiguous authored inventory dimensions by misc ID. */
    public bool TryGetInventoryDimensionsByMiscId(
        int miscId,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId)
            .Select(definition => (definition.InventoryWidth, definition.InventoryHeight))
            .Distinct()
            .ToArray();
        if (values.Length != 1)
            return false;

        width = values[0].InventoryWidth;
        height = values[0].InventoryHeight;
        return true;
    }

    /** Resolves omitted/default inventory dimensions through authored identity. */
    public AuthoritativeItemState ResolveInventoryDimensions(AuthoritativeItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var width = 0;
        var height = 0;
        if (TryGetByState(state, out var definition)) {
            width = definition.InventoryWidth;
            height = definition.InventoryHeight;
        } else if (TryGetInventoryDimensionsByMiscId(
                state.MiscId,
                out var contentWidth,
                out var contentHeight)) {
            width = contentWidth;
            height = contentHeight;
        }
        if (width <= 0 || height <= 0)
            return state;

        if (state.InventoryWidth > 0
            && state.InventoryHeight > 0
            && (state.InventoryWidth != 1
                || state.InventoryHeight != 1
                || (width <= 1 && height <= 1)))
            return state;

        if (state.InventoryWidth == 1
            && state.InventoryHeight == 1
            && (width > 1 || height > 1))
            return state with { InventoryWidth = width, InventoryHeight = height };

        return state with {
            InventoryWidth = state.InventoryWidth > 0 ? state.InventoryWidth : width,
            InventoryHeight = state.InventoryHeight > 0 ? state.InventoryHeight : height,
        };
    }

    /** Resolves omitted persisted gameplay metadata without changing item identity. */
    public AuthoritativeItemState ResolveAuthoredMetadata(AuthoritativeItemState state)
        => ResolveAuthoredMetadata(state, null);

    /**
     * Resolves omitted persisted gameplay metadata, using the stable item seed
     * to restore a native inclusive armor roll when the state omitted it.
     */
    public AuthoritativeItemState ResolveAuthoredMetadata(
        AuthoritativeItemState state,
        uint itemSeed)
    {
        if (itemSeed == 0)
            return ResolveAuthoredMetadata(state, null);
        return ResolveAuthoredMetadata(state, (uint?)itemSeed);
    }

    private AuthoritativeItemState ResolveAuthoredMetadata(
        AuthoritativeItemState state,
        uint? itemSeed)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = ResolveInventoryDimensions(state);
        var hasIdentityDefinition = TryGetByState(state, out var definition);
        var hasPresentationDefinition = hasIdentityDefinition
            || TryGetPresentationDefinitionByMiscId(result.MiscId, out definition);
        if (hasPresentationDefinition) {
            if (result.UniqueId == 0) {
                if (string.IsNullOrEmpty(result.BaseName) && !string.IsNullOrEmpty(definition.Name))
                    result = result with { BaseName = definition.Name };
                if (string.IsNullOrEmpty(result.IdentifiedName))
                    result = result with {
                        IdentifiedName = string.IsNullOrEmpty(definition.IdentifiedName)
                            ? definition.Name
                            : definition.IdentifiedName,
                    };
            }
            if (!result.NativeCursorGraphic.HasValue && definition.NativeCursorGraphic is { } cursorGraphic)
                result = result with { NativeCursorGraphic = cursorGraphic };
        }
        if (hasIdentityDefinition) {
            if (result.ItemType < 0)
                result = result with { ItemType = definition.ItemType };
            if (result.ItemIndex < 0 && definition.NativeItemIndex >= 0)
                result = result with { ItemIndex = definition.NativeItemIndex };
            if (result.EquipLocation == 0 && definition.EquipLocation != 0)
                result = result with { EquipLocation = definition.EquipLocation };
            if (result.ItemClass == 0 && definition.ItemClass != 0)
                result = result with { ItemClass = definition.ItemClass };
            if (result.MinimumStrength == 0 && definition.MinimumStrength > 0)
                result = result with { MinimumStrength = definition.MinimumStrength };
            if (result.MinimumMagic == 0 && definition.MinimumMagic > 0)
                result = result with { MinimumMagic = definition.MinimumMagic };
            if (result.MinimumDexterity == 0 && definition.MinimumDexterity > 0)
                result = result with { MinimumDexterity = definition.MinimumDexterity };
            if (result.Flags == 0 && definition.Flags != 0)
                result = result with { Flags = definition.Flags };
            if (result.HellfireDamageArmorFlags == 0 && definition.HellfireDamageArmorFlags != 0)
                result = result with { HellfireDamageArmorFlags = definition.HellfireDamageArmorFlags };
            if (result.Value == 0)
                result = result with { Value = definition.Value };
            if (result.IdentifiedValue == 0)
                result = result with { IdentifiedValue = definition.IdentifiedValue };
            if (result.MaxDamage == 0 && definition.MaxDamage > 0)
                result = result with {
                    MinDamage = definition.MinDamage,
                    MaxDamage = definition.MaxDamage,
                };
            if (result.ArmorClass == 0
                && definition.MinArmorClass >= 0) {
                var armorClass = ResolveAuthoredArmorClass(definition, itemSeed);
                if (armorClass > 0)
                    result = result with { ArmorClass = armorClass };
            }
            if (result.MaxDurability == 0 && definition.MaxDurability > 0)
                result = result with {
                    Durability = result.Durability == 0
                        ? definition.Durability
                        : result.Durability,
                    MaxDurability = definition.MaxDurability,
                };
            if (result.MaxCharges == 0 && definition.MaxCharges > 0)
                result = result with { MaxCharges = definition.MaxCharges };
            if (result.SpellId == 0 && definition.SpellId > 0)
                result = result with { SpellId = definition.SpellId };
            if (result.SpellSourceKind is null && definition.SpellSourceKind is { } sourceKind)
                result = result with { SpellSourceKind = sourceKind };
            if (result.ItemActionKind == DiabloItemActionKind.None
                && definition.ItemActionKind != DiabloItemActionKind.None)
                result = result with { ItemActionKind = definition.ItemActionKind };
            return result;
        }

        if (result.Value == 0 || result.IdentifiedValue == 0) {
            if (TryGetValueMetadataByMiscId(
                    result.MiscId,
                    out var value,
                    out var identifiedValue)) {
                if (result.Value == 0)
                    result = result with { Value = value };
                if (result.IdentifiedValue == 0)
                    result = result with { IdentifiedValue = identifiedValue };
            }
        }
        if (result.MaxDamage == 0
            && TryGetBaseDamageByMiscId(
                result.MiscId,
                out var minimumDamage,
                out var maximumDamage))
            result = result with {
                MinDamage = minimumDamage,
                MaxDamage = maximumDamage,
            };
        if (result.ArmorClass == 0
            && TryGetArmorClassRangeByMiscId(
                result.MiscId,
                out var minimumArmorClass,
                out var maximumArmorClass)) {
            var armorClass = ResolveAuthoredArmorClass(
                minimumArmorClass,
                maximumArmorClass,
                itemSeed);
            if (armorClass > 0)
                result = result with { ArmorClass = armorClass };
        }
        if (result.MaxDurability == 0
            && TryGetDurabilityMetadataByMiscId(
                result.MiscId,
                out var durability,
                out var maximumDurability))
            result = result with {
                Durability = result.Durability == 0 ? durability : result.Durability,
                MaxDurability = maximumDurability,
            };
        if (result.MaxCharges == 0
            && TryGetChargedItemMetadataByMiscId(
                result.MiscId,
                out var maximumCharges,
                out _))
            result = result with { MaxCharges = maximumCharges };
        if (result.SpellId == 0
            && TryGetChargedItemMetadataByMiscId(
                result.MiscId,
                out _,
                out var chargedSpellId))
            result = result with { SpellId = chargedSpellId };
        if (result.SpellId == 0
            && TryGetConsumableItemSpellIdByMiscId(result.MiscId, out var consumableSpellId))
            result = result with { SpellId = consumableSpellId };
        if (result.SpellSourceKind is null
            && TryGetSpellSourceKindByMiscId(result.MiscId, out var miscSourceKind))
            result = result with { SpellSourceKind = miscSourceKind };
        if (result.ItemActionKind == DiabloItemActionKind.None
            && TryGetItemActionMetadataByMiscId(
                result.MiscId,
                out var actionKind,
                out _))
            result = result with { ItemActionKind = actionKind };
        return result;
    }

    private static int ResolveAuthoredArmorClass(
        AuthoritativeItemDefinition definition,
        uint? itemSeed)
        => ResolveAuthoredArmorClass(
            definition.MinArmorClass,
            definition.MaxArmorClass,
            itemSeed,
            definition.ArmorClass);

    private static int ResolveAuthoredArmorClass(
        int minimumArmorClass,
        int maximumArmorClass,
        uint? itemSeed,
        int fixedArmorClass = 0)
    {
        if (minimumArmorClass < 0 || maximumArmorClass < minimumArmorClass)
            return fixedArmorClass;
        if (minimumArmorClass == maximumArmorClass)
            return minimumArmorClass;
        if (itemSeed is null)
            return fixedArmorClass;
        return checked(
            minimumArmorClass
            + new DeterministicLegacyRandom(itemSeed.Value)
                .Next(checked(maximumArmorClass - minimumArmorClass + 1)));
    }

    /** Resolves unambiguous consumable action metadata by misc ID. */
    public bool TryGetItemActionMetadataByMiscId(
        int miscId,
        out DiabloItemActionKind actionKind,
        out int actionAmount)
    {
        actionKind = DiabloItemActionKind.None;
        actionAmount = 0;
        if (miscId <= 0)
            return false;

        var matches = definitions.Values
            .Where(definition => definition.MiscId == miscId
                && definition.ItemActionKind != DiabloItemActionKind.None)
            .ToArray();
        if (matches.Length == 0)
            return false;

        var actionValues = matches.Select(definition => definition.ItemActionKind).Distinct().ToArray();
        var amountValues = matches.Select(definition => definition.ItemActionAmount).Distinct().ToArray();
        if (actionValues.Length != 1 || amountValues.Length != 1 || amountValues[0] <= 0)
            return false;

        actionKind = actionValues[0];
        actionAmount = amountValues[0];
        return true;
    }

    /** Resolves an authored arena-only admission flag by misc ID. */
    public bool TryGetArenaOnlyByMiscId(int miscId, out bool arenaOnly)
    {
        arenaOnly = false;
        if (miscId <= 0)
            return false;

        var values = definitions.Values
            .Where(definition => definition.MiscId == miscId
                && (definition.ArenaOnly.HasValue
                    || string.Equals(
                        definition.StableSymbol,
                        "arena_potion",
                        StringComparison.OrdinalIgnoreCase)))
            .Select(definition => definition.ArenaOnly
                ?? string.Equals(
                    definition.StableSymbol,
                    "arena_potion",
                    StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
        if (values.Length != 1)
            return false;

        arenaOnly = values[0];
        return true;
    }

    /** Resolves non-empty, unambiguous semantic item tags by misc ID. */
    public bool TryGetGenerationTagsByMiscId(
        int miscId,
        out IReadOnlySet<string> generationTags)
    {
        generationTags = null!;
        if (miscId <= 0)
            return false;

        var matches = definitions.Values
            .Where(definition => definition.MiscId == miscId)
            .ToArray();
        if (matches.Length == 0 || matches[0].GenerationTags.Count == 0)
            return false;

        var firstTags = matches[0].GenerationTags;
        if (matches.Any(match => !firstTags.SetEquals(match.GenerationTags)))
            return false;

        generationTags = firstTags;
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

    /** Creates an opt-in native-profile view while retaining the source catalog unchanged. */
    internal AuthoritativeItemCatalog WithNativePopulationOverlay(
        IReadOnlyDictionary<uint, AuthoritativeItemDefinition> replacements,
        AuthoritativeNativeGoldStackMetadata goldStackMetadata)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        ArgumentNullException.ThrowIfNull(goldStackMetadata);
        if (replacements.Keys.Any(itemId => !definitions.ContainsKey(itemId)))
            throw new InvalidDataException("Native item overlay references an item outside the canonical catalog.");
        var result = new AuthoritativeItemCatalog(
            definitions.Values.Select(definition => replacements.TryGetValue(definition.ItemId, out var replacement)
                ? replacement
                : definition),
            affixes,
            uniqueItems);
        foreach (var symbol in legacySymbols)
            result.legacySymbols.Add(symbol.Key, symbol.Value);
        if (!result.definitions.TryGetValue(goldStackMetadata.StableItemId, out var goldDefinition)
            || goldDefinition.NativeItemIndex != goldStackMetadata.NativeItemIndex
            || goldDefinition.ItemType != goldStackMetadata.NativeItemType
            || goldDefinition.EquipLocation != goldStackMetadata.EquipLocation
            || goldDefinition.ItemClass != goldStackMetadata.ItemClass
            || goldDefinition.Flags != goldStackMetadata.Flags
            || goldDefinition.MiscId != goldStackMetadata.MiscId
            || goldDefinition.SpellId != goldStackMetadata.SpellId
            || goldDefinition.InventoryWidth != goldStackMetadata.InventoryWidth
            || goldDefinition.InventoryHeight != goldStackMetadata.InventoryHeight
            || !string.Equals(goldDefinition.Name, goldStackMetadata.BaseName, StringComparison.Ordinal)
            || !string.Equals(
                string.IsNullOrWhiteSpace(goldDefinition.IdentifiedName)
                    ? goldDefinition.Name
                    : goldDefinition.IdentifiedName,
                goldStackMetadata.IdentifiedName,
                StringComparison.Ordinal)
            || !AuthoritativeItemClassification.IsGold(goldDefinition))
            throw new InvalidDataException("Native Gold metadata does not match the selected overlay item definition.");
        result.NativeGoldStackMetadata = goldStackMetadata;
        return result;
    }

    /** Resolves a unique reward through the catalog attached during content activation. */
    public bool TryGetUnique(uint uniqueId, out AuthoritativeUniqueItemDefinition definition)
    {
        if (uniqueItems is null) {
            definition = null!;
            return false;
        }
        return uniqueItems.TryGet(uniqueId, out definition!);
    }

    public AuthoritativeItemState Generate(uint itemId, uint itemSeed)
        => Generate(itemId, itemSeed, 0);

    public AuthoritativeItemState Generate(uint itemId, uint itemSeed, int itemLevel)
    {
        if (!TryGet(itemId, out var definition))
            throw new InvalidDataException($"Item {itemId} is not present in the authoritative catalog.");
        var useLegacyRandom = affixes?.UsesLegacyRandom == true;
        var random = useLegacyRandom ? new DeterministicLegacyRandom(itemSeed) : null;
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

        var random = new DeterministicLegacyRandom(itemSeed);
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

    /**
     * Generates the native unique-monster random-drop family from one stable
     * seed. Books are eligible at every level; other non-gold items honor
     * their minimum monster level and zero-drop-rate rows are excluded.
     */
    public AuthoritativeItemState GenerateRandomUniqueMonsterDrop(
        uint itemSeed,
        int monsterLevel,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer)
    {
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        if (monsterLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(monsterLevel));

        const int goldItemType = 11;
        const int bookMiscId = 25;
        var candidates = Definitions
            .Where(definition => {
                var isBookTemplate = spellBooks is not null
                    ? spellBooks.IsBookTemplate(this, definition.ItemId)
                    : definition.GenerationTags.Count > 0
                        ? definition.GenerationTags.Contains(
                            "Book",
                            StringComparer.OrdinalIgnoreCase)
                        : definition.MiscId == bookMiscId;
                var isGold = definition.GenerationTags.Count > 0
                    ? AuthoritativeItemClassification.IsGold(definition)
                    : definition.ItemType == goldItemType;
                return definition.DropRate > 0
                    && !isGold
                    && (isBookTemplate
                        || definition.ItemType != 0
                            && definition.MinimumMonsterLevel <= monsterLevel);
            })
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidDataException(
                "The item catalog contains no eligible unique-monster random-drop rows.");

        var random = new DeterministicLegacyRandom(itemSeed);
        var selected = candidates[random.Next(candidates.Length)];
        return GenerateDrop(
            selected.ItemId,
            itemSeed,
            monsterLevel,
            onlyGood: true,
            uniqueChancePercent: affixes?.GenerationRules.UniqueChanceUnique,
            spellBooks,
            gameMode);
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
        StableContentIdCatalog? stableIds = null,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null)
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
                ItemActionAmount = row.OptionalInt32("item_action_amount", 1),
                ItemActionMessageId = row.OptionalInt32("item_action_message_id"),
                Flags = ParseNormalizedFlags(row, nativeItemEffectCatalog),
                HellfireDamageArmorFlags = ParseNormalizedHellfireEffects(
                    row,
                    nativeItemHellfireEffectCatalog),
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
                BookTemplateMinimumItemLevel = row.OptionalInt32("book_template_minimum_item_level", -1),
                DropRate = row.OptionalInt32("drop_rate"),
                Usable = row.OptionalNullableBoolean("usable"),
                ArenaOnly = row.OptionalNullableBoolean("arena_only"),
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
        AuthoritativeItemCursorCatalog? cursorCatalog = null,
        AuthoritativeNativeSpellCatalog? nativeSpellCatalog = null,
        AuthoritativeNativeMiscCatalog? nativeMiscCatalog = null,
        AuthoritativeNativeItemTopologyCatalog? nativeTopologyCatalog = null,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null,
        AuthoritativeNativeItemTagCatalog? nativeItemTagCatalog = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var rows = table.Rows.ToArray();
        var definitions = rows.Select((row, index) => {
            var itemType = ParseLegacyItemType(
                row.Required("itemType"),
                nativeTopologyCatalog);
            var armorMin = row.OptionalInt32("minArmor");
            var armorMax = row.OptionalInt32("maxArmor", armorMin);
            var miscId = ParseLegacyMiscId(
                OptionalText(row, "miscId"),
                nativeMiscCatalog);
            var isStaff = IsLegacyStaffMisc(miscId, nativeMiscCatalog);
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
                    GenerationTags = LegacyTags(
                        row.Required("itemType"),
                        nativeItemTagCatalog),
                    MinArmorClass = armorMin,
                    MaxArmorClass = armorMax,
                    EquipLocation = ParseLegacyEquipLocation(
                        row.Required("equipType"),
                        nativeTopologyCatalog),
                    ItemClass = ParseLegacyItemClass(
                        row.Required("class"),
                        nativeTopologyCatalog),
                    MiscId = miscId,
                    SpellId = ParseLegacySpellId(
                        OptionalText(row, "spell"),
                        spellCatalog,
                        nativeSpellCatalog),
                    Flags = ParseLegacyFlags(
                        OptionalText(row, "specialEffects"),
                        nativeItemEffectCatalog),
                    MinimumStrength = row.OptionalInt32("minStrength"),
                    MinimumMagic = row.OptionalInt32("minMagic"),
                    MinimumDexterity = row.OptionalInt32("minDexterity"),
                    NativeItemIndex = index,
                    Charges = isStaff ? 40 : 0,
                    MaxCharges = isStaff ? 40 : 0,
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

    private static bool IsLegacyStaffMisc(
        int miscId,
        AuthoritativeNativeMiscCatalog? nativeMiscCatalog)
    {
        if (nativeMiscCatalog is not null
            && nativeMiscCatalog.TryGetId("STAFF", out var staffMiscId))
            return miscId == staffMiscId;
        return miscId == 24;
    }

    private static int ParseLegacyItemType(
        string value,
        AuthoritativeNativeItemTopologyCatalog? nativeTopologyCatalog = null)
    {
        if (nativeTopologyCatalog is not null)
            return nativeTopologyCatalog.TryGetId(
                    AuthoritativeNativeItemTopologyKind.ItemType,
                    value,
                    out var nativeId)
                ? nativeId
                : throw new InvalidDataException(
                    $"Legacy item type '{value}' is not defined by the active native item topology catalog.");
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

    private static int ParseLegacyItemClass(
        string value,
        AuthoritativeNativeItemTopologyCatalog? nativeTopologyCatalog = null)
    {
        if (nativeTopologyCatalog is not null)
            return nativeTopologyCatalog.TryGetId(
                    AuthoritativeNativeItemTopologyKind.ItemClass,
                    value,
                    out var nativeId)
                ? nativeId
                : throw new InvalidDataException(
                    $"Legacy item class '{value}' is not defined by the active native item topology catalog.");
        return value.ToLowerInvariant() switch {
            "weapon" => 1,
            "armor" => 2,
            "misc" => 3,
            "gold" => 4,
            "quest" => 5,
            _ => 0,
        };
    }

    private static int ParseLegacyEquipLocation(
        string value,
        AuthoritativeNativeItemTopologyCatalog? nativeTopologyCatalog = null)
    {
        if (nativeTopologyCatalog is not null) {
            var normalized = value.Equals("one-handed", StringComparison.OrdinalIgnoreCase)
                ? "one-handed"
                : value.Equals("two-handed", StringComparison.OrdinalIgnoreCase)
                    ? "two-handed"
                    : value;
            return nativeTopologyCatalog.TryGetId(
                    AuthoritativeNativeItemTopologyKind.EquipLocation,
                    normalized,
                    out var nativeId)
                ? nativeId
                : throw new InvalidDataException(
                    $"Legacy equipment location '{value}' is not defined by the active native item topology catalog.");
        }
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

    private static int ParseLegacyMiscId(
        string value,
        AuthoritativeNativeMiscCatalog? nativeMiscCatalog = null)
    {
        if (nativeMiscCatalog is not null) {
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            return nativeMiscCatalog.TryGetId(value, out var nativeMiscId)
                ? nativeMiscId
                : throw new InvalidDataException(
                    $"Legacy miscellaneous identity '{value}' is not defined by the active native misc catalog.");
        }
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
        AuthoritativeSpellCatalog? spellCatalog = null,
        AuthoritativeNativeSpellCatalog? nativeSpellCatalog = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Null", StringComparison.OrdinalIgnoreCase))
            return 0;
        uint nativeSpellId;
        if (nativeSpellCatalog is not null) {
            if (!nativeSpellCatalog.TryGetId(value, out nativeSpellId))
                throw new InvalidDataException(
                    $"Legacy spell '{value}' is not defined by the active native spell catalog.");
        } else {
            nativeSpellId = ParseLegacyCompatibilitySpellId(value);
            if (nativeSpellId == uint.MaxValue)
                return 0;
        }
        if (spellCatalog is not null
            && spellCatalog.TryGetByNativeId(nativeSpellId, out var definition)) {
            return checked((int)definition.SpellId);
        }
        if (spellCatalog is not null) {
            throw new InvalidDataException(
                $"Legacy spell '{value}' (native spell ID {nativeSpellId}) is not mapped by the active spell catalog.");
        }
        return checked((int)nativeSpellId);
    }

    // Kept only for callers that have not yet supplied the external native
    // namespace. Production content activation always passes its validated
    // native_spells.tsv catalog.
    private static uint ParseLegacyCompatibilitySpellId(string value)
    {
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
        return nativeSpellId < 0 ? uint.MaxValue : checked((uint)nativeSpellId);
    }

    private static uint ParseNormalizedFlags(
        TsvRow row,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null)
    {
        var flags = row.OptionalUInt32("flags");
        if (!row.TryGet("special_effects", out var value)
            || string.IsNullOrWhiteSpace(value)) {
            return flags;
        }
        return flags | ParseSpecialEffects(
            value,
            rejectUnknown: true,
            lineNumber: row.LineNumber,
            nativeItemEffectCatalog);
    }

    private static int ParseNormalizedHellfireEffects(
        TsvRow row,
        AuthoritativeNativeItemHellfireEffectCatalog? nativeItemHellfireEffectCatalog = null)
    {
        if (!row.TryGet("hellfire_effects", out var value)
            || string.IsNullOrWhiteSpace(value)) {
            return 0;
        }

        var flags = 0;
        foreach (var effect in value.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var flag = nativeItemHellfireEffectCatalog is not null
                ? nativeItemHellfireEffectCatalog.TryGetFlag(effect, out var contentFlag)
                    ? contentFlag
                    : 0
                : effect.ToUpperInvariant() switch {
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

    private static uint ParseLegacyFlags(
        string value,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null)
    {
        return ParseSpecialEffects(
            value,
            rejectUnknown: false,
            lineNumber: null,
            nativeItemEffectCatalog);
    }

    private static uint ParseSpecialEffects(
        string value,
        bool rejectUnknown,
        int? lineNumber,
        AuthoritativeNativeItemEffectCatalog? nativeItemEffectCatalog = null)
    {
        var flags = 0U;
        foreach (var effect in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            uint? flag = nativeItemEffectCatalog is not null
                ? nativeItemEffectCatalog.TryGetFlag(effect, out var contentFlag)
                    ? contentFlag
                    : null
                : effect.ToUpperInvariant() switch {
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

    private static IReadOnlySet<string> LegacyTags(
        string itemType,
        AuthoritativeNativeItemTagCatalog? nativeItemTagCatalog = null)
    {
        if (nativeItemTagCatalog is not null)
            return nativeItemTagCatalog.TryGetTags(itemType, out var externalTags)
                ? new HashSet<string>(externalTags, StringComparer.OrdinalIgnoreCase)
                : throw new InvalidDataException(
                    $"Legacy item type '{itemType}' is not defined by the active native item tag catalog.");
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
