namespace Devilution.Diablo;

/** One native CreateRndItem attempt made by an ordinary barrel. */
public sealed record AuthoritativeCathedralBarrelLootEntry(
    int SelectedNativeItemIndex,
    int Slot,
    uint ItemSeed,
    AuthoritativeCathedralInitialItemState? Item,
    AuthoritativeCathedralThemeItemPlacementResult Placement)
{
    public bool Allocated => Slot >= 0;
    public bool Visible => Allocated && Placement.Succeeded;
}

/** Exact mutation applied by native BreakBarrel before loot or blast effects. */
public sealed record AuthoritativeCathedralBarrelOpenedMutation(
    uint AnimationFlag,
    uint AnimationFrame,
    int AnimationDelay,
    bool SolidFlag,
    bool MissilePassable,
    int BreakFlag,
    int SelectionRegion,
    bool PreFlag)
{
    public static AuthoritativeCathedralBarrelOpenedMutation Native { get; } = new(
        AnimationFlag: 1,
        AnimationFrame: 1,
        AnimationDelay: 1,
        SolidFlag: false,
        MissilePassable: true,
        BreakFlag: -1,
        SelectionRegion: 0,
        PreFlag: true);
}

/** Detached native ActivateSkeleton result for an exact pre-spawn slot. */
public sealed record AuthoritativeCathedralBarrelSpawnPlan(
    int NativeMonsterSlot,
    uint EntityId,
    int NativeTypeId,
    int PositionX,
    int PositionY,
    int Facing,
    bool IsCenterSpawn,
    int SpecialFrameCount,
    int SpecialTicksPerFrame);

/** Inputs at the supported ordinary untrapped original-Diablo L1 break boundary. */
public sealed record AuthoritativeCathedralBarrelOperationRequest(
    AuthoritativeCathedralThemeItemWorldState WorldState,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    AuthoritativeCathedralNativeBarrelFacts Barrel,
    IReadOnlyList<bool> SpawnTilesAvailable,
    uint InitialWorldRandomState,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null);

/** Detached item/RNG/spawn result. Blast damage and recursive chains belong to combat. */
public sealed class AuthoritativeCathedralBarrelOperationResult
{
    internal AuthoritativeCathedralBarrelOperationResult(
        AuthoritativeCathedralThemeItemWorldState worldState,
        uint initialWorldRandomState,
        uint finalWorldRandomState,
        bool explosive,
        AuthoritativeCathedralBarrelLootEntry? loot,
        AuthoritativeCathedralBarrelSpawnPlan? spawn,
        AuthoritativeCathedralBarrelOpenedMutation openedMutation)
    {
        WorldState = worldState;
        InitialWorldRandomState = initialWorldRandomState;
        FinalWorldRandomState = finalWorldRandomState;
        IsExplosive = explosive;
        Loot = loot;
        Spawn = spawn;
        OpenedMutation = openedMutation;
    }

    public AuthoritativeCathedralThemeItemWorldState WorldState { get; }
    public uint InitialWorldRandomState { get; }
    public uint FinalWorldRandomState { get; }
    public bool IsExplosive { get; }
    public AuthoritativeCathedralBarrelLootEntry? Loot { get; }
    public AuthoritativeCathedralBarrelSpawnPlan? Spawn { get; }
    public AuthoritativeCathedralBarrelOpenedMutation OpenedMutation { get; }
}

/** Detached ordinary-Diablo BreakBarrel item and pre-spawn behavior. */
public static class AuthoritativeCathedralBarrelOperationGenerator
{
    public const int OrdinaryBarrelType = 57;
    public const int ExplosiveBarrelType = 58;
    private const int NativeDirectionSouthWest = 1;
    private static readonly int[] NativeNeighborDirections = [4, 5, 6, 3, 7, 2, 1, 0];
    private static readonly int[] DirectionX = [1, 0, -1, -1, -1, 0, 1, 1];
    private static readonly int[] DirectionY = [1, 1, 1, 0, -1, -1, -1, 0];

    public static AuthoritativeCathedralBarrelOperationResult Generate(
        AuthoritativeCathedralBarrelOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorldState);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        ArgumentNullException.ThrowIfNull(request.Policy);
        ArgumentNullException.ThrowIfNull(request.Barrel);
        ArgumentNullException.ThrowIfNull(request.SpawnTilesAvailable);
        ValidatePolicy(request.Policy);

        var facts = request.Barrel;
        ValidateFacts(facts, request.WorldState);
        if (request.SpawnTilesAvailable.Count != 9)
            throw new InvalidDataException("Native barrel activation requires center and eight ordered neighbor cells.");

        // BreakBarrel clears selection and removes object solidity before item placement.
        var objectSolid = request.WorldState.ObjectSolid.ToArray();
        var objectCanInteract = request.WorldState.ObjectCanInteract.ToArray();
        objectSolid[facts.ObjectSlot] = false;
        objectCanInteract[facts.ObjectSlot] = false;
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
            objectSolid,
            objectCanInteract,
            request.WorldState.UniqueItemFlags);

        if (facts.IsExplosive) {
            // Native explosive barrels neither SetRndSeed nor make item/actor draws.
            return new(worldState, request.InitialWorldRandomState, request.InitialWorldRandomState,
                explosive: true, null, null, AuthoritativeCathedralBarrelOpenedMutation.Native);
        }

        var randomState = facts.RandomSeed;
        AuthoritativeCathedralBarrelLootEntry? loot = null;
        AuthoritativeCathedralBarrelSpawnPlan? spawn = null;
        if (facts.Var2 <= 1) {
            var itemRequest = new AuthoritativeCathedralThemeItemWorldRequest(
                worldState,
                request.Catalog,
                request.Policy,
                randomState,
                facts.PositionX,
                facts.PositionY,
                OnlyGood: false,
                Player: request.Player);
            var generated = facts.Var3 == 0
                ? AuthoritativeCathedralThemeItemWorldGenerator.CreateUsefulItem(itemRequest, 1)
                : AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(itemRequest);
            worldState = generated.WorldState;
            randomState = generated.FinalRandomState;
            loot = new(generated.SelectedNativeItemIndex, generated.SelectedSlot,
                generated.ItemSeed, generated.Item, generated.Placement);
        }

        if (facts.Var2 >= 8 && facts.Var4 >= 0 && facts.LinkedMonster is { } linked) {
            var available = request.SpawnTilesAvailable;
            if (available[0]) {
                spawn = CreateSpawnPlan(facts, linked, facts.PositionX, facts.PositionY,
                    NativeDirectionSouthWest, isCenter: true);
            } else {
                var candidates = new List<int>(NativeNeighborDirections.Length);
                for (var index = 0; index < NativeNeighborDirections.Length; index++) {
                    if (!available[index + 1])
                        continue;
                    var direction = NativeNeighborDirections[index];
                    var x = checked(facts.PositionX + DirectionX[direction]);
                    var y = checked(facts.PositionY + DirectionY[direction]);
                    if ((uint)x >= AuthoritativeCathedralThemeItemWorldState.Width
                        || (uint)y >= AuthoritativeCathedralThemeItemWorldState.Height)
                        throw new InvalidDataException("Native barrel marks an out-of-bounds spawn neighbor as available.");
                    candidates.Add(direction);
                }

                if (candidates.Count != 0) {
                    var random = new AuthoritativeLegacyRandom(randomState);
                    var nativeChoice = random.GenerateRnd(15);
                    var selected = unchecked((int)(unchecked((ulong)(long)nativeChoice)
                        % (ulong)candidates.Count));
                    var direction = candidates[selected];
                    randomState = random.State;
                    spawn = CreateSpawnPlan(
                        facts,
                        linked,
                        checked(facts.PositionX + DirectionX[direction]),
                        checked(facts.PositionY + DirectionY[direction]),
                        (direction + 4) % 8,
                        isCenter: false);
                }
            }
        }

        return new(worldState, request.InitialWorldRandomState, randomState,
            explosive: false, loot, spawn, AuthoritativeCathedralBarrelOpenedMutation.Native);
    }

    private static void ValidatePolicy(AuthoritativeCathedralThemeItemPolicy policy)
    {
        if (policy.Depth != 1 || policy.CurrentDungeonLevel is not (0 or 1)
            || policy.Hellfire || policy.SetLevel
            || policy.GameMode != DiabloGameMode.SinglePlayer
            || policy.Difficulty != DiabloDifficulty.Normal
            || policy.Spawn || policy.QuestEnabled
            || !policy.LegacyLevelItemGenerationAllowed)
            throw new InvalidDataException("Native barrel operations are limited to ordinary original-Diablo Cathedral level 1.");
    }

    private static void ValidateFacts(
        AuthoritativeCathedralNativeBarrelFacts facts,
        AuthoritativeCathedralThemeItemWorldState worldState)
    {
        if (facts.NativeTypeId is not (OrdinaryBarrelType or ExplosiveBarrelType)
            || facts.EntityId == 0 || facts.LevelId == 0
            || facts.ObjectSlot < 0 || facts.ObjectSlot >= worldState.ObjectCanInteract.Count
            || facts.ObjectSlot >= worldState.ObjectSolid.Count
            || facts.SelectionRegion == 0 || facts.TrapArmed || facts.HasLinkedTrap
            || facts.Var1 != 0 || facts.Var3 is < 0 or > 2
            || (uint)facts.PositionX >= AuthoritativeCathedralThemeItemWorldState.Width
            || (uint)facts.PositionY >= AuthoritativeCathedralThemeItemWorldState.Height
            || !facts.InitialSolid
            || facts.AnimationFlag != 0 || facts.BreakFlag < 0
            || !worldState.ObjectCanInteract[facts.ObjectSlot]
            || !worldState.ObjectSolid[facts.ObjectSlot])
            throw new InvalidDataException("Native barrel facts are outside the supported closed L1 melee boundary.");

        if (facts.IsExplosive) {
            if (facts.Var2 != 0 || facts.LinkedMonster is not null || facts.NativeSpecialFrameCount != 0)
                throw new InvalidDataException("Explosive native barrels cannot carry ordinary loot or pre-spawn bindings.");
        } else {
            if (facts.Var2 is < 0 or > 9)
                throw new InvalidDataException("Ordinary native barrel Var2 is outside its generated range.");
            if (facts.Var2 >= 8 && facts.Var4 >= 0) {
                var linked = facts.LinkedMonster
                    ?? throw new InvalidDataException("Native ordinary barrel lost its exact linked pre-spawn actor.");
                linked.Validate();
                if (linked.Binding.NativeSlot != facts.Var4
                    || linked.Binding.LevelId != facts.LevelId
                    || linked.Binding.EntityId == 0
                    || linked.InitialState.Slot != facts.Var4
                    || linked.InitialState.Type != linked.Binding.NativeTypeId
                    || facts.NativeSpecialFrameCount <= 0
                    || facts.NativeSpecialTicksPerFrame < 0)
                    throw new InvalidDataException("Native barrel pre-spawn actor or Special timing is inconsistent.");
            } else if (facts.LinkedMonster is not null) {
                throw new InvalidDataException("Native barrel carries an unreferenced pre-spawn actor.");
            }
        }

        var originIndex = checked(facts.PositionY * AuthoritativeCathedralThemeItemWorldState.Width
            + facts.PositionX);
        if (worldState.ObjectRefs[originIndex] != facts.ObjectSlot + 1)
            throw new InvalidDataException("Native barrel origin reference does not match its raw object slot.");
    }

    private static AuthoritativeCathedralBarrelSpawnPlan CreateSpawnPlan(
        AuthoritativeCathedralNativeBarrelFacts facts,
        AuthoritativeNativeMonsterInitialization linked,
        int x,
        int y,
        int facing,
        bool isCenter)
        => new(
            linked.Binding.NativeSlot,
            linked.Binding.EntityId,
            linked.Binding.NativeTypeId,
            x,
            y,
            facing,
            isCenter,
            facts.NativeSpecialFrameCount,
            facts.NativeSpecialTicksPerFrame);
}
