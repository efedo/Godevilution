using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectApproachSafetyTests
{
    [Fact]
    public async Task ClearApproachAndInRangeOperationEachCheckHealingAtASettledBoundary()
    {
        var state = DoorBoundary();
        var checks = 0;
        var frames = 0;
        async Task Wait() => await AcceptanceCommandBoundary.WaitAsync(() => state, () => {
            frames++;
            state.Snapshot!.Tick++;
            state = state with { PendingCount = 0,
                Receipt = new CommandResult { ClientSequence = state.LastQueuedSequence,
                    AppliedTick = state.Snapshot.Tick, Status = CommandStatus.Accepted } };
            return Task.CompletedTask;
        }, _ => frames >= 4, "Native object preparation");
        Task HealThroughControls()
        {
            Assert.True(state.CommandsAdmitted && state.PlayerSettled);
            checks++;
            // The existing control queues a potion; planning must wait for its own receipt.
            state = state with { LastQueuedSequence = state.LastQueuedSequence + 1, PendingCount = 1 };
            return Task.CompletedTask;
        }

        // Two clear-path steps followed by the operation already in range: no obstruction callback.
        for (var iteration = 0; iteration < 3; iteration++) {
            await AcceptanceCommandBoundary.PrepareObjectAsync(Wait, HealThroughControls);
            Assert.True(state.CommandsAdmitted && state.PlayerSettled);
            Assert.Equal((ulong)(236 + iteration), state.Receipt!.ClientSequence);
        }
        Assert.Equal(3, checks);
        Assert.Equal(3, frames);
    }

    [Theory]
    [InlineData(20U, 7, 3, 0U)]
    [InlineData(200U, 7, 3, 0U)]
    [InlineData(20U, 13, 0, 20U)]
    [InlineData(200U, 13, 0, 20U)]
    public async Task ReachableObjectPreparationHealsWithoutDivertingIntoNearbyRooms(
        uint hz, int nearby, int adjacent, uint shooter)
    {
        // Synthetic floor: a real cardinal route exists despite melee pressure or incoming fire.
        var player = new PlayerSnapshot { EntityId = 1, LevelId = 4, PositionX = 9, PositionY = 6,
            Life = 5827, LifeMaximum = 11776 };
        var target = new ObjectSnapshot { EntityId = 10, LevelId = 4, PositionX = 2, PositionY = 2 };
        var geometry = new LevelGeometrySnapshot { LevelId = 4, Width = 16, Height = 10,
            StaticObjectBlockedCells = { 2 * 16 + 2 } };
        var snapshot = new Snapshot { Tick = 100, Players = { player }, Objects = { target }, Monsters = {
            new MonsterSnapshot { EntityId = 20, LevelId = 4, PositionX = 13, PositionY = 2, Alive = true },
            new MonsterSnapshot { EntityId = 21, LevelId = 4, PositionX = 10, PositionY = 6, Alive = true },
            new MonsterSnapshot { EntityId = 22, LevelId = 4, PositionX = 10, PositionY = 7, Alive = true },
            new MonsterSnapshot { EntityId = 23, LevelId = 4, PositionX = 9, PositionY = 7, Alive = true },
        } };
        var safety = new JourneyCombatSafety();
        var potions = 14; var drinks = 0; var boundaries = 0; var moves = 0;
        JourneyCombatSafety.Observation Observe() => new(player.EntityId, player.LevelId, snapshot.Tick,
            player.Life, player.LifeMaximum, nearby, adjacent, potions, true, true, hz,
            RangedAttackers: shooter == 0 ? 0 : 3, RangedTarget: shooter,
            PositionX: player.PositionX, PositionY: player.PositionY);
        Task Boundary() { boundaries++; return Task.CompletedTask; }
        async Task Prepare() => await AcceptanceCommandBoundary.PrepareObjectAsync(Boundary,
            async () => await safety.CheckpointAsync(JourneySafetyActivity.ObjectApproach, Observe, () => {
                drinks++; potions--; player.Life = player.LifeMaximum; snapshot.Tick++;
                return Task.CompletedTask;
            }, () => throw new InvalidOperationException("A usable object route was abandoned to clear another room.")));

        while (!NativeObjectApproachPath.IsInRange(player.PositionX, player.PositionY,
                target.PositionX, target.PositionY, pickup: false)) {
            Assert.True(moves < 12, "The synthetic direct approach did not arrive within its path length.");
            await Prepare();
            var path = NativeObjectApproachPath.Find(snapshot, geometry, target);
            Assert.NotNull(path);
            Assert.NotEmpty(path);
            var step = path[0];
            player.PositionX = step.X; player.PositionY = step.Y;
            snapshot.Tick += 8;
            player.Life -= 64; // Walking under fire must not turn every settled step into a new encounter.
            moves++;
        }
        await Prepare(); // Preparing the operation in range uses the same healing-only checkpoint.
        Assert.Equal(10, moves);
        Assert.Equal(1, drinks);
        Assert.Equal(13, potions);
        Assert.Equal(2 * (moves + 1), boundaries);
        Assert.True(safety.Observe(Observe()).StopWalking); // Threat evidence still reaches actual combat/recovery.
    }

    [Fact]
    public async Task HealingOnlyPreparationDoesNotIgnoreAnActualActorObstruction()
    {
        var snapshot = new Snapshot { Tick = 100, Players = {
            new PlayerSnapshot { EntityId = 1, LevelId = 4, PositionX = 1, PositionY = 1, Life = 10000, LifeMaximum = 10000 },
        }, Monsters = {
            new MonsterSnapshot { EntityId = 20, LevelId = 4, PositionX = 3, PositionY = 1, Alive = true },
        } };
        var target = new ObjectSnapshot { EntityId = 10, LevelId = 4, PositionX = 5, PositionY = 1 };
        var geometry = new LevelGeometrySnapshot { LevelId = 4, Width = 7, Height = 3 };
        for (var x = 0; x < 7; x++) { geometry.BlockedCells.Add(x); geometry.BlockedCells.Add(14 + x); }
        await new JourneyCombatSafety().CheckpointAsync(JourneySafetyActivity.ObjectApproach,
            () => new(1, 4, snapshot.Tick, 10000, 10000, 1, 0, 14, true, true, 200,
                RangedAttackers: 1, RangedTarget: 20), () => throw new InvalidOperationException("Healthy character drank."),
            () => throw new InvalidOperationException("Preparation, rather than the obstruction resolver, picked a fight."));
        Assert.Null(NativeObjectApproachPath.Find(snapshot, geometry, target));
        Assert.NotNull(NativeObjectApproachPath.Find(snapshot, geometry, target, ignoreActors: true));
        // The existing caller must resolve this real blocker before the route becomes usable.
        snapshot.Monsters[0].Alive = false;
        Assert.NotNull(NativeObjectApproachPath.Find(snapshot, geometry, target));
    }

    [Fact]
    public async Task HealingWaitsForTheOriginalCommandAndFreshSnapshotBeforePlanning()
    {
        var state = DoorBoundary() with { PendingCount = 1 };
        var healed = false;
        var frames = 0;
        await AcceptanceCommandBoundary.PrepareObjectAsync(
            () => AcceptanceCommandBoundary.WaitAsync(() => state, () => {
                frames++;
                state = state with { PendingCount = 0 };
                state.Snapshot!.Tick++;
                state.Player!.PositionX++;
                return Task.CompletedTask;
            }, _ => frames >= 2, "Native object preparation"), () => {
                Assert.Equal(1, frames);
                Assert.Equal(62, state.Player!.PositionX);
                healed = true;
                return Task.CompletedTask;
            });
        Assert.True(healed);
        Assert.Equal(62, state.Player!.PositionX);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DeathAfterRejectedMovementFailsImmediatelyInsteadOfWaitingForStand(int pending)
    {
        var state = DoorBoundary() with { LastQueuedSequence = 236, PendingCount = pending };
        state.Snapshot!.Tick = 13587;
        state.Player!.Life = 0;
        state.Player.CombatMode = PlayerCombatMode.Death;
        state.Receipt!.ClientSequence = 236;
        state.Receipt.AppliedTick = 12999;
        state.Receipt.Status = CommandStatus.Rejected;
        state.Receipt.RejectReason = CommandRejectReason.NotAllowed;
        var error = Assert.Throws<InvalidOperationException>(() =>
            AcceptanceCommandBoundary.CommandSettled(state, 236, "The cardinal native chest approach command"));
        Assert.Contains("character died", error.Message);
        Assert.Contains("queued=236", error.Message);
        Assert.Contains("receipt=236/12999/Rejected/NotAllowed", error.Message);
        Assert.Contains("snapshotTick=13587", error.Message);
    }

    [Theory]
    [InlineData(CommandStatus.Accepted)]
    [InlineData(CommandStatus.Duplicate)]
    [InlineData(CommandStatus.Rejected)]
    [InlineData(CommandStatus.Rescheduled)]
    public void SettlementStillLeavesReceiptOutcomeValidationToTheCaller(CommandStatus status)
    {
        var state = DoorBoundary();
        state.Receipt!.Status = status;
        Assert.True(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
    }

    [Fact]
    public void SettlementRetainsEveryAdmissionAndAnimationRequirement()
    {
        var state = DoorBoundary();
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 236, "Approach"));
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state with { PendingCount = 1 }, 235, "Approach"));
        state.Snapshot!.Tick = 12924;
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
        state.Snapshot.Tick = 12925;
        state.Player!.NativeWalkPathPending = true;
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
        state.Player.NativeWalkPathPending = false;
        state.Player.CombatMode = PlayerCombatMode.GotHit;
        Assert.False(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
        state.Player.CombatMode = PlayerCombatMode.Stand;
        Assert.True(AcceptanceCommandBoundary.CommandSettled(state, 235, "Approach"));
    }

    private static AcceptanceCommandBoundary.Observation DoorBoundary()
    {
        var snapshot = new Snapshot { Tick = 12925 };
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 1, LevelId = 7, PositionX = 61, PositionY = 74,
            Life = 6000, LifeMaximum = 11776, CombatMode = PlayerCombatMode.Stand });
        return new(snapshot, new CommandResult { ClientSequence = 235, AppliedTick = 12925,
            Status = CommandStatus.Accepted }, 235, 0);
    }
}
