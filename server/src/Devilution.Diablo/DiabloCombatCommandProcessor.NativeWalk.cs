namespace Devilution.Diablo;

/** Persisted native Walk animation, including its initial skipped frame. */
public sealed record AuthoritativeNativeWalkCursor(int FrameIndex, int TickCounter);

public sealed partial class DiabloCombatCommandProcessor
{
    private static void BeginNativeWalk(AuthoritativeCombatTarget target, ulong tick)
    {
        if (!target.IsMoving || target.NativeWalkCursor is not null)
            throw new InvalidOperationException("Native walking requires a newly reserved adjacent tile.");
        target.RestoreNativeWalkCursor(new AuthoritativeNativeWalkCursor(-1, 0));
        _ = AdvanceNativeWalk(target, tick);
    }

    /** Commits the reserved tile only when the source's pre-advance frame is terminal. */
    private static bool AdvanceNativeWalk(AuthoritativeCombatTarget target, ulong tick)
    {
        var cursor = target.NativeWalkCursor
            ?? throw new InvalidDataException("An owned native walk lost its animation cursor.");
        var native = target.NativeInitialization!;
        if (cursor.FrameIndex == native.NativeWalkFrameCount - 1) {
            target.CompleteMovement();
            target.RestoreFallenAnimation(0, 0);
            return true;
        }
        var rate = Math.Max(1, native.NativeWalkTicksPerFrame);
        // Rebase presentation timing after an inactive-level or petrified pause.
        var elapsed = (cursor.FrameIndex + 1) * rate + cursor.TickCounter;
        var remaining = (native.NativeWalkFrameCount - 1 - cursor.FrameIndex) * rate - cursor.TickCounter;
        target.RestoreMovementTiming(checked(tick - (ulong)elapsed), checked(tick + (ulong)remaining));
        var nextTick = cursor.TickCounter + 1;
        var nextFrame = cursor.FrameIndex;
        if (nextTick >= rate) {
            nextTick = 0;
            nextFrame++;
        }
        target.RestoreNativeWalkCursor(new AuthoritativeNativeWalkCursor(nextFrame, nextTick));
        return false;
    }
}
