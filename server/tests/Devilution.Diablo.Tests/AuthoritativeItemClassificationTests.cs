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
    public void ExplicitStaffTagsOverrideConflictingLegacyItemType()
    {
        var definition = CreateDefinition(itemClass: 0, tags: ["Staff"]);
        definition = definition with { ItemType = 99 };

        Assert.True(AuthoritativeItemClassification.IsStaff(definition));
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
