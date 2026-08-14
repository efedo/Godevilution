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
    public void ExplicitContentTagOverridesNativeItemTypeForWeaponClassModifier()
    {
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\n"
                + "7\t4\t1\t1\tWeapon,Mace\t99\n");
        var equipment = new EquippedStoreItem(
            4,
            1,
            AuthoritativeItemState.Empty with {
                ItemIndex = 99,
                ItemType = 1,
                StatFlag = true,
            });

        var damage = AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
            11,
            undead: true,
            [equipment],
            items: items);

        Assert.Equal(16, damage);
    }

    [Fact]
    public void AnimalModifiersReverseTheSwordAndMaceResults()
    {
        Assert.Equal(
            16,
            AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
                11,
                undead: false,
                [CreateWeapon(1)],
                animal: true));
        Assert.Equal(
            6,
            AuthoritativeMonsterDamageRules.ApplyWeaponClassModifier(
                11,
                undead: false,
                [CreateWeapon(4)],
                animal: true));
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
