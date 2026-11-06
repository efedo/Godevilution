using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ManualTimeProviderTests
{
    [Fact]
    public void ClientOptionsKeepTheSystemClockByDefault()
    {
        var options = new Devilution.Client.Protocol.ClientConnectionOptions("localhost", 1, "test", "test", "hash");
        Assert.Same(TimeProvider.System, options.TimeProvider);
    }

    [Fact]
    public void FiresTimersInDeadlineOrderAndStopsDisposedPeriodicTimers()
    {
        var time = new ManualTimeProvider();
        var observed = new List<long>();
        using var late = time.CreateTimer(_ => observed.Add(time.GetTimestamp()), null,
            TimeSpan.FromMilliseconds(25), Timeout.InfiniteTimeSpan);
        using var periodic = time.CreateTimer(_ => observed.Add(time.GetTimestamp()), null,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
        time.AdvanceMilliseconds(25);
        Assert.Equal(new long[] { 100_000, 200_000, 250_000 }, observed);
        periodic.Dispose();
        time.AdvanceMilliseconds(25);
        Assert.Equal(3, observed.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(50), time.GetElapsedTime(0));
    }

    [Fact]
    public async Task AdvancesDelayAndCancellationWithoutSleeping()
    {
        var time = new ManualTimeProvider();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2), time);
        var delay = Task.Delay(TimeSpan.FromSeconds(1), time, cancellation.Token);
        Assert.False(delay.IsCompleted);
        time.AdvanceMilliseconds(1000);
        await delay;
        Assert.False(cancellation.IsCancellationRequested);
        time.AdvanceMilliseconds(1000);
        Assert.True(cancellation.IsCancellationRequested);
    }
}
