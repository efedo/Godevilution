namespace Devilution.Client.Protocol;

/** Advances presentation time without changing the authoritative simulation tick. */
public sealed class AuthoritativePresentationClock
{
    public const uint DefaultTickRateHz = 20;

    public double? CurrentTick { get; private set; }

    public ulong? AuthoritativeSnapshotTick { get; private set; }

    /** True only for the advance that accepted an older authoritative snapshot. */
    public bool LastAdvanceRewound { get; private set; }

    /**
     * Advances from the latest authoritative snapshot while allowing at most
     * one additional server tick of presentation lead for timed walks.
     */
    public double Advance(ulong authoritativeTick, double deltaSeconds, uint tickRateHz)
    {
        LastAdvanceRewound = AuthoritativeSnapshotTick is { } previousTick
            && authoritativeTick < previousTick;
        if (AuthoritativeSnapshotTick is null
            || authoritativeTick < AuthoritativeSnapshotTick.Value) {
            CurrentTick = authoritativeTick;
        } else if (authoritativeTick > AuthoritativeSnapshotTick.Value) {
            CurrentTick = Math.Max(CurrentTick ?? authoritativeTick, authoritativeTick);
        }
        AuthoritativeSnapshotTick = authoritativeTick;

        var frameDelta = double.IsFinite(deltaSeconds) && deltaSeconds > 0
            ? deltaSeconds
            : 0;
        var effectiveTickRate = tickRateHz == 0 ? DefaultTickRateHz : tickRateHz;
        var presentationHorizon = authoritativeTick == ulong.MaxValue
            ? double.MaxValue
            : authoritativeTick + 1d;
        CurrentTick = Math.Min(
            presentationHorizon,
            (CurrentTick ?? authoritativeTick) + frameDelta * effectiveTickRate);
        return CurrentTick.Value;
    }

    public void Reset()
    {
        CurrentTick = null;
        AuthoritativeSnapshotTick = null;
        LastAdvanceRewound = false;
    }
}
