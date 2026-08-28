namespace Devilution.Engine;

/** Native-compatible signed 26.6 arithmetic reusable by authoritative modules. */
public static class DeterministicFixedPointQ6
{
    public const int FractionBits = 6;

    public const int Scale = 1 << FractionBits;

    public static int FromWhole(int value) => checked(value * Scale);

    public static int ToWholeTruncated(int raw) => raw / Scale;

    public static int ToWholeCeiling(int raw)
    {
        var whole = raw / Scale;
        return raw > 0 && raw % Scale != 0 ? checked(whole + 1) : whole;
    }
}
