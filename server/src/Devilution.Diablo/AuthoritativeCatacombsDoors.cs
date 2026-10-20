namespace Devilution.Diablo;

public enum AuthoritativeCatacombsDoorKind { Left = 1, Right = 2 }
public enum AuthoritativeCatacombsDoorStatus { Closed = 0, Open = 1, Blocked = 2 }

/** State bound to an active native L2 door object and one Catacombs level. */
public sealed record AuthoritativeCatacombsDoorState(
    uint EntityId,
    uint LevelId,
    uint NativeObjectId,
    AuthoritativeCatacombsDoorKind Kind,
    int PositionX,
    int PositionY,
    AuthoritativeCatacombsDoorStatus Status,
    int AnimationFrame,
    int SelectionRegion,
    bool PreFlag,
    bool MissFlag,
    bool SolidFlag = false);

public sealed record AuthoritativeCatacombsDoorTransition(
    AuthoritativeLevel Level,
    AuthoritativeCatacombsDoorState Door);

/** Native L2 door state and geometry transitions, isolated from Cathedral L1 swizzles. */
public static class AuthoritativeCatacombsDoors
{
    public const ushort ClosedLeftPiece = 537;
    public const ushort ClosedRightPiece = 539;
    public const ushort OpenLeftPiece = 12;
    public const ushort OpenRightPiece = 16;
    public const ushort OpenLeftSpecial = 5;
    public const ushort OpenRightSpecial = 6;

    /**
     * Binds a generated native object to its retained server entity. L2 doors are
     * already closed by AddDoor before this boundary; geometry scanning is not used
     * to infer object ownership.
     */
    public static AuthoritativeCatacombsDoorState Bind(
        AuthoritativeLevel level,
        uint depth,
        AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCathedralPopulationObjectState raw,
        AuthoritativeWorldObject value)
        => BindCore(level, depth, catalog, raw, value, retainLinkedTrapFlag: false);

    /** Retains an armed generated door; its wall effect remains a separate live capability. */
    internal static AuthoritativeCatacombsDoorState BindRetained(
        AuthoritativeLevel level,
        uint depth,
        AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCathedralThemeRoomState source,
        AuthoritativeCathedralPopulationObjectState raw,
        AuthoritativeWorldObject value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Slot < 0 || raw.Slot >= source.ObjectSlots.Count
            || source.ObjectSlots[raw.Slot] != raw
            || !source.ActiveObjectIds.Take(source.ActiveObjectCount).Contains(raw.Slot)
            || (uint)raw.Position.X >= AuthoritativeCathedralThemeRoomState.Width
            || (uint)raw.Position.Y >= AuthoritativeCathedralThemeRoomState.Width
            || source.ObjectRefs[raw.Position.Y * AuthoritativeCathedralThemeRoomState.Width + raw.Position.X] != raw.Slot + 1)
            throw new InvalidDataException("A retained Catacombs door must bind its exact active native origin.");
        var links = source.ActiveObjectIds.Take(source.ActiveObjectCount)
            .Select(slot => source.ObjectSlots[slot])
            .Where(trap => trap.Type is 53 or 54 && trap.Var1 == raw.Position.X && trap.Var2 == raw.Position.Y)
            .ToArray();
        if (raw.TrapFlag != (links.Length != 0) || links.Any(trap =>
                trap.Var3 is not (0 or 1 or 7) || trap.Var4 != 0
                || (uint)trap.Position.X >= AuthoritativeCathedralThemeRoomState.Width
                || (uint)trap.Position.Y >= AuthoritativeCathedralThemeRoomState.Width
                || source.ObjectRefs[trap.Position.Y * AuthoritativeCathedralThemeRoomState.Width + trap.Position.X] != trap.Slot + 1))
            throw new InvalidDataException("A retained Catacombs door trap flag requires its exact generated wall link.");
        return BindCore(level, depth, catalog, raw, value, retainLinkedTrapFlag: true);
    }

    private static AuthoritativeCatacombsDoorState BindCore(
        AuthoritativeLevel level, uint depth, AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCathedralPopulationObjectState raw, AuthoritativeWorldObject value,
        bool retainLinkedTrapFlag)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(value);
        var definition = catalog.ForDepth(depth);
        if (raw.AnimFrame > byte.MaxValue)
            throw new InvalidDataException("Native Catacombs door frame is outside the object byte field.");
        var kind = raw.Type switch {
            (int)AuthoritativeCatacombsDoorCatalog.LeftNativeObjectId => AuthoritativeCatacombsDoorKind.Left,
            (int)AuthoritativeCatacombsDoorCatalog.RightNativeObjectId => AuthoritativeCatacombsDoorKind.Right,
            _ => throw new InvalidDataException("Only native Catacombs L2 door types 42 and 43 are admitted."),
        };
        var expectedType = kind == AuthoritativeCatacombsDoorKind.Left
            ? definition.LeftNativeObjectId : definition.RightNativeObjectId;
        if ((uint)raw.Type != expectedType)
            throw new InvalidDataException("Catacombs door type does not match the depth catalog.");

        var door = new AuthoritativeCatacombsDoorState(
            value.EntityId, level.LevelId, expectedType, kind,
            raw.Position.X, raw.Position.Y,
            ParseStatus(raw.Var4), checked((int)raw.AnimFrame),
            raw.SelectionRegion, raw.PreFlag, raw.MissilePassable, raw.SolidFlag);
        ValidateCore(level, depth, catalog, door, raw, value, retainLinkedTrapFlag);
        return door;
    }

    public static AuthoritativeCatacombsDoorTransition Operate(
        AuthoritativeLevel level,
        uint depth,
        AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCatacombsDoorState door,
        bool occupied)
    {
        Validate(level, depth, catalog, door);
        if (door.Status == AuthoritativeCatacombsDoorStatus.Closed)
            return SetState(level, catalog, door with {
                AnimationFrame = checked(door.AnimationFrame + 2),
            }, depth, open: true);
        if (occupied)
            return new(level, door with { Status = AuthoritativeCatacombsDoorStatus.Blocked });
        return SetState(level, catalog, door with {
            AnimationFrame = checked(door.AnimationFrame - 2),
        }, depth, open: false);
    }

    /** Native DeltaSyncOpObject opens directly and advances the animation frame. */
    public static AuthoritativeCatacombsDoorTransition DeltaOpen(
        AuthoritativeLevel level,
        uint depth,
        AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCatacombsDoorState door)
    {
        Validate(level, depth, catalog, door);
        return SetState(level, catalog, door with {
            AnimationFrame = checked(door.AnimationFrame + 2),
        }, depth, open: true);
    }

    /** Native SyncDoor changes geometry without advancing the animation. */
    public static AuthoritativeCatacombsDoorTransition Synchronize(
        AuthoritativeLevel level,
        uint depth,
        AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCatacombsDoorState door)
    {
        Validate(level, depth, catalog, door);
        return SetState(level, catalog, door, depth, open: door.Status != AuthoritativeCatacombsDoorStatus.Closed);
    }

    public static AuthoritativeCatacombsDoorState UpdateOccupancy(
        AuthoritativeCatacombsDoorState door, bool occupied)
        => door.Status == AuthoritativeCatacombsDoorStatus.Closed
            ? door
            : door with {
                Status = occupied ? AuthoritativeCatacombsDoorStatus.Blocked
                    : AuthoritativeCatacombsDoorStatus.Open,
            };

    public static void Validate(
        AuthoritativeLevel level,
        uint depth,
        AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCatacombsDoorState door,
        AuthoritativeCathedralPopulationObjectState? raw = null,
        AuthoritativeWorldObject? value = null)
        => ValidateCore(level, depth, catalog, door, raw, value, retainLinkedTrapFlag: false);

    private static void ValidateCore(AuthoritativeLevel level, uint depth,
        AuthoritativeCatacombsDoorCatalog catalog, AuthoritativeCatacombsDoorState door,
        AuthoritativeCathedralPopulationObjectState? raw, AuthoritativeWorldObject? value,
        bool retainLinkedTrapFlag)
    {
        RequireGeometry(level);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(door);
        if (retainLinkedTrapFlag && raw is not null && value is not null && raw.TrapFlag != value.TrapArmed)
            throw new InvalidDataException("A retained Catacombs door changed its generated trap flag.");
        var definition = catalog.ForDepth(depth);
        var isLeft = door.Kind == AuthoritativeCatacombsDoorKind.Left;
        var expectedObjectId = isLeft ? definition.LeftNativeObjectId : definition.RightNativeObjectId;
        var closed = door.Status == AuthoritativeCatacombsDoorStatus.Closed;
        var cell = Cell(level, door.PositionX, door.PositionY);
        var expectedPiece = closed ? (isLeft ? ClosedLeftPiece : ClosedRightPiece)
            : (isLeft ? OpenLeftPiece : OpenRightPiece);
        var expectedSpecial = closed ? (ushort)0 : isLeft ? OpenLeftSpecial : OpenRightSpecial;
        var baseFrame = isLeft ? 1 : 2;
        if (door.EntityId == 0 || door.LevelId != level.LevelId
            || door.NativeObjectId != expectedObjectId
            || !Enum.IsDefined(door.Kind) || !Enum.IsDefined(door.Status)
            || door.AnimationFrame < baseFrame + (closed ? 0 : 2)
            || door.AnimationFrame > (closed ? byte.MaxValue - 2 : byte.MaxValue)
            || (door.AnimationFrame & 1) != (baseFrame & 1)
            || door.SelectionRegion != (closed ? 3 : 2)
            || door.PreFlag != !closed || door.MissFlag != !closed || door.SolidFlag
            || level.NativePieceLayer[cell] != expectedPiece
            || level.NativeSpecialLayer[cell] != expectedSpecial
            || !level.NativePieceProperties.ContainsKey(expectedPiece))
            throw new InvalidDataException("Catacombs door state or retained L2 geometry is invalid.");

        if (raw is not null) {
            if (raw.Slot is < 0 or >= AuthoritativeLevel.NativeMaximumObjectCount
                || raw.Type != (int)expectedObjectId
                || raw.Position.X != door.PositionX || raw.Position.Y != door.PositionY
                || !raw.DoorFlag || raw.TrapFlag && !retainLinkedTrapFlag || raw.SolidFlag
                || raw.AnimDelay != 1000 || raw.AnimLength != 0 || raw.AnimWidth != 64
                || raw.Var4 != (int)door.Status || raw.AnimFrame != (uint)door.AnimationFrame
                || raw.SelectionRegion != door.SelectionRegion || raw.PreFlag != door.PreFlag
                || raw.MissilePassable != door.MissFlag)
                throw new InvalidDataException("Catacombs door does not match its ordered native object record.");
        }
        if (value is not null) {
            if (value.EntityId != door.EntityId || value.LevelId != level.LevelId
                || value.NativeObjectId != expectedObjectId
                || value.PositionX != door.PositionX || value.PositionY != door.PositionY
                || value.Activated || value.Breakable || value.Destroyed || value.TrapArmed && !retainLinkedTrapFlag
                || value.MissilePassable != door.MissFlag
                || value.AnimationFrame != door.AnimationFrame)
                throw new InvalidDataException("Catacombs door does not match its retained server entity.");
        }
    }

    private static AuthoritativeCatacombsDoorTransition SetState(
        AuthoritativeLevel level,
        AuthoritativeCatacombsDoorCatalog catalog,
        AuthoritativeCatacombsDoorState door,
        uint depth,
        bool open)
    {
        var isLeft = door.Kind == AuthoritativeCatacombsDoorKind.Left;
        var changed = door with {
            Status = open ? AuthoritativeCatacombsDoorStatus.Open : AuthoritativeCatacombsDoorStatus.Closed,
            SelectionRegion = open ? 2 : 3,
            PreFlag = open,
            MissFlag = open,
        };
        var pieces = level.NativePieceLayer.ToArray();
        var special = level.NativeSpecialLayer.ToArray();
        var cell = Cell(level, door.PositionX, door.PositionY);
        var previousPiece = pieces[cell];
        pieces[cell] = open ? (isLeft ? OpenLeftPiece : OpenRightPiece)
            : (isLeft ? ClosedLeftPiece : ClosedRightPiece);
        special[cell] = open ? isLeft ? OpenLeftSpecial : OpenRightSpecial : (ushort)0;

        var blocked = level.BlockedCells.ToHashSet();
        var missile = level.MissileBlockedCells.ToHashSet();
        var nativeBlocked = level.NativePieceCollisionCells.ToHashSet();
        var nativeMissile = level.NativePieceMissileBlockedCells.ToHashSet();
        var vision = level.NativeVisionBlockedCells.ToHashSet();
        if (!level.NativePieceProperties.TryGetValue(previousPiece, out var previousFlags)
            || !level.NativePieceProperties.TryGetValue(pieces[cell], out var newFlags))
            throw new InvalidDataException("Catacombs door transition references a missing native SOL row.");
        if (nativeBlocked.Remove(cell))
            blocked.Remove(cell);
        if (level.StaticObjectBlockedCells.Contains(cell))
            blocked.Add(cell);
        if (nativeMissile.Remove(cell))
            missile.Remove(cell);
        if ((previousFlags & (AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight)) != 0)
            vision.Remove(cell);
        if (newFlags.HasFlag(AuthoritativeDunTileFlags.Solid)) {
            blocked.Add(cell);
            nativeBlocked.Add(cell);
        }
        if (newFlags.HasFlag(AuthoritativeDunTileFlags.BlockMissile)) {
            missile.Add(cell);
            nativeMissile.Add(cell);
        }
        if ((newFlags & (AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight)) != 0)
            vision.Add(cell);

        var updated = CopyLevel(level, pieces, special, blocked, missile, nativeBlocked,
            nativeMissile, vision);
        Validate(updated, depth, catalog, changed);
        return new(updated, changed);
    }

    private static AuthoritativeCatacombsDoorStatus ParseStatus(int value)
        => value switch {
            0 => AuthoritativeCatacombsDoorStatus.Closed,
            1 => AuthoritativeCatacombsDoorStatus.Open,
            2 => AuthoritativeCatacombsDoorStatus.Blocked,
            _ => throw new InvalidDataException("Native Catacombs door state is outside closed/open/blocked."),
        };

    private static int Cell(AuthoritativeLevel level, int x, int y)
    {
        if (!level.ContainsPosition(x, y))
            throw new InvalidDataException("Catacombs door is outside the native fine grid.");
        return checked(y * level.Width + x);
    }

    private static void RequireGeometry(AuthoritativeLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (level.NativePieceLayer.Count != checked(level.Width * level.Height)
            || level.NativeSpecialLayer.Count != checked(level.Width * level.Height)
            || level.NativePieceProperties.Count == 0)
            throw new InvalidDataException("Catacombs doors require complete native piece, special and SOL geometry.");
    }

    private static AuthoritativeLevel CopyLevel(AuthoritativeLevel source,
        IEnumerable<ushort>? pieces = null, IEnumerable<ushort>? special = null,
        IEnumerable<int>? blocked = null, IEnumerable<int>? missile = null,
        IEnumerable<int>? nativeBlocked = null, IEnumerable<int>? nativeMissile = null,
        IEnumerable<int>? vision = null)
        => new(source.LevelId, source.Width, source.Height,
            blocked ?? source.BlockedCells, missile ?? source.MissileBlockedCells,
            source.IsDefaultStart, source.SuppressMultiplayerDeathDrops,
            source.NativeTransparency, source.NativeObjectLayer, source.NativeMegaTileLayer,
            pieces ?? source.NativePieceLayer, source.NativePieceProperties,
            nativeBlocked ?? source.NativePieceCollisionCells,
            nativeMissile ?? source.NativePieceMissileBlockedCells,
            source.NativeMonsterLayer, source.StartPositionX, source.StartPositionY,
            source.HasExplicitNativeVisionBlockedCells ? vision ?? source.NativeVisionBlockedCells : null,
            special ?? source.NativeSpecialLayer, source.StaticObjectBlockedCells) {
            StableSymbol = source.StableSymbol,
        };
}
