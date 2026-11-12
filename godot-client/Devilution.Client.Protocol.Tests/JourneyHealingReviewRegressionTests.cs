using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class JourneyHealingReviewRegressionTests
{
    private static JourneyCombatSafety.Observation State(ulong tick = 100, int life = 9900)
        => new(1, 4, tick, life, 10000, 7, 0, 10, true, false, 200,
            InterruptibleWalk: true, PositionX: 36, PositionY: 53);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaypointOwnsItsExactAdmittedReceiptAfterHealingReplacesTheRealModelPair(bool retried)
    {
        const ulong root = 40;
        var wire = retried ? 42UL : root;
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot { Tick = 120, Players = {
            new PlayerSnapshot { EntityId = 1, LevelId = 4, Life = 4900, LifeMaximum = 10000,
                PositionX = 37, PositionY = 53, CombatMode = PlayerCombatMode.GotHit, CombatModeEndTick = 140 },
        } };
        model.Apply(new(Snapshot: snapshot));
        var move = new CommandResult { ClientSequence = wire, AppliedTick = 120, ServerReceiptSequence = 12, Status = CommandStatus.Accepted };
        model.Apply(new(Acknowledgement: new CommandAck { Results = { move } }) {
            AcknowledgedCommands = [new(wire, 120, Command.IntentOneofCase.MoveRequested) { IntentSequence = root }],
        });
        var queued = wire;
        var queuedIntent = root;
        AcceptanceCommandBoundary.Observation Boundary()
            => new(model.Snapshot, model.LastCommandResult, queued, 0, model.LastCommandIntentSequence);
        var safety = new JourneyCombatSafety();
        var preserved = await AcceptanceCommandBoundary.WaypointAsync(() => JourneyCombatSafety.PreserveReceiptAsync(
            () => Task.FromResult(AcceptanceCommandBoundary.AppliedIntentReceipt(Boundary(), queuedIntent, root, "Object-art waypoint")!),
            () => safety.WaitForSettlementAsync(() => {
                var player = model.Snapshot!.Players[0];
                return State(model.Snapshot.Tick, player.Life) with { CommandsAdmitted = Boundary().CommandsAdmitted,
                    Settled = AuthoritativePlayerSettlement.IsSettled(player, 0), InterruptibleWalk = false,
                    HitRecovery = player.CombatMode == PlayerCombatMode.GotHit };
            }, () => {
                var body = model.Snapshot!.Clone(); body.Tick++;
                body.Players[0].CombatMode = PlayerCombatMode.Stand; body.Players[0].CombatModeEndTick = 0;
                model.Apply(new(Snapshot: body));
                return Task.CompletedTask;
            }, () => {
                var body = model.Snapshot!.Clone(); body.Tick++; body.Players[0].Life = 8000;
                model.Apply(new(Snapshot: body));
                queued = wire + 1; queuedIntent = queued;
                model.Apply(new(Acknowledgement: new CommandAck { Results = { new CommandResult {
                    ClientSequence = queued, AppliedTick = body.Tick, ServerReceiptSequence = 13, Status = CommandStatus.Accepted,
                } } }) { AcknowledgedCommands = [new(queued, body.Tick, Command.IntentOneofCase.UseConsumableRequested) { IntentSequence = queued }] });
                return Task.CompletedTask;
            }, state => state.Tick >= 124, "Object-art waypoint settlement")), Boundary, 37, 53, "Object-art waypoint");
        Assert.Equal(wire, preserved.ClientSequence);
        Assert.Equal(120UL, preserved.AppliedTick);
        Assert.True(AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(preserved));
        Assert.Equal((37, 53), (model.Snapshot!.Players[0].PositionX, model.Snapshot.Players[0].PositionY));
        Assert.NotEqual(preserved.ClientSequence, model.LastCommandResult!.ClientSequence);
        // This was the old waypoint assertion: the potion's current intent cannot identify the movement.
        Assert.False(AuthoritativeCommandOutcome.IsForIntent(model.LastCommandResult, model.LastCommandIntentSequence, root));
    }

    [Fact]
    public async Task HealingOnlyRangedYieldMustSettleAgainBeforeANonQueuedAttack()
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: 4900) with { HitRecovery = true, InterruptibleWalk = false };
        var frames = 0; var heals = 0; var queuedChecks = 0; var settled = false;
        var tile = await JourneyCombatSafety.FinishRangedApproachAsync(() => {
            heals++;
            state = state with { Life = 9000, HealingItems = 9 };
            Assert.True(state.HitRecovery); // Quick-use does not finish GotHit.
            return Task.CompletedTask;
        }, () => { queuedChecks++; return null; }, async () => {
            await safety.WaitForSettlementAsync(() => state, () => {
                frames++;
                state = state with { Tick = state.Tick + 5, HitRecovery = frames < 3, Settled = frames == 3 };
                return Task.CompletedTask;
            }, () => throw new InvalidOperationException("Spent another potion unnecessarily."),
                _ => frames >= 4, "Ranged approach after potion");
            settled = true;
        });
        Assert.Null(tile);
        Assert.Equal(1, heals);
        Assert.Equal(1, queuedChecks);
        Assert.Equal(3, frames);
        Assert.True(settled);
        Assert.True(state.Settled);
        Assert.False(state.HitRecovery); // An ordinary ranged attack is now legal.
    }

    [Fact]
    public async Task AStillLegalQueuedShotKeepsTheNativeMovingShotInterruption()
    {
        var healed = false;
        var tile = await JourneyCombatSafety.FinishRangedApproachAsync(() => {
            healed = true; return Task.CompletedTask;
        }, () => {
            Assert.True(healed); // Eligibility must be checked again after quick-use.
            return (42, 53);
        }, () => throw new InvalidOperationException("Stopped an eligible native queued shot."));
        Assert.Equal((42, 53), tile);
    }

    [Fact]
    public async Task AFailedRenewedSettlementCannotFallThroughToAnAttack()
    {
        var failure = new InvalidOperationException("The original approach deadline expired.");
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => JourneyCombatSafety.FinishRangedApproachAsync(
            () => Task.CompletedTask, () => null, () => throw failure));
        Assert.Same(failure, observed);
    }

    [Theory]
    [InlineData(20U)]
    [InlineData(200U)]
    public async Task RepeatedHitsCannotRenewAStationaryWalksStallTimer(uint hz)
    {
        var safety = new JourneyCombatSafety();
        var state = State() with { TickRateHz = hz };
        safety.Observe(state with { Tick = 99, Life = 10000 });
        var frames = 0; var cancels = 0; ulong canceledAt = 0;
        await safety.WaitForSettlementAsync(() => state, () => {
            frames++;
            var hit = frames % 2 == 1;
            state = state with { Tick = state.Tick + 5, Life = state.Life - 10, HitRecovery = hit,
                HitRecoveryEndTick = hit ? state.Tick + 9 : 0, InterruptibleWalk = !hit && cancels == 0,
                CommandsAdmitted = true, Settled = cancels > 0 && !hit };
            return Task.CompletedTask;
        }, () => throw new InvalidOperationException("Burned a healthy potion instead of detecting the stall."),
            _ => frames >= 12, "Walk under repeated hit recovery", () => {
                Assert.False(state.HitRecovery);
                Assert.True(state.InterruptibleWalk);
                cancels++; canceledAt = state.Tick;
                state = state with { CommandsAdmitted = false };
                return Task.CompletedTask;
            });
        Assert.Equal(1, cancels);
        Assert.Equal(120UL, canceledAt);
        Assert.Equal(10, state.HealingItems);
        Assert.Equal((36, 53), (state.PositionX, state.PositionY));
    }

    [Theory]
    [InlineData(7000)]
    [InlineData(7500)]
    [InlineData(8500)]
    public void PotionsDoNotMakeAHealthyWarriorRetreatEarlierThanTheOldSeventyPercentBoundary(int life)
    {
        var safety = new JourneyCombatSafety();
        var state = State(life: life) with { Adjacent = 4 };
        safety.Observe(state with { Tick = 99, Life = 10000 });
        Assert.False(safety.Observe(state).Retreat);
        Assert.False(safety.Observe(state with { Tick = 101, Life = 6900 }).Retreat);
        var critical = safety.Observe(state with { Tick = 102, Life = 4900 });
        Assert.True(critical.Drink);
        Assert.True(critical.Retreat);
    }

    [Theory]
    [InlineData(CommandStatus.Rejected, 37, 53)]
    [InlineData(CommandStatus.Accepted, 38, 53)]
    [InlineData(CommandStatus.Accepted, 37, 54)]
    public async Task AnOwnedWaypointStillRejectsAFailedMoveOrTheWrongEndpoint(CommandStatus status, int x, int y)
    {
        var snapshot = new Snapshot { Tick = 120, Players = { new PlayerSnapshot {
            EntityId = 1, Life = 9000, LifeMaximum = 10000, PositionX = x, PositionY = y,
        } } };
        var move = new CommandResult { ClientSequence = 40, AppliedTick = 120, Status = status };
        await Assert.ThrowsAsync<InvalidOperationException>(() => AcceptanceCommandBoundary.WaypointAsync(
            () => Task.FromResult(move), () => new(snapshot, move, 40, 0), 37, 53, "Object-art waypoint"));
    }
}
