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
            Math.Abs(player.PositionX - targetX),
            Math.Abs(player.PositionY - targetY)) <= 1;
    }

    /** Portal use requires the player's exact source or bidirectional endpoint. */
    public static bool IsAtPortalEndpoint(PlayerSnapshot player, PortalSnapshot portal)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(portal);
        return player.LevelId == portal.SourceLevelId
            && player.PositionX == portal.SourcePositionX
            && player.PositionY == portal.SourcePositionY
            || portal.Bidirectional
                && player.LevelId == portal.DestinationLevelId
                && player.PositionX == portal.DestinationPositionX
                && player.PositionY == portal.DestinationPositionY;
    }
}
