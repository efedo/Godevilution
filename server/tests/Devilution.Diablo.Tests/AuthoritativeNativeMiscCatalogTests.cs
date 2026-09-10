using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeMiscCatalogTests
{
    [Fact]
    public void LoadsNativeMiscellaneousSymbols()
    {
        var catalog = AuthoritativeNativeMiscCatalog.LoadTsv(
            "native_item_misc.tsv",
            "native_misc_id\tnative_misc_symbol\n"
                + "0\tNONE\n"
                + "37\tOILBSMTH\n"
                + "51\tRUNES\n");

        Assert.Equal(3, catalog.Definitions.Count);
        Assert.Equal(37, catalog.GetRequiredId("oilbsmth"));
        Assert.Equal(51, catalog.GetRequiredId(" RUNES "));
        Assert.False(catalog.TryGetId("missing", out _));
    }

    [Fact]
    public void RejectsDuplicateAndOutOfRangeMiscDefinitions()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeMiscCatalog([
                new(21, "SCROLL"),
                new(21, "SCROLLT"),
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeMiscCatalog([
                new(21, "SCROLL"),
                new(22, "scroll"),
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeMiscCatalog([
                new(-1, "Invalid"),
            ]));
    }

    [Fact]
    public void LegacyItemLoadingUsesTheExternalMiscNamespaceWhenProvided()
    {
        var catalog = new AuthoritativeNativeMiscCatalog([
            new(37, "CustomMisc"),
        ]);
        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                + "TEST\t0\tMisc\tUnequippable\tNONE\tMisc\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\tCustomMisc\tNull\tfalse\t1\n",
            nativeMiscCatalog: catalog);

        Assert.Equal(37, items.GetRequired(1).MiscId);
    }

    [Fact]
    public void LegacyStaffChargesUseTheExternalStaffMiscIdentity()
    {
        var catalog = new AuthoritativeNativeMiscCatalog([
            new(99, "STAFF"),
        ]);
        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                + "STAFF_ITEM\t0\tWeapon\tOne-handed\tSTAFF\tStaff\tNONE\tStaff\tStaff\t0\t20\t1\t2\t0\t0\t0\t0\t0\t\tSTAFF\tNull\tfalse\t100\n",
            nativeMiscCatalog: catalog);

        var staff = items.GetRequired(1);
        Assert.Equal(99, staff.MiscId);
        Assert.Equal(40, staff.Charges);
        Assert.Equal(40, staff.MaxCharges);
    }

    [Fact]
    public void LegacyItemLoadingRejectsUnknownExternalMiscSymbols()
    {
        var catalog = new AuthoritativeNativeMiscCatalog([new(37, "CustomMisc")]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCatalog.LoadLegacyTsv(
                "itemdat.tsv",
                "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                    + "TEST\t0\tMisc\tUnequippable\tNONE\tMisc\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\tUnknownMisc\tNull\tfalse\t1\n",
                nativeMiscCatalog: catalog));

        Assert.Contains("UnknownMisc", exception.Message, StringComparison.Ordinal);
    }
}
