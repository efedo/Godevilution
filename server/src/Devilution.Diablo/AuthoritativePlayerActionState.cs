using Devilution.Domain.Protocol;

namespace Devilution.Diablo;

/** Server-owned player actions whose gameplay effect resolves on an animation tick. */
public enum DiabloPlayerActionKind
{
    Move = 1,
    Attack = 2,
    Cast = 3,
    Death = 4,
    LevelTransition = 5,
    RestartTown = 6,
}

/**
 * Protocol-independent lifecycle state for one in-progress player action.
 * Effect and ready ticks mirror native animation-frame admission boundaries.
 */
public sealed record AuthoritativePlayerActionState(
    DiabloPlayerActionKind Kind,
    ulong StartedAtTick,
    ulong EffectTick,
    ulong ReadyTick,
    ulong EndTick)
{
    public bool EffectApplied { get; set; }

    public int DirectionX { get; init; }

    public int DirectionY { get; init; }

    public uint TargetEntityId { get; init; }

    /** Destination retained for a native cell-targeted attack. */
    public int TargetX { get; init; }

    /** Destination retained for a native cell-targeted attack. */
    public int TargetY { get; init; }

    /** Distinguishes a deferred cell attack from an entity-targeted attack. */
    public bool TargetCell { get; init; }

    public DiabloCastIntent? CastIntent { get; init; }
}

/** Shared native-compatible admission and transition rules for timed player actions. */
internal static class DiabloPlayerActionLifecycle
{
    public static bool CanStart(AuthoritativePlayerState player, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Life <= 0 || player.CombatMode == DiabloPlayerCombatMode.Death)
            return false;
        if (player.CombatMode == DiabloPlayerCombatMode.Stand)
            return player.CurrentAction is null;

        var action = player.CurrentAction;
        return action is not null
            && action.EffectApplied
            && tick >= action.ReadyTick
            && player.CombatMode is DiabloPlayerCombatMode.Attack
                or DiabloPlayerCombatMode.RangedAttack
                or DiabloPlayerCombatMode.Spell;
    }

    public static void Begin(
        AuthoritativePlayerState player,
        AuthoritativePlayerActionState action,
        DiabloPlayerCombatMode mode)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(action);
        if (!AuthoritativePlayerActionTimingRules.IsOrdered(
                action.StartedAtTick,
                action.EffectTick,
                action.ReadyTick,
                action.EndTick)) {
            throw new ArgumentException(
                "Player action ticks must form a valid ordered lifecycle.",
                nameof(action));
        }
        if (mode is not (DiabloPlayerCombatMode.WalkNorthwards
                or DiabloPlayerCombatMode.WalkSouthwards
                or DiabloPlayerCombatMode.WalkSideways
                or DiabloPlayerCombatMode.Attack
                or DiabloPlayerCombatMode.RangedAttack
                or DiabloPlayerCombatMode.Spell
                or DiabloPlayerCombatMode.Death
                or DiabloPlayerCombatMode.NewLevel)) {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        player.CurrentAction = action;
        player.CombatMode = mode;
        player.CombatModeEndTick = action.EndTick;
    }

    public static void Cancel(AuthoritativePlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.CurrentAction = null;
    }
}
