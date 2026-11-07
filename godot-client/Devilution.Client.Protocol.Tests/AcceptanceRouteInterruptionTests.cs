using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptanceRouteInterruptionTests
{
    [Theory]
    [InlineData(CommandStatus.Accepted)]
    [InlineData(CommandStatus.Duplicate)]
    public async Task LowHealthRouteReachingTownDuringCancellationCompletes(CommandStatus status)
    {
        var state = ShrineJourneyBoundary();
        var cancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var boundaryWaited = false;
        var operation = AcceptanceCommandBoundary.ArrivedAfterTravelCancellationAsync(
            () => cancellation.Task, async () => {
                boundaryWaited = true;
                await AcceptanceCommandBoundary.WaitAsync(() => state,
                    () => throw new InvalidOperationException("The final arrival is already settled."),
                    _ => false, "Interrupted generated-route traversal");
            }, () => state, 0);
        Assert.False(operation.IsCompleted);
        Assert.False(boundaryWaited);

        // Observed failure: the last L5 sample was (47,62), adjacent to (48,63).
        // The native follow-path command reaches Town while cancellation awaits UI frames.
        ArriveInTown(state);
        state.Receipt!.Status = status;
        cancellation.SetResult();

        Assert.True(await operation);
        Assert.True(boundaryWaited);
        Assert.Equal(7677, state.Player!.Life);
    }

    [Fact]
    public async Task ArrivalWaitsForReceiptSnapshotAndNativeWalkSettlement()
    {
        var state = ShrineJourneyBoundary() with { PendingCount = 1 };
        state.Receipt!.ClientSequence = 163;
        var frames = 0;
        var arrived = await AcceptanceCommandBoundary.ArrivedAfterTravelCancellationAsync(
            () => Task.CompletedTask, () => AcceptanceCommandBoundary.WaitAsync(() => state, () => {
                switch (++frames) {
                case 1: state = state with { PendingCount = 0 }; break;
                case 2:
                    state.Receipt.ClientSequence = 164;
                    state.Receipt.AppliedTick = 8524;
                    break;
                case 3:
                    state.Snapshot!.Tick = 8524;
                    state.Player!.LevelId = 0;
                    break;
                case 4: ArriveInTown(state); break;
                }
                return Task.CompletedTask;
            }, _ => frames >= 5, "Interrupted generated-route traversal"), () => state, 0);

        Assert.True(arrived);
        Assert.Equal(4, frames);
    }

    [Theory]
    [InlineData(5U)]
    [InlineData(4U)]
    public async Task CancellationAwayFromTheDestinationKeepsTheExistingRetry(uint level)
    {
        var state = ShrineJourneyBoundary();
        state.Player!.LevelId = level;
        state.Player.NativeWalkPathPending = false;
        state.Player.CombatMode = PlayerCombatMode.Stand;
        var boundaryWaited = false;
        Assert.False(await AcceptanceCommandBoundary.ArrivedAfterTravelCancellationAsync(
            () => Task.CompletedTask, async () => {
                boundaryWaited = true;
                await AcceptanceCommandBoundary.WaitAsync(() => state,
                    () => throw new InvalidOperationException("Already settled."), _ => false,
                    "Interrupted generated-route traversal");
            }, () => state, 0));
        Assert.True(boundaryWaited);
    }

    [Fact]
    public async Task CancellationDoesNotAcceptADeadArrival()
    {
        var state = ShrineJourneyBoundary();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AcceptanceCommandBoundary.ArrivedAfterTravelCancellationAsync(() => {
                ArriveInTown(state);
                state.Player!.Life = 0;
                return Task.CompletedTask;
            }, () => AcceptanceCommandBoundary.WaitAsync(() => state,
                () => throw new InvalidOperationException("Death must fail immediately."), _ => false,
                "Interrupted generated-route traversal"), () => state, 0));
        Assert.Contains("life=0", failure.Message);
        Assert.Contains("queued=164", failure.Message);
    }

    [Fact]
    public async Task UnadmittedArrivalTimesOutWithTheBoundaryState()
    {
        var state = ShrineJourneyBoundary();
        ArriveInTown(state);
        state.Receipt!.ClientSequence = 163;
        var frames = 0;
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AcceptanceCommandBoundary.ArrivedAfterTravelCancellationAsync(() => Task.CompletedTask,
                () => AcceptanceCommandBoundary.WaitAsync(() => state,
                    () => { frames++; return Task.CompletedTask; }, _ => frames >= 1,
                    "Interrupted generated-route traversal"), () => state, 0));
        Assert.Equal(1, frames);
        Assert.Contains("timed out", failure.Message);
        Assert.Contains("queued=164", failure.Message);
        Assert.Contains("receipt=163/8481/Accepted", failure.Message);
        Assert.Contains("snapshotTick=8656", failure.Message);
        Assert.Contains("pending=0", failure.Message);
    }

    private static AcceptanceCommandBoundary.Observation ShrineJourneyBoundary()
    {
        var snapshot = new Snapshot { Tick = 8523 };
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 1, LevelId = 5,
            PositionX = 47, PositionY = 62, Life = 7677, LifeMaximum = 11648,
            CombatMode = PlayerCombatMode.WalkSouthwards, NativeWalkPathPending = true });
        return new(snapshot, new CommandResult { ClientSequence = 164, AppliedTick = 8481,
            Status = CommandStatus.Accepted }, 164, 0);
    }

    private static void ArriveInTown(AcceptanceCommandBoundary.Observation state)
    {
        state.Snapshot!.Tick = 8656;
        state.Player!.LevelId = 0;
        state.Player.PositionX = 49;
        state.Player.PositionY = 22;
        state.Player.CombatMode = PlayerCombatMode.Stand;
        state.Player.NativeWalkPathPending = false;
    }
}
