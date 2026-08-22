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
}
