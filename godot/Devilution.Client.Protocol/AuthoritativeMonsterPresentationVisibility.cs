using Devilution.Protocol.V1;
using Devilution.Shared;

namespace Devilution.Client.Protocol;

/** Shared visibility rule for projected monster presentation state. */
public static class AuthoritativeMonsterPresentationVisibility
{
    /** Dead and server-hidden monsters never cross the presentation boundary. */
    public static bool IsVisible(MonsterSnapshot monster, uint currentLevelId)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.Alive
            && !monster.SneakHidden
            && AuthoritativeLevelVisibility.IsVisible(monster.LevelId, currentLevelId);
    }

    /** Applies the server's geometry-backed native vision boundary to drawing. */
    public static bool IsVisible(
        MonsterSnapshot monster,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY)
        => IsVisible(monster, currentLevelId, geometry, viewerX, viewerY, 10);

    /** Applies the server's geometry-backed vision boundary for one radius. */
    public static bool IsVisible(
        MonsterSnapshot monster,
        uint currentLevelId,
        LevelGeometrySnapshot? geometry,
        int viewerX,
        int viewerY,
        int viewerLightRadius)
        => IsVisible(monster, currentLevelId)
            && AuthoritativeNativeVisionVisibility.IsVisible(
                monster.LevelId,
                monster.PositionX,
                monster.PositionY,
                currentLevelId,
                geometry,
                viewerX,
                viewerY,
                viewerLightRadius);

}
