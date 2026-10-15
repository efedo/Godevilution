namespace Devilution.Diablo;

/** Immutable source facts for one safely operable native Cathedral sarcophagus. */
public sealed record AuthoritativeCathedralNativeSarcophagusFacts
{
    internal AuthoritativeCathedralNativeSarcophagusFacts(
        int objectSlot,
        uint entityId,
        int nativeTypeId,
        uint levelId,
        AuthoritativeCathedralPopulationPoint position,
        uint randomSeed,
        int var1,
        int var2,
        int selectionRegion,
        uint animationFrame,
        uint animationFlag,
        int animationDelay,
        int animationCount,
        uint animationLength,
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
        SelectionRegion = selectionRegion;
        AnimationFrame = animationFrame;
        AnimationFlag = animationFlag;
        AnimationDelay = animationDelay;
        AnimationCount = animationCount;
        AnimationLength = animationLength;
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
    public int SelectionRegion { get; }
    public uint AnimationFrame { get; }
    /** The original native flag value, retained without bool normalization. */
    public uint AnimationFlag { get; }
    public int AnimationDelay { get; }
    public int AnimationCount { get; }
    public uint AnimationLength { get; }
    public bool TrapArmed { get; }
    public bool HasLinkedTrap { get; }
    public AuthoritativeNativeMonsterInitialization? LinkedMonster { get; }
    /** Bestiary graphic slot 5; kept out of serialized actor initialization for v1-v5 compatibility. */
    public int NativeSpecialFrameCount { get; }
    public int NativeSpecialTicksPerFrame { get; }
}
