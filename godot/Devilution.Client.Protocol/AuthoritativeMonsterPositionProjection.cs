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

    /** Returns true when a cached monster position must snap to the next state. */
    public static bool RequiresPresentationReset(
        MonsterSnapshot previous,
        MonsterSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);

        if (previous.EntityId != next.EntityId
            || AuthoritativeEntityPositionProjection.RequiresLevelReset(
                previous.LevelId,
                next.LevelId))
            return true;

        var previousPosition = ResolvePresentation(previous);
        var nextPosition = ResolvePresentation(next);
        return Math.Abs(nextPosition.X - previousPosition.X) > 1
            || Math.Abs(nextPosition.Y - previousPosition.Y) > 1;
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
