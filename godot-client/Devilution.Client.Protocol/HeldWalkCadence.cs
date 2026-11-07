namespace Devilution.Client.Protocol;

/** Throttle repeated targeting, but never delay continuation when a native reservation advances. */
public sealed class HeldWalkCadence
{
    private double remaining;
    private ClientPosition? previousOrigin;
    public void Advance(double seconds)
    {
        if (double.IsFinite(seconds) && seconds > 0) remaining = Math.Max(0, remaining - seconds);
    }
    public bool Due(ClientPosition origin) => remaining <= 0.000000001 || previousOrigin != origin;
    public void Submitted(ClientPosition origin) { previousOrigin = origin; remaining = 0.1; }
}
