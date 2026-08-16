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
            "town_level_id\tadria_store_id\n7\t73\n");

        Assert.Equal(7U, rules.TownLevelId);
        Assert.Equal(73U, rules.AdriaStoreId);
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

    [Fact]
    public void RejectsServiceStoreCollision()
    {
        var rules = new AuthoritativeWorldRules(0, 10);
        var stores = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\n10\t0\t1\t1\n");

        Assert.Throws<InvalidDataException>(() => rules.ValidateStores(stores));
    }

    [Fact]
    public void RejectsZeroServiceStoreIdentity()
    {
        var rules = new AuthoritativeWorldRules(0, 0);

        Assert.Throws<InvalidDataException>(() => rules.ValidateStores(new StoreCatalog()));
    }
}
