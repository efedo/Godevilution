using Devilution.Protocol.V1;

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
}
