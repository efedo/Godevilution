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
}
