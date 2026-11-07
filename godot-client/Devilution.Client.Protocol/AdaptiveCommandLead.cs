namespace Devilution.Client.Protocol;

/** Scheduling evidence only: retry/probe timeout floors must never delay realtime input. */
internal sealed class AdaptiveCommandLead
{
    internal const double SafetyMarginMilliseconds = 2;
    internal const double DecayHalfLifeMilliseconds = 1000;
    private readonly ClockGatedRoundTripWindow roundTrips = new(0, ClockGatedRoundTripWindow.DefaultWindowMilliseconds);
    private double jitterPeak, feedbackFloor;
    private long jitterAt, feedbackAt;

    public double RoundTripMilliseconds => roundTrips.Estimate;

    public void ObserveRoundTrip(long now, double milliseconds)
    {
        var sample = Normalize(milliseconds);
        var median = roundTrips.Observe(now, sample);
        jitterPeak = Math.Max(Decay(jitterPeak, jitterAt, now), Math.Max(0, sample - median));
        jitterAt = now;
    }

    public int Ticks(long now, uint rate)
    {
        if (rate == 0) throw new ArgumentOutOfRangeException(nameof(rate));
        // The clock stamp ages on the reply's inbound leg; the next command also needs its outbound leg.
        var milliseconds = Math.Max(roundTrips.Estimate + Decay(jitterPeak, jitterAt, now) + SafetyMarginMilliseconds,
            Decay(feedbackFloor, feedbackAt, now));
        return Math.Max(1, (int)Math.Ceiling(Math.Min(milliseconds,
            AuthoritativeClient.MaximumClockGatedLeadMilliseconds) * rate / 1000));
    }

    public void ObserveLateAdmission(long now, uint rate, int sentLeadTicks, ulong lateTicks)
    {
        if (lateTicks == 0) return;
        if (rate == 0) throw new ArgumentOutOfRangeException(nameof(rate));
        // A matched first-send receipt measures real admission error, including host/sender stalls.
        // Add a tick beyond that measured shortfall; repeated/old receipts cannot compound it.
        var needed = Math.Min(AuthoritativeClient.MaximumClockGatedLeadMilliseconds,
            (Math.Max(1, sentLeadTicks) + (double)lateTicks + 1) * 1000 / rate);
        feedbackFloor = Math.Max(Decay(feedbackFloor, feedbackAt, now), needed);
        feedbackAt = now;
    }

    private static double Normalize(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 0, AuthoritativeClient.MaximumClockGatedLeadMilliseconds)
            : AuthoritativeClient.MaximumClockGatedLeadMilliseconds;

    private static double Decay(double value, long then, long now)
        => value * Math.Pow(0.5, Math.Max(0d, (double)now - then) / DecayHalfLifeMilliseconds);
}
