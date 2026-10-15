namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    /** Native BreakObject effect boundary, shared with the ordinary melee callback. */
    internal DiabloCombatIntentResult BreakNativeBarrelAtEffectBoundary(
        AuthoritativePlayerState player,
        uint objectEntityId,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (!WorldState.Objects.TryGetValue(objectEntityId, out var barrel)
            || player.LevelId != barrel.LevelId
            || !WorldState.CanBreakNativeBarrel(barrel))
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var events = new List<DiabloGameplayEvent>();
        return combatCommands.TryBreakNativeMeleeBarrel(player, barrel, tick, participants, events)
            ? new DiabloCombatIntentResult(true, null, events)
            : DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
    }
}
