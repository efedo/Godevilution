using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** A drained transport ledger can precede main-thread admission of its receipt and snapshot. */
internal static class AcceptanceCommandBoundary
{
    // Callers retain their success/rejection rules and player-settlement checks.
    internal static bool HasLatestAppliedReceipt(ulong snapshotTick, CommandResult? receipt, ulong lastQueuedSequence)
        => receipt is {ClientSequence: > 0} && receipt.ClientSequence == lastQueuedSequence
            && snapshotTick >= receipt.AppliedTick;
}
