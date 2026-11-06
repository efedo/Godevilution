using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeStoreAdmissionTests : LiveAuthoritativeTestBase
{

    [Fact]
    public async Task LiveServerConsumesTheExactDeclaredCastSource()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            randomSeed: 0x12345678);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-cast-source", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        var target = Assert.Single(
            model.Snapshot.Monsters,
            monster => monster.EntityId == 9 && monster.Alive);
        Assert.Equal(target.HitPoints * 64, target.HitPointsQ6);
        Assert.Equal(target.MaxHitPoints * 64, target.MaxHitPointsQ6);
        var staff = Assert.Single(player.Equipment, item => item.Slot == 4);
        var initialCharges = staff.State.Charges;
        Assert.Equal(30, staff.State.SpellId);

        var sequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = (uint)staff.State.SpellId,
                TargetEntityId = target.EntityId,
                Source = CastSourceKind.ChargedItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                    StableItemId = staff.State.StableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);

        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Charged cast was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; staff spell was {staff.State.SpellId} with {initialCharges} charges and target was ({target.PositionX},{target.PositionY}).");
        var consumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Equipment
                .Single(item => item.Slot == 4).State.Charges == initialCharges - 1);
        var updatedPlayer = Assert.Single(consumed.Snapshot!.Players);
        Assert.Equal(player.Mana, updatedPlayer.Mana);
        var updatedStaff = Assert.Single(updatedPlayer.Equipment, item => item.Slot == 4);
        Assert.Equal(staff.State.StableItemId, updatedStaff.State.StableItemId);
        Assert.Equal(initialCharges - 1, updatedStaff.State.Charges);
        var bolts = consumed.Snapshot.Projectiles.Where(projectile =>
            projectile.SourceEntityId == player.EntityId && projectile.SpellId == 30).ToArray();
        Assert.NotEmpty(bolts);
        Assert.All(bolts, projectile => {
            Assert.Equal((int)AuthoritativeProjectileBehavior.ChargedBolt, projectile.Behavior);
            Assert.True(projectile.Damage > 0);
        });
        // Autonomous AI and projectile ticks make the live RNG sample variable.
        // Exact vectors/RNG remain pinned by ChargedBoltFixtureMatchesNativeSpawnVectorAndRngState.
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task LiveStoreAdmissionRejectsRemoteEntryWithoutPlayerMutation()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingGoldOverride: 5000,
            randomSeed: 1,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-store-admission", requestedClassId: 0));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var initialPlayer = Assert.Single(model.Snapshot!.Players);
        var smith = model.Snapshot.TownerGreetings.Single(greeting => greeting.TownerId == 0);
        Assert.True(
            Math.Max(
                Math.Abs(initialPlayer.PositionX - smith.PositionX),
                Math.Abs(initialPlayer.PositionY - smith.PositionY)) > 1);
        var initialGold = initialPlayer.Gold;
        var initialInventory = initialPlayer.Inventory
            .Select(item => (item.ItemSeed, item.State.StableItemId, item.Price))
            .ToArray();

        var remoteSequence = client.Queue(new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var remoteAcknowledgement = await WaitForAcknowledgementAsync(client, remoteSequence);
        Assert.Equal(CommandStatus.Rejected, remoteAcknowledgement.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, remoteAcknowledgement.RejectReason);

        await MoveToProjectedTownerAsync(client, model, 0);
        var nearPlayer = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(initialGold, nearPlayer.Gold);
        Assert.Equal(initialInventory, nearPlayer.Inventory
            .Select(item => (item.ItemSeed, item.State.StableItemId, item.Price)));
        Assert.Null(model.Snapshot.ActiveStore);

        var acceptedSequence = client.Queue(new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acceptedAcknowledgement = await WaitForAcknowledgementAsync(client, acceptedSequence);
        Assert.Equal(CommandStatus.Accepted, acceptedAcknowledgement.Status);
        var opened = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.ActiveStore?.StoreId == 1);
        Assert.Equal(initialGold, opened.Snapshot!.Players.Single().Gold);
    }
}
