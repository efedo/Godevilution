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
            startingInventoryGrid: [0, -1, 2]);

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
        Assert.Equal(new[] { 0, -1, 2 }, player.InventoryGrid);

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
    public void ServiceCommandsCanTargetBeltAndEquipmentSlots()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingBelt: [new BeltStoreItem(2, 101)],
            startingEquipment: [new EquippedStoreItem(3, 102)]);
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
        var catalog = CreateCatalog(new StoreItem(0, 42, 10), new StoreItem(1, 43, 10));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100, startingInventoryGrid: [0, 1, 1, -1]);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 0);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 1).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(3, 1), 2).Status);

        var sale = new Command {
            ClientSequence = 4,
            RequestedTick = 3,
            SellItemRequested = new SellItemRequested { InventoryIndex = 0 },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", sale, 3).Status);

        Assert.Equal(new[] { -1, 0, 0, -1 }, executor.GetPlayerState("player-a").InventoryGrid);
    }

    [Fact]
    public void ExplicitMoveTransfersInventoryAndBeltItemsAuthoritatively()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(new StoreItem(0, 42, 10)), startingGold: 100, startingInventoryGrid: [0]);
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
    public void ExplicitMoveSwapsInventoryAndEquipmentItemsAtomically()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10)),
            startingGold: 100,
            startingInventoryGrid: [0],
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
    public void InventoryPlacementHonorsMultiCellItemFootprints()
    {
        var itemState = AuthoritativeItemState.Empty with { ItemType = 1, InventoryWidth = 2, InventoryHeight = 2 };
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
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 8 },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", move, 13).Status);
        var grid = executor.GetPlayerState("player-a").InventoryGrid;
        Assert.Equal(0, grid[8]);
        Assert.Equal(0, grid[9]);
        Assert.Equal(0, grid[18]);
        Assert.Equal(0, grid[19]);
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
        Assert.Equal(528, restored.HitPointsQ6);
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
        var monster = Assert.Single(snapshot.Monsters);
        Assert.Equal(9U, monster.EntityId);
        Assert.Equal(0, monster.HitPoints);
        Assert.Equal(11, monster.MaxHitPoints);
        Assert.False(monster.Alive);

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
        Assert.Throws<InvalidDataException>(() => replacement.ImportPlayerSave("player-a", "{\"FormatVersion\":99}"));
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
}
