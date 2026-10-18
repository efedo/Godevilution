using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Local held-input state; the server remains authoritative for action timing. */
public sealed class HeldEntityAttackController
{
    private long sessionGeneration;
    private long connectionAttemptEpoch;
    private uint playerEntityId;
    private uint levelId;
    private bool isHeld;
    private ulong outstandingSequence;
    private uint outstandingTargetEntityId;
    private ulong previousActionStartedAtTick;
    private bool hadPreviousAction;
    private ulong appliedTick;
    private bool awaitingActionResolution;

    /** Starts repeat ownership for an already queued ordinary first click. */
    public bool Begin(
        long generation,
        long attemptEpoch,
        ulong firstSequence,
        uint targetEntityId,
        HeldEntityAttackObservation beforeClick)
    {
        Reset();
        if (generation <= 0 || attemptEpoch <= 0 || firstSequence == 0 || targetEntityId == 0
            || !beforeClick.Connected || !beforeClick.SupportsQueuedRangedAttacks
            || !beforeClick.RightButtonHeld || beforeClick.PendingCommandCount != 0
            || beforeClick.PlayerLife <= 0 || beforeClick.PlayerEntityId == 0 || beforeClick.LevelId == 0
            || beforeClick.SessionGeneration != generation
            || beforeClick.ConnectionAttemptEpoch != attemptEpoch)
            return false;

        sessionGeneration = generation;
        connectionAttemptEpoch = attemptEpoch;
        playerEntityId = beforeClick.PlayerEntityId;
        levelId = beforeClick.LevelId;
        isHeld = true;
        outstandingSequence = firstSequence;
        outstandingTargetEntityId = targetEntityId;
        hadPreviousAction = beforeClick.HasCurrentAction;
        previousActionStartedAtTick = beforeClick.ActionStartedAtTick;
        return true;
    }

    /** Stops creating new inputs; pending commands and frozen retries remain untouched. */
    public void Release() => isHeld = false;

    /** Drops local ownership after connection/player lifecycle changes only. */
    public void Reset()
    {
        sessionGeneration = 0;
        connectionAttemptEpoch = 0;
        playerEntityId = 0;
        levelId = 0;
        isHeld = false;
        outstandingSequence = 0;
        outstandingTargetEntityId = 0;
        previousActionStartedAtTick = 0;
        hadPreviousAction = false;
        appliedTick = 0;
        awaitingActionResolution = false;
    }

    /** Called for each decoded result before the existing acknowledgement observer. */
    public void ObserveResult(CommandResult result)
    {
        if (result.ClientSequence == 0 || result.ClientSequence != outstandingSequence)
            return;
        if (result.Status == CommandStatus.Rescheduled)
            return;

        if (result.Status != CommandStatus.Accepted
            && !(result.Status == CommandStatus.Duplicate
                && result.RejectReason == CommandRejectReason.Unspecified)) {
            Release();
            outstandingSequence = 0;
            awaitingActionResolution = false;
            return;
        }

        appliedTick = result.AppliedTick;
        outstandingSequence = 0;
        awaitingActionResolution = true;
    }

    /** Returns one fresh entity-targeted click only at a server-authoritative repeat boundary. */
    public bool TryPrepareFollowUp(
        HeldEntityAttackObservation observation,
        uint? currentLiveTargetEntityId,
        out PreparedHeldEntityAttack prepared)
    {
        prepared = default;
        if (!isHeld)
            return false;

        if (!observation.Connected || !observation.SupportsQueuedRangedAttacks
            || observation.SessionGeneration != sessionGeneration
            || observation.ConnectionAttemptEpoch != connectionAttemptEpoch
            || observation.PlayerEntityId != playerEntityId
            || observation.LevelId != levelId || observation.PlayerLife <= 0) {
            Reset();
            return false;
        }
        if (!observation.RightButtonHeld) {
            Release();
            return false;
        }
        if (observation.PendingCommandCount != 0 || outstandingSequence != 0)
            return false;

        if (awaitingActionResolution) {
            if (observation.Tick < appliedTick)
                return false;

            var newerRangedAction = observation.HasCurrentAction
                && observation.CurrentActionKind == PlayerActionKind.Attack
                && IsNativeAttackMode(observation.CombatMode)
                && observation.ActionEffectApplied
                && observation.ActionStartedAtTick >= appliedTick
                && (!hadPreviousAction || observation.ActionStartedAtTick > previousActionStartedAtTick)
                && observation.ActionTargetEntityId == outstandingTargetEntityId;
            if (newerRangedAction) {
                previousActionStartedAtTick = observation.ActionStartedAtTick;
                hadPreviousAction = true;
                awaitingActionResolution = false;
            } else if (observation.Tick > appliedTick
                && !observation.HasCurrentAction
                && observation.CombatMode == PlayerCombatMode.Stand) {
                // A later Stand proves the accepted input did not leave an action to repeat.
                hadPreviousAction = false;
                previousActionStartedAtTick = 0;
                awaitingActionResolution = false;
            } else {
                return false;
            }
        }

        if (currentLiveTargetEntityId is not uint targetEntityId || targetEntityId == 0)
            return false;

        ulong? priorActionStartedAtTick = null;
        if (observation.HasCurrentAction) {
            if (observation.CurrentActionKind != PlayerActionKind.Attack
                || !IsNativeAttackMode(observation.CombatMode)
                || !observation.ActionEffectApplied
                || observation.Tick < observation.ActionReadyTick)
                return false;
            priorActionStartedAtTick = observation.ActionStartedAtTick;
        } else if (observation.CombatMode != PlayerCombatMode.Stand) {
            return false;
        }

        prepared = new PreparedHeldEntityAttack(targetEntityId, priorActionStartedAtTick);
        return true;
    }

    /** Registers only the sequence returned by the ordinary Main.Queue path. */
    public void RegisterQueued(PreparedHeldEntityAttack prepared, ulong sequence)
    {
        if (!isHeld || sequence == 0 || prepared.TargetEntityId == 0)
            return;
        outstandingSequence = sequence;
        outstandingTargetEntityId = prepared.TargetEntityId;
        hadPreviousAction = prepared.PriorActionStartedAtTick.HasValue;
        previousActionStartedAtTick = prepared.PriorActionStartedAtTick ?? 0;
        appliedTick = 0;
        awaitingActionResolution = true;
    }

    private static bool IsNativeAttackMode(PlayerCombatMode mode)
        => mode is PlayerCombatMode.Attack or PlayerCombatMode.RangedAttack;
}

public readonly record struct HeldEntityAttackObservation(
    long SessionGeneration,
    long ConnectionAttemptEpoch,
    bool Connected,
    bool SupportsQueuedRangedAttacks,
    bool RightButtonHeld,
    int PendingCommandCount,
    uint PlayerEntityId,
    int PlayerLife,
    uint LevelId,
    ulong Tick,
    PlayerCombatMode CombatMode,
    bool HasCurrentAction,
    PlayerActionKind CurrentActionKind,
    bool ActionEffectApplied,
    ulong ActionStartedAtTick,
    ulong ActionReadyTick,
    uint ActionTargetEntityId);

public readonly record struct PreparedHeldEntityAttack(
    uint TargetEntityId,
    ulong? PriorActionStartedAtTick);
