using System.Collections.Frozen;

namespace Devilution.Diablo;

/** Explicit single-player hero values needed while constructing a fresh level. */
public sealed record AuthoritativeCathedralFreshLevelHeroContext(
    int MaximumLifeBase,
    int MaximumManaBase,
    int LightRadius,
    int IdleFrames,
    AuthoritativeCathedralPopulationPoint PreEntryFuture,
    bool LevelChanging,
    bool LevelLoading = true);

/** Native InitPlayer animation state prepared after InitThemes and before reseeding. */
public sealed record AuthoritativeCathedralPlayerPreparation(
    int IdleFrames,
    int AnimationCurrentFrame,
    int AnimationTickCounter,
    int TicksPerFrame,
    uint RandomStateBefore,
    uint FinalRandomState,
    bool IsPetrified);

/**
 * Production input for one fresh ordinary Cathedral level. Geometry, pools,
 * tile channels and selected bestiary/themes are deliberately not accepted:
 * Generate derives them from the supplied compiled metadata and seeds.
 */
public sealed class AuthoritativeCathedralFreshLevelRequest
{
    internal AuthoritativeCathedralFreshLevelRequest(
        AuthoritativeCathedralPopulationMetadata metadata,
        int depth,
        uint dungeonSeed,
        uint storySeed,
        AuthoritativeCathedralEntry entry,
        AuthoritativeCathedralFreshLevelHeroContext hero,
        string stableLevelSymbol,
        int maximumPlacementAttempts,
        int maximumItemPlacementAttempts,
        int maximumThemeSelectionAttempts,
        IReadOnlySet<int>? initialUniqueItemFlags = null)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Hero = hero ?? throw new ArgumentNullException(nameof(hero));
        if (depth is < AuthoritativeCathedralDepthGenerator.MinimumSupportedDepth
            or > AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth)
            throw new InvalidDataException("Only ordinary Cathedral depths 1 through 4 are supported.");
        if (entry is not AuthoritativeCathedralEntry.Main and not AuthoritativeCathedralEntry.Previous)
            throw new InvalidDataException("Only the native Cathedral main and previous entries are supported.");
        if (string.IsNullOrWhiteSpace(stableLevelSymbol) || stableLevelSymbol.Contains('\0'))
            throw new InvalidDataException("A non-empty stable Cathedral level symbol is required.");
        if (maximumPlacementAttempts <= 0 || maximumItemPlacementAttempts <= 0
            || maximumThemeSelectionAttempts <= 0)
            throw new InvalidDataException("Fresh Cathedral generation bounds must be positive.");
        ValidateHero(hero);
        if (initialUniqueItemFlags?.Any(value => value is < 0 or >= 128) == true)
            throw new InvalidDataException("Fresh Cathedral unique history must contain native item flag indices.");
        InitialUniqueItemFlags = (initialUniqueItemFlags ?? new HashSet<int>()).ToFrozenSet();

        Depth = depth;
        DungeonSeed = dungeonSeed;
        StorySeed = storySeed;
        Entry = entry;
        StableLevelSymbol = stableLevelSymbol;
        MaximumPlacementAttempts = maximumPlacementAttempts;
        MaximumItemPlacementAttempts = maximumItemPlacementAttempts;
        MaximumThemeSelectionAttempts = maximumThemeSelectionAttempts;
    }

    public AuthoritativeCathedralPopulationMetadata Metadata { get; }
    public int Depth { get; }
    public uint DungeonSeed { get; }
    public uint StorySeed { get; }
    public AuthoritativeCathedralEntry Entry { get; }
    public AuthoritativeCathedralFreshLevelHeroContext Hero { get; }
    public string StableLevelSymbol { get; }
    public int MaximumPlacementAttempts { get; }
    public int MaximumItemPlacementAttempts { get; }
    public int MaximumThemeSelectionAttempts { get; }
    /** Global run history sampled before this floor's first generation, never a borrowed mutable set. */
    public IReadOnlySet<int> InitialUniqueItemFlags { get; }

    private static void ValidateHero(AuthoritativeCathedralFreshLevelHeroContext hero)
    {
        if (hero.MaximumLifeBase <= 0
            || hero.MaximumManaBase < 0
            || hero.LightRadius is < 2 or > 15
            || hero.IdleFrames is < 1 or > sbyte.MaxValue
            || hero.PreEntryFuture is { X: < 0 or >= AuthoritativeCathedralPopulationGeometry.Width }
            || hero.PreEntryFuture is { Y: < 0 or >= AuthoritativeCathedralPopulationGeometry.Width }
            || !hero.LevelLoading)
            throw new InvalidDataException(
                "Fresh ordinary Cathedral generation requires native hero animation and stat bounds.");
    }
}

/**
 * Complete fresh-level result through CreateThemeRooms. The embedded request
 * contains only generated, detached state and may be inspected by native-oracle
 * tests without becoming an input to the generation algorithm.
 */
public sealed class AuthoritativeCathedralFreshLevelResult
{
    internal AuthoritativeCathedralFreshLevelResult(
        AuthoritativeCathedralFreshLevelRequest request,
        AuthoritativeCathedralDepthGeometry geometry,
        AuthoritativeCathedralInitialPopulationParameters initialPopulation,
        AuthoritativeCathedralFullPopulationRequest fullPopulationRequest,
        AuthoritativeCathedralFullPopulationResult population,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
        IReadOnlyList<bool> preThemePopulated,
        IReadOnlyList<byte> initialTileFlags,
        IReadOnlyList<byte> initialLight,
        IReadOnlyList<byte> initialPreLight,
        AuthoritativeCathedralEncounterLightAllocator lightAllocator,
        uint monsterClearFinalRandomState,
        AuthoritativeCathedralPlayerPreparation playerPreparation,
        IReadOnlyList<AuthoritativeCathedralEncounterLightState> visionRecords,
        IReadOnlyList<bool> visionActive,
        bool updateVision)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        InitialPopulation = initialPopulation ?? throw new ArgumentNullException(nameof(initialPopulation));
        FullPopulationRequest = fullPopulationRequest ?? throw new ArgumentNullException(nameof(fullPopulationRequest));
        Population = population ?? throw new ArgumentNullException(nameof(population));
        TriggerPositions = Array.AsReadOnly(triggerPositions.ToArray());
        PreThemePopulated = Array.AsReadOnly(preThemePopulated.ToArray());
        InitialTileFlags = Array.AsReadOnly(initialTileFlags.ToArray());
        InitialLight = Array.AsReadOnly(initialLight.ToArray());
        InitialPreLight = Array.AsReadOnly(initialPreLight.ToArray());
        LightAllocator = lightAllocator ?? throw new ArgumentNullException(nameof(lightAllocator));
        MonsterClearFinalRandomState = monsterClearFinalRandomState;
        PlayerPreparation = playerPreparation ?? throw new ArgumentNullException(nameof(playerPreparation));
        VisionRecords = Array.AsReadOnly(visionRecords.ToArray());
        VisionActive = Array.AsReadOnly(visionActive.ToArray());
        UpdateVision = updateVision;
    }

    public AuthoritativeCathedralFreshLevelRequest Request { get; }
    public AuthoritativeCathedralDepthGeometry Geometry { get; }
    public AuthoritativeCathedralInitialPopulationParameters InitialPopulation { get; }
    public AuthoritativeCathedralFullPopulationRequest FullPopulationRequest { get; }
    public AuthoritativeCathedralFullPopulationResult Population { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions { get; }
    public IReadOnlyList<bool> PreThemePopulated { get; }
    public IReadOnlyList<byte> InitialTileFlags { get; }
    public IReadOnlyList<byte> InitialLight { get; }
    public IReadOnlyList<byte> InitialPreLight { get; }
    public AuthoritativeCathedralEncounterLightAllocator LightAllocator { get; }
    public uint MonsterClearFinalRandomState { get; }
    public AuthoritativeCathedralPlayerPreparation PlayerPreparation { get; }
    public IReadOnlyList<AuthoritativeCathedralEncounterLightState> VisionRecords { get; }
    public IReadOnlyList<bool> VisionActive { get; }
    public bool UpdateVision { get; }
    public int NextTransparencyValue => Geometry.GenerationResult.NextTransparencyValue;
}

/** Fresh normal-order Cathedral geometry and population boundary. */
public static class AuthoritativeCathedralFreshLevelGenerator
{
    private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
    private const int MaximumTransparencyValue = 127;
    private const byte TilePopulated = 1 << 3;
    private const byte DefaultDungeonLight = 15;
    private const int DefaultMaximumAttempts = 1_000_000;

    public static AuthoritativeCathedralFreshLevelRequest CreateRequest(
        AuthoritativeCathedralPopulationMetadata metadata,
        int depth,
        uint dungeonSeed,
        uint storySeed,
        AuthoritativeCathedralEntry entry,
        AuthoritativeCathedralFreshLevelHeroContext hero,
        string? stableLevelSymbol = null,
        int maximumPlacementAttempts = DefaultMaximumAttempts,
        int maximumItemPlacementAttempts = DefaultMaximumAttempts,
        int maximumThemeSelectionAttempts = DefaultMaximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(hero);
        _ = metadata.RuleForDepth(depth);
        var symbol = stableLevelSymbol ?? $"cathedral{depth}";
        return new(metadata, depth, dungeonSeed, storySeed, entry, hero, symbol,
            maximumPlacementAttempts, maximumItemPlacementAttempts,
            maximumThemeSelectionAttempts);
    }

    /** First entry to another floor carries the run's unique history; legacy fresh APIs remain empty-history. */
    public static AuthoritativeCathedralFreshLevelRequest CreateEntryRequest(
        AuthoritativeCathedralPopulationMetadata metadata,
        int depth,
        uint dungeonSeed,
        uint storySeed,
        AuthoritativeCathedralEntry entry,
        AuthoritativeCathedralFreshLevelHeroContext hero,
        IReadOnlySet<int> initialUniqueItemFlags,
        string? stableLevelSymbol = null,
        int maximumPlacementAttempts = DefaultMaximumAttempts,
        int maximumItemPlacementAttempts = DefaultMaximumAttempts,
        int maximumThemeSelectionAttempts = DefaultMaximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(initialUniqueItemFlags);
        _ = metadata.RuleForDepth(depth);
        return new(metadata, depth, dungeonSeed, storySeed, entry, hero,
            stableLevelSymbol ?? $"cathedral{depth}", maximumPlacementAttempts,
            maximumItemPlacementAttempts, maximumThemeSelectionAttempts, initialUniqueItemFlags);
    }

    public static AuthoritativeCathedralFreshLevelResult Generate(
        AuthoritativeCathedralFreshLevelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // InitLevelMonsters runs before CreateLevel. The stream is independent
        // because the native loader reseeds before GetLevelMTypes and again
        // before InitGolems/InitObjects.
        var clearedMonsterPool = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(request.DungeonSeed);
        var coarse = AuthoritativeCathedralDepthGenerator.Generate(
            request.Depth, request.DungeonSeed, request.Entry,
            AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs);
        if (coarse.NextTransparencyValue is < 1 or > MaximumTransparencyValue)
            throw new InvalidDataException("Native transparency-region state is outside the signed native range.");

        var materialized = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
            (uint)request.Depth, coarse, request.Metadata.Geometry.Pieces,
            request.Metadata.Geometry.TileProperties, request.StableLevelSymbol);
        // Materialize owns a detached depth result.  Keep that result so the
        // native transparency counter survives the coarse-to-fine copy path.
        var geometry = materialized;
        var derived = DerivePreThemeState(
            request.Metadata, geometry.Level, geometry.GenerationResult.NextTransparencyValue);
        var entryPosition = new AuthoritativeCathedralPopulationPoint(coarse.EntryPosition.X, coarse.EntryPosition.Y);
        // InitPlayer establishes Tile/Old from the generated view position, but
        // the loader's pre-existing Future is synchronized only after the
        // population phases. Require the independent pre-entry scalar rather
        // than inventing a synchronized future position during population.
        var playerFuture = request.Hero.PreEntryFuture;
        var player = new AuthoritativeCathedralPopulationPlayer(
            0, request.Depth, true, request.Hero.LevelChanging, entryPosition, playerFuture);
        var initialPopulation = request.Metadata.CreateInitialPopulationParameters(
            request.Depth, request.DungeonSeed, request.StorySeed, derived.Geometry,
            player, derived.NextTransparencyValue, derived.TriggerPositions,
            request.MaximumPlacementAttempts, request.MaximumThemeSelectionAttempts);
        var itemPolicy = new AuthoritativeCathedralThemeItemPolicy(
            request.Depth, DiabloGameMode.SinglePlayer, DiabloDifficulty.Normal,
            Hellfire: false, Spawn: false, QuestEnabled: false, SetLevel: false,
            LegacyLevelItemGenerationAllowed: true, CurrentDungeonLevel: request.Depth);
        var itemTiles = Enumerable.Range(0, CellCount)
            .Select(_ => new AuthoritativeCathedralInitialItemTile())
            .ToArray();
        var initialItems = Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.PoolCapacity)
            .Select(_ => new AuthoritativeCathedralInitialItemState())
            .ToArray();
        var activeItemIds = Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.ActiveCapacity).ToArray();
        var initialLight = Enumerable.Repeat(DefaultDungeonLight, CellCount).ToArray();
        var initialPreLight = new byte[CellCount];
        var lightAllocator = CreatePlayerLightAllocator(entryPosition, request.Hero.LightRadius);
        var fullRequest = new AuthoritativeCathedralFullPopulationRequest(
            initialPopulation,
            request.DungeonSeed,
            new int[CellCount],
            new int[CellCount],
            derived.InitialTileFlags,
            new byte[CellCount],
            initialLight,
            initialPreLight,
            lightAllocator,
            derived.TriggerPositions,
            request.Metadata.UniqueMonsters.Definitions,
            request.Metadata.RuleForDepth(request.Depth).MonsterDensityDivisor,
            request.Metadata.ShrineAvailable,
            request.Metadata.Items.Catalog,
            itemPolicy,
            new AuthoritativeCathedralThemeItemPlayerContext(
                PlayerPresent: true,
                LevelLoading: request.Hero.LevelLoading,
                MaximumManaBase: request.Hero.MaximumManaBase,
                MaximumLifeBase: request.Hero.MaximumLifeBase),
            initialItems,
            activeItemIds,
            0,
            request.InitialUniqueItemFlags,
            itemTiles,
            request.Metadata.PotionDefinitions,
            request.MaximumPlacementAttempts,
            request.MaximumItemPlacementAttempts,
            new bool[256],
            playerInitialized: true);
        var population = AuthoritativeCathedralFullPopulationGenerator.Generate(fullRequest);
        var playerPreparation = PreparePlayer(
            population.InitialPopulation.Themes.FinalRandomState, request.Hero.IdleFrames);
        var pendingVision = CreatePendingVision(entryPosition, request.Hero.LightRadius);
        return new(request, geometry, initialPopulation, fullRequest, population,
            derived.TriggerPositions, derived.PreThemePopulated, derived.InitialTileFlags,
            initialLight, initialPreLight, lightAllocator, clearedMonsterPool.FinalRandomState,
            playerPreparation, pendingVision.Records, pendingVision.Active, pendingVision.Update);
    }

    public static AuthoritativeCathedralFreshLevelResult Generate(
        AuthoritativeCathedralPopulationMetadata metadata,
        int depth,
        uint dungeonSeed,
        uint storySeed,
        AuthoritativeCathedralEntry entry,
        AuthoritativeCathedralFreshLevelHeroContext hero,
        string? stableLevelSymbol = null,
        int maximumPlacementAttempts = DefaultMaximumAttempts,
        int maximumItemPlacementAttempts = DefaultMaximumAttempts,
        int maximumThemeSelectionAttempts = DefaultMaximumAttempts)
        => Generate(CreateRequest(metadata, depth, dungeonSeed, storySeed, entry, hero,
            stableLevelSymbol, maximumPlacementAttempts, maximumItemPlacementAttempts,
            maximumThemeSelectionAttempts));

    private static AuthoritativeCathedralFreshPreThemeState DerivePreThemeState(
        AuthoritativeCathedralPopulationMetadata metadata,
        AuthoritativeLevel level,
        int nextTransparencyValue)
    {
        if (level.Width != AuthoritativeCathedralPopulationGeometry.Width
            || level.Height != AuthoritativeCathedralPopulationGeometry.Width
            || level.NativePieceLayer.Count != CellCount
            || level.NativeTransparency.Count != CellCount)
            throw new InvalidDataException("Fresh Cathedral generation requires complete 112x112 fine geometry.");
        var pieces = level.NativePieceLayer.ToArray();
        var special = pieces.Select(InitialSpecial).ToArray();
        var transparency = level.NativeTransparency.Select(value => checked((byte)value)).ToArray();
        var triggerPositions = new List<AuthoritativeCathedralPopulationPoint>();
        var populated = new bool[CellCount];
        for (var y = 0; y < AuthoritativeCathedralPopulationGeometry.Width; y++) {
            for (var x = 0; x < AuthoritativeCathedralPopulationGeometry.Width; x++) {
                var piece = pieces[y * AuthoritativeCathedralPopulationGeometry.Width + x];
                if (piece == 128 || piece == 114)
                    triggerPositions.Add(new(x, y));
            }
        }
        foreach (var trigger in triggerPositions) {
            for (var yy = -2; yy <= 2; yy++) {
                for (var xx = -2; xx <= 2; xx++) {
                    var x = trigger.X + xx;
                    var y = trigger.Y + yy;
                    if ((uint)x >= AuthoritativeCathedralPopulationGeometry.Width
                        || (uint)y >= AuthoritativeCathedralPopulationGeometry.Width)
                        throw new InvalidDataException("Native Freeupstairs would write outside the Cathedral tile grid.");
                    populated[y * AuthoritativeCathedralPopulationGeometry.Width + x] = true;
                }
            }
        }
        if (triggerPositions.Count > 7)
            throw new InvalidDataException("Native Cathedral trigger capacity is seven; no trigger may be truncated.");
        if (triggerPositions.Count == 0)
            throw new InvalidDataException("Native Cathedral generation requires its stair trigger tiles.");
        var initialFlags = populated.Select(value => value ? TilePopulated : (byte)0).ToArray();
        var geometry = new AuthoritativeCathedralPopulationGeometry(
            pieces, special, transparency, metadata.EffectivePieceProperties,
            populated, new bool[CellCount], new bool[CellCount]);
        return new(geometry, triggerPositions, populated, initialFlags, nextTransparencyValue);
    }

    private static AuthoritativeCathedralEncounterLightAllocator CreatePlayerLightAllocator(
        AuthoritativeCathedralPopulationPoint entryPosition,
        int radius)
    {
        var records = Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights)
            .Select(lightId => new AuthoritativeCathedralEncounterLightState(
                lightId, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false))
            .ToArray();
        records[0] = new AuthoritativeCathedralEncounterLightState(
            0, entryPosition, entryPosition, 0, 0, radius, radius, false, true);
        return new AuthoritativeCathedralEncounterLightAllocator(
            Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights).ToArray(),
            1, records, updateLighting: true, disableLighting: false);
    }

    private static AuthoritativeCathedralPlayerPreparation PreparePlayer(
        uint randomState,
        int idleFrames)
    {
        var random = new AuthoritativeLegacyRandom(randomState);
        var currentFrame = random.GenerateRnd(idleFrames - 1);
        var tickCounter = random.GenerateRnd(3);
        return new(idleFrames, currentFrame, tickCounter, 4, randomState, random.State, false);
    }

    private static AuthoritativeCathedralPendingVisionState CreatePendingVision(
        AuthoritativeCathedralPopulationPoint entryPosition,
        int radius)
    {
        var records = Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights)
            .Take(4)
            .Select(lightId => new AuthoritativeCathedralEncounterLightState(
                lightId, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false))
            .ToArray();
        records[0] = new AuthoritativeCathedralEncounterLightState(
            0, entryPosition, new(0, 0), 0, 0, radius, 0, false, false);
        return new(records, new[] { true, false, false, false }, true);
    }

    private static byte InitialSpecial(ushort piece)
        => piece switch {
            11 or 70 or 320 or 210 or 340 or 417 => 1,
            10 or 248 or 324 or 343 or 330 or 420 => 2,
            252 => 3,
            254 => 4,
            258 => 5,
            266 => 6,
            _ => 0,
        };

    private sealed record AuthoritativeCathedralFreshPreThemeState(
        AuthoritativeCathedralPopulationGeometry Geometry,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions,
        IReadOnlyList<bool> PreThemePopulated,
        IReadOnlyList<byte> InitialTileFlags,
        int NextTransparencyValue);

    private sealed record AuthoritativeCathedralPendingVisionState(
        IReadOnlyList<AuthoritativeCathedralEncounterLightState> Records,
        IReadOnlyList<bool> Active,
        bool Update);
}
