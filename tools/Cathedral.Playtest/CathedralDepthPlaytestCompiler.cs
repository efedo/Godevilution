using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;

namespace Godevilution.Tools;

/** Selects the optional, explicitly authored diagnostic population. */
public enum CathedralDepthPlaytestPlacementPolicy
{
    None,
    AuthoredDiagnostic,
}

/** Inputs for the separate offline Cathedral depth-one-through-four compiler. */
public sealed record CathedralDepthPlaytestCompilerOptions(
    ContentPack BasePack,
    string OutputDirectory,
    string PieceDefinitionsTsv,
    string TilePropertiesTsv,
    IReadOnlyList<uint> DepthSeeds)
{
    public CathedralDepthPlaytestCompilerOptions(
        ContentPack basePack,
        string outputDirectory,
        string pieceDefinitionsTsv,
        string tilePropertiesTsv,
        uint depth1Seed,
        uint depth2Seed,
        uint depth3Seed,
        uint depth4Seed)
        : this(basePack, outputDirectory, pieceDefinitionsTsv, tilePropertiesTsv,
            [depth1Seed, depth2Seed, depth3Seed, depth4Seed])
    {
    }

    public string PackId { get; init; } = "godevilution.cathedral-depths-playtest";
    public string PackVersion { get; init; } = "0.1.0";
    public AuthoritativeCathedralEntry Entry { get; init; } = AuthoritativeCathedralEntry.Main;
    public CathedralDepthPlaytestPlacementPolicy PlacementPolicy { get; init; }
        = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic;
    /** Optional base-content drop identity for the authored diagnostic monster. */
    public string? DiagnosticDropItemSymbol { get; init; }
    /** When enabled, route and diagnostic placement may operate native-scanned doors. */
    internal bool DoorAwareConnectivity { get; init; }
    public uint Depth1Seed => DepthSeeds.Count > 0 ? DepthSeeds[0] : 0;
    public uint Depth2Seed => DepthSeeds.Count > 1 ? DepthSeeds[1] : 0;
    public uint Depth3Seed => DepthSeeds.Count > 2 ? DepthSeeds[2] : 0;
    public uint Depth4Seed => DepthSeeds.Count > 3 ? DepthSeeds[3] : 0;
}

/** Native coordinates and path metadata exposed to a depth playtest launcher. */
public readonly record struct CathedralDepthPlaytestPoint(int X, int Y);

/** One route identity emitted in the depth profile. */
public sealed record CathedralDepthPlaytestRouteProfile(
    uint RouteId,
    string RouteSymbol,
    string Direction,
    uint SourceLevelId,
    string SourceLevelSymbol,
    uint DestinationLevelId,
    string DestinationLevelSymbol);

/** Read-only per-depth geometry, placement, and staircase metadata. */
public sealed record CathedralDepthPlaytestLevelProfile(
    int Depth,
    uint LevelId,
    string LevelSymbol,
    uint DungeonSeed,
    string Entry,
    CathedralDepthPlaytestPoint MainEntry,
    CathedralDepthPlaytestPoint PreviousEntry,
    CathedralDepthPlaytestPoint UpTrigger,
    CathedralDepthPlaytestPoint DownTrigger,
    CathedralDepthPlaytestPoint SafeResumePosition,
    CathedralDepthPlaytestPoint BlockedFineCell,
    CathedralDepthPlaytestPoint? EncounterPosition,
    uint? EncounterEntityId,
    CathedralDepthPlaytestPoint? ObjectPosition,
    uint? ObjectEntityId,
    int TraversalPathDistance,
    IReadOnlyList<uint> RouteIds)
{
    /** Explicit staircase IDs used by launchers; L4 has no Down warp. */
    public uint? UpWarpId { get; init; }
    public uint? DownWarpId { get; init; }

    [JsonIgnore]
    public CathedralDepthPlaytestPoint SafeRestorePosition => SafeResumePosition;

    [JsonIgnore]
    public CathedralDepthPlaytestPoint? Encounter => EncounterPosition;

    [JsonIgnore]
    public CathedralDepthPlaytestPoint? Object => ObjectPosition;
}

/** Content-addressed metadata emitted beside a generated Cathedral depth pack. */
public sealed record CathedralDepthPlaytestProfile(
    int FormatVersion,
    string AdapterId,
    string ContentDirectory,
    string ContentId,
    string ContentVersion,
    string ContentManifestHash,
    string PackFileHash,
    IReadOnlyList<uint> DepthSeeds,
    string Entry,
    string PlacementPolicy,
    bool NativePopulationClaim,
    [property: JsonPropertyName("levels")]
    IReadOnlyList<CathedralDepthPlaytestLevelProfile> Levels,
    IReadOnlyList<CathedralDepthPlaytestRouteProfile> Routes)
{
    /** Optional authored diagnostic drop identity; absent profiles retain the legacy template. */
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiagnosticDropItemSymbol { get; init; }

    [JsonIgnore]
    public IReadOnlyList<CathedralDepthPlaytestLevelProfile> ReadonlyLevels => Levels;
}

/**
 * Builds a fresh, opt-in Cathedral depth-one-through-four content pack. The
 * compiler only accepts explicit semantic geometry and explicit per-depth
 * seeds; it never reads MPQs or derives a campaign seed.
 */
public static class CathedralDepthPlaytestCompiler
{
    public const string AdapterId = "devilution.diablo.cathedral-depths.v1";
    public const string ProfileFileName = "cathedral-depths-playtest.json";
    public const string ContentDirectoryName = "content";
    public const string GeneratorId = "original-diablo.cathedral-depths";
    public const int GeneratorRevision = 1;
    public const string CatalogId = "native-cathedral-depths";
    public const string Policy = "quest-disabled-original-diablo";

    private const uint ReturnToTownRouteId = 1099;
    private const uint FirstMiddleUpRouteId = 1100;
    private const uint FirstDiagnosticMonsterEntityId = 9_001;
    private const uint FirstDiagnosticObjectEntityId = 21_001;
    private const uint FirstDiagnosticDropEntityId = 1_000_009;

    private static readonly (
        uint Id, string Symbol, uint Source, string SourceSymbol, uint Destination,
        string DestinationSymbol, string Direction)[] RouteDefinitions =
    [
        (1001, "town_to_cathedral", 0, "town", 1, "cathedral", "down"),
        (1002, "cathedral_to_cathedral_depth_2", 1, "cathedral", 2, "cathedral_depth_2", "down"),
        (1003, "cathedral_depth_2_to_cathedral_depth_3", 2, "cathedral_depth_2", 3, "cathedral_depth_3", "down"),
        (1004, "cathedral_depth_3_to_cathedral_depth_4", 3, "cathedral_depth_3", 4, "cathedral_depth_4", "down"),
        (1099, "cathedral_to_town", 1, "cathedral", 0, "town", "up"),
        (1100, "cathedral_depth_2_to_cathedral", 2, "cathedral_depth_2", 1, "cathedral", "up"),
        (1101, "cathedral_depth_3_to_cathedral_depth_2", 3, "cathedral_depth_3", 2, "cathedral_depth_2", "up"),
        (1102, "cathedral_depth_4_to_cathedral_depth_3", 4, "cathedral_depth_4", 3, "cathedral_depth_3", "up"),
    ];

    private static readonly string[] QuestTables =
    [
        "quest_types.tsv",
        "quests.tsv",
        "quest_states.tsv",
        "quest_pools.tsv",
        "quest_sequence_variants.tsv",
        "quest_dialogue_stages.tsv",
        "quest_towner_interactions.tsv",
        "item_quest_effects.tsv",
    ];

    public static CathedralDepthPlaytestProfile Compile(
        CathedralDepthPlaytestCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BasePack);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PieceDefinitionsTsv);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TilePropertiesTsv);
        if (options.DepthSeeds is null || options.DepthSeeds.Count != 4)
            throw new ArgumentException("Exactly four explicit Cathedral depth seeds are required.", nameof(options.DepthSeeds));
        if (!Enum.IsDefined(options.Entry))
            throw new ArgumentOutOfRangeException(nameof(options.Entry));
        if (!Enum.IsDefined(options.PlacementPolicy))
            throw new ArgumentOutOfRangeException(nameof(options.PlacementPolicy));
        if (options.DiagnosticDropItemSymbol is not null
            && options.PlacementPolicy != CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic)
            throw new ArgumentException(
                "A diagnostic drop item requires the authored-diagnostic placement policy.",
                nameof(options.DiagnosticDropItemSymbol));
        if (options.DiagnosticDropItemSymbol is not null
            && string.IsNullOrWhiteSpace(options.DiagnosticDropItemSymbol))
            throw new ArgumentException("A diagnostic drop item symbol cannot be blank.",
                nameof(options.DiagnosticDropItemSymbol));

        var pieceTable = TsvTable.Parse(
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            options.PieceDefinitionsTsv);
        var propertyTable = TsvTable.Parse(
            AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            options.TilePropertiesTsv);
        AuthoritativeDunPieceCatalog.ValidateExternalizedProduction(pieceTable);
        AuthoritativeDunTilePropertyCatalog.ValidateExternalizedProduction(propertyTable);
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            pieceTable.SourcePath,
            pieceTable.ToCanonicalTsv(),
            propertyTable.SourcePath,
            propertyTable.ToCanonicalTsv());

        var tables = options.BasePack.Tables
            .Select(NormalizeTable)
            .ToDictionary(table => table.SourcePath, StringComparer.OrdinalIgnoreCase);
        tables["levels.tsv"] = BuildLevels(RequireTable(tables, "levels.tsv"));
        tables["content_ids.tsv"] = BuildContentIds(RequireTable(tables, "content_ids.tsv"));
        tables["warps.tsv"] = BuildWarps();
        tables[AuthoritativeNativeGeneratedRouteBindings.TableName] = BuildRouteBindings();
        tables[AuthoritativeCathedralDepthRuntimeCatalog.TableName] = BuildDepthCatalog(options);
        tables[AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath] = NormalizeTable(pieceTable);
        tables[AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath] = NormalizeTable(propertyTable);
        tables.Remove(AuthoritativeCathedralLevel1RuntimeCatalog.TableName);

        // Omit the optional prototype generator rather than activating an empty catalog.
        tables.Remove("procedural_levels.tsv");
        ClearIfPresent(tables, "native_dun_fragments.tsv");
        ClearIfPresent(tables, "native_dun_fragment_objects.tsv");
        ClearIfPresent(tables, "native_dun_objects.tsv");
        ClearIfPresent(tables, "native_dun_levels.tsv");
        ClearIfPresent(tables, "native_dun_monsters.tsv");
        ClearIfPresent(tables, "portals.tsv");
        ClearIfPresent(tables, "world_mutations.tsv");
        tables.Remove("unique_monsters.tsv");
        tables.Remove("unique_monster_placements.tsv");
        tables.Remove("unique_monster_packs.tsv");
        ClearIfPresent(tables, "object_item_gates.tsv");
        ClearIfPresent(tables, "object_presentations.tsv");
        tables.Remove("set_level_objects.tsv");
        tables.Remove("set_level_maps.tsv");
        tables.Remove("set_level_names.tsv");
        tables.Remove("procedural_set_piece_origins.tsv");
        tables.Remove("procedural_quest_piece_origins.tsv");
        ClampLevelGeneration(tables);
        foreach (var tableName in QuestTables)
        {
            if (tableName == "quests.tsv")
                ClearIfPresent(tables, tableName);
            else
                tables.Remove(tableName);
        }
        RemoveGeneratedLevelStartOverrides(tables);

        var stableIds = LoadStableIds(tables["content_ids.tsv"]);
        var diagnosticDropItem = ResolveDiagnosticDropItem(
            options.DiagnosticDropItemSymbol, tables["items.tsv"], stableIds);
        var world = BuildWorld(tables["levels.tsv"]);
        var catalog = AuthoritativeCathedralDepthRuntimeCatalog.Load(
            tables[AuthoritativeCathedralDepthRuntimeCatalog.TableName].ToCanonicalTsv(),
            stableIds);
        var plan = catalog.Prepare(world, geometry);
        if (plan.Candidates.Count != 4)
            throw new InvalidDataException("Cathedral depth compiler requires exactly four generated candidates.");

        ValidateStairConnectivity(plan.Candidates, options.DoorAwareConnectivity);
        var placements = SelectPlacements(
            plan.Candidates, options.PlacementPolicy, options.DoorAwareConnectivity);
        tables["objects.tsv"] = BuildObjects(
            RequireTable(tables, "objects.tsv"), placements, options.PlacementPolicy);
        tables["monsters.tsv"] = BuildMonsters(
            RequireTable(tables, "monsters.tsv"), placements, options.PlacementPolicy, diagnosticDropItem);

        var outputPack = new ContentPack(
            options.PackId,
            options.PackVersion,
            tables.Values.OrderBy(table => table.SourcePath, StringComparer.Ordinal).ToArray(),
            schema: options.BasePack.Schema,
            assets: []);
        var prevalidatedManifest = ValidatePack(outputPack);
        var profileLevels = BuildProfileLevels(plan.Candidates, placements, options);
        var profileRoutes = RouteDefinitions.Select(route => new CathedralDepthPlaytestRouteProfile(
            route.Id,
            route.Symbol,
            route.Direction,
            route.Source,
            route.SourceSymbol,
            route.Destination,
            route.DestinationSymbol)).ToArray();
        var outputRoot = ValidateFreshOutputPath(options.OutputDirectory);
        var stagingRoot = outputRoot + ".staging-" + Guid.NewGuid().ToString("N");
        RejectReparseAncestors(stagingRoot);
        try
        {
            Directory.CreateDirectory(stagingRoot);
            RejectReparseAncestors(stagingRoot);
            var contentRoot = Path.Combine(stagingRoot, ContentDirectoryName);
            Directory.CreateDirectory(contentRoot);
            WritePack(contentRoot, outputPack);
            WritePackManifest(contentRoot, outputPack);
            var validatedManifest = ValidateWrittenPack(contentRoot);
            if (!string.Equals(prevalidatedManifest.Sha256, validatedManifest.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Serialized Cathedral depth content changed after in-memory validation.");
            var profile = new CathedralDepthPlaytestProfile(
                1,
                AdapterId,
                ContentDirectoryName,
                options.PackId,
                options.PackVersion,
                validatedManifest.Sha256,
                ComputePackFileHash(contentRoot),
                options.DepthSeeds.ToArray(),
                options.Entry.ToString().ToLowerInvariant(),
                PlacementPolicyText(options.PlacementPolicy),
                false,
                profileLevels,
                profileRoutes) {
                DiagnosticDropItemSymbol = options.DiagnosticDropItemSymbol,
            };
            WriteProfile(Path.Combine(stagingRoot, ProfileFileName), profile);
            Directory.Move(stagingRoot, outputRoot);
            return profile;
        }
        catch
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
            throw;
        }
    }

    private static CathedralDepthPlaytestLevelProfile[] BuildProfileLevels(
        IReadOnlyList<AuthoritativeCathedralDepthGenerationCandidate> candidates,
        IReadOnlyList<Placement> placements,
        CathedralDepthPlaytestCompilerOptions options)
    {
        return candidates.Select((candidate, index) => {
            var depth = index + 1;
            var placement = placements[index];
            var routeIds = new List<uint>();
            if (depth == 1)
                routeIds.Add(ReturnToTownRouteId);
            else
                routeIds.Add(FirstMiddleUpRouteId + (uint)(depth - 2));
            if (depth < 4)
                routeIds.Add(1000U + (uint)depth + 1U);
            return new CathedralDepthPlaytestLevelProfile(
                depth,
                candidate.Level.LevelId,
                candidate.Level.StableSymbol,
                options.DepthSeeds[index],
                options.Entry.ToString().ToLowerInvariant(),
                ToPoint(candidate.Routes.MainEntry),
                ToPoint(candidate.Routes.PreviousEntry),
                ToPoint(candidate.Routes.UpTrigger),
                ToPoint(candidate.Routes.DownTrigger),
                placement.SafeResume,
                placement.Blocked,
                placement.Encounter,
                placement.Encounter is null ? null : FirstDiagnosticMonsterEntityId + (uint)index,
                placement.Object,
                placement.Object is null ? null : FirstDiagnosticObjectEntityId + (uint)index,
                placement.PathDistance,
                routeIds) {
                UpWarpId = depth == 1
                    ? ReturnToTownRouteId
                    : FirstMiddleUpRouteId + (uint)(depth - 2),
                DownWarpId = depth < 4 ? 1000U + (uint)depth + 1U : null,
            };
        }).ToArray();
    }

    private static IReadOnlyList<Placement> SelectPlacements(
        IReadOnlyList<AuthoritativeCathedralDepthGenerationCandidate> candidates,
        CathedralDepthPlaytestPlacementPolicy policy,
        bool doorAwareConnectivity)
    {
        var placements = new List<Placement>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var path = PathFromEntry(candidate.Level, candidate.Routes, doorAwareConnectivity);
            if (path.Length < 9)
                throw new InvalidDataException($"Generated Cathedral depth {candidate.Level.LevelId} has no playable diagnostic corridor.");
            // Native discovery rebuilds the exact closed collision/layer baseline
            // that activation publishes. Only door mode needs this extra snapshot;
            // the legacy branch intentionally retains its original level predicate.
            var closedLevel = doorAwareConnectivity
                ? AuthoritativeCathedralDoors.Discover(candidate.Level).Level
                : candidate.Level;
            var reserved = doorAwareConnectivity ? DoorReservedCells(closedLevel) : null;
            var safe = reserved is null
                ? path[Math.Min(6, path.Length - 1)]
                : SelectPathCell(path, 6, reserved!, closedLevel,
                    $"Generated Cathedral depth {candidate.Level.LevelId} has no safe closed-state resume cell.");
            var blocked = FindNearbyBlockedCell(closedLevel, safe, reserved);
            CathedralDepthPlaytestPoint? encounter = policy == CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic
                ? reserved is null
                    ? path[Math.Min(7, path.Length - 1)]
                    : SelectPathCell(path, 7, reserved!, closedLevel,
                        $"Generated Cathedral depth {candidate.Level.LevelId} has no safe diagnostic encounter cell.")
                : null;
            CathedralDepthPlaytestPoint? @object = policy == CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic
                ? reserved is null
                    ? path[3]
                    : SelectPathCell(path, 3, reserved!, closedLevel,
                        $"Generated Cathedral depth {candidate.Level.LevelId} has no safe diagnostic object cell.")
                : null;
            if (encounter is not null && @object is not null && encounter == @object)
                throw new InvalidDataException("Authored Cathedral diagnostic encounter and object overlap.");
            placements.Add(new Placement(@object, encounter, safe, blocked, path.Length - 1));
        }
        return placements;
    }

    private static CathedralDepthPlaytestPoint[] PathFromEntry(
        AuthoritativeLevel level,
        AuthoritativeCathedralLevel1RoutePoints routes,
        bool doorAwareConnectivity)
    {
        var navigationLevel = doorAwareConnectivity
            ? OpenReachableDoors(level, routes.MainEntry, routes.DownTrigger)
            : level;
        var start = (routes.MainEntry.X, routes.MainEntry.Y);
        var blockedAnchors = new HashSet<(int X, int Y)>
        {
            (routes.UpTrigger.X, routes.UpTrigger.Y),
            (routes.DownTrigger.X, routes.DownTrigger.Y),
            (routes.MainEntry.X, routes.MainEntry.Y),
            (routes.PreviousEntry.X, routes.PreviousEntry.Y),
        };
        var queue = new Queue<(int X, int Y)>();
        var previous = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        queue.Enqueue(start);
        var directions = DeterministicGridNavigation.NeighborDisplacements;
        (int X, int Y)? target = null;
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            var distance = Distance(previous, current);
            if (distance >= 8 && !blockedAnchors.Contains(current))
            {
                target = current;
                break;
            }
            foreach (var (dx, dy) in directions)
            {
                var next = (X: current.X + dx, Y: current.Y + dy);
                if (previous.ContainsKey(next)
                    || (next != start && blockedAnchors.Contains(next))
                    || !IsCompilerWalkable(navigationLevel, next.X, next.Y, doorAwareConnectivity))
                    continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (target is null)
            throw new InvalidDataException($"Generated Cathedral level {level.LevelId} has no reachable diagnostic corridor.");
        var path = new List<CathedralDepthPlaytestPoint>();
        var cursor = target.Value;
        while (true)
        {
            path.Add(new CathedralDepthPlaytestPoint(cursor.X, cursor.Y));
            if (cursor == start)
                break;
            cursor = previous[cursor];
        }
        path.Reverse();
        return path.ToArray();

        static int Distance(
            IReadOnlyDictionary<(int X, int Y), (int X, int Y)> previous,
            (int X, int Y) point)
        {
            var distance = 0;
            var cursor = point;
            while (previous[cursor] != cursor)
            {
                cursor = previous[cursor];
                distance++;
            }
            return distance;
        }
    }

    private static CathedralDepthPlaytestPoint FindNearbyBlockedCell(
        AuthoritativeLevel level,
        CathedralDepthPlaytestPoint origin,
        IReadOnlySet<(int X, int Y)>? reserved = null)
    {
        for (var radius = 1; radius < 16; radius++)
        {
            for (var y = origin.Y - radius; y <= origin.Y + radius; y++)
            {
                for (var x = origin.X - radius; x <= origin.X + radius; x++)
                {
                    if (Math.Abs(x - origin.X) + Math.Abs(y - origin.Y) != radius
                        || !level.ContainsPosition(x, y)
                        || level.IsWalkable(x, y)
                        || reserved?.Contains((x, y)) == true)
                        continue;
                    return new CathedralDepthPlaytestPoint(x, y);
                }
            }
        }
        throw new InvalidDataException($"Generated Cathedral level {level.LevelId} has no nearby blocked fine cell.");
    }

    private static HashSet<(int X, int Y)> DoorReservedCells(AuthoritativeLevel level)
    {
        var reserved = level.NativePieceLayer.Select((piece, cell) => (piece, cell))
            .Where(value => value.piece == 269)
            .Select(value => (value.cell % level.Width, value.cell / level.Width))
            .ToHashSet();
        foreach (var door in AuthoritativeCathedralDoors.Discover(level).Doors)
            reserved.Add((door.PositionX, door.PositionY));
        return reserved;
    }

    private static CathedralDepthPlaytestPoint SelectPathCell(
        IReadOnlyList<CathedralDepthPlaytestPoint> path,
        int minimumDistance,
        IReadOnlySet<(int X, int Y)> reserved,
        AuthoritativeLevel closedLevel,
        string error)
    {
        for (var index = Math.Min(minimumDistance, path.Count - 1); index < path.Count; index++)
            if (!reserved.Contains((path[index].X, path[index].Y))
                && closedLevel.IsWalkable(path[index].X, path[index].Y))
                return path[index];
        throw new InvalidDataException(error);
    }

    private static void ValidateStairConnectivity(
        IReadOnlyList<AuthoritativeCathedralDepthGenerationCandidate> candidates,
        bool doorAwareConnectivity)
    {
        foreach (var candidate in candidates)
        {
            var routes = candidate.Routes;
            RequireStairPath(candidate.Level, routes.MainEntry, routes.UpTrigger,
                candidate.Identity.Depth < 4 ? routes.DownTrigger : null, "MAIN to Up",
                doorAwareConnectivity);
            if (candidate.Identity.Depth < 4)
            {
                RequireStairPath(candidate.Level, routes.MainEntry, routes.DownTrigger,
                    routes.UpTrigger, "MAIN to Down", doorAwareConnectivity);
                RequireStairPath(candidate.Level, routes.PreviousEntry, routes.UpTrigger,
                    routes.DownTrigger, "PREV to Up", doorAwareConnectivity);
            }
        }
    }

    private static void RequireStairPath(AuthoritativeLevel level,
        AuthoritativeCathedralPoint start, AuthoritativeCathedralPoint target,
        AuthoritativeCathedralPoint? otherTrigger, string direction,
        bool doorAwareConnectivity)
    {
        var navigationLevel = doorAwareConnectivity
            ? OpenReachableDoors(level, start, otherTrigger)
            : level;
        var queue = new Queue<AuthoritativeCathedralPoint>();
        var visited = new HashSet<AuthoritativeCathedralPoint> { start };
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            if (current == target)
                return;
            foreach (var offset in DeterministicGridNavigation.NeighborDisplacements)
            {
                var next = new AuthoritativeCathedralPoint(current.X + offset.X, current.Y + offset.Y);
                if (next == otherTrigger || !IsCompilerWalkable(navigationLevel, next.X, next.Y, doorAwareConnectivity)
                    || !visited.Add(next))
                    continue;
                queue.Enqueue(next);
            }
        }
        throw new InvalidDataException($"Generated Cathedral depth {level.LevelId} has no {direction} stair path. "
            + "The diagnostic profile does not supply native object/door placement; choose explicit supported inputs, not altered geometry.");
    }

    /** Opens only doors reachable from the current side, retaining native mutation semantics. */
    private static AuthoritativeLevel OpenReachableDoors(
        AuthoritativeLevel level,
        AuthoritativeCathedralPoint start,
        AuthoritativeCathedralPoint? excluded)
    {
        var discovery = AuthoritativeCathedralDoors.Discover(level);
        var current = discovery.Level;
        var doors = discovery.Doors.ToArray();
        var opened = new HashSet<(int X, int Y)>();
        while (true)
        {
            var reachable = FloodWalkable(current, start, excluded);
            var changed = false;
            foreach (var door in doors)
            {
                if (opened.Contains((door.PositionX, door.PositionY))
                    || !IsDoorApproachable(door, reachable))
                    continue;
                current = AuthoritativeCathedralDoors.Operate(current, door, occupied: false).Level;
                opened.Add((door.PositionX, door.PositionY));
                changed = true;
            }
            if (!changed)
                return current;
        }
    }

    private static HashSet<(int X, int Y)> FloodWalkable(
        AuthoritativeLevel level,
        AuthoritativeCathedralPoint start,
        AuthoritativeCathedralPoint? excluded)
    {
        var reachable = new HashSet<(int X, int Y)>();
        if (!IsCompilerWalkable(level, start.X, start.Y, doorAwareConnectivity: true))
            return reachable;
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((start.X, start.Y));
        reachable.Add((start.X, start.Y));
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            foreach (var offset in DeterministicGridNavigation.NeighborDisplacements)
            {
                var next = (X: current.X + offset.X, Y: current.Y + offset.Y);
                if (excluded is not null && next.X == excluded.Value.X && next.Y == excluded.Value.Y)
                    continue;
                if (reachable.Contains(next) || !IsCompilerWalkable(level, next.X, next.Y, doorAwareConnectivity: true))
                    continue;
                reachable.Add(next);
                queue.Enqueue(next);
            }
        }
        return reachable;
    }

    private static bool IsDoorApproachable(
        AuthoritativeCathedralDoorState door,
        IReadOnlySet<(int X, int Y)> reachable)
    {
        for (var y = door.PositionY - 1; y <= door.PositionY + 1; y++)
            for (var x = door.PositionX - 1; x <= door.PositionX + 1; x++)
                if (reachable.Contains((x, y)))
                    return true;
        return false;
    }

    private static bool IsCompilerWalkable(
        AuthoritativeLevel level, int x, int y, bool doorAwareConnectivity = false)
        => level.ContainsPosition(x, y)
            && level.IsWalkable(x, y)
            && (!doorAwareConnectivity || level.NativePieceLayer[y * level.Width + x] != 269);

    private static TsvTable BuildDepthCatalog(CathedralDepthPlaytestCompilerOptions options)
    {
        var output = new StringBuilder("level_id\tlevel_symbol\tdepth\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\tpolicy\n");
        for (var depth = 1; depth <= 4; depth++)
        {
            output.Append(depth).Append('\t').Append(LevelSymbol(depth)).Append('\t').Append(depth)
                .Append('\t').Append(GeneratorId).Append('\t').Append(GeneratorRevision)
                .Append('\t').Append(options.DepthSeeds[depth - 1].ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(options.Entry == AuthoritativeCathedralEntry.Main ? "main" : "previous")
                .Append('\t').Append(Policy).Append('\n');
        }
        return TsvTable.Parse(AuthoritativeCathedralDepthRuntimeCatalog.TableName, output.ToString());
    }

    private static TsvTable BuildLevels(TsvTable source)
    {
        var town = source.Rows.SingleOrDefault(row => row.RequiredUInt32("level_id") == 0)
            ?? throw new InvalidDataException("Base content is missing Town level 0.");
        var cathedral = source.Rows.SingleOrDefault(row => row.RequiredUInt32("level_id") == 1)
            ?? throw new InvalidDataException("Base content is missing Cathedral level 1.");
        var rows = new List<IReadOnlyDictionary<string, string>> { town.Values };
        for (var depth = 1; depth <= 4; depth++)
        {
            var values = cathedral.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            values["level_id"] = depth.ToString(CultureInfo.InvariantCulture);
            values["level_symbol"] = LevelSymbol(depth);
            values["default_start"] = depth == 1 ? "1" : "0";
            values["suppress_multiplayer_death_drops"] = "0";
            rows.Add(values);
        }
        return CreateTable(source, rows);
    }

    private static TsvTable BuildContentIds(TsvTable source)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        foreach (var row in source.Rows)
        {
            var kind = row.Required("kind");
            var numericId = row.RequiredUInt32("numeric_id");
            if (kind == "level" && numericId > 1
                || kind == "warp"
                || kind == "portal"
                || kind == "quest"
                || kind == "set_level"
                || kind == "procedural_origin"
                || kind == "unique_monster"
                || kind == "object" && numericId != 1)
                continue;
            rows.Add(row.Values);
        }
        for (var depth = 2; depth <= 4; depth++)
            rows.Add(new Dictionary<string, string>(StringComparer.Ordinal) {
                ["kind"] = "level",
                ["symbol"] = LevelSymbol(depth),
                ["numeric_id"] = depth.ToString(CultureInfo.InvariantCulture),
            });
        foreach (var route in RouteDefinitions)
            rows.Add(new Dictionary<string, string>(StringComparer.Ordinal) {
                ["kind"] = "warp",
                ["symbol"] = route.Symbol,
                ["numeric_id"] = route.Id.ToString(CultureInfo.InvariantCulture),
            });
        return CreateTable(source, rows);
    }

    private static TsvTable BuildWarps()
    {
        const string header = "warp_id\twarp_symbol\tsource_level_id\tsource_level_symbol\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_level_symbol\ttarget_x\ttarget_y\trequired_quest_id\trequired_quest_symbol\trequired_quest_completed\ttarget_set_level_id\ttarget_set_level_symbol\ttarget_set_level_active_map\n";
        var output = new StringBuilder(header);
        foreach (var route in RouteDefinitions)
        {
            var sourceX = route.Source == 0 ? 57 : 36;
            var sourceY = route.Source == 0 ? 40 : 36;
            var targetX = route.Destination == 0 ? 25 : 1;
            var targetY = route.Destination == 0 ? 31 : 1;
            output.Append(route.Id).Append('\t').Append(route.Symbol).Append('\t')
                .Append(route.Source).Append('\t').Append(route.SourceSymbol).Append('\t')
                .Append(sourceX).Append('\t').Append(sourceY).Append('\t')
                .Append(route.Destination).Append('\t').Append(route.DestinationSymbol).Append('\t')
                .Append(targetX).Append('\t').Append(targetY).Append("\t0\t\t0\tnone\t\tfalse\n");
        }
        return TsvTable.Parse("warps.tsv", output.ToString());
    }

    private static TsvTable BuildRouteBindings()
    {
        var output = new StringBuilder("route_kind\troute_id\troute_symbol\tendpoint\tlevel_id\tlevel_symbol\tanchor\n");
        foreach (var route in RouteDefinitions)
        {
            var sourceAnchor = route.Source == 0
                ? "town_cathedral_trigger"
                : route.Direction == "down" ? "down_trigger" : "up_trigger";
            var destinationAnchor = route.Destination == 0
                ? "town_cathedral_return"
                : route.Direction == "down" ? "main_entry" : "previous_entry";
            output.Append("warp\t").Append(route.Id).Append('\t').Append(route.Symbol)
                .Append("\tsource\t").Append(route.Source).Append('\t').Append(route.SourceSymbol)
                .Append('\t').Append(sourceAnchor).Append('\n');
            output.Append("warp\t").Append(route.Id).Append('\t').Append(route.Symbol)
                .Append("\tdestination\t").Append(route.Destination).Append('\t').Append(route.DestinationSymbol)
                .Append('\t').Append(destinationAnchor).Append('\n');
        }
        return TsvTable.Parse(AuthoritativeNativeGeneratedRouteBindings.TableName, output.ToString());
    }

    private static AuthoritativeWorld BuildWorld(TsvTable levels)
    {
        var world = new AuthoritativeWorld();
        foreach (var row in levels.Rows)
        {
            var level = new AuthoritativeLevel(
                row.RequiredUInt32("level_id"),
                row.RequiredInt32("width"),
                row.RequiredInt32("height"),
                isDefaultStart: row.OptionalBoolean("default_start"),
                suppressMultiplayerDeathDrops: row.OptionalBoolean("suppress_multiplayer_death_drops")) {
                StableSymbol = row.Required("level_symbol"),
            };
            world.AddLevel(level);
        }
        return world;
    }

    private static StableContentIdCatalog LoadStableIds(TsvTable table)
    {
        StableContentIdCatalog.ValidateExternalizedProduction(table);
        return StableContentIdCatalog.LoadTsv(table.SourcePath, table.ToCanonicalTsv());
    }

    private static TsvTable BuildObjects(
        TsvTable source,
        IReadOnlyList<Placement> placements,
        CathedralDepthPlaytestPlacementPolicy policy)
    {
        var template = source.Rows.SingleOrDefault(row => row.RequiredUInt32("entity_id") == 2001)
            ?? throw new InvalidDataException("Base content is missing the ordinary object template entity 2001.");
        var rows = new List<IReadOnlyDictionary<string, string>> { BaseTemplate(template) };
        if (policy == CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic)
        {
            for (var index = 0; index < placements.Count; index++)
            {
                var placement = placements[index];
                var values = BaseTemplate(template).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                var depth = index + 1;
                values["entity_id"] = (FirstDiagnosticObjectEntityId + (uint)index).ToString(CultureInfo.InvariantCulture);
                values["level_id"] = depth.ToString(CultureInfo.InvariantCulture);
                values["level_symbol"] = LevelSymbol(depth);
                values["position_x"] = placement.Object!.Value.X.ToString(CultureInfo.InvariantCulture);
                values["position_y"] = placement.Object!.Value.Y.ToString(CultureInfo.InvariantCulture);
                values["template_only"] = "0";
                values["object_symbol"] = "l1_left_door";
                values["effect_kind"] = "Heal";
                values["effect_amount"] = "0";
                values["missile_passable"] = "1";
                rows.Add(values);
            }
        }
        return CreateTable(source, rows);
    }

    private static TsvTable BuildMonsters(
        TsvTable source,
        IReadOnlyList<Placement> placements,
        CathedralDepthPlaytestPlacementPolicy policy,
        DiagnosticDropItem? diagnosticDropItem)
    {
        if (policy == CathedralDepthPlaytestPlacementPolicy.None)
            return CreateTable(source, []);
        var template = source.Rows.SingleOrDefault(row => row.RequiredUInt32("entity_id") == 9)
            ?? throw new InvalidDataException("Base content is missing the ordinary diagnostic monster template entity 9.");
        var rows = new List<IReadOnlyDictionary<string, string>>();
        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            var values = template.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var depth = index + 1;
            values["entity_id"] = (FirstDiagnosticMonsterEntityId + (uint)index).ToString(CultureInfo.InvariantCulture);
            values["level_id"] = depth.ToString(CultureInfo.InvariantCulture);
            values["level_symbol"] = LevelSymbol(depth);
            values["position_x"] = placement.Encounter!.Value.X.ToString(CultureInfo.InvariantCulture);
            values["position_y"] = placement.Encounter!.Value.Y.ToString(CultureInfo.InvariantCulture);
            values["aggro_range"] = "0";
            values["drop_item_entity_id"] = (FirstDiagnosticDropEntityId + (uint)index).ToString(CultureInfo.InvariantCulture);
            values["drop_item_seed"] = (6001U + (uint)index).ToString(CultureInfo.InvariantCulture);
            if (diagnosticDropItem is { } dropItem) {
                values["drop_item_id"] = dropItem.Id.ToString(CultureInfo.InvariantCulture);
                values["drop_item_symbol"] = dropItem.Symbol;
            }
            rows.Add(values);
        }
        return CreateTable(source, rows);
    }

    private static DiagnosticDropItem? ResolveDiagnosticDropItem(
        string? symbol,
        TsvTable itemTable,
        StableContentIdCatalog stableIds)
    {
        if (symbol is null)
            return null;
        var matchingRows = itemTable.Rows.Where(row =>
            string.Equals(row.Required("item_symbol"), symbol, StringComparison.Ordinal)).ToArray();
        if (matchingRows.Length != 1)
            throw new InvalidDataException(
                $"Diagnostic drop item symbol '{symbol}' must identify exactly one base item.");

        if (!stableIds.TryResolve("item", symbol, out var itemId)
            || itemId != matchingRows[0].RequiredUInt32("item_id"))
            throw new InvalidDataException(
                $"Diagnostic drop item symbol '{symbol}' has no matching stable base item ID.");
        return new DiagnosticDropItem(itemId, symbol);
    }

    private sealed record DiagnosticDropItem(uint Id, string Symbol);

    private static IReadOnlyDictionary<string, string> BaseTemplate(TsvRow template)
    {
        var values = template.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values["level_id"] = "0";
        values["level_symbol"] = "town";
        values["position_x"] = "75";
        values["position_y"] = "68";
        values["template_only"] = "1";
        values["quest_id"] = "0";
        values["quest_symbol"] = "";
        values["effect_kind"] = "None";
        values["effect_amount"] = "0";
        values["reward_kind"] = "None";
        values["reward_id"] = "0";
        values["native_object_id"] = "0";
        return values;
    }

    private static void RemoveGeneratedLevelStartOverrides(
        IDictionary<string, TsvTable> tables)
    {
        if (!tables.TryGetValue("level_start_positions.tsv", out var table))
            return;
        var rows = table.Rows.Where(row => row.RequiredUInt32("level_id") == 0).Select(row => row.Values);
        tables[table.SourcePath] = CreateTable(table, rows);
    }

    private static void ClampLevelGeneration(IDictionary<string, TsvTable> tables)
    {
        if (!tables.TryGetValue("level_generation.tsv", out var table))
            return;
        var rows = table.Rows
            .Select(row => {
                var values = row.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                values["maximum_level"] = Math.Min(
                    4,
                    row.RequiredInt32("maximum_level")).ToString(CultureInfo.InvariantCulture);
                return values;
            })
            .Where(values => int.Parse(values["minimum_level"], CultureInfo.InvariantCulture) <= 4
                && int.Parse(values["maximum_level"], CultureInfo.InvariantCulture) >= 0);
        tables[table.SourcePath] = CreateTable(table, rows);
    }

    private static void ClearIfPresent(IDictionary<string, TsvTable> tables, string sourcePath)
    {
        if (tables.TryGetValue(sourcePath, out var table))
            tables[table.SourcePath] = CreateTable(table, []);
    }

    private static string LevelSymbol(int depth)
        => depth == 1 ? "cathedral" : $"cathedral_depth_{depth}";

    private static string PlacementPolicyText(CathedralDepthPlaytestPlacementPolicy policy)
        => policy == CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic
            ? "authored-diagnostic"
            : "none";

    private static TsvTable RequireTable(
        IReadOnlyDictionary<string, TsvTable> tables,
        string sourcePath)
        => tables.TryGetValue(sourcePath, out var table)
            ? table
            : throw new InvalidDataException($"Base content is missing required table '{sourcePath}'.");

    private static TsvTable CreateTable(
        TsvTable source,
        IEnumerable<IReadOnlyDictionary<string, string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendJoin('\t', source.Columns).Append('\n');
        foreach (var row in rows)
            builder.AppendJoin('\t', source.Columns.Select(column => row[column])).Append('\n');
        return NormalizeTable(TsvTable.Parse(source.SourcePath, builder.ToString()));
    }

    private static TsvTable NormalizeTable(TsvTable table)
        => TsvTable.Parse(table.SourcePath, table.ToCanonicalTsv());

    private static ContentManifest ValidateWrittenPack(string contentRoot)
    {
        var writtenPack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        return ValidatePack(writtenPack);
    }

    private static ContentManifest ValidatePack(ContentPack pack)
    {
        var manifest = new ContentManifest(pack.Id, pack.Version, [pack]);
        var composed = ComposedContent.Compose(manifest);
        _ = DiabloModule.Instance.CathedralDepthContentBootstrap.ActivateDepths(composed);
        return manifest;
    }

    private static string ValidateFreshOutputPath(string requested)
    {
        var path = Path.GetFullPath(requested);
        RejectReparseAncestors(path);
        if (Directory.Exists(path))
            throw new IOException($"Playtest output directory '{requested}' must not already exist.");
        if (File.Exists(path))
            throw new IOException($"Playtest output path '{requested}' is an existing file.");
        return path;
    }

    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path);
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.Exists
                && (File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    $"Playtest output path '{path}' traverses reparse-point directory '{directory.FullName}'.");
        }
    }

    private static void WritePack(string root, ContentPack pack)
    {
        foreach (var table in pack.Tables)
        {
            var path = Path.Combine(root, table.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToCrLf(table.ToCanonicalTsv()), new UTF8Encoding(false));
        }
        foreach (var asset in pack.Assets)
        {
            var path = Path.Combine(root, asset.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, asset.Contents.ToArray());
        }
    }

    private static void WritePackManifest(string root, ContentPack pack)
    {
        var manifest = new {
            formatVersion = 1,
            id = pack.Id,
            version = pack.Version,
            schema = pack.Schema is null ? null : new { id = pack.Schema.Id, version = pack.Schema.Version.ToString() },
        };
        File.WriteAllText(
            Path.Combine(root, ContentPackLoader.ManifestFileName),
            ToCrLf(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })),
            new UTF8Encoding(false));
    }

    private static void WriteProfile(string path, CathedralDepthPlaytestProfile profile)
    {
        File.WriteAllText(
            path,
            ToCrLf(JsonSerializer.Serialize(profile, new JsonSerializerOptions {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            })),
            new UTF8Encoding(false));
    }

    private static string ComputePackFileHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(path => new { FullPath = path, Relative = Path.GetRelativePath(root, path).Replace('\\', '/') })
                     .OrderBy(file => file.Relative, StringComparer.Ordinal))
        {
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path.FullPath)));
            var record = $"{path.Relative.Length}:{path.Relative}:{fileHash}\n";
            hash.AppendData(Encoding.UTF8.GetBytes(record));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static CathedralDepthPlaytestPoint ToPoint(AuthoritativeCathedralPoint point)
        => new(point.X, point.Y);

    private sealed record Placement(
        CathedralDepthPlaytestPoint? Object,
        CathedralDepthPlaytestPoint? Encounter,
        CathedralDepthPlaytestPoint SafeResume,
        CathedralDepthPlaytestPoint Blocked,
        int PathDistance);

    private static string ToCrLf(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
}
