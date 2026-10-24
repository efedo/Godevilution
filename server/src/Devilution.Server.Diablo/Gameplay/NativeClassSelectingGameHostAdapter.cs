using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;

namespace Devilution.Server.Gameplay;

/**
 * Serves an all-class native profile. Every class is a separate native run (its
 * start facts bind the run fingerprint), so one simulation per class advances
 * in lockstep until a class is chosen: by the first admitted participant, or by
 * the one run that accepts a restored cohort. The rest are then discarded.
 */
public sealed class NativeClassSelectingGameHostAdapter : IAuthoritativeGameHostAdapter, IAuthoritativeGameCohortAdapter
{
    private readonly IReadOnlyDictionary<uint, StoreSimulationExecutor> runs;
    private readonly uint defaultClassId;
    private readonly object selectionLock = new();
    private StoreSimulationExecutor? selected;

    public NativeClassSelectingGameHostAdapter(IReadOnlyDictionary<uint, StoreSimulationExecutor> runs, uint defaultClassId = 0)
    {
        ArgumentNullException.ThrowIfNull(runs);
        if (runs.Count == 0 || !runs.ContainsKey(defaultClassId))
            throw new ArgumentException("An all-class adapter needs one run per class, including its default class.", nameof(runs));
        this.runs = runs;
        this.defaultClassId = defaultClassId;
    }

    /** The chosen class, or null while every class run is still open. */
    public uint? SelectedClassId
    {
        get {
            var current = Volatile.Read(ref selected);
            return current is null ? null : runs.Single(pair => ReferenceEquals(pair.Value, current)).Key;
        }
    }

    private StoreSimulationExecutor Current => Volatile.Read(ref selected) ?? runs[defaultClassId];

    private StoreSimulationExecutor Selected => Volatile.Read(ref selected)
        ?? throw new InvalidOperationException("No class has been selected for this all-class native run.");

    public AuthoritativeProtocolFeatures SupportedProtocolFeatures => Current.SupportedProtocolFeatures;

    public bool SupportsCohortPersistence => runs.Values.All(run => run.SupportsCohortPersistence);

    public string CohortModuleId => Current.CohortModuleId;

    public int CohortFormatVersion => Current.CohortFormatVersion;

    public void InitializeParticipant(string sessionId, AuthoritativeParticipantSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        lock (selectionLock) {
            if (selected is { } current) {
                current.InitializeParticipant(sessionId, selection);
                return;
            }
            var classId = selection.ArchetypeId ?? defaultClassId;
            if (!runs.TryGetValue(classId, out var run))
                throw new NotSupportedException($"The all-class native profile does not start class {classId}.");
            run.InitializeParticipant(sessionId, selection);
            Volatile.Write(ref selected, run);
        }
    }

    public void AdvanceTo(ulong tick)
    {
        if (Volatile.Read(ref selected) is { } current) {
            current.AdvanceTo(tick);
            return;
        }
        lock (selectionLock) {
            if (selected is not null) {
                selected.AdvanceTo(tick);
                return;
            }
            foreach (var run in runs.Values)
                run.AdvanceTo(tick);
        }
    }

    public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        => Selected.Execute(sessionId, command, appliedTick);

    public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        => Selected.CreateSnapshot(sessionId, entityId, tick);

    public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
        => Selected.DrainEvents(sessionId, entityId, tick);

    public string ExportPlayerSave(string sessionId) => Selected.ExportPlayerSave(sessionId);

    public void ImportPlayerSave(string sessionId, string serializedSave) => Selected.ImportPlayerSave(sessionId, serializedSave);

    public string ExportSessionState(string sessionId) => Selected.ExportSessionState(sessionId);

    public void ImportSessionState(string sessionId, string serializedState) => Selected.ImportSessionState(sessionId, serializedState);

    public ulong GetSessionStateTick(string sessionId) => Selected.GetSessionStateTick(sessionId);

    public bool IsEntityIdOccupied(uint entityId)
        => Volatile.Read(ref selected) is { } current
            ? current.IsEntityIdOccupied(entityId)
            : runs.Values.Any(run => run.IsEntityIdOccupied(entityId));

    public AuthoritativeModuleCohortState CaptureCohortState() => Current.CaptureCohortState();

    /**
     * A cohort with participants belongs to the one class run that validates it;
     * an empty cohort is restored into every open run.
     */
    public IAuthoritativePreparedCohortRestore PrepareCohortRestore(AuthoritativeModuleCohortState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (selectionLock) {
            if (selected is { } current)
                return new PreparedRestore(this, new Dictionary<uint, IAuthoritativePreparedCohortRestore> {
                    [SelectedClassId!.Value] = current.PrepareCohortRestore(state),
                });
            var prepared = new Dictionary<uint, IAuthoritativePreparedCohortRestore>();
            var failures = new List<Exception>();
            foreach (var (classId, run) in runs) {
                try {
                    prepared.Add(classId, run.PrepareCohortRestore(state));
                } catch (Exception failure) when (failure is InvalidDataException or NotSupportedException or InvalidOperationException) {
                    failures.Add(failure);
                }
            }
            if (prepared.Count == 0 || (state.Participants.Count != 0 && prepared.Count != 1)) {
                foreach (var restore in prepared.Values)
                    restore.Dispose();
                throw prepared.Count == 0
                    ? new InvalidDataException("No class run of the all-class native profile accepts the saved cohort.",
                        new AggregateException(failures))
                    : new InvalidDataException("The saved cohort matches more than one class run of the all-class native profile.");
            }
            return new PreparedRestore(this, prepared);
        }
    }

    public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (prepared is not PreparedRestore owned || !ReferenceEquals(owned.Owner, this))
            throw new InvalidOperationException("The prepared cohort belongs to another adapter.");
        lock (selectionLock) {
            foreach (var (classId, restore) in owned.Restores)
                runs[classId].CommitCohortRestore(restore);
            if (owned.CohortState.Participants.Count != 0)
                Volatile.Write(ref selected, runs[owned.Restores.Keys.Single()]);
        }
    }

    private sealed class PreparedRestore(NativeClassSelectingGameHostAdapter owner,
        IReadOnlyDictionary<uint, IAuthoritativePreparedCohortRestore> restores) : IAuthoritativePreparedCohortRestore
    {
        public NativeClassSelectingGameHostAdapter Owner { get; } = owner;

        public IReadOnlyDictionary<uint, IAuthoritativePreparedCohortRestore> Restores { get; } = restores;

        public AuthoritativeModuleCohortState CohortState => Restores.Values.First().CohortState;

        public void Dispose()
        {
            foreach (var restore in Restores.Values)
                restore.Dispose();
        }
    }
}
