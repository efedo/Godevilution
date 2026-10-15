namespace Devilution.Diablo;

/** Lifecycle of the exact native monster slot linked to one sarcophagus. */
public enum DiabloNativeSarcophagusActorState
{
    None = 0,
    Reserved = 1,
    Emerging = 2,
    Active = 3,
    Dead = 4,
}

/** Complete mutable projection for one immutable generated sarcophagus row. */
public sealed record DiabloNativeSarcophagusState(
    int ObjectSlot,
    uint EntityId,
    int NativeTypeId,
    int InitialPositionX,
    int InitialPositionY,
    uint RandomSeed,
    int Var1,
    int Var2,
    int InitialSelectionRegion,
    bool TrapArmed,
    bool HasLinkedTrap,
    uint InitialAnimationFrame,
    uint InitialAnimationFlag,
    int InitialAnimationDelay,
    int InitialAnimationCount,
    uint InitialAnimationLength,
    bool Opened,
    uint AnimationFlag,
    int AnimationDelay,
    int AnimationCount,
    uint AnimationFrame,
    int LinkedMonsterSlot,
    uint LinkedMonsterEntityId,
    int LinkedMonsterTypeId,
    DiabloNativeSarcophagusActorState ActorState,
    int SpawnPositionX,
    int SpawnPositionY,
    int SpawnDirection,
    int SpecialFrameIndex,
    int SpecialTickCounter)
{
    // Native animation delay/count are int fields; SetupObject legitimately uses a 1000-tick delay.
    internal void ValidateShape()
    {
        if (ObjectSlot is < 0 or >= AuthoritativeCathedralPopulationParameters.ObjectCapacity
            || EntityId == 0 || NativeTypeId != 48
            || InitialPositionX is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || InitialPositionY is <= 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || Var1 is < 0 or > 9
            || InitialSelectionRegion is < 1 or > 7
            || InitialAnimationFrame > InitialAnimationLength
            || InitialAnimationLength is 0 or > byte.MaxValue
            || InitialAnimationFlag != 0
            || InitialAnimationDelay < 1
            || InitialAnimationCount < 0 || InitialAnimationCount >= InitialAnimationDelay
            || AnimationFlag is > 1
            || AnimationDelay < 1
            || AnimationCount < 0
            || AnimationFrame > AnimationLengthBound
            || LinkedMonsterSlot is < -1 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || (LinkedMonsterSlot == -1) != (LinkedMonsterEntityId == 0)
            || (LinkedMonsterSlot == -1) != (LinkedMonsterTypeId == -1)
            || (LinkedMonsterSlot >= 0 && LinkedMonsterTypeId < 0)
            || !Enum.IsDefined(ActorState)
            || SpawnDirection is < 0 or > 7
            || SpecialFrameIndex < 0 || SpecialTickCounter < 0)
            throw new InvalidDataException("A native sarcophagus lifecycle record is outside its bounded schema.");

        if (!Opened) {
            if (AnimationFlag != InitialAnimationFlag || AnimationDelay != InitialAnimationDelay
                || AnimationCount != InitialAnimationCount || AnimationFrame != InitialAnimationFrame
                || ActorState != (LinkedMonsterSlot < 0
                    ? DiabloNativeSarcophagusActorState.None
                    : DiabloNativeSarcophagusActorState.Reserved)
                || SpawnPositionX != 0 || SpawnPositionY != 0 || SpawnDirection != 0
                || SpecialFrameIndex != 0 || SpecialTickCounter != 0)
                throw new InvalidDataException("A closed native sarcophagus cannot carry operation state.");
        } else if (AnimationDelay != 3 || AnimationFrame < InitialAnimationFrame
            || AnimationFrame > InitialAnimationLength
            || AnimationCount != InitialAnimationCount && AnimationCount > 2
            || AnimationFlag == 0 && AnimationFrame != InitialAnimationLength)
            throw new InvalidDataException("An opened native sarcophagus lost its one-way source animation state.");

        if (ActorState is DiabloNativeSarcophagusActorState.None or DiabloNativeSarcophagusActorState.Reserved) {
            if ((ActorState == DiabloNativeSarcophagusActorState.None) != (LinkedMonsterSlot == -1)
                || SpawnPositionX != 0 || SpawnPositionY != 0
                || SpawnDirection != 0 || SpecialFrameIndex != 0 || SpecialTickCounter != 0)
                throw new InvalidDataException("An unspawned sarcophagus actor disposition is inconsistent.");
        } else if (!Opened || LinkedMonsterSlot < 0
            || SpawnPositionX is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || SpawnPositionY is < 0 or >= AuthoritativeCathedralThemeRoomState.Width)
            throw new InvalidDataException("A spawned sarcophagus actor lacks its exact owner and location.");

        if (ActorState is DiabloNativeSarcophagusActorState.Emerging or DiabloNativeSarcophagusActorState.Active
            or DiabloNativeSarcophagusActorState.Dead) {
            var expectedDirection = GetExpectedFacing(
                InitialPositionX, InitialPositionY, SpawnPositionX, SpawnPositionY);
            if (expectedDirection < 0 || SpawnDirection != expectedDirection)
                throw new InvalidDataException("A spawned sarcophagus actor has a nonnative spawn direction.");
        }

        if (ActorState == DiabloNativeSarcophagusActorState.Emerging) {
            if (SpecialFrameIndex < 0 || SpecialFrameIndex >= byte.MaxValue
                || SpecialTickCounter >= byte.MaxValue)
                throw new InvalidDataException("A sarcophagus actor has an invalid SpecialStand cursor.");
        } else if (SpecialFrameIndex != 0 || SpecialTickCounter != 0) {
            throw new InvalidDataException("A completed sarcophagus actor retained a SpecialStand cursor.");
        }
    }

    private uint AnimationLengthBound => InitialAnimationLength;

    private static int GetExpectedFacing(int originX, int originY, int x, int y)
    {
        var offset = (x - originX, y - originY);
        if (offset == (0, 0))
            return 1;
        var direction = offset switch {
            (-1, -1) => 4,
            (0, -1) => 5,
            (1, -1) => 6,
            (-1, 0) => 3,
            (1, 0) => 7,
            (-1, 1) => 2,
            (0, 1) => 1,
            (1, 1) => 0,
            _ => -1,
        };
        return direction < 0 ? -1 : (direction + 4) % 8;
    }
}
