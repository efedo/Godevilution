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

    /** Native ItemSpecialEffect::Knockback flag. */
    public const uint KnockbackFlag = 1U << 11;

    /** Native ItemSpecialEffect::MultipleArrows flag. */
    public const uint MultipleArrowsFlag = 1U << 9;

    /** Native ItemSpecialEffect::RandomArrowVelocity flag. */
    public const uint RandomArrowVelocityFlag = 1U << 2;

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
        Func<int, int> generateRnd,
        int randomStealLifeDivisor = 8,
        int stealLife3Percent = 3,
        int stealLife5Percent = 5,
        int stealMana3Percent = 3,
        int stealMana5Percent = 5)
    {
        if (damageQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(damageQ6));
        ArgumentNullException.ThrowIfNull(generateRnd);
        if (randomStealLifeDivisor <= 0
            || stealLife3Percent is < 0 or > 100
            || stealLife5Percent is < 0 or > 100
            || stealMana3Percent is < 0 or > 100
            || stealMana5Percent is < 0 or > 100) {
            throw new ArgumentOutOfRangeException(nameof(randomStealLifeDivisor));
        }
        if (damageQ6 == 0)
            return new ResourceGain(0, 0);

        var lifeQ6 = 0;
        if ((usableEquipmentFlags & RandomStealLifeFlag) != 0)
            lifeQ6 = generateRnd(damageQ6 / randomStealLifeDivisor);

        if ((usableEquipmentFlags & (StealLife3Flag | StealLife5Flag)) != 0) {
            // Native PlrHitMonst reuses the same temporary amount for each
            // fixed-rate branch. A fixed life-steal flag therefore replaces,
            // rather than adds to, an earlier RandomStealLife roll.
            if ((usableEquipmentFlags & StealLife5Flag) != 0)
                lifeQ6 = checked(stealLife5Percent * damageQ6 / 100);
            else if ((usableEquipmentFlags & StealLife3Flag) != 0)
                lifeQ6 = checked(stealLife3Percent * damageQ6 / 100);
        }

        var manaQ6 = 0;
        if ((usableEquipmentFlags & NoManaFlag) == 0) {
            if ((usableEquipmentFlags & StealMana5Flag) != 0)
                manaQ6 = checked(stealMana5Percent * damageQ6 / 100);
            else if ((usableEquipmentFlags & StealMana3Flag) != 0)
                manaQ6 = checked(stealMana3Percent * damageQ6 / 100);
        }

        return new ResourceGain(lifeQ6, manaQ6);
    }
}
