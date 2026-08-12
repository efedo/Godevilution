using Devilution.Protocol.V1;

namespace Devilution.Server.Commands;

/**
 * Applies the authoritative late-command policy before a command reaches game
 * state. Gameplay-changing commands are strict by default; opening a store is
 * non-critical and may be moved to the current tick within a small window.
 */
public sealed class CommandAdmissionPolicy
{
    public CommandAdmissionPolicy(ulong gameplayCriticalLateToleranceTicks = 0, ulong nonCriticalLateToleranceTicks = 2)
    {
        GameplayCriticalLateToleranceTicks = gameplayCriticalLateToleranceTicks;
        NonCriticalLateToleranceTicks = nonCriticalLateToleranceTicks;
    }

    public ulong GameplayCriticalLateToleranceTicks { get; }

    public ulong NonCriticalLateToleranceTicks { get; }

    public CommandAdmission Evaluate(Command command, ulong currentTick)
    {
        if (command.IntentCase == Command.IntentOneofCase.None)
            return CommandAdmission.Rejected(CommandRejectReason.Malformed, currentTick);

        if (command.RequestedTick >= currentTick)
            return CommandAdmission.Accepted(command.RequestedTick);

        var lateness = currentTick - command.RequestedTick;
        var lateTolerance = command.IntentCase == Command.IntentOneofCase.OpenStoreRequested
            ? NonCriticalLateToleranceTicks
            : GameplayCriticalLateToleranceTicks;
        if (lateness <= lateTolerance)
            return CommandAdmission.Rescheduled(currentTick);

        return CommandAdmission.Rejected(CommandRejectReason.TooLate, currentTick);
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
