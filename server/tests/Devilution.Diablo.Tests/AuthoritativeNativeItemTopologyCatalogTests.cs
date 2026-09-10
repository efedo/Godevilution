using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeItemTopologyCatalogTests
{
    [Fact]
    public void LoadsTopologySymbolsByKind()
    {
        var catalog = AuthoritativeNativeItemTopologyCatalog.LoadTsv(
            "native_item_topology.tsv",
            "kind\tsymbol\tnative_id\n"
                + "ItemType\tlight armor\t6\n"
                + "ItemClass\tweapon\t1\n"
                + "EquipLocation\tone-handed\t1\n");

        Assert.Equal(3, catalog.Definitions.Count);
        Assert.Equal(6, catalog.GetRequiredId(
            AuthoritativeNativeItemTopologyKind.ItemType,
            "LIGHT ARMOR"));
        Assert.Equal(1, catalog.GetRequiredId(
            AuthoritativeNativeItemTopologyKind.ItemClass,
            "weapon"));
        Assert.False(catalog.TryGetId(
            AuthoritativeNativeItemTopologyKind.ItemType,
            "weapon",
            out _));
    }

    [Fact]
    public void RejectsDuplicateKeysAndOutOfRangeIds()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemTopologyCatalog([
                new(AuthoritativeNativeItemTopologyKind.ItemType, "weapon", 1),
                new(AuthoritativeNativeItemTopologyKind.ItemType, "WEAPON", 2),
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeItemTopologyCatalog([
                new(AuthoritativeNativeItemTopologyKind.ItemType, "weapon", 128),
            ]));
    }

    [Fact]
    public void LegacyItemLoadingUsesTheExternalTopologyNamespaceWhenProvided()
    {
        var catalog = new AuthoritativeNativeItemTopologyCatalog([
            new(AuthoritativeNativeItemTopologyKind.ItemType, "CustomType", 9),
            new(AuthoritativeNativeItemTopologyKind.ItemClass, "CustomClass", 4),
            new(AuthoritativeNativeItemTopologyKind.EquipLocation, "CustomLocation", 2),
        ]);
        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                + "TEST\t0\tCustomClass\tCustomLocation\tNONE\tCustomType\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\tNONE\tNull\tfalse\t1\n",
            nativeTopologyCatalog: catalog);

        var definition = items.GetRequired(1);
        Assert.Equal((9, 4, 2), (definition.ItemType, definition.ItemClass, definition.EquipLocation));
    }

    [Fact]
    public void LegacyItemLoadingRejectsUnknownExternalTopologySymbols()
    {
        var catalog = new AuthoritativeNativeItemTopologyCatalog([
            new(AuthoritativeNativeItemTopologyKind.ItemType, "CustomType", 9),
            new(AuthoritativeNativeItemTopologyKind.ItemClass, "CustomClass", 4),
            new(AuthoritativeNativeItemTopologyKind.EquipLocation, "CustomLocation", 2),
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCatalog.LoadLegacyTsv(
                "itemdat.tsv",
                "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                    + "TEST\t0\tCustomClass\tUnknownLocation\tNONE\tCustomType\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\tNONE\tNull\tfalse\t1\n",
                nativeTopologyCatalog: catalog));

        Assert.Contains("UnknownLocation", exception.Message, StringComparison.Ordinal);
    }
}
