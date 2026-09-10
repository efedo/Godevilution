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
    public void RejectsIncompleteNamespaceWhenProductionValidationIsRequested()
    {
        var catalog = new AuthoritativeNativeSpellCatalog([
            new(0, "Null"),
            new(1, "Firebolt"),
            new(51, "RuneOfStone"),
        ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => catalog.ValidateCompleteNamespace());

        Assert.Contains("missing IDs: 2, 3, 4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionTsvLoadCanRequireTheCompleteNamespace()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeSpellCatalog.LoadTsv(
                "native_spells.tsv",
                "native_spell_id\tnative_spell_symbol\n0\tNull\n",
                requireCompleteNamespace: true));
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

    [Fact]
    public void LegacyItemLoadingRejectsUnknownExternalSpellSymbols()
    {
        var catalog = new AuthoritativeNativeSpellCatalog([new(5, "CustomSpell")]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCatalog.LoadLegacyTsv(
                "itemdat.tsv",
                "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
                    + "TEST\t0\tMisc\tUnequippable\tNONE\tMisc\tNONE\tTest\tTest\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\tNONE\tUnknownSpell\tfalse\t1\n",
                nativeSpellCatalog: catalog));

        Assert.Contains("UnknownSpell", exception.Message, StringComparison.Ordinal);
    }
}
