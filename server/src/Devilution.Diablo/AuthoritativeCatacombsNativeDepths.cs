using Devilution.Data;

namespace Devilution.Diablo;

/**
 * L5-L8 below a native Cathedral run. Native CreateLevel/InitL2Triggers: each depth's
 * geometry depends only on its seed, so it is generated once at activation for the
 * stair routes; owners are built on first entry from the sampled hero.
 */
internal sealed class AuthoritativeCatacombsNativeDepths
{
    internal const int FirstDepth = 5;
    internal const int LastDepth = 8;
    /** The qualified C2 oracle seed, as the Cathedral depths use fixed content seeds. */
    internal const uint DungeonSeed = 2588;
    private const ushort UpStairPiece = 266;
    private const ushort DownStairPiece = 270;
    private const ushort TownWarpUpPiece = 558;
    private const uint DownWarpIdBase = 1001;
    private const uint UpWarpIdBase = 1099;
    private const uint TownWarpDownId = 1201;
    private const uint TownWarpUpId = 1202;
    /** Native InitTownTriggers and CreateTown ENTRY_TWARPUP for tWarpFrom 5. */
    private static readonly AuthoritativeCathedralPoint TownWarpTrigger = new(49, 21);
    private static readonly AuthoritativeCathedralPoint TownWarpArrival = new(49, 22);

    private readonly AuthoritativeCatacombsPopulationMetadata metadata;
    private readonly AuthoritativeCatacombsAutomapMetadata automap;
    private readonly uint storySeed;
    private readonly IReadOnlyDictionary<uint, AuthoritativeCathedralLevel1RoutePoints> routes;
    /** L5's piece-558 warp-up trigger and its native ENTRY_TWARPDN view position. */
    private readonly (AuthoritativeCathedralPoint Trigger, AuthoritativeCathedralPoint Entry) townWarp;

    private AuthoritativeCatacombsNativeDepths(AuthoritativeCatacombsPopulationMetadata metadata,
        AuthoritativeCatacombsAutomapMetadata automap, uint storySeed, IReadOnlyList<AuthoritativeLevel> levels,
        IReadOnlyDictionary<uint, AuthoritativeCathedralLevel1RoutePoints> routes,
        (AuthoritativeCathedralPoint Trigger, AuthoritativeCathedralPoint Entry) townWarp)
    {
        this.metadata = metadata;
        this.automap = automap;
        this.storySeed = storySeed;
        this.routes = routes;
        this.townWarp = townWarp;
        Levels = levels;
        Fingerprint = AuthoritativeCathedralNativeProgression.ComputeFingerprint(new {
            RunId = "native-catacombs-l5-l8-v1", Seed = DungeonSeed, StorySeed = storySeed,
            Metadata = metadata.Fingerprint, Automap = automap.Fingerprint,
            Routes = routes.OrderBy(entry => entry.Key).Select(entry => new { entry.Key, entry.Value }).ToArray(),
        });
    }

    internal IReadOnlyList<AuthoritativeLevel> Levels { get; }
    internal string Fingerprint { get; }
    internal static bool Owns(uint levelId) => levelId is >= FirstDepth and <= LastDepth;

    internal static AuthoritativeCatacombsNativeDepths Create(AuthoritativeCatacombsPopulationMetadata metadata,
        AuthoritativeCatacombsAutomapMetadata automap, uint storySeed)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(automap);
        var levels = new List<AuthoritativeLevel>();
        var routes = new Dictionary<uint, AuthoritativeCathedralLevel1RoutePoints>();
        (AuthoritativeCathedralPoint Trigger, AuthoritativeCathedralPoint Entry)? townWarp = null;
        for (var depth = FirstDepth; depth <= LastDepth; depth++) {
            var generation = AuthoritativeCatacombsGenerator.Generate(depth, DungeonSeed,
                AuthoritativeCatacombsEntry.Main, null, AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs);
            var level = AuthoritativeCatacombsGeometryMaterializer.Materialize((uint)depth, generation,
                metadata.Geometry.Pieces, metadata.EffectivePieceProperties, Symbol(depth)).Level;
            // Native entry formulas (DRLG_L2 ENTRY_MAIN / ENTRY_PREV) from the coarse stair anchors.
            var up = generation.StairsUp;
            var down = generation.StairsDown;
            var main = new AuthoritativeCathedralPoint(16 + 2 * up.X + 5, 16 + 2 * up.Y + 4);
            var previous = new AuthoritativeCathedralPoint(16 + 2 * down.X + 4, 16 + 2 * down.Y + 6);
            if (main != generation.EntryPosition)
                throw new InvalidDataException("Catacombs main entry does not match its generated stair anchor.");
            var points = new AuthoritativeCathedralLevel1RoutePoints(
                FindUniquePiece(level, UpStairPiece), FindUniquePiece(level, DownStairPiece), main, previous);
            var entries = new List<AuthoritativeCathedralPoint> { points.MainEntry, points.PreviousEntry };
            if (generation.TownWarpStairs is { } warp) {
                // Entry does not change the layout; DRLG_L2 derives ENTRY_TWARPDN from the WARPSTAIRS anchor.
                townWarp = (FindUniquePiece(level, TownWarpUpPiece), new(16 + 2 * warp.X + 5, 16 + 2 * warp.Y + 4));
                entries.Add(townWarp.Value.Entry);
            }
            foreach (var point in entries)
                if (!level.ContainsPosition(point.X, point.Y) || !level.IsWalkable(point.X, point.Y))
                    throw new InvalidDataException("A Catacombs stair entry is not an in-bounds walkable point.");
            // Like the unvisited Cathedral depths, the world holds a plain level until first entry.
            levels.Add(new AuthoritativeLevel((uint)depth, level.Width, level.Height) { StableSymbol = level.StableSymbol });
            routes.Add((uint)depth, points);
        }
        return new(metadata, automap, storySeed, levels, routes,
            townWarp ?? throw new InvalidDataException("Native L5 requires its Town warp stair."));
    }

    /**
     * The native staircase L4<->L5<->...<->L8. Multiplayer adds the always-open Town warp
     * (clvl 8 gate) and L5's warp-up; the single-player unlock is not modelled yet.
     */
    internal IReadOnlyList<AuthoritativeWarpTarget> CreateWarps(AuthoritativeCathedralLevel1RoutePoints level4,
        bool multiplayerTownWarp = false)
    {
        var warps = new List<AuthoritativeWarpTarget>();
        for (var depth = FirstDepth; depth <= LastDepth; depth++) {
            var target = routes[(uint)depth];
            var source = depth == FirstDepth ? level4 : routes[(uint)depth - 1];
            warps.Add(new((uint)(DownWarpIdBase + depth - 1), (uint)depth - 1, source.DownTrigger.X, source.DownTrigger.Y,
                (uint)depth, target.MainEntry.X, target.MainEntry.Y) { StableSymbol = $"{Symbol(depth - 1)}_to_{Symbol(depth)}" });
            warps.Add(new((uint)(UpWarpIdBase + depth - 1), (uint)depth, target.UpTrigger.X, target.UpTrigger.Y,
                (uint)depth - 1, source.PreviousEntry.X, source.PreviousEntry.Y) { StableSymbol = $"{Symbol(depth)}_to_{Symbol(depth - 1)}" });
        }
        if (multiplayerTownWarp) {
            warps.Add(new(TownWarpDownId, 0, TownWarpTrigger.X, TownWarpTrigger.Y, FirstDepth, townWarp.Entry.X, townWarp.Entry.Y) {
                StableSymbol = $"town_warp_to_{Symbol(FirstDepth)}", RequiredCharacterLevel = 8,
                FirstEntry = AuthoritativeCatacombsEntry.TownWarpDown,
            });
            warps.Add(new(TownWarpUpId, FirstDepth, townWarp.Trigger.X, townWarp.Trigger.Y, 0, TownWarpArrival.X, TownWarpArrival.Y) {
                StableSymbol = $"{Symbol(FirstDepth)}_town_warp_up",
            });
        }
        return warps;
    }

    /** Native LoadGameLevel for a first entry: the fresh floor, its ready phase and owners. */
    internal AuthoritativeCatacombsNativePopulationBuild Build(DiabloNativeFloorEntry entry,
        AuthoritativeCathedralNativeCharacterStart characterStart, AuthoritativeCathedralMonsterRuntimeCatalog runtime,
        AuthoritativePlayerClassCatalog classes, AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeItemCatalog items, AuthoritativeSpellCatalog spells,
        IReadOnlyCollection<AuthoritativeItemState> storeItems, AuthoritativeCathedralNativeItemSidecars itemSidecars,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer)
    {
        if (!Owns(entry.LevelId) || entry.CatacombsLifeQ6 is not { } life)
            throw new InvalidDataException("A Catacombs first entry requires its depth and sampled life.");
        var depth = (int)entry.LevelId;
        var hero = new AuthoritativeCatacombsFreshLevelHeroContext(entry.Hero.MaximumLifeBase, entry.Hero.MaximumManaBase,
            entry.Hero.LightRadius, entry.Hero.IdleFrames, entry.Hero.PreEntryFuture, entry.Hero.LevelChanging,
            entry.Hero.LevelLoading);
        var fresh = AuthoritativeCatacombsFreshLevelGenerator.Generate(metadata, depth, DungeonSeed, storySeed,
            entry.CatacombsEntry ?? AuthoritativeCatacombsEntry.Main, hero, Symbol(depth), initialUniqueItemFlags: entry.InitialUniqueItemFlags.ToHashSet(),
            gameMode: gameMode);
        var ready = AuthoritativeCatacombsLevelReadyGenerator.Generate(new(fresh,
            new(0, false, false, false, life, new(0, 0), new(0, 0), true), automap, portalsAbsent: true));
        return AuthoritativeCatacombsNativePopulationBuilder.Build(ready, entry.LevelId, characterStart, runtime,
            classes, monsterTypes, items, spells, [], storeItems, itemSidecars);
    }

    internal static string Symbol(int depth)
        => depth <= AuthoritativeCathedralDepthGenerator.MaximumSupportedDepth
            ? depth == 1 ? "cathedral" : $"cathedral_depth_{depth}"
            : $"catacombs_depth_{depth}";

    private static AuthoritativeCathedralPoint FindUniquePiece(AuthoritativeLevel level, ushort piece)
    {
        var layer = level.NativePieceLayer!;
        var matches = Enumerable.Range(0, layer.Count).Where(cell => layer[cell] == piece)
            .Select(cell => new AuthoritativeCathedralPoint(cell % level.Width, cell / level.Width)).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Native Catacombs requires exactly one stair piece {piece}, found {matches.Length}.");
        return matches[0];
    }
}
