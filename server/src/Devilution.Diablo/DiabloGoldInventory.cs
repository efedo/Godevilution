using Devilution.Domain.Inventory;

namespace Devilution.Diablo;

/** Native-compatible Gold-stack recognition and mutation shared by world and death rules. */
internal static class DiabloGoldInventory
{
    public static bool TryGetTopologyTotal(
        AuthoritativePlayerState player,
        AuthoritativeCombatRules rules,
        bool allowTruncatedWalletRemainder,
        out uint total,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rules);

        var found = false;
        var sum = 0U;
        items ??= player.Items;
        for (var index = 0; index < player.Inventory.Count; index++) {
            var item = player.Inventory[index];
            if (!HasGoldIdentity(item.State, rules, items))
                continue;
            if (!IsValidStack(item, rules, items)
                || InventoryGridLayout.FindAnchor(player.InventoryGrid, index) < 0) {
                total = 0;
                return false;
            }
            found = true;
            sum = checked(sum + item.Price);
        }

        total = sum;
        if (!found) {
            // A native-sized empty grid is canonical zero-Gold topology. Older
            // aggregate compatibility fixtures commonly omit the grid entirely.
            return player.GoldTopologyEnabled
                && player.Gold == 0
                && player.InventoryGrid.Count == 40;
        }
        if (sum < player.Gold)
            return false;
        if (sum == player.Gold)
            return true;
        var truncatedPhysicalMinimum = checked(
            (ulong)player.Gold * (uint)(rules.DeathGoldDivisor - 1));
        return allowTruncatedWalletRemainder
            && sum >= truncatedPhysicalMinimum
            && sum - truncatedPhysicalMinimum < checked((uint)rules.DeathGoldDivisor);
    }

    public static bool HasGoldIdentity(
        AuthoritativeItemState state,
        AuthoritativeCombatRules rules,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        return AuthoritativeItemClassification.IsGold(state, rules, items);
    }

    /** Removes Gold in native store order: nonmaximum piles first, then all piles. */
    public static bool TrySpend(
        AuthoritativePlayerState player,
        uint value,
        AuthoritativeCombatRules rules,
        bool allowTruncatedWalletRemainder = false,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rules);
        if (value > player.Gold
            || !TryGetTopologyTotal(
                player,
                rules,
                allowTruncatedWalletRemainder,
                out _,
                items)) {
            return false;
        }

        var remaining = value;
        SpendPass(player, rules, skipMaximumPiles: true, ref remaining, items);
        SpendPass(player, rules, skipMaximumPiles: false, ref remaining, items);
        if (remaining != 0)
            throw new InvalidOperationException("Validated Gold topology could not satisfy the spend.");
        player.Gold -= value;
        return true;
    }

    /**
     * Adds Gold using native pile and cell order. Capacity is proven before any
     * mutation or RNG allocation, so rejection is transactional.
     */
    public static bool TryAdd(
        AuthoritativePlayerState player,
        uint value,
        AuthoritativeCombatRules rules,
        Func<uint> allocateItemSeed,
        bool allowTruncatedWalletRemainder,
        bool recalculateWalletFromTopology,
        AuthoritativeItemCatalog? items = null)
    {
        return TryAddCore(
                player,
                value,
                rules,
                allocateItemSeed,
                allowTruncatedWalletRemainder,
                recalculateWalletFromTopology,
                items,
                requireFullCapacity: true,
                out var remainder)
            && remainder == 0;
    }

    /**
     * Adds as much Gold as native inventory capacity permits and returns the
     * remainder that must stay on the pickup cursor.
     */
    public static bool TryAddPartial(
        AuthoritativePlayerState player,
        uint value,
        AuthoritativeCombatRules rules,
        Func<uint> allocateItemSeed,
        bool allowTruncatedWalletRemainder,
        bool recalculateWalletFromTopology,
        out uint remainder,
        AuthoritativeItemCatalog? items = null)
    {
        return TryAddCore(
            player,
            value,
            rules,
            allocateItemSeed,
            allowTruncatedWalletRemainder,
            recalculateWalletFromTopology,
            items,
            requireFullCapacity: false,
            out remainder);
    }

    private static bool TryAddCore(
        AuthoritativePlayerState player,
        uint value,
        AuthoritativeCombatRules rules,
        Func<uint> allocateItemSeed,
        bool allowTruncatedWalletRemainder,
        bool recalculateWalletFromTopology,
        AuthoritativeItemCatalog? items,
        bool requireFullCapacity,
        out uint remainder)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(allocateItemSeed);
        remainder = value;
        if (!TryGetTopologyTotal(
                player,
                rules,
                allowTruncatedWalletRemainder,
                out var topologyTotal,
                items)
            || uint.MaxValue - topologyTotal < value) {
            return false;
        }

        var maximumStack = checked((uint)rules.DeathGoldMaximumStack);
        var capacity = 0UL;
        foreach (var candidate in player.Inventory) {
            if (IsValidStack(candidate, rules, items ?? player.Items))
                capacity += maximumStack - candidate.Price;
        }
        var emptyCells = EnumerateNativePlacementCells(player.InventoryGrid)
            .Where(cell => player.InventoryGrid[cell] == -1)
            .Take(Math.Max(0, 40 - player.Inventory.Count))
            .ToArray();
        capacity += checked((ulong)emptyCells.Length * maximumStack);
        if (requireFullCapacity && value > capacity)
            return false;

        var accepted = checked((uint)Math.Min((ulong)value, capacity));
        var remainingToPlace = accepted;
        for (var index = 0; index < player.Inventory.Count && remainingToPlace > 0; index++) {
            var candidate = player.Inventory[index];
            if (!IsValidStack(candidate, rules, items ?? player.Items)
                || candidate.Price == maximumStack)
                continue;
            var added = Math.Min(remainingToPlace, maximumStack - candidate.Price);
            player.Inventory[index] = WithValue(
                candidate,
                candidate.Price + added,
                rules,
                items ?? player.Items);
            remainingToPlace -= added;
        }

        foreach (var cell in emptyCells) {
            if (remainingToPlace == 0)
                break;
            var stackValue = Math.Min(remainingToPlace, maximumStack);
            var inventoryIndex = player.Inventory.Count;
            player.Inventory.Add(CreateStack(
                AllocateNonzeroSeed(allocateItemSeed),
                stackValue,
                rules,
                items ?? player.Items));
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                cell,
                inventoryIndex,
                1,
                1);
            remainingToPlace -= stackValue;
        }

        if (remainingToPlace != 0)
            throw new InvalidOperationException("Validated Gold capacity could not accept the addition.");
        remainder = value - accepted;
        player.Gold = recalculateWalletFromTopology
            ? checked(topologyTotal + accepted)
            : checked(player.Gold + accepted);
        return true;
    }

    public static bool IsValidStack(
        OwnedStoreItem item,
        AuthoritativeCombatRules rules,
        AuthoritativeItemCatalog? items = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rules);
        return item.ItemSeed != 0
            && HasGoldIdentity(item.State, rules, items)
            && item.State.Value > 0
            && item.State.Value <= rules.DeathGoldMaximumStack
            && item.State.IdentifiedValue == item.State.Value
            && item.Price == checked((uint)item.State.Value)
            && item.State.StatFlag
            && item.State.InventoryWidth == 1
            && item.State.InventoryHeight == 1;
    }

    public static OwnedStoreItem CreateStack(
        uint itemSeed,
        uint value,
        AuthoritativeCombatRules rules,
        AuthoritativeItemCatalog? items = null)
    {
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        if (value == 0 || value > rules.DeathGoldMaximumStack)
            throw new ArgumentOutOfRangeException(nameof(value));
        return new OwnedStoreItem(
            0,
            0,
            itemSeed,
            value,
            0,
            CreateState(value, rules, items));
    }

    public static OwnedStoreItem WithValue(
        OwnedStoreItem item,
        uint value,
        AuthoritativeCombatRules rules,
        AuthoritativeItemCatalog? items = null)
    {
        if (!IsValidStack(item, rules, items))
            throw new ArgumentException("The item is not a valid Gold stack.", nameof(item));
        if (value == 0 || value > rules.DeathGoldMaximumStack)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (items?.NativeGoldStackMetadata is { } nativeGold) {
            nativeGold.ValidateExistingStack(item.State, item.Price, rules);
            return item with {
                Price = value,
                State = item.State with {
                    Value = checked((int)value),
                    IdentifiedValue = checked((int)value),
                    NativeCursorGraphic = nativeGold.CursorFor(value),
                },
            };
        }
        return item with {
            Price = value,
            State = item.State with {
                Value = checked((int)value),
                IdentifiedValue = checked((int)value),
            },
        };
    }

    public static AuthoritativeItemState CreateState(
        uint value,
        AuthoritativeCombatRules rules,
        AuthoritativeItemCatalog? items = null)
    {
        if (value == 0 || value > rules.DeathGoldMaximumStack)
            throw new ArgumentOutOfRangeException(nameof(value));
        var itemType = rules.DeathGoldItemType;
        var itemIndex = rules.DeathGoldItemIndex;
        AuthoritativeItemDefinition? definition = null;
        var nativeGold = items?.NativeGoldStackMetadata;
        if (nativeGold is not null) {
            if (items is null || !items.TryGet(nativeGold.StableItemId, out var nativeDefinition)
                || nativeDefinition.NativeItemIndex != nativeGold.NativeItemIndex
                || nativeDefinition.ItemType != nativeGold.NativeItemType
                || !AuthoritativeItemClassification.IsGold(nativeDefinition))
                throw new InvalidDataException("Native Gold metadata has no matching admitted item definition.");
            definition = nativeDefinition;
            itemType = nativeGold.NativeItemType;
            itemIndex = nativeGold.NativeItemIndex;
        } else if (items is not null) {
            definition = items.Definitions.FirstOrDefault(item =>
                item.GenerationTags.Contains("Gold")
                || item.GenerationTags.Contains("Currency"));
            if (definition is not null) {
                itemType = definition.ItemType;
                itemIndex = definition.NativeItemIndex;
            }
        }
        var state = AuthoritativeItemState.Empty with {
            ItemType = itemType,
            StableItemId = definition?.ItemId ?? 0,
            Value = checked((int)value),
            IdentifiedValue = checked((int)value),
            ItemIndex = itemIndex,
            StatFlag = true,
        };
        if (nativeGold is not null)
            state = nativeGold.CompleteNewStack(state, value, rules);
        return state;
    }

    private static void SpendPass(
        AuthoritativePlayerState player,
        AuthoritativeCombatRules rules,
        bool skipMaximumPiles,
        ref uint remaining,
        AuthoritativeItemCatalog? items)
    {
        var maximumStack = checked((uint)rules.DeathGoldMaximumStack);
        for (var index = 0; index < player.Inventory.Count && remaining > 0;) {
            var candidate = player.Inventory[index];
            if (!IsValidStack(candidate, rules, items ?? player.Items)
                || (skipMaximumPiles && candidate.Price == maximumStack)) {
                index++;
                continue;
            }
            if (remaining < candidate.Price) {
                player.Inventory[index] = WithValue(
                    candidate,
                    candidate.Price - remaining,
                    rules,
                    items ?? player.Items);
                remaining = 0;
                return;
            }

            remaining -= candidate.Price;
            DiabloPlayerItemAccess.Remove(
                player,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    checked((uint)index)));
        }
    }

    private static IEnumerable<int> EnumerateNativePlacementCells(
        IReadOnlyList<int> inventoryGrid)
    {
        for (var cell = Math.Min(39, inventoryGrid.Count - 1); cell >= 30; cell--)
            yield return cell;
        for (var x = 9; x >= 0; x--) {
            for (var y = 2; y >= 0; y--) {
                var cell = y * 10 + x;
                if (cell < inventoryGrid.Count)
                    yield return cell;
            }
        }
    }

    private static uint AllocateNonzeroSeed(Func<uint> allocateItemSeed)
    {
        uint itemSeed;
        do {
            itemSeed = allocateItemSeed();
        } while (itemSeed == 0);
        return itemSeed;
    }
}
