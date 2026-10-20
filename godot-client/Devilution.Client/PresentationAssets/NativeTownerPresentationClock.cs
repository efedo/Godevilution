namespace Devilution.Client.PresentationAssets;

/** Cosmetic active-Town time. It owns no simulation cursor or native initialization RNG. */
public sealed class NativeTownerPresentationClock
{
    public const double MaximumFrameSeconds = 0.25;
    private bool active;
    public double Tick { get; private set; }

    public void Reset()
    {
        active = false;
        Tick = 0;
    }

    public double Advance(double delta, uint tickRateHz, bool inTown, bool connected, bool reset = false)
    {
        if (!inTown || reset || !active) {
            Tick = 0;
            active = inTown;
            return Tick;
        }
        if (connected && double.IsFinite(delta) && delta > 0)
            Tick += Math.Min(delta, MaximumFrameSeconds) * Math.Clamp(tickRateHz, 1U, 120U);
        return Tick;
    }
}
