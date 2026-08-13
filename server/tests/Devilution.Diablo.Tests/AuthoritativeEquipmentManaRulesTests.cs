using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentManaRulesTests
{
    [Fact]
    public void FindsUsableIdentifiedNoManaEquipment()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentManaRules.NoManaFlag,
                }),
        };

        Assert.True(AuthoritativeEquipmentManaRules.HasNoManaItem(equipment));
    }

    [Fact]
    public void IgnoresUnusableAndUnidentifiedMagicalEquipment()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = false,
                    Flags = AuthoritativeEquipmentManaRules.NoManaFlag,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    Identified = false,
                    Flags = AuthoritativeEquipmentManaRules.NoManaFlag,
                }),
        };

        Assert.False(AuthoritativeEquipmentManaRules.HasNoManaItem(equipment));
    }
}
