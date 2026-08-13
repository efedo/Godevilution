namespace Devilution.Diablo;

/** Result of applying one native fixed-point damage amount to a player. */
public sealed record AuthoritativePlayerDamageResult(
    int IncomingDamageQ6,
    int LifeDamageQ6,
    int ManaDamageQ6,
    bool ManaShieldDepleted);

/** Content-driven native Mana Shield conversion shared by every player-damage source. */
public sealed class AuthoritativePlayerDamageResolver
{
    private readonly AuthoritativeSpellDefinition? manaShield;

    public AuthoritativePlayerDamageResolver(AuthoritativeSpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        manaShield = spells.Definitions.SingleOrDefault(definition =>
            definition.ProjectileBehavior == AuthoritativeProjectileBehavior.ManaShield);
    }

    public bool SupportsManaShield => manaShield is not null;

    public AuthoritativePlayerDamageResult Apply(
        AuthoritativePlayerState player,
        int incomingDamageQ6,
        int minimumLifeQ6 = 0)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (incomingDamageQ6 < 0)
            throw new ArgumentOutOfRangeException(nameof(incomingDamageQ6));
        if (minimumLifeQ6 < 0 || minimumLifeQ6 > player.Life)
            throw new ArgumentOutOfRangeException(nameof(minimumLifeQ6));
        if (incomingDamageQ6 == 0) {
            return new AuthoritativePlayerDamageResult(0, 0, 0, false);
        }

        if (!player.ManaShieldActive
            || AuthoritativeEquipmentManaRules.HasNoManaItem(player.Equipment)) {
            var unshieldedLifeDamageQ6 = Math.Min(
                player.Life - minimumLifeQ6,
                incomingDamageQ6);
            player.Life -= unshieldedLifeDamageQ6;
            return new AuthoritativePlayerDamageResult(
                incomingDamageQ6,
                unshieldedLifeDamageQ6,
                0,
                false);
        }
        if (manaShield is null) {
            throw new InvalidOperationException(
                "Active Mana Shield state requires a ManaShield spell definition.");
        }

        var learnedLevel = player.MemorizedSpells
            .Where(spell => spell.SpellId == manaShield.SpellId)
            .Select(spell => checked((int)spell.Level))
            .FirstOrDefault();
        var divisor = manaShield.ManaShieldBaseDamageReductionDivisor;
        var manaDamageQ6 = incomingDamageQ6;
        if (learnedLevel > 0) {
            divisor = checked(
                divisor
                - Math.Min(
                    learnedLevel,
                    manaShield.ManaShieldDamageReductionMaximumLevel)
                    * manaShield.ManaShieldDamageReductionLevelStep);
            manaDamageQ6 = checked(manaDamageQ6 + manaDamageQ6 / -divisor);
        }

        if (player.Mana >= manaDamageQ6) {
            player.Mana -= manaDamageQ6;
            var depleted = player.Mana == 0;
            if (depleted)
                player.ManaShieldActive = false;
            return new AuthoritativePlayerDamageResult(
                incomingDamageQ6,
                0,
                manaDamageQ6,
                depleted);
        }

        var consumedManaQ6 = player.Mana;
        var lifeDamageQ6 = checked(manaDamageQ6 - consumedManaQ6);
        if (learnedLevel > 0)
            lifeDamageQ6 = checked(lifeDamageQ6 + lifeDamageQ6 / (divisor - 1));
        player.Mana = 0;
        player.ManaShieldActive = false;
        lifeDamageQ6 = Math.Min(player.Life - minimumLifeQ6, lifeDamageQ6);
        player.Life -= lifeDamageQ6;
        return new AuthoritativePlayerDamageResult(
            incomingDamageQ6,
            lifeDamageQ6,
            consumedManaQ6,
            true);
    }
}
