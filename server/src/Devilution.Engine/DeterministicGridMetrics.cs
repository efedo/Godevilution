namespace Devilution.Engine;

/** Overflow-safe coordinate metrics shared by authoritative grid systems. */
public static class DeterministicGridMetrics
{
    /** Returns native-compatible Chebyshev distance without int overflow. */
    public static long ChebyshevDistance(
        int firstX,
        int firstY,
        int secondX,
        int secondY)
        => Math.Max(
            Math.Abs((long)secondX - firstX),
            Math.Abs((long)secondY - firstY));
}
