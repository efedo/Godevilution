using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeQueuedStoreReconnectTests : LiveAuthoritativeTestBase
{

    [Fact]
    public async Task GodotClientRetriesQueuedStoreCommandAfterReconnect()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingGoldOverride: 5000,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-retry-store", requestedClassId: 0));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        await MoveToProjectedTownerAsync(client, model, 0);

        var openSequence = client.Queue(new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, openSequence)).Status);
        var opened = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.ActiveStore?.StoreId == 1);
        model.Apply(opened);
        var initialStockCount = opened.Snapshot!.ActiveStore!.Items.Count;
        var initialGold = opened.Snapshot.Players.Single().Gold;

        var purchaseSequence = client.Queue(new Command {
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        await client.PollAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, purchaseSequence)).Status);
        var purchased = await WaitForMessageAsync(
            client,
            message => message.Snapshot is { ActiveStore.StoreId: 1 }
                && message.Snapshot.ActiveStore.Items.Count == initialStockCount - 1
                && message.Snapshot.Players.Single().Gold < initialGold);

        var purchasedPlayer = Assert.Single(purchased.Snapshot!.Players);
        Assert.True(purchasedPlayer.Gold < initialGold);
        Assert.Equal(initialStockCount - 1, purchased.Snapshot.ActiveStore!.Items.Count);
    }
}
