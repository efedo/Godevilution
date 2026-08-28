namespace Devilution.Engine;

/** Deterministic collection ordering helpers for authoritative simulation. */
public static class DeterministicRandomOrdering
{
    /**
     * Applies the native-compatible Fisher–Yates order using a caller-owned
     * bounded random source. The callback receives an exclusive upper bound.
     */
    public static void Shuffle<T>(IList<T> values, Func<int, int> nextIndex)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(nextIndex);
        for (var index = values.Count - 1; index > 0; index--) {
            var swapIndex = nextIndex(index + 1);
            if (swapIndex < 0 || swapIndex > index)
                throw new InvalidOperationException(
                    "The deterministic shuffle source returned an invalid index.");
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
