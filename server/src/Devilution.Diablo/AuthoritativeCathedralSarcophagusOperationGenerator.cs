namespace Devilution.Diablo;

/** Detached result for one exact native pre-spawn activation. */
public sealed record AuthoritativeCathedralSarcophagusSpawnPlan(
    int NativeMonsterSlot,
    uint EntityId,
    int NativeTypeId,
    int PositionX,
    int PositionY,
    int Facing,
    bool IsCenterSpawn,
    int SpecialFrameCount,
    int SpecialTicksPerFrame);

/** One possible CreateRndItem call made by OperateSarcophagus. */
public sealed record AuthoritativeCathedralSarcophagusLootEntry(
    int SelectedNativeItemIndex,
    int Slot,
    uint ItemSeed,
    AuthoritativeCathedralInitialItemState? Item,
    AuthoritativeCathedralThemeItemPlacementResult Placement)
{
    public bool Allocated => Slot >= 0;
    public bool Visible => Allocated && Placement.Succeeded;
}

/** Inputs at the ordinary untrapped native Cathedral sarcophagus operation boundary. */
public sealed record AuthoritativeCathedralSarcophagusOperationRequest(
    AuthoritativeCathedralThemeItemWorldState WorldState,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    AuthoritativeCathedralNativeSarcophagusFacts Sarcophagus,
    IReadOnlyList<bool> SpawnTilesAvailable,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null);

/** Detached item/RNG/actor-placement result; object and actor mutation stay with the owner. */
public sealed class AuthoritativeCathedralSarcophagusOperationResult
{
    internal AuthoritativeCathedralSarcophagusOperationResult(
        AuthoritativeCathedralThemeItemWorldState worldState,
        uint initialRandomState,
        uint finalRandomState,
        AuthoritativeCathedralSarcophagusLootEntry? loot,
        AuthoritativeCathedralSarcophagusSpawnPlan? spawn)
    {
        WorldState = worldState;
        InitialRandomState = initialRandomState;
        FinalRandomState = finalRandomState;
        Loot = loot;
        Spawn = spawn;
    }

    public AuthoritativeCathedralThemeItemWorldState WorldState { get; }
    public uint InitialRandomState { get; }
    public uint FinalRandomState { get; }
    public AuthoritativeCathedralSarcophagusLootEntry? Loot { get; }
    public AuthoritativeCathedralSarcophagusSpawnPlan? Spawn { get; }
}

/**
 * Detached ordinary-Diablo OperateSarcophagus behavior for owned Cathedral
 * depths 1 and 2. Native loot and linked-skeleton placement share the sarcophagus
 * seed, while object animation and actor activation are committed by the owner.
 */
public static class AuthoritativeCathedralSarcophagusOperationGenerator
{
    private const int NativeSarcophagusType = 48;
    private const int NativeDirectionSouthWest = 1;
    private static readonly int[] NeighborDirectionOrder = [4, 5, 6, 3, 7, 2, 1, 0];
    private static readonly int[] DirectionX = [1, 0, -1, -1, -1, 0, 1, 1];
    private static readonly int[] DirectionY = [1, 1, 1, 0, -1, -1, -1, 0];

    public static AuthoritativeCathedralSarcophagusOperationResult Generate(
        AuthoritativeCathedralSarcophagusOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorldState);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        ArgumentNullException.ThrowIfNull(request.Policy);
        ArgumentNullException.ThrowIfNull(request.Sarcophagus);
        ArgumentNullException.ThrowIfNull(request.SpawnTilesAvailable);

        ValidatePolicy(request.Policy);
        var facts = request.Sarcophagus;
        ValidateFacts(facts, request.WorldState);
        if (request.SpawnTilesAvailable.Count != 9)
            throw new InvalidDataException("Native sarcophagus placement requires center and eight ordered neighbor cells.");

        // OperateSarcophagus clears selection before CreateRndItem and actor placement.
        var objectCanInteract = request.WorldState.ObjectCanInteract.ToArray();
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
            request.WorldState.ObjectSolid,
            objectCanInteract,
            request.WorldState.UniqueItemFlags);

        var randomState = facts.RandomSeed;
        AuthoritativeCathedralSarcophagusLootEntry? loot = null;
        AuthoritativeCathedralSarcophagusSpawnPlan? spawn = null;
        if (facts.Var1 <= 2) {
            var generated = AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(
                new AuthoritativeCathedralThemeItemWorldRequest(
                    worldState,
                    request.Catalog,
                    request.Policy,
                    randomState,
                    facts.PositionX,
                    facts.PositionY,
                    OnlyGood: false,
                    Player: request.Player));
            worldState = generated.WorldState;
            randomState = generated.FinalRandomState;
            loot = new(
                generated.SelectedNativeItemIndex,
                generated.SelectedSlot,
                generated.ItemSeed,
                generated.Item,
                generated.Placement);
        } else if (facts.Var1 >= 8 && facts.Var2 >= 0) {
            var linked = facts.LinkedMonster
                ?? throw new InvalidDataException("Native sarcophagus does not retain its linked pre-spawn actor.");
            var available = request.SpawnTilesAvailable;
            if (available[0]) {
                spawn = CreateSpawnPlan(facts, linked, facts.PositionX, facts.PositionY,
                    NativeDirectionSouthWest, true);
            } else {
                var candidates = new List<int>(NeighborDirectionOrder.Length);
                for (var index = 0; index < NeighborDirectionOrder.Length; index++) {
                    if (!available[index + 1])
                        continue;
                    var direction = NeighborDirectionOrder[index];
                    var x = checked(facts.PositionX + DirectionX[direction]);
                    var y = checked(facts.PositionY + DirectionY[direction]);
                    if ((uint)x >= AuthoritativeCathedralThemeItemWorldState.Width
                        || (uint)y >= AuthoritativeCathedralThemeItemWorldState.Height)
                        throw new InvalidDataException("Native sarcophagus marks an out-of-bounds neighbor as available.");
                    candidates.Add(direction);
                }

                if (candidates.Count != 0) {
                    var random = new AuthoritativeLegacyRandom(randomState);
                    // spawnOk.count() is size_t in native code, so a negative
                    // GenerateRnd value is converted to 64-bit unsigned before %.
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
                        false);
                }
            }
        }

        return new(worldState, facts.RandomSeed, randomState, loot, spawn);
    }

    private static void ValidatePolicy(AuthoritativeCathedralThemeItemPolicy policy)
    {
        if (policy.Depth is < 1 or > 2
            || policy.CurrentDungeonLevel != 0 && policy.CurrentDungeonLevel != policy.Depth
            || policy.Hellfire || policy.SetLevel
            || policy.GameMode != DiabloGameMode.SinglePlayer
            || policy.Difficulty != DiabloDifficulty.Normal
            || policy.Spawn || policy.QuestEnabled
            || !policy.LegacyLevelItemGenerationAllowed)
            throw new InvalidDataException("Native sarcophagus operations are limited to ordinary original-Diablo Cathedral depths 1 and 2.");
    }

    private static void ValidateFacts(
        AuthoritativeCathedralNativeSarcophagusFacts facts,
        AuthoritativeCathedralThemeItemWorldState worldState)
    {
        if (facts.NativeTypeId != NativeSarcophagusType || facts.EntityId == 0 || facts.LevelId == 0
            || facts.ObjectSlot < 0 || facts.ObjectSlot >= worldState.ObjectCanInteract.Count
            || facts.SelectionRegion == 0 || facts.TrapArmed || facts.HasLinkedTrap
            || facts.Var1 is < 0 or > 9
            || (uint)facts.PositionX >= AuthoritativeCathedralThemeItemWorldState.Width
            || facts.PositionY is <= 0 or >= AuthoritativeCathedralThemeItemWorldState.Height
            || !worldState.ObjectCanInteract[facts.ObjectSlot])
            throw new InvalidDataException("Native sarcophagus facts are outside the supported closed Cathedral operation boundary.");

        var referenceIndex = checked((facts.PositionY - 1) * AuthoritativeCathedralThemeItemWorldState.Width
            + facts.PositionX);
        if (worldState.ObjectRefs[referenceIndex] != -(facts.ObjectSlot + 1))
            throw new InvalidDataException("Native sarcophagus signed object footprint does not match its raw object slot.");
        var originIndex = checked(facts.PositionY * AuthoritativeCathedralThemeItemWorldState.Width
            + facts.PositionX);
        if (worldState.ObjectRefs[originIndex] != facts.ObjectSlot + 1)
            throw new InvalidDataException("Native sarcophagus origin object reference does not match its raw object slot.");

        if (facts.Var1 < 8 || facts.Var2 < 0)
            return;

        var linked = facts.LinkedMonster
            ?? throw new InvalidDataException("Native sarcophagus has no matching pre-spawn actor binding.");
        linked.Validate();
        if (linked.Binding.NativeSlot != facts.Var2
            || linked.Binding.LevelId != facts.LevelId
            || linked.Binding.EntityId == 0
            || linked.InitialState.Slot != facts.Var2
            || linked.InitialState.Type != linked.Binding.NativeTypeId
            || facts.NativeSpecialFrameCount <= 0
            || facts.NativeSpecialTicksPerFrame < 0)
            throw new InvalidDataException("Native sarcophagus pre-spawn actor binding or Special timing is inconsistent.");
    }

    private static AuthoritativeCathedralSarcophagusSpawnPlan CreateSpawnPlan(
        AuthoritativeCathedralNativeSarcophagusFacts facts,
        AuthoritativeNativeMonsterInitialization linked,
        int x,
        int y,
        int facing,
        bool center)
        => new(
            linked.Binding.NativeSlot,
            linked.Binding.EntityId,
            linked.Binding.NativeTypeId,
            x,
            y,
            facing,
            center,
            facts.NativeSpecialFrameCount,
            facts.NativeSpecialTicksPerFrame);
}
