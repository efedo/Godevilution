using System.Collections.Frozen;

namespace Devilution.Diablo;

/**
 * Complete detached ordinary Cathedral population input. The pre-clear stream,
 * initial-object inputs, encounter catalogs, item inputs, and theme catalogs are
 * all supplied independently; no selected native result is accepted as input.
 */
public sealed class AuthoritativeCathedralFullPopulationRequest
{
    private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
    private const int MonsterCapacity = AuthoritativeCathedralPopulationParameters.MonsterCapacity;
    private const int ItemCapacity = AuthoritativeCathedralInitialItemGenerator.PoolCapacity;
    private const int ActiveItemCapacity = AuthoritativeCathedralInitialItemGenerator.ActiveCapacity;
    private const byte TilePopulated = 1 << 3;

    public AuthoritativeCathedralFullPopulationRequest(
        AuthoritativeCathedralInitialPopulationParameters initialPopulation,
        uint monsterClearRandomState,
        IReadOnlyList<int> initialMonsterTileRefs,
        IReadOnlyList<int> initialPlayerRefs,
        IReadOnlyList<byte> initialTileFlags,
        IReadOnlyList<byte> initialCorpses,
        IReadOnlyList<byte> initialLight,
        IReadOnlyList<byte> initialPreLight,
        AuthoritativeCathedralEncounterLightAllocator lightAllocator,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
        IReadOnlyList<AuthoritativeCathedralUniqueDefinition> orderedUniqueDefinitions,
        int monsterDensityDivisor,
        IReadOnlyList<bool> shrineAvailable,
        AuthoritativeCathedralThemeItemCatalog itemCatalog,
        AuthoritativeCathedralThemeItemPolicy itemPolicy,
        AuthoritativeCathedralThemeItemPlayerContext? itemPlayer,
        IReadOnlyList<AuthoritativeCathedralInitialItemState> initialItemSlots,
        IReadOnlyList<int> initialActiveItemIds,
        int initialActiveItemCount,
        IReadOnlySet<int> initialUniqueItemFlags,
        IReadOnlyList<AuthoritativeCathedralInitialItemTile> initialItemTiles,
        IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> potionDefinitions,
        int maximumPlacementAttempts = 1_000_000,
        int maximumItemPlacementAttempts = 1_000_000,
        IReadOnlyList<bool>? initialVisibleTransparencyRegions = null)
    {
        ArgumentNullException.ThrowIfNull(initialPopulation);
        ArgumentNullException.ThrowIfNull(initialMonsterTileRefs);
        ArgumentNullException.ThrowIfNull(initialPlayerRefs);
        ArgumentNullException.ThrowIfNull(initialTileFlags);
        ArgumentNullException.ThrowIfNull(initialCorpses);
        ArgumentNullException.ThrowIfNull(initialLight);
        ArgumentNullException.ThrowIfNull(initialPreLight);
        ArgumentNullException.ThrowIfNull(lightAllocator);
        ArgumentNullException.ThrowIfNull(triggerPositions);
        ArgumentNullException.ThrowIfNull(orderedUniqueDefinitions);
        ArgumentNullException.ThrowIfNull(shrineAvailable);
        ArgumentNullException.ThrowIfNull(itemCatalog);
        ArgumentNullException.ThrowIfNull(itemPolicy);
        ArgumentNullException.ThrowIfNull(initialItemSlots);
        ArgumentNullException.ThrowIfNull(initialActiveItemIds);
        ArgumentNullException.ThrowIfNull(initialUniqueItemFlags);
        ArgumentNullException.ThrowIfNull(initialItemTiles);
        ArgumentNullException.ThrowIfNull(potionDefinitions);
        ValidatePotionRows(itemCatalog, potionDefinitions);

        if (initialPopulation.Depth != itemPolicy.Depth
            || initialPopulation.Depth is < 1 or > 4
            || itemPolicy.GameMode != DiabloGameMode.SinglePlayer
            || itemPolicy.Difficulty != DiabloDifficulty.Normal
            || itemPolicy.Hellfire || itemPolicy.Spawn || itemPolicy.QuestEnabled || itemPolicy.SetLevel
            || !itemPolicy.LegacyLevelItemGenerationAllowed
            || itemPolicy.CurrentDungeonLevel is < 0 or > 4
            || (itemPolicy.CurrentDungeonLevel != 0 && itemPolicy.CurrentDungeonLevel != itemPolicy.Depth))
            throw new InvalidDataException("Only ordinary original-Diablo Cathedral SP Normal generation is supported.");
        if (maximumPlacementAttempts <= 0 || maximumItemPlacementAttempts <= 0)
            throw new InvalidDataException("Full Cathedral population bounds must be positive.");
        if (monsterDensityDivisor <= 0)
            throw new InvalidDataException("Full Cathedral monster density divisor must be positive.");
        RequireCount(initialMonsterTileRefs, CellCount, nameof(initialMonsterTileRefs));
        RequireCount(initialPlayerRefs, CellCount, nameof(initialPlayerRefs));
        RequireCount(initialTileFlags, CellCount, nameof(initialTileFlags));
        RequireCount(initialCorpses, CellCount, nameof(initialCorpses));
        RequireCount(initialLight, CellCount, nameof(initialLight));
        RequireCount(initialPreLight, CellCount, nameof(initialPreLight));
        RequireCount(shrineAvailable, 26, nameof(shrineAvailable));
        if (!shrineAvailable.Any(value => value))
            throw new InvalidDataException("Ordinary Cathedral shrine generation requires an admitted shrine row.");
        if (initialVisibleTransparencyRegions is not null)
            RequireCount(initialVisibleTransparencyRegions, 256, nameof(initialVisibleTransparencyRegions));

        var preThemeGeometry = initialPopulation.PreThemeGeometry;
        if (!triggerPositions.SequenceEqual(initialPopulation.ThemeTemplate.TriggerPositions))
            throw new InvalidDataException("Encounter trigger positions must match the raw initial-population inputs.");
        if (preThemeGeometry.PlayerOccupied.Any(value => value)
            || preThemeGeometry.MonsterOccupied.Any(value => value))
            throw new InvalidDataException("Fresh full population inputs cannot retain pre-phase player or monster occupancy.");
        for (var index = 0; index < CellCount; index++) {
            if (initialMonsterTileRefs[index] != 0 || initialPlayerRefs[index] != 0)
                throw new InvalidDataException("The full ordinary population requires fresh monster and player tile references.");
            if (((initialTileFlags[index] & TilePopulated) != 0) != preThemeGeometry.Populated[index])
                throw new InvalidDataException("Pre-theme TileFlags must match the supplied raw populated channel.");
        }
        if (lightAllocator.ActiveLightCount != 0)
            throw new InvalidDataException("The full ordinary population requires a fresh inactive light allocator.");
        if (initialItemSlots.Count != ItemCapacity
            || initialActiveItemIds.Count != ActiveItemCapacity
            || initialActiveItemCount != 0
            || !initialActiveItemIds.SequenceEqual(Enumerable.Range(0, ActiveItemCapacity)))
            throw new InvalidDataException("The full ordinary population requires a fresh item pool.");
        foreach (var item in initialItemSlots)
            AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
        if (initialUniqueItemFlags.Any(value => value is < 0 or >= ItemCapacity))
            throw new InvalidDataException("Initial unique-item history contains an unsafe native index.");
        RequireCount(initialItemTiles, CellCount, nameof(initialItemTiles));
        foreach (var tile in initialItemTiles) {
            ArgumentNullException.ThrowIfNull(tile);
            if (tile.PlayerRef != 0 || tile.MonsterRef != 0 || tile.ObjectRef != 0 || tile.ItemRef != 0)
                throw new InvalidDataException("Full ordinary item placement requires fresh tile references.");
        }

        MonsterClearRandomState = monsterClearRandomState;
        InitialPopulation = initialPopulation;
        InitialMonsterTileRefs = Array.AsReadOnly(initialMonsterTileRefs.ToArray());
        InitialPlayerRefs = Array.AsReadOnly(initialPlayerRefs.ToArray());
        InitialTileFlags = Array.AsReadOnly(initialTileFlags.ToArray());
        InitialCorpses = Array.AsReadOnly(initialCorpses.ToArray());
        InitialLight = Array.AsReadOnly(initialLight.ToArray());
        InitialPreLight = Array.AsReadOnly(initialPreLight.ToArray());
        LightAllocator = new AuthoritativeCathedralEncounterLightAllocator(
            lightAllocator.ActiveLightSlots, lightAllocator.ActiveLightCount,
            lightAllocator.LightRecords, lightAllocator.UpdateLighting, lightAllocator.DisableLighting);
        TriggerPositions = Array.AsReadOnly(triggerPositions.ToArray());
        OrderedUniqueDefinitions = Array.AsReadOnly(orderedUniqueDefinitions.ToArray());
        MonsterDensityDivisor = monsterDensityDivisor;
        ShrineAvailable = Array.AsReadOnly(shrineAvailable.ToArray());
        ItemCatalog = itemCatalog;
        ItemPolicy = itemPolicy;
        ItemPlayer = itemPlayer;
        InitialItemSlots = Array.AsReadOnly(initialItemSlots.Select(value => value with { Animation = value.Animation with { } }).ToArray());
        InitialActiveItemIds = Array.AsReadOnly(initialActiveItemIds.ToArray());
        InitialActiveItemCount = initialActiveItemCount;
        InitialUniqueItemFlags = initialUniqueItemFlags.ToFrozenSet();
        InitialItemTiles = Array.AsReadOnly(initialItemTiles.ToArray());
        PotionDefinitions = Array.AsReadOnly(potionDefinitions.ToArray());
        MaximumPlacementAttempts = maximumPlacementAttempts;
        MaximumItemPlacementAttempts = maximumItemPlacementAttempts;
        InitialVisibleTransparencyRegions = Array.AsReadOnly(
            (initialVisibleTransparencyRegions ?? new bool[256]).ToArray());
    }

    public uint MonsterClearRandomState { get; }
    public AuthoritativeCathedralInitialPopulationParameters InitialPopulation { get; }
    public IReadOnlyList<int> InitialMonsterTileRefs { get; }
    public IReadOnlyList<int> InitialPlayerRefs { get; }
    public IReadOnlyList<byte> InitialTileFlags { get; }
    public IReadOnlyList<byte> InitialCorpses { get; }
    public IReadOnlyList<byte> InitialLight { get; }
    public IReadOnlyList<byte> InitialPreLight { get; }
    public AuthoritativeCathedralEncounterLightAllocator LightAllocator { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions { get; }
    public IReadOnlyList<AuthoritativeCathedralUniqueDefinition> OrderedUniqueDefinitions { get; }
    public int MonsterDensityDivisor { get; }
    public IReadOnlyList<bool> ShrineAvailable { get; }
    public AuthoritativeCathedralThemeItemCatalog ItemCatalog { get; }
    public AuthoritativeCathedralThemeItemPolicy ItemPolicy { get; }
    public AuthoritativeCathedralThemeItemPlayerContext? ItemPlayer { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemState> InitialItemSlots { get; }
    public IReadOnlyList<int> InitialActiveItemIds { get; }
    public int InitialActiveItemCount { get; }
    public IReadOnlySet<int> InitialUniqueItemFlags { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemTile> InitialItemTiles { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> PotionDefinitions { get; }
    public int MaximumPlacementAttempts { get; }
    public int MaximumItemPlacementAttempts { get; }
    public IReadOnlyList<bool> InitialVisibleTransparencyRegions { get; }

    private static void RequireCount<T>(IReadOnlyList<T> values, int expected, string name)
    {
        if (values.Count != expected)
            throw new InvalidDataException($"Native {name} requires exactly {expected} entries.");
    }

    private static void ValidatePotionRows(
        AuthoritativeCathedralThemeItemCatalog itemCatalog,
        IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> potionDefinitions)
    {
        if (potionDefinitions.Count != 2 || potionDefinitions.Any(value => value is null)
            || potionDefinitions.Select(value => value.NativeItemId).Distinct().Count() != 2
            || potionDefinitions.Any(value => value.NativeItemId is not (
                AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId
                or AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId)))
            throw new InvalidDataException("The full ordinary pipeline requires exactly the two native potion definitions.");
        // InitItems addresses AllItemsList by table index. Do not use the
        // stable base enum ID here: source aliases can share an ID while the
        // ordinary potion rows are still the distinct indexes 24 and 25.
        var rows = itemCatalog.Items
            .Where(row => row.NativeItemIndex is AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId
                or AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId)
            .ToArray();
        if (rows.Length != 2 || rows.Select(row => row.NativeItemIndex).Distinct().Count() != 2)
            throw new InvalidDataException("The full ordinary pipeline requires both native potion rows in its shared item catalog.");

        foreach (var potion in potionDefinitions) {
            var row = rows.Single(value => value.NativeItemIndex == potion.NativeItemId);
            if (row.ItemType != potion.ItemType
                || row.CursorGraphic != potion.CursorGraphic
                || !string.Equals(row.Name, potion.Name, StringComparison.Ordinal)
                || !string.Equals(row.Name, potion.IdentifiedName, StringComparison.Ordinal)
                || row.EquipLocation != potion.EquipLocation
                || row.ItemClass != potion.ItemClass
                || row.MinimumDamage != potion.MinDamage
                || row.MaximumDamage != potion.MaxDamage
                || row.MinimumArmorClass != potion.MinArmorClass
                || row.MaximumArmorClass != potion.MaxArmorClass
                || row.Flags != potion.Flags
                || row.MiscId != potion.MiscId
                || row.SpellId != potion.SpellId
                || row.Value != potion.Value
                || row.Durability != potion.Durability
                || row.MinimumStrength != potion.MinimumStrength
                || row.MinimumMagic != potion.MinimumMagic
                || row.MinimumDexterity != potion.MinimumDexterity
                || row.AnimationFrameCount != potion.AnimationFrameCount
                || row.AnimationTicksPerFrame != potion.AnimationTicksPerFrame
                || potion.Buff != 0)
                throw new InvalidDataException("Potion definitions must match the shared raw item catalog.");
        }
    }
}

/** Full detached result through InitObjects, InitMonsters, InitItems and CreateThemeRooms. */
public sealed class AuthoritativeCathedralFullPopulationResult
{
    internal AuthoritativeCathedralFullPopulationResult(
        AuthoritativeCathedralInitialPopulationResult initialPopulation,
        AuthoritativeCathedralFreshMonsterPool clearedMonsterPool,
        AuthoritativeCathedralEncounterResult encounters,
        AuthoritativeCathedralInitialItemResult initialItems,
        AuthoritativeCathedralThemeRoomState beforeThemeRooms,
        AuthoritativeCathedralThemeRoomResult themeRooms)
    {
        InitialPopulation = initialPopulation;
        ClearedMonsterPool = clearedMonsterPool;
        Encounters = encounters;
        InitialItems = initialItems;
        BeforeThemeRooms = beforeThemeRooms ?? throw new ArgumentNullException(nameof(beforeThemeRooms));
        ThemeRooms = themeRooms;
    }

    public AuthoritativeCathedralInitialPopulationResult InitialPopulation { get; }
    public AuthoritativeCathedralFreshMonsterPool ClearedMonsterPool { get; }
    public AuthoritativeCathedralEncounterResult Encounters { get; }
    public AuthoritativeCathedralInitialItemResult InitialItems { get; }
    /** Complete detached state immediately after InitItems and before CreateThemeRooms. */
    public AuthoritativeCathedralThemeRoomState BeforeThemeRooms { get; }
    public AuthoritativeCathedralThemeRoomResult ThemeRooms { get; }
    public AuthoritativeCathedralThemeRoomState State => ThemeRooms.State;
    public uint FinalRandomState => ThemeRooms.FinalRandomState;
}

/**
 * Composes the controlled ordinary Cathedral phases. It is not a campaign,
 * multiplayer, quest, set-level, renderer, or archive-loading entry point.
 */
public static class AuthoritativeCathedralFullPopulationGenerator
{
    private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
    private const int MonsterCapacity = AuthoritativeCathedralPopulationParameters.MonsterCapacity;
    private const byte TileVisible = 1 << 1;
    private const byte TilePopulated = 1 << 3;
    private const byte TileLit = 1 << 6;

    public static AuthoritativeCathedralFullPopulationResult Generate(
        AuthoritativeCathedralFullPopulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var initial = AuthoritativeCathedralInitialPopulationGenerator.Generate(request.InitialPopulation);
        var bestiary = initial.Bestiary;
        var derivedScatter = bestiary.Entries
            .Where(entry => (entry.PlaceFlags & AuthoritativeCathedralBestiaryGenerator.PlaceScatter) != 0)
            .Select(entry => entry.Index)
            .ToArray();

        var heldGeometry = new AuthoritativeCathedralPopulationGeometry(
            request.InitialPopulation.PreThemeGeometry.Pieces,
            request.InitialPopulation.PreThemeGeometry.Special,
            request.InitialPopulation.PreThemeGeometry.Transparency,
            request.InitialPopulation.PreThemeGeometry.PieceProperties,
            initial.Themes.HeldPopulatedCells,
            request.InitialPopulation.PreThemeGeometry.PlayerOccupied,
            request.InitialPopulation.PreThemeGeometry.MonsterOccupied);
        var objectTemplate = request.InitialPopulation.ObjectTemplate;
        var encounterPopulation = new AuthoritativeCathedralPopulationParameters(
            objectTemplate.Depth, objectTemplate.PopulationSeed, objectTemplate.StorySeed,
            heldGeometry, objectTemplate.Objects, bestiary.PopulationDefinitions,
            objectTemplate.Player, objectTemplate.TrapPercent, objectTemplate.StoryMessages,
            objectTemplate.MaximumPlacementAttempts);

        var clearedMonsterPool = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(
            request.MonsterClearRandomState);
        var initialMonsterSlots = clearedMonsterPool.Monsters
            .Select(value => value with { })
            .ToArray();
        foreach (var monster in initial.Population.Monsters) {
            if (monster.Slot < 0 || monster.Slot >= MonsterCapacity)
                throw new InvalidDataException("Initial population selected an unsafe monster slot.");
            initialMonsterSlots[monster.Slot] = monster with { };
        }

        var initialTileFlags = request.InitialTileFlags.ToArray();
        for (var index = 0; index < CellCount; index++)
            if (initial.Themes.HeldPopulatedCells[index])
                initialTileFlags[index] |= TilePopulated;
        var initialVisibility = initialTileFlags.Select(value => (value & TileVisible) != 0).ToArray();
        var initialLit = initialTileFlags.Select(value => (value & TileLit) != 0).ToArray();

        var light = request.InitialLight.ToArray();
        var preLight = request.InitialPreLight.ToArray();
        AuthoritativeCathedralInitialObjectLighting.Apply(light, preLight, initial.Population);

        var encounters = AuthoritativeCathedralEncounterGenerator.Generate(
            new AuthoritativeCathedralEncounterParameters(
                encounterPopulation,
                initial.Population,
                request.InitialMonsterTileRefs,
                initialVisibility,
                request.TriggerPositions,
                derivedScatter,
                request.OrderedUniqueDefinitions,
                request.MonsterDensityDivisor,
                request.LightAllocator,
                request.MaximumPlacementAttempts,
                request.InitialVisibleTransparencyRegions,
                initialMonsterSlots,
                Enumerable.Range(0, MonsterCapacity).ToArray(),
                initial.Population.Monsters.Count,
                initialLit,
                initialTileFlags,
                bestiary));

        var itemTiles = request.InitialItemTiles
            .Select((tile, index) => tile with {
                PlayerRef = request.InitialPlayerRefs[index],
                MonsterRef = encounters.MonsterTileRefs[index],
                ObjectRef = initial.Population.TileObjectRefs[index],
                // InitItems sees the tile channels after InitObjects and
                // InitMonsters. Set-piece occupancy comes from TileFlags;
                // solidity comes from the resulting piece/SOL row. The
                // caller's convenience booleans are deliberately not used
                // as a second, stale geometry source.
                SetPiece = (encounters.TileFlags[index] & TilePopulated) != 0,
                Solid = (request.InitialPopulation.PreThemeGeometry.PieceProperties[
                    initial.Population.Pieces[index]] & 1) != 0,
            })
            .ToArray();
        var initialItems = AuthoritativeCathedralInitialItemGenerator.Generate(
            new AuthoritativeCathedralInitialItemParameters(
                request.InitialPopulation.Depth,
                encounters.FinalRandomState,
                itemTiles,
                request.InitialItemSlots,
                request.InitialActiveItemIds,
                request.InitialActiveItemCount,
                request.PotionDefinitions,
                request.MaximumItemPlacementAttempts));

        var roomCatalog = new AuthoritativeCathedralThemeRoomCatalog(
            objectTemplate.Objects,
            bestiary.Entries.Select(entry => new AuthoritativeCathedralThemeRoomMonsterDefinition(
                entry.PopulationDefinition,
                checked((uint)entry.PlaceFlags),
                AuthoritativeCathedralThemeRoomGenerator.IsNativeGoat(entry.NativeType))).ToArray(),
            request.ShrineAvailable);
        var roomState = new AuthoritativeCathedralThemeRoomState(
            initial.Population.Objects,
            initial.Population.ActiveObjectIds,
            initial.Population.AvailableObjectIds,
            initial.Population.ActiveObjectCount,
            encounters.MonsterSlots,
            encounters.ActiveMonsterIds,
            encounters.ActiveMonsterCount,
            encounters.TotalMonsters,
            encounters.MonsterTileRefs,
            request.InitialPlayerRefs,
            initialItems.Items,
            initialItems.ActiveItemIds,
            initialItems.ActiveItemCount,
            initialItems.ItemTileRefs,
            request.InitialUniqueItemFlags,
            initial.Population.Pieces,
            initial.Population.Transparency.Select(value => unchecked((sbyte)value)).ToArray(),
            initial.Population.Special,
            request.InitialPopulation.PreThemeGeometry.PieceProperties,
            encounters.TileFlags,
            initial.Population.TileObjectRefs,
            light,
            preLight,
            encounters.ActiveLightSlots,
            encounters.ActiveLightCount,
            encounters.LightRecords,
            encounters.UpdateLighting,
            encounters.DisableLighting,
            new AuthoritativeCathedralThemeRoomFlags(
                initial.Themes.Flags.ArmorAvailable,
                initial.Themes.Flags.WeaponAvailable,
                initial.Themes.Flags.TreasureAvailable,
                initial.Themes.Flags.BloodFountainAvailable,
                initial.Themes.Flags.PurifyingFountainAvailable,
                initial.Themes.Flags.MurkyFountainAvailable,
                initial.Themes.Flags.TearFountainAvailable,
                initial.Themes.Flags.CauldronAvailable),
            request.InitialCorpses,
            new bool[MonsterCapacity]);
        var themeRooms = AuthoritativeCathedralThemeRoomGenerator.Generate(
            new AuthoritativeCathedralThemeRoomRequest(
                roomState,
                roomCatalog,
                request.ItemCatalog,
                request.ItemPolicy,
                initial.Themes.Themes,
                initialItems.FinalRandomState,
                objectTemplate.Player,
                request.ItemPlayer,
                false,
                initial.Themes.PlacementVariant,
                request.MaximumPlacementAttempts,
                initial.Themes.PlacementOrigin));

        return new(initial, clearedMonsterPool, encounters, initialItems, roomState, themeRooms);
    }
}
