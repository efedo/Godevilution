using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentDamageRulesTests
{
    [Fact]
    public void EquippedWeaponRangeAndWarriorStrengthModifierAreNative()
    {
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(20, 20)),
            [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 4,
                    MaxDamage = 8,
                    StatFlag = true,
                })],
            CreateClass("warrior"));

        Assert.Equal(4, profile.MinimumDamage);
        Assert.Equal(8, profile.MaximumDamage);
        Assert.Equal(10, profile.DamageModifier);
    }

    [Fact]
    public void EquippedWeaponRangeResolvesByMiscIdWhenStateOmitsDamage()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 100, 100, 4, 8, 0, 20, 20, 1, 1, true) {
                NativeItemIndex = 101,
                MiscId = 121,
                GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var equipment = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 121,
                ItemType = 1,
                ItemClass = 1,
                StatFlag = true,
            });

        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(20, 20)),
            [equipment],
            CreateClass("warrior"),
            items);

        Assert.Equal(4, profile.MinimumDamage);
        Assert.Equal(8, profile.MaximumDamage);
    }

    [Fact]
    public void MonkUnarmedAndShieldRangesUseNativeLevelRules()
    {
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(100, 100),
            new PlayerAttributeState(0, 0),
            new PlayerAttributeState(20, 20),
            new PlayerAttributeState(20, 20));
        var monk = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Monk,
            MonkUnarmedMinimumDamageLevelDivisor = 5,
            MonkUnarmedMaximumDamageLevelDivisor = 2,
        };

        var unarmed = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            attributes,
            [],
            monk);
        var shielded = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            attributes,
            [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 5,
                    ItemClass = 2,
                    StatFlag = true,
                })],
            monk);

        Assert.Equal((2, 5, 8), (unarmed.MinimumDamage, unarmed.MaximumDamage, unarmed.DamageModifier));
        Assert.Equal((2, 5, 4), (shielded.MinimumDamage, shielded.MaximumDamage, shielded.DamageModifier));
    }

    [Fact]
    public void MonkUnclassifiedHandItemUsesUnarmedModifier()
    {
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(100, 100),
            new PlayerAttributeState(0, 0),
            new PlayerAttributeState(20, 20),
            new PlayerAttributeState(20, 20));
        var monk = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Monk,
        };
        var unclassifiedHandItem = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 0,
                ItemClass = 2,
                StatFlag = true,
            });

        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            attributes,
            [unclassifiedHandItem],
            monk);

        Assert.Equal((5, 10, 8), (
            profile.MinimumDamage,
            profile.MaximumDamage,
            profile.DamageModifier));
    }

    [Fact]
    public void ExplicitContentTagOverridesNativeItemTypeForDamageProfile()
    {
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(100, 100),
            new PlayerAttributeState(0, 0),
            new PlayerAttributeState(20, 20),
            new PlayerAttributeState(20, 20));
        var monk = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Monk,
        };
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\n"
                + "7\t10\t1\t1\tStaff\t99\n");
        var equipment = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemIndex = 99,
                ItemType = 1,
                StatFlag = true,
            });

        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            attributes,
            [equipment],
            monk,
            items);

        Assert.Equal(8, profile.DamageModifier);
    }

    [Fact]
    public void ExplicitShieldTagSuppressesConflictingLegacySwordType()
    {
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(100, 100),
            new PlayerAttributeState(0, 0),
            new PlayerAttributeState(20, 20),
            new PlayerAttributeState(20, 20));
        var bard = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Bard,
        };
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\n"
                + "7\t1\t1\t1\tArmor,Shield\t99\n");
        var equipment = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemIndex = 99,
                ItemType = 1,
                StatFlag = true,
            });

        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            attributes,
            [equipment],
            bard,
            items);

        Assert.Equal(10, profile.DamageModifier);
    }

    [Fact]
    public void BarbarianWeaponStrengthModifierUsesConfiguredDivisor()
    {
        var barbarian = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Barbarian,
        };
        var axe = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 2,
                ItemClass = 1,
                StatFlag = true,
            });
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            barbarianWeaponStrengthDamageDivisor: 50);

        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(20, 20)),
            [axe],
            barbarian,
            barbarianWeaponStrengthDamageDivisor: rules.BarbarianWeaponStrengthDamageDivisor);

        Assert.Equal(22, profile.DamageModifier);
    }

    [Fact]
    public void BarbarianBowModifierUsesConfiguredDivisor()
    {
        var barbarian = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Barbarian,
        };
        var bow = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 3,
                ItemClass = 1,
                StatFlag = true,
            });
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [bow],
            barbarian,
            barbarianBowStrengthDamageDivisor: 100);

        Assert.Equal(10, profile.DamageModifier);
    }

    [Fact]
    public void BarbarianDefaultModifierUsesConfiguredDivisor()
    {
        var barbarian = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Barbarian,
        };
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [],
            barbarian,
            barbarianDefaultStrengthDamageDivisor: 50);

        Assert.Equal(22, profile.DamageModifier);
    }

    [Fact]
    public void BarbarianVitalityModifierUsesConfiguredDivisor()
    {
        var barbarian = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Barbarian,
        };
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100)),
            [],
            barbarian,
            barbarianVitalityDamageDivisor: 50);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void RogueStrengthDexterityModifierUsesConfiguredDivisor()
    {
        var rogue = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Rogue,
        };
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [],
            rogue,
            rogueStrengthDexterityDamageDivisor: 100);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void MonkStaffUnarmedModifierUsesConfiguredDivisor()
    {
        var monk = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Monk,
        };
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [],
            monk,
            monkStaffUnarmedDamageDivisor: 100);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void MonkWeaponModifierUsesConfiguredDivisor()
    {
        var monk = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Monk,
        };
        var sword = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemClass = 1,
                StatFlag = true,
            });
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [sword],
            monk,
            monkWeaponDamageDivisor: 100);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void BardSwordModifierUsesConfiguredDivisor()
    {
        var bard = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Bard,
        };
        var sword = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemClass = 1,
                StatFlag = true,
            });
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [sword],
            bard,
            bardSwordStrengthDexterityDamageDivisor: 100);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void BardBowModifierUsesConfiguredDivisor()
    {
        var bard = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Bard,
        };
        var bow = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 3,
                ItemClass = 1,
                StatFlag = true,
            });
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [bow],
            bard,
            bardBowStrengthDexterityDamageDivisor: 100);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void BardDefaultModifierUsesConfiguredDivisor()
    {
        var bard = CreateClass("custom") with {
            PhysicalDamageFormula = AuthoritativePhysicalDamageFormula.Bard,
        };
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [],
            bard,
            bardDefaultStrengthDamageDivisor: 50);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void StandardModifierUsesConfiguredDivisor()
    {
        var profile = AuthoritativeEquipmentDamageRules.Calculate(
            10,
            new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(20, 20)),
            [],
            CreateClass("custom"),
            standardStrengthDamageDivisor: 50);

        Assert.Equal(20, profile.DamageModifier);
    }

    [Fact]
    public void RejectsInvalidBarbarianWeaponStrengthDivisor()
    {
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                barbarianWeaponStrengthDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeCombatRules(
                10,
                1,
                100,
                barbarianBowStrengthDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                barbarianWeaponStrengthDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                barbarianBowStrengthDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                barbarianDefaultStrengthDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                barbarianVitalityDamageDivisor: 0));
            Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                rogueStrengthDexterityDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                monkStaffUnarmedDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                monkWeaponDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                bardSwordStrengthDexterityDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                bardBowStrengthDexterityDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                bardDefaultStrengthDamageDivisor: 0));
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeEquipmentDamageRules.Calculate(
                1,
                PlayerAttributesState.Zero,
                [],
                CreateClass("custom"),
                standardStrengthDamageDivisor: 0));
    }

    private static AuthoritativePlayerClassDefinition CreateClass(string symbol)
    {
        return new AuthoritativePlayerClassDefinition(
            0,
            symbol,
            symbol,
            DiabloPlayerClassAvailability.BaseGame,
            true,
            DiabloPlayerClassFlags.None,
            0,
            0,
            0,
            0,
            750,
            750,
            750,
            750,
            0,
            50,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }
}
