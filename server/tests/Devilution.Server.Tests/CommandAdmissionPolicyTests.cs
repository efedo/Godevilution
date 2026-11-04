using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class CommandAdmissionPolicyTests
{
    private static Command Attack(ulong requestedTick) => new() {
        ClientSequence = 1,
        RequestedTick = requestedTick,
        AttackRequested = new AttackRequested { TargetEntityId = 1 },
    };

    [Fact]
    public void DefaultPolicyStaysStrictForDirectCallers()
    {
        var admission = new CommandAdmissionPolicy().Evaluate(Attack(99), 100);

        Assert.Equal(CommandStatus.Rejected, admission.Status);
        Assert.Equal(CommandRejectReason.TooLate, admission.RejectReason);
    }

    [Theory]
    [InlineData(20U, 40UL)]
    [InlineData(200U, 400UL)]
    public void LiveHostAcceptsLateGameplayOnTheCurrentTickWithinItsTolerance(uint rate, ulong tolerance)
    {
        var policy = CommandAdmissionPolicy.ForLiveHost(rate);

        var late = policy.Evaluate(Attack(1000), 1000 + tolerance);
        Assert.Equal(CommandStatus.Accepted, late.Status);
        Assert.Equal(CommandRejectReason.Unspecified, late.RejectReason);
        Assert.Equal(1000 + tolerance, late.AppliedTick);

        var stale = policy.Evaluate(Attack(1000), 1000 + tolerance + 1);
        Assert.Equal(CommandStatus.Rejected, stale.Status);
        Assert.Equal(CommandRejectReason.TooLate, stale.RejectReason);
    }

    [Fact]
    public void LiveHostKeepsFutureCommandsOnTheirRequestedTick()
    {
        var admission = CommandAdmissionPolicy.ForLiveHost(200).Evaluate(Attack(1005), 1000);

        Assert.Equal(CommandStatus.Accepted, admission.Status);
        Assert.Equal(1005UL, admission.AppliedTick);
    }

    [Fact]
    public void StoreCommandsKeepTheirShortRescheduleWindow()
    {
        var policy = CommandAdmissionPolicy.ForLiveHost(200);
        var open = new Command {
            ClientSequence = 1,
            RequestedTick = 100,
            OpenStoreRequested = new OpenStoreRequested(),
        };

        Assert.Equal(CommandStatus.Rescheduled, policy.Evaluate(open, 102).Status);
        Assert.Equal(CommandRejectReason.TooLate, policy.Evaluate(open, 103).RejectReason);
    }
}
