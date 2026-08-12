using Devilution.Protocol.V1;

namespace Devilution.Server.Commands;

/** Executes an admitted command exactly once on authoritative game state. */
public interface IAuthoritativeCommandExecutor
{
    CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick);
}

/** Advances autonomous authoritative simulation before command execution. */
public interface IAuthoritativeTickExecutor
{
    void AdvanceTo(ulong tick);
}

/** The domain executor's result after command admission. */
public readonly record struct CommandExecutionResult(bool Succeeded, CommandRejectReason RejectReason)
{
    public static CommandExecutionResult Accepted { get; } = new(true, CommandRejectReason.Unspecified);

    public static CommandExecutionResult Rejected(CommandRejectReason rejectReason)
    {
        return new(false, rejectReason);
    }
}
