using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneyEconomicalCombatTests
{
    private static JourneyCombatSafety.Observation State(int life, ulong tick = 100, uint hz = 200)
        => new(1, 4, tick, life, 11776, 7, 4, 16, true, true, hz);

    [Theory]
    [InlineData(10008, 15)]
    [InlineData(9400, 38)]
    [InlineData(9261, 40)]
    [InlineData(10221, 25)]
    [InlineData(9827, 29)]
    [InlineData(9636, 28)]
    [InlineData(9599, 24)]
    [InlineData(10158, 28)]
    [InlineData(8634, 48)]
    [InlineData(9960, 16)]
    [InlineData(10626, 72)]
    [InlineData(10112, 15)]
    [InlineData(9962, 35)]
    [InlineData(9058, 76)]
    [InlineData(9565, 50)]
    public void RecordedL4HighLifeUsesNoPotionsRegardlessOfPackActivityOrClockRate(int life, int loss)
    {
        foreach (var hz in new uint[] { 20, 200 }) {
            foreach (var activity in Enum.GetValues<JourneySafetyActivity>()) {
                var safety = new JourneyCombatSafety();
                safety.Observe(State(life + loss, 99, hz));
                var decision = safety.Observe(State(life, hz: hz), activity);
                Assert.False(decision.Drink);
                Assert.InRange(decision.HealingThreshold, .50, .65);
                Assert.True(decision.TakingDamage);
            }
        }
    }

    [Theory]
    [InlineData(4999, 0, true, .50)]
    [InlineData(5000, 0, true, .50)]
    [InlineData(5001, 0, false, .50)]
    [InlineData(6000, 50, false, .50)]
    [InlineData(6000, 100, true, .65)]
    [InlineData(6500, 120, true, .65)]
    [InlineData(6501, 120, false, .65)]
    [InlineData(7499, 2000, false, .65)]
    [InlineData(9000, 1000, false, .65)]
    public void HalfLifeOrTwoThirdsWithThreeNativeSecondsToLive(int life, int loss, bool drink, double threshold)
    {
        var safety = new JourneyCombatSafety();
        safety.Observe(State(life + loss, 99) with { MaximumLife = 10000 });
        var decision = safety.Observe(State(life) with { MaximumLife = 10000 });
        Assert.Equal(drink, decision.Drink);
        Assert.Equal(threshold, decision.HealingThreshold);
    }

    [Theory]
    [InlineData(20U)]
    [InlineData(200U)]
    public void RecordedL6LossNeverHoldsEvenBeforeTheShooterHasBeenIdentified(uint hz)
    {
        var safety = new JourneyCombatSafety(); var positioning = new JourneyCombatPositioning();
        var state = State(6546, hz: hz) with { Level = 6, Nearby = 8, Adjacent = 0, HealingItems = 0 };
        safety.Observe(state);
        foreach (var life in new[] { 5190, 4359, 3804, 3120, 2838, 1612, 1405, 841 }) {
            state = state with { Tick = state.Tick + 20, Life = life };
            // Fresh searches and transient visibility changes cannot authorize even the first hold under fire.
            positioning.Observe(state with { Nearby = 0 }, 90, 76);
            positioning.Observe(state, 90, 76);
            var decision = safety.Observe(state);
            Assert.True(decision.TakingDamage);
            Assert.False(positioning.BeginSearch().TryHold(state, 4, decision.TakingDamage));
            Assert.False(decision.Drink);
        }
    }

    [Theory]
    [InlineData(20U)]
    [InlineData(200U)]
    public async Task DamagedStandWithPendingWalkCancelsTheBlockedPathInsteadOfWaitingUntilDeath(uint hz)
    {
        var safety = new JourneyCombatSafety();
        var state = State(6546, hz: hz) with { Level = 6, Nearby = 8, Adjacent = 0, HealingItems = 0,
            Settled = false, InterruptibleWalk = true };
        safety.Observe(state);
        state = state with { Tick = 120, Life = 5190 };
        var cancels = 0; var frames = 0;
        var move = new CommandResult { ClientSequence = 192, AppliedTick = 100, Status = CommandStatus.Accepted };
        var receipt = await JourneyCombatSafety.PreserveReceiptAsync(() => Task.FromResult(move), () =>
            safety.WaitForSettlementAsync(() => state, () => {
                frames++;
                // The replacement is still required to reach its authoritative boundary before replanning.
                state = state with { Tick = state.Tick + 20, CommandsAdmitted = true,
                    Settled = cancels > 0, InterruptibleWalk = cancels == 0 };
                return Task.CompletedTask;
            }, () => throw new InvalidOperationException("No potion remains."), _ => frames >= 3, "Approach under fire", () => {
                Assert.True(state.CommandsAdmitted);
                cancels++;
                state = state with { CommandsAdmitted = false };
                return Task.CompletedTask;
            }));
        Assert.Same(move, receipt);
        Assert.Equal(1, cancels);
        Assert.Equal(2, frames);
        Assert.True(state.Settled);
        Assert.Equal(5190, state.Life);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task CancellationDoesNotOvertakeAPendingCommandInterruptAnAnimationOrInventDamage(
        bool admitted, bool interruptible, bool damaged)
    {
        var safety = new JourneyCombatSafety();
        var state = State(6546) with { HealingItems = 0, Settled = false };
        safety.Observe(state);
        state = state with { Tick = 101, Life = damaged ? 5190 : 6546, CommandsAdmitted = admitted, InterruptibleWalk = interruptible };
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => safety.WaitForSettlementAsync(() => state,
            () => { frames++; return Task.CompletedTask; }, () => throw new InvalidOperationException("No potion remains."),
            _ => frames >= 2, "Unsettled command", () => throw new InvalidOperationException("Unauthorized cancellation.")));
        Assert.Contains("Unsettled command did not settle", error.Message);
    }

    [Fact]
    public void NewDamageEndsAHoldImmediatelyAndHealingCannotEraseThatPressure()
    {
        var safety = new JourneyCombatSafety(); var positioning = new JourneyCombatPositioning();
        var state = State(9000) with { Adjacent = 0, HealingItems = 0 };
        safety.Observe(state);
        Assert.True(positioning.ShouldHold(state, 5, safety.Observe(state).TakingDamage));
        state = state with { Tick = 101, Life = 8500 };
        Assert.False(positioning.ShouldHold(state, 4, safety.Observe(state).TakingDamage));
        state = state with { Tick = 102, Life = 9000 };
        Assert.True(safety.Observe(state).TakingDamage);
        Assert.False(positioning.BeginSearch().TryHold(state, 3, safety.Observe(state).TakingDamage));
        Assert.False(safety.Observe(state with { Tick = 142 }).TakingDamage);
    }
}
