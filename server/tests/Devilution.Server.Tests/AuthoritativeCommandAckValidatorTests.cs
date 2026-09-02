using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeCommandAckValidatorTests
{
    [Fact]
    public void AcceptsKnownStatusesAndTheirRejectReasons()
    {
        AuthoritativeCommandAckValidator.Validate(new CommandAck {
            Results = {
                new CommandResult {
                    ClientSequence = 1,
                    Status = CommandStatus.Accepted,
                },
                new CommandResult {
                    ClientSequence = 2,
                    Status = CommandStatus.Rescheduled,
                },
                new CommandResult {
                    ClientSequence = 3,
                    Status = CommandStatus.Rejected,
                    RejectReason = CommandRejectReason.InvalidTarget,
                },
                new CommandResult {
                    ClientSequence = 4,
                    Status = CommandStatus.Duplicate,
                    RejectReason = CommandRejectReason.InvalidTarget,
                },
            },
        });
    }

    [Fact]
    public void RejectsUndefinedStatusesReasonsAndIncompatibleAcceptedReasons()
    {
        foreach (var result in new[] {
            new CommandResult(),
            new CommandResult { Status = (CommandStatus)99 },
            new CommandResult { Status = CommandStatus.Accepted, RejectReason = CommandRejectReason.TooLate },
            new CommandResult { Status = CommandStatus.Rescheduled, RejectReason = CommandRejectReason.Malformed },
            new CommandResult { Status = CommandStatus.Rejected, RejectReason = (CommandRejectReason)99 },
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeCommandAckValidator.Validate(new CommandAck {
                    Results = { result },
                }));
    }

    [Fact]
    public void RejectsAcknowledgementsBeyondTheResultBound()
    {
        var acknowledgement = new CommandAck();
        for (var index = 0; index <= AuthoritativeCommandAckValidator.MaximumResultCount; index++)
            acknowledgement.Results.Add(new CommandResult {
                ClientSequence = (ulong)index + 1,
                Status = CommandStatus.Accepted,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCommandAckValidator.Validate(acknowledgement));
    }
}
