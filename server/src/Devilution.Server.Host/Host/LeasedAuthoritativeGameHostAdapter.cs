using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;

namespace Devilution.Server.Host;

/**
 * Routes each host operation through one leased ruleset generation. A reload
 * may publish a new adapter between operations, but cannot retire the adapter
 * used by an operation that is already in flight.
 */
public sealed class LeasedAuthoritativeGameHostAdapter
    : IAuthoritativeGameHostAdapter, IAuthoritativeGameCohortAdapter, IDisposable
{
    private readonly GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> rulesets;
    private readonly Action<IAuthoritativeGameHostAdapter> disposeAdapter;
    private readonly ReaderWriterLockSlim operationBarrier = new();
    private int disposed;

    public LeasedAuthoritativeGameHostAdapter(
        GameplayRulesetRuntime<IAuthoritativeGameHostAdapter> rulesets,
        Action<IAuthoritativeGameHostAdapter>? disposeAdapter = null)
    {
        this.rulesets = rulesets ?? throw new ArgumentNullException(nameof(rulesets));
        this.disposeAdapter = disposeAdapter ?? DisposeAdapter;
    }

    /** Identity hash of the generation that will serve the next operation. */
    public string ActiveRulesetIdentityHash => rulesets.Active.Identity.CombinedSha256;

    /** Replaced generations still awaiting explicit disposal by the host. */
    public int RetiredRulesetCount => rulesets.RetiredActivationCount;

    public bool SupportsCohortPersistence
    {
        get {
            ThrowIfDisposed();
            return WithAdapter(adapter => adapter is IAuthoritativeGameCohortAdapter cohort
                && cohort.SupportsCohortPersistence);
        }
    }

    public string CohortModuleId
    {
        get {
            ThrowIfDisposed();
            return WithAdapter(adapter => RequireCohortAdapter(adapter).CohortModuleId);
        }
    }

    public int CohortFormatVersion
    {
        get {
            ThrowIfDisposed();
            return WithAdapter(adapter => RequireCohortAdapter(adapter).CohortFormatVersion);
        }
    }

    public AuthoritativeModuleCohortState CaptureCohortState()
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => RequireCohortAdapter(adapter).CaptureCohortState());
    }

    public bool IsEntityIdOccupied(uint entityId)
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => RequireCohortAdapter(adapter).IsEntityIdOccupied(entityId));
    }

    public IAuthoritativePreparedCohortRestore PrepareCohortRestore(
        AuthoritativeModuleCohortState state)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(state);
        operationBarrier.EnterReadLock();
        try {
            using var lease = rulesets.Acquire();
            var prepared = RequireCohortAdapter(lease.Activation.Target).PrepareCohortRestore(state);
            try {
                return new PreparedCohortRestore(this, lease.Activation, prepared);
            } catch (Exception failure) {
                try { prepared.Dispose(); }
                catch (Exception cleanup) { PreserveCleanupFailure(failure, cleanup, "prepared-wrapper"); }
                throw;
            }
        } finally {
            operationBarrier.ExitReadLock();
        }
    }

    public void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(prepared);
        operationBarrier.EnterWriteLock();
        try {
            if (prepared is not PreparedCohortRestore owned)
                throw new InvalidOperationException("The prepared cohort is not owned by this leased adapter.");
            owned.Install(this, rulesets.Active);
        } finally {
            operationBarrier.ExitWriteLock();
        }
    }

    /**
     * Restores one detached complete graph into the unexposed candidate, then
     * lets the host publish its new identity before Engine activation. The host
     * owns durable-outcome checks and failure fencing; legacy migration below
     * remains unchanged. Lock order is command monitor, lease barrier, runtime.
     */
    public GameplayRulesetReloadResult<IAuthoritativeGameHostAdapter> TryReloadCohort(
        ContentManifest candidateManifest,
        IEnumerable<IGameplayModule> candidateModules,
        Action<GameplayRulesetActivation<IAuthoritativeGameHostAdapter>> prepareCandidate)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(candidateManifest);
        ArgumentNullException.ThrowIfNull(candidateModules);
        ArgumentNullException.ThrowIfNull(prepareCandidate);
        operationBarrier.EnterWriteLock();
        try {
            AuthoritativeModuleCohortState source;
            try {
                source = RequireCohortAdapter(rulesets.Active.Target).CaptureCohortState();
            } catch (Exception exception) {
                return FailedReload("ruleset-cohort-export-failed", exception);
            }
            return rulesets.TryReloadWithActivation(
                candidateManifest,
                candidateModules,
                candidate => {
                    IAuthoritativePreparedCohortRestore? prepared = null;
                    Exception? primaryFailure = null;
                    try {
                        try {
                            var cohort = RequireCohortAdapter(candidate.Target);
                            prepared = cohort.PrepareCohortRestore(source);
                            cohort.CommitCohortRestore(prepared);
                            prepareCandidate(candidate);
                        } catch (Exception failure) {
                            primaryFailure = failure;
                            throw;
                        } finally {
                            try { prepared?.Dispose(); }
                            catch (Exception cleanup) when (primaryFailure is not null) {
                                PreserveCleanupFailure(primaryFailure, cleanup, "prepared-candidate");
                            }
                        }
                    } catch (Exception failure) {
                        try { disposeAdapter(candidate.Target); }
                        catch (Exception cleanup) { PreserveCleanupFailure(failure, cleanup, "candidate-adapter"); }
                        throw;
                    }
                });
        } finally {
            operationBarrier.ExitWriteLock();
        }
    }

    /**
     * Replaces the active ruleset after migrating every named session through
     * the existing save contract. The write barrier prevents host operations
     * from observing either half of the migration.
     */
    public GameplayRulesetReloadResult<IAuthoritativeGameHostAdapter> TryReload(
        ContentManifest candidateManifest,
        IEnumerable<IGameplayModule> candidateModules,
        IEnumerable<string> sessionIds,
        Action<GameplayRulesetActivation<IAuthoritativeGameHostAdapter>>? prepareCandidate = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(candidateManifest);
        ArgumentNullException.ThrowIfNull(candidateModules);
        ArgumentNullException.ThrowIfNull(sessionIds);
        var sessions = sessionIds
            .Select(sessionId => sessionId?.Trim() ?? string.Empty)
            .ToArray();
        if (sessions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Ruleset migration session IDs must not be blank.",
                nameof(sessionIds));
        sessions = sessions
            .Distinct(StringComparer.Ordinal)
            .OrderBy(sessionId => sessionId, StringComparer.Ordinal)
            .ToArray();

        operationBarrier.EnterWriteLock();
        try {
            var previous = rulesets.Active;
            Dictionary<string, string> saves;
            try {
                saves = sessions.ToDictionary(
                    sessionId => sessionId,
                    sessionId => previous.Target.ExportSessionState(sessionId),
                    StringComparer.Ordinal);
            } catch (Exception exception) {
                return FailedReload("ruleset-session-export-failed", exception);
            }

            return rulesets.TryReloadWithActivation(
                candidateManifest,
                candidateModules,
                candidate => {
                    try {
                        foreach (var sessionId in sessions)
                            candidate.Target.ImportSessionState(sessionId, saves[sessionId]);
                        prepareCandidate?.Invoke(candidate);
                    } catch {
                        disposeAdapter(candidate.Target);
                        throw;
                    }
                });
        } finally {
            operationBarrier.ExitWriteLock();
        }
    }

    /** Disposes replaced adapters once the Engine reports them as releasable. */
    public int DisposeRetiredAdapters()
    {
        ThrowIfDisposed();
        operationBarrier.EnterWriteLock();
        try {
            return DisposeRetiredAdaptersCore();
        } finally {
            operationBarrier.ExitWriteLock();
        }
    }

    public CommandExecutionResult Execute(
        string sessionId,
        Command command,
        ulong appliedTick)
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => adapter.Execute(sessionId, command, appliedTick));
    }

    public void AdvanceTo(ulong tick)
    {
        ThrowIfDisposed();
        WithAdapter(adapter => adapter.AdvanceTo(tick));
    }

    public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => adapter.CreateSnapshot(sessionId, entityId, tick));
    }

    public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => adapter.DrainEvents(sessionId, entityId, tick));
    }

    public string ExportPlayerSave(string sessionId)
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => adapter.ExportPlayerSave(sessionId));
    }

    public string ExportSessionState(string sessionId)
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => adapter.ExportSessionState(sessionId));
    }

    public void ImportPlayerSave(string sessionId, string serializedSave)
    {
        ThrowIfDisposed();
        WithAdapter(adapter => adapter.ImportPlayerSave(sessionId, serializedSave));
    }

    public void ImportSessionState(string sessionId, string serializedState)
    {
        ThrowIfDisposed();
        WithAdapter(adapter => adapter.ImportSessionState(sessionId, serializedState));
    }

    public ulong GetSessionStateTick(string sessionId)
    {
        ThrowIfDisposed();
        return WithAdapter(adapter => adapter.GetSessionStateTick(sessionId));
    }

    public void InitializeParticipant(
        string sessionId,
        AuthoritativeParticipantSelection selection)
    {
        ThrowIfDisposed();
        WithAdapter(adapter => adapter.InitializeParticipant(sessionId, selection));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        operationBarrier.EnterWriteLock();
        try {
            Exception? primaryFailure = null;
            try { DisposeRetiredAdaptersCore(); }
            catch (Exception failure) { primaryFailure = failure; }
            try { disposeAdapter(rulesets.Active.Target); }
            catch (Exception cleanup) {
                if (primaryFailure is null)
                    primaryFailure = cleanup;
                else
                    PreserveCleanupFailure(primaryFailure, cleanup, "active-adapter");
            }
            if (primaryFailure is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        } finally {
            operationBarrier.ExitWriteLock();
            operationBarrier.Dispose();
        }
    }

    private TResult WithAdapter<TResult>(Func<IAuthoritativeGameHostAdapter, TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        operationBarrier.EnterReadLock();
        try {
            using var lease = rulesets.Acquire();
            return operation(lease.Activation.Target);
        } finally {
            operationBarrier.ExitReadLock();
        }
    }

    private void WithAdapter(Action<IAuthoritativeGameHostAdapter> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        operationBarrier.EnterReadLock();
        try {
            using var lease = rulesets.Acquire();
            operation(lease.Activation.Target);
        } finally {
            operationBarrier.ExitReadLock();
        }
    }

    private int DisposeRetiredAdaptersCore()
    {
        var retired = rulesets.DrainRetired();
        Exception? primaryFailure = null;
        for (var index = 0; index < retired.Count; index++) {
            try { disposeAdapter(retired[index].Target); }
            catch (Exception cleanup) {
                if (primaryFailure is null)
                    primaryFailure = cleanup;
                else
                    PreserveCleanupFailure(primaryFailure, cleanup, $"retired-adapter-{index}");
            }
        }
        // Draining transfers cleanup ownership here. One adapter failure must
        // not abandon the remaining targets or replace the first diagnostic.
        if (primaryFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        return retired.Count;
    }

    private GameplayRulesetReloadResult<IAuthoritativeGameHostAdapter> FailedReload(
        string code,
        Exception exception)
    {
        return new GameplayRulesetReloadResult<IAuthoritativeGameHostAdapter>(
            false,
            rulesets.Active,
            [new GameplayRulesetActivationDiagnostic(code, exception.Message)]);
    }

    private static void DisposeAdapter(IAuthoritativeGameHostAdapter adapter)
    {
        if (adapter is IDisposable disposable)
            disposable.Dispose();
    }

    private static IAuthoritativeGameCohortAdapter RequireCohortAdapter(
        IAuthoritativeGameHostAdapter adapter)
        => adapter is IAuthoritativeGameCohortAdapter cohort && cohort.SupportsCohortPersistence
            ? cohort
            : throw new NotSupportedException("The active game adapter does not support complete-cohort recovery.");

    private static void PreserveCleanupFailure(Exception primary, Exception cleanup, string stage)
    {
        try { primary.Data[$"cohort-cleanup-{stage}"] = cleanup; }
        catch { /* Diagnostic storage cannot replace the original failure. */ }
    }

    private static AuthoritativeModuleCohortState DetachCohortMetadata(AuthoritativeModuleCohortState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Participants is null || source.Participants.Any(participant => participant is null))
            throw new InvalidDataException("The prepared cohort contains a null binding.");
        return source with {
            Participants = Array.AsReadOnly(source.Participants
                .Select(participant => new AuthoritativeModuleCohortParticipant(participant.SessionId, participant.EntityId))
                .ToArray()),
        };
    }

    private sealed class PreparedCohortRestore(
        LeasedAuthoritativeGameHostAdapter owner,
        GameplayRulesetActivation<IAuthoritativeGameHostAdapter> activation,
        IAuthoritativePreparedCohortRestore inner) : IAuthoritativePreparedCohortRestore
    {
        private readonly object synchronization = new();
        private bool consumed;
        private bool disposed;

        public AuthoritativeModuleCohortState CohortState { get; } = DetachCohortMetadata(inner.CohortState);

        public void Install(
            LeasedAuthoritativeGameHostAdapter requestedOwner,
            GameplayRulesetActivation<IAuthoritativeGameHostAdapter> currentActivation)
        {
            lock (synchronization) {
                if (!ReferenceEquals(owner, requestedOwner)
                    || !ReferenceEquals(activation, currentActivation) || disposed)
                    throw new InvalidOperationException("The prepared cohort belongs to a different or retired generation.");
                if (consumed)
                    throw new InvalidOperationException("The prepared cohort can only be installed once.");
                consumed = true;
                RequireCohortAdapter(currentActivation.Target).CommitCohortRestore(inner);
            }
        }

        public void Dispose()
        {
            lock (synchronization) {
                if (disposed)
                    return;
                disposed = true;
                inner.Dispose();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
    }
}
