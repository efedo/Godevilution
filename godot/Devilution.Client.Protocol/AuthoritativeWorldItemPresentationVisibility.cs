using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Shared visibility rule for projected world-item presentation state. */
public static class AuthoritativeWorldItemPresentationVisibility
{
    /** Applies level visibility and projected native vision to a world item. */
    public static bool IsVisible(
        WorldItemSnapshot item,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY,
        int viewerLightRadius)
    {
        ArgumentNullException.ThrowIfNull(item);
        return AuthoritativeNativeVisionVisibility.IsVisible(
            item.LevelId,
            item.PositionX,
            item.PositionY,
            currentLevelId,
            geometry,
            viewerX,
            viewerY,
            viewerLightRadius);
    }
}
