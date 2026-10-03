using Devilution.Engine;

namespace Devilution.Diablo;

using Devilution.Data;
using Devilution.Shared;
using System.Collections.ObjectModel;
using System.Globalization;

/** Validated level geometry used by authoritative movement and transitions. */
public sealed class AuthoritativeWorld
{
    private Dictionary<uint, AuthoritativeLevel> levels = new();
    private Dictionary<uint, AuthoritativeWarpTarget> warpTargets = new();

    private static readonly IReadOnlyList<string> SymbolicProductionColumns = [
        "level_id",
        "level_symbol",
        "width",
        "height",
        "blocked_cells",
        "missile_blocked_cells",
        "default_start",
        "suppress_multiplayer_death_drops",
    ];

    /**
     * Rejects numeric-only level metadata at the current production boundary.
     * Procedural-only packs may omit levels.tsv; direct callers retain older
     * numeric and partial table compatibility.
     */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(SymbolicProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production level table '{table.SourcePath}' must use the complete ordered symbolic schema.");
        foreach (var row in table.Rows)
            foreach (var column in SymbolicProductionColumns)
                _ = row.Required(column);
    }

    public void AddLevel(AuthoritativeLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (level.IsDefaultStart && levels.Values.Any(candidate => candidate.IsDefaultStart)) {
            throw new InvalidDataException(
                "Authoritative world content must not define more than one default starting level.");
        }
        if (!levels.TryAdd(level.LevelId, level))
            throw new InvalidDataException($"Level {level.LevelId} is registered more than once.");
    }

    /** Replaces an already registered level while retaining world identity invariants. */
    public void ReplaceLevel(AuthoritativeLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!levels.ContainsKey(level.LevelId))
            throw new InvalidDataException($"Level {level.LevelId} is not registered.");
        if (level.IsDefaultStart && levels.Values.Any(candidate =>
                candidate.LevelId != level.LevelId && candidate.IsDefaultStart)) {
            throw new InvalidDataException(
                "Authoritative world content must not define more than one default starting level.");
        }
        levels[level.LevelId] = level;
    }

    public bool IsWalkable(uint levelId, int positionX, int positionY)
    {
        return levels.TryGetValue(levelId, out var level) && level.IsWalkable(positionX, positionY);
    }

    public bool ContainsPosition(uint levelId, int positionX, int positionY)
    {
        return levels.TryGetValue(levelId, out var level)
            && level.ContainsPosition(positionX, positionY);
    }

    public bool IsMissileBlocked(uint levelId, int positionX, int positionY)
    {
        return !levels.TryGetValue(levelId, out var level)
            || level.IsMissileBlocked(positionX, positionY);
    }

    public bool ContainsLevel(uint levelId) => levels.ContainsKey(levelId);

    /** Returns the validated content-owned entry point for a newly created participant. */
    public (int X, int Y) GetStartPosition(uint levelId)
    {
        if (!levels.TryGetValue(levelId, out var level))
            throw new InvalidDataException($"Level {levelId} is not registered.");
        return (level.StartPositionX, level.StartPositionY);
    }

    public bool SuppressMultiplayerDeathDrops(uint levelId)
    {
        return levels.TryGetValue(levelId, out var level)
            && level.SuppressMultiplayerDeathDrops;
    }

    public IReadOnlyList<AuthoritativeLevel> Levels => levels.Values.OrderBy(level => level.LevelId).ToArray();

    public IReadOnlyList<AuthoritativeWarpTarget> WarpTargets => warpTargets.Values
        .OrderBy(target => target.WarpId)
        .ToArray();

    /**
     * Captures detached backing levels and warps in deterministic identity
     * order. The caller owns synchronization; this method does not promise
     * thread-safe multi-swap behavior.
     */
    public DiabloWorldGeometryState CaptureGeometryState()
    {
        if (levels.Values.Any(level => level.StaticObjectBlockedCells.Count != 0))
            throw new InvalidDataException("Live static-object collision must not be stored as backing geometry.");
        return new DiabloWorldGeometryState(
            levels.Values
                .OrderBy(level => level.LevelId)
                .Select(level => new DiabloWorldGeometryLevelState(
                    level.LevelId,
                    level.StableSymbol,
                    level.Width,
                    level.Height,
                    level.BlockedCells.ToArray(),
                    level.MissileBlockedCells.ToArray(),
                    level.NativeTransparency.ToArray(),
                    level.NativeObjectLayer.ToArray(),
                    level.NativeMegaTileLayer.ToArray(),
                    level.NativePieceLayer.ToArray(),
                    level.NativePieceProperties
                        .OrderBy(pair => pair.Key)
                        .ToDictionary(pair => pair.Key, pair => pair.Value),
                    level.NativePieceCollisionCells.ToArray(),
                    level.NativeVisionBlockedCells.ToArray(),
                    level.HasExplicitNativeVisionBlockedCells,
                    level.NativePieceMissileBlockedCells.ToArray(),
                    level.NativeMonsterLayer.ToArray(),
                    level.IsDefaultStart,
                    level.SuppressMultiplayerDeathDrops,
                    level.StartPositionX,
                    level.StartPositionY))
                .ToArray(),
            warpTargets.Values
                .OrderBy(target => target.WarpId)
                .Select(target => new DiabloWorldGeometryWarpState(
                    target.WarpId,
                    target.SourceLevelId,
                    target.TriggerX,
                    target.TriggerY,
                    target.TargetLevelId,
                    target.TargetX,
                    target.TargetY,
                    target.RequiredQuestId,
                    target.RequiredQuestCompleted,
                    target.TargetSetLevelId,
                    target.TargetSetLevelActiveMap,
                    target.TargetSetLevelSymbol,
                    target.StableSymbol))
                .ToArray());
    }

    /**
     * Validates and restores complete backing geometry through a temporary
     * world, swapping dictionaries only after every level and warp succeeds.
     * The caller owns synchronization; this method does not promise thread-safe
     * multi-swap behavior.
     */
    public void RestoreGeometryState(DiabloWorldGeometryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var savedLevels = RequireCollection(state.Levels, "levels");
        var savedWarps = RequireCollection(state.WarpTargets, "warp targets");
        var candidate = new AuthoritativeWorld();

        foreach (var savedLevel in savedLevels) {
            if (savedLevel is null)
                throw new InvalidDataException("The geometry checkpoint contains a null level.");
            var blockedCells = RequireCollection(
                savedLevel.BlockedCells,
                $"level {savedLevel.LevelId} blocked cells");
            var missileBlockedCells = RequireCollection(
                savedLevel.MissileBlockedCells,
                $"level {savedLevel.LevelId} missile-blocked cells");
            var nativeTransparency = RequireCollection(
                savedLevel.NativeTransparency,
                $"level {savedLevel.LevelId} transparency");
            var nativeObjectLayer = RequireCollection(
                savedLevel.NativeObjectLayer,
                $"level {savedLevel.LevelId} object layer");
            var nativeMegaTileLayer = RequireCollection(
                savedLevel.NativeMegaTileLayer,
                $"level {savedLevel.LevelId} mega-tile layer");
            var nativePieceLayer = RequireCollection(
                savedLevel.NativePieceLayer,
                $"level {savedLevel.LevelId} piece layer");
            var nativePieceProperties = RequireMap(
                savedLevel.NativePieceProperties,
                $"level {savedLevel.LevelId} piece properties");
            var nativePieceCollisionCells = RequireCollection(
                savedLevel.NativePieceCollisionCells,
                $"level {savedLevel.LevelId} piece collision cells");
            var nativeVisionBlockedCells = RequireCollection(
                savedLevel.NativeVisionBlockedCells,
                $"level {savedLevel.LevelId} vision cells");
            var nativePieceMissileBlockedCells = RequireCollection(
                savedLevel.NativePieceMissileBlockedCells,
                $"level {savedLevel.LevelId} piece missile cells");
            var nativeMonsterLayer = RequireCollection(
                savedLevel.NativeMonsterLayer,
                $"level {savedLevel.LevelId} monster layer");

            var restoredLevel = new AuthoritativeLevel(
                savedLevel.LevelId,
                savedLevel.Width,
                savedLevel.Height,
                blockedCells,
                missileBlockedCells,
                savedLevel.IsDefaultStart,
                savedLevel.SuppressMultiplayerDeathDrops,
                nativeTransparency,
                nativeObjectLayer,
                nativeMegaTileLayer,
                nativePieceLayer,
                nativePieceProperties,
                nativePieceCollisionCells,
                nativePieceMissileBlockedCells,
                nativeMonsterLayer,
                savedLevel.StartPositionX,
                savedLevel.StartPositionY,
                savedLevel.HasExplicitNativeVisionBlockedCells
                    ? nativeVisionBlockedCells
                    : null) {
                StableSymbol = savedLevel.StableSymbol,
            };
            if (restoredLevel.HasExplicitNativeVisionBlockedCells
                    != savedLevel.HasExplicitNativeVisionBlockedCells
                || !restoredLevel.NativeVisionBlockedCells.SequenceEqual(nativeVisionBlockedCells)) {
                throw new InvalidDataException(
                    $"Level {savedLevel.LevelId} does not preserve its native vision geometry.");
            }
            candidate.AddLevel(restoredLevel);
        }

        foreach (var savedWarp in savedWarps) {
            if (savedWarp is null)
                throw new InvalidDataException("The geometry checkpoint contains a null warp.");
            var restoredWarp = new AuthoritativeWarpTarget(
                savedWarp.WarpId,
                savedWarp.SourceLevelId,
                savedWarp.TriggerX,
                savedWarp.TriggerY,
                savedWarp.TargetLevelId,
                savedWarp.TargetX,
                savedWarp.TargetY,
                savedWarp.RequiredQuestId,
                savedWarp.RequiredQuestCompleted,
                savedWarp.TargetSetLevelId,
                savedWarp.TargetSetLevelActiveMap) {
                TargetSetLevelSymbol = savedWarp.TargetSetLevelSymbol,
                StableSymbol = savedWarp.StableSymbol,
            };
            // ReplaceLevel historically permits a registered warp endpoint to
            // become blocked. Preserve that legal backing state during restore;
            // new public registrations remain walkability-gated below.
            candidate.AddWarpTargetCore(restoredWarp, requireWalkableEndpoints: false);
        }

        levels = candidate.levels;
        warpTargets = candidate.warpTargets;
    }

    private static IReadOnlyList<T> RequireCollection<T>(
        IReadOnlyList<T>? values,
        string description)
    {
        return values ?? throw new InvalidDataException(
            $"The geometry checkpoint contains null {description}.");
    }

    private static IReadOnlyDictionary<TKey, TValue> RequireMap<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue>? values,
        string description)
        where TKey : notnull
    {
        return values ?? throw new InvalidDataException(
            $"The geometry checkpoint contains null {description}.");
    }

    public void AddWarpTarget(AuthoritativeWarpTarget target)
        => AddWarpTargetCore(target, requireWalkableEndpoints: true);

    private void AddWarpTargetCore(
        AuthoritativeWarpTarget target,
        bool requireWalkableEndpoints)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!levels.ContainsKey(target.SourceLevelId)
            || !levels.ContainsKey(target.TargetLevelId)) {
            throw new InvalidDataException(
                $"Warp {target.WarpId} references an unknown source or target level.");
        }
        if (!ContainsPosition(target.SourceLevelId, target.TriggerX, target.TriggerY)
            || !ContainsPosition(target.TargetLevelId, target.TargetX, target.TargetY)) {
            throw new InvalidDataException(
                $"Warp {target.WarpId} contains a position outside its level.");
        }
        if (requireWalkableEndpoints
            && (!IsWalkable(target.SourceLevelId, target.TriggerX, target.TriggerY)
                || !IsWalkable(target.TargetLevelId, target.TargetX, target.TargetY))) {
            throw new InvalidDataException(
                $"Warp {target.WarpId} contains a blocked endpoint.");
        }
        if (warpTargets.Values.Any(existing =>
                existing.SourceLevelId == target.SourceLevelId
                && existing.TriggerX == target.TriggerX
                && existing.TriggerY == target.TriggerY)) {
            throw new InvalidDataException(
                $"Warp {target.WarpId} duplicates an existing source trigger on level {target.SourceLevelId}.");
        }
        if (!warpTargets.TryAdd(target.WarpId, target))
            throw new InvalidDataException($"Warp {target.WarpId} is registered more than once.");
    }

    public AuthoritativeWarpTarget? FindNearestWarpTarget(
        uint sourceLevelId,
        int positionX,
        int positionY,
        Func<AuthoritativeWarpTarget, bool>? isAvailable = null)
    {
        return warpTargets.Values
            .Where(target => target.SourceLevelId == sourceLevelId
                && (isAvailable is null || isAvailable(target)))
            .OrderBy(target => {
                var deltaX = (long)target.TriggerX - positionX;
                var deltaY = (long)target.TriggerY - positionY;
                return checked(deltaX * deltaX + deltaY * deltaY);
            })
            .ThenBy(target => target.WarpId)
            .FirstOrDefault();
    }

    public bool IsWarpTrigger(uint levelId, int positionX, int positionY)
    {
        return warpTargets.Values.Any(target =>
            target.SourceLevelId == levelId
            && target.TriggerX == positionX
            && target.TriggerY == positionY);
    }

    public uint? DefaultStartingLevelId => levels.Values
        .Where(level => level.IsDefaultStart)
        .Select(level => (uint?)level.LevelId)
        .SingleOrDefault();

    public static AuthoritativeWorld LoadTsv(string sourcePath, string contents)
        => LoadTsv(sourcePath, contents, stableIds: null);

    public static AuthoritativeWorld LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var world = new AuthoritativeWorld();
        foreach (var row in table.Rows) {
            var blocked = ParseCells(row, "blocked_cells");
            var missileBlocked = row.TryGet("missile_blocked_cells", out _)
                ? ParseCells(row, "missile_blocked_cells")
                : null;
            var level = new AuthoritativeLevel(
                AuthoritativeLevelIdentity.Resolve(row, stableIds),
                row.RequiredInt32("width"),
                row.RequiredInt32("height"),
                blocked,
                missileBlocked,
                row.OptionalBoolean("default_start"),
                row.OptionalBoolean("suppress_multiplayer_death_drops"),
                startPositionX: row.TryGet("start_x", out _)
                    ? row.RequiredInt32("start_x")
                    : 0,
                startPositionY: row.TryGet("start_y", out _)
                    ? row.RequiredInt32("start_y")
                    : 0) {
                StableSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
            };
            if (row.TryGet("start_x", out _) || row.TryGet("start_y", out _))
                level.ValidateStartPosition();
            world.AddLevel(level);
        }
        return world;
    }

    private static IReadOnlyList<int> ParseCells(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return [];
        }
        return value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(cell => int.TryParse(
                    cell,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var index)
                ? index
                : throw new InvalidDataException(
                    $"Level {row.Required("level_id")} contains an invalid {column} cell '{cell}'."))
            .ToArray();
    }
}

public sealed class AuthoritativeLevel
{
    /** Native compatibility fine-grid dimensions, including the town border. */
    public const int NativeMaximumWidth = 112;
    public const int NativeMaximumHeight = 112;

    /** Native coarse mega-tile dimensions when an additive layer is supplied. */
    public const int NativeMaximumMegaWidth = 40;
    public const int NativeMaximumMegaHeight = 40;

    /** Highest native object identity used by authoritative object presentation. */
    public const int NativeMaximumObjectId = 108;

    /** Highest native monster-table identity accepted by DevilutionX's 200-entry table. */
    public const int NativeMaximumMonsterTypeId = 199;

    /** Native active-monster pool capacity for one authoritative level. */
    public const int NativeMaximumMonsterCount = NativeMaximumMonsterTypeId + 1;

    /** Native active-object pool capacity for one authoritative level (MAXOBJECTS). */
    public const int NativeMaximumObjectCount = 127;

    /** Native active-world-item pool capacity for one authoritative level (MAXITEMS). */
    public const int NativeMaximumItemCount = 127;

    /**
     * Highest raw object-layer index accepted by the native ObjTypeConv table.
     * This is distinct from NativeMaximumObjectId: DUN layers store conversion
     * indices, while object snapshots store the resulting _object_id value.
     */
    public const int NativeMaximumDunObjectLayerId = 146;

    /** Highest raw monster-layer index accepted by the native MonstConvTbl. */
    public const int NativeMaximumDunMonsterLayerId = 157;

    private readonly HashSet<int> blockedCells;
    private readonly HashSet<int> missileBlockedCells;
    private readonly IReadOnlyList<int> blockedCellList;
    private readonly IReadOnlyList<int> missileBlockedCellList;
    private readonly IReadOnlyList<int> nativeVisionBlockedCellList;

    /** Retains the original CLR constructor for previously compiled callers. */
    public AuthoritativeLevel(uint levelId, int width, int height,
        IEnumerable<int>? blockedCells, IEnumerable<int>? missileBlockedCells,
        bool isDefaultStart, bool suppressMultiplayerDeathDrops,
        IEnumerable<ushort>? nativeTransparency, IEnumerable<ushort>? nativeObjectLayer,
        IEnumerable<ushort>? nativeMegaTileLayer, IEnumerable<ushort>? nativePieceLayer,
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? nativePieceProperties,
        IEnumerable<int>? nativePieceCollisionCells, IEnumerable<int>? nativePieceMissileBlockedCells,
        IEnumerable<ushort>? nativeMonsterLayer, int startPositionX, int startPositionY,
        IEnumerable<int>? nativeVisionBlockedCells)
        : this(levelId, width, height, blockedCells, missileBlockedCells, isDefaultStart,
            suppressMultiplayerDeathDrops, nativeTransparency, nativeObjectLayer, nativeMegaTileLayer,
            nativePieceLayer, nativePieceProperties, nativePieceCollisionCells, nativePieceMissileBlockedCells,
            nativeMonsterLayer, startPositionX, startPositionY, nativeVisionBlockedCells, null, null)
    {
    }

    public AuthoritativeLevel(
        uint levelId,
        int width,
        int height,
        IEnumerable<int>? blockedCells = null,
        IEnumerable<int>? missileBlockedCells = null,
        bool isDefaultStart = false,
        bool suppressMultiplayerDeathDrops = false,
        IEnumerable<ushort>? nativeTransparency = null,
        IEnumerable<ushort>? nativeObjectLayer = null,
        IEnumerable<ushort>? nativeMegaTileLayer = null,
        IEnumerable<ushort>? nativePieceLayer = null,
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>? nativePieceProperties = null,
        IEnumerable<int>? nativePieceCollisionCells = null,
        IEnumerable<int>? nativePieceMissileBlockedCells = null,
        IEnumerable<ushort>? nativeMonsterLayer = null,
        int startPositionX = 0,
        int startPositionY = 0,
        IEnumerable<int>? nativeVisionBlockedCells = null,
        IEnumerable<ushort>? nativeSpecialLayer = null,
        IEnumerable<int>? staticObjectBlockedCells = null)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Level dimensions must be positive.");
        if (width > NativeMaximumWidth || height > NativeMaximumHeight)
            throw new InvalidDataException(
                $"Level {levelId} exceeds the native fine-grid dimensions.");
        LevelId = levelId;
        Width = width;
        Height = height;
        IsDefaultStart = isDefaultStart;
        SuppressMultiplayerDeathDrops = suppressMultiplayerDeathDrops;
        StartPositionX = startPositionX;
        StartPositionY = startPositionY;
        HasExplicitNativeVisionBlockedCells = nativeVisionBlockedCells is not null;
        this.blockedCells = (blockedCells ?? []).ToHashSet();
        if (this.blockedCells.Any(cell => cell < 0 || cell >= width * height))
            throw new InvalidDataException($"Level {levelId} contains an out-of-range blocked cell.");
        this.missileBlockedCells = (missileBlockedCells ?? this.blockedCells).ToHashSet();
        if (this.missileBlockedCells.Any(cell => cell < 0 || cell >= width * height)) {
            throw new InvalidDataException(
                $"Level {levelId} contains an out-of-range missile-blocked cell.");
        }
        blockedCellList = Array.AsReadOnly(this.blockedCells.OrderBy(cell => cell).ToArray());
        var objectCells = (staticObjectBlockedCells ?? []).Distinct().Order().ToArray();
        if (objectCells.Any(cell => cell < 0 || cell >= width * height))
            throw new InvalidDataException($"Level {levelId} contains an out-of-range static object cell.");
        StaticObjectBlockedCells = Array.AsReadOnly(objectCells);
        missileBlockedCellList = Array.AsReadOnly(
            this.missileBlockedCells.OrderBy(cell => cell).ToArray());
        var transparency = (nativeTransparency ?? [])
            .ToArray();
        if (transparency.Length != 0 && transparency.Length != width * height)
            throw new InvalidDataException(
                $"Level {levelId} contains a native transparency layer with an invalid length.");
        if (transparency.Any(value => value > byte.MaxValue))
            throw new InvalidDataException(
                $"Level {levelId} contains a native transparency wire byte outside the compatibility range.");
        NativeTransparency = Array.AsReadOnly(transparency);
        var monsters = (nativeMonsterLayer ?? [])
            .ToArray();
        if (monsters.Length != 0 && monsters.Length != width * height)
            throw new InvalidDataException(
                $"Level {levelId} contains a native monster layer with an invalid length.");
        if (monsters.Any(value => value > NativeMaximumDunMonsterLayerId))
            throw new InvalidDataException(
                $"Level {levelId} contains a native DUN monster-layer value outside the compatibility range.");
        NativeMonsterLayer = Array.AsReadOnly(monsters);
        var objects = (nativeObjectLayer ?? [])
            .ToArray();
        if (objects.Length != 0 && objects.Length != width * height)
            throw new InvalidDataException(
                $"Level {levelId} contains a native object layer with an invalid length.");
        if (objects.Any(value => value > NativeMaximumDunObjectLayerId))
            throw new InvalidDataException(
                $"Level {levelId} contains a native DUN object-layer value outside the compatibility range.");
        NativeObjectLayer = Array.AsReadOnly(objects);
        var megaTiles = (nativeMegaTileLayer ?? []).ToArray();
        var megaWidth = (width + 1) / 2;
        var megaHeight = (height + 1) / 2;
        if (megaTiles.Length != 0 && megaTiles.Length != megaWidth * megaHeight)
            throw new InvalidDataException(
                $"Level {levelId} contains a native mega-tile layer with an invalid length.");
        if (megaTiles.Any(value => value > byte.MaxValue))
            throw new InvalidDataException(
                $"Level {levelId} contains a native mega-tile value outside the compatibility range.");
        if (megaTiles.Length != 0
            && (megaWidth > NativeMaximumMegaWidth || megaHeight > NativeMaximumMegaHeight))
            throw new InvalidDataException(
                $"Level {levelId} exceeds the native coarse-grid dimensions.");
        NativeMegaTileLayer = Array.AsReadOnly(megaTiles);
        var pieces = (nativePieceLayer ?? []).ToArray();
        if (pieces.Length != 0 && pieces.Length != width * height)
            throw new InvalidDataException(
                $"Level {levelId} contains a native d-piece layer with an invalid length.");
        if (pieces.Any(value => value >= AuthoritativeDunTilePropertyCatalog.NativeTileCount))
            throw new InvalidDataException(
                $"Level {levelId} contains a native d-piece value outside the MAXTILES range.");
        NativePieceLayer = Array.AsReadOnly(pieces);
        var special = (nativeSpecialLayer ?? []).ToArray();
        if (special.Length != 0 && special.Length != width * height)
            throw new InvalidDataException(
                $"Level {levelId} contains a native special layer with an invalid length.");
        if (special.Any(value => value > byte.MaxValue))
            throw new InvalidDataException(
                $"Level {levelId} contains a native special value outside the compatibility byte range.");
        NativeSpecialLayer = Array.AsReadOnly(special);
        var pieceProperties = (nativePieceProperties ?? new Dictionary<ushort, AuthoritativeDunTileFlags>())
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (pieceProperties.Keys.Any(value => value >= AuthoritativeDunTilePropertyCatalog.NativeTileCount))
            throw new InvalidDataException(
                $"Level {levelId} contains native d-piece properties outside the MAXTILES range.");
        NativePieceProperties = new ReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>(pieceProperties);
        var resolvedNativeVisionBlockedCells = nativeVisionBlockedCells is null
            ? this.blockedCells.ToHashSet()
            : nativeVisionBlockedCells.ToHashSet();
        if (resolvedNativeVisionBlockedCells.Any(cell => cell < 0 || cell >= width * height))
            throw new InvalidDataException(
                $"Level {levelId} contains an out-of-range native vision-blocked cell.");
        // Movement blockers remain vision blockers even when an explicit
        // native layer is supplied; mutations and imported layers must not
        // create a protocol-inconsistent geometry projection.
        resolvedNativeVisionBlockedCells.UnionWith(this.blockedCells);
        for (var cell = 0; cell < pieces.Length; cell++) {
            if (pieces[cell] != 0
                && pieceProperties.TryGetValue(pieces[cell], out var flags)
                && flags.HasFlag(AuthoritativeDunTileFlags.BlockLight))
                resolvedNativeVisionBlockedCells.Add(cell);
        }
        nativeVisionBlockedCellList = Array.AsReadOnly(
            resolvedNativeVisionBlockedCells.OrderBy(cell => cell).ToArray());
        var pieceCollisionCells = (nativePieceCollisionCells ?? [])
            .Distinct()
            .OrderBy(cell => cell)
            .ToArray();
        if (pieceCollisionCells.Any(cell => cell < 0 || cell >= width * height))
            throw new InvalidDataException(
                $"Level {levelId} contains an out-of-range native d-piece collision cell.");
        NativePieceCollisionCells = Array.AsReadOnly(pieceCollisionCells);
        var pieceMissileBlockedCells = (nativePieceMissileBlockedCells ?? [])
            .Distinct()
            .OrderBy(cell => cell)
            .ToArray();
        if (pieceMissileBlockedCells.Any(cell => cell < 0 || cell >= width * height))
            throw new InvalidDataException(
                $"Level {levelId} contains an out-of-range native d-piece missile cell.");
        NativePieceMissileBlockedCells = Array.AsReadOnly(pieceMissileBlockedCells);
    }

    public uint LevelId { get; }

    /** Stable content symbol retained alongside the numeric compatibility ID. */
    public string StableSymbol { get; init; } = string.Empty;

    public int Width { get; }

    public int Height { get; }

    /** Stable row-major movement geometry for protocol and presentation projections. */
    public IReadOnlyList<int> BlockedCells => blockedCellList;

    /** Supplementary live object collision; independent of SOL, missiles and vision. */
    public IReadOnlyList<int> StaticObjectBlockedCells { get; }

    /** Stable row-major missile geometry for protocol and presentation projections. */
    public IReadOnlyList<int> MissileBlockedCells => missileBlockedCellList;

    /** Native d-piece transparency values retained as signed-byte wire values. */
    public IReadOnlyList<ushort> NativeTransparency { get; }

    /** Raw native DUN monster-layer values retained for authoritative set-piece materialization. */
    public IReadOnlyList<ushort> NativeMonsterLayer { get; }

    /** Native DUN object-layer values retained for explicit content mapping. */
    public IReadOnlyList<ushort> NativeObjectLayer { get; }

    /** Native DUN mega-tile IDs retained for external set-piece presentation. */
    public IReadOnlyList<ushort> NativeMegaTileLayer { get; }

    /** Effective native DUN d-piece IDs retained for authoritative set-piece mutations. */
    public IReadOnlyList<ushort> NativePieceLayer { get; }

    /** Native arch/door special values; absent on legacy geometry. */
    public IReadOnlyList<ushort> NativeSpecialLayer { get; }

    /** Native d-piece flags retained for collision-aware authoritative mutations. */
    public IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags> NativePieceProperties { get; }

    /** Cells whose blocked state was materialized from native d-piece flags. */
    public IReadOnlyList<int> NativePieceCollisionCells { get; }

    /** Native vision blockers; movement-blocked cells are the legacy fallback. */
    public IReadOnlyList<int> NativeVisionBlockedCells => nativeVisionBlockedCellList;

    /** Whether native vision blockers were supplied independently of movement collision. */
    public bool HasExplicitNativeVisionBlockedCells { get; }

    /** Cells whose missile state was materialized from native d-piece flags. */
    public IReadOnlyList<int> NativePieceMissileBlockedCells { get; }

    public bool IsDefaultStart { get; }

    /** Native level-16/arena rule suppressing death penalties only in multiplayer. */
    public bool SuppressMultiplayerDeathDrops { get; }

    /** Content-defined entry point for a newly created participant on this level. */
    public int StartPositionX { get; }

    /** Content-defined entry point for a newly created participant on this level. */
    public int StartPositionY { get; }

    /** Validates the content-owned entry point without affecting legacy in-memory fixtures. */
    public void ValidateStartPosition()
    {
        if (!ContainsPosition(StartPositionX, StartPositionY)
            || !IsWalkable(StartPositionX, StartPositionY)) {
            throw new InvalidDataException(
                $"Level {LevelId} contains an invalid start position ({StartPositionX}, {StartPositionY}).");
        }
    }

    /** Creates an immutable entry-point variant without dropping authored geometry. */
    public AuthoritativeLevel WithStartPosition(int startPositionX, int startPositionY)
    {
        var level = new AuthoritativeLevel(
            LevelId,
            Width,
            Height,
            BlockedCells,
            MissileBlockedCells,
            IsDefaultStart,
            SuppressMultiplayerDeathDrops,
            NativeTransparency,
            NativeObjectLayer,
            NativeMegaTileLayer,
            NativePieceLayer,
            NativePieceProperties,
            NativePieceCollisionCells,
            NativePieceMissileBlockedCells,
            NativeMonsterLayer,
            startPositionX,
            startPositionY,
            HasExplicitNativeVisionBlockedCells
                ? NativeVisionBlockedCells
                : null,
            NativeSpecialLayer,
            StaticObjectBlockedCells) {
            StableSymbol = StableSymbol,
        };
        level.ValidateStartPosition();
        return level;
    }

    public bool ContainsPosition(int positionX, int positionY)
    {
        return positionX >= 0 && positionX < Width
            && positionY >= 0 && positionY < Height;
    }

    public bool IsWalkable(int positionX, int positionY)
    {
        return ContainsPosition(positionX, positionY)
            && !blockedCells.Contains(positionY * Width + positionX);
    }

    public bool IsMissileBlocked(int positionX, int positionY)
    {
        return !ContainsPosition(positionX, positionY)
            || missileBlockedCells.Contains(positionY * Width + positionX);
    }

    public bool HasLineOfSight(int startX, int startY, int endX, int endY)
    {
        // Network-originated targets must never turn this bounded grid walk into
        // an overflowing or unbounded traversal. Valid callers already provide
        // level-contained endpoints; reject anything else before arithmetic.
        if (!ContainsPosition(startX, startY) || !ContainsPosition(endX, endY))
            return false;

        return DeterministicGridLineTraversal.HasLineOfSight(
            startX,
            startY,
            endX,
            endY,
            (x, y) => !IsWalkable(x, y));
    }

    /** Native missiles::CheckBlock traversal over Solid cells, including its endpoint. */
    public bool HasSolidLineOfSight(int startX, int startY, int endX, int endY)
    {
        if (!ContainsPosition(startX, startY)
            || !ContainsPosition(endX, endY)) {
            return false;
        }

        var positionX = startX;
        var positionY = startY;
        for (var step = 0; step <= NativeMaximumWidth + NativeMaximumHeight; step++) {
            if (positionX == endX && positionY == endY)
                return true;

            var direction = DeterministicGridDirections.Toward(
                positionX,
                positionY,
                endX,
                endY);
            var displacement = DeterministicGridDirections.GetDisplacement(direction);
            positionX = checked(positionX + displacement.X);
            positionY = checked(positionY + displacement.Y);
            if (!IsWalkable(positionX, positionY))
                return false;
        }

        return false;
    }

    /** Native DoVision-style line check, including diagonal corner blocking. */
    public bool HasNativeVisionLineOfSight(int startX, int startY, int endX, int endY)
        => HasNativeVisionLineOfSight(
            startX,
            startY,
            endX,
            endY,
            AuthoritativeEquipmentLightRules.NativeMaximumLightRadius);

    /** Native DoVision-style line check using the viewer's equipment light radius. */
    public bool HasNativeVisionLineOfSight(
        int startX,
        int startY,
        int endX,
        int endY,
        int visionRadius)
        => NativeVision.IsVisible(
            startX,
            startY,
            endX,
            endY,
            visionRadius is >= AuthoritativeEquipmentLightRules.NativeMinimumLightRadius
                and <= AuthoritativeEquipmentLightRules.NativeMaximumLightRadius
                ? visionRadius
                : AuthoritativeEquipmentLightRules.NativeDefaultLightRadius,
            PassesNativeVision,
            ContainsPosition);

    private bool PassesNativeVision(int positionX, int positionY)
    {
        if (!ContainsPosition(positionX, positionY))
            return false;
        var cell = positionY * Width + positionX;
        if (nativeVisionBlockedCellList.Contains(cell))
            return false;
        if (NativePieceLayer.Count != 0
            && NativePieceProperties.TryGetValue(
                NativePieceLayer[cell],
                out var pieceFlags)
            && pieceFlags.HasFlag(AuthoritativeDunTileFlags.BlockLight))
            return false;
        return IsWalkable(positionX, positionY);
    }

    /** Native monster placement room check; absent transparency retains legacy behavior. */
    public bool IsSameTransparencyRegion(int startX, int startY, int endX, int endY)
    {
        if (!ContainsPosition(startX, startY) || !ContainsPosition(endX, endY))
            return false;
        if (NativeTransparency.Count == 0)
            return true;
        return NativeTransparency[startY * Width + startX]
            == NativeTransparency[endY * Width + endX];
    }
}
