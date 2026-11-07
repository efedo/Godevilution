using Devilution.Protocol.V1;

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
        => AdvanceCore(authoritativeTick, deltaSeconds, tickRateHz, authoritativeTick == ulong.MaxValue
            ? double.MaxValue : authoritativeTick + 1d);

    /** Native animationinfo.cpp: render fractional tick progress; each actor clamps its own action. */
    public double Advance(Snapshot snapshot, double deltaSeconds, uint tickRateHz)
    {
        // Idle animations may loop between polls; a silent connection still stops after one second.
        var horizon = (double)snapshot.Tick + (tickRateHz == 0 ? DefaultTickRateHz : tickRateHz);
        foreach (var player in snapshot.Players)
            if (player.CurrentAction is { } action) horizon = Math.Max(horizon, action.EndTick);
        foreach (var monster in snapshot.Monsters)
            if (monster.Moving) horizon = Math.Max(horizon, monster.MovementEffectTick);
        return AdvanceCore(snapshot.Tick, deltaSeconds, tickRateHz, horizon);
    }

    private double AdvanceCore(ulong authoritativeTick, double deltaSeconds, uint tickRateHz, double presentationHorizon)
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
