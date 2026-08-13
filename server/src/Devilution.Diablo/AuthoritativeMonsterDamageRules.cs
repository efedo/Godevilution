namespace Devilution.Diablo;

/** Native weapon damage adjustments based on an external monster capability. */
public static class AuthoritativeMonsterDamageRules
{
    /** Applies DevilutionX's sword/mace modifiers against undead targets. */
    public static int ApplyWeaponClassModifier(
        int damage,
        bool undead,
        IEnumerable<EquippedStoreItem> equipment)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));
        ArgumentNullException.ThrowIfNull(equipment);
        if (!undead)
            return damage;

        var handTypes = equipment
            .Where(item => item is not null
                && item.State.StatFlag
                && item.Slot is 4 or 5)
            .Select(item => item.State.ItemType)
            .ToArray();
        if (handTypes.Contains(1))
            return damage - damage / 2;
        if (handTypes.Contains(4))
            return checked(damage + damage / 2);
        return damage;
    }
}
