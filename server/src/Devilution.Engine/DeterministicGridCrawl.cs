namespace Devilution.Engine;

/** Native-compatible bounded nearest-position search on an integer grid. */
public static class DeterministicGridCrawl
{
    /** Maximum radius accepted by the native crawl contract. */
    public const int MaximumSupportedRadius = 50;

    /** Finds the first valid position in native crawl order. */
    public static (int X, int Y)? FindClosestValidPosition(
        int startingX,
        int startingY,
        int minimumRadius,
        int maximumRadius,
        Func<int, int, bool> isValid)
    {
        ArgumentNullException.ThrowIfNull(isValid);
        ValidateRadius(minimumRadius, maximumRadius);

        foreach (var displacement in Enumerate(minimumRadius, maximumRadius)) {
            int candidateX;
            int candidateY;
            try {
                candidateX = checked(startingX + displacement.X);
                candidateY = checked(startingY + displacement.Y);
            } catch (OverflowException) {
                // Malformed caller coordinates cannot produce a valid grid cell.
                // Skip them instead of wrapping or faulting the authoritative loop.
                continue;
            }
            if (isValid(candidateX, candidateY))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Enumerates native crawl displacements in stable order for multi-target rules. */
    public static IEnumerable<(int X, int Y)> EnumerateDisplacements(
        int minimumRadius,
        int maximumRadius)
    {
        ValidateRadius(minimumRadius, maximumRadius);
        return Enumerate(minimumRadius, maximumRadius);
    }

    private static void ValidateRadius(int minimumRadius, int maximumRadius)
    {
        if (minimumRadius < 0
            || maximumRadius < minimumRadius
            || maximumRadius > MaximumSupportedRadius) {
            throw new ArgumentOutOfRangeException(nameof(maximumRadius));
        }
    }

    private static IEnumerable<(int X, int Y)> Enumerate(
        int minimumRadius,
        int maximumRadius)
    {
        for (var radius = minimumRadius; radius <= maximumRadius; radius++) {
            yield return (0, radius);
            if (radius == 0)
                continue;
            yield return (0, -radius);
            for (var x = 1; x < radius; x++) {
                yield return (-x, radius);
                yield return (x, radius);
                yield return (-x, -radius);
                yield return (x, -radius);
            }
            if (radius > 1) {
                var diagonal = radius - 1;
                yield return (-diagonal, diagonal);
                yield return (diagonal, diagonal);
                yield return (-diagonal, -diagonal);
                yield return (diagonal, -diagonal);
            }
            yield return (-radius, 0);
            yield return (radius, 0);
            for (var y = 1; y < radius; y++) {
                yield return (-radius, y);
                yield return (radius, y);
                yield return (-radius, -y);
                yield return (radius, -y);
            }
        }
    }
}
