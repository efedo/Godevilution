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
}
