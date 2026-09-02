using Devilution.Protocol.V1;

namespace Devilution.Server.Protocol;

/** Validates command acknowledgements before the host sends them to clients. */
public static class AuthoritativeCommandAckValidator
{
    public const int MaximumResultCount = 256;

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
