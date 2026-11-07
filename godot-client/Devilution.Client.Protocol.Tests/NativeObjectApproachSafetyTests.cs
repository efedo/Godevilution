using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectApproachSafetyTests
{
    [Fact]
    public async Task ClearApproachAndInRangeOperationEachCheckHealingAtASettledBoundary()
    {
        var state = DoorBoundary();
        var checks = 0;
        var frames = 0;
        async Task Wait() => await AcceptanceCommandBoundary.WaitAsync(() => state, () => {
            frames++;
            state.Snapshot!.Tick++;
            state = state with { PendingCount = 0,
                Receipt = new CommandResult { ClientSequence = state.LastQueuedSequence,
                    AppliedTick = state.Snapshot.Tick, Status = CommandStatus.Accepted } };
            return Task.CompletedTask;
        }, _ => frames >= 4, "Native object preparation");
        Task HealThroughControls()
        {
            Assert.True(state.CommandsAdmitted && state.PlayerSettled);
            checks++;
            // The existing control queues a potion; planning must wait for its own receipt.
            state = state with { LastQueuedSequence = state.LastQueuedSequence + 1, PendingCount = 1 };
            return Task.CompletedTask;
        }

        // Two clear-path steps followed by the operation already in range: no obstruction callback.
        for (var iteration = 0; iteration < 3; iteration++) {
            await AcceptanceCommandBoundary.PrepareObjectAsync(Wait, HealThroughControls);
            Assert.True(state.CommandsAdmitted && state.PlayerSettled);
            Assert.Equal((ulong)(236 + iteration), state.Receipt!.ClientSequence);
        }
        Assert.Equal(3, checks);
        Assert.Equal(3, frames);
    }

    [Fact]
    public async Task HealingWaitsForTheOriginalCommandAndFreshSnapshotBeforePlanning()
    {
        var state = DoorBoundary() with { PendingCount = 1 };
        var healed = false;
        var frames = 0;
        await AcceptanceCommandBoundary.PrepareObjectAsync(
            () => AcceptanceCommandBoundary.WaitAsync(() => state, () => {
                frames++;
                state = state with { PendingCount = 0 };
                state.Snapshot!.Tick++;
                state.Player!.PositionX++;
                return Task.CompletedTask;
            }, _ => frames >= 2, "Native object preparation"), () => {
                Assert.Equal(1, frames);
                Assert.Equal(62, state.Player!.PositionX);
                healed = true;
                return Task.CompletedTask;
            });
        Assert.True(healed);
        Assert.Equal(62, state.Player!.PositionX);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DeathAfterRejectedMovementFailsImmediatelyInsteadOfWaitingForStand(int pending)
    {
        var state = DoorBoundary() with { LastQueuedSequence = 236, PendingCount = pending };
        state.Snapshot!.Tick = 13587;
        state.Player!.Life = 0;
        state.Player.CombatMode = PlayerCombatMode.Death;
        state.Receipt!.ClientSequence = 236;
        state.Receipt.AppliedTick = 12999;
        state.Receipt.Status = CommandStatus.Rejected;
        state.Receipt.RejectReason = CommandRejectReason.NotAllowed;
        var error = Assert.Throws<InvalidOperationException>(() =>
            AcceptanceCommandBoundary.CommandSettled(state, 236, "The cardinal native chest approach command"));
        Assert.Contains("character died", error.Message);
        Assert.Contains("queued=236", error.Message);
        Assert.Contains("receipt=236/12999/Rejected/NotAllowed", error.Message);
        Assert.Contains("snapshotTick=13587", error.Message);
    }

    [Theory]
    [InlineData(CommandStatus.Accepted)]
    [InlineData(CommandStatus.Duplicate)]
    [InlineData(CommandStatus.Rejected)]
    [InlineData(CommandStatus.Rescheduled)]
    public void SettlementStillLeavesReceiptOutcomeValidationToTheCaller(CommandStatus status)
    {
        var state = DoorBoundary();
        state.Receipt!.Status = status;
        Assert.True(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
    }

    [Fact]
    public void SettlementRetainsEveryAdmissionAndAnimationRequirement()
    {
        var state = DoorBoundary();
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 236, "Approach"));
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state with { PendingCount = 1 }, 235, "Approach"));
        state.Snapshot!.Tick = 12924;
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
        state.Snapshot.Tick = 12925;
        state.Player!.NativeWalkPathPending = true;
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
        state.Player.NativeWalkPathPending = false;
        state.Player.CombatMode = PlayerCombatMode.GotHit;
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
        state.Player.CombatMode = PlayerCombatMode.Stand;
        Assert.True(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
    }

    private static AcceptanceCommandBoundary.Observation DoorBoundary()
    {
        var snapshot = new Snapshot { Tick = 12925 };
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 1, LevelId = 7, PositionX = 61, PositionY = 74,
            Life = 6000, LifeMaximum = 11776, CombatMode = PlayerCombatMode.Stand });
        return new(snapshot, new CommandResult { ClientSequence = 235, AppliedTick = 12925,
            Status = CommandStatus.Accepted }, 235, 0);
    }
}
