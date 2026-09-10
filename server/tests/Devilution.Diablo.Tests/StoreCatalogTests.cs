using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class StoreCatalogTests
{
    [Fact]
    public void ResolvesStableStoreSymbolsAndRejectsMismatches()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("store", "smith", 1),
        ]);
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_symbol\tstore_slot\titem_seed\tprice\n"
                + "1\tsmith\t0\t42\t75\n",
            stableIds: stableIds);

        Assert.True(catalog.ContainsStore(1));
        var exception = Assert.Throws<InvalidDataException>(() => StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_symbol\tstore_slot\titem_seed\tprice\n"
                + "2\tsmith\t0\t42\t75\n",
            stableIds: stableIds));
        Assert.Contains("maps store 2 to symbol 'smith'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsExplicitVendorRoleAndRetainsItWhenStockIsReplaced()
    {
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_symbol\tstore_role\tstore_slot\titem_seed\tprice\n"
                + "3\twirt\tWirt\t0\t42\t75\n");

        Assert.True(catalog.TryGetRole(3, out var role));
        Assert.Equal(AuthoritativeStoreRole.Wirt, role);

        catalog.ReplaceStore(3, [new StoreItem(1, 43, 80)]);
        Assert.True(catalog.TryGetRole(3, out role));
        Assert.Equal(AuthoritativeStoreRole.Wirt, role);
    }

    [Fact]
    public void ConflictingVendorRolesAreRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() => StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_role\tstore_slot\titem_seed\tprice\n"
                + "1\tSmith\t0\t42\t75\n"
                + "1\tWitch\t1\t43\t80\n"));

        Assert.Contains("conflicting store roles", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownLegacyStoreSymbolsRemainGeneric()
    {
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_symbol\tstore_slot\titem_seed\tprice\n"
                + "99\tcustom_event_store\t0\t42\t75\n");

        Assert.True(catalog.TryGetRole(99, out var role));
        Assert.Equal(AuthoritativeStoreRole.Generic, role);
    }

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

    [Fact]
    public void SpellStaffPricingMustResolveInTheActiveSpellCatalog()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0),
        ]);
        var validPricing = StoreServicePricing.LoadTsv(
            "store_services.tsv",
            "key\tspell_id\tvalue\nspell_staff_cost\t7\t100\n");

        validPricing.ValidateSpellReferences(spells);
        Assert.Equal(100U, validPricing.GetSpellStaffCost(7));

        var invalidPricing = StoreServicePricing.LoadTsv(
            "store_services.tsv",
            "key\tspell_id\tvalue\nspell_staff_cost\t8\t100\n");
        var exception = Assert.Throws<InvalidDataException>(() =>
            invalidPricing.ValidateSpellReferences(spells));
        Assert.Contains("Store service pricing references unknown spell 8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionValidationRequiresEveryServicePricingKey()
    {
        var table = TsvTable.Parse(
            "store_services.tsv",
            "key\tspell_id\tvalue\n"
                + "sale_divisor\t\t4\n"
                + "spell_staff_cost\t0\t0\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            StoreServicePricing.ValidateExternalizedProduction(table));

        Assert.Contains("missing required keys", exception.Message, StringComparison.Ordinal);
        Assert.Contains("normal_repair_divisor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionValidationAcceptsExplicitSpellStaffPricingRow()
    {
        var table = TsvTable.Parse(
            "store_services.tsv",
            "key\tspell_id\tvalue\n"
                + "sale_divisor\t\t4\n"
                + "normal_repair_divisor\t\t2\n"
                + "magical_repair_percent\t\t30\n"
                + "magical_repair_divisor\t\t200\n"
                + "identification_price\t\t100\n"
                + "recharge_divisor\t\t2\n"
                + "mana_chunk\t\t64\n"
                + "spell_staff_cost\t0\t0\n");

        StoreServicePricing.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void SpellStaffPricingResolvesStableSymbolsAndRejectsMismatches()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "fireball", 7),
        ]);
        var validPricing = StoreServicePricing.LoadTsv(
            "store_services.tsv",
            "key\tspell_id\tspell_symbol\tvalue\nspell_staff_cost\t7\tfireball\t100\n",
            stableIds);

        Assert.Equal(100U, validPricing.GetSpellStaffCost(7));

        var exception = Assert.Throws<InvalidDataException>(() =>
            StoreServicePricing.LoadTsv(
                "store_services.tsv",
                "key\tspell_id\tspell_symbol\tvalue\nspell_staff_cost\t8\tfireball\t100\n",
                stableIds));
        Assert.Contains("maps spell 8 to symbol 'fireball'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpellStaffPricingRejectsUnknownStableSymbols()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "fireball", 7),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            StoreServicePricing.LoadTsv(
                "store_services.tsv",
                "key\tspell_id\tspell_symbol\tvalue\nspell_staff_cost\t0\tmissing\t100\n",
                stableIds));
        Assert.Contains("references unknown stable spell symbol 'missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativeSpellStaffPricingIdentitiesAreRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            StoreServicePricing.LoadTsv(
                "store_services.tsv",
                "key\tspell_id\tvalue\nspell_staff_cost\t-1\t100\n"));
        Assert.Contains("contains a negative spell -1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectStoreStockSpellReferencesMustResolveInTheActiveCatalog()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0),
        ]);
        var validStores = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\tspell_id\n1\t0\t42\t75\t7\n");

        validStores.ValidateSpellReferences(spells);

        var invalidStores = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\tspell_id\n1\t0\t42\t75\t8\n");
        var exception = Assert.Throws<InvalidDataException>(() =>
            invalidStores.ValidateSpellReferences(spells));
        Assert.Contains("Store 1 slot 0 references unknown spell 8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectStoreSpellReferencesResolveStableSymbolsAndRejectMismatches()
    {
        var stableIds = new StableContentIdCatalog([
            new StableContentId("spell", "fireball", 7),
        ]);
        var validStores = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\tspell_id\tspell_symbol\n"
                + "1\t0\t42\t75\t7\tfireball\n",
            stableIds: stableIds);

        Assert.True(validStores.TryGetItem(1, 0, out var item));
        Assert.Equal(7, item.State.SpellId);

        var exception = Assert.Throws<InvalidDataException>(() => StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\tspell_id\tspell_symbol\n"
                + "1\t0\t42\t75\t8\tfireball\n",
            stableIds: stableIds));
        Assert.Contains("maps spell 8 to symbol 'fireball'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratesDynamicBookStockFromAValidatedBookTemplate()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);

        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_id\titem_level\n"
                + "1\t0\t42\t100\t114\t1\n",
            items,
            spellBooks: books);

        var item = Assert.Single(catalog.GetItems(1));
        Assert.Equal(7, item.State.SpellId);
        Assert.Equal(DiabloItemActionKind.ReadBook, item.State.ItemActionKind);
        Assert.Equal("Book of Firebolt", item.State.BaseName);
        Assert.Equal(100, item.State.Value);
    }

    [Fact]
    public void MultiplayerStoreRetainsHealOtherBookWhileSinglePlayerExcludesIt()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(34, 1, 0, 0, 0, 0) { NativeSpellId = 34 },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                34,
                "Heal Other",
                1,
                25,
                AuthoritativeSpellBookMagicType.Magic,
                17),
        ]);
        books.Validate(spells, items);
        const string contents = "store_id\tstore_slot\titem_seed\tprice\titem_id\titem_level\n"
            + "1\t0\t42\t100\t114\t1\n";

        Assert.Throws<InvalidDataException>(() => StoreCatalog.LoadTsv(
            "stores.tsv",
            contents,
            items,
            spellBooks: books,
            gameMode: DiabloGameMode.SinglePlayer));

        var multiplayer = StoreCatalog.LoadTsv(
            "stores.tsv",
            contents,
            items,
            spellBooks: books,
            gameMode: DiabloGameMode.Multiplayer);
        var item = Assert.Single(multiplayer.GetItems(1));
        Assert.Equal(34, item.State.SpellId);
    }
}
