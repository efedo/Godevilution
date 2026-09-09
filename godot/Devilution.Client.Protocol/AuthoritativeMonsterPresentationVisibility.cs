using Devilution.Protocol.V1;
using Devilution.Shared;

namespace Devilution.Client.Protocol;

/** Shared visibility rule for projected monster presentation state. */
public static class AuthoritativeMonsterPresentationVisibility
{
    /** Dead and server-hidden monsters never cross the presentation boundary. */
    public static bool IsVisible(MonsterSnapshot monster, uint currentLevelId)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.Alive
            && !monster.SneakHidden
            && AuthoritativeLevelVisibility.IsVisible(monster.LevelId, currentLevelId);
    }

    /** Applies the server's geometry-backed native vision boundary to drawing. */
    public static bool IsVisible(
        MonsterSnapshot monster,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY)
        => IsVisible(monster, currentLevelId, geometry, viewerX, viewerY, 10);

    /** Applies the server's geometry-backed vision boundary for one radius. */
    public static bool IsVisible(
        MonsterSnapshot monster,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY,
        int viewerLightRadius)
    {
        if (!IsVisible(monster, currentLevelId)
            || geometry is null
            || geometry.NativeTransparency.Count == 0
                && geometry.NativeVisionBlockedCells.Count == 0)
            return IsVisible(monster, currentLevelId);

        var nativeVisionRadius = viewerLightRadius is >= 2 and <= 15
            ? viewerLightRadius
            : 10;
        var deltaX = (long)monster.PositionX - viewerX;
        var deltaY = (long)monster.PositionY - viewerY;
        if (Math.Abs(deltaX) > nativeVisionRadius
            || Math.Abs(deltaY) > nativeVisionRadius
            || deltaX * deltaX + deltaY * deltaY
                > (long)nativeVisionRadius * nativeVisionRadius)
            return false;
        if (!Contains(geometry, viewerX, viewerY)
            || !Contains(geometry, monster.PositionX, monster.PositionY))
            return false;

        return NativeVision.IsVisible(
            viewerX,
            viewerY,
            monster.PositionX,
            monster.PositionY,
            nativeVisionRadius,
            (x, y) => !IsBlocked(geometry, x, y),
            (x, y) => Contains(geometry, x, y));
    }

    private static bool Contains(LevelGeometrySnapshot geometry, int x, int y)
        => x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height;

    private static bool IsBlocked(LevelGeometrySnapshot geometry, int x, int y)
        => !Contains(geometry, x, y)
            || (geometry.NativeVisionBlockedCells.Count != 0
                ? geometry.NativeVisionBlockedCells.Contains(y * geometry.Width + x)
                : geometry.BlockedCells.Contains(y * geometry.Width + x));
}
