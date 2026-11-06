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
}
