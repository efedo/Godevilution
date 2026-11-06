using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

[Trait("Category", "Slow")]
public sealed class LiveAuthoritativeStoreDefinitionTests : LiveAuthoritativeTestBase
{

    [Fact]
    public async Task LiveBaseStoreUsesNormalizedShippedItemDefinitions()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingGoldOverride: 5000,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "godot-store-parity-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            server.ContentHash,
            server.RulesetHash));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        await MoveToProjectedTownerAsync(client, model, 0);
        var sequence = client.Queue(new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
        var message = await WaitForMessageAsync(client, message => message.Snapshot?.ActiveStore?.StoreId == 1);
        var stock = message.Snapshot!.ActiveStore!.Items;

        Assert.Equal(2, stock.Count);
        Assert.Equal(1, stock[0].State.ItemIndex);
        Assert.Equal(4, stock[0].State.MinDamage);
        Assert.Equal(8, stock[0].State.MaxDamage);
        Assert.Equal(2, stock[1].State.ItemIndex);
        Assert.Equal(5, stock[1].State.ArmorClass);
        model.Apply(message);

        foreach (var (storeId, itemCount) in new (uint StoreId, int ItemCount)[] {
            (2, 2), // Witch
            (3, 1), // Wirt
            (4, 2), // Healer
        }) {
            var townerId = storeId switch {
                2U => 6,
                3U => 8,
                4U => 1,
                _ => throw new InvalidOperationException($"Unexpected live store {storeId}."),
            };
            await MoveToProjectedTownerAsync(client, model, townerId);
            sequence = client.Queue(new Command {
                OpenStoreRequested = new OpenStoreRequested { StoreId = storeId },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
            message = await WaitForMessageAsync(client, message => message.Snapshot?.ActiveStore?.StoreId == storeId);
            Assert.Equal(itemCount, message.Snapshot!.ActiveStore!.Items.Count);
            model.Apply(message);

            var purchaseSequence = client.Queue(new Command {
                PurchaseRequested = new PurchaseRequested { StoreId = storeId, StoreSlot = 0 },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var purchaseAcknowledgement = await WaitForAcknowledgementAsync(client, purchaseSequence);
            Assert.Equal(CommandStatus.Accepted, purchaseAcknowledgement.Status);
            message = await WaitForMessageAsync(client, message => message.Snapshot?.ActiveStore?.StoreId == storeId
                && message.Snapshot.ActiveStore.Items.Count == itemCount - 1);
            model.Apply(message);
        }

        var closeSequence = client.Queue(new Command {
            CloseStoreRequested = new CloseStoreRequested(),
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var closeAcknowledgement = await WaitForAcknowledgementAsync(client, closeSequence);
        Assert.Equal(CommandStatus.Accepted, closeAcknowledgement.Status);
        message = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null && message.Snapshot.ActiveStore is null);
        Assert.Null(message.Snapshot!.ActiveStore);
    }
}
