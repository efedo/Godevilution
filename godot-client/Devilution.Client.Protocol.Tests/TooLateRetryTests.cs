using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class TooLateRetryTests
{
    [Theory]
    [InlineData(CommandStatus.Rejected)] [InlineData(CommandStatus.Duplicate)]
    public async Task LedgerBackedRefusalGetsAtMostTwoFreshFrozenAttempts(CommandStatus refusal)
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        var original = new Command { CloseStoreRequested = new() };
        var root = fixture.Client.QueueRealtime(original, fixture.World.Tick);
        var first = await fixture.NextAsync(); var frozen = first.Clone();
        var current = first;
        for (var retry = 0; retry < AuthoritativeClient.MaximumTooLateRetries; retry++) {
            var rejection = await fixture.ResultAsync(current, status: refusal);
            var next = await fixture.NextAsync();
            Assert.True(next.ClientSequence > current.ClientSequence);
            Assert.True(next.RequestedTick > rejection.AppliedTick);
            Assert.True(next.RequestedTick - rejection.AppliedTick > 10); // Admission feedback raised the 50 ms / 10-tick lead.
            Assert.Equal(first.IntentCase, next.IntentCase);
            Assert.Null(fixture.Model.LastCommandResult);
            current = next;
        }
        await fixture.ResultAsync(current, status: refusal);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandResult is not null);
        Assert.Equal(CommandRejectReason.TooLate, fixture.Model.LastCommandResult!.RejectReason);
        Assert.Equal(root, fixture.Model.LastCommandIntentSequence);
        Assert.False(fixture.Model.LastCommandWasApplied);
        Assert.Equal(2, fixture.Messages.Count(message => message.CommandRetry.HasValue));
        Assert.Equal(frozen, first); // A refused attempt was never rewritten into its replacement.
        Assert.Equal(root, original.ClientSequence);
    }

    [Fact]
    public async Task AcceptedRetryCorrelatesToTheIntentAndDuplicateReceiptsCannotRepeatIt()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        var root = fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        var first = await fixture.NextAsync(); var rejection = await fixture.ResultAsync(first);
        var retry = await fixture.NextAsync();
        await fixture.SendAsync(new() { CommandAck = new() { Results = { rejection.Clone() } } });
        await fixture.UntilAsync(() => fixture.Messages.Count(message => message.Acknowledgement is not null) >= 2, poll: false);
        Assert.Null(fixture.Model.LastCommandResult);
        var accepted = await fixture.ResultAsync(retry, CommandRejectReason.Unspecified, CommandStatus.Accepted);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandWasApplied
            && fixture.Model.Snapshot!.Tick >= fixture.Model.LastCommandResult!.AppliedTick);
        Assert.Equal(retry.ClientSequence, fixture.Model.LastCommandResult!.ClientSequence);
        Assert.True(AuthoritativeCommandOutcome.IsForIntent(fixture.Model.LastCommandResult, fixture.Model.LastCommandIntentSequence, root));
        var boundary = new AcceptanceCommandBoundary.Observation(fixture.Model.Snapshot, fixture.Model.LastCommandResult,
            retry.ClientSequence, 0, fixture.Model.LastCommandIntentSequence);
        Assert.True(AcceptanceCommandBoundary.CommandSettled(boundary, root, "Retried interface intent"));
        var duplicate = accepted.Clone(); duplicate.Status = CommandStatus.Duplicate;
        await fixture.SendAsync(new() { CommandAck = new() { Results = { duplicate } } });
        await fixture.UntilAsync(() => fixture.Model.LastCommandResult?.Status == CommandStatus.Duplicate, poll: false);
        Assert.Equal(root, fixture.Model.LastCommandIntentSequence);
        Assert.Equal(0, fixture.Client.PendingCommandCount);
        Assert.Single(fixture.Messages.Where(message => message.CommandRetry.HasValue));
    }

    [Theory]
    [InlineData(CommandStatus.Accepted, CommandRejectReason.Unspecified, 1UL)]
    [InlineData(CommandStatus.Duplicate, CommandRejectReason.Unspecified, 1UL)]
    [InlineData(CommandStatus.Rescheduled, CommandRejectReason.Unspecified, 1UL)]
    [InlineData(CommandStatus.Rejected, CommandRejectReason.NotAllowed, 1UL)]
    [InlineData(CommandStatus.Rejected, CommandRejectReason.InvalidTarget, 1UL)]
    [InlineData(CommandStatus.Rejected, CommandRejectReason.TooLate, 0UL)]
    [InlineData(CommandStatus.Duplicate, CommandRejectReason.TooLate, 0UL)]
    public async Task AppliedUncertainRetiredAndOtherRejectedReceiptsNeverCreateANewCommand(CommandStatus status, CommandRejectReason reason, ulong ledger)
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        var command = await fixture.NextAsync(); await fixture.ResultAsync(command, reason, status, receipt: ledger);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandResult is not null);
        Assert.Empty(fixture.Messages.Where(message => message.CommandRetry.HasValue));
        Assert.Equal(command.ClientSequence, fixture.Model.LastCommandResult!.ClientSequence);
    }

    [Fact]
    public async Task RetryWaitsForTheRefusalSnapshotAndFinalizesIfTheActorChanged()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        var command = await fixture.NextAsync(); await fixture.ResultAsync(command, snapshot: false);
        await fixture.UntilAsync(() => fixture.Messages.Any(message => message.Acknowledgement is not null), poll: false);
        Assert.Equal(1, fixture.Client.PendingCommandCount); Assert.Null(fixture.Model.LastCommandResult);
        fixture.World.Players[0].Life = 0; await fixture.SnapshotAsync();
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandResult is not null);
        Assert.Equal(CommandRejectReason.TooLate, fixture.Model.LastCommandResult!.RejectReason);
        Assert.False(fixture.Model.LastCommandWasApplied);
        Assert.Empty(fixture.Messages.Where(message => message.CommandRetry.HasValue));
    }

    [Fact]
    public async Task MissingSnapshotCannotLeaveARefusedIntentPendingForever()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        var command = await fixture.NextAsync(); await fixture.ResultAsync(command, snapshot: false);
        await fixture.UntilAsync(() => fixture.Messages.Any(message => message.Acknowledgement is not null), poll: false);
        fixture.Time.AdvanceMilliseconds(2001);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandResult is not null);
        Assert.Equal(CommandRejectReason.TooLate, fixture.Model.LastCommandResult!.RejectReason);
    }

    [Fact]
    public async Task NewerPlayerInputSupersedesAnUnsentRetry()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        fixture.Client.QueueRealtime(new() { OpenStashRequested = new() }, fixture.World.Tick);
        var first = await fixture.NextAsync(); await fixture.ResultAsync(first);
        await fixture.UntilAsync(() => fixture.Messages.Any(message => message.Acknowledgement is not null), poll: false);
        var newer = fixture.Client.QueueRealtime(new() { CloseStashRequested = new() }, fixture.World.Tick);
        var command = await fixture.NextAsync();
        Assert.Equal(newer, command.ClientSequence); Assert.NotNull(command.CloseStashRequested);
        Assert.Empty(fixture.Messages.Where(message => message.CommandRetry.HasValue));
    }

    [Fact]
    public async Task LostRetryAckResendsTheSameFrozenAttemptRatherThanAllocatingAThirdIdentity()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        var first = await fixture.NextAsync(); await fixture.ResultAsync(first);
        var retry = await fixture.NextAsync();
        fixture.Time.AdvanceMilliseconds(2000);
        var retransmitted = await fixture.NextAsync();
        Assert.Equal(retry, retransmitted);
        await fixture.ResultAsync(retransmitted, CommandRejectReason.Unspecified, CommandStatus.Duplicate);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandWasApplied);
        Assert.Single(fixture.Messages.Where(message => message.CommandRetry.HasValue));
    }

    [Fact]
    public async Task DuplicateRefusalCannotExtendTheSnapshotDeadline()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        var command = await fixture.NextAsync(); var refused = await fixture.ResultAsync(command, snapshot: false);
        await fixture.UntilAsync(() => fixture.Messages.Any(message => message.Acknowledgement is not null), poll: false);
        fixture.Time.AdvanceMilliseconds(1500);
        refused.Status = CommandStatus.Duplicate;
        await fixture.SendAsync(new() { CommandAck = new() { Results = { refused } } });
        await fixture.UntilAsync(() => fixture.Messages.Count(message => message.Acknowledgement is not null) == 2, poll: false);
        fixture.Time.AdvanceMilliseconds(501);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandResult is not null);
        Assert.Equal(CommandRejectReason.TooLate, fixture.Model.LastCommandResult!.RejectReason);
        Assert.Empty(fixture.Messages.Where(message => message.CommandRetry.HasValue));
    }

    [Fact]
    public async Task SequenceExhaustionFinalizesTheActualRefusalWithoutOverflowOrWrapping()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync(lastClientSequence: ulong.MaxValue - 1);
        var root = fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        Assert.Equal(ulong.MaxValue, root);
        var command = await fixture.NextAsync(); await fixture.ResultAsync(command);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandResult is not null);
        Assert.Equal(root, fixture.Model.LastCommandResult!.ClientSequence);
        Assert.Empty(fixture.Messages.Where(message => message.CommandRetry.HasValue));
    }

    [Fact]
    public async Task AReceiptWithoutAModelAdmittedSnapshotKeepsTheLegacyTerminalOutcome()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync(admitToClient: false);
        fixture.Client.QueueRealtime(new() { CloseStoreRequested = new() }, fixture.World.Tick);
        var command = await fixture.NextAsync(); await fixture.ResultAsync(command);
        await fixture.UntilAsync(() => fixture.Client.PendingCommandCount == 0 && fixture.Model.LastCommandResult is not null);
        Assert.False(fixture.Model.LastCommandWasApplied);
        Assert.Empty(fixture.Messages.Where(message => message.CommandRetry.HasValue));
    }
}
