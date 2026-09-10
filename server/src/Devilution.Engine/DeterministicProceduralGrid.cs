namespace Devilution.Engine;

/** Immutable blocked-cell layers produced by the deterministic grid generator. */
public sealed record DeterministicProceduralGridLayout(
    int Width,
    int Height,
    IReadOnlyList<int> BlockedCells,
    IReadOnlyList<int> MissileBlockedCells);

/** Native-compatible deterministic lattice generation independent of game state. */
public static class DeterministicProceduralGrid
{
    /**
     * Generates a connected layout from a caller-owned seed and density
     * percentages. Pocket cells are sampled from a four-cell lattice so the
     * generated walkable corridors remain connected at every supported density.
     * Required walkable cells are cleared from both collision layers after
     * sampling so authored entry points remain usable without changing the
     * seed stream.
     */
    public static DeterministicProceduralGridLayout Generate(
        int width,
        int height,
        uint seed,
        int blockedPercent,
        int missileBlockedPercent,
        IReadOnlyCollection<int>? requiredWalkableCells = null)
    {
        if (width < 3 || height < 3 || width > 512 || height > 512)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (blockedPercent is < 0 or > 80)
            throw new ArgumentOutOfRangeException(nameof(blockedPercent));
        if (missileBlockedPercent is < 0 or > 80)
            throw new ArgumentOutOfRangeException(nameof(missileBlockedPercent));

        if (requiredWalkableCells is not null
            && requiredWalkableCells.Any(cell => cell < 0 || cell >= width * height))
            throw new ArgumentOutOfRangeException(
                nameof(requiredWalkableCells),
                "Required walkable cells must be within the generated grid.");

        var blocked = new HashSet<int>();
        var missileBlocked = new HashSet<int>();
        for (var y = 1; y < height - 1; y++) {
            for (var x = 1; x < width - 1; x++) {
                // Keep a one-cell lattice of corridors and only block pocket
                // cells adjacent to that lattice, so every generated map
                // remains connected even when the authored density is high.
                if (x % 4 != 2 || y % 4 != 2)
                    continue;
                var cell = y * width + x;
                var hash = DeterministicHash.Mix32(
                    seed + checked((uint)cell) * 0x9E3779B9U);
                if (hash % 100U < (uint)blockedPercent)
                    blocked.Add(cell);
                var missileHash = DeterministicHash.Mix32(
                    seed + checked((uint)cell) * 0x9E3779B9U ^ 0xA511E9B3U);
                if (missileHash % 100U < (uint)missileBlockedPercent)
                    missileBlocked.Add(cell);
            }
        }
        missileBlocked.UnionWith(blocked);
        if (requiredWalkableCells is not null) {
            blocked.ExceptWith(requiredWalkableCells);
            missileBlocked.ExceptWith(requiredWalkableCells);
        }
        return new DeterministicProceduralGridLayout(
            width,
            height,
            Array.AsReadOnly(blocked.OrderBy(cell => cell).ToArray()),
            Array.AsReadOnly(missileBlocked.OrderBy(cell => cell).ToArray()));
    }

}
