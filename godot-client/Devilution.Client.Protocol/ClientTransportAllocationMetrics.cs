using System.Diagnostics;

namespace Devilution.Client.Protocol;

/** Opt-in cumulative counters. Only synchronous decode/processing sections use thread allocation counters. */
public sealed class ClientTransportAllocationMetrics
{
    private long snapshots, wireBytes, decodeBytes, processingBytes, decodeTicks;
    public ClientTransportAllocationObservation Capture() => new(Interlocked.Read(ref snapshots),
        Interlocked.Read(ref wireBytes), Interlocked.Read(ref decodeBytes), Interlocked.Read(ref processingBytes),
        Interlocked.Read(ref decodeTicks) * 1000d / Stopwatch.Frequency);

    internal void DecodedSnapshot(int bytes, long allocated, long elapsedTicks)
    {
        Interlocked.Increment(ref snapshots);
        Interlocked.Add(ref wireBytes, bytes);
        Interlocked.Add(ref decodeBytes, allocated);
        Interlocked.Add(ref decodeTicks, elapsedTicks);
    }
    internal void ProcessedSnapshot(long allocated) => Interlocked.Add(ref processingBytes, allocated);
}
public readonly record struct ClientTransportAllocationObservation(long SnapshotCount, long SnapshotWireBytes,
    long DecodeAllocatedBytes, long ProcessingAllocatedBytes, double DecodeMilliseconds);
