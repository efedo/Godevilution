using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Cancellable user-input repetition. The server chooses and executes every step. */
public sealed class AuthoritativeRouteTravel
{
    private uint sourceLevel;
    private uint actor;
    private uint warpId;
    private ulong? pendingSequence;
    private (int X, int Y) submittedPosition;

    public bool Active { get; private set; }
    public string Status { get; private set; } = "Select a route to walk there. The server controls movement.";

    public bool Start(Snapshot? snapshot, uint selectedWarpId, bool connected)
    {
        Active = false;
        pendingSequence = null;
        var player = snapshot?.Players.FirstOrDefault();
        var route = player is null ? null : Resolve(snapshot!, player.LevelId, selectedWarpId);
        if (!connected || player is not { Life: > 0 } || route?.Available != true) {
            Status = "That route is not available.";
            return false;
        }
        sourceLevel = player.LevelId;
        actor = player.EntityId;
        warpId = selectedWarpId;
        Active = true;
        Status = "Travelling. Cancel or use manual input to stop.";
        return true;
    }

    public void Cancel(string message = "Travel cancelled.")
    {
        if (!Active)
            return;
        Active = false;
        pendingSequence = null;
        Status = message;
    }

    /** Returns at most one intent after the preceding acknowledged move has settled. */
    public Command? Next(Snapshot? snapshot, CommandResult? result, bool connected, int pendingCommands)
    {
        if (!Active)
            return null;
        var player = snapshot?.Players.FirstOrDefault();
        if (!connected || player is not { Life: > 0 } || player.EntityId != actor) {
            Cancel("Travel stopped: connection or character unavailable.");
            return null;
        }
        if (player.LevelId != sourceLevel) {
            Cancel("Level changed; travel stopped.");
            return null;
        }
        var route = Resolve(snapshot!, sourceLevel, warpId);
        if (route?.Available != true) {
            Cancel("Travel stopped: route no longer available.");
            return null;
        }
        if (pendingSequence is { } sequence) {
            if (result?.ClientSequence != sequence)
                return null;
            if (result.Status is not (CommandStatus.Accepted or CommandStatus.Duplicate)
                || result.RejectReason != CommandRejectReason.Unspecified) {
                Cancel($"Travel stopped: {result.Status} / {result.RejectReason}.");
                return null;
            }
            if (snapshot!.Tick < result.AppliedTick || !Settled(player))
                return null;
            if ((player.PositionX, player.PositionY) == submittedPosition) {
                Cancel("Travel stopped: the server did not advance this step.");
                return null;
            }
            pendingSequence = null;
        }
        if (pendingCommands != 0 || !Settled(player))
            return null;
        if (player.PositionX == route.TriggerX && player.PositionY == route.TriggerY) {
            Cancel("At the route tile; travel stopped.");
            return null;
        }
        return new Command { MoveRequested = new MoveRequested {
            TargetCell = true, TargetX = route.TriggerX, TargetY = route.TriggerY,
        } };
    }

    public void Submitted(ulong sequence, PlayerSnapshot player)
    {
        if (!Active || pendingSequence is not null || sequence == 0)
            throw new InvalidOperationException("No route step can be submitted in this state.");
        pendingSequence = sequence;
        submittedPosition = (player.PositionX, player.PositionY);
    }

    private static bool Settled(PlayerSnapshot player)
        => player.CombatMode == PlayerCombatMode.Stand && player.CurrentAction is null;

    private static WarpTriggerSnapshot? Resolve(Snapshot snapshot, uint level, uint id)
        => snapshot.LevelGeometry.FirstOrDefault(geometry => geometry.LevelId == level)
            ?.WarpTriggers.FirstOrDefault(route => route.WarpId == id);
}
