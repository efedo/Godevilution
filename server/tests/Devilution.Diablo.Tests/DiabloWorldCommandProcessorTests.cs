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
        OwnedStoreItem? heldItem = null)
    {
        return new AuthoritativePlayerState(
            gold,
            0,
            life,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
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
