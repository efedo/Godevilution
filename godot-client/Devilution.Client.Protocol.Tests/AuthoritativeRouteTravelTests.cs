using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeRouteTravelTests
{
    [Fact]
    public void RepeatsOnlyAfterMatchingAcknowledgementFreshSnapshotAndSettledMovement()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        Assert.Null(travel.Next(snapshot, null, true, 1));
        var command = travel.Next(snapshot, null, true, 0)!;
        Assert.True(command.MoveRequested.TargetCell);
        Assert.Equal(57, command.MoveRequested.TargetX);
        Assert.Equal(40, command.MoveRequested.TargetY);
        travel.Submitted(7, snapshot.Players[0]);
        Assert.Null(travel.Next(snapshot, Receipt(6), true, 0));
        snapshot.Players[0].PositionX = 1;
        Assert.Null(travel.Next(snapshot, Receipt(7), true, 0)); // stale tick
        snapshot.Tick = 11;
        snapshot.Players[0].CombatMode = PlayerCombatMode.WalkSouthwards;
        Assert.Null(travel.Next(snapshot, Receipt(7), true, 0));
        snapshot.Players[0].CombatMode = PlayerCombatMode.Stand;
        Assert.NotNull(travel.Next(snapshot, Receipt(7), true, 0));
        Assert.True(travel.Active);
    }

    [Fact]
    public void ReResolvesCoordinatesFromLatestServerRouteWithoutMutatingSnapshot()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        snapshot.LevelGeometry[0].WarpTriggers[0].TriggerX = 22;
        var before = snapshot.Clone();
        Assert.Equal(22, travel.Next(snapshot, null, true, 0)!.MoveRequested.TargetX);
        Assert.Equal(before, snapshot);
    }

    [Theory]
    [InlineData(CommandStatus.Rejected, CommandRejectReason.TooLate)]
    [InlineData(CommandStatus.Rejected, CommandRejectReason.InvalidTarget)]
    [InlineData(CommandStatus.Duplicate, CommandRejectReason.NotAllowed)]
    public void DoesNotResubmitRejectedOrPreviouslyRejectedCommands(CommandStatus status, CommandRejectReason reason)
    {
        var snapshot = State();
        var travel = Start(snapshot);
        travel.Submitted(7, snapshot.Players[0]);
        var result = Receipt(7);
        result.Status = status;
        result.RejectReason = reason;
        Assert.Null(travel.Next(snapshot, result, true, 0));
        Assert.False(travel.Active);
    }

    [Fact]
    public void AcceptedNoOpStopsInsteadOfFloodingTheServer()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        travel.Submitted(7, snapshot.Players[0]);
        snapshot.Tick = 11;
        Assert.Null(travel.Next(snapshot, Receipt(7), true, 0));
        Assert.False(travel.Active);
        Assert.Contains("did not advance", travel.Status);
    }

    [Fact]
    public void DuplicateAcceptedMovementCanContinueWithoutReplayingTheOldSequence()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        travel.Submitted(7, snapshot.Players[0]);
        snapshot.Tick = 11;
        snapshot.Players[0].PositionX++;
        var receipt = Receipt(7);
        receipt.Status = CommandStatus.Duplicate;
        Assert.NotNull(travel.Next(snapshot, receipt, true, 0));
        travel.Submitted(8, snapshot.Players[0]);
        Assert.Null(travel.Next(snapshot, receipt, true, 0));
    }

    [Theory]
    [InlineData("disconnect")]
    [InlineData("death")]
    [InlineData("character")]
    [InlineData("level")]
    [InlineData("missing")]
    [InlineData("unavailable")]
    [InlineData("manual")]
    public void CancellationNeverAutomaticallyResumes(string cause)
    {
        var snapshot = State();
        var travel = Start(snapshot);
        var changed = snapshot.Clone();
        switch (cause) {
            case "death": changed.Players[0].Life = 0; break;
            case "character": changed.Players[0].EntityId++; break;
            case "level": changed.Players[0].LevelId++; break;
            case "missing": changed.LevelGeometry.Clear(); break;
            case "unavailable": changed.LevelGeometry[0].WarpTriggers[0].Available = false; break;
            case "manual": travel.Cancel(); break;
        }
        Assert.Null(travel.Next(changed, null, cause != "disconnect", 0));
        Assert.False(travel.Active);
        Assert.Null(travel.Next(snapshot, null, true, 0));
    }

    [Fact]
    public void CannotStartUnknownUnavailableOrDisconnectedRoute()
    {
        var snapshot = State();
        var travel = new AuthoritativeRouteTravel();
        Assert.False(travel.Start(snapshot, 999, true));
        Assert.False(travel.Start(snapshot, 1001, false));
        snapshot.LevelGeometry[0].WarpTriggers[0].Available = false;
        Assert.False(travel.Start(snapshot, 1001, true));
        Assert.False(travel.Active);
    }

    [Fact]
    public void BusyPlayerDoesNotProduceInputAndSubmissionCannotOverwriteAnInFlightStep()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        snapshot.Players[0].CurrentAction = new PlayerActionSnapshot();
        Assert.Null(travel.Next(snapshot, null, true, 0));
        snapshot.Players[0].CurrentAction = null;
        Assert.Throws<InvalidOperationException>(() => travel.Submitted(0, snapshot.Players[0]));
        travel.Submitted(7, snapshot.Players[0]);
        Assert.Throws<InvalidOperationException>(() => travel.Submitted(8, snapshot.Players[0]));
    }

    [Fact]
    public void AlreadyAtTriggerStopsWithoutClaimingOrSimulatingATransition()
    {
        var snapshot = State();
        snapshot.Players[0].PositionX = 57;
        snapshot.Players[0].PositionY = 40;
        var before = snapshot.Clone();
        var travel = Start(snapshot);
        Assert.Null(travel.Next(snapshot, null, true, 0));
        Assert.False(travel.Active);
        Assert.Equal("At the route tile; travel stopped.", travel.Status);
        Assert.Equal(before, snapshot);
    }

    private static AuthoritativeRouteTravel Start(Snapshot snapshot)
    {
        var travel = new AuthoritativeRouteTravel();
        Assert.True(travel.Start(snapshot, 1001, true));
        return travel;
    }

    private static CommandResult Receipt(ulong sequence)
        => new() { ClientSequence = sequence, Status = CommandStatus.Accepted, AppliedTick = 11 };

    private static Snapshot State() => new() {
        Tick = 10,
        Players = { new PlayerSnapshot { EntityId = 1, Life = 64, CombatMode = PlayerCombatMode.Stand } },
        LevelGeometry = { new LevelGeometrySnapshot {
            LevelId = 0, Width = 112, Height = 112,
            WarpTriggers = { new WarpTriggerSnapshot {
                WarpId = 1001, TriggerX = 57, TriggerY = 40, TargetLevelId = 1, Available = true,
            } },
        } },
    };
}
