using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptanceRouteRecoveryTests
{
    [Fact]
    public async Task UnexpectedLivingFloorReturnsToTheCallersOriginRecoveryInsteadOfCompletingTheLeg()
    {
        var state = SarcophagusRetreatBoundary();
        var cancelled = false;
        var boundaryWaited = false;
        var completed = await AcceptanceRouteRecovery.TryAsync(
            () => throw new InvalidOperationException("Traversal left the generated level before the checkpoint."),
            () => state, async error => {
                Assert.Contains("left the generated level", error.Message);
                cancelled = true;
                await AcceptanceCommandBoundary.WaitAsync(() => state,
                    () => throw new InvalidOperationException("The observed wrong-floor arrival is already settled."),
                    _ => false, "Wrong-floor route recovery");
                boundaryWaited = true;
            });
        Assert.False(completed); // The existing next-attempt branch sees L4 and returns to its L3 origin.
        Assert.True(cancelled && boundaryWaited);
        Assert.Equal(4U, state.Player!.LevelId);
        Assert.Equal(262UL, state.Receipt!.ClientSequence);
    }

    [Fact]
    public async Task SuccessfulTravelDoesNotCancelOrRetry()
        => Assert.True(await AcceptanceRouteRecovery.TryAsync(() => Task.CompletedTask,
            () => SarcophagusRetreatBoundary(), _ => throw new InvalidOperationException("No recovery needed.")));

    [Fact]
    public async Task DeadWarriorDoesNotRecoverOrHideTheOriginalFailure()
    {
        var state = SarcophagusRetreatBoundary();
        state.Player!.Life = 0;
        var original = new InvalidOperationException("character died");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceRouteRecovery.TryAsync(
            () => throw original, () => state, _ => throw new InvalidOperationException("Must not recover death.")));
        Assert.Same(original, failure);
    }

    [Fact]
    public async Task MissingSnapshotCannotRecover()
    {
        var state = SarcophagusRetreatBoundary() with { Snapshot = null };
        var original = new InvalidOperationException("No admitted world");
        Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceRouteRecovery.TryAsync(
            () => throw original, () => state, _ => throw new InvalidOperationException("Must not recover without a player."))));
    }

    [Fact]
    public async Task FailedCancellationOrBoundaryWaitEscapesTheRetry()
    {
        var recoveryFailure = new InvalidOperationException("Wrong-floor route recovery timed out waiting for receipt 262");
        Assert.Same(recoveryFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceRouteRecovery.TryAsync(
            () => throw new InvalidOperationException("Wrong floor"), () => SarcophagusRetreatBoundary(),
            _ => throw recoveryFailure)));
    }

    [Fact]
    public async Task NonTraversalFailuresAreNotReclassified()
    {
        var original = new FormatException("Invalid evidence");
        Assert.Same(original, await Assert.ThrowsAsync<FormatException>(() => AcceptanceRouteRecovery.TryAsync(
            () => throw original, () => SarcophagusRetreatBoundary(),
            _ => throw new InvalidOperationException("Must not recover this failure."))));
    }

    private static AcceptanceCommandBoundary.Observation SarcophagusRetreatBoundary()
    {
        var snapshot = new Snapshot { Tick = 13480 };
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 1, LevelId = 4, PositionX = 47, PositionY = 68,
            Life = 1744, LifeMaximum = 11776, CombatMode = PlayerCombatMode.Stand });
        return new(snapshot, new CommandResult { ClientSequence = 262, AppliedTick = 13429,
            Status = CommandStatus.Accepted }, 262, 0);
    }
}
