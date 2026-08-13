using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeItemCatalogTests
{
    [Fact]
    public void GeneratesProtocolItemStateFromExternalDefinition()
    {
        var catalog = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\tdurability\tmax_durability\tinventory_width\tinventory_height\tidentified\n"
                + "7\t1\t75\t100\t3\t9\t2\t1\t1\n");

        var state = catalog.Generate(7, 42);
        Assert.Equal(42U, state.CreateInfo);
        Assert.Equal(7, state.ItemIndex);
        Assert.Equal(100, state.IdentifiedValue);
        Assert.Equal(2, state.InventoryWidth);
        Assert.Equal(1, state.InventoryHeight);
    }

    [Fact]
    public void LegacyArmorRollUsesTheNativeLcgSequence()
    {
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 2, 75, 100, 0, 0, 0, 3, 9, 1, 1, true) {
                MinArmorClass = 10,
                MaxArmorClass = 20,
            },
        ]);

        var first = catalog.Generate(7, 42);
        var second = catalog.Generate(7, 42);

        Assert.Equal(12, first.ArmorClass);
        Assert.Equal(first.ArmorClass, second.ArmorClass);
    }

    [Fact]
    public void StoreRowsMayReferenceExternalItemDefinitions()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 100, 0, 0, 0, 3, 9, 2, 1, true),
        ]);
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_id\n1\t0\t42\t75\t7\n",
            items);

        var item = Assert.Single(catalog.GetItems(1));
        Assert.Equal(7, item.State.ItemIndex);
        Assert.Equal(2, item.State.InventoryWidth);
    }

    [Fact]
    public void RejectsDuplicateItemDefinitions()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, true),
        ]));
    }

    [Fact]
    public void AppliesSeedStableAffixesOnlyWhenLevelAndTagsMatch()
    {
        var affixes = AuthoritativeItemAffixCatalog.LoadTsv(
            "item_affixes.tsv",
            "name\tpower\tvalue1\tvalue2\tmin_level\titem_tags\tchance\tuseful\tis_suffix\n"
                + "Bronze\tTOHIT\t1\t5\t1\tWeapon\t1\t1\t0\n"
                + "Strength\tSTR\t2\t2\t1\tWeapon\t1\t1\t1\n",
            new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 100, 100, 66, 59, 74));
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 100, 0, 0, 0, 3, 9, 1, 1, true) {
                GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
            },
        ], affixes);

        var levelZero = catalog.Generate(7, 42, 0);
        var first = catalog.Generate(7, 42, 10);
        var second = catalog.Generate(7, 42, 10);

        Assert.Equal(0, levelZero.PlusToHit);
        Assert.Equal(first, second);
        Assert.InRange(first.PlusToHit, 1, 5);
        Assert.Equal(2, first.PlusStrength);
    }

    [Fact]
    public void LoadsLegacyPrefixAndSuffixColumnsAndGenerationProbabilities()
    {
        var rules = AuthoritativeItemGenerationRules.LoadTsv(
            "item_generation.tsv",
            "magicChanceBase\tmagicChancePerLevel\tuniqueChanceNormal\tuniqueChanceUnique\tbonusLevelsUnique\tprefixPercent\tsuffixPercent\tonlygoodChance\tnoDropPercent\tgoldPercent\n"
                + "10\t1\t1\t15\t4\t0\t100\t66\t59\t74\n");
        var catalog = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Bronze\tTOHIT\t1\t5\t1\tWeapon\tAny\t1\ttrue\t0\t0\t0\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Strength\tSTR\t2\t2\t1\tWeapon\tAny\t1\ttrue\t0\t0\t0\n",
            rules);

        var definition = new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
            GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
        };
        var item = new AuthoritativeItemCatalog([definition], catalog).Generate(7, 42, 10);

        Assert.Equal(0, item.PlusToHit);
        Assert.Equal(2, item.PlusStrength);
    }

    [Fact]
    public void LegacyAffixGenerationUsesTheNativeRandomStreamAndRecalculatesValue()
    {
        var rules = new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 100, 0, 0, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Bronze\tTOHIT\t1\t5\t1\tWeapon\tAny\t1\ttrue\t1\t5\t1\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            rules);
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
                GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
            },
        ], affixes);

        var item = catalog.Generate(7, 42, 10);

        Assert.Equal(3, item.PlusToHit);
        Assert.Equal(78, item.IdentifiedValue);
    }

    [Fact]
    public void LegacyMonsterDropQualityUsesTheSharedRandomStream()
    {
        var rules = new AuthoritativeItemGenerationRules(0, 0, 1, 15, 4, 100, 0, 0, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n"
                + "Bronze\tTOHIT\t1\t1\t1\tWeapon\tAny\t1\ttrue\t0\t0\t0\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            rules);
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true) {
                GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
            },
        ], affixes);

        var normal = catalog.GenerateDrop(7, 42, 10);
        var forced = catalog.GenerateDrop(7, 42, 10, onlyGood: true);

        Assert.Equal(0, normal.Magical);
        Assert.Equal(1, forced.Magical);
        Assert.Equal(1, forced.PlusToHit);
    }

    [Fact]
    public void LegacyMonsterDropSelectsTheLastEligibleUniqueAfterQualityResolution()
    {
        var rules = new AuthoritativeItemGenerationRules(100, 0, 100, 15, 4, 100, 0, 0, 0, 0);
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            "item_suffixes.tsv",
            "name\tpower\tpower.value1\tpower.value2\tminLevel\titemTypes\talignment\tchance\tuseful\tminVal\tmaxVal\tmultVal\n",
            rules);
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1, "First", 7, 1, 100, []),
            new AuthoritativeUniqueItemDefinition(2, "Second", 7, 1, 200, []),
        ], usesLegacyRandom: true);
        var catalog = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 20, 20, 1, 1, true),
        ], affixes, uniques);

        var item = catalog.GenerateDrop(7, 42, 10);

        Assert.Equal(2, item.UniqueId);
        Assert.Equal(2, item.Magical);
        Assert.Equal(200, item.Value);
    }

    [Fact]
    public void GeneratesUniqueItemsFromBaseDefinitionsAndMultiplePowerRows()
    {
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            "unique_id\tname\tbase_item_id\tmin_level\tvalue\tpower\tvalue1\tvalue2\n"
                + "1001\tEmber Blade\t7\t1\t3650\tFIREDAM\t1\t10\n"
                + "1001\tEmber Blade\t7\t1\t3650\tTOHIT\t10\t10\n");
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 5);

        Assert.Equal(1001, item.UniqueId);
        Assert.Equal(3650, item.Value);
        Assert.Equal(10, item.PlusToHit);
        Assert.InRange(item.FireMinDamage, 1, 10);
    }

    [Fact]
    public void LoadsLegacyItemAndUniqueTableShapesWithStableAliases()
    {
        const string itemData = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
            + "IDI_CLEAVER\t0\tWeapon\tTwo-handed\tCLEAVER\tAxe\tCLEAVER\tCleaver\t\t10\t10\t4\t24\t0\t0\t0\t0\t0\t\tUNIQUE\tNull\tfalse\t2000\n";
        var items = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", itemData);

        Assert.Equal(1U, items.ResolveLegacySymbol("IDI_CLEAVER"));
        Assert.Equal(1U, items.ResolveLegacySymbol("CLEAVER"));
        var baseItem = items.Generate(1, 42);
        Assert.Equal(24, baseItem.MaxDamage);
        Assert.Equal(2, baseItem.ItemType);
        Assert.Equal(1, baseItem.ItemClass);
        Assert.Equal(2, baseItem.EquipLocation);
        Assert.Equal(28, baseItem.MiscId);

        var uniques = AuthoritativeUniqueItemCatalog.LoadLegacyTsv(
            "unique_itemdat.tsv",
            "name\tcursorGraphic\tuniqueBaseItem\tminLevel\tvalue\tpower0\tpower0.value1\tpower0.value2\tpower1\tpower1.value1\tpower1.value2\tpower2\tpower2.value1\tpower2.value2\tpower3\tpower3.value1\tpower3.value2\tpower4\tpower4.value1\tpower4.value2\tpower5\tpower5.value1\tpower5.value2\n"
                + "The Butcher's Cleaver\t\tCLEAVER\t1\t3650\tSTR\t10\t10\tSETDAM\t4\t24\t\t\t\t\t\t\t\t\t\t\t\t\n",
            items);
        items.AttachUniqueCatalog(uniques);

        var unique = items.GenerateUnique(1, 42, 1);
        Assert.Equal(1, unique.UniqueId);
        Assert.Equal(10, unique.PlusStrength);
        Assert.Equal(4, unique.MinDamage);
        Assert.Equal(24, unique.MaxDamage);
    }

    [Fact]
    public void LegacySpellSourcesResolveThroughTheActiveStableCatalog()
    {
        const string itemData = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
            + "IDI_GUARDIAN_SCROLL\t1\tMisc\tUnequippable\tSCROLL_OF\tMisc\tNONE\tScroll of Guardian\t\t1\t0\t0\t0\t0\t0\t0\t0\t0\t\tSCROLLT\tGuardian\ttrue\t950\n";
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0) { NativeSpellId = 13 },
        ]);

        var items = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            itemData,
            spellCatalog: spells);

        Assert.Equal(7, Assert.Single(items.Definitions).SpellId);
    }

    [Fact]
    public void LegacyRuneMiscellaneousIdsPreserveNativeSourceIdentity()
    {
        const string header = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n";
        var itemData = header
            + "IDI_RUNE_FIRE\t1\tMisc\tUnequippable\tRUNE_OF_FIRE\tMisc\tNONE\tRune of Fire\tRune\t1\t0\t0\t0\t0\t0\t0\t0\t0\t\tRUNEF\tNull\ttrue\t100\n"
            + "IDI_RUNE_LIGHT\t1\tMisc\tUnequippable\tRUNE_OF_LIGHTNING\tMisc\tNONE\tRune of Lightning\tRune\t3\t0\t0\t0\t0\t0\t0\t13\t0\t\tRUNEL\tNull\ttrue\t200\n"
            + "IDI_GREATER_RUNE_FIRE\t1\tMisc\tUnequippable\tGREATER_RUNE_OF_FIRE\tMisc\tNONE\tGreater Rune of Fire\tRune\t7\t0\t0\t0\t0\t0\t0\t42\t0\t\tGR_RUNEF\tNull\ttrue\t400\n"
            + "IDI_GREATER_RUNE_LIGHT\t1\tMisc\tUnequippable\tGREATER_RUNE_OF_LIGHTNING\tMisc\tNONE\tGreater Rune of Lightning\tRune\t7\t0\t0\t0\t0\t0\t0\t42\t0\t\tGR_RUNEL\tNull\ttrue\t500\n"
            + "IDI_RUNE_STONE\t1\tMisc\tUnequippable\tRUNE_OF_STONE\tMisc\tNONE\tRune of Stone\tRune\t7\t0\t0\t0\t0\t0\t0\t25\t0\t\tRUNES\tNull\ttrue\t300\n";

        var items = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", itemData);

        Assert.Equal([47, 48, 50, 49, 51], items.Definitions.Select(item => item.MiscId));
    }

    [Fact]
    public void UnmappedLegacySpellSourcesAreRejectedWhenAStableCatalogIsActive()
    {
        const string itemData = "id\tdropRate\tclass\tequipType\tcursorGraphic\titemType\tuniqueBaseItem\tname\tshortName\tminMonsterLevel\tdurability\tminDamage\tmaxDamage\tminArmor\tmaxArmor\tminStrength\tminMagic\tminDexterity\tspecialEffects\tmiscId\tspell\tusable\tvalue\n"
            + "IDI_TOWN_PORTAL_SCROLL\t1\tMisc\tUnequippable\tSCROLL_OF\tMisc\tNONE\tScroll of Town Portal\t\t1\t0\t0\t0\t0\t0\t0\t0\t0\t\tSCROLLT\tTownPortal\ttrue\t950\n";
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0) { NativeSpellId = 13 },
        ]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthoritativeItemCatalog.LoadLegacyTsv(
                "itemdat.tsv",
                itemData,
                spellCatalog: spells));

        Assert.Contains("native spell ID 7", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyUniquePowerUsesTheNativeRandomStream()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 0, 0, 0, 1, 1, 1, 1, true),
        ]);
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1, "Ember Blade", 7, 1, 3650, [
                new AuthoritativeUniqueItemPower("FIREDAM", 1, 10),
            ]),
        ], usesLegacyRandom: true);

        items.AttachUniqueCatalog(uniques);
        var unique = items.GenerateUnique(1, 42, 1);

        Assert.Equal(1, unique.FireMinDamage);
        Assert.Equal(10, unique.FireMaxDamage);
        Assert.Equal(0x00000010U, unique.Flags);
    }

    [Fact]
    public void StoreRowsMayReferenceUniqueDefinitions()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ember Blade", 7, 1, 3650, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);
        var stores = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\tunique_item_id\titem_level\n1\t0\t42\t3650\t1001\t1\n",
            items,
            uniques);

        Assert.Equal(1001, Assert.Single(stores.GetItems(1)).State.UniqueId);
    }

    [Fact]
    public void AppliesLegacyUniqueFlagsAndSpellPower()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Ward", 7, 1, 3650, [
                new AuthoritativeUniqueItemPower("SPELL", 10, 76),
                new AuthoritativeUniqueItemPower("FASTATTACK", 2, 2),
                new AuthoritativeUniqueItemPower("ALLRESZERO", 0, 0),
                new AuthoritativeUniqueItemPower("NOMINSTR", 0, 0),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 4, 8, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(10, item.SpellId);
        Assert.Equal(76, item.Charges);
        Assert.Equal(76, item.MaxCharges);
        Assert.Equal(0, item.MinimumStrength);
        Assert.Equal(0x80000000U | 0x00040000U, item.Flags);
    }

    [Fact]
    public void LegacyUniquePowersUseNativeFixedPointAndSignConventions()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Legacy", 7, 1, 1, [
                new AuthoritativeUniqueItemPower("LIFE", 2, 2),
                new AuthoritativeUniqueItemPower("GETHIT", 3, 3),
                new AuthoritativeUniqueItemPower("LIGHT", 4, 4),
                new AuthoritativeUniqueItemPower("INDESTRUCTIBLE", 0, 0),
            ]),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 1, 1, 0, 0, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);

        var item = items.GenerateUnique(1001, 42, 1);

        Assert.Equal(128, item.PlusHitPoints);
        Assert.Equal(-3, item.PlusGetHit);
        Assert.Equal(4, item.PlusLight);
        Assert.Equal(255, item.Durability);
        Assert.Equal(255, item.MaxDurability);
    }

    [Fact]
    public void HellfireResourceConversionsUseNativeBaseResourceTruncation()
    {
        var manaToLife = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with { ManaToLifePercent = 50 });
        var lifeToMana = new EquippedStoreItem(
            5,
            43,
            AuthoritativeItemState.Empty with { LifeToManaPercent = 40 });

        var converted = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            80 << 6,
            124 << 6,
            [manaToLife, lifeToMana]);

        Assert.Equal((80 + 62 - 32) << 6, converted.LifeQ6);
        Assert.Equal((124 + 32 - 62) << 6, converted.ManaQ6);
    }

    [Fact]
    public void EquippedLifeAndManaBonusesRemainQ6ExactAlongsideConversions()
    {
        var item = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                PlusHitPoints = 7 << 6,
                PlusMana = 9 << 6,
                ManaToLifePercent = 50,
            });

        var converted = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            80 << 6,
            124 << 6,
            [item]);

        Assert.Equal((80 + 7 + 62) << 6, converted.LifeQ6);
        Assert.Equal((124 + 9 - 62) << 6, converted.ManaQ6);
    }

    [Fact]
    public void EquippedArmorUsesRequirementsAndNativePercentageTruncation()
    {
        var identifiedArmor = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                ArmorClass = 20,
                PlusArmorClass = 50,
            });
        var unidentifiedMagicalArmor = new EquippedStoreItem(
            5,
            43,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Magical = 1,
                ArmorClass = 7,
                PlusArmorClass = 100,
            });

        var armor = AuthoritativeEquipmentStatRules.CalculateArmorClass(
            5,
            23,
            [identifiedArmor, unidentifiedMagicalArmor]);

        Assert.Equal(5 + (23 / 5) + 20 + 10 + 7, armor);
    }

    [Fact]
    public void HellfireResourceConversionsRejectOutOfRangePercentages()
    {
        var invalid = new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with { ManaToLifePercent = 101 });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
                80 << 6,
                124 << 6,
                [invalid]));
    }

    [Fact]
    public void LiveEquipmentResourceRecalculationPreservesNativeResourceDebt()
    {
        var player = new AuthoritativePlayerState(
            0,
            0,
            60 << 6,
            100 << 6,
            124 << 6,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            80 << 6,
            1,
            0,
            0,
            baseLifeMaximum: 80 << 6,
            baseManaMaximum: 124 << 6);
        player.Equipment.Add(new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with { ManaToLifePercent = 50 }));

        player.RecalculateEquipmentResources();

        Assert.Equal(142 << 6, player.LifeMaximum);
        Assert.Equal(62 << 6, player.ManaMaximum);
        Assert.Equal(122 << 6, player.Life);
        Assert.Equal(38 << 6, player.Mana);

        player.Equipment.Clear();
        player.RecalculateEquipmentResources();

        Assert.Equal(80 << 6, player.LifeMaximum);
        Assert.Equal(124 << 6, player.ManaMaximum);
        Assert.Equal(60 << 6, player.Life);
        Assert.Equal(100 << 6, player.Mana);
    }
}
