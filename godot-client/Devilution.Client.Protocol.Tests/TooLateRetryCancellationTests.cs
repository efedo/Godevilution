using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class TooLateRetryCancellationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task CancelAndNewTravelSelectionsDropTheRefusedDestinationBeforeAnotherCommandIsQueued(int selection)
    {
        await using var peer = await TooLateRetryPeer.StartAsync();
        await using var scene = new Main();
        var root = peer.Client.QueueRealtime(new() { MoveRequested = new() { TargetCell = true, TargetX = 2, TargetY = 0 } }, peer.World.Tick);
        var old = await peer.NextAsync();
        await peer.ResultAsync(old, snapshot: false);
        await peer.UntilAsync(() => peer.Messages.Any(message => message.Acknowledgement is not null), poll: false);
        Assert.Equal(1, peer.Client.PendingCommandCount);
        var snapshot = peer.World.Clone();
        snapshot.LevelGeometry[0].WarpTriggers.Add(new WarpTriggerSnapshot { WarpId = 1, Available = true, TriggerX = 3, TriggerY = 3 });
        scene.AttachTravelForTest(peer.Client, snapshot);
        try {
            if (selection == 0) scene.CancelTravelForTest();
            else if (selection == 1) scene.SelectTravelForTest(1);
            else scene.SelectInteractionForTest();
            Assert.Equal(0, peer.Client.PendingCommandCount); // No Poll, ACK or queued replacement is needed to free it.
            await peer.SnapshotAsync();
            await peer.UntilAsync(() => peer.Model.LastCommandResult is not null, poll: false);
            Assert.Equal(root, peer.Model.LastCommandResult!.ClientSequence);
            Assert.Equal(CommandRejectReason.TooLate, peer.Model.LastCommandResult.RejectReason);
            var next = scene.NextTravelForTest();
            if (selection == 0) Assert.Null(next);
            else {
                Assert.NotNull(next);
                Assert.Equal(selection == 1 ? (3, 3) : (0, 3), (next!.MoveRequested.TargetX, next.MoveRequested.TargetY));
                var nextSequence = peer.Client.QueueRealtime(next, peer.World.Tick);
                var dispatched = await peer.NextAsync();
                Assert.Equal(nextSequence, dispatched.ClientSequence);
                Assert.NotEqual((old.MoveRequested.TargetX, old.MoveRequested.TargetY),
                    (dispatched.MoveRequested.TargetX, dispatched.MoveRequested.TargetY));
            }
            Assert.Empty(peer.Messages.Where(message => message.CommandRetry.HasValue));
        } finally { scene.DetachTravelForTest(); }
    }

    [Fact]
    public async Task CancellationBeforeTheReceiptDisablesFreshRetriesWithoutDiscardingAnUnknownFrozenAttempt()
    {
        await using var peer = await TooLateRetryPeer.StartAsync();
        peer.Client.QueueRealtime(new() { MoveRequested = new() { DirectionX = 1 } }, peer.World.Tick);
        var command = await peer.NextAsync();
        peer.Client.CancelRetainedIntentRetries();
        Assert.Equal(1, peer.Client.PendingCommandCount);
        await peer.ResultAsync(command);
        await peer.UntilAsync(() => peer.Client.PendingCommandCount == 0 && peer.Model.LastCommandResult is not null);
        Assert.Equal(command.ClientSequence, peer.Model.LastCommandResult!.ClientSequence);
        Assert.Empty(peer.Messages.Where(message => message.CommandRetry.HasValue));
    }

    [Theory]
    [InlineData(2UL)] [InlineData(3UL)]
    public void NativeApproachAndHealingGuardsAllowOnlyAttemptsForTheExpectedIntent(ulong attempt)
    {
        AcceptanceCommandBoundary.RequireExpectedQueuedIntent(attempt, 1, 1, "native approach/heal");
        Assert.True(AcceptanceCommandBoundary.QueuedCommandMatchesIntent(attempt, 1, 1));
        Assert.Throws<InvalidOperationException>(() =>
            AcceptanceCommandBoundary.RequireExpectedQueuedIntent(attempt, attempt, 1, "native approach/heal"));
        Assert.False(AcceptanceCommandBoundary.QueuedCommandMatchesIntent(attempt, attempt, 1));
    }
}
