namespace Devilution.Diablo;

public enum AuthoritativeCathedralDoorKind { Left = 1, Right = 2 }
public enum AuthoritativeCathedralDoorStatus { Closed = 0, Open = 1, Blocked = 2 }

/** Native AddDoor caches are one-based; piece layers remain zero-based. */
public sealed record AuthoritativeCathedralDoorState(
    AuthoritativeCathedralDoorKind Kind, int PositionX, int PositionY,
    AuthoritativeCathedralDoorStatus Status, int Var1, int Var2,
    int AnimationFrame, int SelectionRegion, bool PreFlag, bool MissFlag,
    bool SolidFlag = false);

public sealed record AuthoritativeCathedralDoorDiscovery(
    AuthoritativeLevel Level, IReadOnlyList<AuthoritativeCathedralDoorState> Doors);
public sealed record AuthoritativeCathedralDoorTransition(
    AuthoritativeLevel Level, AuthoritativeCathedralDoorState Door);

/** Native Cathedral scanner and ordered door operations; no gameplay RNG or assets. */
public static class AuthoritativeCathedralDoors
{
    public static AuthoritativeCathedralDoorDiscovery Discover(AuthoritativeLevel level,
        int existingObjectCount = 0)
    {
        RequireGeometry(level);
        if (existingObjectCount is < 0 or > AuthoritativeLevel.NativeMaximumObjectCount)
            throw new InvalidDataException("Cathedral scan starts outside the native object pool.");
        var special = level.NativeSpecialLayer.Count == 0
            ? level.NativePieceLayer.Select(InitialSpecial).ToArray()
            : level.NativeSpecialLayer.ToArray();
        var current = CopyLevel(level, special: special);
        var doors = new List<AuthoritativeCathedralDoorState>();
        var occupiedSlots = existingObjectCount;
        // AddL1Objs scans y then x. Each AddDoor caches and closes immediately.
        for (var y = 0; y < level.Height; y++) {
            for (var x = 0; x < level.Width; x++) {
                var piece = current.NativePieceLayer[y * level.Width + x];
                var kind = piece switch {
                    43 or 50 or 213 => AuthoritativeCathedralDoorKind.Left,
                    45 or 55 => AuthoritativeCathedralDoorKind.Right,
                    _ => (AuthoritativeCathedralDoorKind)0,
                };
                if (kind == 0 && piece != 269)
                    continue;
                // L1 lights share the door pool even though their presentation is separate.
                // Reject overflow profiles instead of claiming native truncated-scan parity.
                if (++occupiedSlots > AuthoritativeLevel.NativeMaximumObjectCount)
                    throw new InvalidDataException("Cathedral door/light scan exceeds the native object pool.");
                if (kind == 0)
                    continue;
                var (nx, ny) = Neighbor(kind, x, y);
                if (!level.ContainsPosition(nx, ny))
                    throw new InvalidDataException("A Cathedral door frame lies outside the level.");
                var door = new AuthoritativeCathedralDoorState(kind, x, y,
                    AuthoritativeCathedralDoorStatus.Closed, piece + 1,
                    current.NativePieceLayer[ny * level.Width + nx] + 1,
                    kind == AuthoritativeCathedralDoorKind.Left ? 1 : 2,
                    3, false, false);
                var closed = SetState(current, door, open: false);
                current = closed.Level;
                doors.Add(closed.Door);
            }
        }
        return new AuthoritativeCathedralDoorDiscovery(current, doors.AsReadOnly());
    }

    public static AuthoritativeCathedralDoorTransition Operate(
        AuthoritativeLevel level, AuthoritativeCathedralDoorState door, bool occupied)
    {
        Validate(level, door);
        if (door.Status == AuthoritativeCathedralDoorStatus.Closed)
            return SetState(level, door with { AnimationFrame = checked(door.AnimationFrame + 2) }, true);
        if (occupied)
            return new(level, door with { Status = AuthoritativeCathedralDoorStatus.Blocked });
        return SetState(level, door with { AnimationFrame = checked(door.AnimationFrame - 2) }, false);
    }

    /** DeltaSyncOpObject opens directly, bypassing occupancy, and advances animation. */
    public static AuthoritativeCathedralDoorTransition DeltaOpen(
        AuthoritativeLevel level, AuthoritativeCathedralDoorState door)
    {
        Validate(level, door);
        return SetState(level, door with { AnimationFrame = checked(door.AnimationFrame + 2) }, true);
    }

    /** SyncDoor has no animation increment and normalizes BLOCKED to OPEN. */
    public static AuthoritativeCathedralDoorTransition Synchronize(
        AuthoritativeLevel level, AuthoritativeCathedralDoorState door)
    {
        Validate(level, door);
        return SetState(level, door, door.Status != AuthoritativeCathedralDoorStatus.Closed);
    }

    public static AuthoritativeCathedralDoorState UpdateOccupancy(
        AuthoritativeCathedralDoorState door, bool occupied)
        => door.Status == AuthoritativeCathedralDoorStatus.Closed ? door
            : door with { Status = occupied ? AuthoritativeCathedralDoorStatus.Blocked
                : AuthoritativeCathedralDoorStatus.Open };

    public static void Validate(AuthoritativeLevel level, AuthoritativeCathedralDoorState door)
    {
        RequireGeometry(level);
        ArgumentNullException.ThrowIfNull(door);
        var (nx, ny) = Neighbor(door.Kind, door.PositionX, door.PositionY);
        var closed = door.Status == AuthoritativeCathedralDoorStatus.Closed;
        if (!Enum.IsDefined(door.Kind) || !Enum.IsDefined(door.Status)
            || level.NativeSpecialLayer.Count != checked(level.Width * level.Height)
            || !level.ContainsPosition(door.PositionX, door.PositionY)
            || !level.ContainsPosition(nx, ny)
            || door.Var1 <= 0 || door.Var1 > ushort.MaxValue
            || door.Var2 <= 0 || door.Var2 > ushort.MaxValue
            || !level.NativePieceProperties.ContainsKey((ushort)(door.Var1 - 1))
            || !level.NativePieceProperties.ContainsKey((ushort)(door.Var2 - 1))
            || door.AnimationFrame > byte.MaxValue
            || door.AnimationFrame < (door.Kind == AuthoritativeCathedralDoorKind.Left ? 1 : 2) + (closed ? 0 : 2)
            || (door.AnimationFrame & 1) != (door.Kind == AuthoritativeCathedralDoorKind.Left ? 1 : 0)
            || closed && door.AnimationFrame > byte.MaxValue - 2
            || door.SelectionRegion != (closed ? 3 : 2)
            || door.PreFlag != !closed || door.MissFlag != !closed || door.SolidFlag)
            throw new InvalidDataException("Cathedral door contains invalid native state or cached geometry.");
    }

    private static AuthoritativeCathedralDoorTransition SetState(
        AuthoritativeLevel level, AuthoritativeCathedralDoorState door, bool open)
    {
        var changedDoor = door with {
            Status = open ? AuthoritativeCathedralDoorStatus.Open : AuthoritativeCathedralDoorStatus.Closed,
            SelectionRegion = open ? 2 : 3, PreFlag = open, MissFlag = open,
        };
        Validate(level, changedDoor); // Reject unrepresentable animation before publishing geometry.
        var pieces = level.NativePieceLayer.ToArray();
        var special = level.NativeSpecialLayer.ToArray();
        var cell = door.PositionY * level.Width + door.PositionX;
        var (nx, ny) = Neighbor(door.Kind, door.PositionX, door.PositionY);
        var neighbor = ny * level.Width + nx;
        if (open) {
            pieces[cell] = (ushort)(door.Kind == AuthoritativeCathedralDoorKind.Left
                ? door.Var1 == 214 ? 407 : 392 : 394);
            special[cell] = (ushort)(door.Kind == AuthoritativeCathedralDoorKind.Left ? 7 : 8);
            pieces[neighbor] = OpenFrame(pieces[neighbor], door.Kind);
        } else {
            special[cell] = 0;
            pieces[cell] = checked((ushort)(door.Var1 - 1));
            pieces[neighbor] = door.Var2 == 50 && pieces[neighbor] == 395
                ? (ushort)(door.Kind == AuthoritativeCathedralDoorKind.Left ? 411 : 410)
                : checked((ushort)(door.Var2 - 1));
        }
        var blocked = level.BlockedCells.ToHashSet();
        var missile = level.MissileBlockedCells.ToHashSet();
        var nativeBlocked = level.NativePieceCollisionCells.ToHashSet();
        var nativeMissile = level.NativePieceMissileBlockedCells.ToHashSet();
        var vision = level.NativeVisionBlockedCells.ToHashSet();
        foreach (var changed in new[] { cell, neighbor }) {
            if (!level.NativePieceProperties.TryGetValue(pieces[changed], out var flags))
                throw new InvalidDataException($"Cathedral door requires d-piece {pieces[changed]} in the external SOL catalog.");
            if (nativeBlocked.Remove(changed))
                blocked.Remove(changed);
            if (nativeMissile.Remove(changed))
                missile.Remove(changed);
            if (level.NativePieceProperties.TryGetValue(level.NativePieceLayer[changed], out var previous)
                && (previous & (AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight)) != 0)
                vision.Remove(changed);
            if (flags.HasFlag(AuthoritativeDunTileFlags.Solid)) {
                blocked.Add(changed);
                nativeBlocked.Add(changed);
            }
            if (flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile)) {
                missile.Add(changed);
                nativeMissile.Add(changed);
            }
            if ((flags & (AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight)) != 0)
                vision.Add(changed);
        }
        return new(CopyLevel(level, pieces, special, blocked, missile, nativeBlocked, nativeMissile, vision),
            changedDoor);
    }

    private static (int X, int Y) Neighbor(AuthoritativeCathedralDoorKind kind, int x, int y)
        => kind == AuthoritativeCathedralDoorKind.Left ? (x, y - 1) : (x - 1, y);

    internal static ushort OpenFrame(ushort piece, AuthoritativeCathedralDoorKind kind)
        => piece switch {
            42 => 391, 44 => 393,
            49 => (ushort)(kind == AuthoritativeCathedralDoorKind.Left ? 410 : 411),
            53 => 396, 54 => 397, 60 => 398, 66 => 399, 67 => 400,
            68 => 402, 69 => 403, 71 => 405, 211 => 406, 353 => 408, 354 => 409,
            410 or 411 => 395, _ => piece,
        };

    private static ushort InitialSpecial(ushort piece) => piece switch {
        11 or 70 or 320 or 210 or 340 or 417 => 1,
        10 or 248 or 324 or 343 or 330 or 420 => 2,
        252 => 3, 254 => 4, 258 => 5, 266 => 6, _ => 0,
    };

    private static void RequireGeometry(AuthoritativeLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (level.NativePieceLayer.Count != checked(level.Width * level.Height)
            || level.NativePieceProperties.Count == 0)
            throw new InvalidDataException("Cathedral doors require complete external d-piece and SOL geometry.");
    }

    internal static AuthoritativeLevel CopyLevel(AuthoritativeLevel source,
        IEnumerable<ushort>? pieces = null, IEnumerable<ushort>? special = null,
        IEnumerable<int>? blocked = null, IEnumerable<int>? missile = null,
        IEnumerable<int>? nativeBlocked = null, IEnumerable<int>? nativeMissile = null,
        IEnumerable<int>? vision = null, IEnumerable<int>? staticObjectBlocked = null)
        => new(source.LevelId, source.Width, source.Height,
            blocked ?? source.BlockedCells, missile ?? source.MissileBlockedCells,
            source.IsDefaultStart, source.SuppressMultiplayerDeathDrops,
            source.NativeTransparency, source.NativeObjectLayer, source.NativeMegaTileLayer,
            pieces ?? source.NativePieceLayer, source.NativePieceProperties,
            nativeBlocked ?? source.NativePieceCollisionCells,
            nativeMissile ?? source.NativePieceMissileBlockedCells,
            source.NativeMonsterLayer, source.StartPositionX, source.StartPositionY,
            source.HasExplicitNativeVisionBlockedCells ? vision ?? source.NativeVisionBlockedCells : null,
            special ?? source.NativeSpecialLayer,
            staticObjectBlocked ?? source.StaticObjectBlockedCells) { StableSymbol = source.StableSymbol };
}
