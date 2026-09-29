using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeInteractionTravelTests
{
    [Fact]
    public void DistantPickupRepeatsAcknowledgedSettledStepsThenSubmitsExactlyOnce()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        for (ulong sequence = 1; sequence <= 3; sequence++) {
            var before = snapshot.Clone();
            var move = travel.Next(snapshot, sequence == 1 ? null : Receipt(sequence - 1), true, 0, true, false)!;
            Assert.True(move.MoveRequested.TargetCell);
            Assert.Equal(4, move.MoveRequested.TargetX);
            Assert.Equal(0, move.MoveRequested.TargetY);
            Assert.Equal(before, snapshot);
            travel.Submitted(sequence, snapshot.Players[0]);
            Assert.Null(travel.Next(snapshot, Receipt(sequence - 1), true, 0, true, false));
            snapshot.Players[0].PositionX++;
            snapshot.Tick++;
        }
        var command = travel.Next(snapshot, Receipt(3), true, 0, true, true)!;
        Assert.Equal(99U, command.PickupWorldItemRequested.ItemEntityId);
        Assert.False(travel.Active);
        Assert.Null(travel.Next(snapshot, Receipt(3), true, 0, true, true));
    }

    [Fact]
    public void WaitsForMatchingReceiptFreshSnapshotAndSettledActionEvenWhenNowInRange()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        travel.Submitted(1, snapshot.Players[0]);
        snapshot.Players[0].PositionX++;
        Assert.Null(travel.Next(snapshot, Receipt(2), true, 0, true, true));
        Assert.Null(travel.Next(snapshot, Receipt(1), true, 0, true, true));
        snapshot.Tick++;
        snapshot.Players[0].CombatMode = PlayerCombatMode.WalkSouthwards;
        Assert.Null(travel.Next(snapshot, Receipt(1), true, 0, true, true));
        snapshot.Players[0].CombatMode = PlayerCombatMode.Stand;
        snapshot.Players[0].CurrentAction = new PlayerActionSnapshot();
        Assert.Null(travel.Next(snapshot, Receipt(1), true, 0, true, true));
        snapshot.Players[0].CurrentAction = null;
        Assert.Null(travel.Next(snapshot, Receipt(1), true, 1, true, true));
        Assert.NotNull(travel.Next(snapshot, Receipt(1), true, 0, true, true));
    }

    [Theory]
    [InlineData(CommandStatus.Rejected, CommandRejectReason.InvalidTarget)]
    [InlineData(CommandStatus.Duplicate, CommandRejectReason.NotAllowed)]
    public void RejectionCancelsWithoutRetry(CommandStatus status, CommandRejectReason reason)
    {
        var snapshot = State();
        var travel = Start(snapshot);
        travel.Submitted(1, snapshot.Players[0]);
        var receipt = Receipt(1);
        receipt.Status = status;
        receipt.RejectReason = reason;
        Assert.Null(travel.Next(snapshot, receipt, true, 0, true, false));
        Assert.False(travel.Active);
    }

    [Fact]
    public void DuplicateAcceptedStepCanContinueButAcceptedNoOpStops()
    {
        var snapshot = State();
        var travel = Start(snapshot);
        travel.Submitted(1, snapshot.Players[0]);
        snapshot.Tick++;
        snapshot.Players[0].PositionX++;
        var receipt = Receipt(1);
        receipt.Status = CommandStatus.Duplicate;
        Assert.NotNull(travel.Next(snapshot, receipt, true, 0, true, false));
        travel.Submitted(2, snapshot.Players[0]);
        snapshot.Tick++;
        Assert.Null(travel.Next(snapshot, Receipt(2), true, 0, true, false));
        Assert.False(travel.Active);
    }

    [Theory]
    [InlineData("disconnect")]
    [InlineData("death")]
    [InlineData("character")]
    [InlineData("level")]
    [InlineData("missing")]
    [InlineData("manual")]
    public void CancellationDoesNotAutomaticallyResume(string cause)
    {
        var snapshot = State();
        var travel = Start(snapshot);
        var changed = snapshot.Clone();
        switch (cause) {
            case "death": changed.Players[0].Life = 0; break;
            case "character": changed.Players[0].EntityId++; break;
            case "level": changed.Players[0].LevelId++; break;
            case "manual": travel.Cancel(); break;
        }
        Assert.Null(travel.Next(changed, null, cause != "disconnect", 0, cause != "missing", false));
        Assert.False(travel.Active);
        Assert.Null(travel.Next(snapshot, null, true, 0, true, false));
    }

    [Fact]
    public void ClonesInputAndReturnedPresentationCopy()
    {
        var snapshot = State();
        var command = Pickup();
        var travel = new AuthoritativeInteractionTravel();
        travel.Start(snapshot, 4, 0, command, true);
        command.PickupWorldItemRequested.ItemEntityId = 5;
        travel.Interaction!.PickupWorldItemRequested.ItemEntityId = 6;
        Assert.Equal(99U, travel.Next(snapshot, null, true, 0, true, true)!.PickupWorldItemRequested.ItemEntityId);
    }

    [Fact]
    public void PortalUsesExactEndpointInsteadOfItemAdjacency()
    {
        var snapshot = State();
        var portal = new PortalSnapshot { PortalId = 42, SourcePositionX = 1, SourcePositionY = 0 };
        var travel = new AuthoritativeInteractionTravel();
        travel.Start(snapshot, 1, 0, new Command { UsePortalRequested = new UsePortalRequested { PortalId = 42 } }, true);
        Assert.NotNull(travel.Next(snapshot, null, true, 0, true,
            AuthoritativeInteractionRange.IsAtPortalEndpoint(snapshot.Players[0], portal))!.MoveRequested);
        travel.Submitted(1, snapshot.Players[0]);
        snapshot.Players[0].PositionX = 1;
        snapshot.Tick++;
        Assert.Equal(42U, travel.Next(snapshot, Receipt(1), true, 0, true,
            AuthoritativeInteractionRange.IsAtPortalEndpoint(snapshot.Players[0], portal))!.UsePortalRequested.PortalId);
    }

    [Fact]
    public void CannotStartWithoutLivePlayerOrOverwriteInFlightSequence()
    {
        var snapshot = State();
        var travel = new AuthoritativeInteractionTravel();
        travel.Start(snapshot, 4, 0, Pickup(), false);
        Assert.False(travel.Active);
        travel.Start(null, 4, 0, Pickup(), true);
        Assert.False(travel.Active);
        travel.Start(snapshot, 4, 0, Pickup(), true);
        Assert.Throws<InvalidOperationException>(() => travel.Submitted(0, snapshot.Players[0]));
        travel.Submitted(1, snapshot.Players[0]);
        Assert.Throws<InvalidOperationException>(() => travel.Submitted(2, snapshot.Players[0]));
    }

    private static Command Pickup() => new() { PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 99 } };
    private static AuthoritativeInteractionTravel Start(Snapshot snapshot)
    {
        var travel = new AuthoritativeInteractionTravel();
        travel.Start(snapshot, 4, 0, Pickup(), true);
        return travel;
    }
    private static CommandResult Receipt(ulong sequence)
        => new() { ClientSequence = sequence, AppliedTick = 10 + sequence, Status = CommandStatus.Accepted };
    private static Snapshot State() => new() {
        Tick = 10, Players = { new PlayerSnapshot { EntityId = 1, Life = 64, CombatMode = PlayerCombatMode.Stand } },
    };
}
