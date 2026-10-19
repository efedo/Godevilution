namespace Devilution.Client.Protocol;

/** Determines whether an interpolated entity position must reset on a level change. */
public static class AuthoritativeEntityPositionProjection
{
    /** Returns true when a projected entity may expose a screen position on the active level. */
    public static bool IsVisibleOnActiveLevel(uint entityLevelId, uint currentLevelId)
        => AuthoritativeLevelVisibility.IsVisible(entityLevelId, currentLevelId);

    /** Returns true when a cached screen position cannot safely cross level identities. */
    public static bool RequiresLevelReset(uint previousLevelId, uint nextLevelId)
        => previousLevelId != nextLevelId;
}
