namespace Devilution.Diablo;

using Devilution.Data;
using System.Globalization;

/** Immutable level geometry used by authoritative movement and transitions. */
public sealed class AuthoritativeWorld
{
    private readonly Dictionary<uint, AuthoritativeLevel> levels = new();

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

    public uint? DefaultStartingLevelId => levels.Values
        .Where(level => level.IsDefaultStart)
        .Select(level => (uint?)level.LevelId)
        .SingleOrDefault();

    public static AuthoritativeWorld LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var world = new AuthoritativeWorld();
        foreach (var row in table.Rows) {
            var blocked = ParseCells(row, "blocked_cells");
            var missileBlocked = row.TryGet("missile_blocked_cells", out _)
                ? ParseCells(row, "missile_blocked_cells")
                : null;
            world.AddLevel(new AuthoritativeLevel(
                row.RequiredUInt32("level_id"),
                row.RequiredInt32("width"),
                row.RequiredInt32("height"),
                blocked,
                missileBlocked,
                ParseBooleanFlag(row, "default_start"),
                ParseBooleanFlag(row, "suppress_multiplayer_death_drops")));
        }
        return world;
    }

    private static bool ParseBooleanFlag(TsvRow row, string column)
    {
        var value = row.OptionalInt32(column);
        if (value is not (0 or 1)) {
            throw new InvalidDataException(
                $"Level {row.Required("level_id")} has invalid {column} value {value}.");
        }
        return value == 1;
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

    public AuthoritativeLevel(
        uint levelId,
        int width,
        int height,
        IEnumerable<int>? blockedCells = null,
        IEnumerable<int>? missileBlockedCells = null,
        bool isDefaultStart = false,
        bool suppressMultiplayerDeathDrops = false)
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
    }

    public uint LevelId { get; }

    public int Width { get; }

    public int Height { get; }

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
