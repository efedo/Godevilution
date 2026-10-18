using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class CohortAuthorityTurnGateTests
{
    [Fact]
    public async Task QueuedForegroundTurnCannotBeOvertakenByImmediateReacquire()
    {
        var gate = new CohortAuthorityTurnGate();
        var owner = await gate.EnterAsync(CancellationToken.None);
        var foreground = gate.EnterAsync(CancellationToken.None);

        Assert.False(foreground.IsCompleted);
        Assert.Equal(1, gate.QueuedWaiterCount);
        owner.Dispose();

        var periodic = gate.EnterAsync(CancellationToken.None);
        Assert.Equal(1, gate.QueuedWaiterCount);
        var foregroundLease = await foreground.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(periodic.IsCompleted);

        foregroundLease.Dispose();
        using var periodicLease = await periodic.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task QueuedPeriodicTurnRunsBeforeContinuedLaterIngress()
    {
        var gate = new CohortAuthorityTurnGate();
        var owner = await gate.EnterAsync(CancellationToken.None);
        var periodic = gate.EnterAsync(CancellationToken.None);
        owner.Dispose();

        var laterIngress = Enumerable.Range(0, 8)
            .Select(_ => gate.EnterAsync(CancellationToken.None).AsTask())
            .ToArray();
        var periodicLease = await periodic.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.All(laterIngress, task => Assert.False(task.IsCompleted));

        periodicLease.Dispose();
        foreach (var waitingTurn in laterIngress) {
            var lease = await waitingTurn.WaitAsync(TimeSpan.FromSeconds(2));
            lease.Dispose();
        }
    }

    [Fact]
    public async Task CanceledHeadAndMiddleWaitersAreSkippedWithoutStrandingGate()
    {
        var gate = new CohortAuthorityTurnGate();
        var owner = await gate.EnterAsync(CancellationToken.None);
        using var headCancellation = new CancellationTokenSource();
        using var middleCancellation = new CancellationTokenSource();
        var canceledHead = gate.EnterAsync(headCancellation.Token);
        var survivingMiddle = gate.EnterAsync(CancellationToken.None);
        var canceledMiddle = gate.EnterAsync(middleCancellation.Token);
        var tail = gate.EnterAsync(CancellationToken.None);
        Assert.Equal(4, gate.QueuedWaiterCount);

        headCancellation.Cancel();
        middleCancellation.Cancel();
        Assert.Equal(2, gate.QueuedWaiterCount);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await canceledHead.AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await canceledMiddle.AsTask());

        owner.Dispose();
        var survivorLease = await survivingMiddle.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(tail.IsCompleted);
        survivorLease.Dispose();

        using var tailLease = await tail.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CancellationAfterGrantDoesNotOrphanOrDoubleReleaseLease()
    {
        var gate = new CohortAuthorityTurnGate();
        var owner = await gate.EnterAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var grantedWaiter = gate.EnterAsync(cancellation.Token);
        var nextWaiter = gate.EnterAsync(CancellationToken.None);

        owner.Dispose();
        cancellation.Cancel();

        var grantedLease = await grantedWaiter.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(nextWaiter.IsCompleted);
        grantedLease.Dispose();
        grantedLease.Dispose();

        using var nextLease = await nextWaiter.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CancellationCallbackCompletesWhileLogicalLeaseIsHeld()
    {
        var gate = new CohortAuthorityTurnGate();
        var owner = await gate.EnterAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var canceledWaiter = gate.EnterAsync(cancellation.Token);

        await Task.Run(cancellation.Cancel).WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await canceledWaiter.AsTask());

        var nextWaiter = gate.EnterAsync(CancellationToken.None);
        Assert.False(nextWaiter.IsCompleted);
        owner.Dispose();
        using var nextLease = await nextWaiter.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }
}
