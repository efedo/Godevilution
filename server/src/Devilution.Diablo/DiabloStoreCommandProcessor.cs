using Devilution.Domain.Inventory;
using Devilution.Engine;

namespace Devilution.Diablo;

/**
 * Applies Diablo store and owned-item intents without transport, host, or
 * persistence dependencies.
 */
public sealed class DiabloStoreCommandProcessor
{
    /** Reserved service-only store used by Adria's mana refill action. */
    public const uint AdriaStoreId = 10;
    /** Reserved service-only store used by Cain's paid identification action. */
    public const uint StorytellerStoreId = 11;

    private readonly StoreCatalog catalog;
    private readonly IDiabloStoreRules rules;
    private readonly AuthoritativeCombatRules combatRules;
    private readonly DeterministicLegacyRandom random;
    private readonly uint identifyAbilitySpellId;
    private readonly uint repairAbilitySpellId;
    private readonly uint rechargeAbilitySpellId;
    private readonly uint adriaStoreId;
    private readonly uint storytellerStoreId;
    private readonly AuthoritativeSpellCatalog? spells;
    private readonly AuthoritativeItemCatalog? items;
    private readonly uint townLevelId;

    public DiabloStoreCommandProcessor(
        StoreCatalog catalog,
        IDiabloStoreRules? rules = null,
        AuthoritativeCombatRules? combatRules = null,
        DeterministicLegacyRandom? random = null,
        uint identifyAbilitySpellId = 0,
        uint repairAbilitySpellId = 0,
        uint rechargeAbilitySpellId = 0,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeItemCatalog? items = null,
        uint adriaStoreId = AdriaStoreId,
        uint townLevelId = AuthoritativePortalRules.TownLevelId,
        uint storytellerStoreId = StorytellerStoreId)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.rules = rules ?? new DiabloStoreRules();
        this.combatRules = combatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        this.random = random ?? new DeterministicLegacyRandom(0);
        this.identifyAbilitySpellId = identifyAbilitySpellId;
        this.repairAbilitySpellId = repairAbilitySpellId;
        this.rechargeAbilitySpellId = rechargeAbilitySpellId;
        if (adriaStoreId == 0)
            throw new ArgumentOutOfRangeException(nameof(adriaStoreId));
        if (storytellerStoreId == 0 || storytellerStoreId == adriaStoreId)
            throw new ArgumentOutOfRangeException(nameof(storytellerStoreId));
        this.adriaStoreId = adriaStoreId;
        this.storytellerStoreId = storytellerStoreId;
        this.townLevelId = townLevelId;
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
        HydrateInventoryDimensions(player);

        return intent switch {
            DiabloOpenStoreIntent open => OpenStore(player, open.StoreId),
            DiabloCloseStoreIntent => CloseStore(player),
            DiabloOpenStashIntent => OpenStash(player),
            DiabloCloseStashIntent => CloseStash(player),
            DiabloDepositStashGoldIntent deposit => DepositStashGold(player, deposit.Amount),
            DiabloWithdrawStashGoldIntent withdraw => WithdrawStashGold(player, withdraw.Amount),
            DiabloSplitGoldIntent split => SplitGold(player, split),
            DiabloSortInventoryIntent => SortInventory(player),
            DiabloPurchaseIntent purchase => Purchase(player, purchase, appliedTick),
            DiabloSellItemIntent sale => Sell(player, sale.Item, sale.LegacyInventoryIndex),
            DiabloRepairItemIntent repair => Repair(player, repair),
            DiabloRechargeItemIntent recharge => Recharge(player, recharge),
            DiabloIdentifyItemIntent identify => Identify(player, identify),
            DiabloReadBookIntent readBook => ReadBook(player, readBook),
            DiabloUseConsumableIntent useConsumable => UseConsumable(player, useConsumable),
            DiabloRefillManaIntent => RefillMana(player),
            DiabloMoveInventoryItemIntent moveInventory => MoveInventoryItem(
                player,
                moveInventory.Item,
                moveInventory.InventoryIndex,
                moveInventory.TargetCell),
            DiabloMoveItemIntent move => MoveItem(player, move),
            DiabloAutoEquipItemIntent autoEquip => AutoEquipItem(player, autoEquip),
            _ => DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed),
        };
    }

    private void HydrateInventoryDimensions(AuthoritativePlayerState player)
    {
        if (items is null)
            return;

        for (var index = 0; index < player.Inventory.Count; index++) {
            var item = player.Inventory[index];
            var state = ResolveInventoryDimensions(item.State);
            if (!ReferenceEquals(state, item.State))
                player.Inventory[index] = item with { State = state };
        }
        for (var index = 0; index < player.Belt.Count; index++) {
            var item = player.Belt[index];
            var state = ResolveInventoryDimensions(item.State);
            if (!ReferenceEquals(state, item.State))
                player.Belt[index] = item with { State = state };
        }
        for (var index = 0; index < player.Equipment.Count; index++) {
            var item = player.Equipment[index];
            var state = ResolveInventoryDimensions(item.State);
            if (!ReferenceEquals(state, item.State))
                player.Equipment[index] = item with { State = state };
        }
        for (var index = 0; index < player.Stash.Count; index++) {
            var item = player.Stash[index];
            var state = ResolveInventoryDimensions(item.State);
            if (!ReferenceEquals(state, item.State))
                player.Stash[index] = item with { State = state };
        }
        if (player.HeldItem is { } heldItem) {
            var state = ResolveInventoryDimensions(heldItem.State);
            if (!ReferenceEquals(state, heldItem.State))
                player.HeldItem = heldItem with { State = state };
        }
    }

    private AuthoritativeItemState ResolveInventoryDimensions(AuthoritativeItemState state)
    {
        return items!.ResolveInventoryDimensions(state);
    }

    private DiabloIntentResult OpenStore(AuthoritativePlayerState player, uint storeId)
    {
        if (player.StashOpen)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if ((storeId == adriaStoreId || storeId == storytellerStoreId)
            && player.LevelId != townLevelId)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (storeId != adriaStoreId
            && storeId != storytellerStoreId
            && !catalog.ContainsStore(storeId))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        player.ActiveStoreId = storeId;
        return DiabloIntentResult.Accepted;
    }

    private static DiabloIntentResult CloseStore(AuthoritativePlayerState player)
    {
        player.ActiveStoreId = null;
        return DiabloIntentResult.Accepted;
    }

    private static DiabloIntentResult OpenStash(AuthoritativePlayerState player)
    {
        if (player.ActiveStoreId is not null)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        player.StashOpen = true;
        return DiabloIntentResult.Accepted;
    }

    private static DiabloIntentResult CloseStash(AuthoritativePlayerState player)
    {
        player.StashOpen = false;
        return DiabloIntentResult.Accepted;
    }

    private static DiabloIntentResult DepositStashGold(
        AuthoritativePlayerState player,
        uint amount)
    {
        if (!player.StashOpen || amount == 0)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (amount > player.Gold)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);
        if (amount > uint.MaxValue - player.StashGold)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);
        player.Gold -= amount;
        player.StashGold += amount;
        return DiabloIntentResult.Accepted;
    }

    private static DiabloIntentResult WithdrawStashGold(
        AuthoritativePlayerState player,
        uint amount)
    {
        if (!player.StashOpen || amount == 0)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (amount > player.StashGold)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);
        if (amount > uint.MaxValue - player.Gold)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);
        player.StashGold -= amount;
        player.Gold += amount;
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult SplitGold(
        AuthoritativePlayerState player,
        DiabloSplitGoldIntent intent)
    {
        if (intent.Amount == 0 || player.HeldItem is not null)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (intent.Item.Location is not (DiabloPlayerItemLocation.Inventory
            or DiabloPlayerItemLocation.Belt)
            || !DiabloPlayerItemAccess.TryGet(player, intent.Item, out var source)
            || !DiabloGoldInventory.IsValidStack(source, combatRules, items)
            || intent.Amount > source.Price) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (intent.Amount > player.Gold)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);

        if (intent.Amount == source.Price) {
            DiabloPlayerItemAccess.Remove(player, intent.Item);
        } else {
            DiabloPlayerItemAccess.Replace(
                player,
                intent.Item,
                DiabloGoldInventory.WithValue(
                    source,
                    source.Price - intent.Amount,
                    combatRules,
                    items));
        }

        var held = DiabloGoldInventory.CreateStack(
            AllocateNonzeroGoldStackSeed(),
            intent.Amount,
            combatRules,
            items) with {
                PurchasedAtTick = 0,
            };
        player.Gold -= intent.Amount;
        player.HeldItem = held;
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult SortInventory(AuthoritativePlayerState player)
    {
        if (player.HeldItem is not null)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (player.Inventory.Count == 0)
            return DiabloIntentResult.Accepted;

        var sortedItems = player.Inventory
            .Select((item, index) => (Item: item, Index: index))
            .OrderByDescending(entry => entry.Item.State.InventoryHeight)
            .ThenByDescending(entry => entry.Item.State.InventoryWidth)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Item)
            .ToArray();
        var sortedGrid = Enumerable.Repeat(-1, player.InventoryGrid.Count).ToArray();
        for (var index = 0; index < sortedItems.Length; index++) {
            var state = sortedItems[index].State;
            var targetCell = InventoryGridLayout.FindNativePlacement(
                sortedGrid,
                state.InventoryWidth,
                state.InventoryHeight);
            if (targetCell < 0)
                return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            InventoryGridLayout.Fill(
                sortedGrid,
                targetCell,
                index,
                state.InventoryWidth,
                state.InventoryHeight);
        }

        player.Inventory.Clear();
        player.Inventory.AddRange(sortedItems);
        for (var cell = 0; cell < player.InventoryGrid.Count; cell++)
            player.InventoryGrid[cell] = sortedGrid[cell];
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
        // Gold topology controls wallet mutation; it does not determine whether
        // a supplied inventory grid must receive the purchased item.
        if (player.InventoryGrid.Count > 0) {
            targetCell = InventoryGridLayout.FindNativePlacement(
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
        if (targetCell >= 0) {
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
        if (player.ActiveStoreId.Value == adriaStoreId)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (catalog.TryGetRole(player.ActiveStoreId.Value, out var role)
            && !AuthoritativeStoreSalePolicy.Accepts(
                role,
                item.State,
                items,
                spells,
                combatRules.GameVariant)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
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
        if (IsClosedStashTarget(player, intent.Item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        var isClassAbility = intent.Source == DiabloItemServiceSourceKind.ClassAbility;
        if (isClassAbility) {
            if (!player.HasAbility(
                    DiabloPlayerAbilityKind.Repair,
                    repairAbilitySpellId)) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
        } else if (!AllowsVendorService(player, AuthoritativeStoreRole.Smith)) {
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
        var maximumDurability = ResolveMaximumDurability(item.State);
        ReplaceItem(
            player,
            intent.Item,
            intent.LegacyInventoryIndex,
            item with {
                State = item.State with {
                    Durability = maximumDurability,
                    MaxDurability = maximumDurability == 0
                        ? item.State.MaxDurability
                        : maximumDurability,
                },
            });
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
        var maximumDurability = ResolveMaximumDurability(item.State);
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
        if (IsClosedStashTarget(player, intent.Item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        var isClassAbility = intent.Source == DiabloItemServiceSourceKind.ClassAbility;
        if (isClassAbility) {
            if (!player.HasAbility(
                    DiabloPlayerAbilityKind.Recharge,
                    rechargeAbilitySpellId)) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
        } else if (!AllowsVendorService(player, AuthoritativeStoreRole.Witch)) {
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
        var maximumCharges = ResolveChargedMaximumCharges(item.State);
        ReplaceItem(
            player,
            intent.Item,
            intent.LegacyInventoryIndex,
            item with {
                State = item.State with {
                    Charges = maximumCharges,
                    MaxCharges = maximumCharges == 0
                        ? item.State.MaxCharges
                        : maximumCharges,
                },
            });
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
        var spellId = ResolveChargedSpellId(item.State);
        var maximumCharges = ResolveChargedMaximumCharges(item.State);
        if (!IsStaff(item.State)
            || spellId <= 0
            || spells is null
            || !spells.TryGet(checked((uint)spellId), out var spell)
            || spell.StaffLevel <= 0
            || item.State.Charges == maximumCharges) {
            return DiabloIntentResult.Accepted;
        }

        var rechargeStrength = checked(
            1 + random.GenerateRnd(checked((int)player.CharacterLevel) / spell.StaffLevel));
        var charges = item.State.Charges;
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
        => AuthoritativeItemClassification.IsStaff(state, items);

    /** Allows legacy generic catalogs while constraining declared native vendors. */
    private bool AllowsVendorService(
        AuthoritativePlayerState player,
        AuthoritativeStoreRole requiredRole)
    {
        if (player.ActiveStoreId is not uint storeId || storeId == adriaStoreId)
            return false;
        if (!catalog.TryGetRole(storeId, out var role))
            return false;
        return role is AuthoritativeStoreRole.Generic || role == requiredRole;
    }

    private int ResolveMaximumDurability(AuthoritativeItemState state)
    {
        if (state.MaxDurability != 0)
            return state.MaxDurability;
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.MaxDurability > 0)
                return definition.MaxDurability;
            if (items.TryGetDurabilityMetadataByMiscId(
                    state.MiscId,
                    out _,
                    out var maximumDurability))
                return maximumDurability;
        }
        return 0;
    }

    private (int MinimumDamage, int MaximumDamage) ResolveBaseDamage(
        AuthoritativeItemState state)
    {
        if (state.MaxDamage > 0)
            return (state.MinDamage, state.MaxDamage);
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.MaxDamage > 0)
                return (definition.MinDamage, definition.MaxDamage);
            if (items.TryGetBaseDamageByMiscId(
                    state.MiscId,
                    out var minimumDamage,
                    out var maximumDamage))
                return (minimumDamage, maximumDamage);
        }
        return (state.MinDamage, state.MaxDamage);
    }

    private int ResolveChargedSpellId(AuthoritativeItemState state)
    {
        if (state.SpellId != 0)
            return state.SpellId;
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.SpellSourceKind == DiabloItemSpellSourceKind.ChargedItem
                && definition.SpellId > 0)
                return definition.SpellId;
            if (items.TryGetChargedItemMetadataByMiscId(
                    state.MiscId,
                    out _,
                    out var spellId))
                return spellId;
        }
        return 0;
    }

    private int ResolveChargedMaximumCharges(AuthoritativeItemState state)
    {
        if (state.MaxCharges != 0)
            return state.MaxCharges;
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.SpellSourceKind == DiabloItemSpellSourceKind.ChargedItem
                && definition.MaxCharges > 0)
                return definition.MaxCharges;
            if (items.TryGetChargedItemMetadataByMiscId(
                    state.MiscId,
                    out var maximumCharges,
                    out _))
                return maximumCharges;
        }
        return 0;
    }

    private DiabloIntentResult ReadBook(
        AuthoritativePlayerState player,
        DiabloReadBookIntent intent)
    {
        var reference = intent.Item ?? new DiabloPlayerItemReference(
            DiabloPlayerItemLocation.Inventory,
            intent.LegacyInventoryIndex);
        if (IsClosedStashTarget(player, reference))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (!TryGetItem(
                player,
                reference,
                intent.LegacyInventoryIndex,
                out var item)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (reference.Location is not (DiabloPlayerItemLocation.Inventory
            or DiabloPlayerItemLocation.Belt
            or DiabloPlayerItemLocation.Stash)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var state = item.State;
        AuthoritativeItemDefinition? definition = null;
        if (items is not null)
            items.TryGetByState(state, out definition);
        var action = ResolveItemActionKind(state);
        var spellId = state.SpellId != 0 ? state.SpellId : definition?.SpellId ?? 0;
        var minimumMagic = state.MinimumMagic != 0
            ? state.MinimumMagic
            : definition?.MinimumMagic ?? 0;
        if (items is not null
            && (spellId <= 0 || minimumMagic == 0)
            && items.TryGetBookMetadataByMiscId(
                state.MiscId,
                out var contentSpellId,
                out var contentMinimumMagic)) {
            if (spellId <= 0)
                spellId = contentSpellId;
            if (minimumMagic == 0)
                minimumMagic = contentMinimumMagic;
        }
        if (state.Deleted
            || action != DiabloItemActionKind.ReadBook
            || !IsBookItem(state)
            || spellId <= 0
            || spells is null
            || !spells.TryGet(checked((uint)spellId), out var spell)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var previousLevel = player.MemorizedSpells
            .FirstOrDefault(candidate => candidate.SpellId == spell.SpellId)
            ?.Level
            ?? 0;
        var requiredMagic = ResolveBookMinimumMagic(minimumMagic, previousLevel);
        if (player.Attributes.Magic.Current < requiredMagic)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var nextLevel = checked(previousLevel + 1);
        var memorizedIndex = player.MemorizedSpells.FindIndex(
            candidate => candidate.SpellId == spell.SpellId);
        if (nextLevel <= AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel) {
            var learned = new MemorizedSpellState(spell.SpellId, nextLevel);
            if (memorizedIndex >= 0)
                player.MemorizedSpells[memorizedIndex] = learned;
            else
                player.MemorizedSpells.Add(learned);
        }

        if (!AuthoritativeEquipmentManaRules.HasNoManaItem(player.Equipment)) {
            var manaGain = checked(spell.ManaCost * spell.ManaCostScale);
            player.Mana = Math.Min(player.ManaMaximum, checked(player.Mana + manaGain));
        }
        DiabloPlayerItemAccess.Remove(player, reference);
        return DiabloIntentResult.Accepted;
    }

    private bool IsBookItem(AuthoritativeItemState state)
    {
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.GenerationTags.Count > 0)
                return definition.GenerationTags.Contains("Book", StringComparer.OrdinalIgnoreCase);
            if (items.TryGetGenerationTagsByMiscId(state.MiscId, out var tags)
                && tags.Count > 0)
                return tags.Contains("Book", StringComparer.OrdinalIgnoreCase);
        }
        return state.MiscId == 25;
    }

    /** Mirrors Item::updateRequiredStatsCacheForPlayer for the current spell level. */
    private static int ResolveBookMinimumMagic(int baseMinimumMagic, uint spellLevel)
    {
        var minimumMagic = Math.Max(baseMinimumMagic, 0);
        while (spellLevel > 0) {
            minimumMagic = checked(minimumMagic + 20 * minimumMagic / 100);
            spellLevel--;
            if (minimumMagic + 20 * minimumMagic / 100 > byte.MaxValue)
                return byte.MaxValue;
        }
        return minimumMagic;
    }

    private DiabloIntentResult UseConsumable(
        AuthoritativePlayerState player,
        DiabloUseConsumableIntent intent)
    {
        if (IsClosedStashTarget(player, intent.Item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (intent.Item.Location is not (DiabloPlayerItemLocation.Inventory
            or DiabloPlayerItemLocation.Belt
            or DiabloPlayerItemLocation.Stash)
            || !DiabloPlayerItemAccess.TryGet(player, intent.Item, out var item)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (!IsConsumableUsable(item.State))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var action = ResolveItemActionKind(item.State);
        if (item.State.Deleted
            || action is not (DiabloItemActionKind.RestorePartialLife
                or DiabloItemActionKind.RestoreFullLife
                or DiabloItemActionKind.RestorePartialMana
                or DiabloItemActionKind.RestoreFullMana
                or DiabloItemActionKind.RestorePartialLifeAndMana
                or DiabloItemActionKind.RestoreFullLifeAndMana
                or DiabloItemActionKind.IncreaseStrength
                or DiabloItemActionKind.IncreaseMagic
                or DiabloItemActionKind.IncreaseDexterity
                or DiabloItemActionKind.IncreaseVitality
                or DiabloItemActionKind.IncreaseAllAttributes
                or DiabloItemActionKind.ApplyBlacksmithOil
                or DiabloItemActionKind.ApplyAccuracyOil
                or DiabloItemActionKind.ApplySharpnessOil
                or DiabloItemActionKind.ApplyFortitudeOil
                or DiabloItemActionKind.OpenCathedralMap)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        if (IsArenaOnlyItem(item.State)
            && !AuthoritativeSetLevelMapCatalog.IsArenaRuntimeLevel(player.LevelId)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        if (IsOilAction(action))
            return ApplyOil(player, intent, action);

        if (action == DiabloItemActionKind.OpenCathedralMap) {
            return DiabloIntentResult.Accepted with {
                Events = [new DiabloGameplayEvent(
                    DiabloGameplayEventKind.MapOpened,
                    0,
                    player.EntityId,
                    0)],
            };
        }

        switch (action) {
        case DiabloItemActionKind.RestorePartialLife:
            player.Life = Math.Min(
                player.LifeMaximum,
                checked(player.Life + ResolvePartialResource(player.LifeMaximum, player)));
            break;
        case DiabloItemActionKind.RestoreFullLife:
            player.Life = player.LifeMaximum;
            break;
        case DiabloItemActionKind.RestorePartialMana:
            RestorePartialMana(player);
            break;
        case DiabloItemActionKind.RestoreFullMana:
            RestoreFullMana(player);
            break;
        case DiabloItemActionKind.RestorePartialLifeAndMana:
            player.Life = Math.Min(
                player.LifeMaximum,
                checked(player.Life + ResolvePartialResource(player.LifeMaximum, player)));
            RestorePartialMana(player);
            break;
        case DiabloItemActionKind.RestoreFullLifeAndMana:
            player.Life = player.LifeMaximum;
            RestoreFullMana(player);
            break;
        case DiabloItemActionKind.IncreaseStrength:
        case DiabloItemActionKind.IncreaseMagic:
        case DiabloItemActionKind.IncreaseDexterity:
        case DiabloItemActionKind.IncreaseVitality:
            player.ApplyElixir(
                action,
                combatRules.GameVariant == DiabloGameVariant.Hellfire);
            break;
        case DiabloItemActionKind.IncreaseAllAttributes:
            for (var i = 0; i < ResolveItemActionAmount(item.State, action); i++) {
                player.ApplyElixir(DiabloItemActionKind.IncreaseStrength, false);
                player.ApplyElixir(DiabloItemActionKind.IncreaseMagic, false);
                player.ApplyElixir(DiabloItemActionKind.IncreaseDexterity, false);
                player.ApplyElixir(DiabloItemActionKind.IncreaseVitality, false);
            }
            break;
        default:
            throw new InvalidOperationException("Validated item action was not handled.");
        }

        DiabloPlayerItemAccess.Remove(player, intent.Item);
        return DiabloIntentResult.Accepted;
    }

    private int ResolveItemActionAmount(
        AuthoritativeItemState state,
        DiabloItemActionKind action)
    {
        if (items is not null) {
            var hasDefinition = items.TryGetByState(state, out var definition);
            if (hasDefinition)
                return definition.ItemActionAmount;
            if (items.TryGetItemActionMetadataByMiscId(
                    state.MiscId,
                    out var contentAction,
                    out var contentAmount)
                && contentAction == action)
                return contentAmount;
        }

        // Preserve the native Spectral Elixir behavior for states that cannot
        // resolve a stable or native content identity.
        return action == DiabloItemActionKind.IncreaseAllAttributes ? 3 : 1;
    }

    private DiabloItemActionKind ResolveItemActionKind(AuthoritativeItemState state)
    {
        if (state.ItemActionKind != DiabloItemActionKind.None)
            return state.ItemActionKind;
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.ItemActionKind != DiabloItemActionKind.None)
                return definition.ItemActionKind;
            if (items.TryGetItemActionMetadataByMiscId(
                    state.MiscId,
                    out var contentAction,
                    out _))
                return contentAction;
        }
        return DiabloItemActionKind.None;
    }

    private DiabloIntentResult ApplyOil(
        AuthoritativePlayerState player,
        DiabloUseConsumableIntent intent,
        DiabloItemActionKind action)
    {
        if (IsClosedStashTarget(player, intent.Target))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (intent.Target is not { } targetReference
            || targetReference.Location is not (DiabloPlayerItemLocation.Inventory
                or DiabloPlayerItemLocation.Equipment
                or DiabloPlayerItemLocation.Stash)
            || SameLocation(intent.Item, targetReference)
            || !DiabloPlayerItemAccess.TryGet(player, targetReference, out var target)
            || target.State.Deleted
            || IsOilTargetClass(target.State)
            || (action is DiabloItemActionKind.ApplyAccuracyOil
                or DiabloItemActionKind.ApplySharpnessOil)
                && IsArmor(target.State)) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        var state = target.State;
        var maximumDurability = ResolveMaximumDurability(state);
        if (state.MaxDurability == 0 && maximumDurability > 0)
            state = state with { MaxDurability = maximumDurability };
        if (action == DiabloItemActionKind.ApplySharpnessOil) {
            var baseDamage = ResolveBaseDamage(state);
            if (state.MinDamage == 0
                && state.MaxDamage == 0
                && baseDamage.MaximumDamage > 0) {
                state = state with {
                    MinDamage = baseDamage.MinimumDamage,
                    MaxDamage = baseDamage.MaximumDamage,
                };
            }
        }
        switch (action) {
        case DiabloItemActionKind.ApplyBlacksmithOil:
            if (state.MaxDurability != byte.MaxValue) {
                if (state.Durability < state.MaxDurability) {
                    state = state with {
                        Durability = Math.Min(
                            checked(state.Durability + (state.MaxDurability + 4) / 5),
                            state.MaxDurability),
                    };
                } else if (state.MaxDurability < 100) {
                    state = state with {
                        Durability = state.MaxDurability + 1,
                        MaxDurability = state.MaxDurability + 1,
                    };
                }
            }
            break;
        case DiabloItemActionKind.ApplyAccuracyOil:
            if (state.PlusToHit < 50)
                state = state with { PlusToHit = checked(state.PlusToHit + random.RandomIntBetween(1, 2)) };
            break;
        case DiabloItemActionKind.ApplySharpnessOil:
            if (state.MaxDamage - state.MinDamage < 30 && state.MaxDamage < byte.MaxValue)
                state = state with { MaxDamage = state.MaxDamage + 1 };
            break;
        case DiabloItemActionKind.ApplyFortitudeOil:
            if (state.MaxDurability != byte.MaxValue && state.MaxDurability < 200) {
                var increase = random.RandomIntBetween(10, 50);
                state = state with {
                    MaxDurability = checked(state.MaxDurability + increase),
                    Durability = checked(state.Durability + increase),
                };
            }
            break;
        default:
            throw new InvalidOperationException("Validated item action was not an oil action.");
        }

        DiabloPlayerItemAccess.Replace(player, targetReference, target with { State = state });
        DiabloPlayerItemAccess.Remove(player, intent.Item);
        if (targetReference.Location == DiabloPlayerItemLocation.Equipment)
            player.RecalculateEquipmentResources();
        return DiabloIntentResult.Accepted;
    }

    private static bool IsOilAction(DiabloItemActionKind action)
    {
        return action is DiabloItemActionKind.ApplyBlacksmithOil
            or DiabloItemActionKind.ApplyAccuracyOil
            or DiabloItemActionKind.ApplySharpnessOil
            or DiabloItemActionKind.ApplyFortitudeOil;
    }

    private bool IsArenaOnlyItem(AuthoritativeItemState state)
    {
        if (items is not null) {
            var hasDefinition = items.TryGetByState(state, out var definition);
            if (hasDefinition) {
                if (definition.ArenaOnly.HasValue)
                    return definition.ArenaOnly.Value;
                if (string.Equals(definition.StableSymbol, "arena_potion", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            var miscId = state.MiscId != 0
                ? state.MiscId
                : hasDefinition
                    ? definition.MiscId
                    : 0;
            if (items.TryGetArenaOnlyByMiscId(miscId, out var arenaOnly))
                return arenaOnly;
        }
        if (state.MiscId == 55)
            return true;
        return false;
    }

    /** Enforces authored iUsable metadata before applying a consumable action. */
    private bool IsConsumableUsable(AuthoritativeItemState state)
    {
        if (items is null)
            return true;

        if (items.TryGetByState(state, out var definition))
            return definition.Usable ?? true;
        return items.TryGetUsableByMiscId(state.MiscId, out var usable)
            ? usable
            : true;
    }

    private bool IsOilTargetClass(AuthoritativeItemState state)
    {
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.GenerationTags.Count > 0)
                return HasOilTargetTag(definition.GenerationTags);
            if (items.TryGetGenerationTagsByMiscId(state.MiscId, out var tags)
                && tags.Count > 0)
                return HasOilTargetTag(tags);
        }
        return state.ItemClass is 3 or 4 or 5;
    }

    private static bool HasOilTargetTag(IReadOnlySet<string> tags)
        => tags.Contains("Misc", StringComparer.OrdinalIgnoreCase)
            || tags.Contains("Gold", StringComparer.OrdinalIgnoreCase)
            || tags.Contains("Quest", StringComparer.OrdinalIgnoreCase);

    private bool IsArmor(AuthoritativeItemState state)
        => AuthoritativeItemClassification.IsArmor(state, items);

    private void RestorePartialMana(AuthoritativePlayerState player)
    {
        if (AuthoritativeEquipmentManaRules.HasNoManaItem(player.Equipment))
            return;
        player.Mana = Math.Min(
            player.ManaMaximum,
            checked(player.Mana + ResolvePartialResource(player.ManaMaximum, player, mana: true)));
    }

    private void RestoreFullMana(AuthoritativePlayerState player)
    {
        if (!AuthoritativeEquipmentManaRules.HasNoManaItem(player.Equipment))
            player.Mana = player.ManaMaximum;
    }

    private int ResolvePartialResource(
        int maximumQ6,
        AuthoritativePlayerState player,
        bool mana = false)
    {
        var wholePoints = Math.Max(maximumQ6, 0) >> 6;
        var amount = checked((wholePoints / 8 + random.RandomIntLessThan(wholePoints / 4)) << 6);
        var classSymbol = player.PlayerClass?.Symbol;
        if (mana) {
            if (string.Equals(classSymbol, "sorcerer", StringComparison.OrdinalIgnoreCase))
                amount = checked(amount * 2);
        } else if (string.Equals(classSymbol, "warrior", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classSymbol, "barbarian", StringComparison.OrdinalIgnoreCase)) {
            amount = checked(amount * 2);
        }
        if (string.Equals(classSymbol, "rogue", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classSymbol, "monk", StringComparison.OrdinalIgnoreCase)
            || string.Equals(classSymbol, "bard", StringComparison.OrdinalIgnoreCase)) {
            amount = checked(amount + amount / 2);
        }
        return amount;
    }

    private DiabloIntentResult Identify(
        AuthoritativePlayerState player,
        DiabloIdentifyItemIntent intent)
    {
        if (!Enum.IsDefined(intent.Source))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.Malformed);
        if (IsClosedStashTarget(player, intent.Item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        var isClassAbility = intent.Source == DiabloItemServiceSourceKind.ClassAbility;
        var isConsumableItem = intent.Source == DiabloItemServiceSourceKind.ConsumableItem;
        if (isClassAbility) {
            if (!player.HasAbility(
                    DiabloPlayerAbilityKind.Identify,
                    identifyAbilitySpellId)) {
                return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            }
        } else if (!isConsumableItem && !AllowsPaidIdentification(player)) {
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
            : isConsumableItem
                ? ValidateConsumableIdentification(player, intent, item)
                : rules.ValidateIdentification(item, player.Gold);
        if (rejection.HasValue)
            return DiabloIntentResult.Rejected(rejection.Value);
        if (isConsumableItem
            && intent.SourceItem is { } sourceReference
            && SameLocation(sourceReference, intent.Item ?? new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Inventory,
                intent.LegacyInventoryIndex))) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var hasGoldTopology = false;
        if (!isClassAbility
            && !isConsumableItem
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

        if (isConsumableItem) {
            RemoveItem(
                player,
                intent.SourceItem,
                uint.MaxValue);
            return DiabloIntentResult.Accepted;
        }

        if (!SpendGold(player, rules.GetIdentificationPrice(item), hasGoldTopology))
            throw new InvalidOperationException("Validated identification Gold could not be removed.");
        return DiabloIntentResult.Accepted;
    }

    private DiabloRuleFailure? ValidateConsumableIdentification(
        AuthoritativePlayerState player,
        DiabloIdentifyItemIntent intent,
        OwnedStoreItem target)
    {
        if (intent.SourceItem is not { } sourceReference
            || !DiabloPlayerItemAccess.TryGet(player, sourceReference, out var source)
            || source.State.Deleted
            || !IsConsumableUsable(source.State)
            || ResolveItemActionKind(source.State) != DiabloItemActionKind.Identify
            || sourceReference.Location is not (DiabloPlayerItemLocation.Inventory
                or DiabloPlayerItemLocation.Belt))
            return DiabloRuleFailure.NotAllowed;
        if (target.State.Deleted || target.State.Magical == 0)
            return DiabloRuleFailure.InvalidTarget;
        return target.State.Identified
            ? DiabloRuleFailure.NotAllowed
            : null;
    }

    private DiabloIntentResult RefillMana(AuthoritativePlayerState player)
    {
        if (player.ActiveStoreId != adriaStoreId || player.LevelId != townLevelId)
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

    /** Allows paid Identify only through Cain or legacy generic fixtures. */
    private bool AllowsPaidIdentification(AuthoritativePlayerState player)
    {
        if (player.ActiveStoreId is not uint storeId
            || storeId == adriaStoreId
            || player.LevelId != townLevelId)
            return false;
        if (storeId == storytellerStoreId)
            return true;
        return catalog.TryGetRole(storeId, out var role)
            && role == AuthoritativeStoreRole.Generic;
    }

    private static DiabloIntentResult MoveInventoryItem(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? reference,
        uint inventoryIndex,
        uint targetCell)
    {
        if (reference is { Location: not DiabloPlayerItemLocation.Inventory }
            || (reference is not null
                && (!DiabloPlayerItemAccess.TryGet(player, reference.Value, out _)
                    || reference.Value.Slot >= player.Inventory.Count)))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var resolvedIndex = reference?.Slot ?? inventoryIndex;
        if (!TryGetInventoryItem(player, resolvedIndex, out var item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!InventoryGridLayout.CanPlace(
                player.InventoryGrid,
                targetCell,
                item.State.InventoryWidth,
                item.State.InventoryHeight,
                (int)resolvedIndex))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        InventoryGridLayout.Clear(player.InventoryGrid, (int)resolvedIndex);
        InventoryGridLayout.Fill(
            player.InventoryGrid,
            (int)targetCell,
            (int)resolvedIndex,
            item.State.InventoryWidth,
            item.State.InventoryHeight);
        return DiabloIntentResult.Accepted;
    }

    private DiabloIntentResult MoveItem(
        AuthoritativePlayerState player,
        DiabloMoveItemIntent intent)
    {
        if ((intent.Item.Location == DiabloPlayerItemLocation.Stash
                || intent.Destination.Location == DiabloPlayerItemLocation.Stash)
            && !player.StashOpen) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!TryGetTransfer(player, intent.Item, out var source))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (DiabloGoldInventory.HasGoldIdentity(source.State, combatRules, items)
            && intent.Destination.Location != DiabloPlayerItemLocation.Inventory) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!IsValidDestination(player, intent.Destination, source.State, intent.Item))
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (SameLocation(intent.Item, intent.Destination))
            return DiabloIntentResult.Accepted;

        var destinationIndex = intent.Destination.Location == DiabloPlayerItemLocation.Inventory
            ? InventoryGridLayout.GetItemIndexAt(player.InventoryGrid, intent.Destination.Slot)
            : -1;
        if (intent.Destination.Location == DiabloPlayerItemLocation.Stash) {
            var stashDestination = GetStashItemAt(
                player,
                intent.Destination.Page,
                intent.Destination.Slot);
            if (stashDestination is not null) {
                var destinationReference = new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    stashDestination.Cell,
                    stashDestination.State.StableItemId,
                    stashDestination.Page);
                if (!TryGetTransfer(player, destinationReference, out var destination))
                    return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                return SwapTransfers(
                    player,
                    intent.Item,
                    intent.Destination,
                    source,
                    destinationReference,
                    destination);
            }
        }
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

    private DiabloIntentResult AutoEquipItem(
        AuthoritativePlayerState player,
        DiabloAutoEquipItemIntent intent)
    {
        if (intent.Item.Location != DiabloPlayerItemLocation.Inventory
            || !TryGetTransfer(player, intent.Item, out var source)
            || source.State.Deleted
            || !source.State.StatFlag) {
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (player.CombatMode > DiabloPlayerCombatMode.WalkSideways)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var kind = AuthoritativeItemClassification.GetEquipmentSlotKind(source.State, items);
        if (kind == DiabloEquipmentSlotKind.Unknown)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        var displacementSlots = new List<uint>();
        var targetSlot = ResolveAutoEquipTarget(
            player,
            source.State,
            kind,
            intent.EquipmentSlot,
            displacementSlots);
        if (targetSlot < 0)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var plannedCells = PlanDisplacedInventoryCells(
            player,
            intent.Item.Slot,
            displacementSlots,
            out var plannedDisplacements);
        if (plannedCells is null)
            return DiabloIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        RemoveTransfer(player, intent.Item);
        foreach (var displacement in plannedDisplacements)
            RemoveTransfer(
                player,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Equipment,
                    displacement.Slot));

        AddTransfer(
            player,
            new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Equipment,
                checked((uint)targetSlot)),
            source);
        for (var index = 0; index < plannedDisplacements.Count; index++)
            AddTransfer(
                player,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    checked((uint)plannedCells[index])),
                plannedDisplacements[index].Item);
        player.RecalculateEquipmentResources();
        return DiabloIntentResult.Accepted;
    }

    private int ResolveAutoEquipTarget(
        AuthoritativePlayerState player,
        AuthoritativeItemState state,
        DiabloEquipmentSlotKind kind,
        uint? requestedSlot,
        List<uint> displacementSlots)
    {
        if (requestedSlot.HasValue)
            return ResolveRequestedAutoEquipTarget(
                player,
                state,
                kind,
                requestedSlot.Value,
                displacementSlots);

        if (kind is DiabloEquipmentSlotKind.Helmet
            or DiabloEquipmentSlotKind.BodyArmor
            or DiabloEquipmentSlotKind.Amulet) {
            var target = kind switch {
                DiabloEquipmentSlotKind.Helmet => 0U,
                DiabloEquipmentSlotKind.BodyArmor => 6U,
                _ => 3U,
            };
            if (TryGetEquipment(player, target, out _))
                displacementSlots.Add(target);
            return checked((int)target);
        }

        if (kind == DiabloEquipmentSlotKind.Ring) {
            if (!TryGetEquipment(player, 1, out _))
                return 1;
            if (!TryGetEquipment(player, 2, out _))
                return 2;
            return -1;
        }

        if (kind == DiabloEquipmentSlotKind.TwoHanded) {
            var leftOccupied = TryGetEquipment(player, 4, out _);
            var rightOccupied = TryGetEquipment(player, 5, out _);
            if (!leftOccupied && !rightOccupied)
                return 4;
            if (!rightOccupied) {
                displacementSlots.Add(4);
                return 4;
            }
            if (!leftOccupied) {
                displacementSlots.Add(5);
                return 4;
            }

            // Native ChangeInvCut prefers moving the off-hand first, then the main
            // hand; PlanDisplacedInventoryCells preserves that order and capacity
            // decision. If the first item cannot fit, the native fallback reverses it.
            displacementSlots.Add(5);
            displacementSlots.Add(4);
            return 4;
        }

        if (kind != DiabloEquipmentSlotKind.Hand)
            return -1;

        var leftItem = TryGetEquipment(player, 4, out var leftEquipped) ? leftEquipped : null;
        var rightItem = TryGetEquipment(player, 5, out var rightEquipped) ? rightEquipped : null;
        if (leftItem is null)
            return 4;
        if (rightItem is null) {
            if (IsSameHandClassification(state, leftItem.State)
                || AuthoritativeItemClassification.IsTwoHanded(leftItem.State, items)) {
                displacementSlots.Add(4);
                return 4;
            }
            return 5;
        }
        if (IsSameHandClassification(state, leftItem.State)
            || AuthoritativeItemClassification.IsTwoHanded(leftItem.State, items)) {
            displacementSlots.Add(4);
            return 4;
        }
        if (IsSameHandClassification(state, rightItem.State)) {
            displacementSlots.Add(5);
            return 5;
        }
        return -1;
    }

    private int ResolveRequestedAutoEquipTarget(
        AuthoritativePlayerState player,
        AuthoritativeItemState state,
        DiabloEquipmentSlotKind kind,
        uint requestedSlot,
        List<uint> displacementSlots)
    {
        if (!AuthoritativeItemClassification.IsCompatibleWithEquipmentSlot(kind, requestedSlot))
            return -1;

        if (kind == DiabloEquipmentSlotKind.TwoHanded)
            return ResolveAutoEquipTarget(player, state, kind, null, displacementSlots);

        if (kind != DiabloEquipmentSlotKind.Hand) {
            if (TryGetEquipment(player, requestedSlot, out _))
                displacementSlots.Add(requestedSlot);
            return checked((int)requestedSlot);
        }

        var selectedSlot = requestedSlot;
        var otherSlot = selectedSlot == 4 ? 5U : 4U;
        var hasOther = TryGetEquipment(player, otherSlot, out var otherItem);
        var dualWield = player.PlayerClass?.Flags.HasFlag(DiabloPlayerClassFlags.DualWield) == true;
        var pasteIntoSelected = !hasOther
            || !IsSameHandClassification(otherItem!.State, state)
            || (dualWield
                && AuthoritativeItemClassification.IsWeapon(otherItem.State, items)
                && AuthoritativeItemClassification.IsWeapon(state, items));
        var dequipTwoHanded = hasOther
            && AuthoritativeItemClassification.IsTwoHanded(otherItem!.State, items);
        var pasteSlot = pasteIntoSelected ? selectedSlot : otherSlot;
        var displacementSlot = dequipTwoHanded ? otherSlot : pasteSlot;
        if (TryGetEquipment(player, displacementSlot, out _))
            displacementSlots.Add(displacementSlot);
        return checked((int)pasteSlot);
    }

    private bool IsSameHandClassification(
        AuthoritativeItemState left,
        AuthoritativeItemState right)
    {
        return AuthoritativeItemClassification.IsWeapon(left, items)
            == AuthoritativeItemClassification.IsWeapon(right, items);
    }

    private List<int>? PlanDisplacedInventoryCells(
        AuthoritativePlayerState player,
        uint sourceInventoryIndex,
        IReadOnlyList<uint> displacementSlots,
        out List<(uint Slot, TransferItem Item)> displacements)
    {
        displacements = [];
        var planningGrid = player.InventoryGrid.ToList();
        if (sourceInventoryIndex >= (uint)player.Inventory.Count)
            return null;
        InventoryGridLayout.Clear(planningGrid, checked((int)sourceInventoryIndex));

        foreach (var slot in displacementSlots) {
            if (!TryGetTransfer(
                    player,
                    new DiabloPlayerItemReference(
                        DiabloPlayerItemLocation.Equipment,
                        slot),
                    out var displacement)) {
                displacements.Clear();
                return null;
            }
            var targetCell = InventoryGridLayout.FindNativePlacement(
                planningGrid,
                displacement.State.InventoryWidth,
                displacement.State.InventoryHeight);
            if (targetCell < 0) {
                // Match native two-handed auto-equip's one permitted fallback: if
                // the first displacement fails, try the reverse order once.
                if (displacements.Count == 0 && displacementSlots.Count == 2)
                    return PlanDisplacedInventoryCellsReversed(
                        player,
                        sourceInventoryIndex,
                        displacementSlots,
                        out displacements);
                displacements.Clear();
                return null;
            }
            displacements.Add((slot, displacement));
            InventoryGridLayout.Fill(
                planningGrid,
                targetCell,
                player.Inventory.Count + displacements.Count,
                displacement.State.InventoryWidth,
                displacement.State.InventoryHeight);
        }

        return FindPlannedCells(player, sourceInventoryIndex, displacements);
    }

    private List<int>? PlanDisplacedInventoryCellsReversed(
        AuthoritativePlayerState player,
        uint sourceInventoryIndex,
        IReadOnlyList<uint> displacementSlots,
        out List<(uint Slot, TransferItem Item)> displacements)
    {
        var reversed = displacementSlots.Reverse().ToArray();
        var cells = PlanDisplacedInventoryCellsWithoutFallback(
            player,
            sourceInventoryIndex,
            reversed,
            out displacements);
        return cells;
    }

    private List<int>? PlanDisplacedInventoryCellsWithoutFallback(
        AuthoritativePlayerState player,
        uint sourceInventoryIndex,
        IReadOnlyList<uint> displacementSlots,
        out List<(uint Slot, TransferItem Item)> displacements)
    {
        displacements = [];
        var planningGrid = player.InventoryGrid.ToList();
        InventoryGridLayout.Clear(planningGrid, checked((int)sourceInventoryIndex));
        var cells = new List<int>(displacementSlots.Count);
        foreach (var slot in displacementSlots) {
            if (!TryGetTransfer(
                    player,
                    new DiabloPlayerItemReference(
                        DiabloPlayerItemLocation.Equipment,
                        slot),
                    out var displacement))
                return null;
            var targetCell = InventoryGridLayout.FindNativePlacement(
                planningGrid,
                displacement.State.InventoryWidth,
                displacement.State.InventoryHeight);
            if (targetCell < 0)
                return null;
            cells.Add(targetCell);
            displacements.Add((slot, displacement));
            InventoryGridLayout.Fill(
                planningGrid,
                targetCell,
                player.Inventory.Count + displacements.Count,
                displacement.State.InventoryWidth,
                displacement.State.InventoryHeight);
        }
        return cells;
    }

    private static List<int> FindPlannedCells(
        AuthoritativePlayerState player,
        uint sourceInventoryIndex,
        IReadOnlyList<(uint Slot, TransferItem Item)> displacements)
    {
        var planningGrid = player.InventoryGrid.ToList();
        InventoryGridLayout.Clear(planningGrid, checked((int)sourceInventoryIndex));
        var cells = new List<int>(displacements.Count);
        foreach (var displacement in displacements) {
            var targetCell = InventoryGridLayout.FindNativePlacement(
                planningGrid,
                displacement.Item.State.InventoryWidth,
                displacement.Item.State.InventoryHeight);
            if (targetCell < 0)
                throw new InvalidOperationException("Planned displacement no longer fits.");
            cells.Add(targetCell);
            InventoryGridLayout.Fill(
                planningGrid,
                targetCell,
                player.Inventory.Count + cells.Count,
                displacement.Item.State.InventoryWidth,
                displacement.Item.State.InventoryHeight);
        }
        return cells;
    }

    private static bool TryGetEquipment(
        AuthoritativePlayerState player,
        uint slot,
        out EquippedStoreItem item)
    {
        item = player.Equipment.FirstOrDefault(candidate => candidate.Slot == slot)!;
        return item is not null;
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
        } else if (reference.Location == DiabloPlayerItemLocation.Equipment) {
            var index = player.Equipment.FindIndex(candidate => candidate.Slot == reference.Slot);
            player.Equipment[index] = new EquippedStoreItem(reference.Slot, item.ItemSeed, item.State);
        } else {
            var index = player.Stash.FindIndex(candidate =>
                candidate.Page == reference.Page && candidate.Cell == reference.Slot);
            player.Stash[index] = new StashStoreItem(
                reference.Page,
                reference.Slot,
                item.StoreId,
                item.StoreSlot,
                item.ItemSeed,
                item.Price,
                item.PurchasedAtTick,
                item.State);
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
            return MatchesStableIdentity(owned.State, reference);
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
                return MatchesStableIdentity(belt.State, reference);
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
                return MatchesStableIdentity(equipment.State, reference);
            }
            break;
        case DiabloPlayerItemLocation.Stash:
            var stash = player.Stash.FirstOrDefault(candidate =>
                candidate.Page == reference.Page && candidate.Cell == reference.Slot);
            if (stash is not null) {
                item = new TransferItem(
                    stash.ItemSeed,
                    stash.State,
                    stash.StoreId,
                    stash.StoreSlot,
                    stash.Price,
                    stash.PurchasedAtTick);
                return MatchesStableIdentity(stash.State, reference);
            }
            break;
        }

        item = default;
        return false;
    }

    private static bool MatchesStableIdentity(
        AuthoritativeItemState state,
        DiabloPlayerItemReference reference)
    {
        return reference.StableItemId == 0
            || state.StableItemId == reference.StableItemId;
    }

    private bool IsValidDestination(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference,
        AuthoritativeItemState state,
        DiabloPlayerItemReference? ignoredReference = null)
    {
        return reference.Location switch {
            DiabloPlayerItemLocation.Inventory => InventoryGridLayout.IsWithinBounds(
                player.InventoryGrid.Count,
                reference.Slot,
                state.InventoryWidth,
                state.InventoryHeight),
            DiabloPlayerItemLocation.Belt => reference.Slot < 8
                && state.InventoryWidth == 1
                && state.InventoryHeight == 1,
            DiabloPlayerItemLocation.Equipment => IsValidEquipmentDestination(reference.Slot, state),
            DiabloPlayerItemLocation.Stash => IsValidStashDestination(
                player,
                reference,
                state,
                ignoredReference),
            _ => false,
        };
    }

    private static bool IsValidStashDestination(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference,
        AuthoritativeItemState state,
        DiabloPlayerItemReference? ignoredReference)
    {
        if (reference.Page >= DiabloStashLayout.PageCount
            || reference.Slot >= DiabloStashLayout.CellCount)
            return false;
        var occupied = GetStashItemAt(player, reference.Page, reference.Slot);
        var ignored = ignoredReference;
        if (occupied is not null) {
            ignored = new DiabloPlayerItemReference(
                DiabloPlayerItemLocation.Stash,
                occupied.Cell,
                occupied.State.StableItemId,
                occupied.Page);
        }
        return DiabloStashLayout.CanPlace(
            GetStashPageGrid(player, reference.Page, ignored),
            reference.Slot,
            state.InventoryWidth,
            state.InventoryHeight);
    }

    private bool IsValidEquipmentDestination(
        uint slot,
        AuthoritativeItemState state)
    {
        if (slot >= 7)
            return false;

        // Catalog-less simulations retain the historical permissive behavior while
        // content-backed sessions enforce the same slot topology as loadout validation.
        if (items is null)
            return true;

        if (!items.TryGetByState(state, out _)
            && !items.TryGetGenerationTagsByMiscId(state.MiscId, out _)) {
            return true;
        }

        return AuthoritativeItemClassification.IsCompatibleWithEquipmentSlot(
            AuthoritativeItemClassification.GetEquipmentSlotKind(state, items),
            slot);
    }

    private static bool DestinationSlotOccupied(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference reference)
    {
        return reference.Location switch {
            DiabloPlayerItemLocation.Belt => player.Belt.Any(item => item.Slot == reference.Slot),
            DiabloPlayerItemLocation.Equipment => player.Equipment.Any(item => item.Slot == reference.Slot),
            DiabloPlayerItemLocation.Stash => player.Stash.Any(item =>
                item.Page == reference.Page && item.Cell == reference.Slot),
            _ => false,
        };
    }

    private static int[] GetStashPageGrid(
        AuthoritativePlayerState player,
        uint page,
        DiabloPlayerItemReference? ignoredReference)
    {
        var grid = DiabloStashLayout.CreateEmptyPage();
        foreach (var item in player.Stash.Where(item =>
                     item.Page == page
                     && (ignoredReference is null
                         || ignoredReference.Value.Location != DiabloPlayerItemLocation.Stash
                         || ignoredReference.Value.Page != item.Page
                         || ignoredReference.Value.Slot != item.Cell))) {
            if (DiabloStashLayout.CanPlace(
                    grid,
                    item.Cell,
                    item.State.InventoryWidth,
                    item.State.InventoryHeight)) {
                DiabloStashLayout.Fill(
                    grid,
                    item.Cell,
                    item.State.InventoryWidth,
                    item.State.InventoryHeight);
            }
        }
        return grid;
    }

    private static StashStoreItem? GetStashItemAt(
        AuthoritativePlayerState player,
        uint page,
        uint cell)
    {
        if (page >= DiabloStashLayout.PageCount || cell >= DiabloStashLayout.CellCount)
            return null;
        var grid = DiabloStashLayout.CreateEmptyPage();
        var items = player.Stash
            .Where(item => item.Page == page)
            .OrderBy(item => item.Cell)
            .ToArray();
        for (var index = 0; index < items.Length; index++) {
            if (!DiabloStashLayout.CanPlace(
                    grid,
                    items[index].Cell,
                    items[index].State.InventoryWidth,
                    items[index].State.InventoryHeight))
                continue;
            DiabloStashLayout.Fill(
                grid,
                items[index].Cell,
                items[index].State.InventoryWidth,
                items[index].State.InventoryHeight,
                index);
        }
        var itemIndex = grid[(int)cell];
        return itemIndex >= 0 && itemIndex < items.Length
            ? items[itemIndex]
            : items.FirstOrDefault(item => item.Cell == cell);
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
        return source.Location == destination.Location
            && source.Slot == destination.Slot
            && source.Page == destination.Page;
    }

    private static bool IsClosedStashTarget(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? reference)
    {
        return !player.StashOpen
            && reference?.Location == DiabloPlayerItemLocation.Stash;
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
        } else if (reference.Location == DiabloPlayerItemLocation.Equipment) {
            player.Equipment.RemoveAll(item => item.Slot == reference.Slot);
        } else {
            player.Stash.RemoveAll(item =>
                item.Page == reference.Page && item.Cell == reference.Slot);
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
        } else if (destination.Location == DiabloPlayerItemLocation.Equipment) {
            player.Equipment.Add(new EquippedStoreItem(destination.Slot, item.ItemSeed, item.State));
        } else {
            player.Stash.Add(new StashStoreItem(
                destination.Page,
                destination.Slot,
                item.StoreId,
                item.StoreSlot,
                item.ItemSeed,
                item.Price,
                item.PurchasedAtTick,
                item.State));
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

    private uint AllocateNonzeroGoldStackSeed()
    {
        uint seed;
        do {
            seed = AllocateGoldStackSeed();
        } while (seed == 0);
        return seed;
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
