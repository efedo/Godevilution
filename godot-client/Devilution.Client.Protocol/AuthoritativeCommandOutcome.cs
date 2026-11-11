using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

public static class AuthoritativeCommandOutcome
{
    public static bool IsAwaitingRetry(CommandResult result, IReadOnlyList<AuthoritativeCommandReceiptContext>? contexts)
        => result is { ServerReceiptSequence: > 0, Status: CommandStatus.Rejected or CommandStatus.Duplicate,
                RejectReason: CommandRejectReason.TooLate }
            && contexts?.Any(context => context.ClientSequence == result.ClientSequence && context.IntentSequence > 0 && context.RetryPending) == true;

    /** Correlates a terminal receipt to the input; its wire sequence and rejection remain authoritative. */
    public static bool IsForIntent(CommandResult? result, ulong intentSequence, ulong expectedIntentSequence)
        => result is { ClientSequence: > 0 } && expectedIntentSequence > 0
            && (result.ClientSequence == expectedIntentSequence || intentSequence == expectedIntentSequence);
}
