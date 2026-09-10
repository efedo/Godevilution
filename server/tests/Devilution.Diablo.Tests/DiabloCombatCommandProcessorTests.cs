using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloCombatCommandProcessorTests
{
    [Fact]
    public void TelekinesisPicksUpAVisibleWorldItemWithoutAdjacency()
    {
        var item = new AuthoritativeWorldItem(
            100,
            0,
            4,
            0,
            42,
            25,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var world = new DiabloWorldState(worldItems: [item]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            mana: 2000,
            manaMaximum: 2000,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                100,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(1040, player.Mana);
        Assert.Empty(world.WorldItems);
        Assert.Equal(42U, Assert.Single(player.Inventory).ItemSeed);
    }

    [Fact]
    public void TelekinesisRejectsCursorReplacementBeforeSpendingManaWhenTheDropTileIsBlocked()
    {
        var item = new AuthoritativeWorldItem(
            100,
            0,
            4,
            0,
            42,
            25,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var held = new OwnedStoreItem(
            0,
            0,
            77,
            65,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var world = new DiabloWorldState(
            worldItems: [item],
            combatTargets: [new AuthoritativeCombatTarget(9, 0, 0, 10)]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            mana: 2000,
            manaMaximum: 2000,
            heldItem: held,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                100,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.NotAllowed);
        Assert.Equal(2000, player.Mana);
        Assert.Same(held, player.HeldItem);
        Assert.Same(item, Assert.Single(world.WorldItems.Values));
    }

    [Fact]
    public void TelekinesisActivatesAVisibleObject()
    {
        var @object = new AuthoritativeWorldObject(
            200,
            2,
            0,
            4,
            0,
            effectKind: AuthoritativeObjectEffectKind.Heal,
            effectAmount: 5);
        var world = new DiabloWorldState(objects: [@object]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            life: 10,
            lifeMaximum: 20,
            mana: 2000,
            manaMaximum: 2000,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                200,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.True(@object.Activated);
        Assert.Equal(15, player.Life);
        Assert.Equal(DiabloGameplayEventKind.Healing, Assert.Single(result.Events).Kind);
    }

    [Fact]
    public void TelekinesisRejectsAnInvalidAuthoredObjectBeforeSpendingMana()
    {
        var @object = new AuthoritativeWorldObject(
            200,
            1,
            0,
            4,
            0,
            effectKind: AuthoritativeObjectEffectKind.GrantSpellLevel,
            effectAmount: 999);
        var world = new DiabloWorldState(objects: [@object]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            mana: 2000,
            manaMaximum: 2000,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                200,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2000, player.Mana);
        Assert.Equal(1U, Assert.Single(player.MemorizedSpells).Level);
        Assert.False(@object.Activated);
    }

    [Fact]
    public void TelekinesisKnocksBackAVisibleMonsterOneCell()
    {
        var target = new AuthoritativeCombatTarget(9, 4, 0, 10);
        var world = new DiabloWorldState(combatTargets: [target]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            mana: 2000,
            manaMaximum: 2000,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                9,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal((5, 0), (target.PositionX, target.PositionY));
        Assert.Equal(1040, player.Mana);
    }

    [Fact]
    public void TelekinesisRejectsAnObstructedTargetBeforeSpendingMana()
    {
        var target = new AuthoritativeCombatTarget(9, 4, 0, 10);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [1],
            combatTargets: [target]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            mana: 2000,
            manaMaximum: 2000,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                9,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2000, player.Mana);
        Assert.Equal((4, 0), (target.PositionX, target.PositionY));
    }

    [Fact]
    public void TelekinesisRejectsNativeBlockLightTargetBeforeSpendingMana()
    {
        var geometry = new AuthoritativeWorld();
        var pieces = new ushort[5];
        pieces[1] = 7;
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            5,
            1,
            nativePieceLayer: pieces,
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [7] = AuthoritativeDunTileFlags.BlockLight,
            }));
        var item = new AuthoritativeWorldItem(
            100,
            1,
            2,
            0,
            42,
            25,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var world = new DiabloWorldState(geometry, worldItems: [item]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            levelId: 1,
            mana: 2000,
            manaMaximum: 2000,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                100,
                2,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2000, player.Mana);
        Assert.Same(item, Assert.Single(world.WorldItems.Values));
    }

    [Fact]
    public void DirectSpellRejectsNativeBlockLightTargetBeforeSpendingMana()
    {
        var geometry = new AuthoritativeWorld();
        var pieces = new ushort[5];
        pieces[1] = 7;
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            5,
            1,
            nativePieceLayer: pieces,
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [7] = AuthoritativeDunTileFlags.BlockLight,
            }));
        var target = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            20,
            levelId: 1);
        var world = new DiabloWorldState(geometry, combatTargets: [target]);
        var directSpell = new AuthoritativeSpellDefinition(77, 1, 0, 0, 0, 0) {
            Range = 10,
            DamageType = AuthoritativeDamageType.Magic,
            DamageAmount = 5,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Targeted,
            MinimumManaCost = 1,
        };
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([directSpell]));
        var player = CreatePlayer(
            entityId: 7,
            levelId: 1,
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(77, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                77,
                9,
                2,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(10000, player.Mana);
        Assert.False(target.IsPetrified);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void DirectSpellRejectsTargetOutsidePlayerLightRadiusBeforeSpendingMana()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            20,
            1,
            nativeTransparency: Enumerable.Repeat((ushort)1, 20)));
        var target = new AuthoritativeCombatTarget(
            9,
            11,
            0,
            20,
            levelId: 1);
        var world = new DiabloWorldState(geometry, combatTargets: [target]);
        var directSpell = new AuthoritativeSpellDefinition(77, 1, 0, 0, 0, 0) {
            Range = 20,
            DamageType = AuthoritativeDamageType.Magic,
            DamageAmount = 5,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Targeted,
            MinimumManaCost = 1,
        };
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([directSpell]));
        var player = CreatePlayer(
            entityId: 7,
            levelId: 1,
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(77, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                77,
                9,
                11,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(10000, player.Mana);
        Assert.Equal(20, target.HitPoints);
    }

    [Fact]
    public void TelekinesisRejectsTargetOutsidePlayerLightRadiusBeforeSpendingMana()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            20,
            1,
            nativeTransparency: Enumerable.Repeat((ushort)1, 20)));
        var item = new AuthoritativeWorldItem(
            100,
            1,
            11,
            0,
            42,
            25,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var world = new DiabloWorldState(geometry, worldItems: [item]);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTelekinesisSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            levelId: 1,
            mana: 2000,
            manaMaximum: 2000,
            memorizedSpells: [new MemorizedSpellState(33, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                33,
                100,
                11,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2000, player.Mana);
        Assert.Same(item, Assert.Single(world.WorldItems.Values));
    }

    [Fact]
    public void TownPortalCreatesAReversibleServerOwnedPortalAndExpires()
    {
        var world = new DiabloWorldState(fallbackWidth: 80, fallbackHeight: 80);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateTownPortalSpell() with {
                    TownPortalLifetime = 7,
                    TownPortalDestinationPositionX = 70,
                    TownPortalDestinationPositionY = 42,
                    TownPortalDestinationSlotCount = 3,
                    TownPortalDestinationSlotSpacing = 3,
                },
            ]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            levelId: 1,
            entityId: 7,
            mana: 3000,
            manaMaximum: 3000,
            memorizedSpells: [new MemorizedSpellState(39, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(
                39,
                0,
                12,
                10,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded);
        Assert.Equal(760, player.Mana);
        var portal = Assert.Single(world.Portals.Values);
        Assert.Equal((12, 10), (portal.SourcePositionX, portal.SourcePositionY));
        Assert.Equal((70, 42), (portal.DestinationPositionX, portal.DestinationPositionY));
        Assert.Equal(7U, portal.OwnerEntityId);
        Assert.True(portal.Bidirectional);
        Assert.Equal(7U, portal.RemainingTicks);

        player.PositionX = portal.SourcePositionX;
        player.PositionY = portal.SourcePositionY;
        Assert.True(worldCommands.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            2,
            [player]).Succeeded);
        Assert.Equal((0U, 70, 42), (player.LevelId, player.PositionX, player.PositionY));

        Assert.True(worldCommands.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            3,
            [player]).Succeeded);
        Assert.Equal((1U, 12, 10), (player.LevelId, player.PositionX, player.PositionY));

        processor.AdvanceTick(2, [player]);
        processor.AdvanceTick(102, [player]);
        Assert.Empty(world.Portals);
    }

    [Fact]
    public void ActiveWorldRulesOverrideSpellRowTownPortalGeometry()
    {
        var world = new DiabloWorldState(fallbackWidth: 80, fallbackHeight: 80);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var worldRules = AuthoritativeWorldRules.LoadTsv(
            "world_rules.tsv",
            "town_level_id\tadria_store_id\ttown_portal_destination_position_x\t"
                + "town_portal_destination_position_y\ttown_portal_destination_slot_count\t"
                + "town_portal_destination_slot_spacing\n0\t10\t60\t41\t2\t4\n");
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateTownPortalSpell() with {
                    TownPortalDestinationPositionX = 70,
                    TownPortalDestinationPositionY = 42,
                    TownPortalDestinationSlotCount = 3,
                    TownPortalDestinationSlotSpacing = 3,
                },
            ]),
            worldCommands: worldCommands,
            worldRules: worldRules);
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            levelId: 1,
            entityId: 7,
            mana: 3000,
            manaMaximum: 3000,
            memorizedSpells: [new MemorizedSpellState(39, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(39, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded);
        var portal = Assert.Single(world.Portals.Values);
        Assert.Equal((60, 41), (portal.DestinationPositionX, portal.DestinationPositionY));
    }

    [Fact]
    public void TownPortalRejectsTownCastingBeforeSpendingMana()
    {
        var world = new DiabloWorldState(fallbackWidth: 80, fallbackHeight: 80);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTownPortalSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            entityId: 7,
            mana: 3000,
            manaMaximum: 3000,
            memorizedSpells: [new MemorizedSpellState(39, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                39,
                0,
                12,
                10,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(3000, player.Mana);
        Assert.Empty(world.Portals);
    }

    [Fact]
    public void TownPortalRejectsOutOfLevelRawCoordinatesBeforeDistanceArithmetic()
    {
        var world = new DiabloWorldState(fallbackWidth: 80, fallbackHeight: 80);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTownPortalSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            levelId: 1,
            entityId: 7,
            mana: 3000,
            manaMaximum: 3000,
            memorizedSpells: [new MemorizedSpellState(39, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                39,
                0,
                int.MinValue,
                int.MinValue,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(3000, player.Mana);
        Assert.Empty(world.Portals);
    }

    [Fact]
    public void AttackAppliesDamageExperienceDropAndOrderedEvents()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            8,
            armorClass: 2,
            dropItemEntityId: 1009,
            dropItemSeed: 6001,
            dropItemPrice: 25);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(entityId: 7);
        var otherPlayer = CreatePlayer(
            positionX: 9,
            positionY: 1,
            entityId: 8);

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(9),
            1,
            [player, otherPlayer]);

        Assert.True(result.Succeeded);
        Assert.Equal(0, target.HitPoints);
        Assert.Equal(100U, player.Experience);
        Assert.Equal(2, result.Events.Count);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 9, 8),
            result.Events[0]);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Experience, 7, 7, 100),
            result.Events[1]);
        var drop = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1009U, drop.EntityId);
        Assert.Equal(6001U, drop.ItemSeed);
        Assert.NotEqual((9, 1), (drop.PositionX, drop.PositionY));
        Assert.Contains(9U, world.Corpses.Keys);
    }

    [Fact]
    public void AttackCanResolveAnAdjacentPlayerThroughTheAuthoritativePath()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: true));
        var attacker = CreatePlayer(
            positionX: 0,
            positionY: 0,
            entityId: 7,
            life: 3200,
            lifeMaximum: 3200);
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 0,
            entityId: 8,
            life: 1280,
            lifeMaximum: 1280);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(640, victim.Life);
        var damage = Assert.Single(result.Events);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 8, 10),
            damage);
    }

    [Fact]
    public void MeleeCellAttackBreaksAnAdjacentBreakableObject()
    {
        var breakable = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            breakable: true);
        var weapon = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            Durability = 4,
            MaxDurability = 4,
            StatFlag = true,
        };
        var world = new DiabloWorldState(objects: [breakable]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, weapon)]);

        var result = processor.Execute(
            player,
            new DiabloAttackAtIntent(1, 0),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(breakable.Destroyed);
        Assert.Equal(DiabloPlayerDirection.SouthEast, player.FacingDirection);
        Assert.Equal(3, Assert.Single(player.Equipment).State.Durability);
    }

    [Fact]
    public void BarbarianCellAttackCleaveHitsASideMonsterWhenPrimaryCellIsEmpty()
    {
        var side = new AuthoritativeCombatTarget(10, 1, 1, 6400);
        var classes = LoadShippedPlayerClasses();
        var configuredClasses = new AuthoritativePlayerClassCatalog(
            classes.Definitions.Select(definition => definition.ClassId == 5
                ? definition with { CriticalStrikeChancePerLevelPercent = 0 }
                : definition));
        var world = new DiabloWorldState(combatTargets: [side]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            playerClasses: configuredClasses);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 5,
            characterLevel: 30,
            attributes: configuredClasses.GetRequired(5).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemType = 2,
                    ItemClass = 1,
                    EquipLocation = 2,
                    MinDamage = 10,
                    MaxDamage = 10,
                    Durability = 4,
                    MaxDurability = 4,
                    StatFlag = true,
                })]);

        var result = processor.Execute(
            player,
            new DiabloAttackAtIntent(1, 0),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(6392, side.HitPoints);
        var damage = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal(7U, damage.SourceEntityId);
        Assert.Equal(side.EntityId, damage.TargetEntityId);
        Assert.Equal(8, damage.Amount);
    }

    [Fact]
    public void BarbarianCleaveAppliesHellfireElementalDamageToASideMonster()
    {
        var primary = new AuthoritativeCombatTarget(9, 1, 0, 6400);
        var side = new AuthoritativeCombatTarget(
            10,
            1,
            1,
            6400,
            fireResistance: 50,
            lightningResistance: 25);
        var classes = LoadShippedPlayerClasses();
        var configuredClasses = new AuthoritativePlayerClassCatalog(
            classes.Definitions.Select(definition => definition.ClassId == 5
                ? definition with { CriticalStrikeChancePerLevelPercent = 0 }
                : definition));
        var world = new DiabloWorldState(combatTargets: [primary, side]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire),
            random: new DiabloRandom(0),
            playerClasses: configuredClasses);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 5,
            characterLevel: 30,
            attributes: configuredClasses.GetRequired(5).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemType = 2,
                    ItemClass = 1,
                    EquipLocation = 2,
                    MinDamage = 10,
                    MaxDamage = 10,
                    Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag
                        | AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                    FireMinDamage = 4,
                    FireMaxDamage = 4,
                    LightningMinDamage = 8,
                    LightningMaxDamage = 8,
                    StatFlag = true,
                })]);

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(primary.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(408560, side.HitPointsQ6);
        Assert.Equal(
            [528, 128, 384],
            result.Events
                .Where(gameplayEvent => gameplayEvent.TargetEntityId == side.EntityId)
                .Select(gameplayEvent => gameplayEvent.AmountQ6));
    }

    [Fact]
    public void BarbarianCellAttackCleaveHitsASideMonsterAfterBreakingThePrimaryObject()
    {
        var breakable = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            breakable: true);
        var side = new AuthoritativeCombatTarget(10, 1, 1, 6400);
        var classes = LoadShippedPlayerClasses();
        var configuredClasses = new AuthoritativePlayerClassCatalog(
            classes.Definitions.Select(definition => definition.ClassId == 5
                ? definition with { CriticalStrikeChancePerLevelPercent = 0 }
                : definition));
        var world = new DiabloWorldState(
            objects: [breakable],
            combatTargets: [side]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire),
            random: new DiabloRandom(0),
            playerClasses: configuredClasses);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 5,
            characterLevel: 30,
            attributes: configuredClasses.GetRequired(5).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemType = 2,
                    ItemClass = 1,
                    EquipLocation = 2,
                    MinDamage = 10,
                    MaxDamage = 10,
                    Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag
                        | AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                    FireMinDamage = 4,
                    FireMaxDamage = 4,
                    LightningMinDamage = 8,
                    LightningMaxDamage = 8,
                    Durability = 4,
                    MaxDurability = 4,
                    StatFlag = true,
                })]);

        var result = processor.Execute(
            player,
            new DiabloAttackAtIntent(1, 0),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(breakable.Destroyed);
        Assert.Equal(408304, side.HitPointsQ6);
        Assert.Equal(
            [528, 256, 512],
            result.Events
                .Where(gameplayEvent => gameplayEvent.TargetEntityId == side.EntityId)
                .Select(gameplayEvent => gameplayEvent.AmountQ6));
    }

    [Fact]
    public void TimedCellAttackUpdatesFacingBeforeAnimationAdmission()
    {
        var player = CreatePlayer(entityId: 7);
        var processor = CreateProcessor(
            new DiabloWorldState(),
            playerClasses: LoadShippedPlayerClasses(),
            useNativePlayerActionTiming: true);

        var result = processor.Execute(
            player,
            new DiabloAttackAtIntent(1, 0),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(DiabloPlayerDirection.SouthEast, player.FacingDirection);
        Assert.True(player.CurrentAction?.TargetCell);
    }

    [Fact]
    public void SuccessfulMeleeAttackConsumesNativeWeaponDurability()
    {
        var weapon = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            Durability = 4,
            MaxDurability = 4,
            StatFlag = true,
        };
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, weapon)]);

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(target.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var equipped = Assert.Single(player.Equipment);
        Assert.Equal(3, equipped.State.Durability);
    }

    [Fact]
    public void MissedMeleeAttackDoesNotConsumeNativeWeaponDurability()
    {
        var weapon = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            Durability = 4,
            MaxDurability = 4,
            StatFlag = true,
        };
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100, hitChancePercent: 0),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, weapon)]);

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(target.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(4, Assert.Single(player.Equipment).State.Durability);
    }

    [Fact]
    public void BrokenMeleeWeaponIsRemovedAndRecalculatesEquipmentResources()
    {
        var weapon = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            Durability = 1,
            MaxDurability = 1,
            StatFlag = true,
            PlusHitPoints = 5,
        };
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            life: 50,
            lifeMaximum: 50,
            equipment: [new EquippedStoreItem(4, 42, weapon)]);
        player.RecalculateEquipmentResources();
        var lifeWithWeapon = player.LifeMaximum;

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(target.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Equipment);
        Assert.Equal(lifeWithWeapon - 5, player.LifeMaximum);
    }

    [Fact]
    public void IndestructiblePrimaryWeaponPreservesNativeOffhandBoundary()
    {
        var indestructibleWeapon = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            Durability = 255,
            MaxDurability = 255,
            StatFlag = true,
        };
        var offhandWeapon = indestructibleWeapon with {
            Durability = 4,
            MaxDurability = 4,
        };
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [
                new EquippedStoreItem(4, 42, indestructibleWeapon),
                new EquippedStoreItem(5, 43, offhandWeapon),
            ]);

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(target.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(255, player.Equipment.Single(item => item.Slot == 4).State.Durability);
        Assert.Equal(4, player.Equipment.Single(item => item.Slot == 5).State.Durability);
    }

    [Fact]
    public void UnarmedMeleeAttackDamagesAStandaloneShield()
    {
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            MinDamage = 1,
            MaxDamage = 1,
            Durability = 4,
            MaxDurability = 4,
            StatFlag = true,
        };
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(5, 42, shield)]);

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(target.EntityId),
            1,
            [player]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(3, Assert.Single(player.Equipment).State.Durability);
    }

    [Fact]
    public void BarbarianAxeAttackUsesConfiguredStrengthDamageDivisor()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: true,
                barbarianWeaponStrengthDamageDivisor: 50),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses());
        var axe = AuthoritativeItemState.Empty with {
            ItemType = 2,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            StatFlag = true,
        };
        var attacker = CreatePlayer(
            positionX: 0,
            positionY: 0,
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            characterClassId: 5,
            characterLevel: 10,
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(100, 100),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(20, 20)),
            equipment: [new EquippedStoreItem(4, 42, axe)]);
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 0,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(23, Assert.Single(result.Events).Amount);
        Assert.Equal(4928, victim.Life);
    }

    [Fact]
    public void BarbarianMeleeCleaveAlsoHitsASideMonsterAfterAPlayerPrimaryTarget()
    {
        var side = new AuthoritativeCombatTarget(9, 1, 1, 100);
        var world = new DiabloWorldState(combatTargets: [side]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                hitChancePercent: 100,
                friendlyFire: true),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses());
        var axe = AuthoritativeItemState.Empty with {
            ItemType = 2,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            StatFlag = true,
        };
        var attacker = CreatePlayer(
            entityId: 7,
            characterClassId: 5,
            equipment: [new EquippedStoreItem(4, 42, axe)]);
        var victim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Contains(
            result.Events,
            gameplayEvent => gameplayEvent.TargetEntityId == side.EntityId
                && gameplayEvent.AmountQ6 > 0);
        Assert.True(side.HitPointsQ6 < side.MaxHitPointsQ6);
    }

    [Fact]
    public void ShieldedPlayerTargetBlocksAnAdjacentMeleeAttack()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var attacker = CreatePlayer(
            positionX: 0,
            positionY: 0,
            entityId: 7,
            life: 1280,
            lifeMaximum: 1280);
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 0,
            entityId: 8,
            life: 1280,
            lifeMaximum: 1280,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(result.Events);
        Assert.Equal(1280, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);
        Assert.Equal(5UL, victim.CombatModeEndTick);
    }

    [Fact]
    public void BarbarianCleaveStillHitsASideMonsterWhenThePlayerPrimaryTargetBlocks()
    {
        var side = new AuthoritativeCombatTarget(9, 1, 1, 100);
        var world = new DiabloWorldState(combatTargets: [side]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                hitChancePercent: 100,
                friendlyFire: true),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses());
        var axe = AuthoritativeItemState.Empty with {
            ItemType = 2,
            ItemClass = 1,
            MinDamage = 1,
            MaxDamage = 1,
            StatFlag = true,
        };
        var attacker = CreatePlayer(
            entityId: 7,
            characterClassId: 5,
            equipment: [new EquippedStoreItem(4, 42, axe)]);
        var victim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(
                5,
                99,
                AuthoritativeItemState.Empty with {
                    ItemType = 5,
                    StatFlag = true,
                })]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);
        Assert.Contains(
            result.Events,
            gameplayEvent => gameplayEvent.TargetEntityId == side.EntityId
                && gameplayEvent.AmountQ6 > 0);
        Assert.True(side.HitPointsQ6 < side.MaxHitPointsQ6);
    }

    [Fact]
    public void PlayerBlockUsesConfiguredLevelDifferenceScale()
    {
        var classes = LoadShippedPlayerClasses();
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };

        var defaultVictim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            characterClassId: 2,
            characterLevel: 1,
            life: 1280,
            lifeMaximum: 1280,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(40, 40),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);
        var defaultAttacker = CreatePlayer(
            entityId: 7,
            characterLevel: 10);
        var defaultProcessor = CreateProcessor(
            new DiabloWorldState(),
            random: new DiabloRandom(0),
            playerClasses: classes);
        var defaultResult = defaultProcessor.Execute(
            defaultAttacker,
            new DiabloAttackIntent(defaultVictim.EntityId),
            1,
            [defaultAttacker, defaultVictim]);

        Assert.True(defaultResult.Succeeded, defaultResult.Failure?.ToString());
        Assert.NotEmpty(defaultResult.Events);

        var tunedVictim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            characterClassId: 2,
            characterLevel: 1,
            life: 1280,
            lifeMaximum: 1280,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(40, 40),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);
        var tunedAttacker = CreatePlayer(
            entityId: 7,
            characterLevel: 10);
        var tunedProcessor = CreateProcessor(
            new DiabloWorldState(),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                pvpBlockLevelScale: 0),
            random: new DiabloRandom(0),
            playerClasses: classes);
        var tunedResult = tunedProcessor.Execute(
            tunedAttacker,
            new DiabloAttackIntent(tunedVictim.EntityId),
            1,
            [tunedAttacker, tunedVictim]);

        Assert.True(tunedResult.Succeeded, tunedResult.Failure?.ToString());
        Assert.Empty(tunedResult.Events);
        Assert.Equal(DiabloPlayerCombatMode.Block, tunedVictim.CombatMode);
    }

    [Fact]
    public void FriendlyModeRejectsPlayerAttackWhenRulesDisableFriendlyFire()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: false));
        var attacker = CreatePlayer(entityId: 7);
        var victim = CreatePlayer(positionX: 1, entityId: 8);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(50, victim.Life);
    }

    [Fact]
    public void NativeTimedPlayerAttackResolvesAtItsAuthoritativeEffectTick()
    {
        var world = new DiabloWorldState();
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            playerClasses: classes,
            useNativePlayerActionTiming: true);
        var attacker = CreatePlayer(
            positionX: 0,
            positionY: 0,
            entityId: 7,
            characterClassId: 2,
            life: 3200,
            lifeMaximum: 3200);
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 0,
            entityId: 8,
            life: 1280,
            lifeMaximum: 1280);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.NotNull(attacker.CurrentAction);
        var action = attacker.CurrentAction!;
        Assert.Equal(DiabloPlayerActionKind.Attack, action.Kind);
        Assert.Equal(victim.EntityId, action.TargetEntityId);
        Assert.Equal(1280, victim.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 1; tick <= action.EffectTick; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [attacker, victim]));

        Assert.Equal(640, victim.Life);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 8, 10),
            delivery.Event);
    }

    [Fact]
    public void PlayerMeleeAttackIgnoresArmorPiercingAndTargetArmorForNativePvpDamage()
    {
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            new DiabloWorldState(),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            playerClasses: classes,
            gameMode: DiabloGameMode.Multiplayer);
        var attacker = CreatePlayer(
            entityId: 7,
            characterClassId: 0,
            attributes: classes.GetRequired(0).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    PlusEnemyArmorClass = 100,
                    StatFlag = true,
                })]);
        var target = CreatePlayer(
            positionX: 1,
            entityId: 8,
            armorClass: 2,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(target.EntityId),
            2,
            [attacker, target]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(2, target.ArmorClass);
        Assert.NotEmpty(result.Events);
        Assert.Equal(10, result.Events[0].Amount);
        Assert.Equal(5760, target.Life);
    }

    [Fact]
    public void NativeTimedMeleeAttackUsesConfiguredFastestAttackTierSkip()
    {
        var weapon = AuthoritativeItemState.Empty with {
            ItemType = 1,
            ItemClass = 1,
            StatFlag = true,
            Flags = 1U << 20,
        };
        var defaultAttacker = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            equipment: [new EquippedStoreItem(4, 42, weapon)]);
        var defaultVictim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 1280,
            lifeMaximum: 1280);
        var defaultProcessor = CreateProcessor(
            new DiabloWorldState(),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            playerClasses: LoadShippedPlayerClasses(),
            useNativePlayerActionTiming: true);
        var defaultResult = defaultProcessor.Execute(
            defaultAttacker,
            new DiabloAttackIntent(defaultVictim.EntityId),
            1,
            [defaultAttacker, defaultVictim]);

        var tunedAttacker = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            equipment: [new EquippedStoreItem(4, 42, weapon)]);
        var tunedVictim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 1280,
            lifeMaximum: 1280);
        var tunedProcessor = CreateProcessor(
            new DiabloWorldState(),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: true,
                fastestAttackFrameSkipWithoutFirstFrame: 4),
            playerClasses: LoadShippedPlayerClasses(),
            useNativePlayerActionTiming: true);
        var tunedResult = tunedProcessor.Execute(
            tunedAttacker,
            new DiabloAttackIntent(tunedVictim.EntityId),
            1,
            [tunedAttacker, tunedVictim]);

        Assert.True(defaultResult.Succeeded, defaultResult.Failure?.ToString());
        Assert.True(tunedResult.Succeeded, tunedResult.Failure?.ToString());
        var defaultAction = Assert.IsType<AuthoritativePlayerActionState>(defaultAttacker.CurrentAction);
        var tunedAction = Assert.IsType<AuthoritativePlayerActionState>(tunedAttacker.CurrentAction);
        Assert.Equal(defaultAction.EffectTick - 2, tunedAction.EffectTick);
        Assert.Equal(defaultAction.EndTick - 2, tunedAction.EndTick);
    }

    [Fact]
    public void NativeTimedPlayerAttackPublishesAPlayerBlockAtItsEffectTick()
    {
        var world = new DiabloWorldState();
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            random: new DiabloRandom(0),
            playerClasses: classes,
            useNativePlayerActionTiming: true);
        var attacker = CreatePlayer(
            positionX: 0,
            positionY: 0,
            entityId: 7,
            characterClassId: 2,
            life: 1280,
            lifeMaximum: 1280);
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 0,
            entityId: 8,
            characterClassId: 0,
            life: 1280,
            lifeMaximum: 1280,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var action = Assert.IsType<AuthoritativePlayerActionState>(attacker.CurrentAction);
        Assert.Equal(1280, victim.Life);

        for (ulong tick = 1; tick < action.EffectTick; tick++)
            processor.AdvanceTick(tick, [attacker, victim]);

        processor.AdvanceTick(action.EffectTick, [attacker, victim]);

        Assert.Equal(1280, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);
        Assert.Equal(action.EffectTick + 4, victim.CombatModeEndTick);
    }

    [Fact]
    public void NativeTimedBowAttackPublishesAPlayerBlockAtArrowImpact()
    {
        var bow = AuthoritativeItemState.Empty with {
            ItemIndex = 88,
            ItemType = 1,
            ItemClass = 1,
            MinDamage = 10,
            MaxDamage = 10,
            StatFlag = true,
        };
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var world = new DiabloWorldState();
        var classes = LoadShippedPlayerClasses();
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\n"
                + "7\t3\t1\t1\tBow\t88\n");
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            random: new DiabloRandom(0),
            playerClasses: classes,
            items: items,
            gameMode: DiabloGameMode.Multiplayer,
            useNativePlayerActionTiming: true);
        var attacker = CreatePlayer(
            entityId: 7,
            characterClassId: 1,
            equipment: [new EquippedStoreItem(4, 42, bow)]);
        var victim = CreatePlayer(
            positionX: 3,
            entityId: 8,
            characterClassId: 0,
            life: 6400,
            lifeMaximum: 6400,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var action = Assert.IsType<AuthoritativePlayerActionState>(attacker.CurrentAction);
        Assert.Equal(6400, victim.Life);

        var blocked = false;
        for (ulong tick = 1; tick <= action.EndTick + 8; tick++) {
            processor.AdvanceTick(tick, [attacker, victim]);
            if (victim.CombatMode == DiabloPlayerCombatMode.Block) {
                blocked = true;
                break;
            }
        }

        Assert.True(blocked);
        Assert.Equal(6400, victim.Life);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void LethalAdjacentPlayerAttackCreatesTheAuthoritativePvpEar()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            playerClasses: LoadShippedPlayerClasses(),
            gameMode: DiabloGameMode.Multiplayer,
            items: LoadShippedItems());
        var attacker = CreatePlayer(entityId: 7, characterClassId: 2);
        var victim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 640,
            lifeMaximum: 640,
            characterClassId: 1,
            characterLevel: 3,
            characterName: "Dead-RogueDM",
            gold: 0);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(0, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Death, victim.CombatMode);
        var ear = Assert.Single(world.WorldItems.Values);
        Assert.Equal(23, ear.State.ItemIndex);
        Assert.Equal(44, ear.State.MiscId);
        Assert.Equal("Dead-RogueDM", ear.State.IdentifiedName);
    }

    [Fact]
    public void KnockbackEquipmentDisplacesALiveAdjacentPlayerTarget()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(world);
        var attacker = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentAttackEffectRules.KnockbackFlag,
                })]);
        var victim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 1280,
            lifeMaximum: 1280);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(640, victim.Life);
        Assert.Equal((2, 0), (victim.PositionX, victim.PositionY));
    }

    [Fact]
    public void ElementalEquipmentDamagesPlayerTargetsThroughTheirResistances()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var attacker = CreatePlayer(
            entityId: 7,
            equipment: [
                new EquippedStoreItem(
                    4,
                    42,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
                        FireMinDamage = 4,
                        FireMaxDamage = 4,
                    }),
                new EquippedStoreItem(
                    5,
                    43,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Flags = AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                        LightningMinDamage = 8,
                        LightningMaxDamage = 8,
                    }),
            ]);
        var victim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400,
            equipment: [new EquippedStoreItem(
                0,
                44,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusFireResistance = 50,
                })]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(5120, victim.Life);
        Assert.Equal(
            [
                (DiabloGameplayEventKind.Damage, 10, 640),
                (DiabloGameplayEventKind.Damage, 2, 128),
                (DiabloGameplayEventKind.Damage, 8, 512),
            ],
            result.Events.Select(gameplayEvent => (
                gameplayEvent.Kind,
                gameplayEvent.Amount,
                gameplayEvent.AmountQ6)));
    }

    [Fact]
    public void NaturalResistanceUsesConfiguredClassPercentage()
    {
        var shippedClasses = LoadShippedPlayerClasses();
        var classes = new AuthoritativePlayerClassCatalog(
            shippedClasses.Definitions.Select(definition => definition.ClassId == 5
                ? definition with { NaturalResistancePerLevelPercent = 50 }
                : definition));
        var processor = CreateProcessor(
            new DiabloWorldState(),
            random: new DiabloRandom(0),
            playerClasses: classes);
        var attacker = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
                    FireMinDamage = 100,
                    FireMaxDamage = 100,
                })]);
        var victim = CreatePlayer(
            positionX: 1,
            entityId: 8,
            characterClassId: 5,
            characterLevel: 50,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Contains(
            result.Events,
            gameplayEvent => gameplayEvent.Kind == DiabloGameplayEventKind.Damage
                && gameplayEvent.Amount == 75);
    }

    [Fact]
    public void BowAttackCanResolveAPlayerTargetAtAuthoritativeArrowImpact()
    {
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            MinDamage = 10,
            MaxDamage = 10,
            StatFlag = true,
        };
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true));
        var attacker = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, bow)]);
        var victim = CreatePlayer(
            positionX: 3,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(result.Events);
        Assert.Equal(6400, victim.Life);
        Assert.Single(world.Projectiles.Values);

        var deliveries = processor.AdvanceTick(1, [attacker, victim]).ToList();
        deliveries.AddRange(processor.AdvanceTick(2, [attacker, victim]));
        deliveries.AddRange(processor.AdvanceTick(3, [attacker, victim]));

        Assert.Empty(world.Projectiles);
        Assert.Equal(5760, victim.Life);
        var delivery = Assert.Single(deliveries);
        Assert.Same(attacker, delivery.Recipient);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 8, 10),
            delivery.Event);
    }

    [Fact]
    public void NonRoguePlayerArrowHalvesNativeClassDamageModifier()
    {
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            MinDamage = 10,
            MaxDamage = 10,
            StatFlag = true,
        };
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            new DiabloWorldState(),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            playerClasses: classes,
            gameMode: DiabloGameMode.Multiplayer);
        var attacker = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 20,
            attributes: classes.GetRequired(2).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(4, 42, bow)]);
        var victim = CreatePlayer(
            positionX: 3,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(result.Events);
        Assert.Equal(6400, victim.Life);

        var deliveries = processor.AdvanceTick(1, [attacker, victim]).ToList();
        deliveries.AddRange(processor.AdvanceTick(2, [attacker, victim]));
        deliveries.AddRange(processor.AdvanceTick(3, [attacker, victim]));

        Assert.Equal(5696, victim.Life);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 8, 11),
            delivery.Event);
    }

    [Fact]
    public void InitializedMonsterBowAttackUsesNativeDistanceHitPenalty()
    {
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            MinDamage = 10,
            MaxDamage = 10,
            StatFlag = true,
        };
        var classes = LoadShippedPlayerClasses();
        var target = new AuthoritativeCombatTarget(
            9,
            10,
            0,
            100,
            armorClass: 20);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            playerClasses: classes);
        var attacker = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 1,
            attributes: classes.GetRequired(2).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(4, 42, bow)]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(target.EntityId),
            4);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.False(projectile.AttackHit);
        Assert.Equal(100, target.HitPoints);
    }

    [Fact]
    public void MultipleArrowsUsesConfiguredContentCountAgainstAPlayer()
    {
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            MinDamage = 10,
            MaxDamage = 10,
            StatFlag = true,
            Flags = AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag,
        };
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: true,
                multipleArrowsCount: 2,
                multipleArrowsDelayTicks: 1,
                multipleArrowsSpreadDistance: 0));
        var attacker = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, bow)]);
        var victim = CreatePlayer(
            positionX: 3,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(2, world.Projectiles.Count);
        Assert.Equal(
            [0U, 1U],
            world.Projectiles.Values
                .OrderBy(projectile => projectile.EntityId)
                .Select(projectile => projectile.ActivationDelayTicks)
                .ToArray());
        Assert.All(
            world.Projectiles.Values,
            projectile => Assert.Equal((0, 0), (projectile.PositionX, projectile.PositionY)));
    }

    [Fact]
    public void ShieldedPlayerTargetBlocksAnAuthoritativeBowImpact()
    {
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            MinDamage = 10,
            MaxDamage = 10,
            StatFlag = true,
        };
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var world = new DiabloWorldState();
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var attacker = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, bow)]);
        var victim = CreatePlayer(
            positionX: 3,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(result.Events);
        Assert.Single(world.Projectiles);

        var deliveries = processor.AdvanceTick(1, [attacker, victim]).ToList();
        deliveries.AddRange(processor.AdvanceTick(2, [attacker, victim]));
        deliveries.AddRange(processor.AdvanceTick(3, [attacker, victim]));

        Assert.Empty(world.Projectiles);
        Assert.Empty(deliveries);
        Assert.Equal(6400, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);
        Assert.Equal(7UL, victim.CombatModeEndTick);
    }

    [Fact]
    public void HellfireBlockedBowImpactRetainsItsProjectileImpactPhase()
    {
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            MinDamage = 10,
            MaxDamage = 10,
            StatFlag = true,
        };
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire),
            random: new DiabloRandom(0));
        var attacker = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, bow)]);
        var victim = CreatePlayer(
            positionX: 3,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        var result = processor.Execute(
            attacker,
            new DiabloAttackIntent(victim.EntityId),
            1,
            [attacker, victim]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        processor.AdvanceTick(1, [attacker, victim]);
        processor.AdvanceTick(2, [attacker, victim]);
        processor.AdvanceTick(3, [attacker, victim]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(6400, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);

        processor.AdvanceTick(4, [attacker, victim]);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void NormalBowAttackUsesOneAuthoritativeMissileAndResolvesOnImpact()
    {
        var target = new AuthoritativeCombatTarget(9, 8, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var processor = CreateProcessor(world);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.WeaponArrow, projectile.Behavior);
        Assert.Equal(640, projectile.DamageQ6);
        Assert.Equal(5000, target.HitPoints);

        processor.AdvanceTick(1, [player]);
        processor.AdvanceTick(2, [player]);
        Assert.Equal(5000, target.HitPoints);
        Assert.Same(projectile, Assert.Single(world.Projectiles.Values));

        var deliveries = processor.AdvanceTick(3, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(4990, target.HitPoints);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal(10, damage.Amount);
        Assert.Equal(640, damage.AmountQ6);
    }

    [Fact]
    public void PlayerArrowResolvesTheFirstCombatantOnItsTravelPath()
    {
        var endpoint = new AuthoritativeCombatTarget(9, 8, 5, 5000);
        var intervening = new AuthoritativeCombatTarget(10, 6, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [endpoint, intervening]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var processor = CreateProcessor(world);

        var result = processor.Execute(player, new DiabloAttackIntent(endpoint.EntityId), 1);

        Assert.True(result.Succeeded);
        Assert.Single(world.Projectiles);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(4990, intervening.HitPoints);
        Assert.Equal(5000, endpoint.HitPoints);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal(10, damage.Amount);
        Assert.Equal(640, damage.AmountQ6);
        Assert.Equal(intervening.EntityId, damage.TargetEntityId);
    }

    [Fact]
    public void PlayerArrowMissesWhenItsDeclaredEndpointTargetLeavesThePath()
    {
        var target = new AuthoritativeCombatTarget(9, 8, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var processor = CreateProcessor(world);

        var result = processor.Execute(player, new DiabloAttackIntent(target.EntityId), 1);

        Assert.True(result.Succeeded);
        target.PositionY = 6;

        processor.AdvanceTick(1, [player]);
        processor.AdvanceTick(2, [player]);
        processor.AdvanceTick(3, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(5000, target.HitPoints);
    }

    [Fact]
    public void RandomArrowVelocityUsesAResumableNativeSpeedAndTravelRemainder()
    {
        var target = new AuthoritativeCombatTarget(9, 10, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
            Flags = AuthoritativeEquipmentAttackEffectRules.RandomArrowVelocityFlag,
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0x12345678));

        var result = processor.Execute(player, new DiabloAttackIntent(target.EntityId), 1);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.InRange(projectile.Speed, 16, 47);
        var travelTiles = 4;
        var expectedLifetime = (uint)((travelTiles * 32 + projectile.Speed - 1) / projectile.Speed + 1);
        Assert.Equal(expectedLifetime, projectile.RemainingTicks);
        Assert.Equal(0, projectile.PathTicks);

        processor.AdvanceTick(1, [player]);

        var expectedSteps = projectile.Speed / 32;
        Assert.Equal(5 + expectedSteps, projectile.PositionX);
        Assert.Equal(projectile.Speed % 32, projectile.PathTicks);
    }

    [Fact]
    public void HellfireArrowVelocityIncludesClassAndAttackSpeedModifiers()
    {
        var target = new AuthoritativeCombatTarget(9, 15, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
            // Native QuickAttack and FastestAttack item flags.
            Flags = (1U << 17) | (1U << 20),
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            characterClassId: 1,
            characterLevel: 12,
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire),
            playerClasses: LoadShippedPlayerClasses());

        var result = processor.Execute(player, new DiabloAttackIntent(target.EntityId), 1);

        Assert.True(result.Succeeded);
        var projectile = Assert.Single(world.Projectiles.Values);
        // 32 base + (12 - 1) / 4 Rogue + 1 Quick + 8 Fastest.
        Assert.Equal(43, projectile.Speed);
        Assert.Equal(8U, projectile.RemainingTicks);

        processor.AdvanceTick(1, [player]);

        Assert.Equal((6, 5), (projectile.PositionX, projectile.PositionY));
        Assert.Equal(11, projectile.PathTicks);
    }

    [Fact]
    public void ArrowVelocityUsesConfiguredClassSpeedDivisor()
    {
        var target = new AuthoritativeCombatTarget(9, 15, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            characterClassId: 1,
            characterLevel: 12,
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var shippedClasses = LoadShippedPlayerClasses();
        var configuredClasses = new AuthoritativePlayerClassCatalog(
            shippedClasses.Definitions.Select(definition =>
                definition.Symbol == "rogue"
                    ? definition with { RangedArrowSpeedLevelDivisor = 2 }
                    : definition));
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire),
            playerClasses: configuredClasses);

        var result = processor.Execute(player, new DiabloAttackIntent(target.EntityId), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(37, Assert.Single(world.Projectiles.Values).Speed);
    }

    [Fact]
    public void PlayerArrowBreaksABlockingObjectBeforeRemoval()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            breakable: true);
        var arrow = new AuthoritativeProjectile(
            0x80000000,
            7,
            8,
            0,
            0,
            0,
            0,
            3,
            0,
            10,
            AuthoritativeDamageType.Physical,
            0,
            3,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
            Speed = 1,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            projectiles: [arrow]);
        var processor = CreateProcessor(world);
        var owner = CreatePlayer(entityId: 7);
        var target = CreatePlayer(entityId: 8, positionX: 3);

        processor.AdvanceTick(1, [owner, target]);

        Assert.Equal((0, 0), (arrow.PositionX, arrow.PositionY));
        Assert.True(blocker.Destroyed);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultipleArrowsEmitsOneImmediateAndTwoDelayedAuthoritativeMissiles()
    {
        var target = new AuthoritativeCombatTarget(9, 8, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
            Flags = AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag,
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var processor = CreateProcessor(world);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(3, world.Projectiles.Count);
        Assert.All(
            world.Projectiles.Values,
            projectile => Assert.Equal(
                AuthoritativeProjectileBehavior.WeaponArrow,
                projectile.Behavior));
        Assert.Equal(
            [0U, 2U, 2U],
            world.Projectiles.Values
                .OrderBy(projectile => projectile.EntityId)
                .Select(projectile => projectile.ActivationDelayTicks)
                .ToArray());
        Assert.All(world.Projectiles.Values, projectile => Assert.Equal(640, projectile.DamageQ6));
        Assert.Equal(5000, target.HitPoints);

        processor.AdvanceTick(1, [player]);
        processor.AdvanceTick(2, [player]);
        processor.AdvanceTick(3, [player]);
        Assert.Equal(2, world.Projectiles.Count);
        Assert.Equal(4990, target.HitPoints);

        processor.AdvanceTick(4, [player]);
        processor.AdvanceTick(5, [player]);
        Assert.Empty(world.Projectiles);
        Assert.Equal(4970, target.HitPoints);
    }

    [Fact]
    public void MultipleArrowsUsesConfiguredContentCount()
    {
        var target = new AuthoritativeCombatTarget(9, 8, 5, 5000);
        var world = new DiabloWorldState(combatTargets: [target]);
        var bow = AuthoritativeItemState.Empty with {
            ItemType = 3,
            ItemClass = 1,
            StatFlag = true,
            Flags = AuthoritativeEquipmentAttackEffectRules.MultipleArrowsFlag,
        };
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(4, 100, bow)]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                multipleArrowsCount: 2,
                multipleArrowsDelayTicks: 1,
                multipleArrowsSpreadDistance: 0));

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(2, world.Projectiles.Count);
        Assert.Equal(
            [0U, 1U],
            world.Projectiles.Values
                .OrderBy(projectile => projectile.EntityId)
                .Select(projectile => projectile.ActivationDelayTicks)
                .ToArray());
        Assert.All(
            world.Projectiles.Values,
            projectile => Assert.Equal((5, 5), (projectile.PositionX, projectile.PositionY)));
    }

    [Fact]
    public void ButcherDeathCompletesItsAuthoredQuest()
    {
        var quest = new AuthoritativeQuestState(
            3007,
            2,
            1,
            active: true,
            completionMonsterId: 53);
        var target = new AuthoritativeCombatTarget(
            16,
            1,
            0,
            8,
            levelId: 2,
            monsterId: 53);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(entityId: 7, levelId: 2);

        var result = processor.Execute(player, new DiabloAttackIntent(16), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, quest.Progress);
        Assert.Empty(world.CombatTargets);
    }

    [Fact]
    public void SkeletonKingDeathCompletesItsAuthoredQuest()
    {
        var quest = new AuthoritativeQuestState(
            3013,
            3,
            1,
            active: true,
            completionMonsterId: 52);
        var target = new AuthoritativeCombatTarget(
            17,
            1,
            0,
            8,
            levelId: 3,
            monsterId: 52);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(entityId: 7, levelId: 3);

        var result = processor.Execute(player, new DiabloAttackIntent(17), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, quest.Progress);
        Assert.Empty(world.CombatTargets);
    }

    [Fact]
    public void DefilerDeathCompletesItsQuestAndLeavesTheCathedralMap()
    {
        var quest = new AuthoritativeQuestState(
            3021,
            17,
            1,
            active: true,
            completionUniqueIdentity: "Defiler",
            completionRewardItemId: 43);
        var target = new AuthoritativeCombatTarget(
            16,
            1,
            0,
            8,
            levelId: 17,
            monsterId: 126,
            unique: true,
            uniqueIdentity: "Defiler");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(
                43,
                5,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                1,
                1,
                true) {
                NativeItemIndex = 42,
                MiscId = 43,
                ItemClass = 5,
            },
        ]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            items: items);
        var player = CreatePlayer(entityId: 7, levelId: 17);
        var otherPlayer = CreatePlayer(
            positionX: 16,
            positionY: 1,
            levelId: 17,
            entityId: 8);

        var result = processor.Execute(
            player,
            new DiabloAttackIntent(16),
            1,
            [player, otherPlayer]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, quest.Progress);
        var map = Assert.Single(world.WorldItems.Values);
        Assert.Equal(43, map.State.MiscId);
        Assert.Equal(42, map.State.ItemIndex);
        Assert.NotEqual(0U, map.ItemSeed);
        Assert.Equal((1, 0, 17U), (map.PositionX, map.PositionY, map.LevelId));
    }

    [Fact]
    public void QuestCompletionRewardUsesDynamicSpellBookGeneration()
    {
        var quest = new AuthoritativeQuestState(
            3021,
            17,
            1,
            active: true,
            completionUniqueIdentity: "Defiler",
            completionRewardItemId: 114);
        var target = new AuthoritativeCombatTarget(
            16,
            1,
            0,
            8,
            levelId: 17,
            monsterId: 126,
            unique: true,
            uniqueIdentity: "Defiler");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            items: items,
            spellBooks: books);
        var player = CreatePlayer(entityId: 7, levelId: 17);

        var result = processor.Execute(player, new DiabloAttackIntent(16), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var reward = Assert.Single(world.WorldItems.Values);
        Assert.Equal(7, reward.State.SpellId);
        Assert.Equal(DiabloItemActionKind.ReadBook, reward.State.ItemActionKind);
        Assert.Equal("Book of Firebolt", reward.State.BaseName);
        Assert.NotEqual((16, 1), (reward.PositionX, reward.PositionY));
    }

    [Fact]
    public void UniqueQuestDefeatAppliesItsNativeCompletionVariables()
    {
        var quest = new AuthoritativeQuestState(
            3003,
            0,
            1,
            active: true,
            completionUniqueIdentity: "Gharbad",
            completionVariable1: 5);
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            levelId: 0,
            monsterId: 126,
            unique: true,
            uniqueIdentity: "Gharbad");
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(entityId: 7);

        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 1).Succeeded);

        Assert.True(quest.Completed);
        Assert.Equal(5, quest.Variable1);
    }

    [Fact]
    public void QuestDefeatActivatesItsValidatedFollowOnQuest()
    {
        var quest = new AuthoritativeQuestState(
            3016,
            0,
            1,
            active: true,
            completionUniqueIdentity: "Lazarus",
            completionVariable1: 7,
            completionActivateQuestId: 3006);
        var nextQuest = new AuthoritativeQuestState(
            3006,
            15,
            1,
            active: false,
            activateOnRoute: false);
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            levelId: 0,
            monsterId: 126,
            unique: true,
            uniqueIdentity: "Lazarus");
        var world = new DiabloWorldState(
            combatTargets: [target],
            quests: [quest, nextQuest]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(entityId: 7);

        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 1).Succeeded);

        Assert.True(quest.Completed);
        Assert.Equal(7, quest.Variable1);
        Assert.True(nextQuest.Active);
    }

    [Fact]
    public void DiabloDefeatCompletesItsContentDeclaredQuest()
    {
        var quest = new AuthoritativeQuestState(
            3006,
            15,
            1,
            active: true,
            completionDiablo: true);
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            levelId: 15,
            monsterId: 112,
            diablo: true);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(levelId: 15, entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.Equal(1U, player.DiabloKillLevel);
        Assert.True(world.VictoryTriggered);
        var victory = Assert.Single(
            result.Events,
            gameplayEvent => gameplayEvent.Kind == DiabloGameplayEventKind.Victory);
        Assert.Equal(7U, victory.SourceEntityId);
        Assert.Equal(9U, victory.TargetEntityId);
        Assert.Equal(1, victory.Amount);
    }

    [Fact]
    public void MultiplayerDiabloDefeatDoesNotSetSinglePlayerHeroRank()
    {
        var quest = new AuthoritativeQuestState(
            3006,
            15,
            1,
            active: true,
            completionDiablo: true);
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            levelId: 15,
            monsterId: 112,
            diablo: true);
        var world = new DiabloWorldState(combatTargets: [target], quests: [quest]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            gameMode: DiabloGameMode.Multiplayer);
        var player = CreatePlayer(levelId: 15, entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.True(quest.Completed);
        Assert.Equal(0U, player.DiabloKillLevel);
        Assert.True(world.VictoryTriggered);
        Assert.Equal(
            1,
            result.Events.Count(gameplayEvent => gameplayEvent.Kind == DiabloGameplayEventKind.Victory));
    }

    [Fact]
    public void HellDifficultySetsTheNativeHeroRankThree()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            1,
            levelId: 15,
            monsterId: 112,
            diablo: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            difficulty: DiabloDifficulty.Hell);
        var player = CreatePlayer(levelId: 15, entityId: 7);

        Assert.True(processor.Execute(player, new DiabloAttackIntent(9), 1).Succeeded);

        Assert.Equal(3U, player.DiabloKillLevel);
    }

    [Theory]
    [InlineData(DiabloDifficulty.Normal, 37U)]
    [InlineData(DiabloDifficulty.Nightmare, 2074U)]
    [InlineData(DiabloDifficulty.Hell, 4148U)]
    public void MonsterExperienceUsesNativeDifficultyScaling(
        DiabloDifficulty difficulty,
        uint expectedExperience)
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            1,
            experienceReward: 37);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            difficulty: difficulty);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(expectedExperience, player.Experience);
        Assert.Equal(
            checked((int)expectedExperience),
            Assert.Single(result.Events, gameplayEvent =>
                gameplayEvent.Kind == DiabloGameplayEventKind.Experience).Amount);
    }

    [Fact]
    public void UniqueHellMonsterExperienceRetainsTheNativeDoubleReward()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            1,
            experienceReward: 37,
            unique: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            difficulty: DiabloDifficulty.Hell);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(8296U, player.Experience);
    }

    [Fact]
    public void HellDifficultyAcceptsNativeAccuracyAboveTheBaseByteRange()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 3,
            aggroRange: 5,
            monsterLevel: 1,
            toHit: 220);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            difficulty: DiabloDifficulty.Hell);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.NotEmpty(deliveries);
        Assert.True(player.Life < 20);
    }

    [Theory]
    [InlineData(DiabloDifficulty.Normal, 3)]
    [InlineData(DiabloDifficulty.Nightmare, 10)]
    [InlineData(DiabloDifficulty.Hell, 18)]
    public void MonsterPhysicalDamageUsesNativeDifficultyScaling(
        DiabloDifficulty difficulty,
        int expectedDamage)
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 3,
            aggroRange: 5,
            monsterLevel: 1,
            toHit: 255);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, difficulty: difficulty);
        var player = CreatePlayer(life: 100, lifeMaximum: 100, entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(100 - expectedDamage, player.Life);
        Assert.Equal(expectedDamage, Assert.Single(deliveries).Event.Amount);
    }

    [Fact]
    public void MonsterPhysicalDamageUsesExternalDifficultyTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 3,
            aggroRange: 5,
            monsterLevel: 1,
            toHit: 255);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                difficultyRules: new DiabloDifficultyRules {
                    HellDamageMultiplier = 3,
                    HellDamageBonus = 1,
                }),
            difficulty: DiabloDifficulty.Hell);
        var player = CreatePlayer(life: 100, lifeMaximum: 100, entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(100 - 10, player.Life);
        Assert.Equal(10, Assert.Single(deliveries).Event.Amount);
    }

    [Theory]
    [InlineData(DiabloDifficulty.Normal, 10)]
    [InlineData(DiabloDifficulty.Hell, 90)]
    public void MonsterArmorUsesNativeDifficultyBonus(
        DiabloDifficulty difficulty,
        int expectedHitPoints)
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 10);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(100, 1, 100),
            difficulty: difficulty);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(expectedHitPoints, target.HitPoints);
    }

    [Fact]
    public void SinglePlayerMonsterMeleeUsesNativeHitAndShieldBlockOrder()
    {
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var victim = CreatePlayer(
            positionX: 1,
            entityId: 7,
            life: 100,
            lifeMaximum: 100,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);
        var target = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            attackDamage: 10,
            aggroRange: 1,
            minDamage: 10,
            maxDamage: 10,
            monsterLevel: 1,
            toHit: 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(world, random: random);

        processor.AdvanceTick(1, [victim]);

        Assert.Equal(100, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);
        var expected = new DiabloRandom(0);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(100);
        Assert.Equal(expected.State, random.State);
    }

    [Fact]
    public void FallenDeathTriggersNativeFearForNearbyFallen()
    {
        var deceased = new AuthoritativeCombatTarget(
            9,
            2,
            2,
            8,
            nativeAi: "Fallen",
            monsterLevel: 1,
            deathAnimationTicks: 3);
        var survivor = new AuthoritativeCombatTarget(
            10,
            4,
            2,
            20,
            nativeAi: "Fallen",
            monsterLevel: 3,
            aggroRange: 0);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 8, 8, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [deceased, survivor]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(positionX: 2, positionY: 1, entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(5, survivor.FallenFearTicks);
        Assert.Equal(7, survivor.FallenFearDirection);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(4, survivor.PositionX);
        Assert.Equal(2, survivor.PositionY);
        Assert.True(survivor.IsMoving);
        Assert.Equal(5, survivor.FuturePositionX);
        Assert.Equal(2, survivor.FuturePositionY);
        Assert.Equal(4, survivor.FallenFearTicks);
    }

    [Fact]
    public void FallenFearUsesValidatedMonsterTypeTuning()
    {
        var deceased = new AuthoritativeCombatTarget(
            9,
            2,
            2,
            8,
            monsterId: 1,
            nativeAi: "Zombie",
            monsterLevel: 1,
            deathAnimationTicks: 3);
        var survivor = new AuthoritativeCombatTarget(
            10,
            3,
            2,
            20,
            monsterId: 1,
            nativeAi: "Zombie",
            monsterLevel: 3,
            aggroRange: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Fallen",
                1,
                16,
                1,
                1,
                4,
                0,
                1,
                3,
                0,
                5,
                false,
                false,
                false) {
                NativeAi = "Zombie",
                FallenFearRadius = 1,
                FallenFearLevelOffset = 4,
                FallenFearMinimumTicks = 1,
            },
        ]);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 8, 8, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [deceased, survivor]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            monsterTypes: monsterTypes);
        var player = CreatePlayer(positionX: 2, positionY: 1, entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(1, survivor.FallenFearTicks);

        processor.AdvanceTick(1, [player]);

        Assert.True(survivor.IsMoving);
        Assert.Equal(0, survivor.FallenFearTicks);
    }

    [Fact]
    public void MultiplayerFallenFearConsumesNativeRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var survivor = new AuthoritativeCombatTarget(
            9,
            4,
            2,
            20,
            nativeAi: "Fallen",
            fallenFearTicks: 1,
            fallenFearDirection: 7,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 8, 8, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [survivor]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [CreatePlayer()]);

        Assert.Equal(expectedAiSeed, survivor.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(survivor.IsMoving);
        Assert.Equal((5, 2), (survivor.FuturePositionX, survivor.FuturePositionY));
        Assert.Equal(0, survivor.FallenFearTicks);
    }

    [Fact]
    public void SinglePlayerFallenFearConsumesNativeRandomWalkRolls()
    {
        var survivor = new AuthoritativeCombatTarget(
            9,
            4,
            2,
            20,
            nativeAi: "Fallen",
            fallenFearTicks: 1,
            fallenFearDirection: 7);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 8, 8, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [survivor]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var expected = new DiabloRandom(0xCAFEBABE);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [CreatePlayer()]);

        Assert.Equal(expected.State, random.State);
        Assert.True(survivor.IsMoving);
        Assert.Equal((5, 2), (survivor.FuturePositionX, survivor.FuturePositionY));
        Assert.Equal(0, survivor.FallenFearTicks);
    }

    [Fact]
    public void FallenStandRallyHealsAndRalliesNearbyFallen()
    {
        var leader = new AuthoritativeCombatTarget(
            9,
            2,
            2,
            10,
            maxHitPoints: 20,
            nativeAi: "Fallen",
            intelligence: 2,
            initialStandAnimationFrame: 10,
            initialStandAnimationTick: 2);
        var nearby = new AuthoritativeCombatTarget(
            10,
            4,
            2,
            10,
            maxHitPoints: 20,
            nativeAi: "Fallen",
            intelligence: 1);
        var distant = new AuthoritativeCombatTarget(
            11,
            11,
            11,
            10,
            maxHitPoints: 20,
            nativeAi: "Fallen",
            intelligence: 1);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 16, 16, isDefaultStart: true));
        var world = new DiabloWorldState(
            nativeWorld,
            combatTargets: [leader, nearby, distant]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var expected = new DiabloRandom(0);
        Assert.True(expected.FlipCoin(4));

        processor.AdvanceTick(1, []);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(16, leader.HitPoints);
        Assert.Equal(165, leader.FallenRallyTicks);
        Assert.Equal(164, nearby.FallenRallyTicks);
        Assert.Equal(0, distant.FallenRallyTicks);
        Assert.Equal((0, 0), (leader.FallenStandAnimationFrame, leader.FallenStandAnimationTick));
    }

    [Fact]
    public void FallenRallyPursuesPlayersBeyondNormalAggroRange()
    {
        var fallen = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            aggroRange: 0,
            nativeAi: "Fallen",
            fallenRallyTicks: 2);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 8, 8, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [fallen]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(entityId: 7, positionX: 5, positionY: 1);
        var expected = new DiabloRandom(0);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.True(fallen.IsMoving);
        Assert.Equal((2, 1), (fallen.FuturePositionX, fallen.FuturePositionY));
        Assert.Equal(1, fallen.FallenRallyTicks);
    }

    [Fact]
    public void SneakUsesIntelligenceVisibilityBandAndUnhidesWhenApproached()
    {
        var sneak = new AuthoritativeCombatTarget(
            9,
            5,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Sneak",
            intelligence: 2);
        var world = new DiabloWorldState(combatTargets: [sneak]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);
        Assert.True(sneak.SneakHidden);

        player.PositionX = 4;
        var hiddenAttack = processor.Execute(player, new DiabloAttackIntent(9), 2);
        AssertRejected(hiddenAttack, DiabloRuleFailure.InvalidTarget);

        player.PositionX = 3;
        processor.AdvanceTick(3, [player]);

        Assert.False(sneak.SneakHidden);
    }

    [Fact]
    public void SneakRetreatsForNineTicksAfterAHit()
    {
        var sneak = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Sneak",
            intelligence: 2);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [sneak]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(9, sneak.SneakRetreatTicks);

        processor.AdvanceTick(1, [player]);

        Assert.Equal((1, 0), (sneak.PositionX, sneak.PositionY));
        Assert.True(sneak.IsMoving);
        Assert.Equal((2, 0), (sneak.FuturePositionX, sneak.FuturePositionY));
        Assert.Equal(8, sneak.SneakRetreatTicks);
    }

    [Fact]
    public void SneakUsesValidatedMonsterTypeTuning()
    {
        var sneak = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 1,
            aggroRange: 8,
            nativeAi: "Fallen",
            intelligence: 2);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Sneak",
                1,
                16,
                1,
                1,
                20,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                SneakDistanceBase = 8,
                SneakDistanceIntelligenceScale = 2,
                SneakRetreatReleaseDistance = 1,
                SneakRetreatTicks = 4,
            },
        ]);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [sneak]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100),
            monsterTypes: monsterTypes);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(4, sneak.SneakRetreatTicks);
    }

    [Fact]
    public void MultiplayerSneakConsumesItsNativeAdjacentAdmissionRoll()
    {
        const uint aiSeed = 123;
        var sneak = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            nativeAi: "Sneak",
            intelligence: 2,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [sneak]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var actionRoll = expected.RandomIntLessThan(100);
        Assert.Equal(88, actionRoll);
        Assert.True(actionRoll >= 18);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, sneak.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void SinglePlayerInitializedSneakConsumesNativeAdjacentAdmissionRoll()
    {
        const uint initSeed = 1;
        var sneak = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            nativeAi: "Sneak",
            intelligence: 0,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [sneak]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void MultiplayerSneakRetreatConsumesNativeActionAndRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var sneak = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            aggroRange: 5,
            nativeAi: "Sneak",
            intelligence: 2,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        sneak.StartSneakRetreat(1);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var world = new DiabloWorldState(nativeWorld, combatTargets: [sneak]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, sneak.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(0, sneak.SneakRetreatTicks);
        Assert.True(sneak.IsMoving);
        Assert.Equal((2, 0), (sneak.FuturePositionX, sneak.FuturePositionY));
    }

    [Fact]
    public void ScavengerSeeksAndEatsACorpseWithNativeHealingTiming()
    {
        var scavenger = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            4,
            maxHitPoints: 20,
            nativeAi: "Scavenger");
        var corpse = new AuthoritativeCorpse(10, 0, 4, 0);
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [corpse]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);
        Assert.Equal((3, 0), (scavenger.FuturePositionX, scavenger.FuturePositionY));
        processor.AdvanceTick(2, [player]);
        Assert.Equal((4, 0), (scavenger.FuturePositionX, scavenger.FuturePositionY));
        processor.AdvanceTick(3, [player]);

        Assert.Equal(5, scavenger.HitPoints);
        Assert.Equal(7, scavenger.ScavengerHealingTicks);
        Assert.Contains(10U, world.Corpses.Keys);
    }

    [Fact]
    public void MultiplayerScavengerConsumesItsCorpseAndMovementRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var scavenger = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            4,
            maxHitPoints: 20,
            nativeAi: "Scavenger",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [new AuthoritativeCorpse(10, 0, 4, 0)]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, scavenger.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(10U, scavenger.ScavengerCorpseEntityId);
        Assert.Equal((3, 0), (scavenger.FuturePositionX, scavenger.FuturePositionY));
    }

    [Fact]
    public void SinglePlayerInitializedScavengerConsumesNativeCorpseAndMovementRandomWalkRolls()
    {
        const uint initSeed = 7;
        var scavenger = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            4,
            maxHitPoints: 20,
            nativeAi: "Scavenger",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [new AuthoritativeCorpse(10, 0, 4, 0)]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(10U, scavenger.ScavengerCorpseEntityId);
        Assert.Equal((3, 0), (scavenger.FuturePositionX, scavenger.FuturePositionY));
    }

    [Fact]
    public void ScavengerDefersWhenItsNativeCorpseSeekingCandidatesAreBlocked()
    {
        var scavenger = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            4,
            maxHitPoints: 20,
            nativeAi: "Scavenger");
        var corpse = new AuthoritativeCorpse(10, 0, 0, 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2, 42, 43],
            combatTargets: [scavenger],
            corpses: [corpse]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 10,
            positionY: 10,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 10 = 2: native RandomWalk checks (2,0), (2,1), and
        // (3,1); the old eight-way scan would have selected (4,1).
        processor.AdvanceTick(1, [player]);

        Assert.False(scavenger.IsMoving);
        Assert.Equal((3, 0), (scavenger.PositionX, scavenger.PositionY));
    }

    [Fact]
    public void ScavengerUsesNativeCoinSelectedCorpseScanOrder()
    {
        var scavenger = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            4,
            maxHitPoints: 20,
            nativeAi: "Scavenger");
        var nearCorpse = new AuthoritativeCorpse(10, 0, 1, 0);
        var nativeFirstCorpse = new AuthoritativeCorpse(11, 0, 2, 2);
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [nearCorpse, nativeFirstCorpse]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(entityId: 7);

        // Seed zero makes the native FlipCoin(2) choose reverse traversal:
        // (4,4) through (-4,-4), so (2,2) wins before the nearer (1,0).
        processor.AdvanceTick(1, [player]);

        Assert.Equal(11U, scavenger.ScavengerCorpseEntityId);
        Assert.Equal((1, 1), (scavenger.FuturePositionX, scavenger.FuturePositionY));
    }

    [Fact]
    public void HellfireScavengerConsumesItsCorpseWhenFullyHealed()
    {
        var scavenger = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            4,
            maxHitPoints: 20,
            nativeAi: "Scavenger");
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [new AuthoritativeCorpse(10, 0, 2, 0)]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(entityId: 7);

        for (ulong tick = 1; tick <= 7; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Equal(20, scavenger.HitPoints);
        Assert.DoesNotContain(10U, world.Corpses.Keys);
    }

    [Fact]
    public void ScavengerUsesValidatedHealingAndSearchTuning()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                16,
                "Scavenger",
                0,
                0,
                2,
                3,
                6,
                10,
                1,
                5,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                ScavengerHealingDurationTicks = 10,
                ScavengerCorpseSearchRadius = 5,
                ScavengerHellfireHealingDivisor = 4,
                ScavengerBaseHealingAmount = 3,
                ScavengerBaseHealingTargetPercent = 50,
            },
        ]);
        var scavenger = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            4,
            maxHitPoints: 20,
            monsterId: 1,
            nativeAi: "Zombie");
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [new AuthoritativeCorpse(10, 0, 7, 0)]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(entityId: 7);

        for (ulong tick = 1; tick <= 7; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Equal(10, scavenger.HitPoints);
        Assert.Equal(0, scavenger.ScavengerHealingTicks);
        Assert.Contains(10U, world.Corpses.Keys);
    }

    [Fact]
    public void ScavengerUsesValidatedHealingHealthThreshold()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                16,
                "Scavenger",
                0,
                0,
                2,
                3,
                20,
                10,
                1,
                5,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                ScavengerHealingDurationTicks = 10,
                ScavengerCorpseSearchRadius = 5,
                ScavengerHellfireHealingDivisor = 4,
                ScavengerBaseHealingAmount = 3,
                ScavengerBaseHealingTargetPercent = 50,
                ScavengerHealingHealthThresholdPercent = 75,
            },
        ]);
        var scavenger = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            14,
            maxHitPoints: 20,
            monsterId: 1,
            nativeAi: "Zombie");
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [new AuthoritativeCorpse(10, 0, 2, 0)]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(9, scavenger.ScavengerHealingTicks);
        Assert.True(scavenger.IsMoving);
    }

    [Fact]
    public void ScavengerUsesStandaloneHealingThresholdWithNativeHealingDefaults()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                16,
                "Scavenger",
                0,
                0,
                2,
                3,
                20,
                10,
                1,
                5,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                ScavengerHealingHealthThresholdPercent = 75,
            },
        ]);
        var scavenger = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            14,
            maxHitPoints: 20,
            monsterId: 1,
            nativeAi: "Zombie");
        var world = new DiabloWorldState(
            combatTargets: [scavenger],
            corpses: [new AuthoritativeCorpse(10, 0, 2, 0)]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(9, scavenger.ScavengerHealingTicks);
        Assert.True(scavenger.IsMoving);
    }

    [Fact]
    public void SkeletonKingSummonsAValidatedSkeletonMeleeTargetFromTuning()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(1, 10, 10));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2,
                1,
                "Skeleton",
                1,
                1,
                1,
                4,
                4,
                10,
                2,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                AggroRange = 6,
                SkeletonMeleeDelayChanceBase = 35,
                SkeletonMeleeDelayChanceIntelligenceScale = 4,
                SkeletonMeleeAttackChanceBase = 20,
                SkeletonMeleeAttackChanceIntelligenceScale = 2,
                StandAnimationFrames = 8,
                StandAnimationTicksPerFrame = 4,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            nativeWorld,
            monsterTypes,
            generation);
        var king = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            levelId: 1,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 255);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [king]);
        const uint randomSeed = 0x12345678;
        var random = new DiabloRandom(randomSeed);
        var processor = CreateProcessor(
            world,
            random: random,
            monsterSpawnPlanner: planner);
        var player = CreatePlayer(positionX: 5, positionY: 1, levelId: 1, entityId: 7);

        processor.AdvanceTick(1, [player]);

        var summoned = Assert.Single(
            world.CombatTargets.Values,
            target => target.EntityId != king.EntityId);
        Assert.Equal("Fallen", summoned.NativeAi);
        Assert.Equal(1U, summoned.LevelId);
        Assert.Equal((2, 1), (summoned.PositionX, summoned.PositionY));
        var expected = new DiabloRandom(randomSeed);
        _ = expected.RandomIntLessThan(100); // summon admission
        var summonSeed = expected.State;
        _ = expected.RandomIntLessThan(1); // native skeleton type selection
        _ = expected.RandomIntLessThan(3); // stand tick
        _ = expected.RandomIntLessThan(7); // stand frame
        _ = expected.RandomIntLessThan(4); // initial health
        var expectedItemSeed = unchecked((uint)expected.AdvanceRndSeed());
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(
            2 + (int)((summonSeed / 0x100U) % 2U),
            summoned.AttackDamage);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(expectedItemSeed, summoned.MonsterItemSeed);
        Assert.Equal(expectedAiSeed, summoned.MonsterAiSeed);
    }

    [Fact]
    public void SkeletonKingUsesValidatedSummonDistanceTuning()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(1, 10, 10));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2,
                1,
                "Skeleton",
                1,
                1,
                1,
                4,
                4,
                10,
                2,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "SkeletonMelee",
                AggroRange = 6,
            },
            new AuthoritativeMonsterTypeDefinition(
                51,
                2,
                "Skeleton King",
                1,
                1,
                1,
                20,
                20,
                10,
                2,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                SkeletonKingSummonMinimumDistance = 5,
                SkeletonKingSummonChanceBase = 100,
                SkeletonKingSummonChanceIntelligenceScale = 1,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            nativeWorld,
            monsterTypes,
            generation);
        var king = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            levelId: 1,
            monsterId: 51,
            aggroRange: 8,
            nativeAi: "Fallen",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [king]);
        var processor = CreateProcessor(
            world,
            monsterSpawnPlanner: planner,
            monsterTypes: monsterTypes,
            random: new DiabloRandom(0));
        var player = CreatePlayer(positionX: 5, positionY: 1, levelId: 1, entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Single(world.CombatTargets);
        Assert.Equal((1, 2), (king.FuturePositionX, king.FuturePositionY));
    }

    [Fact]
    public void SkeletonKingUsesNativeRoundWalkWhenSummonAdmissionIsUnavailable()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(1, 12, 12));
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            levelId: 1,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [king]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            levelId: 1,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.True(king.IsMoving);
        Assert.NotEqual((5, 6), (king.FuturePositionX, king.FuturePositionY));
    }

    [Fact]
    public void SkeletonKingUsesValidatedRoundWalkChanceModulo()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                51,
                50,
                "Skeleton King",
                4,
                4,
                9,
                140,
                140,
                70,
                6,
                16,
                0,
                0,
                true,
                true,
                false) {
                NativeAi = "SkeletonKing",
                SkeletonKingSummonMinimumDistance = 3,
                SkeletonKingSummonChanceBase = 35,
                SkeletonKingSummonChanceIntelligenceScale = 4,
                SkeletonKingRoundWalkChanceModulo = 1,
            },
        ]);
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            monsterId: 51,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [king]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7);

        // 101 ^ 9 ^ 7 = 111: movement is admitted and the authored modulo
        // selects RoundWalk even though the native modulo-four branch misses.
        processor.AdvanceTick(101, [player]);

        Assert.True(king.IsMoving);
        Assert.NotEqual((5, 6), (king.FuturePositionX, king.FuturePositionY));
    }

    [Fact]
    public void SkeletonKingDefersWhenItsNativeRandomWalkCandidatesAreBlocked()
    {
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [205, 206, 245, 285, 286],
            combatTargets: [king]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7);

        // 101 ^ 9 ^ 7 = 111: movement is admitted, the RoundWalk branch is
        // skipped, and every native RandomWalk candidate is blocked while
        // bounded navigation still has an alternate route.
        processor.AdvanceTick(101, [player]);

        Assert.False(king.IsMoving);
        Assert.Equal((6, 6), (king.PositionX, king.PositionY));
    }

    [Fact]
    public void SkeletonKingPreservesNativeDelayAfterAFailedMovementAdmission()
    {
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [king]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7);

        // 50 ^ 9 ^ 7 = 62: native LeoricAi takes its delay branch and
        // schedules GenerateRnd(10) + 10 ticks before another decision.
        processor.AdvanceTick(50, [player]);

        Assert.False(king.IsMoving);
        Assert.Equal(10, king.SkeletonKingDelayTicks);

        processor.AdvanceTick(51, [player]);

        Assert.Equal(9, king.SkeletonKingDelayTicks);
        Assert.False(king.IsMoving);
    }

    [Fact]
    public void SkeletonKingUsesValidatedDelayTuning()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                51,
                50,
                "Skeleton King",
                4,
                4,
                9,
                140,
                140,
                70,
                6,
                16,
                0,
                0,
                true,
                true,
                false) {
                NativeAi = "SkeletonKing",
                AggroRange = 8,
                SkeletonKingSummonMinimumDistance = 3,
                SkeletonKingSummonChanceBase = 35,
                SkeletonKingSummonChanceIntelligenceScale = 4,
                SkeletonKingDelayBase = 7,
                SkeletonKingDelayRandomRange = 3,
            },
        ]);
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            monsterId: 51,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [king]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7);

        processor.AdvanceTick(50, [player]);

        Assert.False(king.IsMoving);
        Assert.Equal(7, king.SkeletonKingDelayTicks);
    }

    [Fact]
    public void MultiplayerSkeletonKingConsumesNativeActionAndDelayRolls()
    {
        const uint aiSeed = 123;
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [king]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(88, expected.RandomIntLessThan(100));
        Assert.Equal(76, expected.RandomIntLessThan(100));
        Assert.Equal(5, expected.RandomIntLessThan(10));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, king.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.False(king.IsMoving);
        Assert.Equal(15, king.SkeletonKingDelayTicks);
    }

    [Fact]
    public void MultiplayerSkeletonKingConsumesNativeRoundWalkRolls()
    {
        const uint aiSeed = 7;
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [king]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        Assert.Equal(10, expected.RandomIntLessThan(100));
        Assert.Equal(0, expected.RandomIntLessThan(4));
        Assert.Equal(0, expected.RandomIntLessThan(2));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, king.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(king.IsMoving);
        Assert.Equal((6, 5), (king.FuturePositionX, king.FuturePositionY));
    }

    [Fact]
    public void SinglePlayerInitializedSkeletonKingConsumesNativeMovementAndDelayRolls()
    {
        const uint initSeed = 123;
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [king]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(100);
        var delayRoll = expected.RandomIntLessThan(10);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.False(king.IsMoving);
        Assert.Equal(10 + delayRoll, king.SkeletonKingDelayTicks);
    }

    [Fact]
    public void MultiplayerSkeletonKingUsesNativeAdjacentAttackAdmission()
    {
        const uint aiSeed = 123;
        var king = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 5,
            aggroRange: 5,
            nativeAi: "SkeletonKing",
            intelligence: 0,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [king]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(88, expected.RandomIntLessThan(100));
        var player = CreatePlayer(
            positionX: 2,
            positionY: 0,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, king.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void SinglePlayerSkeletonKingUsesNativeActionRollForAdjacentAdmission()
    {
        const uint randomSeed = 0x12345678;
        var king = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 5,
            aggroRange: 5,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [king]);
        var random = new DiabloRandom(randomSeed);
        var processor = CreateProcessor(world, random: random);
        var player = CreatePlayer(
            positionX: 2,
            positionY: 0,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);
        var expected = new DiabloRandom(randomSeed);
        Assert.Equal(97, expected.RandomIntLessThan(100));

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.Equal(expected.State, random.State);
    }

    [Fact]
    public void SeededMultiplayerMonsterMeleeUsesNativeDamageRangeRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            armorClass: 0,
            attackDamage: 99,
            aggroRange: 5,
            minDamage: 2,
            maxDamage: 4,
            nativeAi: "Diablo",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var player = CreatePlayer(
            positionX: 2,
            positionY: 0,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var expectedDamage = expected.RandomIntBetween(2, 4);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.InRange(expectedDamage, 2, 4);
        Assert.Equal(100 - expectedDamage, player.Life);
    }

    [Fact]
    public void SinglePlayerMonsterMeleeUsesNativeDamageRangeRoll()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            armorClass: 0,
            attackDamage: 99,
            aggroRange: 5,
            minDamage: 2,
            maxDamage: 4,
            nativeAi: "Diablo");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            positionX: 2,
            positionY: 0,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);
        var expected = new DiabloRandom(0xCAFEBABE);
        var expectedDamage = expected.RandomIntBetween(2, 4);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.InRange(expectedDamage, 2, 4);
        Assert.Equal(100 - expectedDamage, player.Life);
    }

    [Fact]
    public void SinglePlayerMonsterSpecialMeleeUsesNativeDamageRangeRoll()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            aggroRange: 5,
            minDamage: 3,
            maxDamage: 3,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            toHit: 100,
            toHitSpecial: 100,
            nativeAi: "GoatMelee");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            positionX: 2,
            positionY: 0,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);
        var expected = new DiabloRandom(0xCAFEBABE);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
        Assert.InRange(expectedDamage, 8, 20);
        Assert.Equal(100 - expectedDamage, player.Life);
    }

    [Fact]
    public void SinglePlayerMonsterMeleeAppliesGetHitInNativeQ6Units()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 8,
            aggroRange: 5,
            minDamage: 8,
            maxDamage: 8,
            toHit: 100,
            nativeAi: "Diablo");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 2,
            entityId: 7,
            life: 100 * DiabloFixedPointQ6.Scale,
            lifeMaximum: 100 * DiabloFixedPointQ6.Scale,
            equipment: [new EquippedStoreItem(
                0,
                99,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusGetHit = 2,
                })]);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(90 * DiabloFixedPointQ6.Scale, player.Life);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(10, damage.Amount);
        Assert.Equal(10 * DiabloFixedPointQ6.Scale, damage.AmountQ6);
    }

    [Fact]
    public void SkeletonKingUsesValidatedMovementChanceTuning()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                51,
                50,
                "Skeleton King",
                4,
                4,
                9,
                140,
                140,
                70,
                6,
                16,
                0,
                0,
                true,
                true,
                false) {
                NativeAi = "SkeletonKing",
                SkeletonKingMovementChanceBase = 1,
                SkeletonKingMovementChanceIntelligenceScale = 1,
            },
        ]);
        var king = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            20,
            monsterId: 51,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [king]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            entityId: 7);

        // 26 ^ 9 ^ 7 = 20: the configured one-percent movement band rejects
        // this roll and enters the native delay branch.
        processor.AdvanceTick(26, [player]);

        Assert.False(king.IsMoving);
        Assert.Equal(10, king.SkeletonKingDelayTicks);
    }

    [Fact]
    public void SkeletonKingUsesValidatedSummonChanceTuning()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(1, 10, 10));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2,
                1,
                "Skeleton",
                1,
                1,
                1,
                4,
                4,
                10,
                2,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "SkeletonMelee",
                AggroRange = 6,
            },
            new AuthoritativeMonsterTypeDefinition(
                51,
                2,
                "Skeleton King",
                1,
                1,
                1,
                20,
                20,
                10,
                2,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "SkeletonKing",
                SkeletonKingSummonMinimumDistance = 3,
                SkeletonKingSummonChanceBase = 1,
                SkeletonKingSummonChanceIntelligenceScale = 1,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(1, 1, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            nativeWorld,
            monsterTypes,
            generation);
        var king = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            levelId: 1,
            monsterId: 51,
            aggroRange: 8,
            nativeAi: "SkeletonKing",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [king]);
        var processor = CreateProcessor(
            world,
            monsterSpawnPlanner: planner,
            monsterTypes: monsterTypes,
            random: new DiabloRandom(9));
        var player = CreatePlayer(positionX: 5, positionY: 1, levelId: 1, entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Single(world.CombatTargets);
        Assert.Equal((1, 2), (king.FuturePositionX, king.FuturePositionY));
    }

    [Fact]
    public void HorkSpawnUsesContentSelectedAdmissionAndValidatedSpawnIdentity()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(18, 10, 10));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                119,
                117,
                "Hork Spawn",
                18,
                19,
                22,
                30,
                30,
                25,
                10,
                25,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Zombie",
                HorkDemonAttackChanceBase = 28,
                HorkDemonAttackChanceIntelligenceScale = 2,
                HorkDemonSpawnMinimumDistance = 3,
                HorkDemonSpawnChanceBase = 43,
                HorkDemonSpawnChanceIntelligenceScale = 2,
                HorkDemonMovementChanceBase = 33,
                HorkDemonMovementChanceIntelligenceScale = 2,
                AggroRange = 6,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(18, 18, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            nativeWorld,
            monsterTypes,
            generation);
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            120,
            levelId: 18,
            monsterId: 119,
            aggroRange: 8,
            nativeAi: "Zombie",
            intelligence: 0,
            direction: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [hork]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Zombie",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119,
                    ProjectileSpeed: 8,
                    ProjectileLifetime: 9,
                    ProjectileImpactLifetime: 6),
            ]),
            monsterSpawnPlanner: planner,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 5,
            positionY: 1,
            levelId: 18,
            entityId: 7);

        // 1 ^ 9 ^ 7 = 15, inside the content-selected 43% spawn band.
        Assert.Empty(processor.AdvanceTick(1, [player]));
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.HorkSpawn, projectile.Behavior);
        Assert.Equal(119U, projectile.SpellId);
        Assert.Equal((8, 8U), (projectile.Speed, projectile.RemainingTicks));
        Assert.Equal(6, projectile.ImpactLifetime);
        Assert.Equal(0, projectile.OriginalDirection);
        Assert.DoesNotContain(world.CombatTargets.Values, target => target.EntityId != hork.EntityId);

        for (ulong tick = 2; tick <= 9; tick++)
            Assert.Empty(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        var summoned = Assert.Single(world.CombatTargets.Values, target => target.EntityId != hork.EntityId);
        Assert.Equal((119U, 117U), (summoned.MonsterId, monsterTypes.Definitions[0].NativeMonsterId));
        Assert.Equal("Zombie", summoned.NativeAi);
        Assert.Equal(0, summoned.Direction);
        Assert.Equal((2, 1), (summoned.PositionX, summoned.PositionY));
    }

    [Fact]
    public void HorkSpawnChecksItsCurrentTileBeforeSummoning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            100,
            monsterLevel: 10,
            aggroRange: 0);
        var projectile = new AuthoritativeProjectile(
            50,
            monster.EntityId,
            7,
            117,
            0,
            1,
            1,
            1,
            1,
            0,
            AuthoritativeDamageType.Physical,
            0,
            1) {
            Behavior = AuthoritativeProjectileBehavior.HorkSpawn,
        };
        var world = new DiabloWorldState(
            combatTargets: [monster],
            projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 1,
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            characterLevel: 1);

        var deliveries = processor.AdvanceTick(1, [player]).ToList();

        Assert.Empty(world.Projectiles);
        Assert.Equal(6336, player.Life);
        var damage = Assert.Single(
            deliveries.Select(delivery => delivery.Event),
            gameplayEvent => gameplayEvent.Kind == DiabloGameplayEventKind.Damage);
        Assert.Equal(monster.EntityId, damage.SourceEntityId);
        Assert.Equal(player.EntityId, damage.TargetEntityId);
        Assert.Equal(64, damage.AmountQ6);
    }

    [Fact]
    public void HorkSpawnBreaksACurrentTileBlockingObjectBeforeExpirySummon()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            100,
            monsterLevel: 10,
            aggroRange: 0);
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            1,
            breakable: true);
        var projectile = new AuthoritativeProjectile(
            50,
            monster.EntityId,
            7,
            117,
            0,
            1,
            1,
            1,
            1,
            0,
            AuthoritativeDamageType.Physical,
            0,
            2,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.HorkSpawn,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            combatTargets: [monster],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);

        processor.AdvanceTick(1, []);

        Assert.True(blocker.Destroyed);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void HorkSpawnChecksABlockedDestinationOnTheFollowingTick()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            100,
            monsterLevel: 10,
            aggroRange: 0);
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            breakable: true);
        var projectile = new AuthoritativeProjectile(
            50,
            monster.EntityId,
            7,
            117,
            0,
            0,
            0,
            3,
            0,
            0,
            AuthoritativeDamageType.Physical,
            0,
            3,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.HorkSpawn,
            Speed = 8,
            VelocityX = 1 << 21,
            VelocityY = 1 << 20,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            combatTargets: [monster],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);

        processor.AdvanceTick(1, []);

        Assert.Same(projectile, Assert.Single(world.Projectiles.Values));
        Assert.Equal((1, 0), (projectile.PositionX, projectile.PositionY));
        Assert.False(blocker.Destroyed);

        processor.AdvanceTick(2, []);

        Assert.True(blocker.Destroyed);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void HorkSpawnMissKeepsItsControllerForTheNextTick()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            100,
            monsterLevel: 10,
            aggroRange: 0);
        var projectile = new AuthoritativeProjectile(
            50,
            monster.EntityId,
            7,
            117,
            0,
            1,
            1,
            1,
            1,
            0,
            AuthoritativeDamageType.Physical,
            0,
            2) {
            Behavior = AuthoritativeProjectileBehavior.HorkSpawn,
        };
        var world = new DiabloWorldState(
            combatTargets: [monster],
            projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(2));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 1,
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            characterLevel: 1);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Same(projectile, Assert.Single(world.Projectiles.Values));
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.Equal(6400, player.Life);
    }

    [Fact]
    public void HorkSpawnCanBeBlockedByAStandingShieldBearer()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            100,
            monsterLevel: 10,
            aggroRange: 0);
        var projectile = new AuthoritativeProjectile(
            50,
            monster.EntityId,
            7,
            117,
            0,
            1,
            1,
            1,
            1,
            0,
            AuthoritativeDamageType.Physical,
            0,
            1) {
            Behavior = AuthoritativeProjectileBehavior.HorkSpawn,
        };
        var world = new DiabloWorldState(
            combatTargets: [monster],
            projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var player = CreatePlayer(
            positionX: 1,
            positionY: 1,
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            equipment: [new EquippedStoreItem(4, 100, shield)],
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            characterLevel: 1);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Empty(world.Projectiles);
        Assert.Equal(6400, player.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, player.CombatMode);
    }

    [Fact]
    public void HorkDemonUsesValidatedSpawnAdmissionTuning()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(18, 10, 10));
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                119,
                117,
                "Hork Spawn",
                18,
                19,
                22,
                30,
                30,
                25,
                10,
                25,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "SkeletonMelee",
                AggroRange = 6,
            },
            new AuthoritativeMonsterTypeDefinition(
                125,
                123,
                "Hork Demon",
                19,
                19,
                27,
                120,
                160,
                80,
                20,
                35,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "HorkDemon",
                HorkDemonAttackChanceBase = 1,
                HorkDemonAttackChanceIntelligenceScale = 1,
                HorkDemonSpawnMinimumDistance = 3,
                HorkDemonSpawnChanceBase = 1,
                HorkDemonSpawnChanceIntelligenceScale = 1,
                HorkDemonMovementChanceBase = 100,
                HorkDemonMovementChanceIntelligenceScale = 1,
                AggroRange = 8,
            },
        ]);
        var generation = new AuthoritativeLevelGenerationCatalog([
            new AuthoritativeLevelGenerationRule(18, 18, 1, 100, 0),
        ]);
        var planner = new AuthoritativeMonsterSpawnPlanner(
            nativeWorld,
            monsterTypes,
            generation);
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            120,
            levelId: 18,
            monsterId: 125,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [hork]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119,
                    ProjectileSpeed: 8,
                    ProjectileLifetime: 9),
            ]),
            monsterSpawnPlanner: planner,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 5,
            positionY: 1,
            levelId: 18,
            entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.Empty(world.Projectiles);
        Assert.Equal((2, 1), (hork.FuturePositionX, hork.FuturePositionY));
    }

    [Fact]
    public void HorkDemonUsesNativeRoundWalkWhenItsSummonRollIsUnavailable()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(18, 12, 12));
        var hork = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            120,
            levelId: 18,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [hork]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119,
                    ProjectileSpeed: 8,
                    ProjectileLifetime: 9),
            ]));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            levelId: 18,
            entityId: 7);

        // 101 ^ 9 ^ 7 = 107: movement is admitted and the native three-in-four
        // far-distance RoundWalk branch selects the lateral candidate order.
        processor.AdvanceTick(101, [player]);

        Assert.True(hork.IsMoving);
        Assert.NotEqual((5, 6), (hork.FuturePositionX, hork.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void HorkDemonUsesValidatedRoundWalkChanceModulo()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                125,
                123,
                "Hork Demon",
                18,
                19,
                27,
                120,
                160,
                80,
                20,
                35,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "HorkDemon",
                HorkDemonAttackChanceBase = 28,
                HorkDemonAttackChanceIntelligenceScale = 2,
                HorkDemonSpawnMinimumDistance = 3,
                HorkDemonSpawnChanceBase = 43,
                HorkDemonSpawnChanceIntelligenceScale = 2,
                HorkDemonMovementChanceBase = 100,
                HorkDemonMovementChanceIntelligenceScale = 1,
                HorkDemonRoundWalkChanceModulo = 5,
            },
        ]);
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(18, 12, 12));
        var hork = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            120,
            levelId: 18,
            monsterId: 125,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [hork]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119,
                    ProjectileSpeed: 8,
                    ProjectileLifetime: 9),
            ]),
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            levelId: 18,
            entityId: 7);

        // 400 ^ 9 ^ 7 = 414: native modulo-four skips RoundWalk, while the
        // authored modulo-five branch admits the lateral candidate order.
        processor.AdvanceTick(400, [player]);

        Assert.True(hork.IsMoving);
        Assert.NotEqual((5, 6), (hork.FuturePositionX, hork.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void HorkDemonDefersWhenItsNativeMovementCandidatesAreBlocked()
    {
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            120,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2, 41, 42],
            combatTargets: [hork]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119),
            ]));
        var player = CreatePlayer(
            positionX: 7,
            positionY: 0,
            entityId: 7);

        // 1 ^ 9 ^ 7 = 15, inside the native movement band. The east and
        // native lateral candidates are blocked, so no generic path is used.
        processor.AdvanceTick(1, [player]);

        Assert.False(hork.IsMoving);
        Assert.Equal((1, 0), (hork.PositionX, hork.PositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void HorkDemonPreservesNativeDelayAfterAMovementAdmissionFails()
    {
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            120,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [hork]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119)]));
        var player = CreatePlayer(
            positionX: 7,
            positionY: 0,
            entityId: 7);

        // 84 ^ 9 ^ 7 = 90: the native 33% movement admission fails and
        // schedules GenerateRnd(10) + 10 ticks of AiDelay.
        processor.AdvanceTick(84, [player]);

        Assert.False(hork.IsMoving);
        Assert.Equal(10, hork.HorkDemonDelayTicks);

        processor.AdvanceTick(85, [player]);

        Assert.Equal(9, hork.HorkDemonDelayTicks);
        Assert.False(hork.IsMoving);
    }

    [Fact]
    public void MultiplayerHorkDemonConsumesNativeAdmissionAndRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            120,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [hork]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        Assert.Equal(10, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, hork.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(hork.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerHorkDemonConsumesItsNativeDelayRoll()
    {
        const uint aiSeed = 17;
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            120,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [hork]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(48, expected.RandomIntLessThan(100));
        Assert.Equal(72, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, hork.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(10 + delayRoll, hork.HorkDemonDelayTicks);
        Assert.False(hork.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerHorkDemonConsumesItsNativeRoundWalkRolls()
    {
        const uint aiSeed = 7;
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            120,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [hork]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        Assert.Equal(10, expected.RandomIntLessThan(100));
        if (!expected.FlipCoin(4))
            _ = expected.RandomIntLessThan(2);
        else {
            _ = expected.FlipCoin(2);
            _ = expected.FlipCoin(2);
        }
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, hork.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(hork.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void BatUsesNativeAttackBandAndTwoStepRetreat()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            maxHitPoints: 20,
            aggroRange: 6,
            levelId: 0,
            nativeAi: "Bat",
            intelligence: 255);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [bat]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(10, 1, 100));
        var player = CreatePlayer(positionX: 2, positionY: 0, levelId: 0, entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(2, bat.BatRetreatTicks);
        Assert.Equal(7, bat.BatRetreatDirection);
        processor.AdvanceTick(2, [player]);
        Assert.Equal((1, 0), (bat.PositionX, bat.PositionY));
        Assert.Equal((0, 0), (bat.FuturePositionX, bat.FuturePositionY));
        Assert.Equal(1, bat.BatRetreatTicks);

        processor.AdvanceTick(3, [player]);

        Assert.Equal((0, 0), (bat.PositionX, bat.PositionY));
        Assert.Equal((1, 1), (bat.FuturePositionX, bat.FuturePositionY));
        Assert.Equal(0, bat.BatRetreatTicks);
        Assert.Equal(0, bat.BatRetreatDirection);
    }

    [Fact]
    public void BatUsesNativeMovementWithoutAChargeMapping()
    {
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 21);
        var world = new DiabloWorldState(combatTargets: [bat]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7, positionX: 7, positionY: 0);

        // 11 ^ 9 ^ 7 = 5: ordinary Bat movement is admitted without a
        // monster missile row because native BatAi still owns this branch.
        processor.AdvanceTick(11, [player]);

        Assert.True(bat.IsMoving);
        Assert.Equal(1, bat.BatMovementMode);
        Assert.Equal(0, bat.BatMovementIdleTicks);
    }

    [Fact]
    public void MultiplayerBatConsumesItsNativeMovementAndRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 21,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [bat]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            positionX: 7,
            positionY: 0,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, bat.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(bat.IsMoving);
        Assert.Equal(1, bat.BatMovementMode);
        Assert.Equal(0, bat.BatMovementIdleTicks);
    }

    [Fact]
    public void SinglePlayerInitializedBatConsumesNativeChargeAndMovementAdmission()
    {
        const uint initSeed = 1;
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 21,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [bat]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.False(bat.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SinglePlayerInitializedBatConsumesNativeMovementFallbackRandomWalkRolls()
    {
        const uint initSeed = 11;
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 21,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [bat]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.True(bat.IsMoving);
        Assert.Equal(1, bat.BatMovementMode);
        Assert.Equal(0, bat.BatMovementIdleTicks);
    }

    [Fact]
    public void BatSkipsAdjacentAttackOutsideNativeAttackBand()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 6,
            levelId: 0,
            nativeAi: "Bat",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [bat]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(positionX: 2, positionY: 0, levelId: 0, entityId: 7);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(0, bat.BatRetreatTicks);
    }

    [Fact]
    public void MultiplayerBatConsumesItsNativeAdjacentAttackRoll()
    {
        const uint aiSeed = 123;
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 5,
            aggroRange: 6,
            nativeAi: "Bat",
            intelligence: 0,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [bat]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(88, expected.RandomIntLessThan(100));
        var player = CreatePlayer(
            positionX: 2,
            positionY: 0,
            entityId: 7,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, bat.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void MultiplayerBatRetreatConsumesNativeActionAndRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            aggroRange: 6,
            levelId: 0,
            nativeAi: "Bat",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        bat.StartBatRetreat(7, 2);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [bat]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            positionX: 4,
            positionY: 4,
            levelId: 0,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, bat.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(1, bat.BatRetreatTicks);
        Assert.True(bat.IsMoving);
    }

    [Fact]
    public void BatUsesValidatedMonsterTypeAttackTuning()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 1,
            aggroRange: 6,
            levelId: 0,
            nativeAi: "Butcher",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Bat",
                1,
                16,
                1,
                1,
                20,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                BatAttackChanceBase = 20,
                BatAttackChanceIntelligenceScale = 1,
                BatRetreatDurationTicks = 5,
            },
        ]);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [bat]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(positionX: 2, positionY: 0, levelId: 0, entityId: 7);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(5, bat.BatRetreatTicks);
        processor.AdvanceTick(3, [player]);

        Assert.Equal((0, 0), (bat.FuturePositionX, bat.FuturePositionY));
        Assert.Equal(4, bat.BatRetreatTicks);
    }

    [Fact]
    public void BatUsesStandaloneRetreatDurationWithNativeAttackDefaults()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 5, 5, isDefaultStart: true));
        var bat = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 1,
            aggroRange: 6,
            levelId: 0,
            nativeAi: "Butcher",
            intelligence: 23);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Bat",
                1,
                16,
                1,
                1,
                20,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                BatRetreatDurationTicks = 5,
            },
        ]);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [bat]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(positionX: 2, positionY: 0, levelId: 0, entityId: 7);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(5, bat.BatRetreatTicks);
    }

    [Fact]
    public void AttackUsesValidatedMonsterExperienceReward()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            8,
            armorClass: 2,
            experienceReward: 37,
            unique: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                uniqueExperienceMultiplier: 3));
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(111U, player.Experience);
        Assert.Contains(
            result.Events,
            gameplayEvent => gameplayEvent == new DiabloGameplayEvent(
                DiabloGameplayEventKind.Experience,
                7,
                7,
                111));
    }

    [Fact]
    public void AttackAppliesUsableIdentifiedEquipmentCombatBonuses()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            armorClass: 10);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(20, 1, 100, hitChancePercent: 0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [
                new EquippedStoreItem(
                    4,
                    42,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        PlusDamage = 25,
                        PlusDamageModifier = 3,
                        PlusEnemyArmorClass = 100,
                    }),
                new EquippedStoreItem(
                    5,
                    43,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Magical = 1,
                        PlusDamage = 100,
                        PlusToHit = 100,
                        PlusEnemyArmorClass = 100,
                    }),
            ]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(22, target.HitPoints);
    }

    [Fact]
    public void InitializedClassPhysicalHitSubtractsTargetArmorBeforeNativeClamp()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(world, playerClasses: classes);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 0,
            characterLevel: 1,
            attributes: classes.GetRequired(0).CreateStartingAttributes());

        var armored = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(armored.Succeeded);
        Assert.Equal(100, target.HitPoints);

        target.ArmorClass = 0;
        var unarmored = processor.Execute(player, new DiabloAttackIntent(9), 2);

        Assert.True(unarmored.Succeeded);
        Assert.Equal(99, target.HitPoints);
    }

    [Fact]
    public void InitializedClassAttackUsesEquippedNativeDamageRange()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(world, playerClasses: classes);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 1,
            attributes: classes.GetRequired(2).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 4,
                    MaxDamage = 4,
                    StatFlag = true,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 2);

        Assert.True(result.Succeeded);
        Assert.Equal(96, target.HitPoints);
    }

    [Fact]
    public void InitializedSwordAttackUsesNativeUndeadWeaponModifier()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            undead: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(world, playerClasses: classes);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 1,
            attributes: classes.GetRequired(2).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 2);

        Assert.True(result.Succeeded);
        Assert.Equal(95, target.HitPoints);
    }

    [Fact]
    public void InitializedSwordAttackUsesNativeAnimalWeaponModifier()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            animal: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(world, playerClasses: classes);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 1,
            attributes: classes.GetRequired(2).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 2);

        Assert.True(result.Succeeded);
        Assert.Equal(85, target.HitPoints);
    }

    [Fact]
    public void TripleDemonDamageMultipliesOnlyDemonHitsFromUsableEquipment()
    {
        var demon = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 0,
            demon: true);
        var animal = new AuthoritativeCombatTarget(
            10,
            1,
            0,
            100,
            armorClass: 0);
        var world = new DiabloWorldState(combatTargets: [demon, animal]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentAttackEffectRules.TripleDemonDamageFlag,
                })]);

        var demonAttack = processor.Execute(player, new DiabloAttackIntent(9), 1);
        var animalAttack = processor.Execute(player, new DiabloAttackIntent(10), 2);

        Assert.True(demonAttack.Succeeded);
        Assert.True(animalAttack.Succeeded);
        Assert.Equal(70, demon.HitPoints);
        Assert.Equal(90, animal.HitPoints);
    }

    [Fact]
    public void TripleDemonDamageUsesConfiguredMultiplier()
    {
        var demon = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 0,
            demon: true);
        var world = new DiabloWorldState(combatTargets: [demon]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                tripleDemonDamageMultiplier: 5));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentAttackEffectRules.TripleDemonDamageFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(50, demon.HitPoints);
    }

    [Fact]
    public void GargoyleSpecialActivationConsumesTheWakingPlayerHit()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            nativeAi: "Gargoyle",
            gargoyleSpecialActive: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag
                        | AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                    FireMinDamage = 10,
                    FireMaxDamage = 10,
                    LightningMinDamage = 10,
                    LightningMaxDamage = 10,
                    Durability = 4,
                    MaxDurability = 4,
                    StatFlag = true,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(target.EntityId), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(100, target.HitPoints);
        Assert.False(target.GargoyleSpecialActive);
        Assert.Empty(result.Events);
        Assert.Equal(3, Assert.Single(player.Equipment).State.Durability);
    }

    [Fact]
    public void PetrifiedMonsterRejectsPlayerPhysicalDamage()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(target.EntityId), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(100, target.HitPoints);
        Assert.True(target.IsPetrified);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void BarbarianCleaveHitsAnAdjacentMonsterForQuarterDamage()
    {
        var primary = new AuthoritativeCombatTarget(9, 1, 0, 6400);
        var side = new AuthoritativeCombatTarget(10, 1, 1, 6400);
        var classes = LoadShippedPlayerClasses();
        var configuredClasses = new AuthoritativePlayerClassCatalog(
            classes.Definitions.Select(definition => definition.ClassId == 5
                ? definition with { CriticalStrikeChancePerLevelPercent = 0 }
                : definition));
        var world = new DiabloWorldState(combatTargets: [primary, side]);
        var processor = CreateProcessor(
            world,
            playerClasses: configuredClasses);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 5,
            characterLevel: 30,
            attributes: configuredClasses.GetRequired(5).CreateStartingAttributes(),
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemType = 2,
                    ItemClass = 1,
                    EquipLocation = 2,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(primary.EntityId), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(6400, primary.HitPoints);
        Assert.Equal(6392, side.HitPoints);
        var eventItem = Assert.Single(result.Events);
        Assert.Equal(8, eventItem.Amount);
        Assert.Equal(side.EntityId, eventItem.TargetEntityId);
    }

    [Fact]
    public void WarriorCriticalStrikeDoublesPhysicalDamageBeforeArmorAndConsumesItsNativeRoll()
    {
        var criticalTarget = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 4);
        var criticalRandom = new DiabloRandom(0);
        var playerClasses = LoadShippedPlayerClasses();
        var criticalProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [criticalTarget]),
            random: criticalRandom,
            playerClasses: playerClasses);
        var warrior = CreatePlayer(
            entityId: 7,
            characterClassId: 0,
            characterLevel: 50,
            attributes: playerClasses.GetRequired(0).CreateStartingAttributes() with {
                Strength = new PlayerAttributeState(0, 0),
                Vitality = new PlayerAttributeState(0, 0),
            },
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var criticalResult = criticalProcessor.Execute(
            warrior,
            new DiabloAttackIntent(9),
            2);

        Assert.True(criticalResult.Succeeded);
        Assert.Equal(84, criticalTarget.HitPoints);
        Assert.NotEqual(0U, criticalRandom.State);

        var nonCriticalTarget = new AuthoritativeCombatTarget(
            10,
            1,
            0,
            100,
            armorClass: 4);
        var nonCriticalRandom = new DiabloRandom(2);
        var nonCriticalProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [nonCriticalTarget]),
            random: nonCriticalRandom,
            playerClasses: playerClasses);

        var nonCriticalResult = nonCriticalProcessor.Execute(
            warrior,
            new DiabloAttackIntent(10),
            3);

        Assert.True(nonCriticalResult.Succeeded);
        Assert.Equal(94, nonCriticalTarget.HitPoints);
        Assert.NotEqual(0U, nonCriticalRandom.State);

        var rogueTarget = new AuthoritativeCombatTarget(11, 1, 0, 100);
        var rogueRandom = new DiabloRandom(0);
        var rogueProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [rogueTarget]),
            random: rogueRandom,
            playerClasses: playerClasses);
        var rogue = CreatePlayer(
            entityId: 8,
            characterClassId: 1,
            characterLevel: 1,
            attributes: playerClasses.GetRequired(1).CreateStartingAttributes() with {
                Strength = new PlayerAttributeState(0, 0),
            },
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var rogueResult = rogueProcessor.Execute(
            rogue,
            new DiabloAttackIntent(11),
            3);

        Assert.True(rogueResult.Succeeded);
        Assert.Equal(90, rogueTarget.HitPoints);
        // Native PlayerAttack only consumes the critical-strike roll for
        // classes carrying CriticalStrike; Rogue leaves the stream untouched.
        Assert.Equal(0U, rogueRandom.State);
    }

    [Fact]
    public void WarriorCriticalStrikeUsesConfiguredClassMultiplier()
    {
        var shippedClasses = LoadShippedPlayerClasses();
        var configuredClasses = new AuthoritativePlayerClassCatalog(
            shippedClasses.Definitions.Select(definition =>
                definition.ClassId == 0
                    ? definition with { CriticalDamageMultiplier = 3 }
                    : definition));
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 4);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            random: new DiabloRandom(0),
            playerClasses: configuredClasses);
        var warrior = CreatePlayer(
            entityId: 7,
            characterClassId: 0,
            characterLevel: 50,
            attributes: configuredClasses.GetRequired(0).CreateStartingAttributes() with {
                Strength = new PlayerAttributeState(0, 0),
                Vitality = new PlayerAttributeState(0, 0),
            },
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var result = processor.Execute(warrior, new DiabloAttackIntent(9), 2);

        Assert.True(result.Succeeded);
        Assert.Equal(74, target.HitPoints);
    }

    [Fact]
    public void WarriorCriticalStrikeUsesConfiguredChancePerLevel()
    {
        var shippedClasses = LoadShippedPlayerClasses();
        var configuredClasses = new AuthoritativePlayerClassCatalog(
            shippedClasses.Definitions.Select(definition =>
                definition.ClassId == 0
                    ? definition with { CriticalStrikeChancePerLevelPercent = 0 }
                    : definition));
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 4);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            random: new DiabloRandom(0),
            playerClasses: configuredClasses);
        var warrior = CreatePlayer(
            entityId: 7,
            characterClassId: 0,
            characterLevel: 50,
            attributes: configuredClasses.GetRequired(0).CreateStartingAttributes() with {
                Strength = new PlayerAttributeState(0, 0),
                Vitality = new PlayerAttributeState(0, 0),
            },
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemClass = 1,
                    MinDamage = 10,
                    MaxDamage = 10,
                    StatFlag = true,
                })]);

        var result = processor.Execute(warrior, new DiabloAttackIntent(9), 2);

        Assert.True(result.Succeeded);
        Assert.Equal(94, target.HitPoints);
    }

    [Fact]
    public void KnockbackEquipmentMovesOnlySuccessfulNonPetrifiedHits()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentAttackEffectRules.KnockbackFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(90, target.HitPoints);
        Assert.Equal((2, 0), (target.PositionX, target.PositionY));

        var petrified = new AuthoritativeCombatTarget(10, 1, 0, 100, petrified: true);
        var petrifiedWorld = new DiabloWorldState(combatTargets: [petrified]);
        var petrifiedProcessor = CreateProcessor(petrifiedWorld);
        var petrifiedResult = petrifiedProcessor.Execute(
            player,
            new DiabloAttackIntent(10),
            2);

        Assert.True(petrifiedResult.Succeeded);
        Assert.Equal(100, petrified.HitPoints);
        Assert.Equal((1, 0), (petrified.PositionX, petrified.PositionY));
    }

    [Fact]
    public void KnockbackEquipmentDoesNotMoveAHitWhenDestinationIsBlockedOrAttackMisses()
    {
        var blocked = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var blockedWorld = new DiabloWorldState(
            fallbackBlockedCells: [2],
            combatTargets: [blocked]);
        var processor = CreateProcessor(blockedWorld);
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentAttackEffectRules.KnockbackFlag,
                })]);

        var blockedResult = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(blockedResult.Succeeded);
        Assert.Equal((1, 0), (blocked.PositionX, blocked.PositionY));

        var missed = new AuthoritativeCombatTarget(10, 1, 0, 100);
        var missedWorld = new DiabloWorldState(combatTargets: [missed]);
        var missProcessor = CreateProcessor(
            missedWorld,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                hitChancePercent: 0));
        var missResult = missProcessor.Execute(player, new DiabloAttackIntent(10), 2);

        Assert.True(missResult.Succeeded);
        Assert.Equal(100, missed.HitPoints);
        Assert.Equal((1, 0), (missed.PositionX, missed.PositionY));
    }

    [Fact]
    public void AttackAppliesUsableIdentifiedEquipmentResourceGains()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 0);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            world,
            random: random);
        var player = CreatePlayer(
            entityId: 7,
            life: 3000,
            lifeMaximum: 6400,
            mana: 1000,
            manaMaximum: 6400,
            equipment: [
                new EquippedStoreItem(
                    4,
                    42,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Flags = AuthoritativeEquipmentAttackEffectRules.RandomStealLifeFlag
                            | AuthoritativeEquipmentAttackEffectRules.StealLife3Flag
                            | AuthoritativeEquipmentAttackEffectRules.StealMana5Flag,
                    }),
                new EquippedStoreItem(
                    5,
                    43,
                    AuthoritativeItemState.Empty with {
                        StatFlag = false,
                        Flags = AuthoritativeEquipmentAttackEffectRules.StealLife5Flag,
                    }),
                new EquippedStoreItem(
                    6,
                    44,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Magical = 1,
                        Identified = false,
                        Flags = AuthoritativeEquipmentAttackEffectRules.StealMana3Flag,
                    }),
            ]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(90, target.HitPoints);
        Assert.Equal(3019, player.Life);
        Assert.Equal(1032, player.Mana);
        Assert.Equal(1U, random.State);
        var healing = Assert.Single(result.Events, gameplayEvent =>
            gameplayEvent.Kind == DiabloGameplayEventKind.Healing);
        Assert.Equal(0, healing.Amount);
        Assert.Equal(19, healing.AmountQ6);
    }

    [Fact]
    public void AttackAppliesUsableIdentifiedElementalWeaponDamageThroughResistances()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            armorClass: 0,
            fireResistance: 50,
            lightningResistance: 25);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [
                new EquippedStoreItem(
                    4,
                    42,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
                        FireMinDamage = 4,
                        FireMaxDamage = 4,
                    }),
                new EquippedStoreItem(
                    5,
                    43,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        Flags = AuthoritativeEquipmentElementalDamageRules.LightningDamageFlag,
                        LightningMinDamage = 8,
                        LightningMaxDamage = 8,
                    }),
                new EquippedStoreItem(
                    6,
                    44,
                    AuthoritativeItemState.Empty with {
                        StatFlag = false,
                        Flags = AuthoritativeEquipmentElementalDamageRules.FireDamageFlag,
                        FireMinDamage = 100,
                        FireMaxDamage = 100,
                    }),
            ]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(82, target.HitPoints);
        Assert.Equal(
            [
                (DiabloGameplayEventKind.Damage, 10, 640),
                (DiabloGameplayEventKind.Damage, 2, 128),
                (DiabloGameplayEventKind.Damage, 6, 384),
            ],
            result.Events.Select(gameplayEvent => (
                gameplayEvent.Kind,
                gameplayEvent.Amount,
                gameplayEvent.AmountQ6)));
    }

    [Fact]
    public void RejectedAttackDoesNotMutateCombatState()
    {
        var target = new AuthoritativeCombatTarget(9, 4, 4, 10);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(10, target.HitPoints);
        Assert.Equal(0U, player.Experience);
        Assert.Empty(world.WorldItems);
    }

    [Fact]
    public void AreaCastUsesResistanceAndStableEntityOrdering()
    {
        var resistant = new AuthoritativeCombatTarget(9, 2, 0, 20, fireResistance: 50);
        var normal = new AuthoritativeCombatTarget(10, 2, 1, 20);
        var distant = new AuthoritativeCombatTarget(11, 4, 0, 20);
        var world = new DiabloWorldState(combatTargets: [normal, distant, resistant]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) {
                DamageAmount = 10,
                Range = 4,
                AreaRadius = 1,
                DamageType = AuthoritativeDamageType.Fire,
            },
        ]);
        var processor = CreateProcessor(world, spells);
        var player = CreatePlayer(
            mana: 10,
            manaMaximum: 10,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(7, 1)]);

        var result = processor.Execute(player, new DiabloCastIntent(7, 0, 2, 0), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(6, player.Mana);
        Assert.Equal(15, resistant.HitPoints);
        Assert.Equal(10, normal.HitPoints);
        Assert.Equal(20, distant.HitPoints);
        Assert.Equal([9U, 10U], result.Events.Select(@event => @event.TargetEntityId));
        Assert.Equal([5, 10], result.Events.Select(@event => @event.Amount));
    }

    [Fact]
    public void BlockedCastIsRejectedBeforeSpendingMana()
    {
        var level = new AuthoritativeLevel(1, 5, 3, [1 * 5 + 2]);
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(level);
        var target = new AuthoritativeCombatTarget(9, 4, 1, 10, levelId: 1);
        var world = new DiabloWorldState(authoritativeWorld, combatTargets: [target]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(4, 2, 0, 0, 0, 0) {
                DamageAmount = 6,
                Range = 5,
            },
        ]);
        var processor = CreateProcessor(world, spells);
        var player = CreatePlayer(
            positionY: 1,
            levelId: 1,
            mana: 10,
            manaMaximum: 10,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(4, 1)]);

        var result = processor.Execute(player, new DiabloCastIntent(4, 9, 0, 0), 1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(10, player.Mana);
        Assert.Equal(10, target.HitPoints);
    }

    [Fact]
    public void ProjectileResolvesOnFutureTickAndDirectsEventsToOwner()
    {
        var target = new AuthoritativeCombatTarget(9, 2, 0, 20);
        var world = new DiabloWorldState(combatTargets: [target]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) {
                DamageAmount = 12,
                Range = 4,
                ProjectileSpeed = 1,
                ProjectileLifetime = 8,
            },
        ]);
        var processor = CreateProcessor(world, spells);
        var player = CreatePlayer(
            mana: 10,
            manaMaximum: 10,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(7, 1)]);

        var cast = processor.Execute(player, new DiabloCastIntent(7, 9, 0, 0), 1);

        Assert.True(cast.Succeeded);
        Assert.Empty(cast.Events);
        Assert.Single(world.Projectiles);
        Assert.Empty(processor.AdvanceTick(2, [player]));

        var deliveries = processor.AdvanceTick(3, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(8, target.HitPoints);
        var delivery = Assert.Single(deliveries);
        Assert.Same(player, delivery.Recipient);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 9, 12),
            delivery.Event);
    }

    [Fact]
    public void AutonomousMonsterMovesThenAttacksDeterministically()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            10,
            levelId: 0,
            attackDamage: 3,
            aggroRange: 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.Equal(2, monster.PositionX);
        Assert.True(monster.IsMoving);
        Assert.Equal(1, monster.FuturePositionX);

        var deliveries = processor.AdvanceTick(2, [player]);

        Assert.Equal(1, monster.PositionX);
        Assert.False(monster.IsMoving);
        Assert.Equal(17, player.Life);
        var delivery = Assert.Single(deliveries);
        Assert.Same(player, delivery.Recipient);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 9, 7, 3),
            delivery.Event);
    }

    [Fact]
    public void AutonomousMonsterUsesConfiguredReflectDamageRange()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            10,
            levelId: 0,
            attackDamage: 10,
            aggroRange: 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([
                CreateReflectSpell() with {
                    ReflectDamagePercentMinimum = 50,
                    ReflectDamagePercentMaximum = 50,
                },
            ]));
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);
        player.ReflectCharges = 1;

        Assert.Empty(processor.AdvanceTick(1, [player]));
        var deliveries = processor.AdvanceTick(2, [player]);

        Assert.Equal(15, player.Life);
        Assert.Equal(5, monster.HitPoints);
        Assert.Equal(0U, player.ReflectCharges);
        Assert.Equal(2, deliveries.Count);
    }

    [Fact]
    public void ButcherAiUsesNativeRandomWalkBeforeTheSharedMeleeAttack()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            levelId: 0,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "Butcher");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));

        Assert.Empty(processor.AdvanceTick(2, [player]));
        Assert.Equal((1, 0), (monster.FuturePositionX, monster.FuturePositionY));

        var deliveries = processor.AdvanceTick(3, [player]);
        Assert.Equal(17, player.Life);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 9, 7, 3),
            Assert.Single(deliveries).Event);
    }

    [Fact]
    public void ButcherAiDefersWhenItsNativeRandomWalkCandidatesAreBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            40,
            aggroRange: 8,
            nativeAi: "Butcher");
        var world = new DiabloWorldState(
            fallbackBlockedCells: [205, 206, 245, 285, 286],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 6,
            life: 100,
            lifeMaximum: 100);

        // 9 ^ 7 = 14: every native RandomWalk candidate is blocked while
        // bounded navigation has an alternate route.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((6, 6), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void ButcherAiUsesValidatedMonsterIdentityForAnAlternateRuntimeLabel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            levelId: 0,
            monsterId: 1,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "Fallen");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Butcher",
                1,
                16,
                1,
                10,
                10,
                0,
                3,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Butcher",
                AggroRange = 5,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void CompleteAlternateAiTuningTakesPrecedenceOverButcherFallback()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            levelId: 0,
            monsterId: 1,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "Zombie");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Butcher",
                1,
                16,
                1,
                10,
                10,
                0,
                3,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Butcher",
                HorkDemonAttackChanceBase = 1,
                HorkDemonAttackChanceIntelligenceScale = 1,
                HorkDemonSpawnMinimumDistance = 3,
                HorkDemonSpawnChanceBase = 1,
                HorkDemonSpawnChanceIntelligenceScale = 1,
                HorkDemonMovementChanceBase = 100,
                HorkDemonMovementChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void SkeletonMeleeAiUsesItsNativeMovementRandomWalk()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            levelId: 0,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "SkeletonMelee",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        // 1 ^ 9 ^ 7 = 15, inside SkeletonAi's native delay band.
        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.False(monster.IsMoving);

        // Native AiDelay persists for 15 ticks before SkeletonAi can choose
        // another action.
        for (var delayedTick = 40; delayedTick <= 54; delayedTick++)
            Assert.Empty(processor.AdvanceTick((ulong)delayedTick, [player]));

        // 55 ^ 9 ^ 7 = 57, outside the delay band and admitted to movement.
        Assert.Empty(processor.AdvanceTick(55, [player]));
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void SkeletonMeleeAiDefersWhenItsNativeRandomWalkCandidatesAreBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            40,
            aggroRange: 8,
            nativeAi: "SkeletonMelee",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [205, 206, 245, 285, 286],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 6,
            life: 100,
            lifeMaximum: 100);

        // 130 ^ 9 ^ 7 = 140: the native delay roll is cleared and every
        // RandomWalk candidate is blocked while bounded navigation has an
        // alternate route.
        processor.AdvanceTick(130, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((6, 6), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void FallenAiUsesItsNativeSkeletonMovementAndAttackBands()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Fallen",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: the native SkeletonAi delay branch holds Fallen.
        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.False(monster.IsMoving);

        // Native AiDelay persists for 15 ticks before SkeletonAi can choose
        // another action. The direct test harness advances one logical tick
        // per call, even when the deterministic RNG seed jumps.
        for (var delayedTick = 40; delayedTick <= 54; delayedTick++)
            Assert.Empty(processor.AdvanceTick((ulong)delayedTick, [player]));

        // 55 ^ 9 ^ 7 = 57: the delayed SkeletonAi branch now uses RandomWalk.
        Assert.Empty(processor.AdvanceTick(55, [player]));
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));

        player.PositionX = 3;
        player.PositionY = 0;
        player.Life = 100;

        // 41 ^ 9 ^ 7 = 45: outside the native 20% adjacent attack band and
        // starts another ten-tick native delay.
        Assert.Empty(processor.AdvanceTick(41, [player]));
        Assert.Equal(100, player.Life);

        for (var delayedTick = 42; delayedTick <= 51; delayedTick++)
            Assert.Empty(processor.AdvanceTick((ulong)delayedTick, [player]));

        // 1 ^ 9 ^ 7 = 15: inside the native 20% adjacent attack band after
        // the delay expires.
        Assert.Single(processor.AdvanceTick(1, [player]));
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void SkeletonMeleeAiUsesValidatedMonsterTypeDelayTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            monsterId: 1,
            levelId: 0,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "SkeletonMelee",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Skeleton",
                1,
                16,
                1,
                1,
                10,
                0,
                1,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                SkeletonMeleeDelayChanceBase = 10,
                SkeletonMeleeDelayChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void SkeletonMeleeAiUsesValidatedMovementDelayBandTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            monsterId: 1,
            levelId: 0,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "SkeletonMelee",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Skeleton",
                1,
                16,
                1,
                1,
                10,
                0,
                1,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "SkeletonMelee",
                SkeletonMeleeDelayChanceBase = 100,
                SkeletonMeleeDelayChanceIntelligenceScale = 1,
                SkeletonMeleeMovementDelayBase = 9,
                SkeletonMeleeMovementDelayIntelligenceScale = 1,
                SkeletonMeleeMovementDelayRandomRange = 4,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(9, monster.SkeletonMeleeDelayTicks);
    }

    [Fact]
    public void NativeAiFallbackUsesValidatedMonsterIdentityBeforeRuntimeLabel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            monsterId: 1,
            levelId: 0,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "Zombie",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Skeleton",
                1,
                16,
                1,
                1,
                10,
                0,
                1,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "SkeletonMelee",
                AggroRange = 5,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(life: 20, lifeMaximum: 20, entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.False(monster.IsMoving);
        for (var delayedTick = 40; delayedTick <= 54; delayedTick++)
            Assert.Empty(processor.AdvanceTick((ulong)delayedTick, [player]));
        Assert.Empty(processor.AdvanceTick(55, [player]));
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void LachdananRemainsPassiveOutsideQuestDialogue()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            monsterId: 1,
            levelId: 0,
            attackDamage: 12,
            aggroRange: 5,
            nativeAi: "Lachdanan",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Lachdanan",
                1,
                16,
                1,
                1,
                10,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Lachdanan",
                AggroRange = 5,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 1,
            life: 20,
            lifeMaximum: 20,
            entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.Equal(20, player.Life);
        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void FireManWithoutRegisteredNativeAiRemainsPassive()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            monsterId: 1,
            levelId: 0,
            attackDamage: 12,
            aggroRange: 5,
            nativeAi: "FireMan",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "FireMan",
                1,
                16,
                1,
                1,
                10,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "FireMan",
                AggroRange = 5,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            positionX: 1,
            life: 20,
            lifeMaximum: 20,
            entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.Equal(20, player.Life);
        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void AutonomousMonsterRoutesAroundBlockedCorridor()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            7,
            5,
            blockedCells: [2, 9, 16, 23],
            nativeVisionBlockedCells: []));
        // Movement blockers are also explicit vision blockers; keep the
        // monster's native active window open so this test exercises routing
        // rather than visibility wake-up.
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            2,
            10,
            levelId: 1,
            attackDamage: 3,
            aggroRange: 10,
            activeForTicks: byte.MaxValue);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        Assert.Equal(
            [2, 9, 16, 23],
            world.LevelGeometry[0].NativeVisionBlockedCells);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 5,
            positionY: 2,
            levelId: 1,
            life: 20,
            lifeMaximum: 20,
            entityId: 7);

        for (ulong tick = 1; tick <= 8; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.True(
            monster.PositionX > 1
            || monster.FuturePositionX > 1,
            $"The monster should route through the open corridor instead of remaining stuck (current={monster.PositionX},{monster.PositionY}; future={monster.FuturePositionX},{monster.FuturePositionY}; moving={monster.IsMoving}).");
        Assert.True(player.Life < 20, "The routed monster should eventually reach attack range.");
    }

    [Fact]
    public void GeometryBackedWorldDoesNotActivateOccludedMonsters()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            7,
            5,
            blockedCells: [16],
            nativeTransparency: Enumerable.Range(0, 35)
                .Select(cell => (ushort)(cell % 7 >= 3 ? 2 : 1))));
        var monster = new AuthoritativeCombatTarget(
            9,
            4,
            2,
            20,
            levelId: 1,
            attackDamage: 20,
            aggroRange: 10,
            toHit: 255);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 2,
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(50, player.Life);
        Assert.Equal(0, monster.ActiveForTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void MonsterAttackAppliesUsableGetHitEquipmentAdjustment()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            levelId: 0,
            attackDamage: 20,
            aggroRange: 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            life: 50,
            lifeMaximum: 50,
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusGetHit = -5,
                })]);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(35, player.Life);
        Assert.Equal(15, Assert.Single(deliveries).Event.Amount);
    }

    [Fact]
    public void MonsterAttackAppliesNativeThornsAfterThePlayerHit()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            1,
            100,
            levelId: 0,
            maxHitPoints: 100,
            attackDamage: 20,
            aggroRange: 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, random: new DiabloRandom(1));
        var player = CreatePlayer(
            positionY: 0,
            life: 50,
            lifeMaximum: 50,
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentIncomingDamageRules.ThornsFlag,
                })]);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(30, player.Life);
        Assert.InRange(monster.HitPoints, 97, 99);
        Assert.Equal(2, deliveries.Count);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 9, 7, 20),
            deliveries[0].Event);
        Assert.Equal(7U, deliveries[1].Event.SourceEntityId);
        Assert.Equal(9U, deliveries[1].Event.TargetEntityId);
        Assert.InRange(deliveries[1].Event.Amount, 1, 3);
    }

    [Fact]
    public void MonsterAttackUsesConfiguredThornsDamageBounds()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            1,
            100,
            levelId: 0,
            maxHitPoints: 100,
            attackDamage: 20,
            aggroRange: 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(1),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                thornsDamageMinimum: 7,
                thornsDamageMaximum: 7));
        var player = CreatePlayer(
            positionY: 0,
            life: 50,
            lifeMaximum: 50,
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentIncomingDamageRules.ThornsFlag,
                })]);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(7, Assert.Single(deliveries.Skip(1)).Event.Amount);
        Assert.Equal(93, monster.HitPoints);
    }

    [Fact]
    public void DrainLifeAppliesFourQ6PerActiveTickButNotInTown()
    {
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = AuthoritativeEquipmentIncomingDamageRules.DrainLifeFlag,
                }),
        };
        var activePlayer = CreatePlayer(
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7,
            equipment: equipment);
        var activeProcessor = CreateProcessor(new DiabloWorldState());

        var activeEvents = activeProcessor.AdvanceTick(1, [activePlayer]);

        Assert.Equal(46, activePlayer.Life);
        Assert.Equal(
            new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 7, 0) {
                AmountQ6 = 4,
            },
            Assert.Single(activeEvents).Event);

        var townPlayer = CreatePlayer(
            levelId: 0,
            life: 50,
            lifeMaximum: 50,
            entityId: 8,
            equipment: equipment);
        var townProcessor = CreateProcessor(new DiabloWorldState());

        Assert.Empty(townProcessor.AdvanceTick(1, [townPlayer]));
        Assert.Equal(50, townPlayer.Life);
    }

    [Fact]
    public void LethalMonsterAttackDropsAndClearsTheAuthoritativeHeldItem()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            levelId: 0,
            attackDamage: 3,
            aggroRange: 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var heldItem = new OwnedStoreItem(
            0,
            0,
            91,
            25,
            13,
            AuthoritativeItemState.Empty with { ItemType = 1, StatFlag = true });
        var player = CreatePlayer(
            life: 2,
            lifeMaximum: 20,
            entityId: 7,
            heldItem: heldItem);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(0, player.Life);
        Assert.Equal(DiabloPlayerCombatMode.Death, player.CombatMode);
        Assert.Null(player.HeldItem);
        var droppedHeldItem = Assert.Single(
            world.WorldItems.Values,
            item => item.ItemSeed == heldItem.ItemSeed);
        Assert.Equal(heldItem.Price, droppedHeldItem.Price);
        Assert.Equal(heldItem.State, droppedHeldItem.State);
        Assert.Same(player, Assert.Single(deliveries).Recipient);
    }

    [Fact]
    public void MultiplayerSuppressionKeepsGoldButStillDropsTheHeldItem()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            16,
            5,
            5,
            suppressMultiplayerDeathDrops: true));
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            1,
            10,
            levelId: 16,
            attackDamage: 3,
            aggroRange: 5);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var random = new DiabloRandom(123);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var heldItem = new OwnedStoreItem(
            0,
            0,
            91,
            25,
            13,
            AuthoritativeItemState.Empty with { ItemType = 1, StatFlag = true });
        var player = CreatePlayer(
            life: 2,
            lifeMaximum: 20,
            entityId: 7,
            positionX: 1,
            positionY: 1,
            levelId: 16,
            equipment: [new EquippedStoreItem(
                0,
                92,
                AuthoritativeItemState.Empty with { ItemType = 1, StatFlag = true })],
            heldItem: heldItem);

        _ = processor.AdvanceTick(1, [player]);

        Assert.Equal(0, player.Life);
        Assert.Equal(100U, player.Gold);
        Assert.Equal(123U, random.State);
        Assert.Null(player.HeldItem);
        Assert.Single(player.Equipment);
        Assert.Equal(heldItem.ItemSeed, Assert.Single(world.WorldItems.Values).ItemSeed);
    }

    [Fact]
    public void SinglePlayerStillDropsGoldOnASuppressedMultiplayerLevel()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            16,
            5,
            5,
            suppressMultiplayerDeathDrops: true));
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            1,
            10,
            levelId: 16,
            attackDamage: 3,
            aggroRange: 5);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var random = new DiabloRandom(123);
        var processor = CreateProcessor(world, random: random);
        var player = CreatePlayer(
            life: 2,
            lifeMaximum: 20,
            entityId: 7,
            positionX: 1,
            positionY: 1,
            levelId: 16);

        _ = processor.AdvanceTick(1, [player]);

        Assert.Equal(50U, player.Gold);
        Assert.Equal(50U, Assert.Single(world.WorldItems.Values).Price);
        Assert.NotEqual(123U, random.State);
    }

    [Fact]
    public void ChargedBoltIgnoresANormalWalkingMonstersFutureOccupancy()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            20,
            attackDamage: 0,
            aggroRange: 5);
        var projectile = CreateStationaryChargedBolt(1, 0);
        var world = new DiabloWorldState(
            combatTargets: [monster],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.True(monster.IsMoving);
        Assert.Equal(1, monster.FuturePositionX);
        Assert.Equal(20, monster.HitPoints);
        Assert.Equal(AuthoritativeProjectilePhase.Travel, projectile.Phase);
    }

    [Fact]
    public void ChargedBoltHitsAPetrifiedWalkingMonstersFutureOccupancy()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            20,
            moving: true,
            futurePositionX: 1,
            futurePositionY: 0,
            petrified: true);
        var projectile = CreateStationaryChargedBolt(1, 0);
        var world = new DiabloWorldState(
            combatTargets: [monster],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(18, monster.HitPoints);
        Assert.True(monster.IsMoving);
        Assert.True(monster.IsPetrified);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Same(player, Assert.Single(deliveries).Recipient);
    }

    [Fact]
    public void ChargedBoltUsesConfiguredMonsterMagicHitBounds()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            0,
            20,
            petrified: false);
        var world = new DiabloWorldState(
            combatTargets: [monster],
            projectiles: [CreateStationaryChargedBolt(1, 0)]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateChargedBoltSpell() with {
                    ChargedBoltHitMonsterLevelScale = 0,
                    ChargedBoltMinimumHitChance = 0,
                    ChargedBoltMaximumHitChance = 0,
                },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(20, monster.HitPoints);
    }

    [Fact]
    public void MonsterChoosesNearestPlayerThenStableEntityId()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            1,
            10,
            levelId: 0,
            attackDamage: 2,
            aggroRange: 5);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var higherId = CreatePlayer(
            positionX: 1,
            positionY: 0,
            life: 20,
            lifeMaximum: 20,
            entityId: 8);
        var lowerId = CreatePlayer(
            positionX: 1,
            positionY: 2,
            life: 20,
            lifeMaximum: 20,
            entityId: 7);

        var deliveries = processor.AdvanceTick(1, [higherId, lowerId]);

        Assert.Equal(20, higherId.Life);
        Assert.Equal(18, lowerId.Life);
        Assert.Same(lowerId, Assert.Single(deliveries).Recipient);
    }

    [Fact]
    public void FatAiUsesItsNativeSpecialMeleeRangeOnTheSpecialAttackBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 1,
            aggroRange: 5,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            toHitSpecial: 255,
            nativeAi: "Fat");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 30 ^ 9 ^ 7 = 16, inside the native 15..20 special band.
        var deliveries = processor.AdvanceTick(30, [player]);

        var expected = new DiabloRandom(0);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);
        Assert.Equal(100 - expectedDamage, player.Life);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal((9U, 7U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
    }

    [Fact]
    public void FatAiUsesItsNativeNormalBandAndDeclinesOutsideItsAttackBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 3,
            aggroRange: 5,
            toHit: 255,
            nativeAi: "Fat");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 0 ^ 9 ^ 7 = 14, inside Fat's normal-admission boundary.
        Assert.Single(processor.AdvanceTick(0, [player]));
        Assert.Equal(97, player.Life);

        player.Life = 100;
        // 40 ^ 9 ^ 7 = 38, outside Fat's total attack boundary.
        Assert.Empty(processor.AdvanceTick(40, [player]));
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void FatAiUsesValidatedAdjacentAttackTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            monsterId: 1,
            attackDamage: 3,
            aggroRange: 5,
            toHit: 255,
            nativeAi: "Fat");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Overlord",
                1,
                16,
                1,
                1,
                40,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                FatAttackChanceBase = 10,
                FatAttackChanceIntelligenceScale = 1,
                FatSpecialChanceBase = 5,
                FatSpecialChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, outside the configured ten-percent attack band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void FatAiUsesValidatedSpecialBandTuningIndependentOfNativeLabel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            monsterId: 1,
            attackDamage: 3,
            aggroRange: 5,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            toHit: 255,
            toHitSpecial: 255,
            nativeAi: "Fallen");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Overlord",
                1,
                16,
                1,
                1,
                40,
                0,
                1,
                3,
                0,
                20,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                FatAttackChanceBase = 30,
                FatAttackChanceIntelligenceScale = 1,
                FatSpecialChanceBase = 10,
                FatSpecialChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: admitted by 30% and above the configured 10%
        // special lower bound, so the validated special range is selected.
        var deliveries = processor.AdvanceTick(1, [player]);

        var expected = new DiabloRandom(0);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);
        Assert.Equal(100 - expectedDamage, player.Life);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
    }

    [Fact]
    public void FatAiUsesItsNativeNonAdjacentMovementRandomWalk()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "Fat",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: admitted to the native 20% movement band.
        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));

        // 40 ^ 9 ^ 7 = 38: outside the movement band after completion.
        Assert.Empty(processor.AdvanceTick(40, [player]));
        Assert.False(monster.IsMoving);
        Assert.Equal((2, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void FatAiUsesItsNativeFreshMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "Fat",
            intelligence: 0,
            fatMovementIdleTicks: 0,
            fatMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 2,000 ^ 9 ^ 7 = 2,014: movement roll 20 is outside the long-idle
        // 20% band but inside the fresh-mode 70% band.
        processor.AdvanceTick(2_000, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(0, monster.FatMovementIdleTicks);
    }

    [Fact]
    public void FatAiUsesValidatedFreshMovementBonus()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                52,
                "Overlord",
                0,
                0,
                15,
                80,
                120,
                30,
                8,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fat",
                FatMovementChanceBase = 10,
                FatMovementChanceIntelligenceScale = 1,
                FatMovementFreshChanceBonus = 100,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            monsterId: 1,
            nativeAi: "Fat",
            intelligence: 0,
            fatMovementIdleTicks: 0,
            fatMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 2,000 ^ 9 ^ 7 = 2,014: a ten-percent long-idle band plus the
        // validated 100-point fresh bonus admits this movement.
        processor.AdvanceTick(2_000, [player]);

        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void FatAiDefersInsideItsNativeMiddleMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "Fat",
            intelligence: 0,
            fatMovementIdleTicks: 10,
            fatMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(2_000, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.FatMovementIdleTicks);
    }

    [Fact]
    public void FatAiDefersWhenItsNativeRandomWalkCandidatesAreBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            40,
            aggroRange: 8,
            nativeAi: "Fat",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [205, 206, 245, 285, 286],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 6,
            life: 100,
            lifeMaximum: 100);

        // 101 ^ 9 ^ 7 = 111: movement is admitted and every native
        // RandomWalk candidate is blocked while bounded navigation has an
        // alternate route.
        processor.AdvanceTick(101, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((6, 6), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void FatAiUsesValidatedMonsterTypeMovementTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            monsterId: 1,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "Fat",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Overlord",
                1,
                16,
                1,
                1,
                40,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                FatMovementChanceBase = 10,
                FatMovementChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void RhinoAiChargesAcrossClearTilesAndKnocksThePlayerBack()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // tick ^ monster entity ^ player entity = 15, inside Rhino's 43% base band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal((6, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(80, player.Life);
        Assert.Equal((8, 0), (player.PositionX, player.PositionY));
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal((9U, 7U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(20, damage.Amount);
    }

    [Fact]
    public void RhinoAiUsesNativeIntelligenceToExtendItsChargeBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38: above the 43% base band, below 43 + 2 * 3.
        var deliveries = processor.AdvanceTick(40, [player]);

        Assert.Equal((6, 0), (monster.PositionX, monster.PositionY));
        Assert.Single(deliveries);
    }

    [Fact]
    public void MultiplayerRhinoConsumesNativeChargeAdmissionRolls()
    {
        const uint aiSeed = 16;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        Assert.True(expected.FlipCoin(4));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal((6, 0), (monster.PositionX, monster.PositionY));
        Assert.Single(deliveries);
    }

    [Fact]
    public void MultiplayerRhinoConsumesNativeFailedChargeRandomWalkDraws()
    {
        const uint aiSeed = 17;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(48, expected.RandomIntLessThan(100));
        Assert.True(expected.FlipCoin(4));
        Assert.Equal(5, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerRhinoConsumesNativeRecoveryDelayRoll()
    {
        const uint aiSeed = 21;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(85, expected.RandomIntLessThan(100));
        Assert.True(expected.FlipCoin(4));
        Assert.Equal(36, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(10 + delayRoll, monster.RhinoDelayTicks);
        Assert.False(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerRhinoConsumesNativeAdjacentAdmissionWhenItMisses()
    {
        const uint aiSeed = 17;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 5,
            aggroRange: 8,
            toHit: 100,
            nativeAi: "Rhino",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(48, expected.RandomIntLessThan(100));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void MultiplayerRhinoConsumesNativeAdjacentAdmissionWhenItHits()
    {
        const uint aiSeed = 16;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 5,
            aggroRange: 8,
            toHit: 100,
            nativeAi: "Rhino",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntBetween(5, 5);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void RhinoUsesValidatedChargeBoundaryTuning()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                64,
                "Horned Demon",
                0,
                0,
                1,
                4,
                4,
                10,
                2,
                5,
                5,
                32,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                RhinoChargeMinimumDistance = 6,
                RhinoChargeChanceBase = 100,
                RhinoChargeChanceIntelligenceScale = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 1,
            levelId: 0,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Fallen");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 6,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void GargoyleAiHealsFromBeyondItsNativeSafetyBoundaryAndLocksItsSpecial()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            nativeAi: "Gargoyle",
            intelligence: 2);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, so native maxHP / (16 * (15 % 5 + 4)) = 1.
        var healing = processor.AdvanceTick(1, [player]);

        Assert.Equal(41, monster.HitPoints);
        Assert.True(monster.GargoyleSpecialActive);
        var healingEvent = Assert.Single(healing).Event;
        Assert.Equal(DiabloGameplayEventKind.Healing, healingEvent.Kind);
        Assert.Equal((9U, 9U), (healingEvent.SourceEntityId, healingEvent.TargetEntityId));
        Assert.Equal(1, healingEvent.Amount);
        Assert.Equal(64, healingEvent.AmountQ6);

        // Native MFLAG_ALLOW_SPECIAL suppresses further actions while the
        // player remains outside intelligence + 2 tiles.
        Assert.Empty(processor.AdvanceTick(2, [player]));
        Assert.Equal(41, monster.HitPoints);
    }

    [Fact]
    public void SinglePlayerInitializedGargoyleConsumesNativeHealingDivisorRoll()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            nativeAi: "Gargoyle",
            intelligence: 2,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        var healRoll = expected.RandomIntLessThan(5);
        var expectedHealing = 100 / (16 * (healRoll + 4));
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var healing = processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(40 + expectedHealing, monster.HitPoints);
        var healingEvent = Assert.Single(healing).Event;
        Assert.Equal(expectedHealing, healingEvent.Amount);
        Assert.True(monster.GargoyleSpecialActive);
    }

    [Fact]
    public void SinglePlayerInitializedZombieConsumesNativeActionAndDirectionRolls()
    {
        const uint initSeed = 13;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            100,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(8);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 11,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((4, 5), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void GargoyleAiUsesNativeAvoidanceMovementWhenHealthy()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 1,
            aggroRange: 5,
            nativeAi: "Gargoyle",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: native AiAvoidance admits its 28% movement roll
        // and ordinary RandomWalk selects the west candidate (2,0).
        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void SinglePlayerInitializedSkeletonMeleeConsumesNativeGlobalRandomStream()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            aggroRange: 5,
            nativeAi: "SkeletonMelee",
            intelligence: 0,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(1);
        var expected = new DiabloRandom(1);
        Assert.True(expected.RandomIntLessThan(100) >= 35);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void SinglePlayerInitializedGoatMeleeConsumesNativeActionRoll()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            aggroRange: 6,
            nativeAi: "GoatMelee",
            intelligence: 0,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(1);
        var expected = new DiabloRandom(1);
        Assert.True(expected.RandomIntLessThan(100) >= 28);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.False(monster.IsMoving);
        Assert.Equal(0, monster.AvoidanceMovementIdleTicks);
    }

    [Fact]
    public void SinglePlayerInitializedFatConsumesNativeMovementRoll()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            aggroRange: 6,
            nativeAi: "Fat",
            intelligence: 0,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(1);
        var expected = new DiabloRandom(1);
        Assert.True(expected.RandomIntLessThan(100) >= 20);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.False(monster.IsMoving);
        Assert.Equal(0, monster.FatMovementIdleTicks);
    }

    [Fact]
    public void SinglePlayerInitializedHorkDemonConsumesNativeActionAndMovementRolls()
    {
        const uint initSeed = 7;
        var hork = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            120,
            aggroRange: 8,
            nativeAi: "HorkDemon",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [hork]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(100);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "HorkDemon",
                    "HorkSpawn",
                    AuthoritativeProjectileBehavior.HorkSpawn,
                    SpawnMonsterId: 119),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.True(hork.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SinglePlayerInitializedSkeletonRangedConsumesNativeMovementAndAttackRolls()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 3,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(100);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.False(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SinglePlayerInitializedGoatRangedConsumesNativePostShotDelayDraw()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        var delayRoll = expected.RandomIntLessThan(20);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: CreateArrowMissileCatalog("GoatRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(delayRoll, monster.RangedDelayTicks);
        Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow);
    }

    [Fact]
    public void SinglePlayerInitializedSuccubusConsumesNativeBloodStarDelayDraw()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Succubus",
            intelligence: 0,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        var delayRoll = expected.RandomIntLessThan(20);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Succubus",
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar,
                    SpellId: 36,
                    ProjectileSpeed: 16,
                    ProjectileLifetime: 256,
                    ProjectileDamage: 4),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(delayRoll, monster.RangedDelayTicks);
        Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar);
    }

    [Fact]
    public void SinglePlayerInitializedRhinoConsumesNativeAdjacentAdmissionWhenItMisses()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 5,
            aggroRange: 8,
            toHit: 100,
            nativeAi: "Rhino",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(100, player.Life);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void GeometryBackedWorldDoesNotActivateMonstersOutsideNativeVisionRadius()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            25,
            5,
            nativeTransparency: Enumerable.Repeat((ushort)1, 125)));
        var monster = new AuthoritativeCombatTarget(
            9,
            12,
            2,
            20,
            levelId: 1,
            attackDamage: 20,
            aggroRange: 20,
            toHit: 255);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 2,
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(50, player.Life);
        Assert.Equal(0, monster.ActiveForTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void GeometryBackedWorldWakesMonstersWithinNativeVisionRadius()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            25,
            5,
            nativeTransparency: Enumerable.Repeat((ushort)1, 125)));
        var monster = new AuthoritativeCombatTarget(
            9,
            8,
            2,
            20,
            levelId: 1,
            attackDamage: 20,
            aggroRange: 20,
            toHit: 255);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 2,
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(byte.MaxValue, monster.ActiveForTicks);
    }

    [Fact]
    public void GeometryBackedWorldUsesEquipmentLightRadiusForActivation()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            20,
            1,
            nativeTransparency: Enumerable.Repeat((ushort)1, 20)));
        var monster = new AuthoritativeCombatTarget(
            9,
            14,
            0,
            20,
            levelId: 1,
            attackDamage: 20,
            aggroRange: 20,
            toHit: 255);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusLight = 5,
                })]);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(byte.MaxValue, monster.ActiveForTicks);
    }

    [Fact]
    public void GeometryBackedWorldHonorsNativeDiagonalVisionOcclusion()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            5,
            5,
            blockedCells: [1, 5],
            nativeTransparency: Enumerable.Repeat((ushort)1, 25)));
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            2,
            20,
            levelId: 1,
            attackDamage: 20,
            aggroRange: 10,
            toHit: 255);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(0, monster.ActiveForTicks);
        Assert.False(monster.IsMoving);
        Assert.Equal(50, player.Life);
    }

    [Fact]
    public void GeometryBackedWorldHonorsNativeVisionOcclusionWithoutTransparency()
    {
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            5,
            5,
            blockedCells: [1, 5]));
        var monster = new AuthoritativeCombatTarget(
            9,
            2,
            2,
            20,
            levelId: 1,
            attackDamage: 20,
            aggroRange: 10,
            toHit: 255);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(0, monster.ActiveForTicks);
        Assert.False(monster.IsMoving);
        Assert.Equal(50, player.Life);
    }

    [Fact]
    public void GeometryBackedWorldUsesNativeBlockLightForVisionOcclusion()
    {
        var geometry = new AuthoritativeWorld();
        var pieces = new ushort[25];
        pieces[1] = 7;
        geometry.AddLevel(new AuthoritativeLevel(
            1,
            5,
            5,
            nativeTransparency: Enumerable.Repeat((ushort)1, 25),
            nativePieceLayer: pieces,
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [7] = AuthoritativeDunTileFlags.BlockLight,
            }));
        var monster = new AuthoritativeCombatTarget(
            9,
            4,
            0,
            20,
            levelId: 1,
            attackDamage: 20,
            aggroRange: 10,
            toHit: 255);
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            levelId: 1,
            life: 50,
            lifeMaximum: 50,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(0, monster.ActiveForTicks);
        Assert.False(monster.IsMoving);
        Assert.Equal(50, player.Life);
    }

    [Fact]
    public void GargoyleAiUsesContentSelectedAvoidanceMovementWhenHealingMetadataIsPresent()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Gargoyle",
                0,
                0,
                9,
                60,
                90,
                45,
                10,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Gargoyle",
                GargoyleSafeDistanceBase = 2,
                GargoyleSafeDistanceIntelligenceScale = 1,
                GargoyleMovementChanceBase = 10,
                GargoyleMovementChanceIntelligenceScale = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 1,
            aggroRange: 5,
            monsterId: 1,
            nativeAi: "Fallen",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 6 ^ 9 ^ 7 = 8: inside the content-selected movement band. The
        // validated Gargoyle healing metadata must not suppress movement.
        processor.AdvanceTick(6, [player]);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void GargoyleAiDefersWhenNativeAvoidanceCandidatesAreBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 1,
            aggroRange: 5,
            nativeAi: "Gargoyle",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2, 42, 43],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((3, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void GargoyleAiUsesNativeAdjacentAttackAdmission()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Gargoyle",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38: outside the native 23% AiAvoidance attack band.
        Assert.Empty(processor.AdvanceTick(40, [player]));
        Assert.Equal(100, player.Life);

        // 1 ^ 9 ^ 7 = 15: inside the same native attack band.
        Assert.Single(processor.AdvanceTick(1, [player]));
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void GargoyleAiUsesContentSelectedAdjacentAttackAdmission()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Gargoyle",
                0,
                0,
                9,
                60,
                90,
                45,
                10,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Gargoyle",
                GargoyleAttackChanceBase = 10,
                GargoyleAttackChanceIntelligenceScale = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            monsterId: 1,
            nativeAi: "Gargoyle",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: outside the content-selected 10% attack band.
        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.Equal(100, player.Life);

        // 6 ^ 9 ^ 7 = 8: inside the same content-selected attack band.
        Assert.Single(processor.AdvanceTick(6, [player]));
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void GargoyleUsesValidatedSafetyBoundaryTuning()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Gargoyle",
                0,
                0,
                9,
                60,
                90,
                45,
                10,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GargoyleSafeDistanceBase = 5,
                GargoyleSafeDistanceIntelligenceScale = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 1,
            nativeAi: "Fallen",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var healing = processor.AdvanceTick(1, [player]);

        Assert.Empty(healing);
        Assert.Equal(40, monster.HitPoints);
        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void GargoyleUsesValidatedHealingHealthThreshold()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Gargoyle",
                0,
                0,
                9,
                60,
                90,
                45,
                10,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GargoyleSafeDistanceBase = 2,
                GargoyleSafeDistanceIntelligenceScale = 1,
                GargoyleHealingHealthThresholdPercent = 75,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            60,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 1,
            nativeAi: "Fallen");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var healing = processor.AdvanceTick(1, [player]);

        Assert.Equal(61, monster.HitPoints);
        Assert.Single(healing);
    }

    [Fact]
    public void GargoyleUsesStandaloneHealingTuningWithNativeSafetyAndFormulaDefaults()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Gargoyle",
                0,
                0,
                9,
                60,
                90,
                45,
                10,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GargoyleHealingHealthThresholdPercent = 75,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            60,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 1,
            nativeAi: "Fallen");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var healing = processor.AdvanceTick(1, [player]);

        Assert.Equal(61, monster.HitPoints);
        Assert.Single(healing);
    }

    [Fact]
    public void GargoyleUsesValidatedHealingFormulaTuning()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Gargoyle",
                0,
                0,
                9,
                60,
                90,
                45,
                10,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GargoyleSafeDistanceBase = 2,
                GargoyleSafeDistanceIntelligenceScale = 1,
                GargoyleHealDivisorMultiplier = 16,
                GargoyleHealDivisorRandomRange = 1,
                GargoyleHealDivisorRandomOffset = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 1,
            nativeAi: "Fallen");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var healing = processor.AdvanceTick(1, [player]);

        Assert.Equal(46, monster.HitPoints);
        var healingEvent = Assert.Single(healing).Event;
        Assert.Equal(DiabloGameplayEventKind.Healing, healingEvent.Kind);
        Assert.Equal(6, healingEvent.Amount);
        Assert.Equal(6 * DiabloFixedPointQ6.Scale, healingEvent.AmountQ6);
    }

    [Fact]
    public void GargoyleUsesNativeRandomOffsetWhenOptionalTuningOmitsIt()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Gargoyle",
                0,
                0,
                9,
                60,
                90,
                45,
                10,
                16,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GargoyleSafeDistanceBase = 2,
                GargoyleSafeDistanceIntelligenceScale = 1,
                GargoyleHealDivisorMultiplier = 16,
                GargoyleHealDivisorRandomRange = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 1,
            nativeAi: "Fallen");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var healing = processor.AdvanceTick(1, [player]);

        Assert.Equal(41, monster.HitPoints);
        Assert.Equal(1, Assert.Single(healing).Event.Amount);
    }

    [Fact]
    public void GargoyleAiClearsItsSpecialLockWhenThePlayerClosesDistance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            nativeAi: "Gargoyle",
            intelligence: 2,
            gargoyleSpecialActive: true);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.False(monster.GargoyleSpecialActive);
        Assert.Equal(40, monster.HitPoints);
    }

    [Fact]
    public void GargoyleAiRetreatsBeforeHealingInsideItsNativeSafetyBoundary()
    {
        var monster = new AuthoritativeCombatTarget(
            8,
            4,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 1,
            aggroRange: 8,
            nativeAi: "Gargoyle",
            intelligence: 2);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 6,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.True(monster.IsMoving);
        Assert.Equal((3, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Equal(40, monster.HitPoints);
        Assert.False(monster.GargoyleSpecialActive);

        processor.AdvanceTick(2, [player]);
        Assert.Equal((3, 0), (monster.PositionX, monster.PositionY));

        var healing = processor.AdvanceTick(3, [player]);
        Assert.Equal((2, 0), (monster.PositionX, monster.PositionY));
        Assert.True(monster.GargoyleSpecialActive);
        Assert.Single(healing);
    }

    [Fact]
    public void GargoyleSpecialLockCanUseAnAlternateValidatedIdentity()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            nativeAi: "Rhino",
            gargoyleSpecialActive: true);

        Assert.True(monster.GargoyleSpecialActive);
    }

    [Fact]
    public void RhinoAiFallsBackToWalkingWhenTheChargeLaneIsBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino");
        var world = new DiabloWorldState(
            fallbackBlockedCells: [4],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void RhinoAiUsesNativeRoundWalkAfterAChargeMiss()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            4,
            4,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 10,
            positionY: 4,
            life: 100,
            lifeMaximum: 100);

        // 144 ^ 9 ^ 7 = 158: the 43-percent charge roll misses, while the
        // native far-distance RoundWalk branch is admitted.
        processor.AdvanceTick(144, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((4, 3), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void RhinoAiUsesValidatedMovementGoalGate()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                66,
                64,
                "Horned Demon",
                7,
                9,
                13,
                40,
                80,
                35,
                2,
                16,
                5,
                32,
                false,
                false,
                false) {
                NativeAi = "Rhino",
                RhinoChargeMinimumDistance = 5,
                RhinoChargeChanceBase = 1,
                RhinoChargeChanceIntelligenceScale = 1,
                RhinoMovementGoalMinimumDistance = 5,
                RhinoMovementGoalChanceModulo = 5,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            4,
            4,
            20,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 66,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            rhinoMovementIdleTicks: 10,
            rhinoMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 10,
            positionY: 4,
            life: 100,
            lifeMaximum: 100);

        // 400 ^ 9 ^ 7 = 414: native modulo-four skips the movement goal and
        // the middle idle band rejects ordinary movement; authored modulo-five
        // admits the persisted Rhino movement goal.
        processor.AdvanceTick(400, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(1, monster.RhinoMovementTicks);
    }

    [Fact]
    public void RhinoAiUsesValidatedMovementGoalGateAfterChargeMiss()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                64,
                "Horned Demon",
                0,
                0,
                13,
                40,
                80,
                35,
                2,
                16,
                5,
                32,
                false,
                false,
                false) {
                NativeAi = "Rhino",
                RhinoChargeMinimumDistance = 10,
                RhinoChargeChanceBase = 1,
                RhinoChargeChanceIntelligenceScale = 1,
                RhinoMovementChanceBase = 1,
                RhinoMovementChanceIntelligenceScale = 1,
                RhinoMovementGoalMinimumDistance = 5,
                RhinoMovementGoalChanceModulo = 5,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 1,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            rhinoMovementIdleTicks: 10,
            rhinoMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1,014 ^ 9 ^ 7 = 1,000: authored modulo-five suppresses the Move
        // goal in both branches, while the legacy modulo-four fallback would
        // admit its RoundWalk before the ordinary middle-band roll.
        processor.AdvanceTick(1_014, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.RhinoMovementIdleTicks);
        Assert.Equal(15, monster.RhinoDelayTicks);
    }

    [Fact]
    public void RhinoAiCarriesItsNativeMoveGoalAcrossTicks()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            4,
            4,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 10,
            positionY: 4,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(144, [player]);
        Assert.Equal(1, monster.RhinoMovementTicks);
        Assert.Equal(1, monster.RhinoMovementTurn);

        processor.AdvanceTick(145, [player]);
        Assert.Equal(2, monster.RhinoMovementTicks);
        Assert.Equal((4, 3), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void RhinoAiDefersAfterChargeAndMovementAdmissionsMiss()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 3632 ^ 9 ^ 7 = 3646: charge misses, the far RoundWalk branch is
        // skipped, and the native 33-percent movement roll also misses.
        processor.AdvanceTick(3632, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void RhinoAiUsesItsNativeFreshMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            intelligence: 0,
            rhinoMovementIdleTicks: 0,
            rhinoMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 3,296 ^ 9 ^ 7 = 3,310: movement roll 33 is outside the long-idle
        // 33% band but inside the fresh-mode 83% band.
        processor.AdvanceTick(3_296, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(0, monster.RhinoMovementIdleTicks);
    }

    [Fact]
    public void RhinoAiUsesValidatedFreshMovementBonus()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                64,
                "Horned Demon",
                0,
                0,
                13,
                40,
                80,
                35,
                2,
                16,
                5,
                32,
                false,
                false,
                false) {
                NativeAi = "Rhino",
                RhinoChargeMinimumDistance = 10,
                RhinoChargeChanceBase = 1,
                RhinoChargeChanceIntelligenceScale = 1,
                RhinoMovementChanceBase = 1,
                RhinoMovementChanceIntelligenceScale = 1,
                RhinoMovementFreshChanceBonus = 100,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            monsterId: 1,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            rhinoMovementIdleTicks: 0,
            rhinoMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 6,000 ^ 9 ^ 7 = 6,014: movement roll 60 misses the native 33 +
        // 2 * intelligence fallback but is admitted by the validated bonus.
        processor.AdvanceTick(6_000, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(0, monster.RhinoMovementIdleTicks);
    }

    [Fact]
    public void RhinoAiStartsItsNativeDelayInsideTheMiddleMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            intelligence: 0,
            rhinoMovementIdleTicks: 10,
            rhinoMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(3_296, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.RhinoMovementIdleTicks);
        Assert.Equal(16, monster.RhinoDelayTicks);
    }

    [Fact]
    public void RhinoAiUsesValidatedDelayTuningInsideTheMiddleMovementBand()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                66,
                64,
                "Horned Demon",
                7,
                9,
                13,
                40,
                80,
                35,
                2,
                16,
                5,
                32,
                false,
                false,
                false) {
                NativeAi = "Rhino",
                AggroRange = 8,
                RhinoChargeMinimumDistance = 5,
                RhinoChargeChanceBase = 43,
                RhinoChargeChanceIntelligenceScale = 2,
                RhinoDelayBase = 8,
                RhinoDelayRandomRange = 4,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            attackDamage: 1,
            monsterId: 66,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            intelligence: 0,
            rhinoMovementIdleTicks: 10,
            rhinoMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(3_296, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.RhinoMovementIdleTicks);
        Assert.Equal(8, monster.RhinoDelayTicks);
    }

    [Fact]
    public void SkeletonRangedAiFiresAnAuthoritativeArrowAcrossAClearLane()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            world,
            random: random,
            monsterMissiles: CreateArrowMissileCatalog(
                "SkeletonRanged",
                projectileSpeed: 3,
                projectileLifetime: 9));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 14 ^ 9 ^ 7 = 0, inside the native 3 + 2 * intelligence band.
        var deliveries = processor.AdvanceTick(14, [player]);

        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(100, player.Life);
        Assert.Empty(deliveries);
        var arrow = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, arrow.Behavior);
        Assert.Equal(2, arrow.Damage);
        Assert.Equal((2, 9), (arrow.DamageMinimum, arrow.DamageMaximum));
        Assert.Equal(3, arrow.Speed);
        Assert.Equal(8U, arrow.RemainingTicks);
        Assert.Equal(0U, random.State);
        var expectedImpactDamage = new DiabloRandom(random.State)
            .RandomIntBetween(arrow.DamageMinimum, arrow.DamageMaximum);

        for (ulong tick = 15; tick < 32 && world.Projectiles.Count != 0; tick++)
            deliveries = deliveries.Concat(processor.AdvanceTick(tick, [player])).ToList();

        Assert.Empty(world.Projectiles);
        Assert.Equal(100 - expectedImpactDamage, player.Life);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal((9U, 7U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(expectedImpactDamage, damage.Amount);
    }

    [Fact]
    public void MonsterArrowBreaksABlockingObjectBeforeRemoval()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            breakable: true);
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            monsterLevel: 1);
        var arrow = new AuthoritativeProjectile(
            0x80000000,
            9,
            7,
            0,
            0,
            0,
            0,
            3,
            0,
            2,
            AuthoritativeDamageType.Physical,
            0,
            3,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
            Speed = 1,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            combatTargets: [monster],
            projectiles: [arrow]);
        var processor = CreateProcessor(world);
        var target = CreatePlayer(entityId: 7, positionX: 3);

        processor.AdvanceTick(1, [target]);

        Assert.Equal((0, 0), (arrow.PositionX, arrow.PositionY));
        Assert.True(blocker.Destroyed);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MonsterArrowUsesConfiguredProjectileSpeedForTravel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            toHit: 100,
            nativeAi: "SkeletonRanged",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog(
                "SkeletonRanged",
                projectileSpeed: 3,
                projectileLifetime: 9));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 14 ^ 9 ^ 7 = 0, inside the native 3 + 2 * intelligence band.
        processor.AdvanceTick(14, [player]);
        var arrow = Assert.Single(world.Projectiles.Values);
        Assert.Equal((4, 0), (arrow.PositionX, arrow.PositionY));
        Assert.Equal(100, player.Life);

        processor.AdvanceTick(15, [player]);

        Assert.Equal((7, 0), (arrow.PositionX, arrow.PositionY));
        Assert.Equal(98, player.Life);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, arrow.Phase);
        processor.AdvanceTick(16, [player]);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MonsterArrowUsesConfiguredProjectileDamage()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            toHit: 100,
            nativeAi: "SkeletonRanged",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "SkeletonRanged",
                    "FixedArrow",
                    AuthoritativeProjectileBehavior.MonsterArrow,
                    ProjectileSpeed: 1,
                    ProjectileLifetime: 9,
                    ProjectileDamage: 7),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(14, [player]);
        for (ulong tick = 15; tick < 32 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(93, player.Life);
    }

    [Fact]
    public void MonsterArrowImpactPublishesExactQ6Damage()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            toHit: 100,
            nativeAi: "SkeletonRanged",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "SkeletonRanged",
                    "FixedArrow",
                    AuthoritativeProjectileBehavior.MonsterArrow,
                    ProjectileSpeed: 1,
                    ProjectileLifetime: 9,
                    ProjectileDamage: 7),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100 * DiabloFixedPointQ6.Scale,
            lifeMaximum: 100 * DiabloFixedPointQ6.Scale);

        processor.AdvanceTick(14, [player]);
        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 15; tick < 32 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(93 * DiabloFixedPointQ6.Scale, player.Life);
        Assert.Equal(7, damage.Amount);
        Assert.Equal(7 * DiabloFixedPointQ6.Scale, damage.AmountQ6);
    }

    [Fact]
    public void MonsterArrowUsesConfiguredDamageTypeAndPlayerResistance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "SkeletonRanged",
                    "FireArrow",
                    AuthoritativeProjectileBehavior.MonsterArrow,
                    ProjectileSpeed: 1,
                    DamageType: AuthoritativeDamageType.Fire),
            ]));
        var fireResistantArmor = AuthoritativeItemState.Empty with {
            StatFlag = true,
            PlusFireResistance = 50,
        };
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            equipment: [new EquippedStoreItem(3, 99, fireResistantArmor)]);

        processor.AdvanceTick(14, [player]);

        var arrow = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeDamageType.Fire, arrow.DamageType);
        for (ulong tick = 15; tick < 32 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(99, player.Life);
    }

    [Fact]
    public void MonsterArrowUsesConfiguredImpactLifetimeAfterPlayerHit()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "GoatRanged",
                    "ImpactArrow",
                    AuthoritativeProjectileBehavior.MonsterArrow,
                    ProjectileSpeed: 1,
                    ProjectileLifetime: 8,
                    ProjectileImpactLifetime: 3),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);
        Assert.Single(world.Projectiles);
        AuthoritativeProjectile? impact = null;
        for (ulong tick = 2; tick < 16 && world.Projectiles.Count != 0; tick++) {
            processor.AdvanceTick(tick, [player]);
            impact = world.Projectiles.Values.SingleOrDefault();
            if (impact?.Phase == AuthoritativeProjectilePhase.Impact)
                break;
        }

        Assert.NotNull(impact);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, impact!.Phase);
        Assert.Equal(3U, impact.RemainingTicks);
        Assert.Equal(98, player.Life);

        processor.AdvanceTick(16, [player]);
        Assert.Single(world.Projectiles);
        Assert.Equal(2U, Assert.Single(world.Projectiles.Values).RemainingTicks);
        processor.AdvanceTick(17, [player]);
        processor.AdvanceTick(18, [player]);
        Assert.Empty(world.Projectiles);
        Assert.Equal(98, player.Life);
    }

    [Fact]
    public void MonsterMagicUsesConfiguredImpactLifetimeAfterPlayerHit()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            levelId: 0,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            nativeAi: "FireBat");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "FireBat",
                    "ImpactMagic",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    SpellId: 1,
                    ProjectileSpeed: 16,
                    ProjectileLifetime: 256,
                    ProjectileImpactLifetime: 3,
                    ProjectileDamage: 2),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 6_400,
            lifeMaximum: 6_400);

        processor.AdvanceTick(1, [player]);
        Assert.Single(world.Projectiles);
        AuthoritativeProjectile? impact = null;
        var impactTick = 0UL;
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++) {
            processor.AdvanceTick(tick, [player]);
            impact = world.Projectiles.Values.SingleOrDefault();
            if (impact?.Phase == AuthoritativeProjectilePhase.Impact) {
                impactTick = tick;
                break;
            }
        }

        Assert.NotNull(impact);
        Assert.NotEqual(0UL, impactTick);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, impact!.Phase);
        Assert.Equal(3U, impact.RemainingTicks);
        Assert.Equal(6_400 - 2 * DiabloFixedPointQ6.Scale, player.Life);

        processor.AdvanceTick(impactTick + 1, [player]);
        processor.AdvanceTick(impactTick + 2, [player]);
        processor.AdvanceTick(impactTick + 3, [player]);
        Assert.Empty(world.Projectiles);
        Assert.Equal(6_400 - 2 * DiabloFixedPointQ6.Scale, player.Life);
    }

    [Fact]
    public void SkeletonRangedAiDefersWhenTheArrowLaneIsBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 3);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [4],
            combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(14, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void GeometryBackedSkeletonRangedAiDefersWhenNativeBlockLightBlocksArrowLane()
    {
        var pieces = new ushort[8];
        pieces[4] = 7;
        var geometry = new AuthoritativeWorld();
        geometry.AddLevel(new AuthoritativeLevel(
            0,
            8,
            1,
            nativePieceLayer: pieces,
            nativePieceProperties: new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [7] = AuthoritativeDunTileFlags.BlockLight,
            }));
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged");
        var world = new DiabloWorldState(geometry, combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(14, [player]);

        Assert.Empty(deliveries);
        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void SkeletonRangedAiRetreatsInsideItsNativeFourTileBoundary()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 2 ^ 9 ^ 7 = 12; the native retreat admission uses the second draw.
        Assert.Empty(processor.AdvanceTick(2, [player]));
        Assert.True(monster.IsMoving);
        Assert.True(monster.FuturePositionX < monster.PositionX);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void SkeletonRangedAiUsesItsNativeFreshMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 0,
            skeletonRangedMovementIdleTicks: 0,
            skeletonRangedMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1,296 ^ 9 ^ 7 = 1,310: movement roll 13 is outside the long-idle
        // 13% band but inside the fresh-mode 63% band.
        processor.AdvanceTick(1_296, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(0, monster.SkeletonRangedMovementIdleTicks);
    }

    [Fact]
    public void SkeletonRangedUsesValidatedFreshMovementBonus()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Skeleton Bow",
                0,
                0,
                1,
                4,
                4,
                10,
                2,
                9,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "SkeletonRanged",
                SkeletonRangedRetreatMinimumDistance = 4,
                SkeletonRangedRetreatChanceBase = 1,
                SkeletonRangedRetreatChanceIntelligenceScale = 1,
                SkeletonRangedAttackChanceBase = 100,
                SkeletonRangedAttackChanceIntelligenceScale = 1,
                SkeletonRangedMovementFreshChanceBonus = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            monsterId: 1,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 0,
            skeletonRangedMovementIdleTicks: 0,
            skeletonRangedMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1,296 ^ 9 ^ 7 = 1,310: movement roll 13 is outside the authored
        // two-percent fresh-mode band, so the retreat branch must defer.
        processor.AdvanceTick(1_296, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(1, monster.SkeletonRangedMovementIdleTicks);
    }

    [Fact]
    public void SkeletonRangedAiDefersInsideItsNativeMiddleMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 0,
            skeletonRangedMovementIdleTicks: 10,
            skeletonRangedMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1_296, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.SkeletonRangedMovementIdleTicks);
    }

    [Fact]
    public void SkeletonRangedUsesValidatedRetreatBoundaryTuning()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                81,
                "Skeleton Bow",
                0,
                0,
                1,
                4,
                4,
                10,
                2,
                9,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                SkeletonRangedRetreatMinimumDistance = 5,
                SkeletonRangedRetreatChanceBase = 100,
                SkeletonRangedRetreatChanceIntelligenceScale = 1,
                SkeletonRangedAttackChanceBase = 100,
                SkeletonRangedAttackChanceIntelligenceScale = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 1,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"),
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.True(monster.IsMoving);
        Assert.True(monster.FuturePositionX < monster.PositionX);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SkeletonRangedAiNeverFallsThroughToAdjacentMelee()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            monsterId: 1,
            nativeAi: "Fallen",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: new AuthoritativeMonsterTypeCatalog([
                new AuthoritativeMonsterTypeDefinition(
                    1,
                    81,
                    "Skeleton Bow",
                    0,
                    0,
                    1,
                    4,
                    4,
                    10,
                    2,
                    9,
                    0,
                    0,
                    false,
                    true,
                    false) {
                    NativeAi = "SkeletonRanged",
                    SkeletonRangedRetreatMinimumDistance = 4,
                    SkeletonRangedRetreatChanceBase = 1,
                    SkeletonRangedRetreatChanceIntelligenceScale = 1,
                    SkeletonRangedAttackChanceBase = 3,
                    SkeletonRangedAttackChanceIntelligenceScale = 2,
                },
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1400 ^ 9 ^ 7 = 1418: no retreat and no 3% ranged admission.
        Assert.Empty(processor.AdvanceTick(1400, [player]));
        Assert.Equal(100, player.Life);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void SnakeAiCreatesAValidatedRhinoChargeAndResolvesSpecialDamage()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 3,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 5,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var charge = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.SnakeCharge, charge.Behavior);
        Assert.Equal(18, charge.Speed);
        Assert.Equal(AuthoritativeDamageType.Physical, charge.DamageType);
        Assert.Equal(100, player.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 16 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.True(player.Life < 100);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7
                && delivery.Event.AmountQ6 == 4 * DiabloFixedPointQ6.Scale);
    }

    [Fact]
    public void SnakeChargeUsesConfiguredImpactLifetimeAfterPlayerHit()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 0,
            maxDamage: 0,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 5,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "ImpactCharge",
                    AuthoritativeProjectileBehavior.SnakeCharge,
                    ProjectileImpactLifetime: 3,
                    ProjectileDamage: 8),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 6_400,
            lifeMaximum: 6_400,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);
        Assert.Single(world.Projectiles);
        AuthoritativeProjectile? impact = null;
        var impactTick = 0UL;
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++) {
            processor.AdvanceTick(tick, [player]);
            impact = world.Projectiles.Values.SingleOrDefault();
            if (impact?.Phase == AuthoritativeProjectilePhase.Impact) {
                impactTick = tick;
                break;
            }
        }

        Assert.NotNull(impact);
        Assert.NotEqual(0UL, impactTick);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, impact!.Phase);
        Assert.Equal(3U, impact.RemainingTicks);
        Assert.Equal(6_400 - 8 * DiabloFixedPointQ6.Scale, player.Life);

        processor.AdvanceTick(impactTick + 1, [player]);
        processor.AdvanceTick(impactTick + 2, [player]);
        processor.AdvanceTick(impactTick + 3, [player]);
        Assert.Empty(world.Projectiles);
        Assert.Equal(6_400 - 8 * DiabloFixedPointQ6.Scale, player.Life);
    }

    [Fact]
    public void SnakeChargeUsesConfiguredDamageTypeAndPlayerResistance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 0,
            maxDamage: 0,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 5,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "FireCharge",
                    AuthoritativeProjectileBehavior.SnakeCharge,
                    DamageType: AuthoritativeDamageType.Fire,
                    ProjectileDamage: 8),
            ]));
        var fireResistantArmor = AuthoritativeItemState.Empty with {
            StatFlag = true,
            PlusFireResistance = 50,
        };
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 6_400,
            lifeMaximum: 6_400,
            characterLevel: 1,
            equipment: [new EquippedStoreItem(3, 99, fireResistantArmor)]);

        processor.AdvanceTick(1, [player]);
        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 16 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.Equal(96 * DiabloFixedPointQ6.Scale, player.Life);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7
                && delivery.Event.AmountQ6 == 4 * DiabloFixedPointQ6.Scale);
    }

    [Fact]
    public void SnakeAiDriftsWhenChargeDistanceIsNotMet()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38, outside Snake's native 35-point delay band. The
        // distance-four target is outside the two-tile charge boundary, so
        // the native drift branch must move instead of idling.
        processor.AdvanceTick(40, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((2, 1), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerSnakeChargeConsumesNoExtraActionRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Single(world.Projectiles);
    }

    [Fact]
    public void MultiplayerSnakeConsumesItsNativeAdjacentDelayRoll()
    {
        const uint aiSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(37, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(10 + delayRoll, monster.SnakeDelayTicks);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SinglePlayerInitializedSnakeConsumesNativeDistantDelayRoll()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        var delayRoll = expected.RandomIntLessThan(10);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(15 + delayRoll, monster.SnakeDelayTicks);
        Assert.False(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerSnakeConsumesItsNativeDistantDelayRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(15 + delayRoll, monster.SnakeDelayTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void MultiplayerSnakeConsumesNativeRandomWalk2FallbackRoll()
    {
        const uint aiSeed = 123;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2, 41, 42],
            combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(88, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(130, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void SnakeAiUsesValidatedDistantDelayAdmissionChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge,
                    SnakeDistantDelayChanceBase: 100,
                    SnakeDistantDelayChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38, outside the native 35-point band. The authored
        // 100-point band must still enter the server-owned delay branch.
        processor.AdvanceTick(40, [player]);

        Assert.Empty(world.Projectiles);
        Assert.False(monster.IsMoving);
        Assert.Equal(15, monster.SnakeDelayTicks);
    }

    [Fact]
    public void SnakeAiUsesValidatedDriftPattern()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge) {
                    SnakeDriftPattern = [0, 0, 0, 0, 0, 0],
                },
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38, outside Snake's native delay band. The configured
        // first rotation is zero, so the drift target is straight ahead (2,0)
        // instead of the legacy lateral (2,1) candidate.
        processor.AdvanceTick(40, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void SnakeChargeUsesConfiguredMonsterPlayerBlockLevelScale()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 3,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 1,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterChargeBaseToHit: 1000,
                monsterPlayerBlockLevelScale: 0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 5,
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        processor.AdvanceTick(1, [player]);
        for (ulong tick = 2; tick < 16 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.True(player.Life < 100);
        Assert.NotEqual(DiabloPlayerCombatMode.Block, player.CombatMode);
    }

    [Fact]
    public void SnakeChargeUsesConfiguredSpecialHitAdmissionFormula()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 0,
            maxDamage: 0,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 5,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMeleeMinimumHitChance: 0,
                monsterMinimumHitChanceLevel14: 0,
                monsterMinimumHitChanceLevel15: 0,
                monsterMinimumHitChanceLevel16: 0,
                monsterToHitLevelScale: 0,
                monsterToHitBaseBonus: 0,
                monsterChargeBaseToHit: 0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);
        for (ulong tick = 2; tick < 16 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void SnakeChargeDispatchUsesMappedProjectileBehaviorInsteadOfNativeAiLabel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 3,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 5,
            nativeAi: "GoatRanged",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "GoatRanged",
                    "CustomCharge",
                    AuthoritativeProjectileBehavior.SnakeCharge,
                    SpellId: 37,
                    DamageType: AuthoritativeDamageType.Fire),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var charge = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.SnakeCharge, charge.Behavior);
        Assert.Equal(37U, charge.SpellId);
        Assert.Equal(AuthoritativeDamageType.Fire, charge.DamageType);
    }

    [Fact]
    public void SnakeUsesValidatedChargeTrajectoryTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 3,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 5,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge,
                    ProjectileSpeed: 7,
                    ProjectileLifetime: 12,
                    ProjectileImpactLifetime: 5),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var charge = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.SnakeCharge, charge.Behavior);
        Assert.Equal(7, charge.Speed);
        Assert.Equal(11U, charge.RemainingTicks);
        Assert.Equal(5, charge.ImpactLifetime);
    }

    [Fact]
    public void SnakeUsesValidatedChargeDistanceTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 3,
            minDamageSpecial: 4,
            maxDamageSpecial: 4,
            monsterLevel: 5,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge,
                    ChargeMinimumDistance: 3),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 40 ^ 9 ^ 7 = 38, outside Snake's native 35-point delay band, so
        // the configured non-charge distance still takes the drift branch.
        Assert.Empty(processor.AdvanceTick(40, [player]));

        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void SnakeAiUsesNativeRandomWalk2WhenItsDriftCandidateIsBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2, 41, 42],
            combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var player = CreatePlayer(entityId: 7, positionX: 5, positionY: 0);

        // 130 ^ 9 ^ 7 = 140: the +1 drift candidate (2,1), then native
        // RandomWalk2's (2,0)/(1,1) candidates are blocked. The old five-way
        // replacement would have moved to (0,1).
        processor.AdvanceTick(130, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void SnakeAiDefersDuringItsNativeDelayBranch()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, inside Snake's native 35-point delay band.
        processor.AdvanceTick(1, [player]);

        Assert.Empty(world.Projectiles);
        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(100, player.Life);
        Assert.Equal(15, monster.SnakeDelayTicks);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(14, monster.SnakeDelayTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void SnakeAiStartsItsNativeDelayAfterAnAdjacentAttackAdmissionFails()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 50 ^ 9 ^ 7 = 60: the native adjacent 20% attack admission fails
        // and schedules 10 + GenerateRnd(10) ticks of AiDelay.
        processor.AdvanceTick(50, [player]);

        Assert.Equal(10, monster.SnakeDelayTicks);
        Assert.Equal(100, player.Life);

        processor.AdvanceTick(51, [player]);

        Assert.Equal(9, monster.SnakeDelayTicks);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void SnakeAiUsesValidatedAdjacentAttackAdmissionChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            toHit: 100,
            nativeAi: "Snake",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Snake",
                    "Rhino",
                    AuthoritativeProjectileBehavior.SnakeCharge,
                    SnakeAdjacentAttackChanceBase: 100,
                    SnakeAdjacentAttackChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 50 ^ 9 ^ 7 = 60: the authored 100-percent band admits the
        // adjacent attack, whereas the native 20-percent fallback would
        // start Snake's delay branch.
        processor.AdvanceTick(50, [player]);

        Assert.Equal(0, monster.SnakeDelayTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void GloomUsesItsMonsterSpecificRhinoMappingForMovementOnlyCharge()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Bat",
            intelligence: 2);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        var charge = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.BatCharge, charge.Behavior);
        Assert.Equal(18, charge.Speed);
        Assert.Equal(100, player.Life);

        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal((6, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void GloomUsesValidatedMappedChargeBoundaryTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Bat",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42,
                    ChargeMinimumDistance: 6,
                    ChargeChanceBase: 100,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 6,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void GloomUsesNativeRandomWalkAfterAChargeRollMiss()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Bat",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.True(monster.IsMoving);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void MultiplayerGloomChargeMissReusesItsNativeMovementRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 21,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Empty(world.Projectiles);
        Assert.True(monster.IsMoving);
        Assert.Equal((1, 0), (monster.BatMovementMode, monster.BatMovementIdleTicks));
    }

    [Fact]
    public void GloomSuppressesGenericNavigationWhenNativeMovementRollDefers()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Bat",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(49, [player]);

        Assert.Empty(world.Projectiles);
        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void BatUsesItsNativeLongIdleMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 21,
            batMovementMode: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(entityId: 7, positionX: 7, positionY: 0);

        // 11 ^ 9 ^ 7 = 5: the charge roll misses and the native 13-point
        // long-idle movement band admits the five-candidate RandomWalk.
        processor.AdvanceTick(11, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(1, monster.BatMovementMode);
        Assert.Equal(0, monster.BatMovementIdleTicks);
    }

    [Fact]
    public void FamiliarEmitsItsNativeLightningAfterAnAdjacentHit()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 43,
            monsterLevel: 13,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Bat",
            intelligence: 24);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(3, 1, 0, 0, 0, 0),
            ]),
            random: new DiabloRandom(1),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "FamiliarArc",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    MonsterId: 43,
                    SpellId: 3,
                    DamageType: AuthoritativeDamageType.Lightning,
                    ProjectileSpeed: 16,
                    ProjectileLifetime: 2,
                    ProjectileImpactLifetime: 8,
                    ProjectileDamageRandomRange: 4,
                    IsPostMeleeMissile: true),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(8, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(AuthoritativeDamageType.Lightning, projectile.DamageType);
        Assert.Equal(3U, projectile.SpellId);
        // Native single-player melee damage consumes its range roll before the
        // post-melee Familiar projectile chooses its random damage.
        Assert.Equal(2, projectile.Damage);
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.True(monster.BatRetreatTicks > 0);
    }

    [Fact]
    public void FamiliarUsesConfiguredFixedProjectileDamage()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 43,
            monsterLevel: 13,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Bat",
            intelligence: 24);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(3, 1, 0, 0, 0, 0),
            ]),
            random: new DiabloRandom(1),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "FamiliarArc",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    MonsterId: 43,
                    SpellId: 3,
                    DamageType: AuthoritativeDamageType.Lightning,
                    ProjectileSpeed: 16,
                    ProjectileLifetime: 2,
                    ProjectileDamage: 7,
                    ProjectileDamageRandomRange: 4,
                    IsPostMeleeMissile: true),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(8, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(7, projectile.Damage);
    }

    [Fact]
    public void BatUsesItsNativeFreshMovementModeBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 0,
            batMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(entityId: 7, positionX: 7, positionY: 0);

        processor.AdvanceTick(11, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(1, monster.BatMovementMode);
        Assert.Equal(0, monster.BatMovementIdleTicks);
    }

    [Fact]
    public void BatUsesValidatedMovementChanceTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 0,
            batMovementMode: 1);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                42,
                42,
                "Bat",
                1,
                2,
                1,
                4,
                7,
                10,
                2,
                5,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Bat",
                BatMovementChanceBase = 1,
                BatMovementChanceIntelligenceScale = 1,
                BatMovementFreshChanceBonus = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(entityId: 7, positionX: 7, positionY: 0);

        // 11 ^ 9 ^ 7 = 5: the charge roll misses, and the authored two-point
        // fresh-mode movement band defers instead of using native 63 percent.
        processor.AdvanceTick(11, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(1, monster.BatMovementIdleTicks);
    }

    [Fact]
    public void BatDefersBeforeItsNativeMovementModeIsReady()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            nativeAi: "Bat",
            intelligence: 0,
            batMovementIdleTicks: 10,
            batMovementMode: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 1,
                    ChargeChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(entityId: 7, positionX: 7, positionY: 0);

        processor.AdvanceTick(11, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(11, monster.BatMovementIdleTicks);
    }

    [Fact]
    public void GloomUsesValidatedChargeTrajectoryTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            monsterId: 42,
            monsterLevel: 9,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Bat",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Bat",
                    "Rhino",
                    AuthoritativeProjectileBehavior.BatCharge,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    100,
                    42,
                    ChargeMinimumDistance: 5,
                    ChargeChanceBase: 100,
                    ChargeChanceIntelligenceScale: 1,
                    ProjectileSpeed: 7,
                    ProjectileLifetime: 12),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        var charge = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.BatCharge, charge.Behavior);
        Assert.Equal(7, charge.Speed);
        Assert.Equal(11U, charge.RemainingTicks);
    }

    [Fact]
    public void ValidatedMappedRangedContentAdmitsAnOtherwiseUnroutedNativeAi()
    {
        // Use a non-degenerate range to verify native launch damage and RNG order.
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 9,
            monsterLevel: 5,
            nativeAi: "Counselor");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "Firebolt",
                    AuthoritativeProjectileBehavior.Firebolt),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Firebolt, projectile.Behavior);
        Assert.Equal(9U, projectile.SourceEntityId);
        // Native CounselorAi consumes GenerateRnd(100), then AddFirebolt
        // captures RandomIntBetween(min_damage, max_damage) at launch.
        Assert.Equal(8, projectile.Damage);
    }

    [Theory]
    [InlineData("Zhar", 107)]
    [InlineData("Lazarus", 113)]
    public void NativeCounselorAliasesUseTheServerOwnedRangedBranch(
        string nativeAi,
        int monsterId)
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: (uint)monsterId,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: nativeAi,
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    nativeAi,
                    "Fireball",
                    AuthoritativeProjectileBehavior.Fireball),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Fireball, projectile.Behavior);
        Assert.Equal(9U, projectile.SourceEntityId);
        Assert.Equal(4, projectile.Damage);
    }

    [Theory]
    [InlineData("Zhar", 107)]
    [InlineData("Lazarus", 113)]
    public void SeededCounselorAliasesUseCounselorRecoveryDelay(
        string nativeAi,
        int monsterId)
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: (uint)monsterId,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: nativeAi,
            intelligence: 3,
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(initSeed),
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    nativeAi,
                    "FixedFireball",
                    AuthoritativeProjectileBehavior.Fireball,
                    RangedDelayBase: 30,
                    RangedDelayRandomRange: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Single(world.Projectiles.Values);
        Assert.Equal(0, monster.RangedDelayTicks);
        Assert.InRange(monster.CounselorDelayTicks, 5, 14);
    }

    [Fact]
    public void ExplicitCounselorAliasWinsOverGenericCatalogMissileMapping()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 107,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Zhar",
            intelligence: 20,
            unique: true,
            uniqueIdentity: "Zhar");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                107,
                107,
                "Generic Counselor Base",
                1,
                16,
                1,
                1,
                4,
                0,
                2,
                3,
                0,
                3,
                false,
                false,
                false) {
                NativeAi = "Zombie",
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Zhar",
                    "AliasFirebolt",
                    AuthoritativeProjectileBehavior.Firebolt,
                    ProjectileDamage: 7),
                new AuthoritativeMonsterMissileDefinition(
                    "Zombie",
                    "GenericFirebolt",
                    AuthoritativeProjectileBehavior.Firebolt,
                    ProjectileDamage: 99),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(7, projectile.Damage);
    }

    [Fact]
    public void WoundedCounselorStartsItsFourAttemptRetreatBeforeRangedDispatch()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            4,
            maxHitPoints: 10,
            aggroRange: 8,
            nativeAi: "Counselor");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "Firebolt",
                    AuthoritativeProjectileBehavior.Firebolt),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(4, monster.CounselorRetreatTicks);
        Assert.False(monster.IsMoving);
        Assert.Empty(world.Projectiles);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(3, monster.CounselorRetreatTicks);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void HealthyCounselorDoesNotEnterWoundedRetreat()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            maxHitPoints: 10,
            aggroRange: 8,
            nativeAi: "Counselor");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(0, monster.CounselorRetreatTicks);
    }

    [Fact]
    public void HealthyCounselorUsesNativeAdjacentFlashLayersInsteadOfMelee()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            maxHitPoints: 20,
            monsterLevel: 5,
            aggroRange: 8,
            levelId: 1,
            nativeAi: "Counselor",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([CreateFlashSpell()]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 100),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 0,
            levelId: 1,
            life: 6_400,
            lifeMaximum: 6_400);

        // 1 ^ 9 ^ 7 = 15: the native 20-percent adjacent admission passes.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(1U, player.LevelId);
        Assert.Equal(2, world.Projectiles.Count);
        Assert.All(
            world.Projectiles.Values,
            projectile => {
                Assert.Contains(
                    projectile.Behavior,
                    new[] {
                        AuthoritativeProjectileBehavior.FlashBottom,
                        AuthoritativeProjectileBehavior.FlashTop,
                    });
                Assert.Equal(monster.EntityId, projectile.SourceEntityId);
                Assert.Equal(1U, projectile.LevelId);
                Assert.Equal((1, 1), (projectile.PositionX, projectile.PositionY));
                Assert.Equal(10, projectile.Damage);
                Assert.Equal(18U, projectile.RemainingTicks);
            });
        Assert.Equal(6_400 - 10 * DiabloFixedPointQ6.Scale, player.Life);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == monster.EntityId
                && delivery.Event.TargetEntityId == player.EntityId
                && delivery.Event.AmountQ6 == 10 * DiabloFixedPointQ6.Scale);
        Assert.Equal(0, monster.CounselorRetreatTicks);
        Assert.Equal(5, monster.CounselorDelayTicks);
    }

    [Fact]
    public void CounselorUsesValidatedAdjacentFlashAdmissionChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            maxHitPoints: 20,
            monsterId: 1,
            monsterLevel: 5,
            aggroRange: 8,
            levelId: 1,
            nativeAi: "Counselor",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Counselor",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Counselor",
                CounselorAdjacentAttackChanceBase = 100,
                CounselorAdjacentAttackChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            spells: new AuthoritativeSpellCatalog([CreateFlashSpell()]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 100),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 0,
            levelId: 1,
            life: 6_400,
            lifeMaximum: 6_400);

        // 60 ^ 9 ^ 7 = 50: the authored 100-percent band admits the Flash
        // layers where the native 20-percent fallback would defer.
        processor.AdvanceTick(60, [player]);

        Assert.Equal(2, world.Projectiles.Count);
        Assert.Equal(5, monster.CounselorDelayTicks);
    }

    [Fact]
    public void CounselorFlashLayersUseTheConfiguredStableSpellIdentity()
    {
        const uint flashSpellId = 99;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            maxHitPoints: 20,
            monsterLevel: 5,
            aggroRange: 8,
            levelId: 1,
            nativeAi: "Counselor",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([
                CreateFlashSpell() with {
                    SpellId = flashSpellId,
                    FlashMonsterDamageLevelScale = 3,
                },
            ]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 100),
            random: new DiabloRandom(0),
            monsterFlashSpellId: flashSpellId);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 0,
            levelId: 1,
            life: 6_400,
            lifeMaximum: 6_400);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(2, world.Projectiles.Count);
        Assert.All(world.Projectiles.Values, projectile => {
            Assert.Equal(flashSpellId, projectile.SpellId);
            Assert.Equal(15, projectile.Damage);
        });
    }

    [Fact]
    public void CounselorUsesNativeRangedAdmissionBeforeItsMappedMissile()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Counselor",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "Firebolt",
                    AuthoritativeProjectileBehavior.Firebolt),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 60 ^ 9 ^ 7 = 50: the native 50-percent attack roll misses, while
        // the second deterministic roll admits the native 30-percent move.
        processor.AdvanceTick(60, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((1, 1), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void CounselorUsesValidatedRangedAdmissionChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterId: 1,
            nativeAi: "Counselor",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Counselor",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Counselor",
                CounselorAttackChanceBase = 100,
                CounselorAttackChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "Firebolt",
                    AuthoritativeProjectileBehavior.Firebolt),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 60 ^ 9 ^ 7 = 50: the authored 100-percent band admits the ranged
        // attack where the native 50-percent fallback would miss.
        processor.AdvanceTick(60, [player]);

        Assert.Single(world.Projectiles.Values);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void CounselorUsesValidatedMoveAdmissionChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterId: 1,
            nativeAi: "Counselor",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Counselor",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Counselor",
                CounselorMoveChancePercent = 100,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "Firebolt",
                    AuthoritativeProjectileBehavior.Firebolt),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 2992 ^ 9 ^ 7 = 3006: the native 50-percent ranged roll misses,
        // while the second roll is 30 and must be admitted by the authored
        // 100-percent movement chance.
        processor.AdvanceTick(2992, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((0, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void CounselorDefersAfterFailedRangedAndMovementAdmissions()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterId: 1,
            nativeAi: "Counselor",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Counselor",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Counselor",
                CounselorDelayBase = 8,
                CounselorDelayIntelligenceScale = 1,
                CounselorDelayRandomRange = 4,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "Firebolt",
                    AuthoritativeProjectileBehavior.Firebolt),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 5100 ^ 9 ^ 7 = 5090: both the 50-percent attack and 30-percent
        // movement bands miss, so generic pursuit must not take over.
        processor.AdvanceTick(5100, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(9, monster.CounselorDelayTicks);

        processor.AdvanceTick(5_101, [player]);

        Assert.Equal(8, monster.CounselorDelayTicks);
        Assert.False(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerCounselorConsumesItsRangedAdmissionAndDelayDraws()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Counselor",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "FixedFirebolt",
                    AuthoritativeProjectileBehavior.Firebolt,
                    ProjectileDamage: 7),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(5 + delayRoll, monster.CounselorDelayTicks);
        Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt);
    }

    [Fact]
    public void SinglePlayerInitializedCounselorConsumesNativeRangedAdmissionAndDelayDraws()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Counselor",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        var delayRoll = expected.RandomIntLessThan(10);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "FixedFirebolt",
                    AuthoritativeProjectileBehavior.Firebolt,
                    ProjectileDamage: 7),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(5 + delayRoll, monster.CounselorDelayTicks);
        Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt);
    }

    [Fact]
    public void MultiplayerCounselorConsumesItsMoveAdmissionDrawAfterARangedMiss()
    {
        const uint aiSeed = 9;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Counselor",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "FixedFirebolt",
                    AuthoritativeProjectileBehavior.Firebolt,
                    ProjectileDamage: 7),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(73, expected.RandomIntLessThan(100));
        Assert.Equal(6, expected.RandomIntLessThan(100));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerCounselorConsumesItsAdjacentAdmissionAndDelayDraws()
    {
        const uint aiSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            20,
            maxHitPoints: 20,
            aggroRange: 8,
            nativeAi: "Counselor",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(37, expected.RandomIntLessThan(100));
        Assert.Equal(85, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(10 + delayRoll, monster.CounselorDelayTicks);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerCounselorRetreatConsumesItsRandomWalkCoins()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            4,
            maxHitPoints: 10,
            aggroRange: 8,
            nativeAi: "Counselor",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.RandomIntLessThan(100);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(4, monster.CounselorRetreatTicks);

        var expectedSecond = new DiabloRandom(expectedAiSeed);
        var expectedSecondAiSeed = unchecked((uint)expectedSecond.AdvanceRndSeed());
        _ = expectedSecond.RandomIntLessThan(100);
        _ = expectedSecond.FlipCoin(2);
        _ = expectedSecond.FlipCoin(2);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(expectedSecondAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expectedSecond.State, random.State);
        Assert.Equal(3, monster.CounselorRetreatTicks);
        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void CounselorChargedBoltUsesTheMonsterOwnedProjectilePath()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 108,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 50,
            nativeAi: "Counselor",
            intelligence: 1);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                108,
                108,
                "Counselor",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Counselor",
                CounselorAttackDelayBase = 7,
                CounselorAttackDelayRandomRange = 3,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([CreateChargedBoltSpell()]),
            random: new DiabloRandom(0),
            monsterTypes: monsterTypes,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Counselor",
                    "ChargedBolt",
                    AuthoritativeProjectileBehavior.MonsterChargedBolt,
                    AuthoritativeMonsterRangedAttackMode.Ranged,
                    100,
                    108),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectiles = world.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(3, projectiles.Length);
        Assert.All(
            projectiles,
            projectile => {
                Assert.Equal(AuthoritativeProjectileBehavior.MonsterChargedBolt, projectile.Behavior);
                Assert.Equal(30U, projectile.SpellId);
                Assert.Equal(8, projectile.Speed);
                Assert.Equal(15, projectile.Damage);
                Assert.InRange(projectile.PathIndex, 1, 15);
            });
        Assert.Equal(7, monster.CounselorDelayTicks);
        Assert.Equal(100, player.Life);

        // Use a stationary controller at the target tile to characterize the
        // monster-owned collision boundary independently of path wandering.
        world.Projectiles.Clear();
        world.Projectiles.Add(
            100,
            new AuthoritativeProjectile(
                100,
                monster.EntityId,
                player.EntityId,
                30,
                0,
                player.PositionX,
                player.PositionY,
                player.PositionX,
                player.PositionY,
                3,
                AuthoritativeDamageType.Lightning,
                0,
                16) {
                Behavior = AuthoritativeProjectileBehavior.MonsterChargedBolt,
                PathTicks = 16,
                Speed = 8,
                ImpactLifetime = 1,
                ResistantDamageDivisor = 1,
            });

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 128 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.True(player.Life < 100);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7);
    }

    [Fact]
    public void GoatRangedAiFiresAnAuthoritativeArrowAcrossAClearLane()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("GoatRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(100, player.Life);
        Assert.Empty(deliveries);
        Assert.Single(world.Projectiles);

        for (ulong tick = 2; tick < 16 && world.Projectiles.Count != 0; tick++)
            deliveries = deliveries.Concat(processor.AdvanceTick(tick, [player])).ToList();

        Assert.Empty(world.Projectiles);
        Assert.NotEqual(100, player.Life);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal((9U, 7U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
    }

    [Fact]
    public void GoatRangedAiFiresAcrossAnAdjacentClearLaneInsteadOfFallingThroughToMelee()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "GoatRanged",
                    "Arrow",
                    AuthoritativeProjectileBehavior.MonsterArrow,
                    RangedRetreatMinimumDistance: 2,
                    RangedRetreatChanceBase: 1,
                    RangedRetreatChanceIntelligenceScale: 1,
                    ProjectileImpactLifetime: 2),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, outside the configured one-per-100 retreat band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7);
        Assert.True(player.Life < 100);
        Assert.False(monster.IsMoving);
        var arrow = Assert.Single(world.Projectiles).Value;
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, arrow.Behavior);
        Assert.Equal((9U, 7U), (arrow.SourceEntityId, arrow.TargetEntityId));
    }

    [Fact]
    public void GoatRangedAiPreservesItsNativePostAttackDelay()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("GoatRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 214 ^ 9 ^ 7 = 216: native GenerateRnd(20) maps to one delay tick.
        processor.AdvanceTick(214, [player]);

        Assert.Single(world.Projectiles);
        Assert.Equal(1, monster.RangedDelayTicks);

        processor.AdvanceTick(215, [player]);

        Assert.Equal(0, monster.RangedDelayTicks);
        Assert.Single(world.Projectiles);
    }

    [Fact]
    public void MultiplayerGoatRangedConsumesNativeRetreatAndRandomWalkDraws()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: CreateArrowMissileCatalog("GoatRanged"));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerGoatRangedConsumesNativePostShotDelayDraw()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: CreateArrowMissileCatalog("GoatRanged"));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var delayRoll = expected.RandomIntLessThan(20);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(delayRoll, monster.RangedDelayTicks);
        Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.MonsterArrow);
    }

    [Fact]
    public void MonsterArrowDispatchUsesMappedProjectileBehaviorInsteadOfNativeAiLabel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "Acid");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "CustomArrow",
                    AuthoritativeProjectileBehavior.MonsterArrow),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        var arrow = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, arrow.Behavior);
    }

    [Fact]
    public void MappedMonsterArrowPrefersValidatedMonsterIdentityOverRuntimeLabel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            monsterId: 1,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "Acid");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Skeleton Bow",
                0,
                0,
                1,
                4,
                4,
                10,
                2,
                9,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "SkeletonRanged",
                AggroRange = 8,
                SkeletonRangedRetreatMinimumDistance = 4,
                SkeletonRangedRetreatChanceBase = 1,
                SkeletonRangedRetreatChanceIntelligenceScale = 1,
                SkeletonRangedAttackChanceBase = 100,
                SkeletonRangedAttackChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "SkeletonRanged",
                    "CustomArrow",
                    AuthoritativeProjectileBehavior.MonsterArrow),
            ]),
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        var arrow = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, arrow.Behavior);
    }

    [Fact]
    public void MappedMonsterArrowDoesNotFallThroughToAdjacentMelee()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "Acid");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "CustomArrow",
                    AuthoritativeProjectileBehavior.MonsterArrow),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(100, player.Life);
        Assert.Empty(world.Projectiles);
    }

    [Theory]
    [InlineData("Succubus")]
    [InlineData("LazarusSuccubus")]
    [InlineData("Acid")]
    public void BloodStarMappingFiresAUserVisibleMagicProjectile(string nativeAi)
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterLevel: 5,
            nativeAi: nativeAi,
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(36, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                Range = 112,
                ProjectileBehavior = AuthoritativeProjectileBehavior.BloodStar,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                MagicDamageDivisor = 2,
                MagicDamageReductionDivisor = 8,
                SpellLevelDamage = 3,
                SelfDamage = 5,
                ResistantDamageDivisor = 4,
            },
            new AuthoritativeSpellDefinition(37, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                Range = 112,
                ProjectileBehavior = AuthoritativeProjectileBehavior.BloodStar,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                MagicDamageDivisor = 2,
                MagicDamageReductionDivisor = 8,
                SpellLevelDamage = 3,
                SelfDamage = 5,
                ResistantDamageDivisor = 4,
            },
        ]);
        var processor = CreateProcessor(
            world,
            spells,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    nativeAi,
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar,
                    SpellId: 37),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, projectile.Behavior);
        Assert.Equal(9U, projectile.SourceEntityId);
        Assert.Equal(37U, projectile.SpellId);
        Assert.Equal(100, player.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.True(player.Life < 100);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal((9U, 7U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(4 * DiabloFixedPointQ6.Scale, damage.AmountQ6);
    }

    [Theory]
    [InlineData(DiabloGameMode.SinglePlayer, 5, false)]
    [InlineData(DiabloGameMode.SinglePlayer, 6, true)]
    [InlineData(DiabloGameMode.Multiplayer, 5, true)]
    public void LazarusMinionUsesNativeSinglePlayerQuestCombatGate(
        DiabloGameMode gameMode,
        int variable1,
        bool expectedAttack)
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterLevel: 5,
            nativeAi: "LazarusSuccubus",
            intelligence: 0);
        var quest = new AuthoritativeQuestState(
            3016,
            15,
            1,
            active: true,
            variable1: variable1,
            completionUniqueIdentity: "Lazarus",
            completionVariable1: 7);
        var world = new DiabloWorldState(
            combatTargets: [monster],
            quests: [quest]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(37, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                Range = 112,
                ProjectileBehavior = AuthoritativeProjectileBehavior.BloodStar,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                MagicDamageDivisor = 2,
                MagicDamageReductionDivisor = 8,
                SpellLevelDamage = 3,
                SelfDamage = 5,
                ResistantDamageDivisor = 4,
            },
        ]);
        var processor = CreateProcessor(
            world,
            spells,
            gameMode: gameMode,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "LazarusSuccubus",
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar,
                    SpellId: 37),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        if (expectedAttack)
            Assert.Single(world.Projectiles.Values);
        else
            Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void BloodStarMappingUsesConfiguredProjectileTransportMetadata()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterLevel: 5,
            nativeAi: "Succubus",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(37, 1, 0, 0, 0, 0) {
                DamageType = AuthoritativeDamageType.Magic,
                Range = 112,
                ProjectileBehavior = AuthoritativeProjectileBehavior.BloodStar,
                ProjectileSpeed = 16,
                ProjectileLifetime = 256,
                MagicDamageDivisor = 2,
                MagicDamageReductionDivisor = 8,
                SpellLevelDamage = 3,
                SelfDamage = 5,
                ResistantDamageDivisor = 4,
            },
        ]);
        var processor = CreateProcessor(
            world,
            spells,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Succubus",
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar,
                    SpellId: 37,
                    DamageType: AuthoritativeDamageType.Fire,
                    ProjectileSpeed: 7,
                    ProjectileLifetime: 12,
                    ProjectileDamage: 13),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, projectile.Behavior);
        Assert.Equal(AuthoritativeDamageType.Fire, projectile.DamageType);
        Assert.Equal(7, projectile.Speed);
        Assert.Equal(11U, projectile.RemainingTicks);
        Assert.Equal(13, projectile.Damage);
    }

    [Fact]
    public void SuccubusFamilyUsesConfiguredRetreatDistance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterLevel: 5,
            nativeAi: "Succubus",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Succubus",
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar,
                    RangedRetreatMinimumDistance: 5,
                    RangedRetreatChanceBase: 100,
                    RangedRetreatChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SuccubusFamilyUsesConfiguredRetreatChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            monsterLevel: 5,
            nativeAi: "Succubus",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Succubus",
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar,
                    RangedRetreatMinimumDistance: 4,
                    RangedRetreatChanceBase: 1,
                    RangedRetreatChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 1 ^ 9 ^ 7 = 15, outside the configured one-per-100 band.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Single(world.Projectiles);
        Assert.Equal(
            AuthoritativeProjectileBehavior.BloodStar,
            Assert.Single(world.Projectiles.Values).Behavior);
    }

    [Fact]
    public void MultiplayerSuccubusConsumesNativeRetreatAndRandomWalkDraws()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Succubus",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Succubus",
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerSuccubusConsumesNativePostShotDelayDraw()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 4,
            nativeAi: "Succubus",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(36, 1, 0, 0, 0, 0) {
                    DamageType = AuthoritativeDamageType.Magic,
                    Range = 112,
                    ProjectileBehavior = AuthoritativeProjectileBehavior.BloodStar,
                    ProjectileSpeed = 16,
                    ProjectileLifetime = 256,
                    MagicDamageDivisor = 2,
                    MagicDamageReductionDivisor = 8,
                    SpellLevelDamage = 3,
                    SelfDamage = 5,
                    ResistantDamageDivisor = 4,
                },
            ]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Succubus",
                    "BloodStar",
                    AuthoritativeProjectileBehavior.BloodStar,
                    SpellId: 36,
                    ProjectileDamage: 7),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var delayRoll = expected.RandomIntLessThan(20);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(delayRoll, monster.RangedDelayTicks);
        Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.BloodStar);
    }

    [Fact]
    public void SuccubusAiDoesNotFallbackToAPhysicalHitWhenItsMissileLaneIsBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            attackDamage: 99,
            minDamage: 4,
            maxDamage: 4,
            monsterLevel: 5,
            nativeAi: "Succubus");
        var world = new DiabloWorldState(
            fallbackMissileBlockedCells: [4],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void FireBatAiFiresTheValidatedFireboltProjectile()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "FireBat",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var spells = new AuthoritativeSpellCatalog([CreateFireboltSpell()]);
        var monsterMissiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "FireBat",
                "Firebolt",
                AuthoritativeProjectileBehavior.Firebolt,
                SpellId: 52,
                ProjectileSpeed: 17,
                ProjectileLifetime: 19,
                ProjectileImpactLifetime: 7),
        ]);
        var processor = CreateProcessor(
            world,
            spells,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 100),
            random: new DiabloRandom(0),
            monsterMissiles: monsterMissiles);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Firebolt, projectile.Behavior);
        Assert.Equal(9U, projectile.SourceEntityId);
        Assert.Equal(52U, projectile.SpellId);
        Assert.Equal(17, projectile.Speed);
        Assert.Equal(18U, projectile.RemainingTicks);
        Assert.Equal(7, projectile.ImpactLifetime);
        Assert.Equal(100, player.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.True(player.Life < 100);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal((9U, 7U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(3 * DiabloFixedPointQ6.Scale, damage.AmountQ6);
    }

    [Fact]
    public void MultiplayerFireBatConsumesNativeAiRangedCloseRangeRandomWalkDraws()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "FireBat",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([CreateFireboltSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "FireBat",
                    "Firebolt",
                    AuthoritativeProjectileBehavior.Firebolt,
                    ProjectileDamage: 3),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SinglePlayerInitializedTorchantConsumesNativeAiRangedPostShotDelayRoll()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Torchant",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        var delayRoll = expected.RandomIntLessThan(20);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([CreateFireballSpell()]),
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Torchant",
                    "Fireball",
                    AuthoritativeProjectileBehavior.Fireball,
                    ProjectileDamage: 3,
                    RangedDelayBase: 11,
                    RangedDelayRandomRange: 20),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(11 + delayRoll, monster.RangedDelayTicks);
        Assert.Single(world.Projectiles);
    }

    [Fact]
    public void MultiplayerTorchantConsumesNativeAiRangedPostShotDelayRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Torchant",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([CreateFireballSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Torchant",
                    "Fireball",
                    AuthoritativeProjectileBehavior.Fireball,
                    ProjectileDamage: 3,
                    RangedDelayBase: 11,
                    RangedDelayRandomRange: 20),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var delayRoll = expected.RandomIntLessThan(20);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(11 + delayRoll, monster.RangedDelayTicks);
        Assert.Single(world.Projectiles);
    }

    [Fact]
    public void TorchantAiFiresTheValidatedFireballProjectileAndExplosion()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Torchant",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var spells = new AuthoritativeSpellCatalog([CreateFireballSpell()]);
        var monsterMissiles = new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                "Torchant",
                "Fireball",
                AuthoritativeProjectileBehavior.Fireball),
        ]);
        var processor = CreateProcessor(
            world,
            spells,
            random: new DiabloRandom(0),
            monsterMissiles: monsterMissiles);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Fireball, projectile.Behavior);
        Assert.Equal(9U, projectile.SourceEntityId);
        Assert.Equal(12U, projectile.SpellId);
        Assert.Equal(100, player.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.True(player.Life < 100);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7);
    }

    [Fact]
    public void TorchantFireballRollsMonsterDamageAtImpactInsteadOfLaunch()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 9,
            monsterLevel: 5,
            nativeAi: "Torchant",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            spells: new AuthoritativeSpellCatalog([CreateFireballSpell()]),
            random: new DiabloRandom(0),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 100,
                monsterMagicToHitMonsterLevelScale: 0,
                monsterMagicToHitPlayerLevelScale: 0,
                monsterMagicToHitBaseBonus: 100,
                monsterMagicDistancePenalty: 0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Torchant",
                    "Fireball",
                    AuthoritativeProjectileBehavior.Fireball),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(4, projectile.Damage);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7
                && delivery.Event.AmountQ6 == 6 * DiabloFixedPointQ6.Scale);
    }

    [Fact]
    public void MegaAiFiresValidatedInfernoAndOwnsPersistentSegments()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // Tick ^ monster entity ^ player entity = 8, inside Mega's native
        // distance-three 5 * (intelligence + 2) admission band.
        var firstEvents = processor.AdvanceTick(1, [player]);

        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController
                && projectile.SourceEntityId == monster.EntityId);
        var firstSegment = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoSegment);
        Assert.Equal(monster.EntityId, firstSegment.SourceEntityId);
        Assert.Equal(3, firstSegment.Damage);
        Assert.Equal(34U, firstSegment.RemainingTicks);
        Assert.Empty(firstEvents);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == monster.EntityId
                && delivery.Event.TargetEntityId == player.EntityId);
        Assert.True(player.Life < 100);
    }

    [Fact]
    public void MultiplayerMegaInfernoConsumesItsAdmissionAndRecoveryRolls()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    ProjectileDamage: 7),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        var controller = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
        Assert.Equal(7, controller.Damage);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(5 + delayRoll, monster.MegaDelayTicks);
    }

    [Fact]
    public void SinglePlayerInitializedMegaConsumesNativeInfernoAdmissionAndRecoveryRolls()
    {
        const uint initSeed = 13;
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        var delayRoll = expected.RandomIntLessThan(10);
        _ = expected.RandomIntLessThan(100);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior
                == AuthoritativeProjectileBehavior.InfernoController);
        Assert.Equal(5 + delayRoll, monster.MegaDelayTicks);
    }

    [Fact]
    public void MultiplayerMegaInfernoMissConsumesItsMovementAndRecoveryRolls()
    {
        const uint aiSeed = 42;
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    ProjectileDamage: 7),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(11, expected.RandomIntLessThan(100));
        Assert.Equal(25, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
        Assert.Equal(5 + delayRoll, monster.MegaDelayTicks);
    }

    [Fact]
    public void MultiplayerMegaInfernoConsumesItsAdjacentChoiceRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    InfernoMaximumDistance: 5,
                    InfernoAdjacentChanceBase: 40,
                    InfernoAdjacentChanceIntelligenceScale: 10,
                    InfernoRangedFarDistance: 3,
                    InfernoRangedChanceBase: 5,
                    InfernoRangedChanceIntelligenceScale: 5,
                    InfernoRangedFarIntelligenceOffset: 1,
                    InfernoAdjacentChoiceModulo: 1,
                    ProjectileDamage: 7),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(1);
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        var controller = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
        Assert.Equal(7, controller.Damage);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(5 + delayRoll, monster.MegaDelayTicks);
    }

    [Fact]
    public void MegaInfernoUsesConfiguredProjectileDamageForEachSegment()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "FixedInferno",
                    AuthoritativeProjectileBehavior.InfernoController,
                    ProjectileDamage: 7),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var controller = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
        Assert.Equal(7, controller.Damage);
        var segment = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoSegment);
        Assert.Equal(7, segment.Damage);
        Assert.Equal(54U, segment.RemainingTicks);
    }

    [Fact]
    public void MegaAiDefersInfernoAtNativeSkeletonFallbackDistance()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 6,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
    }

    [Fact]
    public void MultiplayerMegaConsumesItsNativeFallbackDelayRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(4, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(15 + delayRoll, monster.SkeletonMeleeDelayTicks);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void SinglePlayerInitializedMegaConsumesNativeFallbackDelayRoll()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(100);
        var delayRoll = expected.RandomIntLessThan(10);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.Equal(15 + delayRoll, monster.SkeletonMeleeDelayTicks);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerMegaConsumesItsNativeFallbackRandomWalkRolls()
    {
        const uint aiSeed = 17;
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(48, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MegaAiUsesNativeRandomWalkAfterAnIntermediateInfernoMiss()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    InfernoMaximumDistance: 5,
                    InfernoAdjacentChanceBase: 40,
                    InfernoAdjacentChanceIntelligenceScale: 10,
                    InfernoRangedFarDistance: 3,
                    InfernoRangedChanceBase: 1,
                    InfernoRangedChanceIntelligenceScale: 1,
                    InfernoRangedFarIntelligenceOffset: 1,
                    InfernoAdjacentChoiceModulo: 2),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 14 ^ 7 = 8: the one-percent Inferno roll misses, while the
        // native 50-percent RandomWalk fallback admits its first candidate.
        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Equal(5, monster.MegaDelayTicks);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MegaAiUsesValidatedIntermediateMovementChance()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            monsterId: 1,
            nativeAi: "Mega",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                85,
                "Slayer",
                1,
                16,
                20,
                120,
                140,
                60,
                12,
                20,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Mega",
                MegaIntermediateMovementChanceBase = 100,
                MegaIntermediateMovementChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    InfernoMaximumDistance: 5,
                    InfernoAdjacentChanceBase: 40,
                    InfernoAdjacentChanceIntelligenceScale: 10,
                    InfernoRangedFarDistance: 3,
                    InfernoRangedChanceBase: 1,
                    InfernoRangedChanceIntelligenceScale: 1,
                    InfernoRangedFarIntelligenceOffset: 1,
                    InfernoAdjacentChoiceModulo: 2),
            ]),
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 5100 ^ 14 ^ 7 = 5109: outside the native 50-percent band. The
        // authored 100-percent band must still admit the RandomWalk branch.
        processor.AdvanceTick(5100, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Equal(10, monster.MegaDelayTicks);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MegaAiDefersAfterAnIntermediateInfernoMissWhenRandomWalkRollFails()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    InfernoMaximumDistance: 5,
                    InfernoAdjacentChanceBase: 40,
                    InfernoAdjacentChanceIntelligenceScale: 10,
                    InfernoRangedFarDistance: 3,
                    InfernoRangedChanceBase: 1,
                    InfernoRangedChanceIntelligenceScale: 1,
                    InfernoRangedFarIntelligenceOffset: 1,
                    InfernoAdjacentChoiceModulo: 2),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 5100 ^ 14 ^ 7 = 5109: the second native 50-percent movement roll
        // misses, so the server must defer instead of generic-pursuing.
        processor.AdvanceTick(5100, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MegaAiUsesSkeletonMovementBeyondItsInfernoRange()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 12, 12));
        var monster = new AuthoritativeCombatTarget(
            14,
            6,
            6,
            20,
            levelId: 0,
            aggroRange: 8,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            levelId: 0,
            entityId: 7);

        // 44 ^ 14 ^ 7 = 37: outside SkeletonAi's native 35-point delay band.
        processor.AdvanceTick(44, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((5, 6), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
    }

    [Fact]
    public void MegaAiUsesValidatedDelayAdmissionChance()
    {
        var nativeWorld = new AuthoritativeWorld();
        nativeWorld.AddLevel(new AuthoritativeLevel(0, 12, 12));
        var monster = new AuthoritativeCombatTarget(
            14,
            6,
            6,
            20,
            levelId: 0,
            monsterId: 1,
            aggroRange: 8,
            nativeAi: "Mega",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Slayer",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Mega",
                MegaDelayChanceBase = 100,
                MegaDelayChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(nativeWorld, combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 6,
            levelId: 0,
            entityId: 7);

        // 44 ^ 14 ^ 7 = 37, outside the native 35-point band. The authored
        // 100-point band must still enter the delegated Skeleton delay.
        processor.AdvanceTick(44, [player]);

        Assert.Equal(15, monster.SkeletonMeleeDelayTicks);
        Assert.False(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MegaAiPersistsTheDelegatedSkeletonDelayBeyondItsInfernoRange()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            6,
            6,
            20,
            aggroRange: 8,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 6,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 14 ^ 7 = 8: the delegated SkeletonAi delay band admits AiDelay.
        processor.AdvanceTick(1, [player]);

        Assert.Equal(15, monster.SkeletonMeleeDelayTicks);
        Assert.False(monster.IsMoving);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(14, monster.SkeletonMeleeDelayTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void MegaAiDefersWhenItsSkeletonMovementCandidatesAreBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2, 41, 42],
            combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 14 ^ 7 = 37, outside SkeletonAi's native 35-point delay
        // band. Every native RandomWalk candidate is blocked.
        processor.AdvanceTick(40, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MegaAiFallsBackWhenItsInfernoLaneIsBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(
            fallbackMissileBlockedCells: [2],
            combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
    }

    [Fact]
    public void MegaAiUsesConfiguredInfernoMaximumDistance()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    InfernoMaximumDistance: 4,
                    InfernoAdjacentChanceBase: 40,
                    InfernoAdjacentChanceIntelligenceScale: 10,
                    InfernoRangedFarDistance: 3,
                    InfernoRangedChanceBase: 5,
                    InfernoRangedChanceIntelligenceScale: 5,
                    InfernoRangedFarIntelligenceOffset: 1,
                    InfernoAdjacentChoiceModulo: 2),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // The configured maximum distance is four, so distance four must
        // defer even though the native compatibility maximum is five.
        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
    }

    [Fact]
    public void MegaAiUsesConfiguredInfernoRangedChance()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    InfernoMaximumDistance: 5,
                    InfernoAdjacentChanceBase: 40,
                    InfernoAdjacentChanceIntelligenceScale: 10,
                    InfernoRangedFarDistance: 3,
                    InfernoRangedChanceBase: 1,
                    InfernoRangedChanceIntelligenceScale: 1,
                    InfernoRangedFarIntelligenceOffset: 0,
                    InfernoAdjacentChoiceModulo: 2),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 1 ^ 14 ^ 7 = 8, outside the configured one-per-100 ranged band.
        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
    }

    [Fact]
    public void MegaAiUsesConfiguredInfernoAdjacentChoiceModulo()
    {
        var monster = new AuthoritativeCombatTarget(
            14,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Mega",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Mega",
                    "InfernoControl",
                    AuthoritativeProjectileBehavior.InfernoController,
                    InfernoMaximumDistance: 5,
                    InfernoAdjacentChanceBase: 100,
                    InfernoAdjacentChanceIntelligenceScale: 1,
                    InfernoRangedFarDistance: 3,
                    InfernoRangedChanceBase: 5,
                    InfernoRangedChanceIntelligenceScale: 5,
                    InfernoRangedFarIntelligenceOffset: 1,
                    InfernoAdjacentChoiceModulo: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 100 ^ 14 ^ 7 = 105: the native modulo-two choice would defer the
        // adjacent special, while the configured modulo-one choice admits it.
        processor.AdvanceTick(100, [player]);

        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
    }

    [Theory]
    [InlineData("Lich", "OrangeFlare")]
    [InlineData("ArchLich", "YellowFlare")]
    [InlineData("Psychorb", "BlueFlare")]
    [InlineData("Necromorb", "RedFlare")]
    [InlineData("BoneDemon", "BlueFlare2")]
    public void FlareMonsterAiFiresTheValidatedMonsterMagicProjectile(
        string nativeAi,
        string missileId)
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 4,
            maxDamage: 10,
            monsterLevel: 5,
            nativeAi: nativeAi,
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 100),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    nativeAi,
                    missileId,
                    AuthoritativeProjectileBehavior.MonsterMagic),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(9U, projectile.SourceEntityId);
        // Native AddGenericMagicMissile captures the monster range at launch.
        Assert.Equal(4, projectile.Damage);
        Assert.Equal(100, player.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.True(player.Life < 100);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal((9U, 7U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(4 * DiabloFixedPointQ6.Scale, damage.AmountQ6);
    }

    [Fact]
    public void MonsterMagicProjectileUsesConfiguredHitAdmissionFormula()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Lich",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 0,
                monsterMagicToHitMonsterLevelScale: 0,
                monsterMagicToHitPlayerLevelScale: 0,
                monsterMagicToHitBaseBonus: 0,
                monsterMagicDistancePenalty: 0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Lich",
                    "OrangeFlare",
                    AuthoritativeProjectileBehavior.MonsterMagic),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void BoneDemonAiDefersBlueFlareOutsideItsNativeAvoidanceBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "BoneDemon",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "BoneDemon",
                    "BlueFlare2",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 5000 ^ 9 ^ 7 = 5006, outside the native 1,000-per-10,000 band.
        processor.AdvanceTick(5000, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void StormAiFiresTheValidatedThinLightningController()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Storm",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateLightningSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Storm",
                    "ThinLightningControl",
                    AuthoritativeProjectileBehavior.Lightning,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var controller = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.SourceEntityId == 9
                && projectile.Behavior == AuthoritativeProjectileBehavior.Lightning);
        Assert.Equal(32, controller.Speed);
        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.True(player.Life < 100);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7
                && delivery.Event.Amount == 6);
    }

    [Fact]
    public void StormLightningRollsItsMonsterDamageWithNativeRngEachTick()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 9,
            monsterLevel: 5,
            nativeAi: "Storm",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateLightningSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Storm",
                    "ThinLightningControl",
                    AuthoritativeProjectileBehavior.Lightning,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);
        processor.AdvanceTick(2, [player]);

        var segment = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.LightningSegment);
        // DiabloRandom(0)'s first range draw is zero, so native
        // 2 * RandomIntBetween(3, 9) yields six.
        Assert.Equal(6, segment.Damage);
    }

    [Fact]
    public void MagmaAiFiresTheValidatedFireProjectile()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterMagicMinimumHitChance: 100),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(AuthoritativeDamageType.Fire, projectile.DamageType);
        Assert.Equal(16, projectile.Speed);
        Assert.Equal((2, 0), (projectile.PositionX, projectile.PositionY));
        Assert.Equal(3, projectile.Damage);
        Assert.Equal(100, player.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 2; tick < 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.Empty(world.Projectiles);
        Assert.True(player.Life < 100);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7
                && delivery.Event.AmountQ6 == 3 * DiabloFixedPointQ6.Scale);
    }

    [Fact]
    public void SinglePlayerInitializedMagmaConsumesNativeRangedAvoidanceAdmissionAndDamageRolls()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.RandomIntLessThan(10_000);
        _ = expected.FlipCoin(4);
        _ = expected.RandomIntBetween(3, 3);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(3, projectile.Damage);
    }

    [Fact]
    public void MultiplayerMagmaConsumesNativeRangedAvoidanceAdmissionAndDamageRolls()
    {
        const uint aiSeed = 2;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(85, expected.RandomIntLessThan(10_000));
        Assert.False(expected.FlipCoin(4));
        _ = expected.RandomIntBetween(3, 3);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterMagic, projectile.Behavior);
        Assert.Equal(3, projectile.Damage);
    }

    [Fact]
    public void MultiplayerMagmaConsumesNativeRangedAvoidanceRandomWalkDraws()
    {
        const uint aiSeed = 9;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(1173, expected.RandomIntLessThan(10_000));
        Assert.False(expected.FlipCoin(4));
        Assert.Equal(7, expected.RandomIntLessThan(100));
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((6, 5), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerMagmaConsumesNativeRangedAvoidanceRecoveryDelayRoll()
    {
        const uint aiSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        Assert.Equal(2637, expected.RandomIntLessThan(10_000));
        Assert.False(expected.FlipCoin(4));
        Assert.Equal(90, expected.RandomIntLessThan(100));
        var delayRoll = expected.RandomIntLessThan(10);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(5 + delayRoll, monster.RangedAvoidanceDelayTicks);
        Assert.False(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MagmaAiDefersMagmaBallOutsideItsNativeAvoidanceBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 5000 ^ 9 ^ 7 = 5006, outside the native 1,000-per-10,000 band.
        processor.AdvanceTick(5000, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void RangedAvoidanceUsesNativeAdjacentAttackBandBeforeMeleeFallback()
    {
        (AuthoritativeCombatTarget Monster, AuthoritativePlayerState Player,
            DiabloCombatCommandProcessor Processor) CreateScenario()
        {
            var monster = new AuthoritativeCombatTarget(
                9,
                0,
                0,
                20,
                aggroRange: 8,
                attackDamage: 5,
                nativeAi: "Magma",
                intelligence: 0);
            var world = new DiabloWorldState(combatTargets: [monster]);
            var processor = CreateProcessor(
                world,
                monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                    new AuthoritativeMonsterMissileDefinition(
                        "Magma",
                        "MagmaBall",
                        AuthoritativeProjectileBehavior.MonsterMagic,
                        AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
                ]));
            var player = CreatePlayer(
                entityId: 7,
                positionX: 1,
                positionY: 0,
                life: 100,
                lifeMaximum: 100);
            return (monster, player, processor);
        }

        var admitted = CreateScenario();
        admitted.Processor.AdvanceTick(5_000, [admitted.Player]);

        // 5,000 ^ 9 ^ 7 = 5,006, inside the native 6,000-per-10,000 band.
        Assert.Equal(95, admitted.Player.Life);

        var deferred = CreateScenario();
        deferred.Processor.AdvanceTick(7_000, [deferred.Player]);

        Assert.Equal(100, deferred.Player.Life);
    }

    [Fact]
    public void RangedAvoidanceUsesValidatedAdjacentAttackBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            aggroRange: 8,
            attackDamage: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceAdjacentAttackChanceBase: 1_000,
                    RangedAvoidanceAdjacentAttackChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 5,000 ^ 9 ^ 7 = 5,006: authored 1,000-per-10,000 tuning rejects
        // the adjacent attack that the native 6,000-per-10,000 fallback admits.
        processor.AdvanceTick(5_000, [player]);

        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void RangedAvoidanceUsesValidatedAdjacentAttackChanceDivisor()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            aggroRange: 8,
            attackDamage: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceAdjacentAttackChanceBase: 6_000,
                    RangedAvoidanceAdjacentAttackChanceIntelligenceScale: 1_000,
                    RangedAvoidanceAdjacentAttackChanceDivisor: 2),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 5,000 ^ 9 ^ 7 = 5,006: the validated divisor halves the 6,000
        // band to 3,000, so this roll is rejected.
        processor.AdvanceTick(5_000, [player]);

        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void RangedAvoidanceUsesNativeRoundWalkOrderWhenItsRangedRollFails()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 5,002 ^ 9 ^ 7 = 4,996 selects the native far-distance Move branch.
        processor.AdvanceTick(5_002, [player]);

        Assert.Empty(world.Projectiles);
        Assert.True(monster.IsMoving);
        // The left-turn RoundWalk checks north first.
        Assert.Equal((5, 4), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void RangedAvoidanceUsesNativeRandomWalkOnItsOrdinaryFailedRangedRoll()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 5,000 ^ 9 ^ 7 = 4,998 misses the far-distance Move admission, so
        // native AiRangedAvoidance uses its ordinary RandomWalk branch.
        processor.AdvanceTick(5_000, [player]);

        Assert.Empty(world.Projectiles);
        Assert.True(monster.IsMoving);
        Assert.Equal((6, 5), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void RangedAvoidanceUsesItsNativeFreshMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0,
            rangedAvoidanceMovementIdleTicks: 0,
            rangedAvoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 4,944 ^ 9 ^ 7 = 4,958: the ordinary ranged roll fails, while
        // movement roll 50 is admitted only by the fresh 80% band.
        processor.AdvanceTick(4_944, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(0, monster.RangedAvoidanceMovementIdleTicks);
    }

    [Fact]
    public void RangedAvoidanceUsesValidatedOrdinaryMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0,
            rangedAvoidanceMovementIdleTicks: 0,
            rangedAvoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceMinimumDistance: 2,
                    RangedAvoidanceFarDistance: 3,
                    RangedAvoidanceChanceBase: 500,
                    RangedAvoidanceChanceIntelligenceScale: 500) {
                    RangedAvoidanceMovementChanceBase = 1,
                    RangedAvoidanceMovementChanceIntelligenceScale = 1,
                    RangedAvoidanceMovementFreshChanceBonus = 1,
                },
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 4,944 ^ 9 ^ 7 = 4,958: the authored fresh-mode band is two
        // percent, so movement roll 49 must defer instead of using native 80%.
        processor.AdvanceTick(4_944, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(1, monster.RangedAvoidanceMovementIdleTicks);
    }

    [Fact]
    public void RangedAvoidanceUsesItsNativeLongIdleMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0,
            rangedAvoidanceMovementIdleTicks: 21,
            rangedAvoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 4,992 ^ 9 ^ 7 = 5,006: the movement roll is exactly 50, outside
        // the native long-idle 50% band.
        processor.AdvanceTick(4_992, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(22, monster.RangedAvoidanceMovementIdleTicks);
    }

    [Fact]
    public void RangedAvoidanceDefersInsideItsNativeMiddleMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0,
            rangedAvoidanceMovementIdleTicks: 10,
            rangedAvoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // var2 values 1..20 do not admit ordinary RandomWalk movement.
        processor.AdvanceTick(4_944, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.RangedAvoidanceMovementIdleTicks);
        Assert.Equal(9, monster.RangedAvoidanceDelayTicks);

        processor.AdvanceTick(4_945, [player]);

        Assert.Equal(8, monster.RangedAvoidanceDelayTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void RangedAvoidanceCarriesItsNativeMoveGoalAcrossTicks()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // The first far-distance Move admission starts goalVar1 at zero.
        processor.AdvanceTick(5_002, [player]);
        Assert.Equal(1, monster.RangedAvoidanceMoveTicks);

        // The next tick continues the persisted Move goal even though its
        // independent far-distance admission roll is not selected.
        processor.AdvanceTick(5_003, [player]);
        Assert.Equal(2, monster.RangedAvoidanceMoveTicks);
        Assert.Equal(1, monster.RangedAvoidanceTurn);
    }

    [Fact]
    public void RangedAvoidanceFlipsItsNativeTurnStateAfterBlockedRoundWalk()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            rangedAvoidanceMoveTicks: 0,
            rangedAvoidanceTurn: 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [206, 245, 246],
            combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // The primary southeast, south, and east candidates are blocked.
        // Native RoundWalk tries its opposite-side RandomWalk and flips
        // goalVar2 before selecting north.
        processor.AdvanceTick(5_000, [player]);

        Assert.Equal((5, 4), (monster.FuturePositionX, monster.FuturePositionY));
        Assert.Equal(1, monster.RangedAvoidanceTurn);
        Assert.Equal(1, monster.RangedAvoidanceMoveTicks);
    }

    [Fact]
    public void RangedAvoidanceUsesConfiguredDistanceBoundary()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceMinimumDistance: 4,
                    RangedAvoidanceFarDistance: 5,
                    RangedAvoidanceChanceBase: 10_000,
                    RangedAvoidanceChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // The configured minimum distance supersedes the native compatibility
        // default of two tiles, so this clear lane must remain idle.
        processor.AdvanceTick(1, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void RangedAvoidanceUsesConfiguredChanceBoundary()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 0,
            maxDamage: 0,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceMinimumDistance: 2,
                    RangedAvoidanceFarDistance: 3,
                    RangedAvoidanceChanceBase: 1,
                    RangedAvoidanceChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // Tick 1 produces seed 15 (1 ^ 9 ^ 7), which is outside the
        // configured one-per-10,000 near-distance band.
        processor.AdvanceTick(1, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void RangedAvoidanceUsesValidatedMovementGoalModulo()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            20,
            aggroRange: 8,
            minDamage: 3,
            maxDamage: 3,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0,
            rangedAvoidanceMovementIdleTicks: 10,
            rangedAvoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    RangedAvoidanceMinimumDistance: 2,
                    RangedAvoidanceFarDistance: 3,
                    RangedAvoidanceChanceBase: 500,
                    RangedAvoidanceChanceIntelligenceScale: 500) {
                    RangedAvoidanceMovementChanceModulo = 5,
                },
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 5,002 ^ 9 ^ 7 = 4,996: the native modulo-four fallback would
        // admit the far-distance Move goal, but validated modulo five does not.
        processor.AdvanceTick(5_002, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.RangedAvoidanceMovementIdleTicks);
    }

    [Fact]
    public void AcidAiCreatesAValidatedSplatAndPersistentPuddle()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            monsterLevel: 5,
            nativeAi: "Acid",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "ConfiguredAcid",
                    AuthoritativeProjectileBehavior.Acid,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    50,
                    ProjectileLifetimeBase: 22,
                    ProjectileLifetimeIntelligenceScale: 4,
                    PuddleDurationRandomRange: 1,
                    PuddleDurationBase: 100,
                    PuddleDurationIntelligenceScale: 1,
                    PuddleDamageMinimumMonsterLevel: 1,
                    PuddleDamageLow: 3,
                    PuddleDamageHigh: 7,
                    PuddleImpactLifetime: 4,
                    DamageType: AuthoritativeDamageType.Acid),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);
        var puddlePlayer = CreatePlayer(
            entityId: 8,
            positionX: 8,
            positionY: 1,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player, puddlePlayer]);

        var acid = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Acid, acid.Behavior);
        Assert.Equal(AuthoritativeDamageType.Acid, acid.DamageType);
        Assert.Equal(16, acid.Speed);
        Assert.Equal(21U, acid.RemainingTicks);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        AuthoritativeProjectile? puddle = null;
        for (ulong tick = 2; tick < 64; tick++) {
            deliveries.AddRange(processor.AdvanceTick(tick, [player, puddlePlayer]));
            puddle ??= world.Projectiles.Values.FirstOrDefault(projectile =>
                    projectile.Behavior == AuthoritativeProjectileBehavior.AcidPuddle);
            if (puddle is not null
                && deliveries.Any(delivery =>
                    delivery.Event.TargetEntityId == puddlePlayer.EntityId))
                break;
        }

        Assert.NotNull(puddle);
        Assert.Equal((8, 1), (puddle!.PositionX, puddle.PositionY));
        Assert.Equal(98U, puddle.RemainingTicks);
        Assert.Equal(7, puddle.Damage);
        for (ulong tick = 64; tick < 200 && puddle.Phase != AuthoritativeProjectilePhase.Impact; tick++)
            processor.AdvanceTick(tick, [player, puddlePlayer]);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, puddle.Phase);
        Assert.Equal(4U, puddle.RemainingTicks);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 8
                && delivery.Event.AmountQ6 == 7 * DiabloFixedPointQ6.Scale);
    }

    [Fact]
    public void AcidPuddleUsesConfiguredHitAdmissionFormula()
    {
        var world = new DiabloWorldState();
        world.Projectiles.Add(
            100,
            new AuthoritativeProjectile(
                100,
                9,
                0,
                0,
                0,
                1,
                1,
                1,
                1,
                1,
                AuthoritativeDamageType.Acid,
                0,
                10U,
                allowZeroSpellId: true) {
                Behavior = AuthoritativeProjectileBehavior.AcidPuddle,
            });
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterAcidPuddleMinimumHitChance: 0,
                monsterAcidPuddleBaseHitChance: 0));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 1,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void AcidPuddleBreaksABlockingObjectWithoutEndingThePuddle()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            1,
            breakable: true);
        var puddle = new AuthoritativeProjectile(
            100,
            9,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            1,
            AuthoritativeDamageType.Acid,
            0,
            10U,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.AcidPuddle,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            projectiles: [puddle]);
        var processor = CreateProcessor(world);

        processor.AdvanceTick(1, []);

        Assert.True(blocker.Destroyed);
        Assert.Equal(9U, puddle.RemainingTicks);
        Assert.Contains(puddle, world.Projectiles.Values);
    }

    [Fact]
    public void GenericProjectileBreaksABlockingObjectBeforeRemoval()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            breakable: true);
        var projectile = new AuthoritativeProjectile(
            100,
            9,
            0,
            0,
            0,
            0,
            0,
            3,
            0,
            1,
            AuthoritativeDamageType.Physical,
            0,
            3,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.Targeted,
            Speed = 1,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);

        processor.AdvanceTick(1, []);

        Assert.True(blocker.Destroyed);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void AcidAiDefersItsProjectileOutsideTheNativeHalfRateAvoidanceBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            monsterLevel: 5,
            nativeAi: "Acid",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Acid",
                    "Acid",
                    AuthoritativeProjectileBehavior.Acid,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    50),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 1000 ^ 9 ^ 7 = 1006, outside Acid's 250-per-10,000 band.
        processor.AdvanceTick(1000, [player]);

        Assert.Empty(world.Projectiles);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void AcidUniqueUsesTheSharedAiRangedProjectileBoundary()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            monsterLevel: 5,
            nativeAi: "AcidUnique",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "AcidUnique",
                    "Acid",
                    AuthoritativeProjectileBehavior.Acid),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Acid, projectile.Behavior);
        Assert.Equal(AuthoritativeDamageType.Acid, projectile.DamageType);
        Assert.Equal(16, projectile.Speed);
    }

    [Fact]
    public void MappedRangedAttackUsesConfiguredRetreatDistance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    RangedRetreatMinimumDistance: 5,
                    RangedRetreatChanceBase: 100,
                    RangedRetreatChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MappedRangedAttackUsesConfiguredRetreatChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 2,
            monsterLevel: 5,
            nativeAi: "Magma",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Magma",
                    "MagmaBall",
                    AuthoritativeProjectileBehavior.MonsterMagic,
                    RangedRetreatMinimumDistance: 4,
                    RangedRetreatChanceBase: 1,
                    RangedRetreatChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        // 1 ^ 9 ^ 7 = 15, outside the configured one-per-100 band.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Single(world.Projectiles);
    }

    [Fact]
    public void DiabloAiEmitsAValidatedApocalypseBoomForEachClearLanePlayer()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            aggroRange: 8,
            monsterLevel: 5,
            nativeAi: "Diablo",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateApocalypseSpell()]),
            random: new DiabloRandom(0),
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "Diablo",
                    "DiabloApocalypse",
                    AuthoritativeProjectileBehavior.ApocalypseController,
                    AuthoritativeMonsterRangedAttackMode.RangedAvoidance,
                    ProjectileDamage: 40),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 1);

        var firstDeliveries = processor.AdvanceTick(1, [player]);

        var boom = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.ApocalypseBoom, boom.Behavior);
        Assert.Equal((9U, 7U), (boom.SourceEntityId, boom.TargetEntityId));
        Assert.Equal(AuthoritativeDamageType.Physical, boom.DamageType);
        Assert.Equal(40, boom.Damage);
        Assert.True(player.Life < 100);
        Assert.Contains(
            firstDeliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == 9
                && delivery.Event.TargetEntityId == 7
                && delivery.Event.Amount == 40);

        var deliveries = processor.AdvanceTick(2, [player]);

        Assert.Empty(deliveries);
    }

    [Fact]
    public void GoatRangedAiAvoidsAPlayerInsideItsNativeFourTileBoundary()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            4,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("GoatRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, inside the native 10 * (intelligence + 7) band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.True(monster.IsMoving);
        Assert.True(monster.FuturePositionX < monster.PositionX);
    }

    [Fact]
    public void GoatRangedAiUsesConfiguredRetreatDistance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "GoatRanged",
                    "Arrow",
                    AuthoritativeProjectileBehavior.MonsterArrow,
                    RangedRetreatMinimumDistance: 5,
                    RangedRetreatChanceBase: 100,
                    RangedRetreatChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 4,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void GoatRangedAiUsesConfiguredRetreatChance()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: new AuthoritativeMonsterMissileCatalog([
                new AuthoritativeMonsterMissileDefinition(
                    "GoatRanged",
                    "Arrow",
                    AuthoritativeProjectileBehavior.MonsterArrow,
                    RangedRetreatMinimumDistance: 4,
                    RangedRetreatChanceBase: 1,
                    RangedRetreatChanceIntelligenceScale: 1),
            ]));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, outside the configured one-per-100 band.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Single(world.Projectiles);
    }

    [Fact]
    public void GoatRangedAiDefersWhenItsMissileLaneIsBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 100,
            nativeAi: "GoatRanged");
        var world = new DiabloWorldState(
            fallbackMissileBlockedCells: [4],
            combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterMissiles: CreateArrowMissileCatalog("GoatRanged"));
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void GoatMeleeAiUsesItsNativeAttackAdmissionAndWoundedSpecialBranch()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 2,
            aggroRange: 5,
            minDamage: 3,
            maxDamage: 3,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            toHit: 100,
            toHitSpecial: 100,
            nativeAi: "GoatMelee");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: inside 23 + 2 * intelligence and the native
        // second 50% roll, so the wounded special range is selected.
        var deliveries = processor.AdvanceTick(1, [player]);

        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        var expected = new DiabloRandom(0);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
        Assert.Equal(100 - expectedDamage, player.Life);
    }

    [Fact]
    public void GharbadUsesItsNativeAiAvoidanceCombatBranch()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            attackDamage: 2,
            aggroRange: 5,
            minDamage: 3,
            maxDamage: 3,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            toHit: 100,
            toHitSpecial: 100,
            nativeAi: "Gharbad");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // Gharbad's normal post-dialogue combat delegates to AiAvoidance:
        // 1 ^ 9 ^ 7 = 15 admits the 23% attack and its wounded special branch.
        var deliveries = processor.AdvanceTick(1, [player]);

        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        var expected = new DiabloRandom(0);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
        Assert.Equal(100 - expectedDamage, player.Life);
    }

    [Fact]
    public void GoatMeleeAiUsesValidatedWoundedSpecialThresholdIndependentOfNativeLabel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            70,
            maxHitPoints: 100,
            monsterId: 1,
            attackDamage: 3,
            aggroRange: 5,
            minDamage: 3,
            maxDamage: 3,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            toHit: 100,
            toHitSpecial: 100,
            nativeAi: "Fallen");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Goat",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                8,
                20,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GoatMeleeAttackChanceBase = 100,
                GoatMeleeAttackChanceIntelligenceScale = 1,
                GoatMeleeSpecialHealthThresholdPercent = 80,
                GoatMeleeSpecialChanceGroupPeriod = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        var expected = new DiabloRandom(0);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
        Assert.Equal(100 - expectedDamage, player.Life);
    }

    [Fact]
    public void GoatMeleeAiUsesStandaloneSpecialTuningWithNativeAttackDefaults()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            70,
            maxHitPoints: 100,
            monsterId: 1,
            attackDamage: 3,
            aggroRange: 5,
            minDamage: 3,
            maxDamage: 3,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            toHit: 100,
            toHitSpecial: 100,
            nativeAi: "Fallen");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Goat",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                8,
                20,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GoatMeleeSpecialHealthThresholdPercent = 80,
                GoatMeleeSpecialChanceGroupPeriod = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: native 23 + 2 attack defaults admit the hit, and
        // the standalone validated special tuple selects the wounded branch.
        var deliveries = processor.AdvanceTick(1, [player]);

        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        var expected = new DiabloRandom(0);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
        Assert.Equal(100 - expectedDamage, player.Life);
    }

    [Fact]
    public void GoatMeleeAiCanDeclineAnAdjacentAttackOutsideItsNativeBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "GoatMelee");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38: outside the native 23% admission band.
        var deliveries = processor.AdvanceTick(40, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void GoatMeleeAiUsesValidatedAdjacentAttackTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            monsterId: 1,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Goat",
                1,
                16,
                1,
                1,
                10,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GoatMeleeAttackChanceBase = 10,
                GoatMeleeAttackChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, outside the configured ten-percent band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void GoatMeleeAiUsesItsNativeNonAdjacentMovementAdmission()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: admitted to the native 28% movement band.
        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));

        // 40 ^ 9 ^ 7 = 38: outside the movement band after completion.
        Assert.Empty(processor.AdvanceTick(40, [player]));
        Assert.False(monster.IsMoving);
        Assert.Equal((2, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void GoatMeleeAiUsesItsNativeFreshAvoidanceMovementBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "GoatMelee",
            intelligence: 0,
            avoidanceMovementIdleTicks: 0,
            avoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 50 ^ 9 ^ 7 = 60: the fresh var2 == 0 movement band admits the
        // native 78-point AiAvoidance chance.
        processor.AdvanceTick(50, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(0, monster.AvoidanceMovementIdleTicks);
    }

    [Fact]
    public void GoatMeleeAiUsesValidatedFreshAvoidanceMovementBonus()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                34,
                "Flesh Clan",
                0,
                0,
                8,
                30,
                45,
                15,
                4,
                10,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "GoatMelee",
                GoatMeleeMovementChanceBase = 10,
                GoatMeleeMovementChanceIntelligenceScale = 1,
                AiAvoidanceMovementFreshChanceBonus = 100,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            monsterId: 1,
            nativeAi: "GoatMelee",
            intelligence: 0,
            avoidanceMovementIdleTicks: 0,
            avoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 50 ^ 9 ^ 7 = 60: the configured ten-percent long-idle band plus
        // the validated 100-point fresh bonus admits this movement.
        processor.AdvanceTick(50, [player]);

        Assert.True(monster.IsMoving);
    }

    [Fact]
    public void GoatMeleeAiDefersInsideItsNativeAvoidanceMovementBands()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "GoatMelee",
            intelligence: 0,
            avoidanceMovementIdleTicks: 10,
            avoidanceMovementMode: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: var2 values 1..20 do not admit ordinary movement.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(11, monster.AvoidanceMovementIdleTicks);
    }

    [Fact]
    public void GargoyleAiUsesItsNativeAvoidanceMovementCounter()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            aggroRange: 5,
            nativeAi: "Gargoyle",
            intelligence: 0,
            avoidanceMovementIdleTicks: 21,
            avoidanceMovementMode: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: the long-idle 28-point Gargoyle band admits walk.
        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal(0, monster.AvoidanceMovementIdleTicks);
    }

    [Fact]
    public void GoatMeleeAiUsesDeterministicRoundWalkInItsFarDistanceBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            40,
            attackDamage: 2,
            aggroRange: 7,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: admitted to the native 28% movement band and its
        // deterministic far-distance RoundWalk branch.
        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.True(monster.IsMoving);
        Assert.NotEqual(5, monster.FuturePositionY);
        Assert.Equal(1, monster.AvoidanceMovementTicks);
        Assert.Equal(0, monster.AvoidanceMovementTurn);

        // The next tick continues the native Move goal instead of rerolling
        // the far-distance admission.
        processor.AdvanceTick(2, [player]);
        Assert.Equal(2, monster.AvoidanceMovementTicks);
    }

    [Fact]
    public void AiAvoidanceUsesValidatedMovementGoalGate()
    {
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                34,
                "Flesh Clan",
                4,
                6,
                8,
                30,
                45,
                15,
                4,
                10,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "GoatMelee",
                GoatMeleeMovementChanceBase = 1,
                GoatMeleeMovementChanceIntelligenceScale = 1,
                AiAvoidanceMovementMinimumDistance = 4,
                AiAvoidanceMovementChanceModulo = 1,
                AiAvoidanceRoundWalkChanceBase = 100,
                AiAvoidanceRoundWalkChanceIntelligenceScale = 1,
            },
        ]);
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            40,
            attackDamage: 2,
            aggroRange: 7,
            monsterId: 1,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 5,
            life: 100,
            lifeMaximum: 100);

        // 101 ^ 9 ^ 7 = 107: native modulo-four skips the Move goal and the
        // one-percent ordinary movement roll fails; authored modulo-one must
        // still start the persisted AiAvoidance goal.
        Assert.Empty(processor.AdvanceTick(101, [player]));

        Assert.True(monster.IsMoving);
        Assert.Equal(1, monster.AvoidanceMovementTicks);
    }

    [Fact]
    public void GoatMeleeAiDefersWhenItsNativeRoundWalkCandidatesAreBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            40,
            attackDamage: 2,
            aggroRange: 8,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var world = new DiabloWorldState(
            objects: [
                new AuthoritativeWorldObject(1001, 1, 0, 5, 5),
                new AuthoritativeWorldObject(1002, 2, 0, 6, 5),
                new AuthoritativeWorldObject(1003, 3, 0, 7, 5),
                new AuthoritativeWorldObject(1004, 4, 0, 5, 6),
                new AuthoritativeWorldObject(1005, 5, 0, 7, 6),
                new AuthoritativeWorldObject(1006, 6, 0, 5, 7),
                new AuthoritativeWorldObject(1007, 7, 0, 6, 7),
                new AuthoritativeWorldObject(1008, 8, 0, 7, 7)],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 6,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15: movement is admitted and the far-distance native
        // RoundWalk branch is selected, but every candidate is blocked.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((6, 6), (monster.PositionX, monster.PositionY));
        Assert.Equal(10, monster.AvoidanceDelayTicks);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(9, monster.AvoidanceDelayTicks);
        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void AiAvoidanceUsesValidatedFailureDelayTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            6,
            6,
            40,
            attackDamage: 2,
            aggroRange: 8,
            monsterId: 1,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Goat",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                8,
                20,
                false,
                false,
                false) {
                NativeAi = "GoatMelee",
                AvoidanceDelayBase = 6,
                AvoidanceDelayRandomRange = 3,
            },
        ]);
        var world = new DiabloWorldState(
            objects: [
                new AuthoritativeWorldObject(1001, 1, 0, 5, 5),
                new AuthoritativeWorldObject(1002, 2, 0, 6, 5),
                new AuthoritativeWorldObject(1003, 3, 0, 7, 5),
                new AuthoritativeWorldObject(1004, 4, 0, 5, 6),
                new AuthoritativeWorldObject(1005, 5, 0, 7, 6),
                new AuthoritativeWorldObject(1006, 6, 0, 5, 7),
                new AuthoritativeWorldObject(1007, 7, 0, 6, 7),
                new AuthoritativeWorldObject(1008, 8, 0, 7, 7)],
            combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 1,
            positionY: 6,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, so the configured six-tick base is used with no
        // random increment; the next tick advances the same native delay.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal(6, monster.AvoidanceDelayTicks);

        processor.AdvanceTick(2, [player]);

        Assert.Equal(5, monster.AvoidanceDelayTicks);
    }

    [Fact]
    public void GoatMeleeAiDefersWhenItsNativeRandomWalkCandidatesAreBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var world = new DiabloWorldState(
            // 1 ^ 9 ^ 7 = 15: native RandomWalk checks (2,0), (2,1), and
            // (3,1); the remaining two candidates are outside the map.
            fallbackBlockedCells: [2, 42, 43],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((3, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void WarlordUsesItsNativeSkeletonCombatBranch()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Warlord",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // WarlordAi delegates normal post-dialogue combat to SkeletonAi;
        // 1 ^ 9 ^ 7 = 15 is inside its 20 + 2 * intelligence attack band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Single(deliveries);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void WarlordNativeAiOverrideRetainsSkeletonBranchWithCatalog()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            nativeAi: "Warlord",
            intelligence: 3,
            monsterId: 1);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                0,
                "Base Monster",
                1,
                16,
                1,
                1,
                4,
                0,
                2,
                3,
                0,
                3,
                false,
                false,
                false) {
                NativeAi = "Zombie",
                ZombieActionChanceBase = 10,
                ZombieActionChanceIntelligenceScale = 2,
                ZombieMovementDistanceMinimum = 2,
                ZombieMovementDistanceBase = 4,
                ZombieMovementDistanceIntelligenceScale = 2,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world, monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, which is inside Warlord's native SkeletonAi
        // movement-delay band (35 - 4 * intelligence). A generic catalog
        // fallback would move toward the player on this tick instead.
        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
        Assert.True(monster.SkeletonMeleeDelayTicks > 0);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
    }

    [Fact]
    public void StagedUniqueDialogueSuppressesCombatUntilAuthoredTerminalMarker()
    {
        var quest = new AuthoritativeQuestState(
            3012,
            0,
            1,
            active: true,
            completionUniqueIdentity: "Warlord",
            dialogueVariable1: 2,
            dialogueCompletionVariable1: -1);
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Warlord",
            intelligence: 3,
            unique: true,
            uniqueIdentity: "Warlord");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new AuthoritativeQuestDialogueStage(
                3012,
                0,
                -1,
                110,
                2,
                0),
            new AuthoritativeQuestDialogueStage(
                3012,
                2,
                -1,
                110,
                3,
                0),
        ]);
        var world = new DiabloWorldState(
            combatTargets: [monster],
            quests: [quest]);
        var processor = CreateProcessor(world, questDialogueStages: stages);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        Assert.False(processor.Execute(
            player,
            new DiabloAttackIntent(monster.EntityId),
            1).Succeeded);
        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.Equal(100, player.Life);

        quest.Variable1 = 3;
        var deliveries = processor.AdvanceTick(2, [player]);

        Assert.Single(deliveries);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void SnotSpilUsesItsNativeFallenCombatBranch()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Snotspill",
            intelligence: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // After the banner quest transition SnotSpil delegates to FallenAi;
        // 1 ^ 9 ^ 7 = 15 is inside the native 20% SkeletonAi attack band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Single(deliveries);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void GoatMeleeAiUsesValidatedMonsterTypeMovementTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            monsterId: 1,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "GoatMelee",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Goat",
                1,
                16,
                1,
                1,
                40,
                0,
                1,
                3,
                0,
                0,
                false,
                false,
                false) {
                NativeAi = "Fallen",
                GoatMeleeMovementChanceBase = 10,
                GoatMeleeMovementChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void SkeletonMeleeAiUsesItsNativeAdjacentAttackBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "SkeletonMelee",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, inside the native 20 + 2 * intelligence band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Single(deliveries);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void SkeletonMeleeAiCanDelayAnAdjacentAttackOutsideItsNativeBand()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "SkeletonMelee",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38, outside the native 20 + 2 * intelligence band.
        var deliveries = processor.AdvanceTick(40, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void SkeletonMeleeAiPersistsItsNativeAdjacentDelay()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "SkeletonMelee",
            intelligence: 3);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38 misses the 26% native attack band and starts the
        // four-tick adjacent SkeletonAi delay.
        processor.AdvanceTick(40, [player]);
        Assert.Equal(4, monster.SkeletonMeleeDelayTicks);

        processor.AdvanceTick(41, [player]);
        Assert.Equal(3, monster.SkeletonMeleeDelayTicks);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void SkeletonMeleeAiUsesValidatedAdjacentAttackTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            monsterId: 1,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "SkeletonMelee",
            intelligence: 0);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Skeleton",
                1,
                16,
                1,
                1,
                10,
                0,
                1,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                SkeletonMeleeAttackChanceBase = 10,
                SkeletonMeleeAttackChanceIntelligenceScale = 1,
                SkeletonMeleeAttackDelayBase = 6,
                SkeletonMeleeAttackDelayIntelligenceScale = 1,
                SkeletonMeleeAttackDelayRandomRange = 4,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, outside the configured ten-percent band.
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.Equal(6, monster.SkeletonMeleeDelayTicks);
    }

    [Fact]
    public void ZombieAiUsesItsNativeActionBandBeforeLongRangeMovement()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 40 ^ 9 ^ 7 = 38, outside Zombie's ten-percent base action band.
        var deliveries = processor.AdvanceTick(40, [player]);

        Assert.Empty(deliveries);
        Assert.False(monster.IsMoving);
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void ZombieAiWalksWhenItsNativeLongRangeActionSucceeds()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 6 ^ 9 ^ 7 = 8, inside the native ten-percent action band.
        var deliveries = processor.AdvanceTick(6, [player]);

        Assert.Empty(deliveries);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void ZombieAiDefersWhenItsNativeWalkIsBlocked()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie");
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2],
            combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 6 ^ 9 ^ 7 = 8, inside the native action band. The direct east
        // walk is blocked, so the native controller consumes the tick.
        processor.AdvanceTick(6, [player]);

        Assert.False(monster.IsMoving);
        Assert.Equal((1, 0), (monster.PositionX, monster.PositionY));
        Assert.Equal(100, player.Life);
    }

    [Fact]
    public void ZombieAiRandomWalksInsideItsLongRangeMovementBoundary()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie");
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 3,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        // 6 ^ 9 ^ 7 = 8, inside Zombie's ten-percent action band. The
        // distance-two target is outside melee but below the native distance
        // four long-range pursuit boundary, so RandomWalk must still move.
        var deliveries = processor.AdvanceTick(6, [player]);

        Assert.Empty(deliveries);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void ZombieAiUsesValidatedMonsterTypeActionTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            monsterId: 1,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Zombie",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                ZombieActionChanceBase = 5,
                ZombieActionChanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(6, [player]);

        Assert.False(monster.IsMoving);
    }

    [Fact]
    public void ZombieAiUsesValidatedRandomDirectionTuning()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            100,
            monsterId: 1,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Zombie",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                ZombieActionChanceBase = 100,
                ZombieActionChanceIntelligenceScale = 1,
                ZombieRandomDirectionChanceBase = 10,
                ZombieRandomDirectionChanceIntelligenceScale = 1,
                ZombieMovementDistanceMinimum = 2,
                ZombieMovementDistanceBase = 4,
                ZombieMovementDistanceIntelligenceScale = 2,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 1,
            life: 100,
            lifeMaximum: 100);

        // 1 ^ 9 ^ 7 = 15, outside the configured ten-percent random prepend
        // band, so the direct diagonal step is selected.
        processor.AdvanceTick(1, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((1, 1), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void ZombieAiUsesValidatedMonsterTypeMovementThreshold()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            monsterId: 1,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie");
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                1,
                1,
                "Zombie",
                1,
                16,
                1,
                1,
                100,
                0,
                1,
                3,
                0,
                0,
                false,
                true,
                false) {
                NativeAi = "Fallen",
                ZombieActionChanceBase = 100,
                ZombieActionChanceIntelligenceScale = 1,
                ZombieMovementDistanceMinimum = 8,
                ZombieMovementDistanceBase = 8,
                ZombieMovementDistanceIntelligenceScale = 1,
            },
        ]);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(100, [player]);

        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void NativeMonsterRegenerationUsesTheExternalizedMonsterLevel()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            maxHitPoints: 20,
            monsterLevel: 4,
            aggroRange: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(life: 100, lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(12, monster.HitPoints);
        Assert.Equal(12 * DiabloFixedPointQ6.Scale, monster.HitPointsQ6);
    }

    [Fact]
    public void NativeMonsterRegenerationUsesConfiguredLevelDivisor()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            maxHitPoints: 20,
            monsterLevel: 4,
            aggroRange: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                monsterRegenerationLevelDivisor: 4));
        var player = CreatePlayer(life: 100, lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(11, monster.HitPoints);
        Assert.Equal(11 * DiabloFixedPointQ6.Scale, monster.HitPointsQ6);
    }

    [Theory]
    [InlineData(DiabloDifficulty.Normal, 11)]
    [InlineData(DiabloDifficulty.Nightmare, 18)]
    [InlineData(DiabloDifficulty.Hell, 25)]
    public void NativeMonsterRegenerationUsesDifficultyAdjustedLevel(
        DiabloDifficulty difficulty,
        int expectedHitPoints)
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            maxHitPoints: 100,
            monsterLevel: 1,
            aggroRange: 0);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            difficulty: difficulty);
        var player = CreatePlayer(life: 100, lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedHitPoints, monster.HitPoints);
    }

    [Fact]
    public void NativeMonsterKnockbackDisplacesSuccessfulHitsAndRespectsBlockedTiles()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 10,
            aggroRange: 5,
            knockback: true);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        Assert.Single(processor.AdvanceTick(1, [player]));
        Assert.Equal(90, player.Life);
        Assert.Equal((3, 0), (player.PositionX, player.PositionY));

        var blockedMonster = new AuthoritativeCombatTarget(
            10,
            1,
            0,
            20,
            attackDamage: 10,
            aggroRange: 5,
            knockback: true);
        var blockedWorld = new DiabloWorldState(
            fallbackBlockedCells: [3],
            combatTargets: [blockedMonster]);
        var blockedProcessor = CreateProcessor(blockedWorld);
        var blockedPlayer = CreatePlayer(
            entityId: 8,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        Assert.Single(blockedProcessor.AdvanceTick(1, [blockedPlayer]));
        Assert.Equal((2, 0), (blockedPlayer.PositionX, blockedPlayer.PositionY));
    }

    [Fact]
    public void HellfireMonsterClassArmorFlagsReduceAutonomousMonsterDamage()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 50,
            aggroRange: 5,
            demon: true);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            life: 100,
            lifeMaximum: 100,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.AgainstDemonsFlag,
                })]);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Single(deliveries);
        Assert.Equal(90, player.Life);

        var baseMonster = new AuthoritativeCombatTarget(
            10,
            1,
            0,
            20,
            attackDamage: 50,
            aggroRange: 5,
            demon: true);
        var basePlayer = CreatePlayer(
            entityId: 8,
            life: 100,
            lifeMaximum: 100,
            equipment: player.Equipment);
        var baseProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [baseMonster]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Diablo));

        Assert.Single(baseProcessor.AdvanceTick(1, [basePlayer]));
        Assert.Equal(50, basePlayer.Life);
    }

    [Fact]
    public void HellfireDevastationTriplesOnlySuccessfulEligibleHitsOnItsNativeRoll()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            world,
            random: random,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.DevastationFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(70, target.HitPoints);
        Assert.Equal(1U, random.State);

        var noProcTarget = new AuthoritativeCombatTarget(10, 1, 0, 100);
        var noProcWorld = new DiabloWorldState(combatTargets: [noProcTarget]);
        var noProcProcessor = CreateProcessor(
            noProcWorld,
            random: new DiabloRandom(1),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var noProcResult = noProcProcessor.Execute(
            player,
            new DiabloAttackIntent(10),
            2);

        Assert.True(noProcResult.Succeeded);
        Assert.Equal(90, noProcTarget.HitPoints);

        var ineligibleTarget = new AuthoritativeCombatTarget(11, 1, 0, 100);
        var ineligibleRandom = new DiabloRandom(0);
        var ineligibleProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [ineligibleTarget]),
            random: ineligibleRandom,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var ineligiblePlayer = CreatePlayer(
            entityId: 12,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = false,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.DevastationFlag,
                })]);

        var ineligibleResult = ineligibleProcessor.Execute(
            ineligiblePlayer,
            new DiabloAttackIntent(11),
            3);

        Assert.True(ineligibleResult.Succeeded);
        Assert.Equal(90, ineligibleTarget.HitPoints);
        Assert.Equal(0U, ineligibleRandom.State);
    }

    [Fact]
    public void HellfireDevastationUsesConfiguredChanceAndMultiplier()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            random: random,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire,
                hellfireDevastationChancePercent: 100,
                hellfireDevastationDamageMultiplier: 5));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.DevastationFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(50, target.HitPoints);
        Assert.Equal(1U, random.State);
    }

    [Fact]
    public void HellfireJestersPreservesNativeQ6DamageScaling()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var random = new DiabloRandom(1);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            random: random,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.JestersFlag,
                })]);
        var expectedRandom = new DiabloRandom(1);
        var expectedRoll = expectedRandom.RandomIntBetween(0, 200);
        expectedRoll = expectedRoll >= 100
            ? 100 + (expectedRoll - 100) * 5
            : expectedRoll;

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(6400 - 640 * expectedRoll / 100, target.HitPointsQ6);
        Assert.Equal(expectedRandom.State, random.State);

        var zeroTarget = new AuthoritativeCombatTarget(10, 1, 0, 100);
        var zeroRandom = new DiabloRandom(0);
        var zeroProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [zeroTarget]),
            random: zeroRandom,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var zeroResult = zeroProcessor.Execute(
            player,
            new DiabloAttackIntent(10),
            2);

        Assert.True(zeroResult.Succeeded);
        Assert.Equal(6400, zeroTarget.HitPointsQ6);
        Assert.Equal(1U, zeroRandom.State);
    }

    [Fact]
    public void HellfireJestersUsesConfiguredRollExpansionTerms()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var random = new DiabloRandom(1);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            random: random,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire,
                hellfireJestersRollMaximum: 80,
                hellfireJestersExpansionThreshold: 40,
                hellfireJestersExpansionMultiplier: 2));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.JestersFlag,
                })]);
        var expectedRandom = new DiabloRandom(1);
        var expectedRoll = expectedRandom.RandomIntBetween(0, 80);
        expectedRoll = expectedRoll >= 40
            ? 40 + (expectedRoll - 40) * 2
            : expectedRoll;

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(6400 - 640 * expectedRoll / 100, target.HitPointsQ6);
        Assert.Equal(expectedRandom.State, random.State);
    }

    [Fact]
    public void HellfirePerilDoublesFinalDamageAndHurtsAttackerFromPreModifierDamage()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusGetHit = -2,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.PerilFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(5888, player.Life);
        Assert.Equal(5120, target.HitPointsQ6);
        Assert.Equal(2, result.Events.Count);
        Assert.Equal(512, result.Events[0].AmountQ6);
        Assert.Equal(player.EntityId, result.Events[0].SourceEntityId);
        Assert.Equal(player.EntityId, result.Events[0].TargetEntityId);
        Assert.Equal(1280, result.Events[1].AmountQ6);
        Assert.Equal(target.EntityId, result.Events[1].TargetEntityId);
    }

    [Fact]
    public void HellfirePerilSelfDamageIncludesTheNativeFlatWeaponModifier()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusDamageModifier = 3,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.PerilFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(5568, player.Life);
        Assert.Equal(4736, target.HitPointsQ6);
        Assert.Equal(
            [832, 1664],
            result.Events.Select(gameplayEvent => gameplayEvent.AmountQ6));
    }

    [Fact]
    public void HellfirePerilUsesConfiguredDamageMultiplier()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire,
                hellfirePerilDamageMultiplier: 3));
        var player = CreatePlayer(
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusGetHit = -2,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.PerilFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(4480, target.HitPointsQ6);
        Assert.Equal(5888, player.Life);
        Assert.Equal(1920, result.Events[1].AmountQ6);
    }

    [Fact]
    public void HellfirePerilUsesManaShieldForItsSelfDamage()
    {
        var manaShield = new AuthoritativeSpellDefinition(13, 13, 0, 0, 0, 0) {
            ProjectileBehavior = AuthoritativeProjectileBehavior.ManaShield,
            ManaShieldBaseDamageReductionDivisor = 24,
            ManaShieldDamageReductionLevelStep = 3,
            ManaShieldDamageReductionMaximumLevel = 7,
        };
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            new AuthoritativeSpellCatalog([manaShield]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            life: 6400,
            lifeMaximum: 6400,
            mana: 1000,
            manaMaximum: 1000,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.PerilFlag,
                })]);
        player.ManaShieldActive = true;

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(6400, player.Life);
        Assert.Equal(360, player.Mana);
        Assert.Equal(5120, target.HitPointsQ6);
        Assert.Equal(640, result.Events[0].AmountQ6);
    }

    [Fact]
    public void HellfireDoppelgangerSpawnsAFullHealthCopyOnTheLastAvailableAdjacentCell()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            100,
            maxHitPoints: 100,
            monsterId: 42,
            fireResistance: 20,
            lightningResistance: 30,
            magicResistance: 40,
            acidResistance: 50,
            monsterLevel: 3,
            undead: true,
            minDamage: 4,
            maxDamage: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 10,
            berserkImmune: true,
            deathAnimationTicks: 4,
            demon: true,
            toHit: 61,
            toHitSpecial: 43);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            world,
            random: random,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(1U, random.State);
        Assert.Equal(90, target.HitPoints);
        var copy = Assert.Single(
            world.CombatTargets.Values,
            candidate => candidate.EntityId != target.EntityId);
        Assert.Equal(0x10000000U, copy.EntityId);
        Assert.Equal((2, 1), (copy.PositionX, copy.PositionY));
        Assert.Equal(target.MonsterId, copy.MonsterId);
        Assert.Equal(target.MaxHitPointsQ6, copy.HitPointsQ6);
        Assert.Equal(target.MaxHitPointsQ6, copy.MaxHitPointsQ6);
        Assert.True(copy.IsUndead);
        Assert.True(copy.IsDemon);
        Assert.False(copy.IsUnique);
        Assert.False(copy.IsDiablo);
        Assert.Equal(target.DeathAnimationTicks, copy.DeathAnimationTicks);
        Assert.Equal((target.ToHit, target.ToHitSpecial), (copy.ToHit, copy.ToHitSpecial));
        Assert.Equal(target.NativeAi, copy.NativeAi);
    }

    [Fact]
    public void HellfireDoppelgangerDoesNotRollForUniqueOrDiabloTargets()
    {
        foreach (var target in new[] {
            new AuthoritativeCombatTarget(9, 1, 0, 100, unique: true),
            new AuthoritativeCombatTarget(10, 1, 0, 100, diablo: true),
        }) {
            var random = new DiabloRandom(0);
            var processor = CreateProcessor(
                new DiabloWorldState(combatTargets: [target]),
                random: random,
                rules: new AuthoritativeCombatRules(
                    10,
                    1,
                    100,
                    gameVariant: DiabloGameVariant.Hellfire));
            var player = CreatePlayer(
                entityId: 7,
                equipment: [new EquippedStoreItem(
                    4,
                    42,
                    AuthoritativeItemState.Empty with {
                        StatFlag = true,
                        HellfireDamageArmorFlags =
                            AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag,
                    })]);

            var result = processor.Execute(
                player,
                new DiabloAttackIntent(target.EntityId),
                1);

            Assert.True(result.Succeeded);
            Assert.Equal(0U, random.State);
        }
    }

    [Fact]
    public void HellfireDoppelgangerUsesConfiguredProcChance()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(
            world,
            random: random,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire,
                hellfireDoppelgangerChancePercent: 0));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.DoppelgangerFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(90, target.HitPoints);
        Assert.Single(world.CombatTargets);
        Assert.Equal(1U, random.State);
    }

    [Fact]
    public void HellfireDecayReducesOnlyTheFirstEligibleWeaponAfterAnAttack()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var decay = AuthoritativeItemState.Empty with {
            ItemClass = 1,
            StatFlag = true,
            HellfireDamageArmorFlags =
                AuthoritativeEquipmentIncomingDamageRules.DecayFlag,
        };
        var player = CreatePlayer(
            entityId: 7,
            equipment: [
                new EquippedStoreItem(4, 42, decay),
                new EquippedStoreItem(5, 43, decay),
            ]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(90, target.HitPoints);
        Assert.Equal(-5, player.Equipment.Single(item => item.Slot == 4).State.PlusDamage);
        Assert.Equal(-5, player.Equipment.Single(item => item.Slot == 5).State.PlusDamage);
    }

    [Fact]
    public void HellfireDecayRemovesAWeaponAtTheNativeThresholdAndIgnoresNonWeapons()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var player = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemClass = 1,
                    StatFlag = true,
                    PlusDamage = -95,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.DecayFlag,
                })]);

        var result = processor.Execute(player, new DiabloAttackIntent(9), 1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Equipment);

        var nonWeaponTarget = new AuthoritativeCombatTarget(10, 1, 0, 100);
        var nonWeaponProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [nonWeaponTarget]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));
        var nonWeaponPlayer = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    ItemClass = 2,
                    StatFlag = true,
                    HellfireDamageArmorFlags =
                        AuthoritativeEquipmentIncomingDamageRules.DecayFlag,
                })]);

        var nonWeaponResult = nonWeaponProcessor.Execute(
            nonWeaponPlayer,
            new DiabloAttackIntent(10),
            1);

        Assert.True(nonWeaponResult.Succeeded);
        Assert.Equal(0, nonWeaponPlayer.Equipment.Single().State.PlusDamage);
    }

    [Fact]
    public void HellfireDecayUsesConfiguredReductionAndRemovalThreshold()
    {
        var rules = new AuthoritativeCombatRules(
            10,
            1,
            100,
            gameVariant: DiabloGameVariant.Hellfire,
            hellfireDecayDamageReduction: 7,
            hellfireDecayRemovalThreshold: 21);
        var decay = AuthoritativeItemState.Empty with {
            ItemClass = 1,
            StatFlag = true,
            HellfireDamageArmorFlags =
                AuthoritativeEquipmentIncomingDamageRules.DecayFlag,
        };
        var retainedTarget = new AuthoritativeCombatTarget(9, 1, 0, 100);
        var retainedPlayer = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 42, decay)]);
        var retainedProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [retainedTarget]),
            rules: rules);

        Assert.True(retainedProcessor.Execute(
            retainedPlayer,
            new DiabloAttackIntent(9),
            1).Succeeded);
        Assert.Equal(-7, Assert.Single(retainedPlayer.Equipment).State.PlusDamage);

        var removedTarget = new AuthoritativeCombatTarget(10, 1, 0, 100);
        var removedPlayer = CreatePlayer(
            entityId: 7,
            equipment: [new EquippedStoreItem(
                4,
                42,
                decay with { PlusDamage = -14 })]);
        var removedProcessor = CreateProcessor(
            new DiabloWorldState(combatTargets: [removedTarget]),
            rules: rules);

        Assert.True(removedProcessor.Execute(
            removedPlayer,
            new DiabloAttackIntent(10),
            1).Succeeded);
        Assert.Empty(removedPlayer.Equipment);
    }

    [Fact]
    public void HealingStatusAndExpiryAreModuleOwned()
    {
        var world = new DiabloWorldState();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 20, 4, 10, 2),
        ]);
        var processor = CreateProcessor(world, spells);
        var player = CreatePlayer(
            life: 10,
            lifeMaximum: 25,
            mana: 10,
            manaMaximum: 10,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(1, 1)]);

        DiabloCombatCommandProcessor.AdvanceStatusEffects(player, 1);
        var result = processor.Execute(player, new DiabloCastIntent(1, 0, 0, 0), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(25, player.Life);
        Assert.Equal(5, player.Mana);
        var status = Assert.Single(player.StatusEffects);
        Assert.Equal(4U, status.EffectId);
        Assert.Equal(10U, status.RemainingTicks);

        DiabloCombatCommandProcessor.AdvanceStatusEffects(player, 11);

        Assert.Empty(player.StatusEffects);
        Assert.Equal(11UL, player.LastAppliedTick);
    }

    [Fact]
    public void FireboltCastAdmitsAndResolvesAnAuthoritativePlayerTarget()
    {
        var world = new DiabloWorldState();
        var spells = new AuthoritativeSpellCatalog([CreateFireboltSpell()]);
        var processor = CreateProcessor(
            world,
            spells,
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses(),
            gameMode: DiabloGameMode.Multiplayer);
        var caster = CreatePlayer(
            positionX: 0,
            entityId: 7,
            mana: 1000,
            manaMaximum: 1000,
            memorizedSpells: [new MemorizedSpellState(1, 1)]);
        var target = CreatePlayer(
            positionX: 3,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                1,
                target.EntityId,
                target.PositionX,
                target.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(616, caster.Mana);
        Assert.Single(world.Projectiles);
        Assert.Equal(6400, target.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 1; tick <= 256 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [caster, target]));

        Assert.True(target.Life < 6400);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal((7U, 8U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void FireboltRejectsWrappedExtremePlayerTargetDistance()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireboltSpell()]),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            gameMode: DiabloGameMode.Multiplayer);
        var caster = CreatePlayer(
            positionX: int.MaxValue,
            entityId: 7,
            mana: 1000,
            manaMaximum: 1000,
            memorizedSpells: [new MemorizedSpellState(1, 1)]);
        var target = CreatePlayer(
            positionX: int.MinValue,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                1,
                target.EntityId,
                target.PositionX,
                target.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(1000, caster.Mana);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void DamageCastRejectsOutOfLevelRawCoordinatesBeforeDistanceArithmetic()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireboltSpell()]));
        var player = CreatePlayer(
            entityId: 7,
            mana: 1000,
            manaMaximum: 1000,
            memorizedSpells: [new MemorizedSpellState(1, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                int.MinValue,
                int.MinValue,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(1000, player.Mana);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void FireboltPlayerHitUsesConfiguredDirectMagicAccuracyBounds()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireboltSpell()]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: true,
                playerMagicMinimumHitChance: 0,
                playerMagicMaximumHitChance: 0),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses(),
            gameMode: DiabloGameMode.Multiplayer);
        var caster = CreatePlayer(
            positionX: 0,
            entityId: 7,
            mana: 1000,
            manaMaximum: 1000,
            memorizedSpells: [new MemorizedSpellState(1, 1)]);
        var target = CreatePlayer(
            positionX: 3,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                1,
                target.EntityId,
                target.PositionX,
                target.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        for (ulong tick = 1; tick <= 256 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [caster, target]);

        Assert.Equal(6400, target.Life);
    }

    [Fact]
    public void TileTargetedSpellTreatsVisiblePlayerIdentityAsAPresentationHint()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireballSpell()]));
        var caster = CreatePlayer(
            entityId: 7,
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(12, 1)]);
        var visiblePlayer = CreatePlayer(positionX: 4, entityId: 8);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                12,
                visiblePlayer.EntityId,
                visiblePlayer.PositionX,
                visiblePlayer.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, visiblePlayer]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Fireball, projectile.Behavior);
        Assert.Equal(0U, projectile.TargetEntityId);
        Assert.Equal((4, 0), (projectile.TargetX, projectile.TargetY));
    }

    [Fact]
    public void FriendlyModeRejectsPlayerTargetedFireboltWhenRulesDisableFriendlyFire()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireboltSpell()]),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: false),
            playerClasses: LoadShippedPlayerClasses(),
            gameMode: DiabloGameMode.Multiplayer);
        var caster = CreatePlayer(
            entityId: 7,
            mana: 1000,
            manaMaximum: 1000,
            memorizedSpells: [new MemorizedSpellState(1, 1)]);
        var target = CreatePlayer(positionX: 3, entityId: 8, life: 6400, lifeMaximum: 6400);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                1,
                target.EntityId,
                target.PositionX,
                target.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(1000, caster.Mana);
        Assert.Equal(6400, target.Life);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void ChargedBoltCastAdmitsAndResolvesAnAuthoritativePlayerTarget()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateChargedBoltSpell()]),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            random: new DiabloRandom(0),
            gameMode: DiabloGameMode.Multiplayer);
        var caster = CreatePlayer(
            entityId: 7,
            mana: 1000,
            manaMaximum: 1000,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(30, 2)]);
        var target = CreatePlayer(
            positionX: 1,
            positionY: 1,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                30,
                target.EntityId,
                target.PositionX,
                target.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(994, caster.Mana);
        Assert.Equal(5, world.Projectiles.Count);
        Assert.Equal(6400, target.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 1; tick <= 64 && world.Projectiles.Count != 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [caster, target]));

        Assert.True(target.Life < 6400);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == caster.EntityId
                && delivery.Event.TargetEntityId == target.EntityId);
    }

    [Fact]
    public void BoneSpiritCastAdmitsAndResolvesAnAuthoritativePlayerTarget()
    {
        var world = new DiabloWorldState();
        var spell = CreateBoneSpiritSpell();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            random: new DiabloRandom(0),
            gameMode: DiabloGameMode.Multiplayer);
        var caster = CreatePlayer(
            entityId: 7,
            life: 1280,
            lifeMaximum: 1280,
            mana: 2000,
            manaMaximum: 2000,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(38, 2)]);
        var target = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 6400,
            lifeMaximum: 6400);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                38,
                target.EntityId,
                target.PositionX,
                target.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Single(result.Events);
        Assert.Single(world.Projectiles);
        Assert.Equal(528, caster.Mana);
        Assert.Equal(896, caster.Life);

        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 1; tick <= 64 && target.Life == 6400; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [caster, target]));

        Assert.True(target.Life < 6400);
        Assert.Contains(
            deliveries,
            delivery => delivery.Event.Kind == DiabloGameplayEventKind.Damage
                && delivery.Event.SourceEntityId == caster.EntityId
                && delivery.Event.TargetEntityId == target.EntityId);
    }

    [Fact]
    public void BoneSpiritPlayerHitUsesConfiguredCurrentLifeDamageDivisor()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateBoneSpiritSpell() with {
                    BoneSpiritPlayerDamageHitPointsDivisor = 1000,
                },
            ]),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            random: new DiabloRandom(0),
            gameMode: DiabloGameMode.Multiplayer);
        var caster = CreatePlayer(
            entityId: 7,
            life: 1280,
            lifeMaximum: 1280,
            mana: 2000,
            manaMaximum: 2000,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(38, 2)]);
        var target = CreatePlayer(
            positionX: 1,
            entityId: 8,
            life: 600,
            lifeMaximum: 600);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                38,
                target.EntityId,
                target.PositionX,
                target.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        for (ulong tick = 1; tick <= 64 && world.Projectiles.Count != 0; tick++)
            processor.AdvanceTick(tick, [caster, target]);

        Assert.Equal(600, target.Life);
    }

    [Fact]
    public void AbilityCastRequiresOwnedAbilityAndConsumesNoMana()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(26, 9, 10, 0, 0, 0),
        ]);
        var processor = CreateProcessor(new DiabloWorldState(), spells);
        var player = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            mana: 10,
            manaMaximum: 10,
            abilitySpellId: 26);

        var accepted = processor.Execute(
            player,
            new DiabloCastIntent(26, 0, 0, 0, DiabloCastSourceKind.Ability),
            1);
        var rejected = processor.Execute(
            player,
            new DiabloCastIntent(27, 0, 0, 0, DiabloCastSourceKind.Ability),
            2);

        Assert.True(accepted.Succeeded);
        Assert.Equal(10, player.Mana);
        Assert.Equal(20, player.Life);
        AssertRejected(rejected, DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void ExplicitMemorizedCastRejectsUnlearnedSpellWithoutSpendingMana()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 10, 0, 0, 0),
        ]);
        var processor = CreateProcessor(new DiabloWorldState(), spells);
        var player = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            mana: 10,
            manaMaximum: 10);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(1, 0, 0, 0, DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.NotAllowed);
        Assert.Equal(10, player.Mana);
        Assert.Equal(10, player.Life);
    }

    [Fact]
    public void NoManaEquipmentRejectsMemorizedCastsButNotFreeAbilities()
    {
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(26, 5, 20, 0, 0, 0),
        ]);
        var processor = CreateProcessor(new DiabloWorldState(), spells);
        IReadOnlyList<EquippedStoreItem> noManaEquipment = [new EquippedStoreItem(
            4,
            77,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Flags = AuthoritativeEquipmentManaRules.NoManaFlag,
            })];
        var memorizedPlayer = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            mana: 1000,
            manaMaximum: 1000,
            equipment: noManaEquipment,
            memorizedSpells: [new MemorizedSpellState(26, 1)]);

        var rejected = processor.Execute(
            memorizedPlayer,
            new DiabloCastIntent(26, 0, 0, 0, DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(rejected, DiabloRuleFailure.InsufficientResources);
        Assert.Equal(1000, memorizedPlayer.Mana);
        Assert.Equal(10, memorizedPlayer.Life);

        var abilityPlayer = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            mana: 1000,
            manaMaximum: 1000,
            equipment: noManaEquipment,
            abilitySpellId: 26);
        var accepted = processor.Execute(
            abilityPlayer,
            new DiabloCastIntent(26, 0, 0, 0, DiabloCastSourceKind.Ability),
            2);

        Assert.True(accepted.Succeeded);
        Assert.Equal(20, abilityPlayer.Life);
        Assert.Equal(1000, abilityPlayer.Mana);
    }

    [Fact]
    public void ChargedItemCastConsumesOneLeftHandStaffChargeAndNoMana()
    {
        var staffState = AuthoritativeItemState.Empty with {
            MiscId = 23,
            SpellId = 1,
            Charges = 2,
            MaxCharges = 2,
        };
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 10, 0, 0, 0),
        ]);
        var processor = CreateProcessor(new DiabloWorldState(), spells);
        var player = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            mana: 1,
            manaMaximum: 10,
            equipment: [new EquippedStoreItem(4, 77, staffState)]);
        var reference = new DiabloPlayerItemReference(
            DiabloPlayerItemLocation.Equipment,
            4);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                0,
                0,
                DiabloCastSourceKind.ChargedItem,
                reference),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, player.Mana);
        Assert.Equal(1, Assert.Single(player.Equipment).State.Charges);
    }

    [Fact]
    public void MultiProjectileStaffCastCreatesEveryProjectileButConsumesOneCharge()
    {
        var target = new AuthoritativeCombatTarget(9, 2, 0, 40);
        var world = new DiabloWorldState(combatTargets: [target]);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) {
                DamageAmount = 4,
                DamageType = AuthoritativeDamageType.Lightning,
                Range = 4,
                ProjectileSpeed = 1,
                ProjectileLifetime = 8,
                ProjectileCount = 4,
            },
        ]);
        var staff = AuthoritativeItemState.Empty with {
            MiscId = 23,
            SpellId = 30,
            Charges = 2,
            MaxCharges = 2,
        };
        var processor = CreateProcessor(world, spells);
        var player = CreatePlayer(
            mana: 1,
            manaMaximum: 10,
            entityId: 7,
            equipment: [new EquippedStoreItem(4, 77, staff)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                30,
                9,
                0,
                0,
                DiabloCastSourceKind.ChargedItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 4)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(4, world.Projectiles.Count);
        Assert.All(world.Projectiles.Values, projectile => {
            Assert.Equal(30U, projectile.SpellId);
            Assert.Equal(4, projectile.Damage);
            Assert.Equal(AuthoritativeDamageType.Lightning, projectile.DamageType);
        });
        Assert.Equal(1, player.Mana);
        Assert.Equal(1, Assert.Single(player.Equipment).State.Charges);
    }

    [Fact]
    public void ChargedBoltUsesNativeLevelCountDamageAndRngOrder()
    {
        var world = new DiabloWorldState();
        var spell = CreateChargedBoltSpell();
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: random);
        var staff = AuthoritativeItemState.Empty with {
            MiscId = 23,
            SpellId = 30,
            Charges = 2,
            MaxCharges = 2,
            StatFlag = true,
            SpellLevelAdd = 1,
        };
        var player = CreatePlayer(
            mana: 1,
            manaMaximum: 10,
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(30, 2)],
            equipment: [new EquippedStoreItem(4, 77, staff)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                30,
                0,
                4,
                0,
                DiabloCastSourceKind.ChargedItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 4)),
            1);

        Assert.True(result.Succeeded);
        var projectiles = world.Projectiles.Values.OrderBy(projectile => projectile.EntityId).ToArray();
        Assert.Equal(5, projectiles.Length);
        Assert.Equal([13, 11, 7, 11, 8], projectiles.Select(projectile => projectile.PathIndex));
        Assert.Equal([7, 8, 6, 6, 1], projectiles.Select(projectile => projectile.Damage));
        Assert.All(projectiles, projectile => {
            Assert.Equal(AuthoritativeProjectileBehavior.ChargedBolt, projectile.Behavior);
            Assert.Equal(7, projectile.OriginalDirection);
            Assert.Equal(370720, projectile.VelocityX);
            Assert.Equal(185360, projectile.VelocityY);
            Assert.Equal(256U, projectile.RemainingTicks);
        });
        Assert.Equal(0x7162D42BU, random.State);
        Assert.Equal(1, Assert.Single(player.Equipment).State.Charges);
    }

    [Fact]
    public void ChargedBoltUsesConfiguredInitialPathRandomRange()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateChargedBoltSpell() with { ChargedBoltPathRandomRange = 1 },
            ]),
            random: new DiabloRandom(0x12345678));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 10000,
            manaMaximum: 10000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(30, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                30,
                0,
                14,
                10,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.NotEmpty(world.Projectiles);
        Assert.All(world.Projectiles.Values, projectile => Assert.Equal(1, projectile.PathIndex));
    }

    [Fact]
    public void ChargedBoltUsesValidatedSteeringPattern()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        projectile.PathTicks = 0;
        var world = new DiabloWorldState(projectiles: [projectile]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateChargedBoltSpell() with {
                    ChargedBoltPathPattern = [
                        1, 1, 1, 1, 1, 1, 1, 1,
                        1, 1, 1, 1, 1, 1, 1, 1,
                    ],
                },
            ]));

        processor.AdvanceTick(1, []);

        Assert.Equal(1, projectile.PathIndex);
        Assert.Equal(-370720, projectile.VelocityX);
        Assert.Equal(185360, projectile.VelocityY);
    }

    [Theory]
    [InlineData(DiabloGameVariant.Diablo, 680)]
    [InlineData(DiabloGameVariant.Hellfire, 808)]
    public void FireboltUsesNativeDamageVelocityAndVariantManaCost(
        DiabloGameVariant variant,
        int expectedMana)
    {
        var world = new DiabloWorldState();
        var random = new DiabloRandom(0x12345678);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireboltSpell()]),
            new AuthoritativeCombatRules(10, 1, 100, gameVariant: variant),
            random,
            classes);
        var player = CreatePlayer(
            mana: 1000,
            manaMaximum: 1000,
            entityId: 7,
            characterClassId: 2,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(1, 3)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedMana, player.Mana);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Firebolt, projectile.Behavior);
        Assert.Equal(15, projectile.Damage);
        Assert.Equal((1204840, 602420), (
            projectile.VelocityX,
            projectile.VelocityY));
        Assert.Equal(256U, projectile.RemainingTicks);
        Assert.Equal(0x955E76D9U, random.State);
    }

    [Fact]
    public void FireboltTraversesNativePathAndAppliesQuarterResistanceDamage()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            fireResistance: 25,
            monsterLevel: 1);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(0x12345678);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireboltSpell()]),
            random: random,
            playerClasses: classes);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            abilitySpellId: 1,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(1, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(1, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);

        var deliveries = processor.AdvanceTick(1, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal((0, 0), (projectile.PositionX, projectile.PositionY));
        Assert.Equal(1U, projectile.RemainingTicks);
        Assert.Equal(7, target.HitPoints);
        Assert.Equal(400, target.HitPointsQ6);
        var delivery = Assert.Single(deliveries);
        Assert.Same(player, delivery.Recipient);
        Assert.Equal(3, delivery.Event.Amount);
        Assert.Equal(240, delivery.Event.AmountQ6);
        Assert.Equal(0x3F10CD47U, random.State);

        Assert.Empty(processor.AdvanceTick(2, [player]));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void FireboltUsesConfiguredMonsterMagicHitBounds()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            40,
            monsterLevel: 10);
        var projectile = new AuthoritativeProjectile(
            0x80000000,
            7,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            4,
            AuthoritativeDamageType.Fire,
            0,
            32) {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
            OriginalDirection = 0,
            PathTicks = 1,
            Speed = 26,
            ImpactLifetime = 1,
            ResistantDamageDivisor = 4,
        };
        var world = new DiabloWorldState(
            combatTargets: [target],
            projectiles: [projectile]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateFireboltSpell() with {
                    FireboltHitMonsterLevelScale = 0,
                    FireboltMinimumHitChance = 0,
                    FireboltMaximumHitChance = 0,
                },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(40, target.HitPoints);
    }

    [Fact]
    public void PersistentFireballHitUsesConfiguredMonsterMagicHitBounds()
    {
        var directTarget = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            200,
            monsterLevel: 1,
            petrified: true);
        var splashTarget = new AuthoritativeCombatTarget(
            10,
            2,
            0,
            200,
            monsterLevel: 10);
        var world = new DiabloWorldState(combatTargets: [directTarget, splashTarget]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateFireballSpell() with {
                    PersistentProjectileMinimumHitChance = 0,
                    PersistentProjectileMaximumHitChance = 0,
                },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(12, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(12, 0, 4, 0, DiabloCastSourceKind.MemorizedSpell),
            1);
        Assert.True(cast.Succeeded, cast.Failure?.ToString());

        processor.AdvanceTick(1, [player]);
        processor.AdvanceTick(2, [player]);

        Assert.Equal(200, splashTarget.HitPoints);
    }

    [Fact]
    public void PersistentFireballPlayerHitUsesConfiguredMagicHitBoundsAndDamageDivisor()
    {
        var directTarget = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            200,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [directTarget]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateFireballSpell() with {
                    PersistentProjectilePlayerMinimumHitChance = 100,
                    PersistentProjectilePlayerMaximumHitChance = 100,
                    PersistentProjectilePlayerDamageDivisor = 1000,
                },
            ]),
            rules: new AuthoritativeCombatRules(10, 1, 100, friendlyFire: true),
            random: new DiabloRandom(0));
        var caster = CreatePlayer(
            entityId: 7,
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(12, 1)]);
        var target = CreatePlayer(
            entityId: 8,
            positionX: 2,
            positionY: 0,
            characterLevel: 10,
            life: 100,
            lifeMaximum: 100);

        var cast = processor.Execute(
            caster,
            new DiabloCastIntent(12, 0, 4, 0, DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);
        Assert.True(cast.Succeeded, cast.Failure?.ToString());

        processor.AdvanceTick(1, [caster, target]);
        processor.AdvanceTick(2, [caster, target]);

        Assert.Equal(100, target.Life);
    }

    [Fact]
    public void BloodStarUsesNativeMagicDamageAndAppliesItsPhysicalSelfDamage()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            magicResistance: 25,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var spell = new AuthoritativeSpellDefinition(36, 25, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.BloodStar,
            ManaAdjustment = 2,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 14,
            ManaCostScale = 64,
            MagicDamageDivisor = 2,
            MagicDamageReductionDivisor = 8,
            SpellLevelDamage = 3,
            SelfDamage = 5,
            ResistantDamageDivisor = 4,
        };
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: new DiabloRandom(0x12345678));
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            life: 1280,
            lifeMaximum: 1280,
            mana: 2000,
            manaMaximum: 2000,
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(36, 2)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                36,
                0,
                1,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(960, player.Life);
        Assert.Equal(528, player.Mana);
        var selfDamage = Assert.Single(result.Events);
        Assert.Equal(new DiabloGameplayEvent(DiabloGameplayEventKind.Damage, 7, 7, 5), selfDamage);
        Assert.Equal(320, selfDamage.AmountQ6);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.BloodStar, projectile.Behavior);
        Assert.Equal(19, projectile.Damage);
        Assert.Equal(AuthoritativeDamageType.Magic, projectile.DamageType);
        Assert.Equal((16, 256U, 4), (
            projectile.Speed,
            projectile.RemainingTicks,
            projectile.ResistantDamageDivisor));

        IReadOnlyList<DiabloDirectedGameplayEvent> deliveries = [];
        for (ulong tick = 1; tick <= 32 && target.HitPoints == 40; tick++)
            deliveries = processor.AdvanceTick(tick, [player]);

        Assert.Equal(36, target.HitPoints);
        Assert.Equal(2256, target.HitPointsQ6);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(4, delivery.Event.Amount);
        Assert.Equal(304, delivery.Event.AmountQ6);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void HolyBoltDamagesUndeadButPassesThroughLivingMonsters()
    {
        var spell = new AuthoritativeSpellDefinition(5, 1, 0, 0, 0, 0) {
            DamageType = AuthoritativeDamageType.Physical,
            Range = 4,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.HolyBolt,
            RandomDamageRange = 10,
            DamageBonus = 9,
            CharacterLevelDamageDivisor = 1,
            ProjectileSpeedSpellLevelStep = 2,
            ProjectileSpeedMaximumBonus = 47,
        };
        var undead = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            monsterLevel: 1,
            undead: true);
        var living = new AuthoritativeCombatTarget(10, 1, 1, 40, monsterLevel: 1);
        var world = new DiabloWorldState(combatTargets: [undead, living]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: new DiabloRandom(0x12345678));
        var player = CreatePlayer(
            mana: 100,
            manaMaximum: 100,
            entityId: 7,
            characterLevel: 4,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            },
            memorizedSpells: [new MemorizedSpellState(5, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(5, 0, 1, 0, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        var deliveries = new List<DiabloDirectedGameplayEvent>();
        for (ulong tick = 1; tick <= 64 && world.Projectiles.Count > 0; tick++)
            deliveries.AddRange(processor.AdvanceTick(tick, [player]));

        Assert.True(undead.HitPoints < 40);
        Assert.Equal(40, living.HitPoints);
        Assert.Contains(deliveries, delivery => delivery.Event.TargetEntityId == undead.EntityId);
    }

    [Fact]
    public void HolyBoltUsesConfiguredMonsterMagicHitBounds()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            40,
            monsterLevel: 10,
            undead: true);
        var projectile = new AuthoritativeProjectile(
            0x80000000,
            7,
            0,
            5,
            0,
            1,
            0,
            1,
            0,
            4,
            AuthoritativeDamageType.Physical,
            0,
            32) {
            Behavior = AuthoritativeProjectileBehavior.HolyBolt,
            OriginalDirection = 0,
            PathTicks = 1,
            Speed = 16,
            ImpactLifetime = 1,
        };
        var world = new DiabloWorldState(
            combatTargets: [target],
            projectiles: [projectile]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(5, 1, 0, 0, 0, 0) {
                    DamageType = AuthoritativeDamageType.Physical,
                    Range = 4,
                    ProjectileSpeed = 16,
                    ProjectileLifetime = 256,
                    ProjectileBehavior = AuthoritativeProjectileBehavior.HolyBolt,
                    RandomDamageRange = 10,
                    DamageBonus = 9,
                    CharacterLevelDamageDivisor = 1,
                    ProjectileSpeedSpellLevelStep = 2,
                    ProjectileSpeedMaximumBonus = 47,
                    HolyBoltHitMonsterLevelScale = 0,
                    HolyBoltMinimumHitChance = 0,
                    HolyBoltMaximumHitChance = 0,
                },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(40, target.HitPoints);
    }

    [Fact]
    public void BoneSpiritUsesCurrentLifeDamageHomingAndPhysicalSelfDamage()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            magicResistance: 25,
            monsterLevel: 1,
            petrified: true);
        var spell = new AuthoritativeSpellDefinition(38, 24, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.BoneSpirit,
            ManaAdjustment = 1,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 12,
            ManaCostScale = 64,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 7,
            SelfDamage = 6,
            BoneSpiritDamageHitPointsDivisor = 2,
        };
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: new DiabloRandom(0x12345678));
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            life: 1280,
            lifeMaximum: 1280,
            mana: 2000,
            manaMaximum: 2000,
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(38, 2)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                38,
                0,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(896, player.Life);
        Assert.Equal(528, player.Mana);
        var selfDamage = Assert.Single(result.Events);
        Assert.Equal(384, selfDamage.AmountQ6);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.BoneSpirit, projectile.Behavior);
        Assert.Equal(0, projectile.Damage);
        Assert.Equal((16, 256U, 4), (
            projectile.Speed,
            projectile.RemainingTicks,
            projectile.ResistantDamageDivisor));

        IReadOnlyList<DiabloDirectedGameplayEvent> deliveries = [];
        for (ulong tick = 1; tick <= 32 && target.HitPoints == 40; tick++)
            deliveries = processor.AdvanceTick(tick, [player]);

        Assert.Equal(35, target.HitPoints);
        Assert.Equal(2240, target.HitPointsQ6);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(5, delivery.Event.Amount);
        Assert.Equal(320, delivery.Event.AmountQ6);
        projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(7U, projectile.RemainingTicks);

        for (ulong tick = 2; tick <= 8; tick++)
            processor.AdvanceTick(tick, [player]);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void BoneSpiritUsesConfiguredMonsterMagicHitBounds()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            40,
            monsterLevel: 10);
        var projectile = new AuthoritativeProjectile(
            0x80000000,
            7,
            0,
            38,
            0,
            1,
            0,
            1,
            0,
            0,
            AuthoritativeDamageType.Magic,
            0,
            32) {
            Behavior = AuthoritativeProjectileBehavior.BoneSpirit,
            OriginalDirection = 0,
            PathTicks = 1,
            Speed = 16,
            ImpactLifetime = 7,
            ResistantDamageDivisor = 4,
        };
        var world = new DiabloWorldState(
            combatTargets: [target],
            projectiles: [projectile]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateBoneSpiritSpell() with {
                    BoneSpiritHitMonsterLevelScale = 0,
                    BoneSpiritMinimumHitChance = 0,
                    BoneSpiritMaximumHitChance = 0,
                },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(entityId: 7);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(40, target.HitPoints);
    }

    [Fact]
    public void BoneSpiritRetargetsTheNearestMonsterAfterItsInitialDestination()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            30,
            monsterLevel: 1,
            petrified: true);
        var spell = new AuthoritativeSpellDefinition(38, 24, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.BoneSpirit,
            ManaAdjustment = 1,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 12,
            ManaCostScale = 64,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 7,
            SelfDamage = 6,
        };
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: new DiabloRandom(0x12345678));
        var player = CreatePlayer(
            mana: 2000,
            manaMaximum: 2000,
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            },
            memorizedSpells: [new MemorizedSpellState(38, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(
                38,
                0,
                1,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);

        for (ulong tick = 1; tick <= 64 && target.HitPoints == 30; tick++)
            processor.AdvanceTick(tick, [player]);

        Assert.Equal(20, target.HitPoints);
        Assert.Equal(1, world.Projectiles.Values.Single().PathIndex);
    }

    [Fact]
    public void FireWallControllerGrowsPerpendicularBranchesAndRetainsTypedSegments()
    {
        var spell = CreateFireWallSpell();
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(6, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(6, 0, 8, 5, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded, cast.Failure?.ToString());
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.FireWallController, controller.Behavior);
        Assert.Equal((8, 5, 7U), (controller.PositionX, controller.PositionY, controller.RemainingTicks));
        Assert.Equal((5, 1), (controller.WallLeftDirection, controller.WallRightDirection));

        processor.AdvanceTick(1, [player]);

        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment
                && (projectile.PositionX, projectile.PositionY) == (8, 5));
        processor.AdvanceTick(2, [player]);
        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment
                && (projectile.PositionX, projectile.PositionY) == (8, 4));
        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment
                && (projectile.PositionX, projectile.PositionY) == (8, 6));

        for (ulong tick = 3; tick <= 7; tick++)
            processor.AdvanceTick(tick, [player]);
        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallController);
        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment);
    }

    [Fact]
    public void LightningWallControllerUsesNativeLightningTuningAndPersistentSegments()
    {
        var spell = CreateLightningWallSpell();
        var world = new DiabloWorldState(fallbackWidth: 40, fallbackHeight: 40);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(50, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(50, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded, cast.Failure?.ToString());
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.LightningWallController, controller.Behavior);
        Assert.Equal(7U, controller.RemainingTicks);

        processor.AdvanceTick(1, [player]);

        var segment = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment);
        Assert.Equal(AuthoritativeDamageType.Lightning, segment.DamageType);
        Assert.Equal(144, segment.Damage);
        Assert.InRange(segment.RemainingTicks, 509U, 510U);
    }

    [Fact]
    public void FireWallSegmentsApplyShiftedFireDamageOnEveryAuthoritativeTick()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            8,
            5,
            100,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireWallSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(6, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(6, 0, 8, 5, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);

        var firstTick = processor.AdvanceTick(1, [player]);
        var firstDamage = Assert.Single(firstTick).Event;
        Assert.Equal(AuthoritativeDamageType.Fire, world.Projectiles.Values
            .First(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment)
            .DamageType);
        Assert.True(firstDamage.AmountQ6 > 0);
        var lifeAfterFirstTick = target.HitPointsQ6;

        var secondTick = processor.AdvanceTick(2, [player]);

        Assert.NotEqual(lifeAfterFirstTick, target.HitPointsQ6);
        Assert.NotEmpty(secondTick);
    }

    [Fact]
    public void RingOfFireSpawnsNativeRadiusThreeFireWallSegments()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateRingOfFireSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(44, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(44, 0, 10, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded, cast.Failure?.ToString());
        Assert.Equal(
            AuthoritativeProjectileBehavior.RingOfFireController,
            Assert.Single(world.Projectiles.Values).Behavior);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.RingOfFireController);
        var segments = world.Projectiles.Values
            .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment)
            .ToArray();
        Assert.Equal(24, segments.Length);
        Assert.Contains(segments, projectile => (projectile.PositionX, projectile.PositionY) == (10, 13));
        Assert.Contains(segments, projectile => (projectile.PositionX, projectile.PositionY) == (7, 10));
        Assert.All(segments, projectile => {
            Assert.Equal(AuthoritativeDamageType.Fire, projectile.DamageType);
            Assert.True(projectile.Damage > 0);
        });
    }

    [Fact]
    public void RingOfFireStopsAtTheFirstNativeMissileBlocker()
    {
        var world = new DiabloWorldState(
            fallbackMissileBlockedCells: [13 * 40 + 10]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateRingOfFireSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(44, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(44, 0, 10, 10, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FireWallSegment);
    }

    [Fact]
    public void ImmolationEmitsNativeMovingFireballBurst()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateImmolationSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(45, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(45, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded, cast.Failure?.ToString());
        Assert.Equal(
            AuthoritativeProjectileBehavior.ImmolationController,
            Assert.Single(world.Projectiles.Values).Behavior);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.ImmolationController);
        var fireballs = world.Projectiles.Values
            .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.Fireball)
            .ToArray();
        Assert.Equal(36, fireballs.Length);
        Assert.All(fireballs, projectile => {
            Assert.Equal(AuthoritativeDamageType.Fire, projectile.DamageType);
            Assert.True(projectile.Damage > 0);
            Assert.Equal(255U, projectile.RemainingTicks);
        });
    }

    [Fact]
    public void ImmolationUsesConfiguredProjectileCount()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateImmolationSpell() with { ImmolationProjectileCount = 7 },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(45, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(45, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        processor.AdvanceTick(1, [player]);

        Assert.Equal(
            7,
            world.Projectiles.Values.Count(projectile =>
                projectile.Behavior == AuthoritativeProjectileBehavior.Fireball));
    }

    [Fact]
    public void RadialProjectileCountsRejectValuesOutsideTheSupportedFanOut()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            CreateNovaSpell() with { NovaProjectileCount = 0 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            CreateNovaSpell() with { NovaProjectileCount = 37 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            CreateImmolationSpell() with { ImmolationProjectileCount = 0 },
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            CreateImmolationSpell() with { ImmolationProjectileCount = 37 },
        ]));
    }

    [Fact]
    public void JesterDispatchesThroughTheNativeDeterministicSpellTable()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateJesterSpell() with {
                    JesterSpellIds = [12U, 12U, 12U, 12U, 12U, 12U, 12U, 12U, 12U, 12U],
                },
                CreateFireboltSpell(),
                CreateFireballSpell(),
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(47, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(47, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded, cast.Failure?.ToString());
        Assert.True(player.Mana < 5000);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Fireball, projectile.Behavior);
        Assert.Equal(12U, projectile.SpellId);
    }

    [Fact]
    public void BerserkTransformsEligibleMonsterWithNativeDamageRollOrder()
    {
        const uint seed = 0x12345678;
        var target = new AuthoritativeCombatTarget(
            9,
            12,
            10,
            100,
            minDamage: 10,
            maxDamage: 20,
            minDamageSpecial: 30,
            maxDamageSpecial: 40);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(seed);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateBerserkSpell()]),
            random: random);
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            characterLevel: 4,
            memorizedSpells: [new MemorizedSpellState(48, 2)]);

        var expectedRandom = new DiabloRandom(seed);
        var baseDamage = new[] { 10, 20, 30, 40 };
        var expectedDamage = baseDamage
            .Select(damage => (120 + expectedRandom.RandomIntLessThan(10)) * damage / 100 + 2)
            .ToArray();

        var result = processor.Execute(
            player,
            new DiabloCastIntent(48, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(target.IsBerserked);
        Assert.Equal(expectedDamage[0], target.MinDamage);
        Assert.Equal(expectedDamage[1], target.MaxDamage);
        Assert.Equal(expectedDamage[2], target.MinDamageSpecial);
        Assert.Equal(expectedDamage[3], target.MaxDamageSpecial);
        Assert.Equal(expectedDamage[1], target.AttackDamage);
        Assert.Equal(5000 - 40 * 64, player.Mana);
        Assert.Equal(expectedRandom.State, random.State);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void BerserkUsesConfiguredMagicResistanceFrequency()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            12,
            10,
            100,
            magicResistance: 50,
            minDamage: 10,
            maxDamage: 20,
            minDamageSpecial: 30,
            maxDamageSpecial: 40);
        var processor = CreateProcessor(
            new DiabloWorldState(combatTargets: [target]),
            new AuthoritativeSpellCatalog([
                CreateBerserkSpell() with { BerserkResistanceFrequency = 1 },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(48, 2)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                48,
                9,
                12,
                10,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(target.IsBerserked);
    }

    [Fact]
    public void GolemCastSpawnsAndReplacesOwnedMinionWithNativeStats()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateGolemSpell()]));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 20000,
            manaMaximum: 20000,
            entityId: 7,
            characterLevel: 4,
            memorizedSpells: [new MemorizedSpellState(52, 2)]);

        var first = processor.Execute(
            player,
            new DiabloCastIntent(52, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(first.Succeeded, first.Failure?.ToString());
        var firstGolem = Assert.Single(world.CombatTargets.Values);
        Assert.True(firstGolem.IsPlayerMinion);
        Assert.Equal(7U, firstGolem.OwnerPlayerId);
        Assert.Equal(25, firstGolem.ArmorClass);
        Assert.Equal(12, firstGolem.MinDamage);
        Assert.Equal(20, firstGolem.MaxDamage);
        Assert.Equal(12, firstGolem.DeathAnimationTicks);
        Assert.Equal(2 * (320 * 2 + (20000 * 64) / 3), firstGolem.MaxHitPointsQ6);
        Assert.Equal(20000 - 94 * 64, player.Mana);

        var second = processor.Execute(
            player,
            new DiabloCastIntent(52, 0, 13, 10, DiabloCastSourceKind.MemorizedSpell),
            2);

        Assert.True(second.Succeeded, second.Failure?.ToString());
        var secondGolem = Assert.Single(world.CombatTargets.Values);
        Assert.NotEqual(firstGolem.EntityId, secondGolem.EntityId);
        Assert.Equal(13, secondGolem.PositionX);
        Assert.Equal(10, secondGolem.PositionY);
        Assert.Equal(7U, secondGolem.OwnerPlayerId);
    }

    [Fact]
    public void GolemCastRejectsOutOfLevelRawCoordinatesBeforeDistanceArithmetic()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateGolemSpell()]));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 20000,
            manaMaximum: 20000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(52, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                52,
                0,
                int.MinValue,
                int.MinValue,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(20000, player.Mana);
        Assert.Empty(world.CombatTargets);
    }

    [Fact]
    public void MultiplayerGolemSpawnRetainsNativeItemAndAiSeeds()
    {
        const uint initialRandomState = 0xCAFEBABE;
        var world = new DiabloWorldState();
        var random = new DiabloRandom(initialRandomState);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateGolemSpell()]),
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 20000,
            manaMaximum: 20000,
            entityId: 7,
            characterLevel: 4,
            memorizedSpells: [new MemorizedSpellState(52, 2)]);
        var expected = new DiabloRandom(initialRandomState);
        _ = expected.RandomIntBetween(1, 1);
        var expectedItemSeed = unchecked((uint)expected.AdvanceRndSeed());
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());

        var result = processor.Execute(
            player,
            new DiabloCastIntent(52, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var golem = Assert.Single(world.CombatTargets.Values);
        Assert.Equal(expectedItemSeed, golem.MonsterItemSeed);
        Assert.Equal(expectedAiSeed, golem.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal("Golem", golem.NativeAi);
    }

    [Fact]
    public void GolemCastUsesConfiguredSummonFormulaTerms()
    {
        var world = new DiabloWorldState();
        var spell = CreateGolemSpell() with {
            GolemHitPointsLevelBaseQ6 = 240,
            GolemHitPointsLevelMultiplier = 3,
            GolemHitPointsManaDivisor = 2,
            GolemDamageLevelMultiplier = 4,
            GolemMinimumDamageLevelOffset = 1,
            GolemMaximumDamageLevelOffset = 5,
            GolemArmorClass = 17,
        };
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]));
        var player = CreatePlayer(
            entityId: 7,
            characterLevel: 2,
            mana: 20000,
            manaMaximum: 20000,
            positionX: 10,
            positionY: 10,
            memorizedSpells: [new MemorizedSpellState(52, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(52, 0, 11, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var golem = Assert.Single(world.CombatTargets.Values);
        Assert.Equal(17, golem.ArmorClass);
        Assert.Equal(4 * (1 + 1), golem.MinDamage);
        Assert.Equal(4 * (1 + 5), golem.MaxDamage);
        Assert.Equal(3 * (240 * 1 + (20000 * 64) / 2), golem.MaxHitPointsQ6);
    }

    [Fact]
    public void GolemCastUsesContentOwnedSummonMonsterIdentity()
    {
        var world = new DiabloWorldState();
        var spell = CreateGolemSpell() with { GolemMonsterId = 2 };
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([
            new AuthoritativeMonsterTypeDefinition(
                2,
                2,
                "Configured Golem",
                1,
                16,
                1,
                1,
                2,
                1,
                1,
                2,
                0,
                0,
                false,
                false,
                false) {
                    NativeAi = "Zombie",
                },
        ]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            monsterTypes: monsterTypes);
        var player = CreatePlayer(
            entityId: 7,
            characterLevel: 2,
            mana: 20000,
            manaMaximum: 20000,
            positionX: 10,
            positionY: 10,
            memorizedSpells: [new MemorizedSpellState(52, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(52, 0, 11, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var summon = Assert.Single(world.CombatTargets.Values);
        Assert.Equal(2U, summon.MonsterId);
        Assert.Equal("Zombie", summon.NativeAi);
    }

    [Fact]
    public void GolemDeathAnimationPersistsAndExpiresAtNativeDuration()
    {
        var golem = new AuthoritativeCombatTarget(
            10,
            1,
            0,
            1,
            playerMinion: true,
            ownerPlayerId: 7,
            deathAnimationTicks: 3);
        var world = new DiabloWorldState(combatTargets: [golem]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        var result = processor.Execute(player, new DiabloAttackIntent(10), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(0, golem.HitPoints);
        Assert.Equal(3, golem.DeathTicksRemaining);
        Assert.Same(golem, Assert.Single(world.CombatTargets.Values));

        processor.AdvanceTick(1, [player]);
        Assert.Equal(2, golem.DeathTicksRemaining);
        processor.AdvanceTick(2, [player]);
        Assert.Equal(1, golem.DeathTicksRemaining);
        processor.AdvanceTick(3, [player]);
        Assert.Empty(world.CombatTargets);
    }

    [Fact]
    public void GolemAdvancesTowardAndAttacksNearestMonster()
    {
        var enemy = new AuthoritativeCombatTarget(
            9,
            12,
            10,
            100,
            armorClass: 0);
        var golem = new AuthoritativeCombatTarget(
            10,
            10,
            10,
            100,
            levelId: 0,
            attackDamage: 20,
            playerMinion: true,
            ownerPlayerId: 7,
            minDamage: 12,
            maxDamage: 20);
        var world = new DiabloWorldState(combatTargets: [enemy, golem]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 8,
            positionY: 8,
            entityId: 7);

        processor.AdvanceTick(1, [player]);
        Assert.True(golem.IsMoving);
        Assert.Equal((11, 10), (golem.FuturePositionX, golem.FuturePositionY));

        processor.AdvanceTick(2, [player]);
        Assert.False(golem.IsMoving);
        Assert.Equal((11, 10), (golem.PositionX, golem.PositionY));
        var hitPointsBeforeAttack = enemy.HitPointsQ6;

        processor.AdvanceTick(3, [player]);
        Assert.True(enemy.HitPointsQ6 < hitPointsBeforeAttack);
    }

    [Fact]
    public void GolemPlansAroundBlockedDirectRoute()
    {
        var enemy = new AuthoritativeCombatTarget(9, 13, 10, 100);
        var golem = new AuthoritativeCombatTarget(
            10,
            10,
            10,
            100,
            playerMinion: true,
            ownerPlayerId: 7,
            minDamage: 12,
            maxDamage: 20);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [10 * 40 + 11],
            combatTargets: [enemy, golem]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.True(golem.IsMoving);
        Assert.Equal((11, 11), (golem.FuturePositionX, golem.FuturePositionY));
    }

    [Fact]
    public void MultiplayerGolemUsesNativeRandomWalkWhenPathPlanningFails()
    {
        const uint aiSeed = 7;
        var enemy = new AuthoritativeCombatTarget(9, 13, 10, 100);
        var golem = new AuthoritativeCombatTarget(
            10,
            10,
            10,
            100,
            playerMinion: true,
            ownerPlayerId: 7,
            minDamage: 12,
            maxDamage: 20,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var blockedCells = new[] {
            9 * 40 + 12,
            9 * 40 + 13,
            9 * 40 + 14,
            10 * 40 + 12,
            10 * 40 + 14,
            11 * 40 + 12,
            11 * 40 + 13,
            11 * 40 + 14,
        };
        var world = new DiabloWorldState(
            fallbackBlockedCells: blockedCells,
            combatTargets: [enemy, golem]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            positionX: 8,
            positionY: 8,
            entityId: 7);
        player.FacingDirection = DiabloPlayerDirection.South;

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, golem.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(golem.IsMoving);
        Assert.Equal((11, 11), (golem.FuturePositionX, golem.FuturePositionY));
    }

    [Fact]
    public void GolemWandersInOwnerFacingDirectionWhenNoMonsterIsAvailable()
    {
        var golem = new AuthoritativeCombatTarget(
            10,
            10,
            10,
            100,
            playerMinion: true,
            ownerPlayerId: 7,
            minDamage: 12,
            maxDamage: 20);
        var world = new DiabloWorldState(combatTargets: [golem]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 1,
            positionY: 1,
            entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.True(golem.IsMoving);
        Assert.Equal((11, 11), (golem.FuturePositionX, golem.FuturePositionY));
    }

    [Fact]
    public void GolemIsRemovedWhenOwnerLeavesTheActiveLevel()
    {
        var golem = new AuthoritativeCombatTarget(
            10,
            10,
            10,
            100,
            levelId: 1,
            attackDamage: 20,
            playerMinion: true,
            ownerPlayerId: 7,
            minDamage: 12,
            maxDamage: 20);
        var world = new DiabloWorldState(combatTargets: [golem]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(
            positionX: 8,
            positionY: 8,
            levelId: 2,
            entityId: 7);

        processor.AdvanceTick(1, [player]);
        Assert.Empty(world.CombatTargets);
    }

    [Fact]
    public void BerserkRejectsUniqueAndDiabloTargets()
    {
        var unique = new AuthoritativeCombatTarget(9, 12, 10, 100, unique: true);
        var diablo = new AuthoritativeCombatTarget(10, 13, 10, 100, diablo: true);
        var world = new DiabloWorldState(combatTargets: [unique, diablo]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateBerserkSpell()]));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            memorizedSpells: [new MemorizedSpellState(48, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(48, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, result.Failure);
        Assert.False(unique.IsBerserked);
        Assert.False(diablo.IsBerserked);
    }

    [Fact]
    public void WarpCastUsesTheAuthoritativeLevelTriggerAndConsumesMana()
    {
        var authoritativeWorld = new AuthoritativeWorld();
        authoritativeWorld.AddLevel(new AuthoritativeLevel(1, 5, 5));
        authoritativeWorld.AddLevel(new AuthoritativeLevel(2, 5, 5));
        authoritativeWorld.AddWarpTarget(new AuthoritativeWarpTarget(7, 1, 1, 1, 2, 2, 2));
        var world = new DiabloWorldState(authoritativeWorld);
        var worldCommands = new DiabloWorldCommandProcessor(world);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateWarpSpell()]),
            worldCommands: worldCommands);
        var player = CreatePlayer(
            positionX: 0,
            positionY: 0,
            levelId: 1,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(49, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(49, 0, player.PositionX, player.PositionY, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(5000 - 35 * 64, player.Mana);
    }

    [Fact]
    public void FlameWaveControllerPlacesNativeMovingBranches()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFlameWaveSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(15, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(15, 0, 8, 5, DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded, cast.Failure?.ToString());
        Assert.Equal(1U, Assert.Single(world.Projectiles.Values).RemainingTicks);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveController);
        var segments = world.Projectiles.Values
            .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveSegment)
            .ToArray();
        Assert.Equal(9, segments.Length);
        Assert.Contains(segments, projectile => (projectile.StartX, projectile.StartY) == (6, 5));
        Assert.Contains(segments, projectile => (projectile.StartX, projectile.StartY) == (6, 4));
        Assert.Contains(segments, projectile => (projectile.StartX, projectile.StartY) == (6, 6));
        Assert.All(segments, projectile => Assert.Equal(16, projectile.Speed));
    }

    [Fact]
    public void FlameWaveSegmentUsesNativeFireDamageAndCollision()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            6,
            5,
            100,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFlameWaveSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(15, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(15, 0, 8, 5, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);

        var events = processor.AdvanceTick(1, [player]);

        Assert.NotEmpty(events);
        Assert.True(target.HitPointsQ6 < DiabloFixedPointQ6.FromWhole(100));
        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.FlameWaveSegment
                && projectile.Damage >= 2);
    }

    [Fact]
    public void InfernoControllerEmitsAtMostThreeNativeSegments()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(21, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(21, 0, 8, 5, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);

        processor.AdvanceTick(1, [player]);
        Assert.Single(world.Projectiles.Values, projectile =>
            projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
        Assert.Single(world.Projectiles.Values, projectile =>
            projectile.Behavior == AuthoritativeProjectileBehavior.InfernoSegment);

        processor.AdvanceTick(2, [player]);
        processor.AdvanceTick(3, [player]);

        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoController);
        var segments = world.Projectiles.Values
            .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.InfernoSegment)
            .ToArray();
        Assert.Equal(2, segments.Length);
        Assert.Equal(new[] { 17U, 20U }, segments
            .OrderBy(projectile => projectile.EntityId)
            .Select(projectile => projectile.RemainingTicks)
            .ToArray());
        Assert.All(segments, projectile => Assert.Equal(0, projectile.Speed));
    }

    [Fact]
    public void InfernoSegmentsRepeatNativeFireCollisionUntilExpiry()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            6,
            5,
            100,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateInfernoSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(21, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(21, 0, 6, 5, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        processor.AdvanceTick(1, [player]);
        var firstDamage = target.HitPointsQ6;
        var firstEvents = processor.AdvanceTick(2, [player]);
        var secondDamage = target.HitPointsQ6;
        var secondEvents = processor.AdvanceTick(3, [player]);

        Assert.NotEmpty(firstEvents);
        Assert.NotEmpty(secondEvents);
        Assert.True(secondDamage < firstDamage);
    }

    [Fact]
    public void ChainLightningControllerFansOutNativeLightningBolts()
    {
        var world = new DiabloWorldState(combatTargets: [
            new AuthoritativeCombatTarget(9, 6, 5, 100, petrified: true),
            new AuthoritativeCombatTarget(10, 7, 6, 100, petrified: true),
        ]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateChainLightningSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(16, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(16, 0, 8, 5, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        Assert.Single(world.Projectiles.Values, projectile =>
            projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningController);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningController);
        var bolts = world.Projectiles.Values
            .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.ChainLightningBolt)
            .ToArray();
        Assert.Equal(3, bolts.Length);
        Assert.All(bolts, bolt => {
            Assert.Equal(32, bolt.Speed);
            Assert.Equal(16U, bolt.SpellId);
            Assert.Equal(0, bolt.Damage);
        });
    }

    [Fact]
    public void NovaControllerEmitsNativeRadialLightningBalls()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateNovaSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            characterLevel: 1,
            memorizedSpells: [new MemorizedSpellState(18, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(18, 0, 14, 10, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.NovaController, controller.Behavior);
        Assert.True(controller.Damage > 0);

        processor.AdvanceTick(1, [player]);

        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.NovaController);
        var balls = world.Projectiles.Values
            .Where(projectile => projectile.Behavior == AuthoritativeProjectileBehavior.NovaBall)
            .ToArray();
        Assert.Equal(36, balls.Length);
        Assert.All(balls, ball => {
            Assert.Equal(16, ball.Speed);
            Assert.Equal(18U, ball.SpellId);
            Assert.Equal(controller.Damage, ball.Damage);
            Assert.Equal(254U, ball.RemainingTicks);
        });
    }

    [Fact]
    public void NovaBallSurvivesItsTargetShrineCollision()
    {
        var shrine = new AuthoritativeWorldObject(
            20,
            4,
            0,
            2,
            2,
            missilePassable: false,
            isShrine: true);
        var projectile = new AuthoritativeProjectile(
            50,
            7,
            0,
            18,
            0,
            2,
            2,
            2,
            2,
            1,
            AuthoritativeDamageType.Lightning,
            0,
            2) {
            Behavior = AuthoritativeProjectileBehavior.NovaBall,
        };
        var world = new DiabloWorldState(
            objects: [shrine],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Same(projectile, Assert.Single(world.Projectiles.Values));
        Assert.Equal(1U, projectile.RemainingTicks);
    }

    [Fact]
    public void NovaBallStopsAtARegularMissileBlockingObject()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            2,
            2,
            missilePassable: false);
        var projectile = new AuthoritativeProjectile(
            50,
            7,
            0,
            18,
            0,
            2,
            2,
            2,
            2,
            1,
            AuthoritativeDamageType.Lightning,
            0,
            2) {
            Behavior = AuthoritativeProjectileBehavior.NovaBall,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void NovaBallBreaksARegularBlockingObjectBeforeRemoval()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            2,
            2,
            breakable: true);
        var projectile = new AuthoritativeProjectile(
            50,
            7,
            0,
            18,
            0,
            2,
            2,
            2,
            2,
            1,
            AuthoritativeDamageType.Lightning,
            0,
            2) {
            Behavior = AuthoritativeProjectileBehavior.NovaBall,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            projectiles: [projectile]);
        var processor = CreateProcessor(world);
        var player = CreatePlayer(entityId: 7);

        processor.AdvanceTick(1, [player]);

        Assert.True(blocker.Destroyed);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void NovaControllerUsesConfiguredProjectileCount()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateNovaSpell() with { NovaProjectileCount = 5 },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 5000,
            manaMaximum: 5000,
            entityId: 7,
            characterLevel: 1,
            memorizedSpells: [new MemorizedSpellState(18, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(18, 0, 14, 10, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        processor.AdvanceTick(1, [player]);

        Assert.Equal(
            5,
            world.Projectiles.Values.Count(projectile =>
                projectile.Behavior == AuthoritativeProjectileBehavior.NovaBall));
    }

    [Fact]
    public void GreaterRuneOfLightningResolvesThroughTheStableNovaSource()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateNovaSpell() with { ConsumableMiscId = 49 },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            entityId: 7,
            belt: [new BeltStoreItem(
                2,
                72,
                AuthoritativeItemState.Empty with { MiscId = 49 })]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                18,
                0,
                14,
                10,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 2)),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Belt);
        Assert.Equal(AuthoritativeProjectileBehavior.NovaController,
            Assert.Single(world.Projectiles.Values).Behavior);
    }

    [Fact]
    public void ApocalypseControllerScansRowMajorAndEmitsPhysicalBooms()
    {
        var world = new DiabloWorldState(
            fallbackBlockedCells: [2 * 40 + 3],
            combatTargets: [
                new AuthoritativeCombatTarget(
                    9,
                    2,
                    2,
                    100,
                    petrified: true,
                    playerMinion: true),
                new AuthoritativeCombatTarget(
                    10,
                    4,
                    2,
                    100,
                    petrified: true),
            ]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateApocalypseSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 20000,
            manaMaximum: 20000,
            entityId: 7,
            characterLevel: 3,
            memorizedSpells: [new MemorizedSpellState(25, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(25, 0, 0, 0, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.ApocalypseController, controller.Behavior);
        Assert.Equal((2, 2), (controller.ApocalypseScanX, controller.ApocalypseScanY));

        processor.AdvanceTick(1, [player]);

        Assert.Contains(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseController);
        var boom = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.ApocalypseBoom);
        Assert.Equal((4, 2), (boom.PositionX, boom.PositionY));
        Assert.Equal(AuthoritativeDamageType.Physical, boom.DamageType);
        Assert.Equal(14U, boom.RemainingTicks);
        Assert.True(boom.ApocalypseHit);
        Assert.True(world.CombatTargets[10].HitPoints < 100);
        Assert.Equal(100, world.CombatTargets[9].HitPoints);
    }

    [Fact]
    public void ApocalypseBoomBreaksABlockingObjectWithoutEndingItsLifetime()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            1,
            breakable: true);
        var boom = new AuthoritativeProjectile(
            100,
            7,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            5,
            AuthoritativeDamageType.Physical,
            0,
            2U,
            allowZeroSpellId: true) {
            Behavior = AuthoritativeProjectileBehavior.ApocalypseBoom,
        };
        var world = new DiabloWorldState(
            objects: [blocker],
            projectiles: [boom]);
        var processor = CreateProcessor(world);

        processor.AdvanceTick(1, []);

        Assert.True(blocker.Destroyed);
        Assert.Equal(1U, boom.RemainingTicks);
        Assert.Contains(boom, world.Projectiles.Values);
    }

    [Fact]
    public void RageRunsNativeActiveCooldownAndQ6ExpiryDamage()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateRageSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            life: 6400,
            lifeMaximum: 6400,
            mana: 20000,
            manaMaximum: 20000,
            entityId: 7,
            characterLevel: 3,
            memorizedSpells: [new MemorizedSpellState(22, 1)]);
        var intent = new DiabloCastIntent(
            22,
            0,
            0,
            0,
            DiabloCastSourceKind.MemorizedSpell);

        Assert.True(processor.Execute(player, intent, 1).Succeeded);
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.RageController, controller.Behavior);
        Assert.Equal(251U, controller.RemainingTicks);
        Assert.Equal(18, controller.Damage);
        var manaAfterCast = player.Mana;

        Assert.True(processor.Execute(player, intent, 2).Succeeded);
        Assert.Equal(manaAfterCast, player.Mana);

        for (ulong tick = 1; tick <= 251; tick++)
            processor.AdvanceTick(tick, [player]);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, controller.Phase);
        Assert.Equal(251U, controller.RemainingTicks);

        for (ulong tick = 252; tick <= 501; tick++)
            processor.AdvanceTick(tick, [player]);
        Assert.Equal(1U, controller.RemainingTicks);

        var expiryEvents = processor.AdvanceTick(502, [player]);
        Assert.Empty(world.Projectiles);
        Assert.Equal(6382, player.Life);
        var damage = Assert.Single(expiryEvents).Event;
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal(18, damage.AmountQ6);

        var lowLifePlayer = CreatePlayer(
            life: 192,
            lifeMaximum: 6400,
            mana: 20000,
            manaMaximum: 20000,
            entityId: 8,
            characterLevel: 3,
            memorizedSpells: [new MemorizedSpellState(22, 1)]);
        var lowLifeMana = lowLifePlayer.Mana;
        Assert.True(processor.Execute(lowLifePlayer, intent, 503).Succeeded);
        Assert.Equal(lowLifeMana, lowLifePlayer.Mana);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void RageUsesConfiguredAdmissionLifeThreshold()
    {
        var world = new DiabloWorldState();
        var spell = CreateRageSpell() with {
            RageMinimumLifeQ6PerCharacterLevel = 32,
        };
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            life: 97,
            lifeMaximum: 6400,
            mana: 20000,
            manaMaximum: 20000,
            entityId: 7,
            characterLevel: 3,
            memorizedSpells: [new MemorizedSpellState(22, 1)]);
        var intent = new DiabloCastIntent(
            22,
            0,
            0,
            0,
            DiabloCastSourceKind.MemorizedSpell);

        Assert.True(processor.Execute(player, intent, 1).Succeeded);
        Assert.Single(world.Projectiles.Values);
    }

    [Fact]
    public void ElementalRetargetsAndAppliesPersistentNineCellFireBurst()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            14,
            10,
            1000,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateElementalSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 10000,
            manaMaximum: 10000,
            entityId: 7,
            characterLevel: 3,
            memorizedSpells: [new MemorizedSpellState(31, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(31, 0, 11, 10, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        var elemental = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Elemental, elemental.Behavior);
        Assert.Equal(256U, elemental.RemainingTicks);
        Assert.Equal((11, 10), (elemental.TargetX, elemental.TargetY));
        Assert.True(elemental.Damage > 0);

        ulong tick = 1;
        while (elemental.Phase == AuthoritativeProjectilePhase.Travel
            && elemental.PathIndex == 0
            && tick < 300) {
            processor.AdvanceTick(tick++, [player]);
        }
        Assert.Equal(1, elemental.PathIndex);
        Assert.Equal(255U, elemental.RemainingTicks);
        Assert.Equal((14, 10), (elemental.TargetX, elemental.TargetY));

        while (elemental.Phase == AuthoritativeProjectilePhase.Travel
            && tick < 600) {
            processor.AdvanceTick(tick++, [player]);
        }
        Assert.Equal(AuthoritativeProjectilePhase.Impact, elemental.Phase);
        Assert.Equal(14U, elemental.RemainingTicks);
        var lifeAfterDirectHit = target.HitPointsQ6;
        Assert.True(lifeAfterDirectHit < target.MaxHitPointsQ6);

        for (var burstTick = 0; burstTick < 14; burstTick++)
            processor.AdvanceTick(tick++, [player]);
        Assert.Empty(world.Projectiles);
        Assert.True(target.HitPointsQ6 < lifeAfterDirectHit);
    }

    [Fact]
    public void ElementalUsesValidatedBurstOffsetOrder()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            14,
            11,
            1000,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateElementalSpell() with {
                    ElementalBurstOffsets = [
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0),
                    ],
                },
            ]),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 10000,
            manaMaximum: 10000,
            entityId: 7,
            characterLevel: 3,
            memorizedSpells: [new MemorizedSpellState(31, 1)]);
        world.Projectiles.Add(0x80000001, new AuthoritativeProjectile(
            0x80000001,
            player.EntityId,
            0,
            31,
            player.LevelId,
            14,
            10,
            14,
            10,
            100,
            AuthoritativeDamageType.Fire,
            1,
            14) {
            Behavior = AuthoritativeProjectileBehavior.Elemental,
            Phase = AuthoritativeProjectilePhase.Impact,
            ImpactLifetime = 14,
            ResistantDamageDivisor = 4,
            EffectiveSpellLevel = 1,
            ElementalOriginX = player.PositionX,
            ElementalOriginY = player.PositionY,
        });

        processor.AdvanceTick(1, [player]);

        Assert.Equal(target.MaxHitPointsQ6, target.HitPointsQ6);
    }

    [Fact]
    public void GuardianSpawnsAndFiresNativeFireboltOnSixTileArc()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            18,
            10,
            1000,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateGuardianSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            mana: 10000,
            manaMaximum: 10000,
            entityId: 7,
            characterLevel: 4,
            memorizedSpells: [new MemorizedSpellState(7, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(7, 0, 12, 10, DiabloCastSourceKind.MemorizedSpell),
            1).Succeeded);
        var guardian = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.GuardianController, guardian.Behavior);
        Assert.Equal((12, 10), (guardian.PositionX, guardian.PositionY));
        Assert.Equal(48U, guardian.RemainingTicks);

        for (ulong tick = 1; tick < 16; tick++)
            processor.AdvanceTick(tick, [player]);
        Assert.Single(world.Projectiles.Values);

        processor.AdvanceTick(16, [player]);
        Assert.Equal(3, guardian.GuardianState);
        var bolt = Assert.Single(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt);
        Assert.Equal((18, 10), (bolt.TargetX, bolt.TargetY));
        Assert.Equal(18, bolt.Speed);
        Assert.True(bolt.Damage > 0);
    }

    [Fact]
    public void GuardianUsesValidatedArcOffsetOrder()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            16,
            10,
            1000,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateGuardianSpell() with {
                    GuardianArcOffsets = [
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                    ],
                },
            ]),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            positionX: 10,
            positionY: 10,
            entityId: 7,
            characterClassId: 2);
        world.Projectiles.Add(0x80000001, new AuthoritativeProjectile(
            0x80000001,
            player.EntityId,
            0,
            7,
            player.LevelId,
            10,
            10,
            10,
            10,
            0,
            AuthoritativeDamageType.Fire,
            0,
            17) {
            Behavior = AuthoritativeProjectileBehavior.GuardianController,
            EffectiveSpellLevel = 1,
        });

        processor.AdvanceTick(1, [player]);

        Assert.Single(world.Projectiles);
        Assert.DoesNotContain(
            world.Projectiles.Values,
            projectile => projectile.Behavior == AuthoritativeProjectileBehavior.Firebolt);
    }

    private static AuthoritativeSpellDefinition CreateFireWallSpell()
    {
        return new AuthoritativeSpellDefinition(6, 28, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.FireWallController,
            RandomDamageRange = 10,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 2,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 8,
            ManaCostScale = 64,
            DamageBonus = 2,
            DamageScale = 8,
            RandomDamageIterationsBonus = 2,
            WallControlLifetime = 7,
            WallSegmentBaseDuration = 10,
            WallSegmentSpellLevelDurationStep = 10,
            WallSegmentCharacterLevelDurationStep = 1,
            WallSegmentDurationScale = 16,
            WallCharacterLevelDamageMultiplier = 1,
        };
    }

    private static AuthoritativeSpellDefinition CreateRingOfFireSpell()
    {
        return new AuthoritativeSpellDefinition(44, 28, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.RingOfFireController,
            RandomDamageRange = 10,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 2,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 8,
            ManaCostScale = 64,
            DamageBonus = 2,
            DamageScale = 8,
            RandomDamageIterationsBonus = 2,
            RingOfFireSearchRadius = 3,
            WallSegmentBaseDuration = 10,
            WallSegmentSpellLevelDurationStep = 10,
            WallSegmentCharacterLevelDurationStep = 1,
            WallSegmentDurationScale = 16,
            WallCharacterLevelDamageMultiplier = 1,
        };
    }

    private static AuthoritativeSpellDefinition CreateImmolationSpell()
    {
        return new AuthoritativeSpellDefinition(45, 1, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.ImmolationController,
            RandomDamageRange = 6,
            MagicDamageDivisor = 2,
            DamageBonus = 5,
            CharacterLevelDamageDivisor = 1,
            RandomDamageIterationsBonus = 5,
            SpellEffectScaleDivisor = 8,
            DamageScale = 64,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ProjectileSpeedSpellLevelStep = 1,
            ProjectileSpeedMaximumBonus = 34,
            ImmolationControllerLifetime = 1,
            ImmolationSearchRadius = 4,
            ImmolationProjectileCount = 36,
        };
    }

    private static AuthoritativeSpellDefinition CreateLightningWallSpell()
    {
        return new AuthoritativeSpellDefinition(50, 35, 0, 0, 0, 0) {
            NativeSpellId = 40,
            Range = 112,
            DamageType = AuthoritativeDamageType.Lightning,
            ProjectileLifetime = 1,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.LightningWallController,
            RandomDamageRange = 10,
            RandomDamageIterationsBonus = 2,
            DamageBonus = 2,
            DamageScale = 16,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 3,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 18,
            ManaCostScale = 64,
            WallControlLifetime = 7,
            WallSegmentBaseDuration = 255,
            WallSegmentSpellLevelDurationStep = 255,
            WallSegmentCharacterLevelDurationStep = 0,
            WallSegmentDurationScale = 1,
            WallCharacterLevelDamageMultiplier = 1,
        };
    }

    private static AuthoritativeSpellDefinition CreateJesterSpell()
    {
        return new AuthoritativeSpellDefinition(47, 20, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Jester,
            ResistantDamageDivisor = 1,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 3,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 15,
            ManaCostScale = 64,
            DamageScale = 1,
            HealingScale = 1,
            ResurrectionLifeScale = 1,
            StaffLevel = 10,
        };
    }

    private static AuthoritativeSpellDefinition CreateBerserkSpell()
    {
        return new AuthoritativeSpellDefinition(48, 40, 0, 0, 0, 0) {
            NativeSpellId = 44,
            Range = 5,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Berserk,
            ManaCostScale = 64,
            BerserkSearchRadius = 5,
            BerserkDamagePercentBase = 120,
            BerserkDamagePercentRandomRange = 10,
            BerserkSpellLevelBonus = 1,
        };
    }

    private static AuthoritativeSpellDefinition CreateGolemSpell()
    {
        return new AuthoritativeSpellDefinition(52, 100, 0, 0, 0, 0) {
            NativeSpellId = 21,
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileLifetime = 1,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Golem,
            ManaAdjustment = 6,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 60,
            ManaCostScale = 64,
            StaffLevel = 9,
            GolemDeathAnimationTicks = 12,
        };
    }

    private static AuthoritativeSpellDefinition CreateWarpSpell()
    {
        return new AuthoritativeSpellDefinition(49, 35, 0, 0, 0, 0) {
            NativeSpellId = 42,
            Range = 1,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 2,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Warp,
            ManaAdjustment = 3,
            MinimumManaCost = 18,
            ManaCostScale = 64,
        };
    }

    private static AuthoritativeSpellDefinition CreateFlameWaveSpell()
    {
        return new AuthoritativeSpellDefinition(15, 35, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileSpeed = 16,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.FlameWaveController,
            RandomDamageRange = 10,
            DamageBonus = 1,
            SecondaryProjectileLifetime = 255,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 3,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 20,
            ManaCostScale = 64,
            StaffLevel = 8,
            FlameWaveControllerLifetime = 1,
            FlameWaveBranchBase = 2,
            FlameWaveBranchSpellLevelDivisor = 2,
            FlameWaveCharacterLevelDamageBonus = 1,
        };
    }

    private static AuthoritativeSpellDefinition CreateInfernoSpell()
    {
        return new AuthoritativeSpellDefinition(21, 11, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileSpeed = 32,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.InfernoController,
            RandomDamageRange = 2,
            DamageBonus = 24,
            DamageScale = 12,
            SecondaryProjectileLifetime = 20,
            ResistantDamageDivisor = 1,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 1,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 6,
            ManaCostScale = 64,
            StaffLevel = 2,
            InfernoSegmentLifetimeStep = 1,
        };
    }

    private static AuthoritativeSpellDefinition CreateChainLightningSpell()
    {
        return new AuthoritativeSpellDefinition(16, 30, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Lightning,
            ProjectileSpeed = 32,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.ChainLightningController,
            RandomDamageRange = 2,
            DamageBonus = 2,
            CharacterLevelRandomDamageDivisor = 1,
            DamageScale = 64,
            SecondaryProjectileLifetime = 6,
            SecondaryProjectileLifetimeSpellLevelDivisor = 2,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 1,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 18,
            ManaCostScale = 64,
            StaffLevel = 7,
            ChainLightningSearchRadiusBonus = 3,
            ChainLightningControllerLifetime = 1,
        };
    }

    private static AuthoritativeSpellDefinition CreateNovaSpell()
    {
        return new AuthoritativeSpellDefinition(18, 60, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Lightning,
            ProjectileSpeed = 16,
            ProjectileLifetime = 255,
            ProjectileBehavior = AuthoritativeProjectileBehavior.NovaController,
            RandomDamageRange = 6,
            MagicDamageDivisor = 2,
            DamageBonus = 5,
            RandomDamageIterationsBonus = 5,
            CharacterLevelDamageDivisor = 1,
            SpellEffectScaleDivisor = 8,
            DamageScale = 64,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 3,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 35,
            ManaCostScale = 64,
            StaffLevel = 10,
            NovaControllerLifetime = 1,
            NovaSearchRadius = 4,
            NovaProjectileCount = 36,
        };
    }

    private static AuthoritativeSpellDefinition CreateApocalypseSpell()
    {
        return new AuthoritativeSpellDefinition(25, 150, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 15,
            ProjectileBehavior = AuthoritativeProjectileBehavior.ApocalypseController,
            RandomDamageRange = 6,
            RandomDamageIterationsCharacterLevelDivisor = 1,
            ResistantDamageDivisor = 1,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 6,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 90,
            ManaCostScale = 64,
            StaffLevel = 15,
            ApocalypseControllerLifetime = 255,
            ApocalypseSearchRadius = 8,
        };
    }

    private static AuthoritativeSpellDefinition CreateRageSpell()
    {
        return new AuthoritativeSpellDefinition(22, 15, 0, 0, 0, 0) {
            Range = 1,
            DamageType = AuthoritativeDamageType.Physical,
            ProjectileLifetime = 245,
            ProjectileBehavior = AuthoritativeProjectileBehavior.RageController,
            ResistantDamageDivisor = 1,
            ProjectileImpactLifetime = 1,
            MinimumManaCost = 1,
            ManaCostScale = 64,
            StaffLevel = -1,
            RageControllerLifetime = 245,
            RageCharacterLevelLifetimeScale = 2,
            RageSelfDamageCharacterLevel = 6,
        };
    }

    private static AuthoritativeSpellDefinition CreateElementalSpell()
    {
        return new AuthoritativeSpellDefinition(31, 35, 0, 0, 0, 0) {
            Range = 1,
            DamageAmount = 4,
            AreaRadius = 1,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Elemental,
            RandomDamageRange = 10,
            MagicDamageDivisor = 2,
            RandomDamageIterationsBonus = 2,
            SpellEffectScaleDivisor = 8,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 14,
            ManaAdjustment = 2,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 20,
            ManaCostScale = 64,
            StaffLevel = 6,
            ElementalRetargetLifetime = 255,
            ElementalCharacterLevelDamageMultiplier = 2,
        };
    }

    private static AuthoritativeSpellDefinition CreateGuardianSpell()
    {
        return new AuthoritativeSpellDefinition(7, 50, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.GuardianController,
            RandomDamageRange = 10,
            MagicDamageDivisor = 8,
            SpellLevelDamage = 1,
            DamageBonus = 1,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 1,
            ManaAdjustment = 2,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 30,
            ManaCostScale = 64,
            GuardianDurationTickScale = 16,
            GuardianMinimumDuration = 30,
            GuardianMaximumDuration = 30,
            GuardianCharacterLevelDurationDivisor = 2,
            GuardianSpawnSearchRadius = 5,
            GuardianTargetSearchRadius = 6,
            GuardianFireInterval = 16,
            GuardianProjectileSpeedLevelStep = 2,
            GuardianProjectileSpeedMaximumBonus = 47,
        };
    }

    [Theory]
    [InlineData(DiabloGameVariant.Diablo, 1104)]
    [InlineData(DiabloGameVariant.Hellfire, 1360)]
    public void FireballUsesNativeDamageLevelSpeedAndVariantManaCost(
        DiabloGameVariant variant,
        int expectedMana)
    {
        var world = new DiabloWorldState();
        var random = new DiabloRandom(0x12345678);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireballSpell()]),
            new AuthoritativeCombatRules(10, 1, 100, gameVariant: variant),
            random,
            classes);
        var player = CreatePlayer(
            mana: 2000,
            manaMaximum: 2000,
            entityId: 7,
            characterClassId: 2,
            characterLevel: 12,
            memorizedSpells: [new MemorizedSpellState(12, 3)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                12,
                0,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedMana, player.Mana);
        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Fireball, projectile.Behavior);
        Assert.Equal(58, projectile.Damage);
        Assert.Equal(22, projectile.Speed);
        Assert.Equal((1019480, 509740), (
            projectile.VelocityX,
            projectile.VelocityY));
        Assert.Equal((1, 14, 4), (
            projectile.AreaRadius,
            projectile.ImpactLifetime,
            projectile.ResistantDamageDivisor));
        Assert.Equal(3, projectile.EffectiveSpellLevel);
        Assert.Equal(0x560EB8EEU, random.State);
    }

    [Fact]
    public void FireballStopsAtARegularMissileBlockingObject()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            missilePassable: false);
        var world = new DiabloWorldState(objects: [blocker]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireballSpell()]));
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 12,
            abilitySpellId: 12,
            memorizedSpells: [new MemorizedSpellState(12, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(12, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);

        _ = processor.AdvanceTick(1, [player]);
        _ = processor.AdvanceTick(2, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal((1, 0), (projectile.PositionX, projectile.PositionY));
        Assert.False(blocker.Destroyed);
    }

    [Fact]
    public void FireballBreaksAHitBreakableMissileBlockingObject()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            missilePassable: false,
            breakable: true);
        var world = new DiabloWorldState(objects: [blocker]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireballSpell()]));
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 12,
            abilitySpellId: 12,
            memorizedSpells: [new MemorizedSpellState(12, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(12, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);

        _ = processor.AdvanceTick(1, [player]);
        _ = processor.AdvanceTick(2, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal((1, 0), (projectile.PositionX, projectile.PositionY));
        Assert.True(blocker.Destroyed);
    }

    [Fact]
    public void FireballDirectHitCreatesNativeShiftedSplashAndPersistentImpact()
    {
        var directTarget = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            200,
            monsterLevel: 1,
            petrified: true);
        var splashTarget = new AuthoritativeCombatTarget(
            10,
            1,
            1,
            200,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [directTarget, splashTarget]);
        var random = new DiabloRandom(0x12345678);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFireballSpell()]),
            random: random,
            playerClasses: classes);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 12,
            abilitySpellId: 12,
            memorizedSpells: [new MemorizedSpellState(12, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(12, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        var deliveries = processor.AdvanceTick(2, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal((1, 0), (projectile.PositionX, projectile.PositionY));
        Assert.Equal((0, 0), (projectile.VelocityX, projectile.VelocityY));
        Assert.Equal(14U, projectile.RemainingTicks);
        Assert.Equal(9030, directTarget.HitPointsQ6);
        Assert.Equal(12742, splashTarget.HitPointsQ6);
        Assert.Equal([3712, 58, 58], deliveries.Select(delivery => delivery.Event.AmountQ6));
        Assert.Equal(0xDA029030U, random.State);

        for (ulong tick = 3; tick <= 15; tick++)
            Assert.Empty(processor.AdvanceTick(tick, [player]));
        Assert.Single(world.Projectiles);
        Assert.Empty(processor.AdvanceTick(16, [player]));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void FireballUsesValidatedExplosionOffsetOrder()
    {
        var directTarget = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            200,
            monsterLevel: 1,
            petrified: true);
        var omittedSplashTarget = new AuthoritativeCombatTarget(
            10,
            1,
            1,
            200,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [directTarget, omittedSplashTarget]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateFireballSpell() with {
                    FireballExplosionOffsets = [
                        (0, 0), (0, -1), (1, 0), (1, -1), (1, 1),
                        (-1, 0), (-1, 1), (-1, -1), (-1, -1),
                    ],
                },
            ]),
            random: new DiabloRandom(0x12345678),
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 12,
            abilitySpellId: 12,
            memorizedSpells: [new MemorizedSpellState(12, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(12, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        _ = processor.AdvanceTick(2, [player]);

        Assert.True(directTarget.HitPointsQ6 < DiabloFixedPointQ6.FromWhole(200));
        Assert.Equal(DiabloFixedPointQ6.FromWhole(200), omittedSplashTarget.HitPointsQ6);
    }

    [Fact]
    public void RuneOfFireResolvesAnImmediateBigExplosionAndConsumesTheRune()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            4,
            0,
            200,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateFireballSpell() with { ConsumableMiscId = 47 },
            ]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            belt: [new BeltStoreItem(
                2,
                72,
                AuthoritativeItemState.Empty with { MiscId = 47 })]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                12,
                0,
                4,
                0,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 2)),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Belt);
        Assert.Empty(world.Projectiles);
        Assert.True(target.HitPointsQ6 < DiabloFixedPointQ6.FromWhole(200));
        Assert.Contains(result.Events, gameplayEvent =>
            gameplayEvent.Kind == DiabloGameplayEventKind.Damage
            && gameplayEvent.TargetEntityId == target.EntityId);
    }

    [Theory]
    [InlineData(DiabloGameVariant.Diablo, 488)]
    [InlineData(DiabloGameVariant.Hellfire, 616)]
    public void LightningCreatesNativeControllerAndUsesVariantManaCost(
        DiabloGameVariant variant,
        int expectedMana)
    {
        var world = new DiabloWorldState();
        var random = new DiabloRandom(0x12345678);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateLightningSpell()]),
            new AuthoritativeCombatRules(10, 1, 100, gameVariant: variant),
            random,
            classes);
        var player = CreatePlayer(
            mana: 1000,
            manaMaximum: 1000,
            characterLevel: 12,
            entityId: 7,
            characterClassId: 2,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(3, 3)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                3,
                0,
                4,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedMana, player.Mana);
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Lightning, controller.Behavior);
        Assert.Equal(0, controller.Damage);
        Assert.Equal(3, controller.EffectiveSpellLevel);
        Assert.Equal((1482880, 741440), (
            controller.VelocityX,
            controller.VelocityY));
        Assert.Equal(256U, controller.RemainingTicks);
        Assert.Equal(0x955E76D9U, random.State);
    }

    [Fact]
    public void LightningSpawnsPersistentSegmentAndAppliesNativeQ6Damage()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            lightningResistance: 25,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var random = new DiabloRandom(0x12345678);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateLightningSpell()]),
            random: random,
            playerClasses: classes);
        var player = CreatePlayer(
            characterLevel: 12,
            entityId: 7,
            characterClassId: 2,
            abilitySpellId: 3,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(3, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(3, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);

        var deliveries = processor.AdvanceTick(1, [player]);

        var projectiles = world.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, projectiles.Length);
        var controller = projectiles[0];
        var segment = projectiles[1];
        Assert.Equal(AuthoritativeProjectileBehavior.Lightning, controller.Behavior);
        Assert.Equal((1, 0), (controller.PositionX, controller.PositionY));
        Assert.Equal(254U, controller.RemainingTicks);
        Assert.Equal(AuthoritativeProjectileBehavior.LightningSegment, segment.Behavior);
        Assert.Equal((1, 0), (segment.PositionX, segment.PositionY));
        Assert.Equal((0, 0), (segment.StartX, segment.StartY));
        Assert.Equal(768, segment.Damage);
        Assert.Equal(6U, segment.RemainingTicks);
        Assert.Equal(17, target.HitPoints);
        Assert.Equal(1088, target.HitPointsQ6);
        var delivery = Assert.Single(deliveries);
        Assert.Equal(3, delivery.Event.Amount);
        Assert.Equal(192, delivery.Event.AmountQ6);
        Assert.Equal(0xE8098E4AU, random.State);
    }

    [Fact]
    public void LightningControllerStopsAtBlockingObjectWithoutBreakingItOrSpawningSegment()
    {
        var blocker = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            0,
            breakable: true);
        var world = new DiabloWorldState(objects: [blocker]);
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateLightningSpell()]),
            random: random,
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            characterLevel: 12,
            entityId: 7,
            characterClassId: 2,
            abilitySpellId: 3,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(3, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(3, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);

        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.Empty(world.Projectiles);
        Assert.False(blocker.Destroyed);
        Assert.Equal(0x3F10CD47U, random.State);
    }

    [Fact]
    public void FlashCreatesNativeLayersAtThePlayerAndConsumesIndependentDamageRolls()
    {
        var world = new DiabloWorldState();
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFlashSpell()]),
            random: random,
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            mana: 3000,
            manaMaximum: 3000,
            characterLevel: 12,
            entityId: 7,
            characterClassId: 2,
            memorizedSpells: [new MemorizedSpellState(4, 3)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                4,
                0,
                999,
                999,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(1336, player.Mana);
        var layers = world.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, layers.Length);
        Assert.Equal(AuthoritativeProjectileBehavior.FlashBottom, layers[0].Behavior);
        Assert.Equal(AuthoritativeProjectileBehavior.FlashTop, layers[1].Behavior);
        Assert.Equal((253, 294), (layers[0].Damage, layers[1].Damage));
        Assert.All(layers, layer => {
            Assert.Equal((5, 5), (layer.PositionX, layer.PositionY));
            Assert.Equal((5, 5), (layer.TargetX, layer.TargetY));
            Assert.Equal(19U, layer.RemainingTicks);
            Assert.Equal(AuthoritativeDamageType.Magic, layer.DamageType);
            Assert.Equal(1, layer.AreaRadius);
            Assert.Equal(4, layer.ResistantDamageDivisor);
            Assert.Equal(3, layer.EffectiveSpellLevel);
        });
        Assert.Equal(0xEAC973D6U, random.State);

        var rejected = processor.Execute(
            player,
            new DiabloCastIntent(4, 9, 5, 5, DiabloCastSourceKind.MemorizedSpell),
            2);
        AssertRejected(rejected, DiabloRuleFailure.InvalidTarget);
    }

    [Fact]
    public void FlashUsesValidatedBottomLayerOffsetOrder()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            4,
            5,
            1000,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([
                CreateFlashSpell() with {
                    FlashBottomOffsets = [
                        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0),
                    ],
                },
            ]),
            random: new DiabloRandom(0),
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            entityId: 7,
            characterClassId: 2);
        world.Projectiles.Add(0x80000001, new AuthoritativeProjectile(
            0x80000001,
            player.EntityId,
            0,
            4,
            player.LevelId,
            5,
            5,
            5,
            5,
            100,
            AuthoritativeDamageType.Magic,
            1,
            1) {
            Behavior = AuthoritativeProjectileBehavior.FlashBottom,
            ResistantDamageDivisor = 4,
        });

        processor.AdvanceTick(1, [player]);

        Assert.Equal(target.MaxHitPointsQ6, target.HitPointsQ6);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void FlashLayersRepeatNativeQ6CollisionAcrossTheirDisjointTiles()
    {
        var bottomTarget = new AuthoritativeCombatTarget(
            9,
            4,
            5,
            20,
            magicResistance: 25,
            monsterLevel: 1,
            petrified: true);
        var topTarget = new AuthoritativeCombatTarget(
            10,
            4,
            4,
            20,
            magicResistance: 25,
            monsterLevel: 1,
            petrified: true);
        var world = new DiabloWorldState(combatTargets: [bottomTarget, topTarget]);
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFlashSpell()]),
            random: random,
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            characterLevel: 12,
            entityId: 7,
            characterClassId: 2,
            abilitySpellId: 4,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(4, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(4, 0, 0, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);
        Assert.Equal(2, processor.AdvanceTick(1, [player]).Count);
        Assert.Equal(2, processor.AdvanceTick(2, [player]).Count);

        Assert.Equal(1217, bottomTarget.HitPointsQ6);
        Assert.Equal(1198, topTarget.HitPointsQ6);
        Assert.All(world.Projectiles.Values, layer => Assert.Equal(17U, layer.RemainingTicks));
        Assert.Equal(0x079606CEU, random.State);
    }

    [Fact]
    public void HealingMatchesNativeRogueRollsCapsAndFullLifeConsumption()
    {
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateHealingSpell()]),
            random: random,
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            life: 1000,
            lifeMaximum: 5000,
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            characterClassId: 1,
            characterLevel: 5,
            memorizedSpells: [new MemorizedSpellState(2, 3)]);
        var cast = new DiabloCastIntent(
            2,
            0,
            37,
            -5,
            DiabloCastSourceKind.MemorizedSpell);

        var first = processor.Execute(player, cast, 1);

        Assert.True(first.Succeeded);
        Assert.Equal(4744, player.Life);
        Assert.Equal(2552, player.Mana);
        Assert.Equal(0xCB5679F1U, random.State);
        var firstHealing = Assert.Single(first.Events);
        Assert.Equal(DiabloGameplayEventKind.Healing, firstHealing.Kind);
        Assert.Equal((7U, 7U), (firstHealing.SourceEntityId, firstHealing.TargetEntityId));
        Assert.Equal(58, firstHealing.Amount);
        Assert.Equal(3744, firstHealing.AmountQ6);

        var second = processor.Execute(player, cast, 2);

        Assert.True(second.Succeeded);
        Assert.Equal(5000, player.Life);
        Assert.Equal(2104, player.Mana);
        Assert.Equal(0x2563F5DEU, random.State);
        var cappedHealing = Assert.Single(second.Events);
        Assert.Equal(4, cappedHealing.Amount);
        Assert.Equal(256, cappedHealing.AmountQ6);

        var fullLife = processor.Execute(player, cast, 3);

        Assert.True(fullLife.Succeeded);
        Assert.Empty(fullLife.Events);
        Assert.Equal(5000, player.Life);
        Assert.Equal(1656, player.Mana);
        Assert.Equal(0x8C3C2F4FU, random.State);
    }

    [Fact]
    public void HealOtherMatchesNativeMonkRollsCapsAndFullLifeConsumption()
    {
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateHealOtherSpell()]),
            random: random,
            playerClasses: LoadShippedPlayerClasses());
        var caster = CreatePlayer(
            positionX: 1,
            positionY: 1,
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            characterClassId: 3,
            characterLevel: 5,
            memorizedSpells: [new MemorizedSpellState(34, 3)]);
        var target = CreatePlayer(
            positionX: 10,
            positionY: 10,
            life: 1000,
            lifeMaximum: 10000,
            entityId: 8);
        var cast = new DiabloCastIntent(
            34,
            8,
            0,
            0,
            DiabloCastSourceKind.MemorizedSpell);

        var first = processor.Execute(caster, cast, 1, [caster, target]);

        Assert.True(first.Succeeded);
        Assert.Equal(8488, target.Life);
        Assert.Equal(2552, caster.Mana);
        Assert.Equal(0xCB5679F1U, random.State);
        var healing = Assert.Single(first.Events);
        Assert.Equal(DiabloGameplayEventKind.Healing, healing.Kind);
        Assert.Equal((7U, 8U), (healing.SourceEntityId, healing.TargetEntityId));
        Assert.Equal(117, healing.Amount);
        Assert.Equal(7488, healing.AmountQ6);

        target.Life = target.LifeMaximum;
        var fullLife = processor.Execute(caster, cast, 2, [caster, target]);

        Assert.True(fullLife.Succeeded);
        Assert.Empty(fullLife.Events);
        Assert.Equal(10000, target.Life);
        Assert.Equal(2104, caster.Mana);
        Assert.Equal(0x2563F5DEU, random.State);
    }

    [Fact]
    public void HealOtherRejectsInvalidPlayerTargetsWithoutMutation()
    {
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateHealOtherSpell()]),
            random: random,
            playerClasses: LoadShippedPlayerClasses());
        var caster = CreatePlayer(
            positionX: 0,
            positionY: 0,
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(34, 1)]);
        var deadTarget = CreatePlayer(life: 0, entityId: 8);
        var otherLevelTarget = CreatePlayer(levelId: 1, entityId: 9);
        var distantTarget = CreatePlayer(positionX: 113, entityId: 10);
        var players = new[] { caster, deadTarget, otherLevelTarget, distantTarget };

        foreach (var targetId in new[] { 0U, 7U, 8U, 9U, 10U, 99U }) {
            var result = processor.Execute(
                caster,
                new DiabloCastIntent(
                    34,
                    targetId,
                    0,
                    0,
                    DiabloCastSourceKind.MemorizedSpell),
                1,
                players);
            AssertRejected(result, DiabloRuleFailure.InvalidTarget);
        }

        Assert.Equal(3000, caster.Mana);
        Assert.Equal(0x12345678U, random.State);
    }

    [Fact]
    public void TeleportUsesNativeCrawlOrderAndParticipantOccupancy()
    {
        var blocker = new AuthoritativeCombatTarget(30, 10, 9, 10, 0);
        var world = new DiabloWorldState(
            fallbackBlockedCells: [11 * 40 + 10],
            combatTargets: [blocker],
            objects: [new AuthoritativeWorldObject(40, 1, 0, 9, 10)]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTeleportSpell()]));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 1,
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(23, 3)]);
        var occupyingPlayer = CreatePlayer(
            positionX: 10,
            positionY: 10,
            entityId: 8);
        var deadPlayer = CreatePlayer(
            positionX: 11,
            positionY: 10,
            life: 0,
            entityId: 9);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                23,
                0,
                10,
                10,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [player, occupyingPlayer, deadPlayer]);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Events);
        Assert.Equal(1144, player.Mana);
        Assert.Equal((1, 1), (player.PositionX, player.PositionY));
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Teleport, controller.Behavior);
        Assert.Equal((11, 10, 11, 10, 2U), (
            controller.PositionX,
            controller.PositionY,
            controller.TargetX,
            controller.TargetY,
            controller.RemainingTicks));

        Assert.Empty(processor.AdvanceTick(1, [player, occupyingPlayer, deadPlayer]));
        Assert.Equal((11, 10), (player.PositionX, player.PositionY));
        Assert.Equal(1U, controller.RemainingTicks);
        Assert.Empty(processor.AdvanceTick(2, [player, occupyingPlayer, deadPlayer]));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void TeleportFizzleDoesNotConsumeItsCastSource()
    {
        var world = new DiabloWorldState(fallbackWidth: 1, fallbackHeight: 1);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateTeleportSpell()]));
        var player = CreatePlayer(
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(23, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                23,
                0,
                10,
                10,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(3000, player.Mana);
        Assert.Empty(world.Projectiles);
        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
    }

    [Fact]
    public void PhasingUsesNativeCandidateOrderRngAndTwoTickMovement()
    {
        var world = new DiabloWorldState();
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreatePhasingSpell()]),
            random: random);
        var player = CreatePlayer(
            positionX: 20,
            positionY: 20,
            mana: 10000,
            manaMaximum: 10000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(10, 3)]);
        player.FacingDirection = DiabloPlayerDirection.East;

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                10,
                0,
                -100,
                100,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(9488, player.Mana);
        Assert.Equal(0x955E76D9U, random.State);
        Assert.Equal(DiabloPlayerDirection.East, player.FacingDirection);
        Assert.Equal((20, 20), (player.PositionX, player.PositionY));
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Phasing, controller.Behavior);
        Assert.Equal((24, 15, 24, 15, 2U), (
            controller.PositionX,
            controller.PositionY,
            controller.TargetX,
            controller.TargetY,
            controller.RemainingTicks));

        Assert.Empty(processor.AdvanceTick(1, [player]));
        Assert.Equal((24, 15), (player.PositionX, player.PositionY));
        Assert.Equal(1U, controller.RemainingTicks);
        Assert.Empty(processor.AdvanceTick(2, [player]));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void PhasingWithoutCandidatesConsumesManaWithoutAdvancingRng()
    {
        var world = new DiabloWorldState(fallbackWidth: 1, fallbackHeight: 1);
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreatePhasingSpell()]),
            random: random);
        var player = CreatePlayer(
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(10, 1)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                10,
                0,
                30,
                30,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2232, player.Mana);
        Assert.Equal(0x12345678U, random.State);
        Assert.Empty(world.Projectiles);
        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
    }

    [Fact]
    public void PhasingWithoutCandidatesConsumesTheSelectedScroll()
    {
        var world = new DiabloWorldState(fallbackWidth: 1, fallbackHeight: 1);
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreatePhasingSpell()]),
            random: random);
        var scroll = AuthoritativeItemState.Empty with { MiscId = 21, SpellId = 10 };
        var player = CreatePlayer(
            entityId: 7,
            belt: [new BeltStoreItem(2, 72, scroll)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                10,
                0,
                30,
                30,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 2)),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Belt);
        Assert.Equal(0x12345678U, random.State);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void ResurrectionRevivesAtNativePlacementAndCreatesTimedBeam()
    {
        var world = new DiabloWorldState(
            fallbackBlockedCells: [
                11 * 40 + 10,
                11 * 40 + 11,
                10 * 40 + 12,
                12 * 40 + 10,
                12 * 40 + 11,
                11 * 40 + 12,
            ],
            combatTargets: [new AuthoritativeCombatTarget(30, 10, 10, 10, 0)],
            portals: [new AuthoritativePortal(40, 0, 10, 9, 1, 1, 1)]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateResurrectionSpell()]));
        var caster = CreatePlayer(
            positionX: 1,
            positionY: 1,
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(32, 5)]);
        var target = CreatePlayer(
            positionX: 10,
            positionY: 10,
            life: 0,
            lifeMaximum: 1000,
            mana: 600,
            manaMaximum: 600,
            entityId: 8);
        target.CombatMode = DiabloPlayerCombatMode.Death;
        target.CombatModeEndTick = 50;
        target.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Death,
            0,
            0,
            50,
            50) {
            EffectApplied = true,
        };
        var occupyingPlayer = CreatePlayer(
            positionX: 11,
            positionY: 10,
            entityId: 9);

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                32,
                8,
                0,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target, occupyingPlayer]);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Events);
        Assert.Equal(1720, caster.Mana);
        Assert.Equal((9, 10), (target.PositionX, target.PositionY));
        Assert.Equal(640, target.Life);
        Assert.Equal(0, target.Mana);
        Assert.Equal(DiabloPlayerCombatMode.Stand, target.CombatMode);
        Assert.Equal(0UL, target.CombatModeEndTick);
        Assert.Null(target.CurrentAction);
        var beam = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.ResurrectionBeam, beam.Behavior);
        Assert.Equal((7U, 8U, 10, 10, 16U), (
            beam.SourceEntityId,
            beam.TargetEntityId,
            beam.PositionX,
            beam.PositionY,
            beam.RemainingTicks));

        for (ulong tick = 1; tick < 16; tick++)
            Assert.Empty(processor.AdvanceTick(tick, [caster, target, occupyingPlayer]));
        Assert.Equal(1U, beam.RemainingTicks);
        Assert.Empty(processor.AdvanceTick(16, [caster, target, occupyingPlayer]));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void ResurrectionUsesValidatedInitialPlacementOrder()
    {
        var world = new DiabloWorldState(
            combatTargets: [new AuthoritativeCombatTarget(30, 10, 10, 10, 0)]);
        var spell = CreateResurrectionSpell() with {
            ResurrectionInitialOffsets = [
                (2, 2),
                (0, 0),
                (1, 0),
                (0, 1),
                (1, 1),
                (2, 0),
                (0, 2),
                (2, 1),
            ],
        };
        var processor = CreateProcessor(world, new AuthoritativeSpellCatalog([spell]));
        var caster = CreatePlayer(
            positionX: 1,
            positionY: 1,
            mana: 3000,
            manaMaximum: 3000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(32, 5)]);
        var target = CreatePlayer(
            positionX: 10,
            positionY: 10,
            life: 0,
            lifeMaximum: 1000,
            mana: 600,
            manaMaximum: 600,
            entityId: 8);
        target.CombatMode = DiabloPlayerCombatMode.Death;

        var result = processor.Execute(
            caster,
            new DiabloCastIntent(
                32,
                8,
                0,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, target]);

        Assert.True(result.Succeeded);
        Assert.Equal((12, 12), (target.PositionX, target.PositionY));
    }

    [Fact]
    public void ResurrectionBeamsLivingTargetsButRejectsInvalidPlayerTargets()
    {
        var world = new DiabloWorldState();
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateResurrectionSpell()]));
        var caster = CreatePlayer(
            mana: 6000,
            manaMaximum: 6000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(32, 1)]);
        var livingTarget = CreatePlayer(
            positionX: 2,
            positionY: 2,
            life: 500,
            lifeMaximum: 1000,
            mana: 300,
            manaMaximum: 600,
            entityId: 8);
        var otherLevelTarget = CreatePlayer(
            levelId: 1,
            life: 0,
            entityId: 9);

        var living = processor.Execute(
            caster,
            new DiabloCastIntent(
                32,
                8,
                0,
                0,
                DiabloCastSourceKind.MemorizedSpell),
            1,
            [caster, livingTarget, otherLevelTarget]);

        Assert.True(living.Succeeded);
        Assert.Equal(4720, caster.Mana);
        Assert.Equal((500, 300), (livingTarget.Life, livingTarget.Mana));
        Assert.Single(world.Projectiles);

        foreach (var invalidTargetId in new[] { 0U, 7U, 9U, 99U }) {
            var rejected = processor.Execute(
                caster,
                new DiabloCastIntent(
                    32,
                    invalidTargetId,
                    0,
                    0,
                    DiabloCastSourceKind.MemorizedSpell),
                2,
                [caster, livingTarget, otherLevelTarget]);
            AssertRejected(rejected, DiabloRuleFailure.InvalidTarget);
        }
        Assert.Equal(4720, caster.Mana);
        Assert.Single(world.Projectiles);
    }

    [Fact]
    public void FlashBreaksObjectsButPersistsAcrossBreakableAndStaticBlockers()
    {
        var breakable = new AuthoritativeWorldObject(20, 1, 0, 4, 5, breakable: true);
        var staticBlocker = new AuthoritativeWorldObject(21, 1, 0, 5, 4);
        var world = new DiabloWorldState(objects: [breakable, staticBlocker]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFlashSpell()]),
            random: new DiabloRandom(0x12345678));
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            characterLevel: 12,
            entityId: 7,
            abilitySpellId: 4,
            memorizedSpells: [new MemorizedSpellState(4, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(4, 0, 0, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);
        Assert.Empty(processor.AdvanceTick(1, [player]));

        Assert.True(breakable.Destroyed);
        Assert.False(staticBlocker.Destroyed);
        Assert.Equal(2, world.Projectiles.Count);
        Assert.All(world.Projectiles.Values, layer => Assert.Equal(18U, layer.RemainingTicks));
    }

    [Fact]
    public void FlashProtectionBeginsAfterItsFirstMissileTickAndEndsWithItsLayers()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            6,
            5,
            1000,
            attackDamage: 10,
            aggroRange: 1,
            monsterLevel: 1);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFlashSpell()]),
            random: new DiabloRandom(0x12345678),
            playerClasses: LoadShippedPlayerClasses());
        var player = CreatePlayer(
            positionX: 5,
            positionY: 5,
            life: 100,
            lifeMaximum: 100,
            characterLevel: 12,
            entityId: 7,
            characterClassId: 2,
            abilitySpellId: 4,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(4, 3)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(4, 0, 0, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);
        _ = processor.AdvanceTick(1, [player]);
        Assert.Equal(90, player.Life);
        for (ulong tick = 2; tick <= 19; tick++)
            _ = processor.AdvanceTick(tick, [player]);
        Assert.Equal(90, player.Life);
        Assert.Empty(world.Projectiles);

        _ = processor.AdvanceTick(20, [player]);
        Assert.Equal(80, player.Life);
    }

    [Fact]
    public void ActiveFlashProtectionRejectsLaterPvpProjectileCollisionWithoutRng()
    {
        var world = new DiabloWorldState();
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateFlashSpell()]),
            random: random,
            playerClasses: LoadShippedPlayerClasses());
        var victim = CreatePlayer(
            positionX: 5,
            positionY: 5,
            life: 640,
            lifeMaximum: 640,
            characterLevel: 12,
            entityId: 7,
            characterClassId: 2,
            abilitySpellId: 4,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            memorizedSpells: [new MemorizedSpellState(4, 3)]);
        var attacker = CreatePlayer(
            positionX: 10,
            positionY: 10,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });

        Assert.True(processor.Execute(
            victim,
            new DiabloCastIntent(4, 0, 0, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);
        world.Projectiles.Add(0x80000002, new AuthoritativeProjectile(
            0x80000002,
            attacker.EntityId,
            0,
            3,
            0,
            5,
            5,
            0,
            0,
            128,
            AuthoritativeDamageType.Lightning,
            0,
            6) {
            Behavior = AuthoritativeProjectileBehavior.LightningSegment,
            StartX = 0,
            StartY = 0,
            ResistantDamageDivisor = 4,
        });

        Assert.Empty(processor.AdvanceTick(1, [victim, attacker]));
        Assert.Equal(640, victim.Life);
        Assert.Equal(0xEAC973D6U, random.State);
    }

    [Fact]
    public void ChargedBoltWandersHitsAndConsumesNativeCollisionRng()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            10,
            lightningResistance: 25,
            monsterLevel: 1);
        var world = new DiabloWorldState(combatTargets: [target]);
        var spell = CreateChargedBoltSpell() with {
            ProjectileCount = 1,
            ProjectileCountLevelDivisor = 0,
        };
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]),
            random: random);
        var player = CreatePlayer(
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            },
            abilitySpellId: 30);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(30, 0, 4, 0, DiabloCastSourceKind.Ability),
            1).Succeeded);
        IReadOnlyList<DiabloDirectedGameplayEvent> events = [];
        for (ulong tick = 1; tick <= 4; tick++)
            events = processor.AdvanceTick(tick, [player]);

        var projectile = Assert.Single(world.Projectiles.Values);
        Assert.Equal((1, 1), (projectile.PositionX, projectile.PositionY));
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(8U, projectile.RemainingTicks);
        Assert.Equal(9, target.HitPoints);
        Assert.Equal(528, target.HitPointsQ6);
        var damageEvent = Assert.Single(events).Event;
        Assert.Equal(1, damageEvent.Amount);
        Assert.Equal(112, damageEvent.AmountQ6);
        Assert.Equal(0x49BFD245U, random.State);
    }

    [Fact]
    public void ChargedBoltStopsAtMissileBlockingGeometryWithoutEnteringImpact()
    {
        var projectile = CreateTravelingChargedBolt(velocityY: 1 << 20);
        var world = new DiabloWorldState(
            fallbackWidth: 3,
            fallbackHeight: 3,
            projectiles: [projectile],
            fallbackMissileBlockedCells: [4]);
        var processor = CreateProcessor(world);

        var events = processor.AdvanceTick(1, [CreatePlayer(entityId: 7)]);

        Assert.Empty(events);
        Assert.Empty(world.Projectiles);
        Assert.Equal(AuthoritativeProjectilePhase.Travel, projectile.Phase);
    }

    [Fact]
    public void ChargedBoltPassesThroughMissilePassableObject()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            1,
            missilePassable: true);
        var projectile = CreateTravelingChargedBolt(velocityY: 1 << 20);
        var world = new DiabloWorldState(objects: [@object], projectiles: [projectile]);
        var processor = CreateProcessor(world);

        processor.AdvanceTick(1, [CreatePlayer(entityId: 7)]);

        Assert.Same(projectile, Assert.Single(world.Projectiles.Values));
        Assert.Equal((1, 1), (projectile.PositionX, projectile.PositionY));
        Assert.False(@object.Destroyed);
    }

    [Fact]
    public void ChargedBoltBreaksBlockingObjectAndIsRemovedWithoutImpact()
    {
        var @object = new AuthoritativeWorldObject(
            20,
            4,
            0,
            1,
            1,
            breakable: true);
        var projectile = CreateTravelingChargedBolt(velocityY: 1 << 20);
        var world = new DiabloWorldState(objects: [@object], projectiles: [projectile]);
        var processor = CreateProcessor(world);

        var events = processor.AdvanceTick(1, [CreatePlayer(entityId: 7)]);

        Assert.Empty(events);
        Assert.True(@object.Destroyed);
        Assert.Empty(world.Projectiles);
        Assert.Equal(AuthoritativeProjectilePhase.Travel, projectile.Phase);
    }

    [Fact]
    public void ChargedBoltChecksSweptTilesAndPersistsIntermediateImpactPosition()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 1, 10);
        var projectile = CreateTravelingChargedBolt(velocityY: 3 << 20);
        var world = new DiabloWorldState(
            combatTargets: [target],
            projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });

        var events = processor.AdvanceTick(1, [player]);

        Assert.Equal(9, target.HitPoints);
        Assert.Single(events);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal((1, 1), (projectile.PositionX, projectile.PositionY));
        Assert.Equal((0, 2 << 20), (projectile.TraveledX, projectile.TraveledY));
        Assert.Equal(9U, projectile.LastCollisionTargetEntityId);
    }

    [Fact]
    public void StationaryChargedBoltDoesNotRerollAgainstTheSameOccupant()
    {
        var target = new AuthoritativeCombatTarget(9, 0, 0, 10);
        var projectile = CreateTravelingChargedBolt(velocityY: 1, pathTicks: 10);
        var world = new DiabloWorldState(
            combatTargets: [target],
            projectiles: [projectile]);
        var random = new DiabloRandom(2);
        var processor = CreateProcessor(world, random: random);
        var player = CreatePlayer(entityId: 7);

        Assert.Empty(processor.AdvanceTick(1, [player]));
        var randomStateAfterMiss = random.State;
        Assert.Equal(9U, projectile.LastCollisionTargetEntityId);

        Assert.Empty(processor.AdvanceTick(2, [player]));

        Assert.Equal(10, target.HitPoints);
        Assert.Equal(randomStateAfterMiss, random.State);
        Assert.Equal(9U, projectile.LastCollisionTargetEntityId);
    }

    [Fact]
    public void ResistantOnePointChargedBoltPreservesQuarterPointDamage()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            1,
            10,
            lightningResistance: 25,
            monsterLevel: 1);
        var projectile = new AuthoritativeProjectile(
            0x80000000,
            7,
            0,
            30,
            0,
            0,
            0,
            4,
            0,
            1,
            AuthoritativeDamageType.Lightning,
            0,
            256) {
            Behavior = AuthoritativeProjectileBehavior.ChargedBolt,
            OriginalDirection = 7,
            PathIndex = 13,
            Speed = 8,
            ImpactLifetime = 8,
            ResistantDamageDivisor = 4,
        };
        var world = new DiabloWorldState(
            combatTargets: [target],
            projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(35, 35),
            });

        IReadOnlyList<DiabloDirectedGameplayEvent> events = [];
        for (ulong tick = 1; tick <= 4; tick++)
            events = processor.AdvanceTick(tick, [player]);

        Assert.Equal(10, target.HitPoints);
        Assert.Equal(624, target.HitPointsQ6);
        var damageEvent = Assert.Single(events).Event;
        Assert.Equal(0, damageEvent.Amount);
        Assert.Equal(16, damageEvent.AmountQ6);
    }

    [Fact]
    public void ChargedBoltDamagesAnotherPlayerInNativeQ6AndDirectsTheEventToTheVictim()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(world, random: random);
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8);

        var deliveries = processor.AdvanceTick(1, [owner, victim]);

        Assert.Equal(576, victim.Life);
        Assert.Same(victim, Assert.Single(deliveries).Recipient);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal((7U, 8U), (damage.SourceEntityId, damage.TargetEntityId));
        Assert.Equal(1, damage.Amount);
        Assert.Equal(64, damage.AmountQ6);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(8U, projectile.LastCollisionTargetEntityId);
    }

    [Fact]
    public void LethalPlayerProjectileDoesNotDropVictimEquipment()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var equipped = new EquippedStoreItem(
            0,
            99,
            AuthoritativeItemState.Empty with { ItemType = 1, StatFlag = true });
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 64,
            lifeMaximum: 64,
            entityId: 8,
            equipment: [equipped]);

        Assert.Single(processor.AdvanceTick(1, [owner, victim]));

        Assert.Equal(0, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Death, victim.CombatMode);
        Assert.Equal(equipped, Assert.Single(victim.Equipment));
        Assert.DoesNotContain(world.WorldItems.Values, item => item.ItemSeed == equipped.ItemSeed);
    }

    [Fact]
    public void LethalPlayerProjectileCreatesTheNativePvpEarFromExternalContent()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var random = new DiabloRandom(0);
        var classes = LoadShippedPlayerClasses();
        var processor = CreateProcessor(
            world,
            random: random,
            playerClasses: classes,
            gameMode: DiabloGameMode.Multiplayer,
            items: LoadShippedItems());
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var equipped = new EquippedStoreItem(
            0,
            99,
            AuthoritativeItemState.Empty with { ItemType = 1, StatFlag = true });
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 64,
            lifeMaximum: 64,
            entityId: 8,
            equipment: [equipped],
            characterClassId: 1,
            characterLevel: 3,
            characterName: "Dead-RogueDM",
            gold: 0);
        var expectedRandom = new DiabloRandom(0);
        _ = expectedRandom.RandomIntLessThan(100);
        _ = expectedRandom.RandomIntBetween(2, 2);

        Assert.Single(processor.AdvanceTick(1, [owner, victim]));

        Assert.Equal(0, victim.Life);
        Assert.Equal(equipped, Assert.Single(victim.Equipment));
        var ear = Assert.Single(world.WorldItems.Values);
        Assert.Equal(1633955154U, ear.ItemSeed);
        Assert.Equal(0U, ear.Price);
        Assert.Equal(17509U, ear.State.CreateInfo);
        Assert.Equal(23, ear.State.ItemIndex);
        Assert.Equal(44, ear.State.MiscId);
        Assert.Equal(3, ear.State.Value);
        Assert.Equal("Ear of Dead-RogueDM", ear.State.BaseName);
        Assert.Equal("Dead-RogueDM", ear.State.IdentifiedName);
        Assert.Equal(21, ear.State.NativeCursorGraphic);
        Assert.Equal(expectedRandom.State, random.State);
    }

    [Fact]
    public void DisabledFriendlyFireSkipsPlayerCollisionWithoutConsumingRng()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var random = new DiabloRandom(0x12345678);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: false),
            random: random);
        var owner = CreatePlayer(entityId: 7);
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8);

        var deliveries = processor.AdvanceTick(1, [owner, victim]);

        Assert.Empty(deliveries);
        Assert.Equal(640, victim.Life);
        Assert.Equal(0x12345678U, random.State);
        Assert.Equal(AuthoritativeProjectilePhase.Travel, projectile.Phase);
        Assert.Equal(8U, projectile.LastCollisionTargetEntityId);
    }

    [Fact]
    public void HostileCasterOverridesDisabledFriendlyFireGate()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var processor = CreateProcessor(
            world,
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                friendlyFire: false),
            random: new DiabloRandom(0));
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        owner.FriendlyMode = false;
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8);

        Assert.Single(processor.AdvanceTick(1, [owner, victim]));
        Assert.Equal(576, victim.Life);
    }

    [Fact]
    public void PlayerBlockConsumesDamageRollAndStopsChargedBoltDamage()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var random = new DiabloRandom(0);
        var processor = CreateProcessor(world, random: random);
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        var deliveries = processor.AdvanceTick(1, [owner, victim]);

        Assert.Empty(deliveries);
        Assert.Equal(640, victim.Life);
        Assert.Equal(0x8082A52FU, random.State);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(DiabloPlayerCombatMode.Block, victim.CombatMode);
        Assert.Equal(5UL, victim.CombatModeEndTick);
    }

    [Fact]
    public void OnlyStandAndMeleeAttackModesCanBlockPlayerMissiles()
    {
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var rangedVictim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);
        rangedVictim.CombatMode = DiabloPlayerCombatMode.RangedAttack;
        rangedVictim.CombatModeEndTick = 10;
        var rangedRandom = new DiabloRandom(0);
        var rangedProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: rangedRandom);

        Assert.Single(rangedProcessor.AdvanceTick(1, [owner, rangedVictim]));
        Assert.Equal(576, rangedVictim.Life);

        var expectedRangedRandom = new DiabloRandom(0);
        _ = expectedRangedRandom.RandomIntLessThan(100);
        _ = expectedRangedRandom.RandomIntBetween(2, 2);
        Assert.Equal(expectedRangedRandom.State, rangedRandom.State);

        var attackVictim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 9,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 100, shield)]);
        attackVictim.CombatMode = DiabloPlayerCombatMode.Attack;
        attackVictim.CombatModeEndTick = 10;
        var attackProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: new DiabloRandom(0));

        Assert.Empty(attackProcessor.AdvanceTick(1, [owner, attackVictim]));
        Assert.Equal(640, attackVictim.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, attackVictim.CombatMode);
    }

    [Fact]
    public void FirstSameTickBlockMakesTheVictimIneligibleForTheNextBolt()
    {
        var first = CreateStationaryChargedBolt(1, 1, 0x80000000);
        var second = CreateStationaryChargedBolt(1, 1, 0x80000001);
        var processor = CreateProcessor(
            new DiabloWorldState(projectiles: [second, first]),
            random: new DiabloRandom(0));
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);

        var deliveries = processor.AdvanceTick(1, [owner, victim]);

        Assert.Single(deliveries);
        Assert.Equal(576, victim.Life);
        Assert.Equal(DiabloPlayerCombatMode.GotHit, victim.CombatMode);
        Assert.Equal(7UL, victim.CombatModeEndTick);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, first.Phase);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, second.Phase);
    }

    [Fact]
    public void HellfireBlockConsumesTheNativeRotationSelectionDraw()
    {
        var diabloRandom = new DiabloRandom(0);
        var hellfireRandom = new DiabloRandom(0);
        var diabloVictim = CreateBlockingVictim();
        var hellfireVictim = CreateBlockingVictim();
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var diablo = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Diablo),
            random: diabloRandom);
        var hellfire = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire),
            random: hellfireRandom);

        Assert.Empty(diablo.AdvanceTick(1, [owner, diabloVictim]));
        Assert.Empty(hellfire.AdvanceTick(1, [owner, hellfireVictim]));

        var expected = new DiabloRandom(diabloRandom.State);
        _ = expected.RandomIntLessThan(2);
        Assert.Equal(expected.State, hellfireRandom.State);
    }

    [Fact]
    public void TimedCombatModeExpiresAtItsFirstInactiveTick()
    {
        var player = CreatePlayer();
        player.CombatMode = DiabloPlayerCombatMode.Block;
        player.CombatModeEndTick = 5;

        DiabloCombatCommandProcessor.AdvanceCombatMode(player, 4);
        Assert.Equal(DiabloPlayerCombatMode.Block, player.CombatMode);

        DiabloCombatCommandProcessor.AdvanceCombatMode(player, 5);
        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        Assert.Equal(0UL, player.CombatModeEndTick);
    }

    [Fact]
    public void BlockRecoveryUsesNativeParryDurabilityRoll()
    {
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            Durability = 4,
            MaxDurability = 4,
            StatFlag = true,
        };
        var player = CreatePlayer(
            equipment: [new EquippedStoreItem(5, 42, shield)]);
        player.CombatMode = DiabloPlayerCombatMode.Block;
        player.CombatModeEndTick = 1;
        var processor = CreateProcessor(
            new DiabloWorldState(),
            random: new DiabloRandom(0));

        processor.AdvanceTick(1, [player]);

        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        Assert.Equal(3, Assert.Single(player.Equipment).State.Durability);
    }

    [Fact]
    public void HitRecoveryUsesNativeArmorDurabilityRoll()
    {
        var helmet = AuthoritativeItemState.Empty with {
            ItemType = 6,
            Durability = 4,
            MaxDurability = 4,
            StatFlag = true,
        };
        var player = CreatePlayer(
            equipment: [new EquippedStoreItem(0, 42, helmet)]);
        player.CombatMode = DiabloPlayerCombatMode.GotHit;
        player.CombatModeEndTick = 1;
        var processor = CreateProcessor(
            new DiabloWorldState(),
            random: new DiabloRandom(1));

        processor.AdvanceTick(1, [player]);

        Assert.Equal(DiabloPlayerCombatMode.Stand, player.CombatMode);
        Assert.Equal(3, Assert.Single(player.Equipment).State.Durability);
    }

    [Fact]
    public void ClassFramesAndUsableSpeedFlagsControlNativeModeDurations()
    {
        var classes = LoadShippedPlayerClasses();
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        var rogue = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)],
            characterClassId: 1);
        var rogueProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: new DiabloRandom(0),
            playerClasses: classes);

        Assert.Empty(rogueProcessor.AdvanceTick(1, [owner, rogue]));
        Assert.Equal(11UL, rogue.CombatModeEndTick);

        var fastShield = shield with { Flags = 1U << 24 };
        var fastRogue = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 9,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 100, fastShield)],
            characterClassId: 1);
        var fastProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: new DiabloRandom(0),
            playerClasses: classes);

        Assert.Empty(fastProcessor.AdvanceTick(1, [owner, fastRogue]));
        Assert.Equal(5UL, fastRogue.CombatModeEndTick);

        var tunedFastRogue = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 13,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 100, fastShield)],
            characterClassId: 1);
        var tunedFastProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                equipmentFastBlockFrameSkip: 3),
            random: new DiabloRandom(0),
            playerClasses: classes);

        Assert.Empty(tunedFastProcessor.AdvanceTick(1, [owner, tunedFastRogue]));
        Assert.Equal(fastRogue.CombatModeEndTick + 3, tunedFastRogue.CombatModeEndTick);

        var staff = AuthoritativeItemState.Empty with {
            ItemIndex = 101,
            ItemType = 1,
            StatFlag = true,
        };
        var monk = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 10,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(4, 101, staff)],
            characterClassId: 3);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\n"
                + "7\t10\t1\t1\tStaff\t101\n");
        var monkProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: new DiabloRandom(0),
            playerClasses: classes,
            items: items);

        Assert.Empty(monkProcessor.AdvanceTick(1, [owner, monk]));
        Assert.Equal(5UL, monk.CombatModeEndTick);

        var tunedClasses = new AuthoritativePlayerClassCatalog(
            classes.Definitions.Select(definition => definition.ClassId == 3
                ? definition with { FastBlockFrameSkip = 3 }
                : definition));
        var tunedMonk = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 12,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(4, 101, staff)],
            characterClassId: 3);
        var tunedMonkProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: new DiabloRandom(0),
            playerClasses: tunedClasses,
            items: items);

        Assert.Empty(tunedMonkProcessor.AdvanceTick(1, [owner, tunedMonk]));
        Assert.Equal(monk.CombatModeEndTick + 3, tunedMonk.CombatModeEndTick);

        var recoveryItem = AuthoritativeItemState.Empty with {
            StatFlag = true,
            Flags = 1U << 23,
        };
        var recoveringRogue = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 11,
            equipment: [new EquippedStoreItem(0, 102, recoveryItem)],
            characterClassId: 1);
        var recoveryProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: new DiabloRandom(0),
            playerClasses: classes);

        Assert.Single(recoveryProcessor.AdvanceTick(1, [owner, recoveringRogue]));
        Assert.Equal(DiabloPlayerCombatMode.GotHit, recoveringRogue.CombatMode);
        Assert.Equal(5UL, recoveringRogue.CombatModeEndTick);

        var tunedRecoveringRogue = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 14,
            equipment: [new EquippedStoreItem(0, 102, recoveryItem)],
            characterClassId: 1);
        var tunedRecoveryProcessor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                fastestHitRecoveryFrameSkip: 4),
            random: new DiabloRandom(0),
            playerClasses: classes);

        Assert.Single(tunedRecoveryProcessor.AdvanceTick(1, [owner, tunedRecoveringRogue]));
        Assert.Equal(DiabloPlayerCombatMode.GotHit, tunedRecoveringRogue.CombatMode);
        Assert.Equal(recoveringRogue.CombatModeEndTick - 1, tunedRecoveringRogue.CombatModeEndTick);
    }

    [Fact]
    public void SemanticWeaponTagsEnableMonkFlexibleBlockingWhenLegacyClassConflicts()
    {
        var classes = LoadShippedPlayerClasses();
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 99, 1, 1, 0, 0, 0, 0, 0, 1, 1, true) {
                ItemClass = 0,
                GenerationTags = new HashSet<string>(["Weapon", "Sword"], StringComparer.OrdinalIgnoreCase),
                NativeItemIndex = 101,
                EquipLocation = 1,
            },
        ]);
        var processor = CreateProcessor(
            new DiabloWorldState(projectiles: [CreateStationaryChargedBolt(1, 1)]),
            random: new DiabloRandom(0),
            playerClasses: classes,
            items: items);
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var sword = AuthoritativeItemState.Empty with {
            ItemIndex = 101,
            ItemType = 99,
            ItemClass = 0,
            EquipLocation = 1,
            StatFlag = true,
        };
        var monk = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(4, 101, sword)],
            characterClassId: 3);

        Assert.Empty(processor.AdvanceTick(1, [owner, monk]));
        Assert.Equal(640, monk.Life);
        Assert.Equal(DiabloPlayerCombatMode.Block, monk.CombatMode);
    }

    [Fact]
    public void LightningResistanceOverridesBlockAndUsesNativePercentageDamage()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var resistantShield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
            PlusLightningResistance = 25,
        };
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, resistantShield)]);

        var damage = Assert.Single(processor.AdvanceTick(1, [owner, victim])).Event;

        Assert.Equal(592, victim.Life);
        Assert.Equal(0, damage.Amount);
        Assert.Equal(48, damage.AmountQ6);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, projectile.Phase);
        Assert.Equal(DiabloPlayerCombatMode.Stand, victim.CombatMode);
        Assert.Equal(0UL, victim.CombatModeEndTick);
    }

    [Fact]
    public void ZeroResistanceEquipmentResetsLightningResistanceBeforeDamage()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var processor = CreateProcessor(world, random: new DiabloRandom(0));
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var zeroResistanceShield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
            PlusLightningResistance = 75,
            Flags = AuthoritativeEquipmentResistanceRules.ZeroResistanceFlag,
        };
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            equipment: [new EquippedStoreItem(5, 99, zeroResistanceShield)]);

        var damage = Assert.Single(processor.AdvanceTick(1, [owner, victim])).Event;

        Assert.Equal(576, victim.Life);
        Assert.Equal(1, damage.Amount);
        Assert.Equal(64, damage.AmountQ6);
    }

    [Fact]
    public void LightningResistanceUsesConfiguredMaximumResistance()
    {
        var projectile = CreateStationaryChargedBolt(1, 1);
        var world = new DiabloWorldState(projectiles: [projectile]);
        var processor = CreateProcessor(
            world,
            random: new DiabloRandom(0),
            rules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                maximumResistance: 10));
        var owner = CreatePlayer(
            entityId: 7,
            attributes: PlayerAttributesState.Zero with {
                Magic = new PlayerAttributeState(100, 100),
            });
        var resistantShield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
            PlusLightningResistance = 25,
        };
        var victim = CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            equipment: [new EquippedStoreItem(5, 99, resistantShield)]);

        var damage = Assert.Single(processor.AdvanceTick(1, [owner, victim])).Event;

        Assert.Equal(582, victim.Life);
        Assert.Equal(58, damage.AmountQ6);
    }

    [Fact]
    public void ExhaustedOrNonLeftHandChargedItemIsRejectedWithoutMutation()
    {
        var staffState = AuthoritativeItemState.Empty with {
            MiscId = 23,
            SpellId = 1,
            Charges = 0,
            MaxCharges = 2,
        };
        var processor = CreateProcessor(new DiabloWorldState());
        var player = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            equipment: [
                new EquippedStoreItem(4, 77, staffState),
                new EquippedStoreItem(5, 78, staffState with { Charges = 2 }),
            ]);

        var exhausted = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                0,
                0,
                DiabloCastSourceKind.ChargedItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 4)),
            1);
        var wrongHand = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                0,
                0,
                DiabloCastSourceKind.ChargedItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 5)),
            2);

        AssertRejected(exhausted, DiabloRuleFailure.InsufficientResources);
        AssertRejected(wrongHand, DiabloRuleFailure.NotAllowed);
        Assert.Equal([0, 2], player.Equipment.OrderBy(item => item.Slot).Select(item => item.State.Charges));
    }

    [Fact]
    public void ConsumableCastRemovesExactInventoryScrollAndCompactsGrid()
    {
        var scroll = AuthoritativeItemState.Empty with { MiscId = 21, SpellId = 1 };
        var other = AuthoritativeItemState.Empty with { ItemType = 1 };
        var processor = CreateProcessor(new DiabloWorldState());
        var player = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            inventory: [
                new OwnedStoreItem(0, 0, 71, 10, 0, other),
                new OwnedStoreItem(0, 1, 72, 10, 0, scroll),
            ],
            inventoryGrid: [0, 1, 1, -1]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                0,
                0,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(71U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal([0, -1, -1, -1], player.InventoryGrid);
    }

    [Fact]
    public void OpenStashConsumableCastRemovesExactAuthoritativeSource()
    {
        var scroll = AuthoritativeItemState.Empty with {
            StableItemId = 800,
            MiscId = 21,
            SpellId = 1,
        };
        var processor = CreateProcessor(new DiabloWorldState());
        var player = CreatePlayer(life: 10, lifeMaximum: 20);
        player.Stash.Add(new StashStoreItem(
            0,
            7,
            0,
            0,
            801,
            10,
            0,
            scroll));
        player.StashOpen = true;

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                0,
                0,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    7,
                    800,
                    0)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Stash);
    }

    [Fact]
    public void UnspecifiedCastInfersOpenStashConsumableSource()
    {
        var player = CreatePlayer(life: 10, lifeMaximum: 20);
        player.Stash.Add(new StashStoreItem(
            0,
            9,
            0,
            0,
            804,
            10,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 805,
                MiscId = 21,
                SpellId = 1,
            }));
        player.StashOpen = true;
        var processor = CreateProcessor(new DiabloWorldState());

        var result = processor.Execute(
            player,
            new DiabloCastIntent(1, 0, 0, 0),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Stash);
    }

    [Fact]
    public void MultiplayerMonsterTicksAdvanceNativeAiSeedWithoutChangingItemSeed()
    {
        const uint itemSeed = 0x12345678;
        const uint aiSeed = 0x87654321;
        var multiplayerTarget = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            10,
            monsterItemSeed: itemSeed,
            monsterAiSeed: aiSeed);
        var multiplayerRandom = new DiabloRandom(0xCAFEBABE);
        var multiplayer = CreateProcessor(
            new DiabloWorldState(combatTargets: [multiplayerTarget]),
            random: multiplayerRandom,
            gameMode: DiabloGameMode.Multiplayer);
        var expectedAiRandom = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expectedAiRandom.AdvanceRndSeed());

        multiplayer.AdvanceTick(1, [CreatePlayer()]);

        Assert.Equal(itemSeed, multiplayerTarget.MonsterItemSeed);
        Assert.Equal(expectedAiSeed, multiplayerTarget.MonsterAiSeed);
        Assert.Equal(expectedAiRandom.State, multiplayerRandom.State);

        var singlePlayerTarget = new AuthoritativeCombatTarget(
            9,
            0,
            0,
            10,
            monsterItemSeed: itemSeed,
            monsterAiSeed: aiSeed);
        var singlePlayer = CreateProcessor(
            new DiabloWorldState(combatTargets: [singlePlayerTarget]));

        singlePlayer.AdvanceTick(1, [CreatePlayer()]);

        Assert.Equal(aiSeed, singlePlayerTarget.MonsterAiSeed);
    }

    [Fact]
    public void MultiplayerZombieAiConsumesItsNativeMonsterRandomStream()
    {
        const uint itemSeed = 1;
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            100,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "Zombie",
            monsterItemSeed: itemSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntLessThan(8);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 11,
            positionY: 5,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((4, 4), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void MultiplayerSkeletonMeleeMovementConsumesItsNativeMonsterRandomStream()
    {
        const uint aiSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            100,
            attackDamage: 5,
            aggroRange: 8,
            nativeAi: "SkeletonMelee",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var actionRoll = expected.RandomIntLessThan(100);
        Assert.True(actionRoll >= 35);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 5,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((6, 5), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void SinglePlayerInitializedSkeletonMeleeAdjacentDelayConsumesNativeRandomStream()
    {
        const uint initSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "SkeletonMelee",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.True(attackRoll >= 20);
        var delayRoll = expected.RandomIntLessThan(10);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(10 + delayRoll, monster.SkeletonMeleeDelayTicks);
    }

    [Fact]
    public void MultiplayerSkeletonMeleeAdjacentDelayConsumesItsNativeMonsterRandomStream()
    {
        const uint aiSeed = 1;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "SkeletonMelee",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.True(attackRoll >= 20);
        var delayRoll = expected.RandomIntLessThan(10);

        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);
        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(10 + delayRoll, monster.SkeletonMeleeDelayTicks);
    }

    [Fact]
    public void MultiplayerSkeletonRangedAiConsumesItsNativeMovementAndAttackRolls()
    {
        const uint aiSeed = 9;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 3,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var movementRoll = expected.RandomIntLessThan(100);
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.Equal(73, movementRoll);
        Assert.True(attackRoll < 9);

        var player = CreatePlayer(
            entityId: 7,
            positionX: 7,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);
        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.False(monster.IsMoving);
        Assert.Single(world.Projectiles.Values);
    }

    [Fact]
    public void MultiplayerSkeletonRangedRetreatConsumesOnlyItsNativeMovementRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            20,
            attackDamage: 2,
            aggroRange: 8,
            minDamage: 2,
            maxDamage: 9,
            toHit: 35,
            nativeAi: "SkeletonRanged",
            intelligence: 0,
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer,
            monsterMissiles: CreateArrowMissileCatalog("SkeletonRanged"));
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var movementRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, movementRoll);
        Assert.True(movementRoll < 13);

        var player = CreatePlayer(
            entityId: 7,
            positionX: 5,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);
        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void MultiplayerFatAiConsumesItsNativeMovementAndRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "Fat",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var movementRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, movementRoll);
        Assert.True(movementRoll < 20);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void SinglePlayerInitializedFatConsumesNativeAdjacentAttackRoll()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Fat",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.Equal(24, attackRoll);
        Assert.True(attackRoll >= 20);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.Equal(expected.State, random.State);
    }

    [Fact]
    public void MultiplayerFatAiConsumesItsNativeAdjacentAttackRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Fat",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, attackRoll);
        Assert.True(attackRoll < 20);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntBetween(5, 5);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Single(deliveries);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void SinglePlayerInitializedGoatMeleeConsumesNativeAdjacentAttackRoll()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "GoatMelee",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.Equal(24, attackRoll);
        Assert.True(attackRoll >= 23);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Empty(deliveries);
        Assert.Equal(100, player.Life);
        Assert.Equal(expected.State, random.State);
    }

    [Fact]
    public void MultiplayerGoatMeleeAiConsumesItsNativeMovementAndRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 2,
            aggroRange: 5,
            nativeAi: "GoatMelee",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var movementRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, movementRoll);
        Assert.True(movementRoll < 28);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void MultiplayerGoatMeleeAiConsumesItsNativeMoveGoalRolls()
    {
        const uint aiSeed = 123;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            40,
            attackDamage: 2,
            aggroRange: 7,
            nativeAi: "GoatMelee",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var actionRoll = expected.RandomIntLessThan(100);
        Assert.Equal(88, actionRoll);
        Assert.True(expected.FlipCoin(4));
        _ = expected.RandomIntLessThan(2);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 5,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.False(monster.IsMoving);
        Assert.Equal(1, monster.AvoidanceMovementTicks);
    }

    [Fact]
    public void MultiplayerGoatMeleeAiConsumesItsNativeRoundWalkAdmissionRolls()
    {
        const uint aiSeed = 16;
        var monster = new AuthoritativeCombatTarget(
            9,
            5,
            5,
            40,
            attackDamage: 2,
            aggroRange: 7,
            nativeAi: "GoatMelee",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var actionRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, actionRoll);
        Assert.True(expected.FlipCoin(4));
        _ = expected.RandomIntLessThan(2);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 5,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((5, 4), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void MultiplayerGoatMeleeAiConsumesItsNativeAdjacentAttackRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "GoatMelee",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, attackRoll);
        Assert.True(attackRoll < 23);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntBetween(5, 5);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Single(deliveries);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void MultiplayerGoatMeleeAiConsumesItsNativeWoundedSpecialRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            minDamage: 3,
            maxDamage: 3,
            minDamageSpecial: 8,
            maxDamageSpecial: 20,
            aggroRange: 5,
            toHit: 100,
            toHitSpecial: 100,
            nativeAi: "GoatMelee",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, attackRoll);
        Assert.True(attackRoll < 23);
        var specialRoll = expected.RandomIntLessThan(2);
        Assert.Equal(0, specialRoll);
        _ = expected.RandomIntLessThan(100);
        var expectedDamageQ6 = expected.RandomIntBetween(
            8 * DiabloFixedPointQ6.Scale,
            20 * DiabloFixedPointQ6.Scale);
        var expectedDamage = DiabloFixedPointQ6.ToWholeTruncated(expectedDamageQ6);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Equal(100 - expectedDamage, player.Life);
        var damage = Assert.Single(deliveries).Event;
        Assert.Equal(expectedDamage, damage.Amount);
        Assert.Equal(expectedDamageQ6, damage.AmountQ6);
    }

    [Fact]
    public void MultiplayerGargoyleAiConsumesItsNativeMovementAndRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            40,
            attackDamage: 1,
            aggroRange: 5,
            nativeAi: "Gargoyle",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var movementRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, movementRoll);
        Assert.True(movementRoll < 28);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void MultiplayerGargoyleAiConsumesItsNativeAdjacentAttackRoll()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            attackDamage: 5,
            aggroRange: 5,
            toHit: 100,
            nativeAi: "Gargoyle",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        var attackRoll = expected.RandomIntLessThan(100);
        Assert.Equal(4, attackRoll);
        Assert.True(attackRoll < 23);
        _ = expected.RandomIntLessThan(100);
        _ = expected.RandomIntBetween(5, 5);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.Single(deliveries);
        Assert.Equal(95, player.Life);
    }

    [Fact]
    public void MultiplayerGargoyleAiConsumesItsNativeHealingRoll()
    {
        const uint aiSeed = 2;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            aggroRange: 8,
            nativeAi: "Gargoyle",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.RandomIntLessThan(5);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 8,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        var deliveries = processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.HitPoints > 40);
        Assert.Single(deliveries);
        Assert.True(monster.GargoyleSpecialActive);
    }

    [Fact]
    public void MultiplayerGargoyleAiConsumesItsNativeRetreatRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            40,
            maxHitPoints: 100,
            aggroRange: 5,
            nativeAi: "Gargoyle",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 2,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((0, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void SinglePlayerInitializedButcherConsumesNativeRandomWalkRolls()
    {
        const uint initSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "Butcher",
            monsterItemSeed: initSeed,
            monsterAiSeed: initSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(initSeed);
        var expected = new DiabloRandom(initSeed);
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.SinglePlayer);
        var player = CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100);

        processor.AdvanceTick(1, [player]);

        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void MultiplayerButcherAiConsumesItsNativeRandomWalkRolls()
    {
        const uint aiSeed = 7;
        var monster = new AuthoritativeCombatTarget(
            9,
            3,
            0,
            10,
            attackDamage: 3,
            aggroRange: 5,
            nativeAi: "Butcher",
            monsterItemSeed: aiSeed,
            monsterAiSeed: aiSeed);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var random = new DiabloRandom(0xCAFEBABE);
        var processor = CreateProcessor(
            world,
            random: random,
            gameMode: DiabloGameMode.Multiplayer);
        var expected = new DiabloRandom(aiSeed);
        var expectedAiSeed = unchecked((uint)expected.AdvanceRndSeed());
        _ = expected.FlipCoin(2);
        _ = expected.FlipCoin(2);

        processor.AdvanceTick(1, [CreatePlayer(
            entityId: 7,
            positionX: 0,
            positionY: 0,
            life: 100,
            lifeMaximum: 100)]);

        Assert.Equal(expectedAiSeed, monster.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(monster.IsMoving);
        Assert.Equal((2, 0), (monster.FuturePositionX, monster.FuturePositionY));
    }

    [Fact]
    public void ClosedStashConsumableCastIsRejectedWithoutConsumingSource()
    {
        var scroll = AuthoritativeItemState.Empty with {
            StableItemId = 802,
            MiscId = 21,
            SpellId = 1,
        };
        var processor = CreateProcessor(new DiabloWorldState());
        var player = CreatePlayer(life: 10, lifeMaximum: 20);
        player.Stash.Add(new StashStoreItem(
            0,
            8,
            0,
            0,
            803,
            10,
            0,
            scroll));

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                0,
                0,
                0,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    8,
                    802,
                    0)),
            1);

        AssertRejected(result, DiabloRuleFailure.NotAllowed);
        Assert.Single(player.Stash);
    }

    [Fact]
    public void RejectedTargetDoesNotConsumeChargedOrConsumableItems()
    {
        var target = new AuthoritativeCombatTarget(9, 9, 9, 10);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0) {
                DamageAmount = 4,
                Range = 1,
            },
        ]);
        var staff = AuthoritativeItemState.Empty with {
            MiscId = 23,
            SpellId = 1,
            Charges = 2,
            MaxCharges = 2,
        };
        var scroll = AuthoritativeItemState.Empty with { MiscId = 21, SpellId = 1 };
        var processor = CreateProcessor(new DiabloWorldState(combatTargets: [target]), spells);
        var player = CreatePlayer(
            equipment: [new EquippedStoreItem(4, 77, staff)],
            belt: [new BeltStoreItem(2, 78, scroll)]);

        var charged = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                9,
                0,
                0,
                DiabloCastSourceKind.ChargedItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 4)),
            1);
        var consumable = processor.Execute(
            player,
            new DiabloCastIntent(
                1,
                9,
                0,
                0,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 2)),
            2);

        AssertRejected(charged, DiabloRuleFailure.InvalidTarget);
        AssertRejected(consumable, DiabloRuleFailure.InvalidTarget);
        Assert.Equal(2, Assert.Single(player.Equipment).State.Charges);
        Assert.Single(player.Belt);
    }

    [Fact]
    public void UnspecifiedLegacyCastInfersOnlyAnAuthoritativeOwnedSource()
    {
        var processor = CreateProcessor(new DiabloWorldState());
        var unowned = CreatePlayer(life: 10, lifeMaximum: 20, mana: 10, manaMaximum: 10);
        var learned = CreatePlayer(
            life: 10,
            lifeMaximum: 20,
            mana: 10,
            manaMaximum: 10,
            memorizedSpells: [new MemorizedSpellState(1, 1)]);

        var rejected = processor.Execute(unowned, new DiabloCastIntent(1, 0, 0, 0), 1);
        var accepted = processor.Execute(learned, new DiabloCastIntent(1, 0, 0, 0), 1);

        AssertRejected(rejected, DiabloRuleFailure.NotAllowed);
        Assert.True(accepted.Succeeded);
        Assert.Equal(9, learned.Mana);
    }

    [Fact]
    public void StoneCurseUsesNativeCrawlFutureOccupancyAndTimedRelease()
    {
        var immune = new AuthoritativeCombatTarget(
            8,
            10,
            10,
            20,
            stoneCurseImmune: true);
        var target = new AuthoritativeCombatTarget(
            9,
            9,
            9,
            20,
            moving: true,
            futurePositionX: 10,
            futurePositionY: 9);
        var world = new DiabloWorldState(combatTargets: [immune, target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateStoneCurseSpell()]));
        var player = CreatePlayer(
            positionX: 1,
            positionY: 1,
            mana: 10000,
            manaMaximum: 10000,
            entityId: 7,
            memorizedSpells: [new MemorizedSpellState(8, 3)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                8,
                8,
                10,
                10,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(6544, player.Mana);
        Assert.False(immune.IsPetrified);
        Assert.True(target.IsPetrified);
        Assert.True(target.IsMoving);
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.StoneCurse, controller.Behavior);
        Assert.Equal((9U, 10, 9, 144U, 11, 3), (
            controller.TargetEntityId,
            controller.PositionX,
            controller.PositionY,
            controller.RemainingTicks,
            controller.ImpactLifetime,
            controller.EffectiveSpellLevel));

        for (ulong tick = 1; tick <= 143; tick++)
            Assert.Empty(processor.AdvanceTick(tick, [player]));
        Assert.True(target.IsPetrified);
        Assert.Equal((9, 9), (target.PositionX, target.PositionY));
        Assert.Empty(processor.AdvanceTick(144, [player]));
        Assert.False(target.IsPetrified);
        Assert.Empty(world.Projectiles);
        Assert.Empty(processor.AdvanceTick(145, [player]));
        Assert.Equal((10, 9), (target.PositionX, target.PositionY));
        Assert.False(target.IsMoving);
    }

    [Fact]
    public void RuneOfStoneUsesStableSpellIdentityFromExternalConsumableMapping()
    {
        var target = new AuthoritativeCombatTarget(9, 2, 0, 20);
        var world = new DiabloWorldState(combatTargets: [target]);
        var spell = CreateStoneCurseSpell() with { ConsumableMiscId = 51 };
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([spell]));
        var rune = AuthoritativeItemState.Empty with { MiscId = 51 };
        var player = CreatePlayer(
            mana: 10000,
            manaMaximum: 10000,
            belt: [new BeltStoreItem(2, 72, rune)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                8,
                9,
                2,
                0,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 2)),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Belt);
        Assert.True(target.IsPetrified);
    }

    [Fact]
    public void StoneCurseFizzlePreservesSourceButActiveRecastConsumesIt()
    {
        var spell = CreateStoneCurseSpell();
        var immune = new AuthoritativeCombatTarget(
            8,
            2,
            2,
            20,
            stoneCurseImmune: true);
        var immuneWorld = new DiabloWorldState(combatTargets: [immune]);
        var immuneProcessor = CreateProcessor(
            immuneWorld,
            new AuthoritativeSpellCatalog([spell]));
        var fizzlePlayer = CreatePlayer(
            mana: 5000,
            manaMaximum: 5000,
            memorizedSpells: [new MemorizedSpellState(8, 1)]);
        var intent = new DiabloCastIntent(
            8,
            8,
            2,
            2,
            DiabloCastSourceKind.MemorizedSpell);

        var fizzle = immuneProcessor.Execute(fizzlePlayer, intent, 1);

        Assert.True(fizzle.Succeeded);
        Assert.Equal(5000, fizzlePlayer.Mana);
        Assert.Empty(immuneWorld.Projectiles);

        var petrified = new AuthoritativeCombatTarget(
            9,
            2,
            2,
            20,
            petrified: true);
        var activeWorld = new DiabloWorldState(combatTargets: [petrified]);
        var activeProcessor = CreateProcessor(
            activeWorld,
            new AuthoritativeSpellCatalog([spell]));
        var recastPlayer = CreatePlayer(
            mana: 5000,
            manaMaximum: 5000,
            memorizedSpells: [new MemorizedSpellState(8, 1)]);

        var recast = activeProcessor.Execute(
            recastPlayer,
            intent with { TargetEntityId = 9 },
            1);

        Assert.True(recast.Succeeded);
        Assert.Equal(1160, recastPlayer.Mana);
        Assert.True(petrified.IsPetrified);
        Assert.Empty(activeWorld.Projectiles);
    }

    [Fact]
    public void StoneCurseDeadTargetRunsShatterPhaseAndStaysPetrified()
    {
        var target = new AuthoritativeCombatTarget(9, 2, 2, 20);
        var world = new DiabloWorldState(combatTargets: [target]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateStoneCurseSpell()]));
        var player = CreatePlayer(
            mana: 5000,
            manaMaximum: 5000,
            memorizedSpells: [new MemorizedSpellState(8, 1)]);

        Assert.True(processor.Execute(
            player,
            new DiabloCastIntent(8, 9, 2, 2),
            1).Succeeded);
        target.HitPoints = 0;

        Assert.Empty(processor.AdvanceTick(1, [player]));
        var controller = Assert.Single(world.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, controller.Phase);
        Assert.Equal(11U, controller.RemainingTicks);
        for (ulong tick = 2; tick <= 11; tick++)
            Assert.Empty(processor.AdvanceTick(tick, [player]));
        Assert.Equal(1U, controller.RemainingTicks);
        Assert.Empty(processor.AdvanceTick(12, [player]));
        Assert.Empty(world.Projectiles);
        Assert.True(target.IsPetrified);
    }

    [Fact]
    public void ManaShieldCastConsumesManaOnceAndActiveRecastFizzles()
    {
        var spell = new AuthoritativeSpellDefinition(11, 33, 0, 0, 0, 0) {
            ProjectileBehavior = AuthoritativeProjectileBehavior.ManaShield,
            MinimumManaCost = 33,
            ManaCostScale = 64,
            ManaShieldBaseDamageReductionDivisor = 24,
            ManaShieldDamageReductionLevelStep = 3,
            ManaShieldDamageReductionMaximumLevel = 7,
        };
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([spell]));
        var player = CreatePlayer(
            life: 6400,
            lifeMaximum: 6400,
            mana: 6400,
            manaMaximum: 6400,
            memorizedSpells: [new MemorizedSpellState(11, 1)]);
        var intent = new DiabloCastIntent(
            11,
            0,
            0,
            0,
            DiabloCastSourceKind.MemorizedSpell);

        var first = processor.Execute(player, intent, 1);
        var second = processor.Execute(player, intent, 2);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.True(player.ManaShieldActive);
        Assert.Equal(4288, player.Mana);
        Assert.Empty(first.Events);
        Assert.Empty(second.Events);
    }

    [Fact]
    public void InfravisionUsesNativeDurationScalingAndMaxUnionRecast()
    {
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateInfravisionSpell()]));
        var player = CreatePlayer(
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(9, 3)]);
        var intent = new DiabloCastIntent(
            9,
            0,
            0,
            0,
            DiabloCastSourceKind.MemorizedSpell);

        var first = processor.Execute(player, intent, 1);

        Assert.True(first.Succeeded);
        Assert.Equal(8080, player.Mana);
        var status = Assert.Single(player.StatusEffects);
        Assert.Equal((9U, 2254U, 0), (
            status.EffectId,
            status.RemainingTicks,
            status.Magnitude));
        Assert.Equal(AuthoritativeStatusEffectBehavior.Infravision, status.Behavior);

        player.StatusEffects[0] = status with { RemainingTicks = 3000 };
        var second = processor.Execute(player, intent, 2);

        Assert.True(second.Succeeded);
        Assert.Equal(6160, player.Mana);
        Assert.Equal(3000U, Assert.Single(player.StatusEffects).RemainingTicks);
    }

    [Fact]
    public void InfravisionScrollUsesLevelZeroDurationAndIsConsumed()
    {
        var scroll = AuthoritativeItemState.Empty with { MiscId = 21, SpellId = 9 };
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateInfravisionSpell()]));
        var player = CreatePlayer(belt: [new BeltStoreItem(2, 72, scroll)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                9,
                0,
                0,
                0,
                DiabloCastSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 2)),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Belt);
        var status = Assert.Single(player.StatusEffects);
        Assert.Equal(1584U, status.RemainingTicks);
        Assert.Equal(AuthoritativeStatusEffectBehavior.Infravision, status.Behavior);
    }

    [Fact]
    public void EtherealizeCreatesLevelScopedStatusAndBlocksMonsterDamage()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            100,
            attackDamage: 20,
            aggroRange: 2);
        var world = new DiabloWorldState(combatTargets: [monster]);
        var processor = CreateProcessor(
            world,
            new AuthoritativeSpellCatalog([CreateEtherealizeSpell()]));
        var player = CreatePlayer(
            entityId: 7,
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(53, 1)]);

        var cast = processor.Execute(
            player,
            new DiabloCastIntent(
                53,
                0,
                player.PositionX,
                player.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(cast.Succeeded, cast.Failure?.ToString());
        var status = Assert.Single(player.StatusEffects);
        Assert.Equal((53U, uint.MaxValue, 0), (
            status.EffectId,
            status.RemainingTicks,
            status.Magnitude));
        Assert.Equal(AuthoritativeStatusEffectBehavior.Etherealize, status.Behavior);
        var lifeBefore = player.Life;

        processor.AdvanceTick(2, [player]);

        Assert.Equal(lifeBefore, player.Life);
        Assert.Equal(uint.MaxValue, Assert.Single(player.StatusEffects).RemainingTicks);
    }

    [Fact]
    public void SearchAbilityUsesNativeDurationAndAdditiveRecastsWithoutMana()
    {
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateSearchSpell()]));
        var player = CreatePlayer(
            mana: 960,
            manaMaximum: 960,
            abilitySpellId: 46,
            characterClassId: 3,
            characterLevel: 5);
        var intent = new DiabloCastIntent(
            46,
            0,
            0,
            0,
            DiabloCastSourceKind.Ability);

        Assert.True(processor.Execute(player, intent, 1).Succeeded);
        var first = Assert.Single(player.StatusEffects);
        Assert.Equal((46U, 255U, 0), (
            first.EffectId,
            first.RemainingTicks,
            first.Magnitude));
        Assert.Equal(AuthoritativeStatusEffectBehavior.Search, first.Behavior);
        Assert.Equal(960, player.Mana);

        Assert.True(processor.Execute(player, intent, 1).Succeeded);
        Assert.Equal(510U, Assert.Single(player.StatusEffects).RemainingTicks);
        DiabloCombatCommandProcessor.AdvanceStatusEffects(player, 1);
        DiabloCombatCommandProcessor.AdvanceStatusEffects(player, 2);
        Assert.Equal(509U, Assert.Single(player.StatusEffects).RemainingTicks);
    }

    [Fact]
    public void SearchRecastRetainsNativeDurationWhenSignedAdditionWouldOverflow()
    {
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateSearchSpell()]));
        var player = CreatePlayer(
            abilitySpellId: 46,
            characterClassId: 3,
            characterLevel: 5);
        var retainedDuration = (uint)int.MaxValue - 100;
        player.StatusEffects.Add(new AuthoritativeStatusEffect(
            46,
            retainedDuration,
            0) {
            Behavior = AuthoritativeStatusEffectBehavior.Search,
        });

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                46,
                0,
                0,
                0,
                DiabloCastSourceKind.Ability),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(
            retainedDuration,
            Assert.Single(player.StatusEffects).RemainingTicks);
    }

    [Fact]
    public void ReflectCastAddsNativeLevelScaledChargesAndConsumesMana()
    {
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateReflectSpell()]));
        var player = CreatePlayer(
            entityId: 7,
            characterLevel: 3,
            mana: 10000,
            manaMaximum: 10000,
            memorizedSpells: [new MemorizedSpellState(43, 2)]);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                43,
                0,
                player.PositionX,
                player.PositionY,
                DiabloCastSourceKind.MemorizedSpell),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(6U, player.ReflectCharges);
        Assert.Equal(7760, player.Mana);
    }

    [Fact]
    public void ReflectUsesConfiguredDefaultSpellLevelForAbilityCasts()
    {
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([
                CreateReflectSpell() with { ReflectDefaultSpellLevel = 4 },
            ]));
        var player = CreatePlayer(
            entityId: 7,
            characterLevel: 3,
            mana: 10000,
            manaMaximum: 10000,
            abilitySpellId: 43);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                43,
                0,
                player.PositionX,
                player.PositionY,
                DiabloCastSourceKind.Ability),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(12U, player.ReflectCharges);
        Assert.Equal(10000, player.Mana);
    }

    [Fact]
    public void MagiCastRestoresCurrentManaToTheNativeMaximum()
    {
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateMagiSpell()]));
        var player = CreatePlayer(
            entityId: 7,
            mana: 10,
            manaMaximum: 100,
            abilitySpellId: 40);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                40,
                0,
                player.PositionX,
                player.PositionY,
                DiabloCastSourceKind.Ability),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(100, player.Mana);
    }

    [Fact]
    public void ManaCastUsesNativeLcgLevelAndClassScaling()
    {
        const uint seed = 0x12345678;
        var random = new DiabloRandom(seed);
        var shippedClasses = LoadShippedPlayerClasses();
        var playerClasses = new AuthoritativePlayerClassCatalog(
            shippedClasses.Definitions.Select(definition => definition.ClassId == 2
                ? definition with { ManaSpellPercent = 125 }
                : definition));
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateManaSpell(2, 3, 5)]),
            random: random,
            playerClasses: playerClasses);
        var player = CreatePlayer(
            entityId: 7,
            characterClassId: 2,
            characterLevel: 3,
            mana: 64,
            manaMaximum: 1_000_000,
            abilitySpellId: 41,
            memorizedSpells: [new MemorizedSpellState(41, 2)]);

        var expectedManaQ6 = random.RandomIntBetween(1, 2);
        for (var level = 0; level < player.CharacterLevel; level++)
            expectedManaQ6 += random.RandomIntBetween(1, 3);
        for (var level = 0; level < 2; level++)
            expectedManaQ6 += random.RandomIntBetween(1, 5);
        expectedManaQ6 = expectedManaQ6 * 64 * 125 / 100;
        var expectedRandomState = random.State;
        random.Restore(seed);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                41,
                0,
                player.PositionX,
                player.PositionY,
                DiabloCastSourceKind.Ability),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(64 + expectedManaQ6, player.Mana);
        Assert.Equal(expectedRandomState, random.State);
    }

    [Fact]
    public void ManaCastClampsAtTheNativeMaximum()
    {
        var processor = CreateProcessor(
            new DiabloWorldState(),
            new AuthoritativeSpellCatalog([CreateManaSpell()]),
            random: new DiabloRandom(0));
        var player = CreatePlayer(
            entityId: 7,
            characterLevel: 1,
            mana: 60,
            manaMaximum: 64,
            abilitySpellId: 41);

        var result = processor.Execute(
            player,
            new DiabloCastIntent(
                41,
                0,
                player.PositionX,
                player.PositionY,
                DiabloCastSourceKind.Ability),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(64, player.Mana);
    }

    private static DiabloCombatCommandProcessor CreateProcessor(
        DiabloWorldState world,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeCombatRules? rules = null,
        DiabloRandom? random = null,
        AuthoritativePlayerClassCatalog? playerClasses = null,
        AuthoritativePlayerAnimationCatalog? playerAnimations = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        AuthoritativeItemCatalog? items = null,
        DiabloWorldCommandProcessor? worldCommands = null,
        AuthoritativeMonsterMissileCatalog? monsterMissiles = null,
        AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner = null,
        AuthoritativeMonsterTypeCatalog? monsterTypes = null,
        bool useNativePlayerActionTiming = false,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal,
        AuthoritativeWorldRules? worldRules = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        uint monsterFlashSpellId = DiabloSimulationOptions.LegacyMonsterFlashSpellId,
        AuthoritativeQuestDialogueStageCatalog? questDialogueStages = null)
    {
        if (playerAnimations is null && playerClasses is not null)
            playerAnimations = LoadShippedPlayerAnimations(playerClasses);
        return new DiabloCombatCommandProcessor(
            world,
            spells ?? new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(1, 1, 1, 0, 0, 0),
            ]),
            rules ?? new AuthoritativeCombatRules(10, 1, 100),
            random,
            playerClasses,
            playerAnimations,
            useNativePlayerActionTiming,
            gameMode: gameMode,
            items: items,
            worldCommands: worldCommands,
            monsterMissiles: monsterMissiles,
            monsterSpawnPlanner: monsterSpawnPlanner,
            monsterTypes: monsterTypes,
            difficulty: difficulty,
            worldRules: worldRules,
            spellBooks: spellBooks,
            monsterFlashSpellId: monsterFlashSpellId,
            questDialogueStages: questDialogueStages);
    }

    private static AuthoritativeMonsterMissileCatalog CreateArrowMissileCatalog(
        string nativeAi,
        int projectileSpeed = 0,
        int projectileLifetime = 0,
        AuthoritativeDamageType? damageType = null)
    {
        return new AuthoritativeMonsterMissileCatalog([
            new AuthoritativeMonsterMissileDefinition(
                nativeAi,
                "Arrow",
                AuthoritativeProjectileBehavior.Targeted,
                ProjectileSpeed: projectileSpeed,
                ProjectileLifetime: projectileLifetime,
                DamageType: damageType),
        ]);
    }

    private static AuthoritativeSpellDefinition CreateTelekinesisSpell()
    {
        return new AuthoritativeSpellDefinition(33, 15, 0, 0, 0, 0) {
            NativeSpellId = 33,
            Range = 112,
            ManaAdjustment = 2,
            MinimumManaCost = 8,
            ManaCostScale = 64,
            StaffLevel = 2,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Telekinesis,
        };
    }

    private static AuthoritativeSpellDefinition CreateTownPortalSpell()
    {
        return new AuthoritativeSpellDefinition(39, 35, 0, 0, 0, 0) {
            NativeSpellId = 7,
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.TownPortal,
            ManaAdjustment = 3,
            MinimumManaCost = 18,
            ManaCostScale = 64,
            StaffLevel = 3,
        };
    }

    private static AuthoritativeSpellDefinition CreateChargedBoltSpell()
    {
        return new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) {
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
        };
    }

    private static AuthoritativeSpellDefinition CreateBoneSpiritSpell()
    {
        return new AuthoritativeSpellDefinition(38, 24, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.BoneSpirit,
            ManaAdjustment = 1,
            ManaAdjustmentDivisor = 1,
            MinimumManaCost = 12,
            ManaCostScale = 64,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 7,
            SelfDamage = 6,
        };
    }

    private static AuthoritativeSpellDefinition CreateFireboltSpell()
    {
        return new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileSpeed = 26,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Firebolt,
            ManaAdjustment = 1,
            ManaAdjustmentDivisor = 2,
            MinimumManaCost = 3,
            ManaCostScale = 64,
            RandomDamageRange = 10,
            MagicDamageDivisor = 8,
            SpellLevelDamage = 1,
            DamageBonus = 1,
            ResistantDamageDivisor = 4,
        };
    }

    private static AuthoritativeSpellDefinition CreateFireballSpell()
    {
        return new AuthoritativeSpellDefinition(12, 16, 0, 0, 0, 0) {
            Range = 112,
            AreaRadius = 1,
            DamageType = AuthoritativeDamageType.Fire,
            ProjectileSpeed = 16,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Fireball,
            ManaAdjustment = 1,
            MinimumManaCost = 10,
            ManaCostScale = 64,
            RandomDamageRange = 10,
            DamageBonus = 4,
            DamageScale = 2,
            RandomDamageIterationsBonus = 2,
            CharacterLevelDamageDivisor = 1,
            SpellEffectScaleDivisor = 8,
            ProjectileSpeedSpellLevelStep = 2,
            ProjectileSpeedMaximumBonus = 34,
            ResistantDamageDivisor = 4,
            ProjectileImpactLifetime = 14,
        };
    }

    private static AuthoritativeSpellDefinition CreateLightningSpell()
    {
        return new AuthoritativeSpellDefinition(3, 10, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Lightning,
            ProjectileSpeed = 32,
            ProjectileLifetime = 256,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Lightning,
            ManaAdjustment = 1,
            MinimumManaCost = 6,
            ManaCostScale = 64,
            RandomDamageRange = 2,
            DamageBonus = 2,
            CharacterLevelRandomDamageDivisor = 1,
            DamageScale = 64,
            SecondaryProjectileLifetime = 6,
            SecondaryProjectileLifetimeSpellLevelDivisor = 2,
            ResistantDamageDivisor = 4,
        };
    }

    private static AuthoritativeSpellDefinition CreateFlashSpell()
    {
        return new AuthoritativeSpellDefinition(4, 30, 0, 0, 0, 0) {
            Range = 1,
            AreaRadius = 1,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 19,
            ProjectileCount = 2,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Flash,
            ManaAdjustment = 2,
            MinimumManaCost = 16,
            ManaCostScale = 64,
            RandomDamageRange = 20,
            DamageBonus = 1,
            RandomDamageIterationsCharacterLevelDivisor = 1,
            RandomDamageIterationsBonus = 1,
            CharacterLevelDamageDivisor = 1,
            SpellEffectScaleDivisor = 8,
            PostScaleDamagePercent = 50,
            ResistantDamageDivisor = 4,
        };
    }

    private static AuthoritativeSpellDefinition CreateHealingSpell()
    {
        return new AuthoritativeSpellDefinition(2, 5, 1, 0, 0, 0) {
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
        };
    }

    private static AuthoritativeSpellDefinition CreateHealOtherSpell()
    {
        return CreateHealingSpell() with {
            SpellId = 34,
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.HealOther,
        };
    }

    private static AuthoritativeSpellDefinition CreateTeleportSpell()
    {
        return new AuthoritativeSpellDefinition(23, 35, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 2,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Teleport,
            ManaAdjustment = 3,
            MinimumManaCost = 15,
            ManaCostScale = 64,
        };
    }

    private static AuthoritativeSpellDefinition CreatePhasingSpell()
    {
        return new AuthoritativeSpellDefinition(10, 12, 0, 0, 0, 0) {
            Range = 1,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 2,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Phasing,
            ManaAdjustment = 2,
            MinimumManaCost = 4,
            ManaCostScale = 64,
            PhasingMinimumAxisOffset = 4,
            PhasingMaximumAxisOffset = 6,
        };
    }

    private static AuthoritativeSpellDefinition CreateResurrectionSpell()
    {
        return new AuthoritativeSpellDefinition(32, 20, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 16,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Resurrection,
            ManaAdjustment = 2,
            MinimumManaCost = 20,
            ManaCostScale = 64,
            ResurrectionLife = 10,
            ResurrectionLifeScale = 64,
            ResurrectionSearchRadius = 50,
        };
    }

    private static AuthoritativeSpellDefinition CreateStoneCurseSpell()
    {
        return new AuthoritativeSpellDefinition(8, 60, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.StoneCurse,
            ProjectileImpactLifetime = 11,
            ManaAdjustment = 3,
            MinimumManaCost = 40,
            ManaCostScale = 64,
            StoneCurseBaseDuration = 6,
            StoneCurseMaximumDuration = 15,
            StoneCurseDurationScale = 16,
            StoneCurseSearchRadius = 5,
        };
    }

    private static AuthoritativeSpellDefinition CreateInfravisionSpell()
    {
        return new AuthoritativeSpellDefinition(9, 40, 0, 9, 1584, 0) {
            Range = 1,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Infravision,
            ManaAdjustment = 5,
            MinimumManaCost = 20,
            ManaCostScale = 64,
            InfravisionDurationGrowthDivisor = 8,
        };
    }

    private static AuthoritativeSpellDefinition CreateEtherealizeSpell()
    {
        return new AuthoritativeSpellDefinition(53, 100, 0, 53, uint.MaxValue, 0) {
            NativeSpellId = 25,
            Range = 1,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Etherealize,
            ManaCostScale = 64,
            StaffLevel = -1,
        };
    }

    private static AuthoritativeSpellDefinition CreateSearchSpell()
    {
        return new AuthoritativeSpellDefinition(46, 15, 0, 46, 245, 0) {
            Range = 1,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Search,
            ManaAdjustment = 1,
            MinimumManaCost = 1,
            ManaCostScale = 64,
            SearchCharacterLevelDurationMultiplier = 2,
            SearchSpellLevelDurationMultiplier = 10,
        };
    }

    private static AuthoritativeSpellDefinition CreateReflectSpell()
    {
        return new AuthoritativeSpellDefinition(43, 35, 0, 0, 0, 0) {
            NativeSpellId = 43,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Reflect,
            ReflectDamagePercentMinimum = 20,
            ReflectDamagePercentMaximum = 30,
            ManaAdjustmentDivisor = 1,
            ManaCostScale = 64,
            StaffLevel = -1,
        };
    }

    private static AuthoritativeSpellDefinition CreateMagiSpell()
    {
        return new AuthoritativeSpellDefinition(40, 255, 0, 0, 0, 0) {
            NativeSpellId = 38,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Magi,
            ManaAdjustmentDivisor = 1,
            ManaCostScale = 64,
            StaffLevel = -1,
        };
    }

    private static AuthoritativeSpellDefinition CreateManaSpell(
        int manaRandomRange = 0,
        int manaCharacterLevelRandomRange = 0,
        int manaSpellLevelRandomRange = 0)
    {
        return new AuthoritativeSpellDefinition(41, 255, 0, 0, 0, 0) {
            NativeSpellId = 37,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileCount = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Mana,
            ManaAdjustmentDivisor = 1,
            ManaCostScale = 64,
            ManaRandomRange = manaRandomRange,
            ManaCharacterLevelRandomRange = manaCharacterLevelRandomRange,
            ManaSpellLevelRandomRange = manaSpellLevelRandomRange,
            StaffLevel = -1,
        };
    }

    private static AuthoritativeProjectile CreateTravelingChargedBolt(
        int velocityY,
        int pathTicks = 1)
    {
        return new AuthoritativeProjectile(
            0x80000000,
            7,
            0,
            30,
            0,
            0,
            0,
            4,
            4,
            1,
            AuthoritativeDamageType.Lightning,
            0,
            32) {
            Behavior = AuthoritativeProjectileBehavior.ChargedBolt,
            VelocityY = velocityY,
            OriginalDirection = 0,
            PathTicks = pathTicks,
            Speed = 8,
            ImpactLifetime = 8,
            ResistantDamageDivisor = 4,
        };
    }

    private static AuthoritativeProjectile CreateStationaryChargedBolt(
        int positionX,
        int positionY,
        uint entityId = 0x80000000)
    {
        return new AuthoritativeProjectile(
            entityId,
            7,
            0,
            30,
            0,
            positionX,
            positionY,
            positionX,
            positionY,
            2,
            AuthoritativeDamageType.Lightning,
            0,
            32) {
            Behavior = AuthoritativeProjectileBehavior.ChargedBolt,
            OriginalDirection = 0,
            PathTicks = 1,
            Speed = 8,
            ImpactLifetime = 8,
            ResistantDamageDivisor = 4,
        };
    }

    private static AuthoritativePlayerState CreateBlockingVictim()
    {
        var shield = AuthoritativeItemState.Empty with {
            ItemType = 5,
            StatFlag = true,
        };
        return CreatePlayer(
            positionX: 1,
            positionY: 1,
            life: 640,
            lifeMaximum: 640,
            entityId: 8,
            attributes: PlayerAttributesState.Zero with {
                Dexterity = new PlayerAttributeState(100, 100),
            },
            equipment: [new EquippedStoreItem(5, 99, shield)]);
    }

    private static AuthoritativePlayerClassCatalog LoadShippedPlayerClasses()
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
                var path = Path.Combine(
                    directory.FullName,
                    "server",
                    "content",
                    "base",
                    "classes.tsv");
                if (File.Exists(path))
                    return AuthoritativePlayerClassCatalog.LoadTsv(
                        "classes.tsv",
                        File.ReadAllText(path));
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the shipped player-class catalog.");
    }

    private static AuthoritativePlayerAnimationCatalog LoadShippedPlayerAnimations(
        AuthoritativePlayerClassCatalog playerClasses)
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
                var path = Path.Combine(
                    directory.FullName,
                    "server",
                    "content",
                    "base",
                    "class_animations.tsv");
                if (File.Exists(path)) {
                    return AuthoritativePlayerAnimationCatalog.LoadTsv(
                        "class_animations.tsv",
                        File.ReadAllText(path),
                        playerClasses);
                }
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the shipped player-animation catalog.");
    }

    private static AuthoritativeItemCatalog LoadShippedItems()
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
                var path = Path.Combine(
                    directory.FullName,
                    "server",
                    "content",
                    "base",
                    "items.tsv");
                if (File.Exists(path))
                    return AuthoritativeItemCatalog.LoadTsv(
                        "items.tsv",
                        File.ReadAllText(path));
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the shipped authoritative item catalog.");
    }

    private static AuthoritativePlayerState CreatePlayer(
        int positionX = 0,
        int positionY = 0,
        uint levelId = 0,
        int life = 50,
        int lifeMaximum = 50,
        int mana = 0,
        int manaMaximum = 0,
        int armorClass = 0,
        uint entityId = 0,
        uint abilitySpellId = 0,
        IReadOnlyList<MemorizedSpellState>? memorizedSpells = null,
        IReadOnlyList<EquippedStoreItem>? equipment = null,
        IReadOnlyList<BeltStoreItem>? belt = null,
        IReadOnlyList<OwnedStoreItem>? inventory = null,
        IReadOnlyList<int>? inventoryGrid = null,
        PlayerAttributesState? attributes = null,
        uint characterClassId = 0,
        uint characterLevel = 1,
        OwnedStoreItem? heldItem = null,
        string characterName = "",
        uint gold = 100)
    {
        return new AuthoritativePlayerState(
            gold,
            0,
            life,
            mana,
            manaMaximum,
            attributes ?? PlayerAttributesState.Zero,
            equipment ?? [],
            inventoryGrid ?? Enumerable.Repeat(-1, 40).ToArray(),
            belt ?? [],
            positionX,
            positionY,
            lifeMaximum,
            characterLevel,
            levelId,
            armorClass,
            characterClassId,
            inventory: inventory,
            abilitySpellId: abilitySpellId,
            memorizedSpells: memorizedSpells,
            heldItem: heldItem,
            characterName: characterName) {
            EntityId = entityId,
        };
    }

    private static void AssertRejected(
        DiabloCombatIntentResult result,
        DiabloRuleFailure expectedFailure)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expectedFailure, result.Failure);
        Assert.Empty(result.Events);
    }
}
