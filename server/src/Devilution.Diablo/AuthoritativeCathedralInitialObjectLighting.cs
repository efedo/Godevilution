namespace Devilution.Diablo;

/** Static lights created by ordinary fresh InitObjects, before encounters/themes. */
internal static class AuthoritativeCathedralInitialObjectLighting
{
    internal static void Apply(Span<byte> light, Span<byte> preLight,
        AuthoritativeCathedralPopulationResult population)
        => ApplyCore(light, preLight, population, catacombs: false);

    /** L2 torches and the L8 story candles use direct lighting before SavePreLighting. */
    internal static void ApplyCatacombs(Span<byte> light, Span<byte> preLight,
        AuthoritativeCathedralPopulationResult population)
        => ApplyCore(light, preLight, population, catacombs: true);

    private static void ApplyCore(Span<byte> light, Span<byte> preLight,
        AuthoritativeCathedralPopulationResult population, bool catacombs)
    {
        ArgumentNullException.ThrowIfNull(population);
        if (light.Length != AuthoritativeCathedralPopulationGeometry.CellCount
            || preLight.Length != AuthoritativeCathedralPopulationGeometry.CellCount)
            throw new InvalidDataException("Initial object lighting requires complete native tile channels.");

        // Initial Cathedral lights and depth-four story candles call
        // AddObjectLight regardless of their raw applyLighting flag. Normal
        // InitObjects is not LoadingMapObjects: neither pre-light nor the
        // indexed light allocator changes. Only allocated objects contribute.
        foreach (var slot in population.ActiveObjectIds.Take(population.ActiveObjectCount)) {
            var value = population.Objects[slot];
            var radius = value.Type switch {
                0 when !catacombs => 5,
                >= 44 and <= 47 when catacombs => 8,
                87 => 3,
                _ => -1,
            };
            if (radius >= 0)
                AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight,
                    value.Position, radius, loadingMapObjects: false);
        }
    }
}
