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
}
