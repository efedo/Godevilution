namespace Devilution.Diablo;

/** Applies native difficulty modifiers when a monster is first materialized. */
internal static class DiabloDifficultyMonsterScaling
{
    public static void ApplyInitialHealth(
        AuthoritativeCombatTarget target,
        DiabloDifficulty difficulty,
        DiabloGameVariant gameVariant,
        DiabloGameMode gameMode)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (difficulty == DiabloDifficulty.Normal || target.IsPlayerMinion)
            return;

        var baseMaximumQ6 = target.MaxHitPointsQ6;
        var baseCurrentQ6 = target.HitPointsQ6;
        var (multiplier, bonusWhole) = difficulty switch {
            DiabloDifficulty.Nightmare => (
                3,
                gameVariant == DiabloGameVariant.Hellfire
                    ? gameMode == DiabloGameMode.Multiplayer ? 100 : 50
                    : 100),
            DiabloDifficulty.Hell => (
                4,
                gameVariant == DiabloGameVariant.Hellfire
                    ? gameMode == DiabloGameMode.Multiplayer ? 200 : 100
                    : 200),
            _ => (1, 0),
        };

        var effectiveMaximumQ6 = checked((int)(
            checked((long)baseMaximumQ6 * multiplier)
            + checked((long)bonusWhole * DiabloFixedPointQ6.Scale)));
        var effectiveCurrentQ6 = baseMaximumQ6 == 0
            ? 0
            : checked((int)(
                checked((long)baseCurrentQ6 * effectiveMaximumQ6)
                / baseMaximumQ6));
        target.RestoreHitPointsQ6(effectiveCurrentQ6, effectiveMaximumQ6);
    }

    public static void ApplyInitialResistances(
        AuthoritativeCombatTarget target,
        DiabloDifficulty difficulty,
        AuthoritativeMonsterTypeCatalog? monsterTypes)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (difficulty != DiabloDifficulty.Hell
            || target.IsPlayerMinion
            || monsterTypes is null
            || !monsterTypes.TryGet(target.MonsterId, out var definition)) {
            return;
        }

        target.FireResistance = definition.FireResistanceHell ?? target.FireResistance;
        target.LightningResistance = definition.LightningResistanceHell ?? target.LightningResistance;
        target.MagicResistance = definition.MagicResistanceHell ?? target.MagicResistance;
        target.AcidResistance = definition.AcidResistanceHell ?? target.AcidResistance;
    }
}
