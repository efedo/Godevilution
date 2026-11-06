using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeQuestDialogueTests : LiveAuthoritativeTestBase
{

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
}
