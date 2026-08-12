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
    private readonly CommandAdmissionPolicy admissionPolicy;
    private readonly IAuthoritativeCommandExecutor executor;
    private ulong nextServerReceiptSequence = 1;

    public AuthoritativeCommandServer(IAuthoritativeCommandExecutor executor, CommandAdmissionPolicy? admissionPolicy = null)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.admissionPolicy = admissionPolicy ?? new CommandAdmissionPolicy();
    }

    /** Processes a batch in wire order and returns one result per command. */
    public CommandAck ProcessBatch(string sessionId, CommandBatch batch, ulong currentTick)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var acknowledgement = new CommandAck();
        if (batch.Commands.Count == 0) {
            acknowledgement.Results.Add(CreateMalformedResult(0, currentTick));
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
            return CreateMalformedResult(command.ClientSequence, currentTick);

        lock (synchronization) {
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
            if (executor is IAuthoritativeTickExecutor tickExecutor)
                tickExecutor.AdvanceTo(currentTick);
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

        return nextServerReceiptSequence++;
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
