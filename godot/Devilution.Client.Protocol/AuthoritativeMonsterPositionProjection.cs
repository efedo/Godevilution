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
     * Resolves a monster at an authoritative snapshot tick. Older snapshots
     * omit timing and retain the discrete future-tile presentation.
     */
    public static ClientPresentationPosition ResolvePresentation(
        MonsterSnapshot monster,
        ulong snapshotTick)
        => ResolvePresentation(monster, (double)snapshotTick);

    /**
     * Resolves a monster at a fractional presentation tick. The fractional
     * tick lets a presentation client advance a timed walk between network
     * snapshots without changing the discrete authoritative occupancy.
     */
    public static ClientPresentationPosition ResolvePresentation(
        MonsterSnapshot monster,
        double snapshotTick)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if (!double.IsFinite(snapshotTick) || snapshotTick < 0)
            return ResolvePresentation(monster);
        if (!monster.Moving
            || monster.MovementStartedTick == 0
                && monster.MovementEffectTick == 0
            || monster.MovementEffectTick <= monster.MovementStartedTick) {
            return ResolvePresentation(monster);
        }

        var startedTick = (double)monster.MovementStartedTick;
        var duration = monster.MovementEffectTick - monster.MovementStartedTick;
        var elapsed = Math.Clamp(snapshotTick - startedTick, 0, (double)duration);
        var progress = elapsed / duration;
        return new ClientPresentationPosition(
            monster.PositionX
                + ((double)monster.FuturePositionX - monster.PositionX) * progress,
            monster.PositionY
                + ((double)monster.FuturePositionY - monster.PositionY) * progress);
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
