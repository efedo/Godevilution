using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterDamageRulesTests
{
    [Fact]
    public void SwordDamageIsHalvedAgainstUndead()
    {
        var damage = AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
            11,
            undead: true,
            [CreateWeapon(1)]);

        Assert.Equal(6, damage);
    }

    [Fact]
    public void MaceDamageIsIncreasedAgainstUndead()
    {
        var damage = AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
            11,
            undead: true,
            [CreateWeapon(4)]);

        Assert.Equal(16, damage);
    }

    [Fact]
    public void WeaponClassModifierIgnoresNonUndeadAndNonHandItems()
    {
        var equipment = new[] {
            CreateWeapon(1, slot: 0),
            CreateWeapon(5, slot: 4, itemClass: 2),
        };

        Assert.Equal(
            11,
            AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
                11,
                undead: true,
                equipment));
        Assert.Equal(
            11,
            AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
                11,
                undead: false,
                [CreateWeapon(4)]));
    }

    private static EquippedStoreItem CreateWeapon(
        int itemType,
        uint slot = 4,
        int itemClass = 1)
    {
        return new EquippedStoreItem(
            slot,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = itemType,
                ItemClass = itemClass,
                StatFlag = true,
            });
    }
}
