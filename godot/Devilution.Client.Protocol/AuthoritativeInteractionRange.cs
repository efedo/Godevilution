using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Mirrors the server's range rules for presentation-side interaction routing. */
public static class AuthoritativeInteractionRange
{
    /** Object, item, trap, and quest/towner intents accept adjacent cells. */
    public static bool IsAdjacent(PlayerSnapshot player, int targetX, int targetY)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Math.Max(
            Math.Abs((long)player.PositionX - targetX),
            Math.Abs((long)player.PositionY - targetY)) <= 1;
    }

    /** Portal use requires the player's exact visible source or bidirectional endpoint. */
    public static bool IsAtPortalEndpoint(PlayerSnapshot player, PortalSnapshot portal)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(portal);
        return AuthoritativePortalPresentation.Resolve(portal, player.LevelId) is { } endpoint
            && player.PositionX == endpoint.X
            && player.PositionY == endpoint.Y;
    }
}
