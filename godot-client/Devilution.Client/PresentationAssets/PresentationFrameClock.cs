namespace Devilution.Client.PresentationAssets;

/** Delta-driven frame clock that cannot affect authoritative state or RNG. */
public sealed class PresentationFrameClock
{
    private double elapsedSeconds;

    public double ElapsedSeconds => elapsedSeconds;

    public void Reset() => elapsedSeconds = 0;

    public void Advance(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0)
            return;
        elapsedSeconds = Math.Min(elapsedSeconds + deltaSeconds, double.MaxValue);
    }

    public int ResolveFrame(PresentationFrameLayout? layout)
    {
        if (layout is null || layout.FrameCount <= 1
            || !double.IsFinite(layout.FramesPerSecond) || layout.FramesPerSecond <= 0)
            return 0;
        if (!layout.Loop)
            return (int)Math.Min(Math.Floor(elapsedSeconds * layout.FramesPerSecond), layout.FrameCount - 1);
        // Reduce time before multiplication so even very large finite deltas cannot overflow.
        var period = layout.FrameCount / layout.FramesPerSecond;
        var frame = Math.Floor((elapsedSeconds % period) * layout.FramesPerSecond);
        return (int)(frame % layout.FrameCount);
    }
}
