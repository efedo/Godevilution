using Devilution.Protocol.V1;

namespace Devilution.Protocol.CommandLog;

public sealed record CommandLogComparison(
    int MatchedCommands,
    int UnconfirmedSends,
    IReadOnlyList<string> Mismatches)
{
    public bool Matches => Mismatches.Count == 0 && MatchedCommands != 0;
}

/**
 * Checks one client's log against the server logs of the hosts it played on: sequences, payloads
 * and results. A send with no server record or receipt is unconfirmed, as a closing client leaves
 * it, and is not a mismatch.
 */
public static class CommandLogComparer
{
    private const int MaximumReportedMismatches = 32;

    public static CommandLogComparison Compare(
        IReadOnlyList<CommandLogRecord> client,
        IReadOnlyList<IReadOnlyList<CommandLogRecord>> servers)
    {
        var mismatches = new List<string>();
        if (client.Concat(servers.SelectMany(records => records))
            .Any(record => record.KindCase == CommandLogRecord.KindOneofCase.RecordsDropped))
            mismatches.Add("A log dropped records, so it cannot be compared completely.");
        // A host's receipt sequences are unique across sessions, so a receipt identifies its session.
        var receiptKeys = client.Where(record => record.KindCase == CommandLogRecord.KindOneofCase.ClientReceipt)
            .Select(record => (record.ClientReceipt.Result.ClientSequence, record.ClientReceipt.Result.ServerReceiptSequence))
            .ToHashSet();
        var executed = new Dictionary<ulong, ServerCommandRecord>();
        foreach (var records in servers) {
            var sessions = records.Where(record => record.KindCase == CommandLogRecord.KindOneofCase.ServerCommand
                    && receiptKeys.Contains((record.ServerCommand.Result.ClientSequence, record.ServerCommand.Result.ServerReceiptSequence)))
                .Select(record => record.ServerCommand.SessionId).ToHashSet(StringComparer.Ordinal);
            foreach (var record in records) {
                if (record.KindCase == CommandLogRecord.KindOneofCase.ServerCommand
                    && sessions.Contains(record.ServerCommand.SessionId)
                    && !executed.TryAdd(record.ServerCommand.Command.ClientSequence, record.ServerCommand))
                    mismatches.Add($"Sequence {record.ServerCommand.Command.ClientSequence} executed twice.");
            }
        }

        var sent = new Dictionary<ulong, List<Command>>();
        var receipts = new Dictionary<ulong, CommandResult>();
        foreach (var record in client) {
            if (record.KindCase == CommandLogRecord.KindOneofCase.ClientSent) {
                if (!sent.TryGetValue(record.ClientSent.Command.ClientSequence, out var attempts))
                    sent.Add(record.ClientSent.Command.ClientSequence, attempts = []);
                attempts.Add(record.ClientSent.Command);
            } else if (record.KindCase == CommandLogRecord.KindOneofCase.ClientReceipt) {
                receipts.TryAdd(record.ClientReceipt.Result.ClientSequence, record.ClientReceipt.Result);
            }
        }

        var matched = 0;
        foreach (var (sequence, execution) in executed) {
            if (!sent.TryGetValue(sequence, out var attempts)) {
                mismatches.Add($"Sequence {sequence} executed but the client never sent it.");
                continue;
            }
            if (!attempts.Contains(execution.Command)) {
                mismatches.Add($"Sequence {sequence} executed with a payload the client did not send.");
                continue;
            }
            if (receipts.TryGetValue(sequence, out var receipt) && !SameOutcome(receipt, execution.Result)) {
                mismatches.Add($"Sequence {sequence} receipt {receipt} differs from server result {execution.Result}.");
                continue;
            }
            matched++;
        }
        foreach (var sequence in receipts.Keys.Where(sequence => !executed.ContainsKey(sequence)))
            mismatches.Add($"Sequence {sequence} has a receipt but no server record.");
        var unconfirmed = sent.Keys.Count(sequence => !executed.ContainsKey(sequence) && !receipts.ContainsKey(sequence));
        if (mismatches.Count > MaximumReportedMismatches)
            mismatches.RemoveRange(MaximumReportedMismatches, mismatches.Count - MaximumReportedMismatches);
        return new CommandLogComparison(matched, unconfirmed, mismatches);
    }

    /** A retry's receipt is marked Duplicate and otherwise repeats the original outcome. */
    private static bool SameOutcome(CommandResult receipt, CommandResult execution)
        => receipt.ServerReceiptSequence == execution.ServerReceiptSequence
            && receipt.AppliedTick == execution.AppliedTick
            && receipt.RejectReason == execution.RejectReason
            && (receipt.Status == execution.Status || receipt.Status == CommandStatus.Duplicate);
}
