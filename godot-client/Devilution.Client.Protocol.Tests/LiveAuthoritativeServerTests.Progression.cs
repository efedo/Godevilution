using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeProgressionTests : LiveAuthoritativeTestBase
{

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
        // Observe the completed move before casting; ambient attacks and IO stalls are outside this slice.
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken,
            passiveMonsters: true, controlledClock: true);
        var time = new ManualTimeProvider();
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "godot-live-test",
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            server.ContentHash,
            server.RulesetHash) {
            TimeProvider = time,
            // Fresh observations are explicit while the host's game clock is held.
            SnapshotPollInterval = TimeSpan.Zero,
            RequestedCharacterClassId = 2,
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(50),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(500),
        });
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GameMode.Multiplayer, client.ServerGameMode);
        var initial = await WaitForGameConditionAsync(server, client, message => message.Snapshot is not null);
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
        var moved = await WaitForGameConditionAsync(server, client, message =>
            message.Snapshot?.Players.Single() is { CombatMode: PlayerCombatMode.Stand, CurrentAction: null } candidate
                && candidate.PositionY == player.PositionY + 1);
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
}
