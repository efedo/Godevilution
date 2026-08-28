namespace Devilution.Diablo;

/** Native-compatible nearest-position search used by Diablo gameplay rules. */
public static class DiabloCrawl
{
    public const int MaximumSupportedRadius = 50;

    public static (int X, int Y)? FindClosestValidPosition(
        int startingX,
        int startingY,
        int minimumRadius,
        int maximumRadius,
        Func<int, int, bool> isValid)
    {
        ArgumentNullException.ThrowIfNull(isValid);
        if (minimumRadius < 0
            || maximumRadius < minimumRadius
            || maximumRadius > MaximumSupportedRadius) {
            throw new ArgumentOutOfRangeException(nameof(maximumRadius));
        }

        foreach (var displacement in Enumerate(minimumRadius, maximumRadius)) {
            int candidateX;
            int candidateY;
            try {
                candidateX = checked(startingX + displacement.X);
                candidateY = checked(startingY + displacement.Y);
            } catch (OverflowException) {
                // Client-supplied target cells may be outside the integer
                // neighborhood. Skip overflowing candidates and fail closed
                // instead of disconnecting the authoritative session.
                continue;
            }
            if (isValid(candidateX, candidateY))
                return (candidateX, candidateY);
        }
        return null;
    }

    /** Enumerates native crawl displacements in stable order for multi-target spells. */
    public static IEnumerable<(int X, int Y)> EnumerateDisplacements(
        int minimumRadius,
        int maximumRadius)
    {
        if (minimumRadius < 0
            || maximumRadius < minimumRadius
            || maximumRadius > MaximumSupportedRadius) {
            throw new ArgumentOutOfRangeException(nameof(maximumRadius));
        }
        return Enumerate(minimumRadius, maximumRadius);
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
