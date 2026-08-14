namespace Devilution.Diablo;

/** Native weapon damage adjustments based on an external monster capability. */
public static class AuthoritativeMonsterDamageRules
{
    /** Applies DevilutionX's sword/mace modifiers against undead or animal targets. */
    public static int ApplyWeaponClassModifier(
        int damage,
        bool undead,
        IEnumerable<EquippedStoreItem> equipment,
        bool animal = false,
        AuthoritativeItemCatalog? items = null)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));
        ArgumentNullException.ThrowIfNull(equipment);
        if (!undead && !animal)
            return damage;

        var handItems = equipment
            .Where(item => item is not null
                && item.State.StatFlag
                && item.Slot is 4 or 5)
            .ToArray();
        if (handItems.Any(item => AuthoritativeItemClassification.IsMace(item.State, items)))
            return undead
                ? checked(damage + damage / 2)
                : damage - damage / 2;
        if (handItems.Any(item => AuthoritativeItemClassification.IsSword(item.State, items)))
            return undead
                ? damage - damage / 2
                : checked(damage + damage / 2);
        return damage;
    }
}
