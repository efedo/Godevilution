namespace Devilution.Client.Protocol;

/** Shared visibility rule for projected entities and presentation state. */
public static class AuthoritativeLevelVisibility
{
    /** Level zero is the native compatibility wildcard for the active level. */
    public static bool IsVisible(uint projectedLevelId, uint currentLevelId)
        => projectedLevelId == 0 || projectedLevelId == currentLevelId;
}
