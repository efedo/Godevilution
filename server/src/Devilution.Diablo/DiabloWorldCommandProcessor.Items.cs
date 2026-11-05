using Devilution.Domain.Inventory;
using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldCommandProcessor
{
    private DiabloWorldIntentResult PickupWorldItem(
        AuthoritativePlayerState player,
        uint itemEntityId,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players,
        bool requireAdjacent = true,
        bool applyEffects = true)
    {
        if (!world.WorldItems.TryGetValue(itemEntityId, out var item)
            || item.LevelId != player.LevelId
            || (requireAdjacent
                && DeterministicGridMetrics.ChebyshevDistance(
                    item.PositionX,
                    item.PositionY,
                    player.PositionX,
                    player.PositionY) > 1)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var itemState = ResolveWorldItemState(player, item);
        var targetCell = InventoryGridLayout.FindFirstPlacement(
            player.InventoryGrid,
            itemState.InventoryWidth,
            itemState.InventoryHeight);
        var participants = players as IReadOnlyList<AuthoritativePlayerState>
            ?? players.ToArray();
        if (player.HeldItem is not null
            && !CanDropHeldItemForPickup(player, participants, item.EntityId)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (DiabloGoldInventory.HasGoldIdentity(
                itemState,
                combatRules,
                player.Items)) {
            uint nativeRemainder = 0;
            if (itemState.Value <= 0
                || itemState.Value > combatRules.DeathGoldMaximumStack
                || itemState.IdentifiedValue != itemState.Value
                || item.Price != checked((uint)itemState.Value)
                || !itemState.StatFlag
                || (ulong)player.Gold + item.Price > int.MaxValue
                || uint.MaxValue - player.Gold < item.Price) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
            }
            if (DiabloGoldInventory.TryGetTopologyTotal(
                    player,
                    combatRules,
                    allowTruncatedWalletRemainder: true,
                    out var topologyTotal,
                    player.Items)) {
                if (uint.MaxValue - topologyTotal < item.Price)
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
                if (!applyEffects)
                    return DiabloWorldIntentResult.Accepted;
                if (player.HeldItem is not null)
                    DropHeldItemForPickup(player, participants, item, itemState);
                if (!DiabloGoldInventory.TryAddPartial(
                        player,
                        item.Price,
                        combatRules,
                        AllocateGoldStackSeed,
                        allowTruncatedWalletRemainder: true,
                        recalculateWalletFromTopology: true,
                        out var remainder,
                        player.Items)) {
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
                }
                nativeRemainder = remainder;
                if (remainder > 0) {
                    player.HeldItem = new OwnedStoreItem(
                        0,
                        0,
                        item.ItemSeed,
                        remainder,
                        appliedTick,
                        itemState with {
                            Value = checked((int)remainder),
                            IdentifiedValue = checked((int)remainder),
                        });
                }
            } else {
                if (!applyEffects)
                    return DiabloWorldIntentResult.Accepted;
                if (player.HeldItem is not null)
                    DropHeldItemForPickup(player, participants, item, itemState);
                player.Gold += item.Price;
            }
            world.RemoveWorldItem(itemEntityId, world.NativeGoldPickupRemainder(item, itemState, nativeRemainder));
            return DiabloWorldIntentResult.Accepted;
        }

        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        if (player.HeldItem is not null)
            DropHeldItemForPickup(player, participants, item, itemState);

        if (targetCell < 0) {
            player.HeldItem = new OwnedStoreItem(
                0,
                0,
                item.ItemSeed,
                item.Price,
                appliedTick,
                itemState);
            world.RemoveWorldItem(itemEntityId, itemState);
            return DiabloWorldIntentResult.Accepted;
        }

        var inventoryIndex = player.Inventory.Count;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            item.ItemSeed,
            item.Price,
            appliedTick,
            itemState));
        InventoryGridLayout.Fill(
            player.InventoryGrid,
            targetCell,
            inventoryIndex,
            itemState.InventoryWidth,
            itemState.InventoryHeight);
        world.RemoveWorldItem(itemEntityId, itemState);
        ApplyItemQuestEffects(itemState);
        ApplyItemCombinations(player, appliedTick);
        return DiabloWorldIntentResult.Accepted;
    }

    /** Validates the native pickup origin for a cursor-item replacement. */
    private bool CanDropHeldItemForPickup(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        uint pickupEntityId = 0)
    {
        if (player.HeldItem is null)
            return true;
        var targetX = player.FuturePositionX;
        var targetY = player.FuturePositionY;
        if (world.HasNativeItemLevel(player.LevelId)) {
            world.ValidateNativeCursorDrop(player);
            return world.CanPutNativeItem(player.LevelId, targetX, targetY,
                releasingPickupSlot: world.IsNativeWorldItem(pickupEntityId));
        }
        return IsPlayerDropPositionAvailable(player, players, targetX, targetY);
    }

    private bool IsPlayerDropPositionAvailable(AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players, int targetX, int targetY)
    {
        if (world.HasNativeItemLevel(player.LevelId))
            return world.CanPutNativeItem(player.LevelId, targetX, targetY);
        if (!world.IsWalkable(player.LevelId, targetX, targetY)
            || world.IsCathedralDoorItemBlocked(player.LevelId, targetX, targetY)
            || world.IsOccupied(
                player.LevelId,
                targetX,
                targetY,
                players,
                excludedEntityId: player.EntityId)
            || world.WorldItems.Values.Any(item =>
                item.LevelId == player.LevelId
                && item.PositionX == targetX
                && item.PositionY == targetY)) {
            return false;
        }
        return true;
    }

    /** Drops the cursor item at the native pickup origin before replacing it. */
    private void DropHeldItemForPickup(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players,
        AuthoritativeWorldItem pickedItem,
        AuthoritativeItemState pickedState)
    {
        var heldItem = player.HeldItem
            ?? throw new InvalidOperationException("A cursor item is required for pickup replacement.");
        if (!CanDropHeldItemForPickup(player, players, pickedItem.EntityId))
            throw new InvalidOperationException("The authoritative pickup origin cannot hold a dropped cursor item.");
        var targetX = player.FuturePositionX;
        var targetY = player.FuturePositionY;

        // Native loopback queues the held drop until CleanupItems releases the picked slot.
        // Apply that pool order synchronously inside this one authoritative command.
        if (world.IsNativeWorldItem(pickedItem.EntityId))
            world.RemoveWorldItem(pickedItem.EntityId, pickedState);

        var entityId = AllocateWorldItemEntityId(players);
        var droppedItem = new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            targetX,
            targetY,
            heldItem.ItemSeed,
            heldItem.Price,
            heldItem.State);
        world.AddWorldItem(droppedItem);
        player.HeldItem = null;
        ApplyWorldItemAnchorQuest(droppedItem);
    }

    private AuthoritativeItemState ResolveWorldItemState(
        AuthoritativePlayerState player,
        AuthoritativeWorldItem item)
    {
        if (world.IsNativeWorldItem(item.EntityId))
            return world.ResolveNativePickupState(item, player);
        var state = items?.ResolveAuthoredMetadata(item.State, item.ItemSeed) ?? item.State;
        var isGold = DiabloGoldInventory.HasGoldIdentity(
            state,
            combatRules,
            player.Items ?? items);
        if (!isGold
            && state.StableItemId != 0
            && items?.TryGet(state.StableItemId, out _) == true) {
            state = state with {
                // Known authored items need the same current-stat flag as native pickups.
                StatFlag = player.Attributes.Strength.Current >= state.MinimumStrength
                    && player.Attributes.Magic.Current >= state.MinimumMagic
                    && player.Attributes.Dexterity.Current >= state.MinimumDexterity,
            };
        }
        if (item.Price <= int.MaxValue && isGold) {
            state = state with {
                Value = state.Value == 0 ? checked((int)item.Price) : state.Value,
                IdentifiedValue = state.IdentifiedValue == 0
                    ? checked((int)item.Price)
                    : state.IdentifiedValue,
            };
        }
        return state;
    }

    /** Resolves content-owned quest gates on native level triggers. */
    private bool IsWarpAvailable(AuthoritativeWarpTarget warp)
    {
        if (!world.AllowsNativePopulationLevel(warp.SourceLevelId)
            || !world.CanEnterNativePopulationLevel(warp.TargetLevelId)
            || !world.IsNativeTownWarpOpen(warp.RequiredTownWarp))
            return false;
        if (warp.RequiredQuestId == 0)
            return true;
        if (!world.Quests.TryGetValue(warp.RequiredQuestId, out var quest)
            || quest.Unavailable
            || (warp.RequiredQuestCompleted
                ? !quest.Completed
                : !quest.Active)) {
            return false;
        }
        return true;
    }

    /** Applies deterministic content-owned quest transitions after pickup. */
    private void ApplyItemQuestEffects(AuthoritativeItemState itemState)
    {
        if (items is null || itemQuestEffects is null)
            return;

        foreach (var effect in itemQuestEffects.Definitions) {
            if (!items.TryGet(effect.ItemId, out var itemDefinition)
                || !items.IsStateForDefinition(itemState, itemDefinition)
                || !world.Quests.TryGetValue(effect.QuestId, out var quest)
                || quest.Unavailable) {
                continue;
            }
            if (effect.RequiredVariable1 is int requiredVariable1
                && quest.Variable1 != requiredVariable1) {
                continue;
            }

            switch (effect.EffectKind) {
            case AuthoritativeItemQuestEffectKind.ActivateQuest:
                if (!quest.Completed)
                    quest.Active = true;
                quest.Logged = true;
                quest.Variable1 = Math.Max(quest.Variable1, effect.EffectAmount);
                break;
            case AuthoritativeItemQuestEffectKind.CompleteQuest:
                if (!quest.Active)
                    continue;
                quest.Progress = quest.RequiredProgress;
                quest.Completed = true;
                quest.Active = false;
                quest.Logged = true;
                quest.Variable1 = Math.Max(quest.Variable1, effect.EffectAmount);
                break;
            case AuthoritativeItemQuestEffectKind.SetVariable1:
                if (!quest.Active)
                    continue;
                quest.Variable1 = effect.EffectAmount;
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported item quest effect {effect.EffectKind}.");
            }
        }
    }

    /** Applies deterministic content-owned item combinations after pickup. */
    private void ApplyItemCombinations(
        AuthoritativePlayerState player,
        ulong appliedTick)
    {
        if (items is null || itemCombinations is null)
            return;

        while (true) {
            AuthoritativeItemCombinationDefinition? selected = null;
            var consumedIndices = Array.Empty<int>();
            foreach (var definition in itemCombinations.Definitions) {
                var usedIndices = new HashSet<int>();
                var matchingIndices = new List<int>();
                var allInputsPresent = true;
                foreach (var itemId in definition.RequiredItemIds) {
                    if (!items.TryGet(itemId, out var itemDefinition)) {
                        allInputsPresent = false;
                        break;
                    }
                    var matchingIndex = -1;
                    for (var index = 0; index < player.Inventory.Count; index++) {
                        if (!usedIndices.Contains(index)
                            && items.IsStateForDefinition(
                                player.Inventory[index].State,
                                itemDefinition)) {
                            matchingIndex = index;
                            break;
                        }
                    }
                    if (matchingIndex < 0) {
                        allInputsPresent = false;
                        break;
                    }
                    usedIndices.Add(matchingIndex);
                    matchingIndices.Add(matchingIndex);
                }
                if (allInputsPresent) {
                    selected = definition;
                    consumedIndices = matchingIndices.ToArray();
                    break;
                }
            }

            if (selected is null)
                return;

            var resultIndex = consumedIndices.Min();
            foreach (var index in consumedIndices
                         .Where(index => index != resultIndex)
                         .OrderByDescending(index => index)) {
                InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, index);
                player.Inventory.RemoveAt(index);
            }

            var resultAnchor = InventoryGridLayout.FindAnchor(
                player.InventoryGrid,
                resultIndex);
            var state = items.Generate(
                selected.ResultItemId,
                AllocateGoldStackSeed());
            InventoryGridLayout.Clear(player.InventoryGrid, resultIndex);
            var targetCell = resultAnchor >= 0
                && InventoryGridLayout.CanPlace(
                    player.InventoryGrid,
                    (uint)resultAnchor,
                    state.InventoryWidth,
                    state.InventoryHeight)
                ? resultAnchor
                : InventoryGridLayout.FindFirstPlacement(
                    player.InventoryGrid,
                    state.InventoryWidth,
                    state.InventoryHeight);
            if (targetCell < 0)
                throw new InvalidDataException(
                    $"Item combination result {selected.ResultItemId} has no inventory placement.");
            player.Inventory[resultIndex] = new OwnedStoreItem(
                0,
                0,
                state.CreateInfo,
                0,
                appliedTick,
                state);
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                targetCell,
                resultIndex,
                state.InventoryWidth,
                state.InventoryHeight);
        }
    }

    private DiabloWorldIntentResult DropHeldItem(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return DropItem(player, null, targetX, targetY, players);
    }

    private DiabloWorldIntentResult DropItem(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? sourceReference,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        OwnedStoreItem? sourceItem;
        if (sourceReference is { } reference) {
            if (reference.Location == DiabloPlayerItemLocation.Stash
                || player.HeldItem is not null
                || !DiabloPlayerItemAccess.TryGet(player, reference, out sourceItem)) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
        } else if (player.HeldItem is { } heldItem) {
            sourceItem = heldItem;
        } else {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var originX = player.FuturePositionX;
        var originY = player.FuturePositionY;
        if (!world.ContainsPosition(player.LevelId, targetX, targetY))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (DeterministicGridMetrics.ChebyshevDistance(
                targetX,
                targetY,
                originX,
                originY) > 1
            || !IsPlayerDropPositionAvailable(player, players, targetX, targetY)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var entityId = AllocateWorldItemEntityId(players);
        var worldItem = new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            targetX,
            targetY,
            sourceItem!.ItemSeed,
            sourceItem.Price,
            sourceItem.State);
        world.AddWorldItem(worldItem);
        if (sourceReference is { } source) {
            DiabloPlayerItemAccess.Remove(player, source);
            if (source.Location == DiabloPlayerItemLocation.Equipment)
                player.RecalculateEquipmentResources();
        } else {
            player.HeldItem = null;
        }
        ApplyWorldItemAnchorQuest(worldItem);
        return DiabloWorldIntentResult.Accepted;
    }

    /** Completes a content-authored item-placement quest after the item is persisted in world state. */
    private void ApplyWorldItemAnchorQuest(AuthoritativeWorldItem droppedItem)
    {
        if (questTypes is null)
            return;

        foreach (var quest in world.Quests.Values) {
            if (quest.Unavailable
                || !quest.Active
                || quest.Completed
                || !questTypes.TryGet(quest.QuestId, out var questType)) {
                continue;
            }
            if (gameMode == DiabloGameMode.Multiplayer && questType.SinglePlayerOnly)
                continue;
            if (questType.WorldItemAnchorPositionX is not { } anchorX
                || questType.WorldItemAnchorPositionY is not { } anchorY
                || quest.LevelId != droppedItem.LevelId
                || anchorX != droppedItem.PositionX
                || anchorY != droppedItem.PositionY) {
                continue;
            }

            quest.Progress = quest.RequiredProgress;
            quest.Completed = true;
            quest.Active = false;
            quest.Logged = true;
        }
    }

    private uint AllocateGoldStackSeed()
    {
        uint itemSeed;
        do {
            itemSeed = unchecked((uint)random.AdvanceRndSeed());
        } while (itemSeed == 0);
        return itemSeed;
    }

    private uint AllocateWorldItemEntityId(
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        return worldItemEntityIds.Allocate(entityId =>
            players.Any(player => player.EntityId == entityId)
            || world.IsEntityIdOccupied(entityId));
    }

}
