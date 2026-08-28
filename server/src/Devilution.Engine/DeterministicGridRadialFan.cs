namespace Devilution.Engine;

/** Native-compatible radial fan-out ordering for symmetric grid projectiles. */
public static class DeterministicGridRadialFan
{
    /**
     * Enumerates mirrored quarter-ring offsets in native order, truncated to
     * the requested projectile count.
     */
    public static IReadOnlyList<(int X, int Y)> Enumerate(
        int radius,
        int projectileCount)
    {
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (projectileCount < 0)
            throw new ArgumentOutOfRangeException(nameof(projectileCount));

        var offsets = new List<(int X, int Y)>(projectileCount);
        for (var y = 0; y <= radius && offsets.Count < projectileCount; y++)
            AddQuarterRing(offsets, radius, y, projectileCount);
        for (var x = radius - 1; x >= 0 && offsets.Count < projectileCount; x--)
            AddQuarterRing(offsets, x, radius, projectileCount);
        return Array.AsReadOnly(offsets.ToArray());
    }

    private static void AddQuarterRing(
        ICollection<(int X, int Y)> offsets,
        int x,
        int y,
        int projectileCount)
    {
        var quarter = (X: x, Y: y);
        var candidates = new[] {
            quarter,
            (-quarter.X, -quarter.Y),
            (-quarter.X, quarter.Y),
            (quarter.X, -quarter.Y),
        };
        foreach (var offset in candidates) {
            if (offsets.Count >= projectileCount)
                return;
            offsets.Add(offset);
        }
    }
}
