using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeItemClassificationTests
{
    [Fact]
    public void ExplicitWeaponTagsOverrideConflictingLegacyItemClass()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Weapon", "Sword"]);

        Assert.True(AuthoritativeItemClassification.IsWeapon(definition));
    }

    [Fact]
    public void ExplicitShieldTagsOverrideConflictingLegacyWeaponClass()
    {
        var definition = CreateDefinition(itemClass: 1, tags: ["Shield"]);

        Assert.False(AuthoritativeItemClassification.IsWeapon(definition));
    }

    [Fact]
    public void LegacyWeaponClassRemainsTheFallbackWithoutSubtypeTags()
    {
        var definition = CreateDefinition(itemClass: 1, tags: []);

        Assert.True(AuthoritativeItemClassification.IsWeapon(definition));
    }

    [Fact]
    public void NonWeaponTagsOverrideConflictingLegacyItemClass()
    {
        var definition = CreateDefinition(itemClass: 1, tags: ["Misc"]);

        Assert.False(AuthoritativeItemClassification.IsWeapon(definition));
    }

    [Fact]
    public void ExplicitStaffTagsOverrideConflictingLegacyItemType()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Staff"]);
        definition = definition with { ItemType = 99 };

        Assert.True(AuthoritativeItemClassification.IsStaff(definition));
    }

    [Fact]
    public void NonStaffTagsOverrideConflictingLegacyItemType()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Misc"])
            with { ItemType = 10 };

        Assert.False(AuthoritativeItemClassification.IsStaff(definition));
    }

    [Fact]
    public void StaffTagsResolveByMiscIdWhenNativeItemIndexIsAbsent()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Staff"]) with {
            ItemType = 99,
            MiscId = 121,
            NativeItemIndex = 101,
        };
        var catalog = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            ItemType = 99,
            MiscId = 121,
        };

        Assert.True(AuthoritativeItemClassification.IsStaff(state, catalog));
    }

    [Fact]
    public void WeaponAndTwoHandedTagsResolveByMiscIdWhenNativeItemIndexIsAbsent()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Staff", "TwoHanded"]) with {
            ItemType = 99,
            EquipLocation = 0,
            MiscId = 121,
            NativeItemIndex = 101,
        };
        var catalog = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            ItemClass = 0,
            EquipLocation = 0,
            MiscId = 121,
        };

        Assert.True(AuthoritativeItemClassification.IsWeapon(state, catalog));
        Assert.True(AuthoritativeItemClassification.IsTwoHanded(state, catalog));
    }

    [Fact]
    public void StableItemIdentityTakesPrecedenceOverConflictingNativeIndex()
    {
        var stableDefinition = CreateDefinition(itemClass: 1, tags: ["Weapon"]) with {
            NativeItemIndex = 101,
        };
        var conflictingDefinition = stableDefinition with {
            ItemId = 2,
            GenerationTags = new HashSet<string>(["Shield"], StringComparer.OrdinalIgnoreCase),
        };
        var catalog = new AuthoritativeItemCatalog([stableDefinition, conflictingDefinition]);
        var state = AuthoritativeItemState.Empty with {
            StableItemId = stableDefinition.ItemId,
            ItemIndex = stableDefinition.NativeItemIndex,
            ItemClass = 0,
        };

        Assert.True(AuthoritativeItemClassification.IsWeapon(state, catalog));
        Assert.False(AuthoritativeItemClassification.IsShield(state, catalog));
    }

    [Fact]
    public void EquipmentSubtypeHelpersUseTagsBeforeNativeFallbacks()
    {
        var definition = CreateDefinition(
            itemClass: 0,
            tags: ["Shield", "LightArmor"])
            with {
                ItemType = 99,
            };

        Assert.True(AuthoritativeItemClassification.IsShield(definition));
        Assert.False(AuthoritativeItemClassification.IsSword(definition));
        Assert.True(AuthoritativeItemClassification.IsLightArmor(definition));
        Assert.False(AuthoritativeItemClassification.IsMediumArmor(definition));

        var medium = definition with {
            GenerationTags = new HashSet<string>(["MediumArmor"], StringComparer.OrdinalIgnoreCase),
            ItemType = 6,
        };
        Assert.False(AuthoritativeItemClassification.IsLightArmor(medium));
        Assert.True(AuthoritativeItemClassification.IsMediumArmor(medium));
    }

    [Fact]
    public void EquipmentSubtypeHelpersPreserveLegacyNativeFallbacks()
    {
        Assert.True(AuthoritativeItemClassification.IsShield(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 5 }));
        Assert.True(AuthoritativeItemClassification.IsSword(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 1 }));
        Assert.True(AuthoritativeItemClassification.IsAxe(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 2 }));
        Assert.True(AuthoritativeItemClassification.IsMace(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 4 }));
        Assert.True(AuthoritativeItemClassification.IsBow(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 3 }));
        Assert.True(AuthoritativeItemClassification.IsLightArmor(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 6 }));
        Assert.True(AuthoritativeItemClassification.IsMediumArmor(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 8 }));
        Assert.True(AuthoritativeItemClassification.IsHeavyArmor(
            CreateDefinition(itemClass: 0, tags: []) with { ItemType = 9 }));
    }

    [Fact]
    public void NonMatchingSubtypeTagsOverrideConflictingLegacyItemTypes()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Misc"])
            with { ItemType = 5 };

        Assert.False(AuthoritativeItemClassification.IsShield(definition));
    }

    [Fact]
    public void GoldTagsOverrideConflictingNativeGoldIdentity()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var definition = CreateDefinition(itemClass: 0, tags: ["Gold"])
            with {
                ItemType = 99,
                NativeItemIndex = 99,
            };
        var catalog = new AuthoritativeItemCatalog([definition]);
        var state = definition.Generate(42);

        Assert.True(AuthoritativeItemClassification.IsGold(state, rules, catalog));
    }

    [Fact]
    public void GoldClassificationRetainsLegacyTypeAndIndexFallback()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var state = AuthoritativeItemState.Empty with {
            ItemType = rules.DeathGoldItemType,
            ItemIndex = rules.DeathGoldItemIndex,
        };

        Assert.True(AuthoritativeItemClassification.IsGold(state, rules));
    }

    [Fact]
    public void NonGoldTagsOverrideConflictingNativeGoldIdentity()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var definition = CreateDefinition(itemClass: 0, tags: ["Misc"])
            with {
                ItemType = rules.DeathGoldItemType,
                NativeItemIndex = rules.DeathGoldItemIndex,
            };
        var catalog = new AuthoritativeItemCatalog([definition]);
        var state = definition.Generate(42);

        Assert.False(AuthoritativeItemClassification.IsGold(state, rules, catalog));
    }

    [Fact]
    public void ExplicitBeltTagOverridesConflictingLegacyPlacementMetadata()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Belt"])
            with {
                EquipLocation = 0,
            };

        Assert.True(AuthoritativeItemClassification.IsBelt(definition));
    }

    [Fact]
    public void BeltClassificationRetainsLegacyPlacementFallback()
    {
        var definition = CreateDefinition(itemClass: 3, tags: [])
            with {
                EquipLocation = 7,
            };

        Assert.True(AuthoritativeItemClassification.IsBelt(definition));
    }

    [Fact]
    public void NonBeltTagsOverrideConflictingLegacyPlacement()
    {
        var definition = CreateDefinition(itemClass: 3, tags: ["Misc"])
            with { EquipLocation = 7 };

        Assert.False(AuthoritativeItemClassification.IsBelt(definition));
    }

    [Fact]
    public void BeltTagsResolveByMiscIdWhenNativeItemIndexIsAbsent()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Misc", "Belt"]) with {
            ItemType = 99,
            EquipLocation = 0,
            MiscId = 121,
            NativeItemIndex = 101,
        };
        var catalog = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            ItemType = 99,
            EquipLocation = 0,
            ItemClass = 0,
            MiscId = 121,
        };

        Assert.True(AuthoritativeItemClassification.IsBelt(state, catalog));
    }

    [Fact]
    public void ExplicitTwoHandedTagOverridesConflictingLegacyEquipmentLocation()
    {
        var definition = CreateDefinition(itemClass: 1, tags: ["Weapon", "TwoHanded"])
            with {
                EquipLocation = 1,
                NativeItemIndex = 101,
            };
        var catalog = new AuthoritativeItemCatalog([definition]);

        Assert.True(AuthoritativeItemClassification.IsTwoHanded(definition));
        Assert.True(AuthoritativeItemClassification.IsTwoHanded(
            definition.Generate(42),
            catalog));
    }

    [Fact]
    public void TwoHandedClassificationRetainsLegacyEquipmentLocationFallback()
    {
        var definition = CreateDefinition(itemClass: 1, tags: [])
            with {
                EquipLocation = 2,
            };

        Assert.True(AuthoritativeItemClassification.IsTwoHanded(definition));
    }

    [Fact]
    public void NonTwoHandedTagsOverrideConflictingLegacyEquipmentLocation()
    {
        var definition = CreateDefinition(itemClass: 1, tags: ["Weapon", "OneHanded"])
            with { EquipLocation = 2 };

        Assert.False(AuthoritativeItemClassification.IsTwoHanded(definition));
    }

    [Fact]
    public void ExplicitEquipmentSlotTagsOverrideConflictingLegacyLocation()
    {
        var definition = CreateDefinition(itemClass: 2, tags: ["Armor", "BodyArmor"])
            with {
                EquipLocation = 4,
                NativeItemIndex = 901,
            };
        var catalog = new AuthoritativeItemCatalog([definition]);

        Assert.Equal(
            DiabloEquipmentSlotKind.BodyArmor,
            AuthoritativeItemClassification.GetEquipmentSlotKind(definition));
        Assert.Equal(
            DiabloEquipmentSlotKind.BodyArmor,
            AuthoritativeItemClassification.GetEquipmentSlotKind(
                definition.Generate(42),
                catalog));
    }

    [Fact]
    public void EquipmentSlotClassificationRetainsLegacyLocationFallback()
    {
        var definition = CreateDefinition(itemClass: 2, tags: [])
            with {
                EquipLocation = 5,
            };

        Assert.Equal(
            DiabloEquipmentSlotKind.Ring,
            AuthoritativeItemClassification.GetEquipmentSlotKind(definition));
    }

    [Fact]
    public void NonSlotTagsOverrideConflictingLegacyEquipmentLocation()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Misc"])
            with { EquipLocation = 2 };

        Assert.Equal(
            DiabloEquipmentSlotKind.Unknown,
            AuthoritativeItemClassification.GetEquipmentSlotKind(definition));
    }

    [Fact]
    public void EquipmentSlotTagsResolveByMiscIdWhenNativeItemIndexIsAbsent()
    {
        var definition = CreateDefinition(itemClass: 2, tags: ["Misc", "Ring"]) with {
            EquipLocation = 5,
            MiscId = 121,
            NativeItemIndex = 101,
        };
        var catalog = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            EquipLocation = 0,
            MiscId = 121,
        };

        Assert.Equal(
            DiabloEquipmentSlotKind.Ring,
            AuthoritativeItemClassification.GetEquipmentSlotKind(state, catalog));
    }

    private static AuthoritativeItemDefinition CreateDefinition(
        int itemClass,
        params string[] tags)
    {
        return new AuthoritativeItemDefinition(1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, true) {
            ItemClass = itemClass,
            GenerationTags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase),
        };
    }
}
