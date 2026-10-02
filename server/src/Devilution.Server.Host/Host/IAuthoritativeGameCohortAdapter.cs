namespace Devilution.Server.Host;

/** Stable module-owned binding, independent of connection and resume-token storage. */
public sealed record AuthoritativeModuleCohortParticipant(string SessionId, uint EntityId);

/** Opaque complete module state at one settled authoritative boundary. */
public sealed record AuthoritativeModuleCohortState(
    string ModuleId,
    int FormatVersion,
    ulong Tick,
    IReadOnlyList<AuthoritativeModuleCohortParticipant> Participants,
    string SerializedState);

/** Detached, owner-checked one-shot installation prepared without source mutation. */
public interface IAuthoritativePreparedCohortRestore : IDisposable
{
    AuthoritativeModuleCohortState CohortState { get; }
}

/**
 * Optional complete-state boundary; legacy per-session import remains separate.
 * The host holds its command barrier for capture/commit and validates outer
 * identity, command ledgers and bindings before installation or publication.
 * Prepare must not replay ordinary startup or mutate the active state. Commit
 * installs the already validated graph once into a fresh/unexposed target;
 * ordinary operations between prepare and commit invalidate installation.
 * It is not a live rollback API.
 */
public interface IAuthoritativeGameCohortAdapter
{
    bool SupportsCohortPersistence { get; }

    string CohortModuleId { get; }

    int CohortFormatVersion { get; }

    /** Live/reserved module identities that the host's exact allocator must skip. */
    bool IsEntityIdOccupied(uint entityId);

    AuthoritativeModuleCohortState CaptureCohortState();

    IAuthoritativePreparedCohortRestore PrepareCohortRestore(
        AuthoritativeModuleCohortState state);

    void CommitCohortRestore(IAuthoritativePreparedCohortRestore prepared);
}
