using Devilution.Protocol.V1;

namespace Devilution.Server.Commands;

/** Detached completed ledger for one stable participant, including an empty ledger. */
public sealed record AuthoritativeSessionCommandLedger(
    string SessionId,
    IReadOnlyList<CommandResult> CommandResults);

/**
 * Exact command admission clock and receipt cursor. Zero cursor represents
 * exhaustion; omitted allocated receipts must not be inferred from
 * the maximum surviving per-session receipt. Queued unaccepted intent is not
 * represented here and remains nondurable. Direct Immediate API receipts can
 * be applied ahead of this intermediate clock; complete host publication must
 * separately settle the authoritative module/world boundary.
 */
public sealed record AuthoritativeCommandCohortState(
    ulong CurrentTick,
    ulong NextServerReceiptSequence,
    IReadOnlyList<AuthoritativeSessionCommandLedger> Sessions);
