using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Snapshots;
using Devilution.Server.Stores;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class StoreSimulationExecutorTests
{
    [Fact]
    public void AllocateAttributeSpendsServerOwnedPointsAndProjectsResources()
    {
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(10, 10),
            new PlayerAttributeState(10, 10),
            new PlayerAttributeState(10, 10),
            new PlayerAttributeState(10, 10));
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingAttributes: attributes,
            startingStatPoints: 3);
        var server = new AuthoritativeCommandServer(executor);

        var command = new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AllocateAttributeRequested = new AllocateAttributeRequested {
                Attribute = PlayerAttribute.Vitality,
                Amount = 2,
            },
        };
        var result = server.Process("player-a", command, currentTick: 1);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(1U, state.StatPoints);
        Assert.Equal(12, state.Attributes.Vitality.Base);
        Assert.Equal(12, state.Attributes.Vitality.Current);
    }

    [Fact]
    public void AllocateAttributeRejectsOverspendingWithoutMutation()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0, startingStatPoints: 1);
        var server = new AuthoritativeCommandServer(executor);

        var command = new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AllocateAttributeRequested = new AllocateAttributeRequested {
                Attribute = PlayerAttribute.Strength,
                Amount = 2,
            },
        };
        var result = server.Process("player-a", command, currentTick: 1);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.InsufficientResources, result.RejectReason);
        Assert.Equal(1U, state.StatPoints);
    }

    [Fact]
    public void PurchaseDeductsGoldAndConsumesStockOnceAcrossRetries()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);

        var open = server.Process("player-a", OpenStore(1), currentTick: 10);
        var purchase = server.Process("player-a", Purchase(2, 0), currentTick: 12);
        var duplicate = server.Process("player-a", Purchase(2, 0), currentTick: 13);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Accepted, open.Status);
        Assert.Equal(CommandStatus.Accepted, purchase.Status);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(25U, state.Gold);
        var purchasedItem = Assert.Single(state.Inventory);
        Assert.Equal(42U, purchasedItem.ItemSeed);
        Assert.False(catalog.TryGetItem(1, 0, out _));
    }

    [Fact]
    public void PurchaseFillsWalletOnlyCompatibilityInventoryGrid()
    {
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
        };
        var catalog = CreateCatalog(new StoreItem(0, 42, 10, itemState));
        var executor = new StoreSimulationExecutor(
            catalog,
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), currentTick: 10);

        var result = server.Process("player-a", Purchase(2, 0), currentTick: 12);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(0, state.InventoryGrid[30]);
        var snapshot = executor.CreateSnapshot("player-a", entityId: 7, tick: 12);
        Assert.Equal([0], snapshot.Players[0].InventoryGrid.Skip(30).Take(1));
    }

    [Fact]
    public void CloseStoreClearsAuthoritativeStoreSnapshot()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 10).Status);
        Assert.Equal(1U, executor.GetPlayerState("player-a").ActiveStoreId);

        var close = server.Process("player-a", CloseStore(2, 12), 12);
        var repeated = server.Process("player-a", CloseStore(3, 13), 13);

        Assert.Equal(CommandStatus.Accepted, close.Status);
        Assert.Equal(CommandStatus.Accepted, repeated.Status);
        Assert.Null(executor.GetPlayerState("player-a").ActiveStoreId);
    }

    [Fact]
    public void InsufficientGoldLeavesWalletAndStockUnchanged()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 50);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), currentTick: 10);

        var result = server.Process("player-a", Purchase(2, 0), currentTick: 12);
        var duplicate = server.Process("player-a", Purchase(2, 0), currentTick: 13);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.InsufficientResources, result.RejectReason);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(50U, state.Gold);
        Assert.Empty(state.Inventory);
        Assert.True(catalog.TryGetItem(1, 0, out _));
    }

    [Fact]
    public void PurchaseRequiresTheMatchingOpenStore()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", Purchase(1, 0), currentTick: 10);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, result.RejectReason);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
    }

    [Fact]
    public void StockIsSharedAcrossSessions()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), currentTick: 10);
        server.Process("player-b", OpenStore(3), currentTick: 10);
        var firstPurchase = server.Process("player-a", Purchase(2, 0), currentTick: 12);
        var secondPurchase = server.Process("player-b", Purchase(4, 0), currentTick: 12);

        Assert.Equal(CommandStatus.Accepted, firstPurchase.Status);
        Assert.Equal(CommandStatus.Rejected, secondPurchase.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, secondPurchase.RejectReason);
        Assert.Single(executor.GetPlayerState("player-a").Inventory);
        Assert.Empty(executor.GetPlayerState("player-b").Inventory);
    }

    [Fact]
    public void UnknownStoreAndUnknownSlotAreRejected()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);

        var unknownStore = server.Process("player-a", OpenStore(1, 999), currentTick: 10);
        server.Process("player-a", OpenStore(2, 1), currentTick: 11);
        var unknownSlot = server.Process("player-a", Purchase(3, 99), currentTick: 12);

        Assert.Equal(CommandRejectReason.InvalidTarget, unknownStore.RejectReason);
        Assert.Equal(CommandRejectReason.InvalidTarget, unknownSlot.RejectReason);
    }

    [Fact]
    public void SnapshotContainsAuthoritativeWalletStoreAndInventory()
    {
        var itemState = new AuthoritativeItemState(
            123,
            1,
            4,
            -2,
            false,
            true,
            1,
            2,
            3,
            100,
            80,
            1,
            3,
            4,
            7,
            5,
            6,
            9,
            2,
            4,
            10,
            20);
        var catalog = CreateCatalog(new StoreItem(0, 42, 75, itemState));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), currentTick: 10);
        server.Process("player-a", Purchase(2, 0), currentTick: 12);

        var snapshot = executor.CreateSnapshot("player-a", entityId: 7, tick: 12);
        var player = Assert.Single(snapshot.Players);
        var item = Assert.Single(player.Inventory);

        Assert.Equal(12UL, snapshot.Tick);
        Assert.Equal(7U, player.EntityId);
        Assert.Equal(25U, player.Gold);
        Assert.Equal(1U, player.ActiveStoreId);
        Assert.Equal("f44870bf6d2003b08092b991adc473c757b0548b6744fb48831cd70027baf877", snapshot.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(snapshot), snapshot.StateSha256);
        Assert.Equal(1U, item.StoreId);
        Assert.Equal(0U, item.StoreSlot);
        Assert.Equal(42U, item.ItemSeed);
        Assert.Equal(75U, item.Price);
        Assert.Equal(12UL, item.PurchasedAtTick);
        Assert.Equal(123U, item.State.CreateInfo);
        Assert.Equal(1, item.State.ItemType);
        Assert.Equal(4, item.State.PositionX);
        Assert.Equal(-2, item.State.PositionY);
        Assert.True(item.State.Identified);
        Assert.Equal(100, item.State.Value);
        Assert.Equal(3, item.State.MaxDamage);
        Assert.Equal(7U, item.State.Flags);
        Assert.Equal(20, item.State.MaxDurability);
    }

    [Fact]
    public void MultiplayerSnapshotsPutTheReceiverFirstAndHashEveryParticipantCanonically()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingLife: 640,
            startingLifeMaximum: 800);
        executor.CreateSnapshot("player-b", entityId: 8, tick: 0);
        executor.CreateSnapshot("player-a", entityId: 7, tick: 0);

        var playerA = executor.CreateSnapshot("player-a", entityId: 7, tick: 0);
        var playerB = executor.CreateSnapshot("player-b", entityId: 8, tick: 0);

        Assert.Equal([7U, 8U], playerA.Players.Select(player => player.EntityId));
        Assert.Equal([8U, 7U], playerB.Players.Select(player => player.EntityId));
        Assert.Equal(playerA.StateSha256, playerB.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(playerA), playerA.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(playerB), playerB.StateSha256);
    }

    [Fact]
    public void SnapshotIncludesConfiguredBaselinePlayerResources()
    {
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(10, 12),
            new PlayerAttributeState(8, 9),
            new PlayerAttributeState(15, 16),
            new PlayerAttributeState(20, 21));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 100,
            startingExperience: 200,
            startingLife: 640,
            startingMana: 32,
            startingAttributes: attributes,
            startingEquipment: [new EquippedStoreItem(0, 77)],
            startingInventory: [
                new OwnedStoreItem(0, 0, 71, 0, 0),
                new OwnedStoreItem(0, 1, 72, 0, 0),
                new OwnedStoreItem(0, 2, 73, 0, 0),
            ],
            startingInventoryGrid: [0, 1, 2, ..Enumerable.Repeat(-1, 37)]);

        var state = executor.GetPlayerState("player-a");
        var snapshot = executor.CreateSnapshot("player-a", entityId: 7, tick: 0);
        var player = Assert.Single(snapshot.Players);

        Assert.Equal(200U, state.Experience);
        Assert.Equal(640, state.Life);
        Assert.Equal(32, state.Mana);
        Assert.Equal(200U, player.Experience);
        Assert.Equal(640, player.Life);
        Assert.Equal(32, player.Mana);
        Assert.Equal(12, player.Attributes.Strength.Current);
        Assert.Equal(9, player.Attributes.Magic.Current);
        Assert.Equal(16, player.Attributes.Dexterity.Current);
        Assert.Equal(21, player.Attributes.Vitality.Current);
        Assert.Equal(77U, Assert.Single(player.Equipment).ItemSeed);
        Assert.Equal([0, 1, 2], player.InventoryGrid.Take(3));

        var changed = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = player.EntityId,
                    Experience = player.Experience + 1,
                    Life = player.Life,
                    Mana = player.Mana,
                    Gold = player.Gold,
                },
            },
        };
        Assert.NotEqual(snapshot.StateSha256, SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void SnapshotProjectsNativeEquipmentLightRadius()
    {
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 0,
            startingEquipment: [new EquippedStoreItem(
                4,
                77,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusLight = 4,
                })]);

        var player = Assert.Single(executor.CreateSnapshot("player-a", 7, 0).Players);

        Assert.Equal(14, player.LightRadius);
    }

    [Fact]
    public void ModuleOwnedStoreTransactionsSupportIdentificationRepairRechargeMoveAndSale()
    {
        var itemState = new AuthoritativeItemState(
            1,
            10,
            0,
            0,
            false,
            false,
            1,
            0,
            1,
            100,
            100,
            1,
            2,
            5,
            0,
            0,
            0,
            1,
            1,
            3,
            10,
            20);
        var catalog = CreateCatalog(new StoreItem(0, 42, 75, itemState));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 1000, startingInventoryGrid: [-1, -1]);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 0).Status);
        var purchase = Purchase(2, 0);
        purchase.RequestedTick = 1;
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", purchase, 1).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 2,
            IdentifyItemRequested = new IdentifyItemRequested { InventoryIndex = 0 },
        }, 2).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 3,
            RepairItemRequested = new RepairItemRequested { InventoryIndex = 0 },
        }, 3).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 5,
            RequestedTick = 4,
            RechargeItemRequested = new RechargeItemRequested { InventoryIndex = 0 },
        }, 4).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 6,
            RequestedTick = 5,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 1 },
        }, 5).Status);

        var state = executor.GetPlayerState("player-a");
        var item = Assert.Single(state.Inventory);
        Assert.Equal(0, state.InventoryGrid[1]);
        Assert.True(item.State.Identified);
        Assert.Equal(item.State.MaxDurability, item.State.Durability);
        Assert.Equal(item.State.MaxCharges, item.State.Charges);

        var sale = server.Process("player-a", new Command {
            ClientSequence = 7,
            RequestedTick = 6,
            SellItemRequested = new SellItemRequested { InventoryIndex = 0 },
        }, 6);

        Assert.Equal(CommandStatus.Accepted, sale.Status);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
        Assert.Equal(810U, executor.GetPlayerState("player-a").Gold);
    }

    [Fact]
    public void ProtocolIdentifyAbilityUsesAuthoritativeClassSkillWithoutVendorState()
    {
        const uint identifySpellId = 5;
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                Magical = 1,
                Value = 100,
                IdentifiedValue = 250,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventory: [item],
            startingAbilitySpellId: identifySpellId,
            identifyAbilitySpellId: identifySpellId);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            IdentifyItemRequested = new IdentifyItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        Assert.True(Assert.Single(player.Inventory).State.Identified);
        Assert.Equal(0U, player.Gold);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void ProtocolIdentifyScrollCarriesAndConsumesTheAuthoritativeSourceReference()
    {
        var target = new OwnedStoreItem(0, 0, 42, 100, 0,
            AuthoritativeItemState.Empty with { Magical = 1 });
        var scroll = new OwnedStoreItem(0, 0, 43, 100, 0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.Identify,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventory: [target, scroll]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            IdentifyItemRequested = new IdentifyItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
                Source = ItemServiceSourceKind.ConsumableItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 1,
                },
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        var remaining = Assert.Single(player.Inventory);
        Assert.True(remaining.State.Identified);
        Assert.Equal(42U, remaining.ItemSeed);
        Assert.Equal(0U, player.Gold);
    }

    [Fact]
    public void ProtocolReadBookUsesAuthoritativeSpellLearningAndItemConsumption()
    {
        var book = new OwnedStoreItem(0, 0, 42, 100, 0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ReadBook,
                MiscId = 25,
                SpellId = 1,
                MinimumMagic = 15,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingMana: 100,
            startingManaMaximum: 640,
            startingAttributes: new PlayerAttributesState(
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 20),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 0)),
            startingInventory: [book],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
                    ManaCostScale = 64,
                },
            ]));
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            ReadBookRequested = new ReadBookRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        Assert.Empty(player.Inventory);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(player.MemorizedSpells));
        Assert.Equal(484, player.Mana);
    }

    [Fact]
    public void ProtocolUseConsumableUsesAuthoritativeResourceEffectAndConsumesItem()
    {
        var potion = new OwnedStoreItem(0, 0, 42, 100, 0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
                StatFlag = true,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 64,
            startingLifeMaximum: 640,
            startingMana: 64,
            startingManaMaximum: 640,
            startingInventory: [potion]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            UseConsumableRequested = new UseConsumableRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        Assert.Empty(player.Inventory);
        Assert.Equal(640, player.Life);
        Assert.Equal(640, player.Mana);
    }

    [Fact]
    public void ProtocolUseConsumableAppliesAuthoritativeElixirAttributeEffect()
    {
        var elixir = new OwnedStoreItem(0, 0, 43, 100, 0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.IncreaseDexterity,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingAttributes: new PlayerAttributesState(
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(0, 0)),
            startingInventory: [elixir]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            UseConsumableRequested = new UseConsumableRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        Assert.Empty(player.Inventory);
        Assert.Equal(21, player.Attributes.Dexterity.Base);
        Assert.Equal(21, player.Attributes.Dexterity.Current);
    }

    [Fact]
    public void ProtocolUseConsumableAppliesSpectralElixirToAllAttributesAndResources()
    {
        var elixir = new OwnedStoreItem(0, 0, 47, 100, 0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.IncreaseAllAttributes,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 100,
            startingLifeMaximum: 640,
            startingMana: 100,
            startingManaMaximum: 640,
            startingAttributes: new PlayerAttributesState(
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(10, 10),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(25, 25)),
            startingPlayerClass: new AuthoritativePlayerClassDefinition(
                1,
                "spectral-test",
                "Spectral Test",
                DiabloPlayerClassAvailability.Hellfire,
                true,
                DiabloPlayerClassFlags.None,
                30,
                10,
                20,
                25,
                750,
                750,
                750,
                750,
                0,
                50,
                50,
                50,
                0,
                0,
                0,
                0,
                64,
                64,
                0,
                0),
            startingInventory: [elixir]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            UseConsumableRequested = new UseConsumableRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        Assert.Empty(player.Inventory);
        Assert.Equal(33, player.Attributes.Strength.Base);
        Assert.Equal(13, player.Attributes.Magic.Base);
        Assert.Equal(23, player.Attributes.Dexterity.Base);
        Assert.Equal(28, player.Attributes.Vitality.Base);
        Assert.True(player.LifeMaximum > 640);
        Assert.True(player.ManaMaximum > 640);
        Assert.True(player.Life > 100);
        Assert.True(player.Mana > 100);
    }

    [Fact]
    public void ProtocolUseConsumableAppliesOilToTheReferencedTarget()
    {
        var oil = new OwnedStoreItem(0, 0, 44, 100, 0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ApplySharpnessOil,
                ItemClass = 3,
                StableItemId = 44,
            });
        var weapon = new OwnedStoreItem(0, 0, 45, 100, 0,
            AuthoritativeItemState.Empty with {
                ItemClass = 1,
                MinDamage = 5,
                MaxDamage = 10,
                StableItemId = 45,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventory: [oil, weapon]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            UseConsumableRequested = new UseConsumableRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = 44,
                },
                TargetItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 1,
                    StableItemId = 45,
                },
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        var applied = Assert.Single(player.Inventory);
        Assert.Equal(45U, applied.ItemSeed);
        Assert.Equal(11, applied.State.MaxDamage);
    }

    [Fact]
    public void ProtocolRepairAbilityUsesAuthoritativeClassSkillAndNativeRandomRule()
    {
        const uint repairSpellId = 26;
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                Durability = 1,
                MaxDurability = 20,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingCharacterLevel: 10,
            startingInventory: [item],
            startingAbilitySpellId: repairSpellId,
            repairAbilitySpellId: repairSpellId);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RepairItemRequested = new RepairItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        var repaired = Assert.Single(player.Inventory);
        Assert.Equal(18, repaired.State.Durability);
        Assert.Equal(18, repaired.State.MaxDurability);
        Assert.Equal(0U, player.Gold);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void ProtocolRechargeAbilityUsesAuthoritativeClassSkillAndNativeRandomRule()
    {
        const uint rechargeSpellId = 27;
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 10,
                SpellId = 30,
                Charges = 1,
                MaxCharges = 10,
            });
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
        ]);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingCharacterLevel: 10,
            startingInventory: [item],
            startingAbilitySpellId: rechargeSpellId,
            startingSpells: spells,
            rechargeAbilitySpellId: rechargeSpellId);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RechargeItemRequested = new RechargeItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
                Source = ItemServiceSourceKind.ClassAbility,
            },
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        var recharged = Assert.Single(player.Inventory);
        Assert.Equal(5, recharged.State.Charges);
        Assert.Equal(5, recharged.State.MaxCharges);
        Assert.Equal(0U, player.Gold);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void ProtocolRepairRejectsUnknownItemServiceSource()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RepairItemRequested = new RepairItemRequested {
                Source = (ItemServiceSourceKind)99,
            },
        }, 0);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.Malformed, result.RejectReason);
    }

    [Fact]
    public void ProtocolRechargeRejectsUnknownItemServiceSource()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RechargeItemRequested = new RechargeItemRequested {
                Source = (ItemServiceSourceKind)99,
            },
        }, 0);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.Malformed, result.RejectReason);
    }

    [Fact]
    public void ProtocolIdentifyRejectsUnknownItemServiceSource()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            IdentifyItemRequested = new IdentifyItemRequested {
                Source = (ItemServiceSourceKind)99,
            },
        }, 0);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.Malformed, result.RejectReason);
    }

    [Fact]
    public void ProtocolItemReferencesRejectNativeSlotOverflow()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        var server = new AuthoritativeCommandServer(executor);

        var inventory = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 40,
                },
            },
        }, 0);
        var belt = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Belt,
                    Slot = 8,
                },
            },
        }, 1);
        var equipment = server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 3,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 7,
                },
            },
        }, 2);
        var stash = server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 4,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Slot = 0,
                    Page = 100,
                },
            },
        }, 3);

        Assert.All(
            new[] { inventory, belt, equipment, stash },
            result => Assert.Equal(CommandRejectReason.Malformed, result.RejectReason));
    }

    [Fact]
    public void ServiceCommandsCanTargetBeltAndEquipmentSlots()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingBelt: [new BeltStoreItem(
                2,
                101,
                AuthoritativeItemState.Empty with { ItemType = 1, ItemIndex = 1 })],
            startingEquipment: [new EquippedStoreItem(
                3,
                102,
                AuthoritativeItemState.Empty with { ItemType = 1, ItemIndex = 2 })]);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 0).Status);

        var beltSale = new Command {
            ClientSequence = 2,
            RequestedTick = 1,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Belt, Slot = 2 },
            },
        };
        var equipmentSale = new Command {
            ClientSequence = 3,
            RequestedTick = 2,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Equipment, Slot = 3 },
            },
        };

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", beltSale, 1).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", equipmentSale, 2).Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Empty(state.Belt);
        Assert.Empty(state.Equipment);
        Assert.Equal(102U, state.Gold);
    }

    [Fact]
    public void ManaRefillUsesConfiguredMaximumAndDeductsDeterministicPrice()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 100, startingMana: 32, startingManaMaximum: 640);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1, 10), 0).Status);

        var refill = new Command {
            ClientSequence = 2,
            RequestedTick = 10,
            RefillManaRequested = new RefillManaRequested(),
        };
        var result = server.Process("player-a", refill, 10);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(640, state.Mana);
        Assert.Equal(90U, state.Gold);
        Assert.Equal(640, executor.CreateSnapshot("player-a", 1, 10).Players[0].ManaMaximum);

        var alreadyFull = server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 11,
            RefillManaRequested = new RefillManaRequested(),
        }, 11);
        Assert.Equal(CommandStatus.Rejected, alreadyFull.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, alreadyFull.RejectReason);
    }

    [Fact]
    public void RemovingInventoryItemCompactsGridReferences()
    {
        var catalog = CreateCatalog(
            new StoreItem(
                0,
                42,
                10,
                AuthoritativeItemState.Empty with { ItemType = 1 }),
            new StoreItem(
                1,
                43,
                10,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    InventoryWidth = 2,
                    InventoryHeight = 1,
                }));
        var executor = new StoreSimulationExecutor(
            catalog,
            startingGold: 100,
            startingInventory: [
                new OwnedStoreItem(
                    0,
                    0,
                    42,
                    10,
                    0,
                    AuthoritativeItemState.Empty with { ItemType = 1 }),
                new OwnedStoreItem(
                    0,
                    1,
                    43,
                    10,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = 1,
                        InventoryWidth = 2,
                        InventoryHeight = 1,
                    }),
            ],
            startingInventoryGrid: [0, 1, 1, -1, ..Enumerable.Repeat(-1, 36)]);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 0);

        var sale = new Command {
            ClientSequence = 4,
            RequestedTick = 3,
            SellItemRequested = new SellItemRequested { InventoryIndex = 0 },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", sale, 3).Status);

        Assert.Equal(
            [-1, 0, 0, -1],
            executor.GetPlayerState("player-a").InventoryGrid.Take(4));
    }

    [Fact]
    public void ExplicitMoveTransfersInventoryAndBeltItemsAuthoritatively()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(new StoreItem(0, 42, 10)), startingGold: 100, startingInventoryGrid: [-1]);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 10).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 12).Status);

        var toBelt = new Command {
            ClientSequence = 3,
            RequestedTick = 13,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 0 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Belt, Slot = 0 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", toBelt, 13).Status);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
        Assert.Equal(42U, Assert.Single(executor.GetPlayerState("player-a").Belt).ItemSeed);

        var toInventory = new Command {
            ClientSequence = 4,
            RequestedTick = 14,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Belt, Slot = 0 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 0 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", toInventory, 14).Status);
        Assert.Equal(42U, Assert.Single(executor.GetPlayerState("player-a").Inventory).ItemSeed);
        Assert.Equal(new[] { 0 }, executor.GetPlayerState("player-a").InventoryGrid);
        Assert.Empty(executor.GetPlayerState("player-a").Belt);
    }

    [Fact]
    public void ExplicitMoveTransfersInventoryAndStashItemsThroughProtocol()
    {
        var inventoryState = AuthoritativeItemState.Empty with {
            StableItemId = 910,
            ItemType = 1,
            ItemIndex = 10,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var stashState = AuthoritativeItemState.Empty with {
            StableItemId = 911,
            ItemType = 1,
            ItemIndex = 11,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingInventoryGrid: [0],
            startingInventory: [new OwnedStoreItem(0, 0, 42, 10, 0, inventoryState)],
            startingStash: [new StashStoreItem(6, 12, 3, 4, 99, 11, 7, stashState)],
            startingStashGold: 250);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            OpenStashRequested = new OpenStashRequested(),
        }, 0).Status);

        var toStash = new Command {
            ClientSequence = 2,
            RequestedTick = 1,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = 910,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 6,
                    Slot = 20,
                    StableItemId = 910,
                },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", toStash, 1).Status);
        var afterStash = executor.GetPlayerState("player-a");
        Assert.Empty(afterStash.Inventory);
        Assert.Equal(250U, afterStash.StashGold);
        Assert.Contains(afterStash.Stash, item =>
            item.Page == 6 && item.Cell == 20 && item.ItemSeed == 42);

        var toInventory = new Command {
            ClientSequence = 3,
            RequestedTick = 2,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 6,
                    Slot = 20,
                    StableItemId = 910,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = 910,
                },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", toInventory, 2).Status);
        var restored = executor.GetPlayerState("player-a");
        Assert.Equal(42U, Assert.Single(restored.Inventory).ItemSeed);
        Assert.Equal(7UL, Assert.Single(restored.Stash).PurchasedAtTick);
        Assert.Contains(restored.Stash, item => item.ItemSeed == 99);
    }

    [Fact]
    public void ExplicitMoveSwapsInventoryAndEquipmentItemsAtomically()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10)),
            startingGold: 100,
            startingInventoryGrid: [-1],
            startingEquipment: [new EquippedStoreItem(3, 99)]);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        server.Process("player-a", Purchase(2, 0), 12);

        var swap = new Command {
            ClientSequence = 3,
            RequestedTick = 13,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 0 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Equipment, Slot = 3 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", swap, 13).Status);
        Assert.Equal(99U, Assert.Single(executor.GetPlayerState("player-a").Inventory).ItemSeed);
        Assert.Equal(42U, Assert.Single(executor.GetPlayerState("player-a").Equipment).ItemSeed);
        Assert.Equal(new[] { 0 }, executor.GetPlayerState("player-a").InventoryGrid);
    }

    [Fact]
    public void SortInventoryUsesNativeSizeOrderAndPlacementOrder()
    {
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[30] = 0;
        grid[0] = 1;
        grid[10] = 1;
        grid[2] = 2;
        grid[3] = 2;
        var inventory = new[] {
            new OwnedStoreItem(
                0,
                0,
                11,
                0,
                0,
                AuthoritativeItemState.Empty with {
                    StableItemId = 801,
                    ItemType = 1,
                    ItemIndex = 1,
                    InventoryWidth = 1,
                    InventoryHeight = 1,
                }),
            new OwnedStoreItem(
                0,
                1,
                22,
                0,
                0,
                AuthoritativeItemState.Empty with {
                    StableItemId = 802,
                    ItemType = 1,
                    ItemIndex = 2,
                    InventoryWidth = 1,
                    InventoryHeight = 2,
                }),
            new OwnedStoreItem(
                0,
                2,
                33,
                0,
                0,
                AuthoritativeItemState.Empty with {
                    StableItemId = 803,
                    ItemType = 1,
                    ItemIndex = 3,
                    InventoryWidth = 2,
                    InventoryHeight = 1,
                }),
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: grid,
            startingInventory: inventory);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            SortInventoryRequested = new SortInventoryRequested(),
        }, 0);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(new uint[] { 22, 33, 11 }, state.Inventory.Select(item => item.ItemSeed));
        Assert.Equal(0, state.InventoryGrid[9]);
        Assert.Equal(0, state.InventoryGrid[19]);
        Assert.Equal(1, state.InventoryGrid[30]);
        Assert.Equal(1, state.InventoryGrid[31]);
        Assert.Equal(2, state.InventoryGrid[32]);
    }

    [Fact]
    public void SortInventoryRejectsAHeldCursorWithoutMutatingInventory()
    {
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var item = new OwnedStoreItem(
            0,
            0,
            11,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 804,
                ItemType = 1,
                ItemIndex = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: grid,
            startingInventory: [item],
            startingHeldItem: item with { ItemSeed = 99 });
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            SortInventoryRequested = new SortInventoryRequested(),
        }, 0);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, result.RejectReason);
        var state = executor.GetPlayerState("player-a");
        Assert.Same(item, Assert.Single(state.Inventory));
        Assert.Equal(0, state.InventoryGrid[0]);
        Assert.Equal(99U, state.HeldItem!.ItemSeed);
    }

    [Fact]
    public void StashGoldCommandsUseTheAuthoritativeSessionBoundary()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingStashGold: 25);
        var server = new AuthoritativeCommandServer(executor);

        var closedDeposit = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            DepositStashGoldRequested = new DepositStashGoldRequested { Amount = 10 },
        }, 1);
        Assert.Equal(CommandStatus.Rejected, closedDeposit.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, closedDeposit.RejectReason);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            OpenStashRequested = new OpenStashRequested(),
        }, 2).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 3,
            DepositStashGoldRequested = new DepositStashGoldRequested { Amount = 40 },
        }, 3).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 4,
            WithdrawStashGoldRequested = new WithdrawStashGoldRequested { Amount = 15 },
        }, 4).Status);

        var state = executor.GetPlayerState("player-a");
        Assert.Equal(75U, state.Gold);
        Assert.Equal(50U, state.StashGold);
        Assert.True(state.StashOpen);
    }

    [Fact]
    public void SplitGoldProjectsAServerOwnedHeldStackAndPreservesInventoryTotal()
    {
        var goldState = AuthoritativeItemState.Empty with {
            ItemType = 11,
            ItemIndex = 0,
            Value = 100,
            IdentifiedValue = 100,
            StatFlag = true,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingInventoryGrid: inventoryGrid,
            startingInventory: [new OwnedStoreItem(0, 0, 42, 100, 0, goldState)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            SplitGoldRequested = new SplitGoldRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
                Amount = 40,
            },
        }, 0);

        var player = executor.GetPlayerState("player-a");
        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(60U, player.Gold);
        Assert.Equal(60U, Assert.Single(player.Inventory).Price);
        Assert.NotNull(player.HeldItem);
        Assert.Equal(40U, player.HeldItem!.Price);
        Assert.Equal(100U, player.Gold + player.HeldItem!.Price);
        var snapshot = executor.CreateSnapshot("player-a", 1, 1);
        Assert.Equal(60U, Assert.Single(snapshot.Players).Gold);
        Assert.Equal(40U, Assert.Single(snapshot.Players).HeldItem.Price);
    }

    [Fact]
    public void SplitGoldRejectsASecondCursorStackWithoutMutatingTheSource()
    {
        var goldState = AuthoritativeItemState.Empty with {
            ItemType = 11,
            ItemIndex = 0,
            Value = 100,
            IdentifiedValue = 100,
            StatFlag = true,
        };
        var held = new OwnedStoreItem(0, 0, 43, 5, 0, goldState with { Value = 5, IdentifiedValue = 5 });
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingInventoryGrid: inventoryGrid,
            startingInventory: [new OwnedStoreItem(0, 0, 42, 100, 0, goldState)],
            startingHeldItem: held);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            SplitGoldRequested = new SplitGoldRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                },
                Amount = 40,
            },
        }, 0);

        var player = executor.GetPlayerState("player-a");
        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, result.RejectReason);
        Assert.Equal(100U, Assert.Single(player.Inventory).Price);
        Assert.NotNull(player.HeldItem);
        Assert.Equal(5U, player.HeldItem!.Price);
    }

    [Fact]
    public void AutoEquipCommandDispatchesAtomicEquipmentSwap()
    {
        var sourceState = AuthoritativeItemState.Empty with {
            StableItemId = 905,
            EquipLocation = 1,
            ItemClass = 1,
            StatFlag = true,
        };
        var displacedState = AuthoritativeItemState.Empty with {
            StableItemId = 906,
            EquipLocation = 1,
            ItemClass = 1,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingInventoryGrid: grid,
            startingInventory: [new OwnedStoreItem(0, 0, 42, 0, 0, sourceState)],
            startingEquipment: [new EquippedStoreItem(4, 43, displacedState)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AutoEquipItemRequested = new AutoEquipItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = 905,
                },
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(42U, Assert.Single(state.Equipment).ItemSeed);
        Assert.Equal(43U, Assert.Single(state.Inventory).ItemSeed);
        Assert.Equal(0, state.InventoryGrid[30]);
    }

    [Fact]
    public void AutoEquipCommandDispatchesAnExplicitEquipmentSlot()
    {
        var sourceState = AuthoritativeItemState.Empty with {
            StableItemId = 913,
            EquipLocation = 5,
            StatFlag = true,
        };
        var displacedState = AuthoritativeItemState.Empty with {
            StableItemId = 914,
            EquipLocation = 5,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingInventoryGrid: grid,
            startingInventory: [new OwnedStoreItem(0, 0, 55, 0, 0, sourceState)],
            startingEquipment: [new EquippedStoreItem(2, 56, displacedState)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AutoEquipItemRequested = new AutoEquipItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = 913,
                },
                EquipmentSlot = 2,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(55U, Assert.Single(state.Equipment, item => item.Slot == 2).ItemSeed);
        Assert.Equal(56U, Assert.Single(state.Inventory).ItemSeed);
        Assert.Equal(0, state.InventoryGrid[30]);
    }

    [Fact]
    public void InventoryPlacementHonorsMultiCellItemFootprints()
    {
        var itemState = AuthoritativeItemState.Empty with {
            StableItemId = 700,
            ItemType = 1,
            InventoryWidth = 2,
            InventoryHeight = 2,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10, itemState)),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        server.Process("player-a", Purchase(2, 0), 12);

        var move = new Command {
            ClientSequence = 3,
            RequestedTick = 13,
            MoveInventoryItemRequested = new MoveInventoryItemRequested {
                InventoryIndex = 0,
                TargetCell = 8,
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = 700,
                },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", move, 13).Status);
        var grid = executor.GetPlayerState("player-a").InventoryGrid;
        Assert.Equal(0, grid[8]);
        Assert.Equal(0, grid[9]);
        Assert.Equal(0, grid[18]);
        Assert.Equal(0, grid[19]);
    }

    [Fact]
    public void InventoryPlacementSupportsValidatedExternalFootprintsBeyondNativeOrdering()
    {
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 1,
            InventoryWidth = 1,
            InventoryHeight = 4,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10, itemState)),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 12).Status);
        var grid = executor.GetPlayerState("player-a").InventoryGrid;
        Assert.Equal(0, grid[0]);
        Assert.Equal(0, grid[10]);
        Assert.Equal(0, grid[20]);
        Assert.Equal(0, grid[30]);
        Assert.Single(executor.CreateSnapshot("player-a", 7, 12).Players[0].Inventory);
    }

    [Fact]
    public void MultiCellPlacementRejectsEdgeAndCollisionTargets()
    {
        var large = AuthoritativeItemState.Empty with { ItemType = 1, InventoryWidth = 2, InventoryHeight = 1 };
        var small = AuthoritativeItemState.Empty with { ItemType = 2 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10, large), new StoreItem(1, 43, 10, small)),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 12).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(3, 1), 12).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 14,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 8 },
        }, 14).Status);

        Assert.Equal(CommandStatus.Rejected, server.Process("player-a", new Command {
            ClientSequence = 5,
            RequestedTick = 15,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 9 },
        }, 15).Status);
        Assert.Equal(CommandStatus.Rejected, server.Process("player-a", new Command {
            ClientSequence = 6,
            RequestedTick = 16,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 1, TargetCell = 9 },
        }, 16).Status);
    }

    [Fact]
    public void MultiCellInventoryItemsSwapOnlyWhenBothFootprintsFit()
    {
        var large = AuthoritativeItemState.Empty with { ItemType = 1, InventoryWidth = 2, InventoryHeight = 1 };
        var small = AuthoritativeItemState.Empty with { ItemType = 2 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10, large), new StoreItem(1, 43, 10, small)),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        server.Process("player-a", Purchase(2, 0), 12);
        server.Process("player-a", Purchase(3, 1), 12);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 14,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 8 },
        }, 14).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 5,
            RequestedTick = 15,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 1, TargetCell = 20 },
        }, 15).Status);

        var swap = new Command {
            ClientSequence = 6,
            RequestedTick = 16,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 1 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 8 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", swap, 16).Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(0, state.InventoryGrid[8]);
        Assert.Equal(-1, state.InventoryGrid[9]);
        Assert.Equal(1, state.InventoryGrid[20]);
        Assert.Equal(1, state.InventoryGrid[21]);
    }

    [Fact]
    public void MovementIsValidatedAndIncludedInTheAuthoritativeSnapshot()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLife: 40,
            startingLifeMaximum: 60,
            startingCharacterLevel: 3);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 5,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = -1 },
        }, 5).Status);
        var snapshot = executor.CreateSnapshot("player-a", 7, 5);
        var player = Assert.Single(snapshot.Players);
        Assert.Equal(2, player.PositionX);
        Assert.Equal(0, player.PositionY);
        Assert.Equal(60, player.LifeMaximum);
        Assert.Equal(3U, player.CharacterLevel);

        var invalidDirection = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 6,
            MoveRequested = new MoveRequested(),
        }, 6);
        Assert.Equal(CommandRejectReason.Malformed, invalidDirection.RejectReason);
        var outOfBounds = server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 7,
            MoveRequested = new MoveRequested { DirectionX = -1, DirectionY = -1 },
        }, 7);
        Assert.Equal(CommandRejectReason.InvalidTarget, outOfBounds.RejectReason);
    }

    [Fact]
    public void TargetedMovementUsesTheAuthoritativeDeterministicFirstStep()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLife: 40,
            startingLifeMaximum: 60,
            startingCharacterLevel: 3);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 5,
            MoveRequested = new MoveRequested {
                TargetCell = true,
                TargetX = 4,
                TargetY = 1,
            },
        }, 5);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = Assert.Single(executor.CreateSnapshot("player-a", 7, 5).Players);
        Assert.Equal((2, 1), (player.PositionX, player.PositionY));
    }

    [Fact]
    public void TargetedMovementRejectsAnOutOfBoundsCell()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingPositionX: 1,
            startingPositionY: 1);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 5,
            MoveRequested = new MoveRequested {
                TargetCell = true,
                TargetX = -1,
                TargetY = 1,
            },
        }, 5);

        Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
    }

    [Fact]
    public void HealingCastConsumesManaAndCannotExceedLifeMaximum()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingLife: 10,
            startingLifeMaximum: 25,
            startingMana: 10,
            startingManaMaximum: 10,
            startingMemorizedSpells: [new MemorizedSpellState(1, 1)]);
        var server = new AuthoritativeCommandServer(executor);
        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 1,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(25, state.Life);
        Assert.Equal(5, state.Mana);

        var full = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            CastRequested = new CastRequested {
                SpellId = 1,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 2);
        Assert.Equal(CommandRejectReason.NotAllowed, full.RejectReason);
    }

    [Fact]
    public void ProtocolCastSourcesConsumeTheExactChargedOrConsumableItem()
    {
        var staff = AuthoritativeItemState.Empty with {
            MiscId = 23,
            SpellId = 1,
            Charges = 2,
            MaxCharges = 2,
        };
        var scroll = AuthoritativeItemState.Empty with { MiscId = 21, SpellId = 1 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingLife: 10,
            startingLifeMaximum: 40,
            startingMana: 0,
            startingManaMaximum: 10,
            startingEquipment: [new EquippedStoreItem(4, 71, staff)],
            startingBelt: [new BeltStoreItem(2, 72, scroll)]);
        var server = new AuthoritativeCommandServer(executor);

        var charged = server.Process("charged-player", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 1,
                Source = CastSourceKind.ChargedItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                },
            },
        }, 1);
        var consumable = server.Process("scroll-player", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 1,
                Source = CastSourceKind.ConsumableItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Belt,
                    Slot = 2,
                },
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, charged.Status);
        Assert.Equal(1, Assert.Single(
            executor.GetPlayerState("charged-player").Equipment).State.Charges);
        Assert.Equal(CommandStatus.Accepted, consumable.Status);
        Assert.Empty(executor.GetPlayerState("scroll-player").Belt);
        Assert.Equal(0, executor.GetPlayerState("scroll-player").Mana);
    }

    [Fact]
    public void ProtocolCastSourceRejectsMalformedOrUnownedClaims()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingLife: 10,
            startingLifeMaximum: 40,
            startingMana: 10,
            startingManaMaximum: 10);
        var server = new AuthoritativeCommandServer(executor);

        var itemOnAbility = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 1,
                Source = CastSourceKind.Ability,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = 4,
                },
            },
        }, 1);
        var unknownSource = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            CastRequested = new CastRequested {
                SpellId = 1,
                Source = (CastSourceKind)99,
            },
        }, 2);
        var unowned = server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 3,
            CastRequested = new CastRequested {
                SpellId = 1,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 3);

        Assert.Equal(CommandRejectReason.Malformed, itemOnAbility.RejectReason);
        Assert.Equal(CommandRejectReason.Malformed, unknownSource.RejectReason);
        Assert.Equal(CommandRejectReason.NotAllowed, unowned.RejectReason);
        Assert.Equal(10, executor.GetPlayerState("player-a").Mana);
        Assert.Equal(10, executor.GetPlayerState("player-a").Life);
    }

    [Fact]
    public void DataDrivenDamageSpellUsesTargetRangeArmorAndEvents()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingMana: 10,
            startingManaMaximum: 10,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 1, 0, 20, armorClass: 2)],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) { DamageAmount = 12, Range = 1 },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(7, 1)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 7,
                TargetEntityId = 9,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(10, Assert.Single(executor.DrainEvents("player-a", 7, 1)!.Events).Damage.Amount);
        Assert.Equal(6, executor.GetPlayerState("player-a").Mana);
    }

    [Fact]
    public void ProjectileSpellResolvesOnAnAuthoritativeFutureTick()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingMana: 10,
            startingManaMaximum: 10,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 2, 0, 20)],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) {
                    DamageAmount = 12,
                    Range = 4,
                    ProjectileSpeed = 1,
                    ProjectileLifetime = 8,
                },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(7, 1)]);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 7,
                TargetEntityId = 9,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1).Status);
        Assert.Equal(20, executor.CreateSnapshot("player-a", 1, 1).Monsters.Single().HitPoints);
        Assert.Single(executor.CreateSnapshot("player-a", 1, 1).Projectiles);

        executor.AdvanceTo(3);
        Assert.Equal(8, executor.CreateSnapshot("player-a", 1, 3).Monsters.Single().HitPoints);
        Assert.Empty(executor.CreateSnapshot("player-a", 1, 3).Projectiles);
        Assert.Equal(12, Assert.Single(executor.DrainEvents("player-a", 1, 3)!.Events).Damage.Amount);
    }

    [Fact]
    public void HorkSpawnExpiryPersistsTheMaterializedMonsterAcrossSaveRestore()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 40, 40));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                117,
                117,
                "Hork Spawn",
                0,
                100,
                1,
                1,
                1,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "HorkDemon",
                AggroRange = 6,
            },
        ]);
        var spawnPlanner = new AuthoritativeMonsterSpawnPlanner(
            world,
            monsterTypes,
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(0, 100, 100, 100, 0),
            ]));
        var horkSpawn = new AuthoritativeProjectile(
            0x80000000,
            9,
            1,
            117,
            1,
            1,
            1,
            5,
            1,
            0,
            AuthoritativeDamageType.Physical,
            0,
            1) {
            Behavior = AuthoritativeProjectileBehavior.HorkSpawn,
            Speed = 8,
            ImpactLifetime = 1,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 100,
            startingLifeMaximum: 100,
            startingPositionX: 5,
            startingPositionY: 1,
            startingLevelId: 1,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                9,
                1,
                1,
                120,
                levelId: 1,
                monsterId: 119)],
            startingWorld: world,
            startingProjectiles: [horkSpawn],
            startingMonsterSpawnPlanner: spawnPlanner);

        executor.AdvanceTo(1);
        var spawned = Assert.Single(
            executor.CreateSnapshot("player-a", 1, 1).Monsters,
            monster => monster.EntityId != 9);
        Assert.Equal(117U, spawned.MonsterId);
        Assert.Equal((1, 2), (spawned.PositionX, spawned.PositionY));
        Assert.Empty(executor.CreateSnapshot("player-a", 1, 1).Projectiles);

        var replacement = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        replacement.ImportPlayerSave("player-a", executor.ExportPlayerSave("player-a"));
        var restored = Assert.Single(
            replacement.CreateSnapshot("player-a", 1, 1).Monsters,
            monster => monster.EntityId != 9);
        Assert.Equal(spawned.EntityId, restored.EntityId);
        Assert.Equal(spawned.MonsterId, restored.MonsterId);
        Assert.Equal(spawned.HitPointsQ6, restored.HitPointsQ6);
        Assert.Equal((spawned.PositionX, spawned.PositionY), (restored.PositionX, restored.PositionY));
    }

    [Fact]
    public void ChargedBoltPublishesAndPersistsExactQ6MonsterDamage()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            10,
            lightningResistance: 25,
            monsterLevel: 1);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 40,
            startingMana: 0,
            startingManaMaximum: 0,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            startingCombatTargets: [target],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) {
                    Range = 4,
                    DamageType = AuthoritativeDamageType.Lightning,
                    ProjectileSpeed = 8,
                    ProjectileLifetime = 256,
                    ProjectileCount = 1,
                    ProjectileBehavior = AuthoritativeProjectileBehavior.ChargedBolt,
                    RandomMagicDamageDivisor = 4,
                    ResistantDamageDivisor = 4,
                    ProjectileImpactLifetime = 8,
                },
            ]),
            startingAbilitySpellId: 30,
            startingRandomSeed: 0x12345678);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 30,
                TargetX = 4,
                TargetY = 0,
                Source = CastSourceKind.Ability,
            },
        }, 1).Status);
        executor.AdvanceTo(5);

        var damage = Assert.Single(executor.DrainEvents("player-a", 7, 5)!.Events).Damage;
        Assert.Equal(1, damage.Amount);
        Assert.Equal(112, damage.AmountQ6);
        var damaged = Assert.Single(executor.CreateSnapshot("player-a", 7, 5).Monsters);
        Assert.Equal(9, damaged.HitPoints);
        Assert.Equal(528, damaged.HitPointsQ6);

        var replacement = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 40);
        replacement.ImportPlayerSave("player-a", executor.ExportPlayerSave("player-a"));
        var restored = Assert.Single(replacement.CreateSnapshot("player-a", 7, 5).Monsters);
        Assert.Equal(640, restored.HitPointsQ6);
    }

    [Fact]
    public void FriendlyModeCommandPersistsExplicitHostileStateInSnapshots()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 640,
            startingLifeMaximum: 640);
        var server = new AuthoritativeCommandServer(executor);

        var first = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            SetFriendlyModeRequested = new SetFriendlyModeRequested {
                FriendlyMode = false,
            },
        }, 1);
        var duplicate = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            SetFriendlyModeRequested = new SetFriendlyModeRequested {
                FriendlyMode = false,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, first.Status);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.False(executor.GetPlayerState("player-a").FriendlyMode);
        Assert.True(Assert.Single(
            executor.CreateSnapshot("player-a", 7, 1).Players).HostileMode);
    }

    [Fact]
    public void DataDrivenObjectEffectUpdatesPlayerAndEmitsEvent()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingLife: 10,
            startingLifeMaximum: 30,
            startingObjects: [new AuthoritativeWorldObject(20, 4, 0, 1, 0, effectKind: AuthoritativeObjectEffectKind.Heal, effectAmount: 12)]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(22, executor.GetPlayerState("player-a").Life);
        Assert.True(executor.CreateSnapshot("player-a", 1, 1).Objects.Single().Activated);
        var healing = Assert.Single(executor.DrainEvents("player-a", 1, 1)!.Events).Healing;
        Assert.Equal(12, healing.Amount);
        Assert.Equal(12, healing.AmountQ6);
    }

    [Fact]
    public void CathedralMapObjectInteractionConsumesServerOwnedMapAndCompletesQuest()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                43,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 22,
                Name = "Cathedral Map",
                IdentifiedName = "Cathedral Map",
            },
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 23,
                MiscId = 44,
                Name = "Ear",
                IdentifiedName = "Ear",
            },
        ]);
        var map = new OwnedStoreItem(
            0,
            0,
            99,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 43,
                ItemIndex = 22,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var quest = new AuthoritativeQuestState(
            3017,
            17,
            1,
            active: true,
            logged: true,
            completionVariable1: 7,
            completionVariable2: 3,
            completionActivateQuestId: 3018);
        var followOnQuest = new AuthoritativeQuestState(3018, 17, 1, active: false);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventory: [map],
            startingItems: items,
            startingObjects: [new AuthoritativeWorldObject(
                40,
                6004,
                0,
                1,
                0,
                questId: 3017,
                effectKind: AuthoritativeObjectEffectKind.QuestItemCompletion,
                effectAmount: 43)],
            startingQuests: [quest, followOnQuest]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = 40 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
        Assert.True(executor.CreateSnapshot("player-a", 7, 1).Objects.Single().Activated);
        var snapshotQuests = executor.CreateSnapshot("player-a", 7, 1).Quests;
        Assert.True(snapshotQuests.Single(quest => quest.QuestId == 3017).Completed);
    }

    [Fact]
    public void HiveObjectInteractionConsumesServerOwnedRuneBombAndCompletesQuest()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                2101,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 39,
                Name = "Rune Bomb",
                IdentifiedName = "Rune Bomb",
            },
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 23,
                MiscId = 44,
                Name = "Ear",
                IdentifiedName = "Ear",
            },
        ]);
        var bomb = new OwnedStoreItem(
            0,
            0,
            99,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 2101,
                ItemIndex = 39,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var quest = new AuthoritativeQuestState(3018, 9, 1, active: false);
        var hiveObject = new AuthoritativeWorldObject(
            40,
            6005,
            0,
            79,
            61,
            questId: 3018,
            effectKind: AuthoritativeObjectEffectKind.QuestItemCompletion,
            effectAmount: 2101);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 100, 100));
        world.AddLevel(new AuthoritativeLevel(17, 100, 100));
        world.AddWarpTarget(new AuthoritativeWarpTarget(
            3,
            0,
            80,
            62,
            17,
            20,
            20,
            requiredQuestId: 3018,
            requiredQuestCompleted: true));
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingPositionX: 79,
            startingPositionY: 62,
            startingWorld: world,
            startingInventory: [bomb],
            startingItems: items,
            startingObjects: [hiveObject],
            startingQuests: [quest]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = 40 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
        Assert.True(executor.CreateSnapshot("player-a", 1, 1).Objects.Single().Activated);
        Assert.True(executor.CreateSnapshot("player-a", 1, 1).Quests.Single().Completed);

        var warpResult = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            MoveRequested = new MoveRequested {
                DirectionX = 1,
                DirectionY = 0,
            },
        }, 2);

        Assert.Equal(CommandStatus.Accepted, warpResult.Status);
        var player = Assert.Single(executor.CreateSnapshot("player-a", 1, 2).Players);
        Assert.Equal((17U, 20, 20), (player.LevelId, player.PositionX, player.PositionY));
    }

    [Fact]
    public void TrapDisarmProtocolIntentClearsServerOwnedArmedObject()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 640,
            startingLifeMaximum: 640,
            startingAbilitySpellId: 28,
            startingObjects: [new AuthoritativeWorldObject(
                20,
                4,
                0,
                1,
                0,
                trapArmed: true)],
            trapDisarmAbilitySpellId: 28);

        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            DisarmTrapRequested = new DisarmTrapRequested { ObjectEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.False(executor.CreateSnapshot("player-a", 1, 1).Objects.Single().TrapArmed);
    }

    [Fact]
    public void NativeHealingEventPreservesLegacyAmountAndExplicitQ6Amount()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 1000,
            startingMana: 3000,
            startingManaMaximum: 3000,
            startingLifeMaximum: 5000,
            startingCharacterLevel: 5,
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(2, 5, 1, 0, 0, 0) {
                    Range = 1,
                    ProjectileBehavior = AuthoritativeProjectileBehavior.Healing,
                    ManaAdjustment = 3,
                    CharacterLevelManaCost = 2,
                    MinimumManaCost = 1,
                    ManaCostScale = 64,
                    RandomHealingRange = 10,
                    CharacterLevelRandomHealingRange = 4,
                    CharacterLevelHealing = 1,
                    SpellLevelRandomHealingRange = 6,
                    SpellLevelHealing = 1,
                    HealingScale = 64,
                },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(2, 3)],
            startingRandomSeed: 0x12345678);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 2,
                TargetX = 37,
                TargetY = -5,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(3496, executor.GetPlayerState("player-a").Life);
        var healing = Assert.Single(executor.DrainEvents("player-a", 7, 1)!.Events).Healing;
        Assert.Equal(2496, healing.Amount);
        Assert.Equal(2496, healing.AmountQ6);
    }

    [Fact]
    public void DamageSpellMayResolveAnAuthoritativeTargetByCell()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingMana: 10,
            startingManaMaximum: 10,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 1, 1, 20)],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) { DamageAmount = 12, Range = 2 },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(7, 1)]);

        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 7,
                TargetX = 1,
                TargetY = 1,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(12, Assert.Single(executor.DrainEvents("player-a", 7, 1)!.Events).Damage.Amount);
    }

    [Fact]
    public void AreaDamageUsesTypedResistanceAndStableTargetOrdering()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingMana: 10,
            startingManaMaximum: 10,
            startingCombatTargets: [
                new AuthoritativeCombatTarget(9, 2, 0, 20, fireResistance: 50),
                new AuthoritativeCombatTarget(10, 2, 1, 20),
                new AuthoritativeCombatTarget(11, 4, 0, 20),
            ],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) {
                    DamageAmount = 10,
                    Range = 4,
                    AreaRadius = 1,
                    DamageType = AuthoritativeDamageType.Fire,
                },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(7, 1)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 7,
                TargetX = 2,
                TargetY = 0,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var snapshot = executor.CreateSnapshot("player-a", 1, 1);
        Assert.Equal(15, snapshot.Monsters.Single(monster => monster.EntityId == 9).HitPoints);
        Assert.Equal(10, snapshot.Monsters.Single(monster => monster.EntityId == 10).HitPoints);
        Assert.Equal(20, snapshot.Monsters.Single(monster => monster.EntityId == 11).HitPoints);
        var events = executor.DrainEvents("player-a", 7, 1)!.Events;
        Assert.Equal([9U, 10U], events.Select(@event => @event.Damage.TargetEntityId));
        Assert.Equal([5, 10], events.Select(@event => @event.Damage.Amount));
    }

    [Fact]
    public void PickupTransfersAnAuthoritativeWorldItemIntoTheInventory()
    {
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            Value = 75,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorldItems: [new AuthoritativeWorldItem(20, 0, 1, 0, 42, 75, itemState)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        var item = Assert.Single(player.Inventory);
        Assert.Equal(42U, item.ItemSeed);
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Empty(executor.CreateSnapshot("player-a", 7, 1).WorldItems);
    }

    [Fact]
    public void PickupPlacesAnInventoryOverflowItemOnTheAuthoritativeCursor()
    {
        var fillerState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var inventory = Enumerable.Range(0, 40)
            .Select(index => new OwnedStoreItem(
                0,
                0,
                checked(100U + (uint)index),
                0,
                0,
                fillerState))
            .ToArray();
        var itemState = fillerState with { StableItemId = 900 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: Enumerable.Range(0, 40).ToArray(),
            startingInventory: inventory,
            startingWorldItems: [new AuthoritativeWorldItem(20, 0, 1, 0, 42, 75, itemState)]);

        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        Assert.Equal(40, player.Inventory.Count);
        Assert.NotNull(player.HeldItem);
        Assert.Equal(42U, player.HeldItem!.ItemSeed);
        Assert.Empty(executor.CreateSnapshot("player-a", 7, 1).WorldItems);
    }

    [Fact]
    public void PickupReplacesAnAuthoritativeCursorItemByDroppingItAtThePlayerTile()
    {
        var fillerState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var inventory = Enumerable.Range(0, 40)
            .Select(index => new OwnedStoreItem(
                0,
                0,
                checked(100U + (uint)index),
                0,
                0,
                fillerState))
            .ToArray();
        var held = new OwnedStoreItem(
            0,
            0,
            77,
            65,
            0,
            fillerState);
        var itemState = fillerState with {
            StableItemId = 901,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: Enumerable.Range(0, 40).ToArray(),
            startingInventory: inventory,
            startingHeldItem: held,
            startingWorldItems: [new AuthoritativeWorldItem(20, 0, 1, 0, 42, 75, itemState)]);

        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var snapshot = executor.CreateSnapshot("player-a", 7, 1);
        var projectedPlayer = Assert.Single(snapshot.Players);
        Assert.NotNull(projectedPlayer.HeldItem);
        Assert.Equal(42U, projectedPlayer.HeldItem!.ItemSeed);
        var dropped = Assert.Single(snapshot.WorldItems);
        Assert.Equal(77U, dropped.ItemSeed);
        Assert.Equal((0, 0), (dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void PickupAppliesServerOwnedMagicRockQuestTransition()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                2135,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 9,
                Name = "Magic Rock",
                IdentifiedName = "Magic Rock",
                NativeCursorGraphic = 64,
            },
            new AuthoritativeItemDefinition(
                2138,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 18,
                Name = "Anvil of Fury",
                IdentifiedName = "Anvil of Fury",
                NativeCursorGraphic = 64,
            },
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 23,
                MiscId = 44,
                Name = "Ear",
                IdentifiedName = "Ear",
                NativeCursorGraphic = 64,
            },
        ]);
        var itemQuestEffects = new AuthoritativeItemQuestEffectCatalog([
            new AuthoritativeItemQuestEffectDefinition(
                2135,
                3001,
                AuthoritativeItemQuestEffectKind.ActivateQuest,
                1),
            new AuthoritativeItemQuestEffectDefinition(
                2138,
                3011,
                AuthoritativeItemQuestEffectKind.ActivateQuest,
                1),
        ]);
        var quest = new AuthoritativeQuestState(3001, 1, 3, active: false);
        var anvilQuest = new AuthoritativeQuestState(3011, 10, 1, active: false);
        var itemState = AuthoritativeItemState.Empty with {
            StableItemId = 2135,
            ItemIndex = 9,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingItems: items,
            startingItemQuestEffects: itemQuestEffects,
            startingQuests: [quest, anvilQuest],
            startingWorldItems: [
                new AuthoritativeWorldItem(20, 0, 1, 0, 99, 0, itemState),
                new AuthoritativeWorldItem(
                    21,
                    0,
                    1,
                    1,
                    100,
                    0,
                    itemState with {
                        StableItemId = 2138,
                        ItemIndex = 18,
                    }),
            ]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        var firstItem = Assert.Single(player.Inventory);
        Assert.Equal(2135U, firstItem.State.StableItemId);
        var snapshotQuest = Assert.Single(
            executor.CreateSnapshot("player-a", 7, 1).Quests,
            questSnapshot => questSnapshot.QuestId == 3001);
        Assert.False(snapshotQuest.Initial);
        Assert.True(snapshotQuest.Logged);
        Assert.Equal(1, snapshotQuest.Variable1);

        var anvilResult = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 21 },
        }, 2);

        Assert.Equal(CommandStatus.Accepted, anvilResult.Status);
        var updatedPlayer = executor.GetPlayerState("player-a");
        Assert.Collection(
            updatedPlayer.Inventory,
            first => Assert.Equal(2135U, first.State.StableItemId),
            second => Assert.Equal(2138U, second.State.StableItemId));
        var anvilSnapshot = Assert.Single(
            executor.CreateSnapshot("player-a", 7, 1).Quests,
            questSnapshot => questSnapshot.QuestId == 3011);
        Assert.False(anvilSnapshot.Initial);
        Assert.True(anvilSnapshot.Logged);
        Assert.Equal(1, anvilSnapshot.Variable1);
    }

    [Fact]
    public void PickupCompletesActiveOpticAmuletQuestTransition()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                114,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 12,
                Name = "Optic Amulet",
                IdentifiedName = "Optic Amulet",
                NativeCursorGraphic = 64,
            },
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 23,
                MiscId = 44,
                Name = "Ear",
                IdentifiedName = "Ear",
                NativeCursorGraphic = 64,
            },
        ]);
        var itemQuestEffects = new AuthoritativeItemQuestEffectCatalog([
            new AuthoritativeItemQuestEffectDefinition(
                114,
                3009,
                AuthoritativeItemQuestEffectKind.CompleteQuest,
                0),
        ]);
        var quest = new AuthoritativeQuestState(3009, 8, 1, active: true);
        var itemState = AuthoritativeItemState.Empty with {
            StableItemId = 114,
            ItemIndex = 12,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingItems: items,
            startingItemQuestEffects: itemQuestEffects,
            startingQuests: [quest],
            startingWorldItems: [new AuthoritativeWorldItem(20, 0, 1, 0, 99, 0, itemState)]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var player = executor.GetPlayerState("player-a");
        var item = Assert.Single(player.Inventory);
        Assert.Equal(114U, item.State.StableItemId);
        var snapshotQuest = Assert.Single(
            executor.CreateSnapshot("player-a", 7, 1).Quests,
            questSnapshot => questSnapshot.QuestId == 3009);
        Assert.True(snapshotQuest.Completed);
        Assert.False(snapshotQuest.Initial);
        Assert.True(snapshotQuest.Logged);
    }

    [Fact]
    public void PickupAppliesConditionalBlackMushroomQuestTransition()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                2139,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 19,
                Name = "Black Mushroom",
                IdentifiedName = "Black Mushroom",
                NativeCursorGraphic = 64,
            },
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                false) {
                NativeItemIndex = 23,
                MiscId = 44,
                Name = "Ear",
                IdentifiedName = "Ear",
                NativeCursorGraphic = 64,
            },
        ]);
        var itemQuestEffects = new AuthoritativeItemQuestEffectCatalog([
            new AuthoritativeItemQuestEffectDefinition(
                2139,
                3002,
                AuthoritativeItemQuestEffectKind.SetVariable1,
                4,
                RequiredVariable1: 3),
        ]);
        var quest = new AuthoritativeQuestState(
            3002,
            9,
            1,
            active: true,
            variable1: 3);
        var itemState = AuthoritativeItemState.Empty with {
            StableItemId = 2139,
            ItemIndex = 19,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingItems: items,
            startingItemQuestEffects: itemQuestEffects,
            startingQuests: [quest],
            startingWorldItems: [new AuthoritativeWorldItem(20, 0, 1, 0, 99, 0, itemState)]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = 20 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var snapshotQuest = Assert.Single(
            executor.CreateSnapshot("player-a", 7, 1).Quests,
            questSnapshot => questSnapshot.QuestId == 3002);
        Assert.Equal(4, snapshotQuest.Variable1);
    }

    [Fact]
    public void DropHeldItemCommandReturnsServerOwnedCursorStateToWorld()
    {
        var heldItem = new OwnedStoreItem(
            0,
            0,
            42,
            65,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 11,
                ItemIndex = 0,
                Value = 65,
                IdentifiedValue = 65,
                StatFlag = true,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingHeldItem: heldItem);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            DropHeldItemRequested = new DropHeldItemRequested {
                TargetX = 1,
                TargetY = 0,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var snapshot = executor.CreateSnapshot("player-a", 7, 1);
        Assert.Null(Assert.Single(snapshot.Players).HeldItem);
        var dropped = Assert.Single(snapshot.WorldItems);
        Assert.Equal(42U, dropped.ItemSeed);
        Assert.Equal(65U, dropped.Price);
        Assert.Equal((1, 0), (dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void DropItemCommandMovesAStableInventoryItemToTheAuthoritativeWorld()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            65,
            1,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                ItemIndex = 1,
                Value = 65,
                IdentifiedValue = 65,
                StableItemId = 7001,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingPositionX: 0,
            startingPositionY: 0,
            startingInventory: [item],
            startingInventoryGrid: [0, ..Enumerable.Repeat(-1, 39)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            DropItemRequested = new DropItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = 0,
                    StableItemId = 7001,
                },
                TargetX = 1,
                TargetY = 0,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var snapshot = executor.CreateSnapshot("player-a", 7, 1);
        Assert.Empty(Assert.Single(snapshot.Players).Inventory);
        var dropped = Assert.Single(snapshot.WorldItems);
        Assert.Equal(7001U, dropped.State.StableItemId);
        Assert.Equal((1, 0), (dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void ObjectInteractionAndQuestProgressAreAuthoritative()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingPositionX: 1,
            startingPositionY: 1,
            startingObjects: [new AuthoritativeWorldObject(20, 4, 0, 2, 1, questId: 30)],
            startingQuests: [new AuthoritativeQuestState(30, 0, 2)]);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = 20 },
        }, 1).Status);
        var objectActivatedSnapshot = executor.CreateSnapshot("player-a", 7, 1);
        Assert.Equal(1U, Assert.Single(objectActivatedSnapshot.Quests).Progress);
        Assert.Equal(CommandRejectReason.InvalidTarget, server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = 20 },
        }, 2).RejectReason);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 3,
            AdvanceQuestRequested = new AdvanceQuestRequested { QuestId = 30 },
        }, 3).Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 4,
            AdvanceQuestRequested = new AdvanceQuestRequested { QuestId = 30 },
        }, 4).RejectReason);

        var snapshot = executor.CreateSnapshot("player-a", 7, 4);
        var objectSnapshot = Assert.Single(snapshot.Objects);
        Assert.True(objectSnapshot.Activated);
        Assert.Equal(30U, objectSnapshot.QuestId);
        var quest = Assert.Single(snapshot.Quests);
        Assert.Equal(2U, quest.Progress);
        Assert.True(quest.Completed);
    }

    [Fact]
    public void TurnInQuestProtocolCommandReachesAuthoritativeTownerBoundary()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(24, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 23,
                MiscId = 44,
            },
        ]);
        var quest = new AuthoritativeQuestState(
            3011,
            10,
            1,
            active: true,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 2,
            turnInPositionY: 1,
            turnInItemId: 7,
            turnInRequiredVariable2: 1);
        var inventoryItem = new OwnedStoreItem(
            0,
            0,
            99,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 7,
                ItemIndex = 7,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingPositionX: 1,
            startingPositionY: 1,
            startingInventory: [inventoryItem],
            startingItems: items,
            startingQuests: [quest]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            TurnInQuestRequested = new TurnInQuestRequested {
                QuestId = 3011,
                TownerId = 0,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var snapshot = executor.CreateSnapshot("player-a", 7, 1);
        var projectedQuest = Assert.Single(snapshot.Quests);
        Assert.Equal(1, projectedQuest.Variable2);
        Assert.Single(executor.GetPlayerState("player-a").Inventory);
    }

    [Fact]
    public void QuestDialogueCommandActivatesOnlyTheMatchingUniqueEncounter()
    {
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 0,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCombatTargets: [new AuthoritativeCombatTarget(
                20,
                1,
                0,
                100,
                unique: true,
                uniqueIdentity: "Gharbad")],
            startingQuests: [new AuthoritativeQuestState(
                3003,
                0,
                1,
                active: false,
                messageId: 77,
                completionUniqueIdentity: "Gharbad")]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            TalkToQuestRequested = new TalkToQuestRequested {
                QuestId = 3003,
                TargetEntityId = 20,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var events = executor.DrainEvents("player-a", 7, 1);
        var dialogue = Assert.Single(events!.Events).Dialogue;
        Assert.Equal(3003U, dialogue.QuestId);
        Assert.Equal(20U, dialogue.TargetEntityId);
        Assert.Equal(77, dialogue.MessageId);
        Assert.False(Assert.Single(executor.CreateSnapshot("player-a", 7, 1).Quests).Initial);
    }

    [Fact]
    public void DefeatingAMonsterSpawnsItsConfiguredDrop()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 1, dropItemEntityId: 1009, dropItemSeed: 6001, dropItemPrice: 25);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 0,
            startingLife: 40,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCombatTargets: [target]);

        var result = executor.Execute("player-a", new Command { AttackRequested = new AttackRequested { TargetEntityId = 9 } }, 1);

        Assert.True(result.Succeeded);
        var item = Assert.Single(executor.CreateSnapshot("player-a", 7, 1).WorldItems);
        Assert.Equal(1009U, item.EntityId);
        Assert.Equal(6001U, item.ItemSeed);
    }

    [Fact]
    public void AdjacentAttackDamagesTargetsAndAwardsExperienceOnDefeat()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, hitPoints: 11, armorClass: 2);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCharacterLevel: 1,
            startingCombatTargets: [target]);
        var server = new AuthoritativeCommandServer(executor);

        var first = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AttackRequested = new AttackRequested { TargetEntityId = 9 },
        }, 1);
        Assert.Equal(CommandStatus.Accepted, first.Status);
        Assert.Equal(3, target.HitPoints);
        Assert.Equal(0U, executor.GetPlayerState("player-a").Experience);
        var hitEvents = executor.DrainEvents("player-a", 7, 1);
        Assert.NotNull(hitEvents);
        Assert.Equal(8, Assert.Single(hitEvents.Events).Damage.Amount);

        var second = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            AttackRequested = new AttackRequested { TargetEntityId = 9 },
        }, 2);
        Assert.Equal(CommandStatus.Accepted, second.Status);
        Assert.Equal(0, target.HitPoints);
        Assert.Equal(100U, executor.GetPlayerState("player-a").Experience);
        var defeatEvents = executor.DrainEvents("player-a", 7, 2);
        Assert.NotNull(defeatEvents);
        Assert.Equal(2, defeatEvents.Events.Count);
        Assert.Equal(100U, defeatEvents.Events.Single(gameEvent => gameEvent.Experience is not null).Experience.Amount);

        var snapshot = executor.CreateSnapshot("player-a", 7, 2);
        Assert.Empty(snapshot.Monsters);
        var corpse = Assert.Single(snapshot.Corpses);
        Assert.Equal(9U, corpse.EntityId);

        var distant = new AuthoritativeCombatTarget(10, 4, 4, 10);
        var distantExecutor = new StoreSimulationExecutor(CreateCatalog(), 100, startingCombatTargets: [distant]);
        var distantResult = new AuthoritativeCommandServer(distantExecutor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AttackRequested = new AttackRequested { TargetEntityId = 10 },
        }, 1);
        Assert.Equal(CommandRejectReason.InvalidTarget, distantResult.RejectReason);
    }

    [Fact]
    public void CellTargetedAttackResolvesAgainstTheAdjacentDirectionalOccupant()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, hitPoints: 11, armorClass: 2);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCombatTargets: [target]);

        var result = executor.Execute("player-a", new Command {
            AttackRequested = new AttackRequested {
                TargetCell = true,
                TargetX = 1,
                TargetY = 0,
            },
        }, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(3, target.HitPoints);
        Assert.Equal(8, Assert.Single(executor.DrainEvents("player-a", 7, 1)!.Events).Damage.Amount);
    }

    [Fact]
    public void EmptyCellTargetedAttackIsAcceptedWithoutACombatEvent()
    {
        var target = new AuthoritativeCombatTarget(9, 2, 0, hitPoints: 11, armorClass: 2);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCombatTargets: [target]);

        var result = executor.Execute("player-a", new Command {
            AttackRequested = new AttackRequested {
                TargetCell = true,
                TargetX = 1,
                TargetY = 0,
            },
        }, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(11, target.HitPoints);
        Assert.Null(executor.DrainEvents("player-a", 7, 1));
    }

    [Fact]
    public void DiabloDefeatPublishesVictoryEventAndSnapshotState()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 0,
            startingPositionY: 0,
            difficulty: DiabloDifficulty.Hell,
            startingCombatRules: new AuthoritativeCombatRules(1000, 1, 100),
            startingCombatTargets: [new AuthoritativeCombatTarget(
                9,
                1,
                0,
                1,
                monsterId: 112,
                diablo: true)]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AttackRequested = new AttackRequested { TargetEntityId = 9 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var events = executor.DrainEvents("player-a", 7, 1);
        var victory = Assert.Single(
            events!.Events,
            gameEvent => gameEvent.Victory is not null).Victory;
        Assert.Equal(7U, victory.PlayerEntityId);
        Assert.Equal(9U, victory.BossEntityId);
        Assert.Equal(3U, victory.DiabloKillLevel);
        var victorySnapshot = executor.CreateSnapshot("player-a", 7, 1);
        Assert.True(victorySnapshot.VictoryTriggered);
        Assert.Equal(GameDifficulty.Hell, victorySnapshot.Difficulty);

        var followUp = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 2);
        Assert.Equal(CommandStatus.Rejected, followUp.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, followUp.RejectReason);
    }

    [Fact]
    public void MovementRejectsBlockedCellsWithoutChangingPosition()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            worldWidth: 4,
            worldHeight: 4,
            startingBlockedCells: [1 * 4 + 2]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 1);

        Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(1, state.PositionX);
        Assert.Equal(1, state.PositionY);
    }

    [Fact]
    public void PortalRequiresSourceCellAndProjectsDestinationLevel()
    {
        var portal = new AuthoritativePortal(5, 2, 1, 1, 7, 3, 4);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLevelId: 2,
            startingPortals: [portal]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            UsePortalRequested = new UsePortalRequested { PortalId = 5 },
        }, 1);
        var snapshot = executor.CreateSnapshot("player-a", 7, 1);
        var player = Assert.Single(snapshot.Players);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(7U, player.LevelId);
        Assert.Equal(3, player.PositionX);
        Assert.Equal(4, player.PositionY);

        var invalidExecutor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLevelId: 1,
            startingPortals: [portal]);
        var invalid = new AuthoritativeCommandServer(invalidExecutor).Process("player-b", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            UsePortalRequested = new UsePortalRequested { PortalId = 5 },
        }, 1);
        Assert.Equal(CommandRejectReason.InvalidTarget, invalid.RejectReason);
    }

    [Fact]
    public void MovementUsesLevelSpecificAuthoritativeWorldGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(2, 4, 4, [1 * 4 + 2]));
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLevelId: 2,
            startingWorld: world);

        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 1);

        Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
    }

    [Fact]
    public void LiveSnapshotsProjectAuthoritativeLevelGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(
            2,
            4,
            4,
            blockedCells: [6],
            missileBlockedCells: [5],
            nativeTransparency: [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25],
            nativePieceLayer: [0, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [7] = AuthoritativeDunTileFlags.BlockLight,
            }));
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingLevelId: 2,
            startingWorld: world);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 1);

        var snapshot = executor.CreateSnapshot("player-a", 1, 1);
        var geometry = Assert.Single(snapshot.LevelGeometry);

        Assert.Equal(2U, geometry.LevelId);
        Assert.Equal(4, geometry.Width);
        Assert.Equal(4, geometry.Height);
        Assert.Equal([6], geometry.BlockedCells);
        Assert.Equal([5], geometry.MissileBlockedCells);
        Assert.Equal([1, 6], geometry.NativeVisionBlockedCells);
        Assert.Equal([10U, 11U, 12U, 13U, 14U, 15U, 16U, 17U, 18U, 19U, 20U, 21U, 22U, 23U, 24U, 25U], geometry.NativeTransparency);
        Assert.Equal(SnapshotStateHasher.Compute(snapshot), snapshot.StateSha256);

        var saveDocument = System.Text.Json.JsonSerializer.Deserialize<AuthoritativeSaveDocument>(
            executor.ExportPlayerSave("player-a"));
        var savedSnapshot = Snapshot.Parser.ParseFrom(
            Convert.FromBase64String(saveDocument!.SnapshotBase64));
        Assert.Empty(savedSnapshot.LevelGeometry);
    }

    [Fact]
    public void MovementRejectsOccupiedMonsterCells()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 1, 0, 10)]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 1);

        Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
        Assert.Equal(0, executor.GetPlayerState("player-a").PositionX);
    }

    [Fact]
    public void MovementRejectsAnotherAuthoritativePlayerCell()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 100, startingPositionX: 0, startingPositionY: 0);
        executor.CreateSnapshot("player-a", 7, 0);
        executor.CreateSnapshot("player-b", 8, 0);
        var otherPlayerMove = new AuthoritativeCommandServer(executor).Process("player-b", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 1);
        Assert.Equal(CommandStatus.Accepted, otherPlayerMove.Status);

        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 2,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 2);

        Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
    }

    [Fact]
    public void AutonomousMonsterTickMovesAndAttacksDeterministically()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 20,
            startingLifeMaximum: 20,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 2, 0, 10, levelId: 0, monsterId: 1, attackDamage: 3, aggroRange: 5)],
            worldWidth: 5,
            worldHeight: 5);
        executor.CreateSnapshot("player-a", 7, 0);
        var server = new AuthoritativeCommandServer(executor);

        var firstTick = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 0 },
        }, 1);
        Assert.Equal(CommandStatus.Rejected, firstTick.Status);
        var movingMonster = Assert.Single(
            executor.CreateSnapshot("player-a", 7, 1).Monsters);
        Assert.Equal(2, movingMonster.PositionX);
        Assert.True(movingMonster.Moving);
        Assert.Equal(1, movingMonster.FuturePositionX);

        var secondTick = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 0 },
        }, 2);
        Assert.Equal(CommandStatus.Rejected, secondTick.Status);
        Assert.Equal(17, executor.GetPlayerState("player-a").Life);
        var events = executor.DrainEvents("player-a", 7, 2);
        Assert.NotNull(events);
        var damage = Assert.Single(events.Events).Damage;
        Assert.Equal(9U, damage.SourceEntityId);
        Assert.Equal(7U, damage.TargetEntityId);
        Assert.Equal(3, damage.Amount);
    }

    [Fact]
    public void AutonomousMonsterSimulationCatchesUpEveryAuthoritativeTick()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 20,
            startingLifeMaximum: 20,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 3, 0, 10, levelId: 0, monsterId: 1, attackDamage: 2, aggroRange: 5)],
            worldWidth: 5,
            worldHeight: 5);
        executor.CreateSnapshot("player-a", 7, 0);

        executor.AdvanceTo(3);

        var monster = Assert.Single(executor.CreateSnapshot("player-a", 7, 3).Monsters);
        Assert.Equal(1, monster.PositionX);
        Assert.Equal(18, executor.GetPlayerState("player-a").Life);
    }

    [Fact]
    public void AutonomousSimulationAdvancesMultipleActorsInStableEntityOrder()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 30,
            startingLifeMaximum: 30,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCombatTargets: [
                new AuthoritativeCombatTarget(11, 3, 0, 10, levelId: 0, monsterId: 1, attackDamage: 1, aggroRange: 6),
                new AuthoritativeCombatTarget(9, 4, 1, 10, levelId: 0, monsterId: 1, attackDamage: 1, aggroRange: 6),
            ],
            worldWidth: 6,
            worldHeight: 6);
        executor.CreateSnapshot("player-a", 7, 0);

        executor.AdvanceTo(2);

        var monsters = executor.CreateSnapshot("player-a", 7, 2).Monsters.OrderBy(monster => monster.EntityId).ToArray();
        Assert.Equal(2, monsters.Length);
        Assert.Equal(9U, monsters[0].EntityId);
        Assert.Equal(11U, monsters[1].EntityId);
        Assert.NotEqual((4, 1), (monsters[0].PositionX, monsters[0].PositionY));
        Assert.NotEqual((3, 0), (monsters[1].PositionX, monsters[1].PositionY));
        Assert.Equal(30, executor.GetPlayerState("player-a").Life);
    }

    [Fact]
    public void AutonomousMultipleMonsterSaveRestoreContinuesInStableEntityOrder()
    {
        var targets = new[] {
            new AuthoritativeCombatTarget(
                11,
                3,
                0,
                10,
                levelId: 0,
                monsterId: 1,
                attackDamage: 1,
                aggroRange: 6),
            new AuthoritativeCombatTarget(
                9,
                4,
                1,
                10,
                levelId: 0,
                monsterId: 1,
                attackDamage: 1,
                aggroRange: 6),
        };
        var source = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 30,
            startingLifeMaximum: 30,
            startingCombatTargets: targets,
            worldWidth: 6,
            worldHeight: 6,
            startingRandomSeed: 0x12345678);
        source.CreateSnapshot("player-a", 7, 0);
        source.AdvanceTo(2);
        var expectedAtSave = source.CreateSnapshot("player-a", 7, 2);

        var replacement = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        replacement.ImportPlayerSave("player-a", source.ExportPlayerSave("player-a"));
        var restoredAtSave = replacement.CreateSnapshot("player-a", 7, 2);

        Assert.Equal(expectedAtSave.Tick, restoredAtSave.Tick);
        Assert.Equal(expectedAtSave.StateSha256, restoredAtSave.StateSha256);
        Assert.Equal(
            expectedAtSave.Monsters.Select(monster => (
                monster.EntityId,
                monster.PositionX,
                monster.PositionY,
                monster.HitPointsQ6)),
            restoredAtSave.Monsters.Select(monster => (
                monster.EntityId,
                monster.PositionX,
                monster.PositionY,
                monster.HitPointsQ6)));

        source.AdvanceTo(3);
        replacement.AdvanceTo(3);
        var expectedAfterResume = source.CreateSnapshot("player-a", 7, 3);
        var restoredAfterResume = replacement.CreateSnapshot("player-a", 7, 3);
        Assert.Equal(expectedAfterResume.StateSha256, restoredAfterResume.StateSha256);
        Assert.Equal(
            expectedAfterResume.Monsters.Select(monster => monster.EntityId),
            restoredAfterResume.Monsters.Select(monster => monster.EntityId));
    }

    [Fact]
    public void AutonomousMonsterAttackCanMissFromExternalCombatRules()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 20,
            startingLifeMaximum: 20,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 1, 0, 10, levelId: 0, monsterId: 1, attackDamage: 3, aggroRange: 5)],
            startingCombatRules: new AuthoritativeCombatRules(10, 1, 100, hitChancePercent: 0),
            worldWidth: 5,
            worldHeight: 5);
        executor.CreateSnapshot("player-a", 7, 0);

        executor.AdvanceTo(1);

        Assert.Equal(20, executor.GetPlayerState("player-a").Life);
    }

    [Fact]
    public void DamageSpellCannotPassThroughAuthoritativeGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 5, 3, [1 * 5 + 2]));
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingMana: 10,
            startingManaMaximum: 10,
            startingPositionX: 0,
            startingPositionY: 1,
            startingLevelId: 1,
            startingWorld: world,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 4, 1, 10, levelId: 1, monsterId: 1)],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(4, 2, 0, 0, 0, 0) { DamageAmount = 6, Range = 5 },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(4, 1)]);

        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 4,
                TargetEntityId = 9,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
        Assert.Equal(10, Assert.Single(executor.CreateSnapshot("player-a", 7, 1).Monsters).HitPoints);
    }

    [Fact]
    public void HasteStatusIsAuthoritativeAndExpiresByAppliedTick()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingMana: 10,
            startingManaMaximum: 10,
            startingMemorizedSpells: [new MemorizedSpellState(2, 1)]);
        var server = new AuthoritativeCommandServer(executor);
        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 2,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(7, executor.GetPlayerState("player-a").Mana);
        var active = executor.CreateSnapshot("player-a", 7, 1).Players[0];
        var effect = Assert.Single(active.StatusEffects);
        Assert.Equal(1U, effect.EffectId);
        Assert.Equal(10U, effect.RemainingTicks);

        var expired = executor.CreateSnapshot("player-a", 7, 11).Players[0];
        Assert.Empty(expired.StatusEffects);
    }

    [Fact]
    public void AuthoritativeSaveRoundTripsPlayerStateAndRejectsMalformedState()
    {
        var worldItemState = AuthoritativeItemState.Empty with { ItemType = 1, ItemIndex = 2, Value = 75 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingExperience: 25,
            startingLife: 40,
            startingLifeMaximum: 60,
            startingMana: 8,
            startingManaMaximum: 10,
            startingPositionX: 2,
            startingPositionY: 3,
            worldWidth: 4,
            worldHeight: 4,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 1, 1, 12, armorClass: 2, maxHitPoints: 20, monsterId: 4, attackDamage: 4, aggroRange: 6, fireResistance: 25)],
            startingWorldItems: [new AuthoritativeWorldItem(20, 0, 2, 2, 42, 75, worldItemState)],
            startingObjects: [new AuthoritativeWorldObject(30, 4, 0, 0, 1, activated: true, questId: 40)],
            startingQuests: [
                new AuthoritativeQuestState(40, 0, 2, progress: 1),
                new AuthoritativeQuestState(41, 0, 2, unavailable: true),
                new AuthoritativeQuestState(42, 0, 2, active: false, logged: true),
                new AuthoritativeQuestState(43, 0, 2, variable1: 2, variable2: 7, messageId: 123),
            ]);
        var state = executor.GetPlayerState("player-a");
        var save = executor.ExportPlayerSave("player-a");

        var replacement = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 40,
            startingLifeMaximum: 60,
            startingMana: 0,
            startingManaMaximum: 10,
            worldWidth: 4,
            worldHeight: 4);
        replacement.ImportPlayerSave("player-a", save);
        var restored = replacement.GetPlayerState("player-a");

        Assert.Equal(state.Gold, restored.Gold);
        Assert.Equal(state.Experience, restored.Experience);
        Assert.Equal(state.PositionX, restored.PositionX);
        Assert.Equal(state.PositionY, restored.PositionY);
        var restoredSnapshot = replacement.CreateSnapshot("player-a", restored.EntityId, 0);
        var restoredMonster = Assert.Single(restoredSnapshot.Monsters);
        Assert.Equal(12, restoredMonster.HitPoints);
        Assert.Equal(1, restoredMonster.PositionX);
        Assert.Equal(4, restoredMonster.AttackDamage);
        Assert.Equal(6, restoredMonster.AggroRange);
        Assert.Equal(25, restoredMonster.FireResistance);
        var restoredWorldItem = Assert.Single(restoredSnapshot.WorldItems);
        Assert.Equal(42U, restoredWorldItem.ItemSeed);
        Assert.Equal(75U, restoredWorldItem.Price);
        Assert.True(Assert.Single(restoredSnapshot.Objects).Activated);
        Assert.Equal(40U, Assert.Single(restoredSnapshot.Objects).QuestId);
        Assert.Equal(1U, restoredSnapshot.Quests.Single(quest => quest.QuestId == 40).Progress);
        Assert.True(restoredSnapshot.Quests.Single(quest => quest.QuestId == 41).Unavailable);
        Assert.True(restoredSnapshot.Quests.Single(quest => quest.QuestId == 42).Initial);
        Assert.True(restoredSnapshot.Quests.Single(quest => quest.QuestId == 42).Logged);
        var nativeQuest = restoredSnapshot.Quests.Single(quest => quest.QuestId == 43);
        Assert.Equal((2, 7, 123), (nativeQuest.Variable1, nativeQuest.Variable2, nativeQuest.MessageId));
        Assert.Throws<InvalidDataException>(() => replacement.ImportPlayerSave("player-a", "{\"FormatVersion\":99}"));
    }

    [Fact]
    public void ImportingAPlayerSaveDiscardsEventsQueuedBeforeTheRestore()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingMana: 10,
            startingManaMaximum: 10,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 1, 0, 20, armorClass: 2)],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) {
                    DamageAmount = 12,
                    Range = 1,
                },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(7, 1)]);
        var commandServer = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, commandServer.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 7,
                TargetEntityId = 9,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1).Status);
        var save = executor.ExportPlayerSave("player-a");
        // Queue another event immediately before restoring the earlier save.
        Assert.Equal(CommandStatus.Accepted, commandServer.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 4,
            CastRequested = new CastRequested {
                SpellId = 7,
                TargetEntityId = 9,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 4).Status);
        executor.ImportPlayerSave("player-a", save);

        Assert.Null(executor.DrainEvents("player-a", 7, 1));
    }

    [Fact]
    public void AuthoritativeSaveRestoresRemainingActiveStoreStock()
    {
        var source = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10)),
            startingGold: 100);
        var server = new AuthoritativeCommandServer(source);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 0).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 1).Status);

        var replacementCatalog = CreateCatalog(new StoreItem(0, 42, 10));
        var replacement = new StoreSimulationExecutor(replacementCatalog, startingGold: 0);
        replacement.ImportPlayerSave("player-a", source.ExportPlayerSave("player-a"));

        Assert.Empty(replacementCatalog.GetItems(1));
        Assert.Empty(replacement.CreateSnapshot("player-a", 7, 0).ActiveStore.Items);
    }

    [Fact]
    public void AuthoritativeSessionSavesRestoreMultipleParticipantsWithCanonicalHash()
    {
        var source = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 100,
            startingLifeMaximum: 100,
            startingLevelId: 1);
        source.InitializeParticipant(
            "player-b",
            DiabloPlayerTemplate.CreateDefault(0, 100, 0, 1));
        _ = source.CreateSnapshot("player-a", 1, 0);
        _ = source.CreateSnapshot("player-b", 2, 0);
        source.AdvanceTo(5);
        var expected = source.CreateSnapshot("player-a", 1, 5);
        var playerASave = source.ExportSessionState("player-a");
        var playerBSave = source.ExportSessionState("player-b");

        var replacement = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 100,
            startingLifeMaximum: 100,
            startingLevelId: 1);
        replacement.ImportSessionState("player-a", playerASave);
        replacement.ImportSessionState("player-b", playerBSave);

        var restored = replacement.CreateSnapshot("player-a", 1, 5);
        Assert.Equal(expected.StateSha256, restored.StateSha256);
        Assert.Equal([1U, 2U], restored.Players.Select(player => player.EntityId));
    }

    [Fact]
    public void AuthoritativeSaveRoundTripRetainsMultiLevelWorldEntities()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 5, 5, []));
        world.AddLevel(new AuthoritativeLevel(2, 5, 5, []));
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 40,
            startingLevelId: 1,
            startingWorld: world,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 2, 2, 10, levelId: 2, monsterId: 1)],
            startingWorldItems: [new AuthoritativeWorldItem(20, 2, 3, 3, 42, 75, AuthoritativeItemState.Empty)],
            startingObjects: [new AuthoritativeWorldObject(30, 4, 2, 1, 1)]);

        var save = executor.ExportPlayerSave("player-a");
        var replacement = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0, startingLife: 40, startingLevelId: 1, startingWorld: world);
        replacement.ImportPlayerSave("player-a", save);

        var restored = replacement.CreateSnapshot("player-a", 1, 0);
        Assert.Single(restored.Monsters);
        Assert.Single(restored.WorldItems);
        Assert.Single(restored.Objects);
        Assert.Contains(restored.Monsters, monster => monster.EntityId == 9 && monster.LevelId == 2);
        Assert.Contains(restored.WorldItems, item => item.EntityId == 20 && item.LevelId == 2);
        Assert.Contains(restored.Objects, @object => @object.EntityId == 30 && @object.LevelId == 2);
    }

    [Fact]
    public void AuthoritativeSaveRoundTripRetainsProjectilesAndObjectEffects()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLife: 20,
            startingLifeMaximum: 40,
            startingMana: 10,
            startingManaMaximum: 10,
            startingAttributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 2, 0, 20)],
            startingObjects: [new AuthoritativeWorldObject(
                30,
                4,
                0,
                1,
                0,
                effectKind: AuthoritativeObjectEffectKind.Heal,
                effectAmount: 5,
                missilePassable: true)],
            startingSpells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) {
                    Range = 4,
                    DamageType = AuthoritativeDamageType.Lightning,
                    ProjectileSpeed = 8,
                    ProjectileLifetime = 256,
                    ProjectileCount = 4,
                    ProjectileCountLevelDivisor = 2,
                    ProjectileBehavior = AuthoritativeProjectileBehavior.ChargedBolt,
                    RandomMagicDamageDivisor = 4,
                    ResistantDamageDivisor = 4,
                    ProjectileImpactLifetime = 8,
                },
            ]),
            startingMemorizedSpells: [new MemorizedSpellState(30, 1)],
            startingRandomSeed: 0x12345678);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested {
                SpellId = 30,
                TargetEntityId = 9,
                Source = CastSourceKind.MemorizedSpell,
            },
        }, 1).Status);

        var replacement = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0, startingLife: 20, startingLifeMaximum: 40, startingMana: 0, startingManaMaximum: 10);
        replacement.ImportPlayerSave("player-a", executor.ExportPlayerSave("player-a"));
        var restored = replacement.CreateSnapshot("player-a", 0, 0);

        Assert.Equal(4, restored.Projectiles.Count);
        Assert.Equal(0x858FBDECU, restored.RandomState);
        Assert.All(restored.Projectiles, projectile => {
            Assert.Equal((int)AuthoritativeProjectileBehavior.ChargedBolt, projectile.Behavior);
            Assert.Equal(8, projectile.Speed);
            Assert.Equal(8, projectile.ImpactLifetime);
            Assert.Equal(4, projectile.ResistantDamageDivisor);
        });
        var restoredObject = Assert.Single(restored.Objects);
        Assert.Equal((int)AuthoritativeObjectEffectKind.Heal, restoredObject.EffectKind);
        Assert.Equal(5, restoredObject.EffectAmount);
        Assert.True(restoredObject.MissilePassable);
    }

    [Fact]
    public void AuthoritativeSaveRejectsDuplicateItemsAndInvalidInventoryFootprints()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 10));
        var source = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(source);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 0).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 1).Status);

        var document = System.Text.Json.JsonSerializer.Deserialize<AuthoritativeSaveDocument>(source.ExportPlayerSave("player-a"))!;
        var snapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(document.SnapshotBase64));
        var player = Assert.Single(snapshot.Players);
        player.InventoryGrid.Add(0);
        player.Inventory[0].State.InventoryWidth = 2;
        var invalidFootprint = document with { SnapshotBase64 = Convert.ToBase64String(snapshot.ToByteArray()) };

        var destination = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        Assert.Throws<InvalidDataException>(() => destination.ImportPlayerSave("player-a", System.Text.Json.JsonSerializer.Serialize(invalidFootprint)));

        var duplicateSnapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(document.SnapshotBase64));
        duplicateSnapshot.Players[0].Equipment.Add(new EquippedItemSnapshot {
            Slot = 0,
            ItemSeed = duplicateSnapshot.Players[0].Inventory[0].ItemSeed,
            State = duplicateSnapshot.Players[0].Inventory[0].State,
        });
        var duplicateItem = document with { SnapshotBase64 = Convert.ToBase64String(duplicateSnapshot.ToByteArray()) };
        Assert.Throws<InvalidDataException>(() => destination.ImportPlayerSave("player-a", System.Text.Json.JsonSerializer.Serialize(duplicateItem)));
    }

    [Fact]
    public void AuthoritativeSaveRejectsNegativeAttributeState()
    {
        var source = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        var document = System.Text.Json.JsonSerializer.Deserialize<AuthoritativeSaveDocument>(
            source.ExportPlayerSave("player-a"))!;
        var snapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(document.SnapshotBase64));
        var player = Assert.Single(snapshot.Players);
        player.Attributes.Strength.Base = -1;
        var invalidAttributes = document with {
            SnapshotBase64 = Convert.ToBase64String(snapshot.ToByteArray()),
        };

        var destination = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        Assert.Throws<InvalidDataException>(() => destination.ImportPlayerSave(
            "player-a",
            System.Text.Json.JsonSerializer.Serialize(invalidAttributes)));
    }

    [Fact]
    public void AuthoritativeSaveRejectsUnrepresentablePlayerResources()
    {
        var source = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        var document = System.Text.Json.JsonSerializer.Deserialize<AuthoritativeSaveDocument>(
            source.ExportPlayerSave("player-a"))!;
        var destination = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);

        void AssertRejected(Action<PlayerSnapshot> mutate)
        {
            var snapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(document.SnapshotBase64));
            mutate(Assert.Single(snapshot.Players));
            var invalidResources = document with {
                SnapshotBase64 = Convert.ToBase64String(snapshot.ToByteArray()),
            };
            Assert.Throws<InvalidDataException>(() => destination.ImportPlayerSave(
                "player-a",
                System.Text.Json.JsonSerializer.Serialize(invalidResources)));
        }

        AssertRejected(player => player.Gold = (uint)int.MaxValue + 1U);
        AssertRejected(player => player.StashGold = (uint)int.MaxValue + 1U);
        AssertRejected(player => player.StatPoints = (uint)int.MaxValue + 1U);
    }

    [Fact]
    public void AuthoritativeSaveRejectsOverlappingStashFootprints()
    {
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 20,
            StableItemId = 920,
            InventoryWidth = 2,
            InventoryHeight = 1,
        };
        var source = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingStash: [
                new StashStoreItem(0, 0, 0, 0, 501, 1, 0, itemState),
                new StashStoreItem(0, 1, 0, 1, 502, 1, 0, itemState with {
                    StableItemId = 921,
                    ItemIndex = 21,
                }),
            ]);
        var destination = new StoreSimulationExecutor(CreateCatalog(), startingGold: 0);
        var sourceServer = new AuthoritativeCommandServer(source);
        Assert.Equal(CommandStatus.Accepted, sourceServer.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 0,
            SetFriendlyModeRequested = new SetFriendlyModeRequested { FriendlyMode = true },
        }, 0).Status);

        Assert.Throws<InvalidDataException>(() => destination.ImportPlayerSave(
            "player-a",
            source.ExportPlayerSave("player-a")));
    }

    [Fact]
    public void AuthoritativeWorldRejectsImpossibleStartingPlacement()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 2, 2, [0]));

        Assert.Throws<InvalidDataException>(() => new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingLevelId: 1,
            startingWorld: world));
    }

    [Fact]
    public void AuthoritativeWorldRejectsOverlappingLiveEntities()
    {
        Assert.Throws<InvalidDataException>(() => new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 0,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 0, 0, 10)],
            startingWorldItems: [new AuthoritativeWorldItem(20, 0, 0, 0, 42, 10, AuthoritativeItemState.Empty)]));
    }

    [Fact]
    public void AuthoritativeSaveStoreWritesAndReplacesOnlySafeSessionPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "devilution-save-tests", Guid.NewGuid().ToString("N"));
        try {
            var store = new AuthoritativeSaveStore(root);
            store.Save("player-a", "first");
            store.Save("player-a", "second");

            Assert.Equal("second", store.Load("player-a"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
            Assert.True(store.Delete("player-a"));
            Assert.Null(store.Load("player-a"));
            Assert.Throws<ArgumentException>(() => store.Save("../escape", "invalid"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static StoreCatalog CreateCatalog(params StoreItem[] items)
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, items);
        return catalog;
    }

    private static Command OpenStore(ulong clientSequence, uint storeId = 1)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = 10,
            OpenStoreRequested = new OpenStoreRequested { StoreId = storeId },
        };
    }

    private static Command Purchase(ulong clientSequence, uint slot)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = 12,
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = slot },
        };
    }

    private static Command CloseStore(ulong clientSequence, ulong requestedTick)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            CloseStoreRequested = new CloseStoreRequested(),
        };
    }
}
