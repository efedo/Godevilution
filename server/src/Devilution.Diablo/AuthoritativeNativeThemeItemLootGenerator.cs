namespace Devilution.Diablo;

/** Ordinary Diablo item objects. Object state and items commit together through the shared pool. */
public static class AuthoritativeNativeThemeItemLootGenerator
{
    public static bool Supports(int type) => type is 61 or 62 or 63 or 64 or 67 or 77 or 89 or 90 or 92;

    public static AuthoritativeNativeThemeItemLootResult Generate(AuthoritativeNativeThemeItemLootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy;
        if (!Supports(request.NativeObjectId) || policy.Depth is < 1 or > 8
            || policy.Hellfire || policy.SetLevel || policy.QuestEnabled || policy.Spawn
            || !policy.LegacyLevelItemGenerationAllowed
            || policy.CurrentDungeonLevel != 0 && policy.CurrentDungeonLevel != policy.Depth
            || policy.GameMode is not (DiabloGameMode.SinglePlayer or DiabloGameMode.Multiplayer))
            throw new InvalidDataException("Native theme item operations require ordinary questless Diablo L1-L8.");
        var state = request.WorldState;
        if ((uint)request.PositionX >= AuthoritativeCathedralThemeItemWorldState.Width
            || (uint)request.PositionY >= AuthoritativeCathedralThemeItemWorldState.Height
            || (uint)request.ObjectSlot >= state.ObjectCanInteract.Count
            || !state.ObjectCanInteract[request.ObjectSlot]
            || Math.Abs(state.ObjectRefs[request.PositionY * 112 + request.PositionX]) != request.ObjectSlot + 1
            || request.AnimationFrame is < 1 or > 253
            || request.NativeObjectId is 62 or 63 && request.AnimationFrame < 3)
            throw new InvalidDataException("Native theme item operation lost its selectable generated object.");
        // OperateObject consumes selection before ItemSpaceOk observes the signed footprint.
        var interactive = state.ObjectCanInteract.ToArray();
        interactive[request.ObjectSlot] = false;
        var opened = new AuthoritativeCathedralThemeItemWorldState(state.Items, state.ActiveItemIds,
            state.ActiveItemCount, state.ItemRefs, state.PlayerRefs, state.MonsterRefs, state.ObjectRefs,
            state.SolidTiles, state.SetPieceTiles, state.ObjectSolid, interactive, state.UniqueItemFlags);
        request = request with { WorldState = opened };
        return request.NativeObjectId switch {
            61 or 64 => OperateBookStand(request),
            62 or 63 => OperateBookcase(request),
            67 => OperateDecapitatedBody(request),
            77 or 89 => OperateArmorStand(request),
            90 or 92 => OperateWeaponRack(request),
            _ => throw new InvalidOperationException("An admitted native item object has no operation."),
        };
    }

    /** Native objects.cpp OperateBookStand: FlipCoin(5) precedes CreateTypeItem even with a full pool. */
    private static AuthoritativeNativeThemeItemLootResult OperateBookStand(AuthoritativeNativeThemeItemLootRequest value)
    {
        var rng = new AuthoritativeLegacyRandom(value.RandomSeed);
        var misc = rng.GenerateRnd(5) == 0 ? 24 : 21; // IMISC_BOOK / IMISC_SCROLL
        return CreateTypeItem(value, rng.State, false, 0, misc, value.AnimationFrame + 2);
    }

    /** Native objects.cpp OperateBookcase; the quest-associated Zhar owner is refused before dispatch. */
    private static AuthoritativeNativeThemeItemLootResult OperateBookcase(AuthoritativeNativeThemeItemLootRequest value)
        => CreateTypeItem(value, value.RandomSeed, false, 0, 24, value.AnimationFrame - 2);

    /** Native objects.cpp OperateDecapitatedBody. */
    private static AuthoritativeNativeThemeItemLootResult OperateDecapitatedBody(AuthoritativeNativeThemeItemLootRequest value)
    {
        var generated = AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(
            ItemRequest(value, value.RandomSeed, false), Family(value.Policy.Depth));
        return Result(value, generated, value.AnimationFrame);
    }

    /** Native objects.cpp OperateArmorStand: consume the coin at every depth, including the forced light-armor branch. */
    private static AuthoritativeNativeThemeItemLootResult OperateArmorStand(AuthoritativeNativeThemeItemLootRequest value)
    {
        var rng = new AuthoritativeLegacyRandom(value.RandomSeed);
        var uniqueRnd = rng.GenerateRnd(2) != 0;
        return value.Policy.Depth <= 5
            ? CreateTypeItem(value, rng.State, true, 6, 0, value.AnimationFrame + 1)
            : CreateTypeItem(value, rng.State, uniqueRnd, 8, 0, value.AnimationFrame + 1);
    }

    /** Native objects.cpp OperateWeaponRack: PickRandomlyAmong(Sword, Axe, Bow, Mace), then family quality. */
    private static AuthoritativeNativeThemeItemLootResult OperateWeaponRack(AuthoritativeNativeThemeItemLootRequest value)
    {
        var rng = new AuthoritativeLegacyRandom(value.RandomSeed);
        int[] types = [1, 2, 3, 4];
        var weaponType = types[rng.GenerateRnd(types.Length)];
        return CreateTypeItem(value, rng.State, value.Policy.Depth >= 5, weaponType, 0, value.AnimationFrame + 1);
    }

    private static AuthoritativeNativeThemeItemLootResult CreateTypeItem(AuthoritativeNativeThemeItemLootRequest value,
        uint state, bool good, int type, int misc, int frame)
        => Result(value, AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(
            ItemRequest(value, state, good) with { RequestedItemType = type, RequestedMiscId = misc },
            Family(value.Policy.Depth)), frame);

    private static AuthoritativeCathedralThemeItemWorldRequest ItemRequest(AuthoritativeNativeThemeItemLootRequest value,
        uint state, bool good) => new(value.WorldState, value.Catalog, value.Policy, state,
            value.PositionX, value.PositionY, good, Player: value.Player);

    private static AuthoritativeNativeDungeonFamilyContext Family(int depth)
        => depth <= 4 ? AuthoritativeNativeDungeonFamilyContext.Cathedral(depth)
            : AuthoritativeNativeDungeonFamilyContext.Catacombs(depth);

    private static AuthoritativeNativeThemeItemLootResult Result(AuthoritativeNativeThemeItemLootRequest value,
        AuthoritativeCathedralThemeItemWorldResult generated, int frame)
        => new(new(generated.WorldState, value.RandomSeed, generated.FinalRandomState,
            [new(generated.SelectedNativeItemIndex, generated.SelectedSlot, generated.ItemSeed,
                generated.Item, generated.Placement)]), frame);
}
