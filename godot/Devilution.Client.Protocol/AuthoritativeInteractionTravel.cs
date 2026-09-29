using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Repeats ordinary server movement intents, then submits one requested interaction. */
public sealed class AuthoritativeInteractionTravel
{
    private Command? interaction;
    private uint actor;
    private uint sourceLevel;
    private ulong? pendingSequence;
    private (int X, int Y) submittedPosition;

    public bool Active => interaction is not null;
    public (int X, int Y) Target { get; private set; }
    public Command? Interaction => interaction?.Clone();

    public void Start(Snapshot? snapshot, int x, int y, Command command, bool connected)
    {
        Cancel();
        var player = snapshot?.Players.FirstOrDefault();
        if (!connected || player is not { Life: > 0 })
            return;
        actor = player.EntityId;
        sourceLevel = player.LevelId;
        Target = (x, y);
        interaction = command.Clone();
    }

    public void Cancel()
    {
        interaction = null;
        pendingSequence = null;
    }

    /** Presence and range are read from the current authoritative presentation. */
    public Command? Next(Snapshot? snapshot, CommandResult? result, bool connected,
        int pendingCommands, bool targetPresent, bool inRange)
    {
        if (!Active)
            return null;
        var player = snapshot?.Players.FirstOrDefault();
        if (!connected || !targetPresent || player is not { Life: > 0 }
            || player.EntityId != actor || player.LevelId != sourceLevel) {
            Cancel();
            return null;
        }
        if (pendingSequence is { } sequence) {
            if (result?.ClientSequence != sequence)
                return null;
            if (result.Status is not (CommandStatus.Accepted or CommandStatus.Duplicate)
                || result.RejectReason != CommandRejectReason.Unspecified) {
                Cancel();
                return null;
            }
            if (snapshot!.Tick < result.AppliedTick || !Settled(player))
                return null;
            if ((player.PositionX, player.PositionY) == submittedPosition) {
                Cancel();
                return null;
            }
            pendingSequence = null;
        }
        if (pendingCommands != 0 || !Settled(player))
            return null;
        if (inRange) {
            var command = interaction;
            Cancel();
            return command;
        }
        return new Command { MoveRequested = new MoveRequested {
            TargetCell = true, TargetX = Target.X, TargetY = Target.Y,
        } };
    }

    public void Submitted(ulong sequence, PlayerSnapshot player)
    {
        if (!Active || pendingSequence is not null || sequence == 0)
            throw new InvalidOperationException("No interaction approach step can be submitted in this state.");
        pendingSequence = sequence;
        submittedPosition = (player.PositionX, player.PositionY);
    }

    private static bool Settled(PlayerSnapshot player)
        => player.CombatMode == PlayerCombatMode.Stand && player.CurrentAction is null;
}
