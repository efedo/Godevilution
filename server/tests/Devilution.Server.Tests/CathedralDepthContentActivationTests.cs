using Devilution.Data;
using Devilution.Diablo;
using Devilution.Domain.Inventory;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Server.Tests;

/** Synthetic catalogs test the production depth adapter, not native geometry or continuous GUI traversal. */
public sealed class CathedralDepthContentActivationTests
{
    [Theory]
    [InlineData(AuthoritativeGameMode.SinglePlayer)]
    [InlineData(AuthoritativeGameMode.Multiplayer)]
    public void EveryNativeStairBoundaryAndMiddleFloorColdRestoreUseTheDepthIdentity(AuthoritativeGameMode mode)
    {
        using var fixture = new Fixture();
        var activation = Activate(fixture.Content);
        Assert.Null(activation.Simulation.NativeGeneratedWorldIdentity);
        Assert.Equal(4, activation.Simulation.CathedralDepthWorldIdentity!.Levels.Count);
        var world = activation.Simulation.WorldState.World!;
        Assert.Equal(8, world.WarpTargets.Count);
        var host = CreateHost(fixture.Content, mode);
        host.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        host.CreateSnapshot("player", 1, 0);
        ulong tick = 0;
        ulong sequence = 0;
        foreach (var destination in new uint[] { 1, 2, 3, 4, 3, 2, 1, 0 }) {
            var source = host.GetPlayerState("player").LevelId;
            var warp = world.WarpTargets.Single(warp => warp.SourceLevelId == source && warp.TargetLevelId == destination);
            MoveOntoWarp(host, world, warp, ref tick, ref sequence);
            var arrived = host.GetPlayerState("player");
            Assert.Equal((destination, warp.TargetX, warp.TargetY), (arrived.LevelId, arrived.PositionX, arrived.PositionY));
            if (destination != 2 || source != 1)
                continue;
            var saved = host.ExportPlayerSave("player");
            var decoded = new AuthoritativeSaveCodec().Deserialize(saved);
            Assert.Equal(2U, decoded.Player.LevelId);
            Assert.Equal(4, decoded.CathedralDepthWorldIdentity!.Levels.Count);
            var replacement = CreateHost(fixture.Content, mode);
            replacement.ImportPlayerSave("player", saved);
            Assert.Equal(saved, replacement.ExportPlayerSave("player"));
            Assert.Equal(tick, replacement.GetSessionStateTick("player"));
            var geometry = replacement.CreateSnapshot("player", 1, tick).LevelGeometry;
            foreach (var depth in new uint[] { 1, 2, 3, 4 }) {
                var restored = Assert.Single(geometry, level => level.LevelId == depth);
                var original = world.Levels.Single(level => level.LevelId == depth);
                Assert.Equal(original.NativePieceLayer.Select(value => (uint)value), restored.NativePieceLayer);
                Assert.Equal(original.NativeTransparency.Select(value => (uint)value), restored.NativeTransparency);
                Assert.Equal(original.BlockedCells, restored.BlockedCells);
            }
            host = replacement;
            sequence = 0; // This bridge restores simulation state; the generic host owns connection ledgers.
        }
        Assert.Equal((0U, 25, 31), (host.GetPlayerState("player").LevelId,
            host.GetPlayerState("player").PositionX, host.GetPlayerState("player").PositionY));
        Assert.DoesNotContain(world.WarpTargets, warp => warp.SourceLevelId == 4 && warp.TargetLevelId == 5);
    }

    [Fact]
    public void AuthoredCathedralLootAndMutationsSurviveFreshHostRestoreAndTownReturn()
    {
        using var fixture = new Fixture(CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
            diagnosticDropItemSymbol: "short_sword_standard",
            townPortalRoute: true);
        Assert.Equal("authored-diagnostic", fixture.Profile.PlacementPolicy);
        Assert.Equal("short_sword_standard", fixture.Profile.DiagnosticDropItemSymbol);
        Assert.Equal(CathedralDepthPlaytestCompiler.TownPortalRouteMarker, fixture.Profile.TownPortalRoute);
        Assert.False(string.IsNullOrWhiteSpace(fixture.Profile.EffectiveInputManifestHash));
        var levelOne = Assert.Single(fixture.Profile.Levels, level => level.Depth == 1);
        Assert.True(levelOne.EncounterPosition.HasValue);
        Assert.True(levelOne.EncounterEntityId.HasValue);
        Assert.True(levelOne.ObjectPosition.HasValue);
        Assert.True(levelOne.ObjectEntityId.HasValue);
        var monster = fixture.Pack.Tables.Single(table => table.SourcePath == "monsters.tsv")
            .Rows.Single(row => row.RequiredUInt32("entity_id") == levelOne.EncounterEntityId.Value);
        Assert.Equal("short_sword_standard", monster.Values["drop_item_symbol"]);
        Assert.Equal(2160U, monster.RequiredUInt32("drop_item_id"));
        var dropEntityId = monster.RequiredUInt32("drop_item_entity_id");
        var dropSeed = monster.RequiredUInt32("drop_item_seed");
        var world = Activate(fixture.Content).Simulation.WorldState.World!;
        var host = CreateHost(fixture.Content);
        host.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        host.CreateSnapshot("player", 1, 0);
        var server = new AuthoritativeCommandServer(host);
        ulong tick = 0;
        ulong sequence = 0;

        var smithGreeting = fixture.Pack.Tables.Single(table => table.SourcePath == "towner_greetings.tsv")
            .Rows.Single(row => row.RequiredInt32("towner_id") == 0 && row.RequiredUInt32("level_id") == 0);
        WalkToAdjacent(host, server, world, 0,
            smithGreeting.RequiredInt32("position_x"), smithGreeting.RequiredInt32("position_y"),
            ref tick, ref sequence);
        var startingTown = host.CreateSnapshot("player", 1, tick).Players.Single();
        Assert.Equal(100U, startingTown.Gold);
        var portalStock = fixture.Pack.Tables.Single(table => table.SourcePath == "stores.tsv")
            .Rows.Single(row => row.Required("item_symbol") == "scroll_of_town_portal");
        var smithStoreId = portalStock.RequiredUInt32("store_id");
        var openedSmith = SendIntent(host, server, ref tick, ref sequence, new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = smithStoreId },
        });
        Assert.Equal(CommandStatus.Accepted, openedSmith.Status);
        var purchasedPortal = SendIntent(host, server, ref tick, ref sequence, new Command {
            PurchaseRequested = new PurchaseRequested {
                StoreId = smithStoreId,
                StoreSlot = portalStock.RequiredUInt32("store_slot"),
            },
        });
        Assert.Equal(CommandStatus.Accepted, purchasedPortal.Status);
        var afterPortalPurchase = host.CreateSnapshot("player", 1, tick).Players.Single();
        Assert.Equal(50U, afterPortalPurchase.Gold);
        var portalScroll = Assert.Single(afterPortalPurchase.Inventory,
            item => item.ItemSeed == portalStock.RequiredUInt32("item_seed"));
        Assert.Equal(2119U, portalScroll.State.StableItemId);
        Assert.Equal(39, portalScroll.State.SpellId);
        var closedSmith = SendIntent(host, server, ref tick, ref sequence, new Command {
            CloseStoreRequested = new CloseStoreRequested(),
        });
        Assert.Equal(CommandStatus.Accepted, closedSmith.Status);

        var entrance = Assert.Single(world.WarpTargets, warp => warp.SourceLevelId == 0 && warp.TargetLevelId == 1);
        WalkOntoWarp(host, server, world, entrance, ref tick, ref sequence);
        Assert.Equal(1U, host.GetPlayerState("player").LevelId);

        var beforeObject = host.CreateSnapshot("player", 1, tick);
        Assert.False(Assert.Single(beforeObject.Objects, item => item.EntityId == levelOne.ObjectEntityId.Value).Activated);
        Assert.Contains(beforeObject.Monsters, item => item.EntityId == levelOne.EncounterEntityId.Value && item.Alive);
        Assert.DoesNotContain(beforeObject.WorldItems, item => item.EntityId == dropEntityId);
        WalkToAdjacent(host, server, world, levelOne.LevelId,
            levelOne.ObjectPosition.Value.X, levelOne.ObjectPosition.Value.Y, ref tick, ref sequence);
        var activated = SendIntent(host, server, ref tick, ref sequence, new Command {
            OperateObjectRequested = new OperateObjectRequested { ObjectEntityId = levelOne.ObjectEntityId.Value },
        });
        Assert.Equal(CommandStatus.Accepted, activated.Status);
        Assert.True(Assert.Single(host.CreateSnapshot("player", 1, tick).Objects,
            item => item.EntityId == levelOne.ObjectEntityId.Value).Activated);

        WalkToAdjacent(host, server, world, levelOne.LevelId,
            levelOne.EncounterPosition.Value.X, levelOne.EncounterPosition.Value.Y, ref tick, ref sequence);
        var attackCount = 0;
        while (host.CreateSnapshot("player", 1, tick).Monsters.Any(
                   item => item.EntityId == levelOne.EncounterEntityId.Value && item.Alive)) {
            Assert.True(attackCount < 24, "The authored encounter did not die after 24 ordinary attack intents.");
            var attack = SendIntent(host, server, ref tick, ref sequence, new Command {
                AttackRequested = new AttackRequested { TargetEntityId = levelOne.EncounterEntityId.Value },
            }, settleTicks: 100);
            Assert.Equal(CommandStatus.Accepted, attack.Status);
            attackCount++;
        }

        var defeated = host.CreateSnapshot("player", 1, tick);
        Assert.DoesNotContain(defeated.Monsters,
            item => item.EntityId == levelOne.EncounterEntityId.Value && item.Alive);
        Assert.Contains(defeated.Corpses, corpse => corpse.EntityId == levelOne.EncounterEntityId.Value);
        var droppedSword = Assert.Single(defeated.WorldItems, item => item.EntityId == dropEntityId);
        Assert.Equal(dropSeed, droppedSword.ItemSeed);
        WalkToAdjacent(host, server, world, levelOne.LevelId,
            droppedSword.PositionX, droppedSword.PositionY, ref tick, ref sequence);
        var pickup = SendIntent(host, server, ref tick, ref sequence, new Command {
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = dropEntityId },
        });
        Assert.Equal(CommandStatus.Accepted, pickup.Status);
        var afterPickup = host.CreateSnapshot("player", 1, tick);
        Assert.DoesNotContain(afterPickup.WorldItems, item => item.EntityId == dropEntityId);
        var earnedItem = Assert.Single(afterPickup.Players.Single().Inventory,
            item => item.ItemSeed == dropSeed);
        Assert.Equal(2160U, earnedItem.State.StableItemId);
        Assert.Equal(1, earnedItem.State.EquipLocation);
        var pickupAttributes = afterPickup.Players.Single().Attributes;
        Assert.True(pickupAttributes.Strength.Current >= earnedItem.State.MinimumStrength);
        Assert.True(pickupAttributes.Magic.Current >= earnedItem.State.MinimumMagic);
        Assert.True(pickupAttributes.Dexterity.Current >= earnedItem.State.MinimumDexterity);
        Assert.True(earnedItem.State.StatFlag);
        var startingBow = Assert.Single(afterPickup.Players.Single().Equipment, item => item.Slot == 4);
        var inventoryCell = InventoryGridLayout.FindNativePlacement(afterPickup.Players.Single().InventoryGrid,
            checked((int)startingBow.State.InventoryWidth), checked((int)startingBow.State.InventoryHeight));
        Assert.True(inventoryCell >= 0, "The starting bow has no valid inventory cell for unequipping.");
        var unequip = SendIntent(host, server, ref tick, ref sequence, new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = startingBow.Slot,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = checked((uint)inventoryCell),
                },
            },
        });
        Assert.True(unequip.Status == CommandStatus.Accepted,
            $"Expected to move the starting bow into inventory, got {unequip.Status} ({unequip.RejectReason}).");
        var afterUnequip = host.CreateSnapshot("player", 1, tick).Players.Single();
        Assert.DoesNotContain(afterUnequip.Equipment,
            item => item.ItemSeed == startingBow.ItemSeed);
        Assert.Contains(afterUnequip.Inventory, item => item.ItemSeed == startingBow.ItemSeed);
        var equipInventory = afterUnequip.Inventory;
        var equipSwordIndex = Array.FindIndex(equipInventory.ToArray(), item => item.ItemSeed == dropSeed);
        Assert.True(equipSwordIndex >= 0, "The earned short sword was lost while unequipping the starting bow.");
        var equip = SendIntent(host, server, ref tick, ref sequence, new Command {
            AutoEquipItemRequested = new AutoEquipItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = checked((uint)equipSwordIndex),
                },
            },
        });
        Assert.True(equip.Status == CommandStatus.Accepted,
            $"Expected to equip the earned short sword, got {equip.Status} ({equip.RejectReason}).");
        Assert.Contains(host.CreateSnapshot("player", 1, tick).Players.Single().Equipment,
            item => item.ItemSeed == dropSeed);

        var down = Assert.Single(world.WarpTargets, warp => warp.SourceLevelId == 1 && warp.TargetLevelId == 2);
        WalkOntoWarp(host, server, world, down, ref tick, ref sequence);
        Assert.Equal(2U, host.GetPlayerState("player").LevelId);
        var saved = host.ExportPlayerSave("player");
        var decoded = new AuthoritativeSaveCodec().Deserialize(saved);
        Assert.Equal(2U, decoded.Player.LevelId);
        Assert.Contains(decoded.Corpses, corpse => corpse.EntityId == levelOne.EncounterEntityId.Value);
        Assert.Contains(decoded.Objects, item => item.EntityId == levelOne.ObjectEntityId.Value && item.Activated);

        var replacement = CreateHost(fixture.Content);
        replacement.ImportPlayerSave("player", saved);
        Assert.Equal(saved, replacement.ExportPlayerSave("player"));
        host = replacement;
        server = new AuthoritativeCommandServer(host);
        tick = host.GetSessionStateTick("player");
        sequence = 0;
        var restored = host.CreateSnapshot("player", 1, tick);
        Assert.Equal(2U, restored.Players.Single().LevelId);
        Assert.Contains(restored.Corpses, corpse => corpse.EntityId == levelOne.EncounterEntityId.Value);
        Assert.Contains(restored.Objects, item => item.EntityId == levelOne.ObjectEntityId.Value && item.Activated);
        Assert.DoesNotContain(restored.WorldItems, item => item.EntityId == dropEntityId);
        Assert.Contains(restored.Players.Single().Equipment, item => item.ItemSeed == dropSeed);
        Assert.Contains(restored.Players.Single().Inventory, item => item.ItemSeed == portalStock.RequiredUInt32("item_seed"));

        var returnToOne = Assert.Single(world.WarpTargets, warp => warp.SourceLevelId == 2 && warp.TargetLevelId == 1);
        WalkOntoWarp(host, server, world, returnToOne, ref tick, ref sequence);
        var revisited = host.CreateSnapshot("player", 1, tick);
        Assert.Equal(1U, revisited.Players.Single().LevelId);
        Assert.Contains(revisited.Corpses, corpse => corpse.EntityId == levelOne.EncounterEntityId.Value);
        Assert.Contains(revisited.Objects, item => item.EntityId == levelOne.ObjectEntityId.Value && item.Activated);
        Assert.DoesNotContain(revisited.Monsters, item => item.EntityId == levelOne.EncounterEntityId.Value && item.Alive);
        Assert.DoesNotContain(revisited.WorldItems, item => item.EntityId == dropEntityId);
        Assert.Contains(revisited.Players.Single().Equipment, item => item.ItemSeed == dropSeed);

        var portalPlayer = host.GetPlayerState("player");
        var portalScrollIndex = Array.FindIndex(portalPlayer.Inventory.ToArray(),
            item => item.ItemSeed == portalStock.RequiredUInt32("item_seed"));
        Assert.True(portalScrollIndex >= 0, "The purchased Town Portal scroll did not survive the depth restore.");
        var castPortal = SendIntent(host, server, ref tick, ref sequence, new Command {
            CastRequested = new CastRequested {
                SpellId = 39,
                Source = CastSourceKind.ConsumableItem,
                SourceItem = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = checked((uint)portalScrollIndex),
                    StableItemId = 2119,
                },
                TargetX = portalPlayer.PositionX,
                TargetY = portalPlayer.PositionY,
            },
        });
        Assert.True(castPortal.Status == CommandStatus.Accepted,
            $"Expected to cast the purchased Town Portal scroll, got {castPortal.Status} ({castPortal.RejectReason}).");
        var castAction = Assert.IsType<AuthoritativePlayerActionState>(host.GetPlayerState("player").CurrentAction);
        Assert.True(castAction.EndTick > tick);
        tick = castAction.EndTick;
        host.AdvanceTo(tick);
        var portal = Assert.Single(host.CreateSnapshot("player", 1, tick).Portals,
            item => item.OwnerEntityId == host.GetPlayerState("player").EntityId
                && item.SourceLevelId == 1 && item.DestinationLevelId == 0);
        Assert.DoesNotContain(host.GetPlayerState("player").Inventory,
            item => item.ItemSeed == portalStock.RequiredUInt32("item_seed"));

        var portalWorld = host.CreateSnapshot("player", 1, tick);
        var portalPath = FindWalkPath(world, portalWorld, portalWorld.Players.Single().EntityId, 1,
            portalWorld.Players.Single().PositionX, portalWorld.Players.Single().PositionY,
            (x, y) => x == portal.SourcePositionX && y == portal.SourcePositionY);
        foreach (var next in portalPath) {
            var player = host.GetPlayerState("player");
            var move = SendIntent(host, server, ref tick, ref sequence, new Command {
                MoveRequested = new MoveRequested {
                    DirectionX = next.X - player.PositionX,
                    DirectionY = next.Y - player.PositionY,
                },
            });
            Assert.Equal(CommandStatus.Accepted, move.Status);
            var action = Assert.IsType<AuthoritativePlayerActionState>(host.GetPlayerState("player").CurrentAction);
            Assert.True(action.EndTick > tick);
            tick = action.EndTick;
            host.AdvanceTo(tick);
        }
        var atPortal = host.GetPlayerState("player");
        Assert.Equal((portal.SourcePositionX, portal.SourcePositionY), (atPortal.PositionX, atPortal.PositionY));
        var portalReturn = SendIntent(host, server, ref tick, ref sequence, new Command {
            UsePortalRequested = new UsePortalRequested { PortalId = portal.PortalId },
        });
        Assert.True(portalReturn.Status == CommandStatus.Accepted,
            $"Expected to use the Town Portal, got {portalReturn.Status} ({portalReturn.RejectReason}).");
        var townReturn = host.CreateSnapshot("player", 1, tick);
        Assert.Equal(0U, townReturn.Players.Single().LevelId);
        Assert.Equal(portal.DestinationPositionX, townReturn.Players.Single().PositionX);
        Assert.Equal(portal.DestinationPositionY, townReturn.Players.Single().PositionY);

        var barmaidGreeting = fixture.Pack.Tables.Single(table => table.SourcePath == "towner_greetings.tsv")
            .Rows.Single(row => row.RequiredInt32("towner_id") == 7 && row.RequiredUInt32("level_id") == 0);
        WalkToAdjacent(host, server, world, 0,
            barmaidGreeting.RequiredInt32("position_x"), barmaidGreeting.RequiredInt32("position_y"),
            ref tick, ref sequence);
        var openStash = SendIntent(host, server, ref tick, ref sequence, new Command {
            OpenStashRequested = new OpenStashRequested(),
        });
        Assert.Equal(CommandStatus.Accepted, openStash.Status);
        var swordBeforeStash = host.GetPlayerState("player");
        var equippedBeforeStash = Assert.Single(swordBeforeStash.Equipment, item => item.ItemSeed == dropSeed);
        Assert.Equal(50U, swordBeforeStash.Gold);
        Assert.Equal(0U, swordBeforeStash.StashGold);
        var stashDeposit = SendIntent(host, server, ref tick, ref sequence, new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = equippedBeforeStash.Slot,
                    StableItemId = equippedBeforeStash.State.StableItemId,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = 0,
                    Slot = 0,
                    StableItemId = equippedBeforeStash.State.StableItemId,
                },
            },
        });
        Assert.Equal(CommandStatus.Accepted, stashDeposit.Status);
        var stashed = host.GetPlayerState("player");
        var stashedSword = Assert.Single(stashed.Stash, item => item.ItemSeed == dropSeed);
        Assert.Equal(swordBeforeStash.Gold, stashed.Gold);
        Assert.Equal(swordBeforeStash.StashGold, stashed.StashGold);
        Assert.Equal(equippedBeforeStash.State, stashedSword.State);
        Assert.DoesNotContain(stashed.Equipment, item => item.ItemSeed == dropSeed);

        var stashInventoryCell = InventoryGridLayout.FindNativePlacement(stashed.InventoryGrid,
            checked((int)stashedSword.State.InventoryWidth), checked((int)stashedSword.State.InventoryHeight));
        Assert.True(stashInventoryCell >= 0, "The stash cannot return the earned sword to the inventory.");
        var withdrawSword = SendIntent(host, server, ref tick, ref sequence, new Command {
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Stash,
                    Page = stashedSword.Page,
                    Slot = stashedSword.Cell,
                    StableItemId = stashedSword.State.StableItemId,
                },
                Destination = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = checked((uint)stashInventoryCell),
                    StableItemId = stashedSword.State.StableItemId,
                },
            },
        });
        Assert.Equal(CommandStatus.Accepted, withdrawSword.Status);
        var afterWithdraw = host.GetPlayerState("player");
        Assert.Equal(swordBeforeStash.Gold, afterWithdraw.Gold);
        Assert.Equal(swordBeforeStash.StashGold, afterWithdraw.StashGold);
        Assert.DoesNotContain(afterWithdraw.Stash, item => item.ItemSeed == dropSeed);
        var withdrawnItemIndex = Array.FindIndex(afterWithdraw.Inventory.ToArray(), item => item.ItemSeed == dropSeed);
        Assert.True(withdrawnItemIndex >= 0);
        var reEquipSword = SendIntent(host, server, ref tick, ref sequence, new Command {
            AutoEquipItemRequested = new AutoEquipItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = checked((uint)withdrawnItemIndex),
                    StableItemId = stashedSword.State.StableItemId,
                },
            },
        });
        Assert.Equal(CommandStatus.Accepted, reEquipSword.Status);
        var afterReEquip = host.GetPlayerState("player");
        Assert.Equal(swordBeforeStash.Gold, afterReEquip.Gold);
        Assert.Equal(swordBeforeStash.StashGold, afterReEquip.StashGold);
        var returnedSword = Assert.Single(afterReEquip.Equipment, item => item.ItemSeed == dropSeed);
        Assert.Equal(equippedBeforeStash.State, returnedSword.State);
        Assert.Equal(1, afterReEquip.Inventory.Count(item => item.ItemSeed == dropSeed)
            + afterReEquip.Equipment.Count(item => item.ItemSeed == dropSeed)
            + afterReEquip.Belt.Count(item => item.ItemSeed == dropSeed)
            + afterReEquip.Stash.Count(item => item.ItemSeed == dropSeed));
        Assert.Equal(CommandStatus.Accepted,
            SendIntent(host, server, ref tick, ref sequence, new Command {
                CloseStashRequested = new CloseStashRequested(),
            }).Status);

        var greeting = fixture.Pack.Tables.Single(table => table.SourcePath == "towner_greetings.tsv")
            .Rows.Single(row => row.RequiredInt32("towner_id") == 0 && row.RequiredUInt32("level_id") == 0);
        WalkToAdjacent(host, server, world, 0,
            greeting.RequiredInt32("position_x"), greeting.RequiredInt32("position_y"), ref tick, ref sequence);
        var smith = fixture.Pack.Tables.Single(table => table.SourcePath == "stores.tsv")
            .Rows.First(row => row.Values["store_role"] == "Smith");
        Assert.Equal(smithStoreId, smith.RequiredUInt32("store_id"));
        var openStore = SendIntent(host, server, ref tick, ref sequence, new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = smithStoreId },
        });
        Assert.Equal(CommandStatus.Accepted, openStore.Status);
        var beforeRepair = host.CreateSnapshot("player", 1, tick).Players.Single();
        Assert.Equal(smithStoreId, beforeRepair.ActiveStoreId);
        var equippedSword = Assert.Single(beforeRepair.Equipment, item => item.ItemSeed == dropSeed);
        Assert.True(equippedSword.State.MaxDurability > 0);
        Assert.Equal(equippedSword.State.MaxDurability, equippedSword.State.Durability);
        var repair = SendIntent(host, server, ref tick, ref sequence, new Command {
            RepairItemRequested = new RepairItemRequested {
                Item = new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = equippedSword.Slot,
                },
            },
        });
        Assert.Equal(CommandStatus.Rejected, repair.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, repair.RejectReason);
        var afterRepair = host.CreateSnapshot("player", 1, tick).Players.Single();
        var retainedSword = Assert.Single(afterRepair.Equipment, item => item.ItemSeed == dropSeed);
        Assert.Equal(beforeRepair.Gold, afterRepair.Gold);
        Assert.Equal(equippedSword.State.Durability, retainedSword.State.Durability);
        Assert.Equal(0U, afterRepair.LevelId);
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("attempt")]
    [InlineData("final-rng")]
    [InlineData("coarse")]
    [InlineData("fine")]
    [InlineData("level-routes")]
    [InlineData("catalog")]
    [InlineData("world-routes")]
    [InlineData("missing")]
    [InlineData("legacy-l1")]
    public void RejectsDifferentDepthIdentityBeforeMutatingTheLiveHost(string change)
    {
        using var fixture = new Fixture();
        var host = CreateHost(fixture.Content);
        host.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        host.CreateSnapshot("player", 1, 0);
        var before = host.ExportPlayerSave("player");
        var codec = new AuthoritativeSaveCodec();
        var snapshot = codec.Deserialize(before);
        var identity = snapshot.CathedralDepthWorldIdentity!;
        var levels = identity.Levels.ToArray();
        var changed = levels[2];
        levels[2] = change switch {
            "seed" => changed with { DungeonSeed = changed.DungeonSeed + 1 },
            "attempt" => changed with { AttemptSeed = changed.AttemptSeed + 1 },
            "final-rng" => changed with { FinalRandomState = changed.FinalRandomState + 1 },
            "coarse" => changed with { CoarseFingerprint = DifferentFingerprint(changed.CoarseFingerprint) },
            "fine" => changed with { FineFingerprint = DifferentFingerprint(changed.FineFingerprint) },
            "level-routes" => changed with { RouteFingerprint = DifferentFingerprint(changed.RouteFingerprint) },
            "catalog" => changed with { CatalogFingerprint = DifferentFingerprint(changed.CatalogFingerprint) },
            _ => changed,
        };
        var replacement = new DiabloCathedralDepthWorldIdentity(levels,
            change == "world-routes" ? DifferentFingerprint(identity.RouteFingerprint) : identity.RouteFingerprint);
        snapshot = snapshot with {
            CathedralDepthWorldIdentity = change is "missing" or "legacy-l1" ? null : replacement,
            NativeGeneratedWorldIdentity = change == "legacy-l1" ? LegacyIdentity() : null,
            Player = snapshot.Player with { Gold = snapshot.Player.Gold + 50 },
        };
        Assert.Throws<InvalidDataException>(() => host.ImportPlayerSave("player", codec.Serialize(snapshot)));
        Assert.Equal(before, host.ExportPlayerSave("player"));
        Assert.Equal(0UL, host.GetSessionStateTick("player"));
    }

    [Fact]
    public void AuthoredHostCannotRestoreADepthProfileAndLegacyActivationRejectsItsCatalog()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidDataException>(() => DiabloModule.Instance.ContentBootstrap.Activate(fixture.Content));
        Assert.Throws<InvalidDataException>(() => DiabloModule.Instance.NativeGeneratedContentBootstrap.ActivateGenerated(fixture.Content));
        var generated = CreateHost(fixture.Content);
        generated.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var baseContent = ComposedContent.Compose(new ContentManifest("base-test", "1", [LoadBasePack()]));
        var catalog = BuiltInAuthoritativeGameHostAdapters.Create();
        var registry = new GameplayModuleRegistry();
        registry.Load(catalog.CreateBuiltInModules(), new GameplayModuleContext(baseContent.Manifest, new DelegateAuthoritativeClock(() => 0)));
        var authored = Assert.IsType<StoreSimulationExecutor>(catalog.Activate(registry, baseContent,
            new AuthoritativePlayerStartConfiguration(null, null, null)));
        authored.InitializeParticipant("player", new AuthoritativeParticipantSelection(0));
        var before = authored.ExportPlayerSave("player");
        Assert.Throws<InvalidDataException>(() => authored.ImportPlayerSave("player", generated.ExportPlayerSave("player")));
        Assert.Equal(before, authored.ExportPlayerSave("player"));
    }

    [Fact]
    public void DepthSeedsDoNotConsumeOrDependOnGameplayRandomness()
    {
        using var fixture = new Fixture();
        var first = Activate(fixture.Content, 42);
        var second = Activate(fixture.Content, 99);
        Assert.Equal(first.Simulation.CathedralDepthWorldIdentity!.Levels, second.Simulation.CathedralDepthWorldIdentity!.Levels);
        var start = first.Simulation.WorldState.World!.GetStartPosition(0);
        var player = first.PlayerLoadouts.CreateTemplate("player", 0, null, null, null, 0, null, start.X, start.Y).CreatePlayer();
        Assert.NotEqual(first.Simulation.CaptureSnapshot(player, 0).RandomState, second.Simulation.CaptureSnapshot(player, 0).RandomState);
    }

    [Theory]
    [InlineData("native_cathedral_depths.tsv")]
    [InlineData("native_generated_route_bindings.tsv")]
    [InlineData("native_dun_piece_definitions.tsv")]
    [InlineData("native_dun_tile_properties.tsv")]
    public void MissingDepthInputsCannotFallBackToAuthoredGeometry(string path)
    {
        using var fixture = new Fixture();
        var pack = new ContentPack(fixture.Pack.Id, fixture.Pack.Version,
            fixture.Pack.Tables.Where(table => table.SourcePath != path).ToArray(), schema: fixture.Pack.Schema,
            assets: fixture.Pack.Assets);
        Assert.Throws<InvalidDataException>(() => Activate(ComposedContent.Compose(new ContentManifest(pack.Id, pack.Version, [pack]))));
    }

    private static void MoveOntoWarp(StoreSimulationExecutor host, AuthoritativeWorld world, AuthoritativeWarpTarget warp,
        ref ulong tick, ref ulong sequence)
    {
        var geometry = world.Levels.Single(level => level.LevelId == warp.SourceLevelId);
        var offsets = new[] { (X: 0, Y: 1), (X: 1, Y: 0), (X: -1, Y: 0), (X: 0, Y: -1) };
        var offset = offsets.First(offset => geometry.IsWalkable(warp.TriggerX - offset.X, warp.TriggerY - offset.Y)
            && !world.WarpTargets.Any(other => other.SourceLevelId == warp.SourceLevelId
                && other.TriggerX == warp.TriggerX - offset.X && other.TriggerY == warp.TriggerY - offset.Y));
        var codec = new AuthoritativeSaveCodec();
        var snapshot = codec.Deserialize(host.ExportPlayerSave("player"));
        // Synthetic boundary verification deliberately positions a valid server checkpoint;
        // the separate Godot acceptance proves continuous ordinary-control traversal.
        host.ImportPlayerSave("player", codec.Serialize(snapshot with {
            Player = snapshot.Player with { LevelId = warp.SourceLevelId,
                PositionX = warp.TriggerX - offset.X, PositionY = warp.TriggerY - offset.Y },
        }));
        var result = new AuthoritativeCommandServer(host).Process("player", new Command {
            ClientSequence = ++sequence, RequestedTick = tick + 1,
            MoveRequested = new MoveRequested { DirectionX = offset.X, DirectionY = offset.Y },
        }, tick + 1);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        tick += 100;
        host.AdvanceTo(tick);
    }

    private static CommandResult SendIntent(
        StoreSimulationExecutor host,
        AuthoritativeCommandServer server,
        ref ulong tick,
        ref ulong sequence,
        Command command,
        ulong settleTicks = 0)
    {
        var requestedTick = checked(tick + 1);
        command.ClientSequence = checked(++sequence);
        command.RequestedTick = requestedTick;
        var result = server.Process("player", command, requestedTick);
        tick = checked(requestedTick + settleTicks);
        host.AdvanceTo(tick);
        return result;
    }

    private static void WalkOntoWarp(
        StoreSimulationExecutor host,
        AuthoritativeCommandServer server,
        AuthoritativeWorld world,
        AuthoritativeWarpTarget warp,
        ref ulong tick,
        ref ulong sequence)
    {
        WalkToAdjacent(host, server, world, warp.SourceLevelId, warp.TriggerX, warp.TriggerY,
            ref tick, ref sequence);
        var player = host.GetPlayerState("player");
        var directionX = warp.TriggerX - player.PositionX;
        var directionY = warp.TriggerY - player.PositionY;
        Assert.InRange(directionX, -1, 1);
        Assert.InRange(directionY, -1, 1);
        Assert.NotEqual((0, 0), (directionX, directionY));
        var result = SendIntent(host, server, ref tick, ref sequence, new Command {
            MoveRequested = new MoveRequested { DirectionX = directionX, DirectionY = directionY },
        }, settleTicks: 100);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        var arrived = host.GetPlayerState("player");
        Assert.Equal((warp.TargetLevelId, warp.TargetX, warp.TargetY),
            (arrived.LevelId, arrived.PositionX, arrived.PositionY));
    }

    private static void WalkToAdjacent(
        StoreSimulationExecutor host,
        AuthoritativeCommandServer server,
        AuthoritativeWorld world,
        uint levelId,
        int targetX,
        int targetY,
        ref ulong tick,
        ref ulong sequence)
    {
        var player = host.GetPlayerState("player");
        Assert.Equal(levelId, player.LevelId);
        var snapshot = host.CreateSnapshot("player", 1, tick);
        var path = FindWalkPath(world, snapshot, player.EntityId, levelId,
            player.PositionX, player.PositionY,
            (x, y) => Math.Max(Math.Abs(targetX - x), Math.Abs(targetY - y)) <= 1);
        foreach (var next in path) {
            player = host.GetPlayerState("player");
            var directionX = next.X - player.PositionX;
            var directionY = next.Y - player.PositionY;
            Assert.InRange(directionX, -1, 1);
            Assert.InRange(directionY, -1, 1);
            Assert.NotEqual((0, 0), (directionX, directionY));
            var result = SendIntent(host, server, ref tick, ref sequence, new Command {
                MoveRequested = new MoveRequested { DirectionX = directionX, DirectionY = directionY },
            }, settleTicks: 100);
            Assert.Equal(CommandStatus.Accepted, result.Status);
            var arrived = host.GetPlayerState("player");
            Assert.Equal((levelId, next.X, next.Y), (arrived.LevelId, arrived.PositionX, arrived.PositionY));
        }
        player = host.GetPlayerState("player");
        Assert.InRange(Math.Max(Math.Abs(targetX - player.PositionX), Math.Abs(targetY - player.PositionY)), 0, 1);
    }

    private static IReadOnlyList<(int X, int Y)> FindWalkPath(
        AuthoritativeWorld world,
        Snapshot snapshot,
        uint playerEntityId,
        uint levelId,
        int startX,
        int startY,
        Func<int, int, bool> isGoal)
    {
        var level = world.Levels.Single(candidate => candidate.LevelId == levelId);
        var blocked = snapshot.Monsters
            .Where(monster => monster.LevelId == levelId && monster.Alive)
            .Select(monster => (monster.PositionX, monster.PositionY))
            .Concat(snapshot.Objects
                .Where(@object => @object.LevelId == levelId && !@object.Activated && !@object.Destroyed)
                .Select(@object => (@object.PositionX, @object.PositionY)))
            .Concat(snapshot.Corpses
                .Where(corpse => corpse.LevelId == levelId)
                .Select(corpse => (corpse.PositionX, corpse.PositionY)))
            .Concat(snapshot.Players
                .Where(other => other.EntityId != playerEntityId && other.LevelId == levelId)
                .Select(other => (other.PositionX, other.PositionY)))
            .ToHashSet();
        var warpTriggers = world.WarpTargets
            .Where(warp => warp.SourceLevelId == levelId)
            .Select(warp => (warp.TriggerX, warp.TriggerY))
            .ToHashSet();
        var start = (X: startX, Y: startY);
        if (isGoal(startX, startY))
            return Array.Empty<(int X, int Y)>();

        var parents = new Dictionary<(int X, int Y), (int X, int Y)>();
        var visited = new HashSet<(int X, int Y)> { start };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);
        (int X, int Y)? destination = null;
        var directions = new[] {
            (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0),
            (X: 1, Y: -1), (X: 1, Y: 1), (X: -1, Y: 1), (X: -1, Y: -1),
        };
        while (queue.Count > 0 && !destination.HasValue) {
            var current = queue.Dequeue();
            foreach (var direction in directions) {
                var next = (X: current.X + direction.X, Y: current.Y + direction.Y);
                if (!level.IsWalkable(next.X, next.Y)
                    || blocked.Contains(next)
                    || warpTriggers.Contains(next)
                    || !visited.Add(next)) {
                    continue;
                }
                parents[next] = current;
                if (isGoal(next.X, next.Y)) {
                    destination = next;
                    break;
                }
                queue.Enqueue(next);
            }
        }

        Assert.True(destination.HasValue,
            $"No walkable path satisfies the requested destination on level {levelId}.");
        var path = new List<(int X, int Y)>();
        var cursor = destination.GetValueOrDefault();
        while (cursor != start) {
            path.Add(cursor);
            cursor = parents[cursor];
        }
        path.Reverse();
        return path;
    }

    private static DiabloContentActivation Activate(ComposedContent content, uint randomSeed = 42)
        => DiabloModule.Instance.CathedralDepthContentBootstrap.ActivateDepths(content, randomSeed);

    private static StoreSimulationExecutor CreateHost(ComposedContent content, AuthoritativeGameMode mode = AuthoritativeGameMode.SinglePlayer)
    {
        var catalog = BuiltInAuthoritativeGameHostAdapters.Create(DiabloAuthoritativeGameHostAdapterFactory.CathedralDepthId);
        var registry = new GameplayModuleRegistry();
        registry.Load(catalog.CreateBuiltInModules(), new GameplayModuleContext(content.Manifest, new DelegateAuthoritativeClock(() => 0)));
        return Assert.IsType<StoreSimulationExecutor>(catalog.Activate(registry, content,
            new AuthoritativePlayerStartConfiguration(null, null, null) { StartingLevelId = 0, RandomSeed = 42, GameMode = mode }));
    }

    private static string DifferentFingerprint(string fingerprint)
        => fingerprint == new string('A', 64) ? new string('B', 64) : new string('A', 64);

    private static DiabloNativeGeneratedWorldIdentity LegacyIdentity()
        => new([new(AuthoritativeCathedralLevel1RuntimeCatalog.CatalogId,
            AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId, 1, 1, "cathedral", 17,
            AuthoritativeCathedralEntry.Main, 17, 0, new string('A', 64), new string('B', 64),
            new string('C', 64), new string('D', 64))], new string('E', 64));

    private static ContentPack LoadBasePack()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return ContentPackLoader.LoadManifestDirectory(Path.Combine(directory.FullName, "server", "content", "base"));
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static (ContentPack TownLoopPreview, ContentPack TownPortalPreview) LoadTownPreviewPacks()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) {
                var contentRoot = Path.Combine(directory.FullName, "server", "content");
                return (
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "town-loop-preview")),
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "town-portal-preview")));
            }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string output = Path.Combine(Path.GetTempPath(), "godevilution-depth-host-" + Guid.NewGuid().ToString("N"));
        public ContentPack Pack { get; }
        public ComposedContent Content { get; }

        public CathedralDepthPlaytestProfile Profile { get; }

        public Fixture(
            CathedralDepthPlaytestPlacementPolicy placementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
            string? diagnosticDropItemSymbol = null,
            bool townPortalRoute = false)
        {
            var pieces = "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n"
                + string.Join('\n', Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
                    .Select(id => $"{id}\t{(id == 63 ? 128 : id == 58 ? 114 : id == 21 ? 1 : 2)}\t2\t2\t2")) + "\n";
            var properties = "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap\n"
                + string.Join('\n', Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
                    .Select(id => $"{id}\t{id == 1}\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse")) + "\n";
            ContentPack? townLoopPreviewPack = null;
            ContentPack? townPortalPreviewPack = null;
            if (townPortalRoute)
                (townLoopPreviewPack, townPortalPreviewPack) = LoadTownPreviewPacks();
            Profile = CathedralDepthPlaytestCompiler.Compile(new CathedralDepthPlaytestCompilerOptions(
                LoadBasePack(), output, pieces, properties, [17, 17, 17, 17]) {
                PlacementPolicy = placementPolicy,
                DiagnosticDropItemSymbol = diagnosticDropItemSymbol,
                TownPortalRoute = townPortalRoute ? CathedralDepthPlaytestCompiler.TownPortalRouteMarker : null,
                TownLoopPreviewPack = townLoopPreviewPack,
                TownPortalPreviewPack = townPortalPreviewPack,
            });
            Pack = ContentPackLoader.LoadManifestDirectory(Path.Combine(output, "content"));
            Content = ComposedContent.Compose(new ContentManifest(Pack.Id, Pack.Version, [Pack]));
        }

        public void Dispose()
        {
            var target = Path.GetFullPath(output);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(target).StartsWith("godevilution-depth-host-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe depth host fixture cleanup target.");
            if (Directory.Exists(target))
                Directory.Delete(target, true);
        }
    }
}
