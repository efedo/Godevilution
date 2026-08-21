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
