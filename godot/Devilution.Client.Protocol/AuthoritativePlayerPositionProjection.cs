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
        if (player.CurrentAction is { Kind: PlayerActionKind.Move } move) {
            x += move.DirectionX;
            y += move.DirectionY;
        }
        return new ClientPosition(x, y);
    }
}
