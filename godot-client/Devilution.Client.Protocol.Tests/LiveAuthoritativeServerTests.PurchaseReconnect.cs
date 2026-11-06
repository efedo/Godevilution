using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativePurchaseReconnectTests : LiveAuthoritativeTestBase
{
    private readonly ManualTimeProvider time = new();

    [Fact]
    public async Task GodotClientReconnectPreservesPurchasedStateAndSecondParticipant()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingGoldOverride: 5000,
            startingLevelOverride: 0, controlledClock: true);
        await using var firstClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-store-first", requestedClassId: 0) with {
                TimeProvider = time,
                RequestedCharacterName = "Godot-Store-First",
            }) { CommandTimingRecorder = new AuthoritativeClientCommandTimingRecorder() };
        await firstClient.ConnectAsync(TestContext.Current.CancellationToken);
        var firstInitial = await WaitForMessageAsync(firstClient, message => message.Snapshot is not null);
        var firstModel = new AuthoritativeClientModel();
        firstModel.Apply(firstInitial);
        var firstEntityId = Assert.Single(firstModel.Snapshot!.Players).EntityId;

        await using var secondClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-store-second", requestedClassId: 1) with {
                TimeProvider = time,
                RequestedCharacterName = "Godot-Store-Second",
            }) { CommandTimingRecorder = new AuthoritativeClientCommandTimingRecorder() };
        await secondClient.ConnectAsync(TestContext.Current.CancellationToken);
        var secondInitial = await WaitForMessageAsync(
            secondClient,
            message => message.Snapshot is { Players.Count: 2 });
        var secondModel = new AuthoritativeClientModel();
        secondModel.Apply(secondInitial);
        var secondSnapshot = secondInitial.Snapshot!;
        var secondEntityId = secondSnapshot.Players.Single(player => player.EntityId != firstEntityId).EntityId;

        // Purchase/retention assertions depend on game boundaries, not elapsed socket time.
        await MoveToProjectedTownerAsync(firstClient, firstModel, 0, observation: server);
        await MoveToProjectedTownerAsync(secondClient, secondModel, 0, secondEntityId, server);

        var firstSettledPlayer = firstModel.Snapshot!.Players.Single(
            player => player.EntityId == firstEntityId);
        var firstSettledPosition = (firstSettledPlayer.PositionX, firstSettledPlayer.PositionY);
        var firstFresh = await WaitForMessageAsync(
            firstClient,
            message => message.Snapshot is { Players.Count: 2 } snapshot
                && snapshot.Tick >= secondModel.CurrentTick
                && snapshot.Players.SingleOrDefault(player => player.EntityId == firstEntityId) is { } player
                && player.LevelId == firstSettledPlayer.LevelId
                && (player.PositionX, player.PositionY) == firstSettledPosition
                && player.CombatMode == PlayerCombatMode.Stand
                && player.CurrentAction is null);
        firstModel.Apply(firstFresh);

        var openReceipt = await SubmitFreshUiCommandAsync(firstClient, firstModel, new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        });
        Assert.True(openReceipt.Status == CommandStatus.Accepted,
            $"Store open was {openReceipt.Status}/{openReceipt.RejectReason} at {openReceipt.AppliedTick}; "
                + $"model={firstModel.CurrentTick}, requested={firstClient.SuggestedCommandTick(firstModel.CurrentTick)}, "
                + $"player=({firstSettledPlayer.PositionX},{firstSettledPlayer.PositionY}) mode={firstSettledPlayer.CombatMode}.");
        var opened = await WaitForMessageAsync(
            firstClient,
            message => message.Snapshot is { Players.Count: 2, ActiveStore.StoreId: 1 });
        firstModel.Apply(opened);
        var initialStockCount = opened.Snapshot!.ActiveStore!.Items.Count;
        var initialGold = opened.Snapshot.Players.Single(player => player.EntityId == firstEntityId).Gold;

        var purchaseSequence = firstClient.Queue(new Command {
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
        }, firstClient.SuggestedCommandTick(firstModel.CurrentTick));
        await firstClient.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(firstClient, purchaseSequence)).Status);
        var purchased = await WaitForMessageAsync(
            firstClient,
            message => message.Snapshot is { Players.Count: 2, ActiveStore.StoreId: 1 }
                && message.Snapshot.ActiveStore.Items.Count == initialStockCount - 1
                && message.Snapshot.Players.Single(player => player.EntityId == firstEntityId).Gold < initialGold);
        firstModel.Apply(purchased);
        var purchasedSnapshot = purchased.Snapshot!;
        var purchasedPlayer = purchasedSnapshot.Players.Single(player => player.EntityId == firstEntityId);

        await firstClient.ReconnectAsync(TestContext.Current.CancellationToken);
        var resumed = await WaitForMessageAsync(
            firstClient,
            message => message.Snapshot is { Players.Count: 2 }
                && message.Snapshot.Players[0].EntityId == firstEntityId
                && message.Snapshot.Players.Any(player => player.EntityId == secondEntityId)
                && message.Snapshot.Players.Single(player => player.EntityId == firstEntityId).Gold == purchasedPlayer.Gold);
        var resumedSnapshot = resumed.Snapshot!;

        Assert.True(secondClient.IsConnected);
        Assert.Equal([firstEntityId, secondEntityId], resumedSnapshot.Players.Select(player => player.EntityId));
        Assert.Equal("Godot-Store-First", resumedSnapshot.Players[0].CharacterName);
        Assert.Equal("Godot-Store-Second", resumedSnapshot.Players[1].CharacterName);
        Assert.Equal(purchasedPlayer.Gold, resumedSnapshot.Players[0].Gold);
        Assert.Equal(purchasedSnapshot.ActiveStore.Items.Select(item => item.ItemSeed), resumedSnapshot.ActiveStore!.Items.Select(item => item.ItemSeed));
        Assert.NotEmpty(resumedSnapshot.StateSha256);
    }
}
