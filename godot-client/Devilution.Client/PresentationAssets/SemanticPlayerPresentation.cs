using Devilution.Protocol.V1;

namespace Devilution.Client.PresentationAssets;

/** Selects client-only actor art from an authoritative player snapshot. */
public static class SemanticPlayerPresentation
{
    /**
     * Selects the unique most-specific matching rule. Unknown server enum
     * values and equal-specificity overlaps deliberately produce no result so
     * the caller can keep the procedural/default path.
     */
    public static bool TrySelect(
        PresentationAssetCatalog catalog,
        PlayerSnapshot player,
        out PresentationActorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(player);
        selection = default!;

        if (!Enum.IsDefined(typeof(PlayerCombatMode), player.CombatMode)
            || !Enum.IsDefined(typeof(PlayerDirection), player.FacingDirection))
            return false;

        var action = ResolveAction(player);
        if (action is null)
            return false;

        PresentationActorRule? best = null;
        var bestSpecificity = -1;
        var ambiguous = false;
        foreach (var rule in catalog.ActorRules) {
            if (!Matches(rule.Selector, player, action.Value))
                continue;
            var specificity = rule.Selector.Specificity;
            if (specificity > bestSpecificity) {
                best = rule;
                bestSpecificity = specificity;
                ambiguous = false;
            } else if (specificity == bestSpecificity) {
                ambiguous = true;
            }
        }

        if (best is null || ambiguous)
            return false;
        selection = new PresentationActorSelection(best.Id, best.AssetId, bestSpecificity);
        return true;
    }

    private static bool Matches(
        PresentationActorSelector selector,
        PlayerSnapshot player,
        PresentationActorAction action)
        => (!selector.CharacterClassId.HasValue || selector.CharacterClassId.Value == player.CharacterClassId)
            && (!selector.Action.HasValue || selector.Action.Value == action)
            && (!selector.CombatMode.HasValue || selector.CombatMode.Value == player.CombatMode)
            && (!selector.FacingDirection.HasValue || selector.FacingDirection.Value == player.FacingDirection);

    private static PresentationActorAction? ResolveAction(PlayerSnapshot player)
    {
        var kind = player.CurrentAction?.Kind ?? PlayerActionKind.Unspecified;
        if (kind == PlayerActionKind.Unspecified)
            return PresentationActorAction.Idle;
        if (!Enum.IsDefined(typeof(PlayerActionKind), kind))
            return null;
        return kind switch {
            PlayerActionKind.Move => PresentationActorAction.Move,
            PlayerActionKind.Attack => PresentationActorAction.Attack,
            PlayerActionKind.Cast => PresentationActorAction.Cast,
            PlayerActionKind.Death => PresentationActorAction.Death,
            PlayerActionKind.LevelTransition => PresentationActorAction.LevelTransition,
            PlayerActionKind.RestartTown => PresentationActorAction.RestartTown,
            _ => null,
        };
    }
}
