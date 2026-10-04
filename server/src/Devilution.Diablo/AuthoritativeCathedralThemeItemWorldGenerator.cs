using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace Devilution.Diablo;

/** Minimal native object metadata used by ItemSpaceOk. Index zero is Objects[0]. */
public sealed record AuthoritativeCathedralThemeItemWorldObject(
    bool Solid = false,
    bool CanInteract = false);

/**
 * Detached world inputs for one ordinary Cathedral CreateRndItem/CreateTypeItem
 * call. The arrays retain native row-major y * 112 + x storage and signed Tile
 * field values. SetPieceTiles is retained as input state but is intentionally
 * ignored by ItemSpaceOk, matching the native item path.
 */
public sealed class AuthoritativeCathedralThemeItemWorldState
{
    public const int Width = AuthoritativeCathedralInitialItemGenerator.NativeTileWidth;
    public const int Height = AuthoritativeCathedralInitialItemGenerator.NativeTileHeight;
    public const int CellCount = Width * Height;
    public const int PoolCapacity = AuthoritativeCathedralInitialItemGenerator.PoolCapacity;
    public const int ActiveCapacity = AuthoritativeCathedralInitialItemGenerator.ActiveCapacity;

    public AuthoritativeCathedralThemeItemWorldState(
        IReadOnlyList<AuthoritativeCathedralInitialItemState> items,
        IReadOnlyList<int> activeItemIds,
        int activeItemCount,
        IReadOnlyList<int> itemRefs,
        IReadOnlyList<int> playerRefs,
        IReadOnlyList<int> monsterRefs,
        IReadOnlyList<int> objectRefs,
        IReadOnlyList<bool> solidTiles,
        IReadOnlyList<bool> setPieceTiles,
        IReadOnlyList<bool> objectSolid,
        IReadOnlyList<bool> objectCanInteract,
        IReadOnlySet<int>? uniqueItemFlags = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(activeItemIds);
        ArgumentNullException.ThrowIfNull(itemRefs);
        ArgumentNullException.ThrowIfNull(playerRefs);
        ArgumentNullException.ThrowIfNull(monsterRefs);
        ArgumentNullException.ThrowIfNull(objectRefs);
        ArgumentNullException.ThrowIfNull(solidTiles);
        ArgumentNullException.ThrowIfNull(setPieceTiles);
        ArgumentNullException.ThrowIfNull(objectSolid);
        ArgumentNullException.ThrowIfNull(objectCanInteract);

        if (items.Count != PoolCapacity)
            throw new InvalidDataException($"Native item storage requires exactly {PoolCapacity} entries.");
        if (activeItemIds.Count != ActiveCapacity)
            throw new InvalidDataException($"Native ActiveItems requires exactly {ActiveCapacity} entries.");
        if (activeItemCount is < 0 or > ActiveCapacity)
            throw new InvalidDataException("Native ActiveItemCount is outside the native pool range.");
        if (activeItemIds.Any(id => id is < 0 or >= ActiveCapacity)
            || activeItemIds.Distinct().Count() != ActiveCapacity)
            throw new InvalidDataException("ActiveItems must be a permutation of native item slots 0 through 126.");
        ValidateCellCount(itemRefs, nameof(itemRefs));
        ValidateCellCount(playerRefs, nameof(playerRefs));
        ValidateCellCount(monsterRefs, nameof(monsterRefs));
        ValidateCellCount(objectRefs, nameof(objectRefs));
        ValidateCellCount(solidTiles, nameof(solidTiles));
        ValidateCellCount(setPieceTiles, nameof(setPieceTiles));
        if (objectSolid.Count != objectCanInteract.Count || objectSolid.Count > ActiveCapacity)
            throw new InvalidDataException("Native object metadata must have one solid and interactive flag per object.");

        foreach (var item in items) {
            AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
        }
        if (itemRefs.Any(value => value is < sbyte.MinValue or > sbyte.MaxValue)
            || playerRefs.Any(value => value is < sbyte.MinValue or > sbyte.MaxValue)
            || monsterRefs.Any(value => value is < short.MinValue or > short.MaxValue)
            || objectRefs.Any(value => value is < -127 or > sbyte.MaxValue)) {
            throw new InvalidDataException("A signed native Tile reference is outside its safe field range.");
        }
        foreach (var objectRef in objectRefs) {
            if (objectRef == 0)
                continue;
            var objectIndex = Math.Abs(objectRef) - 1;
            if (objectIndex < 0 || objectIndex >= objectSolid.Count)
                throw new InvalidDataException("A Tile object reference is outside the supplied native object metadata.");
        }

        Items = new ReadOnlyCollection<AuthoritativeCathedralInitialItemState>(
            items.Select(CloneItem).ToArray());
        ActiveItemIds = new ReadOnlyCollection<int>(activeItemIds.ToArray());
        ActiveItemCount = activeItemCount;
        ItemRefs = new ReadOnlyCollection<int>(itemRefs.ToArray());
        PlayerRefs = new ReadOnlyCollection<int>(playerRefs.ToArray());
        MonsterRefs = new ReadOnlyCollection<int>(monsterRefs.ToArray());
        ObjectRefs = new ReadOnlyCollection<int>(objectRefs.ToArray());
        SolidTiles = new ReadOnlyCollection<bool>(solidTiles.ToArray());
        SetPieceTiles = new ReadOnlyCollection<bool>(setPieceTiles.ToArray());
        ObjectSolid = new ReadOnlyCollection<bool>(objectSolid.ToArray());
        ObjectCanInteract = new ReadOnlyCollection<bool>(objectCanInteract.ToArray());
        UniqueItemFlags = ((IEnumerable<int>?)uniqueItemFlags ?? Array.Empty<int>()).ToFrozenSet();
        if (UniqueItemFlags.Any(id => id is < 0 or >= PoolCapacity))
            throw new InvalidDataException("Unique item history contains an unsafe native unique ID.");
    }

    public IReadOnlyList<AuthoritativeCathedralInitialItemState> Items { get; }
    public IReadOnlyList<int> ActiveItemIds { get; }
    public int ActiveItemCount { get; }
    public IReadOnlyList<int> ItemRefs { get; }
    public IReadOnlyList<int> SignedItemGrid => ItemRefs;
    public IReadOnlyList<int> PlayerRefs { get; }
    public IReadOnlyList<int> MonsterRefs { get; }
    public IReadOnlyList<int> ObjectRefs { get; }
    public IReadOnlyList<bool> SolidTiles { get; }
    public IReadOnlyList<bool> SetPieceTiles { get; }
    public IReadOnlyList<bool> ObjectSolid { get; }
    public IReadOnlyList<bool> ObjectCanInteract { get; }
    public IReadOnlySet<int> UniqueItemFlags { get; }

    private static void ValidateCellCount<T>(IReadOnlyList<T> values, string name)
    {
        if (values.Count != CellCount)
            throw new InvalidDataException($"Native {name} requires exactly {CellCount} entries.");
    }

    private static AuthoritativeCathedralInitialItemState CloneItem(
        AuthoritativeCathedralInitialItemState item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.Animation);
        return item with { Animation = item.Animation with { } };
    }
}

/** Selector-only world request. It never allocates, places, or advances an item seed. */
public sealed record AuthoritativeCathedralThemeItemWorldSelectionRequest(
    uint RandomState,
    int CurrentLevel,
    bool OnlyGood,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    int RequestedItemType = -1,
    int RequestedMiscId = -1);

/** Native Rnd* result before AdvanceRndSeed and item generation. */
public sealed record AuthoritativeCathedralThemeItemWorldSelectionResult(
    int SelectedNativeItemIndex,
    uint FinalRandomState)
{
    public int SelectedIndex => SelectedNativeItemIndex;
    public uint SelectorRandomState => FinalRandomState;
    public uint RandomState => FinalRandomState;
}

/** One detached ordinary Cathedral world-item request. */
public sealed record AuthoritativeCathedralThemeItemWorldRequest(
    AuthoritativeCathedralThemeItemWorldState State,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    uint RandomState,
    int PositionX,
    int PositionY,
    bool OnlyGood,
    bool PreGenerate = false,
    int RequestedItemType = -1,
    int RequestedMiscId = -1,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null);

/** Exact placement diagnostic; it has no extra native side effects. */
public sealed record AuthoritativeCathedralThemeItemPlacementResult(
    bool Succeeded,
    bool UsedSuperItemSpaceFallback,
    int X,
    int Y);

/**
 * Detached result of one native CreateRndItem/CreateTypeItem operation. No
 * network, delta, renderer, or world-object mutation is performed.
 */
public sealed class AuthoritativeCathedralThemeItemWorldResult
{
    internal AuthoritativeCathedralThemeItemWorldResult(
        AuthoritativeCathedralThemeItemWorldState state,
        int selectedNativeItemIndex,
        int selectedSlot,
        uint selectorRandomState,
        uint finalRandomState,
        uint itemSeed,
        AuthoritativeCathedralInitialItemState? item,
        AuthoritativeCathedralThemeItemPlacementResult placement)
    {
        WorldState = state;
        SelectedNativeItemIndex = selectedNativeItemIndex;
        SelectedSlot = selectedSlot;
        SelectorRandomState = selectorRandomState;
        FinalRandomState = finalRandomState;
        ItemSeed = itemSeed;
        Item = item;
        Placement = placement;
    }

    public AuthoritativeCathedralThemeItemWorldState WorldState { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemState> Items => WorldState.Items;
    public IReadOnlyList<int> ActiveItemIds => WorldState.ActiveItemIds;
    public int ActiveItemCount => WorldState.ActiveItemCount;
    public IReadOnlyList<int> ItemRefs => WorldState.ItemRefs;
    public IReadOnlyList<int> SignedItemGrid => WorldState.ItemRefs;
    public IReadOnlyList<int> PlayerRefs => WorldState.PlayerRefs;
    public IReadOnlyList<int> MonsterRefs => WorldState.MonsterRefs;
    public IReadOnlyList<int> ObjectRefs => WorldState.ObjectRefs;
    public IReadOnlyList<bool> SolidTiles => WorldState.SolidTiles;
    public IReadOnlyList<bool> SetPieceTiles => WorldState.SetPieceTiles;
    public IReadOnlyList<bool> ObjectSolid => WorldState.ObjectSolid;
    public IReadOnlyList<bool> ObjectCanInteract => WorldState.ObjectCanInteract;
    public IReadOnlySet<int> UniqueItemFlags => WorldState.UniqueItemFlags;
    public int SelectedNativeItemIndex { get; }
    public int SelectedIndex => SelectedNativeItemIndex;
    public int SelectedSlot { get; }
    public uint SelectorRandomState { get; }
    // Initial AdvanceRndSeed setup value; unique retries may replace Item.Seed.
    public uint ItemSeed { get; }
    public uint FinalRandomState { get; }
    public uint RandomState => FinalRandomState;
    public AuthoritativeCathedralInitialItemState? Item { get; }
    public AuthoritativeCathedralThemeItemPlacementResult Placement { get; }
    public bool PlacementSucceeded => Placement.Succeeded;
    public bool UsedSuperItemSpaceFallback => Placement.UsedSuperItemSpaceFallback;
    public int PlacedX => Placement.X;
    public int PlacedY => Placement.Y;
}

/**
 * Ordinary single-player Normal Cathedral world selection/allocation. This is
 * deliberately limited to detached CreateRndItem/CreateTypeItem behavior;
 * inventory, initial ItemPlace rules, networking, and delta persistence are
 * outside this contract.
 */
public static class AuthoritativeCathedralThemeItemWorldGenerator
{
    public const int Width = AuthoritativeCathedralThemeItemWorldState.Width;
    public const int Height = AuthoritativeCathedralThemeItemWorldState.Height;
    public const int CellCount = AuthoritativeCathedralThemeItemWorldState.CellCount;
    public const int PoolCapacity = AuthoritativeCathedralThemeItemWorldState.PoolCapacity;
    public const int ActiveCapacity = AuthoritativeCathedralThemeItemWorldState.ActiveCapacity;

    public static AuthoritativeCathedralThemeItemWorldSelectionResult SelectRandomItem(
        AuthoritativeCathedralThemeItemWorldSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AuthoritativeCathedralThemeItemGenerator.SelectRandomItemForWorld(
            request.RandomState, request.CurrentLevel, request.OnlyGood, request.Catalog, request.Policy);
    }

    public static AuthoritativeCathedralThemeItemWorldSelectionResult SelectTypeItem(
        AuthoritativeCathedralThemeItemWorldSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AuthoritativeCathedralThemeItemGenerator.SelectTypeItemForWorld(
            request.RandomState, request.CurrentLevel, request.OnlyGood,
            request.RequestedItemType, request.RequestedMiscId, request.Catalog, request.Policy);
    }

    public static AuthoritativeCathedralThemeItemWorldResult CreateRandomItem(
        AuthoritativeCathedralThemeItemWorldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var selection = SelectRandomItem(new AuthoritativeCathedralThemeItemWorldSelectionRequest(
            request.RandomState, request.Policy.Depth, request.OnlyGood, request.Catalog, request.Policy));
        return CreateCore(request, selection);
    }

    public static AuthoritativeCathedralThemeItemWorldResult CreateTypeItem(
        AuthoritativeCathedralThemeItemWorldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var selection = SelectTypeItem(new AuthoritativeCathedralThemeItemWorldSelectionRequest(
            request.RandomState, request.Policy.Depth, request.OnlyGood, request.Catalog, request.Policy,
            request.RequestedItemType, request.RequestedMiscId));
        return CreateCore(request, selection);
    }

    private static AuthoritativeCathedralThemeItemWorldResult CreateCore(
        AuthoritativeCathedralThemeItemWorldRequest request,
        AuthoritativeCathedralThemeItemWorldSelectionResult selection)
    {
        ValidateRequest(request);
        if (request.State.ActiveItemCount >= ActiveCapacity) {
            return new AuthoritativeCathedralThemeItemWorldResult(
                request.State,
                selection.SelectedNativeItemIndex,
                -1,
                selection.FinalRandomState,
                selection.FinalRandomState,
                0,
                null,
                new(false, false, 0, 0));
        }

        var items = request.State.Items.Select(CloneItem).ToArray();
        var activeItemIds = request.State.ActiveItemIds.ToArray();
        var itemRefs = request.State.ItemRefs.ToArray();
        var slot = activeItemIds[request.State.ActiveItemCount];
        if (slot is < 0 or >= ActiveCapacity)
            throw new InvalidDataException("ActiveItems selected an unsafe allocation slot.");

        // AllocateItem assigns the complete value, including fields that clear
        // would intentionally preserve. Placement then runs before item setup.
        items[slot] = new AuthoritativeCathedralInitialItemState();
        var random = new AuthoritativeLegacyRandom(selection.FinalRandomState);
        var placement = PlaceItem(request.State, request.PositionX, request.PositionY,
            slot, random, itemRefs);
        items[slot] = items[slot] with { PositionX = placement.X, PositionY = placement.Y };

        var itemSeed = unchecked((uint)random.AdvanceRndSeed());
        var itemRequest = new AuthoritativeCathedralThemeItemRequest(
            itemSeed,
            selection.SelectedNativeItemIndex,
            checked(request.Policy.Depth * 2),
            request.Catalog.Rules.UniqueChanceNormal,
            request.OnlyGood,
            request.PreGenerate,
            InputItem: items[slot],
            ResetBeforeGetItemAttrs: false,
            Player: request.Player,
            UniqueItemFlags: request.State.UniqueItemFlags);
        var generated = AuthoritativeCathedralThemeItemGenerator.Generate(
            itemRequest, request.Catalog, request.Policy);
        items[slot] = generated.Item;
        var nextState = new AuthoritativeCathedralThemeItemWorldState(
            items,
            activeItemIds,
            checked(request.State.ActiveItemCount + 1),
            itemRefs,
            request.State.PlayerRefs,
            request.State.MonsterRefs,
            request.State.ObjectRefs,
            request.State.SolidTiles,
            request.State.SetPieceTiles,
            request.State.ObjectSolid,
            request.State.ObjectCanInteract,
            generated.UniqueItemFlags);
        return new AuthoritativeCathedralThemeItemWorldResult(
            nextState,
            selection.SelectedNativeItemIndex,
            slot,
            selection.FinalRandomState,
            generated.FinalRandomState,
            itemSeed,
            generated.Item,
            placement);
    }

    private static AuthoritativeCathedralThemeItemPlacementResult PlaceItem(
        AuthoritativeCathedralThemeItemWorldState state,
        int positionX,
        int positionY,
        int slot,
        AuthoritativeLegacyRandom random,
        int[] itemRefs)
    {
        if (GetItemSpace(state, positionX, positionY, slot, random, itemRefs, out var x, out var y))
            return new(true, false, x, y);

        // Native GetSuperItemSpace uses inclusive full-square scans, k=2..49, with
        // no random draw. Exhaustion intentionally leaves the allocated item at
        // (0,0) and does not write a tile reference.
        for (var k = 2; k < 50; k++) {
            for (var offsetY = -k; offsetY <= k; offsetY++) {
                for (var offsetX = -k; offsetX <= k; offsetX++) {
                    var candidateX = checked(positionX + offsetX);
                    var candidateY = checked(positionY + offsetY);
                    if (!ItemSpaceOk(state, candidateX, candidateY))
                        continue;
                    var index = TileIndex(candidateX, candidateY);
                    itemRefs[index] = checked(slot + 1);
                    return new(true, true, candidateX, candidateY);
                }
            }
        }
        return new(false, true, 0, 0);
    }

    private static bool GetItemSpace(
        AuthoritativeCathedralThemeItemWorldState state,
        int positionX,
        int positionY,
        int slot,
        AuthoritativeLegacyRandom random,
        int[] itemRefs,
        out int selectedX,
        out int selectedY)
    {
        var available = new bool[3, 3];
        for (var y = 0; y < 3; y++) {
            for (var x = 0; x < 3; x++) {
                var candidateX = checked(positionX + x - 1);
                var candidateY = checked(positionY + y - 1);
                available[x, y] = ItemSpaceOk(state, candidateX, candidateY);
            }
        }

        var hasAvailable = false;
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                hasAvailable |= available[x, y];

        // Native GenerateRnd(15)+1 is deliberately not clamped. In particular,
        // a signed non-positive draw retains the upper-left (0,0) cursor.
        var remaining = random.GenerateRnd(15) + 1;
        selectedX = checked(positionX - 1);
        selectedY = checked(positionY - 1);
        if (!hasAvailable)
            return false;

        var xIndex = 0;
        var yIndex = 0;
        while (remaining > 0) {
            if (available[xIndex, yIndex])
                remaining--;
            if (remaining <= 0)
                break;
            xIndex++;
            if (xIndex != 3)
                continue;
            xIndex = 0;
            yIndex++;
            if (yIndex == 3)
                yIndex = 0;
        }

        selectedX = checked(positionX + xIndex - 1);
        selectedY = checked(positionY + yIndex - 1);
        if ((uint)selectedX >= Width || (uint)selectedY >= Height)
            throw new InvalidDataException("Native GetItemSpace selected an unsafe out-of-bounds tile.");
        itemRefs[TileIndex(selectedX, selectedY)] = checked(slot + 1);
        return true;
    }

    private static bool ItemSpaceOk(
        AuthoritativeCathedralThemeItemWorldState state,
        int x,
        int y)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return false;
        var index = TileIndex(x, y);
        if (state.SolidTiles[index]
            || state.ItemRefs[index] != 0
            || state.MonsterRefs[index] != 0
            || state.PlayerRefs[index] != 0)
            return false;
        return !IsItemBlockingObjectAtPosition(state, x, y);
    }

    private static bool IsItemBlockingObjectAtPosition(
        AuthoritativeCathedralThemeItemWorldState state,
        int x,
        int y)
    {
        var direct = FindObjectIndex(state, x, y, true);
        if (direct is not null && state.ObjectSolid[direct.Value])
            return true;

        var south = FindObjectIndex(state, checked(x + 1), checked(y + 1), true);
        if (south is not null && state.ObjectCanInteract[south.Value])
            return true;

        var southEast = FindObjectIndex(state, checked(x + 1), y, false);
        if (southEast is null)
            return false;
        var southWest = FindObjectIndex(state, x, checked(y + 1), false);
        return southWest is not null
            && state.ObjectCanInteract[southEast.Value]
            && state.ObjectCanInteract[southWest.Value];
    }

    private static int? FindObjectIndex(
        AuthoritativeCathedralThemeItemWorldState state,
        int x,
        int y,
        bool considerLargeObjects)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return null;
        var objectReference = state.ObjectRefs[TileIndex(x, y)];
        if (objectReference > 0 || considerLargeObjects && objectReference != 0)
            return Math.Abs(objectReference) - 1;
        return null;
    }

    private static int TileIndex(int x, int y)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            throw new InvalidDataException("A native item placement tile is outside MAXDUNX/MAXDUNY.");
        return checked(y * Width + x);
    }

    private static void ValidateRequest(AuthoritativeCathedralThemeItemWorldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.State);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        ArgumentNullException.ThrowIfNull(request.Policy);
        if ((uint)request.PositionX >= Width || (uint)request.PositionY >= Height)
            throw new InvalidDataException("Native item placement requires an in-bounds origin tile.");
        if (request.RequestedItemType < -1 || request.RequestedMiscId < -1)
            throw new InvalidDataException("Native typed item selectors require non-negative type/misc IDs.");
    }

    private static AuthoritativeCathedralInitialItemState CloneItem(
        AuthoritativeCathedralInitialItemState item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item with { Animation = item.Animation with { } };
    }
}
