using Devilution.Protocol.V1;

namespace Devilution.Server.Protocol;

/** Validates command-batch size before commands reach authoritative execution. */
public static class AuthoritativeCommandBatchValidator
{
    public const int MaximumCommandBatchCount = 256;

    public static void Validate(CommandBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Commands.Count > MaximumCommandBatchCount)
            throw new InvalidDataException("The authoritative command batch is too large.");
    }
}
