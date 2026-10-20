namespace Devilution.Diablo;

/** Runtime disposition of a generated Gargoyle statue's native special animation. */
public enum AuthoritativeNativeCatacombsMonsterPhaseState : byte
{
    Frozen = 0,
    Playing = 1,
    Completed = 2,
    Interrupted = 3,
}

/** Detached, identity-bound cursor for one generated L5-L8 Gargoyle. */
public sealed record AuthoritativeNativeCatacombsMonsterPhase(
    int NativeSlot,
    uint EntityId,
    uint LevelId,
    int NativeTypeId,
    string MetadataFingerprint,
    int FrameCount,
    int TicksPerFrame,
    int FrameIndex,
    int TickCounter,
    AuthoritativeNativeCatacombsMonsterPhaseState State)
{
    public void ValidateShape()
    {
        if (NativeSlot is < 0 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || EntityId == 0 || LevelId == 0
            || NativeTypeId is < 0 or >= AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount
            || !AuthoritativeNativeMonsterInitialization.IsFingerprint(MetadataFingerprint)
            || FrameCount is < 1 or > sbyte.MaxValue
            || TicksPerFrame is < 1 or > sbyte.MaxValue
            || FrameIndex < 0 || FrameIndex >= FrameCount
            || TickCounter < 0 || TickCounter >= TicksPerFrame
            || !Enum.IsDefined(State)
            || (State is AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
                or AuthoritativeNativeCatacombsMonsterPhaseState.Completed
                or AuthoritativeNativeCatacombsMonsterPhaseState.Interrupted)
                && (FrameIndex != 0 || TickCounter != 0))
            throw new InvalidDataException("A Catacombs monster animation phase is outside its bound native identity or cursor.");
    }

    internal AuthoritativeNativeCatacombsMonsterPhase Advance()
    {
        ValidateShape();
        if (State != AuthoritativeNativeCatacombsMonsterPhaseState.Playing)
            return this;
        if (FrameIndex == FrameCount - 1)
            return this with {
                FrameIndex = 0,
                TickCounter = 0,
                State = AuthoritativeNativeCatacombsMonsterPhaseState.Completed,
            };
        if (TickCounter + 1 < TicksPerFrame)
            return this with { TickCounter = TickCounter + 1 };
        if (FrameIndex + 1 < FrameCount)
            return this with { FrameIndex = FrameIndex + 1, TickCounter = 0 };
        throw new InvalidOperationException("A Catacombs special cursor exceeded its native terminal frame.");
    }

    internal AuthoritativeNativeCatacombsMonsterPhase Start()
    {
        ValidateShape();
        return State == AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
            ? this with { State = AuthoritativeNativeCatacombsMonsterPhaseState.Playing }
            : this;
    }

    internal AuthoritativeNativeCatacombsMonsterPhase Interrupt()
    {
        ValidateShape();
        return State is AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
                or AuthoritativeNativeCatacombsMonsterPhaseState.Playing
            ? this with {
                FrameIndex = 0,
                TickCounter = 0,
                State = AuthoritativeNativeCatacombsMonsterPhaseState.Interrupted,
            }
            : this;
    }
}
