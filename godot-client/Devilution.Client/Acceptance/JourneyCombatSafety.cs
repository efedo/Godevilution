namespace Devilution.Client.Acceptance;

internal enum JourneySafetyActivity { Travel, ObjectApproach, Combat, Retreat, Stairs }

/** Acceptance decisions only: resource units are opaque, and native simulation ticks measure damage. */
internal sealed class JourneyCombatSafety
{
    internal readonly record struct Observation(uint Player, uint Level, ulong Tick, int Life, int MaximumLife,
        int Nearby, int Adjacent, int HealingItems, bool CommandsAdmitted, bool Settled,
        uint TickRateHz = 20, double RoundTripMilliseconds = 0, bool HitRecovery = false, ulong HitRecoveryEndTick = 0,
        int RangedAttackers = 0, uint RangedTarget = 0, bool InterruptibleWalk = false, int PositionX = 0, int PositionY = 0);
    internal readonly record struct Decision(bool Drink, bool StopWalking, bool Retreat,
        double HealingThreshold, double LossPerTick, int Nearby, int Adjacent, int RangedAttackers = 0, uint RangedTarget = 0,
        bool TakingDamage = false)
    {
        internal int Attackers => Adjacent + RangedAttackers;
    }
    private readonly (ulong Tick, ulong Interval, int Loss)[] losses = new (ulong, ulong, int)[32];
    private int count, cursor;
    private Observation? previous;
    private ulong? lastDamageTick;
    internal ulong DamageObservations { get; private set; }

    internal void Reset() { previous = null; count = cursor = 0; lastDamageTick = null; DamageObservations = 0; }

    internal Decision Observe(Observation state, JourneySafetyActivity activity = JourneySafetyActivity.Combat)
    {
        RequireAlive(state);
        if (previous is not { } old || old.Player != state.Player || old.Level != state.Level
            || old.MaximumLife != state.MaximumLife || state.Tick < old.Tick) {
            count = cursor = 0;
            lastDamageTick = null;
        } else {
            if (state.Life < old.Life || state.HitRecovery && (!old.HitRecovery || state.HitRecoveryEndTick > old.HitRecoveryEndTick)) {
                DamageObservations++;
                lastDamageTick = state.Tick;
            }
            if (state.Tick > old.Tick && state.Life < old.Life) {
                losses[cursor] = (state.Tick, state.Tick - old.Tick, old.Life - state.Life);
                cursor = (cursor + 1) % losses.Length;
                count = Math.Min(losses.Length, count + 1);
            }
        }
        previous = state;
        if (state.Level == 0 || state.MaximumLife <= 0)
            return default;

        long lost = 0;
        var earliest = state.Tick;
        for (var index = 0; index < count; index++) {
            var sample = losses[index];
            // Two native seconds, including accelerated 200 Hz acceptance runs.
            if (state.Tick < sample.Tick || state.Tick - sample.Tick > 40) continue;
            lost += sample.Loss;
            earliest = Math.Min(earliest, sample.Tick >= sample.Interval ? sample.Tick - sample.Interval : 0);
        }
        var rate = lost / (double)Math.Max(1UL, state.Tick - earliest);
        var takingDamage = state.HitRecovery || lastDamageTick is { } hit && state.Tick - hit <= 40;
        // Spend most of a dose: half life normally, two thirds only with <=3 native seconds to live.
        // Native simulation is 20 ticks/second even when the acceptance host clocks it at 200 Hz.
        var threshold = rate > 0 && state.Life / rate <= 60 ? .65 : .50;
        // A poll, a real UI click, scheduling lead and a round trip must fit before the next burst.
        var roundTrip = double.IsFinite(state.RoundTripMilliseconds) ? Math.Clamp(state.RoundTripMilliseconds, 0, 250) : 0;
        var reactionTicks = Math.Max(8, Math.Ceiling(Math.Max(1, state.TickRateHz) * (.35 + roundTrip / 1000)));
        var projected = state.Life - rate * reactionTicks;
        // With no quick-use left, healthy melee is safer than repeatedly yielding to a pursuing pack.
        // A completed kill can retain loss history before corpse conversion; require a live threat to retreat.
        var retreat = state.HealingItems == 0
            ? state.Life <= state.MaximumLife * .35 && state.Nearby > 0
            : state.Adjacent >= 3 && state.Life < state.MaximumLife * .80
                || rate > 0 && projected < state.MaximumLife * .20 && state.Life < state.MaximumLife * .90;
        // Yielding indefinitely cannot disengage stationary shooters. Close on a confirmed
        // damage source, defending adjacent interceptors, unless melee already surrounds us.
        if (state.HealingItems == 0 && state.RangedTarget != 0 && state.Adjacent < 3) retreat = false;
        // The six-tile scan is not a request to clear a quiet room. Actual incoming fire,
        // adjacent pressure and the existing blocked-route recovery still stop the walk.
        return new(state.HealingItems > 0 && state.Life < state.MaximumLife && state.Life <= state.MaximumLife * threshold,
            state.Adjacent >= 2 || state.RangedTarget != 0 || takingDamage && state.Adjacent == 0
                || rate > 0 && projected < state.MaximumLife * .5,
            retreat, threshold, rate, state.Nearby, state.Adjacent, state.RangedAttackers, state.RangedTarget, takingDamage);
    }

    internal static void RequireAlive(Observation state)
    {
        if (state.Life <= 0)
            throw new InvalidOperationException($"Journey safety: the character died at tick {state.Tick}; "
                + $"entity={state.Player}, level={state.Level}, life={state.Life}/{state.MaximumLife}, "
                + $"nearby={state.Nearby}, adjacent={state.Adjacent}, potions={state.HealingItems}.");
    }

    /** Dungeon membership or nearby attackers alone cannot excuse a potion with no observed effect. */
    internal bool HealingOutcomeValid(int before, int maximum, int after, ulong damageAtDispatch)
        => after > before || DamageObservations > damageAtDispatch && before < maximum && after > 0;

    /** Shared by floor attempts: selecting the pursuer again must not replenish retreat work. */
    internal sealed class RetreatBudget(int maximumSteps)
    {
        internal int Used { get; private set; }
        internal void Charge(Observation state)
        {
            if (state.Life > state.MaximumLife * .35) return;
            if (++Used >= maximumSteps)
                throw new InvalidOperationException($"Native Warrior cannot disengage on L{state.Level} after {Used}/{maximumSteps} retreat steps; "
                    + (state.HealingItems == 0 ? "out of potions" : $"potions={state.HealingItems}")
                    + $", life={state.Life}/{state.MaximumLife}, nearby={state.Nearby}, tick={state.Tick}.");
        }
    }

    /** A later quick-use receipt cannot identify the accepted attack that earned a kill. */
    internal static ulong KillAttackSequence(uint target, IEnumerable<(uint Target, ulong Sequence)> acceptedShots)
    {
        ulong sequence = 0;
        foreach (var shot in acceptedShots)
            if (shot.Target == target && shot.Sequence > 0) sequence = shot.Sequence;
        return sequence > 0 ? sequence
            : throw new InvalidOperationException($"Native target {target} produced a kill without a preserved accepted attack receipt.");
    }

    /** Callers invoke this before planning the next action, never while validating an earlier command's receipt. */
    internal async Task<bool> CheckpointAsync(JourneySafetyActivity activity, Func<Observation> observe,
        Func<Task> drink, Func<Task>? defend = null)
    {
        var state = observe();
        var decision = Observe(state, activity);
        if (!state.CommandsAdmitted) return false;
        var changed = false;
        if (decision.Drink) {
            await drink(); changed = true;
            state = observe(); decision = Observe(state, activity);
        }
        if (activity is JourneySafetyActivity.Travel or JourneySafetyActivity.ObjectApproach or JourneySafetyActivity.Stairs
            && state.Settled && decision.StopWalking && defend is not null) {
            await defend(); changed = true;
            Observe(observe(), activity);
        }
        return changed;
    }

    /** Hit recovery must not defer a due quick-use until Stand; the earlier command's receipt is already owned by its caller. */
    internal async Task WaitForSettlementAsync(Func<Observation> observe, Func<Task> frame, Func<Task> drink,
        Func<Observation, bool> expired, string context, Func<Task>? interruptWalk = null)
    {
        var walkInterrupted = false;
        Observation? previousWalk = null;
        ulong idleWalkSince = 0;
        while (true) {
            var state = observe();
            var decision = Observe(state);
            if (previousWalk is not { } old || old.Player != state.Player || old.Level != state.Level
                || state.Tick < old.Tick || old.PositionX != state.PositionX || old.PositionY != state.PositionY
                || !state.InterruptibleWalk)
                idleWalkSince = state.Tick;
            previousWalk = state;
            if (state.CommandsAdmitted && state.Settled) return;
            if (expired(state)) throw new InvalidOperationException($"{context} did not settle at tick {state.Tick}; life={state.Life}/{state.MaximumLife}.");
            // Quick-use must run before any nested movement/receipt wait can take control.
            if (state.CommandsAdmitted && decision.Drink) {
                await drink();
                await frame();
                continue;
            }
            // Stand between native steps is not a stall. Require a native second with no position
            // or action progress before replacing a blocked destination, irrespective of host Hz.
            // A blocked native destination may remain pending in Stand indefinitely. Replace it
            // once through the ordinary control. Replacing that replacement walks back and forth
            // between stale snapshot cells and starves both healing and the original approach.
            if (!walkInterrupted && state.CommandsAdmitted && state.InterruptibleWalk && state.Tick - idleWalkSince >= 20
                && decision.TakingDamage && interruptWalk is not null) {
                walkInterrupted = true;
                await interruptWalk();
            }
            await frame();
        }
    }

    internal static async Task<TReceipt> PreserveReceiptAsync<TReceipt>(Func<Task<TReceipt>> admit, Func<Task> settle)
    {
        var receipt = await admit();
        await settle();
        return receipt;
    }

    /** State-only waits own no pending receipt: finish first, otherwise keep quick-use live. */
    internal async Task<bool> PollAsync(Func<bool> complete, Func<Observation> observe, Func<Task> drink)
    {
        var state = observe();
        var decision = Observe(state);
        if (complete()) return true;
        if (state.CommandsAdmitted && decision.Drink) await drink();
        return false;
    }

    /** A queued ranged approach yields to its caller's quick-use after owning the movement receipt. */
    internal bool MovementWaitReady(Observation state)
    {
        var decision = Observe(state);
        return state.CommandsAdmitted && (state.Settled || decision.Drink);
    }

    internal static async Task ProtectRouteAsync(uint sourceLevel, Func<uint> level, Func<bool> active,
        Func<Task> cancel, Func<Task> drain, Func<Task> protect, Func<Task> settle, Func<Task> resume)
    {
        if (active()) await cancel();
        await drain();
        if (level() != sourceLevel) { await settle(); return; }
        await protect();
        await settle();
        // A final walking step can reach the stairs while cancellation/healing is being admitted.
        if (level() == sourceLevel) await resume();
    }
}
