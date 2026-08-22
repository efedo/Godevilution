using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeSpellCatalogTests
{
    [Fact]
    public void LoadsTheCompleteNativeCompatibilityNamespace()
    {
        var catalog = AuthoritativeNativeSpellCatalog.LoadTsv(
            "native_spells.tsv",
            "native_spell_id\tnative_spell_symbol\n"
                + "0\tNull\n"
                + "5\tIdentify\n"
                + "51\tRuneOfStone\n");

        Assert.Equal(3, catalog.Definitions.Count);
        Assert.Equal(5U, catalog.GetRequiredId("identify"));
        Assert.Equal(51U, catalog.GetRequiredId(" RuneOfStone "));
        Assert.False(catalog.TryGetId("missing", out _));
    }

    [Fact]
    public void RejectsDuplicateNativeIdsAndSymbols()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeSpellCatalog([
                new(1, "Firebolt"),
                new(1, "Healing"),
            ]));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeSpellCatalog([
                new(1, "Firebolt"),
                new(2, "firebolt"),
            ]));
    }

    [Fact]
    public void RejectsNativeIdsOutsideCompatibilityRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeNativeSpellCatalog([
                new(52, "Unsupported"),
            ]));
    }

    [Fact]
    public void LegacyItemLoadingUsesTheExternalNamespaceWhenProvided()
    {
        var catalog = new AuthoritativeNativeSpellCatalog([
            new(5, "CustomSpell"),
        ]);
        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                + "TEST\t0\tMisc\tUnequippable\tNONE\tMisc\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\tNONE\tCustomSpell\tfalse\t1\n",
            nativeSpellCatalog: catalog);

        Assert.Equal(5, items.GetRequired(1).SpellId);
    }
}
