using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** A native walk can land on another stair; the caller's next attempt returns through its origin. */
internal static class AcceptanceRouteRecovery
{
    internal static async Task<bool> TryAsync(Func<Task> travel,
        Func<AcceptanceCommandBoundary.Observation> observe, Func<InvalidOperationException, Task> recover)
    {
        try {
            await travel();
            return true;
        } catch (InvalidOperationException failure) when (observe().Player is { Life: > 0 }) {
            await recover(failure);
            return false;
        }
    }

    /** A complete native path can be blocked beyond the local clearing radius; approach its first blocker. */
    internal static async Task<bool> ClearBlockedRouteAsync(AcceptanceCommandBoundary.Observation boundary,
        Command.IntentOneofCase intent, uint destination, Func<MonsterSnapshot, Task> clear)
    {
        if (!boundary.CommandsAdmitted || !boundary.PlayerSettled || boundary.Player is not { Life: > 0 } player
            || intent != Command.IntentOneofCase.MoveRequested
            || boundary.Receipt is not { Status: CommandStatus.Rejected, RejectReason: CommandRejectReason.InvalidTarget })
            return false;
        var snapshot = boundary.Snapshot!;
        var geometry = snapshot.LevelGeometry.SingleOrDefault(level => level.LevelId == player.LevelId);
        var route = geometry?.WarpTriggers.FirstOrDefault(value => value.Available && value.TargetLevelId == destination);
        if (route is null) return false;
        var path = NativeObjectApproachPath.Find(snapshot, geometry!, new ObjectSnapshot {
            LevelId = player.LevelId, PositionX = route.TriggerX, PositionY = route.TriggerY,
        }, pickup: true, ignoreActors: true);
        if (path is null) return false;
        // Only ignore actors while identifying the obstruction. The existing combat loop
        // approaches it one real input at a time, with collision, healing and retreat checks.
        foreach (var cell in path) {
            var blocker = snapshot.Monsters.Where(monster => monster.Alive && !monster.IsPlayerMinion
                    && AuthoritativeLevelVisibility.IsVisible(monster.LevelId, player.LevelId)
                    && (monster.PositionX == cell.X && monster.PositionY == cell.Y
                        || monster.Moving && monster.FuturePositionX == cell.X && monster.FuturePositionY == cell.Y))
                .OrderBy(monster => monster.EntityId).FirstOrDefault();
            if (blocker is null) continue;
            await clear(blocker);
            return true;
        }
        return false;
    }
}
