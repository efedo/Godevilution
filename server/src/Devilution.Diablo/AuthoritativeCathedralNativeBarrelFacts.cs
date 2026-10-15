namespace Devilution.Diablo;

/** Immutable source facts for one operable native Cathedral L1 barrel. */
public sealed record AuthoritativeCathedralNativeBarrelFacts
{
    internal AuthoritativeCathedralNativeBarrelFacts(
        int objectSlot,
        uint entityId,
        int nativeTypeId,
        uint levelId,
        AuthoritativeCathedralPopulationPoint position,
        uint randomSeed,
        int var1,
        int var2,
        int var3,
        int var4,
        int selectionRegion,
        uint animationFrame,
        uint animationFlag,
        int animationDelay,
        int animationCount,
        uint animationLength,
        bool initialSolid,
        bool initialMissilePassable,
        int breakFlag,
        bool preFlag,
        bool trapArmed,
        bool hasLinkedTrap,
        AuthoritativeNativeMonsterInitialization? linkedMonster,
        int nativeSpecialFrameCount,
        int nativeSpecialTicksPerFrame)
    {
        ObjectSlot = objectSlot;
        EntityId = entityId;
        NativeTypeId = nativeTypeId;
        LevelId = levelId;
        PositionX = position.X;
        PositionY = position.Y;
        RandomSeed = randomSeed;
        Var1 = var1;
        Var2 = var2;
        Var3 = var3;
        Var4 = var4;
        SelectionRegion = selectionRegion;
        AnimationFrame = animationFrame;
        AnimationFlag = animationFlag;
        AnimationDelay = animationDelay;
        AnimationCount = animationCount;
        AnimationLength = animationLength;
        InitialSolid = initialSolid;
        InitialMissilePassable = initialMissilePassable;
        BreakFlag = breakFlag;
        PreFlag = preFlag;
        TrapArmed = trapArmed;
        HasLinkedTrap = hasLinkedTrap;
        LinkedMonster = linkedMonster;
        NativeSpecialFrameCount = nativeSpecialFrameCount;
        NativeSpecialTicksPerFrame = nativeSpecialTicksPerFrame;
    }

    public int ObjectSlot { get; }
    public uint EntityId { get; }
    public int NativeTypeId { get; }
    public uint LevelId { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public uint RandomSeed { get; }
    public int Var1 { get; }
    public int Var2 { get; }
    public int Var3 { get; }
    public int Var4 { get; }
    public int SelectionRegion { get; }
    public uint AnimationFrame { get; }
    public uint AnimationFlag { get; }
    public int AnimationDelay { get; }
    public int AnimationCount { get; }
    public uint AnimationLength { get; }
    public bool InitialSolid { get; }
    public bool InitialMissilePassable { get; }
    public int BreakFlag { get; }
    public bool PreFlag { get; }
    public bool TrapArmed { get; }
    public bool HasLinkedTrap { get; }
    public AuthoritativeNativeMonsterInitialization? LinkedMonster { get; }
    public int NativeSpecialFrameCount { get; }
    public int NativeSpecialTicksPerFrame { get; }

    public bool IsExplosive => NativeTypeId == AuthoritativeCathedralBarrelOperationGenerator.ExplosiveBarrelType;
}
