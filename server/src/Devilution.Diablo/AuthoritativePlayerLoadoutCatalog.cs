using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;
using Devilution.Domain.Inventory;
using Devilution.Shared;

namespace Devilution.Diablo;

public enum DiabloPlayerLoadoutItemLocation
{
    Inventory,
    Equipment,
    Belt,
}

/** Semantic class skill identity exposed to authoritative clients. */
public enum DiabloPlayerAbilityKind
{
    None,
    Repair,
    TrapDisarm,
    Recharge,
    Search,
    Identify,
    Rage,
}

/** One explicitly placed item in a content-defined class loadout. */
public sealed record AuthoritativePlayerLoadoutItemDefinition(
    uint Order,
    uint ItemId,
    DiabloPlayerLoadoutItemLocation Location,
    uint Slot);

/** Native-compatible resources, spell knowledge, and items for one class. */
public sealed record AuthoritativePlayerLoadoutDefinition(
    uint ClassId,
    uint StartingGold,
    uint AbilitySpellId,
    uint StartingSpellId,
    uint StartingSpellLevel,
    IReadOnlyList<AuthoritativePlayerLoadoutItemDefinition> Items)
{
    /** Content-owned semantic role for the class skill's presentation. */
    public DiabloPlayerAbilityKind AbilityKind { get; init; }
}

/** Validated external starting loadouts keyed by stable player-class ID. */
public sealed class AuthoritativePlayerLoadoutCatalog
{
    /** Maximum native spell identity retained by legacy item/animation fields. */
    public const uint MaximumNativeSpellId = NativeCompatibilityLimits.MaximumNativeSpellId;
    /** Maximum stable spell identity accepted in authoritative player state. */
    public const uint MaximumSpellId = NativeCompatibilityLimits.MaximumStableSpellId;
    public const uint MaximumNativeSpellLevel = NativeCompatibilityLimits.MaximumNativeSpellLevel;
    public const int MaximumMemorizedSpellCount = NativeCompatibilityLimits.MaximumMemorizedSpellCount;
    public const uint MaximumNativeCharacterLevel = NativeCompatibilityLimits.MaximumNativeCharacterLevel;
    public const uint MaximumNativeDiabloKillLevel = NativeCompatibilityLimits.MaximumNativeDiabloKillLevel;
    public const uint MaximumNativeReflectCharges = NativeCompatibilityLimits.MaximumNativeReflectCharges;
    public const uint MaximumStartingGold = 5000;
    private const int InventoryCellCount = 40;
    private const int MaximumLoadoutItemCount = 5;
    private static readonly IReadOnlyList<string> LoadoutColumns = [
        "class_id",
        "starting_gold",
        "ability_spell_id",
        "starting_spell_id",
        "starting_spell_level",
    ];
    private static readonly IReadOnlyList<string> CurrentLoadoutColumns = [
        "class_id",
        "starting_gold",
        "ability_spell_id",
        "ability_kind",
        "starting_spell_id",
        "starting_spell_level",
    ];
    private static readonly IReadOnlyList<string> SymbolicLoadoutColumns = [
        "class_id",
        "starting_gold",
        "ability_spell_id",
        "ability_spell_symbol",
        "ability_kind",
        "starting_spell_id",
        "starting_spell_symbol",
        "starting_spell_level",
    ];
    private static readonly IReadOnlyList<string> ClassSymbolicLoadoutColumns = [
        "class_id",
        "class_symbol",
        .. SymbolicLoadoutColumns.Skip(1),
    ];
    private static readonly IReadOnlyList<string> ClassCurrentLoadoutColumns = [
        "class_id",
        "class_symbol",
        .. CurrentLoadoutColumns.Skip(1),
    ];
    private static readonly IReadOnlyList<string> ClassLoadoutColumns = [
        "class_id",
        "class_symbol",
        .. LoadoutColumns.Skip(1),
    ];
    private static readonly IReadOnlyList<string> ItemColumns = [
        "class_id",
        "item_order",
        "item_id",
        "location",
        "slot",
    ];
    private static readonly IReadOnlyList<string> SymbolicItemColumns = [
        "class_id",
        "item_order",
        "item_id",
        "item_symbol",
        "location",
        "slot",
    ];
    private static readonly IReadOnlyList<string> ClassSymbolicItemColumns = [
        "class_id",
        "class_symbol",
        .. SymbolicItemColumns.Skip(1),
    ];
    private static readonly IReadOnlyList<string> ClassItemColumns = [
        "class_id",
        "class_symbol",
        .. ItemColumns.Skip(1),
    ];

    private readonly AuthoritativePlayerClassCatalog playerClasses;
    private readonly AuthoritativeItemCatalog items;
    private readonly AuthoritativeCombatRules combatRules;
    private readonly Dictionary<uint, AuthoritativePlayerLoadoutDefinition> byClassId;

    public AuthoritativePlayerLoadoutCatalog(
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativeItemCatalog items,
        AuthoritativeCombatRules combatRules,
        IEnumerable<AuthoritativePlayerLoadoutDefinition> definitions)
    {
        this.playerClasses = playerClasses
            ?? throw new ArgumentNullException(nameof(playerClasses));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.combatRules = combatRules
            ?? throw new ArgumentNullException(nameof(combatRules));
        ArgumentNullException.ThrowIfNull(definitions);
        byClassId = new Dictionary<uint, AuthoritativePlayerLoadoutDefinition>();
        foreach (var definition in definitions) {
            ArgumentNullException.ThrowIfNull(definition);
            Validate(definition);
            if (!byClassId.TryAdd(definition.ClassId, definition)) {
                throw new InvalidDataException(
                    $"Player loadout class ID {definition.ClassId} is defined more than once.");
            }
        }

        var missingClassIds = playerClasses.Definitions
            .Select(playerClass => playerClass.ClassId)
            .Where(classId => !byClassId.ContainsKey(classId))
            .ToArray();
        if (missingClassIds.Length != 0) {
            throw new InvalidDataException(
                $"Player loadouts are missing class IDs: {string.Join(", ", missingClassIds)}.");
        }
    }

    public IReadOnlyList<AuthoritativePlayerLoadoutDefinition> Definitions
        => byClassId.Values.OrderBy(definition => definition.ClassId).ToArray();

    public AuthoritativePlayerLoadoutDefinition GetRequired(uint classId)
    {
        return byClassId.TryGetValue(classId, out var definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Player loadout class ID {classId} is not defined.");
    }

    /**
     * Ensures every directly castable starting spell source has activated
     * behavior. Class abilities are validated separately because several
     * native class skills select an item/object interaction instead of casting.
     */
    public void ValidateCastSources(AuthoritativeSpellCatalog? spells)
    {
        foreach (var loadout in Definitions) {
            RequireSpell(loadout.ClassId, "memorized", loadout.StartingSpellId, spells);
            foreach (var loadoutItem in loadout.Items) {
                var item = items.GetRequired(loadoutItem.ItemId).Generate(1);
                var spellId = DiabloCastSourceRules.GetItemSpellId(item, spells, items);
                var source = DiabloCastSourceRules.ClassifyItem(item, spells, items)
                    is DiabloItemSpellSourceKind.ChargedItem
                    ? "charged"
                    : "consumable";
                if (spellId != 0) {
                    RequireSpell(
                        loadout.ClassId,
                        $"{source} item {loadoutItem.ItemId}",
                        spellId,
                        spells);
                }
            }
        }
    }

    /** Creates fresh deterministic state for a newly registered session. */
    public DiabloPlayerTemplate CreateTemplate(
        string sessionId,
        uint classId,
        uint? goldOverride,
        int? lifeOverride,
        int? manaOverride,
        uint startingLevelId,
        uint? statPointsOverride = null,
        int startingPositionX = 0,
        int startingPositionY = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var playerClass = playerClasses.GetRequired(classId);
        var loadout = GetRequired(classId);
        var attributes = playerClass.CreateStartingAttributes();
        var life = lifeOverride
            ?? playerClass.CalculateBaseLifeQ6(1, attributes.Vitality.Base);
        var mana = manaOverride
            ?? playerClass.CalculateBaseManaQ6(1, attributes.Magic.Base);
        var equipment = new List<EquippedStoreItem>();
        var belt = new List<BeltStoreItem>();
        var inventory = new List<OwnedStoreItem>();
        var inventoryGrid = Enumerable.Repeat(-1, InventoryCellCount).ToArray();
        var usedSeeds = new HashSet<uint>();

        foreach (var loadoutItem in loadout.Items.OrderBy(item => item.Order)) {
            var seed = CreateUniqueItemSeed(
                sessionId,
                classId,
                loadoutItem,
                usedSeeds);
            var itemDefinition = items.GetRequired(loadoutItem.ItemId);
            var state = items.Generate(loadoutItem.ItemId, seed) with {
                StatFlag = itemDefinition.MinimumStrength <= attributes.Strength.Current
                    && itemDefinition.MinimumMagic <= attributes.Magic.Current
                    && itemDefinition.MinimumDexterity <= attributes.Dexterity.Current,
            };
            switch (loadoutItem.Location) {
            case DiabloPlayerLoadoutItemLocation.Equipment:
                equipment.Add(new EquippedStoreItem(loadoutItem.Slot, seed, state));
                break;
            case DiabloPlayerLoadoutItemLocation.Belt:
                belt.Add(new BeltStoreItem(loadoutItem.Slot, seed, state));
                break;
            case DiabloPlayerLoadoutItemLocation.Inventory:
                var inventoryIndex = inventory.Count;
                inventory.Add(new OwnedStoreItem(
                    0,
                    loadoutItem.Order,
                    seed,
                    0,
                    0,
                    state));
                InventoryGridLayout.Fill(
                    inventoryGrid,
                    checked((int)loadoutItem.Slot),
                    inventoryIndex,
                    state.InventoryWidth,
                    state.InventoryHeight);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported starting-item location '{loadoutItem.Location}'.");
            }
        }

        var baseLife = life;
        var baseMana = mana;
        var adjustedResources = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            life,
            mana,
            equipment,
            playerClass);
        life = adjustedResources.LifeQ6;
        mana = adjustedResources.ManaQ6;

        var startingGold = goldOverride ?? loadout.StartingGold;
        AddStartingGold(
            sessionId,
            classId,
            startingGold,
            inventory,
            inventoryGrid,
            usedSeeds);

        var memorizedSpells = loadout.StartingSpellId == 0
            ? Array.Empty<MemorizedSpellState>()
            : [new MemorizedSpellState(
                loadout.StartingSpellId,
                loadout.StartingSpellLevel)];
        return new DiabloPlayerTemplate(
            startingGold,
            0,
            life,
            mana,
            mana,
            attributes,
            equipment,
            inventoryGrid,
            belt,
            startingPositionX,
            startingPositionY,
            life,
            1,
            startingLevelId,
            0,
            classId,
            inventory,
            loadout.AbilitySpellId,
            memorizedSpells,
            baseLifeMaximum: baseLife,
            baseManaMaximum: baseMana,
            playerClass: playerClass,
            abilityKind: loadout.AbilityKind,
            items: items,
            statPoints: statPointsOverride ?? 0);
    }

    public static AuthoritativePlayerLoadoutCatalog LoadTsv(
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativeItemCatalog items,
        AuthoritativeCombatRules combatRules,
        string loadoutSourcePath,
        string loadoutContents,
        string itemSourcePath,
        string itemContents,
        StableContentIdCatalog? stableIds = null)
    {
        var loadoutTable = TsvTable.Parse(loadoutSourcePath, loadoutContents);
        var itemTable = TsvTable.Parse(itemSourcePath, itemContents);
        var hasClassSymbols = loadoutTable.Columns.SequenceEqual(
                ClassSymbolicLoadoutColumns,
                StringComparer.Ordinal)
            || loadoutTable.Columns.SequenceEqual(
                ClassCurrentLoadoutColumns,
                StringComparer.Ordinal)
            || loadoutTable.Columns.SequenceEqual(
                ClassLoadoutColumns,
                StringComparer.Ordinal);
        var hasAbilityKind = loadoutTable.Columns.SequenceEqual(
                CurrentLoadoutColumns,
                StringComparer.Ordinal)
            || loadoutTable.Columns.SequenceEqual(
                ClassCurrentLoadoutColumns,
                StringComparer.Ordinal);
        var hasSymbols = loadoutTable.Columns.SequenceEqual(
                SymbolicLoadoutColumns,
                StringComparer.Ordinal)
            || loadoutTable.Columns.SequenceEqual(
                ClassSymbolicLoadoutColumns,
                StringComparer.Ordinal);
        var hasItemClassSymbols = itemTable.Columns.SequenceEqual(
                ClassSymbolicItemColumns,
                StringComparer.Ordinal)
            || itemTable.Columns.SequenceEqual(
                ClassItemColumns,
                StringComparer.Ordinal);
        var hasItemSymbols = itemTable.Columns.SequenceEqual(
                SymbolicItemColumns,
                StringComparer.Ordinal)
            || itemTable.Columns.SequenceEqual(
                ClassSymbolicItemColumns,
                StringComparer.Ordinal);
        var hasLegacyLoadout = loadoutTable.Columns.SequenceEqual(
                LoadoutColumns,
                StringComparer.Ordinal)
            || loadoutTable.Columns.SequenceEqual(
                ClassLoadoutColumns,
                StringComparer.Ordinal);
        var hasLegacyItems = itemTable.Columns.SequenceEqual(
                ItemColumns,
                StringComparer.Ordinal)
            || itemTable.Columns.SequenceEqual(
                ClassItemColumns,
                StringComparer.Ordinal);
        if (!hasSymbols
            && !hasAbilityKind
            && !hasLegacyLoadout
            || !hasItemSymbols && !hasLegacyItems) {
            throw new InvalidDataException(
                "Player-loadout tables do not match their required ordered schemas.");
        }

        var itemsByClass = itemTable.Rows
            .Select(row => new {
                ClassId = hasItemClassSymbols
                    ? AuthoritativeClassIdentity.ResolveOptional(
                        row,
                        stableIds,
                        "class_id",
                        "class_symbol")
                    : row.RequiredUInt32("class_id"),
                Item = new AuthoritativePlayerLoadoutItemDefinition(
                    row.RequiredUInt32("item_order"),
                    hasItemSymbols
                        ? AuthoritativeItemIdentity.Resolve(row, stableIds)
                        : row.RequiredUInt32("item_id"),
                    ParseLocation(row),
                    row.RequiredUInt32("slot")),
            })
            .GroupBy(entry => entry.ClassId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AuthoritativePlayerLoadoutItemDefinition>)
                    group.Select(entry => entry.Item).ToArray());
        var definitions = loadoutTable.Rows.Select(row => {
            var classId = hasClassSymbols
                ? AuthoritativeClassIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "class_id",
                    "class_symbol")
                : row.RequiredUInt32("class_id");
            return new AuthoritativePlayerLoadoutDefinition(
                classId,
                row.RequiredUInt32("starting_gold"),
                ResolveSpellId(
                    row,
                    "ability_spell_id",
                    hasSymbols ? "ability_spell_symbol" : null,
                    stableIds,
                    "ability"),
                ResolveSpellId(
                    row,
                    "starting_spell_id",
                    hasSymbols ? "starting_spell_symbol" : null,
                    stableIds,
                    "spell"),
                row.RequiredUInt32("starting_spell_level"),
                itemsByClass.GetValueOrDefault(classId) ?? []) {
                AbilityKind = hasAbilityKind || hasSymbols
                    ? ParseAbilityKind(row)
                    : DiabloPlayerAbilityKind.None,
            };
        }).ToArray();
        var extraItemClassIds = itemsByClass.Keys
            .Except(definitions.Select(definition => definition.ClassId))
            .Order()
            .ToArray();
        if (extraItemClassIds.Length != 0) {
            throw new InvalidDataException(
                $"Player-loadout items reference undefined class IDs: {string.Join(", ", extraItemClassIds)}.");
        }
        return new AuthoritativePlayerLoadoutCatalog(
            playerClasses,
            items,
            combatRules,
            definitions);
    }

    private static uint ResolveSpellId(
        TsvRow row,
        string numericColumn,
        string? symbolColumn,
        StableContentIdCatalog? stableIds,
        string stableKind)
    {
        var numericId = row.RequiredUInt32(numericColumn);
        if (symbolColumn is null
            || !row.TryGet(symbolColumn, out var symbol)
            || string.IsNullOrWhiteSpace(symbol)) {
            return numericId;
        }
        if (stableIds is null) {
            if (numericId == 0) {
                throw new InvalidDataException(
                    $"Class loadout row {row.LineNumber} requires stable spell IDs to resolve '{symbol}'.");
            }
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve(stableKind, symbol);
        } catch (KeyNotFoundException exception) {
            if (!string.Equals(stableKind, "ability", StringComparison.Ordinal)
                || !stableIds.TryResolve("spell", symbol, out resolvedId)) {
                throw new InvalidDataException(
                    $"Class loadout row {row.LineNumber} references unknown stable {stableKind} symbol '{symbol}'.",
                    exception);
            }
        }
        if (numericId != 0 && numericId != resolvedId) {
            throw new InvalidDataException(
                $"Class loadout row {row.LineNumber} maps {stableKind} {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        }
        return resolvedId;
    }

    private static DiabloPlayerAbilityKind ParseAbilityKind(TsvRow row)
    {
        var value = row.Required("ability_kind");
        if (Enum.TryParse<DiabloPlayerAbilityKind>(value, ignoreCase: false, out var kind)
            && Enum.IsDefined(kind)) {
            return kind;
        }
        throw new InvalidDataException(
            $"Class loadout row {row.LineNumber} contains unknown ability kind '{value}'.");
    }

    private void AddStartingGold(
        string sessionId,
        uint classId,
        uint value,
        List<OwnedStoreItem> inventory,
        int[] inventoryGrid,
        ISet<uint> usedSeeds)
    {
        if (value == 0)
            return;

        var maximumStack = checked((uint)combatRules.DeathGoldMaximumStack);
        var remaining = value;
        var stackOrdinal = 0U;
        foreach (var cell in EnumerateStartingGoldCells(inventoryGrid.Length)) {
            if (remaining == 0)
                break;
            if (!InventoryGridLayout.CanPlace(inventoryGrid, checked((uint)cell), 1, 1))
                continue;
            if (inventory.Count >= InventoryCellCount)
                break;

            var stackValue = Math.Min(remaining, maximumStack);
            var itemSeed = CreateUniqueGoldSeed(
                sessionId,
                classId,
                stackOrdinal++,
                usedSeeds);
            var inventoryIndex = inventory.Count;
            inventory.Add(DiabloGoldInventory.CreateStack(
                itemSeed,
                stackValue,
                combatRules,
                items));
            InventoryGridLayout.Fill(
                inventoryGrid,
                cell,
                inventoryIndex,
                1,
                1);
            remaining -= stackValue;
        }

        if (remaining != 0) {
            throw new InvalidDataException(
                $"Starting Gold {value} does not fit in the class {classId} inventory.");
        }
    }

    private static IEnumerable<int> EnumerateStartingGoldCells(int cellCount)
    {
        if (cellCount > 30)
            yield return 30;
        for (var cell = Math.Min(39, cellCount - 1); cell >= 31; cell--)
            yield return cell;
        for (var x = 9; x >= 0; x--) {
            for (var y = 2; y >= 0; y--) {
                var cell = y * InventoryGridLayout.ColumnCount + x;
                if (cell < cellCount)
                    yield return cell;
            }
        }
    }

    private void Validate(AuthoritativePlayerLoadoutDefinition definition)
    {
        if (!playerClasses.TryGet(definition.ClassId, out var playerClass)) {
            throw new InvalidDataException(
                $"Player loadout references unknown class ID {definition.ClassId}.");
        }
        if (definition.StartingGold > MaximumStartingGold
            || definition.AbilitySpellId > MaximumSpellId
            || definition.StartingSpellId > MaximumSpellId
            || definition.StartingSpellLevel > MaximumNativeSpellLevel
            || (definition.StartingSpellId == 0)
                != (definition.StartingSpellLevel == 0)) {
            throw new InvalidDataException(
                $"Player loadout {definition.ClassId} has invalid resource or spell bounds.");
        }
        if (!Enum.IsDefined(definition.AbilityKind)
            || (definition.AbilityKind != DiabloPlayerAbilityKind.None
                && definition.AbilitySpellId == 0)) {
            throw new InvalidDataException(
                $"Player loadout {definition.ClassId} has an invalid ability kind.");
        }
        if (definition.Items is null
            || definition.Items.Count > MaximumLoadoutItemCount) {
            throw new InvalidDataException(
                $"Player loadout {definition.ClassId} has too many starting items.");
        }

        var orderedItems = definition.Items.OrderBy(item => item.Order).ToArray();
        for (var index = 0; index < orderedItems.Length; index++) {
            if (orderedItems[index].Order != (uint)index) {
                throw new InvalidDataException(
                    $"Player loadout {definition.ClassId} item order must be contiguous and start at zero.");
            }
        }
        if (orderedItems.Select(item => item.Order).Distinct().Count()
            != orderedItems.Length) {
            throw new InvalidDataException(
                $"Player loadout {definition.ClassId} contains duplicate item order values.");
        }

        var equipmentSlots = new HashSet<uint>();
        var beltSlots = new HashSet<uint>();
        var inventoryGrid = Enumerable.Repeat(-1, InventoryCellCount).ToArray();
        var equipmentDefinitions = new List<(uint Slot, AuthoritativeItemDefinition Item)>();
        var inventoryIndex = 0;
        foreach (var loadoutItem in orderedItems) {
            if (!items.TryGet(loadoutItem.ItemId, out var item)) {
                throw new InvalidDataException(
                    $"Player loadout {definition.ClassId} references unknown item {loadoutItem.ItemId}.");
            }
            switch (loadoutItem.Location) {
            case DiabloPlayerLoadoutItemLocation.Equipment:
                ValidateEquipmentSlot(playerClass, loadoutItem.Slot, item);
                if (!equipmentSlots.Add(loadoutItem.Slot)) {
                    throw new InvalidDataException(
                        $"Player loadout {definition.ClassId} contains duplicate equipment slot {loadoutItem.Slot}.");
                }
                equipmentDefinitions.Add((loadoutItem.Slot, item));
                break;
            case DiabloPlayerLoadoutItemLocation.Belt:
                if (loadoutItem.Slot >= 8
                    || item.InventoryWidth != 1
                    || item.InventoryHeight != 1
                    || !AuthoritativeItemClassification.IsBelt(item)
                    || !beltSlots.Add(loadoutItem.Slot)) {
                    throw new InvalidDataException(
                        $"Player loadout {definition.ClassId} has an invalid belt item or slot.");
                }
                break;
            case DiabloPlayerLoadoutItemLocation.Inventory:
                if (!InventoryGridLayout.CanPlace(
                        inventoryGrid,
                        loadoutItem.Slot,
                        item.InventoryWidth,
                        item.InventoryHeight)) {
                    throw new InvalidDataException(
                        $"Player loadout {definition.ClassId} has an overlapping or out-of-range inventory item.");
                }
                InventoryGridLayout.Fill(
                    inventoryGrid,
                    checked((int)loadoutItem.Slot),
                    inventoryIndex++,
                    item.InventoryWidth,
                    item.InventoryHeight);
                break;
            default:
                throw new InvalidDataException(
                    $"Player loadout {definition.ClassId} has an unknown item location.");
            }
        }

        var twoHanded = equipmentDefinitions
            .FirstOrDefault(entry => entry.Slot == 4
                && AuthoritativeItemClassification.IsTwoHanded(entry.Item));
        if (twoHanded.Item is not null
            && equipmentDefinitions.Any(entry => entry.Slot == 5)) {
            throw new InvalidDataException(
                $"Player loadout {definition.ClassId} equips an off-hand item with a two-handed weapon.");
        }
    }

    private static void RequireSpell(
        uint classId,
        string source,
        uint spellId,
        AuthoritativeSpellCatalog? spells)
    {
        if (spellId == 0)
            return;
        if (spells is not null && spells.TryGet(spellId, out _))
            return;
        throw new InvalidDataException(
            $"Player loadout class ID {classId} has {source} spell {spellId}, but active spells.tsv does not define it.");
    }

    private static void ValidateEquipmentSlot(
        AuthoritativePlayerClassDefinition playerClass,
        uint slot,
        AuthoritativeItemDefinition item)
    {
        var slotMatchesLocation = AuthoritativeItemClassification.IsCompatibleWithEquipmentSlot(
            AuthoritativeItemClassification.GetEquipmentSlotKind(item),
            slot);
        var weaponInOffHand = slot == 5
            && AuthoritativeItemClassification.IsWeapon(item)
            && !IsShield(item);
        if (!slotMatchesLocation
            || (weaponInOffHand
                && !playerClass.Flags.HasFlag(DiabloPlayerClassFlags.DualWield))
            || item.MinimumStrength > playerClass.BaseStrength
            || item.MinimumMagic > playerClass.BaseMagic
            || item.MinimumDexterity > playerClass.BaseDexterity) {
            throw new InvalidDataException(
                $"Player loadout {playerClass.ClassId} cannot equip item {item.ItemId} in slot {slot}.");
        }
    }

    private static bool IsShield(AuthoritativeItemDefinition item)
        => AuthoritativeItemClassification.IsShield(item);

    private static DiabloPlayerLoadoutItemLocation ParseLocation(TsvRow row)
    {
        var value = row.Required("location");
        if (Enum.TryParse<DiabloPlayerLoadoutItemLocation>(
                value,
                ignoreCase: false,
                out var location)
            && Enum.IsDefined(location)
            && Enum.GetName(location) == value) {
            return location;
        }
        throw new InvalidDataException(
            $"Player-loadout item row {row.LineNumber} contains unknown location '{value}'.");
    }

    private static uint CreateUniqueItemSeed(
        string sessionId,
        uint classId,
        AuthoritativePlayerLoadoutItemDefinition item,
        ISet<uint> usedSeeds)
    {
        for (var attempt = 0U; ; attempt++) {
            var material = Encoding.UTF8.GetBytes(
                $"godevilution-starting-item-v1\n{sessionId}\n{classId}\n{item.Order}\n{item.ItemId}\n{attempt}");
            var seed = BinaryPrimitives.ReadUInt32LittleEndian(
                SHA256.HashData(material));
            if (seed != 0 && usedSeeds.Add(seed))
                return seed;
        }
    }

    private static uint CreateUniqueGoldSeed(
        string sessionId,
        uint classId,
        uint stackOrdinal,
        ISet<uint> usedSeeds)
    {
        for (var attempt = 0U; ; attempt++) {
            var material = Encoding.UTF8.GetBytes(
                $"godevilution-starting-gold-v1\n{sessionId}\n{classId}\n{stackOrdinal}\n{attempt}");
            var seed = BinaryPrimitives.ReadUInt32LittleEndian(
                SHA256.HashData(material));
            if (seed != 0 && usedSeeds.Add(seed))
                return seed;
        }
    }
}
