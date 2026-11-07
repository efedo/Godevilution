using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class TownPortalReceiptAdmissionTests : LiveAuthoritativeTestBase
{
    [Theory]
    [InlineData(0, 1UL)] [InlineData(0, 2UL)]
    [InlineData(1, 1UL)] [InlineData(1, 2UL)]
    [InlineData(2, 1UL)] [InlineData(2, 2UL)]
    [InlineData(3, 1UL)] [InlineData(3, 2UL)]
    public void AppliedInterfaceCommandsAllowTheirFinalRescheduledReceipt(int intent, ulong lateness)
        => AssertTownPortalAppliedReceipt(Receipt(100 + lateness), InterfaceCommand(intent));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void GameplayCommandsStillRequireAccepted(int intent)
    {
        var command = intent switch {
            0 => new Command { MoveRequested = new() },
            1 => new Command { CastRequested = new() },
            _ => new Command { PurchaseRequested = new() },
        };
        command.ClientSequence = 1; command.RequestedTick = 100;
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertTownPortalAppliedReceipt(Receipt(101), command));
    }

    [Theory]
    [InlineData(99UL)] [InlineData(100UL)] [InlineData(103UL)]
    public void RescheduledInterfaceReceiptsMustStayInsideTheHostsTwoTickTolerance(ulong tick)
        => Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertTownPortalAppliedReceipt(Receipt(tick), InterfaceCommand(0)));

    [Fact]
    public void ARejectionOrUnsuccessfulReceiptStillFails()
    {
        var result = Receipt(101);
        result.RejectReason = CommandRejectReason.NotAllowed;
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertTownPortalAppliedReceipt(result, InterfaceCommand(0)));
        result.Status = CommandStatus.Rejected;
        result.RejectReason = CommandRejectReason.TooLate;
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertTownPortalAppliedReceipt(result, InterfaceCommand(0)));
        result.Status = CommandStatus.Duplicate;
        result.RejectReason = CommandRejectReason.Unspecified;
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertTownPortalAppliedReceipt(result, InterfaceCommand(0)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RescheduledUiSuccessRequiresTheMatchingSequenceAndReceiptIdentity(int intent)
    {
        var command = InterfaceCommand(intent);
        var result = Receipt(101);
        bool Applied() => AuthoritativeCommandAckValidator.IsSuccessfulRescheduledInterfaceCommand(result,
            command.IntentCase, command.ClientSequence, command.RequestedTick);
        Assert.True(Applied());
        result.ClientSequence = 2;
        Assert.False(Applied());
        result.ClientSequence = 1;
        result.ServerReceiptSequence = 0;
        Assert.False(Applied());
        result.ServerReceiptSequence = 1;
        command.ClientSequence = 0;
        Assert.False(Applied());
    }

    [Fact]
    public void ARescheduledReceiptWithoutItsSentCommandCannotBorrowEarlierUiEvidence()
    {
        var model = new AuthoritativeClientModel();
        var receipt = Receipt(101);
        model.Apply(new(Acknowledgement: new() { Results = { receipt } }) {
            AcknowledgedCommands = [new(1, 100, Command.IntentOneofCase.OpenStoreRequested)],
        });
        Assert.True(model.LastCommandWasApplied);
        receipt.ClientSequence = 2;
        model.Apply(new(Acknowledgement: new() { Results = { receipt } }));
        Assert.False(model.LastCommandWasApplied);
        model.Apply(new(Acknowledgement: new() { Results = { receipt } }) {
            AcknowledgedCommands = [new(1, 100, Command.IntentOneofCase.OpenStoreRequested)],
        });
        Assert.False(model.LastCommandWasApplied);
        // An older receipt cannot overwrite the decision for the latest command.
        receipt.ClientSequence = 1;
        model.Apply(new(Acknowledgement: new() { Results = { receipt } }) {
            AcknowledgedCommands = [new(1, 100, Command.IntentOneofCase.OpenStoreRequested)],
        });
        Assert.Equal(2UL, model.LastCommandResult!.ClientSequence);
        Assert.False(model.LastCommandWasApplied);
    }

    private static Command InterfaceCommand(int intent)
    {
        var command = intent switch {
            0 => new Command { OpenStoreRequested = new() },
            1 => new Command { CloseStoreRequested = new() },
            2 => new Command { OpenStashRequested = new() },
            _ => new Command { CloseStashRequested = new() },
        };
        command.ClientSequence = 1; command.RequestedTick = 100;
        return command;
    }

    private static CommandResult Receipt(ulong tick) => new() {
        ClientSequence = 1, ServerReceiptSequence = 1, Status = CommandStatus.Rescheduled, AppliedTick = tick,
    };
}
