using Devilution.Diablo;
using Devilution.Data;
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
    public void LoadsOptionalOrdinaryMonsterCapacity()
    {
        var catalog = AuthoritativeLevelGenerationCatalog.LoadTsv(
            "level_generation.tsv",
            "minimum_level\tmaximum_level\tmonster_density_divisor\tmultiplayer_monster_multiplier_percent\ttrap_percent\tordinary_monster_capacity\n"
                + "1\t1\t30\t150\t10\t17\n");

        Assert.Equal(17, catalog.GetRequired(1).OrdinaryMonsterCapacity);
    }

    [Fact]
    public void ProductionValidationRequiresCompleteOrderedSchema()
    {
        var complete = TsvTable.Parse(
            "level_generation.tsv",
            "minimum_level\tmaximum_level\tmonster_density_divisor\tmultiplayer_monster_multiplier_percent\ttrap_percent\tordinary_monster_capacity\n"
                + "0\t0\t30\t150\t0\t190\n");

        AuthoritativeLevelGenerationCatalog.ValidateExternalizedProduction(complete);

        var legacy = TsvTable.Parse(
            "level_generation.tsv",
            "minimum_level\tmaximum_level\tmonster_density_divisor\tmultiplayer_monster_multiplier_percent\ttrap_percent\n"
                + "0\t0\t30\t150\t0\n");
        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeLevelGenerationCatalog.ValidateExternalizedProduction(legacy));
        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
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
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(0, 1, 30, 150, 10) {
                OrdinaryMonsterCapacity = 0,
            },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(0, 1, 30, 150, 10) {
                OrdinaryMonsterCapacity = 201,
            },
        ]));
    }

    [Fact]
    public void CalculatesDeterministicMonsterCountsFromWalkableCells()
    {
        var catalog = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 30, 150, 10),
        ]);

        Assert.Equal(2, catalog.CalculateMonsterCount(1, 60, multiplayer: false));
        Assert.Equal(3, catalog.CalculateMonsterCount(1, 60, multiplayer: true));
        Assert.Equal(0, catalog.CalculateMonsterCount(1, 29, multiplayer: false));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => catalog.CalculateMonsterCount(1, -1, multiplayer: false));
    }

    [Fact]
    public void ValidatesCoverageForEveryDeclaredWorldLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        world.AddLevel(new AuthoritativeLevel(3, 4, 4));

        var complete = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 30, 100, 0),
            new AuthoritativeLevelGenerationRule(3, 3, 30, 100, 0),
        ]);
        complete.ValidateWorld(world);

        var incomplete = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 30, 100, 0),
        ]);
        Assert.Throws<InvalidDataException>(() => incomplete.ValidateWorld(world));
    }
}
