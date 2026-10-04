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
 * A module-owned command slot within each participant's autonomous tick. The
 * synchronous callback must run once per participant, in supplied order, and
 * its returned events must be merged at that slot into the tick's event stream.
 */
public interface IAuthoritativeSimulationTickLifecycle<TState, TEvent>
    : IAuthoritativeSimulationLifecycle<TState, TEvent>
    where TState : class
{
    IReadOnlyList<DirectedSimulationEvent<TState, TEvent>> AdvanceTickWithCommands(
        ulong tick,
        IReadOnlyList<TState> participants,
        Func<TState, IReadOnlyList<DirectedSimulationEvent<TState, TEvent>>> executeCommands);
}

/** One operation for an actor's tick slot; semantic deduplication belongs to the host. */
public sealed record SimulationTickCommand<TState, TEvent, TResult>(
    string SessionId,
    Func<TState, IReadOnlyList<TState>, SimulationStepResult<TResult, TEvent>> Operation)
    where TState : class;

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
    private bool tickExecutionOpen;
    private bool tickExecutionFaulted;

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
            EnsureAvailable();
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
        lock (synchronization) {
            EnsureAvailable();
            return capture(lastAdvancedTick, sessions.GetOrderedSessions());
        }
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
            EnsureAvailable();
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
        lock (synchronization) {
            EnsureAvailable();
            sessions.Add(sessionId, state);
        }
    }

    /** Registers module-created state only when no state exists for the session. */
    public bool TryAddParticipant(string sessionId, TState state)
    {
        lock (synchronization) {
            EnsureAvailable();
            return sessions.TryAdd(sessionId, state);
        }
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
            EnsureAvailable();
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
            EnsureAvailable();
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

    /**
     * Executes a still-open tick, catching up through tick-1 before admitting
     * new actors. Actors execute in ordinal session order; each actor's commands
     * retain input order and results retain the original whole-batch order.
     * Tick zero and already closed ticks cannot accept commands. Module/operation
     * faults after mutable hooks begin poison this runtime, not roll back module
     * state or RNG: rebuild from a known checkpoint instead of retrying/capturing.
     * Existing Execute/AdvanceTo semantics remain unchanged outside this API.
     */
    public IReadOnlyList<TResult> ExecuteTick<TResult>(
        ulong tick,
        IReadOnlyList<SimulationTickCommand<TState, TEvent, TResult>> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        lock (synchronization) {
            EnsureAvailable();
            if (tick == 0 || tick <= lastAdvancedTick)
                throw new InvalidOperationException("Commands require a still-open tick after initialization.");
            if (lifecycle is not IAuthoritativeSimulationTickLifecycle<TState, TEvent> tickLifecycle)
                throw new InvalidOperationException("The simulation lifecycle has no ordered command tick slot.");

            // Detach and validate every input before catch-up or factory hooks.
            var detached = commands.ToArray();
            foreach (var command in detached) {
                ArgumentNullException.ThrowIfNull(command);
                ArgumentException.ThrowIfNullOrWhiteSpace(command.SessionId);
                ArgumentNullException.ThrowIfNull(command.Operation);
            }
            var commandIndices = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var commandSessions = new List<string>();
            for (var index = 0; index < detached.Length; index++) {
                var id = detached[index].SessionId;
                if (!commandIndices.TryGetValue(id, out var indices)) {
                    commandIndices.Add(id, indices = []);
                    commandSessions.Add(id);
                }
                indices.Add(index);
            }
            var results = new TResult[detached.Length];
            var effectsStarted = false;
            tickExecutionOpen = true;
            try {
                if (tick - 1 > lastAdvancedTick) {
                    effectsStarted = true;
                    AdvanceToCore(tick - 1);
                    EnsureTickHealthy();
                }

                var fresh = new List<SimulationSession<TState>>();
                foreach (var id in commandSessions) {
                    if (sessions.TryGet(id, out _))
                        continue;
                    effectsStarted = true;
                    var state = lifecycle.CreateParticipant(id)
                        ?? throw new InvalidOperationException("The simulation state factory returned null.");
                    EnsureTickHealthy();
                    fresh.Add(new(id, state));
                }
                sessions.AddRange(fresh);
                var participants = Array.AsReadOnly(GetOrderedStates().ToArray());
                foreach (var participant in participants) {
                    effectsStarted = true;
                    lifecycle.AdvanceParticipant(participant, tick);
                    EnsureTickHealthy();
                }

                var executionThread = Environment.CurrentManagedThreadId;
                // 0=closed, 1=open, 2=violated. Closing atomically observes a
                // cross-thread attempt even if the module catches its exception.
                var callbackScope = 1;
                var nextParticipant = 0;
                var callbackRunning = false;
                IReadOnlyList<DirectedSimulationEvent<TState, TEvent>> ExecuteCommands(TState participant)
                {
                    if (Environment.CurrentManagedThreadId != executionThread) {
                        if (Interlocked.CompareExchange(ref callbackScope, 2, 1) == 0)
                            throw new InvalidOperationException("The command tick callback has expired.");
                        throw new InvalidOperationException("The command tick callback must remain synchronous on its execution thread.");
                    }
                    if (Volatile.Read(ref callbackScope) == 0)
                        throw new InvalidOperationException("The command tick callback has expired.");
                    try {
                        if (tickExecutionFaulted || Volatile.Read(ref callbackScope) != 1 || callbackRunning ||
                            nextParticipant >= participants.Count ||
                            !ReferenceEquals(participants[nextParticipant], participant))
                            throw new InvalidOperationException("Command tick callbacks must visit each ordered participant exactly once.");
                        callbackRunning = true;
                        nextParticipant++;
                        var events = new List<DirectedSimulationEvent<TState, TEvent>>();
                        var id = sessions.GetRequiredSessionId(participant);
                        if (commandIndices.TryGetValue(id, out var indices)) {
                            foreach (var index in indices) {
                                var result = detached[index].Operation(participant, participants);
                                if (result.Events is null)
                                    throw new InvalidOperationException("A simulation operation returned a null event collection.");
                                results[index] = result.Result;
                                foreach (var simulationEvent in result.Events)
                                    events.Add(new(participant, simulationEvent));
                                if (Volatile.Read(ref callbackScope) != 1)
                                    throw new InvalidOperationException("The command tick callback contract was violated.");
                                EnsureTickHealthy();
                            }
                        }
                        return events.AsReadOnly();
                    } catch {
                        tickExecutionFaulted = true;
                        Interlocked.CompareExchange(ref callbackScope, 2, 1);
                        throw;
                    } finally {
                        callbackRunning = false;
                    }
                }

                IReadOnlyList<DirectedSimulationEvent<TState, TEvent>> deliveries;
                effectsStarted = true;
                try {
                    deliveries = tickLifecycle.AdvanceTickWithCommands(tick, participants, ExecuteCommands)
                        ?? throw new InvalidOperationException("The simulation lifecycle returned a null event collection.");
                } finally {
                    if (Interlocked.Exchange(ref callbackScope, 0) == 2)
                        tickExecutionFaulted = true;
                }
                EnsureTickHealthy();
                if (nextParticipant != participants.Count)
                    throw new InvalidOperationException("The command tick lifecycle omitted participant callbacks.");

                // Validate the whole returned stream before publishing any event.
                var detachedDeliveries = deliveries.ToArray();
                foreach (var delivery in detachedDeliveries)
                    sessions.GetRequiredSessionId(delivery.Recipient);
                EnsureTickHealthy();
                foreach (var delivery in detachedDeliveries)
                    sessions.EnqueueEvent(delivery.Recipient, delivery.Event);
                lastAdvancedTick = tick;
                return Array.AsReadOnly(results);
            } catch {
                if (effectsStarted)
                    tickExecutionFaulted = true;
                throw;
            } finally {
                tickExecutionOpen = false;
            }
        }
    }

    public TResult Read<TResult>(string sessionId, Func<TState, TResult> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        lock (synchronization) {
            EnsureAvailable();
            return read(sessions.GetOrCreate(sessionId));
        }
    }

    public void Write(string sessionId, Action<TState> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        lock (synchronization) {
            EnsureAvailable();
            write(sessions.GetOrCreate(sessionId));
        }
    }

    /** Advances existing participants before capturing the requested one. */
    public TResult Capture<TResult>(
        string sessionId,
        ulong tick,
        Func<TState, TResult> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        lock (synchronization) {
            EnsureAvailable();
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
            EnsureAvailable();
            AdvanceToCore(tick);
            var state = sessions.GetOrCreate(sessionId);
            lifecycle.AdvanceParticipant(state, tick);
            return capture(state, GetOrderedStates());
        }
    }

    public void AdvanceTo(ulong tick)
    {
        lock (synchronization) {
            EnsureAvailable();
            AdvanceToCore(tick);
        }
    }

    public IReadOnlyList<TEvent> DrainEvents(string sessionId)
    {
        lock (synchronization) {
            EnsureAvailable();
            return sessions.DrainEvents(sessionId);
        }
    }

    private void EnsureAvailable()
    {
        if (tickExecutionOpen) {
            tickExecutionFaulted = true;
            throw new InvalidOperationException("Runtime access cannot reenter an ordered command tick.");
        }
        EnsureTickHealthy();
    }

    private void EnsureTickHealthy()
    {
        if (tickExecutionFaulted)
            throw new InvalidOperationException("The ordered command tick faulted; rebuild this runtime from a known checkpoint.");
    }

    private void AdvanceToCore(ulong tick)
    {
        if (tick <= lastAdvancedTick)
            return;

        var participants = GetOrderedStates();
        foreach (var participant in participants) {
            lifecycle.AdvanceParticipant(participant, tick);
            if (tickExecutionOpen)
                EnsureTickHealthy();
        }
        for (var simulationTick = lastAdvancedTick + 1; ; simulationTick++) {
            var deliveries = lifecycle.AdvanceTick(simulationTick, participants)
                ?? throw new InvalidOperationException(
                    "The simulation lifecycle returned a null event collection.");
            if (tickExecutionOpen)
                EnsureTickHealthy();
            if (tickExecutionOpen) {
                deliveries = deliveries.ToArray();
                foreach (var delivery in deliveries)
                    sessions.GetRequiredSessionId(delivery.Recipient);
                EnsureTickHealthy();
            }
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
