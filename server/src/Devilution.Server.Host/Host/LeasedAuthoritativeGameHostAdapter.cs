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
public sealed class LeasedAuthoritativeGameHostAdapter : IAuthoritativeGameHostAdapter, IDisposable
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
            DisposeRetiredAdaptersCore();
            disposeAdapter(rulesets.Active.Target);
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
        foreach (var activation in retired)
            disposeAdapter(activation.Target);
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
    }
}
