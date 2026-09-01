using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation classification for one server-authored level cell. */
public enum AuthoritativeLevelGeometryCell
{
    Walkable,
    MissileBlocked,
    Blocked,
}

/** Resolves authoritative collision layers without introducing client gameplay rules. */
public static class AuthoritativeLevelGeometryCellResolver
{
    /** Returns valid missile-only cells, excluding cells already blocked for walking. */
    public static IReadOnlySet<int> GetMissileOnlyCells(LevelGeometrySnapshot geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var cellCount = (long)geometry.Width * geometry.Height;
        var blocked = geometry.BlockedCells.ToHashSet();
        return geometry.MissileBlockedCells
            .Where(cell => cell >= 0 && cell < cellCount && !blocked.Contains(cell))
            .ToHashSet();
    }

    public static AuthoritativeLevelGeometryCell Resolve(
        LevelGeometrySnapshot geometry,
        int x,
        int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (x < 0 || y < 0 || x >= geometry.Width || y >= geometry.Height)
            return AuthoritativeLevelGeometryCell.Blocked;

        var cell = checked((long)y * geometry.Width + x);
        if (geometry.BlockedCells.Contains(checked((int)cell)))
            return AuthoritativeLevelGeometryCell.Blocked;
        return geometry.MissileBlockedCells.Contains(checked((int)cell))
            ? AuthoritativeLevelGeometryCell.MissileBlocked
            : AuthoritativeLevelGeometryCell.Walkable;
    }
}
