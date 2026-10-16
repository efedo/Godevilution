namespace Devilution.Diablo;

/** Native lifecycle of one retained wall trap and its linked trigger. */
public enum DiabloNativeTrapDisposition
{
    Armed = 0,
    Disarmed = 1,
    Fired = 2,
}

/** Immutable identity reconstructed from the exact generated trap/trigger rows. */
public sealed record AuthoritativeCathedralNativeTrapFacts(
    int TrapObjectSlot,
    uint TrapEntityId,
    int TrapNativeTypeId,
    uint LevelId,
    int ActiveObjectOrder,
    int SourceX,
    int SourceY,
    uint RandomSeed,
    int LinkX,
    int LinkY,
    int MissileTypeId,
    int InitialTrapVar4,
    int TriggerObjectSlot,
    uint TriggerEntityId,
    int TriggerNativeTypeId,
    int TriggerX,
    int TriggerY,
    bool InitialTriggerTrapArmed)
{
    internal void ValidateShape()
    {
        if (TrapObjectSlot is < 0 or >= AuthoritativeCathedralPopulationParameters.ObjectCapacity
            || TrapEntityId == 0 || TrapNativeTypeId is not (53 or 54)
            || LevelId == 0 || ActiveObjectOrder is < 0 or >= AuthoritativeCathedralPopulationParameters.ObjectCapacity
            || (uint)SourceX >= AuthoritativeCathedralThemeRoomState.Width
            || (uint)SourceY >= AuthoritativeCathedralThemeRoomState.Width
            || TrapObjectSlot == TriggerObjectSlot || TrapEntityId == TriggerEntityId
            || LinkX != TriggerX || LinkY != TriggerY
            || (uint)TriggerX >= AuthoritativeCathedralThemeRoomState.Width
            || (uint)TriggerY >= AuthoritativeCathedralThemeRoomState.Width
            || MissileTypeId != 0 || InitialTrapVar4 != 0
            || TriggerObjectSlot is < 0 or >= AuthoritativeCathedralPopulationParameters.ObjectCapacity
            || TriggerEntityId == 0 || TriggerNativeTypeId is not (1 or 2 or 5 or 6 or 7)
            || !InitialTriggerTrapArmed)
            throw new InvalidDataException("Native wall-trap facts do not describe a supported L1 Arrow trigger link.");
    }
}

/** Mutable, checkpointed disposition. Raw trap/trigger identities stay immutable. */
public sealed record DiabloNativeTrapState(
    AuthoritativeCathedralNativeTrapFacts Facts,
    int TrapVar4,
    bool TriggerTrapArmed,
    DiabloNativeTrapDisposition Disposition)
{
    internal void ValidateShape()
    {
        ArgumentNullException.ThrowIfNull(Facts);
        Facts.ValidateShape();
        if (TrapVar4 is < 0 or > 1 || !Enum.IsDefined(Disposition))
            throw new InvalidDataException("A native wall-trap state is outside its bounded schema.");
        if (Disposition == DiabloNativeTrapDisposition.Armed) {
            if (TrapVar4 != Facts.InitialTrapVar4 || !TriggerTrapArmed)
                throw new InvalidDataException("An armed native wall trap lost its initial armed state.");
        } else if (TrapVar4 != 1 || TriggerTrapArmed) {
            throw new InvalidDataException("A consumed native wall trap must retain its one-shot state and cleared trigger flag.");
        }
    }
}

/** Ordered object-tick work committed before the corresponding Arrow missile is created. */
public sealed record DiabloNativeTrapFiringRecord(
    uint TrapEntityId,
    int TrapObjectSlot,
    uint LevelId,
    int SourceX,
    int SourceY,
    int TargetX,
    int TargetY,
    int MissileTypeId,
    int Direction);

public enum DiabloNativeTrapDisarmResult
{
    NotApplicable = 0,
    Failed = 1,
    Disarmed = 2,
}
