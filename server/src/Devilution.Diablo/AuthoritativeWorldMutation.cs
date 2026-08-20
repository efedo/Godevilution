using System.Globalization;
using Devilution.Data;

namespace Devilution.Diablo;

/** One rectangle of geometry cells authored for a world interaction. */
public sealed class AuthoritativeWorldMutationRectangle
{
    public AuthoritativeWorldMutationRectangle(int x, int y, int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public IEnumerable<int> EnumerateCells(int levelWidth, int levelHeight)
    {
        if (levelWidth <= 0 || levelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelWidth));
        var endX = checked(X + Width);
        var endY = checked(Y + Height);
        if (X < 0 || Y < 0 || endX > levelWidth || endY > levelHeight)
            throw new InvalidDataException("A world mutation rectangle exceeds its level bounds.");
        for (var y = Y; y < endY; y++)
            for (var x = X; x < endX; x++)
                yield return checked(y * levelWidth + x);
    }
}

/** One deterministic cell overlay applied by an authored world interaction. */
public sealed class AuthoritativeWorldMutation
{
    public AuthoritativeWorldMutation(
        uint mutationId,
        uint levelId,
        IEnumerable<int>? blockedCellsAdd = null,
        IEnumerable<int>? blockedCellsRemove = null,
        IEnumerable<int>? missileBlockedCellsAdd = null,
        IEnumerable<int>? missileBlockedCellsRemove = null,
        IEnumerable<AuthoritativeWorldMutationRectangle>? geometryRectanglesAdd = null,
        IEnumerable<AuthoritativeWorldMutationRectangle>? geometryRectanglesRemove = null)
    {
        if (mutationId == 0)
            throw new ArgumentOutOfRangeException(nameof(mutationId));
        MutationId = mutationId;
        LevelId = levelId;
        GeometryRectanglesAdd = NormalizeRectangles(geometryRectanglesAdd);
        GeometryRectanglesRemove = NormalizeRectangles(geometryRectanglesRemove);
        BlockedCellsAdd = Normalize(blockedCellsAdd);
        BlockedCellsRemove = Normalize(blockedCellsRemove);
        MissileBlockedCellsAdd = Normalize(missileBlockedCellsAdd);
        MissileBlockedCellsRemove = Normalize(missileBlockedCellsRemove);
        if (BlockedCellsAdd.Any(BlockedCellsRemoveOverlap)
            || MissileBlockedCellsAdd.Any(MissileBlockedCellsRemoveOverlap)) {
            throw new InvalidDataException(
                "A world mutation cannot add and remove the same geometry cell.");
        }

        bool BlockedCellsRemoveOverlap(int cell)
            => BlockedCellsRemove.Contains(cell);

        bool MissileBlockedCellsRemoveOverlap(int cell)
            => MissileBlockedCellsRemove.Contains(cell);
    }

    public uint MutationId { get; }

    public uint LevelId { get; }

    public IReadOnlyList<int> BlockedCellsAdd { get; }

    public IReadOnlyList<int> BlockedCellsRemove { get; }

    public IReadOnlyList<int> MissileBlockedCellsAdd { get; }

    public IReadOnlyList<int> MissileBlockedCellsRemove { get; }

    /** Rectangles that add both walkable and missile-blocking geometry. */
    public IReadOnlyList<AuthoritativeWorldMutationRectangle> GeometryRectanglesAdd { get; }

    /** Rectangles that remove both walkable and missile-blocking geometry. */
    public IReadOnlyList<AuthoritativeWorldMutationRectangle> GeometryRectanglesRemove { get; }

    public IReadOnlyList<int> GetBlockedCells(int levelWidth, int levelHeight)
        => MergeCells(BlockedCellsAdd, GeometryRectanglesAdd, levelWidth, levelHeight);

    public IReadOnlyList<int> GetBlockedCellsToRemove(int levelWidth, int levelHeight)
        => MergeCells(BlockedCellsRemove, GeometryRectanglesRemove, levelWidth, levelHeight);

    public IReadOnlyList<int> GetMissileBlockedCells(int levelWidth, int levelHeight)
        => MergeCells(MissileBlockedCellsAdd, GeometryRectanglesAdd, levelWidth, levelHeight);

    public IReadOnlyList<int> GetMissileBlockedCellsToRemove(int levelWidth, int levelHeight)
        => MergeCells(MissileBlockedCellsRemove, GeometryRectanglesRemove, levelWidth, levelHeight);

    private static IReadOnlyList<int> Normalize(IEnumerable<int>? cells)
        => (cells ?? [])
            .Distinct()
            .OrderBy(cell => cell)
            .ToArray();

    private static IReadOnlyList<AuthoritativeWorldMutationRectangle> NormalizeRectangles(
        IEnumerable<AuthoritativeWorldMutationRectangle>? rectangles)
        => (rectangles ?? [])
            .Where(rectangle => rectangle is not null)
            .OrderBy(rectangle => rectangle.X)
            .ThenBy(rectangle => rectangle.Y)
            .ThenBy(rectangle => rectangle.Width)
            .ThenBy(rectangle => rectangle.Height)
            .ToArray();

    private static IReadOnlyList<int> MergeCells(
        IReadOnlyList<int> cells,
        IReadOnlyList<AuthoritativeWorldMutationRectangle> rectangles,
        int levelWidth,
        int levelHeight)
        => cells
            .Concat(rectangles.SelectMany(rectangle => rectangle.EnumerateCells(levelWidth, levelHeight)))
            .Distinct()
            .OrderBy(cell => cell)
            .ToArray();
}

/** Validated external world-cell mutations used by object interactions. */
public sealed class AuthoritativeWorldMutationCatalog
{
    private readonly IReadOnlyDictionary<uint, AuthoritativeWorldMutation> definitions;

    public AuthoritativeWorldMutationCatalog(IEnumerable<AuthoritativeWorldMutation> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.GroupBy(definition => definition.MutationId).Any(group => group.Count() != 1))
            throw new InvalidDataException("World mutations contain duplicate mutation IDs.");
        this.definitions = values.ToDictionary(definition => definition.MutationId);
    }

    public IReadOnlyList<AuthoritativeWorldMutation> Definitions
        => definitions.Values.OrderBy(definition => definition.MutationId).ToArray();

    public bool TryGet(uint mutationId, out AuthoritativeWorldMutation mutation)
        => definitions.TryGetValue(mutationId, out mutation!);

    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var mutation in definitions.Values) {
            if (!world.ContainsLevel(mutation.LevelId))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} references unknown level {mutation.LevelId}.");
            var level = world.Levels.Single(candidate => candidate.LevelId == mutation.LevelId);
            IReadOnlyList<int> blockedAdd;
            IReadOnlyList<int> blockedRemove;
            IReadOnlyList<int> missileBlockedAdd;
            IReadOnlyList<int> missileBlockedRemove;
            try {
                blockedAdd = mutation.GetBlockedCells(level.Width, level.Height);
                blockedRemove = mutation.GetBlockedCellsToRemove(level.Width, level.Height);
                missileBlockedAdd = mutation.GetMissileBlockedCells(level.Width, level.Height);
                missileBlockedRemove = mutation.GetMissileBlockedCellsToRemove(level.Width, level.Height);
            } catch (Exception exception)
                when (exception is InvalidDataException or OverflowException) {
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} contains an out-of-range rectangle.",
                    exception);
            }
            if (blockedAdd.Intersect(blockedRemove).Any()
                || missileBlockedAdd.Intersect(missileBlockedRemove).Any()) {
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} adds and removes the same geometry cell.");
            }
            foreach (var cell in blockedAdd
                         .Concat(blockedRemove)
                         .Concat(missileBlockedAdd)
                         .Concat(missileBlockedRemove)) {
                if (cell < 0 || cell >= checked(level.Width * level.Height))
                    throw new InvalidDataException(
                        $"World mutation {mutation.MutationId} contains an out-of-range cell {cell}.");
            }
        }
    }

    public static AuthoritativeWorldMutationCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeWorldMutationCatalog(table.Rows.Select(row =>
            new AuthoritativeWorldMutation(
                row.RequiredUInt32("mutation_id"),
                ResolveTargetLevelId(row, stableIds),
                ParseCells(row, "blocked_cells_add"),
                ParseCells(row, "blocked_cells_remove"),
                ParseCells(row, "missile_blocked_cells_add"),
                ParseCells(row, "missile_blocked_cells_remove"),
                ParseRectangles(row, "geometry_rect_add"),
                ParseRectangles(row, "geometry_rect_remove"))));
    }

    private static uint ResolveTargetLevelId(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var levelTargetPresent = HasValue(row, "level_id") || HasValue(row, "level_symbol");
        var setLevelTargetPresent = HasValue(row, "set_level_id") || HasValue(row, "set_level_symbol");
        if (levelTargetPresent == setLevelTargetPresent) {
            throw new InvalidDataException(
                $"World mutation row {row.LineNumber} must specify exactly one level target.");
        }

        if (levelTargetPresent) {
            if (HasValue(row, "active_map")) {
                throw new InvalidDataException(
                    $"World mutation row {row.LineNumber} uses active_map without a set-level target.");
            }
            return AuthoritativeLevelIdentity.Resolve(row, stableIds);
        }

        var setLevelId = ResolveSetLevelId(row, stableIds);
        var activeMap = row.OptionalBoolean("active_map", true);
        try {
            return AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
                checked((int)setLevelId),
                activeMap);
        } catch (Exception exception)
            when (exception is ArgumentOutOfRangeException or OverflowException) {
            throw new InvalidDataException(
                $"World mutation row {row.LineNumber} contains an invalid set-level target.",
                exception);
        }
    }

    private static uint ResolveSetLevelId(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        var numericPresent = HasValue(row, "set_level_id");
        var numericId = row.OptionalUInt32("set_level_id");
        var symbol = row.TryGet("set_level_symbol", out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value
            : string.Empty;
        if (symbol.Length == 0)
            return numericId;
        if (stableIds is null) {
            if (!numericPresent)
                throw new InvalidDataException(
                    $"World mutation row {row.LineNumber} requires stable IDs to resolve set-level '{symbol}'.");
            return numericId;
        }

        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("set_level", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"World mutation row {row.LineNumber} references unknown set-level symbol '{symbol}'.",
                exception);
        }
        if (numericPresent && numericId != resolvedId)
            throw new InvalidDataException(
                $"World mutation row {row.LineNumber} maps set level {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    private static bool HasValue(TsvRow row, string column)
        => row.TryGet(column, out var value) && !string.IsNullOrWhiteSpace(value);

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
                    out var parsed)
                ? parsed
                : throw new InvalidDataException(
                    $"World mutation row {row.LineNumber} contains an invalid {column} cell '{cell}'."))
            .ToArray();
    }

    private static IReadOnlyList<AuthoritativeWorldMutationRectangle> ParseRectangles(
        TsvRow row,
        string column)
    {
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return [];
        }
        return value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(rectangle => {
                var fields = rectangle.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (fields.Length != 4
                    || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                    || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                    || !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.");
                }
                try {
                    return new AuthoritativeWorldMutationRectangle(x, y, width, height);
                } catch (ArgumentOutOfRangeException exception) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.",
                        exception);
                }
            })
            .ToArray();
    }
}
