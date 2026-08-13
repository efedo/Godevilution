namespace Devilution.Diablo;

/** Native weapon damage adjustments based on an external monster capability. */
public static class AuthoritativeMonsterDamageRules
{
    /** Applies DevilutionX's sword/mace modifiers against undead or animal targets. */
    public static int ApplyWeaponClassModifier(
        int damage,
        bool undead,
        IEnumerable<EquippedStoreItem> equipment,
        bool animal = false)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));
        ArgumentNullException.ThrowIfNull(equipment);
        if (!undead && !animal)
            return damage;

        var handTypes = equipment
            .Where(item => item is not null
                && item.State.StatFlag
                && item.Slot is 4 or 5)
            .Select(item => item.State.ItemType)
            .ToArray();
        if (handTypes.Contains(4))
            return undead
                ? checked(damage + damage / 2)
                : damage - damage / 2;
        if (handTypes.Contains(1))
            return undead
                ? damage - damage / 2
                : checked(damage + damage / 2);
        return damage;
    }
}
