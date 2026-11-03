namespace Devilution.Client.Acceptance;

/** One acceptance run's melee reaction ticks: from the snapshot a swing was decided on to the tick it applied. */
internal sealed record AcceptanceReactionSummary(int Count, double MeanTicks, ulong MedianTicks, ulong P90Ticks, ulong MaxTicks);

/**
 * Game-tick budgets for acceptance waits, expressed as seconds at the 20 Hz reference rate so a
 * faster test server keeps the same game-time limits. At 20 Hz stages keep their wall-time limits;
 * at other rates a wall-time backstop still fails a stalled server, and a wall-time floor keeps a
 * host stall or a late snapshot from spending a whole stage at once.
 */
internal static class AcceptanceTickBudget
{
    public const uint ReferenceTickRateHz = 20;
    public const double MinimumStageWallSeconds = 3;

    public static ulong Ticks(double referenceSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(referenceSeconds);
        return (ulong)Math.Ceiling(referenceSeconds * ReferenceTickRateHz);
    }

    /** The server's announced rate, or the reference rate before a handshake reports one. */
    public static uint EffectiveRate(uint? tickRateHz) => tickRateHz is null or 0 ? ReferenceTickRateHz : tickRateHz.Value;

    /** Twice the budget's game time at the server's rate, plus ten seconds for startup and frame jitter. */
    public static double WallBackstopSeconds(ulong budget, uint tickRateHz) => 2.0 * budget / Math.Max(1U, tickRateHz) + 10;

    public static bool Expired(ulong currentTick, ulong startTick, ulong budget) => currentTick > startTick + budget;

    /** Whether a wait of `referenceSeconds` may end on ticks yet; the floor never exceeds the wait's own length. */
    public static bool PastWallFloor(double referenceSeconds, double elapsedSeconds)
        => elapsedSeconds >= Math.Min(referenceSeconds, MinimumStageWallSeconds);

    /**
     * Whether a stage of `stageSeconds` is over: wall time at the reference rate or before ticks are
     * observed, otherwise the game-time budget once the wall-time floor has passed, or its wall-time
     * backstop.
     */
    public static bool StageExpired(double stageSeconds, double elapsedSeconds, uint? tickRateHz, ulong? startTick, ulong? currentTick)
    {
        var rate = EffectiveRate(tickRateHz);
        if (rate == ReferenceTickRateHz || startTick is null || currentTick is null)
            return elapsedSeconds >= stageSeconds;
        var budget = Ticks(stageSeconds);
        return Expired(currentTick.Value, startTick.Value, budget) && PastWallFloor(stageSeconds, elapsedSeconds)
            || elapsedSeconds >= WallBackstopSeconds(budget, rate);
    }

    public static AcceptanceReactionSummary? Summarize(IEnumerable<ulong> reactions)
    {
        var sorted = reactions.Order().ToArray();
        if (sorted.Length == 0)
            return null;
        return new(sorted.Length, Math.Round(sorted.Average(ticks => (double)ticks), 2), sorted[sorted.Length / 2],
            sorted[sorted.Length * 9 / 10], sorted[^1]);
    }
}
