using Devilution.Domain.Inventory;

namespace Devilution.Diablo;

/** Native-compatible Gold-stack recognition and mutation shared by world and death rules. */
internal static class DiabloGoldInventory
{
    public static bool TryGetTopologyTotal(
        AuthoritativePlayerState player,
        AuthoritativeCombatRules rules,
        bool allowTruncatedWalletRemainder,
        out uint total)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rules);

        var found = false;
        var sum = 0U;
        for (var index = 0; index < player.Inventory.Count; index++) {
            var item = player.Inventory[index];
            if (!HasGoldIdentity(item.State, rules))
                continue;
            if (!IsValidStack(item, rules)
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
            return player.Gold == 0 && player.InventoryGrid.Count == 40;
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
        AuthoritativeCombatRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        return state.ItemType == rules.DeathGoldItemType
            && state.ItemIndex == rules.DeathGoldItemIndex;
    }

    /** Removes Gold in native store order: nonmaximum piles first, then all piles. */
    public static bool TrySpend(
        AuthoritativePlayerState player,
        uint value,
        AuthoritativeCombatRules rules,
        bool allowTruncatedWalletRemainder = false)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rules);
        if (value > player.Gold
            || !TryGetTopologyTotal(
                player,
                rules,
                allowTruncatedWalletRemainder,
                out _)) {
            return false;
        }

        var remaining = value;
        SpendPass(player, rules, skipMaximumPiles: true, ref remaining);
        SpendPass(player, rules, skipMaximumPiles: false, ref remaining);
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
        bool recalculateWalletFromTopology)
    {
        return TryAddCore(
                player,
                value,
                rules,
                allocateItemSeed,
                allowTruncatedWalletRemainder,
                recalculateWalletFromTopology,
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
        out uint remainder)
    {
        return TryAddCore(
            player,
            value,
            rules,
            allocateItemSeed,
            allowTruncatedWalletRemainder,
            recalculateWalletFromTopology,
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
                out var topologyTotal)
            || uint.MaxValue - topologyTotal < value) {
            return false;
        }

        var maximumStack = checked((uint)rules.DeathGoldMaximumStack);
        var capacity = 0UL;
        foreach (var candidate in player.Inventory) {
            if (IsValidStack(candidate, rules))
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
            if (!IsValidStack(candidate, rules) || candidate.Price == maximumStack)
                continue;
            var added = Math.Min(remainingToPlace, maximumStack - candidate.Price);
            player.Inventory[index] = WithValue(
                candidate,
                candidate.Price + added,
                rules);
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
                rules));
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
        AuthoritativeCombatRules rules)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rules);
        return item.ItemSeed != 0
            && HasGoldIdentity(item.State, rules)
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
        AuthoritativeCombatRules rules)
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
            CreateState(value, rules));
    }

    public static OwnedStoreItem WithValue(
        OwnedStoreItem item,
        uint value,
        AuthoritativeCombatRules rules)
    {
        if (!IsValidStack(item, rules))
            throw new ArgumentException("The item is not a valid Gold stack.", nameof(item));
        if (value == 0 || value > rules.DeathGoldMaximumStack)
            throw new ArgumentOutOfRangeException(nameof(value));
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
        AuthoritativeCombatRules rules)
    {
        if (value == 0 || value > rules.DeathGoldMaximumStack)
            throw new ArgumentOutOfRangeException(nameof(value));
        return AuthoritativeItemState.Empty with {
            ItemType = rules.DeathGoldItemType,
            Value = checked((int)value),
            IdentifiedValue = checked((int)value),
            ItemIndex = rules.DeathGoldItemIndex,
            StatFlag = true,
        };
    }

    private static void SpendPass(
        AuthoritativePlayerState player,
        AuthoritativeCombatRules rules,
        bool skipMaximumPiles,
        ref uint remaining)
    {
        var maximumStack = checked((uint)rules.DeathGoldMaximumStack);
        for (var index = 0; index < player.Inventory.Count && remaining > 0;) {
            var candidate = player.Inventory[index];
            if (!IsValidStack(candidate, rules)
                || (skipMaximumPiles && candidate.Price == maximumStack)) {
                index++;
                continue;
            }
            if (remaining < candidate.Price) {
                player.Inventory[index] = WithValue(
                    candidate,
                    candidate.Price - remaining,
                    rules);
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
