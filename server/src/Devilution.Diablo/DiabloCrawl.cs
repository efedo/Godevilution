using Devilution.Engine;

namespace Devilution.Diablo;

/**
 * Source-compatible facade for callers that still use the Diablo module
 * surface. Authoritative Diablo paths call <see cref="DeterministicGridCrawl"/>
 * directly so reusable crawl behavior remains engine-owned.
 */
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
