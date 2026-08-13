namespace Devilution.Diablo;

/** Native equipment effects that restore player resources after a physical hit. */
public static class AuthoritativeEquipmentAttackEffectRules
{
    /** Native ItemSpecialEffect::RandomStealLife flag. */
    public const uint RandomStealLifeFlag = 1U << 1;

    /** Native ItemSpecialEffect::StealMana3 flag. */
    public const uint StealMana3Flag = 1U << 13;

    /** Native ItemSpecialEffect::StealMana5 flag. */
    public const uint StealMana5Flag = 1U << 14;

    /** Native ItemSpecialEffect::StealLife3 flag. */
    public const uint StealLife3Flag = 1U << 15;

    /** Native ItemSpecialEffect::StealLife5 flag. */
    public const uint StealLife5Flag = 1U << 16;

    /** Native ItemSpecialEffect::NoMana flag; it suppresses mana-on-hit. */
    public const uint NoManaFlag = AuthoritativeEquipmentManaRules.NoManaFlag;

    /** Native ItemSpecialEffect::TripleDemonDamage flag. */
    public const uint TripleDemonDamageFlag = 1U << 30;

    /** Resource gains in the simulation's native Q6 unit. */
    public readonly record struct ResourceGain(int LifeQ6, int ManaQ6);

    /**
     * Resolves native post-hit resource gains. The random callback receives the
     * native exclusive upper bound used by GenerateRnd and returns a value in
     * that range.
     */
    public static ResourceGain Resolve(
        int damageQ6,
        uint usableEquipmentFlags,
        Func<int, int> generateRnd)
    {
        if (damageQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(damageQ6));
        ArgumentNullException.ThrowIfNull(generateRnd);
        if (damageQ6 == 0)
            return new ResourceGain(0, 0);

        var lifeQ6 = 0;
        if ((usableEquipmentFlags & RandomStealLifeFlag) != 0)
            lifeQ6 = generateRnd(damageQ6 / 8);

        if ((usableEquipmentFlags & (StealLife3Flag | StealLife5Flag)) != 0) {
            if ((usableEquipmentFlags & StealLife5Flag) != 0)
                lifeQ6 = checked(lifeQ6 + 5 * damageQ6 / 100);
            else if ((usableEquipmentFlags & StealLife3Flag) != 0)
                lifeQ6 = checked(lifeQ6 + 3 * damageQ6 / 100);
        }

        var manaQ6 = 0;
        if ((usableEquipmentFlags & NoManaFlag) == 0) {
            if ((usableEquipmentFlags & StealMana5Flag) != 0)
                manaQ6 = checked(5 * damageQ6 / 100);
            else if ((usableEquipmentFlags & StealMana3Flag) != 0)
                manaQ6 = checked(3 * damageQ6 / 100);
        }

        return new ResourceGain(lifeQ6, manaQ6);
    }
}
