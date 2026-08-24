using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Shared visibility rule for server-owned defeated corpse presentation. */
public static class AuthoritativeCorpsePresentationVisibility
{
    /** Corpse tiles are visible only on the active level, including native level-zero compatibility projections. */
    public static bool IsVisible(CorpseSnapshot corpse, uint currentLevelId)
    {
        ArgumentNullException.ThrowIfNull(corpse);
        return AuthoritativeLevelVisibility.IsVisible(corpse.LevelId, currentLevelId);
    }
}
