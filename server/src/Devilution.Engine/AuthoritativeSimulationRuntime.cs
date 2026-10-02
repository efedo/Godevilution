namespace Devilution.Engine;

/** One event paired with the module state that should receive it. */
public readonly record struct DirectedSimulationEvent<TState, TEvent>(
    TState Recipient,
    TEvent Event)
    where TState : class;

/** Result of one participant operation plus its transient events. */
public readonly record struct SimulationStepResult<TResult, TEvent>(
    TResult Result,
    IReadOnlyList<TEvent> Events)
{
    public SimulationStepResult(TResult result)
        : this(result, [])
    {
    }
}

/** Game-module hooks consumed by the generic authoritative runtime. */
public interface IAuthoritativeSimulationLifecycle<TState, TEvent>
    where TState : class
{
    TState CreateParticipant(string sessionId);

    void AdvanceParticipant(TState state, ulong tick);

    IReadOnlyList<DirectedSimulationEvent<TState, TEvent>> AdvanceTick(
        ulong tick,
        IReadOnlyList<TState> participants);
}

/**
 * Serializes participant access, advances deterministic ticks exactly once,
 * and owns transient event delivery without depending on a game or transport.
 */
public sealed class AuthoritativeSimulationRuntime<TState, TEvent>
    where TState : class
{
    private readonly object synchronization = new();
    private readonly IAuthoritativeSimulationLifecycle<TState, TEvent> lifecycle;
    private readonly SimulationSessionCoordinator<TState, TEvent> sessions;
    private ulong lastAdvancedTick;
    private bool cohortInitialized;

    public AuthoritativeSimulationRuntime(
        IAuthoritativeSimulationLifecycle<TState, TEvent> lifecycle)
    {
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        sessions = new(lifecycle.CreateParticipant);
    }

    public ulong LastAdvancedTick
    {
        get {
            lock (synchronization)
                return lastAdvancedTick;
        }
    }

    /**
     * Synchronizes the runtime clock with a module snapshot loaded from
     * persistence. Pending events are transient and are discarded because
     * they are not part of the restored snapshot. The shared world timeline
     * is monotonic, so an older participant checkpoint cannot rewind it when
     * multiple sessions are restored into one runtime.
     */
    public void RestoreAtTick(ulong tick)
    {
        lock (synchronization) {
            lastAdvancedTick = Math.Max(lastAdvancedTick, tick);
            sessions.ClearEvents();
        }
    }

    /**
     * Captures the exact clock and all existing participants in ordinal session
     * order under one barrier, without creation, advancement or event delivery.
     * The callback must detach its payload before returning: module states are
     * mutable and must not escape the barrier as a purported immutable checkpoint.
     */
    public TResult CaptureCohort<TResult>(
        Func<ulong, IReadOnlyList<SimulationSession<TState>>, TResult> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        lock (synchronization)
            return capture(lastAdvancedTick, sessions.GetOrderedSessions());
    }

    /**
     * Installs a prevalidated module cohort in a fresh, unexposed runtime without
     * invoking lifecycle hooks. Invalid bindings leave the roster and clock
     * untouched. This is one-shot even for an empty cohort at tick zero; it is
     * not a rollback mechanism for a live module's shared mutable state.
     */
    public void InitializeCohort(
        ulong tick,
        IReadOnlyList<SimulationSession<TState>> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        lock (synchronization) {
            if (cohortInitialized || sessions.Count != 0 || lastAdvancedTick != 0)
                throw new InvalidOperationException(
                    "Cohort initialization requires a fresh, empty runtime.");
            sessions.AddRange(participants);
            lastAdvancedTick = tick;
            cohortInitialized = true;
        }
    }

    /** Registers module-created state before any implicit participant access. */
    public void AddParticipant(string sessionId, TState state)
    {
        lock (synchronization)
            sessions.Add(sessionId, state);
    }

    /** Registers module-created state only when no state exists for the session. */
    public bool TryAddParticipant(string sessionId, TState state)
    {
        lock (synchronization)
            return sessions.TryAdd(sessionId, state);
    }

    /**
     * Writes existing state or validates freshly created state before registering
     * it, so failed initialization cannot leave a partial participant behind.
     */
    public bool WriteOrAddParticipant(
        string sessionId,
        Func<TState> stateFactory,
        Action<TState> write)
    {
        ArgumentNullException.ThrowIfNull(stateFactory);
        ArgumentNullException.ThrowIfNull(write);
        lock (synchronization) {
            if (sessions.TryGet(sessionId, out var existing)) {
                write(existing!);
                return false;
            }

            var state = stateFactory()
                ?? throw new InvalidOperationException(
                    "The participant state factory returned null.");
            write(state);
            sessions.Add(sessionId, state);
            return true;
        }
    }

    public TResult Execute<TResult>(
        string sessionId,
        ulong appliedTick,
        Func<TState, IReadOnlyList<TState>, SimulationStepResult<TResult, TEvent>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (synchronization) {
            var state = sessions.GetOrCreate(sessionId);
            lifecycle.AdvanceParticipant(state, appliedTick);
            var participants = GetOrderedStates();
            var result = operation(state, participants);
            if (result.Events is null) {
                throw new InvalidOperationException(
                    "A simulation operation returned a null event collection.");
            }
            foreach (var simulationEvent in result.Events)
                sessions.EnqueueEvent(sessionId, simulationEvent);
            return result.Result;
        }
    }

    public TResult Read<TResult>(string sessionId, Func<TState, TResult> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        lock (synchronization)
            return read(sessions.GetOrCreate(sessionId));
    }

    public void Write(string sessionId, Action<TState> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        lock (synchronization)
            write(sessions.GetOrCreate(sessionId));
    }

    /** Advances existing participants before capturing the requested one. */
    public TResult Capture<TResult>(
        string sessionId,
        ulong tick,
        Func<TState, TResult> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        lock (synchronization) {
            AdvanceToCore(tick);
            var state = sessions.GetOrCreate(sessionId);
            lifecycle.AdvanceParticipant(state, tick);
            return capture(state);
        }
    }

    /**
     * Advances the world, then atomically captures the requested participant
     * together with every participant in stable session order.
     */
    public TResult Capture<TResult>(
        string sessionId,
        ulong tick,
        Func<TState, IReadOnlyList<TState>, TResult> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        lock (synchronization) {
            AdvanceToCore(tick);
            var state = sessions.GetOrCreate(sessionId);
            lifecycle.AdvanceParticipant(state, tick);
            return capture(state, GetOrderedStates());
        }
    }

    public void AdvanceTo(ulong tick)
    {
        lock (synchronization)
            AdvanceToCore(tick);
    }

    public IReadOnlyList<TEvent> DrainEvents(string sessionId)
    {
        lock (synchronization)
            return sessions.DrainEvents(sessionId);
    }

    private void AdvanceToCore(ulong tick)
    {
        if (tick <= lastAdvancedTick)
            return;

        var participants = GetOrderedStates();
        foreach (var participant in participants)
            lifecycle.AdvanceParticipant(participant, tick);
        for (var simulationTick = lastAdvancedTick + 1; ; simulationTick++) {
            var deliveries = lifecycle.AdvanceTick(simulationTick, participants)
                ?? throw new InvalidOperationException(
                    "The simulation lifecycle returned a null event collection.");
            foreach (var delivery in deliveries)
                sessions.EnqueueEvent(delivery.Recipient, delivery.Event);
            // Stop before incrementing the final ulong tick; wrapping to zero
            // would replay indefinitely after a near-limit cohort restore.
            if (simulationTick == tick)
                break;
        }
        lastAdvancedTick = tick;
    }

    private IReadOnlyList<TState> GetOrderedStates()
    {
        return sessions.GetOrderedSessions()
            .Select(session => session.State)
            .ToArray();
    }
}
