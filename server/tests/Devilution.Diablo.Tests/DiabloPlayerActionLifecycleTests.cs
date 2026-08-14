using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloPlayerActionLifecycleTests
{
    [Fact]
    public void MovementReservesItsDestinationUntilTheNativeWalkCompletes()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var world = new DiabloWorldState(fallbackWidth: 5, fallbackHeight: 5);
        var worldCommands = new DiabloWorldCommandProcessor(
            world,
            animations,
            useNativePlayerActionTiming: true);
        var combatCommands = CreateCombatProcessor(world, classes, animations);
        var player = CreatePlayer(entityId: 1);
        var other = CreatePlayer(positionX: 2, entityId: 2);

        var result = worldCommands.Execute(
            player,
            new DiabloMoveIntent(1, 0),
            10,
            [player, other]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
        Assert.Equal((1, 0), (player.FuturePositionX, player.FuturePositionY));
        Assert.Equal(DiabloPlayerDirection.SouthEast, player.FacingDirection);
        Assert.Equal(DiabloPlayerCombatMode.WalkSouthwards, player.CombatMode);
        Assert.Equal(18UL, player.CombatModeEndTick);
        Assert.True(world.IsOccupied(0, 1, 0, [player, other], other.EntityId));
        Assert.Equal(
            DiabloRuleFailure.InvalidTarget,
            worldCommands.Execute(
                other,
                new DiabloMoveIntent(-1, 0),
                10,
                [player, other]).Failure);

        Assert.Empty(combatCommands.AdvanceTick(17, [player, other]));
        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
        Assert.Empty(combatCommands.AdvanceTick(18, [player, other]));
        Assert.Equal((1, 0), (player.PositionX, player.PositionY));
        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);
    }

    [Fact]
    public void MeleeDamageResolvesAtTheNativeActionFrameAndCanChainAfterward()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateCombatProcessor(world, classes, animations);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 10);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Events);
        Assert.Equal(100, target.HitPoints);
        Assert.Equal(DiabloPlayerDirection.SouthEast, player.FacingDirection);
        Assert.Equal(DiabloPlayerCombatMode.Attack, player.CombatMode);
        Assert.Equal((18UL, 19UL, 25UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));

        Assert.Empty(processor.AdvanceTick(17, [player]));
        Assert.Equal(100, target.HitPoints);
        var delivery = Assert.Single(processor.AdvanceTick(18, [player]));
        Assert.Same(player, delivery.Recipient);
        Assert.Equal(80, target.HitPoints);
        Assert.Equal(
            DiabloRuleFailure.NotAllowed,
            processor.Execute(player, new DiabloAttackIntent(9), 18).Failure);

        Assert.Empty(processor.AdvanceTick(19, [player]));
        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 19).Succeeded);
        Assert.Equal(34UL, player.CombatModeEndTick);
    }

    [Fact]
    public void SpellResourcesAndEffectsResolveAtTheNativeCastingFrame()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var target = new AuthoritativeCombatTarget(9, 2, 0, 20);
        var world = new DiabloWorldState(combatTargets: [target]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) {
                DamageAmount = 6,
                Range = 4,
            },
        ]);
        var processor = CreateCombatProcessor(world, classes, animations, spells);
        var player = CreatePlayer(
            mana: 10,
            manaMaximum: 10,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(7, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(7, 9, 0, 0),
            10);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(10, player.Mana);
        Assert.Equal(20, target.HitPoints);
        Assert.Equal(DiabloPlayerDirection.SouthEast, player.FacingDirection);
        Assert.Equal(DiabloPlayerCombatMode.Spell, player.CombatMode);
        Assert.Equal((24UL, 24UL, 29UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));

        Assert.Empty(processor.AdvanceTick(23, [player]));
        var delivery = Assert.Single(processor.AdvanceTick(24, [player]));
        Assert.Equal(DiabloGameplayEventKind.Damage, delivery.Event.Kind);
        Assert.Equal(6, player.Mana);
        Assert.Equal(14, target.HitPoints);
        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(7, 9, 0, 0),
            24).Succeeded);
    }

    [Fact]
    public void NativeFirstAttackSpeedFlagsShortenOnlyPreEffectFrames()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateCombatProcessor(world, classes, animations);
        var fastestSword = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            StatFlag = true,
            Flags = 1U << 20,
        };
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 100, fastestSword)]);

        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 10).Succeeded);

        Assert.Equal((16UL, 17UL, 23UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
    }

    [Fact]
    public void SemanticWeaponTagsSelectMonkSwordTimingWhenLegacyClassConflicts()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 99, 1, 1, 0, 0, 0, 0, 0, 1, 1, true) {
                ItemClass = 0,
                GenerationTags = new HashSet<string>(["Weapon", "Sword"], StringComparer.OrdinalIgnoreCase),
                NativeItemIndex = 101,
                EquipLocation = 1,
            },
        ]);
        var processor = CreateCombatProcessor(world, classes, animations, items: items);
        var sword = AuthoritativeItemState.Empty with {
            ItemIndex = 101,
            ItemType = 99,
            ItemClass = 0,
            EquipLocation = 1,
            StatFlag = true,
        };
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 3,
            equipment: [new EquippedStoreItem(4, 101, sword)]);

        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 10).Succeeded);
        Assert.Equal((21UL, 22UL, 25UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
    }

    [Fact]
    public void NativeQuickAttackAddsItsFirstFrameSkipOnlyToChainedMeleeAttacks()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var target = new AuthoritativeCombatTarget(9, 1, 0, 1000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateCombatProcessor(world, classes, animations);
        var quickSword = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            StatFlag = true,
            Flags = 1U << 17,
        };
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 100, quickSword)]);

        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 10).Succeeded);
        Assert.Equal((18UL, 19UL, 25UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));

        processor.AdvanceTick(18, [player]);
        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 19).Succeeded);
        Assert.Equal((26UL, 27UL, 33UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
    }

    [Fact]
    public void NativeQuickAttackAddsItsFirstFrameSkipToChainedBaseGameBowAttacks()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var target = new AuthoritativeCombatTarget(9, 3, 0, 1000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateCombatProcessor(world, classes, animations);
        var quickBow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
            Flags = 1U << 17,
        };
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 1,
            equipment: [new EquippedStoreItem(4, 100, quickBow)]);

        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 10).Succeeded);
        Assert.Equal((16UL, 17UL, 21UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));

        processor.AdvanceTick(16, [player]);
        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 17).Succeeded);
        Assert.Equal((22UL, 23UL, 27UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));

        var hellfireTarget = new AuthoritativeCombatTarget(10, 3, 0, 1000);
        var hellfireWorld = new DiabloWorldState(combatTargets: [hellfireTarget]);
        var hellfireProcessor = CreateCombatProcessor(
            hellfireWorld,
            classes,
            animations,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var hellfirePlayer = CreatePlayer(
            entityId: 8,
            characterClassId: 1,
            equipment: [new EquippedStoreItem(4, 101, quickBow)]);

        Assert.True(hellfireProcessor.Execute(
            hellfirePlayer,
            new DiabloAttackIntent(10),
            10).Succeeded);
        Assert.Equal((16UL, 17UL, 21UL), (
            hellfirePlayer.CurrentAction!.EffectTick,
            hellfirePlayer.CurrentAction.ReadyTick,
            hellfirePlayer.CurrentAction.EndTick));

        hellfireProcessor.AdvanceTick(16, [hellfirePlayer]);
        Assert.True(hellfireProcessor.Execute(
            hellfirePlayer,
            new DiabloAttackIntent(10),
            17).Succeeded);
        Assert.Equal((23UL, 24UL, 28UL), (
            hellfirePlayer.CurrentAction!.EffectTick,
            hellfirePlayer.CurrentAction.ReadyTick,
            hellfirePlayer.CurrentAction.EndTick));
    }

    [Fact]
    public void BowTimingUsesRangedModeAndHellfireDisablesBaseGameFrameSkipping()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var baseTarget = new AuthoritativeCombatTarget(9, 3, 0, 100);
        var hellfireTarget = new AuthoritativeCombatTarget(10, 3, 0, 100);
        var baseWorld = new DiabloWorldState(combatTargets: [baseTarget]);
        var hellfireWorld = new DiabloWorldState(combatTargets: [hellfireTarget]);
        var baseProcessor = CreateCombatProcessor(baseWorld, classes, animations);
        var hellfireProcessor = CreateCombatProcessor(
            hellfireWorld,
            classes,
            animations,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var fastBow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
            Flags = 1U << 18,
        };
        var baseRogue = CreatePlayer(
            entityId: 7,
            characterClassId: 1,
            equipment: [new EquippedStoreItem(4, 100, fastBow)]);
        var hellfireRogue = CreatePlayer(
            entityId: 8,
            characterClassId: 1,
            equipment: [new EquippedStoreItem(4, 100, fastBow)]);

        Assert.True(baseProcessor.Execute(
            baseRogue,
            new DiabloAttackIntent(9),
            10).Succeeded);
        Assert.True(hellfireProcessor.Execute(
            hellfireRogue,
            new DiabloAttackIntent(10),
            10).Succeeded);

        Assert.Equal(DiabloPlayerCombatMode.RangedAttack, baseRogue.CombatMode);
        Assert.Equal((15UL, 16UL, 20UL), (
            baseRogue.CurrentAction!.EffectTick,
            baseRogue.CurrentAction.ReadyTick,
            baseRogue.CurrentAction.EndTick));
        Assert.Equal((16UL, 17UL, 21UL), (
            hellfireRogue.CurrentAction!.EffectTick,
            hellfireRogue.CurrentAction.ReadyTick,
            hellfireRogue.CurrentAction.EndTick));
    }

    [Fact]
    public void LethalMonsterDamageCancelsMovementAndCompletesDeathAnimation()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var worldCommands = new DiabloWorldCommandProcessor(
            world,
            animations,
            useNativePlayerActionTiming: true);
        var combatCommands = CreateCombatProcessor(world, classes, animations);
        var player = CreatePlayer(entityId: 7, life: 5);

        Assert.True(worldCommands.Execute(
            player,
            new DiabloMoveIntent(0, 1),
            1,
            [player]).Succeeded);
        var damage = Assert.Single(combatCommands.AdvanceTick(1, [player]));

        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Event.Kind);
        Assert.Equal(0, player.Life);
        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
        Assert.Equal(DiabloPlayerCombatMode.Death, player.CombatMode);
        Assert.Equal(40UL, player.CombatModeEndTick);
        Assert.Equal(DiabloPlayerActionKind.Death, player.CurrentAction!.Kind);
        Assert.True(player.CurrentAction.EffectApplied);
        Assert.Equal(9U, player.CurrentAction.TargetEntityId);
        Assert.Equal((1UL, 1UL, 40UL, 40UL), (
            player.CurrentAction.StartedAtTick,
            player.CurrentAction.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
        Assert.Equal(
            DiabloRuleFailure.NotAllowed,
            worldCommands.Execute(
                player,
                new DiabloMoveIntent(1, 0),
                2,
                [player]).Failure);

        Assert.Empty(combatCommands.AdvanceTick(39, [player]));
        Assert.NotNull(player.CurrentAction);
        Assert.Empty(combatCommands.AdvanceTick(40, [player]));
        Assert.Equal(DiabloPlayerCombatMode.Death, player.CombatMode);
        Assert.Equal(0UL, player.CombatModeEndTick);
        Assert.Null(player.CurrentAction);
    }

    [Fact]
    public void LethalMonsterDamageDropsHalfGoldAsDeterministicWorldStacks()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            deathGoldMaximumStack: 20);
        var combatCommands = CreateCombatProcessor(
            world,
            classes,
            animations,
            rules: rules);
        var player = CreatePlayer(entityId: 7, life: 5, gold: 101);

        Assert.Single(combatCommands.AdvanceTick(1, [player]));

        Assert.Equal(50U, player.Gold);
        var drops = world.WorldItems.Values.OrderBy(item => item.EntityId).ToArray();
        Assert.Equal(3, drops.Length);
        Assert.Equal([20U, 20U, 10U], drops.Select(item => item.Price).ToArray());
        Assert.Equal([(0, 1), (0, 2), (1, 2)], drops
            .Select(item => (item.PositionX, item.PositionY))
            .ToArray());
        Assert.Equal([0x40000000U, 0x40000001U, 0x40000002U], drops
            .Select(item => item.EntityId)
            .ToArray());
        Assert.All(drops, item => {
            Assert.NotEqual(0U, item.ItemSeed);
            Assert.Equal(11, item.State.ItemType);
            Assert.Equal(0, item.State.ItemIndex);
            Assert.Equal((int)item.Price, item.State.Value);
            Assert.Equal((int)item.Price, item.State.IdentifiedValue);
            Assert.True(item.State.StatFlag);
        });
    }

    [Fact]
    public void LethalMonsterDamageDropsEquipmentInNativeSlotAndDirectionOrder()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var monster = new AuthoritativeCombatTarget(
            9,
            11,
            10,
            100,
            attackDamage: 5,
            aggroRange: 1);
        var world = new DiabloWorldState(
            fallbackWidth: 25,
            fallbackHeight: 25,
            combatTargets: [monster]);
        var combatCommands = CreateCombatProcessor(world, classes, animations);
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemIndex = 4,
            Value = 12,
            IdentifiedValue = 34,
            StatFlag = true,
        };
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            entityId: 7,
            life: 5,
            equipment: [
                new EquippedStoreItem(6, 103, itemState with { ItemIndex = 6 }),
                new EquippedStoreItem(0, 101, itemState),
                new EquippedStoreItem(3, 102, itemState with { ItemIndex = 5 }),
            ]);
        player.FacingDirection = DiabloPlayerDirection.East;

        Assert.Single(combatCommands.AdvanceTick(1, [player]));

        Assert.Empty(player.Equipment);
        var drops = world.WorldItems.Values.OrderBy(item => item.EntityId).ToArray();
        Assert.Equal([101U, 102U, 103U], drops.Select(item => item.ItemSeed).ToArray());
        Assert.Equal([(10, 9), (9, 11), (10, 11)], drops
            .Select(item => (item.PositionX, item.PositionY))
            .ToArray());
        Assert.All(drops, item => Assert.Equal(34U, item.Price));
        Assert.Equal([4, 5, 6], drops.Select(item => item.State.ItemIndex).ToArray());
    }

    [Fact]
    public void LethalMonsterDamagePreservesNativeGoldStackOrderSeedsAndOddRemainder()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            deathGoldMaximumStack: 20);
        var combatCommands = CreateCombatProcessor(
            world,
            classes,
            animations,
            rules: rules);
        var inventory = new[] {
            CreateGoldStack(101, 20, rules),
            CreateGoldStack(102, 7, rules),
            CreateGoldStack(103, 4, rules),
            CreateGoldStack(104, 20, rules),
        };
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        for (var index = 0; index < inventory.Length; index++)
            inventoryGrid[index] = index;
        var player = CreatePlayer(
            entityId: 7,
            life: 5,
            gold: 51,
            inventory: inventory,
            inventoryGrid: inventoryGrid);

        Assert.Single(combatCommands.AdvanceTick(1, [player]));

        Assert.Equal(25U, player.Gold);
        Assert.Equal([6, 20], player.Inventory.Select(item => item.State.Value).ToArray());
        Assert.Equal([101U, 104U], player.Inventory.Select(item => item.ItemSeed).ToArray());
        Assert.Equal(26, player.Inventory.Sum(item => item.State.Value));
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Equal(-1, player.InventoryGrid[1]);
        Assert.Equal(-1, player.InventoryGrid[2]);
        Assert.Equal(1, player.InventoryGrid[3]);

        var drops = world.WorldItems.Values.OrderBy(item => item.EntityId).ToArray();
        Assert.Equal([7U, 4U, 14U], drops.Select(item => item.Price).ToArray());
        Assert.Equal([102U, 103U, 1U], drops.Select(item => item.ItemSeed).ToArray());
        Assert.Equal([(0, 1), (0, 2), (1, 2)], drops
            .Select(item => (item.PositionX, item.PositionY))
            .ToArray());
    }

    [Fact]
    public void DeathGoldRoundTripsThroughPersistenceAndCanBeRecoveredAfterRestart()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var original = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    100,
                    attackDamage: 5,
                    aggroRange: 1)],
                CombatRules = rules,
                PlayerClasses = classes,
                PlayerAnimations = animations,
                UseNativePlayerActionTiming = true,
                RandomSeed = 1,
            });
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var player = CreatePlayer(
            entityId: 7,
            life: 5,
            gold: 101,
            inventory: [CreateGoldStack(123, 101, rules)],
            inventoryGrid: inventoryGrid);

        Assert.Single(original.AdvanceTick(1, [player]));
        var snapshot = original.CaptureSnapshot(player, 1);
        var savedGold = Assert.Single(snapshot.WorldItems);

        var restored = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatRules = rules,
                PlayerClasses = classes,
                PlayerAnimations = animations,
                UseNativePlayerActionTiming = true,
            });
        var destination = CreatePlayer(entityId: 7, life: 5);
        restored.RestoreSnapshot(destination, snapshot);

        Assert.Equal(50U, destination.Gold);
        Assert.Equal(51, Assert.Single(destination.Inventory).State.Value);
        Assert.Equal(snapshot.RandomState, restored.RandomState);
        Assert.Equal(savedGold.ItemSeed, Assert.Single(restored.WorldState.WorldItems).Value.ItemSeed);
        restored.WorldState.CombatTargets.Clear();
        Assert.Empty(restored.AdvanceTick(40, [destination]));
        Assert.True(restored.Execute(
            destination,
            new DiabloRestartTownIntent(),
            41,
            [destination]).Succeeded);
        Assert.Empty(restored.AdvanceTick(42, [destination]));
        Assert.True(restored.Execute(
            destination,
            new DiabloPickupWorldItemIntent(savedGold.EntityId),
            42,
            [destination]).Succeeded);
        Assert.Equal(101U, destination.Gold);
        Assert.Equal(101, Assert.Single(destination.Inventory).State.Value);
        Assert.Empty(restored.WorldState.WorldItems);
    }

    [Fact]
    public void RestartTownRequiresTerminalDeathAndAppliesNativeResourcesAndBarrier()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(0, 112, 112));
        geometry.AddLevel(new AuthoritativeLevel(1, 40, 40, isDefaultStart: true));
        var world = new DiabloWorldState(geometry);
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            restartPositionX: 75,
            restartPositionY: 68);
        var worldCommands = new DiabloWorldCommandProcessor(
            world,
            animations,
            useNativePlayerActionTiming: true,
            rules);
        var combatCommands = CreateCombatProcessor(
            world,
            classes,
            animations,
            rules: rules);
        var player = CreatePlayer(
            positionX: 12,
            positionY: 13,
            mana: 1280,
            manaMaximum: 1280,
            entityId: 7,
            life: 0,
            lifeMaximum: 640,
            levelId: 1);
        player.CombatMode = DiabloPlayerCombatMode.Death;
        player.ActiveStoreId = 10;

        var result = worldCommands.Execute(
            player,
            new DiabloRestartTownIntent(),
            20,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal((0U, 75, 68), (
            player.LevelId,
            player.PositionX,
            player.PositionY));
        Assert.Equal((64, 0), (player.Life, player.Mana));
        Assert.Null(player.ActiveStoreId);
        Assert.Equal(DiabloPlayerCombatMode.NewLevel, player.CombatMode);
        Assert.Equal(21UL, player.CombatModeEndTick);
        Assert.Equal(DiabloPlayerActionKind.RestartTown, player.CurrentAction!.Kind);
        Assert.True(player.CurrentAction.EffectApplied);
        Assert.Equal(0U, player.CurrentAction.TargetEntityId);

        Assert.Empty(combatCommands.AdvanceTick(21, [player]));
        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        Assert.Null(player.CurrentAction);

        Assert.Equal(
            DiabloRuleFailure.NotAllowed,
            worldCommands.Execute(
                player,
                new DiabloRestartTownIntent(),
                22,
                [player]).Failure);
    }

    [Fact]
    public void PortalUseCreatesNewLevelBarrierBeforeReturningToStand()
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var portal = new AuthoritativePortal(5, 0, 0, 0, 2, 3, 4);
        var world = new DiabloWorldState(portals: [portal]);
        var worldCommands = new DiabloWorldCommandProcessor(
            world,
            animations,
            useNativePlayerActionTiming: true);
        var combatCommands = CreateCombatProcessor(world, classes, animations);
        var player = CreatePlayer(entityId: 7);

        var result = worldCommands.Execute(
            player,
            new DiabloUsePortalIntent(5),
            10,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal((2U, 3, 4), (
            player.LevelId,
            player.PositionX,
            player.PositionY));
        Assert.Equal(DiabloPlayerCombatMode.NewLevel, player.CombatMode);
        Assert.Equal(11UL, player.CombatModeEndTick);
        Assert.Equal(
            DiabloPlayerActionKind.LevelTransition,
            player.CurrentAction!.Kind);
        Assert.True(player.CurrentAction.EffectApplied);
        Assert.Equal(5U, player.CurrentAction.TargetEntityId);
        Assert.Equal(
            DiabloRuleFailure.NotAllowed,
            worldCommands.Execute(
                player,
                new DiabloMoveIntent(1, 0),
                10,
                [player]).Failure);

        Assert.Empty(combatCommands.AdvanceTick(11, [player]));
        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        Assert.Equal(0UL, player.CombatModeEndTick);
        Assert.Null(player.CurrentAction);
    }

    private static DiabloCombatCommandProcessor CreateCombatProcessor(
        DiabloWorldState world,
        AuthoritativePlayerClassCatalog classes,
        AuthoritativePlayerAnimationCatalog animations,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeCombatRules? rules = null,
        AuthoritativeItemCatalog? items = null)
    {
        return new DiabloCombatCommandProcessor(
            world,
            spells ?? new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(1, 1, 1, 0, 0, 0),
            ]),
            rules ?? new AuthoritativeCombatRules(10, 1, 100),
            playerClasses: classes,
            playerAnimations: animations,
            items: items,
            useNativePlayerActionTiming: true);
    }

    private static AuthoritativePlayerState CreatePlayer(
        int positionX = 0,
        int positionY = 0,
        int mana = 0,
        int manaMaximum = 0,
        uint entityId = 0,
        IReadOnlyList<MemorizedSpellState>? memorizedSpells = null,
        IReadOnlyList<EquippedStoreItem>? equipment = null,
        uint characterClassId = 0,
        int life = 50,
        int? lifeMaximum = null,
        uint levelId = 0,
        uint gold = 0,
        IReadOnlyList<OwnedStoreItem>? inventory = null,
        IReadOnlyList<int>? inventoryGrid = null)
    {
        return new AuthoritativePlayerState(
            gold,
            0,
            life,
            mana,
            manaMaximum,
            PlayerAttributesState.Zero,
            equipment ?? [],
            inventoryGrid ?? Enumerable.Repeat(-1, 40).ToArray(),
            [],
            positionX,
            positionY,
            lifeMaximum ?? life,
            1,
            levelId,
            0,
            characterClassId,
            inventory,
            memorizedSpells: memorizedSpells) {
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
            AuthoritativeItemState.Empty with {
                ItemType = rules.DeathGoldItemType,
                Value = checked((int)value),
                IdentifiedValue = checked((int)value),
                ItemIndex = rules.DeathGoldItemIndex,
                StatFlag = true,
            });
    }

    private static (
        AuthoritativePlayerClassCatalog Classes,
        AuthoritativePlayerAnimationCatalog Animations)
        LoadShippedPlayerMetadata()
    {
        var root = FindRepositoryRoot();
        var contentRoot = Path.Combine(root, "server", "content", "base");
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        return (classes, animations);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }
}
