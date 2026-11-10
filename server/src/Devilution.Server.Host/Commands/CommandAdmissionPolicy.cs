using Devilution.Protocol.V1;

namespace Devilution.Server.Commands;

/**
 * Applies the authoritative late-command policy before a command reaches game
 * state. Gameplay-changing commands are strict by default; a live host sets a
 * tolerance within which a late one runs on the current tick and is accepted.
 * Opening a store is non-critical and may be moved to the current tick within
 * a small window, reported as rescheduled.
 */
public sealed class CommandAdmissionPolicy
{
    private readonly ulong gameplayCriticalLateToleranceTicks;
    private readonly ulong nonCriticalLateToleranceTicks;
    private readonly Func<uint>? gameSpeedMultiplier;

    public CommandAdmissionPolicy(ulong gameplayCriticalLateToleranceTicks = 0, ulong nonCriticalLateToleranceTicks = 2)
        : this(gameplayCriticalLateToleranceTicks, nonCriticalLateToleranceTicks, null)
    {
    }

    private CommandAdmissionPolicy(ulong gameplayCriticalLateToleranceTicks, ulong nonCriticalLateToleranceTicks,
        Func<uint>? gameSpeedMultiplier)
    {
        this.gameplayCriticalLateToleranceTicks = gameplayCriticalLateToleranceTicks;
        this.nonCriticalLateToleranceTicks = nonCriticalLateToleranceTicks;
        this.gameSpeedMultiplier = gameSpeedMultiplier;
    }

    /** Live tolerances are wall time, so they scale with the current game speed. */
    public ulong GameplayCriticalLateToleranceTicks => Scaled(gameplayCriticalLateToleranceTicks);

    public ulong NonCriticalLateToleranceTicks => Scaled(nonCriticalLateToleranceTicks);

    private ulong Scaled(ulong ticks)
        => gameSpeedMultiplier is null ? ticks : checked(ticks * Math.Max(1U, gameSpeedMultiplier()));

    /** A live host's policy at its tick rate, tolerating `LiveLateToleranceSeconds` of lateness. */
    public static CommandAdmissionPolicy ForLiveHost(uint tickRateHz)
        => new(gameplayCriticalLateToleranceTicks: (ulong)LiveLateToleranceSeconds * tickRateHz);

    /** A live host's policy whose paced rate is `tickRateHz` times the current game speed. */
    public static CommandAdmissionPolicy ForLiveHost(uint tickRateHz, Func<uint> gameSpeedMultiplier)
        => new((ulong)LiveLateToleranceSeconds * tickRateHz, 2, gameSpeedMultiplier
            ?? throw new ArgumentNullException(nameof(gameSpeedMultiplier)));

    /** Matches the client's longest retry timeout, so only a stale resend is still refused. */
    public const uint LiveLateToleranceSeconds = 2;

    public CommandAdmission Evaluate(Command command, ulong currentTick)
    {
        if (command.IntentCase == Command.IntentOneofCase.None)
            return CommandAdmission.Rejected(CommandRejectReason.Malformed, currentTick);

        if (command.RequestedTick >= currentTick)
            return CommandAdmission.Accepted(command.RequestedTick);

        var lateness = currentTick - command.RequestedTick;
        if (command.IntentCase is Command.IntentOneofCase.OpenStoreRequested
            or Command.IntentOneofCase.CloseStoreRequested
            or Command.IntentOneofCase.OpenStashRequested
            or Command.IntentOneofCase.CloseStashRequested) {
            return lateness <= NonCriticalLateToleranceTicks
                ? CommandAdmission.Rescheduled(currentTick)
                : CommandAdmission.Rejected(CommandRejectReason.TooLate, currentTick);
        }
        return lateness <= GameplayCriticalLateToleranceTicks
            ? CommandAdmission.Accepted(currentTick)
            : CommandAdmission.Rejected(CommandRejectReason.TooLate, currentTick);
    }
}

/** A deterministic admission decision used to construct a protocol result. */
public readonly record struct CommandAdmission(CommandStatus Status, CommandRejectReason RejectReason, ulong AppliedTick)
{
    public static CommandAdmission Accepted(ulong appliedTick)
    {
        return new(CommandStatus.Accepted, CommandRejectReason.Unspecified, appliedTick);
    }

    public static CommandAdmission Rescheduled(ulong appliedTick)
    {
        return new(CommandStatus.Rescheduled, CommandRejectReason.Unspecified, appliedTick);
    }

    public static CommandAdmission Rejected(CommandRejectReason rejectReason, ulong appliedTick)
    {
        return new(CommandStatus.Rejected, rejectReason, appliedTick);
    }
}
