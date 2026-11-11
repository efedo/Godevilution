using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Allocation counters are thread-local: keep each warmed synchronous window off the runner's other collections. */
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AllocationMeasurementCollection
{
    public const string Name = "Synchronous hot-path allocation";
}

internal static class HotPathAllocation
{
    static HotPathAllocation()
    {
        // Prime measurement APIs and their test-only call site before any measured production operation.
        Measure(static _ => { }, 1);
    }

    public static void Warm(Action<int> operation, int iterations = 4096)
    {
        for (var i = 0; i < iterations; i++) operation(i);
    }

    // Avoid tiered/OSR transitions in the measuring loop itself; production callees retain their normal JIT policy.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static (long Bytes, TimeSpan Elapsed) Measure(Action<int> operation, int iterations)
    {
        var started = Stopwatch.GetTimestamp();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) operation(i);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return (bytes, Stopwatch.GetElapsedTime(started)); // Timing, formatting and assertions are outside the byte window.
    }
}

[Collection(AllocationMeasurementCollection.Name)]
public sealed class HotPathAllocationTests
{
    [Fact]
    public void WarmupIsExcludedButEveryMeasuredAllocationIsCounted()
    {
        Action<int> allocating = static _ => GC.KeepAlive(new byte[32]);
        HotPathAllocation.Warm(allocating);
        var empty = HotPathAllocation.Measure(static _ => { }, 1000);
        Assert.Equal(0, empty.Bytes);
        var allocated = HotPathAllocation.Measure(allocating, 1000);
        Assert.True(allocated.Bytes >= 32000);
    }

    [Fact]
    public void MeasurementRunsExactlyTheRequestedIterationsWithoutRetrying()
    {
        var calls = new int[17];
        var result = HotPathAllocation.Measure(i => calls[i]++, calls.Length);
        Assert.Equal(0, result.Bytes);
        Assert.All(calls, count => Assert.Equal(1, count));
    }
}
