using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Validates server command acknowledgements before they mutate client state. */
public static class AuthoritativeCommandAckValidator
{
    public const int MaximumResultCount = AuthoritativeProtocolLimits.MaximumCommandAcknowledgementResultCount;

    /** Accepted or successful replayed receipt; does not directly accept Rescheduled. */
    public static bool IsAcceptedOrSuccessfulDuplicate(CommandResult? result)
        // Duplicate retains the original rejection reason; duplicate alone is not success.
        => result is { Status: CommandStatus.Accepted or CommandStatus.Duplicate,
            RejectReason: CommandRejectReason.Unspecified };

    public static void Validate(CommandAck acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (acknowledgement.Results.Count > MaximumResultCount)
            throw new InvalidDataException("The authoritative command acknowledgement is too large.");

        foreach (var result in acknowledgement.Results) {
            if (result is null
                || result.Status == CommandStatus.Unspecified
                || !Enum.IsDefined(result.Status)
                || !Enum.IsDefined(result.RejectReason)
                || result.Status is not (CommandStatus.Rejected or CommandStatus.Duplicate)
                    && result.RejectReason != CommandRejectReason.Unspecified) {
                throw new InvalidDataException(
                    "The authoritative command acknowledgement contains an invalid result.");
            }
        }
    }
}
