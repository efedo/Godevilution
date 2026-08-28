namespace Devilution.Engine;

/** Stable 32-bit mixing functions for native-compatible deterministic data. */
public static class DeterministicHash
{
    /** Applies the engine's avalanche mix without platform-dependent state. */
    public static uint Mix32(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352DU;
        value ^= value >> 15;
        value *= 0x846CA68BU;
        return value ^ (value >> 16);
    }
}
