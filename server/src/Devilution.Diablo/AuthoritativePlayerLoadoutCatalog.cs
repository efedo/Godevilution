using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;
using Devilution.Domain.Inventory;

namespace Devilution.Diablo;

public enum DiabloPlayerLoadoutItemLocation
{
    Inventory,
    Equipment,
    Belt,
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
    IReadOnlyList<AuthoritativePlayerLoadoutItemDefinition> Items);

/** Validated external starting loadouts keyed by stable player-class ID. */
public sealed class AuthoritativePlayerLoadoutCatalog
{
    /** Maximum native spell identity retained by legacy item/animation fields. */
    public const uint MaximumNativeSpellId = 51;
    /** Maximum stable spell identity accepted in authoritative player state. */
    public const uint MaximumSpellId = byte.MaxValue;
    public const uint MaximumNativeSpellLevel = 15;
    public const int MaximumMemorizedSpellCount = 51;
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
    private static readonly IReadOnlyList<string> ItemColumns = [
        "class_id",
        "item_order",
        "item_id",
        "location",
        "slot",
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
                var item = items.GetRequired(loadoutItem.ItemId);
                var spellId = DiabloCastSourceRules.GetChargedSpellId(
                    item.MiscId,
                    item.SpellId,
                    item.MaxCharges);
                var source = "charged";
                if (spellId == 0) {
                    spellId = DiabloCastSourceRules.GetConsumableSpellId(
                        item.MiscId,
                        item.SpellId,
                        spells);
                    source = "consumable";
                }
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
        uint startingLevelId)
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
            0,
            0,
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
            playerClass: playerClass);
    }

    public static AuthoritativePlayerLoadoutCatalog LoadTsv(
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativeItemCatalog items,
        AuthoritativeCombatRules combatRules,
        string loadoutSourcePath,
        string loadoutContents,
        string itemSourcePath,
        string itemContents)
    {
        var loadoutTable = TsvTable.Parse(loadoutSourcePath, loadoutContents);
        var itemTable = TsvTable.Parse(itemSourcePath, itemContents);
        if (!loadoutTable.Columns.SequenceEqual(LoadoutColumns, StringComparer.Ordinal)
            || !itemTable.Columns.SequenceEqual(ItemColumns, StringComparer.Ordinal)) {
            throw new InvalidDataException(
                "Player-loadout tables do not match their required ordered schemas.");
        }

        var itemsByClass = itemTable.Rows
            .Select(row => new {
                ClassId = row.RequiredUInt32("class_id"),
                Item = new AuthoritativePlayerLoadoutItemDefinition(
                    row.RequiredUInt32("item_order"),
                    row.RequiredUInt32("item_id"),
                    ParseLocation(row),
                    row.RequiredUInt32("slot")),
            })
            .GroupBy(entry => entry.ClassId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AuthoritativePlayerLoadoutItemDefinition>)
                    group.Select(entry => entry.Item).ToArray());
        var definitions = loadoutTable.Rows.Select(row => {
            var classId = row.RequiredUInt32("class_id");
            return new AuthoritativePlayerLoadoutDefinition(
                classId,
                row.RequiredUInt32("starting_gold"),
                row.RequiredUInt32("ability_spell_id"),
                row.RequiredUInt32("starting_spell_id"),
                row.RequiredUInt32("starting_spell_level"),
                itemsByClass.GetValueOrDefault(classId) ?? []);
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
                combatRules));
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
                    || item.EquipLocation != 7
                    || item.ItemClass != 3
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
            .FirstOrDefault(entry => entry.Slot == 4 && entry.Item.EquipLocation == 2);
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
        var slotMatchesLocation = item.EquipLocation switch {
            1 => slot is 4 or 5,
            2 => slot == 4,
            3 => slot == 6,
            4 => slot == 0,
            5 => slot is 1 or 2,
            6 => slot == 3,
            _ => false,
        };
        var weaponInOffHand = slot == 5 && item.ItemClass == 1 && item.ItemType != 5;
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
