using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves the native-compatible occupied cells for a projected monster. */
public static class AuthoritativeMonsterPositionProjection
{
    /** Resolves the server-owned presentation destination for a monster. */
    public static ClientPresentationPosition ResolvePresentation(MonsterSnapshot monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.Moving
            ? new ClientPresentationPosition(monster.FuturePositionX, monster.FuturePositionY)
            : new ClientPresentationPosition(monster.PositionX, monster.PositionY);
    }

    /**
     * Native walking keeps both the current origin and the authoritative future
     * destination occupied until movement completes.
     */
    public static bool IsAt(MonsterSnapshot monster, int targetX, int targetY)
        => (monster.PositionX == targetX && monster.PositionY == targetY)
            || (monster.Moving
                && monster.FuturePositionX == targetX
                && monster.FuturePositionY == targetY);
}
