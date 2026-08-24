using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Resolves the native-compatible occupied cells for a projected monster. */
public static class AuthoritativeMonsterPositionProjection
{
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
