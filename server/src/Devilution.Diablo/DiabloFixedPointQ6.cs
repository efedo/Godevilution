using Devilution.Engine;

namespace Devilution.Diablo;

/** Backward-compatible Diablo facade over engine-native signed 26.6 arithmetic. */
public static class DiabloFixedPointQ6
{
    public const int FractionBits = DeterministicFixedPointQ6.FractionBits;

    public const int Scale = DeterministicFixedPointQ6.Scale;

    public static int FromWhole(int value) => DeterministicFixedPointQ6.FromWhole(value);

    public static int ToWholeTruncated(int raw)
        => DeterministicFixedPointQ6.ToWholeTruncated(raw);

    public static int ToWholeCeiling(int raw)
        => DeterministicFixedPointQ6.ToWholeCeiling(raw);
}
