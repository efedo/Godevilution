namespace Devilution.Client.Protocol;

/**
 * The clock-gated command lead's round-trip estimate: the median probe sample of
 * the last `window`, never below `floor`. A lone stall does not raise the lead,
 * because a command scheduled seconds ahead leaves the client without snapshots
 * and a live host accepts one that arrives late; a sustained rise still does.
 * Not thread-safe; the client calls it under its pending lock.
 */
public sealed class ClockGatedRoundTripWindow(double floorMilliseconds, long windowMilliseconds)
{
    private readonly Queue<(long At, double Sample)> samples = new();

    public ClockGatedRoundTripWindow()
        : this(DefaultFloorMilliseconds, DefaultWindowMilliseconds)
    {
    }

    public const double DefaultFloorMilliseconds = 100;
    public const long DefaultWindowMilliseconds = 10_000;

    public double Estimate { get; private set; } = floorMilliseconds;

    /** Records one probe sample taken at `now` and returns the new estimate. */
    public double Observe(long now, double sampleMilliseconds)
    {
        samples.Enqueue((now, Math.Max(0, sampleMilliseconds)));
        while (samples.Count > 1 && now - samples.Peek().At > windowMilliseconds)
            samples.Dequeue();
        var sorted = samples.Select(entry => entry.Sample).Order().ToArray();
        return Estimate = Math.Max(floorMilliseconds, sorted[sorted.Length / 2]);
    }
}
