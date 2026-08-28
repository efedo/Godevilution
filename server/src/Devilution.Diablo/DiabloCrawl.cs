using Devilution.Engine;

namespace Devilution.Diablo;

/** Native-compatible nearest-position search used by Diablo gameplay rules. */
public static class DiabloCrawl
{
    public const int MaximumSupportedRadius = DeterministicGridCrawl.MaximumSupportedRadius;

    public static (int X, int Y)? FindClosestValidPosition(
        int startingX,
        int startingY,
        int minimumRadius,
        int maximumRadius,
        Func<int, int, bool> isValid)
        => DeterministicGridCrawl.FindClosestValidPosition(
            startingX,
            startingY,
            minimumRadius,
            maximumRadius,
            isValid);

    /** Enumerates native crawl displacements in stable order for multi-target spells. */
    public static IEnumerable<(int X, int Y)> EnumerateDisplacements(
        int minimumRadius,
        int maximumRadius)
        => DeterministicGridCrawl.EnumerateDisplacements(minimumRadius, maximumRadius);
}
