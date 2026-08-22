using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeItemEffectCatalogTests
{
    [Fact]
    public void LoadsNativeEffectFlags()
    {
        var catalog = AuthoritativeNativeItemEffectCatalog.LoadTsv(
            "native_item_effects.tsv",
            "symbol\tnative_flag\n"
                + "CustomEffect\t8\n"
                + "ZeroResistance\t2147483648\n");

        Assert.Equal(2, catalog.Definitions.Count);
        Assert.Equal(8U, catalog.GetRequiredFlag(" customeffect "));
        Assert.Equal(2147483648U, catalog.GetRequiredFlag("ZeroResistance"));
        Assert.False(catalog.TryGetFlag("MissingEffect", out _));
    }

    [Fact]
    public void RejectsDuplicateAndZeroFlags()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemEffectCatalog([
                new("FireDamage", 16),
                new("firedamage", 32),
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemEffectCatalog([
                new("Empty", 0),
            ]));
    }

    [Fact]
    public void LegacyItemLoadingUsesExternalEffectNamespaceWhenProvided()
    {
        var catalog = new AuthoritativeNativeItemEffectCatalog([
            new("CustomEffect", 8),
        ]);
        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                + "TEST\t0\tMisc\tUnequippable\tNONE\tMisc\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\tCustomEffect\tNONE\tNull\tfalse\t1\n",
            nativeItemEffectCatalog: catalog);

        Assert.Equal(8U, items.GetRequired(1).Flags);
    }

    [Fact]
    public void NormalizedItemLoadingUsesExternalEffectNamespaceWhenProvided()
    {
        var catalog = new AuthoritativeNativeItemEffectCatalog([
            new("CustomEffect", 8),
        ]);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tspecial_effects\n"
                + "7\t1\t1\t1\tCustomEffect\n",
            nativeItemEffectCatalog: catalog);

        Assert.Equal(8U, items.GetRequired(7).Flags);
    }
}
