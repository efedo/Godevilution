namespace Devilution.Diablo;

/** First-entry skeleton emergence retained separately from immutable native loader provenance. */
public sealed record AuthoritativeNativeInitialSpecialStand(
    int NativeSlot,
    uint EntityId,
    uint LevelId,
    int FrameCount,
    int TicksPerFrame,
    int FrameIndex,
    int TickCounter,
    bool Completed = false)
{
    internal void ValidateShape()
    {
        if (NativeSlot is < 0 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || EntityId == 0 || LevelId == 0 || FrameCount is < 1 or > sbyte.MaxValue
            || TicksPerFrame is < 1 or > sbyte.MaxValue
            || FrameIndex < 0 || FrameIndex >= FrameCount
            || TickCounter < 0 || TickCounter >= TicksPerFrame
            || Completed && (FrameIndex != 0 || TickCounter != 0))
            throw new InvalidDataException("An initial native skeleton emergence cursor is outside its animation bounds.");
    }

    /** Native mode checks precede animation advance; the last frame admits AI on the following tick. */
    internal AuthoritativeNativeInitialSpecialStand Advance()
    {
        ValidateShape();
        if (Completed)
            return this;
        if (FrameIndex == FrameCount - 1)
            return this with { FrameIndex = 0, TickCounter = 0, Completed = true };
        var tick = TickCounter + 1;
        return tick >= TicksPerFrame
            ? this with { FrameIndex = FrameIndex + 1, TickCounter = 0 }
            : this with { TickCounter = tick };
    }
}
