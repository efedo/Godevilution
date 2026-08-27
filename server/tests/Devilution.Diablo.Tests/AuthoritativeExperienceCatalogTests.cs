using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeExperienceCatalogTests
{
    [Fact]
    public void LoadsContiguousExperienceThresholds()
    {
        var catalog = AuthoritativeExperienceCatalog.LoadTsv(
            "experience.tsv",
            "level\texperience\n"
                + "1\t2000\n"
                + "2\t4620\n"
                + "3\t8040\n");

        Assert.Equal(3, catalog.Definitions.Count);
        Assert.Equal(2000U, catalog.GetRequiredExperience(1));
        Assert.Equal(4620U, catalog.GetRequiredExperience(2));
        Assert.False(catalog.TryGetRequiredExperience(4, out _));
    }

    [Fact]
    public void RejectsGapsAndNonIncreasingThresholds()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeExperienceCatalog([
            new(1, 2000),
            new(3, 8040),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeExperienceCatalog([
            new(1, 2000),
            new(2, 2000),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeExperienceCatalog([
            new(1, 0),
        ]));
    }
}
