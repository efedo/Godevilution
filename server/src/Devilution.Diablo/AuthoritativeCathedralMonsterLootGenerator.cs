using Devilution.Engine;

namespace Devilution.Diablo;

/** Native death-time branch reached by ordinary Cathedral SpawnLoot. */
public enum AuthoritativeCathedralMonsterLootOutcome
{
    DroppedItem,
    NoDropFlag,
    NoDropRoll,
    PoolFull,
    MinionSkipped,
}

/**
 * Inputs at the MonsterDeath -> SpawnLoot boundary.  MonsterItemSeed is the
 * native rndItemSeed stream boundary; it replaces the ambient RNG before any
 * branch is evaluated.  Raw MonsterData level and effective Monster::level
 * intentionally remain separate because native uses them in different calls.
 */
public sealed record AuthoritativeCathedralMonsterLootRequest(
    AuthoritativeCathedralThemeItemWorldState WorldState,
    AuthoritativeCathedralThemeItemCatalog Catalog,
    AuthoritativeCathedralThemeItemPolicy Policy,
    AuthoritativeCathedralMonsterRuntimeCatalog MonsterCatalog,
    int MonsterId,
    uint MonsterItemSeed,
    int PositionX,
    int PositionY,
    bool IsUnique = false,
    bool IsPlayerMinion = false,
    AuthoritativeCathedralUniqueDefinition? Unique = null,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null);

/** Detached result of one bounded ordinary Cathedral death-time loot branch. */
public sealed class AuthoritativeCathedralMonsterLootResult
{
    internal AuthoritativeCathedralMonsterLootResult(
        AuthoritativeCathedralThemeItemWorldState worldState,
        AuthoritativeCathedralMonsterLootOutcome outcome,
        uint initialRandomState,
        uint finalRandomState,
        int rawDataLevel,
        uint effectiveMonsterLevel,
        int selectedNativeItemIndex,
        int selectedSlot,
        uint itemSeed,
        AuthoritativeCathedralInitialItemState? item,
        AuthoritativeCathedralThemeItemPlacementResult placement)
    {
        WorldState = worldState;
        Outcome = outcome;
        InitialRandomState = initialRandomState;
        FinalRandomState = finalRandomState;
        RawDataLevel = rawDataLevel;
        EffectiveMonsterLevel = effectiveMonsterLevel;
        SelectedNativeItemIndex = selectedNativeItemIndex;
        SelectedSlot = selectedSlot;
        ItemSeed = itemSeed;
        Item = item;
        Placement = placement;
    }

    public AuthoritativeCathedralThemeItemWorldState WorldState { get; }
    public AuthoritativeCathedralMonsterLootOutcome Outcome { get; }
    public uint InitialRandomState { get; }
    public uint FinalRandomState { get; }
    public int RawDataLevel { get; }
    public uint EffectiveMonsterLevel { get; }
    public int SelectedNativeItemIndex { get; }
    public int SelectedSlot { get; }
    public uint ItemSeed { get; }
    public AuthoritativeCathedralInitialItemState? Item { get; }
    public AuthoritativeCathedralThemeItemPlacementResult Placement { get; }
    public bool Dropped => Outcome == AuthoritativeCathedralMonsterLootOutcome.DroppedItem;
    public bool PlacementSucceeded => Placement.Succeeded;
    public int ActiveItemCount => WorldState.ActiveItemCount;
    public IReadOnlyList<AuthoritativeCathedralInitialItemState> Items => WorldState.Items;
    public IReadOnlyList<int> SignedItemGrid => WorldState.SignedItemGrid;
    public IReadOnlySet<int> UniqueItemFlags => WorldState.UniqueItemFlags;
}

/**
 * Bounded native-compatible ordinary Diablo Cathedral monster loot.  Quest,
 * Hellfire, special-treasure, and generation-time prefab branches are outside
 * this compositor and are rejected by the validated policy/catalog boundary.
 */
public static class AuthoritativeCathedralMonsterLootGenerator
{
    private const int MiscBook = 24;
    private const int ItemTypeGold = 11;
    private const int FirstHellfireMonsterId = 111;
    private const int HorkDemonUniqueId = 10;
    private const int DefilerUniqueId = 11;
    private const int NaKrulUniqueId = 12;

    public static AuthoritativeCathedralMonsterLootResult SpawnLoot(
        AuthoritativeCathedralMonsterLootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var monster = request.MonsterCatalog.Get(request.MonsterId);
        var rawDataLevel = monster.Level;
        var effectiveMonsterLevel = request.MonsterCatalog.GetEffectiveLevel(
            request.MonsterId, request.Policy.Difficulty, request.Unique);
        var initialRandomState = request.MonsterItemSeed;
        var random = new AuthoritativeLegacyRandom(initialRandomState);

        // SpawnLoot excludes player minions before calling SpawnItem, but the
        // MonsterDeath boundary has already restored rndItemSeed.
        if (request.IsPlayerMinion)
            return Result(request, AuthoritativeCathedralMonsterLootOutcome.MinionSkipped,
                initialRandomState, random.State, rawDataLevel, effectiveMonsterLevel);

        // In single-player full-quest mode this branch calls SpawnUnique and is
        // not an ordinary generated item.  Never fabricate its quest reward.
        if (request.MonsterCatalog.HasUniqueTreasure(request.MonsterId))
            throw new NotSupportedException(
                "Cathedral special-treasure death loot is outside the ordinary compositor.");

        int selectedNativeItemIndex;
        if (request.IsUnique) {
            selectedNativeItemIndex = SelectUniqueItem(random, effectiveMonsterLevel, request);
        } else {
            if (request.MonsterCatalog.HasNoDrop(request.MonsterId))
                return Result(request, AuthoritativeCathedralMonsterLootOutcome.NoDropFlag,
                    initialRandomState, random.State, rawDataLevel, effectiveMonsterLevel);

            // Native SpawnItem performs the monster-level no-drop gate before
            // RndItemForMonsterLevel's gold and weighted item selection.
            if (random.GenerateRnd(100) >= 100 - request.Catalog.Rules.NoDropPercent)
                return Result(request, AuthoritativeCathedralMonsterLootOutcome.NoDropRoll,
                    initialRandomState, random.State, rawDataLevel, effectiveMonsterLevel);

            selectedNativeItemIndex = random.GenerateRnd(100)
                < request.Catalog.Rules.GoldPercent
                ? FindGoldItem(request.Catalog)
                : SelectWeightedItem(random, request.Catalog,
                    item => item.MinimumMonsterLevel <= unchecked((sbyte)effectiveMonsterLevel),
                    considerDropRate: true);
        }

        // Native selection happens even when AllocateItem subsequently refuses
        // a full ActiveItems pool.  No placement or item seed draw follows.
        if (request.WorldState.ActiveItemCount >= AuthoritativeCathedralThemeItemWorldState.ActiveCapacity)
            return Result(request, AuthoritativeCathedralMonsterLootOutcome.PoolFull,
                initialRandomState, random.State, rawDataLevel, effectiveMonsterLevel,
                selectedNativeItemIndex);

        var worldRequest = new AuthoritativeCathedralThemeItemWorldRequest(
            request.WorldState,
            request.Catalog,
            request.Policy,
            random.State,
            request.PositionX,
            request.PositionY,
            request.IsUnique,
            PreGenerate: false,
            Player: request.Player);
        var worldResult = AuthoritativeCathedralThemeItemWorldGenerator.CreateSelectedItem(
            worldRequest,
            selectedNativeItemIndex,
            random.State,
            rawDataLevel,
            request.IsUnique
                ? request.Catalog.Rules.UniqueChanceUnique
                : request.Catalog.Rules.UniqueChanceNormal);
        return new AuthoritativeCathedralMonsterLootResult(
            worldResult.WorldState,
            AuthoritativeCathedralMonsterLootOutcome.DroppedItem,
            initialRandomState,
            worldResult.FinalRandomState,
            rawDataLevel,
            effectiveMonsterLevel,
            worldResult.SelectedNativeItemIndex,
            worldResult.SelectedSlot,
            worldResult.ItemSeed,
            worldResult.Item,
            worldResult.Placement);
    }

    public static AuthoritativeCathedralMonsterLootResult Generate(
        AuthoritativeCathedralMonsterLootRequest request)
        => SpawnLoot(request);

    private static int SelectUniqueItem(
        AuthoritativeLegacyRandom random,
        uint effectiveMonsterLevel,
        AuthoritativeCathedralMonsterLootRequest request)
    {
        // RndUItem stores Monster::level in a native signed int (unlike the
        // int8 cast in RndItemForMonsterLevel). Preserve that conversion.
        var nativeLevel = unchecked((int)effectiveMonsterLevel);
        return SelectWeightedItem(random, request.Catalog,
            item => item.MinimumMonsterLevel <= nativeLevel
                || item.ItemType == 0 && item.MiscId == MiscBook,
            considerDropRate: false,
            item => item.ItemType != ItemTypeGold
                && (item.ItemType != 0 || item.MiscId == MiscBook));
    }

    private static int SelectWeightedItem(
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        Func<AuthoritativeCathedralNativeItemDefinition, bool> predicate,
        bool considerDropRate,
        Func<AuthoritativeCathedralNativeItemDefinition, bool>? extraPredicate = null)
    {
        var candidates = catalog.Items
            .Where(item => item.Available && item.DropRate > 0
                && item.SpellId is not 32 and not 34
                && predicate(item)
                && (extraPredicate?.Invoke(item) ?? true))
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidDataException("Native monster loot has no eligible item row.");

        var cumulative = 0;
        var weighted = new (AuthoritativeCathedralNativeItemDefinition Item, int Cumulative)[candidates.Length];
        for (var index = 0; index < candidates.Length; index++) {
            cumulative = checked(cumulative + (considerDropRate ? candidates[index].DropRate : 1));
            weighted[index] = (candidates[index], cumulative);
        }
        var target = random.RandomIntLessThan(cumulative);
        foreach (var row in weighted)
            if (target < row.Cumulative)
                return row.Item.NativeItemIndex;
        throw new InvalidDataException("Native monster loot produced an unsafe weighted index.");
    }

    private static int FindGoldItem(AuthoritativeCathedralThemeItemCatalog catalog)
    {
        var gold = catalog.Items.SingleOrDefault(item => item.NativeItemIndex == 0);
        if (gold is null || !gold.Available || gold.ItemType != ItemTypeGold
            || gold.CursorGraphic == byte.MaxValue)
            throw new InvalidDataException(
                "Native monster loot requires an available Gold ItemData row at IDI_GOLD index 0.");
        return 0;
    }

    private static AuthoritativeCathedralMonsterLootResult Result(
        AuthoritativeCathedralMonsterLootRequest request,
        AuthoritativeCathedralMonsterLootOutcome outcome,
        uint initialRandomState,
        uint finalRandomState,
        int rawDataLevel,
        uint effectiveMonsterLevel,
        int selectedNativeItemIndex = -1)
        => new(
            request.WorldState,
            outcome,
            initialRandomState,
            finalRandomState,
            rawDataLevel,
            effectiveMonsterLevel,
            selectedNativeItemIndex,
            -1,
            0,
            null,
            new(false, false, 0, 0));

    private static void ValidateRequest(AuthoritativeCathedralMonsterLootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.WorldState);
        ArgumentNullException.ThrowIfNull(request.Catalog);
        ArgumentNullException.ThrowIfNull(request.Policy);
        ArgumentNullException.ThrowIfNull(request.MonsterCatalog);
        if (request.Policy.Depth is < 1 or > 4
            || request.Policy.CurrentDungeonLevel is < 0 or > 4
            || request.Policy.GameMode != DiabloGameMode.SinglePlayer
            || request.Policy.Difficulty != DiabloDifficulty.Normal
            || request.Policy.Hellfire || request.Policy.Spawn
            || request.Policy.QuestEnabled || request.Policy.SetLevel
            || !request.Policy.LegacyLevelItemGenerationAllowed
            || request.Policy.CurrentDungeonLevel != 0
                && request.Policy.CurrentDungeonLevel != request.Policy.Depth)
            throw new InvalidDataException(
                "Only ordinary original-Diablo Cathedral SP Normal monster loot is supported.");
        if (request.MonsterId < 0 || request.MonsterId >= AuthoritativeCathedralMonsterRuntimeCatalog.NativeMonsterCount)
            throw new InvalidDataException("Native monster loot requires a valid runtime monster ID.");
        if (request.MonsterId >= FirstHellfireMonsterId)
            throw new NotSupportedException(
                "Hellfire monster death loot is outside the ordinary Cathedral compositor.");
        if (!request.IsUnique && request.Unique is not null)
            throw new InvalidDataException("Unique metadata cannot be supplied for an ordinary monster.");
        if (request.IsUnique && request.Unique is null)
            throw new InvalidDataException("Unique monster loot requires validated unique metadata.");
        var monster = request.MonsterCatalog.Get(request.MonsterId);
        if (request.Unique is not null
            && (request.Unique.NativeUniqueId is < 0 or >= 100
                || request.Unique.QuestOnly
                || request.Unique.NativeUniqueId is HorkDemonUniqueId or DefilerUniqueId or NaKrulUniqueId
                || request.Unique.MonsterType != monster.NativeMonsterId))
            throw new InvalidDataException(
                "Unique metadata must bind the active native monster and be non-quest.");
        if (request.Player is not null
            && (request.Player.MaximumManaBase < 0 || request.Player.MaximumLifeBase < 0))
            throw new InvalidDataException("Native player item-power bases cannot be negative.");
        if ((uint)request.PositionX >= AuthoritativeCathedralThemeItemWorldState.Width
            || (uint)request.PositionY >= AuthoritativeCathedralThemeItemWorldState.Height)
            throw new InvalidDataException("Native monster loot placement origin is outside the level.");
    }
}
