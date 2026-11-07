using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptanceCommandBoundaryTests
{
    [Fact]
    public void DrainedTransportAndAnEarlierMovementReceiptDoNotCompleteTheHealerApproach()
    {
        // Observed shrine journey: approach started at 273, movement 274 arrived,
        // and the transport retired greeting 275 before the model admitted it.
        var receipt = new CommandResult {ClientSequence = 274, AppliedTick = 13170, Status = CommandStatus.Accepted};
        Assert.True(receipt.ClientSequence > 273);
        Assert.True(AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt));
        Assert.False(AcceptanceCommandBoundary.HasLatestAppliedReceipt(13631, receipt, 275));
    }

    [Fact]
    public void MatchingReceiptStillWaitsForItsPublishedSnapshot()
    {
        var receipt = new CommandResult {ClientSequence = 275, AppliedTick = 13632, Status = CommandStatus.Accepted};
        Assert.False(AcceptanceCommandBoundary.HasLatestAppliedReceipt(13631, receipt, 275));
        Assert.True(AcceptanceCommandBoundary.HasLatestAppliedReceipt(13632, receipt, 275));
        Assert.True(AcceptanceCommandBoundary.HasLatestAppliedReceipt(13640, receipt, 275));
    }

    [Theory]
    [InlineData(0UL)] [InlineData(274UL)] [InlineData(276UL)]
    public void AReceiptForAnotherOrUnqueuedCommandCannotCompleteArrival(ulong queued)
        => Assert.False(AcceptanceCommandBoundary.HasLatestAppliedReceipt(13632,
            new CommandResult {ClientSequence = 275, AppliedTick = 13632, Status = CommandStatus.Accepted}, queued));

    [Fact]
    public void MissingReceiptDoesNotCompleteArrival()
        => Assert.False(AcceptanceCommandBoundary.HasLatestAppliedReceipt(13632, null, 275));

    [Theory]
    [InlineData(CommandStatus.Accepted)] [InlineData(CommandStatus.Duplicate)]
    [InlineData(CommandStatus.Rejected)] [InlineData(CommandStatus.Rescheduled)]
    public void BoundaryDoesNotReclassifyTheCallersReceiptOutcome(CommandStatus status)
        => Assert.True(AcceptanceCommandBoundary.HasLatestAppliedReceipt(13632,
            new CommandResult {ClientSequence = 275, AppliedTick = 13632, Status = status}, 275));
}
