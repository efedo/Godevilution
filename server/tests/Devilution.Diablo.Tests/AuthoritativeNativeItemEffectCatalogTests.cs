using Devilution.Data;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeItemEffectCatalogTests
{
    [Fact]
    public void AcceptsCompleteProductionSchema()
    {
        var table = TsvTable.Parse(
            "native_item_effects.tsv",
            "symbol\tnative_flag\nCustomEffect\t8\n");

        AuthoritativeNativeItemEffectCatalog.ValidateExternalizedProduction(table);
    }

    [Fact]
    public void RejectsPartialProductionSchema()
    {
        var table = TsvTable.Parse(
            "native_item_effects.tsv",
            "native_flag\tsymbol\n8\tCustomEffect\n");

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeNativeItemEffectCatalog.ValidateExternalizedProduction(table));

        Assert.Contains("complete ordered schema", exception.Message, StringComparison.Ordinal);
    }

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
            new("StealMana3Percent", 32768),
            new("StealMana5Percent", 65536),
            new("StealLife3Percent", 131072),
            new("StealLife5Percent", 262144),
            new("FastAttack1", 524288),
            new("FastAttack2", 1048576),
            new("FastAttack3", 2097152),
            new("FastAttack4", 4194304),
            new("FastRecover1", 8388608),
            new("FastRecover2", 16777216),
            new("FastRecover3", 33554432),
            new("FastBlock", 67108864),
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
            new AuthoritativeUniqueItemDefinition(1005, "Fast Ward", 7, 1, 1, [
                new("STEALMANA", 3, 3),
                new("STEALLIFE", 3, 3),
                new("FASTATTACK", 1, 1),
                new("FASTRECOVER", 1, 1),
                new("FASTBLOCK", 0, 0),
            ]),
            new AuthoritativeUniqueItemDefinition(1006, "Faster Ward", 7, 1, 1, [
                new("STEALMANA", 5, 5),
                new("STEALLIFE", 5, 5),
                new("FASTATTACK", 4, 4),
                new("FASTRECOVER", 4, 4),
            ]),
            new AuthoritativeUniqueItemDefinition(1007, "Arrow Ward", 7, 1, 1, [
                new("ADDACLIFE", 2, 4),
            ]),
            new AuthoritativeUniqueItemDefinition(1008, "Mana Ward", 7, 1, 1, [
                new("ADDMANAAC", 2, 4),
            ]),
            new AuthoritativeUniqueItemDefinition(1009, "Fire Edge", 8, 1, 1, [
                new("FIREDAM", 2, 4),
            ]),
            new AuthoritativeUniqueItemDefinition(1010, "Lightning Edge", 9, 1, 1, [
                new("LIGHTDAM", 2, 4),
            ]),
        ], nativeItemEffectCatalog: effects);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, true),
            new AuthoritativeItemDefinition(8, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, true) {
                Flags = 16384U,
            },
            new AuthoritativeItemDefinition(9, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, true) {
                Flags = 4096U,
            },
        ]);

        Assert.Equal(2U | 4U | 8U | 16U | 32U | 64U, uniques.Generate(items, 1001, 42, 1).Flags);
        Assert.Equal(128U | 256U | 512U | 1024U, uniques.Generate(items, 1002, 42, 1).Flags);
        Assert.Equal(4096U | 8192U | 16384U, uniques.Generate(items, 1003, 42, 1).Flags);
        Assert.Equal(2048U | 8192U, uniques.Generate(items, 1004, 42, 1).Flags);
        Assert.Equal(32768U | 131072U | 524288U | 8388608U | 67108864U, uniques.Generate(items, 1005, 42, 1).Flags);
        Assert.Equal(65536U | 262144U | 4194304U | 33554432U, uniques.Generate(items, 1006, 42, 1).Flags);
        Assert.Equal(2048U | 8192U, uniques.Generate(items, 1007, 42, 1).Flags);
        Assert.Equal(4096U | 16384U, uniques.Generate(items, 1008, 42, 1).Flags);
        Assert.Equal(4096U, uniques.Generate(items, 1009, 42, 1).Flags);
        Assert.Equal(16384U, uniques.Generate(items, 1010, 42, 1).Flags);
    }

    [Fact]
    public void UniqueElementalPowersRetainNativeFallbackWithoutCatalog()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(2001, "Fire Edge", 7, 1, 1, [
                new("FIREDAM", 2, 4),
            ]),
            new AuthoritativeUniqueItemDefinition(2002, "Lightning Edge", 8, 1, 1, [
                new("LIGHTDAM", 2, 4),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, true) {
                Flags = AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
            },
            new AuthoritativeItemDefinition(8, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, true) {
                Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
            },
        ]);

        Assert.Equal(
            AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
            uniques.Generate(items, 2001, 42, 1).Flags);
        Assert.Equal(
            AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
            uniques.Generate(items, 2002, 42, 1).Flags);
    }

    [Fact]
    public void LegacyAffixLoadingUsesExternalEffectNamespaceWhenProvided()
    {
        var effects = new AuthoritativeNativeItemEffectCatalog([
            new("FireDamage", 1024),
            new("LightningDamage", 2048),
        ]);
        const string header = "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n";
        const string row = "Flame\tFIREDAM\t2\t4\t0\tAny\tAny\t1\ttrue\t0\t0\t0\n";
        var rules = new AuthoritativeItemGenerationRules(0, 0, 0, 0, 0, 100, 0, 100, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            header + row,
            "item_suffixes.tsv",
            header,
            rules,
            nativeItemEffectCatalog: effects);
        var state = AuthoritativeItemState.Empty with {
            Flags = 2048U,
            Value = 1,
        };

        var remapped = affixes.Apply(state, 42, 1, new HashSet<string>(["Any"]));

        Assert.Equal(1024U, remapped.Flags);

        var fallbackAffixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            header + row,
            "item_suffixes.tsv",
            header,
            rules);
        var fallback = fallbackAffixes.Apply(
            state with { Flags = AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag },
            42,
            1,
            new HashSet<string>(["Any"]));
        Assert.Equal(AuthoritativeEquipmentElementalDamageRules.FireDamageFlag, fallback.Flags);
    }

    [Fact]
    public void LegacyAffixLoadingUsesExternalHellfireEffectNamespaceWhenProvided()
    {
        var effects = new AuthoritativeNativeItemHellfireEffectCatalog([
            new("DEVASTATION", 2),
        ]);
        const string header = "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n";
        const string row = "Devastation\tDEVASTATION\t0\t0\t0\tAny\tAny\t1\ttrue\t0\t0\t0\n";
        var rules = new AuthoritativeItemGenerationRules(0, 0, 0, 0, 0, 100, 0, 100, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            header + row,
            "item_suffixes.tsv",
            header,
            rules,
            nativeItemHellfireEffectCatalog: effects);

        var remapped = affixes.Apply(
            AuthoritativeItemState.Empty,
            42,
            1,
            new HashSet<string>(["Any"]));

        Assert.Equal(2, remapped.HellfireDamageArmorFlags);

        var fallbackAffixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            header + row,
            "item_suffixes.tsv",
            header,
            rules);
        var fallback = fallbackAffixes.Apply(
            AuthoritativeItemState.Empty,
            42,
            1,
            new HashSet<string>(["Any"]));
        Assert.Equal(
            AuthoritativeEquipmentIncomingDamageRules.DevastationFlag,
            fallback.HellfireDamageArmorFlags);
    }
}
