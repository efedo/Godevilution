using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{
    private static readonly ConditionalWeakTable<AuthoritativeClient, MessageBacklog> messageBacklogs = new();

    // These overlays edit the checked-in base tables, so their schema must come
    // from that same pack, not a version copied into every live test.
    private static string BaseContentSchemaJson
    {
        get {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                FindRepositoryRoot(), "server", "content", "base", "content-pack.json")));
            return manifest.RootElement.GetProperty("schema").GetRawText();
        }
    }

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
                gameMode: "single-player")) {
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
                gameMode: "single-player");
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

    [Fact]
    public async Task LiveServerProjectsAuthoredWarpTriggersToThePresentationModel()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            gameMode: "single-player");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-warp-projection", requestedClassId: 2));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        model.Apply(await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null));

        var trigger = Assert.Single(
            model.VisibleWarpTriggers,
            warp => warp.StableSymbol == "cathedral_to_catacombs");
        Assert.Equal((36, 36), (trigger.TriggerX, trigger.TriggerY));
        Assert.Equal(2U, trigger.TargetLevelId);
        Assert.True(trigger.Available);
    }

    [Fact]
    public async Task GodotTransportSubmitsAnAuthoredQuestTownerDialogueStage()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Quest-Towner-Overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.godot-quest-towner-stage",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    { "table": "levels.tsv", "keys": ["level_id"], "mode": "upsert" },
                    { "table": "quest_states.tsv", "keys": ["quest_id"], "mode": "upsert" },
                    { "table": "quest_towner_interactions.tsv", "keys": ["interaction_id"], "mode": "upsert" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var repositoryRoot = FindRepositoryRoot();
            var levelsPath = Path.Combine(repositoryRoot, "server", "content", "base", "levels.tsv");
            var levelLines = await File.ReadAllLinesAsync(
                levelsPath,
                TestContext.Current.CancellationToken);
            var townLevel = levelLines
                .Single(line => line.StartsWith("0\t", StringComparison.Ordinal))
                .Split('\t');
            townLevel[6] = "1";
            var cathedralLevel = levelLines
                .Single(line => line.StartsWith("1\t", StringComparison.Ordinal))
                .Split('\t');
            cathedralLevel[6] = "0";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "levels.tsv"),
                [levelLines[0], string.Join('\t', townLevel), string.Join('\t', cathedralLevel)],
                TestContext.Current.CancellationToken);

            var questStatesPath = Path.Combine(repositoryRoot, "server", "content", "base", "quest_states.tsv");
            var questStateLines = await File.ReadAllLinesAsync(
                questStatesPath,
                TestContext.Current.CancellationToken);
            var questStateHeader = questStateLines[0].Split('\t');
            int QuestStateColumn(string name)
                => Array.IndexOf(questStateHeader, name);
            var kingLeoric = questStateLines
                .Single(line => line.StartsWith("3013\t", StringComparison.Ordinal))
                .Split('\t');
            kingLeoric[6] = "1";
            kingLeoric[8] = "0";
            kingLeoric[9] = "1";
            kingLeoric[11] = "1";
            kingLeoric[QuestStateColumn("turn_in_towner_id")] = "3";
            kingLeoric[QuestStateColumn("turn_in_level_id")] = "0";
            kingLeoric[QuestStateColumn("turn_in_position_x")] = "0";
            kingLeoric[QuestStateColumn("turn_in_position_y")] = "0";
            kingLeoric[QuestStateColumn("turn_in_on_completion")] = "true";
            kingLeoric[QuestStateColumn("turn_in_completion_variable2")] = "2";
            kingLeoric[QuestStateColumn("turn_in_completion_message_id")] = "999";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "quest_states.tsv"),
                [questStateLines[0], string.Join('\t', kingLeoric)],
                TestContext.Current.CancellationToken);

            var interactionsPath = Path.Combine(repositoryRoot, "server", "content", "base", "quest_towner_interactions.tsv");
            var interactionLines = await File.ReadAllLinesAsync(
                interactionsPath,
                TestContext.Current.CancellationToken);
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "quest_towner_interactions.tsv"),
                [
                    interactionLines[0],
                    "399001\t3013\tking_leoric\t3\t0\ttown\t0\t0\t0\tNONE\tDialogue\t-1\t1\t2\t2\t999\tfalse\ttrue\t0\t0\tfalse\t0\t\ttrue\tfalse\tfalse\tfalse",
                ],
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                gameMode: "single-player");
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-live-quest-towner-stage", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(0U, player.LevelId);
            Assert.Equal((0, 0), (player.PositionX, player.PositionY));
            var interaction = model.QuestTownerInteractionAt(0, 0);
            Assert.NotNull(interaction);
            Assert.Equal(399001U, interaction.InteractionId);
            Assert.Equal(3013U, interaction.QuestId);
            Assert.Equal(3, interaction.TownerId);

            var sequence = client.Queue(new Command {
                TurnInQuestRequested = new TurnInQuestRequested {
                    QuestId = interaction.QuestId,
                    TownerId = checked((uint)interaction.TownerId),
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var dialogue = await WaitForMessageAsync(
                client,
                message => message.Events?.Events.Any(gameEvent =>
                    gameEvent.Dialogue is {
                        QuestId: 3013,
                        MessageId: 999,
                    }) == true);
            var eventDialogue = Assert.Single(
                dialogue.Events!.Events,
                gameEvent => gameEvent.Dialogue is not null).Dialogue;
            Assert.Equal(3013U, eventDialogue.QuestId);
            Assert.Equal(999, eventDialogue.MessageId);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GodotTransportAppliesQuestTransitionFollowUpToAuthoritativeGeometry()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Object-Follow-Up-Overlay-"
                + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.godot-object-follow-up",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    { "table": "quest_states.tsv", "keys": ["quest_id"], "mode": "upsert" },
                    { "table": "objects.tsv", "keys": ["entity_id"], "mode": "upsert" },
                    { "table": "world_mutations.tsv", "keys": ["mutation_id"], "mode": "upsert" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var repositoryRoot = FindRepositoryRoot();
            var questStatesPath = Path.Combine(
                repositoryRoot,
                "server",
                "content",
                "base",
                "quest_states.tsv");
            var questStateLines = await File.ReadAllLinesAsync(
                questStatesPath,
                TestContext.Current.CancellationToken);
            var questStateHeader = questStateLines[0].Split('\t');
            var questState = questStateLines
                .Single(line => line.StartsWith("3008\t", StringComparison.Ordinal))
                .Split('\t');
            void SetQuestState(string column, string value)
                => questState[Array.IndexOf(questStateHeader, column)] = value;
            SetQuestState("level_id", "1");
            SetQuestState("level_symbol", "cathedral");
            SetQuestState("initial", "0");
            SetQuestState("completed", "0");
            SetQuestState("unavailable", "0");
            SetQuestState("variable1", "0");
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "quest_states.tsv"),
                [questStateLines[0], string.Join('\t', questState)],
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "objects.tsv"),
                "entity_id\tobject_id\tobject_symbol\tlevel_id\tlevel_symbol\tposition_x\tposition_y\tactivated\tquest_id\tquest_symbol\teffect_kind\teffect_amount\teffect_item_symbol\tmissile_passable\tbreakable\tdestroyed\ttrap_armed\ttemplate_only\tquest_sequence_follow_up_kind\tquest_sequence_follow_up_amount\tquest_sequence_follow_up_monster_symbol\tquest_sequence_follow_up_offset_x\tquest_sequence_follow_up_offset_y\tquest_sequence_follow_up_world_mutation_id\teffect_spell_symbol\tprojectile_target_offset_x\tprojectile_target_offset_y\tprojectile_source\tquest_transition_kind\tquest_transition_amount\tquest_transition_required_variable1\tquest_transition_required_variable2\tactivate_on_occupancy\trequired_activated_linked_objects\tnative_object_id\treward_kind\treward_id\treward_item_level\treward_offset_x\treward_offset_y\tdialogue_message_id\n2999\t1\tl1_left_door\t1\tcathedral\t0\t1\t0\t3008\togdens_sign\tNone\t0\t\t0\t0\t0\t0\t0\tWorldMutation\t9901\t\t0\t0\t0\t\t0\t0\tObject\tCompleteAndSetVariable1\t1\t0\t-1\tfalse\t0\t1\tNone\t0\t0\t0\t0\t0\n",
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "world_mutations.tsv"),
                "mutation_id\tlevel_id\tanchor_entity_id\tanchor_unique_identity\tblocked_cells_add\tblocked_cells_remove\tmissile_blocked_cells_add\tmissile_blocked_cells_remove\tgeometry_rect_add\tgeometry_rect_remove\ttransparency_rects\tobject_layer_rects\tpiece_layer_rects\tanchor_mega_offset_x\tanchor_mega_offset_y\tmega_tile_assignments\tcreate_transparency_layer\tmega_tile_copy_source_level_id\tmega_tile_copy_rects\tpiece_property_overrides\tmonster_layer_rects\tmega_tile_asset_path\ttile_asset_path\tsol_asset_path\tnative_layer_asset_path\n9901\t1\t0\t\t42\tnone\tnone\tnone\tnone\tnone\tnone\tnone\tnone\t0\t0\tnone\tfalse\t0\tnone\tnone\tnone\tnone\tnone\tnone\tnone\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLevelOverride: 1,
                // Seed 1 keeps Ogden's Sign available in native and managed pools.
                randomSeed: 1,
                gameMode: "single-player");
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-live-object-follow-up", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            Assert.Equal(1U, Assert.Single(model.Snapshot!.Players).LevelId);
            var authoredObject = Assert.Single(
                model.Snapshot.Objects,
                @object => @object.EntityId == 2999);
            Assert.Equal(7, authoredObject.QuestTransitionKind);
            Assert.Equal(1, authoredObject.QuestTransitionAmount);
            Assert.Equal(1, authoredObject.QuestSequenceFollowUpKind);
            Assert.Equal(9901U, authoredObject.QuestSequenceFollowUpAmount);
            Assert.False(Assert.Single(model.Snapshot.Quests, quest => quest.QuestId == 3008).Unavailable);
            var initialGeometry = model.Snapshot.LevelGeometry
                .Single(level => level.LevelId == 1);
            Assert.DoesNotContain(42, initialGeometry.BlockedCells);

            var sequence = client.Queue(new Command {
                OperateObjectRequested = new OperateObjectRequested {
                    ObjectEntityId = authoredObject.EntityId,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Object operation was rejected: {acknowledgement.RejectReason}");

            var applied = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } snapshot
                    && snapshot.Objects.Single(@object => @object.EntityId == 2999).Activated);
            model.Apply(applied);
            Assert.True(Assert.Single(model.Snapshot!.Objects, @object => @object.EntityId == 2999).Activated);
            Assert.True(Assert.Single(model.Snapshot.Quests, quest => quest.QuestId == 3008).Completed);
            Assert.Contains(
                42,
                model.Snapshot.LevelGeometry.Single(level => level.LevelId == 1).BlockedCells);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GodotTransportUsesAuthoredInitialUniqueMonsterDialogueStage()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Snotspill-Dialogue-Overlay-"
                + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.godot-snotspill-dialogue",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    { "table": "quest_states.tsv", "keys": ["quest_id"], "mode": "upsert" },
                    { "table": "unique_monsters.tsv", "keys": ["identity"], "mode": "upsert" },
                    { "table": "unique_monster_placements.tsv", "keys": ["placement_id"], "mode": "upsert" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var repositoryRoot = FindRepositoryRoot();
            var questStatesPath = Path.Combine(
                repositoryRoot,
                "server",
                "content",
                "base",
                "quest_states.tsv");
            var questStateLines = await File.ReadAllLinesAsync(
                questStatesPath,
                TestContext.Current.CancellationToken);
            var questStateHeader = questStateLines[0].Split('\t');
            int QuestStateColumn(string name)
                => Array.IndexOf(questStateHeader, name);
            var questState = questStateLines
                .Single(line => line.StartsWith("3008\t", StringComparison.Ordinal))
                .Split('\t');
            questState[QuestStateColumn("level_id")] = "1";
            questState[QuestStateColumn("level_symbol")] = "cathedral";
            questState[QuestStateColumn("initial")] = "0";
            questState[QuestStateColumn("logged")] = "0";
            questState[QuestStateColumn("variable1")] = "0";
            questState[QuestStateColumn("variable2")] = "0";
            questState[QuestStateColumn("completion_unique_identity")] = "Lachdanan";
            questState[QuestStateColumn("dialogue_completion_variable1")] = "-1";
            questState[QuestStateColumn("completion_unique_identity")] = "FixtureUnique";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "quest_states.tsv"),
                [questStateLines[0], string.Join('\t', questState)],
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "unique_monsters.tsv"),
                "entity_id\tmonster_id\tmonster_symbol\tidentity\tnative_ai\tspawn_health_percent\tspawn_armor_delta\tspawn_fire_resistance\tspawn_lightning_resistance\tspawn_magic_resistance\tspawn_acid_resistance\n"
                    + "0\t0\t\tFixtureUnique\tZombie\tnone\t0\tnone\tnone\tnone\tnone\n",
                TestContext.Current.CancellationToken);

            var placementsPath = Path.Combine(
                repositoryRoot,
                "server",
                "content",
                "base",
                "unique_monster_placements.tsv");
            var placementLines = await File.ReadAllLinesAsync(
                placementsPath,
                TestContext.Current.CancellationToken);
            var placementHeader = placementLines[0].Split('\t');
            int PlacementColumn(string name)
                => Array.IndexOf(placementHeader, name);
            var placement = placementLines[1].Split('\t');
            placement[PlacementColumn("unique_identity")] = "FixtureUnique";
            placement[PlacementColumn("monster_id")] = "1";
            placement[PlacementColumn("monster_symbol")] = "base_monster";
            placement[PlacementColumn("level_id")] = "1";
            placement[PlacementColumn("level_symbol")] = "cathedral";
            placement[PlacementColumn("entity_id")] = "100";
            placement[PlacementColumn("position_x")] = "0";
            placement[PlacementColumn("position_y")] = "1";
            placement[PlacementColumn("ignore_dungeon_level_range")] = "true";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "unique_monster_placements.tsv"),
                [placementLines[0], string.Join('\t', placement)],
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLevelOverride: 1,
                gameMode: "multiplayer");
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-live-snotspill-dialogue", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var target = Assert.Single(
                model.Snapshot.Monsters,
                monster => monster.EntityId == 100 && monster.IsUnique);
            Assert.Equal(1U, player.LevelId);
            Assert.Equal((0, 0), (player.PositionX, player.PositionY));
            Assert.Equal((0, 1), (target.PositionX, target.PositionY));
            Assert.Equal(0, Assert.Single(model.Snapshot.Quests, quest => quest.QuestId == 3008).Variable1);
            Assert.Equal(12, target.DialogueMessageId);
            Assert.Equal(
                new AuthoritativeMonsterDialogueTarget(3008, 100),
                AuthoritativeMonsterDialogueSelector.Select(model.Snapshot, player.EntityId, 0, 1));

            var attackSequence = client.Queue(new Command {
                AttackRequested = new AttackRequested {
                    TargetEntityId = target.EntityId,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var attackAcknowledgement = await WaitForAcknowledgementAsync(client, attackSequence);
            Assert.Equal(CommandStatus.Rejected, attackAcknowledgement.Status);
            Assert.Equal(CommandRejectReason.InvalidTarget, attackAcknowledgement.RejectReason);
            model.Apply(await WaitForMessageAsync(client, message =>
                message.Snapshot?.Tick >= attackAcknowledgement.AppliedTick));

            var sequence = client.Queue(new Command {
                TalkToQuestRequested = new TalkToQuestRequested {
                    QuestId = 3008,
                    TargetEntityId = target.EntityId,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var dialogue = await WaitForMessageAsync(
                client,
                message => message.Events?.Events.Any(gameEvent =>
                    gameEvent.Dialogue is {
                        QuestId: 3008,
                        MessageId: 12,
                    }) == true);
            model.Apply(dialogue);
            var advanced = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Quests.SingleOrDefault(quest => quest.QuestId == 3008)?.Variable1 == 1);
            model.Apply(advanced);
            Assert.Equal(
                21,
                Assert.Single(model.Snapshot.Monsters, monster => monster.EntityId == 100).DialogueMessageId);
            Assert.Equal(
                1,
                Assert.Single(model.Snapshot.Quests, quest => quest.QuestId == 3008).Variable1);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GodotTransportUsesTheItemAbsentBranchForSharedLachdananDialogue()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Lachdanan-Dialogue-Overlay-"
                + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.godot-lachdanan-dialogue",
                  "version": "1",
                  "dependencies": ["devilution.hellfire"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.hellfire",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    { "table": "content_ids.tsv", "keys": ["kind", "symbol"], "mode": "upsert" },
                    { "table": "quest_states.tsv", "keys": ["quest_id"], "mode": "upsert" },
                    { "table": "monster_types.tsv", "keys": ["monster_id"], "mode": "upsert" },
                    { "table": "monster_spawn_policies.tsv", "keys": ["monster_id"], "mode": "upsert" },
                    { "table": "unique_monsters.tsv", "keys": ["identity"], "mode": "upsert" },
                    { "table": "unique_monster_placements.tsv", "keys": ["placement_id"], "mode": "upsert" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var repositoryRoot = FindRepositoryRoot();
            var questStatesPath = Path.Combine(
                repositoryRoot,
                "server",
                "content",
                "hellfire",
                "quest_states.tsv");
            var questStateLines = await File.ReadAllLinesAsync(
                questStatesPath,
                TestContext.Current.CancellationToken);
            var questStateHeader = questStateLines[0].Split('\t');
            int QuestStateColumn(string name)
                => Array.IndexOf(questStateHeader, name);
            var questState = questStateLines
                .Single(line => line.StartsWith("3005\t", StringComparison.Ordinal))
                .Split('\t');
            questState[QuestStateColumn("level_id")] = "14";
            questState[QuestStateColumn("level_symbol")] = "catacombs_depth_14";
            questState[QuestStateColumn("initial")] = "0";
            questState[QuestStateColumn("logged")] = "0";
            questState[QuestStateColumn("variable1")] = "0";
            questState[QuestStateColumn("variable2")] = "0";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "quest_states.tsv"),
                [questStateLines[0], string.Join('\t', questState)],
                TestContext.Current.CancellationToken);

            var monsterTypesPath = Path.Combine(
                repositoryRoot,
                "server",
                "content",
                "base",
                "monster_types.tsv");
            var monsterTypeLines = await File.ReadAllLinesAsync(
                monsterTypesPath,
                TestContext.Current.CancellationToken);
            var monsterTypeHeader = monsterTypeLines[0].Split('\t');
            int MonsterTypeColumn(string name)
                => Array.IndexOf(monsterTypeHeader, name);
            var monsterType = monsterTypeLines
                .Single(line => line.StartsWith("1\t", StringComparison.Ordinal))
                .Split('\t');
            monsterType[MonsterTypeColumn("monster_id")] = "5000";
            monsterType[MonsterTypeColumn("monster_symbol")] = "test_lachdanan";
            monsterType[MonsterTypeColumn("name")] = "Lachdanan Fixture";
            monsterType[MonsterTypeColumn("minimum_dungeon_level")] = "1";
            monsterType[MonsterTypeColumn("maximum_dungeon_level")] = "16";
            monsterType[MonsterTypeColumn("native_ai")] = "Lachdanan";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "monster_types.tsv"),
                [monsterTypeLines[0], string.Join('\t', monsterType)],
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "monster_spawn_policies.tsv"),
                "monster_id\tmonster_symbol\tspawn_role\tcoverage_mode\n"
                    + "5000\ttest_lachdanan\tQuest\tComplete\n",
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "unique_monsters.tsv"),
                "entity_id\tmonster_id\tmonster_symbol\tidentity\tnative_ai\tspawn_health_percent\tspawn_armor_delta\tspawn_fire_resistance\tspawn_lightning_resistance\tspawn_magic_resistance\tspawn_acid_resistance\n"
                    + "0\t5000\ttest_lachdanan\tLachdanan\tLachdanan\tnone\t0\tnone\tnone\tnone\tnone\n",
                TestContext.Current.CancellationToken);

            var placementsPath = Path.Combine(
                repositoryRoot,
                "server",
                "content",
                "base",
                "unique_monster_placements.tsv");
            var placementLines = await File.ReadAllLinesAsync(
                placementsPath,
                TestContext.Current.CancellationToken);
            var placementHeader = placementLines[0].Split('\t');
            int PlacementColumn(string name)
                => Array.IndexOf(placementHeader, name);
            var placement = placementLines[1].Split('\t');
            placement[PlacementColumn("unique_identity")] = "Lachdanan";
            placement[PlacementColumn("monster_id")] = "5000";
            placement[PlacementColumn("monster_symbol")] = "test_lachdanan";
            placement[PlacementColumn("level_id")] = "14";
            placement[PlacementColumn("level_symbol")] = "catacombs_depth_14";
            placement[PlacementColumn("entity_id")] = "100";
            placement[PlacementColumn("position_x")] = "0";
            placement[PlacementColumn("position_y")] = "1";
            placement[PlacementColumn("ignore_dungeon_level_range")] = "true";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "unique_monster_placements.tsv"),
                [placementLines[0], string.Join('\t', placement)],
                TestContext.Current.CancellationToken);

            var hellfireRoot = Path.Combine(repositoryRoot, "server", "content", "hellfire");
            await File.WriteAllTextAsync(Path.Combine(overlayRoot, "content_ids.tsv"),
                "kind\tsymbol\tnumeric_id\nmonster\ttest_lachdanan\t5000\n",
                TestContext.Current.CancellationToken);
            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                hellfireRoot,
                overlayRoot,
                startingLevelOverride: 14,
                gameMode: "multiplayer");
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-live-lachdanan-dialogue", requestedClassId: 2));
            await using var secondClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-lachdanan-dialogue-second", requestedClassId: 0));
            var model = new AuthoritativeClientModel();
            var secondModel = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            await secondClient.ConnectAsync(TestContext.Current.CancellationToken);
            secondModel.Apply(await WaitForMessageAsync(
                secondClient,
                message => message.Snapshot is not null));
            var target = Assert.Single(
                model.Snapshot!.Monsters,
                monster => monster.EntityId == 100 && monster.IsUnique);
            var secondTarget = Assert.Single(
                secondModel.Snapshot!.Monsters,
                monster => monster.EntityId == 100 && monster.IsUnique);
            Assert.Equal(81, target.DialogueMessageId);
            Assert.Equal(81, secondTarget.DialogueMessageId);
            Assert.Equal(
                new AuthoritativeMonsterDialogueTarget(3005, 100),
                AuthoritativeMonsterDialogueSelector.Select(model.Snapshot, model.PlayerEntityId, 0, 1));
            Assert.Equal(
                new AuthoritativeMonsterDialogueTarget(3005, 100),
                AuthoritativeMonsterDialogueSelector.Select(secondModel.Snapshot, secondModel.PlayerEntityId, 0, 1));

            var sequence = client.Queue(new Command {
                TalkToQuestRequested = new TalkToQuestRequested {
                    QuestId = 3005,
                    TargetEntityId = target.EntityId,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var dialogue = await WaitForMessageAsync(
                client,
                message => message.Events?.Events.Any(gameEvent =>
                    gameEvent.Dialogue is {
                        QuestId: 3005,
                        MessageId: 81,
                    }) == true);
            model.Apply(dialogue);
            Assert.Equal(
                81,
                Assert.Single(model.Snapshot.Monsters, monster => monster.EntityId == 100).DialogueMessageId);
            Assert.Equal(
                0,
                Assert.Single(model.Snapshot.Quests, quest => quest.QuestId == 3005).Variable1);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GodotTransportFollowsAnAuthoredProgressionWarp()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Warp-Transition-Overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.godot-warp-transition",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "warps.tsv",
                      "keys": ["warp_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "warps.tsv"),
                "warp_id\twarp_symbol\tsource_level_id\tsource_level_symbol\ttrigger_x\ttrigger_y\ttarget_level_id\ttarget_level_symbol\ttarget_x\ttarget_y\trequired_quest_id\trequired_quest_symbol\trequired_quest_completed\ttarget_set_level_id\ttarget_set_level_symbol\ttarget_set_level_active_map\n"
                + "1002\tcathedral_to_catacombs\t1\tcathedral\t0\t1\t2\tcatacombs\t10\t10\t0\t\t0\tnone\t\tfalse\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                gameMode: "single-player");
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-live-warp-transition", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(1U, player.LevelId);
            Assert.Equal((0, 0), (player.PositionX, player.PositionY));
            var trigger = Assert.Single(
                model.VisibleWarpTriggers,
                warp => warp.StableSymbol == "cathedral_to_catacombs");
            Assert.Equal((0, 1), (trigger.TriggerX, trigger.TriggerY));
            Assert.True(trigger.Available);

            var sequence = client.Queue(new Command {
                MoveRequested = new MoveRequested {
                    DirectionX = 0,
                    DirectionY = 1,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var transitioned = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single().LevelId == trigger.TargetLevelId);
            model.Apply(transitioned);
            var destination = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(2U, destination.LevelId);
            Assert.Equal((10, 10), (destination.PositionX, destination.PositionY));

            var sessionToken = client.SessionToken;
            await client.ReconnectAsync(TestContext.Current.CancellationToken);
            Assert.Equal(sessionToken, client.SessionToken);
            var resumed = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single().LevelId == trigger.TargetLevelId);
            model.Apply(resumed);
            var resumedPlayer = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(2U, resumedPlayer.LevelId);
            Assert.Equal((10, 10), (resumedPlayer.PositionX, resumedPlayer.PositionY));
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GodotTransportCompletesLiveMoveAndCastSlice()
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "godot-live-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            server.ContentHash,
            server.RulesetHash) {
            RequestedCharacterClassId = 2,
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(50),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(500),
        });
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GameMode.Multiplayer, client.ServerGameMode);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        model.Apply(initial);
        var player = model.Snapshot!.Players.Single();
        Assert.Equal("Firebolt", Assert.Single(player.MemorizedSpells).DisplayName);
        Assert.Equal(
            SpellTargetingKind.CombatEntity,
            Assert.Single(player.MemorizedSpells).TargetingKind);
        var chargedStaff = Assert.Single(player.Equipment, item => item.State?.SpellId == 30);
        Assert.Equal("Charged Bolt", chargedStaff.State!.SpellDisplayName);
        Assert.Equal(SpellTargetingKind.CombatEntity, chargedStaff.State.SpellTargetingKind);
        var monster = model.Snapshot.Monsters.Single(
            monster => monster.EntityId == 9 && monster.Alive);

        var moveSequence = client.Queue(new Command {
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var moveAcknowledgement = await WaitForAcknowledgementAsync(client, moveSequence);
        Assert.True(moveAcknowledgement.Status == CommandStatus.Accepted,
            $"Move was {moveAcknowledgement.Status}/{moveAcknowledgement.RejectReason} at tick {moveAcknowledgement.AppliedTick}; player was ({player.PositionX},{player.PositionY}) level {player.LevelId}.");
        var moved = await WaitForMessageAsync(client, message => message.Snapshot?.Players.Single().PositionY == player.PositionY + 1);
        model.Apply(moved);

        var castSequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = 1,
                TargetEntityId = monster.EntityId,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var castAcknowledgement = await WaitForAcknowledgementAsync(client, castSequence);
        Assert.True(castAcknowledgement.Status == CommandStatus.Accepted,
            $"Cast was {castAcknowledgement.Status}/{castAcknowledgement.RejectReason} at tick {castAcknowledgement.AppliedTick}; player was ({model.Snapshot!.Players.Single().PositionX},{model.Snapshot.Players.Single().PositionY}), target was ({monster.PositionX},{monster.PositionY}).");
    }

    [Fact]
    public async Task GodotTransportUsesTheSameProtocolInSinglePlayerMode()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            gameMode: "single-player");
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-single-player-live-test", requestedClassId: 2));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GameMode.SinglePlayer, client.ServerGameMode);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        model.Apply(initial);
        Assert.Single(model.Snapshot!.Players);
        var player = model.Snapshot.Players.Single();
        var monster = model.Snapshot.Monsters.Single(
            monster => monster.EntityId == 9 && monster.Alive);

        var castSequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = 1,
                TargetEntityId = monster.EntityId,
                TargetX = monster.PositionX,
                TargetY = monster.PositionY,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var castAcknowledgement = await WaitForAcknowledgementAsync(client, castSequence);
        Assert.True(
            castAcknowledgement.Status == CommandStatus.Accepted,
            $"Single-player cast was {castAcknowledgement.Status}/{castAcknowledgement.RejectReason} at tick {castAcknowledgement.AppliedTick}; player was ({player.PositionX},{player.PositionY}), target was ({monster.PositionX},{monster.PositionY}).");
    }

    [Fact]
    public async Task GodotTransportProjectsLiveHolyBoltAgainstAnUndeadMonster()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Holy-Bolt-Overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.godot-holy-bolt",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    { "table": "levels.tsv", "keys": ["level_id"], "mode": "upsert" },
                    { "table": "class_loadouts.tsv", "keys": ["class_id"], "mode": "upsert" },
                    { "table": "monsters.tsv", "keys": ["entity_id"], "mode": "upsert" },
                    { "table": "spells.tsv", "keys": ["spell_id"], "mode": "upsert" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var repositoryRoot = FindRepositoryRoot();
            var levelsPath = Path.Combine(repositoryRoot, "server", "content", "base", "levels.tsv");
            var levelLines = await File.ReadAllLinesAsync(
                levelsPath,
                TestContext.Current.CancellationToken);
            var townLevel = levelLines
                .Single(line => line.StartsWith("0\t", StringComparison.Ordinal))
                .Split('\t');
            townLevel[6] = "1";
            var cathedralLevel = levelLines
                .Single(line => line.StartsWith("1\t", StringComparison.Ordinal))
                .Split('\t');
            cathedralLevel[6] = "0";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "levels.tsv"),
                [levelLines[0], string.Join('\t', townLevel), string.Join('\t', cathedralLevel)],
                TestContext.Current.CancellationToken);

            var loadoutPath = Path.Combine(repositoryRoot, "server", "content", "base", "class_loadouts.tsv");
            var loadoutLines = await File.ReadAllLinesAsync(
                loadoutPath,
                TestContext.Current.CancellationToken);
            var loadoutHeader = loadoutLines[0].Split('\t');
            int LoadoutColumn(string name)
                => Array.IndexOf(loadoutHeader, name);
            var sorcererLoadout = loadoutLines
                .Single(line => line.StartsWith("2\t", StringComparison.Ordinal))
                .Split('\t');
            sorcererLoadout[LoadoutColumn("starting_spell_id")] = "5";
            sorcererLoadout[LoadoutColumn("starting_spell_symbol")] = "holy_bolt";
            sorcererLoadout[LoadoutColumn("starting_spell_level")] = "1";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                [loadoutLines[0], string.Join('\t', sorcererLoadout)],
                TestContext.Current.CancellationToken);

            var monsterPath = Path.Combine(repositoryRoot, "server", "content", "base", "monsters.tsv");
            var monsterLines = await File.ReadAllLinesAsync(
                monsterPath,
                TestContext.Current.CancellationToken);
            var monsterHeader = monsterLines[0].Split('\t');
            int MonsterColumn(string name)
                => Array.IndexOf(monsterHeader, name);
            var monster = monsterLines
                .Single(line => line.StartsWith("9\t", StringComparison.Ordinal))
                .Split('\t');
            monster[MonsterColumn("level_id")] = "0";
            monster[MonsterColumn("level_symbol")] = "town";
            monster[MonsterColumn("position_x")] = "1";
            monster[MonsterColumn("position_y")] = "0";
            monster[MonsterColumn("undead")] = "1";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "monsters.tsv"),
                [monsterLines[0], string.Join('\t', monster)],
                TestContext.Current.CancellationToken);

            var spellsPath = Path.Combine(repositoryRoot, "server", "content", "base", "spells.tsv");
            var spellLines = await File.ReadAllLinesAsync(
                spellsPath,
                TestContext.Current.CancellationToken);
            var spellHeader = spellLines[0].Split('\t');
            int SpellColumn(string name)
                => Array.IndexOf(spellHeader, name);
            var holyBolt = spellLines
                .Single(line => line.StartsWith("5\t", StringComparison.Ordinal))
                .Split('\t');
            holyBolt[SpellColumn("holy_bolt_minimum_hit_chance")] = "100";
            holyBolt[SpellColumn("holy_bolt_maximum_hit_chance")] = "100";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "spells.tsv"),
                [spellLines[0], string.Join('\t', holyBolt)],
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                gameMode: "single-player",
                randomSeed: 0);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-live-holy-bolt", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var target = Assert.Single(
                model.Snapshot.Monsters,
                candidate => candidate.EntityId == 9 && candidate.Alive);
            Assert.Equal(0U, player.LevelId);
            Assert.Equal((0, 0), (player.PositionX, player.PositionY));
            Assert.Equal((1, 0), (target.PositionX, target.PositionY));
            Assert.True(target.IsUndead);
            Assert.Contains(
                player.MemorizedSpells,
                spell => spell.SpellId == 5 && spell.Level == 1);
            var targetLife = target.HitPoints;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 5,
                    TargetEntityId = target.EntityId,
                    TargetX = target.PositionX,
                    TargetY = target.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var resolved = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Monsters.Any(
                    candidate => candidate.EntityId == target.EntityId
                        && (!candidate.Alive || candidate.HitPoints < targetLife)) == true
                    || message.Events?.Events.Any(gameEvent =>
                        gameEvent.Damage is {
                            TargetEntityId: var targetEntityId,
                            Amount: > 0,
                        } && targetEntityId == target.EntityId) == true);
            if (resolved.Snapshot is not null) {
                var resolvedTarget = Assert.Single(
                    resolved.Snapshot.Monsters,
                    candidate => candidate.EntityId == target.EntityId);
                Assert.True(!resolvedTarget.Alive || resolvedTarget.HitPoints < targetLife);
            } else {
                Assert.Contains(
                    resolved.Events!.Events,
                    gameEvent => gameEvent.Damage is {
                        TargetEntityId: var targetEntityId,
                        Amount: > 0,
                    } && targetEntityId == target.EntityId);
            }
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GodotTransportProjectsAuthoredUniqueMonsterPackState()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Godot-Unique-Pack-Overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.godot-unique-pack",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    { "table": "unique_monsters.tsv", "keys": ["identity"], "mode": "upsert" },
                    { "table": "unique_monster_placements.tsv", "keys": ["placement_id"], "mode": "upsert" },
                    { "table": "unique_monster_packs.tsv", "keys": ["pack_id", "placement_id"], "mode": "upsert" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "unique_monsters.tsv"),
                "entity_id\tmonster_id\tmonster_symbol\tidentity\tnative_ai\tspawn_health_percent\tspawn_armor_delta\tspawn_fire_resistance\tspawn_lightning_resistance\tspawn_magic_resistance\tspawn_acid_resistance\n"
                    + "0\t1\tbase_monster\tGenerated Zombie\tZombie\tnone\t0\tnone\tnone\tnone\tnone\n",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "unique_monster_placements.tsv"),
                "placement_id\tunique_identity\tmonster_id\tmonster_symbol\tlevel_id\tlevel_symbol\tentity_id\tposition_x\tposition_y\tseed\tdirection\tignore_dungeon_level_range\trequired_quest_id\trequired_quest_symbol\n"
                    + "1\tGenerated Zombie\t1\tbase_monster\t1\tcathedral\t100\t10\t10\t99\t0\tfalse\t0\t\n",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "unique_monster_packs.tsv"),
                "pack_id\tplacement_id\tminion_monster_id\tminion_monster_symbol\tpack_size\tpack_kind\n"
                    + "1\t1\t1\tbase_monster\t2\tleashed\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                gameMode: "single-player",
                randomSeed: 0);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-live-unique-pack", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));

            var leader = Assert.Single(
                model.Snapshot!.Monsters,
                monster => monster.EntityId == 100 && monster.IsUnique);
            var members = model.Snapshot.Monsters
                .Where(monster => monster.LeaderEntityId == leader.EntityId)
                .ToArray();
            Assert.Equal(2, members.Length);
            Assert.All(
                members,
                member => Assert.Equal(1, member.LeaderRelation));
            Assert.Equal(2, leader.PackSize);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

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
    public async Task LiveServerRestoresAuthoritativeStateAfterProcessRestart()
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
                await Task.Delay(500, TestContext.Current.CancellationToken);
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
    public async Task LiveServerRestoresTwoParticipantsAfterProcessRestart()
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
                await Task.Delay(500, TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task LiveServerAdmitsEntityTargetedAttackAndPublishesActionState()
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-live-attack", requestedClassId: 0));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        var target = Assert.Single(
            model.Snapshot.Monsters,
            monster => monster.EntityId == 9 && monster.Alive);

        var sequence = client.Queue(new Command {
            AttackRequested = new AttackRequested {
                TargetEntityId = target.EntityId,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Attack was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; player was ({player.PositionX},{player.PositionY}), target was ({target.PositionX},{target.PositionY}).");

        var action = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().CurrentAction is {
                Kind: PlayerActionKind.Attack,
                TargetEntityId: var targetEntityId,
            } && targetEntityId == target.EntityId);
        var actionSnapshot = action.Snapshot!.Players.Single().CurrentAction!;
        Assert.Equal(PlayerActionKind.Attack, actionSnapshot.Kind);
        Assert.Equal(target.EntityId, actionSnapshot.TargetEntityId);
        Assert.True(actionSnapshot.EffectTick >= actionSnapshot.StartedAtTick);
        Assert.True(actionSnapshot.EndTick >= actionSnapshot.EffectTick);
    }

    [Fact]
    public async Task LiveServerResolvesAPlayerTargetedMeleeAttackInTheSharedSession()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            randomSeed: 1);
        await using var attackerClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-pvp-attacker", requestedClassId: 0));
        await attackerClient.ConnectAsync(TestContext.Current.CancellationToken);
        var attackerInitial = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot is not null);
        var attackerEntityId = Assert.Single(attackerInitial.Snapshot!.Players).EntityId;

        await using var victimClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-pvp-victim", requestedClassId: 1));
        await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
        var victimInitial = await WaitForMessageAsync(
            victimClient,
            message => message.Snapshot is { Players.Count: 2 });
        var victim = victimInitial.Snapshot!.Players.Single(
            player => player.EntityId != attackerEntityId);
        var attacker = victimInitial.Snapshot.Players.Single(
            player => player.EntityId == attackerEntityId);
        Assert.InRange(
            Math.Max(
                Math.Abs(attacker.PositionX - victim.PositionX),
                Math.Abs(attacker.PositionY - victim.PositionY)),
            0,
            1);

        var model = new AuthoritativeClientModel();
        model.Apply(victimInitial);

        var friendlyModeSequence = attackerClient.Queue(new Command {
            SetFriendlyModeRequested = new SetFriendlyModeRequested {
                FriendlyMode = false,
            },
        }, attackerClient.SuggestedCommandTick(model.CurrentTick));
        await attackerClient.PollAsync(TestContext.Current.CancellationToken);
        var friendlyModeAcknowledgement = await WaitForAcknowledgementAsync(
            attackerClient,
            friendlyModeSequence);
        Assert.Equal(CommandStatus.Accepted, friendlyModeAcknowledgement.Status);
        model.Apply(await WaitForMessageAsync(attackerClient, message =>
            message.Snapshot?.Tick >= friendlyModeAcknowledgement.AppliedTick));

        var sequence = attackerClient.Queue(new Command {
            AttackRequested = new AttackRequested {
                TargetEntityId = victim.EntityId,
            },
        }, FindGuaranteedHitTick(
            attackerClient.SuggestedCommandTick(model.CurrentTick),
            attackerEntityId,
            victim.EntityId));
        await attackerClient.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(attackerClient, sequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

        var action = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot?.Players.Single(
                player => player.EntityId == attackerEntityId).CurrentAction is {
                    Kind: PlayerActionKind.Attack,
                    TargetEntityId: var targetEntityId,
                } && targetEntityId == victim.EntityId);
        Assert.Equal(
            PlayerActionKind.Attack,
            action.Snapshot!.Players.Single(
                player => player.EntityId == attackerEntityId).CurrentAction!.Kind);
        var victimLifeBeforeImpact = action.Snapshot.Players.Single(
            player => player.EntityId == victim.EntityId).Life;

        var resolved = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot?.Players.Single(
                    player => player.EntityId == victim.EntityId).Life < victimLifeBeforeImpact
                    || message.Events?.Events.Any(gameEvent =>
                        gameEvent.Damage is not null
                        && gameEvent.Damage.TargetEntityId == victim.EntityId
                        && gameEvent.Damage.Amount > 0) == true);
        if (resolved.Snapshot is not null) {
            Assert.True(
                resolved.Snapshot.Players.Single(player => player.EntityId == victim.EntityId).Life
                    < victimLifeBeforeImpact);
        } else {
            var damage = Assert.Single(resolved.Events!.Events).Damage;
            Assert.Equal(victim.EntityId, damage.TargetEntityId);
            Assert.True(damage.Amount > 0);
        }
    }

    [Fact]
    public async Task LiveServerResolvesARangedPlayerTargetThroughWeaponArrowImpact()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            randomSeed: 1);
        await using var victimClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-pvp-ranged-victim", requestedClassId: 0));
        await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
        var victimInitial = await WaitForMessageAsync(
            victimClient,
            message => message.Snapshot is not null);
        var initialVictim = Assert.Single(victimInitial.Snapshot!.Players);
        var moveModel = new AuthoritativeClientModel();
        moveModel.Apply(victimInitial);
        var moveSequence = victimClient.Queue(new Command {
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
        }, victimClient.SuggestedCommandTick(moveModel.CurrentTick));
        await victimClient.PollAsync(TestContext.Current.CancellationToken);
        var moveAcknowledgement = await WaitForAcknowledgementAsync(victimClient, moveSequence);
        Assert.True(
            moveAcknowledgement.Status == CommandStatus.Accepted,
            $"Move was {moveAcknowledgement.Status}/{moveAcknowledgement.RejectReason} at tick {moveAcknowledgement.AppliedTick}.");
        var moved = await WaitForMessageAsync(
            victimClient,
            message => message.Snapshot?.Players.Single(
                player => player.EntityId == initialVictim.EntityId).PositionY
                == initialVictim.PositionY + 1);
        var victim = moved.Snapshot!.Players.Single(
            player => player.EntityId == initialVictim.EntityId);

        await using var attackerClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-pvp-ranged-attacker", requestedClassId: 1));
        await attackerClient.ConnectAsync(TestContext.Current.CancellationToken);
        var attackerInitial = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot is { Players.Count: 2 });
        var attackerEntityId = attackerInitial.Snapshot!.Players.Single(
            player => player.EntityId != victim.EntityId).EntityId;
        Assert.Equal(victim.PositionY, attackerInitial.Snapshot.Players.Single(
            player => player.EntityId == victim.EntityId).PositionY);
        var model = new AuthoritativeClientModel();
        model.Apply(attackerInitial);
        var sequence = attackerClient.Queue(new Command {
            AttackRequested = new AttackRequested {
                TargetEntityId = victim.EntityId,
            },
        }, attackerClient.SuggestedCommandTick(model.CurrentTick));
        await attackerClient.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(attackerClient, sequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

        var action = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot?.Players.Single(
                player => player.EntityId == attackerEntityId).CurrentAction is {
                    Kind: PlayerActionKind.Attack,
                    TargetEntityId: var targetEntityId,
                } && targetEntityId == victim.EntityId);
        Assert.Equal(
            PlayerActionKind.Attack,
            action.Snapshot!.Players.Single(
                player => player.EntityId == attackerEntityId).CurrentAction!.Kind);
        var victimLifeBeforeImpact = action.Snapshot.Players.Single(
            player => player.EntityId == victim.EntityId).Life;

        var resolved = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot?.Players.Single(
                player => player.EntityId == victim.EntityId).Life < victimLifeBeforeImpact
                || message.Events?.Events.Any(gameEvent =>
                    gameEvent.Damage is not null
                    && gameEvent.Damage.TargetEntityId == victim.EntityId
                    && gameEvent.Damage.Amount > 0) == true);
        if (resolved.Snapshot is not null) {
            Assert.True(
                resolved.Snapshot.Players.Single(player => player.EntityId == victim.EntityId).Life
                    < victimLifeBeforeImpact);
        } else {
            var damage = Assert.Single(resolved.Events!.Events).Damage;
            Assert.Equal(victim.EntityId, damage.TargetEntityId);
            Assert.True(damage.Amount > 0);
        }
    }

    [Fact]
    public async Task LiveServerResolvesAPlayerTargetedFireboltThroughTheSharedProtocol()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            randomSeed: 1);
        await using var victimClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-spell-victim", requestedClassId: 1));
        await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
        var victimInitial = await WaitForMessageAsync(
            victimClient,
            message => message.Snapshot is not null);
        var initialVictim = Assert.Single(victimInitial.Snapshot!.Players);
        var moveModel = new AuthoritativeClientModel();
        moveModel.Apply(victimInitial);
        var moveSequence = victimClient.Queue(new Command {
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
        }, victimClient.SuggestedCommandTick(moveModel.CurrentTick));
        await victimClient.PollAsync(TestContext.Current.CancellationToken);
        var moveAcknowledgement = await WaitForAcknowledgementAsync(victimClient, moveSequence);
        Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
        var moved = await WaitForMessageAsync(
            victimClient,
            message => message.Snapshot?.Players.Single(
                player => player.EntityId == initialVictim.EntityId).PositionY
                == initialVictim.PositionY + 1);
        var victim = moved.Snapshot!.Players.Single(
            player => player.EntityId == initialVictim.EntityId);

        await using var attackerClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-spell-attacker", requestedClassId: 2));
        await attackerClient.ConnectAsync(TestContext.Current.CancellationToken);
        var attackerInitial = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot is { Players.Count: 2 });
        var attackerEntityId = attackerInitial.Snapshot!.Players.Single(
            player => player.EntityId != victim.EntityId).EntityId;
        var targetSnapshot = attackerInitial.Snapshot.Players.Single(
            player => player.EntityId == victim.EntityId);
        Assert.Equal(victim.PositionY, targetSnapshot.PositionY);
        var model = new AuthoritativeClientModel();
        model.Apply(attackerInitial);
        var sequence = attackerClient.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = 1,
                TargetEntityId = victim.EntityId,
                TargetX = victim.PositionX,
                TargetY = victim.PositionY,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, attackerClient.SuggestedCommandTick(model.CurrentTick));
        await attackerClient.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(attackerClient, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Firebolt was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; target was ({victim.PositionX},{victim.PositionY}).");

        var action = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot?.Players.Single(
                player => player.EntityId == attackerEntityId).CurrentAction is {
                    Kind: PlayerActionKind.Cast,
                    TargetEntityId: var targetEntityId,
                } && targetEntityId == victim.EntityId);
        var actionSnapshot = action.Snapshot!.Players.Single(
            player => player.EntityId == attackerEntityId).CurrentAction!;
        Assert.Equal(PlayerActionKind.Cast, actionSnapshot.Kind);
        Assert.Equal(1U, actionSnapshot.Cast.SpellId);
        Assert.Equal(victim.EntityId, actionSnapshot.TargetEntityId);
        var victimLifeBeforeImpact = action.Snapshot.Players.Single(
            player => player.EntityId == victim.EntityId).Life;

        var resolved = await WaitForMessageAsync(
            attackerClient,
            message => message.Snapshot?.Players.Single(
                player => player.EntityId == victim.EntityId).Life < victimLifeBeforeImpact
                || message.Events?.Events.Any(gameEvent =>
                    gameEvent.Damage is not null
                    && gameEvent.Damage.TargetEntityId == victim.EntityId
                    && gameEvent.Damage.Amount > 0) == true);
        if (resolved.Snapshot is not null) {
            Assert.True(
                resolved.Snapshot.Players.Single(player => player.EntityId == victim.EntityId).Life
                    < victimLifeBeforeImpact);
        } else {
            var damage = Assert.Single(resolved.Events!.Events).Damage;
            Assert.Equal(victim.EntityId, damage.TargetEntityId);
            Assert.True(damage.Amount > 0);
        }
    }

    [Fact]
    public async Task LiveServerResolvesAPlayerTargetedHealOtherFromValidatedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Heal-Other-Pvp-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.heal-other-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tNone\t34\theal_other\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLifeOverride: 640,
                startingManaOverride: 10000,
                randomSeed: 0);
            await using var victimClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-heal-other-victim", requestedClassId: 1));
            await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
            var victimInitial = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot is not null);
            var initialVictim = Assert.Single(victimInitial.Snapshot!.Players);
            Assert.True(initialVictim.Life > 0);
            var moveModel = new AuthoritativeClientModel();
            moveModel.Apply(victimInitial);
            var moveSequence = victimClient.Queue(new Command {
                MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
            }, victimClient.SuggestedCommandTick(moveModel.CurrentTick));
            await victimClient.PollAsync(TestContext.Current.CancellationToken);
            var moveAcknowledgement = await WaitForAcknowledgementAsync(victimClient, moveSequence);
            Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
            var moved = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == initialVictim.EntityId).PositionY
                    == initialVictim.PositionY + 1);
            var victim = moved.Snapshot!.Players.Single(
                player => player.EntityId == initialVictim.EntityId);

            await using var healerClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-heal-other-healer", requestedClassId: 2));
            await healerClient.ConnectAsync(TestContext.Current.CancellationToken);
            var healerInitial = await WaitForMessageAsync(
                healerClient,
                message => message.Snapshot is { Players.Count: 2 });
            var healerEntityId = healerInitial.Snapshot!.Players.Single(
                player => player.EntityId != victim.EntityId).EntityId;
            var healerSnapshot = healerInitial.Snapshot.Players.Single(
                player => player.EntityId == healerEntityId);
            Assert.Contains(
                healerSnapshot.MemorizedSpells,
                memorized => memorized.SpellId == 34 && memorized.Level == 1);
            var targetSnapshot = healerInitial.Snapshot.Players.Single(
                player => player.EntityId == victim.EntityId);
            Assert.Equal(victim.PositionY, targetSnapshot.PositionY);

            var model = new AuthoritativeClientModel();
            model.Apply(healerInitial);
            var sequence = healerClient.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 34,
                    TargetEntityId = victim.EntityId,
                    TargetX = victim.PositionX,
                    TargetY = victim.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, healerClient.SuggestedCommandTick(model.CurrentTick));
            await healerClient.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(healerClient, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Heal Other was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; target was ({victim.PositionX},{victim.PositionY}).");

            var action = await WaitForMessageAsync(
                healerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == healerEntityId).CurrentAction is {
                        Kind: PlayerActionKind.Cast,
                        TargetEntityId: var targetEntityId,
                    } && targetEntityId == victim.EntityId);
            var actionSnapshot = action.Snapshot!.Players.Single(
                player => player.EntityId == healerEntityId).CurrentAction!;
            Assert.Equal(PlayerActionKind.Cast, actionSnapshot.Kind);
            Assert.Equal(34U, actionSnapshot.Cast.SpellId);
            var victimLifeBeforeHealing = action.Snapshot.Players.Single(
                player => player.EntityId == victim.EntityId).Life;

            var resolved = await WaitForMessageAsync(
                healerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == victim.EntityId).Life > victimLifeBeforeHealing);
            Assert.True(
                resolved.Snapshot!.Players.Single(player => player.EntityId == victim.EntityId).Life
                    > victimLifeBeforeHealing);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerResolvesResurrectionAgainstADeadRemotePlayer()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Resurrection-Pvp-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.resurrection-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tNone\t1\tfirebolt\t1\n3\tmonk\t100\t22\trage\tNone\t32\tresurrection\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLifeOverride: 640,
                startingManaOverride: 10000,
                randomSeed: 1);
            await using var victimClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-resurrection-victim", requestedClassId: 1));
            await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
            var victimInitial = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot is not null);
            var initialVictim = Assert.Single(victimInitial.Snapshot!.Players);
            var moveModel = new AuthoritativeClientModel();
            moveModel.Apply(victimInitial);
            var moveSequence = victimClient.Queue(new Command {
                MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
            }, victimClient.SuggestedCommandTick(moveModel.CurrentTick));
            await victimClient.PollAsync(TestContext.Current.CancellationToken);
            var moveAcknowledgement = await WaitForAcknowledgementAsync(victimClient, moveSequence);
            Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
            var moved = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == initialVictim.EntityId).PositionY
                    == initialVictim.PositionY + 1);
            var victim = moved.Snapshot!.Players.Single(
                player => player.EntityId == initialVictim.EntityId);

            await using var attackerClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-resurrection-attacker", requestedClassId: 2));
            await attackerClient.ConnectAsync(TestContext.Current.CancellationToken);
            var attackerInitial = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot is { Players.Count: 2 });
            var attackerEntityId = attackerInitial.Snapshot!.Players.Single(
                player => player.EntityId != victim.EntityId).EntityId;
            var attackerSnapshot = attackerInitial.Snapshot.Players.Single(
                player => player.EntityId == attackerEntityId);
            Assert.Contains(
                attackerSnapshot.MemorizedSpells,
                memorized => memorized.SpellId == 1 && memorized.Level == 1);
            var attackModel = new AuthoritativeClientModel();
            attackModel.Apply(attackerInitial);
            var attackSequence = attackerClient.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 1,
                    TargetEntityId = victim.EntityId,
                    TargetX = victim.PositionX,
                    TargetY = victim.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, attackerClient.SuggestedCommandTick(attackModel.CurrentTick));
            await attackerClient.PollAsync(TestContext.Current.CancellationToken);
            var attackAcknowledgement = await WaitForAcknowledgementAsync(attackerClient, attackSequence);
            Assert.Equal(CommandStatus.Accepted, attackAcknowledgement.Status);
            var attackAction = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == attackerEntityId).CurrentAction is {
                        Kind: PlayerActionKind.Cast,
                        TargetEntityId: var targetEntityId,
                    } && targetEntityId == victim.EntityId);
            var victimLifeBeforeImpact = attackAction.Snapshot!.Players.Single(
                player => player.EntityId == victim.EntityId).Life;
            var dead = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == victim.EntityId).Life == 0);
            Assert.True(
                dead.Snapshot!.Players.Single(player => player.EntityId == victim.EntityId).Life
                    < victimLifeBeforeImpact);

            await using var resurrectorClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-resurrection-caster", requestedClassId: 3));
            await resurrectorClient.ConnectAsync(TestContext.Current.CancellationToken);
            var resurrectorInitial = await WaitForMessageAsync(
                resurrectorClient,
                message => message.Snapshot is { Players.Count: 3 });
            var resurrectorEntityId = resurrectorInitial.Snapshot!.Players.Single(
                player => player.EntityId != victim.EntityId && player.EntityId != attackerEntityId).EntityId;
            var resurrectorSnapshot = resurrectorInitial.Snapshot.Players.Single(
                player => player.EntityId == resurrectorEntityId);
            Assert.Contains(
                resurrectorSnapshot.MemorizedSpells,
                memorized => memorized.SpellId == 32 && memorized.Level == 1);
            var deadTargetSnapshot = resurrectorInitial.Snapshot.Players.Single(
                player => player.EntityId == victim.EntityId);
            Assert.Equal(0, deadTargetSnapshot.Life);

            var resurrectModel = new AuthoritativeClientModel();
            resurrectModel.Apply(resurrectorInitial);
            var resurrectSequence = resurrectorClient.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 32,
                    TargetEntityId = victim.EntityId,
                    TargetX = deadTargetSnapshot.PositionX,
                    TargetY = deadTargetSnapshot.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, resurrectorClient.SuggestedCommandTick(resurrectModel.CurrentTick));
            await resurrectorClient.PollAsync(TestContext.Current.CancellationToken);
            var resurrectAcknowledgement = await WaitForAcknowledgementAsync(
                resurrectorClient,
                resurrectSequence);
            Assert.True(
                resurrectAcknowledgement.Status == CommandStatus.Accepted,
                $"Resurrection was {resurrectAcknowledgement.Status}/{resurrectAcknowledgement.RejectReason} at tick {resurrectAcknowledgement.AppliedTick}.");
            var resurrectAction = await WaitForMessageAsync(
                resurrectorClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == resurrectorEntityId).CurrentAction is {
                        Kind: PlayerActionKind.Cast,
                        TargetEntityId: var targetEntityId,
                    } && targetEntityId == victim.EntityId);
            var actionSnapshot = resurrectAction.Snapshot!.Players.Single(
                player => player.EntityId == resurrectorEntityId).CurrentAction!;
            Assert.Equal(PlayerActionKind.Cast, actionSnapshot.Kind);
            Assert.Equal(32U, actionSnapshot.Cast.SpellId);

            var restored = await WaitForMessageAsync(
                resurrectorClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == victim.EntityId).Life > 0);
            Assert.True(
                restored.Snapshot!.Players.Single(player => player.EntityId == victim.EntityId).Life
                    > 0);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerResolvesAPlayerTargetedChargedBoltFromValidatedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Charged-Bolt-Pvp-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.charged-bolt-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tNone\t30\tcharged_bolt\t2\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 0);
            await using var victimClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-charged-bolt-victim", requestedClassId: 1));
            await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
            var victimInitial = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot is not null);
            var initialVictim = Assert.Single(victimInitial.Snapshot!.Players);
            var moveModel = new AuthoritativeClientModel();
            moveModel.Apply(victimInitial);
            var moveSequence = victimClient.Queue(new Command {
                MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
            }, victimClient.SuggestedCommandTick(moveModel.CurrentTick));
            await victimClient.PollAsync(TestContext.Current.CancellationToken);
            var moveAcknowledgement = await WaitForAcknowledgementAsync(victimClient, moveSequence);
            Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
            var moved = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == initialVictim.EntityId).PositionY
                    == initialVictim.PositionY + 1);
            var victim = moved.Snapshot!.Players.Single(
                player => player.EntityId == initialVictim.EntityId);

            await using var attackerClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-charged-bolt-attacker", requestedClassId: 2));
            await attackerClient.ConnectAsync(TestContext.Current.CancellationToken);
            var attackerInitial = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot is { Players.Count: 2 });
            var attackerEntityId = attackerInitial.Snapshot!.Players.Single(
                player => player.EntityId != victim.EntityId).EntityId;
            var attackerSnapshot = attackerInitial.Snapshot.Players.Single(
                player => player.EntityId == attackerEntityId);
            Assert.Contains(
                attackerSnapshot.MemorizedSpells,
                memorized => memorized.SpellId == 30 && memorized.Level == 2);
            var targetSnapshot = attackerInitial.Snapshot.Players.Single(
                player => player.EntityId == victim.EntityId);
            Assert.Equal(victim.PositionY, targetSnapshot.PositionY);
            var model = new AuthoritativeClientModel();
            model.Apply(attackerInitial);
            var sequence = attackerClient.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 30,
                    TargetEntityId = victim.EntityId,
                    TargetX = victim.PositionX,
                    TargetY = victim.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, attackerClient.SuggestedCommandTick(model.CurrentTick));
            await attackerClient.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(attackerClient, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Charged Bolt was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; target was ({victim.PositionX},{victim.PositionY}).");

            var action = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == attackerEntityId).CurrentAction is {
                        Kind: PlayerActionKind.Cast,
                        TargetEntityId: var targetEntityId,
                    } && targetEntityId == victim.EntityId);
            var actionSnapshot = action.Snapshot!.Players.Single(
                player => player.EntityId == attackerEntityId).CurrentAction!;
            Assert.Equal(PlayerActionKind.Cast, actionSnapshot.Kind);
            Assert.Equal(30U, actionSnapshot.Cast.SpellId);
            var victimLifeBeforeImpact = action.Snapshot.Players.Single(
                player => player.EntityId == victim.EntityId).Life;

            var resolved = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == victim.EntityId).Life < victimLifeBeforeImpact
                    || message.Events?.Events.Any(gameEvent =>
                        gameEvent.Damage is not null
                        && gameEvent.Damage.TargetEntityId == victim.EntityId
                        && gameEvent.Damage.Amount > 0) == true);
            if (resolved.Snapshot is not null) {
                Assert.True(
                    resolved.Snapshot.Players.Single(player => player.EntityId == victim.EntityId).Life
                        < victimLifeBeforeImpact);
            } else {
                var damage = Assert.Single(resolved.Events!.Events).Damage;
                Assert.Equal(victim.EntityId, damage.TargetEntityId);
                Assert.True(damage.Amount > 0);
            }
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerResolvesAPlayerTargetedBoneSpiritFromValidatedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Bone-Spirit-Pvp-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.bone-spirit-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tNone\t38\tbone_spirit\t2\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 0);
            await using var victimClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-bone-spirit-victim", requestedClassId: 1));
            await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
            var victimInitial = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot is not null);
            var initialVictim = Assert.Single(victimInitial.Snapshot!.Players);
            var moveModel = new AuthoritativeClientModel();
            moveModel.Apply(victimInitial);
            var moveSequence = victimClient.Queue(new Command {
                MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
            }, victimClient.SuggestedCommandTick(moveModel.CurrentTick));
            await victimClient.PollAsync(TestContext.Current.CancellationToken);
            var moveAcknowledgement = await WaitForAcknowledgementAsync(victimClient, moveSequence);
            Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
            var moved = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == initialVictim.EntityId).PositionY
                    == initialVictim.PositionY + 1);
            var victim = moved.Snapshot!.Players.Single(
                player => player.EntityId == initialVictim.EntityId);

            await using var attackerClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-bone-spirit-attacker", requestedClassId: 2));
            await attackerClient.ConnectAsync(TestContext.Current.CancellationToken);
            var attackerInitial = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot is { Players.Count: 2 });
            var attackerEntityId = attackerInitial.Snapshot!.Players.Single(
                player => player.EntityId != victim.EntityId).EntityId;
            var attackerSnapshot = attackerInitial.Snapshot.Players.Single(
                player => player.EntityId == attackerEntityId);
            Assert.Contains(
                attackerSnapshot.MemorizedSpells,
                memorized => memorized.SpellId == 38 && memorized.Level == 2);
            var targetSnapshot = attackerInitial.Snapshot.Players.Single(
                player => player.EntityId == victim.EntityId);
            Assert.Equal(victim.PositionY, targetSnapshot.PositionY);
            var model = new AuthoritativeClientModel();
            model.Apply(attackerInitial);
            var sequence = attackerClient.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 38,
                    TargetEntityId = victim.EntityId,
                    TargetX = victim.PositionX,
                    TargetY = victim.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, attackerClient.SuggestedCommandTick(model.CurrentTick));
            await attackerClient.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(attackerClient, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Bone Spirit was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; target was ({victim.PositionX},{victim.PositionY}).");

            var action = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == attackerEntityId).CurrentAction is {
                        Kind: PlayerActionKind.Cast,
                        TargetEntityId: var targetEntityId,
                    } && targetEntityId == victim.EntityId);
            var actionSnapshot = action.Snapshot!.Players.Single(
                player => player.EntityId == attackerEntityId).CurrentAction!;
            Assert.Equal(PlayerActionKind.Cast, actionSnapshot.Kind);
            Assert.Equal(38U, actionSnapshot.Cast.SpellId);
            var victimLifeBeforeImpact = action.Snapshot.Players.Single(
                player => player.EntityId == victim.EntityId).Life;

            var resolved = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == victim.EntityId).Life < victimLifeBeforeImpact
                    || message.Events?.Events.Any(gameEvent =>
                        gameEvent.Damage is not null
                        && gameEvent.Damage.TargetEntityId == victim.EntityId
                        && gameEvent.Damage.Amount > 0) == true);
            if (resolved.Snapshot is not null) {
                Assert.True(
                    resolved.Snapshot.Players.Single(player => player.EntityId == victim.EntityId).Life
                        < victimLifeBeforeImpact);
            } else {
                var damage = Assert.Single(resolved.Events!.Events).Damage;
                Assert.Equal(victim.EntityId, damage.TargetEntityId);
                Assert.True(damage.Amount > 0);
            }
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerResolvesShieldBlockForAPlayerTargetedMeleeAttack()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Pvp-Block-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.pvp-block-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "classes.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    },
                    {
                      "table": "monsters.tsv",
                      "keys": ["entity_id"],
                      "mode": "upsert"
                    },
                    {
                      "table": "class_animations.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            var classTablePath = Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "classes.tsv");
            var classLines = await File.ReadAllLinesAsync(
                classTablePath,
                TestContext.Current.CancellationToken);
            var warriorCells = classLines.Single(
                line => line.StartsWith("0\t", StringComparison.Ordinal)).Split('\t');
            warriorCells[14] = "100";
            warriorCells[16] = "100";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "classes.tsv"),
                [classLines[0], string.Join('\t', warriorCells)],
                TestContext.Current.CancellationToken);
            var monsterTablePath = Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "monsters.tsv");
            var monsterLines = await File.ReadAllLinesAsync(
                monsterTablePath,
                TestContext.Current.CancellationToken);
            var monsterCells = monsterLines.Single(
                line => line.StartsWith("9\t", StringComparison.Ordinal)).Split('\t');
            monsterCells[5] = "20";
            monsterCells[6] = "20";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "monsters.tsv"),
                [monsterLines[0], string.Join('\t', monsterCells)],
                TestContext.Current.CancellationToken);
            // Native Warrior blocking lasts four ticks; a loaded runner can sample
            // snapshots on either side of it. Lengthen it to about three seconds.
            var animationTablePath = Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "class_animations.tsv");
            var animationLines = await File.ReadAllLinesAsync(
                animationTablePath,
                TestContext.Current.CancellationToken);
            var animationHeader = animationLines[0].Split('\t');
            var warriorAnimation = animationLines.Single(
                line => line.StartsWith("0\t", StringComparison.Ordinal)).Split('\t');
            warriorAnimation[Array.IndexOf(animationHeader, "blocking_frames")] = "21";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "class_animations.tsv"),
                [animationLines[0], string.Join('\t', warriorAnimation)],
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                randomSeed: 0);
            await using var victimClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-pvp-block-victim", requestedClassId: 0));
            await victimClient.ConnectAsync(TestContext.Current.CancellationToken);
            var victimInitial = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot is not null);
            var initialVictim = Assert.Single(victimInitial.Snapshot!.Players);
            var moveModel = new AuthoritativeClientModel();
            moveModel.Apply(victimInitial);
            var moveSequence = victimClient.Queue(new Command {
                MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
            }, victimClient.SuggestedCommandTick(moveModel.CurrentTick));
            await victimClient.PollAsync(TestContext.Current.CancellationToken);
            var moveAcknowledgement = await WaitForAcknowledgementAsync(victimClient, moveSequence);
            Assert.Equal(CommandStatus.Accepted, moveAcknowledgement.Status);
            _ = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == initialVictim.EntityId).PositionY
                    == initialVictim.PositionY + 1);
            var ready = await WaitForMessageAsync(
                victimClient,
                message => message.Snapshot?.Players.Single(
                    player => player.EntityId == initialVictim.EntityId) is {
                        PositionY: var positionY,
                        CombatMode: PlayerCombatMode.Stand,
                    } && positionY == initialVictim.PositionY + 1);
            var victim = ready.Snapshot!.Players.Single(
                player => player.EntityId == initialVictim.EntityId);
            Assert.Equal(0U, victim.CharacterClassId);
            Assert.Contains(victim.Equipment, item =>
                item.State.ItemType == 5 && item.State.StatFlag);

            await using var attackerClient = new AuthoritativeClient(
                CreateOptions(server, "godot-live-pvp-block-attacker", requestedClassId: 0));
            await attackerClient.ConnectAsync(TestContext.Current.CancellationToken);
            var attackerInitial = await WaitForMessageAsync(
                attackerClient,
                message => message.Snapshot is { Players.Count: 2 });
            var attackerEntityId = attackerInitial.Snapshot!.Players.Single(
                player => player.EntityId != victim.EntityId).EntityId;
            var attacker = attackerInitial.Snapshot.Players.Single(
                player => player.EntityId == attackerEntityId);
            Assert.InRange(
                Math.Max(
                    Math.Abs(attacker.PositionX - victim.PositionX),
                    Math.Abs(attacker.PositionY - victim.PositionY)),
                0,
                1);
            var model = new AuthoritativeClientModel();
            model.Apply(attackerInitial);
            PlayerSnapshot? blockedVictim = null;
            var attemptTrace = new List<string>();
            string Describe(Snapshot snapshot)
            {
                var a = snapshot.Players.Single(player => player.EntityId == attackerEntityId);
                var v = snapshot.Players.Single(player => player.EntityId == victim.EntityId);
                return $"tick {snapshot.Tick}: attacker ({a.PositionX},{a.PositionY}) {a.CombatMode} action {a.CurrentAction?.Kind}; "
                    + $"victim ({v.PositionX},{v.PositionY}) {v.CombatMode} until {v.CombatModeEndTick} life {v.Life}";
            }
            for (var attempt = 0; attempt < 8 && blockedVictim is null; attempt++) {
                // The legacy PvP melee hit roll is (seed * 1664525 + 1013904223) % 100
                // with seed = tick ^ target ^ attacker. For early ticks and small IDs
                // it depends only on seed mod 4 (23/48/73/98), so attempts that keep
                // landing on one tick residue can miss every time. Cycle residues so
                // at least three of every four attempts roll a hit.
                var requestedTick = attackerClient.SuggestedCommandTick(model.CurrentTick);
                requestedTick += (ulong)((attempt - (int)(requestedTick % 4) + 4) % 4);
                var sequence = attackerClient.Queue(new Command {
                    AttackRequested = new AttackRequested {
                        TargetEntityId = victim.EntityId,
                    },
                }, requestedTick);
                await attackerClient.PollAsync(TestContext.Current.CancellationToken);
                var acknowledgement = await WaitForAcknowledgementAsync(attackerClient, sequence);
                Assert.True(
                    acknowledgement.Status == CommandStatus.Accepted,
                    $"Melee block attempt {attempt} was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; client tick was {model.CurrentTick}.");

                var action = await WaitForMessageAsync(
                    attackerClient,
                    message => message.Snapshot?.Players.Single(
                        player => player.EntityId == attackerEntityId).CurrentAction is {
                            Kind: PlayerActionKind.Attack,
                            StartedAtTick: var startTick,
                            TargetEntityId: var targetEntityId,
                        } && startTick == acknowledgement.AppliedTick
                            && targetEntityId == victim.EntityId);
                model.Apply(action);
                var effectTick = action.Snapshot!.Players.Single(
                    player => player.EntityId == attackerEntityId).CurrentAction!.EffectTick;
                var endTick = action.Snapshot.Players.Single(
                    player => player.EntityId == attackerEntityId).CurrentAction!.EndTick;
                var victimLifeBeforeImpact = action.Snapshot.Players.Single(
                    player => player.EntityId == victim.EntityId).Life;
                var resolved = await WaitForMessageAsync(
                    attackerClient,
                    message => {
                        if (message.Snapshot is not { Tick: var tick }
                            || tick < effectTick)
                            return false;
                        var candidate = message.Snapshot.Players.Single(
                            player => player.EntityId == victim.EntityId);
                        return candidate.CombatMode == PlayerCombatMode.Block
                            || candidate.Life < victimLifeBeforeImpact
                            || tick >= endTick;
                    });
                model.Apply(resolved);
                var candidate = resolved.Snapshot!.Players.Single(
                    player => player.EntityId == victim.EntityId);
                attemptTrace.Add($"attempt {attempt}: requested {requestedTick}, ack {acknowledgement.AppliedTick}, effect {effectTick}, end {endTick}; "
                    + $"action [{Describe(action.Snapshot)}]; resolved [{Describe(resolved.Snapshot)}]");
                if (candidate.CombatMode == PlayerCombatMode.Block) {
                    blockedVictim = candidate;
                    Assert.Equal(victimLifeBeforeImpact, candidate.Life);
                }
            }

            Assert.True(blockedVictim is not null,
                "No melee attempt observed a block:" + Environment.NewLine
                    + string.Join(Environment.NewLine, attemptTrace));
            Assert.Equal(PlayerCombatMode.Block, blockedVictim!.CombatMode);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerProjectsTheCheckedInHellfireOverlayEncounter()
    {
        var hellfireRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "hellfire");
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            hellfireRoot);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-hellfire-overlay", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var monster = Assert.Single(
            initial.Snapshot!.Monsters,
            candidate => candidate.EntityId == 10);
        var defilerQuest = Assert.Single(
            initial.Snapshot.Quests,
            candidate => candidate.QuestId == 3021);

        Assert.Equal(2U, monster.MonsterId);
        Assert.Equal(11, monster.MaxHitPoints);
        Assert.True(monster.IsUndead);
        Assert.Equal(0, defilerQuest.MessageId);
        Assert.True(defilerQuest.Initial);
        Assert.False(defilerQuest.Logged);
    }

    [Fact]
    public async Task LiveServerProjectsHellfireLittleGirlPreActivationHandIn()
    {
        var hellfireRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "hellfire");
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            hellfireRoot);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-hellfire-little-girl", requestedClassId: 2));
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        model.Apply(await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null));

        var completion = Assert.Single(
            model.Snapshot!.QuestTownerInteractions,
            interaction => interaction.InteractionId == 301903);
        Assert.Equal(3019U, completion.QuestId);
        Assert.Equal(11, completion.TownerId);
        Assert.Equal(2102U, completion.ItemId);
        Assert.Equal(285, completion.MessageId);
        Assert.True(completion.ConsumeItem);
        Assert.True(completion.AllowInitial);
        Assert.True(completion.UseQuestTurnInReward);
    }

    [Fact]
    public async Task LiveServerConsumesTheHellfireSorcererFireboltStaff()
    {
        var hellfireRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "hellfire");
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            hellfireRoot,
            randomSeed: 0x00000001);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-hellfire-sorcerer", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        var staff = Assert.Single(player.Equipment, item => item.Slot == 4);

        Assert.Equal(1, staff.State.SpellId);
        Assert.Equal(166, staff.State.ItemIndex);
        Assert.Equal("Short Staff of Charged Bolt", staff.State.BaseName);
        Assert.Equal("Short Staff of Charged Bolt", staff.State.IdentifiedName);
        Assert.Equal(109, staff.State.NativeCursorGraphic);
        Assert.Equal(40, staff.State.Charges);
        Assert.Equal(40, staff.State.MaxCharges);
        Assert.Equal(ItemSpellSourceKind.ChargedItem, staff.State.SpellSourceKind);

        var sequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = (uint)staff.State.SpellId,
                // Fire away from the adjacent monsters so the live snapshot can
                // observe the projectile before an immediate hit removes it.
                TargetX = player.PositionX,
                TargetY = player.PositionY + 8,
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
            $"Hellfire Firebolt was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; staff item was {staff.State.ItemIndex} with {staff.State.Charges} charges.");

        var consumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot is { } snapshot && snapshot.Players.Single().Equipment
                .Any(item => item.Slot == 4 && item.State.StableItemId == staff.State.StableItemId
                    && item.State.Charges == 39)
                && snapshot.Projectiles.Any(projectile =>
                    projectile.SourceEntityId == player.EntityId && projectile.SpellId == 1));
        var updatedPlayer = Assert.Single(consumed.Snapshot!.Players);
        var updatedStaff = Assert.Single(updatedPlayer.Equipment, item => item.Slot == 4);
        Assert.Equal(staff.State.StableItemId, updatedStaff.State.StableItemId);
        Assert.Equal(39, updatedStaff.State.Charges);
        Assert.Equal(player.Mana, updatedPlayer.Mana);
        var bolt = Assert.Single(consumed.Snapshot.Projectiles, projectile =>
            projectile.SourceEntityId == player.EntityId && projectile.SpellId == 1);
        Assert.Equal((int)AuthoritativeProjectileBehavior.Firebolt, bolt.Behavior);
        Assert.True(bolt.Damage > 0);
        // A native Firebolt may miss. Verify the live source and lifecycle;
        // deterministic replay tests own exact hit/damage/RNG expectations.
        _ = await WaitForMessageAsync(client, message =>
            message.Snapshot is { } snapshot && snapshot.Tick >= consumed.Snapshot.Tick
                && snapshot.Projectiles.All(projectile => projectile.EntityId != bolt.EntityId));
    }

    [Fact]
    public async Task LiveServerUsesBardIdentifyAbilityOnEquipment()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-bard-identify", requestedClassId: 4));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(4U, player.CharacterClassId);
        Assert.Equal(5U, player.AbilitySpellId);
        Assert.Equal(PlayerAbilityKind.Identify, player.AbilityKind);
        var item = Assert.Single(player.Equipment, candidate => candidate.Slot == 4);
        Assert.False(item.State.Identified);

        var sequence = client.Queue(new Command {
            IdentifyItemRequested = new IdentifyItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Bard Identify was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; class was {player.CharacterClassId}, ability was {player.AbilitySpellId}.");

        var identified = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Equipment
                .Single(candidate => candidate.Slot == 4).State.Identified == true);
        Assert.True(
            Assert.Single(identified.Snapshot!.Players).Equipment
                .Single(candidate => candidate.Slot == 4).State.Identified);
    }

    [Fact]
    public async Task LiveServerUsesBardIdentifyAbilityOnAnOpenStashItem()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-bard-identify-stash", requestedClassId: 4));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        await MoveToProjectedTownerAsync(client, model, 7);
        var initialPlayer = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(PlayerAbilityKind.Identify, initialPlayer.AbilityKind);
        var equipment = Assert.Single(initialPlayer.Equipment, item => item.Slot == 4);
        var stableItemId = equipment.State.StableItemId;
        Assert.NotEqual(0U, stableItemId);
        Assert.False(equipment.State.Identified);

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

        var identifySequence = client.Queue(new Command {
            IdentifyItemRequested = new IdentifyItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, identifySequence)).Status);

        var identified = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Stash.Any(item =>
                item.Page == 0
                && item.Cell == 0
                && item.Item.State.StableItemId == stableItemId
                && item.Item.State.Identified) == true);
        var identifiedItem = Assert.Single(identified.Snapshot!.Players).Stash
            .Single(item => item.Page == 0 && item.Cell == 0);
        Assert.Equal(stableItemId, identifiedItem.Item.State.StableItemId);
        Assert.True(identifiedItem.Item.State.Identified);
    }

    [Fact]
    public async Task LiveServerUsesWarriorRepairAbilityOnFullyRepairedEquipment()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-warrior-repair", requestedClassId: 0));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(0U, player.CharacterClassId);
        Assert.Equal(26U, player.AbilitySpellId);
        Assert.Equal(PlayerAbilityKind.Repair, player.AbilityKind);
        var equipment = Assert.Single(player.Equipment, item => item.Slot == 4);
        Assert.Equal(1, equipment.State.ItemIndex);
        Assert.Equal(24, equipment.State.Durability);
        Assert.Equal(24, equipment.State.MaxDurability);

        var sequence = client.Queue(new Command {
            RepairItemRequested = new RepairItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Warrior Repair was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; equipment was {equipment.State.Durability}/{equipment.State.MaxDurability}.");

        var repaired = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Equipment
                .Single(item => item.Slot == 4).State is { Durability: 24, MaxDurability: 24 });
        var repairedEquipment = Assert.Single(repaired.Snapshot!.Players).Equipment
            .Single(item => item.Slot == 4).State;
        Assert.Equal(24, repairedEquipment.Durability);
        Assert.Equal(24, repairedEquipment.MaxDurability);
    }

    [Fact]
    public async Task LiveServerUsesWarriorRepairAbilityOnAnOpenStashItem()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingLevelOverride: 0);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-warrior-repair-stash", requestedClassId: 0));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        await MoveToProjectedTownerAsync(client, model, 7);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(PlayerAbilityKind.Repair, player.AbilityKind);
        var equipment = Assert.Single(player.Equipment, item => item.Slot == 4);
        // Native class repair accepts an already-full item without consuming durability.
        Assert.Equal(24, equipment.State.Durability);
        Assert.Equal(24, equipment.State.MaxDurability);
        var stableItemId = equipment.State.StableItemId;
        Assert.NotEqual(0U, stableItemId);

        var openSequence = client.Queue(new Command {
            OpenStashRequested = new OpenStashRequested(),
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var openAcknowledgement = await WaitForAcknowledgementAsync(client, openSequence);
        Assert.True(
            openAcknowledgement.Status == CommandStatus.Accepted,
            $"Warrior stash entry was {openAcknowledgement.Status}/{openAcknowledgement.RejectReason} at tick {openAcknowledgement.AppliedTick}; player was at ({player.PositionX},{player.PositionY}) on level {player.LevelId}.");
        var opened = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().StashOpen == true);
        model.Apply(opened);

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

        var repairSequence = client.Queue(new Command {
            RepairItemRequested = new RepairItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var repairAcknowledgement = await WaitForAcknowledgementAsync(client, repairSequence);
        Assert.True(
            repairAcknowledgement.Status == CommandStatus.Accepted,
            $"Warrior stash repair was {repairAcknowledgement.Status}/{repairAcknowledgement.RejectReason} at tick {repairAcknowledgement.AppliedTick}.");
        var repaired = await WaitForMessageAsync(
            client,
            message => message.Snapshot is { } snapshot
                && snapshot.Tick >= repairAcknowledgement.AppliedTick
                && snapshot.Players.Single().Stash.Any(item =>
                item.Page == 0
                && item.Cell == 0
                && item.Item.State.StableItemId == stableItemId
                && item.Item.State.Durability == 24
                && item.Item.State.MaxDurability == 24));
        var repairedItem = Assert.Single(repaired.Snapshot!.Players).Stash
            .Single(item => item.Page == 0 && item.Cell == 0);
        Assert.Equal(24, repairedItem.Item.State.Durability);
        Assert.Equal(24, repairedItem.Item.State.MaxDurability);
    }

    [Fact]
    public async Task LiveServerUsesRogueTrapDisarmAbilityOnArmedObject()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Trap-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.trap-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "objects.tsv",
                      "keys": ["entity_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "objects.tsv"),
                "entity_id\tobject_id\tobject_symbol\tlevel_id\tlevel_symbol\tposition_x\tposition_y\tactivated\tquest_id\tquest_symbol\teffect_kind\teffect_amount\teffect_item_symbol\tmissile_passable\tbreakable\tdestroyed\ttrap_armed\ttemplate_only\tquest_sequence_follow_up_kind\tquest_sequence_follow_up_amount\tquest_sequence_follow_up_monster_symbol\tquest_sequence_follow_up_offset_x\tquest_sequence_follow_up_offset_y\tquest_sequence_follow_up_world_mutation_id\teffect_spell_symbol\tprojectile_target_offset_x\tprojectile_target_offset_y\tprojectile_source\tquest_transition_kind\tquest_transition_amount\tquest_transition_required_variable1\tquest_transition_required_variable2\tactivate_on_occupancy\trequired_activated_linked_objects\tnative_object_id\treward_kind\treward_id\treward_item_level\treward_offset_x\treward_offset_y\tdialogue_message_id\n2001\t1\tl1_left_door\t1\tcathedral\t0\t1\t0\t0\t\tNone\t0\t\t0\t0\t0\t1\t0\tNone\t0\t\t0\t0\t0\t\t0\t0\tObject\tNone\t0\t\t\tfalse\t0\t0\tNone\t0\t0\t0\t0\t0\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-rogue-trap-disarm", requestedClassId: 1));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var player = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(1U, player.CharacterClassId);
            Assert.Equal(28U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.TrapDisarm, player.AbilityKind);
            var trap = Assert.Single(model.Snapshot.Objects, @object => @object.EntityId == 2001);
            Assert.True(trap.TrapArmed);
            Assert.Equal(
                Devilution.Protocol.V1.ObjectPresentationKind.Trap,
                trap.PresentationKind);

            await using var warriorClient = new AuthoritativeClient(
                CreateOptions(server, "godot-warrior-trap-view", requestedClassId: 0));
            await warriorClient.ConnectAsync(TestContext.Current.CancellationToken);
            var warriorInitial = await WaitForMessageAsync(
                warriorClient,
                message => message.Snapshot is not null);
            var warriorModel = new AuthoritativeClientModel();
            warriorModel.Apply(warriorInitial);
            Assert.Contains(
                warriorModel.Snapshot!.Players,
                player => player.CharacterClassId == 0);
            Assert.False(
                Assert.Single(
                    warriorModel.Snapshot.Objects,
                    @object => @object.EntityId == 2001).TrapArmed);

            var sequence = client.Queue(new Command {
                DisarmTrapRequested = new DisarmTrapRequested { ObjectEntityId = trap.EntityId },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Rogue Trap Disarm was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; trap {trap.EntityId} was armed.");

            var disarmed = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Objects
                    .Single(@object => @object.EntityId == trap.EntityId).TrapArmed == false);
            Assert.False(
                Assert.Single(disarmed.Snapshot!.Objects, @object => @object.EntityId == trap.EntityId).TrapArmed);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesMonkSearchAbilityAsAuthoritativeStatus()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-monk-search", requestedClassId: 3));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(3U, player.CharacterClassId);
        Assert.Equal(46U, player.AbilitySpellId);
        Assert.Equal(PlayerAbilityKind.Search, player.AbilityKind);
        Assert.DoesNotContain(
            player.StatusEffects,
            effect => effect.Behavior == StatusEffectBehavior.Search);

        var sequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = 46,
                Source = CastSourceKind.Ability,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Monk Search was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; class was {player.CharacterClassId}, ability was {player.AbilitySpellId}.");

        var searched = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().StatusEffects
                .Any(effect => effect.Behavior == StatusEffectBehavior.Search
                    && effect.RemainingTicks > 0) == true);
        var search = Assert.Single(
            Assert.Single(searched.Snapshot!.Players).StatusEffects,
            effect => effect.Behavior == StatusEffectBehavior.Search);
        Assert.True(search.RemainingTicks > 0);
        Assert.Equal(0, search.Magnitude);
    }

    [Fact]
    public async Task LiveServerUsesBarbarianRageAbilityController()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-barbarian-rage", requestedClassId: 5));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(5U, player.CharacterClassId);
        Assert.Equal(22U, player.AbilitySpellId);
        Assert.Equal(PlayerAbilityKind.Rage, player.AbilityKind);
        var initialLife = player.Life;

        var sequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = 22,
                Source = CastSourceKind.Ability,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Barbarian Rage was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; class was {player.CharacterClassId}, ability was {player.AbilitySpellId}.");

        var active = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Projectiles.Any(projectile =>
                projectile.SourceEntityId == player.EntityId
                && projectile.SpellId == 22
                && projectile.Behavior == 34
                && projectile.RemainingTicks > 0) == true);
        var activePlayer = Assert.Single(active.Snapshot!.Players);
        Assert.InRange(activePlayer.Life, 1, initialLife);
        var rage = Assert.Single(
            active.Snapshot.Projectiles,
            projectile => projectile.SourceEntityId == player.EntityId
                && projectile.SpellId == 22
                && projectile.Behavior == 34);
        Assert.True(rage.RemainingTicks > 0);
        Assert.Equal(Devilution.Protocol.V1.ProjectilePresentationKind.Hidden, rage.PresentationKind);
    }

    [Fact]
    public async Task LiveServerUsesEtherealizeFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Etherealize-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.etherealize-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t53\tetherealize\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-etherealize-overlay", requestedClassId: 2));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var player = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(2U, player.CharacterClassId);
            Assert.Equal(27U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.Recharge, player.AbilityKind);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 53);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Etherealize", spell.DisplayName);

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 53,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Etherealize was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; memorized spell was {spell.SpellId} level {spell.Level}.");

            var etherealized = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single().StatusEffects
                    .Any(effect => effect.Behavior == StatusEffectBehavior.Etherealize
                        && effect.RemainingTicks > 0) == true);
            var effect = Assert.Single(
                Assert.Single(etherealized.Snapshot!.Players).StatusEffects,
                candidate => candidate.Behavior == StatusEffectBehavior.Etherealize);
            Assert.True(effect.RemainingTicks > 0);
            Assert.Equal(0, effect.Magnitude);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesReflectFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Reflect-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.reflect-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t43\treflect\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-reflect-overlay", requestedClassId: 2));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var player = Assert.Single(model.Snapshot!.Players);
            Assert.Equal(2U, player.CharacterClassId);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 43);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Reflect", spell.DisplayName);
            Assert.Equal(0U, player.ReflectCharges);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 43,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var reflected = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single().ReflectCharges > 0);
            var reflectedPlayer = Assert.Single(reflected.Snapshot!.Players);
            Assert.Equal(1U, reflectedPlayer.ReflectCharges);
            Assert.True(reflectedPlayer.Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesGolemFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Golem-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.golem-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t52\tgolem\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 1);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-golem-overlay", requestedClassId: 2));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 52);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Golem", spell.DisplayName);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 52,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var summoned = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Monsters.Any(monster =>
                    monster.IsPlayerMinion
                    && monster.OwnerPlayerId == player.EntityId) == true);
            var golem = Assert.Single(
                summoned.Snapshot!.Monsters,
                monster => monster.IsPlayerMinion
                    && monster.OwnerPlayerId == player.EntityId);
            Assert.True(golem.Alive);
            Assert.True(golem.HitPoints > 0);
            Assert.True(summoned.Snapshot.Players.Single().Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerAcceptsImmolationFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Immolation-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.immolation-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t45\timmolation\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLifeOverride: 6400,
                startingManaOverride: 10000,
                randomSeed: 1);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-immolation-overlay", requestedClassId: 2));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 45);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Immolation", spell.DisplayName);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 45,
                    TargetX = player.PositionX,
                    TargetY = player.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Immolation cast was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}.");

            var cast = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single().CurrentAction is {
                    Kind: PlayerActionKind.Cast,
                });
            var castAction = cast.Snapshot!.Players.Single().CurrentAction!;
            Assert.Equal(45U, castAction.Cast.SpellId);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerAcceptsNovaFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Nova-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.nova-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t18\tnova\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLifeOverride: 6400,
                startingManaOverride: 10000,
                randomSeed: 1);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-nova-overlay", requestedClassId: 2));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 18);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Nova", spell.DisplayName);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 18,
                    TargetX = player.PositionX,
                    TargetY = player.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var cast = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single().CurrentAction is {
                    Kind: PlayerActionKind.Cast,
                });
            var castAction = cast.Snapshot!.Players.Single().CurrentAction!;
            Assert.Equal(18U, castAction.Cast.SpellId);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesTeleportFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Teleport-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.teleport-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t23\tteleport\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 1);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-teleport-overlay", requestedClassId: 2));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var model = new AuthoritativeClientModel();
            model.Apply(initial);
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 23);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Teleport", spell.DisplayName);
            var initialMana = player.Mana;
            var targetX = player.PositionX + 3;
            var targetY = player.PositionY;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 23,
                    TargetX = targetX,
                    TargetY = targetY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);

            var arrived = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single().PositionX == targetX
                    && message.Snapshot.Players.Single().PositionY == targetY);
            var arrivedPlayer = Assert.Single(arrived.Snapshot!.Players);
            Assert.Equal((targetX, targetY), (arrivedPlayer.PositionX, arrivedPlayer.PositionY));
            Assert.True(arrivedPlayer.Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesTelekinesisFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Telekinesis-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.telekinesis-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t33\ttelekinesis\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                gameMode: "single-player",
                randomSeed: 0);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-telekinesis-overlay", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 33);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Telekinesis", spell.DisplayName);
            Assert.Equal(SpellTargetingKind.WorldEntity, spell.TargetingKind);
            var target = Assert.Single(
                model.Snapshot.Monsters,
                monster => monster.EntityId == 9 && monster.Alive);
            var initialPosition = (target.PositionX, target.PositionY);
            var directionX = Math.Sign(target.PositionX - player.PositionX);
            var directionY = Math.Sign(target.PositionY - player.PositionY);
            var expectedPosition = (
                target.PositionX + directionX,
                target.PositionY + directionY);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 33,
                    TargetEntityId = target.EntityId,
                    TargetX = target.PositionX,
                    TargetY = target.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var moved = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Monsters.Any(
                    monster => monster.EntityId == target.EntityId
                        && (monster.PositionX, monster.PositionY) == expectedPosition) == true);
            var movedTarget = Assert.Single(
                moved.Snapshot!.Monsters,
                monster => monster.EntityId == target.EntityId);
            Assert.Equal(expectedPosition, (movedTarget.PositionX, movedTarget.PositionY));
            Assert.NotEqual(initialPosition, expectedPosition);
            Assert.True(moved.Snapshot.Players.Single().Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesManaShieldFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-ManaShield-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.mana-shield-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t11\tmana_shield\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                gameMode: "single-player",
                randomSeed: 0);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-mana-shield-overlay", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 11);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Mana Shield", spell.DisplayName);
            Assert.False(player.ManaShieldActive);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 11,
                    TargetX = player.PositionX,
                    TargetY = player.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var active = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single() is { } candidate
                    && candidate.ManaShieldActive
                    && candidate.Mana < initialMana);
            var activePlayer = Assert.Single(active.Snapshot!.Players);
            Assert.True(activePlayer.ManaShieldActive);
            Assert.True(activePlayer.Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerProjectsManaShieldToRemoteParticipantAndRestoresItOnReconnect()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-ManaShield-Multiplayer-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.mana-shield-multiplayer",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t11\tmana_shield\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                gameMode: "multiplayer",
                randomSeed: 0);
            await using var first = new AuthoritativeClient(
                CreateOptions(server, "godot-mana-shield-first", requestedClassId: 2));
            await first.ConnectAsync(TestContext.Current.CancellationToken);
            var firstInitial = await WaitForMessageAsync(
                first,
                message => message.Snapshot is { Players.Count: 1 });
            var firstModel = new AuthoritativeClientModel();
            firstModel.Apply(firstInitial);
            var firstPlayer = Assert.Single(firstInitial.Snapshot!.Players);
            var firstEntityId = firstPlayer.EntityId;
            var initialMana = firstPlayer.Mana;
            var sessionToken = first.SessionToken;

            await using var second = new AuthoritativeClient(
                CreateOptions(server, "godot-mana-shield-second", requestedClassId: 2));
            await second.ConnectAsync(TestContext.Current.CancellationToken);
            var secondInitial = await WaitForMessageAsync(
                second,
                message => message.Snapshot is { Players.Count: 2 });
            Assert.Contains(
                secondInitial.Snapshot!.Players,
                player => player.EntityId == firstEntityId);

            var sequence = first.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 11,
                    TargetX = firstPlayer.PositionX,
                    TargetY = firstPlayer.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, first.SuggestedCommandTick(firstModel.CurrentTick));
            await first.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(first, sequence)).Status);

            var firstActive = await WaitForMessageAsync(
                first,
                message => message.Snapshot?.Players.SingleOrDefault(
                    player => player.EntityId == firstEntityId) is { } candidate
                    && candidate.ManaShieldActive
                    && candidate.Mana < initialMana);
            var activeFirst = firstActive.Snapshot!.Players.Single(
                player => player.EntityId == firstEntityId);

            var remoteActive = await WaitForMessageAsync(
                second,
                message => message.Snapshot?.Players.SingleOrDefault(
                    player => player.EntityId == firstEntityId) is { } candidate
                    && candidate.ManaShieldActive
                    && candidate.Mana == activeFirst.Mana);
            var activeRemote = remoteActive.Snapshot!.Players.Single(
                player => player.EntityId == firstEntityId);
            Assert.Equal(activeFirst.Mana, activeRemote.Mana);
            Assert.Equal(activeFirst.ManaMaximum, activeRemote.ManaMaximum);

            await first.ReconnectAsync(TestContext.Current.CancellationToken);
            Assert.Equal(sessionToken, first.SessionToken);
            var resumed = await WaitForMessageAsync(
                first,
                message => message.Snapshot is { Players.Count: 2 }
                    && message.Snapshot.Players.SingleOrDefault(
                        player => player.EntityId == firstEntityId) is { } candidate
                    && candidate.ManaShieldActive
                    && candidate.Mana == activeFirst.Mana);
            var resumedFirst = resumed.Snapshot!.Players.Single(
                player => player.EntityId == firstEntityId);
            Assert.True(resumedFirst.ManaShieldActive);
            Assert.Equal(activeFirst.Mana, resumedFirst.Mana);
            Assert.True(second.IsConnected);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesBerserkFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Berserk-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.berserk-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t48\tberserk\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                gameMode: "single-player",
                randomSeed: 0);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-berserk-overlay", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 48);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Berserk", spell.DisplayName);
            Assert.Equal(SpellTargetingKind.CombatEntity, spell.TargetingKind);
            var target = Assert.Single(
                model.Snapshot.Monsters,
                monster => monster.EntityId == 9 && monster.Alive);
            Assert.False(target.IsBerserked);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 48,
                    TargetEntityId = target.EntityId,
                    TargetX = target.PositionX,
                    TargetY = target.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var berserked = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Monsters.Any(
                    monster => monster.EntityId == target.EntityId
                        && monster.IsBerserked) == true);
            var berserkedTarget = Assert.Single(
                berserked.Snapshot!.Monsters,
                monster => monster.EntityId == target.EntityId);
            Assert.True(berserkedTarget.IsBerserked);
            Assert.True(berserked.Snapshot.Players.Single().Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerProjectsRingOfFireSegmentsFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-RingOfFire-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.ring-of-fire-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t44\tring_of_fire\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 1);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-ring-of-fire-overlay", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 44);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Ring of Fire", spell.DisplayName);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 44,
                    TargetX = player.PositionX,
                    TargetY = player.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var projected = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Projectiles.Any(projectile =>
                    projectile.SpellId == 44
                    && projectile.Behavior == 23) == true);
            var segments = projected.Snapshot!.Projectiles
                .Where(projectile => projectile.SpellId == 44 && projectile.Behavior == 23)
                .ToArray();
            // The generated starting level may occlude part of the native radius-three ring.
            Assert.InRange(segments.Length, 1, 24);
            Assert.All(segments, projectile =>
                Assert.Equal(Devilution.Protocol.V1.ProjectilePresentationKind.Fire, projectile.PresentationKind));
            Assert.True(projected.Snapshot.Players.Single().Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerProjectsLightningWallSegmentsWithLightningVisuals()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-LightningWall-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.lightning-wall-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t50\tlightning_wall\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 1);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-lightning-wall-overlay", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 50);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Lightning Wall", spell.DisplayName);
            var target = Assert.Single(
                model.Snapshot.Monsters,
                monster => monster.EntityId == 9 && monster.Alive);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 50,
                    TargetX = target.PositionX,
                    TargetY = target.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var projected = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Projectiles.Any(projectile =>
                    projectile.SpellId == 50
                    && projectile.Behavior == 23
                    && projectile.PresentationKind == Devilution.Protocol.V1.ProjectilePresentationKind.Lightning) == true);
            var segments = projected.Snapshot!.Projectiles
                .Where(projectile => projectile.SpellId == 50 && projectile.Behavior == 23)
                .ToArray();
            Assert.NotEmpty(segments);
            Assert.All(segments, segment => {
                Assert.Equal(
                    Devilution.Protocol.V1.ProjectilePresentationKind.Lightning,
                    segment.PresentationKind);
                Assert.Equal(
                    Devilution.Protocol.V1.NativeProjectileVisual.LightningWall,
                    segment.NativeVisual);
            });
            Assert.True(projected.Snapshot.Players.Single().Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesStoneCurseFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-StoneCurse-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.stone-curse-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t8\tstone_curse\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 0);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-stone-curse-overlay", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 8);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Stone Curse", spell.DisplayName);
            Assert.Equal(SpellTargetingKind.Tile, spell.TargetingKind);
            var target = Assert.Single(
                model.Snapshot.Monsters,
                monster => monster.EntityId == 9 && monster.Alive);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 8,
                    TargetX = target.PositionX,
                    TargetY = target.PositionY,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var petrified = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Monsters.Any(
                    monster => monster.EntityId == target.EntityId && monster.Petrified) == true);
            var petrifiedTarget = Assert.Single(
                petrified.Snapshot!.Monsters,
                monster => monster.EntityId == target.EntityId);
            Assert.True(petrifiedTarget.Petrified);
            Assert.True(petrified.Snapshot.Players.Single().Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesPhasingFromExternalMemorizedLoadout()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Phasing-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.phasing-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "class_loadouts.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "class_loadouts.tsv"),
                "class_id\tclass_symbol\tstarting_gold\tability_spell_id\tability_spell_symbol\tability_kind\tstarting_spell_id\tstarting_spell_symbol\tstarting_spell_level\n2\tsorcerer\t100\t27\trecharge\tRecharge\t10\tphasing\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingManaOverride: 10000,
                randomSeed: 0x12345678);
            await using var client = new AuthoritativeClient(
                CreateOptions(server, "godot-phasing-overlay", requestedClassId: 2));
            var model = new AuthoritativeClientModel();

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            model.Apply(await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null));
            var player = Assert.Single(model.Snapshot!.Players);
            var spell = Assert.Single(
                player.MemorizedSpells,
                memorized => memorized.SpellId == 10);
            Assert.Equal(1U, spell.Level);
            Assert.Equal("Phasing", spell.DisplayName);
            Assert.Equal(SpellTargetingKind.Tile, spell.TargetingKind);
            var initialPosition = (player.PositionX, player.PositionY);
            var initialMana = player.Mana;

            var sequence = client.Queue(new Command {
                CastRequested = new CastRequested {
                    SpellId = 10,
                    Source = CastSourceKind.MemorizedSpell,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                CommandStatus.Accepted,
                (await WaitForAcknowledgementAsync(client, sequence)).Status);

            var phased = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.Players.Single() is { } candidate
                    && (candidate.PositionX, candidate.PositionY) != initialPosition);
            var phasedPlayer = Assert.Single(phased.Snapshot!.Players);
            Assert.NotEqual(initialPosition, (phasedPlayer.PositionX, phasedPlayer.PositionY));
            Assert.True(phasedPlayer.Mana < initialMana);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesSorcererRechargeAbilityOnStaff()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            randomSeed: 1);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-sorcerer-recharge", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(2U, player.CharacterClassId);
        Assert.Equal(27U, player.AbilitySpellId);
        Assert.Equal(PlayerAbilityKind.Recharge, player.AbilityKind);
        var target = Assert.Single(
            model.Snapshot.Monsters,
            monster => monster.EntityId == 9 && monster.Alive);
        var staff = Assert.Single(player.Equipment, item => item.Slot == 4);
        Assert.Equal(30, staff.State.SpellId);
        Assert.Equal(40, staff.State.Charges);
        Assert.Equal(40, staff.State.MaxCharges);

        var castSequence = client.Queue(new Command {
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
        var castAcknowledgement = await WaitForAcknowledgementAsync(client, castSequence);
        Assert.True(
            castAcknowledgement.Status == CommandStatus.Accepted,
            $"Sorcerer charged cast was {castAcknowledgement.Status}/{castAcknowledgement.RejectReason} at tick {castAcknowledgement.AppliedTick}.");
        var consumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Equipment
                .Single(item => item.Slot == 4).State.Charges == 39);
        model.Apply(consumed);

        var rechargeSequence = client.Queue(new Command {
            RechargeItemRequested = new RechargeItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var rechargeAcknowledgement = await WaitForAcknowledgementAsync(client, rechargeSequence);
        Assert.True(
            rechargeAcknowledgement.Status == CommandStatus.Accepted,
            $"Sorcerer Recharge was {rechargeAcknowledgement.Status}/{rechargeAcknowledgement.RejectReason} at tick {rechargeAcknowledgement.AppliedTick}; staff had {staff.State.Charges}/{staff.State.MaxCharges} charges before the cast.");

        var recharged = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Equipment
                .Single(item => item.Slot == 4).State is { Charges: 39, MaxCharges: 39 });
        var rechargedStaff = Assert.Single(recharged.Snapshot!.Players).Equipment
            .Single(item => item.Slot == 4).State;
        Assert.Equal(39, rechargedStaff.Charges);
        Assert.Equal(39, rechargedStaff.MaxCharges);
    }

    [Fact]
    public async Task LiveServerUsesSorcererRechargeAbilityOnAnOpenStashStaff()
    {
        var townLoopPreviewRoot = Path.Combine(
            FindRepositoryRoot(),
            "server",
            "content",
            "town-loop-preview");
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            randomSeed: 1,
            additionalContentOverlayRoot: townLoopPreviewRoot,
            startingLevelOverride: 1);
        await using var client = new AuthoritativeClient(
            CreateOptions(server, "godot-sorcerer-recharge-stash", requestedClassId: 2));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(
            client,
            message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal(PlayerAbilityKind.Recharge, player.AbilityKind);
        var target = Assert.Single(
            model.Snapshot.Monsters,
            monster => monster.EntityId == 9 && monster.Alive);
        var staff = Assert.Single(player.Equipment, item => item.Slot == 4);
        var stableItemId = staff.State.StableItemId;
        Assert.Equal(30, staff.State.SpellId);
        Assert.Equal(40, staff.State.Charges);
        Assert.Equal(40, staff.State.MaxCharges);

        var castSequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = (uint)staff.State.SpellId,
                TargetEntityId = target.EntityId,
                Source = CastSourceKind.ChargedItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                    StableItemId = stableItemId,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var castAcknowledgement = await WaitForAcknowledgementAsync(client, castSequence);
        Assert.True(
            castAcknowledgement.Status == CommandStatus.Accepted,
            $"Sorcerer stash charged cast was {castAcknowledgement.Status}/{castAcknowledgement.RejectReason} at tick {castAcknowledgement.AppliedTick}.");
        var consumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Equipment
                .Single(item => item.Slot == 4).State.Charges == 39);
        model.Apply(consumed);

        await MoveToProjectedWarpAsync(
            client,
            model,
            "preview_cathedral_to_town",
            castAcknowledgement.AppliedTick);
        Assert.Equal(0U, Assert.Single(model.Snapshot!.Players).LevelId);
        await MoveToProjectedTownerAsync(client, model, 7);

        var openSequence = client.Queue(new Command {
            OpenStashRequested = new OpenStashRequested(),
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var openAcknowledgement = await WaitForAcknowledgementAsync(client, openSequence);
        Assert.True(
            openAcknowledgement.Status == CommandStatus.Accepted,
            $"Sorcerer stash open was {openAcknowledgement.Status}/{openAcknowledgement.RejectReason} at tick {openAcknowledgement.AppliedTick}.");
        var opened = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().StashOpen == true);
        model.Apply(opened);

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
        var moveAcknowledgement = await WaitForAcknowledgementAsync(client, moveSequence);
        Assert.True(
            moveAcknowledgement.Status == CommandStatus.Accepted,
            $"Sorcerer stash move was {moveAcknowledgement.Status}/{moveAcknowledgement.RejectReason} at tick {moveAcknowledgement.AppliedTick}.");
        var moved = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Stash.Any(item =>
                item.Page == 0
                && item.Cell == 0
                && item.Item.State.StableItemId == stableItemId) == true);
        model.Apply(moved);

        var rechargeSequence = client.Queue(new Command {
            RechargeItemRequested = new RechargeItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = stableItemId,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var rechargeAcknowledgement = await WaitForAcknowledgementAsync(client, rechargeSequence);
        Assert.True(
            rechargeAcknowledgement.Status == CommandStatus.Accepted,
            $"Sorcerer stash recharge was {rechargeAcknowledgement.Status}/{rechargeAcknowledgement.RejectReason} at tick {rechargeAcknowledgement.AppliedTick}.");
        var recharged = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Stash.Any(item =>
                item.Page == 0
                && item.Cell == 0
                && item.Item.State.StableItemId == stableItemId
                && item.Item.State.Charges == 39
                && item.Item.State.MaxCharges == 39) == true);
        var rechargedItem = Assert.Single(recharged.Snapshot!.Players).Stash
            .Single(item => item.Page == 0 && item.Cell == 0);
        Assert.Equal(39, rechargedItem.Item.State.Charges);
        Assert.Equal(39, rechargedItem.Item.State.MaxCharges);
    }

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

    [Fact]
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

    [Fact]
    public async Task GodotClientReconnectPreservesPurchasedStateAndSecondParticipant()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingGoldOverride: 5000,
            startingLevelOverride: 0);
        await using var firstClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-store-first", requestedClassId: 0) with {
                RequestedCharacterName = "Godot-Store-First",
            });
        await firstClient.ConnectAsync(TestContext.Current.CancellationToken);
        var firstInitial = await WaitForMessageAsync(firstClient, message => message.Snapshot is not null);
        var firstModel = new AuthoritativeClientModel();
        firstModel.Apply(firstInitial);
        var firstEntityId = Assert.Single(firstModel.Snapshot!.Players).EntityId;

        await using var secondClient = new AuthoritativeClient(
            CreateOptions(server, "godot-live-store-second", requestedClassId: 1) with {
                RequestedCharacterName = "Godot-Store-Second",
            });
        await secondClient.ConnectAsync(TestContext.Current.CancellationToken);
        var secondInitial = await WaitForMessageAsync(
            secondClient,
            message => message.Snapshot is { Players.Count: 2 });
        var secondModel = new AuthoritativeClientModel();
        secondModel.Apply(secondInitial);
        var secondSnapshot = secondInitial.Snapshot!;
        var secondEntityId = secondSnapshot.Players.Single(player => player.EntityId != firstEntityId).EntityId;

        await MoveToProjectedTownerAsync(firstClient, firstModel, 0);
        await MoveToProjectedTownerAsync(secondClient, secondModel, 0, secondEntityId);

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

        var openSequence = firstClient.Queue(new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, firstClient.SuggestedCommandTick(firstModel.CurrentTick));
        await firstClient.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(firstClient, openSequence)).Status);
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

    [Fact]
    public async Task LiveServerCreatesRequestedClassesPerSessionAndResumeStateWins()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        string sorcererToken;
        await using (var sorcerer = new AuthoritativeClient(
            CreateOptions(server, "godot-sorcerer", requestedClassId: 2))) {
            await sorcerer.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                sorcerer,
                message => message.Snapshot is not null);
            var player = initial.Snapshot!.Players[0];
            Assert.Equal(2U, player.CharacterClassId);
            Assert.Equal(15, player.Attributes.Strength.Base);
            Assert.Equal(35, player.Attributes.Magic.Base);
            Assert.Equal(1920, player.LifeMaximum);
            Assert.Equal(4480, player.ManaMaximum);
            Assert.Equal(100U, player.Gold);
            Assert.Equal(27U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.Recharge, player.AbilityKind);
            var firebolt = Assert.Single(player.MemorizedSpells);
            Assert.Equal(1U, firebolt.SpellId);
            Assert.Equal(2U, firebolt.Level);
            var staff = Assert.Single(player.Equipment);
            Assert.Equal(166, staff.State.ItemIndex);
            Assert.Equal(40, staff.State.Charges);
            Assert.Equal([0U, 1U], player.Belt.Select(item => item.Slot));
            Assert.All(player.Belt, item => Assert.Equal(6, item.State.MiscId));
            var gold = Assert.Single(player.Inventory);
            Assert.Equal(100U, gold.Price);
            Assert.Equal(11, gold.State.ItemType);
            Assert.Equal(
                Devilution.Protocol.V1.ItemPresentationKind.Gold,
                gold.State.PresentationKind);
            Assert.Equal(0, player.InventoryGrid[30]);
            sorcererToken = sorcerer.SessionToken;
        }

        await using (var rogue = new AuthoritativeClient(
            CreateOptions(server, "godot-rogue", requestedClassId: 1))) {
            await rogue.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                rogue,
                message => message.Snapshot is not null);
            Assert.Equal(2, initial.Snapshot!.Players.Count);
            var player = initial.Snapshot.Players[0];
            Assert.Equal(1U, player.CharacterClassId);
            Assert.Equal(20, player.Attributes.Strength.Base);
            Assert.Equal(30, player.Attributes.Dexterity.Base);
            Assert.Equal(2880, player.LifeMaximum);
            Assert.Equal(1440, player.ManaMaximum);
            Assert.Equal(100U, player.Gold);
            Assert.Equal(28U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.TrapDisarm, player.AbilityKind);
            Assert.Empty(player.MemorizedSpells);
            Assert.Equal(4, Assert.Single(player.Equipment).State.ItemIndex);
            Assert.Equal(2, player.Belt.Count);
        }

        await using (var resumed = new AuthoritativeClient(
            CreateOptions(server, "godot-resume", requestedClassId: 1) with {
                ResumeToken = sorcererToken,
            })) {
            await resumed.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                resumed,
                message => message.Snapshot is not null);
            Assert.Equal(2, initial.Snapshot!.Players.Count);
            var player = initial.Snapshot.Players[0];
            Assert.Equal(2U, player.CharacterClassId);
            Assert.Equal(27U, player.AbilitySpellId);
            Assert.Equal(PlayerAbilityKind.Recharge, player.AbilityKind);
            Assert.Equal(1U, Assert.Single(player.MemorizedSpells).SpellId);
            Assert.Equal(166, Assert.Single(player.Equipment).State.ItemIndex);
            Assert.Equal(2, player.Belt.Count);
        }

        await using var invalid = new AuthoritativeClient(
            CreateOptions(server, "godot-invalid-class", requestedClassId: 99));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            invalid.ConnectAsync(TestContext.Current.CancellationToken));
        Assert.Contains("class ID 99", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveClientReconnectsThroughEightCyclesWithSecondParticipantConnected()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken);
        await using var first = new AuthoritativeClient(
            CreateOptions(server, "godot-live-reconnect-first", requestedClassId: 2));
        await first.ConnectAsync(TestContext.Current.CancellationToken);
        var firstInitial = await WaitForMessageAsync(
            first,
            message => message.Snapshot is not null);
        var firstEntityId = Assert.Single(firstInitial.Snapshot!.Players).EntityId;
        var sessionToken = first.SessionToken;

        await using var second = new AuthoritativeClient(
            CreateOptions(server, "godot-live-reconnect-second", requestedClassId: 1));
        await second.ConnectAsync(TestContext.Current.CancellationToken);
        var secondInitial = await WaitForMessageAsync(
            second,
            message => message.Snapshot is not null);
        var secondEntityId = secondInitial.Snapshot!.Players[0].EntityId;
        Assert.NotEqual(firstEntityId, secondEntityId);
        Assert.Equal(
            [secondEntityId, firstEntityId],
            secondInitial.Snapshot.Players.Select(player => player.EntityId));

        const int reconnectCycles = 8;
        for (var cycle = 0; cycle < reconnectCycles; cycle++) {
            await first.ReconnectAsync(TestContext.Current.CancellationToken);
            Assert.Equal(sessionToken, first.SessionToken);

            var firstResumed = await WaitForMessageAsync(
                first,
                message => message.Snapshot is { Players.Count: 2 });
            Assert.Equal(
                [firstEntityId, secondEntityId],
                firstResumed.Snapshot!.Players.Select(player => player.EntityId));
            Assert.NotEmpty(firstResumed.Snapshot.StateSha256);
            Assert.True(second.IsConnected);

            await second.PollAsync(TestContext.Current.CancellationToken);
            var secondResumed = await WaitForMessageAsync(
                second,
                message => message.Snapshot is { Players.Count: 2 });
            Assert.Equal(
                [secondEntityId, firstEntityId],
                secondResumed.Snapshot!.Players.Select(player => player.EntityId));
            Assert.NotEmpty(secondResumed.Snapshot.StateSha256);
        }
    }

    [Fact]
    public async Task LiveServerAppliesDeclaredContentOverlayToAuthoritativeStore()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Content-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.store-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "stores.tsv",
                      "keys": ["store_id", "store_slot"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "stores.tsv"),
                "store_id\tstore_symbol\tstore_role\tstore_slot\titem_seed\tprice\titem_id\titem_symbol\n1\tsmith\tSmith\t0\t42\t777\t1\tshort_sword\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLevelOverride: 0);
            await using var client = new AuthoritativeClient(new ClientConnectionOptions(
                "127.0.0.1",
                server.Port,
                "godot-overlay-test",
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
            var message = await WaitForMessageAsync(
                client,
                message => message.Snapshot?.ActiveStore?.StoreId == 1);

            Assert.Equal(777U, message.Snapshot!.ActiveStore!.Items[0].Price);
            Assert.Equal(25U, message.Snapshot.ActiveStore.Items[1].Price);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LiveServerUsesOverlaySelectedDefaultPlayerClass()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Class-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                $$"""
                {
                  "formatVersion": 1,
                  "id": "test.class-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": {{BaseContentSchemaJson}},
                  "dependencyConstraints": [
                    {
                      "id": "devilution.base",
                      "minimumVersion": "0.1.0",
                      "maximumVersion": "0.1.0"
                    }
                  ],
                  "overlays": [
                    {
                      "table": "classes.tsv",
                      "keys": ["class_id"],
                      "mode": "upsert"
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
            var classTablePath = Path.Combine(
                FindRepositoryRoot(),
                "server",
                "content",
                "base",
                "classes.tsv");
            var classLines = await File.ReadAllLinesAsync(
                classTablePath,
                TestContext.Current.CancellationToken);
            var warrior = SetDefaultClass(
                classLines.Single(line => line.StartsWith("0\t", StringComparison.Ordinal)),
                isDefault: false);
            var monk = SetDefaultClass(
                classLines.Single(line => line.StartsWith("3\t", StringComparison.Ordinal)),
                isDefault: true);
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "classes.tsv"),
                [classLines[0], warrior, monk],
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot);
            await using var client = new AuthoritativeClient(new ClientConnectionOptions(
                "127.0.0.1",
                server.Port,
                "godot-class-overlay-test",
                ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
                server.ContentHash,
                server.RulesetHash));

            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var initial = await WaitForMessageAsync(
                client,
                message => message.Snapshot is not null);
            var player = Assert.Single(initial.Snapshot!.Players);

            Assert.Equal(3U, player.CharacterClassId);
            Assert.Equal(25, player.Attributes.Strength.Base);
            Assert.Equal(15, player.Attributes.Magic.Base);
            Assert.Equal(25, player.Attributes.Dexterity.Base);
            Assert.Equal(20, player.Attributes.Vitality.Base);
            Assert.Equal(2880, player.LifeMaximum);
            Assert.Equal(1440, player.ManaMaximum);
        } finally {
            if (Directory.Exists(overlayRoot))
                Directory.Delete(overlayRoot, recursive: true);
        }
    }

    private static string SetDefaultClass(string row, bool isDefault)
    {
        var cells = row.Split('\t');
        cells[4] = isDefault ? "true" : "false";
        return string.Join('\t', cells);
    }

    private static ClientConnectionOptions CreateOptions(
        StartedServer server,
        string clientBuildId,
        uint requestedClassId)
    {
        return new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            clientBuildId,
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            server.ContentHash,
            server.RulesetHash) {
            RequestedCharacterClassId = requestedClassId,
        };
    }

    /** Never open a legacy canonical checkpoint while its File.Move publisher is live. */
    private static AuthoritativeSessionDocument ReadStoppedSessionCheckpoint(
        StartedServer server, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!server.HasExited)
            throw new InvalidOperationException("Checkpoint inspection requires confirmed owned-host exit.");
        var path = Assert.Single(Directory.EnumerateFiles(server.SaveRoot, "*.session.json"));
        var sessionId = Path.GetFileName(path)[..^".session.json".Length];
        return new AuthoritativeSessionStore(server.SaveRoot).Load(sessionId, server.RulesetHash)
            ?? throw new InvalidDataException("The stopped host has no durable session checkpoint.");
    }

    private static Snapshot ParseCheckpointSnapshot(AuthoritativeSessionDocument checkpoint)
    {
        using var save = JsonDocument.Parse(checkpoint.SerializedState);
        var snapshotBase64 = save.RootElement
            .GetProperty("SnapshotBase64")
            .GetString()
            ?? throw new InvalidDataException("The checkpoint SnapshotBase64 property is empty.");
        return Snapshot.Parser.ParseFrom(Convert.FromBase64String(snapshotBase64));
    }

    private static async Task<AuthoritativeClientMessage> WaitForMessageAsync(
        AuthoritativeClient client,
        Func<AuthoritativeClientMessage, bool> predicate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backlog = messageBacklogs.GetOrCreateValue(client);
        for (var attempt = 0; attempt < 200; attempt++) {
            lock (backlog.Synchronization) {
                foreach (var message in client.DrainMessages())
                    backlog.Pending.Enqueue(message);

                var pendingCount = backlog.Pending.Count;
                for (var index = 0; index < pendingCount; index++) {
                    var message = backlog.Pending.Dequeue();
                    if (message.Error is not null)
                        throw new InvalidOperationException(
                            $"Authoritative live-client error: {message.Error.Code}/{message.Error.Detail}");
                    if (predicate(message))
                        return message;
                    backlog.Pending.Enqueue(message);
                }
            }
            await client.PollAsync(cancellationToken);
            await Task.Delay(25, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for a live authoritative client message.");
    }

    /** Keeps this live melee assertion independent of wall-clock scheduling. */
    private static ulong FindGuaranteedHitTick(
        ulong suggestedTick,
        uint attackerEntityId,
        uint targetEntityId)
    {
        for (var offset = 0UL; offset < 100; offset++) {
            var tick = checked(suggestedTick + offset);
            var seed = unchecked((uint)tick ^ attackerEntityId ^ targetEntityId);
            var roll = unchecked(seed * 1664525U + 1013904223U) % 100U;
            if (roll < 50)
                return tick;
        }
        throw new InvalidOperationException("Could not find a deterministic player-hit tick.");
    }

    private sealed class MessageBacklog
    {
        public object Synchronization { get; } = new();

        public Queue<AuthoritativeClientMessage> Pending { get; } = [];
    }

    private static async Task<CommandResult> WaitForAcknowledgementAsync(AuthoritativeClient client, ulong sequence)
    {
        var message = await WaitForMessageAsync(client, message =>
            message.Acknowledgement?.Results.Any(result => result.ClientSequence == sequence) == true);
        return message.Acknowledgement!.Results.Single(result => result.ClientSequence == sequence);
    }

    /** Walks through acknowledged native target-cell steps to an authored level trigger. */
    private static async Task MoveToProjectedWarpAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        string warpSymbol,
        ulong minimumFreshTick = 0)
    {
        var snapshot = model.Snapshot
            ?? throw new InvalidOperationException("A live snapshot is required before native warp travel.");
        var player = snapshot.Players.FirstOrDefault()
            ?? throw new InvalidOperationException("The live snapshot has no local player.");
        var warp = model.VisibleWarpTriggers.SingleOrDefault(candidate =>
            candidate.StableSymbol == warpSymbol
            && candidate.Available);
        Assert.NotNull(warp);
        var sourceLevel = player.LevelId;
        var targetLevel = warp!.TargetLevelId;
        Assert.NotEqual(sourceLevel, targetLevel);
        var sourceEntityId = player.EntityId;
        var sourceObservationTick = Math.Max(model.CurrentTick, minimumFreshTick);
        var settledSource = await WaitForMessageAsync(
            client,
            message => message.Snapshot is { } settledSnapshot
                && settledSnapshot.Tick > sourceObservationTick
                && settledSnapshot.Players.SingleOrDefault(
                    candidate => candidate.EntityId == sourceEntityId) is { } settledPlayer
                && settledPlayer.LevelId == sourceLevel
                && settledPlayer.CombatMode == PlayerCombatMode.Stand
                && settledPlayer.CurrentAction is null);
        model.Apply(settledSource);

        for (var attempt = 0; attempt < 32; attempt++) {
            player = model.Snapshot!.Players.Single(candidate => candidate.EntityId == sourceEntityId);
            if (player.LevelId == targetLevel) {
                if (player.CombatMode == PlayerCombatMode.Stand
                    && player.CurrentAction is null)
                    return;

                var targetObservationTick = model.CurrentTick;
                var settledTarget = await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is { } settledSnapshot
                        && settledSnapshot.Tick > targetObservationTick
                        && settledSnapshot.Players.SingleOrDefault(
                            candidate => candidate.EntityId == sourceEntityId) is { } settledPlayer
                        && settledPlayer.LevelId == targetLevel
                        && settledPlayer.CombatMode == PlayerCombatMode.Stand
                        && settledPlayer.CurrentAction is null);
                model.Apply(settledTarget);
                return;
            }
            Assert.Equal(sourceLevel, player.LevelId);

            var beforePosition = (player.PositionX, player.PositionY);
            var priorObservedTick = model.CurrentTick;
            var sequence = client.Queue(new Command {
                MoveRequested = new MoveRequested {
                    TargetCell = true,
                    TargetX = warp.TriggerX,
                    TargetY = warp.TriggerY,
                },
            }, client.SuggestedCommandTick(model.CurrentTick));
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Authored warp step to '{warpSymbol}' was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}.");
            var moved = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } movedSnapshot
                    && movedSnapshot.Tick >= acknowledgement.AppliedTick
                    && movedSnapshot.Tick > priorObservedTick
                    && movedSnapshot.Players.SingleOrDefault(
                        candidate => candidate.EntityId == sourceEntityId) is { } candidate
                    && ((candidate.LevelId == targetLevel
                            && candidate.CombatMode == PlayerCombatMode.Stand
                            && candidate.CurrentAction is null)
                        || (candidate.LevelId == sourceLevel
                            && (candidate.PositionX, candidate.PositionY) != beforePosition
                            && candidate.CombatMode == PlayerCombatMode.Stand
                            && candidate.CurrentAction is null)));
            model.Apply(moved);
        }

        throw new TimeoutException(
            $"Could not reach authored warp trigger '{warpSymbol}' from the authoritative player position.");
    }

    /** Walks through acknowledged native target-cell steps to a projected towner. */
    private static async Task MoveToProjectedTownerAsync(
        AuthoritativeClient client,
        AuthoritativeClientModel model,
        int townerId,
        uint? localEntityId = null)
    {
        var snapshot = model.Snapshot
            ?? throw new InvalidOperationException("A live snapshot is required before native town travel.");
        var player = (localEntityId is uint entityId
                ? snapshot.Players.SingleOrDefault(candidate => candidate.EntityId == entityId)
                : snapshot.Players.FirstOrDefault())
            ?? throw new InvalidOperationException("The live snapshot has no local player.");
        var greeting = snapshot.TownerGreetings.SingleOrDefault(candidate =>
            candidate.TownerId == townerId && candidate.LevelId == player.LevelId);
        Assert.NotNull(greeting);

        for (var attempt = 0; attempt < 200; attempt++) {
            player = model.Snapshot!.Players.Single(candidate => candidate.EntityId == player.EntityId);
            var distance = Math.Max(
                Math.Abs(player.PositionX - greeting!.PositionX),
                Math.Abs(player.PositionY - greeting.PositionY));
            if (distance <= 1
                && player.LevelId == greeting.LevelId
                && player.CombatMode == PlayerCombatMode.Stand
                && player.CurrentAction is null)
                return;

            var beforePosition = (player.PositionX, player.PositionY);
            var priorObservedTick = model.CurrentTick;
            var requestedTick = client.SuggestedCommandTick(model.CurrentTick);
            var sequence = client.Queue(new Command {
                MoveRequested = new MoveRequested {
                    TargetCell = true,
                    TargetX = greeting.PositionX,
                    TargetY = greeting.PositionY,
                },
            }, requestedTick);
            await client.PollAsync(TestContext.Current.CancellationToken);
            var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
            if (acknowledgement.Status == CommandStatus.Rejected
                && acknowledgement.RejectReason == CommandRejectReason.TooLate) {
                // A loaded host can outrun the model tick that chose the requested
                // tick. Catch up to the host's reported tick and request again.
                model.Apply(await WaitForMessageAsync(
                    client,
                    message => message.Snapshot is { } current
                        && current.Tick >= acknowledgement.AppliedTick));
                continue;
            }
            var actionEndTick = player.CurrentAction is { } action
                ? action.EndTick.ToString()
                : "none";
            Assert.True(
                acknowledgement.Status == CommandStatus.Accepted,
                $"Projected towner {townerId} attempt {attempt} sequence {sequence} requestedTick {requestedTick} "
                    + $"ack={acknowledgement.Status}/{acknowledgement.RejectReason} appliedTick={acknowledgement.AppliedTick} "
                    + $"modelTick={model.CurrentTick} player=({player.PositionX},{player.PositionY}) level={player.LevelId} "
                    + $"mode={player.CombatMode} actionEndTick={actionEndTick} target=({greeting.PositionX},{greeting.PositionY}).");
            var moved = await WaitForMessageAsync(
                client,
                message => message.Snapshot is { } movedSnapshot
                    && movedSnapshot.Tick >= acknowledgement.AppliedTick
                    && movedSnapshot.Tick > priorObservedTick
                    && movedSnapshot.Players.SingleOrDefault(
                        candidate => candidate.EntityId == player.EntityId) is { } candidate
                    && candidate.LevelId == greeting.LevelId
                    && (candidate.PositionX, candidate.PositionY) != beforePosition
                    && candidate.CombatMode == PlayerCombatMode.Stand
                    && candidate.CurrentAction is null);
            model.Apply(moved);
        }

        throw new TimeoutException(
            $"Could not reach projected towner {townerId} from the authoritative player position.");
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "protocol/devilution.proto")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root.");
    }

    private static string ResolveBuildConfiguration()
        => LiveServerProcess.BuildConfiguration;

    private sealed class StartedServer : IDisposable
    {
        private readonly LiveServerProcess process;
        private readonly string saveRoot;
        private readonly bool deleteSaveRootOnDispose;
        private readonly bool ownsSaveRoot;
        private readonly LiveAuthoritativeServerTests owner;
        private readonly LiveTestSaveRoot? cleanupRoot;
        private LiveServerProcess.Identity identity = new(0, string.Empty, string.Empty);
        private bool startupComplete;
        private bool disposed;
        private bool retainRootAfterCleanupFailure;

        private StartedServer(
            LiveServerProcess process,
            string saveRoot,
            bool deleteSaveRootOnDispose,
            bool ownsSaveRoot,
            LiveAuthoritativeServerTests owner,
            LiveTestSaveRoot? cleanupRoot)
        {
            this.process = process;
            this.saveRoot = saveRoot;
            this.deleteSaveRootOnDispose = deleteSaveRootOnDispose;
            this.ownsSaveRoot = ownsSaveRoot;
            this.owner = owner;
            this.cleanupRoot = cleanupRoot;
        }

        public int Port => identity.Port;

        public string ContentHash => identity.ContentHash;

        public string RulesetHash => identity.RulesetHash;

        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;
        public string SaveRoot => saveRoot;

        public static async Task<StartedServer> StartAsync(
            CancellationToken cancellationToken,
            string? contentOverlayRoot = null,
            string? additionalContentOverlayRoot = null,
            uint? startingGoldOverride = null,
            int? startingLifeOverride = null,
            int? startingManaOverride = null,
            uint? startingStatPointsOverride = null,
            uint? startingLevelOverride = null,
            uint? randomSeed = null,
            string? gameMode = null,
            string? difficultyOverride = null,
            int? portOverride = null,
            string? saveRootOverride = null,
            bool deleteSaveRootOnDispose = true,
            string? gameAdapterPackageRoot = null,
            string? gameAdapterId = null,
            string? performanceOutput = null,
            CommandSchedulingMode? commandSchedulingMode = null,
            string? persistenceMode = null)
        {
            if (commandSchedulingMode.HasValue
                && commandSchedulingMode.Value is not (CommandSchedulingMode.Immediate or CommandSchedulingMode.ClockGated)) {
                throw new ArgumentOutOfRangeException(nameof(commandSchedulingMode),
                    "Live host fixtures support only Immediate and ClockGated command scheduling.");
            }
            if (persistenceMode is not null && persistenceMode is not ("legacy" or "cohort")) {
                throw new ArgumentOutOfRangeException(nameof(persistenceMode),
                    "Live host fixtures support only legacy and cohort persistence.");
            }
            var repositoryRoot = LiveAuthoritativeServerTests.FindRepositoryRoot();
            var owner = TestContext.Current.TestClassInstance as LiveAuthoritativeServerTests
                ?? throw new InvalidOperationException("Live hosts require a per-test cleanup owner.");
            var assembly = await LiveServerProcess.ResolveAssemblyAsync(
                repositoryRoot, ResolveBuildConfiguration(),
                Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH"),
                Environment.GetEnvironmentVariable("GODEVILUTION_SERVER_ASSEMBLY"),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var ownsSaveRoot = string.IsNullOrWhiteSpace(saveRootOverride);
            var saveRoot = Path.GetFullPath(saveRootOverride ?? Path.Combine(
                Path.GetTempPath(),
                "Godevilution-Godot-Test-" + Guid.NewGuid().ToString("N")));
            var cleanupRoot = ownsSaveRoot || deleteSaveRootOnDispose
                ? LiveTestSaveRoot.Register(saveRoot) : null;
            Directory.CreateDirectory(saveRoot);
            var startInfo = LiveServerProcess.CreateStartInfo(repositoryRoot, assembly);
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add((portOverride ?? 0).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--protocol-version");
            startInfo.ArgumentList.Add(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity);
            startInfo.ArgumentList.Add("--save-root");
            startInfo.ArgumentList.Add(saveRoot);
            if (performanceOutput is not null) {
                startInfo.ArgumentList.Add("--performance-output");
                startInfo.ArgumentList.Add(performanceOutput);
            }
            if (commandSchedulingMode.HasValue) {
                startInfo.ArgumentList.Add("--command-scheduling");
                startInfo.ArgumentList.Add(commandSchedulingMode.Value == CommandSchedulingMode.ClockGated
                    ? "clock-gated" : "immediate");
            }
            if (persistenceMode is not null) {
                startInfo.ArgumentList.Add("--persistence-mode");
                startInfo.ArgumentList.Add(persistenceMode);
            }
            if (startingGoldOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-gold");
                startInfo.ArgumentList.Add(startingGoldOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingLifeOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-life");
                startInfo.ArgumentList.Add(startingLifeOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingManaOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-mana");
                startInfo.ArgumentList.Add(startingManaOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingStatPointsOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-stat-points");
                startInfo.ArgumentList.Add(startingStatPointsOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (startingLevelOverride.HasValue) {
                startInfo.ArgumentList.Add("--starting-level");
                startInfo.ArgumentList.Add(startingLevelOverride.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (randomSeed.HasValue) {
                startInfo.ArgumentList.Add("--random-seed");
                startInfo.ArgumentList.Add(randomSeed.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(gameMode)) {
                startInfo.ArgumentList.Add("--game-mode");
                startInfo.ArgumentList.Add(gameMode);
            }
            if (!string.IsNullOrWhiteSpace(difficultyOverride)) {
                startInfo.ArgumentList.Add("--difficulty");
                startInfo.ArgumentList.Add(difficultyOverride);
            }
            if (!string.IsNullOrWhiteSpace(contentOverlayRoot)) {
                startInfo.ArgumentList.Add("--content-overlay");
                startInfo.ArgumentList.Add(Path.GetFullPath(contentOverlayRoot));
            }
            if (!string.IsNullOrWhiteSpace(additionalContentOverlayRoot)) {
                startInfo.ArgumentList.Add("--content-overlay");
                startInfo.ArgumentList.Add(Path.GetFullPath(additionalContentOverlayRoot));
            }
            if (!string.IsNullOrWhiteSpace(gameAdapterPackageRoot)) {
                startInfo.ArgumentList.Add("--game-adapter-package");
                startInfo.ArgumentList.Add(Path.GetFullPath(gameAdapterPackageRoot));
            }
            if (!string.IsNullOrWhiteSpace(gameAdapterId)) {
                startInfo.ArgumentList.Add("--game-adapter");
                startInfo.ArgumentList.Add(gameAdapterId);
            }
            LiveServerProcess process;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                process = LiveServerProcess.Start(startInfo);
            } catch (Exception failure) {
                if (failure.Data["LiveServerConstructionExitConfirmed"] is false) {
                    owner.cleanupFailures.Add(new InvalidOperationException(
                        "The started host did not confirm exit; retain its owned save root.", failure));
                } else if (ownsSaveRoot) {
                    try { cleanupRoot!.Delete(); } catch (Exception cleanup) {
                        failure.Data["LiveServerStartupCleanupFailure"] = cleanup;
                        owner.cleanupFailures.Add(cleanup);
                    }
                }
                throw;
            }
            var server = new StartedServer(process, saveRoot, deleteSaveRootOnDispose, ownsSaveRoot, owner, cleanupRoot);
            owner.ownedServers.Add(server);
            try {
                server.identity = await process.WaitForIdentityAsync(cancellationToken);
                server.startupComplete = true;
                return server;
            } catch (Exception failure) {
                server.Dispose();
                failure.Data["LiveServerDiagnostics"] = process.Diagnostics;
                throw;
            }
        }

        public async Task StartPerformanceRecordingAsync(CancellationToken cancellationToken)
            => await process.StartPerformanceRecordingAsync(cancellationToken);

        public async Task QuitAsync(CancellationToken cancellationToken)
        {
            await process.QuitAsync(cancellationToken);
            Assert.Equal(0, process.ExitCode);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            try {
                process.DisposeAsync().AsTask().GetAwaiter().GetResult();
                disposed = true;
                if (!retainRootAfterCleanupFailure && (startupComplete ? deleteSaveRootOnDispose : ownsSaveRoot))
                    cleanupRoot!.Delete();
            } catch (Exception failure) {
                retainRootAfterCleanupFailure = true;
                // Report at class cleanup so a using scope cannot mask its body failure.
                owner.cleanupFailures.Add(failure);
            }
        }

    }
}
