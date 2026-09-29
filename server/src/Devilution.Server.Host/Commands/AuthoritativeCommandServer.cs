using Devilution.Protocol.V1;

namespace Devilution.Server.Commands;

/**
 * Serializes command admission, allocates global receipt sequences, and
 * deduplicates client sequences within each session.
 *
 * The server's simulation loop is expected to call this at its authoritative
 * tick boundary. The lock preserves exactly-once execution when transport
 * receive work is concurrent.
 */
public sealed class AuthoritativeCommandServer
{
    private readonly object synchronization = new();
    private readonly Dictionary<string, Dictionary<ulong, CommandResult>> completedCommandsBySession = new(StringComparer.Ordinal);
    private readonly HashSet<ulong> allocatedReceiptSequences = [];
    private readonly CommandAdmissionPolicy admissionPolicy;
    private readonly IAuthoritativeCommandExecutor executor;
    private ulong nextServerReceiptSequence = 1;
    private ulong currentTick;

    public AuthoritativeCommandServer(IAuthoritativeCommandExecutor executor, CommandAdmissionPolicy? admissionPolicy = null)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.admissionPolicy = admissionPolicy ?? new CommandAdmissionPolicy();
    }

    /** Returns the greatest authoritative tick admitted or simulated so far. */
    public ulong CurrentTick
    {
        get {
            lock (synchronization)
                return currentTick;
        }
    }

    /** Processes a batch in wire order and returns one result per command. */
    public CommandAck ProcessBatch(string sessionId, CommandBatch batch, ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var acknowledgement = new CommandAck();
        if (batch.Commands.Count == 0) {
            acknowledgement.Results.Add(CreateMalformedResult(0, Math.Max(CurrentTick, currentTick)));
            return acknowledgement;
        }

        foreach (var command in batch.Commands)
            acknowledgement.Results.Add(Process(sessionId, command, currentTick));
        return acknowledgement;
    }

    /** Processes one command or returns a duplicate acknowledgement. */
    public CommandResult Process(string sessionId, Command command, ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(sessionId) || command.ClientSequence == 0)
            return CreateMalformedResult(command.ClientSequence, Math.Max(CurrentTick, currentTick));

        lock (synchronization) {
            this.currentTick = Math.Max(this.currentTick, currentTick);
            currentTick = this.currentTick;
            if (executor is IAuthoritativeTickExecutor tickExecutor)
                tickExecutor.AdvanceTo(currentTick);
            var completedCommands = GetCompletedCommands(sessionId);
            if (completedCommands.TryGetValue(command.ClientSequence, out var priorResult))
                return CreateDuplicateResult(priorResult);

            var receiptSequence = AllocateReceiptSequence();
            var admission = admissionPolicy.Evaluate(command, currentTick);
            var result = CreateResult(command.ClientSequence, receiptSequence, admission);
            if (admission.Status != CommandStatus.Rejected) {
                var execution = executor.Execute(sessionId, command, admission.AppliedTick);
                if (!execution.Succeeded) {
                    result.Status = CommandStatus.Rejected;
                    result.RejectReason = execution.RejectReason;
                }
            }

            completedCommands.Add(command.ClientSequence, result.Clone());
            return result;
        }
    }

    /** Advances autonomous simulation without requiring a client command. */
    public void AdvanceTo(ulong currentTick)
    {
        lock (synchronization) {
            this.currentTick = Math.Max(this.currentTick, currentTick);
            if (executor is IAuthoritativeTickExecutor tickExecutor)
                tickExecutor.AdvanceTo(this.currentTick);
        }
    }

    /**
     * Runs a session persistence callback while command execution and
     * autonomous advancement are excluded. The callback may export the
     * adapter state and completed-command ledger as one consistent view.
     */
    public TResult WithSynchronizedSession<TResult>(string sessionId, Func<TResult> callback)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));
        return WithSynchronizedState(callback);
    }

    /** Serializes whole-host publication, including ruleset replacement. */
    public TResult WithSynchronizedState<TResult>(Func<TResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (synchronization)
            return callback();
    }

    /** Runs a void persistence callback under the same authoritative lock. */
    public void WithSynchronizedSession(string sessionId, Action callback)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));
        ArgumentNullException.ThrowIfNull(callback);
        lock (synchronization)
            callback();
    }

    /** Returns the per-session high-water mark for fresh-process resume. */
    public ulong LastClientSequence(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (synchronization) {
            return completedCommandsBySession.TryGetValue(sessionId, out var commands)
                && commands.Count > 0 ? commands.Keys.Max() : 0;
        }
    }

    /** Returns a defensive copy of the completed command ledger for a session. */
    public IReadOnlyDictionary<ulong, CommandResult> ExportCompletedCommands(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));
        lock (synchronization) {
            return completedCommandsBySession.TryGetValue(sessionId, out var completedCommands)
                ? completedCommands.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.Clone())
                : new Dictionary<ulong, CommandResult>();
        }
    }

    /** Hydrates an empty session ledger from durable command receipts. */
    public void ImportCompletedCommands(
        string sessionId,
        IEnumerable<CommandResult> results)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));
        ArgumentNullException.ThrowIfNull(results);
        lock (synchronization) {
            var pending = results.ToArray();
            var clientSequences = new HashSet<ulong>();
            var receiptSequences = new HashSet<ulong>();
            foreach (var result in pending) {
                ArgumentNullException.ThrowIfNull(result);
                if (result.ClientSequence == 0
                    || result.ServerReceiptSequence == 0
                    || !Enum.IsDefined(result.Status)
                    || !Enum.IsDefined(result.RejectReason)
                    || !clientSequences.Add(result.ClientSequence)
                    || !receiptSequences.Add(result.ServerReceiptSequence)
                    || allocatedReceiptSequences.Contains(result.ServerReceiptSequence)) {
                    throw new InvalidDataException("The command receipt ledger contains an invalid or duplicate result.");
                }
            }
            if (receiptSequences.Contains(ulong.MaxValue))
                throw new InvalidDataException("The command receipt ledger exhausts receipt sequence space.");

            var completedCommands = GetCompletedCommands(sessionId);
            if (completedCommands.Count != 0)
                throw new InvalidOperationException(
                    $"The command ledger for session '{sessionId}' is already initialized.");
            foreach (var result in pending) {
                completedCommands.Add(result.ClientSequence, result.Clone());
                allocatedReceiptSequences.Add(result.ServerReceiptSequence);
                if (result.ServerReceiptSequence >= nextServerReceiptSequence)
                    nextServerReceiptSequence = checked(result.ServerReceiptSequence + 1);
            }
        }
    }

    private Dictionary<ulong, CommandResult> GetCompletedCommands(string sessionId)
    {
        if (!completedCommandsBySession.TryGetValue(sessionId, out var completedCommands)) {
            completedCommands = new Dictionary<ulong, CommandResult>();
            completedCommandsBySession.Add(sessionId, completedCommands);
        }

        return completedCommands;
    }

    private ulong AllocateReceiptSequence()
    {
        if (nextServerReceiptSequence == 0)
            throw new InvalidOperationException("Server receipt sequence space is exhausted.");

        var receiptSequence = nextServerReceiptSequence++;
        if (!allocatedReceiptSequences.Add(receiptSequence))
            throw new InvalidOperationException("The server receipt sequence ledger contains a duplicate sequence.");
        return receiptSequence;
    }

    private static CommandResult CreateResult(ulong clientSequence, ulong receiptSequence, CommandAdmission admission)
    {
        return new CommandResult {
            ClientSequence = clientSequence,
            ServerReceiptSequence = receiptSequence,
            AppliedTick = admission.AppliedTick,
            Status = admission.Status,
            RejectReason = admission.RejectReason,
        };
    }

    private static CommandResult CreateDuplicateResult(CommandResult priorResult)
    {
        var duplicate = priorResult.Clone();
        duplicate.Status = CommandStatus.Duplicate;
        return duplicate;
    }

    private static CommandResult CreateMalformedResult(ulong clientSequence, ulong currentTick)
    {
        return new CommandResult {
            ClientSequence = clientSequence,
            AppliedTick = currentTick,
            Status = CommandStatus.Rejected,
            RejectReason = CommandRejectReason.Malformed,
        };
    }
}
