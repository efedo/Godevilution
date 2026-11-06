namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Native objects.cpp OperateShrineCryptic/OperateShrineHoly: player-owned AddMissile, no cast or mana charge. */
    internal void AddNativeShrineMissile(AuthoritativePlayerState player,
        AuthoritativeProjectileBehavior behavior, int spellLevel, IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var spell = spells.Definitions.Single(value => value.ProjectileBehavior == behavior);
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
