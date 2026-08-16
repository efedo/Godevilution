using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldRulesTests
{
    [Fact]
    public void LoadsExternalTownLevelIdentity()
    {
        var rules = AuthoritativeWorldRules.LoadTsv(
            "world_rules.tsv",
            "town_level_id\n7\n");

        Assert.Equal(7U, rules.TownLevelId);
    }

    [Fact]
    public void ValidatesTownLevelAgainstWorldGeometry()
    {
        var rules = new AuthoritativeWorldRules(7);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(7, 8, 8));

        rules.ValidateWorld(world);

        var missing = new AuthoritativeWorldRules(8);
        Assert.Throws<InvalidDataException>(() => missing.ValidateWorld(world));
    }
}
