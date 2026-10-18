using System.Runtime.ExceptionServices;
using Devilution.Protocol.V1;

namespace Devilution.Server.Commands;

/**
 * Holds commands whose requested tick is ahead of the host's current
 * authoritative boundary. The wrapped command server remains the only
 * admission, execution, and receipt-ledger authority.
 *
 * The host supplies the current boundary on every submission and calls
 * DrainAndAdvanceTo before advancing autonomous simulation. A submission's
 * cancellation token is consulted only before the intent is accepted; once
 * queued, connection lifetime cannot discard the intent.
 * Queued intents are not durable across host restart. The optional publication
 * callback runs under the authoritative lock for each newly processed result
 * and must checkpoint state plus receipts before returning. Re-entry through
 * Submit, Drain or Dispose is rejected; Cancel and read-only queries are allowed.
 */
public sealed class ClockGatedCommandScheduler : IDisposable
{
    public const int DefaultMaximumPendingCommands = 1024;

    private readonly AuthoritativeCommandServer commandServer;
    private readonly Action<string, CommandAck>? durablePublication;
    private readonly Action<Exception>? executionFailure;
    private readonly int maximumPendingCommands;
    private readonly Dictionary<CommandKey, PendingCommand> pendingByKey = [];
    private readonly HashSet<PendingWork> activeWorks = [];
    private readonly List<PendingWork> queuedWorks = [];
    private readonly HashSet<CommandKey> undurableKeys = [];
    private readonly CancellationTokenRegistration hostCancellationRegistration;
    private ulong nextArrivalOrder;
    private int reservedPendingSlots;
    private bool cancelled;
    private bool operationInProgress;

    public ClockGatedCommandScheduler(
        AuthoritativeCommandServer commandServer,
        Action<string, CommandAck>? durablePublication = null,
        int maximumPendingCommands = DefaultMaximumPendingCommands,
        CancellationToken cancellationToken = default)
        : this(commandServer, durablePublication, maximumPendingCommands, cancellationToken, null)
    {
    }

    /** Optional whole-host fatal fence, invoked synchronously before further work. */
    public ClockGatedCommandScheduler(
        AuthoritativeCommandServer commandServer,
        Action<string, CommandAck>? durablePublication,
        int maximumPendingCommands,
        CancellationToken cancellationToken,
        Action<Exception>? executionFailure)
    {
        this.commandServer = commandServer ?? throw new ArgumentNullException(nameof(commandServer));
        if (maximumPendingCommands <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumPendingCommands));
        this.maximumPendingCommands = maximumPendingCommands;
        this.durablePublication = durablePublication;
        this.executionFailure = executionFailure;
        if (cancellationToken.CanBeCanceled)
            hostCancellationRegistration = cancellationToken.Register(Cancel);
    }

    public int MaximumPendingCommands => maximumPendingCommands;

    /** Retained wire slots, including duplicates and completed batch prefixes. */
    public int PendingCommandCount => commandServer.WithSynchronizedState(
        () => reservedPendingSlots);

    /** Fresh resumed processes must not reuse a sequence already accepted into the queue. */
    public ulong LastAdmittedClientSequence(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return commandServer.WithSynchronizedState(() => Math.Max(
            commandServer.LastClientSequence(sessionId),
            pendingByKey.Keys.Where(key => key.SessionId == sessionId)
                .Select(key => key.ClientSequence).DefaultIfEmpty(0UL).Max()));
    }

    /** Queues or immediately executes one wire batch at an authoritative boundary. */
    public Task<CommandAck> SubmitBatchAsync(
        string sessionId,
        CommandBatch batch,
        ulong actualAdmissionTick,
        CancellationToken cancellationToken = default)
        => SubmitBatchAsync(
            sessionId,
            batch,
            actualAdmissionTick,
            cancellationToken,
            lane: null);

    /** Queues one batch on a connection-owned lane without changing direct API semantics. */
    internal Task<CommandAck> SubmitBatchAsync(
        string sessionId,
        CommandBatch batch,
        ulong actualAdmissionTick,
        CancellationToken cancellationToken,
        ClockGatedCommandLane? lane)
        => SubmitBatchAsync(
            sessionId,
            batch,
            actualAdmissionTick,
            cancellationToken,
            lane,
            commitBoundaryPublication: null);

    /** Optional per-wire response publication for one new original command only. */
    internal Task<CommandAck> SubmitBatchAsync(
        string sessionId,
        CommandBatch batch,
        ulong actualAdmissionTick,
        CancellationToken cancellationToken,
        ClockGatedCommandLane? lane,
        Func<string, CommandAck, bool>? commitBoundaryPublication)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        return commandServer.WithSynchronizedState(
            () => SubmitBatchCore(
                sessionId,
                batch,
                actualAdmissionTick,
                cancellationToken,
                lane,
                commitBoundaryPublication));
    }

    /** Poisons a connection lane and fails all of its not-yet-durable work. */
    internal void PoisonLane(ClockGatedCommandLane lane, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(lane);
        ArgumentNullException.ThrowIfNull(failure);
        commandServer.WithSynchronizedState(() => {
            if (lane.TryPoison(failure)) {
                foreach (var work in activeWorks
                    .Where(work => ReferenceEquals(work.Lane, lane))
                    .ToArray())
                    FailWork(work, failure, null);
            }
            return true;
        });
    }

    /** Executes due work without advancing the wrapped server's clock. */
    private int RunDueWork(ulong targetTick)
    {
        var executedCommands = 0;
        while (TryTakeDueWork(targetTick, out var work)) {
            if (ExecuteNextStep(work))
                executedCommands++;
            RequeueIfReady(work);
            if (cancelled)
                break;
        }
        return executedCommands;
    }

    /**
     * Processes due batch heads at or before the supplied boundary, in
     * requested-tick and stable-arrival order, then advances the wrapped server
     * to the boundary. Future commands never advance the wrapped server while
     * they remain queued.
     * Returns the count of newly processed, successfully published results,
     * including rejections but excluding duplicate-only entries.
     */
    public int DrainAndAdvanceTo(ulong actualTick)
    {
        return commandServer.WithSynchronizedState(() => {
            EnsureActive();
            if (operationInProgress)
                throw new InvalidOperationException(
                    "Clock advancement cannot re-enter while scheduler work is in progress.");
            operationInProgress = true;
            try {
                var targetTick = Math.Max(actualTick, commandServer.CurrentTick);
                var executedCommands = RunDueWork(targetTick);
                if (!cancelled)
                    commandServer.AdvanceTo(targetTick);
                return executedCommands;
            } finally {
                operationInProgress = false;
            }
        });
    }

    /** Cancels all queued work for whole-host shutdown; connection loss is not cancellation. */
    public void Cancel()
    {
        commandServer.WithSynchronizedState(() => {
            CancelCore();
            return true;
        });
    }

    public void Dispose()
    {
        commandServer.WithSynchronizedState(() => {
            if (operationInProgress)
                throw new InvalidOperationException(
                    "The scheduler cannot be disposed while scheduler work is in progress.");
            CancelCore();
            return true;
        });
        hostCancellationRegistration.Dispose();
    }

    private Task<CommandAck> SubmitBatchCore(
        string sessionId,
        CommandBatch batch,
        ulong actualAdmissionTick,
        CancellationToken cancellationToken,
        ClockGatedCommandLane? lane,
        Func<string, CommandAck, bool>? commitBoundaryPublication)
    {
        EnsureActive();
        lane?.ThrowIfPoisoned();
        if (operationInProgress)
            throw new InvalidOperationException(
                "Command submission cannot re-enter while scheduler work is in progress.");
        operationInProgress = true;
        try {
            return SubmitBatchCoreBody(
                sessionId,
                batch,
                actualAdmissionTick,
                cancellationToken,
                lane,
                commitBoundaryPublication);
        } finally {
            operationInProgress = false;
        }
    }

    private Task<CommandAck> SubmitBatchCoreBody(
        string sessionId,
        CommandBatch batch,
        ulong actualAdmissionTick,
        CancellationToken cancellationToken,
        ClockGatedCommandLane? lane,
        Func<string, CommandAck, bool>? commitBoundaryPublication)
    {
        EnsureActive();
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveAdmissionTick = Math.Max(actualAdmissionTick, commandServer.CurrentTick);
        RunDueWork(effectiveAdmissionTick);
        EnsureActive();
        lane?.ThrowIfPoisoned();
        cancellationToken.ThrowIfCancellationRequested();

        if (batch.Commands.Count == 0 && lane is not null)
            return SubmitLaneBarrierBatch(sessionId, batch, lane, effectiveAdmissionTick);

        if (string.IsNullOrWhiteSpace(sessionId)
            || batch.Commands.Count == 0)
            return ExecuteImmediateBatch(sessionId, batch, effectiveAdmissionTick);

        if (checked(reservedPendingSlots + batch.Commands.Count) > maximumPendingCommands)
            throw new ClockGatedCommandCapacityException(
                "The clock-gated command scheduler pending-command capacity is exhausted.");

        var completed = commandServer.ExportCompletedCommands(sessionId);
        var commands = batch.Commands.Select(command => command.Clone()).ToArray();
        var slots = new List<CommandSlot>(commands.Length);
        var originals = new List<PendingCommand>();
        var redurableKeys = new HashSet<CommandKey>();
        var redurableResults = new List<CommandResult>();
        var localNewCommands = new Dictionary<CommandKey, PendingCommand>();
        var reservationCount = 0;
        var arrivalOrder = NextArrivalOrder();

        foreach (var command in commands) {
            reservationCount++;
            if (command.ClientSequence == 0) {
                var malformed = new PendingCommand(
                    new CommandKey(sessionId, 0),
                    command,
                    effectiveAdmissionTick,
                    false);
                originals.Add(malformed);
                slots.Add(CommandSlot.Original(malformed));
                continue;
            }

            var key = new CommandKey(sessionId, command.ClientSequence);
            if (localNewCommands.TryGetValue(key, out var pending)
                || pendingByKey.TryGetValue(key, out pending)) {
                slots.Add(CommandSlot.Duplicate(pending));
                continue;
            }

            if (completed.TryGetValue(command.ClientSequence, out var priorResult)) {
                var duplicate = priorResult.Clone();
                duplicate.Status = CommandStatus.Duplicate;
                slots.Add(CommandSlot.Immediate(duplicate));
                if (undurableKeys.Contains(key)) {
                    redurableKeys.Add(key);
                    redurableResults.Add(duplicate.Clone());
                }
                continue;
            }

            pending = new PendingCommand(
                key,
                command,
                command.IntentCase == Command.IntentOneofCase.None
                    ? effectiveAdmissionTick
                    : Math.Max(effectiveAdmissionTick, command.RequestedTick),
                true);
            localNewCommands.Add(key, pending);
            originals.Add(pending);
            pendingByKey.Add(key, pending);
            slots.Add(CommandSlot.Original(pending));
        }

        var originalSlotPublication = batch.Commands.Count == 1
            && slots.Count == 1
            && slots[0].ImmediateResult is null
            && !slots[0].IsDuplicate
            && slots[0].PendingOwner is { TracksLedger: true }
            ? commitBoundaryPublication
            : null;
        var work = new PendingWork(
            sessionId,
            slots,
            originals,
            redurableKeys.ToArray(),
            redurableResults,
            arrivalOrder,
            reservationCount,
            lane,
            lane?.TakePredecessor(),
            commitBoundaryPublication: originalSlotPublication);
        lane?.SetTail(work.Completion.Task);
        activeWorks.Add(work);
        reservedPendingSlots += reservationCount;
        foreach (var slot in slots)
            if (slot.PendingOwner is { } owner && slot.IsDuplicate)
                owner.DependentWorks.Add(work);

        if (!work.IsLanePredecessorReady
            || work.HasProgressableStep
            || work.HasPendingDurablePublication) {
            queuedWorks.Add(work);
            work.IsQueued = true;
        } else
            TryCompleteWork(work);
        RunDueWork(effectiveAdmissionTick);
        return work.Completion.Task;
    }

    /**
     * Empty clock-gated batches are still ordered wire work. They do not
     * consume scheduler slots, but preserve the ordinary malformed-command
     * result and publication after earlier work on the same connection lane.
     */
    private Task<CommandAck> SubmitLaneBarrierBatch(
        string sessionId,
        CommandBatch batch,
        ClockGatedCommandLane lane,
        ulong effectiveAdmissionTick)
    {
        var work = new PendingWork(
            sessionId,
            slots: [],
            originals: [],
            redurableKeys: [],
            redurableResults: [],
            arrivalOrder: NextArrivalOrder(),
            reservationCount: 0,
            lane,
            lane.TakePredecessor(),
            laneBarrierBatch: batch.Clone(),
            laneBarrierAdmissionTick: effectiveAdmissionTick);
        lane.SetTail(work.Completion.Task);
        activeWorks.Add(work);
        queuedWorks.Add(work);
        work.IsQueued = true;
        RunDueWork(effectiveAdmissionTick);
        return work.Completion.Task;
    }

    private Task<CommandAck> ExecuteImmediateBatch(
        string sessionId,
        CommandBatch batch,
        ulong actualAdmissionTick)
    {
        try {
            var acknowledgement = commandServer.ProcessBatch(
                sessionId,
                batch,
                Math.Max(actualAdmissionTick, commandServer.CurrentTick));
            PublishDurable(sessionId, acknowledgement);
            if (cancelled)
                throw new OperationCanceledException("The scheduler was cancelled during publication.");
            return Task.FromResult(acknowledgement.Clone());
        } catch (Exception exception) {
            ReportExecutionFailure(exception);
            return Task.FromException<CommandAck>(exception);
        }
    }

    private void PublishDurable(string sessionId, CommandAck acknowledgement)
    {
        if (durablePublication is null)
            return;
        durablePublication(sessionId, acknowledgement.Clone());
    }

    private bool ExecuteNextStep(PendingWork work)
    {
        if (!activeWorks.Contains(work))
            return false;
        if (work.TryGetBlockingFailure(out var dependencyFailure)) {
            FailWork(work, dependencyFailure!, null);
            return false;
        }

        if (!work.TryPrepareNextStep(out var slot)) {
            if (work.LaneBarrierBatch is { } barrierBatch) {
                try {
                    var acknowledgement = ExecuteImmediateBatch(work.SessionId, barrierBatch,
                        Math.Max(work.LaneBarrierAdmissionTick, commandServer.CurrentTick))
                        .GetAwaiter().GetResult();
                    work.Completion.TrySetResult(acknowledgement);
                    ReleaseWork(work);
                    NotifyLaneSuccessors(work);
                } catch (Exception failure) {
                    FailWork(work, failure, null);
                }
                return false;
            }
            if (work.HasPendingDurablePublication)
                PublishRedurableResults(work);
            TryCompleteWork(work);
            return false;
        }

        var pending = slot.PendingOwner!;
        try {
            var executionTick = Math.Max(pending.DueTick, commandServer.CurrentTick);
            var result = commandServer.Process(work.SessionId, pending.Command, executionTick);
            pending.Result = result.Clone();
            var acknowledgement = CreateSingleResultAcknowledgement(result);
            if (!work.TryPublishAtCommitBoundary(acknowledgement))
                PublishDurable(work.SessionId, acknowledgement);
            if (cancelled)
                return false;
            pending.Durable = true;
            if (pending.TracksLedger)
                pendingByKey.Remove(pending.Key);
            work.NextSlotIndex++;
            NotifyDependents(pending);
            TryCompleteWork(work);
            return true;
        } catch (Exception exception) {
            FailWork(work, exception, pending);
            ReportExecutionFailure(exception);
            return false;
        }
    }

    private void PublishRedurableResults(PendingWork work)
    {
        try {
            if (work.RedurableResults.Count != 0)
                PublishDurable(
                    work.SessionId,
                    CreateAcknowledgement(work.RedurableResults));
            if (cancelled)
                return;
            foreach (var key in work.RedurableKeys)
                undurableKeys.Remove(key);
            work.RedurablePublicationComplete = true;
        } catch (Exception exception) {
            FailWork(work, exception, null);
            ReportExecutionFailure(exception);
        }
    }

    private void ReportExecutionFailure(Exception failure)
    {
        if (executionFailure is null)
            return;
        try { executionFailure(failure); }
        catch (Exception diagnosticFailure) {
            try { failure.Data["SchedulerFailureNotification"] = diagnosticFailure; } catch { }
        } finally {
            // Even a fallible observer cannot permit a queued peer or final
            // autonomous advance after a configured whole-host fatal failure.
            CancelCore();
        }
    }

    private void FailWork(
        PendingWork work,
        Exception exception,
        PendingCommand? failedPending,
        bool poisonLane = true)
    {
        if (!activeWorks.Contains(work))
            return;
        if (poisonLane
            && work.Lane is { } lane
            && lane.TryPoison(exception)) {
            foreach (var peer in activeWorks
                .Where(candidate => ReferenceEquals(candidate.Lane, lane)
                    && !ReferenceEquals(candidate, work))
                .ToArray())
                FailWork(peer, exception, null, poisonLane: false);
        }
        if (failedPending is not null
            && failedPending.Result is not null
            && !failedPending.Durable
            && failedPending.TracksLedger)
            undurableKeys.Add(failedPending.Key);

        var failedOwners = new List<PendingCommand>();
        foreach (var pending in work.Originals) {
            if (pending.Durable)
                continue;
            if (pending.TracksLedger)
                pendingByKey.Remove(pending.Key);
            if (pending.Result is not null && pending.TracksLedger)
                undurableKeys.Add(pending.Key);
            pending.Failure = exception;
            failedOwners.Add(pending);
        }

        work.Completion.TrySetException(exception);
        foreach (var slot in work.Slots)
            if (slot.PendingOwner is { } owner && slot.IsDuplicate)
                owner.DependentWorks.Remove(work);
        ReleaseWork(work);
        foreach (var pending in failedOwners)
            NotifyDependents(pending);
        NotifyLaneSuccessors(work);
    }

    private void NotifyDependents(PendingCommand pending)
    {
        foreach (var dependent in pending.DependentWorks.ToArray())
            RequeueIfReady(dependent);
        pending.DependentWorks.Clear();
    }

    private void RequeueIfReady(PendingWork work)
    {
        if (!activeWorks.Contains(work))
            return;
        if (work.TryGetBlockingFailure(out var dependencyFailure)) {
            FailWork(work, dependencyFailure!, null);
            return;
        }
        if (work.IsQueued) {
            if (work.IsLanePredecessorReady
                && !work.IsLaneBarrier
                && !work.HasProgressableStep
                && !work.HasPendingDurablePublication) {
                queuedWorks.Remove(work);
                work.IsQueued = false;
                TryCompleteWork(work);
            }
            return;
        }
        if (work.HasPendingDurablePublication && !work.HasProgressableStep) {
            queuedWorks.Add(work);
            work.IsQueued = true;
            return;
        }
        if (!work.TryGetNextDueTick(out _)) {
            TryCompleteWork(work);
            return;
        }
        queuedWorks.Add(work);
        work.IsQueued = true;
    }

    private void TryCompleteWork(PendingWork work)
    {
        if (!activeWorks.Contains(work) || work.Completion.Task.IsCompleted)
            return;
        if (!work.IsLanePredecessorReady)
            return;
        if (work.TryGetBlockingFailure(out var dependencyFailure)) {
            FailWork(work, dependencyFailure!, null);
            return;
        }
        if (work.HasProgressableStep || !work.RedurablePublicationComplete)
            return;

        var results = new List<CommandResult>(work.Slots.Count);
        foreach (var slot in work.Slots) {
            if (slot.ImmediateResult is { } immediate) {
                results.Add(immediate.Clone());
                continue;
            }

            var owner = slot.PendingOwner!;
            if (owner.Failure is { } failure) {
                FailWork(work, failure, null);
                return;
            }
            if (!owner.Durable || owner.Result is null)
                return;
            results.Add(slot.IsDuplicate
                ? CreateDuplicateResult(owner.Result)
                : owner.Result.Clone());
        }

        work.Completion.TrySetResult(CreateAcknowledgement(results));
        ReleaseWork(work);
        NotifyLaneSuccessors(work);
    }

    private void NotifyLaneSuccessors(PendingWork completed)
    {
        var completion = completed.Completion.Task;
        foreach (var successor in activeWorks
            .Where(work => ReferenceEquals(work.PredecessorCompletion, completion))
            .ToArray())
            RequeueIfReady(successor);
    }

    private static CommandAck CreateSingleResultAcknowledgement(CommandResult result)
        => CreateAcknowledgement([result]);

    private static CommandAck CreateAcknowledgement(IEnumerable<CommandResult> results)
    {
        var acknowledgement = new CommandAck();
        acknowledgement.Results.Add(results.Select(result => result.Clone()));
        return acknowledgement;
    }

    private void ReleaseWork(PendingWork work)
    {
        if (!activeWorks.Remove(work))
            return;
        if (work.IsQueued) {
            queuedWorks.Remove(work);
            work.IsQueued = false;
        }
        reservedPendingSlots -= work.ReservationCount;
        if (reservedPendingSlots < 0)
            throw new InvalidOperationException("The scheduler pending-slot accounting was corrupted.");
    }

    private bool TryTakeDueWork(ulong targetTick, out PendingWork work)
    {
        var candidateIndex = -1;
        var selectedDueTick = ulong.MaxValue;
        for (var index = 0; index < queuedWorks.Count; index++) {
            var candidate = queuedWorks[index];
            var hasNext = candidate.TryGetNextDueTick(out var candidateDueTick);
            var hasBarrier = !hasNext && candidate.IsLaneBarrier
                && candidate.IsLanePredecessorReady;
            if (!hasNext && candidate.HasPendingDurablePublication)
                candidateDueTick = 0;
            if (hasBarrier)
                candidateDueTick = 0;
            if (!candidate.IsLanePredecessorReady)
                continue;
            if (!hasNext && !candidate.HasPendingDurablePublication && !hasBarrier
                || candidateDueTick > targetTick)
                continue;
            if (candidateIndex < 0
                || candidateDueTick < selectedDueTick
                || candidateDueTick == selectedDueTick
                    && candidate.ArrivalOrder < queuedWorks[candidateIndex].ArrivalOrder)
            {
                candidateIndex = index;
                selectedDueTick = candidateDueTick;
            }
        }

        if (candidateIndex < 0) {
            work = null!;
            return false;
        }

        work = queuedWorks[candidateIndex];
        queuedWorks.RemoveAt(candidateIndex);
        work.IsQueued = false;
        return true;
    }

    private ulong NextArrivalOrder()
    {
        if (nextArrivalOrder == ulong.MaxValue)
            throw new InvalidOperationException("The clock-gated command arrival sequence is exhausted.");
        return ++nextArrivalOrder;
    }

    private void CancelCore()
    {
        if (cancelled)
            return;
        cancelled = true;
        var cancellation = new CancellationToken(true);
        foreach (var work in activeWorks.ToArray())
            work.Completion.TrySetCanceled(cancellation);
        pendingByKey.Clear();
        activeWorks.Clear();
        queuedWorks.Clear();
        undurableKeys.Clear();
        reservedPendingSlots = 0;
    }

    private void EnsureActive()
    {
        if (cancelled)
            throw new ObjectDisposedException(nameof(ClockGatedCommandScheduler));
    }

    private static CommandResult CreateDuplicateResult(CommandResult result)
    {
        var duplicate = result.Clone();
        duplicate.Status = CommandStatus.Duplicate;
        return duplicate;
    }

    private readonly record struct CommandKey(string SessionId, ulong ClientSequence);

    private sealed class PendingCommand(
        CommandKey key,
        Command command,
        ulong dueTick,
        bool tracksLedger)
    {
        public CommandKey Key { get; } = key;
        public Command Command { get; } = command;
        public ulong DueTick { get; } = dueTick;
        public bool TracksLedger { get; } = tracksLedger;
        public List<PendingWork> DependentWorks { get; } = [];
        public CommandResult? Result { get; set; }
        public Exception? Failure { get; set; }
        public bool Durable { get; set; }
    }

    private sealed class CommandSlot
    {
        private CommandSlot(CommandResult? immediateResult, PendingCommand? pendingOwner, bool isDuplicate)
        {
            ImmediateResult = immediateResult;
            PendingOwner = pendingOwner;
            IsDuplicate = isDuplicate;
        }

        public CommandResult? ImmediateResult { get; }
        public PendingCommand? PendingOwner { get; }
        public bool IsDuplicate { get; }

        public static CommandSlot Immediate(CommandResult result)
            => new(result.Clone(), null, false);

        public static CommandSlot Original(PendingCommand pending)
            => new(null, pending, false);

        public static CommandSlot Duplicate(PendingCommand pending)
            => new(null, pending, true);
    }

    private sealed class PendingWork(
        string sessionId,
        IReadOnlyList<CommandSlot> slots,
        IReadOnlyList<PendingCommand> originals,
        IReadOnlyList<CommandKey> redurableKeys,
        IReadOnlyList<CommandResult> redurableResults,
        ulong arrivalOrder,
        int reservationCount,
        ClockGatedCommandLane? lane,
        Task<CommandAck>? predecessorCompletion,
        CommandBatch? laneBarrierBatch = null,
        ulong laneBarrierAdmissionTick = 0,
        Func<string, CommandAck, bool>? commitBoundaryPublication = null)
    {
        public string SessionId { get; } = sessionId;
        public IReadOnlyList<CommandSlot> Slots { get; } = slots;
        public IReadOnlyList<PendingCommand> Originals { get; } = originals;
        public IReadOnlyList<CommandKey> RedurableKeys { get; } = redurableKeys;
        public IReadOnlyList<CommandResult> RedurableResults { get; } = redurableResults;
        public ulong ArrivalOrder { get; } = arrivalOrder;
        public int ReservationCount { get; } = reservationCount;
        public ClockGatedCommandLane? Lane { get; } = lane;
        public Task<CommandAck>? PredecessorCompletion { get; } = predecessorCompletion;
        public CommandBatch? LaneBarrierBatch { get; } = laneBarrierBatch;
        public ulong LaneBarrierAdmissionTick { get; } = laneBarrierAdmissionTick;
        public Func<string, CommandAck, bool>? CommitBoundaryPublication { get; } = commitBoundaryPublication;
        public bool IsLaneBarrier => LaneBarrierBatch is not null;
        public TaskCompletionSource<CommandAck> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int NextSlotIndex { get; set; }
        public bool RedurablePublicationComplete { get; set; }
            = redurableResults.Count == 0;
        public bool IsQueued { get; set; }

        public bool HasPendingDurablePublication
            => RedurableResults.Count != 0 && !RedurablePublicationComplete;

        public bool TryPublishAtCommitBoundary(CommandAck acknowledgement)
            => CommitBoundaryPublication?.Invoke(SessionId, acknowledgement) == true;

        public bool IsLanePredecessorReady
            => PredecessorCompletion is null || PredecessorCompletion.IsCompleted;

        public bool HasProgressableStep
            => TryGetNextDueTick(out _);

        public bool TryPrepareNextStep(out CommandSlot slot)
        {
            while (NextSlotIndex < Slots.Count) {
                var candidate = Slots[NextSlotIndex];
                if (candidate.ImmediateResult is not null) {
                    NextSlotIndex++;
                    continue;
                }

                var owner = candidate.PendingOwner!;
                if (candidate.IsDuplicate) {
                    if (owner.Failure is not null || !owner.Durable) {
                        slot = null!;
                        return false;
                    }
                    NextSlotIndex++;
                    continue;
                }

                slot = candidate;
                return true;
            }

            slot = null!;
            return false;
        }

        public bool TryGetNextDueTick(out ulong dueTick)
        {
            if (!IsLanePredecessorReady) {
                dueTick = 0;
                return false;
            }
            if (!TryPrepareNextStep(out var slot)) {
                dueTick = 0;
                return false;
            }
            dueTick = slot.PendingOwner!.DueTick;
            return true;
        }

        public bool TryGetBlockingFailure(out Exception? failure)
        {
            if (Lane?.Failure is { } laneFailure) {
                failure = laneFailure;
                return true;
            }
            if (PredecessorCompletion?.IsFaulted == true) {
                failure = PredecessorCompletion.Exception?.GetBaseException()
                    ?? new InvalidOperationException("A preceding clock-gated lane work failed.");
                return true;
            }
            for (var index = NextSlotIndex; index < Slots.Count; index++) {
                var candidate = Slots[index];
                if (candidate.ImmediateResult is not null)
                    continue;

                var owner = candidate.PendingOwner!;
                if (candidate.IsDuplicate && owner.Failure is { } ownerFailure) {
                    failure = ownerFailure;
                    return true;
                }
            }

            failure = null;
            return false;
        }
    }
}

/** Connection-owned failure fence for clock-gated admission lanes. */
internal sealed class ClockGatedCommandLane
{
    private Exception? failure;
    private Task<CommandAck>? tail;
    private int transportClosed;
    private int failureReported;

    public Exception? Failure => Volatile.Read(ref failure);
    internal bool IsTransportClosed => Volatile.Read(ref transportClosed) != 0;

    internal void MarkTransportClosed()
        => Volatile.Write(ref transportClosed, 1);

    internal bool TryMarkFailureReported()
        => Interlocked.Exchange(ref failureReported, 1) == 0;

    internal bool TryPoison(Exception exception)
        => Interlocked.CompareExchange(ref failure, exception, null) is null;

    internal void ThrowIfPoisoned()
    {
        if (Failure is { } exception)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    internal Task<CommandAck>? TakePredecessor()
    {
        var predecessor = tail;
        if (predecessor?.IsCompleted == true)
            predecessor = null;
        return predecessor;
    }

    internal void SetTail(Task<CommandAck> completion)
        => tail = completion;
}

/** Resource refusal is not gameplay lateness and allocates no new receipt. */
public sealed class ClockGatedCommandCapacityException(string message) : InvalidOperationException(message);
