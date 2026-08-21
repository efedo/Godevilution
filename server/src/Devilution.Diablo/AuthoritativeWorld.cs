namespace Devilution.Diablo;

using Devilution.Data;
using System.Globalization;

/** Immutable level geometry used by authoritative movement and transitions. */
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
            .OrderBy(target => checked(
                (long)(target.TriggerX - positionX) * (target.TriggerX - positionX)
                + (long)(target.TriggerY - positionY) * (target.TriggerY - positionY)))
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
    private readonly HashSet<int> blockedCells;
    private readonly HashSet<int> missileBlockedCells;
    private readonly IReadOnlyList<int> blockedCellList;
    private readonly IReadOnlyList<int> missileBlockedCellList;

    public AuthoritativeLevel(
        uint levelId,
        int width,
        int height,
        IEnumerable<int>? blockedCells = null,
        IEnumerable<int>? missileBlockedCells = null,
        bool isDefaultStart = false,
        bool suppressMultiplayerDeathDrops = false,
        IEnumerable<ushort>? nativeTransparency = null,
        IEnumerable<ushort>? nativeObjectLayer = null)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Level dimensions must be positive.");
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
        NativeTransparency = Array.AsReadOnly(transparency);
        var objects = (nativeObjectLayer ?? [])
            .ToArray();
        if (objects.Length != 0 && objects.Length != width * height)
            throw new InvalidDataException(
                $"Level {levelId} contains a native object layer with an invalid length.");
        NativeObjectLayer = Array.AsReadOnly(objects);
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

    /** Native d-piece transparency values retained separately from collision. */
    public IReadOnlyList<ushort> NativeTransparency { get; }

    /** Native DUN object-layer values retained for explicit content mapping. */
    public IReadOnlyList<ushort> NativeObjectLayer { get; }

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
        var x = startX;
        var y = startY;
        var deltaX = Math.Abs(endX - startX);
        var deltaY = Math.Abs(endY - startY);
        var stepX = startX < endX ? 1 : -1;
        var stepY = startY < endY ? 1 : -1;
        var error = deltaX - deltaY;

        while (true) {
            if ((x != startX || y != startY) && (x != endX || y != endY) && !IsWalkable(x, y))
                return false;
            if (x == endX && y == endY)
                return true;

            var doubledError = 2 * error;
            if (doubledError > -deltaY) {
                error -= deltaY;
                x += stepX;
            }
            if (doubledError < deltaX) {
                error += deltaX;
                y += stepY;
            }
        }
    }
}
