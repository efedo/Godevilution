using Devilution.Protocol.V1;
using Devilution.Shared;

namespace Devilution.Client.Protocol;

/** Shared projected native-vision boundary for world presentation entities. */
public static class AuthoritativeNativeVisionVisibility
{
    /** Applies the server-projected native light radius and ray table to a tile. */
    public static bool IsVisible(
        uint entityLevelId,
        int entityX,
        int entityY,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY,
        int viewerLightRadius)
    {
        if (!AuthoritativeLevelVisibility.IsVisible(entityLevelId, currentLevelId))
            return false;
        if (geometry is null
            || geometry.NativeTransparency.Count == 0
                && geometry.NativeVisionBlockedCells.Count == 0)
            return true;

        var nativeVisionRadius = viewerLightRadius is >= 2 and <= 15
            ? viewerLightRadius
            : 10;
        var deltaX = (long)entityX - viewerX;
        var deltaY = (long)entityY - viewerY;
        if (Math.Abs(deltaX) > nativeVisionRadius
            || Math.Abs(deltaY) > nativeVisionRadius
            || deltaX * deltaX + deltaY * deltaY
                > (long)nativeVisionRadius * nativeVisionRadius)
            return false;
        if (!Contains(geometry, viewerX, viewerY)
            || !Contains(geometry, entityX, entityY))
            return false;

        return NativeVision.IsVisible(
            viewerX,
            viewerY,
            entityX,
            entityY,
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
