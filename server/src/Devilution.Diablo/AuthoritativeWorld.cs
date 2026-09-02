using Devilution.Engine;

namespace Devilution.Diablo;

using Devilution.Data;
using Devilution.Shared;
using System.Collections.ObjectModel;
using System.Globalization;

/** Validated level geometry used by authoritative movement and transitions. */
public sealed class AuthoritativeWorld
{
    private readonly Dictionary<uint, AuthoritativeLevel> levels = new();
    private readonly Dictionary<uint, AuthoritativeWarpTarget> warpTargets = new();

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

    public bool SuppressMultiplayerDeathDrops(uint levelId)
    {
        return levels.TryGetValue(levelId, out var level)
            && level.SuppressMultiplayerDeathDrops;
    }

    public IReadOnlyList<AuthoritativeLevel> Levels => levels.Values.OrderBy(level => level.LevelId).ToArray();

    public IReadOnlyList<AuthoritativeWarpTarget> WarpTargets => warpTargets.Values
        .OrderBy(target => target.WarpId)
        .ToArray();

    public void AddWarpTarget(AuthoritativeWarpTarget target)
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
        if (!IsWalkable(target.SourceLevelId, target.TriggerX, target.TriggerY)
            || !IsWalkable(target.TargetLevelId, target.TargetX, target.TargetY)) {
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
                row.OptionalBoolean("suppress_multiplayer_death_drops")) {
                StableSymbol = AuthoritativeLevelIdentity.ReadSymbol(row),
            };
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
        IEnumerable<ushort>? nativeMonsterLayer = null)
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
        this.blockedCells = (blockedCells ?? []).ToHashSet();
        if (this.blockedCells.Any(cell => cell < 0 || cell >= width * height))
            throw new InvalidDataException($"Level {levelId} contains an out-of-range blocked cell.");
        this.missileBlockedCells = (missileBlockedCells ?? this.blockedCells).ToHashSet();
        if (this.missileBlockedCells.Any(cell => cell < 0 || cell >= width * height)) {
            throw new InvalidDataException(
                $"Level {levelId} contains an out-of-range missile-blocked cell.");
        }
        blockedCellList = Array.AsReadOnly(this.blockedCells.OrderBy(cell => cell).ToArray());
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
        var pieceProperties = (nativePieceProperties ?? new Dictionary<ushort, AuthoritativeDunTileFlags>())
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (pieceProperties.Keys.Any(value => value >= AuthoritativeDunTilePropertyCatalog.NativeTileCount))
            throw new InvalidDataException(
                $"Level {levelId} contains native d-piece properties outside the MAXTILES range.");
        NativePieceProperties = new ReadOnlyDictionary<ushort, AuthoritativeDunTileFlags>(pieceProperties);
        var nativeVisionBlockedCells = this.blockedCells.ToHashSet();
        for (var cell = 0; cell < pieces.Length; cell++) {
            if (pieces[cell] != 0
                && pieceProperties.TryGetValue(pieces[cell], out var flags)
                && flags.HasFlag(AuthoritativeDunTileFlags.BlockLight))
                nativeVisionBlockedCells.Add(cell);
        }
        nativeVisionBlockedCellList = Array.AsReadOnly(
            nativeVisionBlockedCells.OrderBy(cell => cell).ToArray());
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

    /** Native d-piece flags retained for collision-aware authoritative mutations. */
    public IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags> NativePieceProperties { get; }

    /** Cells whose blocked state was materialized from native d-piece flags. */
    public IReadOnlyList<int> NativePieceCollisionCells { get; }

    /** Derived native vision blockers, including movement-blocked cells. */
    public IReadOnlyList<int> NativeVisionBlockedCells => nativeVisionBlockedCellList;

    /** Cells whose missile state was materialized from native d-piece flags. */
    public IReadOnlyList<int> NativePieceMissileBlockedCells { get; }

    public bool IsDefaultStart { get; }

    /** Native level-16/arena rule suppressing death penalties only in multiplayer. */
    public bool SuppressMultiplayerDeathDrops { get; }

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

    /** Native DoVision-style line check, including diagonal corner blocking. */
    public bool HasNativeVisionLineOfSight(int startX, int startY, int endX, int endY)
        => NativeVision.IsVisible(
            startX,
            startY,
            endX,
            endY,
            15,
            PassesNativeVision,
            ContainsPosition);

    private bool PassesNativeVision(int positionX, int positionY)
    {
        if (!ContainsPosition(positionX, positionY))
            return false;
        var cell = positionY * Width + positionX;
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
