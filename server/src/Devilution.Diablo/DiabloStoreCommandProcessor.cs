using Devilution.Domain.Inventory;

namespace Devilution.Diablo;

/**
 * Applies Diablo store and owned-item intents without transport, host, or
 * persistence dependencies.
 */
public sealed class DiabloStoreCommandProcessor
{
    /** Reserved service-only store used by Adria's mana refill action. */
    public const uint AdriaStoreId = 10;

    private readonly StoreCatalog catalog;
    private readonly IDiabloStoreRules rules;
    private readonly AuthoritativeCombatRules combatRules;
    private readonly DiabloRandom random;
    private readonly uint identifyAbilitySpellId;
    private readonly uint repairAbilitySpellId;
    private readonly uint rechargeAbilitySpellId;
    private readonly AuthoritativeSpellCatalog? spells;
    private readonly AuthoritativeItemCatalog? items;

    public DiabloStoreCommandProcessor(
        StoreCatalog catalog,
        IDiabloStoreRules? rules = null,
        AuthoritativeCombatRules? combatRules = null,
        DiabloRandom? random = null,
        uint identifyAbilitySpellId = 0,
        uint repairAbilitySpellId = 0,
        uint rechargeAbilitySpellId = 0,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeItemCatalog? items = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.rules = rules ?? new DiabloStoreRules();
        this.combatRules = combatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        this.random = random ?? new DiabloRandom(0);
        this.identifyAbilitySpellId = identifyAbilitySpellId;
        this.repairAbilitySpellId = repairAbilitySpellId;
        this.rechargeAbilitySpellId = rechargeAbilitySpellId;
        this.spells = spells;
        this.items = items;
    }

    public DiabloIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloStoreIntent intent,
        ulong appliedTick)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(intent);

        return intent switch {
            DiabloOpenStoreIntent open => OpenStore(player, open.StoreId),
            DiabloPurchaseIntent purchase => Purchase(player, purchase, appliedTick),
            DiabloSellItemIntent sale => Sell(player, sale.Item, sale.LegacyInventoryIndex),
            DiabloRepairItemIntent repair => Repair(player, repair),
            DiabloRechargeItemIntent recharge => Recharge(player, recharge),
            DiabloIdentifyItemIntent identify => Identify(player, identify),
            DiabloRefillManaIntent => RefillMana(player),
            DiabloMoveInventoryItemIntent moveInventory => MoveInventoryItem(
                player,
                moveInventory.InventoryIndex,
                moveInventory.TargetCell),
            DiabloMoveItemIntent move => MoveItem(player, move),
            _ => DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed),
        };
    }

    private DiabloIntentResult OpenStore(AuthoritativePlayerState player, uint storeId)
    {
        if (storeId != AdriaStoreId && !catalog.ContainsStore(storeId))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        player.ActiveStoreId = storeId;
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult Purchase(
        AuthoritativePlayerState player,
        DiabloPurchaseIntent intent,
        ulong appliedTick)
    {
        if (player.ActiveStoreId != intent.StoreId)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (!catalog.TryGetItem(intent.StoreId, intent.StoreSlot, out var item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var rejection = rules.ValidatePurchase(item, player.Gold);
        if (rejection.HasValue)
            return DiabloIntentResult.Rejected(rejection.Value);

        if (!TryResolveGoldTopology(player, out var hasGoldTopology))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        var targetCell = -1;
        if (hasGoldTopology) {
            targetCell = FindNativeInventoryPlacement(
                player.InventoryGrid,
                item.State.InventoryWidth,
                item.State.InventoryHeight);
            if (targetCell < 0 || player.Inventory.Count >= 40)
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        if (!catalog.RemoveItem(intent.StoreId, intent.StoreSlot))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        if (!SpendGold(player, item.Price, hasGoldTopology))
            throw new InvalidOperationException("Validated purchase Gold could not be removed.");
        var inventoryIndex = player.Inventory.Count;
        player.Inventory.Add(new OwnedStoreItem(
            intent.StoreId,
            item.StoreSlot,
            item.ItemSeed,
            item.Price,
            appliedTick,
            item.State));
        if (hasGoldTopology) {
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                targetCell,
                inventoryIndex,
                item.State.InventoryWidth,
                item.State.InventoryHeight);
        }
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult Sell(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? reference,
        uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (!TryGetItem(player, reference, inventoryIndex, out var item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var rejection = rules.ValidateSale(item);
        if (rejection.HasValue)
            return DiabloIntentResult.Rejected(rejection.Value);

        if (!TryResolveGoldTopology(player, out var hasGoldTopology))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        if (hasGoldTopology && DiabloGoldInventory.HasGoldIdentity(item.State, combatRules, items))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var salePrice = rules.GetSalePrice(item);
        if (uint.MaxValue - player.Gold < salePrice)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);

        if (hasGoldTopology) {
            var topology = CaptureItemTopology(player);
            RemoveItem(player, reference, inventoryIndex);
            if (!DiabloGoldInventory.TryAdd(
                    player,
                    salePrice,
                    combatRules,
                    AllocateGoldStackSeed,
                    allowTruncatedWalletRemainder: true,
                    recalculateWalletFromTopology: false,
                    items)) {
                RestoreItemTopology(player, topology);
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
            return DiabloIntentResult.Accepted;
        }

        player.Gold = checked(player.Gold + salePrice);
        RemoveItem(player, reference, inventoryIndex);
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult Repair(
        AuthoritativePlayerState player,
        DiabloRepairItemIntent intent)
    {
        if (!Enum.IsDefined(intent.Source))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        var isClassAbility = intent.Source == DiabloItemServiceSourceKind.ClassAbility;
        if (isClassAbility) {
            if (!player.HasAbility(
                    DiabloPlayerAbilityKind.Repair,
                    repairAbilitySpellId)) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
        } else if (player.ActiveStoreId is null) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!TryGetItem(
                player,
                intent.Item,
                intent.LegacyInventoryIndex,
                out var item)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (isClassAbility) {
            if (item.State.Deleted)
                return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            return RepairWithClassAbility(player, intent, item);
        }
        var rejection = rules.ValidateRepair(item, player.Gold);
        if (rejection.HasValue)
            return DiabloIntentResult.Rejected(rejection.Value);

        if (!TryResolveGoldTopology(player, out var hasGoldTopology))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        ReplaceItem(
            player,
            intent.Item,
            intent.LegacyInventoryIndex,
            item with { State = item.State with { Durability = item.State.MaxDurability } });
        if (!SpendGold(player, rules.GetRepairPrice(item), hasGoldTopology))
            throw new InvalidOperationException("Validated repair Gold could not be removed.");
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult RepairWithClassAbility(
        AuthoritativePlayerState player,
        DiabloRepairItemIntent intent,
        OwnedStoreItem item)
    {
        var durability = item.State.Durability;
        var maximumDurability = item.State.MaxDurability;
        if (durability == maximumDurability)
            return DiabloIntentResult.Accepted;
        if (maximumDurability <= 0) {
            RemoveItem(player, intent.Item, intent.LegacyInventoryIndex);
            return DiabloIntentResult.Accepted;
        }

        var level = checked((int)player.CharacterLevel);
        var durabilityLevelOffset = player.PlayerClass?.RepairAbilityDurabilityLevelOffset > 0
            ? player.PlayerClass.RepairAbilityDurabilityLevelOffset
            : 9;
        var repaired = 0;
        do {
            repaired = checked(repaired + level + random.GenerateRnd(level));
            maximumDurability -= Math.Max(
                maximumDurability / (level + durabilityLevelOffset),
                1);
            if (maximumDurability == 0) {
                RemoveItem(player, intent.Item, intent.LegacyInventoryIndex);
                return DiabloIntentResult.Accepted;
            }
        } while (repaired + durability < maximumDurability);

        ReplaceItem(
            player,
            intent.Item,
            intent.LegacyInventoryIndex,
            item with {
                State = item.State with {
                    Durability = Math.Min(durability + repaired, maximumDurability),
                    MaxDurability = maximumDurability,
                },
            });
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult Recharge(
        AuthoritativePlayerState player,
        DiabloRechargeItemIntent intent)
    {
        if (!Enum.IsDefined(intent.Source))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        var isClassAbility = intent.Source == DiabloItemServiceSourceKind.ClassAbility;
        if (isClassAbility) {
            if (!player.HasAbility(
                    DiabloPlayerAbilityKind.Recharge,
                    rechargeAbilitySpellId)) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
        } else if (player.ActiveStoreId is null) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!TryGetItem(
                player,
                intent.Item,
                intent.LegacyInventoryIndex,
                out var item)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (isClassAbility)
            return RechargeWithClassAbility(player, intent, item);
        var rejection = rules.ValidateRecharge(item, player.Gold);
        if (rejection.HasValue)
            return DiabloIntentResult.Rejected(rejection.Value);

        if (!TryResolveGoldTopology(player, out var hasGoldTopology))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        ReplaceItem(
            player,
            intent.Item,
            intent.LegacyInventoryIndex,
            item with { State = item.State with { Charges = item.State.MaxCharges } });
        if (!SpendGold(player, rules.GetRechargePrice(item), hasGoldTopology))
            throw new InvalidOperationException("Validated recharge Gold could not be removed.");
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult RechargeWithClassAbility(
        AuthoritativePlayerState player,
        DiabloRechargeItemIntent intent,
        OwnedStoreItem item)
    {
        if (item.State.Deleted)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!IsStaff(item.State)
            || item.State.SpellId <= 0
            || spells is null
            || !spells.TryGet(checked((uint)item.State.SpellId), out var spell)
            || spell.StaffLevel <= 0
            || item.State.Charges == item.State.MaxCharges) {
            return DiabloIntentResult.Accepted;
        }

        var rechargeStrength = checked(
            1 + random.GenerateRnd(checked((int)player.CharacterLevel) / spell.StaffLevel));
        var charges = item.State.Charges;
        var maximumCharges = item.State.MaxCharges;
        do {
            maximumCharges--;
            if (maximumCharges == 0) {
                ReplaceItem(
                    player,
                    intent.Item,
                    intent.LegacyInventoryIndex,
                    item with {
                        State = item.State with { MaxCharges = 0 },
                    });
                return DiabloIntentResult.Accepted;
            }
            charges = checked(charges + rechargeStrength);
        } while (charges < maximumCharges);

        ReplaceItem(
            player,
            intent.Item,
            intent.LegacyInventoryIndex,
            item with {
                State = item.State with {
                    Charges = Math.Min(charges, maximumCharges),
                    MaxCharges = maximumCharges,
                },
            });
        return DiabloIntentResult.Accepted;
    }

    private bool IsStaff(AuthoritativeItemState state)
    {
        if (items is not null
            && items.TryGetByNativeItemIndex(state.ItemIndex, out var definition)) {
            if (definition.GenerationTags.Contains("Staff"))
                return true;
            if (HasExplicitEquipmentSubtype(definition.GenerationTags))
                return false;
        }
        return state.ItemType == 10;
    }

    private static bool HasExplicitEquipmentSubtype(IReadOnlySet<string> tags)
        => tags.Contains("Sword")
            || tags.Contains("Axe")
            || tags.Contains("Mace")
            || tags.Contains("Bow")
            || tags.Contains("Shield")
            || tags.Contains("Staff");

    private DiabloIntentResult Identify(
        AuthoritativePlayerState player,
        DiabloIdentifyItemIntent intent)
    {
        if (!Enum.IsDefined(intent.Source))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        var isClassAbility = intent.Source == DiabloItemServiceSourceKind.ClassAbility;
        if (isClassAbility) {
            if (!player.HasAbility(
                    DiabloPlayerAbilityKind.Identify,
                    identifyAbilitySpellId)) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
        } else if (player.ActiveStoreId is null) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!TryGetItem(
                player,
                intent.Item,
                intent.LegacyInventoryIndex,
                out var item)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var rejection = isClassAbility
            ? item.State.Deleted
                ? DiabloRuleFailure.InvalidTarget
                : (DiabloRuleFailure?)null
            : rules.ValidateIdentification(item, player.Gold);
        if (rejection.HasValue)
            return DiabloIntentResult.Rejected(rejection.Value);
        var hasGoldTopology = false;
        if (!isClassAbility
            && !TryResolveGoldTopology(player, out hasGoldTopology)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        }

        ReplaceItem(
            player,
            intent.Item,
            intent.LegacyInventoryIndex,
            item with { State = item.State with { Identified = true } });
        if (isClassAbility)
            return DiabloIntentResult.Accepted;

        if (!SpendGold(player, rules.GetIdentificationPrice(item), hasGoldTopology))
            throw new InvalidOperationException("Validated identification Gold could not be removed.");
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult RefillMana(AuthoritativePlayerState player)
    {
        if (player.ActiveStoreId != AdriaStoreId)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        var rejection = rules.ValidateManaRefill(player.Mana, player.ManaMaximum, player.Gold);
        if (rejection.HasValue)
            return DiabloIntentResult.Rejected(rejection.Value);

        if (!TryResolveGoldTopology(player, out var hasGoldTopology))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        if (!SpendGold(
                player,
                rules.GetManaRefillPrice(player.Mana, player.ManaMaximum),
                hasGoldTopology)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);
        }
        player.Mana = player.ManaMaximum;
        return DiabloIntentResult.Accepted;
    }

    private static DiabloIntentResult MoveInventoryItem(
        AuthoritativePlayerState player,
        uint inventoryIndex,
        uint targetCell)
    {
        if (!TryGetInventoryItem(player, inventoryIndex, out var item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!InventoryGridLayout.CanPlace(
                player.InventoryGrid,
                targetCell,
                item.State.InventoryWidth,
                item.State.InventoryHeight,
                (int)inventoryIndex))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        InventoryGridLayout.Clear(player.InventoryGrid, (int)inventoryIndex);
        InventoryGridLayout.Fill(
            player.InventoryGrid,
            (int)targetCell,
            (int)inventoryIndex,
            item.State.InventoryWidth,
            item.State.InventoryHeight);
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult MoveItem(
        AuthoritativePlayerState player,
        DiabloMoveItemIntent intent)
    {
        if (!TryGetTransfer(player, intent.Item, out var source))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (DiabloGoldInventory.HasGoldIdentity(source.State, combatRules, items)
            && intent.Destination.Location != DiabloPlayerItemLocation.Inventory) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!IsValidDestination(player, intent.Destination, source.State))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (SameLocation(intent.Item, intent.Destination))
            return DiabloIntentResult.Accepted;

        var destinationIndex = intent.Destination.Location == DiabloPlayerItemLocation.Inventory
            ? InventoryGridLayout.GetItemIndexAt(player.InventoryGrid, intent.Destination.Slot)
            : -1;
        if (destinationIndex >= 0) {
            var destinationReference = intent.Destination with {
                Location = DiabloPlayerItemLocation.Inventory,
                Slot = (uint)destinationIndex,
            };
            if (!TryGetTransfer(player, destinationReference, out var destination))
                return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            if (DiabloGoldInventory.HasGoldIdentity(destination.State, combatRules, items)
                && intent.Item.Location != DiabloPlayerItemLocation.Inventory) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
            return SwapTransfers(
                player,
                intent.Item,
                intent.Destination,
                source,
                destinationReference,
                destination);
        }

        if (intent.Destination.Location != DiabloPlayerItemLocation.Inventory
            && DestinationSlotOccupied(player, intent.Destination)) {
            if (!TryGetTransfer(player, intent.Destination, out var destination))
                return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            if (DiabloGoldInventory.HasGoldIdentity(destination.State, combatRules, items)
                && intent.Item.Location != DiabloPlayerItemLocation.Inventory) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
            return SwapTransfers(
                player,
                intent.Item,
                intent.Destination,
                source,
                intent.Destination,
                destination);
        }

        if (intent.Destination.Location == DiabloPlayerItemLocation.Inventory && destinationIndex < 0) {
            var ignoredIndex = intent.Item.Location == DiabloPlayerItemLocation.Inventory
                ? (int)intent.Item.Slot
                : -1;
            if (!InventoryGridLayout.CanPlace(
                    player.InventoryGrid,
                    intent.Destination.Slot,
                    source.State.InventoryWidth,
                    source.State.InventoryHeight,
                    ignoredIndex))
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            if (intent.Item.Location == DiabloPlayerItemLocation.Inventory) {
                InventoryGridLayout.Clear(player.InventoryGrid, (int)intent.Item.Slot);
                InventoryGridLayout.Fill(
                    player.InventoryGrid,
                    (int)intent.Destination.Slot,
                    (int)intent.Item.Slot,
                    source.State.InventoryWidth,
                    source.State.InventoryHeight);
            } else {
                AddTransfer(player, intent.Destination, source);
                RemoveTransfer(player, intent.Item);
                RecalculateEquipmentResourcesIfChanged(
                    player,
                    intent.Item,
                    intent.Destination);
            }
            return DiabloIntentResult.Accepted;
        }

        RemoveTransfer(player, intent.Item);
        AddTransfer(player, intent.Destination, source);
        RecalculateEquipmentResourcesIfChanged(
            player,
            intent.Item,
            intent.Destination);
        return DiabloIntentResult.Accepted;
    }

    private static DiabloIntentResult SwapTransfers(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference sourceReference,
        DiabloPlayerItemReference destinationReference,
        TransferItem source,
        DiabloPlayerItemReference actualDestinationReference,
        TransferItem destination)
    {
        var equipmentChanged = sourceReference.Location == DiabloPlayerItemLocation.Equipment
            || destinationReference.Location == DiabloPlayerItemLocation.Equipment
            || actualDestinationReference.Location == DiabloPlayerItemLocation.Equipment;
        if (sourceReference.Location == DiabloPlayerItemLocation.Inventory
            && actualDestinationReference.Location == DiabloPlayerItemLocation.Inventory) {
            var sourceIndex = (int)sourceReference.Slot;
            var destinationIndex = (int)actualDestinationReference.Slot;
            var sourceAnchor = InventoryGridLayout.FindAnchor(player.InventoryGrid, sourceIndex);
            if (sourceAnchor < 0
                || !InventoryGridLayout.CanPlace(
                    player.InventoryGrid,
                    (uint)sourceAnchor,
                    destination.State.InventoryWidth,
                    destination.State.InventoryHeight,
                    sourceIndex,
                    destinationIndex)
                || !InventoryGridLayout.CanPlace(
                    player.InventoryGrid,
                    destinationReference.Slot,
                    source.State.InventoryWidth,
                    source.State.InventoryHeight,
                    sourceIndex,
                    destinationIndex))
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

            InventoryGridLayout.Clear(player.InventoryGrid, sourceIndex);
            InventoryGridLayout.Clear(player.InventoryGrid, destinationIndex);
            player.Inventory[sourceIndex] = ToOwnedItem(destination);
            player.Inventory[destinationIndex] = ToOwnedItem(source);
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                sourceAnchor,
                sourceIndex,
                destination.State.InventoryWidth,
                destination.State.InventoryHeight);
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                (int)destinationReference.Slot,
                destinationIndex,
                source.State.InventoryWidth,
                source.State.InventoryHeight);
            if (equipmentChanged)
                player.RecalculateEquipmentResources();
            return DiabloIntentResult.Accepted;
        }

        if (actualDestinationReference.Location == DiabloPlayerItemLocation.Inventory) {
            var destinationIndex = (int)actualDestinationReference.Slot;
            if (!InventoryGridLayout.CanPlace(
                    player.InventoryGrid,
                    destinationReference.Slot,
                    source.State.InventoryWidth,
                    source.State.InventoryHeight,
                    destinationIndex))
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            var destinationAnchor = (int)destinationReference.Slot;
            InventoryGridLayout.Clear(player.InventoryGrid, destinationIndex);
            player.Inventory[destinationIndex] = ToOwnedItem(source);
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                destinationAnchor,
                destinationIndex,
                source.State.InventoryWidth,
                source.State.InventoryHeight);
            ReplaceTransfer(player, sourceReference, destination);
            if (equipmentChanged)
                player.RecalculateEquipmentResources();
            return DiabloIntentResult.Accepted;
        }

        if (sourceReference.Location == DiabloPlayerItemLocation.Inventory) {
            var sourceIndex = (int)sourceReference.Slot;
            var sourceAnchor = InventoryGridLayout.FindAnchor(player.InventoryGrid, sourceIndex);
            if (sourceAnchor < 0 || !InventoryGridLayout.CanPlace(
                    player.InventoryGrid,
                    (uint)sourceAnchor,
                    destination.State.InventoryWidth,
                    destination.State.InventoryHeight,
                    sourceIndex))
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            InventoryGridLayout.Clear(player.InventoryGrid, sourceIndex);
            player.Inventory[sourceIndex] = ToOwnedItem(destination);
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                sourceAnchor,
                sourceIndex,
                destination.State.InventoryWidth,
                destination.State.InventoryHeight);
            ReplaceTransfer(player, destinationReference, source);
            if (equipmentChanged)
                player.RecalculateEquipmentResources();
            return DiabloIntentResult.Accepted;
        }

        ReplaceTransfer(player, sourceReference, destination);
        ReplaceTransfer(player, actualDestinationReference, source);
        if (equipmentChanged)
            player.RecalculateEquipmentResources();
        return DiabloIntentResult.Accepted;
    }

    private static void ReplaceTransfer(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference,
        TransferItem item)
    {
        if (reference.Location == DiabloPlayerItemLocation.Inventory) {
            player.Inventory[(int)reference.Slot] = ToOwnedItem(item);
        } else if (reference.Location == DiabloPlayerItemLocation.Belt) {
            var index = player.Belt.FindIndex(candidate => candidate.Slot == reference.Slot);
            player.Belt[index] = new BeltStoreItem(reference.Slot, item.ItemSeed, item.State);
        } else {
            var index = player.Equipment.FindIndex(candidate => candidate.Slot == reference.Slot);
            player.Equipment[index] = new EquippedStoreItem(reference.Slot, item.ItemSeed, item.State);
        }
    }

    private static bool TryGetTransfer(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference,
        out TransferItem item)
    {
        switch (reference.Location) {
        case DiabloPlayerItemLocation.Inventory when reference.Slot < player.Inventory.Count:
            var owned = player.Inventory[(int)reference.Slot];
            item = new TransferItem(
                owned.ItemSeed,
                owned.State,
                owned.StoreId,
                owned.StoreSlot,
                owned.Price,
                owned.PurchasedAtTick);
            return true;
        case DiabloPlayerItemLocation.Belt:
            var belt = player.Belt.FirstOrDefault(candidate => candidate.Slot == reference.Slot);
            if (belt is not null) {
                item = new TransferItem(
                    belt.ItemSeed,
                    belt.State,
                    0,
                    belt.Slot,
                    GetReferencePrice(belt.State),
                    0);
                return true;
            }
            break;
        case DiabloPlayerItemLocation.Equipment:
            var equipment = player.Equipment.FirstOrDefault(candidate => candidate.Slot == reference.Slot);
            if (equipment is not null) {
                item = new TransferItem(
                    equipment.ItemSeed,
                    equipment.State,
                    0,
                    equipment.Slot,
                    GetReferencePrice(equipment.State),
                    0);
                return true;
            }
            break;
        }

        item = default;
        return false;
    }

    private static bool IsValidDestination(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference,
        AuthoritativeItemState state)
    {
        return reference.Location switch {
            DiabloPlayerItemLocation.Inventory => InventoryGridLayout.IsWithinBounds(
                player.InventoryGrid.Count,
                reference.Slot,
                state.InventoryWidth,
                state.InventoryHeight),
            DiabloPlayerItemLocation.Belt or DiabloPlayerItemLocation.Equipment => reference.Slot < 8
                && state.InventoryWidth <= 1
                && state.InventoryHeight <= 1,
            _ => false,
        };
    }

    private static bool DestinationSlotOccupied(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference)
    {
        return reference.Location switch {
            DiabloPlayerItemLocation.Belt => player.Belt.Any(item => item.Slot == reference.Slot),
            DiabloPlayerItemLocation.Equipment => player.Equipment.Any(item => item.Slot == reference.Slot),
            _ => false,
        };
    }

    private static OwnedStoreItem ToOwnedItem(TransferItem item)
    {
        return new OwnedStoreItem(
            item.StoreId,
            item.StoreSlot,
            item.ItemSeed,
            item.Price,
            item.PurchasedAtTick,
            item.State);
    }

    private static bool SameLocation(
        DiabloPlayerItemReference source,
        DiabloPlayerItemReference destination)
    {
        return source.Location == destination.Location && source.Slot == destination.Slot;
    }

    private static void RecalculateEquipmentResourcesIfChanged(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference source,
        DiabloPlayerItemReference destination)
    {
        if (source.Location == DiabloPlayerItemLocation.Equipment
            || destination.Location == DiabloPlayerItemLocation.Equipment) {
            player.RecalculateEquipmentResources();
        }
    }

    private static void RemoveTransfer(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference)
    {
        if (reference.Location == DiabloPlayerItemLocation.Inventory) {
            player.Inventory.RemoveAt((int)reference.Slot);
            InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, (int)reference.Slot);
        } else if (reference.Location == DiabloPlayerItemLocation.Belt) {
            player.Belt.RemoveAll(item => item.Slot == reference.Slot);
        } else {
            player.Equipment.RemoveAll(item => item.Slot == reference.Slot);
        }
    }

    private static void AddTransfer(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference destination,
        TransferItem item)
    {
        if (destination.Location == DiabloPlayerItemLocation.Inventory) {
            var index = player.Inventory.Count;
            player.Inventory.Add(ToOwnedItem(item));
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                (int)destination.Slot,
                index,
                item.State.InventoryWidth,
                item.State.InventoryHeight);
        } else if (destination.Location == DiabloPlayerItemLocation.Belt) {
            player.Belt.Add(new BeltStoreItem(destination.Slot, item.ItemSeed, item.State));
        } else {
            player.Equipment.Add(new EquippedStoreItem(destination.Slot, item.ItemSeed, item.State));
        }
    }

    private static bool TryGetInventoryItem(
        AuthoritativePlayerState player,
        uint inventoryIndex,
        out OwnedStoreItem item)
    {
        return DiabloPlayerItemAccess.TryGet(
            player,
            new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                inventoryIndex),
            out item);
    }

    private static bool TryGetItem(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? reference,
        uint inventoryIndex,
        out OwnedStoreItem item)
    {
        if (reference is null)
            return TryGetInventoryItem(player, inventoryIndex, out item);
        return DiabloPlayerItemAccess.TryGet(player, reference.Value, out item);
    }

    private static void ReplaceItem(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? reference,
        uint inventoryIndex,
        OwnedStoreItem item)
    {
        DiabloPlayerItemAccess.Replace(
            player,
            reference ?? new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                inventoryIndex),
            item);
    }

    private static void RemoveItem(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? reference,
        uint inventoryIndex)
    {
        DiabloPlayerItemAccess.Remove(
            player,
            reference ?? new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                inventoryIndex));
    }

    private static uint GetReferencePrice(AuthoritativeItemState state)
    {
        var value = state.Identified && state.IdentifiedValue > 0
            ? state.IdentifiedValue
            : state.Value;
        return (uint)Math.Max(1, value);
    }

    private bool TryResolveGoldTopology(
        AuthoritativePlayerState player,
        out bool hasGoldTopology)
    {
        hasGoldTopology = DiabloGoldInventory.TryGetTopologyTotal(
            player,
            combatRules,
            allowTruncatedWalletRemainder: true,
            out _,
            items);
        return hasGoldTopology || !player.Inventory.Any(
            item => DiabloGoldInventory.HasGoldIdentity(item.State, combatRules, items));
    }

    private bool SpendGold(
        AuthoritativePlayerState player,
        uint value,
        bool hasGoldTopology)
    {
        if (hasGoldTopology)
            return DiabloGoldInventory.TrySpend(
                player,
                value,
                combatRules,
                allowTruncatedWalletRemainder: true,
                items);
        if (value > player.Gold)
            return false;
        player.Gold -= value;
        return true;
    }

    private uint AllocateGoldStackSeed()
    {
        return unchecked((uint)random.AdvanceRndSeed());
    }

    private static int FindNativeInventoryPlacement(
        IReadOnlyList<int> inventoryGrid,
        int width,
        int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (height == 1) {
            for (var cell = 30; cell <= Math.Min(39, inventoryGrid.Count - 1); cell++) {
                if (InventoryGridLayout.CanPlace(inventoryGrid, checked((uint)cell), width, height))
                    return cell;
            }
            for (var x = 9; x >= 0; x--) {
                for (var y = 2; y >= 0; y--) {
                    var cell = y * InventoryGridLayout.ColumnCount + x;
                    if (cell < inventoryGrid.Count
                        && InventoryGridLayout.CanPlace(
                            inventoryGrid,
                            checked((uint)cell),
                            width,
                            height)) {
                        return cell;
                    }
                }
            }
            return -1;
        }
        if (height == 2) {
            for (var x = InventoryGridLayout.ColumnCount - width; x >= 0; x--) {
                for (var y = 0; y < 3; y++) {
                    var cell = y * InventoryGridLayout.ColumnCount + x;
                    if (InventoryGridLayout.CanPlace(
                            inventoryGrid,
                            checked((uint)cell),
                            width,
                            height)) {
                        return cell;
                    }
                }
            }
            return -1;
        }
        if (width == 1 && height == 3) {
            for (var cell = 0; cell < Math.Min(20, inventoryGrid.Count); cell++) {
                if (InventoryGridLayout.CanPlace(inventoryGrid, checked((uint)cell), width, height))
                    return cell;
            }
            return -1;
        }
        if (width == 2 && height == 3) {
            for (var cell = 0; cell < Math.Min(9, inventoryGrid.Count); cell++) {
                if (InventoryGridLayout.CanPlace(inventoryGrid, checked((uint)cell), width, height))
                    return cell;
            }
            for (var cell = 10; cell < Math.Min(19, inventoryGrid.Count); cell++) {
                if (InventoryGridLayout.CanPlace(inventoryGrid, checked((uint)cell), width, height))
                    return cell;
            }
        }
        return -1;
    }

    private static ItemTopologySnapshot CaptureItemTopology(
        AuthoritativePlayerState player)
    {
        return new ItemTopologySnapshot(
            player.Inventory.ToArray(),
            player.InventoryGrid.ToArray(),
            player.Belt.ToArray(),
            player.Equipment.ToArray());
    }

    private static void RestoreItemTopology(
        AuthoritativePlayerState player,
        ItemTopologySnapshot topology)
    {
        player.Inventory.Clear();
        player.Inventory.AddRange(topology.Inventory);
        for (var index = 0; index < player.InventoryGrid.Count; index++)
            player.InventoryGrid[index] = topology.InventoryGrid[index];
        player.Belt.Clear();
        player.Belt.AddRange(topology.Belt);
        player.Equipment.Clear();
        player.Equipment.AddRange(topology.Equipment);
    }

    private readonly record struct TransferItem(
        uint ItemSeed,
        AuthoritativeItemState State,
        uint StoreId,
        uint StoreSlot,
        uint Price,
        ulong PurchasedAtTick);

    private sealed record ItemTopologySnapshot(
        OwnedStoreItem[] Inventory,
        int[] InventoryGrid,
        BeltStoreItem[] Belt,
        EquippedStoreItem[] Equipment);
}
