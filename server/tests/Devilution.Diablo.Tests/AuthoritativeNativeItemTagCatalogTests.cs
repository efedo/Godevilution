using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeItemTagCatalogTests
{
    [Fact]
    public void LoadsItemTypeTagsCaseInsensitively()
    {
        var catalog = AuthoritativeNativeItemTagCatalog.LoadTsv(
            "native_item_tags.tsv",
            "item_type\ttags\n"
                + "ring\tMisc,Ring\n"
                + "light armor\tArmor\n");

        Assert.Equal(2, catalog.Definitions.Count);
        Assert.Equal(
            ["Misc", "Ring"],
            catalog.GetRequiredTags(" RING ").OrderBy(tag => tag));
        Assert.Contains("Armor", catalog.GetRequiredTags("LIGHT ARMOR"));
        Assert.False(catalog.TryGetTags("unknown", out _));
    }

    [Fact]
    public void RejectsDuplicateTypesAndEmptyTags()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemTagCatalog([
                new("ring", new HashSet<string>(["Misc"], StringComparer.OrdinalIgnoreCase)),
                new("RING", new HashSet<string>(["Ring"], StringComparer.OrdinalIgnoreCase)),
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemTagCatalog([
                new("empty", new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            ]));
    }

    [Fact]
    public void LegacyItemLoadingUsesExternalItemTagNamespaceWhenProvided()
    {
        var catalog = new AuthoritativeNativeItemTagCatalog([
            new("CustomType", new HashSet<string>(["Misc", "Ring"], StringComparer.OrdinalIgnoreCase)),
        ]);
        var topology = new AuthoritativeNativeItemTopologyCatalog([
            new(AuthoritativeNativeItemTopologyKind.ItemType, "CustomType", 0),
        ]);
        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                + "TEST\t0\tMisc\tUnequippable\tNONE\tCustomType\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\tNONE\tNONE\tNull\tfalse\t1\n",
            nativeTopologyCatalog: topology,
            nativeItemTagCatalog: catalog);

        Assert.Equal(
            ["Misc", "Ring"],
            items.GetRequired(1).GenerationTags.OrderBy(tag => tag));
    }

    [Fact]
    public void LegacyItemLoadingRejectsUnknownExternalItemTypes()
    {
        var catalog = new AuthoritativeNativeItemTagCatalog([
            new("CustomType", new HashSet<string>(["Misc"], StringComparer.OrdinalIgnoreCase)),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCatalog.LoadLegacyTsv(
                "itemdat.tsv",
                "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                    + "TEST\t0\tMisc\tUnequippable\tNONE\tUnknownType\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\tNONE\tNull\tfalse\t1\n",
                nativeItemTagCatalog: catalog));

        Assert.Contains("UnknownType", exception.Message, StringComparison.Ordinal);
    }
}
