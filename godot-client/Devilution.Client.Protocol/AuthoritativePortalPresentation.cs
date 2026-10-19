using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves the server-owned portal endpoint visible on the active level. */
public static class AuthoritativePortalPresentation
{
    /** Resolves the source first, then a bidirectional destination, by exact level identity. */
    public static PortalEndpoint? Resolve(PortalSnapshot portal, uint currentLevelId)
    {
        ArgumentNullException.ThrowIfNull(portal);
        if (portal.SourceLevelId == currentLevelId)
            return new PortalEndpoint(portal.SourcePositionX, portal.SourcePositionY);
        if (portal.Bidirectional
            && portal.DestinationLevelId == currentLevelId)
            return new PortalEndpoint(portal.DestinationPositionX, portal.DestinationPositionY);
        return null;
    }

    /** Resolves a portal endpoint only when its tile crosses projected native vision. */
    public static PortalEndpoint? Resolve(
        PortalSnapshot portal,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY,
        int viewerLightRadius)
    {
        var endpoint = Resolve(portal, currentLevelId);
        if (endpoint is not { } visibleEndpoint)
            return null;
        var endpointLevelId = portal.SourceLevelId == currentLevelId
            ? portal.SourceLevelId
            : portal.DestinationLevelId;
        return AuthoritativeNativeVisionVisibility.IsVisible(
                endpointLevelId,
                visibleEndpoint.X,
                visibleEndpoint.Y,
                currentLevelId,
                geometry,
                viewerX,
                viewerY,
                viewerLightRadius)
            ? visibleEndpoint
            : null;
    }
}

public readonly record struct PortalEndpoint(int X, int Y);
