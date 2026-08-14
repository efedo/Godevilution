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
