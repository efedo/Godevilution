using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeClassAbilityTests : LiveAuthoritativeTestBase
{

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
            randomSeed: 0x00000001, passiveMonsters: true);
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

        using var observation = server.HoldClockForObservation(client.SuggestedCommandTick(model.CurrentTick));
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
        }, server.CurrentTick);
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Hellfire Firebolt was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; staff item was {staff.State.ItemIndex} with {staff.State.Charges} charges.");

        var consumed = await WaitForGameConditionAsync(server,
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
        _ = await WaitForGameConditionAsync(server, client, message =>
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
    [Trait("Category", "Slow")]
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
    [Trait("Category", "Slow")]
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

        using var observation = server.HoldClockForObservation(client.SuggestedCommandTick(model.CurrentTick));
        var sequence = client.Queue(new Command {
            CastRequested = new CastRequested {
                SpellId = 22,
                Source = CastSourceKind.Ability,
            },
        }, server.CurrentTick);
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.True(
            acknowledgement.Status == CommandStatus.Accepted,
            $"Barbarian Rage was {acknowledgement.Status}/{acknowledgement.RejectReason} at tick {acknowledgement.AppliedTick}; class was {player.CharacterClassId}, ability was {player.AbilitySpellId}.");

        var active = await WaitForGameConditionAsync(server,
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
}
