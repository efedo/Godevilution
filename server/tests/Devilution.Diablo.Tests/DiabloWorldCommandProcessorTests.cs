using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloWorldCommandProcessorTests
{
    [Fact]
    public void FriendlyModeIntentSetsExplicitRetrySafeState()
    {
        var processor = new DiabloWorldCommandProcessor(new DiabloWorldState());
        var player = CreatePlayer();

        Assert.True(processor.Execute(
            player,
            new DiabloSetFriendlyModeIntent(false),
            1,
            [player]).Succeeded);
        Assert.False(player.FriendlyMode);

        Assert.True(processor.Execute(
            player,
            new DiabloSetFriendlyModeIntent(false),
            2,
            [player]).Succeeded);
        Assert.False(player.FriendlyMode);
    }

    [Fact]
    public void MovementRejectsMalformedBlockedAndOccupiedDestinationsWithoutMutation()
    {
        var world = new DiabloWorldState(
            fallbackWidth: 3,
            fallbackHeight: 3,
            fallbackBlockedCells: [1]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);
        var otherPlayer = CreatePlayer(positionY: 1, entityId: 2);

        AssertRejected(
            processor.Execute(player, new DiabloMoveIntent(2, 0), 1, [player, otherPlayer]),
            DiabloRuleFailure.Malformed);
        AssertRejected(
            processor.Execute(player, new DiabloMoveIntent(1, 0), 1, [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);
        AssertRejected(
            processor.Execute(player, new DiabloMoveIntent(0, 1), 1, [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);

        Assert.Equal(0, player.PositionX);
        Assert.Equal(0, player.PositionY);
        Assert.Equal(DiabloPlayerDirection.South, player.FacingDirection);
    }

    [Fact]
    public void MovementRejectsLiveMonsterOccupancy()
    {
        var monster = new AuthoritativeCombatTarget(10, 1, 0, 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var player = CreatePlayer(entityId: 1);
        var processor = new DiabloWorldCommandProcessor(world);

        var result = processor.Execute(player, new DiabloMoveIntent(1, 0), 1, [player]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0, player.PositionX);
    }

    [Fact]
    public void PortalRequiresExactSourceAndUnoccupiedDestination()
    {
        var portal = new AuthoritativePortal(5, 0, 0, 0, 2, 3, 4);
        var world = new DiabloWorldState(portals: [portal]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionY: 1, entityId: 1);
        var otherPlayer = CreatePlayer(positionX: 3, positionY: 4, levelId: 2, entityId: 2);

        AssertRejected(
            processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player, otherPlayer]),
            DiabloRuleFailure.InvalidTarget);

        player.PositionY = 0;
        AssertRejected(
            processor.Execute(player, new DiabloUsePortalIntent(5), 2, [player, otherPlayer]),
            DiabloRuleFailure.NotAllowed);

        otherPlayer.PositionX = 2;
        player.StatusEffects.Add(new AuthoritativeStatusEffect(7, 30, 2));
        player.StatusEffects.Add(new AuthoritativeStatusEffect(9, 40, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Infravision,
        });
        player.StatusEffects.Add(new AuthoritativeStatusEffect(46, 50, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Search,
        });
        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 3, [player, otherPlayer]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(3, player.PositionX);
        Assert.Equal(4, player.PositionY);
        var retainedStatus = Assert.Single(player.StatusEffects);
        Assert.Equal(7U, retainedStatus.EffectId);
    }

    [Fact]
    public void TownPortalUsesTheValidatedPlacementRadiusAndLifetime()
    {
        var world = new DiabloWorldState(fallbackWidth: 9, fallbackHeight: 9);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 2, positionY: 2, levelId: 1, entityId: 1);
        var blockedPositions = new[] {
            (4, 4), (3, 3), (4, 3), (5, 3),
            (3, 4),           (5, 4),
            (3, 5), (4, 5), (5, 5),
        };
        var blockers = blockedPositions
            .Select((position, index) => CreatePlayer(
                positionX: position.Item1,
                positionY: position.Item2,
                levelId: 1,
                entityId: (uint)(10 + index)))
            .ToArray();
        var participants = new[] { player }.Concat(blockers).ToArray();

        var tooSmall = processor.ExecuteTownPortal(
            player,
            4,
            4,
            1,
            participants,
            searchRadius: 1,
            lifetime: 7);

        Assert.True(tooSmall.Succeeded);
        Assert.Empty(world.Portals);

        var sufficient = processor.ExecuteTownPortal(
            player,
            4,
            4,
            1,
            participants,
            searchRadius: 2,
            lifetime: 7);

        Assert.True(sufficient.Succeeded);
        var portal = Assert.Single(world.Portals.Values);
        Assert.Equal(7U, portal.RemainingTicks);
        Assert.Equal(2, Math.Max(
            Math.Abs(portal.SourcePositionX - 4),
            Math.Abs(portal.SourcePositionY - 4)));

        var malformed = processor.ExecuteTownPortal(
            player,
            4,
            4,
            1,
            participants,
            destinationPositionX: -1);

        Assert.False(malformed.Succeeded);
        Assert.Equal(DiabloRuleFailure.Malformed, malformed.Failure);
    }

    [Fact]
    public void TownPortalUsesTheExternalTownLevelIdentity()
    {
        var world = new DiabloWorldState(fallbackWidth: 80, fallbackHeight: 80);
        var combatRules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            townLevelId: 5);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: combatRules);
        var player = CreatePlayer(positionX: 10, positionY: 10, levelId: 1, entityId: 1);

        var created = processor.ExecuteTownPortal(
            player,
            12,
            10,
            1,
            [player],
            searchRadius: 2,
            lifetime: 7);

        Assert.True(created.Succeeded);
        var portal = Assert.Single(world.Portals.Values);
        Assert.Equal(5U, portal.DestinationLevelId);

        player.LevelId = 5;
        var rejected = processor.ExecuteTownPortal(
            player,
            12,
            10,
            2,
            [player]);

        AssertRejected(rejected, DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void EnteringQuestRouteLevelActivatesInitialQuestFromExternalMetadata()
    {
        var portal = new AuthoritativePortal(5, 1, 0, 0, 2, 3, 4);
        var quest = new AuthoritativeQuestState(3001, 2, 1, active: false);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3001, 0, "The Magic Rock", 2, -1, string.Empty, 0, true),
        ]);
        var world = new DiabloWorldState(portals: [portal], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);

        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.True(quest.Active);
    }

    [Fact]
    public void MultiplayerRouteDoesNotActivateSinglePlayerOnlyQuest()
    {
        var portal = new AuthoritativePortal(5, 1, 0, 0, 2, 3, 4);
        var quest = new AuthoritativeQuestState(3001, 2, 1, active: false);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3001, 0, "The Magic Rock", 2, 2, string.Empty, 0, true),
        ]);
        var world = new DiabloWorldState(portals: [portal], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            questTypes: questTypes,
            gameMode: DiabloGameMode.Multiplayer);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);

        var result = processor.Execute(player, new DiabloUsePortalIntent(5), 1, [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.False(quest.Active);
    }

    [Fact]
    public void WarpSelectsNearestTriggerAndCrawlsAroundOccupiedDestination()
    {
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(new AuthoritativeLevel(1, 5, 5));
        authoritativeWorld.AddLevel(new AuthoritativeLevel(2, 5, 5));
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(7, 1, 1, 1, 2, 2, 2));
        var blocker = CreatePlayer(positionX: 2, positionY: 2, levelId: 2, entityId: 2);
        var world = new DiabloWorldState(authoritativeWorld);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);
        player.StatusEffects.Add(new AuthoritativeStatusEffect(9, 20, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Infravision,
        });
        player.StatusEffects.Add(new AuthoritativeStatusEffect(46, 30, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Search,
        });

        var result = processor.ExecuteWarp(player, 12, [player, blocker]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.NotEqual((2, 2), (player.PositionX, player.PositionY));
        Assert.True(authoritativeWorld.IsWalkable(player.LevelId, player.PositionX, player.PositionY));
        Assert.Empty(player.StatusEffects);
    }

    [Fact]
    public void WarpUsesTheValidatedDestinationSearchRadius()
    {
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(new AuthoritativeLevel(1, 7, 7));
        authoritativeWorld.AddLevel(new AuthoritativeLevel(2, 7, 7));
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(7, 1, 1, 1, 2, 3, 3));
        var world = new DiabloWorldState(authoritativeWorld);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 0, positionY: 0, levelId: 1, entityId: 1);
        var blockedPositions = new[] {
            (3, 3), (2, 2), (3, 2), (4, 2),
            (2, 3),           (4, 3),
            (2, 4), (3, 4), (4, 4),
        };
        var blockers = blockedPositions
            .Select((position, index) => CreatePlayer(
                positionX: position.Item1,
                positionY: position.Item2,
                levelId: 2,
                entityId: (uint)(10 + index)))
            .ToArray();
        var participants = new[] { player }.Concat(blockers).ToArray();

        var tooSmall = processor.ExecuteWarp(
            player,
            12,
            participants,
            searchRadius: 1);

        AssertRejected(tooSmall, DiabloRuleFailure.NotAllowed);
        Assert.Equal(1U, player.LevelId);

        var sufficient = processor.ExecuteWarp(
            player,
            12,
            participants,
            searchRadius: 2);

        Assert.True(sufficient.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(2, Math.Max(
            Math.Abs(player.PositionX - 3),
            Math.Abs(player.PositionY - 3)));
    }

    [Fact]
    public void QuestGatedWarpRequiresCompletionBeforeLeavingTown()
    {
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(new AuthoritativeLevel(0, 40, 40));
        authoritativeWorld.AddLevel(new AuthoritativeLevel(17, 40, 40));
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(
            2,
            0,
            36,
            22,
            17,
            36,
            24,
            requiredQuestId: 3017,
            requiredQuestCompleted: true));
        var quest = new AuthoritativeQuestState(3017, 17, 1, active: true);
        var world = new DiabloWorldState(authoritativeWorld, quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(positionX: 36, positionY: 22, levelId: 0, entityId: 1);

        var spellWarp = processor.ExecuteWarp(player, 12, [player]);
        AssertRejected(spellWarp, DiabloRuleFailure.NotAllowed);

        var locked = processor.ExecuteLevelTrigger(player, 12, [player]);

        AssertRejected(locked, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0U, player.LevelId);

        quest.Completed = true;
        quest.Active = false;
        player.PositionX = 35;
        var opened = processor.Execute(
            player,
            new DiabloMoveIntent(1, 0),
            12,
            [player]);

        Assert.True(opened.Succeeded);
        Assert.Equal(17U, player.LevelId);
        Assert.Equal((36, 24), (player.PositionX, player.PositionY));
    }

    [Fact]
    public void PickupPlacesMultiCellItemAndRemovesItFromWorld()
    {
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            InventoryWidth = 2,
            InventoryHeight = 1,
        };
        var item = new AuthoritativeWorldItem(20, 0, 1, 0, 42, 75, itemState);
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        var ownedItem = Assert.Single(player.Inventory);
        Assert.Equal(42U, ownedItem.ItemSeed);
        Assert.Equal(27UL, ownedItem.PurchasedAtTick);
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Equal(0, player.InventoryGrid[1]);
        Assert.DoesNotContain(20U, world.WorldItems.Keys);
    }

    [Fact]
    public void PickupGoldBypassesInventorySpaceAndRestoresServerOwnedBalance()
    {
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = 11,
                ItemIndex = 0,
                Value = 75,
                IdentifiedValue = 75,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(
            entityId: 1,
            inventoryGrid: Enumerable.Repeat(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(175U, player.Gold);
        Assert.Empty(player.Inventory);
        Assert.DoesNotContain(20U, world.WorldItems.Keys);
    }

    [Fact]
    public void PickupGoldUsesNativeTopologyAndPlacementOrderWhenStacksArePresent()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var worldGold = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = rules.DeathGoldItemType,
                ItemIndex = rules.DeathGoldItemIndex,
                Value = 75,
                IdentifiedValue = 75,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [worldGold]);
        var random = new DiabloRandom(0);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            random: random);
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var fullStack = new OwnedStoreItem(
            0,
            0,
            123,
            5000,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = rules.DeathGoldItemType,
                ItemIndex = rules.DeathGoldItemIndex,
                Value = 5000,
                IdentifiedValue = 5000,
                StatFlag = true,
            });
        var player = CreatePlayer(
            entityId: 1,
            gold: 5000,
            inventory: [fullStack],
            inventoryGrid: inventoryGrid);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(5075U, player.Gold);
        Assert.Equal([5000, 75], player.Inventory.Select(item => item.State.Value).ToArray());
        Assert.Equal(1U, player.Inventory[1].ItemSeed);
        Assert.Equal(1, player.InventoryGrid[39]);
        Assert.Equal(1U, random.State);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void TopologyAwareGoldPickupMovesFullOverflowToHeldItem()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var worldGold = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = rules.DeathGoldItemType,
                ItemIndex = rules.DeathGoldItemIndex,
                Value = 75,
                IdentifiedValue = 75,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [worldGold]);
        var random = new DiabloRandom(0);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            random: random);
        var inventory = Enumerable.Range(0, 40)
            .Select(index => index == 0
                ? new OwnedStoreItem(
                    0,
                    0,
                    123,
                    5000,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = rules.DeathGoldItemType,
                        ItemIndex = rules.DeathGoldItemIndex,
                        Value = 5000,
                        IdentifiedValue = 5000,
                        StatFlag = true,
                    })
                : new OwnedStoreItem(
                    0,
                    0,
                    checked((uint)(123 + index)),
                    1,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = 1,
                        StatFlag = true,
                    }))
            .ToArray();
        var player = CreatePlayer(
            entityId: 1,
            gold: 5000,
            inventory: inventory,
            inventoryGrid: Enumerable.Range(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(5000U, player.Gold);
        Assert.Equal(5000, player.Inventory[0].State.Value);
        Assert.Equal(0U, random.State);
        Assert.Empty(world.WorldItems);
        Assert.Equal(42U, player.HeldItem?.ItemSeed);
        Assert.Equal(75U, player.HeldItem?.Price);
        Assert.Equal(75, player.HeldItem?.State.Value);
    }

    [Fact]
    public void TopologyAwareGoldPickupPartiallyFillsInventoryAndHoldsRemainder()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var worldGold = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            CreateGoldState(75, rules));
        var fillerState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            StatFlag = true,
        };
        var inventory = new List<OwnedStoreItem> {
            CreateGoldStack(123, 4990, rules),
        };
        for (var index = 1; index < 40; index++) {
            inventory.Add(new OwnedStoreItem(
                0,
                0,
                checked((uint)(200 + index)),
                1,
                0,
                fillerState));
        }
        var world = new DiabloWorldState(worldItems: [worldGold]);
        var random = new DiabloRandom(0);
        var processor = new DiabloWorldCommandProcessor(
            world,
            combatRules: rules,
            random: random);
        var player = CreatePlayer(
            entityId: 1,
            gold: 4990,
            inventory: inventory,
            inventoryGrid: Enumerable.Range(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(5000U, player.Gold);
        Assert.Equal(5000U, player.Inventory[0].Price);
        Assert.Equal(42U, player.HeldItem?.ItemSeed);
        Assert.Equal(65U, player.HeldItem?.Price);
        Assert.Equal(27UL, player.HeldItem?.PurchasedAtTick);
        Assert.Equal(0U, random.State);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void HeldItemDropValidatesTargetAndReturnsItemToWorld()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var heldItem = CreateGoldStack(42, 65, rules) with { PurchasedAtTick = 27 };
        var world = new DiabloWorldState();
        var processor = new DiabloWorldCommandProcessor(world, combatRules: rules);
        var player = CreatePlayer(entityId: 1, gold: 0, heldItem: heldItem);

        var rejected = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(2, 0),
            28,
            [player]);

        AssertRejected(rejected, DiabloRuleFailure.NotAllowed);
        Assert.Same(heldItem, player.HeldItem);
        Assert.Empty(world.WorldItems);

        var accepted = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(1, 0),
            29,
            [player]);

        Assert.True(accepted.Succeeded);
        Assert.Null(player.HeldItem);
        var dropped = Assert.Single(world.WorldItems).Value;
        Assert.Equal(0x40000000U, dropped.EntityId);
        Assert.Equal(42U, dropped.ItemSeed);
        Assert.Equal(65U, dropped.Price);
        Assert.Equal((1, 0), (dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void HeldItemDropUsesTheReservedMovementDestination()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var heldItem = CreateGoldStack(42, 65, rules);
        var world = new DiabloWorldState();
        var processor = new DiabloWorldCommandProcessor(world, combatRules: rules);
        var player = CreatePlayer(entityId: 1, gold: 0, heldItem: heldItem);
        player.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Move,
            1,
            2,
            2,
            2) {
            DirectionX = 1,
        };

        var result = processor.Execute(
            player,
            new DiabloDropHeldItemIntent(2, 0),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Null(player.HeldItem);
        var dropped = Assert.Single(world.WorldItems).Value;
        Assert.Equal((2, 0), (dropped.PositionX, dropped.PositionY));
    }

    [Fact]
    public void MalformedGoldPickupDoesNotMutateBalanceOrWorld()
    {
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with {
                ItemType = 11,
                ItemIndex = 0,
                Value = 75,
                IdentifiedValue = 74,
                StatFlag = true,
            });
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        AssertRejected(result, DiabloRuleFailure.Malformed);
        Assert.Equal(100U, player.Gold);
        Assert.Empty(player.Inventory);
        Assert.Same(item, world.WorldItems[20]);
    }

    [Fact]
    public void RejectedPickupLeavesWorldAndInventoryUnchanged()
    {
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty with { ItemType = 1 });
        var world = new DiabloWorldState(worldItems: [item]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1, inventoryGrid: Enumerable.Repeat(0, 40).ToArray());

        var result = processor.Execute(
            player,
            new DiabloPickupWorldItemIntent(20),
            27,
            [player]);

        AssertRejected(result, DiabloRuleFailure.NotAllowed);
        Assert.Empty(player.Inventory);
        Assert.Same(item, world.WorldItems[20]);
    }

    [Fact]
    public void ObjectAppliesLinkedQuestAndEmitsTransportNeutralEvent()
    {
        var quest = new AuthoritativeQuestState(30, 0, 1);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            questId: 30,
            effectKind: AuthoritativeObjectEffectKind.Heal,
            effectAmount: 25);
        var world = new DiabloWorldState(objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(life: 30, lifeMaximum: 50, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.True(quest.Completed);
        Assert.Equal(50, player.Life);
        var gameplayEvent = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Healing, gameplayEvent.Kind);
        Assert.Equal(20U, gameplayEvent.SourceEntityId);
        Assert.Equal(7U, gameplayEvent.TargetEntityId);
        Assert.Equal(20, gameplayEvent.Amount);
    }

    [Fact]
    public void ArmedTrapUsesNativeHalfTrapDamageEquipmentAndPreservesQ6EventAmount()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.Damage,
            effectAmount: 64,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(
            life: 100,
            lifeMaximum: 100,
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag,
                })]);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(68, player.Life);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 20, 7, 0) {
                AmountQ6 = 32,
            },
            Assert.Single(result.Events));
    }

    [Fact]
    public void LinkedObjectAppliesConfiguredQuestProgressAndClampsAtRequiredAmount()
    {
        var quest = new AuthoritativeQuestState(30, 0, 5, progress: 4);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            questId: 30,
            questProgressAmount: 3);
        var world = new DiabloWorldState(objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]).Succeeded);

        Assert.Equal(5U, quest.Progress);
        Assert.True(quest.Completed);
    }

    [Fact]
    public void QuestSequenceObjectsRequireNativeOrderAndPersistCompletion()
    {
        var quest = new AuthoritativeQuestState(
            3022,
            0,
            1,
            completionVariable1: 3,
            completionVariable2: 4,
            completionActivateQuestId: 3023);
        var nextQuest = new AuthoritativeQuestState(3023, 0, 1, active: false);
        var objects = new[] {
            new AuthoritativeWorldObject(
                2401,
                6001,
                0,
                1,
                0,
                questId: 3022,
                effectKind: AuthoritativeObjectEffectKind.QuestSequence,
                effectAmount: 1),
            new AuthoritativeWorldObject(
                2402,
                6002,
                0,
                1,
                0,
                questId: 3022,
                effectKind: AuthoritativeObjectEffectKind.QuestSequence,
                effectAmount: 2),
            new AuthoritativeWorldObject(
                2403,
                6003,
                0,
                1,
                0,
                questId: 3022,
                effectKind: AuthoritativeObjectEffectKind.QuestSequence,
                effectAmount: 3),
        };
        var world = new DiabloWorldState(objects: objects, quests: [quest, nextQuest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer();

        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2402),
            1,
            [player]).Succeeded);
        Assert.Equal(0, quest.Variable1);
        Assert.False(quest.Completed);

        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2401),
            2,
            [player]).Succeeded);
        Assert.Equal(1, quest.Variable1);
        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2402),
            3,
            [player]).Succeeded);
        Assert.Equal(2, quest.Variable1);
        Assert.True(processor.Execute(
            player,
            new DiabloOperateObjectIntent(2403),
            4,
            [player]).Succeeded);

        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, quest.Progress);
        Assert.Equal(3, quest.Variable1);
        Assert.Equal(4, quest.Variable2);
        Assert.True(nextQuest.Active);
        Assert.All(objects, @object => Assert.False(@object.Activated));
    }

    [Fact]
    public void QuestDialogueValidatesUniqueIdentityActivatesQuestAndEmitsMessage()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            active: false,
            messageId: 77,
            completionUniqueIdentity: "Gharbad",
            dialogueVariable1: 1,
            dialogueVariable2: 2);
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Gharbad");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3003, 20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.Equal((1, 2), (quest.Variable1, quest.Variable2));
        var dialogue = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Dialogue, dialogue.Kind);
        Assert.Equal(7U, dialogue.SourceEntityId);
        Assert.Equal(20U, dialogue.TargetEntityId);
        Assert.Equal(3003U, dialogue.QuestId);
        Assert.Equal(77, dialogue.MessageId);
        Assert.Equal(0, dialogue.Amount);
    }

    [Fact]
    public void QuestDialogueDoesNotRegressNativeDialogueVariablesOnRetry()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            variable1: 3,
            dialogueVariable1: 1,
            completionUniqueIdentity: "Gharbad");
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Gharbad");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        Assert.True(processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3003, 20),
            5,
            [player]).Succeeded);

        Assert.Equal(3, quest.Variable1);
    }

    [Fact]
    public void QuestDialogueRejectsMismatchedUniqueIdentity()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            messageId: 77,
            completionUniqueIdentity: "Gharbad");
        var target = new AuthoritativeCombatTarget(
            20,
            1,
            0,
            100,
            levelId: 0,
            unique: true,
            uniqueIdentity: "Zhar");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTalkToQuestIntent(3003, 20),
                5,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Logged);
    }

    [Fact]
    public void NativeUniqueDialogueHandInConsumesElixirAndSpawnsVeilOfSteel()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1008, "Veil of Steel", 117, 1, 63800, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(117, 1, 63800, 63800, 0, 0, 18, 60, 60, 1, 1, true),
            new AuthoritativeItemDefinition(2137, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 14,
            unique: true,
            uniqueIdentity: "Lachdanan");
        var quest = new AuthoritativeQuestState(
            3005,
            14,
            1,
            active: false,
            messageId: 81,
            completionUniqueIdentity: "Lachdanan",
            dialogueItemId: 2137,
            dialogueRewardUniqueId: 1008,
            dialogueRewardItemLevel: 1,
            dialogueRewardOffsetY: 1,
            dialogueCompletionMessageId: 83);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 14,
            entityId: 7,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray());

        var initial = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            1,
            [player]);

        Assert.True(initial.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(81, Assert.Single(initial.Events).MessageId);

        var elixir = items.Generate(2137, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, elixir.CreateInfo, 0, 0, elixir));
        player.InventoryGrid[0] = 0;

        var completed = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(83, Assert.Single(completed.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1008, reward.State.UniqueId);
        Assert.Equal((20, 21), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void AuthoredDialogueNormalRewardUsesValidatedSpellBookGeneration()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
            new AuthoritativeItemDefinition(2137, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var spellBooks = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 14,
            unique: true,
            uniqueIdentity: "Lachdanan");
        var quest = new AuthoritativeQuestState(
            3005,
            14,
            1,
            active: false,
            messageId: 81,
            completionUniqueIdentity: "Lachdanan",
            dialogueItemId: 2137,
            dialogueRewardItemId: 114,
            dialogueRewardItemLevel: 1,
            dialogueRewardOffsetY: 1,
            dialogueCompletionMessageId: 83);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            gameMode: DiabloGameMode.SinglePlayer,
            items: items,
            spellBooks: spellBooks);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 14,
            entityId: 7,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray());

        var initial = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            1,
            [player]);
        Assert.True(initial.Succeeded);

        var elixir = items.Generate(2137, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, elixir.CreateInfo, 0, 0, elixir));
        player.InventoryGrid[0] = 0;

        var completed = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3005, 20),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(114, reward.State.ItemIndex);
        Assert.Equal(7, reward.State.SpellId);
        Assert.Equal(DiabloItemActionKind.ReadBook, reward.State.ItemActionKind);
        Assert.Equal("Book of Firebolt", reward.State.BaseName);
        Assert.Equal((20, 21), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void AuthoredDialogueActivationRewardSpawnsOnlyOnFirstConversation()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var spellBooks = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 8,
            unique: true,
            uniqueIdentity: "Zhar");
        var quest = new AuthoritativeQuestState(
            3004,
            8,
            1,
            active: false,
            messageId: 148,
            completionUniqueIdentity: "Zhar",
            dialogueVariable1: 2,
            dialogueRewardItemId: 114,
            dialogueRewardItemLevel: 8,
            dialogueRewardOffsetX: 1,
            dialogueRewardOffsetY: 1,
            dialogueRewardOnActivation: true);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            gameMode: DiabloGameMode.SinglePlayer,
            items: items,
            spellBooks: spellBooks);
        var player = CreatePlayer(
            positionX: 19,
            positionY: 20,
            levelId: 8,
            entityId: 7);

        var first = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3004, 20),
            1,
            [player]);

        Assert.True(first.Succeeded);
        Assert.True(quest.Active);
        Assert.Equal(2, quest.Variable1);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(7, reward.State.SpellId);
        Assert.Equal("Book of Firebolt", reward.State.BaseName);
        Assert.Equal((21, 21), (reward.PositionX, reward.PositionY));

        var second = processor.Execute(
            player,
            new DiabloTalkToQuestIntent(3004, 20),
            2,
            [player]);

        Assert.True(second.Succeeded);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void NativeTownerTurnInStartsQuestConversationThenConsumesItemAndSpawnsUniqueReward()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1001, "Reward Blade", 7, 1, 100, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 1, 2, 0, 20, 20, 1, 1, true),
        ], uniqueItems: uniques);
        var requiredItem = items.Generate(7, 42);
        var inventory = new OwnedStoreItem(0, 0, requiredItem.CreateInfo, 0, 0, requiredItem);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 63,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [inventory],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3011,
            10,
            1,
            messageId: 88,
            variable2: 0,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 7,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1001,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 61,
            turnInRewardPositionY: 64);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Single(player.Inventory);
        var startDialogue = Assert.Single(started.Events);
        Assert.Equal(DiabloGameplayEventKind.Dialogue, startDialogue.Kind);
        Assert.Equal(88, startDialogue.MessageId);

        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1001, reward.State.UniqueId);
        Assert.Equal((61, 64), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void NativeMagicRockTurnInUsesTheAuthoredConversationGateAndEmpyreanBand()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1004, "Empyrean Band", 113, 1, 8000, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(113, 1, 8000, 8000, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(2135, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var requiredItem = items.Generate(2135, 42);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 63,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, requiredItem.CreateInfo, 0, 0, requiredItem)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3001,
            1,
            3,
            messageId: 115,
            variable2: 0,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 2135,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1004,
            turnInRewardItemLevel: 5,
            turnInRewardPositionX: 61,
            turnInRewardPositionY: 64,
            turnInMessageId: 117);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Single(player.Inventory);
        Assert.Equal(115, Assert.Single(started.Events).MessageId);

        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.Empty(player.Inventory);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1004, reward.State.UniqueId);
        Assert.Equal((61, 64), (reward.PositionX, reward.PositionY));
        Assert.Equal(117, Assert.Single(completed.Events).MessageId);
    }

    [Fact]
    public void NativeAnvilTurnInUsesTheAuthoredConversationGateAndGriswoldsEdge()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1010, "Griswold's Edge", 119, 1, 42000, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(119, 1, 750, 750, 4, 12, 0, 50, 50, 1, 1, true),
            new AuthoritativeItemDefinition(2138, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var quest = new AuthoritativeQuestState(
            3011,
            10,
            1,
            active: true,
            messageId: 88,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 2138,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1010,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 61,
            turnInRewardPositionY: 64,
            turnInMessageId: 90);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 63, entityId: 7);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Equal(88, Assert.Single(started.Events).MessageId);

        var anvil = items.Generate(2138, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, anvil.CreateInfo, 0, 0, anvil));
        player.InventoryGrid[0] = 0;

        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3011, 0),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.Empty(player.Inventory);
        Assert.Equal(90, Assert.Single(completed.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1010, reward.State.UniqueId);
        Assert.Equal((61, 64), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void NativeTownerConversationGateRunsBeforeRequiredItemValidation()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2135, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3001,
            1,
            3,
            messageId: 115,
            variable2: 0,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 2135,
            turnInRequiredVariable2: 1,
            turnInMessageId: 117);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 63, entityId: 7);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3001, 0),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Empty(player.Inventory);
        Assert.Equal(115, Assert.Single(started.Events).MessageId);
    }

    [Fact]
    public void NativeOgdenSignTurnInUsesAuthoredStagesAndHarlequinReward()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1007, "Harlequin Crest", 116, 1, 4000, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(116, 7, 4000, 4000, 0, 0, 0, 15, 15, 1, 1, true),
            new AuthoritativeItemDefinition(2136, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var quest = new AuthoritativeQuestState(
            3008,
            4,
            1,
            active: true,
            variable1: 1,
            messageId: 12,
            completionVariable1: 3,
            turnInTownerId: 3,
            turnInLevelId: 0,
            turnInPositionX: 55,
            turnInPositionY: 62,
            turnInItemId: 2136,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1007,
            turnInRewardItemLevel: 4,
            turnInRewardPositionX: 54,
            turnInRewardPositionY: 63,
            turnInMessageId: 13);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 54, positionY: 62, entityId: 7);

        var started = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3008, 3),
            1,
            [player]);

        Assert.True(started.Succeeded);
        Assert.Equal(1, quest.Variable2);
        Assert.Equal(12, Assert.Single(started.Events).MessageId);

        var sign = items.Generate(2136, 42);
        player.Inventory.Add(new OwnedStoreItem(0, 0, sign.CreateInfo, 0, 0, sign));
        player.InventoryGrid[0] = 0;

        var completed = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3008, 3),
            2,
            [player]);

        Assert.True(completed.Succeeded);
        Assert.True(quest.Completed);
        Assert.Equal(3, quest.Variable1);
        Assert.Empty(player.Inventory);
        Assert.Equal(13, Assert.Single(completed.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1007, reward.State.UniqueId);
        Assert.Equal((54, 63), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void NativeTownerTurnInRejectsWrongTownerOrMissingItemWithoutMutation()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 1, 2, 0, 20, 20, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3011,
            10,
            1,
            variable2: 1,
            turnInTownerId: 0,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 63,
            turnInItemId: 7,
            turnInRequiredVariable2: 1);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 63, entityId: 7);

        AssertRejected(
            processor.Execute(player, new DiabloTurnInQuestIntent(3011, 1), 1, [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.False(quest.Completed);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void NativeTownerTurnInSpawnsConfiguredNormalItemReward()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 75, 75, 1, 2, 0, 20, 20, 1, 1, true),
            new AuthoritativeItemDefinition(171, 13, 1200, 1200, 0, 0, 0, 0, 0, 1, 1, false),
        ]);
        var requiredItem = items.Generate(7, 42);
        var player = CreatePlayer(
            positionX: 77,
            positionY: 43,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, requiredItem.CreateInfo, 0, 0, requiredItem)]);
        var quest = new AuthoritativeQuestState(
            3019,
            17,
            1,
            turnInTownerId: 11,
            turnInLevelId: 0,
            turnInPositionX: 77,
            turnInPositionY: 43,
            turnInItemId: 7,
            turnInRewardItemId: 171,
            turnInRewardItemLevel: 13,
            turnInRewardPositionX: 77,
            turnInRewardPositionY: 43);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3019, 11),
            1,
            [player]);

        Assert.True(result.Succeeded);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(171U, reward.State.StableItemId);
        Assert.Equal((77, 43), (reward.PositionX, reward.PositionY));
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void AuthoredJerseyTurnInConsumesBrownSuitAndSpawnsBovinePlate()
    {
        var uniques = new AuthoritativeUniqueItemCatalog([
            new AuthoritativeUniqueItemDefinition(1011, "Bovine Plate", 120, 1, 400, []),
        ]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(120, 9, 0, 0, 0, 0, 40, 40, 40, 2, 2, true),
            new AuthoritativeItemDefinition(2104, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ], uniqueItems: uniques);
        var suit = items.Generate(2104, 42);
        var player = CreatePlayer(
            positionX: 60,
            positionY: 22,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, suit.CreateInfo, 0, 0, suit)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3024,
            9,
            1,
            active: true,
            messageId: 300,
            variable2: 1,
            turnInTownerId: 12,
            turnInLevelId: 0,
            turnInPositionX: 61,
            turnInPositionY: 22,
            turnInItemId: 2104,
            turnInRequiredVariable2: 1,
            turnInRewardUniqueId: 1011,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 62,
            turnInRewardPositionY: 23,
            turnInMessageId: 304);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3024, 12),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(304, Assert.Single(result.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1011, reward.State.UniqueId);
        Assert.Equal((62, 23), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void AuthoredTownerAlternateItemConsumesGreySuitWithoutCompletingQuest()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2104, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            new AuthoritativeItemDefinition(2105, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var greySuit = items.Generate(2105, 42);
        var player = CreatePlayer(
            positionX: 60,
            positionY: 22,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, greySuit.CreateInfo, 0, 0, greySuit)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3024,
            9,
            1,
            active: true,
            turnInTownerId: 12,
            turnInLevelId: 0,
            turnInPositionX: 61,
            turnInPositionY: 22,
            turnInItemId: 2104,
            turnInAlternateItemId: 2105,
            turnInAlternateMessageId: 303);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3024, 12),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(quest.Completed);
        Assert.True(quest.Active);
        Assert.Empty(player.Inventory);
        Assert.Equal(303, Assert.Single(result.Events).MessageId);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void AuthoredTownerActivationSpawnsInitialRuneBombOnce()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2101, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3018,
            9,
            1,
            active: false,
            activateOnRoute: false,
            messageId: 277,
            completionVariable1: 1,
            turnInTownerId: 10,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 16,
            turnInRewardItemId: 2101,
            turnInRewardItemLevel: 1,
            turnInRewardPositionX: 63,
            turnInRewardPositionY: 16,
            turnInActivationOnly: true);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 16, entityId: 7);

        var first = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3018, 10),
            1,
            [player]);

        Assert.True(first.Succeeded);
        Assert.True(quest.Active);
        Assert.Equal(1, quest.Variable1);
        Assert.Equal(277, Assert.Single(first.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(2101U, reward.State.StableItemId);
        Assert.Equal((63, 16), (reward.PositionX, reward.PositionY));

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3018, 10),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void NativeButcherActivationUsesAuthoredDeadGuyInteraction()
    {
        var quest = new AuthoritativeQuestState(
            3007,
            2,
            1,
            active: false,
            messageId: 65,
            completionMonsterId: 53,
            turnInTownerId: 2,
            turnInLevelId: 0,
            turnInPositionX: 24,
            turnInPositionY: 32,
            turnInActivationOnly: true,
            turnInActivationVariable1: 1);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(
            world,
            items: new AuthoritativeItemCatalog([
                new AuthoritativeItemDefinition(1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
            ]));
        var player = CreatePlayer(positionX: 23, positionY: 32, entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3007, 2),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(1, quest.Variable1);
        Assert.Equal(65, Assert.Single(result.Events).MessageId);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3007, 2),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void AuthoredTownerActivationConsumesItemAndUsesExplicitNativeMarker()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(34, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var staff = items.Generate(34, 42);
        var player = CreatePlayer(
            positionX: 61,
            positionY: 71,
            inventoryGrid: [0, ..Enumerable.Repeat(-1, 39)],
            inventory: [new OwnedStoreItem(0, 0, staff.CreateInfo, 0, 0, staff)],
            entityId: 7);
        var quest = new AuthoritativeQuestState(
            3016,
            15,
            1,
            active: false,
            completionVariable1: 7,
            turnInTownerId: 4,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 71,
            turnInItemId: 34,
            turnInMessageId: 35,
            turnInActivationOnly: true,
            turnInActivationVariable1: 2);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        Assert.Equal(35, quest.TurnInMessageId);
        Assert.True(quest.TurnInActivationOnly);

        var result = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3016, 4),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.True(quest.Active);
        Assert.False(quest.Completed);
        Assert.Equal(2, quest.Variable1);
        Assert.Empty(player.Inventory);
        Assert.Equal(35, Assert.Single(result.Events).MessageId);
        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3016, 4),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void AuthoredTownerCompletionStageSpawnsAuricOnce()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2103, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true),
        ]);
        var quest = new AuthoritativeQuestState(
            3018,
            9,
            1,
            completed: true,
            active: false,
            variable2: 0,
            turnInTownerId: 10,
            turnInLevelId: 0,
            turnInPositionX: 62,
            turnInPositionY: 16,
            turnInOnCompletion: true,
            turnInCompletionVariable2: 1,
            turnInCompletionMessageId: 280,
            turnInCompletionRewardItemId: 2103,
            turnInCompletionRewardPositionX: 63,
            turnInCompletionRewardPositionY: 16);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world, items: items);
        var player = CreatePlayer(positionX: 61, positionY: 16, entityId: 7);

        var first = processor.Execute(
            player,
            new DiabloTurnInQuestIntent(3018, 10),
            1,
            [player]);

        Assert.True(first.Succeeded);
        Assert.True(quest.Completed);
        Assert.Equal(1, quest.Variable2);
        Assert.Equal(280, Assert.Single(first.Events).MessageId);
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(2103U, reward.State.StableItemId);
        Assert.Equal((63, 16), (reward.PositionX, reward.PositionY));

        AssertRejected(
            processor.Execute(
                player,
                new DiabloTurnInQuestIntent(3018, 10),
                2,
                [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Single(world.WorldItems);
    }

    [Fact]
    public void TrapDisarmRequiresRogueAbilityAndClearsArmedState()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var player = new AuthoritativePlayerState(
            0,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            50,
            1,
            0,
            0,
            abilitySpellId: 28) {
            EntityId = 7,
        };
        var processor = new DiabloWorldCommandProcessor(
            world,
            trapDisarmAbilitySpellId: 28);

        var result = processor.Execute(
            player,
            new DiabloDisarmTrapIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(@object.TrapArmed);
    }

    [Fact]
    public void SemanticTrapDisarmRoleAuthorizesAConfiguredCustomSpellIdentity()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var player = new AuthoritativePlayerState(
            0,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            50,
            1,
            0,
            0,
            abilitySpellId: 900,
            abilityKind: DiabloPlayerAbilityKind.TrapDisarm) {
            EntityId = 7,
        };
        var processor = new DiabloWorldCommandProcessor(
            world,
            trapDisarmAbilitySpellId: 28);

        var result = processor.Execute(
            player,
            new DiabloDisarmTrapIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.False(@object.TrapArmed);
    }

    [Fact]
    public void TrapDisarmRejectsWrongAbilityAndLeavesTrapArmed()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            trapArmed: true);
        var world = new DiabloWorldState(objects: [@object]);
        var player = CreatePlayer(entityId: 7);
        var processor = new DiabloWorldCommandProcessor(
            world,
            trapDisarmAbilitySpellId: 28);

        AssertRejected(
            processor.Execute(
                player,
                new DiabloDisarmTrapIntent(20),
                5,
                [player]),
            DiabloRuleFailure.NotAllowed);
        Assert.True(@object.TrapArmed);
    }

    [Fact]
    public void ExperienceObjectLevelsPlayerAndEmitsEvent()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            effectKind: AuthoritativeObjectEffectKind.Experience,
            effectAmount: 2100);
        var world = new DiabloWorldState(objects: [@object]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(
            player,
            new DiabloOperateObjectIntent(20),
            5,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2100U, player.Experience);
        Assert.Equal(3U, player.CharacterLevel);
        Assert.Equal(
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Experience,
                7,
                7,
                2100),
            Assert.Single(result.Events));
    }

    [Fact]
    public void QuestAdvancementCompletesThenRejectsFurtherProgress()
    {
        var quest = new AuthoritativeQuestState(30, 0, 2);
        var world = new DiabloWorldState(quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        Assert.True(processor.Execute(
            player,
            new DiabloAdvanceQuestIntent(30),
            1,
            [player]).Succeeded);
        Assert.True(quest.Active);
        Assert.True(quest.Logged);
        Assert.True(processor.Execute(
            player,
            new DiabloAdvanceQuestIntent(30),
            2,
            [player]).Succeeded);
        Assert.Equal(2U, quest.Progress);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);

        AssertRejected(
            processor.Execute(player, new DiabloAdvanceQuestIntent(30), 3, [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2U, quest.Progress);
    }

    [Fact]
    public void UnavailableQuestRejectsAdvancementAndLinkedObjectProgress()
    {
        var quest = new AuthoritativeQuestState(30, 0, 2, unavailable: true);
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            questId: 30);
        var world = new DiabloWorldState(objects: [@object], quests: [quest]);
        var processor = new DiabloWorldCommandProcessor(world);
        var player = CreatePlayer(entityId: 1);

        AssertRejected(
            processor.Execute(player, new DiabloAdvanceQuestIntent(30), 1, [player]),
            DiabloRuleFailure.InvalidTarget);
        AssertRejected(
            processor.Execute(player, new DiabloOperateObjectIntent(20), 2, [player]),
            DiabloRuleFailure.InvalidTarget);
        Assert.Equal(0U, quest.Progress);
        Assert.False(@object.Activated);
    }

    [Fact]
    public void InitialWorldValidationRejectsOverlappingLiveEntities()
    {
        var monster = new AuthoritativeCombatTarget(10, 1, 0, 5);
        var item = new AuthoritativeWorldItem(
            20,
            0,
            1,
            0,
            42,
            75,
            AuthoritativeItemState.Empty);
        var world = new DiabloWorldState(combatTargets: [monster], worldItems: [item]);

        Assert.Throws<InvalidDataException>(() => world.ValidateInitialPlayerPosition(0, 0, 0));
    }

    private static AuthoritativePlayerState CreatePlayer(
        int positionX = 0,
        int positionY = 0,
        uint levelId = 0,
        int life = 50,
        int lifeMaximum = 50,
        uint entityId = 0,
        IReadOnlyList<int>? inventoryGrid = null,
        uint gold = 100,
        IReadOnlyList<OwnedStoreItem>? inventory = null,
        OwnedStoreItem? heldItem = null,
        IReadOnlyList<EquippedStoreItem>? equipment = null)
    {
        return new AuthoritativePlayerState(
            gold,
            0,
            life,
            0,
            0,
            PlayerAttributesState.Zero,
            equipment ?? [],
            inventoryGrid ?? Enumerable.Repeat(-1, 40).ToArray(),
            [],
            positionX,
            positionY,
            lifeMaximum,
            1,
            levelId,
            0,
            inventory: inventory,
            heldItem: heldItem) {
            EntityId = entityId,
        };
    }

    private static OwnedStoreItem CreateGoldStack(
        uint itemSeed,
        uint value,
        AuthoritativeCombatRules rules)
    {
        return new OwnedStoreItem(
            0,
            0,
            itemSeed,
            value,
            0,
            CreateGoldState(value, rules));
    }

    private static AuthoritativeItemState CreateGoldState(
        uint value,
        AuthoritativeCombatRules rules)
    {
        return AuthoritativeItemState.Empty with {
            ItemType = rules.DeathGoldItemType,
            ItemIndex = rules.DeathGoldItemIndex,
            Value = checked((int)value),
            IdentifiedValue = checked((int)value),
            StatFlag = true,
        };
    }

    private static void AssertRejected(
        DiabloWorldIntentResult result,
        DiabloRuleFailure expectedFailure)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expectedFailure, result.Failure);
        Assert.Empty(result.Events);
    }
}
