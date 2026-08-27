using Devilution.Protocol.V1;

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
            || geometry.NativeTransparency.Count == 0)
            return IsVisible(monster, currentLevelId);

        var nativeVisionRadius = viewerLightRadius is >= 2 and <= 15
            ? viewerLightRadius
            : 10;
        var deltaX = monster.PositionX - viewerX;
        var deltaY = monster.PositionY - viewerY;
        if ((long)deltaX * deltaX + (long)deltaY * deltaY
            > nativeVisionRadius * nativeVisionRadius)
            return false;
        if (!Contains(geometry, viewerX, viewerY)
            || !Contains(geometry, monster.PositionX, monster.PositionY))
            return false;

        return HasNativeVisionLineOfSight(geometry, viewerX, viewerY, monster.PositionX, monster.PositionY);
    }

    private static bool HasNativeVisionLineOfSight(
        LevelGeometrySnapshot geometry,
        int startX,
        int startY,
        int endX,
        int endY)
    {
        var x = startX;
        var y = startY;
        var deltaX = Math.Abs(endX - startX);
        var deltaY = Math.Abs(endY - startY);
        var stepX = startX < endX ? 1 : -1;
        var stepY = startY < endY ? 1 : -1;
        var error = deltaX - deltaY;

        while (x != endX || y != endY) {
            var doubledError = 2 * error;
            var movesX = doubledError > -deltaY;
            var movesY = doubledError < deltaX;
            if (movesX && movesY
                && IsBlocked(geometry, x + stepX, y)
                && IsBlocked(geometry, x, y + stepY))
                return false;
            if (movesX) {
                error -= deltaY;
                x += stepX;
            }
            if (movesY) {
                error += deltaX;
                y += stepY;
            }
            if ((x != endX || y != endY) && IsBlocked(geometry, x, y))
                return false;
        }
        return true;
    }

    private static bool Contains(LevelGeometrySnapshot geometry, int x, int y)
        => x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height;

    private static bool IsBlocked(LevelGeometrySnapshot geometry, int x, int y)
        => !Contains(geometry, x, y)
            || (geometry.NativeVisionBlockedCells.Count != 0
                ? geometry.NativeVisionBlockedCells.Contains(y * geometry.Width + x)
                : geometry.BlockedCells.Contains(y * geometry.Width + x));
}
