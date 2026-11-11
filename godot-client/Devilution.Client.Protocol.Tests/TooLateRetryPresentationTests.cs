using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class TooLateRetryPresentationTests
{
    [Fact]
    public async Task PredictionIsRetainedThroughRefusalAndReboundToTheFreshWireSequence()
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        var sequence = fixture.Client.QueueRealtime(new() { MoveRequested = new() { TargetCell = true, TargetX = 2, TargetY = 0 } }, fixture.World.Tick);
        fixture.Model.TrackPredictedMoveTo(sequence, 2, 0);
        var first = await fixture.NextAsync();
        var predicted = fixture.Model.LocalMovementTarget;
        Assert.NotNull(predicted);
        await fixture.ResultAsync(first);
        var retry = await fixture.NextAsync();
        Assert.Equal(predicted, fixture.Model.LocalMovementTarget);
        Assert.Null(fixture.Model.LastCommandResult);
        fixture.World.Players[0].PositionX = 1;
        await fixture.ResultAsync(retry, CommandRejectReason.Unspecified, CommandStatus.Accepted);
        await fixture.UntilAsync(() => fixture.Model.LastCommandWasApplied && fixture.Model.Snapshot!.Players[0].PositionX == 1);
        Assert.Equal(sequence, fixture.Model.LastCommandIntentSequence);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TravelWaitsForTheReplacementReceiptInsteadOfLosingItsPendingStep(bool interaction)
    {
        var snapshot = TooLateRetryPolicyTests.World();
        snapshot.LevelGeometry[0].WarpTriggers.Add(new WarpTriggerSnapshot { WarpId = 1, Available = true, TriggerX = 3, TriggerY = 3 });
        var route = new AuthoritativeRouteTravel();
        var approach = new AuthoritativeInteractionTravel();
        if (interaction) {
            approach.Start(snapshot, 3, 3, new() { OperateObjectRequested = new() { ObjectEntityId = 2 } }, true);
            approach.Submitted(1, snapshot.Players[0]); approach.ObserveRetry(new(1, 1, 2));
        } else {
            Assert.True(route.Start(snapshot, 1, true)); route.Submitted(1, snapshot.Players[0]); route.ObserveRetry(new(1, 1, 2));
        }
        snapshot.Players[0].PositionX = 1;
        var receipt = new CommandResult { ClientSequence = 2, AppliedTick = snapshot.Tick, Status = CommandStatus.Accepted };
        Assert.NotNull(interaction ? approach.Next(snapshot, receipt, true, 0, true, false) : route.Next(snapshot, receipt, true, 0));
    }

    [Fact]
    public async Task SceneGuardMatchesTheOriginalInputWithoutBorrowingAnotherReceiptsCorrelation()
    {
        await using var scene = new Main();
        var receipt = new CommandResult { ClientSequence = 2, ServerReceiptSequence = 2, Status = CommandStatus.Accepted };
        scene.AdmissionModel.Apply(new(Acknowledgement: new() { Results = { receipt } }) {
            AcknowledgedCommands = [new(2, 100, Command.IntentOneofCase.CloseStoreRequested) { IntentSequence = 1 }],
        });
        Assert.True(scene.ObservedReceiptMatchesIntent(receipt.Clone(), 1));
        Assert.True(scene.ObservedReceiptMatchesIntent(receipt.Clone(), 2));
        var another = receipt.Clone(); another.ClientSequence = 3;
        Assert.False(scene.ObservedReceiptMatchesIntent(another, 1));
        Assert.False(scene.ObservedReceiptMatchesIntent(receipt, 3));
    }

    [Fact]
    public void FinalRefusalIsNotHiddenAndCannotSatisfyAnIntentSuccessCheck()
    {
        var model = new AuthoritativeClientModel();
        var receipt = new CommandResult { ClientSequence = 3, ServerReceiptSequence = 3, Status = CommandStatus.Rejected,
            RejectReason = CommandRejectReason.TooLate };
        model.Apply(new(Acknowledgement: new() { Results = { receipt } }) {
            AcknowledgedCommands = [new(3, 100, Command.IntentOneofCase.CloseStoreRequested) { IntentSequence = 1 }],
        });
        Assert.Equal(receipt, model.LastCommandResult);
        Assert.True(AuthoritativeCommandOutcome.IsForIntent(model.LastCommandResult, model.LastCommandIntentSequence, 1));
        Assert.False(model.LastCommandWasApplied);
        Assert.False(AuthoritativeCommandOutcome.IsAwaitingRetry(receipt, null));
        receipt.ServerReceiptSequence = 0;
        Assert.False(AuthoritativeCommandOutcome.IsAwaitingRetry(receipt,
            [new(3, 100, Command.IntentOneofCase.CloseStoreRequested) { IntentSequence = 1, RetryPending = true }]));
    }
}
