using System.Globalization;
using Devilution.Data;
using Devilution.Domain.Protocol;

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
        if (value > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
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
        if (objectId > AuthoritativeLevel.NativeMaximumDunObjectLayerId)
            throw new ArgumentOutOfRangeException(nameof(objectId));
        ObjectId = objectId;
    }

    public ushort ObjectId { get; }
}

/** A native d-piece value applied to every cell in an authored rectangle. */
public sealed class AuthoritativeWorldMutationPieceRectangle
    : AuthoritativeWorldMutationRectangle
{
    public AuthoritativeWorldMutationPieceRectangle(
        int x,
        int y,
        int width,
        int height,
        ushort pieceId)
        : base(x, y, width, height)
    {
        PieceId = pieceId;
        if (pieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            throw new ArgumentOutOfRangeException(nameof(pieceId));
    }

    public ushort PieceId { get; }
}

/** A validated native collision flag set for a d-piece used by a mutation. */
public sealed record AuthoritativeWorldMutationPieceProperty(
    ushort PieceId,
    AuthoritativeDunTileFlags Flags)
{
    public AuthoritativeWorldMutationPieceProperty Validate()
    {
        const AuthoritativeDunTileFlags supportedFlags =
            AuthoritativeDunTileFlags.Solid
            | AuthoritativeDunTileFlags.BlockLight
            | AuthoritativeDunTileFlags.BlockMissile
            | AuthoritativeDunTileFlags.Transparent
            | AuthoritativeDunTileFlags.TransparentLeft
            | AuthoritativeDunTileFlags.TransparentRight
            | AuthoritativeDunTileFlags.Trap;
        if (PieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount
            || (Flags & ~supportedFlags) != 0) {
            throw new InvalidDataException(
                $"World mutation d-piece {PieceId} has invalid collision flags.");
        }
        return this;
    }
}

/** One native mega-tile value assigned by an authored world mutation. */
public sealed class AuthoritativeWorldMutationMegaTileAssignment
{
    public AuthoritativeWorldMutationMegaTileAssignment(int x, int y, ushort tileId)
    {
        X = x;
        Y = y;
        TileId = tileId;
        if (tileId > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(tileId));
    }

    public int X { get; }

    public int Y { get; }

    public ushort TileId { get; }
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
        string anchorUniqueIdentity = "",
        IEnumerable<AuthoritativeWorldMutationPieceRectangle>? pieceLayerRectangles = null,
        IEnumerable<AuthoritativeWorldMutationMegaTileAssignment>? megaTileAssignments = null,
        int anchorMegaOffsetX = 0,
        int anchorMegaOffsetY = 0,
        bool createTransparencyLayer = false,
        uint megaTileCopySourceLevelId = 0,
        IEnumerable<AuthoritativeWorldMutationRectangle>? megaTileCopyRectangles = null,
        IEnumerable<AuthoritativeWorldMutationPieceProperty>? piecePropertyOverrides = null,
        int anchorSetLevelId = -1)
    {
        if (mutationId == 0)
            throw new ArgumentOutOfRangeException(nameof(mutationId));
        MutationId = mutationId;
        LevelId = levelId;
        AnchorEntityId = anchorEntityId;
        AnchorUniqueIdentity = anchorUniqueIdentity.Trim();
        if (anchorSetLevelId is < -1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(anchorSetLevelId));
        if ((AnchorEntityId != 0 ? 1 : 0)
            + (AnchorUniqueIdentity.Length != 0 ? 1 : 0)
            + (anchorSetLevelId >= 0 ? 1 : 0) > 1)
            throw new InvalidDataException(
                "A world mutation cannot use more than one anchor kind.");
        if (!NativeTextLimits.IsIdentityWithinLimit(AnchorUniqueIdentity))
            throw new ArgumentOutOfRangeException(nameof(anchorUniqueIdentity));
        if (AnchorEntityId == 0 && AnchorUniqueIdentity.Length == 0 && anchorSetLevelId < 0
            && (anchorMegaOffsetX != 0 || anchorMegaOffsetY != 0)) {
            throw new InvalidDataException(
                "Mega-tile anchor offsets require an object, unique-identity, or set-level anchor.");
        }
        if (anchorMegaOffsetX is < -255 or > 255)
            throw new ArgumentOutOfRangeException(nameof(anchorMegaOffsetX));
        if (anchorMegaOffsetY is < -255 or > 255)
            throw new ArgumentOutOfRangeException(nameof(anchorMegaOffsetY));
        AnchorMegaOffsetX = anchorMegaOffsetX;
        AnchorMegaOffsetY = anchorMegaOffsetY;
        AnchorSetLevelId = anchorSetLevelId;
        CreateTransparencyLayer = createTransparencyLayer;
        GeometryRectanglesAdd = NormalizeRectangles(geometryRectanglesAdd);
        GeometryRectanglesRemove = NormalizeRectangles(geometryRectanglesRemove);
        TransparencyRectangles = NormalizeTransparencyRectangles(transparencyRectangles);
        ObjectLayerRectangles = NormalizeObjectLayerRectangles(objectLayerRectangles);
        PieceLayerRectangles = NormalizePieceLayerRectangles(pieceLayerRectangles);
        MegaTileAssignments = NormalizeMegaTileAssignments(megaTileAssignments);
        MegaTileCopySourceLevelId = megaTileCopySourceLevelId;
        MegaTileCopyRectangles = NormalizeRectangles(megaTileCopyRectangles);
        PiecePropertyOverrides = NormalizePiecePropertyOverrides(piecePropertyOverrides);
        if ((MegaTileCopySourceLevelId == 0) != (MegaTileCopyRectangles.Count == 0)) {
            throw new InvalidDataException(
                "Mega-tile copy rectangles require exactly one source level.");
        }
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

    /** Optional published set-level origin used as the mutation coordinate origin. */
    public int AnchorSetLevelId { get; }

    /** World-cell offsets used to resolve an anchored mega-tile origin. */
    public int AnchorMegaOffsetX { get; }

    public int AnchorMegaOffsetY { get; }

    /** Allows a mutation to create the initially absent native transparency layer. */
    public bool CreateTransparencyLayer { get; }

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

    /** Native d-piece assignments retained for exact set-piece mutations. */
    public IReadOnlyList<AuthoritativeWorldMutationPieceRectangle> PieceLayerRectangles { get; }

    /** Native mega-tile values assigned at local mega coordinates. */
    public IReadOnlyList<AuthoritativeWorldMutationMegaTileAssignment> MegaTileAssignments { get; }

    /** Source level whose native mega-tile values are copied into the target. */
    public uint MegaTileCopySourceLevelId { get; }

    /** Native mega-tile rectangles copied from the source level at matching coordinates. */
    public IReadOnlyList<AuthoritativeWorldMutationRectangle> MegaTileCopyRectangles { get; }

    /** Native .sol-compatible collision flags for d-pieces introduced by this mutation. */
    public IReadOnlyList<AuthoritativeWorldMutationPieceProperty> PiecePropertyOverrides { get; }

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

    public IReadOnlyList<(int Cell, ushort PieceId)> GetPieceLayerCells(
        int levelWidth,
        int levelHeight,
        int originX = 0,
        int originY = 0)
        => PieceLayerRectangles
            .SelectMany(rectangle => rectangle
                .EnumerateCells(levelWidth, levelHeight, originX, originY)
                .Select(cell => (cell, rectangle.PieceId)))
            .ToArray();

    public IReadOnlyList<(int Cell, ushort TileId)> GetMegaTileCells(
        int levelWidth,
        int levelHeight,
        int anchorX = 0,
        int anchorY = 0)
    {
        if (MegaTileAssignments.Count == 0)
            return [];

        var megaWidth = checked((levelWidth + 1) / 2);
        var megaHeight = checked((levelHeight + 1) / 2);
        var originX = 0;
        var originY = 0;
        if (AnchorEntityId != 0
            || AnchorUniqueIdentity.Length != 0
            || AnchorSetLevelId >= 0) {
            var translatedX = checked(anchorX + AnchorMegaOffsetX);
            var translatedY = checked(anchorY + AnchorMegaOffsetY);
            if ((translatedX & 1) != 0 || (translatedY & 1) != 0)
                throw new InvalidDataException(
                    $"World mutation {MutationId} resolves an unaligned mega-tile anchor.");
            originX = translatedX / 2;
            originY = translatedY / 2;
        }

        return MegaTileAssignments
            .Select(assignment => {
                var x = checked(originX + assignment.X);
                var y = checked(originY + assignment.Y);
                if (x < 0 || x >= megaWidth || y < 0 || y >= megaHeight)
                    throw new InvalidDataException(
                        $"World mutation {MutationId} contains an out-of-range mega-tile assignment.");
                return (checked(y * megaWidth + x), assignment.TileId);
            })
            .ToArray();
    }

    public IReadOnlyList<(int TargetCell, int SourceCell)> GetMegaTileCopyCells(
        int targetLevelWidth,
        int targetLevelHeight,
        int sourceLevelWidth,
        int sourceLevelHeight)
    {
        if (MegaTileCopyRectangles.Count == 0)
            return [];
        var targetMegaWidth = checked((targetLevelWidth + 1) / 2);
        var targetMegaHeight = checked((targetLevelHeight + 1) / 2);
        var sourceMegaWidth = checked((sourceLevelWidth + 1) / 2);
        var sourceMegaHeight = checked((sourceLevelHeight + 1) / 2);
        var targetCells = MegaTileCopyRectangles
            .SelectMany(rectangle => rectangle.EnumerateCells(targetMegaWidth, targetMegaHeight))
            .ToArray();
        return targetCells
            .Select(targetCell => {
                var x = targetCell % targetMegaWidth;
                var y = targetCell / targetMegaWidth;
                if (x >= sourceMegaWidth || y >= sourceMegaHeight)
                    throw new InvalidDataException(
                        "A mega-tile copy rectangle exceeds the source level bounds.");
                return (targetCell, checked(y * sourceMegaWidth + x));
            })
            .ToArray();
    }

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

    private static IReadOnlyList<AuthoritativeWorldMutationPieceProperty>
        NormalizePiecePropertyOverrides(
            IEnumerable<AuthoritativeWorldMutationPieceProperty>? overrides)
    {
        var values = (overrides ?? [])
            .Select(@override => @override.Validate())
            .OrderBy(@override => @override.PieceId)
            .ToArray();
        if (values.Select(@override => @override.PieceId).Distinct().Count() != values.Length)
            throw new InvalidDataException(
                "World mutation d-piece collision overrides contain duplicate IDs.");
        return values;
    }

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

    private static IReadOnlyList<AuthoritativeWorldMutationPieceRectangle>
        NormalizePieceLayerRectangles(
            IEnumerable<AuthoritativeWorldMutationPieceRectangle>? rectangles)
        => (rectangles ?? [])
            .Where(rectangle => rectangle is not null)
            .OrderBy(rectangle => rectangle.X)
            .ThenBy(rectangle => rectangle.Y)
            .ThenBy(rectangle => rectangle.Width)
            .ThenBy(rectangle => rectangle.Height)
            .ThenBy(rectangle => rectangle.PieceId)
            .ToArray();

    private static IReadOnlyList<AuthoritativeWorldMutationMegaTileAssignment>
        NormalizeMegaTileAssignments(
            IEnumerable<AuthoritativeWorldMutationMegaTileAssignment>? assignments)
        => (assignments ?? [])
            .Where(assignment => assignment is not null)
            .OrderBy(assignment => assignment.X)
            .ThenBy(assignment => assignment.Y)
            .ThenBy(assignment => assignment.TileId)
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
            if (!world.ContainsLevel(mutation.LevelId)) {
                if (AuthoritativeSetLevelMapCatalog.IsRuntimeLevelId(mutation.LevelId))
                    continue;
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} references unknown level {mutation.LevelId}.");
            }
            var level = world.Levels.Single(candidate => candidate.LevelId == mutation.LevelId);
            AuthoritativeLevel? megaTileCopySource = null;
            if (mutation.MegaTileCopyRectangles.Count > 0) {
                if (!world.ContainsLevel(mutation.MegaTileCopySourceLevelId)) {
                    if (AuthoritativeSetLevelMapCatalog.IsRuntimeLevelId(
                            mutation.MegaTileCopySourceLevelId)) {
                        continue;
                    }
                    throw new InvalidDataException(
                        $"World mutation {mutation.MutationId} references unknown mega-tile copy source level {mutation.MegaTileCopySourceLevelId}.");
                }
                megaTileCopySource = world.Levels.Single(candidate =>
                    candidate.LevelId == mutation.MegaTileCopySourceLevelId);
                if (level.NativeMegaTileLayer.Count == 0
                    || megaTileCopySource.NativeMegaTileLayer.Count == 0) {
                    throw new InvalidDataException(
                        $"World mutation {mutation.MutationId} requires native mega-tile layers on both target and source levels.");
                }
                if (level.NativePieceLayer.Count != checked(level.Width * level.Height)
                    || megaTileCopySource.NativePieceLayer.Count
                        != checked(megaTileCopySource.Width * megaTileCopySource.Height)) {
                    throw new InvalidDataException(
                        $"World mutation {mutation.MutationId} requires native d-piece layers on both target and source levels.");
                }
            }
            var anchor = ResolveAnchor(mutation, objects, combatTargets, level);
            if ((mutation.AnchorUniqueIdentity.Length != 0 || mutation.AnchorSetLevelId >= 0)
                && anchor is null)
                // Dynamic anchors may be materialized after content activation;
                // the simulation validates the resolved position.
                continue;
            IReadOnlyList<int> blockedAdd;
            IReadOnlyList<int> blockedRemove;
            IReadOnlyList<int> missileBlockedAdd;
            IReadOnlyList<int> missileBlockedRemove;
            IReadOnlyList<(int Cell, ushort Value)> transparencyCells;
            IReadOnlyList<(int Cell, ushort ObjectId)> objectLayerCells;
            IReadOnlyList<(int Cell, ushort PieceId)> pieceLayerCells;
            IReadOnlyList<(int Cell, ushort TileId)> megaTileCells;
            IReadOnlyList<(int TargetCell, int SourceCell)> megaTileCopyCells;
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
                pieceLayerCells = mutation.GetPieceLayerCells(
                    level.Width,
                    level.Height,
                    anchor?.X ?? 0,
                    anchor?.Y ?? 0);
                megaTileCells = mutation.GetMegaTileCells(
                    level.Width,
                    level.Height,
                    anchor?.X ?? 0,
                    anchor?.Y ?? 0);
                megaTileCopyCells = mutation.GetMegaTileCopyCells(
                    level.Width,
                    level.Height,
                    megaTileCopySource?.Width ?? 0,
                    megaTileCopySource?.Height ?? 0);
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
            if (transparencyCells.Count > 0
                && level.NativeTransparency.Count == 0
                && !mutation.CreateTransparencyLayer)
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} requires a native transparency layer.");
            if (transparencyCells.GroupBy(entry => entry.Cell).Any(group => group.Count() > 1))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} assigns transparency more than once to a cell.");
            if (objectLayerCells.GroupBy(entry => entry.Cell).Any(group => group.Count() > 1))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} assigns an object layer value more than once to a cell.");
            if (pieceLayerCells.GroupBy(entry => entry.Cell).Any(group => group.Count() > 1))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} assigns a d-piece value more than once to a cell.");
            if (megaTileCells.GroupBy(entry => entry.Cell).Any(group => group.Count() > 1))
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} assigns a mega-tile value more than once to a cell.");
            if (megaTileCopyCells.GroupBy(entry => entry.TargetCell).Any(group => group.Count() > 1)
                || megaTileCells.Select(entry => entry.Cell)
                    .Intersect(megaTileCopyCells.Select(entry => entry.TargetCell))
                    .Any()) {
                throw new InvalidDataException(
                    $"World mutation {mutation.MutationId} assigns a mega-tile value more than once to a cell.");
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
                ParseRectangles(row, "geometry_rect_remove"),
                ParseTransparencyRectangles(row),
                ParseObjectLayerRectangles(row),
                row.OptionalUInt32("anchor_entity_id"),
                row.TryGet("anchor_unique_identity", out var anchorUniqueIdentity)
                    ? anchorUniqueIdentity
                    : string.Empty,
                ParsePieceLayerRectangles(row),
                ParseMegaTileAssignments(row),
                row.OptionalInt32("anchor_mega_offset_x"),
                row.OptionalInt32("anchor_mega_offset_y"),
                row.OptionalBoolean("create_transparency_layer"),
                row.OptionalUInt32("mega_tile_copy_source_level_id"),
                ParseRectangles(row, "mega_tile_copy_rects"),
                ParsePiecePropertyOverrides(row),
                ParseAnchorSetLevelId(row, stableIds))));
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

    private static int ParseAnchorSetLevelId(
        TsvRow row,
        StableContentIdCatalog? stableIds)
    {
        if (!HasValue(row, "anchor_set_level_id")
            && !HasValue(row, "anchor_set_level_symbol"))
            return -1;
        return AuthoritativeSetLevelIdentity.ResolveOptional(
            row,
            stableIds,
            "anchor_set_level_id",
            "anchor_set_level_symbol");
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
        if (mutation.AnchorEntityId == 0
            && mutation.AnchorUniqueIdentity.Length == 0
            && mutation.AnchorSetLevelId < 0)
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

        if (mutation.AnchorSetLevelId >= 0)
            return null;

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

    private static IReadOnlyList<AuthoritativeWorldMutationPieceRectangle>
        ParsePieceLayerRectangles(TsvRow row)
    {
        const string column = "piece_layer_rects";
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
                    || !ushort.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pieceId)) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.");
                }
                try {
                    return new AuthoritativeWorldMutationPieceRectangle(
                        x,
                        y,
                        width,
                        height,
                        pieceId);
                } catch (ArgumentOutOfRangeException exception) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} rectangle '{rectangle}'.",
                        exception);
                }
            })
            .ToArray();
    }

    private static IReadOnlyList<AuthoritativeWorldMutationMegaTileAssignment>
        ParseMegaTileAssignments(TsvRow row)
    {
        const string column = "mega_tile_assignments";
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return [];
        }
        return value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(assignment => {
                var fields = assignment.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (fields.Length != 3
                    || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                    || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    || !ushort.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileId)) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} assignment '{assignment}'.");
                }
                try {
                    return new AuthoritativeWorldMutationMegaTileAssignment(x, y, tileId);
                } catch (ArgumentOutOfRangeException exception) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} assignment '{assignment}'.",
                        exception);
                }
            })
            .ToArray();
    }

    private static IReadOnlyList<AuthoritativeWorldMutationPieceProperty>
        ParsePiecePropertyOverrides(TsvRow row)
    {
        const string column = "piece_property_overrides";
        if (!row.TryGet(column, out var value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) {
            return [];
        }
        return value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => {
                var fields = entry.Split(
                    ':',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (fields.Length != 2
                    || !ushort.TryParse(
                        fields[0],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var pieceId)) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} entry '{entry}'.");
                }
                try {
                    return new AuthoritativeWorldMutationPieceProperty(
                        pieceId,
                        ParsePieceFlags(fields[1], row, column, entry));
                } catch (InvalidDataException) {
                    throw;
                } catch (Exception exception) when (exception is ArgumentOutOfRangeException) {
                    throw new InvalidDataException(
                        $"World mutation row {row.LineNumber} contains an invalid {column} entry '{entry}'.",
                        exception);
                }
            })
            .ToArray();
    }

    private static AuthoritativeDunTileFlags ParsePieceFlags(
        string value,
        TsvRow row,
        string column,
        string entry)
    {
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            return AuthoritativeDunTileFlags.None;
        var flags = AuthoritativeDunTileFlags.None;
        foreach (var token in value.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            flags |= token.ToLowerInvariant() switch {
                "solid" => AuthoritativeDunTileFlags.Solid,
                "block_light" => AuthoritativeDunTileFlags.BlockLight,
                "block_missile" => AuthoritativeDunTileFlags.BlockMissile,
                "transparent" => AuthoritativeDunTileFlags.Transparent,
                "transparent_left" => AuthoritativeDunTileFlags.TransparentLeft,
                "transparent_right" => AuthoritativeDunTileFlags.TransparentRight,
                "trap" => AuthoritativeDunTileFlags.Trap,
                _ => throw new InvalidDataException(
                    $"World mutation row {row.LineNumber} contains an invalid {column} flag in '{entry}'."),
            };
        }
        return flags;
    }
}
