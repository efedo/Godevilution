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

    [Fact]
    public void UniquePowersUseExternalEffectNamespaceWhenProvided()
    {
        var effects = new AuthoritativeNativeItemEffectCatalog([
            new("MultipleArrows", 2),
            new("RandomArrowVelocity", 4),
            new("RandomStealLife", 8),
            new("DrainLife", 16),
            new("NoMana", 32),
            new("Knockback", 64),
            new("Thorns", 128),
            new("TripleDemonDamage", 256),
            new("HalfTrapDamage", 512),
            new("ZeroResistance", 1024),
            new("FireArrows", 2048),
            new("FireDamage", 4096),
            new("LightningArrows", 8192),
            new("LightningDamage", 16384),
        ]);
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Flags One", 7, 1, 1, [
                new("MULT_ARROWS", 0, 0),
                new("RNDARROWVEL", 0, 0),
                new("RNDSTEALLIFE", 0, 0),
                new("DRAINLIFE", 0, 0),
                new("NOMANA", 0, 0),
                new("KNOCKBACK", 0, 0),
            ]),
            new AuthoritativeUniqueItemDefinition(1002, "Flags Two", 7, 1, 1, [
                new("THORNS", 0, 0),
                new("3XDAMVDEM", 0, 0),
                new("ABSHALFTRAP", 0, 0),
                new("ALLRESZERO", 0, 0),
            ]),
            new AuthoritativeUniqueItemDefinition(1003, "Elemental Arrows", 7, 1, 1, [
                new("FIRE_ARROWS", 2, 4),
                new("LIGHT_ARROWS", 3, 5),
            ]),
            new AuthoritativeUniqueItemDefinition(1004, "Fireball Bow", 7, 1, 1, [
                new("FIREBALL", 15, 20),
            ]),
        ], nativeItemEffectCatalog: effects);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, true),
        ]);

        Assert.Equal(2U | 4U | 8U | 16U | 32U | 64U, uniques.Generate(items, 1001, 42, 1).Flags);
        Assert.Equal(128U | 256U | 512U | 1024U, uniques.Generate(items, 1002, 42, 1).Flags);
        Assert.Equal(4096U | 8192U | 16384U, uniques.Generate(items, 1003, 42, 1).Flags);
        Assert.Equal(2048U | 8192U, uniques.Generate(items, 1004, 42, 1).Flags);
    }
}
