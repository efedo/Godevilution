using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeItemHellfireEffectCatalogTests
{
    [Fact]
    public void LoadsHellfireEffectFlagsAndAliases()
    {
        var catalog = AuthoritativeNativeItemHellfireEffectCatalog.LoadTsv(
            "native_item_hellfire_effects.tsv",
            "symbol\tnative_flag\n"
                + "Doppelganger\t16\n"
                + "ACAgainstDemons\t32\n"
                + "ACDEMON\t32\n");

        Assert.Equal(3, catalog.Definitions.Count);
        Assert.Equal(16, catalog.GetRequiredFlag(" doppelganger "));
        Assert.Equal(32, catalog.GetRequiredFlag("ACDEMON"));
        Assert.False(catalog.TryGetFlag("MissingEffect", out _));
    }

    [Fact]
    public void RejectsDuplicateSymbolsAndOutOfRangeFlags()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemHellfireEffectCatalog([
                new("Doppelganger", 16),
                new("doppelganger", 32),
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemHellfireEffectCatalog([
                new("TooLarge", 128),
            ]));
    }

    [Fact]
    public void NormalizedItemLoadingUsesExternalHellfireEffectNamespaceWhenProvided()
    {
        var catalog = new AuthoritativeNativeItemHellfireEffectCatalog([
            new("CustomEffect", 8),
        ]);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\thellfire_effects\n"
                + "7\t1\t1\t1\tCustomEffect\n",
            nativeItemHellfireEffectCatalog: catalog);

        Assert.Equal(8, items.GetRequired(7).HellfireDamageArmorFlags);
    }

    [Fact]
    public void UniqueItemHellfirePowersUseTheExternalEffectNamespaceWhenProvided()
    {
        var effects = new AuthoritativeNativeItemHellfireEffectCatalog([
            new("ACDEMON", 2),
            new("ACUNDEAD", 4),
        ]);
        var uniques = new AuthoritativeUniqueItemCatalog(
            [
                new AuthoritativeUniqueItemDefinition(
                    1001,
                    "Ward",
                    7,
                    1,
                    1,
                    [
                        new AuthoritativeUniqueItemPower("ACDEMON", 0, 0),
                        new AuthoritativeUniqueItemPower("ACUNDEAD", 0, 0),
                    ]),
            ],
            nativeItemHellfireEffectCatalog: effects);
        var items = new AuthoritativeItemCatalog(
            [new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, true)],
            uniqueItems: uniques);

        var state = uniques.Generate(items, 1001, 42, 1);

        Assert.Equal(6, state.HellfireDamageArmorFlags);
    }
}
