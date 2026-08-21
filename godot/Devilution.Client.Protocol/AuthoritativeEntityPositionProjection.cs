namespace Devilution.Client.Protocol;

/** Determines whether an interpolated entity position must reset on a level change. */
public static class AuthoritativeEntityPositionProjection
{
    /** Returns true when a cached screen position cannot safely cross level identities. */
    public static bool RequiresLevelReset(uint previousLevelId, uint nextLevelId)
        => previousLevelId != nextLevelId;
}
