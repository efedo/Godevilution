using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeCommandBatchValidatorTests
{
    [Fact]
    public void AcceptsBatchesWithinTheBound()
    {
        var batch = new CommandBatch();
        for (var index = 0; index < AuthoritativeCommandBatchValidator.MaximumCommandBatchCount; index++)
            batch.Commands.Add(new Command());

        AuthoritativeCommandBatchValidator.Validate(batch);
    }

    [Fact]
    public void RejectsOversizedBatches()
    {
        var batch = new CommandBatch();
        for (var index = 0; index <= AuthoritativeCommandBatchValidator.MaximumCommandBatchCount; index++)
            batch.Commands.Add(new Command());

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCommandBatchValidator.Validate(batch));
    }
}
