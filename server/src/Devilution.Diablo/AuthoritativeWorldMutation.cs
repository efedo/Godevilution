using System.Globalization;
using Devilution.Data;

namespace Devilution.Diablo;

/** One rectangle of geometry cells authored for a world interaction. */
public class AuthoritativeWorldMutationRectangle
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

    public IEnumerable<int> EnumerateCells(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
    {
        if (levelWidth <= 0 || levelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelWidth));
        var startX = checked(X + originX);
        var startY = checked(Y + originY);
        var endX = checked(startX + Width);
        var endY = checked(startY + Height);
        if (startX < 0 || startY < 0 || endX > levelWidth || endY > levelHeight)
            throw new InvalidDataException("A world mutation rectangle exceeds its level bounds.");
        for (var y = startY; y < endY; y++)
            for (var x = startX; x < endX; x++)
                yield return checked(y * levelWidth + x);
    }
}

/** A native transparency value applied to every cell in an authored rectangle. */
public sealed class AuthoritativeWorldMutationTransparencyRectangle
    : AuthoritativeWorldMutationRectangle
{
    public AuthoritativeWorldMutationTransparencyRectangle(
        int x,
        int y,
        int width,
        int height,
        ushort value)
        : base(x, y, width, height)
    {
        Value = value;
    }

    public ushort Value { get; }
}

/** A native object-layer value applied to every cell in an authored rectangle. */
public sealed class AuthoritativeWorldMutationObjectRectangle
    : AuthoritativeWorldMutationRectangle
{
    public AuthoritativeWorldMutationObjectRectangle(
        int x,
        int y,
        int width,
        int height,
        ushort objectId)
        : base(x, y, width, height)
    {
        ObjectId = objectId;
    }

    public ushort ObjectId { get; }
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
        IEnumerable<AuthoritativeWorldMutationRectangle>? geometryRectanglesRemove = null,
        IEnumerable<AuthoritativeWorldMutationTransparencyRectangle>? transparencyRectangles = null,
        IEnumerable<AuthoritativeWorldMutationObjectRectangle>? objectLayerRectangles = null,
        uint anchorEntityId = 0,
        string anchorUniqueIdentity = "")
    {
        if (mutationId == 0)
            throw new ArgumentOutOfRangeException(nameof(mutationId));
        MutationId = mutationId;
        LevelId = levelId;
        AnchorEntityId = anchorEntityId;
        AnchorUniqueIdentity = anchorUniqueIdentity.Trim();
        if (AnchorEntityId != 0 && AnchorUniqueIdentity.Length != 0)
            throw new InvalidDataException(
                "A world mutation cannot use both an object and unique-identity anchor.");
        if (AnchorUniqueIdentity.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(anchorUniqueIdentity));
        GeometryRectanglesAdd = NormalizeRectangles(geometryRectanglesAdd);
        GeometryRectanglesRemove = NormalizeRectangles(geometryRectanglesRemove);
        TransparencyRectangles = NormalizeTransparencyRectangles(transparencyRectangles);
        ObjectLayerRectangles = NormalizeObjectLayerRectangles(objectLayerRectangles);
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

    /** Optional runtime object whose position becomes the mutation origin. */
    public uint AnchorEntityId { get; }

    /** Optional server-owned unique encounter identity whose position is the mutation origin. */
    public string AnchorUniqueIdentity { get; }

    public IReadOnlyList<int> BlockedCellsAdd { get; }

    public IReadOnlyList<int> BlockedCellsRemove { get; }

    public IReadOnlyList<int> MissileBlockedCellsAdd { get; }

    public IReadOnlyList<int> MissileBlockedCellsRemove { get; }

    /** Rectangles that add both walkable and missile-blocking geometry. */
    public IReadOnlyList<AuthoritativeWorldMutationRectangle> GeometryRectanglesAdd { get; }

    /** Rectangles that remove both walkable and missile-blocking geometry. */
    public IReadOnlyList<AuthoritativeWorldMutationRectangle> GeometryRectanglesRemove { get; }

    /** Native transparency assignments applied after geometry rectangles. */
    public IReadOnlyList<AuthoritativeWorldMutationTransparencyRectangle> TransparencyRectangles { get; }

    /** Native object-layer assignments retained for explicit object mapping. */
    public IReadOnlyList<AuthoritativeWorldMutationObjectRectangle> ObjectLayerRectangles { get; }

    public IReadOnlyList<int> GetBlockedCells(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
        => MergeCells(
            BlockedCellsAdd,
            GeometryRectanglesAdd,
            levelWidth,
            levelHeight,
            originX,
            originY);

    public IReadOnlyList<int> GetBlockedCellsToRemove(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
        => MergeCells(
            BlockedCellsRemove,
            GeometryRectanglesRemove,
            levelWidth,
            levelHeight,
            originX,
            originY);

    public IReadOnlyList<int> GetMissileBlockedCells(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
        => MergeCells(
            MissileBlockedCellsAdd,
            GeometryRectanglesAdd,
            levelWidth,
            levelHeight,
            originX,
            originY);

    public IReadOnlyList<int> GetMissileBlockedCellsToRemove(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
        => MergeCells(
            MissileBlockedCellsRemove,
            GeometryRectanglesRemove,
            levelWidth,
            levelHeight,
            originX,
            originY);

    public IReadOnlyList<(int Cell, ushort Value)> GetTransparencyCells(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
        => TransparencyRectangles
            .SelectMany(rectangle => rectangle
                .EnumerateCells(levelWidth, levelHeight, originX, originY)
                .Select(cell => (cell, rectangle.Value)))
            .ToArray();

    public IReadOnlyList<(int Cell, ushort ObjectId)> GetObjectLayerCells(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
        => ObjectLayerRectangles
            .SelectMany(rectangle => rectangle
                .EnumerateCells(levelWidth, levelHeight, originX, originY)
                .Select(cell => (cell, rectangle.ObjectId)))
            .ToArray();

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

    private static IReadOnlyList<AuthoritativeWorldMutationTransparencyRectangle>
        NormalizeTransparencyRectangles(
            IEnumerable<AuthoritativeWorldMutationTransparencyRectangle>? rectangles)
        => (rectangles ?? [])
            .Where(rectangle => rectangle is not null)
            .OrderBy(rectangle => rectangle.X)
            .ThenBy(rectangle => rectangle.Y)
            .ThenBy(rectangle => rectangle.Width)
            .ThenBy(rectangle => rectangle.Height)
            .ThenBy(rectangle => rectangle.Value)
            .ToArray();

    private static IReadOnlyList<AuthoritativeWorldMutationObjectRectangle>
        NormalizeObjectLayerRectangles(
            IEnumerable<AuthoritativeWorldMutationObjectRectangle>? rectangles)
        => (rectangles ?? [])
            .Where(rectangle => rectangle is not null)
            .OrderBy(rectangle => rectangle.X)
            .ThenBy(rectangle => rectangle.Y)
            .ThenBy(rectangle => rectangle.Width)
            .ThenBy(rectangle => rectangle.Height)
            .ThenBy(rectangle => rectangle.ObjectId)
            .ToArray();

    private static IReadOnlyList<int> MergeCells(
        IReadOnlyList<int> cells,
        IReadOnlyList<AuthoritativeWorldMutationRectangle> rectangles,
        int levelWidth,
        int levelHeight,
        int originX,
        int originY)
        => cells
            .Select(cell => TranslateCell(cell, levelWidth, levelHeight, originX, originY))
            .Concat(rectangles.SelectMany(rectangle => rectangle.EnumerateCells(
                levelWidth,
                levelHeight,
                originX,
                originY)))
            .Distinct()
            .OrderBy(cell => cell)
            .ToArray();

    private static int TranslateCell(
        int cell,
        int levelWidth,
        int levelHeight,
        int originX,
        int originY)
    {
        if (cell < 0 || cell >= checked(levelWidth * levelHeight))
            throw new InvalidDataException("A world mutation cell is outside its level bounds.");
        var x = checked(cell % levelWidth + originX);
        var y = checked(cell / levelWidth + originY);
        if (x < 0 || x >= levelWidth || y < 0 || y >= levelHeight)
            throw new InvalidDataException("An anchored world mutation cell is outside its level bounds.");
        return checked(y * levelWidth + x);
    }
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

    public void ValidateWorld(
        AuthoritativeWorld world,
        IEnumerable<AuthoritativeWorldObject>? objects = null,
        IEnumerable<AuthoritativeCombatTarget>? combatTargets = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var mutation in definitions.Values) {
            if (!world.ContainsLevel(mutation.LevelId))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} references unknown level {mutation.LevelId}.");
            var level = world.Levels.Single(candidate => candidate.LevelId == mutation.LevelId);
            var anchor = ResolveAnchor(mutation, objects, combatTargets, level);
            if (mutation.AnchorUniqueIdentity.Length != 0 && anchor is null)
                // Generated unique encounters may be materialized after content
                // activation; the simulation validates the resolved position.
                continue;
            IReadOnlyList<int> blockedAdd;
            IReadOnlyList<int> blockedRemove;
            IReadOnlyList<int> missileBlockedAdd;
            IReadOnlyList<int> missileBlockedRemove;
            IReadOnlyList<(int Cell, ushort Value)> transparencyCells;
            IReadOnlyList<(int Cell, ushort ObjectId)> objectLayerCells;
            try {
                blockedAdd = mutation.GetBlockedCells(
                    level.Width,
                    level.Height,
                    anchor?.X ?? 0,
                    anchor?.Y ?? 0);
                blockedRemove = mutation.GetBlockedCellsToRemove(
                    level.Width,
                    level.Height,
                    anchor?.X ?? 0,
                    anchor?.Y ?? 0);
                missileBlockedAdd = mutation.GetMissileBlockedCells(
                    level.Width,
                    level.Height,
                    anchor?.X ?? 0,
                    anchor?.Y ?? 0);
                missileBlockedRemove = mutation.GetMissileBlockedCellsToRemove(
                    level.Width,
                    level.Height,
                        anchor?.X ?? 0,
                        anchor?.Y ?? 0);
                transparencyCells = mutation.GetTransparencyCells(
                    level.Width,
                    level.Height,
                    anchor?.X ?? 0,
                    anchor?.Y ?? 0);
                objectLayerCells = mutation.GetObjectLayerCells(
                    level.Width,
                    level.Height,
                    anchor?.X ?? 0,
                    anchor?.Y ?? 0);
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
            if (transparencyCells.Count > 0 && level.NativeTransparency.Count == 0)
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} requires a native transparency layer.");
            if (transparencyCells.GroupBy(entry => entry.Cell).Any(group => group.Count() > 1))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} assigns transparency more than once to a cell.");
            if (objectLayerCells.GroupBy(entry => entry.Cell).Any(group => group.Count() > 1))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} assigns an object layer value more than once to a cell.");
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
                ParseRectangles(row, "geometry_rect_remove"),
                ParseTransparencyRectangles(row),
                ParseObjectLayerRectangles(row),
                row.OptionalUInt32("anchor_entity_id"),
                row.TryGet("anchor_unique_identity", out var anchorUniqueIdentity)
                    ? anchorUniqueIdentity
                    : string.Empty)));
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

    private static (int X, int Y)? ResolveAnchor(
        AuthoritativeWorldMutation mutation,
        IEnumerable<AuthoritativeWorldObject>? objects,
        IEnumerable<AuthoritativeCombatTarget>? combatTargets,
        AuthoritativeLevel level)
    {
        if (mutation.AnchorEntityId == 0 && mutation.AnchorUniqueIdentity.Length == 0)
            return null;
        if (mutation.AnchorEntityId != 0) {
            var anchor = objects?.SingleOrDefault(@object =>
                @object.EntityId == mutation.AnchorEntityId);
            if (anchor is null)
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} references missing anchor entity {mutation.AnchorEntityId}.");
            if (anchor.LevelId != level.LevelId)
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} anchor entity {mutation.AnchorEntityId} is on level {anchor.LevelId}, expected {level.LevelId}.");
            return (anchor.PositionX, anchor.PositionY);
        }

        if (combatTargets is null)
            return null;
        var matches = combatTargets
            .Where(target => target.IsUnique
                && string.Equals(
                    target.UniqueIdentity,
                    mutation.AnchorUniqueIdentity,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
            return null;
        if (matches.Length != 1)
            throw new InvalidDataException(
                $"World mutation {mutation.MutationId} unique anchor '{mutation.AnchorUniqueIdentity}' "
                + $"must match exactly one encounter; found {matches.Length}.");
        if (matches[0].LevelId != level.LevelId)
            throw new InvalidDataException(
                $"World mutation {mutation.MutationId} unique anchor '{mutation.AnchorUniqueIdentity}' "
                + $"is on level {matches[0].LevelId}, expected {level.LevelId}.");
        return (matches[0].PositionX, matches[0].PositionY);
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

    private static IReadOnlyList<AuthoritativeWorldMutationTransparencyRectangle>
        ParseTransparencyRectangles(TsvRow row)
    {
        const string column = "transparency_rects";
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
                if (fields.Length != 5
                    || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                    || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                    || !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
                    || !ushort.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var transparency)) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.");
                }
                try {
                    return new AuthoritativeWorldMutationTransparencyRectangle(
                        x,
                        y,
                        width,
                        height,
                        transparency);
                } catch (ArgumentOutOfRangeException exception) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.",
                        exception);
                }
            })
            .ToArray();
    }

    private static IReadOnlyList<AuthoritativeWorldMutationObjectRectangle>
        ParseObjectLayerRectangles(TsvRow row)
    {
        const string column = "object_layer_rects";
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
                if (fields.Length != 5
                    || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                    || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                    || !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
                    || !ushort.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId)) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.");
                }
                try {
                    return new AuthoritativeWorldMutationObjectRectangle(
                        x,
                        y,
                        width,
                        height,
                        objectId);
                } catch (ArgumentOutOfRangeException exception) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.",
                        exception);
                }
            })
            .ToArray();
    }
}
