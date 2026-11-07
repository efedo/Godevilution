using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** A drained transport ledger can precede main-thread admission of its receipt and snapshot. */
internal static class AcceptanceCommandBoundary
{
    // Callers retain their success/rejection rules and player-settlement checks.
    internal static bool HasLatestAppliedReceipt(ulong snapshotTick, CommandResult? receipt, ulong lastQueuedSequence)
        => receipt is {ClientSequence: > 0} && receipt.ClientSequence == lastQueuedSequence
            && snapshotTick >= receipt.AppliedTick;

    /** Real potion input can admit another command; plan or operate only from its settled snapshot. */
    internal static async Task PrepareObjectAsync(Func<Task> waitForBoundary, Func<Task> heal)
    {
        await waitForBoundary();
        await heal();
        await waitForBoundary();
    }

    internal static void RequireAlive(Observation state, string context)
    {
        if (state.Player is { Life: <= 0 })
            throw new InvalidOperationException($"{context}: the character died; {state.Describe()}.");
    }

    /** A rejected movement can precede death; it must not wait forever for Stand. */
    internal static bool CommandSettled(Observation state, ulong sequence, string context)
    {
        RequireAlive(state, context);
        return state.LastQueuedSequence == sequence && state.CommandsAdmitted && state.PlayerSettled;
    }

    /** Keeps command admission separate from animation settlement: native quick-use can run during an action. */
    internal readonly record struct Observation(Snapshot? Snapshot, CommandResult? Receipt, ulong LastQueuedSequence, int PendingCount)
    {
        internal PlayerSnapshot? Player => Snapshot?.Players.Count == 1 ? Snapshot.Players[0] : null;
        internal bool CommandsAdmitted => Snapshot is not null && PendingCount == 0
            && (LastQueuedSequence == 0 ? Receipt is null : HasLatestAppliedReceipt(Snapshot.Tick, Receipt, LastQueuedSequence));
        internal bool PlayerSettled => Player is { } player && AuthoritativePlayerSettlement.IsSettled(player, PendingCount);
        internal string Describe()
            => $"queued={LastQueuedSequence}, receipt={Receipt?.ClientSequence}/{Receipt?.AppliedTick}/{Receipt?.Status}/{Receipt?.RejectReason}, "
                + $"snapshotTick={Snapshot?.Tick}, pending={PendingCount}, level={Player?.LevelId}, "
                + $"position=({Player?.PositionX},{Player?.PositionY}), life={Player?.Life}, mode={Player?.CombatMode}, "
                + $"action={Player?.CurrentAction?.Kind}, walkPending={Player?.NativeWalkPathPending}";
    }

    /** Cancellation can finish on the route trigger while its real UI input is being revealed. */
    internal static async Task<bool> ArrivedAfterTravelCancellationAsync(Func<Task> cancelTravel,
        Func<Task> waitForBoundary, Func<Observation> observe, uint destinationLevel)
    {
        await cancelTravel();
        await waitForBoundary();
        return observe().Player?.LevelId == destinationLevel;
    }

    /** Polls the live boundary; the caller supplies the existing tick/wall budget and real frame pump. */
    internal static async Task WaitAsync(Func<Observation> observe, Func<Task> nextFrame,
        Func<Observation, bool> expired, string context, bool settlePlayer = true)
    {
        while (true) {
            var state = observe();
            RequireAlive(state, context);
            if (state.PendingCount < 0)
                throw new InvalidOperationException($"{context} has an invalid player boundary; {state.Describe()}.");
            if (state.CommandsAdmitted && (!settlePlayer || state.PlayerSettled))
                return;
            if (expired(state))
                throw new InvalidOperationException($"{context} timed out waiting for the client command boundary to settle; {state.Describe()}.");
            await nextFrame();
        }
    }
}
