using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class StoreCatalogTests
{
    [Fact]
    public void DuplicateSlotsDoNotPartiallyRegisterAStore()
    {
        var catalog = new StoreCatalog();

        Assert.Throws<ArgumentException>(() => catalog.AddStore(1, [
            new StoreItem(0, 42, 75),
            new StoreItem(0, 43, 80),
        ]));

        catalog.AddStore(1, [new StoreItem(0, 42, 75)]);
        Assert.True(catalog.TryGetItem(1, 0, out var item));
        Assert.Equal(42U, item.ItemSeed);
    }

    [Fact]
    public void ItemStateIsRetainedInAuthoritativeStock()
    {
        var state = AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 };
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75, state)]);

        Assert.True(catalog.TryGetItem(1, 0, out var item));
        Assert.Equal(state, item.State);
    }

    [Fact]
    public void OptionalInventoryDimensionsAreLoadedAndDefaultToOneCell()
    {
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_type\tinventory_width\tinventory_height\n"
            + "1\t0\t42\t75\t1\t2\t3\n"
            + "1\t1\t43\t25\t1\t\t\n");

        Assert.True(catalog.TryGetItem(1, 0, out var multiCell));
        Assert.Equal(2, multiCell.State.InventoryWidth);
        Assert.Equal(3, multiCell.State.InventoryHeight);
        Assert.True(catalog.TryGetItem(1, 1, out var legacy));
        Assert.Equal(1, legacy.State.InventoryWidth);
        Assert.Equal(1, legacy.State.InventoryHeight);
    }

    [Fact]
    public void LoadsCompleteProtocolItemModifiersFromExternalData()
    {
        const string contents = "store_id\tstore_slot\titem_seed\tprice\titem_type\tplus_damage\tplus_magic\tspell_level_add\tstat_flag\tbuff\tinventory_width\tinventory_height\n"
            + "1\t0\t42\t75\t1\t7\t3\t2\t1\t4\t2\t3\n";

        var catalog = StoreCatalog.LoadTsv("stores.tsv", contents);
        Assert.True(catalog.TryGetItem(1, 0, out var item));
        Assert.Equal(7, item.State.PlusDamage);
        Assert.Equal(3, item.State.PlusMagic);
        Assert.Equal(2, item.State.SpellLevelAdd);
        Assert.True(item.State.StatFlag);
        Assert.Equal(4U, item.State.Buff);
        Assert.Equal(2, item.State.InventoryWidth);
        Assert.Equal(3, item.State.InventoryHeight);
    }
}
