using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class HeldAttackInputTests
{
    private const uint Target = 42;
    private static HeldEntityAttackObservation Idle => new(
        1, 2, true, true, true, 0, 1, 100, 3, 5,
        PlayerCombatMode.Stand, false, PlayerActionKind.Unspecified, false, 0, 0, 0);

    private static HeldEntityAttackObservation Ready(PlayerCombatMode mode) => Idle with {
        Tick = 15, CombatMode = mode, HasCurrentAction = true,
        CurrentActionKind = PlayerActionKind.Attack, ActionEffectApplied = true,
        ActionStartedAtTick = 10, ActionReadyTick = 15, ActionTargetEntityId = Target,
    };
    private static CommandResult Accepted(ulong sequence, ulong applied = 10) => new() {
        ClientSequence = sequence, Status = CommandStatus.Accepted, AppliedTick = applied,
    };
    private static HeldEntityAttackController Begin(HeldEntityAttackObservation? before = null)
    {
        var controller = new HeldEntityAttackController();
        Assert.True(controller.Begin(1, 2, 1, Target, before ?? Idle));
        return controller;
    }

    [Fact]
    public void LegacyConnectionKeepsSingleClickBehavior()
    {
        var controller = new HeldEntityAttackController();
        Assert.False(controller.Begin(1, 2, 1, Target, Idle with { SupportsQueuedRangedAttacks = false }));
        controller.ObserveResult(Accepted(1));
        Assert.False(controller.TryPrepareFollowUp(Ready(PlayerCombatMode.RangedAttack), Target, out _));
    }

    [Theory]
    [InlineData(PlayerCombatMode.Attack)]
    [InlineData(PlayerCombatMode.RangedAttack)]
    public void ExactReceiptAndAuthoritativeReadyAllowOnlyOneNextInput(PlayerCombatMode mode)
    {
        var controller = Begin();
        var ready = Ready(mode);
        controller.ObserveResult(Accepted(99));
        Assert.False(controller.TryPrepareFollowUp(ready, Target, out _));
        controller.ObserveResult(Accepted(1));
        Assert.False(controller.TryPrepareFollowUp(ready with { Tick = 14 }, Target, out _));
        Assert.False(controller.TryPrepareFollowUp(ready with { ActionEffectApplied = false }, Target, out _));
        Assert.False(controller.TryPrepareFollowUp(ready with { PendingCommandCount = 1 }, Target, out _));
        Assert.True(controller.TryPrepareFollowUp(ready, Target, out var input));
        Assert.Equal(Target, input.TargetEntityId);
        controller.RegisterQueued(input, 2);
        Assert.False(controller.TryPrepareFollowUp(ready, Target, out _));
        controller.ObserveResult(Accepted(2, 16));
        Assert.False(controller.TryPrepareFollowUp(ready with { Tick = 20 }, Target, out _));
        Assert.True(controller.TryPrepareFollowUp(ready with {
            Tick = 22, ActionStartedAtTick = 16, ActionReadyTick = 22,
        }, Target, out _));
    }

    [Fact]
    public void ReleaseIsNotRearmedByLateAcceptedOrDuplicateResults()
    {
        var controller = Begin();
        controller.Release();
        controller.ObserveResult(Accepted(1));
        controller.ObserveResult(new CommandResult {
            ClientSequence = 1, Status = CommandStatus.Duplicate, AppliedTick = 10,
        });
        Assert.False(controller.TryPrepareFollowUp(Ready(PlayerCombatMode.RangedAttack), Target, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void LifecycleLossRequiresANewPress(int loss)
    {
        var controller = Begin();
        var ready = Ready(PlayerCombatMode.RangedAttack);
        var changed = loss switch {
            0 => ready with { SessionGeneration = 2 },
            1 => ready with { ConnectionAttemptEpoch = 3 },
            2 => ready with { PlayerEntityId = 2 },
            3 => ready with { LevelId = 4 },
            4 => ready with { PlayerLife = 0 },
            5 => ready with { Connected = false },
            6 => ready with { SupportsQueuedRangedAttacks = false },
            _ => ready with { RightButtonHeld = false },
        };
        Assert.False(controller.TryPrepareFollowUp(changed, Target, out _));
        controller.ObserveResult(Accepted(1));
        Assert.False(controller.TryPrepareFollowUp(ready, Target, out _));
    }

    [Fact]
    public void RecoveryAndUnrelatedHealingReceiptDoNotReleaseOutstandingAction()
    {
        var controller = Begin(Ready(PlayerCombatMode.RangedAttack) with {
            Tick = 5, ActionStartedAtTick = 1, ActionReadyTick = 8,
        });
        controller.ObserveResult(Accepted(1));
        controller.ObserveResult(Accepted(2, 12));
        Assert.False(controller.TryPrepareFollowUp(Idle with {
            Tick = 12, CombatMode = PlayerCombatMode.GotHit,
        }, Target, out _));
        Assert.False(controller.TryPrepareFollowUp(Ready(PlayerCombatMode.RangedAttack) with {
            Tick = 20, ActionStartedAtTick = 1,
        }, Target, out _));
    }

    [Fact]
    public void CursorLossCannotInventAnAttackTarget()
    {
        var controller = Begin();
        controller.ObserveResult(Accepted(1));
        var ready = Ready(PlayerCombatMode.RangedAttack);
        Assert.False(controller.TryPrepareFollowUp(ready, null, out _));
        Assert.True(controller.TryPrepareFollowUp(ready, 43, out var input));
        Assert.Equal(43U, input.TargetEntityId);
    }

    [Fact]
    public void RejectedAttackStopsFurtherRepeats()
    {
        var controller = Begin();
        controller.ObserveResult(new CommandResult {
            ClientSequence = 1, Status = CommandStatus.Rejected,
            RejectReason = CommandRejectReason.NotAllowed, AppliedTick = 10,
        });
        controller.ObserveResult(Accepted(1));
        Assert.False(controller.TryPrepareFollowUp(Ready(PlayerCombatMode.RangedAttack), Target, out _));
    }
}
