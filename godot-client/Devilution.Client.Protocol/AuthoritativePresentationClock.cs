using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Advances presentation time without changing the authoritative simulation tick. */
public sealed class AuthoritativePresentationClock
{
    public const uint DefaultTickRateHz = 20;
    private double leadCorrection;

    public double? CurrentTick { get; private set; }

    public ulong? AuthoritativeSnapshotTick { get; private set; }

    /** True only for the advance that accepted an older authoritative snapshot. */
    public bool LastAdvanceRewound { get; private set; }

    /**
     * Advances from the latest authoritative snapshot while allowing at most
     * one additional server tick of presentation lead for timed walks.
     */
    public double Advance(ulong authoritativeTick, double deltaSeconds, uint tickRateHz)
        => AdvanceCore(authoritativeTick, deltaSeconds, tickRateHz, authoritativeTick == ulong.MaxValue
            ? double.MaxValue : authoritativeTick + 1d);

    /** Native animationinfo.cpp: render fractional tick progress; each actor clamps its own action. */
    public double Advance(Snapshot snapshot, double deltaSeconds, uint tickRateHz)
    {
        // Idle animations may loop between polls; a silent connection still stops after one second.
        var horizon = (double)snapshot.Tick + (tickRateHz == 0 ? DefaultTickRateHz : tickRateHz);
        for (var i = 0; i < snapshot.Players.Count; i++)
            if (snapshot.Players[i].CurrentAction is { } action) horizon = Math.Max(horizon, action.EndTick);
        for (var i = 0; i < snapshot.Monsters.Count; i++)
            if (snapshot.Monsters[i].Moving) horizon = Math.Max(horizon, snapshot.Monsters[i].MovementEffectTick);
        return AdvanceCore(snapshot.Tick, deltaSeconds, tickRateHz, horizon, consumeForwardAdmissionDelta: true);
    }

    private double AdvanceCore(ulong authoritativeTick, double deltaSeconds, uint tickRateHz, double presentationHorizon,
        bool consumeForwardAdmissionDelta = false)
    {
        var frameAlreadyObserved = false;
        LastAdvanceRewound = AuthoritativeSnapshotTick is { } previousTick
            && authoritativeTick < previousTick;
        if (AuthoritativeSnapshotTick is null
            || authoritativeTick < AuthoritativeSnapshotTick.Value) {
            CurrentTick = authoritativeTick;
            leadCorrection = 0;
        } else if (authoritativeTick > AuthoritativeSnapshotTick.Value) {
            frameAlreadyObserved = consumeForwardAdmissionDelta && CurrentTick <= authoritativeTick;
            CurrentTick = Math.Max(CurrentTick ?? authoritativeTick, authoritativeTick);
            // The paced host drops elapsed time after a stall. Repay that lead at
            // half speed on later frames; never reverse an actor's current animation.
            leadCorrection = CurrentTick.Value - authoritativeTick;
        }
        AuthoritativeSnapshotTick = authoritativeTick;

        var frameDelta = double.IsFinite(deltaSeconds) && deltaSeconds > 0
            ? deltaSeconds
            : 0;
        var effectiveTickRate = tickRateHz == 0 ? DefaultTickRateHz : tickRateHz;
        // A forward snapshot already covers this frame's elapsed time. Adding it
        // again skips walk frames, particularly when the server runs above frame rate.
        var advance = frameAlreadyObserved ? 0 : frameDelta * effectiveTickRate;
        var correction = Math.Min(leadCorrection, advance * 0.5);
        leadCorrection -= correction;
        var current = CurrentTick ?? authoritativeTick;
        CurrentTick = Math.Max(current, Math.Min(presentationHorizon, current + advance - correction));
        return CurrentTick.Value;
    }

    public void Reset()
    {
        CurrentTick = null;
        AuthoritativeSnapshotTick = null;
        LastAdvanceRewound = false;
        leadCorrection = 0;
    }
}
