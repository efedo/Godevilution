namespace Devilution.Diablo;

/** Native Object::_otype values for the three ordinary chest variants. */
public enum AuthoritativeCathedralOrdinaryChestType
{
    Chest1 = 5,
    Chest2 = 6,
    Chest3 = 7,
}

/** Inputs at the ordinary untrapped Cathedral OperateChest -> item creation boundary. */
public sealed record AuthoritativeCathedralChestLootRequest(
    AuthoritativeCathedralThemeItemWorldState WorldState,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    AuthoritativeCathedralOrdinaryChestType ChestType,
    uint ChestRandomSeed,
    int PositionX,
    int PositionY,
    int ItemCount,
    int ItemBranch,
    bool TrapArmed = false,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null)
{
    /** Native object slot whose selection is cleared before item placement. */
    public int ChestObjectSlot { get; init; } = -1;
}

/** One ordinary native chest item attempt, including invisible pool allocations. */
public sealed record AuthoritativeCathedralChestLootEntry(
    int SelectedNativeItemIndex,
    int Slot,
    uint ItemSeed,
    AuthoritativeCathedralInitialItemState? Item,
    AuthoritativeCathedralThemeItemPlacementResult Placement)
{
    public bool Allocated => Slot >= 0;
    public bool Visible => Allocated && Placement.Succeeded;
}

/** Detached full result of one bounded ordinary chest operation. */
public sealed class AuthoritativeCathedralChestLootResult
{
    internal AuthoritativeCathedralChestLootResult(
        AuthoritativeCathedralThemeItemWorldState worldState,
        uint initialRandomState,
        uint finalRandomState,
        IReadOnlyList<AuthoritativeCathedralChestLootEntry> entries)
    {
        WorldState = worldState;
        InitialRandomState = initialRandomState;
        FinalRandomState = finalRandomState;
        Entries = entries.ToArray();
    }

    public AuthoritativeCathedralThemeItemWorldState WorldState { get; }
    public uint InitialRandomState { get; }
    public uint FinalRandomState { get; }
    public IReadOnlyList<AuthoritativeCathedralChestLootEntry> Entries { get; }
    public int AllocatedCount => Entries.Count(entry => entry.Allocated);
    public int VisibleCount => Entries.Count(entry => entry.Visible);
}

/**
 * Native ordinary-Diablo Cathedral chest contents for depths 1 through 4. It models
 * OperateChest's SetRndSeed(_oRndSeed) and per-item call order; object mutation
 * and event/network effects remain with the world command owner.
 */
public static class AuthoritativeCathedralChestLootGenerator
{
    private const int Chest1 = 5;
    private const int Chest3 = 7;

    public static AuthoritativeCathedralChestLootResult Generate(
        AuthoritativeCathedralChestLootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorldState);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        ArgumentNullException.ThrowIfNull(request.Policy);
        if (request.Policy.Depth is < 1 or > 4
            || request.Policy.CurrentDungeonLevel != 0 && request.Policy.CurrentDungeonLevel != request.Policy.Depth
            || request.Policy.Hellfire || request.Policy.SetLevel
            || request.Policy.GameMode != DiabloGameMode.SinglePlayer
            || request.Policy.Difficulty != DiabloDifficulty.Normal
            || request.Policy.Spawn || request.Policy.QuestEnabled
            || !request.Policy.LegacyLevelItemGenerationAllowed)
            throw new InvalidDataException("Native chest contents are limited to ordinary original-Diablo Cathedral depths 1 through 4.");
        if (request.TrapArmed)
            throw new InvalidDataException("Trapped native chests are outside the ordinary chest contract.");

        var chestType = (int)request.ChestType;
        if (chestType is < (int)AuthoritativeCathedralOrdinaryChestType.Chest1
            or > Chest3)
            throw new InvalidDataException("Native chest type is not an ordinary chest variant.");
        // AddChest selects GenerateRnd(type - 3), so chest variants 5–7
        // contain at most 1, 2, and 3 items respectively.
        var maximumItems = chestType - Chest1 + 1;
        if (request.ItemCount < 0 || request.ItemCount > maximumItems)
            throw new InvalidDataException("Native chest item count is outside AddChest's variant range.");
        if (request.ItemBranch is < 0 or > 7)
            throw new InvalidDataException("Native chest item branch is outside Var2's generated range.");
        if ((uint)request.PositionX >= AuthoritativeCathedralThemeItemWorldState.Width
            || (uint)request.PositionY >= AuthoritativeCathedralThemeItemWorldState.Height)
            throw new InvalidDataException("Native chest origin is outside the item world.");
        if (request.ChestObjectSlot < 0
            || request.ChestObjectSlot >= request.WorldState.ObjectCanInteract.Count
            || !request.WorldState.ObjectCanInteract[request.ChestObjectSlot]
            || Math.Abs(request.WorldState.ObjectRefs[request.PositionY * AuthoritativeCathedralThemeItemWorldState.Width
                + request.PositionX]) != request.ChestObjectSlot + 1)
            throw new InvalidDataException("Native chest must be the selected object at its captured origin.");

        return GenerateItems(request, AuthoritativeNativeDungeonFamilyContext.Cathedral(request.Policy.Depth));
    }

    /** Family-aware item placement used by the separately admitted Catacombs chest wrapper. */
    internal static AuthoritativeCathedralChestLootResult Generate(
        AuthoritativeCathedralChestLootRequest request,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorldState);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        ArgumentNullException.ThrowIfNull(request.Policy);
        familyContext.RequireMatch(request.Policy.Depth);
        if (!familyContext.IsCatacombs)
            throw new InvalidDataException("The family-aware chest path is reserved for Catacombs objects.");
        if (request.Policy.Depth is < 5 or > 8
            || request.Policy.CurrentDungeonLevel != 0 && request.Policy.CurrentDungeonLevel != request.Policy.Depth
            || request.Policy.Hellfire || request.Policy.SetLevel
            || request.Policy.GameMode != DiabloGameMode.SinglePlayer
            || request.Policy.Difficulty != DiabloDifficulty.Normal
            || request.Policy.Spawn || request.Policy.QuestEnabled
            || !request.Policy.LegacyLevelItemGenerationAllowed)
            throw new InvalidDataException("Native chest contents require ordinary original-Diablo Catacombs depths 5 through 8.");

        var nativeType = (int)request.ChestType;
        var lootTier = AuthoritativeCatacombsNativeObjectPolicy.LootTierForChestType(nativeType);
        ValidateLootRequest(request, lootTier);
        return GenerateItems(request, familyContext);
    }

    private static void ValidateLootRequest(
        AuthoritativeCathedralChestLootRequest request,
        int lootTier)
    {
        if (request.ItemCount < 0 || request.ItemCount > lootTier - Chest1 + 1)
            throw new InvalidDataException("Native chest item count is outside AddChest's variant range.");
        if (request.ItemBranch is < 0 or > 7)
            throw new InvalidDataException("Native chest item branch is outside Var2's generated range.");
        if ((uint)request.PositionX >= AuthoritativeCathedralThemeItemWorldState.Width
            || (uint)request.PositionY >= AuthoritativeCathedralThemeItemWorldState.Height)
            throw new InvalidDataException("Native chest origin is outside the item world.");
        if (request.ChestObjectSlot < 0
            || request.ChestObjectSlot >= request.WorldState.ObjectCanInteract.Count
            || !request.WorldState.ObjectCanInteract[request.ChestObjectSlot]
            || Math.Abs(request.WorldState.ObjectRefs[request.PositionY * AuthoritativeCathedralThemeItemWorldState.Width
                + request.PositionX]) != request.ChestObjectSlot + 1)
            throw new InvalidDataException("Native chest must be the selected object at its captured origin.");
    }

    private static AuthoritativeCathedralChestLootResult GenerateItems(
        AuthoritativeCathedralChestLootRequest request,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        // Native OperateChest clears SelectionRegion before it calls either
        // CreateRndUseful or CreateRndItem, which changes placement at the chest.
        var objectCanInteract = request.WorldState.ObjectCanInteract.ToArray();
        objectCanInteract[request.ChestObjectSlot] = false;
        var worldState = new AuthoritativeCathedralThemeItemWorldState(
            request.WorldState.Items,
            request.WorldState.ActiveItemIds,
            request.WorldState.ActiveItemCount,
            request.WorldState.ItemRefs,
            request.WorldState.PlayerRefs,
            request.WorldState.MonsterRefs,
            request.WorldState.ObjectRefs,
            request.WorldState.SolidTiles,
            request.WorldState.SetPieceTiles,
            request.WorldState.ObjectSolid,
            objectCanInteract,
            request.WorldState.UniqueItemFlags);
        var randomState = request.ChestRandomSeed;
        var entries = new AuthoritativeCathedralChestLootEntry[request.ItemCount];
        for (var i = 0; i < entries.Length; i++) {
            var itemRequest = new AuthoritativeCathedralThemeItemWorldRequest(
                worldState,
                request.Catalog,
                request.Policy,
                randomState,
                request.PositionX,
                request.PositionY,
                OnlyGood: false,
                Player: request.Player);
            var generated = request.ItemBranch == 0
                ? AuthoritativeCathedralThemeItemWorldGenerator.CreateUsefulItem(
                    itemRequest, request.Policy.Depth, familyContext)
                : AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(itemRequest, familyContext);
            worldState = generated.WorldState;
            randomState = generated.FinalRandomState;
            entries[i] = new(
                generated.SelectedNativeItemIndex,
                generated.SelectedSlot,
                generated.ItemSeed,
                generated.Item,
                generated.Placement);
        }

        return new(worldState, request.ChestRandomSeed, randomState, entries);
    }
}
