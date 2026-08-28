namespace Devilution.Engine;

/** Deterministic placement helpers for authored multi-cell geometry. */
public static class DeterministicGridPlacement
{
    /**
     * Selects one aligned walkable footprint in stable row-major order.
     * Throws when the requested footprint cannot fit or no candidate is
     * walkable; callers can translate that failure into content diagnostics.
     */
    public static (int X, int Y) SelectAlignedWalkableFootprint(
        int width,
        int height,
        int footprintWidth,
        int footprintHeight,
        int alignment,
        uint seed,
        Func<int, int, bool> isWalkable)
    {
        ArgumentNullException.ThrowIfNull(isWalkable);
        if (width < 1 || height < 1
            || footprintWidth < 1 || footprintHeight < 1
            || footprintWidth > width || footprintHeight > height
            || alignment < 1) {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var candidates = new List<(int X, int Y)>();
        var maxX = width - footprintWidth;
        var maxY = height - footprintHeight;
        for (var y = 0; y <= maxY; y += alignment) {
            for (var x = 0; x <= maxX; x += alignment) {
                if (IsWalkableFootprint(
                        x,
                        y,
                        footprintWidth,
                        footprintHeight,
                        isWalkable)) {
                    candidates.Add((x, y));
                }
            }
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException(
                "No aligned walkable footprint is available.");
        return candidates[(int)(seed % (uint)candidates.Count)];
    }

    private static bool IsWalkableFootprint(
        int originX,
        int originY,
        int width,
        int height,
        Func<int, int, bool> isWalkable)
    {
        for (var y = originY; y < originY + height; y++) {
            for (var x = originX; x < originX + width; x++) {
                if (!isWalkable(x, y))
                    return false;
            }
        }
        return true;
    }
}
