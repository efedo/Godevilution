using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests : LiveAuthoritativeTestBase
{

    [Fact]
    public async Task GodotTransportProjectsTheServerSelectedDifficulty()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            difficultyOverride: "hell");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-difficulty", requestedClassId: 2));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        model.Apply(await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null));

        Assert.Equal(GameDifficulty.Hell, model.Snapshot!.Difficulty);
        Assert.Equal(
            "Hell",
            AuthoritativeDifficultyPresentation.Format(model.Snapshot.Difficulty));
    }

    [Fact]
    public async Task GodotTransportRunsThroughPackagedDiabloAdapterAndReconnects()
    {
        using var package = new PackagedDiabloAdapterPackage();
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            gameAdapterPackageRoot: package.Root,
            gameAdapterId: "godevilution.diablo.package",
            gameMode: "single-player");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-packaged-diablo", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var initialPlayer = Assert.Single(initial.Snapshot!.Players);
        var sessionToken = client.SessionToken;
        Assert.NotEmpty(sessionToken);

        await client.ReconnectAsync(TestContext.Current.CancellationToken);
        var resumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null
                && message.Snapshot.Players.Single().EntityId == initialPlayer.EntityId);

        Assert.Equal(sessionToken, client.SessionToken);
        Assert.Equal(initialPlayer.EntityId, resumed.Snapshot!.Players.Single().EntityId);
        Assert.NotEmpty(resumed.Snapshot.StateSha256);
    }

    [Fact]
    public async Task PackagedDiabloAdapterRestoresAuthoritativeStateAfterProcessRestart()
    {
        using var package = new PackagedDiabloAdapterPackage("0.1.0");
        using var upgradedPackage = new PackagedDiabloAdapterPackage("0.2.0");
        Assert.NotEqual(package.Version, upgradedPackage.Version);
        var saveRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Packaged-Diablo-Restart-Test-"
                + Guid.NewGuid().ToString("N"));
        var saveRootLease = LiveTestSaveRoot.Register(saveRoot);
        AuthoritativeClient? client = null;
        Exception? primaryFailure = null;
        try {
            uint entityId;
            int movedPositionY;
            int serverPort;
            using (var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                saveRootOverride: saveRoot,
                deleteSaveRootOnDispose: false,
                gameAdapterPackageRoot: package.Root,
                gameAdapterId: "godevilution.diablo.package",
                gameMode: "single-player", useProcess: true)) {
                serverPort = server.Port;
                client = new AuthoritativeClient(
                    CreateOptions(server, "godot-live-packaged-diablo-restart", requestedClassId: 2));
                await client.ConnectAsync(TestContext.Current.CancellationToken);
                var initial = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is not null);
                var player = Assert.Single(initial.Snapshot!.Players);
                entityId = player.EntityId;
                Assert.Throws<InvalidOperationException>(() => ReadStoppedSessionCheckpoint(
                    server, TestContext.Current.CancellationToken));
                var model = new AuthoritativeClientModel();
                model.Apply(initial);
                var moveSequence = client.Queue(new Command {
                    MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
                }, client.SuggestedCommandTick(model.CurrentTick));
                await client.PollAsync(TestContext.Current.CancellationToken);
                var acceptedMove = await WaitForAcknowledgementAsync(client, moveSequence);
                Assert.Equal(CommandStatus.Accepted, acceptedMove.Status);
                var moved = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot?.Players.Single().PositionY
                        == player.PositionY + 1);
                movedPositionY = moved.Snapshot!.Players.Single().PositionY;

                // Accepted receipts are published only after SaveSession. Kill,
                // do not gracefully flush, then inspect the quiescent checkpoint.
                // Live readers can interfere with Windows File.Move publication.
                server.Dispose();
                Assert.True(server.HasExited);
                Assert.Empty(cleanupFailures);
                using (var canceledRead = new CancellationTokenSource()) {
                    canceledRead.Cancel();
                    Assert.Throws<OperationCanceledException>(() => ReadStoppedSessionCheckpoint(
                        server, canceledRead.Token));
                }
                var checkpoint = ReadStoppedSessionCheckpoint(server, TestContext.Current.CancellationToken);
                var savedMove = Assert.Single(checkpoint.CommandReceipts, receipt => receipt.ClientSequence == moveSequence);
                Assert.Equal(acceptedMove, savedMove);
                var checkpointPlayer = Assert.Single(ParseCheckpointSnapshot(checkpoint).Players);
                Assert.Equal(entityId, checkpointPlayer.EntityId);
                Assert.Equal(2U, checkpointPlayer.CharacterClassId);
                Assert.Equal(player.PositionX, checkpointPlayer.PositionX);
                Assert.Equal(movedPositionY, checkpointPlayer.PositionY);
            }

            using var restartedServer = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                portOverride: serverPort,
                saveRootOverride: saveRoot,
                gameAdapterPackageRoot: upgradedPackage.Root,
                gameAdapterId: "godevilution.diablo.package",
                gameMode: "single-player", useProcess: true);
            Assert.NotEqual(package.Version, upgradedPackage.Version);
            Assert.Equal(client.ServerIdentity!.ContentManifestHash, restartedServer.ContentHash);
            Assert.Equal(client.ServerIdentity.RulesetIdentityHash, restartedServer.RulesetHash);
            await client.ReconnectAsync(TestContext.Current.CancellationToken);
            var restored = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null
                    && message.Snapshot.Players.Single().EntityId == entityId
                    && message.Snapshot.Players.Single().PositionY == movedPositionY);
            var restoredPlayer = Assert.Single(restored.Snapshot!.Players);
            Assert.Equal(entityId, restoredPlayer.EntityId);
            Assert.Equal(2U, restoredPlayer.CharacterClassId);
            Assert.Equal(movedPositionY, restoredPlayer.PositionY);
            Assert.NotEmpty(restored.Snapshot.StateSha256);
            Assert.Equal(restartedServer.ContentHash, client.ServerIdentity!.ContentManifestHash);
            Assert.Equal(restartedServer.RulesetHash, client.ServerIdentity.RulesetIdentityHash);
        } catch (Exception failure) {
            primaryFailure = failure;
            throw;
        } finally {
            if (client is not null) {
                try {
                    await client.DisposeAsync();
                } catch (Exception cleanupFailure) {
                    cleanupFailures.Add(cleanupFailure);
                }
            }
            if (primaryFailure is null && cleanupFailures.Count == 0)
                CleanupLocalRoot(saveRootLease);
        }
    }

    [Fact]
    public async Task GodotTransportReceivesDifficultyScaledAuthoritativeMonsterState()
    {
        var hellfireRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "hellfire");
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            hellfireRoot,
            difficultyOverride: "hell");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-difficulty-monster", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var snapshot = initial.Snapshot!;
        Assert.Equal(GameDifficulty.Hell, snapshot.Difficulty);
        var monster = Assert.Single(snapshot.Monsters, candidate => candidate.EntityId == 10);

        Assert.Equal(2U, monster.MonsterId);
        Assert.Equal(244, monster.MaxHitPoints);
        Assert.Equal(monster.MaxHitPoints, monster.HitPoints);
    }

    [Fact]
    public async Task GodotTransportAllocatesASeededAuthoritativeAttributePoint()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingStatPointsOverride: 1,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-stat-point", requestedClassId: 0));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        model.Apply(await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null));
        var initial = Assert.Single(model.Snapshot!.Players);
        var initialVitality = initial.Attributes.Vitality.Current;
        var initialLife = initial.Life;
        Assert.Equal(1U, initial.StatPoints);

        var sequence = client.Queue(new Command {
            AllocateAttributeRequested = new AllocateAttributeRequested {
                Attribute = PlayerAttribute.Vitality,
                Amount = 1,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, sequence)).Status);

        var allocated = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single() is { } player
                && player.StatPoints == 0
                && player.Attributes.Vitality.Current == initialVitality + 1
                && player.Life > initialLife);
        model.Apply(allocated);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task GodotTransportReconcilesAuthoritativeStashSessionGoldAndMoves()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-stash-session", requestedClassId: 0));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        await MoveToProjectedTownerAsync(client, model, 7);
        var initialPlayer = Assert.Single(model.Snapshot!.Players);
        Assert.True(initialPlayer.Gold >= 10);
        var initialGold = initialPlayer.Gold;
        var initialStashGold = initialPlayer.StashGold;
        var equipment = Assert.Single(initialPlayer.Equipment, item => item.Slot == 4);
        var stableItemId = equipment.State.StableItemId;
        Assert.NotEqual(0U, stableItemId);

        var openSequence = client.Queue(new Command {
            OpenStashRequested = new OpenStashRequested(),
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, openSequence)).Status);
        var opened = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().StashOpen == true);
        model.Apply(opened);

        var depositSequence = client.Queue(new Command {
            DepositStashGoldRequested = new DepositStashGoldRequested { Amount = 10 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, depositSequence)).Status);
        var deposited = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single() is { StashOpen: true }
                player
                && player.Gold == initialGold - 10
                && player.StashGold == initialStashGold + 10);
        model.Apply(deposited);

        var withdrawSequence = client.Queue(new Command {
            WithdrawStashGoldRequested = new WithdrawStashGoldRequested { Amount = 4 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, withdrawSequence)).Status);
        var withdrawn = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single() is { StashOpen: true }
                player
                && player.Gold == initialGold - 6
                && player.StashGold == initialStashGold + 6);
        model.Apply(withdrawn);

        var moveSequence = client.Queue(new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                    StableItemId = stableItemId,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, moveSequence)).Status);
        var moved = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Stash.Any(item =>
                item.Page == 0
                && item.Cell == 0
                && item.Item.State.StableItemId == stableItemId) == true);
        model.Apply(moved);

        var closeSequence = client.Queue(new Command {
            CloseStashRequested = new CloseStashRequested(),
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, closeSequence)).Status);
        var closed = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().StashOpen == false);
        model.Apply(closed);

        var rejectedMoveSequence = client.Queue(new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var rejectedMove = await WaitForAcknowledgementAsync(client, rejectedMoveSequence);
        Assert.Equal(CommandStatus.Rejected, rejectedMove.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, rejectedMove.RejectReason);
    }
}
