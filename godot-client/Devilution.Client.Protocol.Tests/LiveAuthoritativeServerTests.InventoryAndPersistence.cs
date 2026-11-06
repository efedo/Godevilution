using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeInventoryPersistenceTests : LiveAuthoritativeTestBase
{

    [Fact]
    public async Task LiveServerMovesInventoryByStableReferenceAndRejectsStaleMove()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            gameMode: "single-player");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-stable-inventory-move", requestedClassId: 2));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        var item = Assert.Single(player.Inventory);
        Assert.NotEqual(0U, item.State.StableItemId);
        Assert.Equal(0, player.InventoryGrid[30]);

        var moveSequence = client.Queue(new Command {
            MoveInventoryItemRequested = new MoveInventoryItemRequested {
                InventoryIndex = 0,
                TargetCell = 0,
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = item.State.StableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var moveAcknowledgement = await WaitForAcknowledgementAsync(client, moveSequence);
        Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);

        var moved = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().InventoryGrid.FirstOrDefault() == 0);
        model.Apply(moved);
        Assert.Equal(0, model.Snapshot!.Players.Single().InventoryGrid[0]);
        Assert.Equal(-1, model.Snapshot.Players.Single().InventoryGrid[30]);

        var staleSequence = client.Queue(new Command {
            MoveInventoryItemRequested = new MoveInventoryItemRequested {
                InventoryIndex = 0,
                TargetCell = 1,
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = item.State.StableItemId + 1,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var staleAcknowledgement = await WaitForAcknowledgementAsync(client, staleSequence);
        Assert.Equal(CommandStatus.Rejected, staleAcknowledgement.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, staleAcknowledgement.RejectReason);
    }

    [Fact]
    public async Task GodotTransportDropsAStableInventoryReferenceIntoTheWorld()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            gameMode: "single-player");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-stable-world-drop", requestedClassId: 0));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        model.Apply(await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null));
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.NotEmpty(player.Inventory);
        var inventoryItem = player.Inventory[0];
        var inventoryReference = new PlayerItemReference {
            Location = PlayerItemLocation.Inventory,
            Slot = 0,
            StableItemId = inventoryItem.State.StableItemId,
        };

        async Task DropAsync(PlayerItemReference reference, int targetX, int targetY)
        {
            var sequence = client.Queue(new Command {
                DropItemRequested = new DropItemRequested {
                    Item = reference.Clone(),
                    TargetX = targetX,
                    TargetY = targetY,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var dropped = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.WorldItems.Any(item =>
                    item.PositionX == targetX
                    && item.PositionY == targetY
                    && item.State.StableItemId == reference.StableItemId) == true);
            model.Apply(dropped);
        }

        await DropAsync(inventoryReference, 0, 1);

        var finalPlayer = Assert.Single(model.Snapshot!.Players);
        Assert.DoesNotContain(
            finalPlayer.Inventory,
            item => item.State.StableItemId == inventoryReference.StableItemId);
        Assert.Contains(
            model.Snapshot.WorldItems,
            item => item.PositionX == 0
                && item.PositionY == 1
                && item.State.StableItemId == inventoryReference.StableItemId);

        var staleSequence = client.Queue(new Command {
            DropItemRequested = new DropItemRequested {
                Item = inventoryReference.Clone(),
                TargetX = 0,
                TargetY = 1,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var staleAcknowledgement = await WaitForAcknowledgementAsync(client, staleSequence);
        Assert.Equal(CommandStatus.Rejected, staleAcknowledgement.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, staleAcknowledgement.RejectReason);
    }

    [Fact]
    public async Task LiveServerConsumesBeltPotionThroughStableReference()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            gameMode: "single-player");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-stable-belt-consumable", requestedClassId: 0));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(2, player.Belt.Count);
        var source = player.Belt[0];
        Assert.NotEqual(0U, source.State.StableItemId);
        Assert.Equal(ItemActionKind.RestorePartialLife, source.State.ItemActionKind);

        var sequence = client.Queue(new Command {
            UseConsumableRequested = new UseConsumableRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Belt,
                    Slot = source.Slot,
                    StableItemId = source.State.StableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

        var consumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Belt.Count == 1);
        var remaining = Assert.Single(consumed.Snapshot!.Players).Belt;
        Assert.DoesNotContain(remaining, item => item.Slot == source.Slot);
    }

    [Fact]
    public async Task LiveServerRestoresAuthoritativeStateAfterHostRestart()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Persistence-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        try {
            string resumeToken;
            int movedPositionY;
            using (var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false)) {
                await using var client = new AuthoritativeClient(
                    CreateOptions(server, "godot-persistence-first", requestedClassId: 0));
                await client.ConnectAsync(TestContext.Current.CancellationToken);
                var initial = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is not null);
                var model = new AuthoritativeClientModel();
                model.Apply(initial);
                var player = Assert.Single(model.Snapshot!.Players);
                var moveSequence = client.Queue(new Command {
                    MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
                }, client.SuggestedCommandTick(model.CurrentTick));
                await client.PollAsync(TestContext.Current.CancellationToken);
                var moveAcknowledgement = await WaitForAcknowledgementAsync(client, moveSequence);
                Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
                var moved = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot?.Players.Single(
                        candidate => candidate.EntityId == player.EntityId).PositionY
                        == player.PositionY + 1);
                movedPositionY = moved.Snapshot!.Players.Single(
                    candidate => candidate.EntityId == player.EntityId).PositionY;
                resumeToken = client.SessionToken;

                for (var attempt = 0; attempt < 200 && !Directory.EnumerateFiles(saveRoot, "*.json").Any(); attempt++)
                    await Task.Delay(25, TestContext.Current.CancellationToken);
                Assert.NotEmpty(Directory.EnumerateFiles(saveRoot, "*.json"));
                await WaitForMessageAsync(client, message => message.Snapshot is { } settled
                    && settled.Tick >= moveAcknowledgement.AppliedTick
                    && settled.Players.Single().CombatMode == PlayerCombatMode.Stand
                    && settled.Players.Single().CurrentAction is null);
            }

            using var restartedServer = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                saveRootOverride: saveRoot);
            await using var resumedClient = new AuthoritativeClient(
                CreateOptions(restartedServer, "godot-persistence-resumed", requestedClassId: 2) with {
                    ResumeToken = resumeToken,
                });
            await resumedClient.ConnectAsync(TestContext.Current.CancellationToken);
            var restored = await WaitForMessageAsync(
                resumedClient,
                message => message.Snapshot is not null);
            var restoredPlayer = Assert.Single(restored.Snapshot!.Players);
            Assert.Equal(movedPositionY, restoredPlayer.PositionY);
            Assert.Equal(0U, restoredPlayer.CharacterClassId);
            var resumedSequence = resumedClient.Queue(new Command {
                MoveRequested = new MoveRequested { DirectionY = 1 },
            }, resumedClient.SuggestedCommandTick(restored.Snapshot.Tick));
            Assert.Equal(2UL, resumedSequence);
            await resumedClient.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(resumedClient, resumedSequence)).Status);
            await WaitForMessageAsync(resumedClient,
                message => message.Snapshot?.Players.Single().PositionY == movedPositionY + 1);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerRestoresTwoParticipantsAfterHostRestart()
    {
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Multiplayer-Persistence-Test-"
                + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        try {
            string firstResumeToken;
            string secondResumeToken;
            uint firstEntityId;
            uint secondEntityId;
            int movedPositionY;

            using (var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false)) {
                await using var firstClient = new AuthoritativeClient(
                    CreateOptions(server, "godot-multiplayer-persistence-first", requestedClassId: 0) with {
                        RequestedCharacterName = "Godot-Restart-First",
                    });
                await firstClient.ConnectAsync(TestContext.Current.CancellationToken);
                var firstInitial = await WaitForMessageAsync(
                    firstClient,
                    message => message.Snapshot is not null);
                var firstModel = new AuthoritativeClientModel();
                firstModel.Apply(firstInitial);
                firstEntityId = Assert.Single(firstModel.Snapshot!.Players).EntityId;
                firstResumeToken = firstClient.SessionToken;

                await using var secondClient = new AuthoritativeClient(
                    CreateOptions(server, "godot-multiplayer-persistence-second", requestedClassId: 1) with {
                        RequestedCharacterName = "Godot-Restart-Second",
                    });
                await secondClient.ConnectAsync(TestContext.Current.CancellationToken);
                var secondInitial = await WaitForMessageAsync(
                    secondClient,
                    message => message.Snapshot is { Players.Count: 2 });
                secondEntityId = secondInitial.Snapshot!.Players[0].EntityId;
                Assert.NotEqual(firstEntityId, secondEntityId);
                Assert.Equal(
                    [secondEntityId, firstEntityId],
                    secondInitial.Snapshot.Players.Select(player => player.EntityId));
                secondResumeToken = secondClient.SessionToken;

                var moveSequence = firstClient.Queue(new Command {
                    MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
                }, firstClient.SuggestedCommandTick(firstModel.CurrentTick));
                await firstClient.PollAsync(TestContext.Current.CancellationToken);
                Assert.Equal(
                    CommandStatus.Accepted,
                    (await WaitForAcknowledgementAsync(firstClient, moveSequence)).Status);
                var moved = await WaitForMessageAsync(
                    firstClient,
                    message => message.Snapshot is { Players.Count: 2 }
                        && message.Snapshot.Players.Single(
                            player => player.EntityId == firstEntityId).PositionY
                            == firstModel.Snapshot.Players.Single(
                                player => player.EntityId == firstEntityId).PositionY + 1);
                movedPositionY = moved.Snapshot!.Players.Single(
                    player => player.EntityId == firstEntityId).PositionY;

                for (var attempt = 0; attempt < 200
                    && Directory.EnumerateFiles(saveRoot, "*.json").Count() < 2; attempt++) {
                    await Task.Delay(25, TestContext.Current.CancellationToken);
                }
                Assert.True(Directory.EnumerateFiles(saveRoot, "*.json").Count() >= 2);
                await WaitForMessageAsync(firstClient, message => message.Snapshot is { } settled
                    && settled.Tick >= moved.Snapshot!.Tick
                    && settled.Players.Single(player => player.EntityId == firstEntityId).CombatMode == PlayerCombatMode.Stand
                    && settled.Players.Single(player => player.EntityId == firstEntityId).CurrentAction is null);
            }

            using var restartedServer = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                saveRootOverride: saveRoot);
            await using var resumedFirst = new AuthoritativeClient(
                CreateOptions(restartedServer, "godot-multiplayer-persistence-resumed-first", requestedClassId: 2) with {
                    ResumeToken = firstResumeToken,
                });
            await resumedFirst.ConnectAsync(TestContext.Current.CancellationToken);
            var restoredFirst = await WaitForMessageAsync(
                resumedFirst,
                message => message.Snapshot is not null);
            var restoredFirstPlayer = Assert.Single(restoredFirst.Snapshot!.Players);
            Assert.Equal(firstEntityId, restoredFirstPlayer.EntityId);
            Assert.Equal("Godot-Restart-First", restoredFirstPlayer.CharacterName);
            Assert.Equal(movedPositionY, restoredFirstPlayer.PositionY);
            Assert.Equal(0U, restoredFirstPlayer.CharacterClassId);

            await using var resumedSecond = new AuthoritativeClient(
                CreateOptions(restartedServer, "godot-multiplayer-persistence-resumed-second", requestedClassId: 2) with {
                    ResumeToken = secondResumeToken,
                });
            await resumedSecond.ConnectAsync(TestContext.Current.CancellationToken);
            var restoredSecond = await WaitForMessageAsync(
                resumedSecond,
                message => message.Snapshot is { Players.Count: 2 });
            Assert.Equal(
                [secondEntityId, firstEntityId],
                restoredSecond.Snapshot!.Players.Select(player => player.EntityId));
            var restoredSecondPlayer = restoredSecond.Snapshot.Players[0];
            var restoredFirstParticipant = restoredSecond.Snapshot.Players[1];
            Assert.Equal("Godot-Restart-Second", restoredSecondPlayer.CharacterName);
            Assert.Equal(1U, restoredSecondPlayer.CharacterClassId);
            Assert.Equal("Godot-Restart-First", restoredFirstParticipant.CharacterName);
            Assert.Equal(movedPositionY, restoredFirstParticipant.PositionY);
            Assert.Equal(0U, restoredFirstParticipant.CharacterClassId);
            Assert.NotEmpty(restoredSecond.Snapshot.StateSha256);

            var restoredFirstComplete = await WaitForMessageAsync(
                resumedFirst,
                message => message.Snapshot is { Players.Count: 2 });
            Assert.Equal(
                [firstEntityId, secondEntityId],
                restoredFirstComplete.Snapshot!.Players.Select(player => player.EntityId));
            Assert.Equal(
                movedPositionY,
                restoredFirstComplete.Snapshot.Players.Single(
                    player => player.EntityId == firstEntityId).PositionY);
        } finally {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, recursive: true);
        }
    }
}
