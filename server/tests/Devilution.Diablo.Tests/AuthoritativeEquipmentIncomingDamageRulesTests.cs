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

    [Fact]
    public void AppliesHellfireMonsterClassArmorOncePerEligibleFlag()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.AgainstDemonsFlag,
                }),
            new EquippedStoreItem(
                5,
                2,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.AgainstUndeadFlag,
                }),
            new EquippedStoreItem(
                6,
                3,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Magical = 1,
                    Identified = false,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.AgainstDemonsFlag
                        | AuthoritativeEquipmentIncomingDamageRules.AgainstUndeadFlag,
                }),
        };

        Assert.Equal(
            70,
            AuthoritativeEquipmentIncomingDamageRules.ApplyHellfireMonsterClassArmor(
                30,
                targetDemon: true,
                targetUndead: false,
                equipment));
        Assert.Equal(
            50,
            AuthoritativeEquipmentIncomingDamageRules.ApplyHellfireMonsterClassArmor(
                30,
                targetDemon: false,
                targetUndead: true,
                equipment));
        Assert.Equal(
            30,
            AuthoritativeEquipmentIncomingDamageRules.ApplyHellfireMonsterClassArmor(
                30,
                targetDemon: false,
                targetUndead: false,
                equipment));
    }

    [Fact]
    public void UsesConfiguredHellfireMonsterClassArmorBonuses()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.AgainstDemonsFlag
                        | AuthoritativeEquipmentIncomingDamageRules.AgainstUndeadFlag,
                }),
        };

        Assert.Equal(
            57,
            AuthoritativeEquipmentIncomingDamageRules.ApplyHellfireMonsterClassArmor(
                30,
                targetDemon: true,
                targetUndead: true,
                equipment,
                againstDemonsArmorBonus: 17,
                againstUndeadArmorBonus: 10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeEquipmentIncomingDamageRules.ApplyHellfireMonsterClassArmor(
                30,
                targetDemon: true,
                targetUndead: false,
                equipment,
                againstDemonsArmorBonus: -1));
    }
}
