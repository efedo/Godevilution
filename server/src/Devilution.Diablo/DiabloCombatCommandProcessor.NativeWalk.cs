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
            if (native.CatacombsDepth is >= 5 and <= 8)
                RecordNativeWalkStand(target);
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

    /**
     * Native startStand records the finished walk's mode in var1 and clears
     * var2; the Bat, AiAvoidance, Overlord and Rhino movement bands read both on this
     * same update, so their managed var1/var2 mirrors switch from the walk's
     * displacement to its mode here.
     */
    private static void RecordNativeWalkStand(AuthoritativeCombatTarget target)
    {
        var mode = NativeMovementMode(
            target.FuturePositionX - target.PositionX, target.FuturePositionY - target.PositionY);
        switch (target.NativeAi) {
        case "Bat":
            target.RestoreBatMovementState(0, mode);
            break;
        case "GoatMelee":
            target.RestoreAvoidanceMovementState(0, mode);
            break;
        case "Fat":
            target.RestoreFatMovementState(0, mode);
            break;
        case "Sneak":
            RecordNativeSneakStand(target, mode);
            break;
        case "Rhino":
            target.RestoreRhinoMovementState(0, mode);
            break;
        }
    }

    /** Native GetMonsterMovementMode for one tile step: 1 northwards, 2 southwards, 3 sideways. */
    internal static int NativeMovementMode(int deltaX, int deltaY)
        => (deltaX, deltaY) switch {
            (-1, -1) or (0, -1) or (-1, 0) => 1,
            (1, 1) or (0, 1) or (1, 0) => 2,
            (-1, 1) or (1, -1) => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(deltaX), "A native walk moves exactly one tile."),
        };
}
