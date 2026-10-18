namespace Devilution.Diablo;

public sealed partial class DiabloCombatCommandProcessor
{
    /** Native ranged destination input preserves the current walk, replacing only future work. */
    private DiabloCombatIntentResult QueueNativeRangedAttack(
        AuthoritativePlayerState player,
        DiabloCombatIntent intent,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (player.Life <= 0 || GetAttackAnimationProfile(player) != DiabloPlayerAttackAnimationProfile.Bow)
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        DiabloNativeQueuedRangedAttackState pending;
        if (intent is DiabloAttackAtIntent cell) {
            if (!world.ContainsPosition(player.LevelId, cell.TargetX, cell.TargetY))
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            pending = new(player.LevelId, 0, cell.TargetX, cell.TargetY, true);
        } else if (intent is DiabloAttackIntent entity) {
            if (!TryGetAttackTarget(player, entity.TargetEntityId, ranged: true, out _,
                    requireMissileLineOfSight: !world.OwnsNativePopulationLevel(player.LevelId))
                && !TryGetPlayerAttackTarget(player, entity.TargetEntityId, ranged: true, players, out _))
                return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            pending = new(player.LevelId, entity.TargetEntityId, 0, 0, false);
        } else {
            return DiabloCombatIntentResult.Rejected(DiabloRuleFailure.Malformed);
        }

        player.NativeWalkPath = null;
        player.NativeQueuedRangedAttack = pending;
        return DiabloCombatIntentResult.Accepted;
    }

    /** CheckNewPath runs after the old mode completes, before another walk step or monster turn. */
    private void AdvanceNativeQueuedRangedAttack(
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        bool completedPriorMode,
        ICollection<DiabloDirectedGameplayEvent> events)
    {
        if (player.NativeQueuedRangedAttack is not { } pending)
            return;
        if (player.Life <= 0 || pending.LevelId != player.LevelId
            || player.CombatMode is DiabloPlayerCombatMode.Death
                or DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit) {
            player.NativeQueuedRangedAttack = null;
            return;
        }
        if (player.CurrentAction is not null || player.CombatMode != DiabloPlayerCombatMode.Stand)
            return;

        // The accepted input is consumed once. A vanished target or changed weapon
        // cancels the destination action without a second receipt or random draw.
        player.NativeQueuedRangedAttack = null;
        if (!useNativePlayerActionTiming || !world.SupportsNativeTargetCellPathing
            || GetAttackAnimationProfile(player) != DiabloPlayerAttackAnimationProfile.Bow)
            return;
        var result = pending.TargetCell
            ? AttackAt(player, pending.TargetX, pending.TargetY, tick, players, completedPriorMode)
            : Attack(player, pending.TargetEntityId, tick, players, completedPriorMode);
        if (result.Succeeded) {
            AddDirectedEvents(player, result, events);
            if (completedPriorMode)
                AdvancePlayerAction(player, tick, players, events);
        }
    }
}
