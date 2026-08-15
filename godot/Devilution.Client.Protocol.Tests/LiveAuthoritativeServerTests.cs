using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeServerTests
{
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
        var monster = model.Snapshot.Monsters.Single(monster => monster.Alive);

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
        var monster = model.Snapshot.Monsters.Single(monster => monster.Alive);

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
            monster => monster.Alive);

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
            CreateOptions(server, "godot-live-pvp-attacker", requestedClassId: 2));
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.heal-other-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
                "class_id\tstarting_gold\tability_spell_id\tability_kind\tstarting_spell_id\tstarting_spell_level\n2\t100\t27\tNone\t34\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLifeOverride: 64,
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.resurrection-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
                "class_id\tstarting_gold\tability_spell_id\tability_kind\tstarting_spell_id\tstarting_spell_level\n2\t100\t27\tNone\t1\t1\n3\t100\t22\tNone\t32\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot,
                startingLifeOverride: 64,
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.charged-bolt-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
                "class_id\tstarting_gold\tability_spell_id\tability_kind\tstarting_spell_id\tstarting_spell_level\n2\t100\t27\tNone\t30\t2\n",
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.bone-spirit-pvp-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
                "class_id\tstarting_gold\tability_spell_id\tability_kind\tstarting_spell_id\tstarting_spell_level\n2\t100\t27\tNone\t38\t2\n",
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.pvp-block-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
            monsterCells[3] = "20";
            monsterCells[4] = "20";
            await File.WriteAllLinesAsync(
                Path.Combine(overlayRoot, "monsters.tsv"),
                [monsterLines[0], string.Join('\t', monsterCells)],
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
            for (var attempt = 0; attempt < 8 && blockedVictim is null; attempt++) {
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
                if (candidate.CombatMode == PlayerCombatMode.Block) {
                    blockedVictim = candidate;
                    Assert.Equal(victimLifeBeforeImpact, candidate.Life);
                }
            }

            Assert.NotNull(blockedVictim);
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

        Assert.Equal(2U, monster.MonsterId);
        Assert.Equal(11, monster.MaxHitPoints);
        Assert.True(monster.IsUndead);
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
        var target = Assert.Single(model.Snapshot.Monsters, monster => monster.EntityId == 9);
        var staff = Assert.Single(player.Equipment, item => item.Slot == 4);

        Assert.Equal(1, staff.State.SpellId);
        Assert.Equal(166, staff.State.ItemIndex);
        Assert.Equal(40, staff.State.Charges);
        Assert.Equal(40, staff.State.MaxCharges);
        Assert.Equal(ItemSpellSourceKind.ChargedItem, staff.State.SpellSourceKind);

        var sequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = (uint)staff.State.SpellId,
                TargetEntityId = target.EntityId,
                Source = CastSourceKind.ChargedItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Hellfire Firebolt was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; staff item was {staff.State.ItemIndex} with {staff.State.Charges} charges and target was ({target.PositionX},{target.PositionY}).");

        var consumed = await WaitForMessageAsync(
            client,
            message => message.Snapshot?.Players.Single().Equipment
                .Single(item => item.Slot == 4).State.Charges == 39);
        var updatedPlayer = Assert.Single(consumed.Snapshot!.Players);
        Assert.Equal(39, Assert.Single(updatedPlayer.Equipment, item => item.Slot == 4).State.Charges);
        var resolved = await WaitForMessageAsync(
            client,
            message => {
                var candidate = message.Snapshot?.Monsters.FirstOrDefault(monster => monster.EntityId == target.EntityId);
                return candidate is null
                    || !candidate.Alive
                    || candidate.HitPoints < target.HitPoints;
            });
        var resolvedTarget = resolved.Snapshot!.Monsters
            .FirstOrDefault(monster => monster.EntityId == target.EntityId);
        if (resolvedTarget is not null)
            Assert.True(!resolvedTarget.Alive || resolvedTarget.HitPoints < target.HitPoints);
        else
            Assert.Contains(resolved.Snapshot.Corpses, corpse => corpse.EntityId == target.EntityId);
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
    public async Task LiveServerUsesRogueTrapDisarmAbilityOnArmedObject()
    {
        var overlayRoot = Path.Combine(
            Path.GetTempPath(),
            "Godevilution-Trap-Overlay-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(overlayRoot);
        try {
            await File.WriteAllTextAsync(
                Path.Combine(overlayRoot, "content-pack.json"),
                """
                {
                  "formatVersion": 1,
                  "id": "test.trap-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
                "entity_id\tobject_id\tlevel_id\tposition_x\tposition_y\tactivated\tquest_id\teffect_kind\teffect_amount\tmissile_passable\tbreakable\tdestroyed\ttrap_armed\n2001\t1\t1\t0\t1\t0\t0\tNone\t0\t0\t0\t0\t1\n",
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.etherealize-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
                "class_id\tstarting_gold\tability_spell_id\tability_kind\tstarting_spell_id\tstarting_spell_level\n2\t100\t27\tRecharge\t53\t1\n",
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
        var target = Assert.Single(model.Snapshot.Monsters, monster => monster.Alive);
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
                },
            },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            CommandStatus.Accepted,
            (await WaitForAcknowledgementAsync(client, castSequence)).Status);
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
        var target = Assert.Single(model.Snapshot.Monsters, monster => monster.Alive);
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
        Assert.Equal(initialCharges - 1,
            Assert.Single(updatedPlayer.Equipment, item => item.Slot == 4).State.Charges);
        Assert.Equal(0x858FBDECU, consumed.Snapshot.RandomState);
        Assert.InRange(consumed.Snapshot.Projectiles.Count, 3, 4);
        Assert.Equal([7, 8, 6],
            consumed.Snapshot.Projectiles.Select(projectile => projectile.Damage).Distinct());
        Assert.All(consumed.Snapshot.Projectiles, projectile => Assert.Equal(1, projectile.Behavior));
    }

    [Fact]
    public async Task LiveBaseStoreUsesNormalizedShippedItemDefinitions()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken,
            startingGoldOverride: 5000);
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.store-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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
                "store_id\tstore_slot\titem_seed\tprice\titem_id\n1\t0\t42\t777\t1\n",
                TestContext.Current.CancellationToken);

            using var server = await StartedServer.StartAsync(
                TestContext.Current.CancellationToken,
                overlayRoot);
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
                """
                {
                  "formatVersion": 1,
                  "id": "test.class-overlay",
                  "version": "1",
                  "dependencies": ["devilution.base"],
                  "schema": { "id": "devilution.diablo", "version": "1.196.0" },
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

    private static async Task<AuthoritativeClientMessage> WaitForMessageAsync(
        AuthoritativeClient client,
        Func<AuthoritativeClientMessage, bool> predicate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        for (var attempt = 0; attempt < 200; attempt++) {
            foreach (var message in client.DrainMessages()) {
                if (predicate(message))
                    return message;
            }
            await client.PollAsync(cancellationToken);
            await Task.Delay(25, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for a live authoritative client message.");
    }

    private static async Task<CommandResult> WaitForAcknowledgementAsync(AuthoritativeClient client, ulong sequence)
    {
        var message = await WaitForMessageAsync(client, message =>
            message.Acknowledgement?.Results.Any(result => result.ClientSequence == sequence) == true);
        return message.Acknowledgement!.Results.Single(result => result.ClientSequence == sequence);
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

    private sealed class StartedServer : IDisposable
    {
        private readonly Process process;
        private readonly string saveRoot;
        private readonly bool deleteSaveRootOnDispose;

        private StartedServer(
            Process process,
            string saveRoot,
            bool deleteSaveRootOnDispose,
            int port,
            string contentHash,
            string rulesetHash)
        {
            this.process = process;
            this.saveRoot = saveRoot;
            this.deleteSaveRootOnDispose = deleteSaveRootOnDispose;
            Port = port;
            ContentHash = contentHash;
            RulesetHash = rulesetHash;
        }

        public int Port { get; }

        public string ContentHash { get; }

        public string RulesetHash { get; }

        public static async Task<StartedServer> StartAsync(
            CancellationToken cancellationToken,
            string? contentOverlayRoot = null,
            uint? startingGoldOverride = null,
            int? startingLifeOverride = null,
            int? startingManaOverride = null,
            uint? randomSeed = null,
            string? gameMode = null,
            string? saveRootOverride = null,
            bool deleteSaveRootOnDispose = true)
        {
            var repositoryRoot = LiveAuthoritativeServerTests.FindRepositoryRoot();
            var ownsSaveRoot = string.IsNullOrWhiteSpace(saveRootOverride);
            var saveRoot = saveRootOverride ?? Path.Combine(
                Path.GetTempPath(),
                "Godevilution-Godot-Test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveRoot);
            var startInfo = new ProcessStartInfo {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "server/src/Devilution.Server/Devilution.Server.csproj"));
            startInfo.ArgumentList.Add("--no-build");
            var artifactsPath = Environment.GetEnvironmentVariable("GODEVILUTION_ARTIFACTS_PATH");
            if (!string.IsNullOrWhiteSpace(artifactsPath)) {
                startInfo.ArgumentList.Add("--artifacts-path");
                startInfo.ArgumentList.Add(Path.GetFullPath(artifactsPath));
            }
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("--protocol-version");
            startInfo.ArgumentList.Add(ProtocolSchemaVersions.IndependentContentAndRulesetIdentity);
            startInfo.ArgumentList.Add("--save-root");
            startInfo.ArgumentList.Add(saveRoot);
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
            if (randomSeed.HasValue) {
                startInfo.ArgumentList.Add("--random-seed");
                startInfo.ArgumentList.Add(randomSeed.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(gameMode)) {
                startInfo.ArgumentList.Add("--game-mode");
                startInfo.ArgumentList.Add(gameMode);
            }
            if (!string.IsNullOrWhiteSpace(contentOverlayRoot)) {
                startInfo.ArgumentList.Add("--content-overlay");
                startInfo.ArgumentList.Add(Path.GetFullPath(contentOverlayRoot));
            }
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the authoritative server.");
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            int? port = null;
            string? contentHash = null;
            string? rulesetHash = null;
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line) {
                if (line.Contains("listening on", StringComparison.Ordinal)
                    && int.TryParse(line[(line.LastIndexOf(':') + 1)..], out var parsedPort))
                    port = parsedPort;
                if (line.StartsWith("Content manifest: ", StringComparison.Ordinal))
                    contentHash = line["Content manifest: ".Length..];
                if (line.StartsWith("Ruleset identity: ", StringComparison.Ordinal))
                    rulesetHash = line["Ruleset identity: ".Length..];
                if (port.HasValue && contentHash is not null && rulesetHash is not null)
                    return new StartedServer(
                        process,
                        saveRoot,
                        deleteSaveRootOnDispose,
                        port.Value,
                        contentHash,
                        rulesetHash);
            }
            var error = await standardError;
            await process.WaitForExitAsync(cancellationToken);
            var exitCode = process.ExitCode;
            process.Dispose();
            if (ownsSaveRoot && Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, true);
            var detail = string.IsNullOrWhiteSpace(error) ? string.Empty : Environment.NewLine + error.Trim();
            throw new InvalidOperationException(
                $"The authoritative server exited with code {exitCode} before publishing its identity.{detail}");
        }

        public void Dispose()
        {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            process.Dispose();
            if (deleteSaveRootOnDispose && Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, true);
        }
    }
}
