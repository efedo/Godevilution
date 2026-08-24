using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves the server-owned portal endpoint visible on the active level. */
public static class AuthoritativePortalPresentation
{
    /** Resolves the source first, then a bidirectional destination, with level zero as a wildcard. */
    public static PortalEndpoint? Resolve(PortalSnapshot portal, uint currentLevelId)
    {
        ArgumentNullException.ThrowIfNull(portal);
        if (AuthoritativeLevelVisibility.IsVisible(portal.SourceLevelId, currentLevelId))
            return new PortalEndpoint(portal.SourcePositionX, portal.SourcePositionY);
        if (portal.Bidirectional
            && AuthoritativeLevelVisibility.IsVisible(portal.DestinationLevelId, currentLevelId))
            return new PortalEndpoint(portal.DestinationPositionX, portal.DestinationPositionY);
        return null;
    }
}

public readonly record struct PortalEndpoint(int X, int Y);
