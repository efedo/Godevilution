namespace Devilution.Client.Protocol;

/**
 * The clock-gated command lead's round-trip estimate: the largest probe sample of
 * the last `window`, never below `floor`. A lone server stall raises the lead
 * only until later probes age it out, instead of for the rest of the session.
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
        var largest = floorMilliseconds;
        foreach (var (_, sample) in samples)
            largest = Math.Max(largest, sample);
        return Estimate = largest;
    }
}
