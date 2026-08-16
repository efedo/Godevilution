using Devilution.Data;
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
        Assert.False(rules.HasTownPortalDestinationGeometry);
    }

    [Fact]
    public void ResolvesStableTownLevelSymbol()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("level", "town", 0),
        ]);
        var rules = AuthoritativeWorldRules.LoadTsv(
            "world_rules.tsv",
            "town_level_id\ttown_level_symbol\tadria_store_id\n0\ttown\t73\n",
            stableIds);

        Assert.Equal(0U, rules.TownLevelId);
        Assert.Equal(73U, rules.AdriaStoreId);
    }

    [Fact]
    public void LoadsExternalTownPortalDestinationGeometry()
    {
        var rules = AuthoritativeWorldRules.LoadTsv(
            "world_rules.tsv",
            "town_level_id\tadria_store_id\ttown_portal_destination_position_x\t"
                + "town_portal_destination_position_y\ttown_portal_destination_slot_count\t"
                + "town_portal_destination_slot_spacing\n7\t73\t60\t41\t2\t4\n");

        Assert.True(rules.HasTownPortalDestinationGeometry);
        Assert.Equal(60, rules.TownPortalDestinationPositionX);
        Assert.Equal(41, rules.TownPortalDestinationPositionY);
        Assert.Equal(2, rules.TownPortalDestinationSlotCount);
        Assert.Equal(4, rules.TownPortalDestinationSlotSpacing);
    }

    [Fact]
    public void RejectsPartialTownPortalDestinationGeometry()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeWorldRules.LoadTsv(
            "world_rules.tsv",
            "town_level_id\ttown_portal_destination_position_x\n7\t60\n"));
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
    public void ValidatesExternalTownPortalDestinationSlotsAgainstWorldGeometry()
    {
        var rules = new AuthoritativeWorldRules(7, 73, 2, 3, 2, 2) {
            HasTownPortalDestinationGeometry = true,
        };
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(7, 8, 8));

        rules.ValidateWorld(world);
    }

    [Fact]
    public void RejectsExternalTownPortalDestinationOutsideWorldGeometry()
    {
        var rules = new AuthoritativeWorldRules(7, 73, 7, 3, 2, 1) {
            HasTownPortalDestinationGeometry = true,
        };
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(7, 8, 8));

        Assert.Throws<InvalidDataException>(() => rules.ValidateWorld(world));
    }

    [Fact]
    public void RejectsExternalTownPortalDestinationOnBlockedCell()
    {
        var rules = new AuthoritativeWorldRules(7, 73, 2, 3, 1, 0) {
            HasTownPortalDestinationGeometry = true,
        };
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(7, 8, 8, blockedCells: [26]));

        Assert.Throws<InvalidDataException>(() => rules.ValidateWorld(world));
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
