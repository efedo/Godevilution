namespace Devilution.Domain.Protocol;

/** Shared tick-boundary rules for server-owned player action lifecycles. */
public static class AuthoritativePlayerActionTimingRules
{
    /** Returns whether an action's effect and ready ticks form an ordered lifecycle. */
    public static bool IsOrdered(
        ulong startedAtTick,
        ulong effectTick,
        ulong readyTick,
        ulong endTick)
        => effectTick >= startedAtTick
            && readyTick >= effectTick
            && readyTick <= endTick
            && endTick >= effectTick;

    /** Returns whether a timed action is internally consistent at a snapshot tick. */
    public static bool IsValidAtSnapshot(
        ulong snapshotTick,
        ulong startedAtTick,
        ulong effectTick,
        ulong readyTick,
        ulong endTick,
        bool effectApplied)
        => startedAtTick <= snapshotTick
            && snapshotTick <= endTick
            && effectApplied == (snapshotTick >= effectTick)
            && IsOrdered(startedAtTick, effectTick, readyTick, endTick);
}
