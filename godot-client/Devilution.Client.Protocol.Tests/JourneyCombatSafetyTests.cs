using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneyCombatSafetyTests
{
    private static JourneyCombatSafety.Observation State(int life = 10000, ulong tick = 100,
        int nearby = 1, int adjacent = 1, int potions = 6, bool admitted = true, bool settled = true, uint hz = 20)
        => new(1, 5, tick, life, 10000, nearby, adjacent, potions, admitted, settled, hz);

    [Fact]
    public void ASevenMonsterPackDoesNotSpendAPotionAboveTwoThirdsLife()
    {
        var state = State(life: 9200, nearby: 7, adjacent: 4);
        var safety = new JourneyCombatSafety();
        safety.Observe(state with { Tick = state.Tick - 1, Life = state.MaximumLife });
        var decision = safety.Observe(state);
        Assert.True(state.Life > state.MaximumLife * 2 / 3);
        Assert.False(decision.Drink);
        Assert.True(decision.StopWalking);
        Assert.Equal(.65, decision.HealingThreshold);
    }

    [Fact]
    public void RecentLifeLossRaisesTheThresholdEvenAgainstOneMonster()
    {
        var safety = new JourneyCombatSafety();
        safety.Observe(State());
        var burst = State(life: 6000, tick: 101);
        Assert.False(new JourneyCombatSafety().Observe(burst).Drink);
        Assert.True(safety.Observe(burst).Drink);
    }

    [Fact]
    public void AcceleratedTicksAndRoundTripDoNotSpendPotionsAtHigherLife()
    {
        var ordinary = new JourneyCombatSafety(); var accelerated = new JourneyCombatSafety();
        ordinary.Observe(State()); accelerated.Observe(State(hz: 200));
        Assert.True(ordinary.Observe(State(life: 6000, tick: 110)).Drink);
        Assert.True(accelerated.Observe(State(life: 6000, tick: 110, hz: 200)).Drink);
        var local = new JourneyCombatSafety(); var distant = new JourneyCombatSafety();
        local.Observe(State()); distant.Observe(State());
        Assert.False(local.Observe(State(life: 8800, tick: 105)).Drink);
        Assert.False(distant.Observe(State(life: 8800, tick: 105) with { RoundTripMilliseconds = 250 }).Drink);
    }

    [Fact]
    public void DuplicateObservationsDoNotInventDamageAndOldLossAgesOut()
    {
        var safety = new JourneyCombatSafety(); safety.Observe(State());
        var burst = State(life: 9000, tick: 101);
        var first = safety.Observe(burst);
        for (var frame = 0; frame < 100; frame++) Assert.Equal(first, safety.Observe(burst));
        var quiet = safety.Observe(burst with { Tick = 142 });
        Assert.Equal(0, quiet.LossPerTick);
        Assert.False(quiet.Drink);
    }

    [Theory]
    [InlineData(2U, 5U, 10000, 102UL)]
    [InlineData(1U, 6U, 10000, 102UL)]
    [InlineData(1U, 5U, 12000, 102UL)]
    [InlineData(1U, 5U, 10000, 1UL)]
    public void IdentityLevelMaximumOrClockChangesResetTheDamageHistory(uint player, uint level, int maximum, ulong tick)
    {
        var safety = new JourneyCombatSafety(); safety.Observe(State());
        safety.Observe(State(life: 9000, tick: 101));
        var next = State(tick: tick) with { Player = player, Level = level, MaximumLife = maximum, Life = maximum };
        Assert.Equal(0, safety.Observe(next).LossPerTick);
    }

    [Fact]
    public void TownAndFullLifeDoNotWastePotionsAndExhaustionRequestsRetreat()
    {
        var safety = new JourneyCombatSafety();
        Assert.False(safety.Observe(State(nearby: 7, adjacent: 7)).Drink);
        Assert.False(safety.Observe(State(life: 1000) with { Level = 0 }).Drink);
        var exhausted = safety.Observe(State(life: 3000, potions: 0, nearby: 7, adjacent: 4));
        Assert.False(exhausted.Drink);
        Assert.True(exhausted.Retreat);
    }

    [Theory]
    [InlineData((int)JourneySafetyActivity.Travel, true)]
    [InlineData((int)JourneySafetyActivity.ObjectApproach, true)]
    [InlineData((int)JourneySafetyActivity.Stairs, true)]
    [InlineData((int)JourneySafetyActivity.Combat, false)]
    [InlineData((int)JourneySafetyActivity.Retreat, false)]
    public async Task EveryLoopUsesTheSamePolicyAndReobservesAfterRealConsumption(int kind, bool defend)
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: 4900, nearby: 7, adjacent: 4);
        var actions = new List<string>();
        await safety.CheckpointAsync((JourneySafetyActivity)kind, () => state, () => {
            actions.Add("real potion control");
            state = state with { Tick = state.Tick + 1, Life = 10000, HealingItems = state.HealingItems - 1 };
            return Task.CompletedTask;
        }, () => {
            Assert.Equal(10000, state.Life);
            actions.Add("fight or safer retreat");
            state = state with { Nearby = 0, Adjacent = 0 };
            return Task.CompletedTask;
        });
        Assert.Equal(defend ? new[] { "real potion control", "fight or safer retreat" } : ["real potion control"], actions);
    }

    [Fact]
    public async Task PendingReceiptPreventsASecondCommandAndDeathFailsBeforeAnyInput()
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: 1000, admitted: false);
        Assert.False(await safety.CheckpointAsync(JourneySafetyActivity.Combat, () => state,
            () => throw new InvalidOperationException("Overtook the pending receipt.")));
        state = state with { Life = 0 };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => safety.CheckpointAsync(
            JourneySafetyActivity.Travel, () => state, () => throw new InvalidOperationException("Healed a dead character.")));
        Assert.Contains("character died", error.Message);
        Assert.Contains("level=5", error.Message);
        Assert.Contains("tick 100", error.Message);
    }

    [Fact]
    public async Task HitRecoveryCanDrinkBeforeStandWithoutLosingTheAttacksReceipt()
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: 4900, settled: false);
        var latest = new CommandResult { ClientSequence = 40, AppliedTick = 100, Status = CommandStatus.Accepted };
        var drinks = 0; var frames = 0;
        var attack = await JourneyCombatSafety.PreserveReceiptAsync(() => Task.FromResult(latest),
            () => safety.WaitForSettlementAsync(() => state, () => {
                frames++;
                state = state with { Tick = state.Tick + 1, Settled = true };
                return Task.CompletedTask;
            }, () => {
                Assert.False(state.Settled);
                drinks++;
                latest = new CommandResult { ClientSequence = 41, AppliedTick = 101, Status = CommandStatus.Accepted };
                state = state with { Tick = 101, Life = 10000, HealingItems = 5 };
                return Task.CompletedTask;
            }, _ => frames >= 3, "Between native melee swings"));
        Assert.Equal(1, drinks);
        Assert.Equal(40UL, attack.ClientSequence);
        Assert.Equal(41UL, latest.ClientSequence);
    }

    [Theory]
    [InlineData(CommandStatus.Accepted)]
    [InlineData(CommandStatus.Rejected)]
    public async Task ObjectApproachRetainsItsMovementOutcomeWhileHealingDuringHitRecovery(CommandStatus status)
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: 4900, nearby: 3, settled: false);
        var move = new CommandResult { ClientSequence = 50, AppliedTick = 100, Status = status };
        var latest = move;
        var frames = 0;
        var result = await JourneyCombatSafety.PreserveReceiptAsync(() => Task.FromResult(latest),
            () => safety.WaitForSettlementAsync(() => state, () => {
                frames++;
                state = state with { Tick = state.Tick + 1, Settled = true };
                return Task.CompletedTask;
            }, () => {
                Assert.False(state.Settled);
                latest = new CommandResult { ClientSequence = 51, AppliedTick = 101, Status = CommandStatus.Accepted };
                state = state with { Tick = 101, Life = 10000, HealingItems = 5 };
                return Task.CompletedTask;
            }, _ => frames > 2, "Cardinal native object approach"));
        Assert.Same(move, result);
        Assert.Equal(status, result.Status);
        Assert.Equal(51UL, latest.ClientSequence);
    }

    [Fact]
    public async Task ASettledDeathCannotPassAndAnUnendingActionStillHasABound()
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => safety.WaitForSettlementAsync(() => state,
            () => throw new InvalidOperationException("Pumped after death"), () => Task.CompletedTask, _ => false, "Fight"));
        state = State(settled: false);
        var frames = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => safety.WaitForSettlementAsync(() => state,
            () => { frames++; return Task.CompletedTask; }, () => Task.CompletedTask, _ => frames == 2, "Fight"));
        Assert.Contains("Fight did not settle", error.Message);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task TravelStopsBeforeHealingAndDoesNotRestartACompletedStairTransition(bool running, bool arrived)
    {
        var calls = new List<string>();
        var level = 5U;
        var drained = false;
        await JourneyCombatSafety.ProtectRouteAsync(5, () => level, () => running,
            () => { calls.Add("cancel real travel"); return Task.CompletedTask; },
            () => { drained = true; calls.Add("admitted boundary"); return Task.CompletedTask; },
            () => {
                Assert.True(drained);
                calls.Add("potion and defense");
                if (arrived) level = 6;
                return Task.CompletedTask;
            },
            () => { calls.Add("settle"); return Task.CompletedTask; },
            () => { calls.Add("resume selected route"); return Task.CompletedTask; });
        Assert.Equal(running, calls.Contains("cancel real travel"));
        Assert.Equal(!arrived, calls.Contains("resume selected route"));
        Assert.True(calls.IndexOf("admitted boundary") < calls.IndexOf("potion and defense"));
        Assert.True(calls.IndexOf("potion and defense") < calls.IndexOf("settle"));
    }

    [Fact]
    public async Task ArrivalWhileCancelDrainsDoesNotFightOnTheNewFloorBeforeItsEntryEvidence()
    {
        var level = 5U; var settled = false;
        await JourneyCombatSafety.ProtectRouteAsync(5, () => level, () => true, () => Task.CompletedTask,
            () => { level = 6; return Task.CompletedTask; },
            () => throw new InvalidOperationException("Mutated the destination before arrival evidence."),
            () => { settled = true; return Task.CompletedTask; },
            () => throw new InvalidOperationException("Restarted the old route."));
        Assert.True(settled);
    }

    [Theory]
    [InlineData((int)JourneySafetyActivity.Stairs)]
    [InlineData((int)JourneySafetyActivity.Retreat)]
    [InlineData((int)JourneySafetyActivity.ObjectApproach)]
    public void QuietStairsObjectsAndRetreatUseTheSameEconomicalThreshold(int activity)
    {
        var safety = new JourneyCombatSafety();
        var quiet = State(life: 8000, nearby: 0, adjacent: 0);
        Assert.False(safety.Observe(quiet).Drink);
        Assert.False(safety.Observe(quiet, (JourneySafetyActivity)activity).Drink);
        Assert.True(safety.Observe(quiet with { Life = 4900 }, (JourneySafetyActivity)activity).Drink);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public void PursuitWithoutPotionsCannotResetTheFloorRetreatBudget(int stepsPerAttempt)
    {
        var budget = new JourneyCombatSafety.RetreatBudget(8);
        var state = State(life: 3000, potions: 0);
        var attempts = 0;
        var attacks = 0;
        var error = Assert.Throws<InvalidOperationException>(() => {
            while (attacks == 0) {
                attempts++;
                // A fresh target/approach loop still shares the floor's retreat work.
                for (var approaches = 0; approaches < stepsPerAttempt; approaches++)
                    budget.Charge(state with { Tick = state.Tick + (ulong)budget.Used });
                if (attempts > 8) throw new InvalidOperationException("The pursuit loop never charged its retreats.");
            }
        });
        Assert.Equal(8, budget.Used);
        Assert.Equal(8 / stepsPerAttempt, attempts);
        Assert.Equal(0, attacks);
        Assert.Contains("out of potions", error.Message);
        Assert.Contains("cannot disengage", error.Message);
        Assert.Contains("L5", error.Message);
    }

    [Theory]
    [InlineData(8000, false)]
    [InlineData(9000, true)]
    [InlineData(0, false)]
    public void QuietDungeonPotionMustRestoreLifeEvenWithNearbyAttackers(int after, bool valid)
    {
        var safety = new JourneyCombatSafety();
        safety.Observe(State(life: 8000, nearby: 7));
        var baseline = safety.DamageObservations;
        // A dungeon, Attack mode or missed attacks are not evidence of concurrent damage.
        if (after > 0) safety.Observe(State(life: after, tick: 101, nearby: 7));
        Assert.Equal(valid, safety.HealingOutcomeValid(8000, 10000, after, baseline));
    }

    [Fact]
    public void LifeLossDuringQuickUseCanMaskItsNetGainButEarlierDamageCannot()
    {
        var safety = new JourneyCombatSafety();
        safety.Observe(State());
        safety.Observe(State(life: 8000, tick: 101));
        var baseline = safety.DamageObservations;
        safety.Observe(State(life: 8000, tick: 102));
        Assert.False(safety.HealingOutcomeValid(8000, 10000, 8000, baseline));
        safety.Observe(State(life: 6000, tick: 103));
        safety.Observe(State(life: 7500, tick: 104));
        Assert.True(safety.HealingOutcomeValid(8000, 10000, 7500, baseline));
        Assert.False(safety.HealingOutcomeValid(8000, 10000, 0, baseline));
        Assert.False(safety.HealingOutcomeValid(10000, 10000, 10000, baseline));
    }

    [Theory]
    [InlineData(false, 110UL, true)]
    [InlineData(true, 110UL, false)]
    [InlineData(true, 120UL, true)]
    public void OnlyFreshHitRecoveryDuringQuickUseCanMaskThePotionsLifeGain(bool alreadyHit, ulong afterEnd, bool valid)
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: 8000) with { HitRecovery = alreadyHit, HitRecoveryEndTick = alreadyHit ? 110UL : 0 };
        safety.Observe(state);
        var baseline = safety.DamageObservations;
        var after = state with { Tick = 101, HitRecovery = true, HitRecoveryEndTick = afterEnd };
        safety.Observe(after);
        var damage = safety.DamageObservations;
        safety.Observe(after);
        Assert.Equal(damage, safety.DamageObservations);
        Assert.Equal(valid, safety.HealingOutcomeValid(8000, 10000, 8000, baseline));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KillEvidenceUsesThePreservedAttackWhenHealingReplacesTheLatestReceipt(bool duringAttack)
    {
        var attack = new CommandResult { ClientSequence = 60, AppliedTick = 100, Status = CommandStatus.Accepted };
        var latest = attack;
        var preserved = await JourneyCombatSafety.PreserveReceiptAsync(() => Task.FromResult(latest), () => {
            if (duringAttack) latest = new CommandResult { ClientSequence = 61, AppliedTick = 101, Status = CommandStatus.Accepted };
            return Task.CompletedTask;
        });
        // Otherwise the arrow settlement uses a potion after the attack's action boundary.
        if (!duringAttack) latest = new CommandResult { ClientSequence = 61, AppliedTick = 101, Status = CommandStatus.Accepted };
        (uint Target, ulong Sequence)[] shots = [(123U, preserved.ClientSequence), (456U, 62UL)];
        Assert.Equal(60UL, JourneyCombatSafety.KillAttackSequence(123U, shots));
        Assert.NotEqual(latest.ClientSequence, JourneyCombatSafety.KillAttackSequence(123U, shots));
        Assert.Throws<InvalidOperationException>(() => JourneyCombatSafety.KillAttackSequence(789U, shots));
    }

    [Theory]
    [InlineData(CommandStatus.Accepted)]
    [InlineData(CommandStatus.Duplicate)]
    [InlineData(CommandStatus.Rejected)]
    public void JourneyAndHealingBoundariesRequireTheCorrelatedAppliedIntent(CommandStatus status)
    {
        var snapshot = new Snapshot { Tick = 120,
            Players = { new PlayerSnapshot { EntityId = 1, Life = 4000, LifeMaximum = 10000 } } };
        var receipt = new CommandResult { ClientSequence = 3, AppliedTick = 120, Status = status,
            RejectReason = status == CommandStatus.Rejected ? CommandRejectReason.TooLate : CommandRejectReason.Unspecified };
        var state = new AcceptanceCommandBoundary.Observation(snapshot, receipt, 3, 0, ReceiptIntentSequence: 1);
        Assert.Same(receipt, AcceptanceCommandBoundary.AppliedIntentReceipt(state, 1, 1, "Quick-use retry"));
        Assert.Equal(status != CommandStatus.Rejected, AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt));
        Assert.Null(AcceptanceCommandBoundary.AppliedIntentReceipt(state with { PendingCount = 1 }, 1, 1, "Quick-use retry"));
        snapshot.Tick = 119;
        Assert.Null(AcceptanceCommandBoundary.AppliedIntentReceipt(state, 1, 1, "Quick-use retry"));
        snapshot.Tick = 120;
        Assert.Throws<InvalidOperationException>(() => AcceptanceCommandBoundary.AppliedIntentReceipt(
            state with { ReceiptIntentSequence = 2 }, 1, 1, "Unrelated receipt"));
        Assert.Throws<InvalidOperationException>(() => AcceptanceCommandBoundary.AppliedIntentReceipt(state, 2, 1, "Unrelated queued input"));
        snapshot.Players[0].Life = 0;
        Assert.Contains("character died", Assert.Throws<InvalidOperationException>(() =>
            AcceptanceCommandBoundary.AppliedIntentReceipt(state, 1, 1, "Quick-use retry")).Message);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    public async Task RealRetriedWalkAndPotionReceiptsAreValidatedBeforeHealingReplacesTheModelPair(int kind)
    {
        await using var fixture = await TooLateRetryPeer.StartAsync();
        fixture.World.Players[0].Life = 32;
        fixture.World.Players[0].Inventory.Add(TooLateRetryPolicyTests.World().Players[0].Inventory[0].Clone());
        var secondPotion = fixture.World.Players[0].Inventory[0].Clone();
        secondPotion.ItemSeed++;
        secondPotion.State.StableItemId++;
        fixture.World.Players[0].Inventory.Add(secondPotion);
        await fixture.SnapshotAsync();
        await fixture.UntilAsync(() => fixture.Model.Snapshot!.Players[0].Inventory.Count == 2);
        var root = fixture.Client.QueueRealtime(TooLateRetryPolicyTests.Intent(kind), fixture.World.Tick);
        var first = await fixture.NextAsync();
        await fixture.ResultAsync(first);
        var retry = await fixture.NextAsync();
        AcceptanceCommandBoundary.Observation Observe(ulong queued)
            => new(fixture.Model.Snapshot, fixture.Model.LastCommandResult, queued, fixture.Client.PendingCommandCount,
                fixture.Model.LastCommandIntentSequence);
        Assert.Null(AcceptanceCommandBoundary.AppliedIntentReceipt(Observe(retry.ClientSequence), root, root, "Retried journey intent"));
        if (kind == 7) {
            fixture.World.Players[0].Inventory.RemoveAt(0);
            fixture.World.Players[0].Life = 48;
        }
        await fixture.ResultAsync(retry, CommandRejectReason.Unspecified, CommandStatus.Accepted);
        var preserved = await JourneyCombatSafety.PreserveReceiptAsync(async () => {
            await fixture.UntilAsync(() => AcceptanceCommandBoundary.AppliedIntentReceipt(
                Observe(retry.ClientSequence), root, root, "Retried journey intent") is not null);
            return AcceptanceCommandBoundary.AppliedIntentReceipt(Observe(retry.ClientSequence), root, root, "Retried journey intent")!;
        }, async () => {
            // A real later quick-use changes the model pair while the earlier action is settling.
            var potion = fixture.Model.Snapshot!.Players[0].Inventory[0];
            var healing = fixture.Client.QueueRealtime(new() { UseConsumableRequested = new() { Item = new() { StableItemId = potion.State.StableItemId } } },
                fixture.World.Tick);
            var command = await fixture.NextAsync();
            Assert.Equal(healing, command.ClientSequence);
            fixture.World.Players[0].Inventory.RemoveAt(0);
            fixture.World.Players[0].Life += 16;
            await fixture.ResultAsync(command, CommandRejectReason.Unspecified, CommandStatus.Accepted);
            await fixture.UntilAsync(() => fixture.Model.LastCommandResult?.ClientSequence == healing && fixture.Client.PendingCommandCount == 0
                && fixture.Model.Snapshot!.Tick >= fixture.Model.LastCommandResult.AppliedTick);
        });
        Assert.Equal(retry.ClientSequence, preserved.ClientSequence);
        Assert.NotEqual(root, preserved.ClientSequence);
        Assert.NotEqual(preserved.ClientSequence, fixture.Model.LastCommandResult!.ClientSequence);
        Assert.Throws<InvalidOperationException>(() => AcceptanceCommandBoundary.AppliedIntentReceipt(
            Observe(fixture.Model.LastCommandResult.ClientSequence), fixture.Model.LastCommandIntentSequence, root, "Later quick-use"));
        if (kind == 7) {
            Assert.Empty(fixture.Model.Snapshot!.Players[0].Inventory);
            Assert.Equal(64, fixture.Model.Snapshot.Players[0].Life);
        }
    }

    [Theory]
    [InlineData(8923, 5)]
    [InlineData(8984, 7)]
    [InlineData(9204, 6)]
    public void RecordedHealthyUnhealedWarriorsFightInsteadOfFailingRetreat(int life, int attackers)
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: life, potions: 0, nearby: attackers, adjacent: attackers, hz: 200)
            with { MaximumLife = 11776 };
        safety.Observe(state with { Tick = 99, Life = 11776 });
        Assert.False(safety.Observe(state).Retreat); // Even a burst forecast cannot make high-life flight mandatory.
        Assert.True(safety.Observe(state).StopWalking);
        var budget = new JourneyCombatSafety.RetreatBudget(8);
        for (var step = 0; step < 100; step++) budget.Charge(state);
        Assert.Equal(0, budget.Used);
        Assert.True(safety.Observe(state with { Tick = 101, Life = 3500 }).Retreat);
    }

    [Theory]
    [InlineData(11388, false)]
    [InlineData(10000, false)]
    [InlineData(5800, true)]
    public void AStaticPackDoesNotWastePotionsButStillHealsBeforeLowLife(int life, bool drink)
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: life, nearby: 9, adjacent: 8, hz: 200) with { MaximumLife = 11776 };
        Assert.Equal(drink, safety.Observe(state).Drink);
        Assert.Equal(.5, safety.Observe(state).HealingThreshold);
        safety.Reset();
        safety.Observe(state with { Tick = 99, Life = 11776 });
        // Even a measured burst cannot waste most of a potion on a nearly full life bulb.
        Assert.Equal(drink, safety.Observe(state).Drink);
    }
}
