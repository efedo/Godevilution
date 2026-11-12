using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneySettlementSafetyTests
{
    private static JourneyCombatSafety.Observation State(int life, ulong tick = 11160)
        => new(1, 4, tick, life, 11776, 11, 0, 10, true, false, 200, InterruptibleWalk: true, PositionX: 36, PositionY: 53);

    [Fact]
    public async Task RecordedSarcophagusCancellationLoopKeepsHealingInsteadOfDyingWithTenPotions()
    {
        var safety = new JourneyCombatSafety();
        var state = State(10655);
        safety.Observe(state with { Tick = 11159, Life = 11776 });
        var frames = 0; var cancels = 0; var drinks = 0;
        var original = new CommandResult { ClientSequence = 252, AppliedTick = 11160, Status = CommandStatus.Accepted };
        var latest = original;
        var preserved = await JourneyCombatSafety.PreserveReceiptAsync(() => Task.FromResult(original), () =>
            safety.WaitForSettlementAsync(() => state, () => {
                frames++;
                // The observed run alternated accepted moves between (36,53) and (37,53)
                // until death at 12109. Its replacement can remain pending between native steps.
                state = state with { Tick = state.Tick + 25, Life = state.Life - 500,
                    CommandsAdmitted = true, Settled = frames >= 38, InterruptibleWalk = frames < 38 };
                return Task.CompletedTask;
            }, () => {
                Assert.True(state.CommandsAdmitted);
                Assert.True(state.HealingItems > 0);
                drinks++;
                latest = new() { ClientSequence = latest.ClientSequence + 1, AppliedTick = state.Tick, Status = CommandStatus.Accepted };
                state = state with { Life = Math.Min(state.MaximumLife, state.Life + 4864), HealingItems = state.HealingItems - 1 };
                return Task.CompletedTask;
            }, _ => frames > 40, "Required sarcophagus approach", () => {
                Assert.True(state.CommandsAdmitted);
                cancels++;
                latest = new() { ClientSequence = latest.ClientSequence + 1, AppliedTick = state.Tick, Status = CommandStatus.Accepted };
                state = state with { CommandsAdmitted = false };
                return Task.CompletedTask;
            }));
        Assert.Same(original, preserved);
        Assert.NotEqual(original.ClientSequence, latest.ClientSequence);
        Assert.Equal(1, cancels);
        Assert.Equal(3, drinks);
        Assert.Equal(6247, state.Life);
        Assert.Equal(7, state.HealingItems);
        Assert.True(state.Settled);
    }

    [Fact]
    public async Task DuePotionPrecedesTheNestedCancellationReceiptWait()
    {
        var safety = new JourneyCombatSafety();
        var state = State(7000);
        safety.Observe(state);
        state = state with { Tick = state.Tick + 1, Life = 5000 };
        var actions = new List<string>(); var canceled = false; var frames = 0;
        await safety.WaitForSettlementAsync(() => state, () => {
            frames++;
            state = state with { Tick = state.Tick + 20, CommandsAdmitted = true, Settled = canceled };
            return Task.CompletedTask;
        }, () => {
            actions.Add("real potion control");
            state = state with { Life = 10000, HealingItems = 9 };
            return Task.CompletedTask;
        }, _ => frames >= 4, "Approach settlement", () => {
            actions.Add("real cancellation and its receipt");
            canceled = true;
            state = state with { CommandsAdmitted = false };
            return Task.CompletedTask;
        });
        Assert.Equal(new[] { "real potion control", "real cancellation and its receipt" }, actions);
    }

    [Theory]
    [InlineData(20U)]
    [InlineData(200U)]
    public async Task AReplacementWalkCannotCancelItselfOnEveryIdleNativeStep(uint hz)
    {
        var safety = new JourneyCombatSafety();
        var state = State(10000) with { TickRateHz = hz };
        safety.Observe(state);
        state = state with { Tick = state.Tick + 1, Life = 9900 };
        var cancels = 0; var frames = 0;
        await safety.WaitForSettlementAsync(() => state, () => {
            frames++;
            state = state with { Tick = state.Tick + 1, Life = state.Life - 10,
                Settled = frames == 4, InterruptibleWalk = frames < 4 };
            return Task.CompletedTask;
        }, () => throw new InvalidOperationException("Healthy life must conserve potions."),
            _ => frames >= 5, "Replacement walk", () => { cancels++; return Task.CompletedTask; });
        Assert.Equal(0, cancels); // A healthy four-tick native boundary is not a stalled path.
        Assert.Equal(4, frames);
    }

    [Theory]
    [InlineData(20U)]
    [InlineData(200U)]
    public async Task OrdinaryWalkStepsUnderFireAdvanceWithoutCancellationOrNewPotionDebt(uint hz)
    {
        var safety = new JourneyCombatSafety();
        var state = State(10000) with { TickRateHz = hz };
        safety.Observe(state);
        state = state with { Tick = state.Tick + 1, Life = 9900 };
        var frames = 0;
        await safety.WaitForSettlementAsync(() => state, () => {
            frames++;
            state = state with { Tick = state.Tick + 25, Life = state.Life - 100, PositionX = state.PositionX + 1,
                Settled = frames == 8, InterruptibleWalk = frames < 8 };
            return Task.CompletedTask;
        }, () => throw new InvalidOperationException("Wasted a potion during a healthy approach."), _ => frames >= 9,
            "Native walk toward shooter", () => throw new InvalidOperationException("Cancelled a progressing native walk."));
        Assert.Equal(44, state.PositionX);
        Assert.Equal(10, state.HealingItems);
        Assert.Equal(9100, state.Life);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorldObservationWaitsHealInAnActionAfterAdmissionButDoNotOvertakePendingCommands(bool admitted)
    {
        var safety = new JourneyCombatSafety();
        var state = State(5000) with { CommandsAdmitted = admitted, InterruptibleWalk = false };
        var drinks = 0;
        Assert.False(await safety.PollAsync(() => false, () => state, () => {
            Assert.False(state.Settled); // Rendering, release and peer waits cannot require Stand before drinking.
            drinks++;
            state = state with { Life = state.Life + 4864, HealingItems = 9 };
            return Task.CompletedTask;
        }));
        Assert.Equal(admitted ? 1 : 0, drinks);
        Assert.Equal(admitted ? 9864 : 5000, state.Life);
    }

    [Fact]
    public async Task ACompletedObservationKeepsItsEvidenceAndADeathFailsBeforeAnyInput()
    {
        var safety = new JourneyCombatSafety(); var state = State(5000);
        Assert.True(await safety.PollAsync(() => true, () => state,
            () => throw new InvalidOperationException("Changed the completed evidence.")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => safety.PollAsync(() => true,
            () => state with { Life = 0 }, () => throw new InvalidOperationException("Used a potion after death.")));
        Assert.Contains("character died", error.Message);
        Assert.Contains("level=4", error.Message);
    }

    [Theory]
    [InlineData(3, (int)JourneySafetyActivity.Travel)]
    [InlineData(7, (int)JourneySafetyActivity.Travel)]
    [InlineData(11, (int)JourneySafetyActivity.Travel)]
    [InlineData(3, (int)JourneySafetyActivity.ObjectApproach)]
    [InlineData(7, (int)JourneySafetyActivity.ObjectApproach)]
    [InlineData(11, (int)JourneySafetyActivity.ObjectApproach)]
    public async Task AQuietNearbyGroupDoesNotStartAnOptionalRoomClearingFightButItsFirstDamageDoes(int nearby, int activity)
    {
        var safety = new JourneyCombatSafety();
        var state = State(10655) with { Nearby = nearby, Settled = true, InterruptibleWalk = false };
        var fights = 0;
        Task Defend() {
            fights++;
            state = state with { Nearby = 0 };
            return Task.CompletedTask;
        }
        Assert.False(await safety.CheckpointAsync((JourneySafetyActivity)activity, () => state,
            () => throw new InvalidOperationException("Wasted a healthy potion."), Defend));
        Assert.Equal(0, fights);
        // Aiming at the player is enough to stop, even before the first projectile takes life.
        Assert.True(safety.Observe(state with { RangedTarget = 42, RangedAttackers = 1 }).StopWalking);
        state = state with { Tick = state.Tick + 1, Life = 10155 };
        Assert.True(await safety.CheckpointAsync((JourneySafetyActivity)activity, () => state,
            () => throw new InvalidOperationException("Wasted a healthy potion."), Defend));
        Assert.Equal(1, fights);
    }

    [Theory]
    [InlineData(true, false, 5000, true)]
    [InlineData(true, false, 9000, false)]
    [InlineData(true, true, 9000, true)]
    [InlineData(false, false, 5000, false)]
    [InlineData(false, true, 5000, false)]
    public void AQueuedRangedWalkYieldsForDueHealingOnlyAfterItsReceiptIsAdmitted(bool admitted, bool settled, int life, bool ready)
    {
        var state = State(life) with { CommandsAdmitted = admitted, Settled = settled };
        Assert.Equal(ready, new JourneyCombatSafety().MovementWaitReady(state));
    }

    [Fact]
    public async Task AQueuedRangedWalkCanHealDuringHitRecoveryWithoutReplacingItsOwnedMoveReceipt()
    {
        var safety = new JourneyCombatSafety(); var state = State(7000);
        safety.Observe(state);
        state = state with { Tick = state.Tick + 20, Life = 5000, InterruptibleWalk = false, HitRecovery = true };
        var move = new CommandResult { ClientSequence = 260, AppliedTick = state.Tick, Status = CommandStatus.Accepted };
        var latest = move;
        Assert.True(safety.MovementWaitReady(state));
        var preserved = await JourneyCombatSafety.PreserveReceiptAsync(() => Task.FromResult(move), () => {
            latest = new() { ClientSequence = 261, AppliedTick = state.Tick + 10, Status = CommandStatus.Accepted };
            state = state with { Life = 9864, HealingItems = 9 };
            return Task.CompletedTask;
        });
        Assert.Same(move, preserved);
        Assert.Equal(261UL, latest.ClientSequence);
        Assert.False(safety.MovementWaitReady(state)); // Healing does not pretend the walk is already settled.
    }
}
