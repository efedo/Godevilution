using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves the server-owned presentation position for one player. */
public static class AuthoritativePlayerPositionProjection
{
    /**
     * Returns the reserved destination while a native-timed walk is in
     * progress, otherwise the authoritative occupied tile.
     */
    public static ClientPosition Resolve(PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var x = player.PositionX;
        var y = player.PositionY;
        if (player.CurrentAction is { Kind: PlayerActionKind.Move, EffectApplied: false } move) {
            x += move.DirectionX;
            y += move.DirectionY;
        }
        return new ClientPosition(x, y);
    }

    /**
     * Returns the presentation position at an authoritative snapshot tick.
     *
     * Only an in-progress walk is interpolated. Gameplay and target selection
     * continue to use Resolve, which returns the discrete occupied or reserved
     * tile. A missing or zero-length timing window is treated as an immediate
     * move so malformed timing cannot produce an unbounded presentation state.
     */
    public static ClientPresentationPosition ResolvePresentation(
        PlayerSnapshot player,
        ulong snapshotTick)
        => ResolvePresentation(player, (double)snapshotTick);

    /** Resolves an in-progress walk at a fractional presentation tick. */
    public static ClientPresentationPosition ResolvePresentation(
        PlayerSnapshot player,
        double snapshotTick)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!double.IsFinite(snapshotTick) || snapshotTick < 0)
            return ResolvePresentation(player, 0UL);

        var x = (double)player.PositionX;
        var y = (double)player.PositionY;
        var action = player.CurrentAction;
        if (action is not { Kind: PlayerActionKind.Move, EffectApplied: false })
            return new ClientPresentationPosition(x, y);

        var destinationX = x + action.DirectionX;
        var destinationY = y + action.DirectionY;
        if (action.EffectTick <= action.StartedAtTick)
            return new ClientPresentationPosition(destinationX, destinationY);

        var startedTick = (double)action.StartedAtTick;
        var duration = action.EffectTick - action.StartedAtTick;
        var elapsed = Math.Clamp(snapshotTick - startedTick, 0, (double)duration);
        var progress = elapsed / duration;
        return new ClientPresentationPosition(
            x + action.DirectionX * progress,
            y + action.DirectionY * progress);
    }

    /**
     * Resolves a local presentation position, preferring bounded prediction
     * while it remains queued and otherwise using the same authoritative
     * action-tick projection as remote players.
     */
    public static ClientPresentationPosition ResolvePresentation(
        PlayerSnapshot player,
        ulong snapshotTick,
        ClientPosition? predicted)
        => ResolvePresentation(player, (double)snapshotTick, predicted);

    /** Resolves a local fractional presentation position with bounded prediction. */
    public static ClientPresentationPosition ResolvePresentation(
        PlayerSnapshot player,
        double snapshotTick,
        ClientPosition? predicted)
    {
        ArgumentNullException.ThrowIfNull(player);
        return predicted is { } position
            ? new ClientPresentationPosition(position.X, position.Y)
            : ResolvePresentation(player, snapshotTick);
    }

    /**
     * Returns true when a cached player presentation position must snap to the
     * next authoritative position instead of interpolating toward it.
     */
    public static bool RequiresPresentationReset(PlayerSnapshot previous, PlayerSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);

        if (previous.EntityId != next.EntityId
            || AuthoritativeEntityPositionProjection.RequiresLevelReset(
                previous.LevelId,
                next.LevelId))
            return true;

        if (IsDiscontinuousAction(previous.CurrentAction)
            || IsDiscontinuousAction(next.CurrentAction))
            return true;

        var previousPosition = Resolve(previous);
        var nextPosition = Resolve(next);
        return Math.Abs(nextPosition.X - previousPosition.X) > 1
            || Math.Abs(nextPosition.Y - previousPosition.Y) > 1;
    }

    private static bool IsDiscontinuousAction(PlayerActionSnapshot? action)
        => action?.Kind is PlayerActionKind.LevelTransition or PlayerActionKind.RestartTown;
}

/** A fractional, presentation-only position derived from authoritative state. */
public readonly record struct ClientPresentationPosition(double X, double Y);
