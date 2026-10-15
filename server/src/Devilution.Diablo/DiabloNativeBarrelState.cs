namespace Devilution.Diablo;

/** Lifecycle of the exact native monster slot linked to a barrel. */
public enum DiabloNativeBarrelActorState
{
    None = 0,
    Reserved = 1,
    Emerging = 2,
    Active = 3,
    Dead = 4,
}

/** Complete mutable projection for one immutable generated native barrel row. */
public sealed record DiabloNativeBarrelState(
    int ObjectSlot,
    uint EntityId,
    int NativeTypeId,
    int InitialPositionX,
    int InitialPositionY,
    uint RandomSeed,
    int Var1,
    int Var2,
    int Var3,
    int Var4,
    int InitialSelectionRegion,
    uint InitialAnimationFrame,
    uint InitialAnimationFlag,
    int InitialAnimationDelay,
    int InitialAnimationCount,
    uint InitialAnimationLength,
    bool InitialSolidFlag,
    bool InitialMissilePassable,
    int InitialBreakFlag,
    bool InitialPreFlag,
    bool TrapArmed,
    bool HasLinkedTrap,
    bool Broken,
    uint AnimationFlag,
    uint AnimationFrame,
    int AnimationDelay,
    int AnimationCount,
    bool SolidFlag,
    bool MissilePassable,
    int BreakFlag,
    int SelectionRegion,
    bool PreFlag,
    int LinkedMonsterSlot,
    uint LinkedMonsterEntityId,
    int LinkedMonsterTypeId,
    DiabloNativeBarrelActorState ActorState,
    int SpawnPositionX,
    int SpawnPositionY,
    int SpawnDirection,
    int SpecialFrameIndex,
    int SpecialTickCounter)
{
    internal void ValidateShape()
    {
        if (ObjectSlot is < 0 or >= AuthoritativeCathedralPopulationParameters.ObjectCapacity
            || EntityId == 0 || NativeTypeId is not (57 or 58)
            || InitialPositionX is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || InitialPositionY is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || InitialSelectionRegion is < 1 or > 7
            || InitialAnimationFlag != 0
            || InitialAnimationDelay < 1
            || InitialAnimationCount < 0 || InitialAnimationCount >= InitialAnimationDelay
            || InitialAnimationLength is 0 or > byte.MaxValue
            || InitialAnimationFrame > InitialAnimationLength
            || !InitialSolidFlag || TrapArmed || HasLinkedTrap
            || Var1 < 0 || Var2 < 0 || Var3 < 0 || Var4 < -1
            || (NativeTypeId == 57 && (Var2 > 9 || Var3 > 2))
            || (NativeTypeId == 58 && Var2 != 0)
            || (NativeTypeId == 57 && ((Var2 >= 8 && Var4 >= 0) != (LinkedMonsterSlot >= 0)))
            || (LinkedMonsterSlot >= 0 && Var4 != LinkedMonsterSlot)
            || AnimationFlag is > 1 || AnimationDelay < 1 || AnimationCount < 0
            || AnimationFrame is 0 or > byte.MaxValue
            || LinkedMonsterSlot is < -1 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || (LinkedMonsterSlot == -1) != (LinkedMonsterEntityId == 0)
            || (LinkedMonsterSlot == -1) != (LinkedMonsterTypeId == -1)
            || (LinkedMonsterSlot >= 0 && LinkedMonsterTypeId < 0)
            || !Enum.IsDefined(ActorState)
            || SpawnDirection is < 0 or > 7
            || SpecialFrameIndex < 0 || SpecialTickCounter < 0)
            throw new InvalidDataException("A native barrel lifecycle record is outside its bounded schema.");

        if (!Broken) {
            if (AnimationFlag != InitialAnimationFlag || AnimationFrame != InitialAnimationFrame
                || AnimationDelay != InitialAnimationDelay || AnimationCount != InitialAnimationCount
                || SolidFlag != InitialSolidFlag || MissilePassable != InitialMissilePassable
                || BreakFlag != InitialBreakFlag || SelectionRegion != InitialSelectionRegion
                || PreFlag != InitialPreFlag
                || ActorState != (LinkedMonsterSlot < 0
                    ? DiabloNativeBarrelActorState.None
                    : DiabloNativeBarrelActorState.Reserved)
                || SpawnPositionX != 0 || SpawnPositionY != 0 || SpawnDirection != 0
                || SpecialFrameIndex != 0 || SpecialTickCounter != 0)
                throw new InvalidDataException("An intact native barrel cannot carry break or actor state.");
        } else if (SolidFlag || !MissilePassable || BreakFlag != -1 || SelectionRegion != 0 || !PreFlag
            || AnimationFrame < 1 || AnimationFrame > InitialAnimationLength
            || AnimationFlag != 1
            || (AnimationDelay == 1 && AnimationCount != 0)
            || (AnimationDelay == 1000
                && (AnimationFrame != InitialAnimationLength || AnimationCount is < 1 or >= 1000))
            || AnimationDelay is not (1 or 1000)) {
            throw new InvalidDataException("A broken native barrel lost its exact one-way source mutation.");
        }

        if (ActorState is DiabloNativeBarrelActorState.None or DiabloNativeBarrelActorState.Reserved) {
            if ((ActorState == DiabloNativeBarrelActorState.None) != (LinkedMonsterSlot == -1)
                || SpawnPositionX != 0 || SpawnPositionY != 0 || SpawnDirection != 0
                || SpecialFrameIndex != 0 || SpecialTickCounter != 0)
                throw new InvalidDataException("An unspawned native barrel actor disposition is inconsistent.");
        } else if (!Broken || LinkedMonsterSlot < 0
            || SpawnPositionX is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || SpawnPositionY is < 0 or >= AuthoritativeCathedralThemeRoomState.Width) {
            throw new InvalidDataException("A spawned native barrel actor lacks its exact broken owner and location.");
        }

        if (ActorState is DiabloNativeBarrelActorState.Emerging or DiabloNativeBarrelActorState.Active
            or DiabloNativeBarrelActorState.Dead) {
            if (!IsAdjacentOrCenter(InitialPositionX, InitialPositionY, SpawnPositionX, SpawnPositionY))
                throw new InvalidDataException("A native barrel actor spawned outside the source activation neighborhood.");
            var expectedDirection = ExpectedSpawnDirection(
                InitialPositionX, InitialPositionY, SpawnPositionX, SpawnPositionY);
            if (expectedDirection < 0 || SpawnDirection != expectedDirection)
                throw new InvalidDataException("A native barrel actor retained a nonnative spawn direction.");
        }
        if (ActorState == DiabloNativeBarrelActorState.Emerging) {
            if (SpecialFrameIndex >= byte.MaxValue || SpecialTickCounter >= byte.MaxValue)
                throw new InvalidDataException("A barrel skeleton has an invalid SpecialStand cursor.");
        } else if (SpecialFrameIndex != 0 || SpecialTickCounter != 0) {
            throw new InvalidDataException("A completed barrel skeleton retained a SpecialStand cursor.");
        }
    }

    private static bool IsAdjacentOrCenter(int originX, int originY, int x, int y)
        => Math.Abs(originX - x) <= 1 && Math.Abs(originY - y) <= 1;

    private static int ExpectedSpawnDirection(int originX, int originY, int x, int y)
    {
        var offset = (x - originX, y - originY);
        if (offset == (0, 0))
            return 1;
        return offset switch {
            (-1, -1) => 0,
            (0, -1) => 1,
            (1, -1) => 2,
            (-1, 0) => 7,
            (1, 0) => 3,
            (-1, 1) => 6,
            (0, 1) => 5,
            (1, 1) => 4,
            _ => -1,
        };
    }
}
