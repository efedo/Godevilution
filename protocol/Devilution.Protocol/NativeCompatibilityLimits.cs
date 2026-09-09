namespace Devilution.Shared;

/** Native compatibility limits shared by the protocol and game modules. */
public static class NativeCompatibilityLimits
{
    /** Native carried-item slot counts retained by authoritative references. */
    public const int MaximumInventoryCount = 40;
    public const int MaximumEquipmentCount = 7;
    public const int MaximumBeltCount = 8;
    public const int StashPageCount = 100;
    public const int StashCellCount = 100;

    /** Highest stable spell identity retained by native-compatible snapshots. */
    public const uint MaximumStableSpellId = byte.MaxValue;

    /** Highest native SpellID retained by legacy item and replay fields. */
    public const uint MaximumNativeSpellId = 51;

    /** Highest native spell level retained by legacy spell-book fields. */
    public const uint MaximumNativeSpellLevel = 15;

    /** Maximum memorized-spell slots retained by the native player layout. */
    public const int MaximumMemorizedSpellCount = 51;

    /** Highest character level retained by native progression state. */
    public const uint MaximumNativeCharacterLevel = 50;

    /** Highest Diablo victory rank retained by native player state. */
    public const uint MaximumNativeDiabloKillLevel = byte.MaxValue;

    /** Highest Reflect charge count retained by native player state. */
    public const uint MaximumNativeReflectCharges = ushort.MaxValue;

    /** Maximum native monster AI timer retained by authoritative snapshots. */
    public const int MaximumNativeMonsterAiTimerTicks = 1024;

    /** Maximum native monster death-animation duration retained by snapshots. */
    public const int MaximumNativeMonsterDeathAnimationTicks = MaximumNativeMonsterAiTimerTicks;

    /** Maximum native monster experience reward retained by authoritative state. */
    public const uint MaximumNativeMonsterExperienceReward = 1_000_000;

    /** Maximum native monster attack accuracy retained by authoritative state. */
    public const int MaximumNativeMonsterToHit = byte.MaxValue;

    /** Maximum native monster intelligence retained by authoritative state. */
    public const int MaximumNativeMonsterIntelligence = byte.MaxValue;

    /** Maximum native monster direction ordinal retained by authoritative state. */
    public const int MaximumNativeMonsterDirection = 7;

    /** Maximum native monster pack size retained by authoritative state. */
    public const int MaximumNativeMonsterPackSize = byte.MaxValue;

    /** Maximum native monster activation duration retained by authoritative state. */
    public const int MaximumNativeMonsterActiveTicks = byte.MaxValue;

    /** Maximum native monster movement counter retained by authoritative state. */
    public const int MaximumNativeMonsterMovementTicks = byte.MaxValue;

    /** Minimum native monster movement mode retained by authoritative state. */
    public const int MinimumNativeMonsterMovementMode = -1;

    /** Maximum native monster movement mode retained by authoritative state. */
    public const int MaximumNativeMonsterMovementMode = 3;

    /** Minimum native monster movement turn retained by authoritative state. */
    public const int MinimumNativeMonsterMovementTurn = 0;

    /** Maximum native monster movement turn retained by authoritative state. */
    public const int MaximumNativeMonsterMovementTurn = 1;

    /** Maximum native Fallen stand animation frame retained by authoritative state. */
    public const int MaximumNativeMonsterFallenStandAnimationFrame = 127;

    /** Maximum native Fallen stand animation tick retained by authoritative state. */
    public const int MaximumNativeMonsterFallenStandAnimationTick = 127;

    /** Maximum native Fallen rally duration retained by authoritative state. */
    public const int MaximumNativeMonsterFallenRallyTicks = 4096;

    /** Minimum native monster resistance retained by authoritative state. */
    public const int MinimumNativeMonsterResistance = -100;

    /** Maximum native monster resistance retained by authoritative state. */
    public const int MaximumNativeMonsterResistance = 100;

    /** Maximum native monster short AI counter retained by authoritative snapshots. */
    public const int MaximumNativeMonsterShortTimerTicks = 64;

    /** Maximum native monster retreat counter retained by authoritative snapshots. */
    public const int MaximumNativeMonsterRetreatTicks = MaximumNativeMonsterShortTimerTicks;
}
