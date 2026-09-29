using Devilution.Protocol.V1;
using Devilution.Server.Host;

namespace Devilution.Server.Stores;

/** Publishes the combined checkpoint before legacy compatibility mirrors. */
public sealed class AuthoritativeSessionPersistence
{
    private readonly AuthoritativeSaveStore? saveStore;
    private readonly AuthoritativeCommandReceiptStore? commandReceiptStore;
    private readonly AuthoritativeSessionStore? sessionStore;

    public AuthoritativeSessionPersistence(
        AuthoritativeSaveStore? saveStore = null,
        AuthoritativeCommandReceiptStore? commandReceiptStore = null,
        AuthoritativeSessionStore? sessionStore = null)
    {
        this.saveStore = saveStore;
        this.commandReceiptStore = commandReceiptStore;
        this.sessionStore = sessionStore;
    }

    /** Writes the authoritative checkpoint before compatibility-only files. */
    public void Save(
        string sessionId,
        string? serializedState,
        IReadOnlyDictionary<ulong, CommandResult> commandResults,
        string? rulesetIdentityHash = null)
        => Save(sessionId, serializedState, commandResults, rulesetIdentityHash, null);

    /** Host-only timings leave the public save and batch contracts unchanged. */
    internal void Save(
        string sessionId,
        string? serializedState,
        IReadOnlyDictionary<ulong, CommandResult> commandResults,
        string? rulesetIdentityHash,
        HostPerformanceRecorder? recorder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(commandResults);

        if (sessionStore is not null && serializedState is not null) {
            var publication = recorder?.Begin() ?? 0;
            sessionStore.Save(sessionId, serializedState, commandResults, rulesetIdentityHash);
            recorder?.Complete(HostPerformanceMetric.CombinedCheckpointPublishMilliseconds, publication);
        }
        if (saveStore is not null && serializedState is not null) {
            var publication = recorder?.Begin() ?? 0;
            saveStore.Save(sessionId, serializedState);
            recorder?.Complete(HostPerformanceMetric.CompatibilityStatePublishMilliseconds, publication);
        }
        if (commandReceiptStore is not null) {
            var publication = recorder?.Begin() ?? 0;
            commandReceiptStore.Save(sessionId, commandResults);
            recorder?.Complete(HostPerformanceMetric.CompatibilityReceiptsPublishMilliseconds, publication);
        }
    }

    /**
     * Publishes a set of session checkpoints as one in-process transaction.
     * Existing combined and compatibility files are restored when any write
     * fails, so a failed multi-session reload cannot leave a partial identity
     * transition behind.
     */
    public void SaveBatch(IEnumerable<AuthoritativeSessionWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);
        var entries = writes.ToArray();
        var sessionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries) {
            ArgumentNullException.ThrowIfNull(entry);
            if (!sessionIds.Add(entry.SessionId))
                throw new ArgumentException(
                    $"The session write set contains duplicate session '{entry.SessionId}'.",
                    nameof(writes));
        }
        ValidateGlobalReceiptSequences(entries);

        var backups = entries
            .Select(entry => Capture(entry.SessionId))
            .ToArray();
        try {
            foreach (var entry in entries)
                Save(
                    entry.SessionId,
                    entry.SerializedState,
                    entry.CommandResults,
                    entry.RulesetIdentityHash);
        } catch {
            for (var index = backups.Length - 1; index >= 0; index--) {
                try {
                    Restore(backups[index]);
                } catch {
                    // Preserve the original persistence failure. The combined
                    // checkpoint remains the authoritative recovery boundary.
                }
            }
            throw;
        }
    }

    /** Rejects a batch that would publish duplicate globally ordered receipts. */
    private static void ValidateGlobalReceiptSequences(
        IReadOnlyList<AuthoritativeSessionWrite> entries)
    {
        var receiptSequences = new HashSet<ulong>();
        foreach (var entry in entries) {
            ArgumentNullException.ThrowIfNull(entry.CommandResults);
            foreach (var result in entry.CommandResults.Values) {
                ArgumentNullException.ThrowIfNull(result);
                if (result.ServerReceiptSequence == 0
                    || !receiptSequences.Add(result.ServerReceiptSequence)) {
                    throw new InvalidDataException(
                        "The session write set contains an invalid or duplicate global server receipt sequence.");
                }
            }
        }
    }

    private SessionPersistenceBackup Capture(string sessionId)
    {
        var combined = sessionStore?.Load(sessionId);
        var legacySave = saveStore?.Load(sessionId);
        var commandReceiptsExist = commandReceiptStore?.Exists(sessionId) == true;
        var commandReceipts = commandReceiptsExist
            ? commandReceiptStore!.Load(sessionId)
            : null;
        return new SessionPersistenceBackup(
            sessionId,
            combined,
            legacySave,
            commandReceiptsExist,
            commandReceipts);
    }

    private void Restore(SessionPersistenceBackup backup)
    {
        if (sessionStore is not null) {
            if (backup.Combined is null)
                sessionStore.Delete(backup.SessionId);
            else
                sessionStore.Save(
                    backup.SessionId,
                    backup.Combined.SerializedState,
                    backup.Combined.CommandReceipts.ToDictionary(
                        receipt => receipt.ClientSequence,
                        receipt => receipt),
                    backup.Combined.RulesetIdentityHash);
        }

        if (saveStore is not null) {
            if (backup.LegacySave is null)
                saveStore.Delete(backup.SessionId);
            else
                saveStore.Save(backup.SessionId, backup.LegacySave);
        }

        if (commandReceiptStore is not null) {
            if (!backup.CommandReceiptsExist)
                commandReceiptStore.Delete(backup.SessionId);
            else
                commandReceiptStore.Save(
                    backup.SessionId,
                    backup.CommandReceipts!.ToDictionary(
                        receipt => receipt.ClientSequence,
                        receipt => receipt));
        }
    }

    private sealed record SessionPersistenceBackup(
        string SessionId,
        AuthoritativeSessionDocument? Combined,
        string? LegacySave,
        bool CommandReceiptsExist,
        IReadOnlyList<CommandResult>? CommandReceipts);
}

/** One complete session checkpoint supplied to a batch persistence operation. */
public sealed record AuthoritativeSessionWrite(
    string SessionId,
    string SerializedState,
    IReadOnlyDictionary<ulong, CommandResult> CommandResults,
    string? RulesetIdentityHash = null);
