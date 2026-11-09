using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptanceCommandBoundaryTests
{
    [Theory]
    [InlineData(Command.IntentOneofCase.DropItemRequested)]
    [InlineData(Command.IntentOneofCase.DropHeldItemRequested)]
    [InlineData(Command.IntentOneofCase.MoveRequested)]
    [InlineData(Command.IntentOneofCase.DisarmTrapRequested)]
    public void AWorldClickWithAnotherIntentCannotProveAnObjectOpening(Command.IntentOneofCase intent)
    {
        var error = Assert.Throws<InvalidOperationException>(() => AcceptanceCommandBoundary.RequireObjectOperation(
            267, 268, intent, 0, 151199857, "Open the required native trapped sarcophagus"));
        Assert.Contains(intent.ToString(), error.Message);
        Assert.Contains("OperateObjectRequested", error.Message);
        Assert.Contains("151199857", error.Message);
        Assert.Contains("queuedSequence=268", error.Message);
    }

    [Theory]
    [InlineData(267UL, 151199857U)] [InlineData(268UL, 151199858U)] [InlineData(268UL, 0U)]
    public void AnOpeningRequiresANewOperationForTheExactObject(ulong sequence, uint target)
        => Assert.Throws<InvalidOperationException>(() => AcceptanceCommandBoundary.RequireObjectOperation(
            267, sequence, Command.IntentOneofCase.OperateObjectRequested, target, 151199857, "Open sarcophagus"));

    [Fact]
    public void TheActualQueuedOperationCanProveTheOpening()
        => AcceptanceCommandBoundary.RequireObjectOperation(267, 268,
            Command.IntentOneofCase.OperateObjectRequested, 151199857, 151199857, "Open sarcophagus");

    [Fact]
    public async Task KnockbackAfterPreparationCanApproachThenQueueTheExactOperation()
    {
        const uint target = 151199857;
        var boundary = ReadyNativeBoundary();
        var player = boundary.Player!;
        player.PositionX = 31;
        player.PositionY = 62;
        // Preparation saw (32,62); knockback committed before the click, leaving it out of range.
        var travel = new AuthoritativeInteractionTravel();
        travel.Start(boundary.Snapshot, 33, 62, new Command {
            OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = target },
        }, connected: true);
        var move = travel.Next(boundary.Snapshot, boundary.Receipt, true, 0, targetPresent: true, inRange: false)!;
        Assert.NotNull(move.MoveRequested);
        travel.Submitted(125, player);
        boundary = boundary with { LastQueuedSequence = 125, PendingCount = 1 };
        var queued = new AcceptanceCommandBoundary.QueuedCommandObservation(boundary, move.IntentCase, 0);
        var frames = 0;
        await AcceptanceCommandBoundary.WaitForObjectOperationAsync(() => queued, () => {
            boundary.Snapshot!.Tick++;
            switch (++frames) {
            case 1:
                player.PositionX = 32;
                player.CombatMode = PlayerCombatMode.WalkNorthwards;
                break;
            case 2:
                boundary = boundary with { Receipt = new CommandResult {
                    ClientSequence = 125, AppliedTick = boundary.Snapshot.Tick, Status = CommandStatus.Accepted,
                }, PendingCount = 0 };
                break;
            case 3: player.CombatMode = PlayerCombatMode.Stand; break;
            }
            var command = travel.Next(boundary.Snapshot, boundary.Receipt, true, boundary.PendingCount,
                targetPresent: true, AuthoritativeInteractionRange.IsAdjacent(player, 33, 62));
            if (command is not null) {
                boundary = boundary with { LastQueuedSequence = boundary.LastQueuedSequence + 1, PendingCount = 1 };
                queued = new(boundary, command.IntentCase, command.OperateObjectRequested?.ObjectEntityId ?? 0);
                if (command.MoveRequested is not null) travel.Submitted(boundary.LastQueuedSequence, player);
            } else queued = queued with { Boundary = boundary };
            return Task.CompletedTask;
        }, _ => frames >= 4, 124, target, "Open sarcophagus after knockback");

        Assert.Equal(3, frames);
        Assert.False(travel.Active);
        Assert.Equal(Command.IntentOneofCase.OperateObjectRequested, queued.Intent);
        Assert.Equal(target, queued.ObjectEntityId);
        Assert.Equal(126UL, queued.Boundary.LastQueuedSequence);
        // The movement receipt cannot prove opening; the caller still waits for operation 126's receipt.
        Assert.Equal(125UL, queued.Boundary.Receipt!.ClientSequence);
    }

    [Fact]
    public async Task AnAlreadyQueuedExactOperationNeedsNoExtraFrame()
    {
        var queued = new AcceptanceCommandBoundary.QueuedCommandObservation(ReadyNativeBoundary(),
            Command.IntentOneofCase.OperateObjectRequested, 151199857);
        await AcceptanceCommandBoundary.WaitForObjectOperationAsync(() => queued,
            () => throw new InvalidOperationException("An immediate operation must not add a frame."),
            _ => true, 123, 151199857, "Open sarcophagus");
    }

    [Fact]
    public async Task ThePreviousClicksCommandDoesNotStandInForThisClicksOperation()
    {
        var queued = new AcceptanceCommandBoundary.QueuedCommandObservation(ReadyNativeBoundary(),
            Command.IntentOneofCase.DropItemRequested, 0);
        var frames = 0;
        await AcceptanceCommandBoundary.WaitForObjectOperationAsync(() => queued, () => {
            frames++;
            queued = new(queued.Boundary with { LastQueuedSequence = 125 },
                Command.IntentOneofCase.OperateObjectRequested, 151199857);
            return Task.CompletedTask;
        }, _ => frames >= 2, 124, 151199857, "Open sarcophagus");
        Assert.Equal(1, frames);
    }

    [Theory]
    [InlineData(Command.IntentOneofCase.DropItemRequested)]
    [InlineData(Command.IntentOneofCase.DropHeldItemRequested)]
    [InlineData(Command.IntentOneofCase.DisarmTrapRequested)]
    [InlineData(Command.IntentOneofCase.UseConsumableRequested)]
    [InlineData(Command.IntentOneofCase.CastRequested)]
    [InlineData(Command.IntentOneofCase.None)]
    public async Task AnApproachCannotHideAnotherCommandInsteadOfOperating(Command.IntentOneofCase intent)
    {
        var queued = new AcceptanceCommandBoundary.QueuedCommandObservation(ReadyNativeBoundary(), Command.IntentOneofCase.MoveRequested, 0);
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitForObjectOperationAsync(
            () => queued, () => {
                frames++;
                queued = new(queued.Boundary with { LastQueuedSequence = 125 }, intent, 0);
                return Task.CompletedTask;
            }, _ => frames >= 2, 123, 151199857, "Open sarcophagus"));
        Assert.Equal(1, frames);
        Assert.Contains(intent.ToString(), error.Message);
        Assert.Contains("expected OperateObjectRequested for object 151199857", error.Message);
    }

    [Theory]
    [InlineData(0U)] [InlineData(151199858U)]
    public async Task AnApproachCannotCompleteByOperatingAnotherTarget(uint target)
    {
        var queued = new AcceptanceCommandBoundary.QueuedCommandObservation(ReadyNativeBoundary(), Command.IntentOneofCase.MoveRequested, 0);
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitForObjectOperationAsync(
            () => queued, () => {
                frames++;
                queued = new(queued.Boundary with { LastQueuedSequence = 125 }, Command.IntentOneofCase.OperateObjectRequested, target);
                return Task.CompletedTask;
            }, _ => frames >= 2, 123, 151199857, "Open sarcophagus"));
        Assert.Equal(1, frames);
        Assert.Contains($"for object {target}", error.Message);
        Assert.Contains("expected OperateObjectRequested for object 151199857", error.Message);
    }

    [Theory]
    [InlineData(123UL)] [InlineData(124UL)]
    public async Task AnApproachOrAnUnqueuedClickHasABoundedDeadlineWithItsLatestState(ulong sequence)
    {
        var boundary = ReadyNativeBoundary() with { LastQueuedSequence = sequence, PendingCount = 1 };
        boundary.Receipt!.ClientSequence = 123;
        var queued = new AcceptanceCommandBoundary.QueuedCommandObservation(boundary, Command.IntentOneofCase.MoveRequested, 0);
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitForObjectOperationAsync(
            () => queued, () => {
                frames++;
                boundary.Snapshot!.Tick++;
                return Task.CompletedTask;
            }, _ => frames >= 2, 123, 151199857, "Open sarcophagus"));
        Assert.Equal(2, frames);
        Assert.Contains("timed out waiting for OperateObjectRequested for object 151199857", error.Message);
        Assert.Contains("intent=MoveRequested", error.Message);
        Assert.Contains($"queued={sequence}", error.Message);
        Assert.Contains("receipt=123/6625", error.Message);
        Assert.Contains("snapshotTick=6771", error.Message);
        Assert.Contains("pending=1", error.Message);
    }

    [Fact]
    public async Task DeathDuringTheDeferredApproachFailsWithoutWaitingForItsDeadline()
    {
        var queued = new AcceptanceCommandBoundary.QueuedCommandObservation(ReadyNativeBoundary(), Command.IntentOneofCase.MoveRequested, 0);
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitForObjectOperationAsync(
            () => queued, () => {
                frames++;
                queued.Boundary.Player!.Life = 0;
                return Task.CompletedTask;
            }, _ => frames >= 2, 123, 151199857, "Open sarcophagus"));
        Assert.Equal(1, frames);
        Assert.Contains("character died", error.Message);
    }

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

    [Theory]
    [InlineData(PlayerCombatMode.Attack)] [InlineData(PlayerCombatMode.GotHit)]
    [InlineData(PlayerCombatMode.Block)] [InlineData(PlayerCombatMode.WalkNorthwards)]
    public async Task ObjectApproachWaitsForRecoveryAndPlansFromTheNewPosition(PlayerCombatMode mode)
    {
        var state = ReadyNativeBoundary();
        state.Player!.CombatMode = mode;
        state.Player.CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Attack };
        var frames = 0;
        await AcceptanceCommandBoundary.WaitAsync(() => state, () => {
            frames++;
            state.Snapshot!.Tick++;
            state.Player.CombatMode = PlayerCombatMode.Stand;
            state.Player.CurrentAction = null;
            state.Player.PositionX = 43;
            return Task.CompletedTask;
        }, _ => frames >= 2, "Native object approach");

        Assert.Equal(1, frames);
        Assert.Equal(43, state.Player.PositionX);
        Assert.Equal(6770UL, state.Snapshot!.Tick);
    }

    [Fact]
    public async Task ApproachWaitsThroughTransportAckModelAdmissionSnapshotAndAnimation()
    {
        var state = ReadyNativeBoundary() with { PendingCount = 1 };
        state.Snapshot!.Tick = 6624;
        state.Receipt!.ClientSequence = 123;
        state.Receipt.AppliedTick = 6557;
        state.Receipt.Status = CommandStatus.Rejected;
        state.Receipt.RejectReason = CommandRejectReason.NotAllowed;
        var frames = 0;
        await AcceptanceCommandBoundary.WaitAsync(() => state, () => {
            switch (++frames) {
            case 1: state = state with { PendingCount = 0 }; break;
            case 2:
                state = state with { Receipt = ReadyNativeBoundary().Receipt };
                break;
            case 3:
                state.Snapshot.Tick = 6625;
                state.Player!.CombatMode = PlayerCombatMode.Attack;
                state.Player.CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Attack };
                break;
            case 4:
                state.Player!.CombatMode = PlayerCombatMode.Stand;
                state.Player.CurrentAction = null;
                break;
            }
            return Task.CompletedTask;
        }, _ => frames >= 5, "Native object approach");

        Assert.Equal(4, frames);
    }

    [Fact]
    public async Task DrainedLedgerWithNoAdmittedReceiptStillWaits()
    {
        var state = ReadyNativeBoundary() with { Receipt = null };
        var frames = 0;
        await AcceptanceCommandBoundary.WaitAsync(() => state, () => {
            frames++;
            state = state with { Receipt = ReadyNativeBoundary().Receipt };
            return Task.CompletedTask;
        }, _ => frames >= 2, "Native object approach");
        Assert.Equal(1, frames);
    }

    [Fact]
    public async Task FreshHostWithoutQueuedCommandsAlreadyHasASettledBoundary()
    {
        var state = ReadyNativeBoundary() with { LastQueuedSequence = 0, Receipt = null };
        await AcceptanceCommandBoundary.WaitAsync(() => state,
            () => throw new InvalidOperationException("A cold settled host should not need another frame."),
            _ => true, "Native object approach");
    }

    [Theory]
    [InlineData(CommandStatus.Accepted)] [InlineData(CommandStatus.Duplicate)]
    [InlineData(CommandStatus.Rejected)] [InlineData(CommandStatus.Rescheduled)]
    public async Task TerminalReceiptOutcomesDoNotKeepTheBoundaryWaiting(CommandStatus status)
    {
        var state = ReadyNativeBoundary();
        state.Receipt!.Status = status;
        await AcceptanceCommandBoundary.WaitAsync(() => state,
            () => throw new InvalidOperationException("Receipt success belongs to the command's caller."),
            _ => true, "Native object approach");
    }

    [Fact]
    public async Task TimeoutReportsTheLatestQueuedReceiptSnapshotAndPendingState()
    {
        var state = ReadyNativeBoundary() with { PendingCount = 1 };
        state.Receipt!.ClientSequence = 123;
        state.Receipt.AppliedTick = 6557;
        state.Receipt.Status = CommandStatus.Rejected;
        state.Receipt.RejectReason = CommandRejectReason.NotAllowed;
        state.Player!.CombatMode = PlayerCombatMode.GotHit;
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitAsync(
            () => state, () => {
                frames++;
                state.Snapshot!.Tick++;
                return Task.CompletedTask;
            }, _ => frames >= 2, "Native barrel approach"));

        Assert.Equal(2, frames);
        Assert.Contains("Native barrel approach", error.Message);
        Assert.Contains("queued=124", error.Message);
        Assert.Contains("receipt=123/6557/Rejected/NotAllowed", error.Message);
        Assert.Contains("snapshotTick=6771", error.Message);
        Assert.Contains("pending=1", error.Message);
        Assert.Contains("mode=GotHit", error.Message);
    }

    [Fact]
    public async Task DeadPlayerFailsImmediatelyWithBoundaryState()
    {
        var state = ReadyNativeBoundary();
        state.Player!.Life = 0;
        state.Player.CombatMode = PlayerCombatMode.Death;
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitAsync(
            () => state, () => { frames++; return Task.CompletedTask; }, _ => frames > 0, "Native object approach"));
        Assert.Equal(0, frames);
        Assert.Contains("life=0", error.Message);
        Assert.Contains("queued=124", error.Message);
    }

    [Fact]
    public async Task MissingSnapshotWaitsUntilTheBudgetExpires()
    {
        var state = ReadyNativeBoundary() with { Snapshot = null };
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitAsync(
            () => state, () => { frames++; return Task.CompletedTask; }, _ => frames >= 1, "Native object approach"));
        Assert.Equal(1, frames);
        Assert.Contains("snapshotTick=", error.Message);
        Assert.Contains("pending=0", error.Message);
    }

    [Fact]
    public async Task InvalidPendingCountFailsImmediately()
    {
        var state = ReadyNativeBoundary() with { PendingCount = -1 };
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaitAsync(
            () => state, () => { frames++; return Task.CompletedTask; }, _ => frames > 0, "Native object approach"));
        Assert.Equal(0, frames);
        Assert.Contains("pending=-1", error.Message);
    }

    [Fact]
    public async Task QueuedNativeWalkMustFinishBeforeTheNextWalkIsPlanned()
    {
        var state = ReadyNativeBoundary();
        state.Player!.NativeWalkPathPending = true;
        var frames = 0;
        await AcceptanceCommandBoundary.WaitAsync(() => state, () => {
            frames++;
            state.Player.NativeWalkPathPending = false;
            return Task.CompletedTask;
        }, _ => frames >= 2, "Native object approach");
        Assert.Equal(1, frames);
    }

    [Fact]
    public async Task QuickUseWaitsForCommandAdmissionWhileAllowingAnActivePlayerAction()
    {
        var state = ReadyNativeBoundary() with { PendingCount = 1 };
        state.Player!.CombatMode = PlayerCombatMode.WalkNorthwards;
        state.Player.NativeWalkPathPending = true;
        state.Player.CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Move };
        var frames = 0;
        await AcceptanceCommandBoundary.WaitAsync(() => state, () => {
            frames++;
            state = state with { PendingCount = 0 };
            return Task.CompletedTask;
        }, _ => frames >= 2, "Native quick-use", settlePlayer: false);
        Assert.Equal(1, frames);
        Assert.NotNull(state.Player.CurrentAction);
        Assert.True(state.Player.NativeWalkPathPending);
    }

    private static AcceptanceCommandBoundary.Observation ReadyNativeBoundary()
    {
        var snapshot = new Snapshot { Tick = 6769 };
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 1, LevelId = 4, PositionX = 42, PositionY = 61,
            Life = 7741, LifeMaximum = 11776, CombatMode = PlayerCombatMode.Stand });
        return new(snapshot, new CommandResult { ClientSequence = 124, AppliedTick = 6625, Status = CommandStatus.Accepted }, 124, 0);
    }
}
