namespace Devilution.Client.Acceptance;

internal enum TownCapturePhase { Warmup, Idle, Walking, UnreachableClicks, ResidentClicks, Complete }

/** Wall-clock phases keep the ordinary 20 Hz game, renderer and input cadence unchanged. */
internal sealed class TownCaptureSequence(bool smoke)
{
    internal double WarmupSeconds => smoke ? 2 : 10;
    internal double IdleSeconds => smoke ? 3 : 45;
    internal double WalkingSeconds => smoke ? 8 : 60;
    internal const int UnreachableClickCount = 10;
    internal const int ResidentClickCount = 3;
    internal TownCapturePhase Phase { get; private set; } = TownCapturePhase.Warmup;
    internal double PhaseStartedAt { get; private set; }
    internal int Clicks { get; private set; }

    internal bool UsesHeldKeys(double elapsed)
        => Phase == TownCapturePhase.Walking && elapsed - PhaseStartedAt < WalkingSeconds / 2;

    internal bool Advance(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < PhaseStartedAt)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        var done = Phase switch {
            TownCapturePhase.Warmup => elapsed - PhaseStartedAt >= WarmupSeconds,
            TownCapturePhase.Idle => elapsed - PhaseStartedAt >= IdleSeconds,
            TownCapturePhase.Walking => elapsed - PhaseStartedAt >= WalkingSeconds,
            TownCapturePhase.UnreachableClicks => Clicks == UnreachableClickCount,
            TownCapturePhase.ResidentClicks => Clicks == ResidentClickCount,
            _ => false,
        };
        if (!done) return false;
        Phase++;
        PhaseStartedAt = elapsed;
        Clicks = 0;
        return true;
    }

    internal void Clicked()
    {
        var maximum = Phase switch {
            TownCapturePhase.UnreachableClicks => UnreachableClickCount,
            TownCapturePhase.ResidentClicks => ResidentClickCount,
            _ => throw new InvalidOperationException("This capture phase does not count clicks."),
        };
        if (Clicks >= maximum) throw new InvalidOperationException("The capture phase already has all required clicks.");
        Clicks++;
    }
}
