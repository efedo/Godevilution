using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

public sealed partial class AuthoritativeClientModel
{
    private MovementIntent? movementIntent;
    /** At most one unconfirmed tile beyond the native reserved tile; never gameplay occupancy. */
    public ClientPosition? LocalMovementTarget { get; private set; }

    private void RememberMovement(ulong sequence, int dx, int dy)
    {
        if (Snapshot?.Players.FirstOrDefault() is not { Life: > 0 } player) return;
        var origin = AuthoritativePlayerPositionProjection.Resolve(player);
        movementIntent = new(sequence, player.EntityId, player.LevelId,
            new ClientPosition(origin.X + dx, origin.Y + dy), false, null);
    }

    /** Target clicks and held directions share the same bounded presentation prediction. */
    public void TrackPredictedMoveTo(ulong sequence, int x, int y, bool followPath = true)
    {
        if (sequence == 0 || Snapshot?.Players.FirstOrDefault() is not { Life: > 0 } player) return;
        var target = new ClientPosition(x, y);
        if (!followPath) {
            var first = FirstVisualStep(Snapshot!, player, AuthoritativePlayerPositionProjection.Resolve(player), target);
            if (first is null) { movementIntent = null; LocalMovementTarget = null; return; }
            target = first.Value;
        }
        movementIntent = new(sequence, player.EntityId, player.LevelId, target, followPath, null);
        RebuildMovementTarget();
    }

    private void ObserveMovementResult(CommandResult result)
    {
        if (movementIntent is not { } intent || intent.Sequence != result.ClientSequence) return;
        movementIntent = AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(result)
            ? intent with { AppliedTick = result.AppliedTick } : null;
    }

    private void RebuildMovementTarget()
    {
        LocalMovementTarget = null;
        if (movementIntent is not { } intent) return;
        var player = Snapshot?.Players.FirstOrDefault();
        if (player is not { Life: > 0 } || player.EntityId != intent.Entity || player.LevelId != intent.Level) {
            movementIntent = null;
            return;
        }
        if (player.CurrentAction is { Kind: not PlayerActionKind.Move }) return;
        if (intent.AppliedTick is { } applied && Snapshot!.Tick >= applied
            && player.CurrentAction is null && !player.NativeWalkPathPending) {
            movementIntent = null;
            return;
        }
        var origin = AuthoritativePlayerPositionProjection.Resolve(player);
        if (!intent.FollowPath) {
            if (IsProjectedMoveAvailable(Snapshot!, player, intent.Target.X, intent.Target.Y))
                LocalMovementTarget = intent.Target;
            return;
        }
        LocalMovementTarget = FirstVisualStep(Snapshot!, player, origin, intent.Target);
    }

    /** Native engine/path.cpp ordering, using only projected collision; never advances gameplay. */
    private static ClientPosition? FirstVisualStep(Snapshot snapshot, PlayerSnapshot player,
        ClientPosition origin, ClientPosition destination)
    {
        if (origin == destination || (uint)origin.X > byte.MaxValue || (uint)origin.Y > byte.MaxValue
            || (uint)destination.X > byte.MaxValue || (uint)destination.Y > byte.MaxValue) return null;
        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        bool Corner(int x, int y) => geometry is null || (x >= 0 && y >= 0 && x < geometry.Width && y < geometry.Height
            && !geometry.BlockedCells.Contains(y * geometry.Width + x)
            && !geometry.StaticObjectBlockedCells.Contains(y * geometry.Width + x));
        var path = NativeWalkPredictionPathfinder.FindPath(origin.X, origin.Y, destination.X, destination.Y,
            NativeWalkPredictionPathfinder.MaximumPathLength,
            (x, y) => IsProjectedMoveAvailable(snapshot, player, x, y),
            (x, y, nx, ny) => x == nx || y == ny || Corner(nx, y) && Corner(x, ny));
        if (path.Length == 0) return null;
        var step = NativeWalkPredictionPathfinder.GetDisplacement(path[0]);
        var next = new ClientPosition(origin.X + step.X, origin.Y + step.Y);
        return IsProjectedMoveAvailable(snapshot, player, next.X, next.Y) ? next : null;
    }

    private sealed record MovementIntent(ulong Sequence, uint Entity, uint Level, ClientPosition Target,
        bool FollowPath, ulong? AppliedTick);
}
