using System.Diagnostics;

namespace Devilution.Client.Acceptance;

/**
 * Wall time against server ticks for the Warrior depth journey, written as
 * <phase>-timing.json even when the phase fails. A clock share near 1 means the
 * leg waited on the server's real-time tick clock; lower values point at client,
 * checkpoint or rendering cost instead. Melee reactions record the ticks from the
 * snapshot a swing was decided on to the tick it applied.
 */
public partial class SessionAcceptance
{
    private sealed record NativeWarriorTimingSegment(string Kind, string Label, int Depth, double WallSeconds, ulong Ticks);

    private sealed class NativeWarriorTiming(Main main)
    {
        private readonly Stopwatch total = Stopwatch.StartNew();
        private readonly ulong startTick = main.ObservedSnapshot!.Tick;
        private readonly List<NativeWarriorTimingSegment> segments = [];
        private readonly List<ulong> reactions = [];
        private int depth;

        /** Times one journey step until the returned scope is disposed; nested steps keep their own entries. */
        public IDisposable Step(string kind, string label)
        {
            var started = total.Elapsed.TotalSeconds;
            var tick = main.ObservedSnapshot!.Tick;
            var stepDepth = depth++;
            return new Scope(() => {
                depth--;
                segments.Add(new(kind, label, stepDepth, Math.Round(total.Elapsed.TotalSeconds - started, 3),
                    main.ObservedSnapshot!.Tick - tick));
            });
        }

        public void Reaction(ulong ticks) => reactions.Add(ticks);

        public void Write(string directory, string phase, bool passed)
        {
            var rate = AcceptanceTickBudget.EffectiveRate(main.ObservedIdentity?.TickRateHz);
            double Share(double seconds, ulong ticks) => seconds <= 0 ? 0 : Math.Round(ticks / (double)rate / seconds, 3);
            var wall = total.Elapsed.TotalSeconds;
            var ticks = main.ObservedSnapshot!.Tick - startTick;
            var kinds = segments.Where(segment => segment.Depth == 0).GroupBy(segment => segment.Kind)
                .Select(group => new {
                    Kind = group.Key, Count = group.Count(), WallSeconds = Math.Round(group.Sum(item => item.WallSeconds), 3),
                    Ticks = group.Aggregate(0UL, (sum, item) => sum + item.Ticks),
                })
                .Select(group => new { group.Kind, group.Count, group.WallSeconds, group.Ticks, ClockShare = Share(group.WallSeconds, group.Ticks) })
                .OrderByDescending(group => group.WallSeconds).ToArray();
            WriteEvidence(directory, phase.ToLowerInvariant() + "-timing.json", new {
                Phase = phase, Passed = passed, TickRateHz = rate, WallSeconds = Math.Round(wall, 3), Ticks = ticks,
                ClockShare = Share(wall, ticks),
                UntimedSeconds = Math.Round(wall - kinds.Sum(kind => kind.WallSeconds), 3),
                Kinds = kinds, MeleeReactions = AcceptanceTickBudget.Summarize(reactions), Segments = segments.ToArray(),
            });
        }

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }

    private NativeWarriorTiming? nativeWarriorTiming;

    private IDisposable? NativeWarriorStep(string kind, string label) => nativeWarriorTiming?.Step(kind, label);

    /** Waits while `waiting` holds, for at most `referenceSeconds` of game time at 20 Hz; never fails. */
    private async Task WaitTicksWhile(Main main, Func<bool> waiting, double referenceSeconds)
    {
        var budget = AcceptanceTickBudget.Ticks(referenceSeconds);
        var start = main.ObservedSnapshot!.Tick;
        var wallLimit = AcceptanceTickBudget.WallBackstopSeconds(budget, AcceptanceTickBudget.EffectiveRate(main.ObservedIdentity?.TickRateHz));
        var backstop = Stopwatch.StartNew();
        while (waiting() && backstop.Elapsed.TotalSeconds < wallLimit
            && !(AcceptanceTickBudget.Expired(main.ObservedSnapshot!.Tick, start, budget)
                && AcceptanceTickBudget.PastWallFloor(referenceSeconds, backstop.Elapsed.TotalSeconds)))
            await Frame();
    }
}
