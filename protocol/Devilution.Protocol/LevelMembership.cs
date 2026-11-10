namespace Devilution.Shared;

/** Level membership shared by authoritative collision and connection/presentation views. */
public static class LevelMembership
{
    /** Physical projections support legacy unspecified (zero) levels; exact Town metadata does not. */
    public static bool IsOnLevel(uint entityLevelId, uint currentLevelId, bool legacyZeroWildcard = true)
        => entityLevelId == currentLevelId || legacyZeroWildcard && entityLevelId == 0;
}
