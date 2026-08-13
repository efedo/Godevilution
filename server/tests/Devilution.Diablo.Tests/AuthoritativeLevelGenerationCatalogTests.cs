using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeLevelGenerationCatalogTests
{
    [Fact]
    public void LoadsNativeLevelRangesAndResolvesBoundaries()
    {
        var catalog = AuthoritativeLevelGenerationCatalog.LoadTsv(
            "level_generation.tsv",
            "minimum_level\tmaximum_level\tmonster_density_divisor\tmultiplayer_monster_multiplier_percent\ttrap_percent\n"
                + "0\t0\t30\t150\t0\n"
                + "1\t4\t30\t150\t15\n");

        Assert.Equal(2, catalog.Definitions.Count);
        Assert.Equal(15, catalog.GetRequired(4).TrapPercent);
        Assert.False(catalog.TryGet(5, out _));
    }

    [Fact]
    public void RejectsOverlappingOrInvalidRanges()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(0, 2, 30, 150, 15),
            new AuthoritativeLevelGenerationRule(2, 4, 30, 150, 15),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(0, 1, 0, 150, 10),
        ]));
    }
}
