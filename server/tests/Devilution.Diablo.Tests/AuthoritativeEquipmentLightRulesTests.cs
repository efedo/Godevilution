using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentLightRulesTests
{
    [Fact]
    public void AppliesUsableIdentifiedLightModifiers()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusLight = 3,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    PlusLight = 4,
                }),
            new EquippedStoreItem(
                6,
                3,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    Identified = true,
                    PlusLight = -2,
                }),
        };

        Assert.Equal(11, AuthoritativeEquipmentLightRules.CalculateLightRadius(equipment));
    }

    [Fact]
    public void IgnoresUnusableAndUnidentifiedMagicalModifiers()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = false,
                    PlusLight = 100,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    Identified = false,
                    PlusLight = 100,
                }),
        };

        Assert.Equal(10, AuthoritativeEquipmentLightRules.CalculateLightRadius(equipment));
    }

    [Theory]
    [InlineData(-100, 2)]
    [InlineData(100, 15)]
    public void ClampsToNativeLightRadiusBounds(int modifier, int expected)
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusLight = modifier,
                }),
        };

        Assert.Equal(expected, AuthoritativeEquipmentLightRules.CalculateLightRadius(equipment));
    }
}
