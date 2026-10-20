namespace Devilution.Diablo;

/** One C2 chest command input with raw native identity and trap fields retained. */
public sealed record AuthoritativeCatacombsChestLootRequest(
    AuthoritativeCathedralThemeItemWorldState WorldState,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    AuthoritativeCatacombsNativeChestFacts Chest,
    int CurrentPlayerX,
    int CurrentPlayerY,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null);

/** Exact post-OperateChest object animation and intrinsic-trap projection. */
public sealed record AuthoritativeCatacombsChestOpenedMutation(
    uint AnimationFrame,
    int SelectionRegion,
    bool TrapFlag);

/** Detached Catacombs chest loot and optional intrinsic missile plan. */
public sealed class AuthoritativeCatacombsChestLootResult
{
    internal AuthoritativeCatacombsChestLootResult(
        AuthoritativeCatacombsNativeChestFacts input,
        AuthoritativeCathedralChestLootResult loot,
        AuthoritativeCatacombsChestOpenedMutation openedMutation,
        AuthoritativeCatacombsChestTrapPlan? trapPlan)
    {
        Input = input;
        Loot = loot;
        OpenedMutation = openedMutation;
        TrapPlan = trapPlan;
    }

    public AuthoritativeCatacombsNativeChestFacts Input { get; }
    public AuthoritativeCathedralChestLootResult Loot { get; }
    public AuthoritativeCatacombsChestOpenedMutation OpenedMutation { get; }
    public AuthoritativeCatacombsChestTrapPlan? TrapPlan { get; }
}

/** Native C2 AddChest/OperateChest loot-tier alias and intrinsic trap policy. */
public static class AuthoritativeCatacombsChestLootGenerator
{
    internal static AuthoritativeCatacombsChestLootResult Generate(
        AuthoritativeCatacombsChestLootRequest request,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorldState);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        ArgumentNullException.ThrowIfNull(request.Policy);
        ArgumentNullException.ThrowIfNull(request.Chest);
        familyContext.RequireMatch(request.Policy.Depth);
        if (!familyContext.IsCatacombs
            || request.Policy.Depth is < 5 or > 8
            || request.Policy.CurrentDungeonLevel != 0 && request.Policy.CurrentDungeonLevel != request.Policy.Depth
            || request.Policy.Hellfire || request.Policy.SetLevel
            || request.Policy.GameMode != DiabloGameMode.SinglePlayer
            || request.Policy.Difficulty != DiabloDifficulty.Normal
            || request.Policy.Spawn || request.Policy.QuestEnabled
            || !request.Policy.LegacyLevelItemGenerationAllowed)
            throw new InvalidDataException("Native chest contents require ordinary original-Diablo Catacombs depths 5 through 8.");

        var chest = request.Chest;
        var classification = AuthoritativeCatacombsNativeObjectPolicy.Classify(chest.NativeObjectId);
        if (classification.Operation != AuthoritativeCatacombsNativeObjectOperation.ChestLoot
            || !classification.SupportedInCatacombs)
            throw new InvalidDataException("Catacombs chest type is not admitted for native loot operations.");
        var lootTier = AuthoritativeCatacombsNativeObjectPolicy.LootTierForChestType(chest.NativeObjectId);
        var intrinsicTrap = chest.NativeObjectId is >= 68 and <= 70;
        if (chest.LinkedWallTrap is { } wall) {
            if (intrinsicTrap || wall.Type is not (53 or 54) || wall.Slot == chest.ObjectSlot
                || wall.Slot < 0 || wall.Slot >= request.WorldState.ObjectCanInteract.Count
                || (uint)wall.Position.X >= AuthoritativeCathedralThemeItemWorldState.Width
                || (uint)wall.Position.Y >= AuthoritativeCathedralThemeItemWorldState.Height
                || request.WorldState.ObjectRefs[wall.Position.Y * AuthoritativeCathedralThemeItemWorldState.Width
                    + wall.Position.X] != wall.Slot + 1
                || wall.Var1 != chest.PositionX || wall.Var2 != chest.PositionY)
                throw new InvalidDataException("Native chest wall-trap provenance does not match its retained origin.");
        } else if (chest.TrapFlag && !intrinsicTrap) {
            throw new InvalidDataException("An armed ordinary Catacombs chest requires its exact linked wall-trap row.");
        }
        if (chest.Var1 < 0 || chest.Var1 > lootTier - 4)
            throw new InvalidDataException("Native chest Var1 item count is outside its AddChest tier range.");
        if (chest.Var2 is < 0 or > 7)
            throw new InvalidDataException("Native chest Var2 item branch is outside its generated range.");
        if (intrinsicTrap && chest.Var4 is not 0 and not 1)
            throw new InvalidDataException("Intrinsic Catacombs chest trap Var4 must select Arrow (0) or FireArrow (1).");
        if ((uint)chest.PositionX >= AuthoritativeCathedralThemeItemWorldState.Width
            || (uint)chest.PositionY >= AuthoritativeCathedralThemeItemWorldState.Height
            || (uint)request.CurrentPlayerX >= AuthoritativeCathedralThemeItemWorldState.Width
            || (uint)request.CurrentPlayerY >= AuthoritativeCathedralThemeItemWorldState.Height)
            throw new InvalidDataException("Native chest or current player position is outside the item world.");
        if (chest.ObjectSlot < 0
            || chest.ObjectSlot >= request.WorldState.ObjectCanInteract.Count
            || !request.WorldState.ObjectCanInteract[chest.ObjectSlot]
            || request.WorldState.ObjectRefs[chest.PositionY * AuthoritativeCathedralThemeItemWorldState.Width
                + chest.PositionX] != chest.ObjectSlot + 1)
            throw new InvalidDataException("Native chest must be the selected object at its captured origin.");
        if (chest.AnimationFrame > uint.MaxValue - 2)
            throw new InvalidDataException("Native chest animation frame cannot advance by OperateChest's two-frame step.");

        var itemRequest = new AuthoritativeCathedralChestLootRequest(
            request.WorldState,
            request.Catalog,
            request.Policy,
            (AuthoritativeCathedralOrdinaryChestType)chest.NativeObjectId,
            chest.RandomSeed,
            chest.PositionX,
            chest.PositionY,
            chest.Var1,
            chest.Var2,
            chest.TrapFlag,
            request.Player) {
                ChestObjectSlot = chest.ObjectSlot,
            };
        var loot = AuthoritativeCathedralChestLootGenerator.Generate(itemRequest, familyContext);
        var trapPlan = AuthoritativeCatacombsNativeObjectPolicy.PlanIntrinsicTrap(
            chest, request.CurrentPlayerX, request.CurrentPlayerY);
        return new(
            chest,
            loot,
            new(chest.AnimationFrame + 2, SelectionRegion: 0, TrapFlag: intrinsicTrap ? false : chest.TrapFlag),
            trapPlan);
    }
}
