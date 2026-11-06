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
}
