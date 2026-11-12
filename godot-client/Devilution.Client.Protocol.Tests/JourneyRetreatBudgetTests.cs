using Devilution.Client.Acceptance;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneyRetreatBudgetTests
{
    private static JourneyCombatSafety.Observation State(int life = 6800, int potions = 3, ulong tick = 100)
        => new(1, 4, tick, life, 10000, 3, 3, potions, true, true, 200);

    [Theory]
    [InlineData(5000, false, true)]
    [InlineData(5001, false, false)]
    [InlineData(6400, false, false)]
    [InlineData(6400, true, true)]
    [InlineData(6500, true, true)]
    [InlineData(6501, true, false)]
    [InlineData(6800, false, false)]
    [InlineData(6800, true, false)]
    [InlineData(6999, false, false)]
    public void AvailablePotionsNeverRequireRetreatWhileDrinkingIsForbidden(int life, bool burst, bool drink)
    {
        foreach (var hz in new uint[] { 20, 200 }) {
            var safety = new JourneyCombatSafety();
            var state = State(life) with { TickRateHz = hz };
            if (burst) safety.Observe(state with { Tick = 99, Life = 10000 });
            var decision = safety.Observe(state);
            Assert.Equal(drink, decision.Drink);
            Assert.Equal(drink, decision.Retreat); // Three adjacent pursuers: retreat and quick-use share the threshold.
            Assert.InRange(decision.HealingThreshold, .50, .65);
        }
    }

    [Fact]
    public async Task ReapproachingPursuersAtSixtyEightPercentSpendsAttacksInsteadOfFreeKitingCycles()
    {
        var safety = new JourneyCombatSafety(); var state = State();
        var attacks = 0; var moves = 0; var potions = 0;
        for (var attempt = 0; attempt < 4; attempt++) {
            for (var approach = 0; approach < 8; approach++) {
                await safety.CheckpointAsync(JourneySafetyActivity.Combat, () => state, () => {
                    potions++;
                    state = state with { Life = 10000, HealingItems = state.HealingItems - 1 };
                    return Task.CompletedTask;
                });
                if (!safety.Observe(state).Retreat) { attacks++; break; }
                safety.RequireRetreatAvailable(state);
                moves++;
                safety.RecordRetreat(state);
                state = state with { Tick = state.Tick + 1 };
            }
        }
        Assert.Equal(4, attacks);
        Assert.Equal(0, moves);
        Assert.Equal(0, potions);
        Assert.Equal(3, state.HealingItems);
    }

    [Theory]
    [InlineData(3000, 0)]
    [InlineData(3000, 3)]
    [InlineData(6800, 0)]
    [InlineData(6800, 3)]
    [InlineData(9000, 0)]
    [InlineData(9000, 3)]
    public void EveryCompletedRetreatCountsRegardlessOfLifeOrRemainingPotions(int life, int potions)
    {
        var budget = new JourneyCombatSafety.RetreatBudget(3); var state = State(life, potions);
        budget.Charge(state);
        Assert.Equal(1, budget.Used);
        budget.Charge(state);
        Assert.Equal(2, budget.Used);
        var error = Assert.Throws<InvalidOperationException>(() => budget.Charge(state));
        Assert.Equal(3, budget.Used);
        Assert.Contains("cannot disengage", error.Message);
        Assert.Contains("3/3 retreat steps", error.Message);
    }

    [Fact]
    public void KillingATargetHealingOrVisitingTownCannotRefillTheSameFloorsBudget()
    {
        var safety = new JourneyCombatSafety(); var state = State();
        for (var step = 0; step < 3; step++) safety.RecordRetreat(state);
        safety.Observe(state with { Level = 0, Tick = 110, Life = 10000 });
        state = state with { Tick = 120, Life = 9000, HealingItems = 16 };
        safety.Observe(state); // A new target/approach on the same floor still owns its previous work.
        for (var step = 0; step < 4; step++) safety.RecordRetreat(state);
        var error = Assert.Throws<InvalidOperationException>(() => safety.RecordRetreat(state));
        Assert.Contains("8/8 retreat steps", error.Message);
        Assert.Contains("potions=16", error.Message);
    }

    [Fact]
    public void CatchingExhaustionCannotDispatchAnotherRetreatOnTheNextClearingAttempt()
    {
        var safety = new JourneyCombatSafety(); var state = State();
        var physicalSteps = 0;
        for (var attempt = 0; attempt < 4; attempt++) {
            var error = Assert.Throws<InvalidOperationException>(() => {
                for (var step = 0; step < 8; step++) {
                    safety.RequireRetreatAvailable(state);
                    physicalSteps++; // The accepted real-control retreat or cover step.
                    safety.RecordRetreat(state);
                }
            });
            Assert.Contains("8/8 retreat steps", error.Message);
        }
        Assert.Equal(8, physicalSteps);
    }

    [Fact]
    public void BudgetsBelongToEachPlayerAndFloorAndOnlyExplicitPhaseResetClearsThem()
    {
        var safety = new JourneyCombatSafety(); var state = State();
        for (var step = 0; step < 7; step++) safety.RecordRetreat(state);
        safety.RecordRetreat(state with { Player = 2 });
        safety.RecordRetreat(state with { Level = 5 });
        Assert.Throws<InvalidOperationException>(() => safety.RecordRetreat(state));
        safety.RequireRetreatAvailable(state with { Player = 2 });
        safety.RequireRetreatAvailable(state with { Level = 5 });
        safety.Reset();
        safety.RequireRetreatAvailable(state);
        safety.RecordRetreat(state);
    }
}
