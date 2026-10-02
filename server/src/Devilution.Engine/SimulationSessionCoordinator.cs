namespace Devilution.Engine;

/** One caller-named participant and its module-owned simulation state. */
public sealed record SimulationSession<TState>(string Id, TState State)
    where TState : class;

/**
 * Coordinates caller-named simulation participants without depending on a
 * transport, protocol, or game module. Callers serialize state transitions;
 * this type provides deterministic ordering and transient-event ownership.
 */
public sealed class SimulationSessionCoordinator<TState, TEvent>
    where TState : class
{
    private readonly Func<string, TState> stateFactory;
    private readonly Dictionary<string, TState> states = new(StringComparer.Ordinal);
    private readonly Dictionary<TState, string> sessionIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, List<TEvent>> pendingEvents = new(StringComparer.Ordinal);

    public SimulationSessionCoordinator(Func<string, TState> stateFactory)
    {
        this.stateFactory = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
    }

    public int Count => states.Count;

    public TState GetOrCreate(string sessionId)
    {
        ValidateSessionId(sessionId);
        if (states.TryGetValue(sessionId, out var existing))
            return existing;

        var state = stateFactory(sessionId)
            ?? throw new InvalidOperationException("The simulation state factory returned null.");
        Add(sessionId, state);
        return state;
    }

    /** Adds explicitly constructed module state for a new participant. */
    public void Add(string sessionId, TState state)
    {
        ValidateSessionId(sessionId);
        ArgumentNullException.ThrowIfNull(state);
        if (!TryAdd(sessionId, state)) {
            throw new InvalidOperationException(
                $"Session '{sessionId}' already owns simulation state.");
        }
    }

    /** Adds state only when the named participant has not yet been registered. */
    public bool TryAdd(string sessionId, TState state)
    {
        ValidateSessionId(sessionId);
        ArgumentNullException.ThrowIfNull(state);
        if (states.ContainsKey(sessionId))
            return false;
        if (sessionIds.ContainsKey(state)) {
            throw new InvalidOperationException(
                "The simulation state is already owned by another session.");
        }

        states.Add(sessionId, state);
        sessionIds.Add(state, sessionId);
        return true;
    }

    /**
     * Validates the entire binding set before registering any caller-created
     * state. Validation never invokes the fallback state factory and compares
     * state ownership by reference, not module-defined value equality.
     */
    public void AddRange(IEnumerable<SimulationSession<TState>> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var entries = participants.ToArray();
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        var candidateStates = new HashSet<TState>(ReferenceEqualityComparer.Instance);
        foreach (var entry in entries) {
            ArgumentNullException.ThrowIfNull(entry);
            ValidateSessionId(entry.Id);
            ArgumentNullException.ThrowIfNull(entry.State);
            if (states.ContainsKey(entry.Id) || !candidateIds.Add(entry.Id))
                throw new InvalidOperationException(
                    $"Session '{entry.Id}' already owns simulation state.");
            if (sessionIds.ContainsKey(entry.State) || !candidateStates.Add(entry.State))
                throw new InvalidOperationException(
                    "The simulation state is already owned by another session.");
        }
        foreach (var entry in entries)
            Add(entry.Id, entry.State);
    }

    /** Finds state without implicitly creating a participant. */
    public bool TryGet(string sessionId, out TState? state)
    {
        ValidateSessionId(sessionId);
        return states.TryGetValue(sessionId, out state);
    }

    public IReadOnlyList<SimulationSession<TState>> GetOrderedSessions()
    {
        return states
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new SimulationSession<TState>(entry.Key, entry.Value))
            .ToArray();
    }

    public bool TryGetSessionId(TState state, out string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return sessionIds.TryGetValue(state, out sessionId);
    }

    public string GetRequiredSessionId(TState state)
    {
        return TryGetSessionId(state, out var sessionId)
            ? sessionId!
            : throw new InvalidOperationException(
                "The simulation state is not owned by this session coordinator.");
    }

    public void EnqueueEvent(string sessionId, TEvent simulationEvent)
    {
        ValidateSessionId(sessionId);
        if (!states.ContainsKey(sessionId)) {
            throw new InvalidOperationException(
                $"Session '{sessionId}' does not own simulation state.");
        }

        if (!pendingEvents.TryGetValue(sessionId, out var events)) {
            events = [];
            pendingEvents.Add(sessionId, events);
        }
        events.Add(simulationEvent);
    }

    public void EnqueueEvent(TState state, TEvent simulationEvent)
    {
        EnqueueEvent(GetRequiredSessionId(state), simulationEvent);
    }

    public IReadOnlyList<TEvent> DrainEvents(string sessionId)
    {
        ValidateSessionId(sessionId);
        if (!pendingEvents.Remove(sessionId, out var events))
            return [];
        return events.ToArray();
    }

    /** Discards transient events that are not represented in persisted state. */
    public void ClearEvents()
    {
        pendingEvents.Clear();
    }

    private static void ValidateSessionId(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
    }
}
