using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeEquipmentElementalDamageRulesTests
{
    [Fact]
    public void ResolveUsesInclusiveRangesAndNativeEligibility()
    {
        var calls = new List<int>();
        var damage = AuthoritativeEquipmentElementalDamageRules.Resolve(
            [
                new EquippedStoreItem(
                    4,
                    42,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
                        FireMinDamage = 2,
                        FireMaxDamage = 4,
                    }),
                new EquippedStoreItem(
                    5,
                    43,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Magical = 1,
                        Identified = false,
                        Flags = AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                        LightningMinDamage = 20,
                        LightningMaxDamage = 20,
                    }),
                new EquippedStoreItem(
                    6,
                    44,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Flags = AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                        LightningMinDamage = 3,
                        LightningMaxDamage = 5,
                    }),
            ],
            limit => {
                calls.Add(limit);
                return limit - 1;
            });

        Assert.Equal(new AuthoritativeEquipmentElementalDamageRules.ElementalDamage(4, 5), damage);
        Assert.Equal([3, 3], calls);
    }

    [Fact]
    public void ResolveDoesNotConsumeRandomnessForInactiveChannels()
    {
        var calls = 0;
        var damage = AuthoritativeEquipmentElementalDamageRules.Resolve(
            [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with { StatFlag = true })],
            _ => ++calls);

        Assert.Equal(new AuthoritativeEquipmentElementalDamageRules.ElementalDamage(0, 0), damage);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ResolveSelectsArrowChannelsForRangedAttacks()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
                    FireMinDamage = 2,
                    FireMaxDamage = 2,
                }),
            new EquippedStoreItem(
                5,
                43,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentElementalDamageRules.FireArrowsFlag,
                    FireMinDamage = 7,
                    FireMaxDamage = 7,
                }),
            new EquippedStoreItem(
                6,
                44,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                    LightningMinDamage = 3,
                    LightningMaxDamage = 3,
                }),
            new EquippedStoreItem(
                0,
                45,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentElementalDamageRules.LightningArrowsFlag,
                    LightningMinDamage = 11,
                    LightningMaxDamage = 11,
                }),
        };

        var melee = AuthoritativeEquipmentElementalDamageRules.Resolve(
            equipment,
            _ => 0);
        var ranged = AuthoritativeEquipmentElementalDamageRules.Resolve(
            equipment,
            _ => 0,
            ranged: true);

        Assert.Equal(new AuthoritativeEquipmentElementalDamageRules.ElementalDamage(2, 3), melee);
        Assert.Equal(new AuthoritativeEquipmentElementalDamageRules.ElementalDamage(7, 11), ranged);
    }
}
