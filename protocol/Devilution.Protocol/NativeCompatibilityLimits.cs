namespace Devilution.Shared;

/** Native compatibility limits shared by the protocol and game modules. */
public static class NativeCompatibilityLimits
{
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

    /** Maximum native monster short AI counter retained by authoritative snapshots. */
    public const int MaximumNativeMonsterShortTimerTicks = 64;

    /** Maximum native monster retreat counter retained by authoritative snapshots. */
    public const int MaximumNativeMonsterRetreatTicks = MaximumNativeMonsterShortTimerTicks;
}
