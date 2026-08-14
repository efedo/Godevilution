using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentResistanceRulesTests
{
    [Fact]
    public void AppliesUsableIdentifiedBonusesAndNaturalResistance()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusFireResistance = 30,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    Identified = false,
                    PlusFireResistance = 200,
                }),
        };

        Assert.Equal(
            42,
            AuthoritativeEquipmentResistanceRules.Calculate(
                equipment,
                AuthoritativeDamageType.Fire,
                naturalResistanceBonus: 12));
    }

    [Fact]
    public void ZeroResistanceResetsNaturalAndItemResistance()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusLightningResistance = 75,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentResistanceRules.ZeroResistanceFlag,
                }),
        };

        Assert.Equal(
            0,
            AuthoritativeEquipmentResistanceRules.Calculate(
                equipment,
                AuthoritativeDamageType.Lightning,
                naturalResistanceBonus: 12));
    }

    [Fact]
    public void UnusableOrUnidentifiedZeroResistanceDoesNotReset()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = false,
                    Flags = AuthoritativeEquipmentResistanceRules.ZeroResistanceFlag,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    Identified = false,
                    Flags = AuthoritativeEquipmentResistanceRules.ZeroResistanceFlag,
                }),
        };

        Assert.Equal(
            12,
            AuthoritativeEquipmentResistanceRules.Calculate(
                equipment,
                AuthoritativeDamageType.Magic,
                naturalResistanceBonus: 12));
    }

    [Fact]
    public void AcidUsesTheNativeMagicResistanceChannel()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusMagicResistance = 35,
                }),
        };

        Assert.Equal(
            35,
            AuthoritativeEquipmentResistanceRules.Calculate(
                equipment,
                AuthoritativeDamageType.Acid));
    }
}
