using Devilution.Data;

namespace Devilution.Diablo;

/** Native vendor role used to validate which items a store will buy. */
public enum AuthoritativeStoreRole
{
    Generic,
    Smith,
    Witch,
    Wirt,
    Healer,
    Adria,
}

/** Core authoritative item fields shared by store, inventory, and equipment projections. */
public sealed record AuthoritativeItemState(
    uint CreateInfo,
    int ItemType,
    int PositionX,
    int PositionY,
    bool Deleted,
    bool Identified,
    int Magical,
    int EquipLocation,
    int ItemClass,
    int Value,
    int IdentifiedValue,
    int MinDamage,
    int MaxDamage,
    int ArmorClass,
    uint Flags,
    int MiscId,
    int SpellId,
    int ItemIndex,
    int Charges,
    int MaxCharges,
    int Durability,
    int MaxDurability)
{
    /** Stable authoritative content ID; zero means legacy state has no stable identity. */
    public uint StableItemId { get; init; }

    /** Explicit content-owned classification; null preserves legacy native fallback. */
    public DiabloItemSpellSourceKind? SpellSourceKind { get; init; }

    /** Explicit content-owned item action; None preserves legacy fallback. */
    public DiabloItemActionKind ItemActionKind { get; init; }

    public int PlusDamage { get; init; }

    public int PlusToHit { get; init; }

    public int PlusArmorClass { get; init; }

    public int PlusStrength { get; init; }

    public int PlusMagic { get; init; }

    public int PlusDexterity { get; init; }

    public int PlusVitality { get; init; }

    public int PlusFireResistance { get; init; }

    public int PlusLightningResistance { get; init; }

    public int PlusMagicResistance { get; init; }

    public int PlusMana { get; init; }

    public int PlusHitPoints { get; init; }

    /** Native Hellfire conversion of a percentage of base mana into life. */
    public int ManaToLifePercent { get; init; }

    /** Native Hellfire conversion of a percentage of base life into mana. */
    public int LifeToManaPercent { get; init; }

    public int PlusDamageModifier { get; init; }

    public int PlusGetHit { get; init; }

    public int PlusLight { get; init; }

    public int SpellLevelAdd { get; init; }

    public int UniqueId { get; init; }

    public int FireMinDamage { get; init; }

    public int FireMaxDamage { get; init; }

    public int LightningMinDamage { get; init; }

    public int LightningMaxDamage { get; init; }

    public int PlusEnemyArmorClass { get; init; }

    public int PrefixPower { get; init; } = -1;

    public int SuffixPower { get; init; } = -1;

    public int ValueAdd1 { get; init; }

    public int ValueMultiply1 { get; init; }

    public int ValueAdd2 { get; init; }

    public int ValueMultiply2 { get; init; }

    public int MinimumStrength { get; init; }

    public int MinimumMagic { get; init; }

    public int MinimumDexterity { get; init; }

    public bool StatFlag { get; init; }

    public int HellfireDamageArmorFlags { get; init; }

    public uint Buff { get; init; }

    /** Inventory footprint in the authoritative 10-column grid. */
    public int InventoryWidth { get; init; } = 1;

    public int InventoryHeight { get; init; } = 1;

    /** Content-owned or runtime-authored base name for an item. */
    public string BaseName { get; init; } = string.Empty;

    /** Content-owned or runtime-authored identified name for an item. */
    public string IdentifiedName { get; init; } = string.Empty;

    /** Optional native inventory cursor graphic projection or override. */
    public int? NativeCursorGraphic { get; init; }

    public static AuthoritativeItemState Empty { get; } = new(
        0, -1, 0, 0, false, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0);
}

/** A deterministic item offered in one authoritative store slot. */
public sealed record StoreItem(uint StoreSlot, uint ItemSeed, uint Price)
{
    public AuthoritativeItemState State { get; init; } = AuthoritativeItemState.Empty;

    public StoreItem(uint storeSlot, uint itemSeed, uint price, AuthoritativeItemState state)
        : this(storeSlot, itemSeed, price)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }
}

/**
 * Immutable-by-convention store stock owned by the authoritative simulation.
 * The executor serializes mutations through its own state lock.
 */
public sealed class StoreCatalog
{
    private readonly Dictionary<uint, Dictionary<uint, StoreItem>> stores = new();
    private readonly Dictionary<uint, AuthoritativeStoreRole> roles = new();
    private readonly Dictionary<(uint StoreId, uint StoreSlot), NativePinnedStoreSlot> nativePinnedStoreSlots = [];

    private sealed record NativePinnedStoreSlot(
        uint ItemId,
        int NativeItemIndex,
        uint Price,
        uint ItemSeed,
        AuthoritativeItemState State);

    private const int NativePinnedHealerCreateInfo = 6;

    private static readonly IReadOnlyList<string> SymbolicProductionColumns = [
        "store_id",
        "store_symbol",
        "store_role",
        "store_slot",
        "item_seed",
        "price",
        "item_id",
        "item_symbol",
    ];

    private static readonly IReadOnlyList<string> NativePinnedStoreColumns = [
        "store_id",
        "store_symbol",
        "store_role",
        "store_slot",
        "item_id",
        "item_symbol",
        "native_item_index",
        "price",
    ];

    /**
     * Rejects numeric-only store and item identities at the current
     * production boundary. Direct callers may continue to load older shapes.
     */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(SymbolicProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production store table '{table.SourcePath}' must use the complete ordered symbolic schema.");
        foreach (var row in table.Rows)
            foreach (var column in SymbolicProductionColumns)
                _ = row.Required(column);
    }

    /** Loads the first store slice from an external TSV table. */
    public static StoreCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeItemCatalog? itemCatalog = null,
        AuthoritativeUniqueItemCatalog? uniqueItems = null,
        StableContentIdCatalog? stableIds = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var catalog = new StoreCatalog();
        var grouped = table.Rows.GroupBy(row =>
            AuthoritativeStoreIdentity.Resolve(row, stableIds));
        foreach (var store in grouped) {
            var rows = store.ToArray();
            catalog.AddStore(store.Key, rows.Select(row => new StoreItem(
                row.RequiredUInt32("store_slot"),
                row.RequiredUInt32("item_seed"),
                row.RequiredUInt32("price"),
                ParseItemState(row, itemCatalog, uniqueItems, stableIds, spellBooks, gameMode))),
                ParseRole(rows));
        }
        if (!catalog.stores.Any())
            throw new InvalidDataException($"Store table '{sourcePath}' does not contain any stores.");
        return catalog;
    }

    public void AddStore(
        uint storeId,
        IEnumerable<StoreItem> items,
        AuthoritativeStoreRole role = AuthoritativeStoreRole.Generic)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (stores.ContainsKey(storeId))
            throw new ArgumentException($"Store {storeId} has already been registered.", nameof(storeId));

        var stock = new Dictionary<uint, StoreItem>();
        foreach (var item in items) {
            if (!stock.TryAdd(item.StoreSlot, item))
                throw new ArgumentException($"Store {storeId} contains duplicate slot {item.StoreSlot}.", nameof(items));
        }

        stores.Add(storeId, stock);
        roles.Add(storeId, role);
    }

    public bool ContainsStore(uint storeId)
    {
        return stores.ContainsKey(storeId);
    }

    /** Returns the native vendor role for a store, if one was declared. */
    public bool TryGetRole(uint storeId, out AuthoritativeStoreRole role)
    {
        return roles.TryGetValue(storeId, out role);
    }

    public bool TryGetItem(uint storeId, uint storeSlot, out StoreItem item)
    {
        if (stores.TryGetValue(storeId, out var stock) && stock.TryGetValue(storeSlot, out item!))
            return true;

        item = null!;
        return false;
    }

    public bool RemoveItem(uint storeId, uint storeSlot)
    {
        if (nativePinnedStoreSlots.ContainsKey((storeId, storeSlot)))
            return false;
        return stores.TryGetValue(storeId, out var stock) && stock.Remove(storeSlot);
    }

    /**
     * Enables Pepin's pinned rows only when the explicit progression content
     * table and both exact stock identities agree. Unmarked catalogs stay finite.
     */
    internal void ConfigureNativePepinPinnedHealingStock(
        AuthoritativeItemCatalog items,
        TsvTable capabilityTable)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(capabilityTable);
        if (!string.Equals(capabilityTable.SourcePath, "native_pinned_stores.tsv", StringComparison.Ordinal)
            || !capabilityTable.Columns.SequenceEqual(NativePinnedStoreColumns, StringComparer.Ordinal)
            || capabilityTable.Rows.Count != 2)
            throw new InvalidDataException("Native Pepin stock capability must use its exact two-row schema.");
        var capabilityRows = capabilityTable.Rows.OrderBy(row => row.RequiredUInt32("store_slot")).ToArray();
        ValidatePinnedHealerCapabilityRow(capabilityRows[0], 0, 2180, "potion_healing_standard", 77, 50);
        ValidatePinnedHealerCapabilityRow(capabilityRows[1], 1, 2181, "potion_full_healing", 78, 150);

        const uint healerStoreId = 4;
        if (!roles.TryGetValue(healerStoreId, out var healerRole) || healerRole != AuthoritativeStoreRole.Healer)
            throw new InvalidDataException("Native Pepin stock capability requires store 4 with the Healer role.");
        var healing = ResolveNativePinnedHealerDefinition(items, "potion_healing_standard", 2180, 77, 3,
            DiabloItemActionKind.RestorePartialLife, 50);
        var fullHealing = ResolveNativePinnedHealerDefinition(items, "potion_full_healing", 2181, 78, 2,
            DiabloItemActionKind.RestoreFullLife, 150);
        var stock = stores[healerStoreId];
        var hasHealing = stock.TryGetValue(0, out var healingStock)
            && healingStock.State.StableItemId == healing.ItemId;
        var hasFullHealing = stock.TryGetValue(1, out var fullHealingStock)
            && fullHealingStock.State.StableItemId == fullHealing.ItemId;
        if (!hasHealing || !hasFullHealing)
            throw new InvalidDataException("Native Pepin pinned healing stock must contain both exact slots.");
        if (nativePinnedStoreSlots.Keys.Any(key => key.StoreId == healerStoreId))
            throw new InvalidDataException("Native Pepin pinned healing stock is already configured.");

        var configuredHealing = PreparePinnedSlot(0, healing, stock);
        var configuredFullHealing = PreparePinnedSlot(1, fullHealing, stock);
        stock[0] = configuredHealing.Stock;
        stock[1] = configuredFullHealing.Stock;
        nativePinnedStoreSlots.Add((healerStoreId, 0), configuredHealing.Policy);
        nativePinnedStoreSlots.Add((healerStoreId, 1), configuredFullHealing.Policy);
    }

    private static void ValidatePinnedHealerCapabilityRow(
        TsvRow row,
        uint expectedSlot,
        uint expectedItemId,
        string expectedSymbol,
        int expectedNativeIndex,
        uint expectedPrice)
    {
        if (row.RequiredUInt32("store_id") != 4
            || row.Required("store_symbol") != "healer"
            || row.Required("store_role") != "Healer"
            || row.RequiredUInt32("store_slot") != expectedSlot
            || row.RequiredUInt32("item_id") != expectedItemId
            || row.Required("item_symbol") != expectedSymbol
            || row.RequiredInt32("native_item_index") != expectedNativeIndex
            || row.RequiredUInt32("price") != expectedPrice)
            throw new InvalidDataException("Native Pepin stock capability contains an unsupported pinned row.");
    }

    internal bool TryGetNativePinnedStoreItem(uint storeId, uint storeSlot, out StoreItem item)
    {
        if (nativePinnedStoreSlots.TryGetValue((storeId, storeSlot), out var definition)
            && stores.TryGetValue(storeId, out var stock)
            && stock.TryGetValue(storeSlot, out item!)
            && MatchesNativePinnedStoreItem(item, definition))
            return true;

        item = null!;
        return false;
    }

    /** Returns a purchased copy with its fresh seed without mutating persistent stock. */
    internal bool TryCreateNativePinnedPurchaseItem(uint storeId, uint storeSlot, uint itemSeed, out StoreItem item)
    {
        if (!TryGetNativePinnedStoreItem(storeId, storeSlot, out var current)) {
            item = null!;
            return false;
        }

        item = current with { ItemSeed = itemSeed };
        return true;
    }

    internal void CopyNativePinnedStorePoliciesTo(StoreCatalog destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var (key, definition) in nativePinnedStoreSlots) {
            if (!destination.nativePinnedStoreSlots.TryAdd(key, definition))
                throw new InvalidDataException("A cohort store catalog contains duplicate native pinned stock policy.");
        }
    }

    /** Validates direct store-stock spell identities against active spells. */
    public void ValidateSpellReferences(AuthoritativeSpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        foreach (var (storeId, stock) in stores) {
            foreach (var (storeSlot, item) in stock) {
                var spellId = item.State.SpellId;
                if (spellId != 0
                    && (spellId < 0 || !spells.TryGet(checked((uint)spellId), out _)))
                    throw new InvalidDataException(
                        $"Store {storeId} slot {storeSlot} references unknown spell {spellId}.");
            }
        }
    }

    /** Replaces one known store's stock from a validated persistence projection. */
    public void ReplaceStore(uint storeId, IEnumerable<StoreItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!stores.ContainsKey(storeId))
            throw new ArgumentException($"Store {storeId} is not registered.", nameof(storeId));

        var stock = new Dictionary<uint, StoreItem>();
        foreach (var item in items) {
            ArgumentNullException.ThrowIfNull(item);
            if (!stock.TryAdd(item.StoreSlot, item)) {
                throw new ArgumentException(
                    $"Store {storeId} contains duplicate slot {item.StoreSlot}.",
                    nameof(items));
            }
        }
        ValidateNativePinnedStoreItems(storeId, stock);
        stores[storeId] = stock;
    }

    private static AuthoritativeStoreRole ParseRole(IReadOnlyList<TsvRow> rows)
    {
        var values = rows
            .Select(row => row.TryGet("store_role", out var value) ? value : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length > 1)
            throw new InvalidDataException(
                $"Store rows declare conflicting store roles: {string.Join(", ", values)}.");

        var explicitValue = values.SingleOrDefault();
        if (!string.IsNullOrWhiteSpace(explicitValue)) {
            if (!Enum.TryParse<AuthoritativeStoreRole>(explicitValue, ignoreCase: true, out var explicitRole)
                || !Enum.IsDefined(explicitRole)) {
                throw new InvalidDataException(
                    $"Store rows declare unsupported store role '{explicitValue}'.");
            }
            return explicitRole;
        }

        // Legacy/custom store symbols do not necessarily name a native vendor.
        // Infer only known roles and retain Generic for all other symbols.
        var symbol = rows
            .Select(row => row.TryGet("store_symbol", out var value) ? value : string.Empty)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return Enum.TryParse<AuthoritativeStoreRole>(symbol, ignoreCase: true, out var inferredRole)
                && Enum.IsDefined(inferredRole)
            ? inferredRole
            : AuthoritativeStoreRole.Generic;
    }

    /** Returns a stable copy of the remaining items in one store. */
    public IReadOnlyList<StoreItem> GetItems(uint storeId)
    {
        if (!stores.TryGetValue(storeId, out var stock))
            return [];

        return stock.Values.OrderBy(item => item.StoreSlot).ToArray();
    }

    /** Captures detached remaining stock and roles for every registered store. */
    public IReadOnlyList<DiabloStoreCatalogState> CaptureAll()
    {
        return stores
            .OrderBy(pair => pair.Key)
            .Select(pair => new DiabloStoreCatalogState(
                pair.Key,
                roles[pair.Key],
                pair.Value.Values
                    .OrderBy(item => item.StoreSlot)
                    .Select(CloneStoreItem)
                    .ToArray()))
            .ToArray();
    }

    /**
     * Restores all registered stock atomically after validating the complete
     * store identity and role set. Store registration and roles remain owned
     * by the active content catalog.
     */
    public void RestoreAll(IReadOnlyList<DiabloStoreCatalogState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count != stores.Count)
            throw new InvalidDataException(
                "The store catalog checkpoint does not contain the exact registered store set.");

        var seenStoreIds = new HashSet<uint>();
        var restoredStocks = new Dictionary<uint, Dictionary<uint, StoreItem>>();
        foreach (var state in states) {
            if (state is null)
                throw new InvalidDataException("The store catalog checkpoint contains a null store.");
            if (!seenStoreIds.Add(state.StoreId))
                throw new InvalidDataException(
                    $"The store catalog checkpoint contains duplicate store {state.StoreId}.");
            if (!stores.ContainsKey(state.StoreId))
                throw new InvalidDataException(
                    $"The store catalog checkpoint references unregistered store {state.StoreId}.");
            if (!Enum.IsDefined(state.Role))
                throw new InvalidDataException(
                    $"The store catalog checkpoint contains an invalid role for store {state.StoreId}.");
            if (roles[state.StoreId] != state.Role)
                throw new InvalidDataException(
                    $"The store catalog checkpoint role does not match store {state.StoreId}.");
            if (state.Items is null)
                throw new InvalidDataException(
                    $"The store catalog checkpoint contains null stock for store {state.StoreId}.");

            var stock = new Dictionary<uint, StoreItem>();
            foreach (var item in state.Items) {
                if (item is null || item.State is null)
                    throw new InvalidDataException(
                        $"The store catalog checkpoint contains a null item in store {state.StoreId}.");
                if (!stock.TryAdd(item.StoreSlot, CloneStoreItem(item)))
                    throw new InvalidDataException(
                        $"The store catalog checkpoint contains duplicate slot {item.StoreSlot} in store {state.StoreId}.");
            }
            restoredStocks.Add(state.StoreId, stock);
        }

        if (seenStoreIds.Count != stores.Count
            || stores.Keys.Any(storeId => !seenStoreIds.Contains(storeId))) {
            throw new InvalidDataException(
                "The store catalog checkpoint does not contain the exact registered store set.");
        }

        foreach (var storeId in nativePinnedStoreSlots.Keys.Select(key => key.StoreId).Distinct()) {
            if (!restoredStocks.TryGetValue(storeId, out var stock))
                throw new InvalidDataException("The store checkpoint omits native pinned Healer stock.");
            ValidateNativePinnedStoreItems(storeId, stock);
        }

        foreach (var (storeId, stock) in restoredStocks)
            stores[storeId] = stock;
    }

    private static AuthoritativeItemDefinition ResolveNativePinnedHealerDefinition(
        AuthoritativeItemCatalog items,
        string symbol,
        uint itemId,
        int nativeItemIndex,
        int miscId,
        DiabloItemActionKind action,
        uint price)
    {
        var definition = items.Definitions.SingleOrDefault(value =>
                string.Equals(value.StableSymbol, symbol, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Native Pepin stock is missing stable item '{symbol}'.");
        if (definition.ItemId != itemId || definition.NativeItemIndex != nativeItemIndex
            || definition.ItemType != 0 || definition.MiscId != miscId
            || definition.ItemActionKind != action || definition.Value != price
            || definition.InventoryWidth != 1 || definition.InventoryHeight != 1
            || string.IsNullOrWhiteSpace(definition.Name))
            throw new InvalidDataException($"Native Pepin stock item '{symbol}' does not match its pinned native identity.");
        return definition;
    }

    private static (StoreItem Stock, NativePinnedStoreSlot Policy) PreparePinnedSlot(
        uint slot,
        AuthoritativeItemDefinition definition,
        Dictionary<uint, StoreItem> stock)
    {
        if (!stock.TryGetValue(slot, out var item)
            || item.State.StableItemId != definition.ItemId
            || item.State.ItemIndex != definition.NativeItemIndex
            || item.Price != definition.Value)
            throw new InvalidDataException($"Native Pepin stock slot {slot} does not match its pinned item and price.");

        var nativeState = definition.Generate(item.ItemSeed) with {
            CreateInfo = NativePinnedHealerCreateInfo,
            Identified = false,
            StatFlag = true,
        };
        return (
            item with { State = nativeState },
            new NativePinnedStoreSlot(
                definition.ItemId,
                definition.NativeItemIndex,
                checked((uint)definition.Value),
                item.ItemSeed,
                nativeState));
    }

    private void ValidateNativePinnedStoreItems(uint storeId, Dictionary<uint, StoreItem> stock)
    {
        foreach (var ((configuredStoreId, slot), definition) in nativePinnedStoreSlots) {
            if (configuredStoreId != storeId)
                continue;
            if (!stock.TryGetValue(slot, out var item) || !MatchesNativePinnedStoreItem(item, definition))
                throw new InvalidDataException($"Native Pepin pinned stock slot {slot} is missing or has changed identity.");
        }
    }

    private static bool MatchesNativePinnedStoreItem(StoreItem item, NativePinnedStoreSlot definition)
        => item.State is not null
            && item.State.StableItemId == definition.ItemId
            && item.State.ItemIndex == definition.NativeItemIndex
            && item.Price == definition.Price
            && item.ItemSeed == definition.ItemSeed
            && item.State == definition.State;

    private static StoreItem CloneStoreItem(StoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.State is null)
            throw new InvalidDataException("A store item must contain item state.");
        return item with { State = item.State with { } };
    }

    private static AuthoritativeItemState ParseItemState(
        TsvRow row,
        AuthoritativeItemCatalog? itemCatalog,
        AuthoritativeUniqueItemCatalog? uniqueItems,
        StableContentIdCatalog? stableIds,
        AuthoritativeSpellBookCatalog? spellBooks,
        DiabloGameMode gameMode)
    {
        var hasUniqueItemId = row.TryGet("unique_item_id", out var uniqueId)
            && !string.IsNullOrWhiteSpace(uniqueId);
        var hasUniqueItemSymbol = row.TryGet("unique_item_symbol", out var uniqueSymbol)
            && !string.IsNullOrWhiteSpace(uniqueSymbol);
        if (itemCatalog is not null && uniqueItems is not null
            && (hasUniqueItemId || hasUniqueItemSymbol))
            return itemCatalog.GenerateUnique(
                AuthoritativeUniqueItemIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "unique_item_id",
                    "unique_item_symbol"),
                row.RequiredUInt32("item_seed"),
                row.OptionalInt32("item_level", 1));
        if (itemCatalog is not null && row.TryGet("item_id", out var itemId)
            && !string.IsNullOrWhiteSpace(itemId)) {
            var resolvedItemId = AuthoritativeItemIdentity.Resolve(row, stableIds);
            var itemSeed = row.RequiredUInt32("item_seed");
            var itemLevel = row.OptionalInt32("item_level");
            if (spellBooks is not null
                && spellBooks.IsBookTemplate(itemCatalog, resolvedItemId)) {
                var spellId = ResolveSpellId(row, stableIds);
                if (spellId < 0)
                    throw new InvalidDataException(
                        $"Store row {row.LineNumber} contains a negative book spell {spellId}.");
                return spellId == 0
                    ? spellBooks.GenerateRandom(
                        itemCatalog,
                        itemSeed,
                        itemLevel,
                        gameMode,
                        resolvedItemId)
                    : spellBooks.Generate(itemCatalog, checked((uint)spellId), itemSeed, itemLevel);
            }
            return itemCatalog.Generate(resolvedItemId, itemSeed, itemLevel);
        }
        return AuthoritativeItemState.Empty with {
            CreateInfo = row.OptionalUInt32("create_info"),
            ItemType = row.OptionalInt32("item_type", -1),
            PositionX = row.OptionalInt32("position_x"),
            PositionY = row.OptionalInt32("position_y"),
            Deleted = row.OptionalBoolean("deleted"),
            Identified = row.OptionalBoolean("identified"),
            Magical = row.OptionalInt32("magical"),
            EquipLocation = row.OptionalInt32("equip_location"),
            ItemClass = row.OptionalInt32("item_class"),
            Value = row.OptionalInt32("value"),
            IdentifiedValue = row.OptionalInt32("identified_value"),
            MinDamage = row.OptionalInt32("min_damage"),
            MaxDamage = row.OptionalInt32("max_damage"),
            ArmorClass = row.OptionalInt32("armor_class"),
            Flags = row.OptionalUInt32("flags"),
            MiscId = row.OptionalInt32("misc_id"),
            SpellId = ResolveSpellId(row, stableIds),
            SpellSourceKind = AuthoritativeItemCatalog.ParseSpellSourceKind(row),
            ItemActionKind = AuthoritativeItemCatalog.ParseItemActionKind(row),
            ItemIndex = row.OptionalInt32("item_index", -1),
            Charges = row.OptionalInt32("charges"),
            MaxCharges = row.OptionalInt32("max_charges"),
            Durability = row.OptionalInt32("durability"),
            MaxDurability = row.OptionalInt32("max_durability"),
            PlusDamage = row.OptionalInt32("plus_damage"),
            PlusToHit = row.OptionalInt32("plus_to_hit"),
            PlusArmorClass = row.OptionalInt32("plus_armor_class"),
            PlusStrength = row.OptionalInt32("plus_strength"),
            PlusMagic = row.OptionalInt32("plus_magic"),
            PlusDexterity = row.OptionalInt32("plus_dexterity"),
            PlusVitality = row.OptionalInt32("plus_vitality"),
            PlusFireResistance = row.OptionalInt32("plus_fire_resistance"),
            PlusLightningResistance = row.OptionalInt32("plus_lightning_resistance"),
            PlusMagicResistance = row.OptionalInt32("plus_magic_resistance"),
            PlusMana = row.OptionalInt32("plus_mana"),
            PlusHitPoints = row.OptionalInt32("plus_hit_points"),
            ManaToLifePercent = row.OptionalInt32("mana_to_life_percent"),
            LifeToManaPercent = row.OptionalInt32("life_to_mana_percent"),
            PlusDamageModifier = row.OptionalInt32("plus_damage_modifier"),
            PlusGetHit = row.OptionalInt32("plus_get_hit"),
            PlusLight = row.OptionalInt32("plus_light"),
            SpellLevelAdd = row.OptionalInt32("spell_level_add"),
            UniqueId = row.OptionalInt32("unique_id"),
            FireMinDamage = row.OptionalInt32("fire_min_damage"),
            FireMaxDamage = row.OptionalInt32("fire_max_damage"),
            LightningMinDamage = row.OptionalInt32("lightning_min_damage"),
            LightningMaxDamage = row.OptionalInt32("lightning_max_damage"),
            PlusEnemyArmorClass = row.OptionalInt32("plus_enemy_armor_class"),
            PrefixPower = row.OptionalInt32("prefix_power", -1),
            SuffixPower = row.OptionalInt32("suffix_power", -1),
            ValueAdd1 = row.OptionalInt32("value_add_1"),
            ValueMultiply1 = row.OptionalInt32("value_multiply_1"),
            ValueAdd2 = row.OptionalInt32("value_add_2"),
            ValueMultiply2 = row.OptionalInt32("value_multiply_2"),
            MinimumStrength = row.OptionalInt32("minimum_strength"),
            MinimumMagic = row.OptionalInt32("minimum_magic"),
            MinimumDexterity = row.OptionalInt32("minimum_dexterity"),
            StatFlag = row.OptionalBoolean("stat_flag"),
            HellfireDamageArmorFlags = row.OptionalInt32("hellfire_damage_armor_flags"),
            Buff = row.OptionalUInt32("buff"),
            InventoryWidth = Math.Max(1, row.OptionalInt32("inventory_width", 1)),
            InventoryHeight = Math.Max(1, row.OptionalInt32("inventory_height", 1)),
        };
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
                    $"Store row {row.LineNumber} requires stable spell IDs to resolve '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("spell", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Store row {row.LineNumber} references unknown stable spell symbol '{symbol}'.",
                exception);
        }
        if (resolvedId > int.MaxValue)
            throw new InvalidDataException(
                $"Store row {row.LineNumber} resolves spell symbol '{symbol}' "
                + $"to unsupported ID {resolvedId}.");
        var resolvedInt = checked((int)resolvedId);
        if (numericId != 0 && numericId != resolvedInt)
            throw new InvalidDataException(
                $"Store row {row.LineNumber} maps spell {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedInt}.");
        return resolvedInt;
    }
}
