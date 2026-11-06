namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Native objects.cpp OperateShrineCryptic/OperateShrineHoly/OperateFountains: AddMissile without casting or charging mana. */
    internal void AddNativeShrineMissile(AuthoritativePlayerState player,
        AuthoritativeProjectileBehavior behavior, int spellLevel, IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var spell = spells.Definitions.Single(value => value.ProjectileBehavior == behavior);
        if (behavior == AuthoritativeProjectileBehavior.Infravision) {
            var duration = ResolveInfravisionDuration(spell, spellLevel);
            var previous = player.StatusEffects.Where(effect => effect.EffectId == spell.StatusEffectId)
                .Select(effect => effect.RemainingTicks).DefaultIfEmpty().Max();
            player.StatusEffects.RemoveAll(effect => effect.EffectId == spell.StatusEffectId);
            player.StatusEffects.Add(new(spell.StatusEffectId, Math.Max(duration, previous), spell.StatusMagnitude) {
                Behavior = AuthoritativeStatusEffectBehavior.Infravision,
            });
            return;
        }
        var destination = behavior == AuthoritativeProjectileBehavior.Phasing
            ? FindPhasingDestination(player, spell, participants) : null;
        if (behavior == AuthoritativeProjectileBehavior.Phasing && destination is null) return;
        var id = AllocateProjectileEntityId();
        var missile = behavior == AuthoritativeProjectileBehavior.NovaController
            ? CreateNovaController(id, player, spell, spellLevel, player.PositionX, player.PositionY)
            : CreateMovementController(id, player, spell, destination!.Value, AuthoritativeProjectileBehavior.Phasing);
        world.Projectiles.Add(id, missile);
    }
}
