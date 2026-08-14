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
