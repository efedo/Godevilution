using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentIncomingDamageRulesTests
{
    [Fact]
    public void AppliesUsableIdentifiedGetHitAndPreservesMinimumDamage()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusGetHit = -5,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusGetHit = 2,
                }),
        };

        Assert.Equal(
            17,
            AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                20,
                1,
                equipment));
        Assert.Equal(
            1,
            AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                4,
                1,
                equipment));
    }

    [Fact]
    public void IgnoresUnusableAndUnidentifiedMagicalGetHit()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = false,
                    PlusGetHit = -20,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    Identified = false,
                    PlusGetHit = -20,
                }),
        };

        Assert.Equal(
            20,
            AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                20,
                1,
                equipment));
    }
}
