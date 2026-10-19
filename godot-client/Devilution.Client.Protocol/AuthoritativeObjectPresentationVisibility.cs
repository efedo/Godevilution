using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Shared visibility rule for projected object presentation state. */
public static class AuthoritativeObjectPresentationVisibility
{
    /** Native compatibility objects disappear after activation or destruction. */
    public static bool IsVisible(ObjectSnapshot @object, uint currentLevelId)
    {
        ArgumentNullException.ThrowIfNull(@object);
        return (!@object.Activated || @object.KeepVisibleAfterActivation)
            && !@object.Destroyed
            && AuthoritativeLevelVisibility.IsVisible(@object.LevelId, currentLevelId);
    }

    /** Applies the projected native vision boundary to an authored object. */
    public static bool IsVisible(
        ObjectSnapshot @object,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY,
        int viewerLightRadius)
        => IsVisible(@object, currentLevelId)
            && AuthoritativeNativeVisionVisibility.IsVisible(
                @object.LevelId,
                @object.PositionX,
                @object.PositionY,
                currentLevelId,
                geometry,
                viewerX,
                viewerY,
                viewerLightRadius);
}
