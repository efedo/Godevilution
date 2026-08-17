using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloStoreCommandProcessorTests
{
    [Fact]
    public void SorcererRechargeAbilityUsesNativeChargeLossAndRandomSequence()
    {
        const uint rechargeSpellId = 27;
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 88,
                ItemType = 1,
                SpellId = 30,
                Charges = 1,
                MaxCharges = 10,
            });
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = rechargeSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(1);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
        ]);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\n"
                + "7\t10\t1\t1\tStaff\t88\n");
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            rechargeAbilitySpellId: rechargeSpellId,
            spells: spells,
            items: items);

        var result = processor.Execute(
            player,
            new DiabloRechargeItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        var recharged = Assert.Single(player.Inventory);
        Assert.Equal(8, recharged.State.Charges);
        Assert.Equal(8, recharged.State.MaxCharges);
        Assert.Equal(22695478U, random.State);
        Assert.Equal(0U, player.Gold);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void SemanticRechargeRoleAuthorizesAConfiguredCustomSpellIdentity()
    {
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
        var player = CreatePlayer(
            gold: 0,
            inventory: [item],
            abilityKind: DiabloPlayerAbilityKind.Recharge);
        player.AbilitySpellId = 900;
        player.CharacterLevel = 10;
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: new DiabloRandom(1),
            rechargeAbilitySpellId: 27,
            spells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
            ]));

        var result = processor.Execute(
            player,
            new DiabloRechargeItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(8, Assert.Single(player.Inventory).State.Charges);
    }

    [Fact]
    public void ExplicitSemanticRoleRejectsAConflictingLegacySpellIdentity()
    {
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
        var player = CreatePlayer(
            gold: 0,
            inventory: [item],
            abilityKind: DiabloPlayerAbilityKind.Repair);
        player.AbilitySpellId = 27;
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            rechargeAbilitySpellId: 27,
            spells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
            ]));

        var result = processor.Execute(
            player,
            new DiabloRechargeItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(1, Assert.Single(player.Inventory).State.Charges);
    }

    [Fact]
    public void SorcererRechargeAbilityAcceptsFullyChargedStaffWithoutAdvancingRandom()
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
                Charges = 10,
                MaxCharges = 10,
            });
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = rechargeSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(0x12345678);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
        ]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            rechargeAbilitySpellId: rechargeSpellId,
            spells: spells);

        var result = processor.Execute(
            player,
            new DiabloRechargeItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(10, Assert.Single(player.Inventory).State.Charges);
        Assert.Equal(0x12345678U, random.State);
    }

    [Fact]
    public void SorcererRechargeAbilityCanExhaustTheLastMaximumCharge()
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
                Charges = 0,
                MaxCharges = 1,
            });
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = rechargeSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(0);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
        ]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            rechargeAbilitySpellId: rechargeSpellId,
            spells: spells);

        var result = processor.Execute(
            player,
            new DiabloRechargeItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        var exhausted = Assert.Single(player.Inventory).State;
        Assert.Equal(0, exhausted.Charges);
        Assert.Equal(0, exhausted.MaxCharges);
        Assert.Equal(1U, random.State);
    }

    [Fact]
    public void RechargeAbilityRejectsPlayerWithoutTheConfiguredClassSkill()
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
        var player = CreatePlayer(gold: 100, inventory: [item]);
        var random = new DiabloRandom(0);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
        ]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            rechargeAbilitySpellId: rechargeSpellId,
            spells: spells);

        var result = processor.Execute(
            player,
            new DiabloRechargeItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(1, Assert.Single(player.Inventory).State.Charges);
        Assert.Equal(0U, random.State);
        Assert.Equal(100U, player.Gold);
    }

    [Fact]
    public void WarriorRepairAbilityUsesNativeDurabilityLossAndRandomSequence()
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
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = repairSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(0);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            repairAbilitySpellId: repairSpellId);

        var result = processor.Execute(
            player,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        var repaired = Assert.Single(player.Inventory);
        Assert.Equal(18, repaired.State.Durability);
        Assert.Equal(18, repaired.State.MaxDurability);
        Assert.Equal(22695478U, random.State);
        Assert.Equal(0U, player.Gold);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void WarriorRepairAbilityUsesConfiguredDurabilityLevelOffset()
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
        var player = CreatePlayer(
            gold: 0,
            inventory: [item],
            playerClass: LoadWarriorClass() with {
                RepairAbilityDurabilityLevelOffset = 1,
            });
        player.AbilitySpellId = repairSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(0);
        var expectedRandom = new DiabloRandom(0);
        var expectedDurability = 1;
        var expectedMaximum = 20;
        var repaired = 0;
        do {
            repaired += 10 + expectedRandom.GenerateRnd(10);
            expectedMaximum -= Math.Max(expectedMaximum / 11, 1);
        } while (repaired + expectedDurability < expectedMaximum);

        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            repairAbilitySpellId: repairSpellId);
        var result = processor.Execute(
            player,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        var repairedItem = Assert.Single(player.Inventory);
        Assert.Equal(expectedMaximum, repairedItem.State.MaxDurability);
        Assert.Equal(
            Math.Min(expectedDurability + repaired, expectedMaximum),
            repairedItem.State.Durability);
        Assert.Equal(expectedRandom.State, random.State);
    }

    [Fact]
    public void WarriorRepairAbilityAcceptsFullyRepairedItemWithoutAdvancingRandom()
    {
        const uint repairSpellId = 26;
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                Durability = 20,
                MaxDurability = 20,
            });
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = repairSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(0x12345678);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            repairAbilitySpellId: repairSpellId);

        var result = processor.Execute(
            player,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(20, Assert.Single(player.Inventory).State.Durability);
        Assert.Equal(0x12345678U, random.State);
    }

    [Fact]
    public void RepairAbilityRejectsPlayerWithoutTheConfiguredClassSkill()
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
        var player = CreatePlayer(gold: 100, inventory: [item]);
        var random = new DiabloRandom(0);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            repairAbilitySpellId: repairSpellId);

        var result = processor.Execute(
            player,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(1, Assert.Single(player.Inventory).State.Durability);
        Assert.Equal(0U, random.State);
        Assert.Equal(100U, player.Gold);
    }

    [Fact]
    public void BardIdentifyAbilityIdentifiesOwnedItemWithoutStoreOrGold()
    {
        const uint identifySpellId = 5;
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                Magical = 0,
                Identified = false,
                Value = 100,
                IdentifiedValue = 250,
            });
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = identifySpellId;
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            identifyAbilitySpellId: identifySpellId);

        var result = processor.Execute(
            player,
            new DiabloIdentifyItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        Assert.True(Assert.Single(player.Inventory).State.Identified);
        Assert.Equal(0U, player.Gold);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void IdentifyScrollIdentifiesMagicalItemAndConsumesOnlyTheSourceScroll()
    {
        var target = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with { Magical = 1, Identified = false });
        var scroll = new OwnedStoreItem(
            0,
            0,
            43,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.Identify,
                MiscId = 21,
            });
        var player = CreatePlayer(gold: 500, inventory: [target, scroll]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloIdentifyItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.True(result.Succeeded);
        var remaining = Assert.Single(player.Inventory);
        Assert.True(remaining.State.Identified);
        Assert.Equal(42U, remaining.ItemSeed);
        Assert.Equal(500U, player.Gold);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void IdentifyScrollRejectsNonActionSource()
    {
        var target = new OwnedStoreItem(0, 0, 42, 100, 0,
            AuthoritativeItemState.Empty with { Magical = 1 });
        var source = new OwnedStoreItem(0, 0, 43, 100, 0, AuthoritativeItemState.Empty);
        var player = CreatePlayer(inventory: [target, source]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloIdentifyItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.False(player.Inventory[0].State.Identified);
        Assert.Equal(2, player.Inventory.Count);
    }

    [Fact]
    public void IdentifyAbilityRejectsPlayerWithoutTheConfiguredClassSkill()
    {
        const uint identifySpellId = 5;
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with { Magical = 1 });
        var player = CreatePlayer(gold: 100, inventory: [item]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            identifyAbilitySpellId: identifySpellId);

        var result = processor.Execute(
            player,
            new DiabloIdentifyItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.False(Assert.Single(player.Inventory).State.Identified);
        Assert.Equal(100U, player.Gold);
    }

    [Fact]
    public void ReadBookLearnsAndLevelsSpellConsumesBookAndRefundsNativeManaCost()
    {
        var book = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ReadBook,
                MiscId = 25,
                SpellId = 1,
                MinimumMagic = 15,
            });
        var player = CreatePlayer(
            mana: 100,
            manaMaximum: 640,
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 20),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 0)),
            inventory: [book]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            spells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
                    ManaCostScale = 64,
                },
            ]));

        var result = processor.Execute(
            player,
            new DiabloReadBookIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(player.MemorizedSpells));
        Assert.Equal(484, player.Mana);
    }

    [Fact]
    public void ReadBookUsesTheCurrentLearnedLevelForNativeMagicRequirement()
    {
        var book = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ReadBook,
                MiscId = 25,
                SpellId = 1,
                MinimumMagic = 15,
            });
        var player = CreatePlayer(
            mana: 0,
            manaMaximum: 640,
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 17),
                new PlayerAttributeState(0, 0),
                new PlayerAttributeState(0, 0)),
            inventory: [book]);
        player.MemorizedSpells.Add(new MemorizedSpellState(1, 1));
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            spells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(1, 6, 0, 0, 0, 0) {
                    ManaCostScale = 64,
                },
            ]));

        var result = processor.Execute(
            player,
            new DiabloReadBookIntent(null, 0),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(player.MemorizedSpells));
        Assert.Single(player.Inventory);
        Assert.Equal(0, player.Mana);
    }

    [Fact]
    public void AppliesCompleteVendorLifecycleWithoutProtocolTypes()
    {
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 10,
            Magical = 1,
            Value = 100,
            IdentifiedValue = 100,
            Charges = 0,
            MaxCharges = 1,
            Durability = 5,
            MaxDurability = 20,
        };
        var catalog = CreateCatalog(new StoreItem(0, 42, 75, itemState));
        var processor = new DiabloStoreCommandProcessor(catalog);
        var player = CreatePlayer(gold: 1000);

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        Assert.True(processor.Execute(player, new DiabloPurchaseIntent(1, 0), 12).Succeeded);
        Assert.True(processor.Execute(player, new DiabloIdentifyItemIntent(null, 0), 13).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRepairItemIntent(null, 0), 14).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRechargeItemIntent(null, 0), 15).Succeeded);
        Assert.True(processor.Execute(player, new DiabloMoveInventoryItemIntent(0, 8), 16).Succeeded);

        var item = Assert.Single(player.Inventory);
        Assert.Equal(12UL, item.PurchasedAtTick);
        Assert.True(item.State.Identified);
        Assert.Equal(item.State.MaxDurability, item.State.Durability);
        Assert.Equal(item.State.MaxCharges, item.State.Charges);
        Assert.Equal(0, player.InventoryGrid[8]);
        Assert.Empty(catalog.GetItems(1));

        Assert.True(processor.Execute(player, new DiabloSellItemIntent(null, 0), 17).Succeeded);
        Assert.Empty(player.Inventory);
        Assert.All(player.InventoryGrid, cell => Assert.Equal(-1, cell));
    }

    [Fact]
    public void AppliesManaAndEquipmentServicesThroughModuleReferences()
    {
        var serviceState = AuthoritativeItemState.Empty with {
            ItemType = 10,
            Magical = 1,
            Value = 100,
            IdentifiedValue = 100,
            Charges = 0,
            MaxCharges = 1,
            Durability = 5,
            MaxDurability = 20,
        };
        var player = CreatePlayer(
            gold: 1000,
            mana: 32,
            manaMaximum: 640,
            equipment: [new EquippedStoreItem(3, 99, serviceState)]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.AdriaStoreId),
            0).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRefillManaIntent(), 1).Succeeded);
        Assert.Equal(640, player.Mana);

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.AdriaStoreId),
            2).Succeeded);
        var equipment = new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 3);
        Assert.True(processor.Execute(player, new DiabloIdentifyItemIntent(equipment, 0), 3).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRepairItemIntent(equipment, 0), 4).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRechargeItemIntent(equipment, 0), 5).Succeeded);

        var updated = Assert.Single(player.Equipment).State;
        Assert.True(updated.Identified);
        Assert.Equal(updated.MaxDurability, updated.Durability);
        Assert.Equal(updated.MaxCharges, updated.Charges);
    }

    [Fact]
    public void UsesConfiguredAdriaServiceStoreIdentity()
    {
        var player = CreatePlayer(gold: 1000, mana: 32, manaMaximum: 640);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            adriaStoreId: 73);

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(73),
            0).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRefillManaIntent(), 1).Succeeded);
        Assert.Equal(640, player.Mana);

        player.ActiveStoreId = DiabloStoreCommandProcessor.AdriaStoreId;
        Assert.False(processor.Execute(player, new DiabloRefillManaIntent(), 2).Succeeded);
    }

    [Fact]
    public void MovingHellfireConversionItemInAndOutOfEquipmentRecalculatesResources()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusHitPoints = 2 << 6,
                PlusMana = 3 << 6,
                ManaToLifePercent = 50,
            });
        var player = CreatePlayer(
            mana: 640,
            manaMaximum: 640,
            inventory: [item]);
        player.InventoryGrid[0] = 0;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var equip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0)),
            1);

        Assert.True(equip.Succeeded);
        Assert.Equal(1088, player.LifeMaximum);
        Assert.Equal(512, player.ManaMaximum);
        Assert.Equal(1088, player.Life);
        Assert.Equal(512, player.Mana);

        var unequip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            2);

        Assert.True(unequip.Succeeded);
        Assert.Equal(640, player.LifeMaximum);
        Assert.Equal(640, player.ManaMaximum);
        Assert.Equal(640, player.Life);
        Assert.Equal(640, player.Mana);
    }

    [Fact]
    public void MovingArmorInAndOutOfEquipmentRecalculatesEffectiveArmor()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                ArmorClass = 20,
                PlusArmorClass = 50,
            });
        var player = CreatePlayer(inventory: [item]);
        player.InventoryGrid[0] = 0;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var equip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0)),
            1);

        Assert.True(equip.Succeeded);
        Assert.Equal(30, player.ArmorClass);

        var unequip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            2);

        Assert.True(unequip.Succeeded);
        Assert.Equal(0, player.ArmorClass);
    }

    [Fact]
    public void MovingGetHitEquipmentInAndOutChangesIncomingDamageRule()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusGetHit = -5,
            });
        var player = CreatePlayer(inventory: [item]);
        player.InventoryGrid[0] = 0;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var equip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0)),
            1);

        Assert.True(equip.Succeeded);
        Assert.Equal(
            15,
            AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                20,
                1,
                player.Equipment));

        var unequip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            2);

        Assert.True(unequip.Succeeded);
        Assert.Equal(
            20,
            AuthoritativeEquipmentIncomingDamageRules.ApplyGetHit(
                20,
                1,
                player.Equipment));
    }

    [Fact]
    public void MovingAttributeItemInAndOutOfEquipmentRecalculatesAttributes()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusStrength = 4,
                PlusMagic = 5,
                PlusDexterity = 6,
                PlusVitality = 7,
            });
        var baseAttributes = new PlayerAttributesState(
            new PlayerAttributeState(10, 10),
            new PlayerAttributeState(20, 20),
            new PlayerAttributeState(30, 30),
            new PlayerAttributeState(40, 40));
        var player = CreatePlayer(
            inventory: [item],
            attributes: baseAttributes);
        player.InventoryGrid[0] = 0;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var equip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0)),
            1);

        Assert.True(equip.Succeeded);
        Assert.Equal(14, player.Attributes.Strength.Current);
        Assert.Equal(25, player.Attributes.Magic.Current);
        Assert.Equal(36, player.Attributes.Dexterity.Current);
        Assert.Equal(47, player.Attributes.Vitality.Current);

        var unequip = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            2);

        Assert.True(unequip.Succeeded);
        Assert.Equal(baseAttributes, player.Attributes);
    }

    [Fact]
    public void SwapsMultiCellInventoryItemsAtomically()
    {
        var large = AuthoritativeItemState.Empty with {
            ItemType = 1,
            InventoryWidth = 2,
            InventoryHeight = 1,
        };
        var small = AuthoritativeItemState.Empty with { ItemType = 2 };
        var player = CreatePlayer();
        player.Inventory.Add(new OwnedStoreItem(1, 0, 42, 10, 1, large));
        player.Inventory.Add(new OwnedStoreItem(1, 1, 43, 10, 2, small));
        player.InventoryGrid[8] = 0;
        player.InventoryGrid[9] = 0;
        player.InventoryGrid[20] = 1;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 8)),
            3);

        Assert.True(result.Succeeded);
        Assert.Equal(43U, player.Inventory[0].ItemSeed);
        Assert.Equal(42U, player.Inventory[1].ItemSeed);
        Assert.Equal(0, player.InventoryGrid[8]);
        Assert.Equal(-1, player.InventoryGrid[9]);
        Assert.Equal(1, player.InventoryGrid[20]);
        Assert.Equal(1, player.InventoryGrid[21]);
    }

    [Fact]
    public void StableItemReferenceRejectsAReboundInventorySlot()
    {
        var state = AuthoritativeItemState.Empty with {
            StableItemId = 7,
            ItemType = 1,
        };
        var player = CreatePlayer();
        player.Inventory.Add(new OwnedStoreItem(1, 0, 42, 10, 1, state));
        player.InventoryGrid[0] = 0;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    StableItemId: 8),
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    1)),
            2);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, result.Failure);
        Assert.Equal(42U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Equal(-1, player.InventoryGrid[1]);
    }

    [Fact]
    public void RejectedIntentDoesNotPartiallyMutateState()
    {
        var large = AuthoritativeItemState.Empty with {
            ItemType = 1,
            InventoryWidth = 2,
            InventoryHeight = 1,
        };
        var player = CreatePlayer();
        player.Inventory.Add(new OwnedStoreItem(1, 0, 42, 10, 1, large));
        player.InventoryGrid[8] = 0;
        player.InventoryGrid[9] = 0;
        var before = player.InventoryGrid.ToArray();
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloMoveInventoryItemIntent(0, 9),
            2);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, result.Failure);
        Assert.Equal(before, player.InventoryGrid);
        Assert.Equal(42U, Assert.Single(player.Inventory).ItemSeed);
    }

    [Fact]
    public void CanonicalGoldPurchaseUsesNativeSpendAndInventoryPlacementOrder()
    {
        var combatRules = new AuthoritativeCombatRules(10, 1, 100);
        var itemState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            Value = 100,
            IdentifiedValue = 100,
        };
        var catalog = CreateCatalog(new StoreItem(0, 42, 75, itemState));
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[39] = 0;
        grid[38] = 1;
        var player = CreatePlayer(
            gold: 5100,
            inventory: [
                CreateGoldStack(101, 5000, combatRules),
                CreateGoldStack(102, 100, combatRules),
            ],
            inventoryGrid: grid);
        var processor = new DiabloStoreCommandProcessor(
            catalog,
            combatRules: combatRules,
            random: new DiabloRandom(0));

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        var result = processor.Execute(player, new DiabloPurchaseIntent(1, 0), 12);

        Assert.True(result.Succeeded);
        Assert.Equal(5025U, player.Gold);
        Assert.Equal([101U, 102U], player.Inventory.Take(2).Select(item => item.ItemSeed));
        Assert.Equal([5000U, 25U], player.Inventory.Take(2).Select(item => item.Price));
        var purchased = player.Inventory[2];
        Assert.Equal(42U, purchased.ItemSeed);
        Assert.Equal(2, player.InventoryGrid[30]);
        Assert.Equal(1, player.InventoryGrid[38]);
        Assert.Equal(0, player.InventoryGrid[39]);
    }

    [Fact]
    public void CanonicalGoldSaleTopsOffThenCreatesNativeSeededPile()
    {
        var combatRules = new AuthoritativeCombatRules(10, 1, 100);
        var saleState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            Value = 100,
            IdentifiedValue = 100,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[38] = 0;
        grid[39] = 1;
        var player = CreatePlayer(
            gold: 4990,
            inventory: [
                CreateGoldStack(101, 4990, combatRules),
                new OwnedStoreItem(0, 0, 201, 100, 0, saleState),
            ],
            inventoryGrid: grid);
        var random = new DiabloRandom(0);
        var processor = new DiabloStoreCommandProcessor(
            CreateCatalog(),
            combatRules: combatRules,
            random: random);

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        var result = processor.Execute(player, new DiabloSellItemIntent(null, 1), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(5015U, player.Gold);
        Assert.Equal([5000U, 15U], player.Inventory.Select(item => item.Price));
        Assert.Equal([101U, 1U], player.Inventory.Select(item => item.ItemSeed));
        Assert.Equal(0, player.InventoryGrid[38]);
        Assert.Equal(1, player.InventoryGrid[39]);
        Assert.Equal(1U, random.State);
    }

    [Fact]
    public void CanonicalGoldSaleRejectsWithoutMutationWhenNoPileCapacityExists()
    {
        var combatRules = new AuthoritativeCombatRules(10, 1, 100);
        var fillerState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            Value = 100,
            IdentifiedValue = 100,
        };
        var inventory = new List<OwnedStoreItem> {
            CreateGoldStack(101, 5000, combatRules),
        };
        for (var index = 1; index < 40; index++) {
            inventory.Add(new OwnedStoreItem(
                0,
                checked((uint)index),
                checked((uint)(200 + index)),
                100,
                0,
                fillerState));
        }
        var grid = Enumerable.Range(0, 40).ToArray();
        var beltItem = new BeltStoreItem(0, 999, fillerState);
        var player = CreatePlayer(
            gold: 5000,
            inventory: inventory,
            inventoryGrid: grid,
            belt: [beltItem]);
        var random = new DiabloRandom(0);
        var processor = new DiabloStoreCommandProcessor(
            CreateCatalog(),
            combatRules: combatRules,
            random: random);

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        var beforeGrid = player.InventoryGrid.ToArray();
        var result = processor.Execute(
            player,
            new DiabloSellItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 0),
                0),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(5000U, player.Gold);
        Assert.Equal(40, player.Inventory.Count);
        Assert.Equal(beforeGrid, player.InventoryGrid);
        Assert.Equal(999U, Assert.Single(player.Belt).ItemSeed);
        Assert.Equal(0U, random.State);
    }

    [Fact]
    public void CanonicalGoldServicePreservesPostDeathWalletRemainderAndItemReference()
    {
        var combatRules = new AuthoritativeCombatRules(10, 1, 100);
        var serviceState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            Magical = 1,
            Identified = false,
            Value = 100,
            IdentifiedValue = 200,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[39] = 0;
        grid[38] = 1;
        grid[30] = 2;
        var player = CreatePlayer(
            gold: 125,
            inventory: [
                CreateGoldStack(101, 25, combatRules),
                CreateGoldStack(102, 101, combatRules),
                new OwnedStoreItem(0, 0, 201, 100, 0, serviceState),
            ],
            inventoryGrid: grid);
        var processor = new DiabloStoreCommandProcessor(
            CreateCatalog(),
            combatRules: combatRules,
            random: new DiabloRandom(0));

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        var result = processor.Execute(player, new DiabloIdentifyItemIntent(null, 2), 1);

        Assert.True(result.Succeeded);
        Assert.Equal(25U, player.Gold);
        Assert.Equal(26U, player.Inventory[0].Price);
        Assert.Equal(102U, player.Inventory[0].ItemSeed);
        Assert.True(player.Inventory[1].State.Identified);
        Assert.Equal(0, player.InventoryGrid[38]);
        Assert.Equal(1, player.InventoryGrid[30]);
        Assert.Equal(-1, player.InventoryGrid[39]);
    }

    [Fact]
    public void CanonicalGoldCannotMoveOrSwapOutsideInventory()
    {
        var combatRules = new AuthoritativeCombatRules(10, 1, 100);
        var fillerState = AuthoritativeItemState.Empty with { ItemType = 1 };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[39] = 0;
        var player = CreatePlayer(
            gold: 100,
            inventory: [CreateGoldStack(101, 100, combatRules)],
            inventoryGrid: grid,
            belt: [new BeltStoreItem(0, 201, fillerState)]);
        var processor = new DiabloStoreCommandProcessor(
            CreateCatalog(),
            combatRules: combatRules);

        var directMove = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 1)),
            1);
        var swap = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 39)),
            2);

        Assert.False(directMove.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, directMove.Failure);
        Assert.False(swap.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, swap.Failure);
        Assert.Equal(100U, player.Gold);
        Assert.Equal(101U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal(0, player.InventoryGrid[39]);
        Assert.Equal(201U, Assert.Single(player.Belt).ItemSeed);
        Assert.Empty(player.Equipment);
    }

    [Fact]
    public void SemanticGoldTagGuardsAStackFromStoreTransfer()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\n"
                + "25\t99\t0\t0\tGold\t0\n");
        var state = AuthoritativeItemState.Empty with {
            ItemType = 99,
            ItemIndex = 0,
            Value = 100,
            IdentifiedValue = 100,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[39] = 0;
        var player = CreatePlayer(
            gold: 100,
            inventory: [new OwnedStoreItem(0, 0, 101, 100, 0, state)],
            inventoryGrid: grid,
            items: items);
        var processor = new DiabloStoreCommandProcessor(
            CreateCatalog(),
            combatRules: rules,
            items: items);

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        var result = processor.Execute(player, new DiabloSellItemIntent(null, 0), 1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(100U, player.Gold);
        Assert.Single(player.Inventory);
    }

    private static StoreCatalog CreateCatalog(params StoreItem[] items)
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, items);
        return catalog;
    }

    private static AuthoritativePlayerState CreatePlayer(
        uint gold = 100,
        int mana = 0,
        int manaMaximum = 0,
        IReadOnlyList<EquippedStoreItem>? equipment = null,
        IReadOnlyList<OwnedStoreItem>? inventory = null,
        IReadOnlyList<int>? inventoryGrid = null,
        IReadOnlyList<BeltStoreItem>? belt = null,
        PlayerAttributesState? attributes = null,
        AuthoritativePlayerClassDefinition? playerClass = null,
        DiabloPlayerAbilityKind abilityKind = DiabloPlayerAbilityKind.None,
        AuthoritativeItemCatalog? items = null)
    {
        return new AuthoritativePlayerState(
            gold,
            0,
            640,
            mana,
            manaMaximum,
            attributes ?? PlayerAttributesState.Zero,
            equipment ?? [],
            inventoryGrid ?? Enumerable.Repeat(-1, 40).ToArray(),
            belt ?? [],
            0,
            0,
            640,
            1,
            0,
            0,
            inventory: inventory,
            playerClass: playerClass,
            abilityKind: abilityKind,
            items: items);
    }

    private static AuthoritativePlayerClassDefinition LoadWarriorClass()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "server", "content", "base", "classes.tsv");
        return AuthoritativePlayerClassCatalog.LoadTsv(path, File.ReadAllText(path))
            .Resolve("warrior");
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] {
                configuredRoot!,
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
            };
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

    private static OwnedStoreItem CreateGoldStack(
        uint itemSeed,
        uint value,
        AuthoritativeCombatRules rules)
    {
        var state = AuthoritativeItemState.Empty with {
            ItemType = rules.DeathGoldItemType,
            Value = checked((int)value),
            IdentifiedValue = checked((int)value),
            ItemIndex = rules.DeathGoldItemIndex,
            StatFlag = true,
        };
        return new OwnedStoreItem(0, 0, itemSeed, value, 0, state);
    }
}
