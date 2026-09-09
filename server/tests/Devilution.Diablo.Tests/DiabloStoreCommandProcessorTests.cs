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
    public void SorcererRechargeAbilityResolvesChargedMetadataByMiscId()
    {
        const uint rechargeSpellId = 27;
        var definition = new AuthoritativeItemDefinition(
            2304,
            0,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 124,
            NativeItemIndex = 104,
            SpellId = 30,
            SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            MaxCharges = 10,
            GenerationTags = new HashSet<string>(["Staff"], StringComparer.OrdinalIgnoreCase),
        };
        var item = new OwnedStoreItem(
            0,
            0,
            2304,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 124,
                Charges = 1,
                MaxCharges = 0,
                SpellId = 0,
            });
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = rechargeSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(1);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            rechargeAbilitySpellId: rechargeSpellId,
            spells: new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(30, 6, 0, 0, 0, 0) { StaffLevel = 1 },
            ]),
            items: new AuthoritativeItemCatalog([definition]));

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
    }

    [Fact]
    public void VendorRechargeUsesChargedCapacityByMiscIdWhenStateOmitsIt()
    {
        var definition = new AuthoritativeItemDefinition(
            2305,
            0,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 125,
            NativeItemIndex = 105,
            SpellId = 30,
            SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            MaxCharges = 10,
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            MiscId = 125,
            Charges = 1,
            MaxCharges = 0,
            Value = 100,
        };
        var player = CreatePlayer(
            gold: 1000,
            equipment: [new EquippedStoreItem(3, 2305, state)]);
        var pricing = new StoreServicePricing(
            spellStaffCosts: new Dictionary<int, uint> { [30] = 0 });
        var catalog = new StoreCatalog();
        catalog.AddStore(2, [], AuthoritativeStoreRole.Witch);
        var processor = new DiabloStoreCommandProcessor(
            catalog,
            rules: new DiabloStoreRules(pricing, items),
            items: items);

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(2),
            0).Succeeded);
        var result = processor.Execute(
            player,
            new DiabloRechargeItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 3),
                0),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var recharged = Assert.Single(player.Equipment).State;
        Assert.Equal(10, recharged.Charges);
        Assert.Equal(10, recharged.MaxCharges);
        Assert.Equal(1000U - 45U, player.Gold);
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
    public void WarriorRepairAbilityResolvesMaximumDurabilityByMiscId()
    {
        const uint repairSpellId = 26;
        var definition = new AuthoritativeItemDefinition(
            2306,
            1,
            100,
            100,
            0,
            0,
            0,
            20,
            20,
            1,
            1,
            true) {
            MiscId = 126,
            NativeItemIndex = 106,
        };
        var item = new OwnedStoreItem(
            0,
            0,
            2306,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 126,
                Durability = 1,
                MaxDurability = 0,
            });
        var player = CreatePlayer(gold: 0, inventory: [item]);
        player.AbilitySpellId = repairSpellId;
        player.CharacterLevel = 10;
        var random = new DiabloRandom(0);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random,
            repairAbilitySpellId: repairSpellId,
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        var repaired = Assert.Single(player.Inventory).State;
        Assert.Equal(18, repaired.Durability);
        Assert.Equal(18, repaired.MaxDurability);
        Assert.Equal(22695478U, random.State);
    }

    [Fact]
    public void VendorRepairMaterializesMaximumDurabilityByMiscId()
    {
        var definition = new AuthoritativeItemDefinition(
            2307,
            1,
            100,
            100,
            0,
            0,
            0,
            20,
            20,
            1,
            1,
            true) {
            MiscId = 127,
            NativeItemIndex = 107,
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var item = new OwnedStoreItem(
            0,
            0,
            2307,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 127,
                Durability = 1,
                MaxDurability = 0,
                Value = 100,
            });
        var player = CreatePlayer(
            gold: 1000,
            equipment: [new EquippedStoreItem(3, 2307, item.State)]);
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var processor = new DiabloStoreCommandProcessor(
            catalog,
            rules: new DiabloStoreRules(items: items),
            items: items);

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(1),
            0).Succeeded);
        var result = processor.Execute(
            player,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 3),
                0),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var repaired = Assert.Single(player.Equipment).State;
        Assert.Equal(20, repaired.Durability);
        Assert.Equal(20, repaired.MaxDurability);
        Assert.Equal(953U, player.Gold);
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
    public void IdentifyScrollResolvesAuthoredActionByMiscIdWhenNativeIdentityIsAbsent()
    {
        var definition = new AuthoritativeItemDefinition(
            2303,
            0,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 123,
            NativeItemIndex = 103,
            ItemActionKind = DiabloItemActionKind.Identify,
            Usable = true,
        };
        var target = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                Magical = 1,
                Identified = false,
            });
        var scroll = new OwnedStoreItem(
            0,
            0,
            2303,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 123,
            });
        var player = CreatePlayer(inventory: [target, scroll]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloIdentifyItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0,
                DiabloItemServiceSourceKind.ConsumableItem,
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.True(Assert.Single(player.Inventory).State.Identified);
        Assert.Equal(42U, Assert.Single(player.Inventory).ItemSeed);
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
    public void ReadBookUsesContentBookTagWhenLegacyMiscIdDiffers()
    {
        var definition = new AuthoritativeItemDefinition(
            42,
            6,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 99,
            NativeItemIndex = 42,
            ItemActionKind = DiabloItemActionKind.ReadBook,
            SpellId = 1,
            MinimumMagic = 15,
            GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
        };
        var book = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 42,
                ItemIndex = 42,
                MiscId = 99,
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
            ]),
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloReadBookIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(player.MemorizedSpells));
    }

    [Fact]
    public void ReadBookUsesContentBookTagWhenOnlyMiscIdSurvives()
    {
        var definition = new AuthoritativeItemDefinition(
            43,
            6,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 99,
            NativeItemIndex = 42,
            GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
        };
        var book = new OwnedStoreItem(
            0,
            0,
            43,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ReadBook,
                ItemIndex = 999,
                MiscId = 99,
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
            ]),
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloReadBookIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(player.MemorizedSpells));
    }

    [Fact]
    public void ReadBookResolvesSpellMetadataByMiscIdWhenStateOmitsBookFields()
    {
        var definition = new AuthoritativeItemDefinition(
            43,
            6,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 99,
            NativeItemIndex = 42,
            ItemActionKind = DiabloItemActionKind.ReadBook,
            SpellId = 1,
            MinimumMagic = 15,
            GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
        };
        var book = new OwnedStoreItem(
            0,
            0,
            43,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 99,
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
            ]),
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloReadBookIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                0),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Inventory);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(player.MemorizedSpells));
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
    public void OpenStashBookLearnsAndConsumesTheAuthoritativeSource()
    {
        var book = new StashStoreItem(
            0,
            7,
            0,
            0,
            700,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 700,
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
                new PlayerAttributeState(0, 0)));
        player.Stash.Add(book);
        player.StashOpen = true;
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
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 7, 700, 0),
                0),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Stash);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(player.MemorizedSpells));
        Assert.Equal(484, player.Mana);
    }

    [Fact]
    public void OpenStashConsumableRestoresResourcesAndConsumesTheAuthoritativeSource()
    {
        var potion = new StashStoreItem(
            0,
            8,
            0,
            0,
            701,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 701,
                ItemActionKind = DiabloItemActionKind.RestoreFullLife,
            });
        var player = CreatePlayer(life: 10);
        player.Stash.Add(potion);
        player.StashOpen = true;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 8, 701, 0)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Stash);
        Assert.Equal(640, player.Life);
    }

    [Fact]
    public void OpenStashOilCanTargetInventoryItem()
    {
        var oil = new StashStoreItem(
            0,
            9,
            0,
            0,
            702,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 702,
                ItemActionKind = DiabloItemActionKind.ApplyBlacksmithOil,
                ItemClass = 3,
            });
        var target = new OwnedStoreItem(
            0,
            0,
            703,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 703,
                ItemClass = 1,
                Durability = 5,
                MaxDurability = 20,
            });
        var player = CreatePlayer(inventory: [target]);
        player.Stash.Add(oil);
        player.StashOpen = true;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 9, 702, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 703)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Stash);
        Assert.Equal(9, Assert.Single(player.Inventory).State.Durability);
    }

    [Fact]
    public void ClosedStashConsumableSourceIsRejectedWithoutMutation()
    {
        var potion = new StashStoreItem(
            0,
            10,
            0,
            0,
            704,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 704,
                ItemActionKind = DiabloItemActionKind.RestoreFullLife,
            });
        var player = CreatePlayer(life: 10);
        player.Stash.Add(potion);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 10, 704, 0)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(10, player.Life);
        Assert.Single(player.Stash);
    }

    [Fact]
    public void FullRejuvenationConsumesInventoryItemAndRestoresBothResources()
    {
        var potion = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
                StableItemId = 42,
                StatFlag = true,
            });
        var player = CreatePlayer(
            mana: 64,
            manaMaximum: 640,
            inventory: [potion]);
        player.Life = 64;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    42)),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(640, player.Life);
        Assert.Equal(640, player.Mana);
    }

    [Fact]
    public void FullRejuvenationConsumesBeltItemThroughItsStableReference()
    {
        var potion = new BeltStoreItem(
            2,
            43,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
                StableItemId = 42,
                StatFlag = true,
            });
        var player = CreatePlayer(
            mana: 64,
            manaMaximum: 640,
            belt: [potion]);
        player.Life = 64;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 2, 42)),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Belt);
        Assert.Equal(640, player.Life);
        Assert.Equal(640, player.Mana);
    }

    [Fact]
    public void ArenaPotionIsRejectedOutsideAnActiveArena()
    {
        var potion = new OwnedStoreItem(
            0,
            0,
            2200,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
                StableItemId = 2200,
                MiscId = 55,
                StatFlag = true,
            });
        var player = CreatePlayer(mana: 64, manaMaximum: 640, inventory: [potion]);
        player.Life = 64;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 2200)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Same(potion, Assert.Single(player.Inventory));
        Assert.Equal(64, player.Life);
        Assert.Equal(64, player.Mana);
    }

    [Fact]
    public void ExplicitArenaAdmissionOverridesLegacyMiscIdFallback()
    {
        var definition = new AuthoritativeItemDefinition(
            2300,
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
            true) {
            StableSymbol = "non_arena_misc",
            MiscId = 55,
            ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
            ArenaOnly = false,
            Usable = true,
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var item = new OwnedStoreItem(
            0,
            0,
            2300,
            100,
            0,
            definition.Generate(2301));
        var player = CreatePlayer(
            mana: 64,
            manaMaximum: 640,
            inventory: [item]);
        player.Life = 64;
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 2300)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Inventory);
        Assert.Equal(640, player.Life);
        Assert.Equal(640, player.Mana);
    }

    [Fact]
    public void ArenaAdmissionUsesContentMetadataByMiscIdWhenNativeIdentityIsAbsent()
    {
        var definition = new AuthoritativeItemDefinition(
            2301,
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
            true) {
            MiscId = 121,
            NativeItemIndex = 101,
            ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
            ArenaOnly = true,
            Usable = true,
        };
        var potion = new OwnedStoreItem(
            0,
            0,
            2301,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 121,
                ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
                StatFlag = true,
            });
        var player = CreatePlayer(
            mana: 64,
            manaMaximum: 640,
            inventory: [potion]);
        player.Life = 64;
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Same(potion, Assert.Single(player.Inventory));
        Assert.Equal(64, player.Life);
        Assert.Equal(64, player.Mana);
    }

    [Fact]
    public void ExplicitlyUnusableConsumablesCannotBeUsedByMiscId()
    {
        var definition = new AuthoritativeItemDefinition(
            2302,
            0,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            MiscId = 122,
            NativeItemIndex = 102,
            ItemActionKind = DiabloItemActionKind.RestoreFullLife,
            Usable = false,
        };
        var potion = new OwnedStoreItem(
            0,
            0,
            2302,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 122,
                ItemActionKind = DiabloItemActionKind.RestoreFullLife,
            });
        var player = CreatePlayer(mana: 64, manaMaximum: 640, inventory: [potion]);
        player.Life = 64;
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Same(potion, Assert.Single(player.Inventory));
        Assert.Equal(64, player.Life);
    }

    [Fact]
    public void ArenaPotionRestoresAndConsumesOnAnActiveArena()
    {
        var potion = new OwnedStoreItem(
            0,
            0,
            2200,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.RestoreFullLifeAndMana,
                StableItemId = 2200,
                MiscId = 55,
                StatFlag = true,
            });
        var player = CreatePlayer(mana: 64, manaMaximum: 640, inventory: [potion]);
        player.LevelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(6, activeMap: true);
        player.Life = 64;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 2200)),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(640, player.Life);
        Assert.Equal(640, player.Mana);
    }

    [Fact]
    public void WarriorPartialHealingUsesNativeQ6RandomAndClassMultiplier()
    {
        var potion = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.RestorePartialLife,
                StatFlag = true,
            });
        var player = CreatePlayer(
            playerClass: LoadWarriorClass(),
            inventory: [potion]);
        player.Life = 0;
        var random = new DiabloRandom(0);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: random);

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(128, player.Life);
        Assert.Equal(1U, random.State);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void ElixirOfStrengthIncreasesTheNativeBaseAndCurrentAttributes()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            43,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.IncreaseStrength,
            });
        var player = CreatePlayer(
            playerClass: LoadClass("warrior"),
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(10, 10),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(25, 25)),
            inventory: [elixir]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(31, player.Attributes.Strength.Base);
        Assert.Equal(31, player.Attributes.Strength.Current);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void HellfireSpectralElixirIncreasesAllAttributesAndNativeResources()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            47,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.IncreaseAllAttributes,
                StableItemId = 2142,
            });
        var player = CreatePlayer(
            life: 100,
            mana: 100,
            manaMaximum: 640,
            playerClass: LoadClass("warrior"),
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(10, 10),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(25, 25)),
            inventory: [elixir]);
        var initialLifeMaximum = player.LifeMaximum;
        var initialManaMaximum = player.ManaMaximum;
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            "item_id\titem_type\tvalue\tidentified_value\titem_tags\tnative_item_index\titem_action_kind\titem_action_amount\n"
                + "2142\t0\t0\t0\tMisc\t20\tIncreaseAllAttributes\t3\n");
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            combatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(33, player.Attributes.Strength.Base);
        Assert.Equal(13, player.Attributes.Magic.Base);
        Assert.Equal(23, player.Attributes.Dexterity.Base);
        Assert.Equal(28, player.Attributes.Vitality.Base);
        Assert.True(player.LifeMaximum > initialLifeMaximum);
        Assert.True(player.ManaMaximum > initialManaMaximum);
        Assert.True(player.Life > 100);
        Assert.True(player.Mana > 100);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void ItemActionAmountResolvesThroughNativeItemIndex()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            47,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 20,
            });
        var player = CreatePlayer(
            playerClass: LoadClass("warrior"),
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(10, 10),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(25, 25)),
            inventory: [elixir]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2142, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 20,
                ItemActionKind = DiabloItemActionKind.IncreaseAllAttributes,
                ItemActionAmount = 2,
                GenerationTags = new HashSet<string>(["Misc"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(32, player.Attributes.Strength.Base);
        Assert.Equal(12, player.Attributes.Magic.Base);
        Assert.Equal(22, player.Attributes.Dexterity.Base);
        Assert.Equal(27, player.Attributes.Vitality.Base);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void ItemActionMetadataResolvesByMiscIdWhenNativeIdentityIsAbsent()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            48,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 121,
            });
        var player = CreatePlayer(
            playerClass: LoadClass("warrior"),
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(10, 10),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(25, 25)),
            inventory: [elixir]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2143, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 20,
                MiscId = 121,
                ItemActionKind = DiabloItemActionKind.IncreaseAllAttributes,
                ItemActionAmount = 2,
                GenerationTags = new HashSet<string>(["Misc"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(32, player.Attributes.Strength.Base);
        Assert.Equal(12, player.Attributes.Magic.Base);
        Assert.Equal(22, player.Attributes.Dexterity.Base);
        Assert.Equal(27, player.Attributes.Vitality.Base);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void AmbiguousItemActionMetadataByMiscIdFailsClosed()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            49,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 121,
            });
        var player = CreatePlayer(inventory: [elixir]);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(2144, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 20,
                MiscId = 121,
                ItemActionKind = DiabloItemActionKind.IncreaseStrength,
                ItemActionAmount = 1,
            },
            new AuthoritativeItemDefinition(2145, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                NativeItemIndex = 21,
                MiscId = 121,
                ItemActionKind = DiabloItemActionKind.IncreaseStrength,
                ItemActionAmount = 2,
            },
        ]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Single(player.Inventory);
    }

    [Fact]
    public void HellfireElixirOfMagicIncreasesResourcesAndRestoresMana()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            44,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.IncreaseMagic,
            });
        var player = CreatePlayer(
            mana: 100,
            manaMaximum: 640,
            playerClass: LoadClass("sorcerer"),
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(15, 15),
                new PlayerAttributeState(35, 35),
                new PlayerAttributeState(15, 15),
                new PlayerAttributeState(20, 20)),
            inventory: [elixir]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            combatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(36, player.Attributes.Magic.Base);
        Assert.Equal(768, player.BaseManaMaximum);
        Assert.Equal(768, player.ManaMaximum);
        Assert.Equal(768, player.Mana);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void NoManaSuppressesHellfireMagicElixirRestoration()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            45,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.IncreaseMagic,
            });
        var noMana = new EquippedStoreItem(
            0,
            99,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Flags = AuthoritativeEquipmentManaRules.NoManaFlag,
            });
        var player = CreatePlayer(
            mana: 100,
            manaMaximum: 640,
            equipment: [noMana],
            playerClass: LoadClass("sorcerer"),
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(15, 15),
                new PlayerAttributeState(35, 35),
                new PlayerAttributeState(15, 15),
                new PlayerAttributeState(20, 20)),
            inventory: [elixir]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            combatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(768, player.ManaMaximum);
        Assert.Equal(100, player.Mana);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void HellfireElixirOfVitalityIncreasesResourcesAndRestoresLife()
    {
        var elixir = new OwnedStoreItem(
            0,
            0,
            46,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.IncreaseVitality,
            });
        var player = CreatePlayer(
            life: 100,
            playerClass: LoadClass("warrior"),
            attributes: new PlayerAttributesState(
                new PlayerAttributeState(30, 30),
                new PlayerAttributeState(10, 10),
                new PlayerAttributeState(20, 20),
                new PlayerAttributeState(25, 25)),
            inventory: [elixir]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            combatRules: new AuthoritativeCombatRules(
                10,
                1,
                100,
                gameVariant: DiabloGameVariant.Hellfire));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(26, player.Attributes.Vitality.Base);
        Assert.Equal(768, player.BaseLifeMaximum);
        Assert.Equal(768, player.LifeMaximum);
        Assert.Equal(768, player.Life);
        Assert.Empty(player.Inventory);
    }

    [Theory]
    [InlineData(DiabloItemActionKind.ApplyBlacksmithOil, 5, 20, 0, 0, 0, 9, 20)]
    [InlineData(DiabloItemActionKind.ApplySharpnessOil, 0, 0, 5, 10, 0, 0, 0)]
    public void OilActionsMutateTheReferencedTargetAndConsumeTheSource(
        DiabloItemActionKind action,
        int durability,
        int maxDurability,
        int minDamage,
        int maxDamage,
        int plusToHit,
        int expectedDurability,
        int expectedMaxDurability)
    {
        var oil = new OwnedStoreItem(
            0,
            0,
            70,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = action,
                ItemClass = 3,
            });
        var target = new OwnedStoreItem(
            0,
            0,
            71,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemClass = 1,
                Durability = durability,
                MaxDurability = maxDurability,
                MinDamage = minDamage,
                MaxDamage = maxDamage,
                PlusToHit = plusToHit,
            });
        var player = CreatePlayer(inventory: [oil, target]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: new DiabloRandom(0));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.True(result.Succeeded);
        var applied = Assert.Single(player.Inventory);
        Assert.Equal(71U, applied.ItemSeed);
        Assert.Equal(expectedDurability, applied.State.Durability);
        Assert.Equal(expectedMaxDurability, applied.State.MaxDurability);
        if (action == DiabloItemActionKind.ApplySharpnessOil)
            Assert.Equal(11, applied.State.MaxDamage);
    }

    [Fact]
    public void OilCanTargetAnOpenStashItem()
    {
        var oil = new OwnedStoreItem(
            0,
            0,
            80,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 80,
                ItemActionKind = DiabloItemActionKind.ApplyBlacksmithOil,
                ItemClass = 3,
            });
        var target = new StashStoreItem(
            0,
            7,
            0,
            0,
            81,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 81,
                ItemClass = 1,
                Durability = 5,
                MaxDurability = 20,
            });
        var player = CreatePlayer(inventory: [oil]);
        player.Stash.Add(target);
        player.StashOpen = true;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 7, 81, 0)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Empty(player.Inventory);
        var applied = Assert.Single(player.Stash);
        Assert.Equal(9, applied.State.Durability);
        Assert.Equal(20, applied.State.MaxDurability);
    }

    [Fact]
    public void OilRejectsAClosedStashTargetWithoutConsumingTheSource()
    {
        var oil = new OwnedStoreItem(
            0,
            0,
            82,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 82,
                ItemActionKind = DiabloItemActionKind.ApplyBlacksmithOil,
                ItemClass = 3,
            });
        var target = new StashStoreItem(
            0,
            8,
            0,
            0,
            83,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 83,
                ItemClass = 1,
                Durability = 5,
                MaxDurability = 20,
            });
        var player = CreatePlayer(inventory: [oil]);
        player.Stash.Add(target);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 8, 83, 0)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Single(player.Inventory);
        Assert.Equal(5, Assert.Single(player.Stash).State.Durability);
    }

    [Fact]
    public void BlacksmithOilResolvesTargetDurabilityByMiscId()
    {
        var oil = new OwnedStoreItem(
            0,
            0,
            2308,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ApplyBlacksmithOil,
                ItemClass = 3,
            });
        var targetDefinition = new AuthoritativeItemDefinition(
            2309,
            1,
            100,
            100,
            0,
            0,
            0,
            20,
            20,
            1,
            1,
            true) {
            MiscId = 128,
            NativeItemIndex = 108,
            GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
        };
        var target = new OwnedStoreItem(
            0,
            0,
            2309,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 128,
                ItemClass = 1,
                Durability = 5,
                MaxDurability = 0,
            });
        var player = CreatePlayer(inventory: [oil, target]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: new AuthoritativeItemCatalog([targetDefinition]));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var applied = Assert.Single(player.Inventory);
        Assert.Equal(2309U, applied.ItemSeed);
        Assert.Equal(9, applied.State.Durability);
        Assert.Equal(20, applied.State.MaxDurability);
    }

    [Fact]
    public void SharpnessOilResolvesTargetDamageByMiscId()
    {
        var oil = new OwnedStoreItem(
            0,
            0,
            2310,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ApplySharpnessOil,
                ItemClass = 3,
            });
        var targetDefinition = new AuthoritativeItemDefinition(
            2311,
            1,
            100,
            100,
            5,
            10,
            0,
            20,
            20,
            1,
            1,
            true) {
            MiscId = 129,
            NativeItemIndex = 109,
            GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
        };
        var target = new OwnedStoreItem(
            0,
            0,
            2311,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemIndex = 999,
                MiscId = 129,
                ItemClass = 1,
                MinDamage = 0,
                MaxDamage = 0,
            });
        var player = CreatePlayer(inventory: [oil, target]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: new AuthoritativeItemCatalog([targetDefinition]));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var applied = Assert.Single(player.Inventory);
        Assert.Equal(5, applied.State.MinDamage);
        Assert.Equal(11, applied.State.MaxDamage);
    }

    [Fact]
    public void AccuracyAndFortitudeOilsUseNativeBoundedRandomEffects()
    {
        var accuracy = new OwnedStoreItem(
            0,
            0,
            72,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ApplyAccuracyOil,
                ItemClass = 3,
            });
        var target = new OwnedStoreItem(
            0,
            0,
            73,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemClass = 1,
                PlusToHit = 0,
            });
        var player = CreatePlayer(inventory: [accuracy, target]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: new DiabloRandom(0));

        Assert.True(processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1).Succeeded);
        Assert.InRange(Assert.Single(player.Inventory).State.PlusToHit, 1, 2);

        var fortitude = new OwnedStoreItem(
            0,
            0,
            74,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ApplyFortitudeOil,
                ItemClass = 3,
            });
        var durabilityTarget = new OwnedStoreItem(
            0,
            0,
            75,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemClass = 2,
                Durability = 10,
                MaxDurability = 20,
            });
        player.Inventory.Add(fortitude);
        player.Inventory.Add(durabilityTarget);
        Assert.True(processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 2)),
            2).Succeeded);
        var applied = Assert.Single(player.Inventory, item => item.ItemSeed == 75);
        Assert.InRange(applied.State.MaxDurability, 30, 70);
        Assert.Equal(applied.State.MaxDurability - 10, applied.State.Durability);
    }

    [Fact]
    public void OilTargetTagsOverrideConflictingLegacyItemClassByMiscId()
    {
        var oil = new OwnedStoreItem(
            0,
            0,
            78,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ApplyAccuracyOil,
                ItemClass = 3,
            });
        var targetDefinition = new AuthoritativeItemDefinition(
            910,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            ItemClass = 3,
            MiscId = 121,
            NativeItemIndex = 101,
            GenerationTags = new HashSet<string>(["Weapon"], StringComparer.OrdinalIgnoreCase),
        };
        var target = new OwnedStoreItem(
            0,
            0,
            79,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemClass = 3,
                ItemIndex = 999,
                MiscId = 121,
                PlusToHit = 0,
            });
        var player = CreatePlayer(inventory: [oil, target]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: new DiabloRandom(0),
            items: new AuthoritativeItemCatalog([targetDefinition]));

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            1);

        Assert.True(result.Succeeded);
        Assert.InRange(Assert.Single(player.Inventory).State.PlusToHit, 1, 2);
    }

    [Fact]
    public void OilRejectsMissingOrIncompatibleTargetsWithoutConsumingTheSource()
    {
        var oil = new OwnedStoreItem(
            0,
            0,
            76,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemActionKind = DiabloItemActionKind.ApplyAccuracyOil,
                ItemClass = 3,
            });
        var armor = new OwnedStoreItem(
            0,
            0,
            77,
            100,
            0,
            AuthoritativeItemState.Empty with { ItemClass = 2 });
        var player = CreatePlayer(inventory: [oil, armor]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var missing = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            1);
        var incompatible = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 1)),
            2);

        Assert.False(missing.Succeeded);
        Assert.False(incompatible.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, missing.Failure);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, incompatible.Failure);
        Assert.Equal(2, player.Inventory.Count);
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
    public void NativeVendorRolesConstrainRepairAndRechargeServices()
    {
        var repairState = AuthoritativeItemState.Empty with {
            ItemType = 1,
            Value = 100,
            IdentifiedValue = 100,
            Durability = 5,
            MaxDurability = 20,
        };
        var rechargeState = AuthoritativeItemState.Empty with {
            ItemType = 10,
            Value = 100,
            IdentifiedValue = 100,
            Charges = 0,
            MaxCharges = 10,
        };

        var invalidRepairCatalog = new StoreCatalog();
        invalidRepairCatalog.AddStore(4, [], AuthoritativeStoreRole.Healer);
        var invalidRepairPlayer = CreatePlayer(
            gold: 1000,
            inventory: [new OwnedStoreItem(0, 0, 401, 100, 0, repairState)],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var invalidRepairProcessor = new DiabloStoreCommandProcessor(invalidRepairCatalog);
        Assert.True(invalidRepairProcessor.Execute(
            invalidRepairPlayer,
            new DiabloOpenStoreIntent(4),
            0).Succeeded);
        var invalidRepair = invalidRepairProcessor.Execute(
            invalidRepairPlayer,
            new DiabloRepairItemIntent(null, 0),
            1);
        Assert.False(invalidRepair.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, invalidRepair.Failure);
        Assert.Equal(5, Assert.Single(invalidRepairPlayer.Inventory).State.Durability);

        var validRepairCatalog = new StoreCatalog();
        validRepairCatalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var validRepairPlayer = CreatePlayer(
            gold: 1000,
            inventory: [new OwnedStoreItem(0, 0, 402, 100, 0, repairState)],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var validRepairProcessor = new DiabloStoreCommandProcessor(validRepairCatalog);
        Assert.True(validRepairProcessor.Execute(
            validRepairPlayer,
            new DiabloOpenStoreIntent(1),
            0).Succeeded);
        Assert.True(validRepairProcessor.Execute(
            validRepairPlayer,
            new DiabloRepairItemIntent(null, 0),
            1).Succeeded);
        Assert.Equal(20, Assert.Single(validRepairPlayer.Inventory).State.Durability);

        var invalidRechargeCatalog = new StoreCatalog();
        invalidRechargeCatalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var invalidRechargePlayer = CreatePlayer(
            gold: 1000,
            inventory: [new OwnedStoreItem(0, 0, 403, 100, 0, rechargeState)],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var invalidRechargeProcessor = new DiabloStoreCommandProcessor(invalidRechargeCatalog);
        Assert.True(invalidRechargeProcessor.Execute(
            invalidRechargePlayer,
            new DiabloOpenStoreIntent(1),
            0).Succeeded);
        var invalidRecharge = invalidRechargeProcessor.Execute(
            invalidRechargePlayer,
            new DiabloRechargeItemIntent(null, 0),
            1);
        Assert.False(invalidRecharge.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, invalidRecharge.Failure);
        Assert.Equal(0, Assert.Single(invalidRechargePlayer.Inventory).State.Charges);

        var validRechargeCatalog = new StoreCatalog();
        validRechargeCatalog.AddStore(2, [], AuthoritativeStoreRole.Witch);
        var validRechargePlayer = CreatePlayer(
            gold: 1000,
            inventory: [new OwnedStoreItem(0, 0, 404, 100, 0, rechargeState)],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var validRechargeProcessor = new DiabloStoreCommandProcessor(validRechargeCatalog);
        Assert.True(validRechargeProcessor.Execute(
            validRechargePlayer,
            new DiabloOpenStoreIntent(2),
            0).Succeeded);
        Assert.True(validRechargeProcessor.Execute(
            validRechargePlayer,
            new DiabloRechargeItemIntent(null, 0),
            1).Succeeded);
        Assert.Equal(10, Assert.Single(validRechargePlayer.Inventory).State.Charges);
    }

    [Fact]
    public void AdriaServiceStoreRejectsVendorMutations()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            405,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                Value = 100,
                IdentifiedValue = 100,
                Magical = 1,
                Identified = false,
                Durability = 5,
                MaxDurability = 20,
            });
        var catalog = new StoreCatalog();
        var player = CreatePlayer(
            gold: 1000,
            inventory: [item],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var processor = new DiabloStoreCommandProcessor(catalog);

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.AdriaStoreId),
            0).Succeeded);
        var sale = processor.Execute(player, new DiabloSellItemIntent(null, 0), 1);
        var repair = processor.Execute(player, new DiabloRepairItemIntent(null, 0), 2);
        var identify = processor.Execute(player, new DiabloIdentifyItemIntent(null, 0), 3);

        Assert.False(sale.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, sale.Failure);
        Assert.False(repair.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, repair.Failure);
        Assert.False(identify.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, identify.Failure);
        Assert.Single(player.Inventory);
        Assert.False(player.Inventory[0].State.Identified);
        Assert.Equal(1000U, player.Gold);
    }

    [Fact]
    public void VendorRoleRejectsSalesThatNativeVendorsDoNotBuy()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            77,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemClass = 1,
                ItemType = 1,
                ItemIndex = 1,
                Value = 100,
                IdentifiedValue = 100,
            });

        foreach (var (storeId, role) in new[] {
            (3U, AuthoritativeStoreRole.Wirt),
            (4U, AuthoritativeStoreRole.Healer),
        }) {
            var catalog = new StoreCatalog();
            catalog.AddStore(storeId, [], role);
            var player = CreatePlayer(
                gold: 100,
                inventory: [item],
                inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
            var processor = new DiabloStoreCommandProcessor(catalog);

            Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(storeId), 0).Succeeded);
            var result = processor.Execute(player, new DiabloSellItemIntent(null, 0), 1);

            Assert.False(result.Succeeded);
            Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
            Assert.Single(player.Inventory);
            Assert.Equal(100U, player.Gold);
        }
    }

    [Fact]
    public void SmithAndWitchUseTheirNativeMiscAndOilBuyFilters()
    {
        var potion = new OwnedStoreItem(
            0,
            0,
            77,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemClass = 3,
                ItemType = 0,
                MiscId = 3,
                ItemIndex = 105,
                Value = 100,
                IdentifiedValue = 100,
            });
        var oil = potion with {
            ItemSeed = 78,
            State = potion.State with { MiscId = 37 },
        };

        var smithCatalog = new StoreCatalog();
        smithCatalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var smith = CreatePlayer(
            inventory: [potion],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var smithProcessor = new DiabloStoreCommandProcessor(smithCatalog);
        Assert.True(smithProcessor.Execute(smith, new DiabloOpenStoreIntent(1), 0).Succeeded);
        Assert.False(smithProcessor.Execute(smith, new DiabloSellItemIntent(null, 0), 1).Succeeded);

        var witchCatalog = new StoreCatalog();
        witchCatalog.AddStore(2, [], AuthoritativeStoreRole.Witch);
        var witch = CreatePlayer(
            inventory: [potion],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var witchProcessor = new DiabloStoreCommandProcessor(witchCatalog);
        Assert.True(witchProcessor.Execute(witch, new DiabloOpenStoreIntent(2), 0).Succeeded);
        Assert.True(witchProcessor.Execute(witch, new DiabloSellItemIntent(null, 0), 1).Succeeded);

        var oilCatalog = new StoreCatalog();
        oilCatalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        var smithOil = CreatePlayer(
            inventory: [oil],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var smithOilProcessor = new DiabloStoreCommandProcessor(oilCatalog);
        Assert.True(smithOilProcessor.Execute(smithOil, new DiabloOpenStoreIntent(1), 0).Succeeded);
        Assert.True(smithOilProcessor.Execute(smithOil, new DiabloSellItemIntent(null, 0), 1).Succeeded);

        var witchOilCatalog = new StoreCatalog();
        witchOilCatalog.AddStore(2, [], AuthoritativeStoreRole.Witch);
        var witchOil = CreatePlayer(
            inventory: [oil],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var witchOilProcessor = new DiabloStoreCommandProcessor(witchOilCatalog);
        Assert.True(witchOilProcessor.Execute(witchOil, new DiabloOpenStoreIntent(2), 0).Succeeded);
        Assert.False(witchOilProcessor.Execute(witchOil, new DiabloSellItemIntent(null, 0), 1).Succeeded);
    }

    [Fact]
    public void StaffSalesMatchNativeSmithAndWitchHellfireFilters()
    {
        var invalidStaff = AuthoritativeItemState.Empty with {
            ItemType = 10,
            ItemClass = 1,
            ItemIndex = 50,
        };
        var validSpells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 50, 0, 0, 0, 0),
        ]);

        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            invalidStaff,
            null,
            null,
            DiabloGameVariant.Diablo));
        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            invalidStaff,
            null,
            null,
            DiabloGameVariant.Diablo));
        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            invalidStaff,
            null,
            null,
            DiabloGameVariant.Hellfire));
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            invalidStaff,
            null,
            null,
            DiabloGameVariant.Hellfire));

        var validStaff = invalidStaff with { SpellId = 1 };
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            validStaff,
            null,
            validSpells,
            DiabloGameVariant.Hellfire));
        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            validStaff,
            null,
            validSpells,
            DiabloGameVariant.Hellfire));

        var nonQuestClass = invalidStaff with {
            ItemType = 1,
            ItemClass = 1,
            ItemIndex = 6,
        };
        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            nonQuestClass,
            null,
            null,
            DiabloGameVariant.Diablo));
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            nonQuestClass,
            null,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void NormalizedNoSaleTagOverridesLegacyItemIndexFallback()
    {
        var definition = new AuthoritativeItemDefinition(
            34,
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
            true) {
            NativeItemIndex = 33,
            GenerationTags = new HashSet<string>(["Misc", "NoSale"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            StableItemId = 34,
            ItemIndex = 999,
            ItemType = 0,
            ItemClass = 0,
        };

        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void MiscIdOnlyNoSaleTagOverridesLegacyVendorClassification()
    {
        var definition = new AuthoritativeItemDefinition(
            34,
            1,
            100,
            100,
            1,
            4,
            0,
            20,
            20,
            1,
            1,
            true) {
            NativeItemIndex = 33,
            MiscId = 44,
            GenerationTags = new HashSet<string>(["Weapon", "NoSale"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            ItemType = 1,
            ItemClass = 1,
            MiscId = 44,
        };

        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void MiscIdOnlyMiscTagOverridesLegacyVendorClassification()
    {
        var definition = new AuthoritativeItemDefinition(
            2186,
            1,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            ItemClass = 1,
            NativeItemIndex = 101,
            MiscId = 3,
            GenerationTags = new HashSet<string>(["Misc", "Belt"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            ItemType = 1,
            ItemClass = 1,
            MiscId = 3,
        };

        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void NonMiscAndNonQuestTagsOverrideConflictingLegacyVendorClasses()
    {
        var miscConflict = new AuthoritativeItemDefinition(
            2401,
            1,
            100,
            100,
            1,
            4,
            0,
            20,
            20,
            1,
            1,
            true) {
            ItemClass = 3,
            NativeItemIndex = 501,
            MiscId = 131,
            GenerationTags = new HashSet<string>(["Weapon", "Sword"], StringComparer.OrdinalIgnoreCase),
        };
        var questConflict = new AuthoritativeItemDefinition(
            2402,
            1,
            100,
            100,
            1,
            4,
            0,
            20,
            20,
            1,
            1,
            true) {
            ItemClass = 5,
            NativeItemIndex = 502,
            MiscId = 132,
            GenerationTags = new HashSet<string>(["Weapon", "Sword"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([miscConflict, questConflict]);

        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            miscConflict.Generate(0x12345678),
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            miscConflict.Generate(0x12345678),
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            questConflict.Generate(0x23456789),
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            questConflict.Generate(0x23456789),
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void NormalizedOilActionUsesContentMetadataForVendorFilters()
    {
        var definition = new AuthoritativeItemDefinition(
            2186,
            0,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            ItemClass = 3,
            ItemActionKind = DiabloItemActionKind.ApplyBlacksmithOil,
            NativeItemIndex = 999,
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = definition.Generate(0x12345678) with { MiscId = 0 };

        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void NormalizedItemMetadataSuppressesLegacyOilMiscFallback()
    {
        var definition = new AuthoritativeItemDefinition(
            2186,
            0,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            ItemClass = 3,
            MiscId = 37,
            NativeItemIndex = 999,
            GenerationTags = new HashSet<string>(["Misc"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = definition.Generate(0x12345678);

        Assert.False(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Witch,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void NormalizedItemMetadataSuppressesLegacyLazarusIndexFallback()
    {
        var definition = new AuthoritativeItemDefinition(
            2401,
            1,
            100,
            100,
            1,
            4,
            0,
            20,
            20,
            1,
            1,
            true) {
            ItemClass = 1,
            NativeItemIndex = 33,
            GenerationTags = new HashSet<string>(["Weapon", "Sword"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([definition]);

        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            definition.Generate(0x12345678),
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void NormalizedItemClassificationReplacesLegacyQuestIndexRange()
    {
        var definition = new AuthoritativeItemDefinition(
            2400,
            1,
            100,
            100,
            1,
            4,
            0,
            20,
            20,
            1,
            1,
            true) {
            ItemClass = 1,
            NativeItemIndex = 6,
            GenerationTags = new HashSet<string>(["Weapon", "Sword"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([definition]);

        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            definition.Generate(0x12345678),
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void MiscIdOnlyQuestTagsOverrideLegacyQuestItemClass()
    {
        var definition = new AuthoritativeItemDefinition(
            2400,
            1,
            100,
            100,
            1,
            4,
            0,
            20,
            20,
            1,
            1,
            true) {
            ItemClass = 1,
            NativeItemIndex = 6,
            MiscId = 7,
            GenerationTags = new HashSet<string>(["Weapon", "Sword"], StringComparer.OrdinalIgnoreCase),
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            ItemType = 1,
            ItemClass = 5,
            MiscId = 7,
        };

        Assert.True(AuthoritativeStoreSalePolicy.Accepts(
            AuthoritativeStoreRole.Smith,
            state,
            items,
            null,
            DiabloGameVariant.Diablo));
    }

    [Fact]
    public void CloseStoreClearsActiveStoreAndIsIdempotent()
    {
        var processor = new DiabloStoreCommandProcessor(CreateCatalog());
        var player = CreatePlayer();

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 0).Succeeded);
        Assert.Equal(1U, player.ActiveStoreId);

        var closed = processor.Execute(player, new DiabloCloseStoreIntent(), 1);
        var repeated = processor.Execute(player, new DiabloCloseStoreIntent(), 2);

        Assert.True(closed.Succeeded);
        Assert.True(repeated.Succeeded);
        Assert.Null(player.ActiveStoreId);
    }

    [Fact]
    public void MovesInventoryItemIntoAndOutOfAuthoritativeStashPage()
    {
        var state = AuthoritativeItemState.Empty with {
            StableItemId = 701,
            ItemType = 1,
            InventoryWidth = 2,
            InventoryHeight = 1,
            ItemIndex = 12,
        };
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(9, 4, 1234, 77, 55, state)],
            inventoryGrid: [0, 0, -1, -1]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());
        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 0).Succeeded);

        var toStash = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    state.StableItemId),
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    11,
                    state.StableItemId,
                    3)),
            100);

        Assert.True(toStash.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.All(player.InventoryGrid, cell => Assert.Equal(-1, cell));
        var stashed = Assert.Single(player.Stash);
        Assert.Equal((uint)3, stashed.Page);
        Assert.Equal((uint)11, stashed.Cell);
        Assert.Equal((uint)1234, stashed.ItemSeed);
        Assert.Equal((uint)9, stashed.StoreId);
        Assert.Equal((ulong)55, stashed.PurchasedAtTick);

        var toInventory = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Stash,
                    11,
                    state.StableItemId,
                    3),
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    2,
                    state.StableItemId)),
            101);

        Assert.True(toInventory.Succeeded);
        Assert.Empty(player.Stash);
        var restored = Assert.Single(player.Inventory);
        Assert.Equal((uint)1234, restored.ItemSeed);
        Assert.Equal((uint)9, restored.StoreId);
        Assert.Equal((ulong)55, restored.PurchasedAtTick);
        Assert.Equal([-1, -1, 0, 0], player.InventoryGrid);
    }

    [Fact]
    public void InventoryMoveHydratesFootprintByMiscIdWhenStateOmitsDimensions()
    {
        var definition = new AuthoritativeItemDefinition(
            7,
            1,
            100,
            100,
            0,
            0,
            0,
            0,
            0,
            2,
            1,
            true) {
            NativeItemIndex = 101,
            MiscId = 121,
        };
        var state = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            MiscId = 121,
        };
        var inventoryGrid = Enumerable.Repeat(-1, 40).ToArray();
        inventoryGrid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 1234, 77, 0, state)],
            inventoryGrid: inventoryGrid);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: new AuthoritativeItemCatalog([definition]));

        var result = processor.Execute(
            player,
            new DiabloMoveInventoryItemIntent(0, 2),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        var moved = Assert.Single(player.Inventory);
        Assert.Equal(2, moved.State.InventoryWidth);
        Assert.Equal(1, moved.State.InventoryHeight);
        Assert.Equal(-1, player.InventoryGrid[0]);
        Assert.Equal(0, player.InventoryGrid[2]);
        Assert.Equal(0, player.InventoryGrid[3]);
    }

    [Fact]
    public void StashMoveRejectsOverlappingFootprintAndPreservesState()
    {
        var firstState = AuthoritativeItemState.Empty with {
            StableItemId = 702,
            ItemType = 1,
            InventoryWidth = 2,
            InventoryHeight = 1,
            ItemIndex = 13,
        };
        var secondState = firstState with { StableItemId = 703, ItemIndex = 14 };
        var player = CreatePlayer(
            inventory: [
                new OwnedStoreItem(0, 0, 200, 1, 0, firstState),
                new OwnedStoreItem(0, 1, 201, 1, 0, secondState),
            ],
            inventoryGrid: [0, 0, 1, 1]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());
        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 0).Succeeded);

        Assert.True(processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 702),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 10, 702)),
            1).Succeeded);
        var before = player.Stash.ToArray();

        var rejected = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 703),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 9, 703)),
            2);

        Assert.False(rejected.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, rejected.Failure);
        Assert.Equal(before, player.Stash);
        Assert.Single(player.Inventory);
    }

    [Fact]
    public void StashMoveSwapsWithItemCoveredByDestinationCell()
    {
        var inventoryState = AuthoritativeItemState.Empty with {
            StableItemId = 704,
            ItemType = 1,
            ItemIndex = 15,
            InventoryWidth = 1,
            InventoryHeight = 1,
        };
        var stashState = inventoryState with { StableItemId = 705, ItemIndex = 16 };
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 300, 1, 0, inventoryState)],
            inventoryGrid: [0],
            belt: [],
            equipment: []);
        player.Stash.Add(new StashStoreItem(0, 10, 0, 1, 301, 2, 0, stashState));
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());
        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 0).Succeeded);

        var result = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 704),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 10, 704)),
            3);

        Assert.True(result.Succeeded);
        Assert.Equal(301U, Assert.Single(player.Inventory).ItemSeed);
        var stashed = Assert.Single(player.Stash);
        Assert.Equal(300U, stashed.ItemSeed);
        Assert.Equal((uint)10, stashed.Cell);
    }

    [Fact]
    public void RepairAbilityCanTargetAnAuthoritativeStashItem()
    {
        const uint repairSpellId = 26;
        var state = AuthoritativeItemState.Empty with {
            StableItemId = 706,
            ItemType = 1,
            ItemIndex = 17,
            Durability = 1,
            MaxDurability = 20,
        };
        var player = CreatePlayer(gold: 0);
        player.AbilitySpellId = repairSpellId;
        player.CharacterLevel = 10;
        player.Stash.Add(new StashStoreItem(2, 13, 0, 0, 1234, 1, 0, state));
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            random: new DiabloRandom(1),
            repairAbilitySpellId: repairSpellId);
        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 0).Succeeded);

        var result = processor.Execute(
            player,
            new DiabloRepairItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 13, state.StableItemId, 2),
                0,
                DiabloItemServiceSourceKind.ClassAbility),
            1);

        Assert.True(result.Succeeded);
        var repaired = Assert.Single(player.Stash);
        Assert.Equal((uint)2, repaired.Page);
        Assert.Equal((uint)13, repaired.Cell);
        Assert.True(repaired.State.Durability > state.Durability);
        Assert.True(repaired.State.MaxDurability < state.MaxDurability);
    }

    [Fact]
    public void ItemServicesRejectClosedStashTargets()
    {
        var repairState = AuthoritativeItemState.Empty with {
            StableItemId = 707,
            Durability = 1,
            MaxDurability = 20,
        };
        var repairPlayer = CreatePlayer(gold: 0);
        repairPlayer.AbilitySpellId = 26;
        repairPlayer.Stash.Add(new StashStoreItem(0, 0, 0, 0, 1001, 1, 0, repairState));
        var repair = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            repairAbilitySpellId: 26).Execute(
                repairPlayer,
                new DiabloRepairItemIntent(
                    new DiabloPlayerItemReference(
                        DiabloPlayerItemLocation.Stash,
                        0,
                        repairState.StableItemId,
                        0),
                    0,
                    DiabloItemServiceSourceKind.ClassAbility),
                1);

        var rechargeState = AuthoritativeItemState.Empty with {
            StableItemId = 708,
            ItemType = 10,
            SpellId = 30,
            Charges = 1,
            MaxCharges = 10,
        };
        var rechargePlayer = CreatePlayer(gold: 0);
        rechargePlayer.AbilitySpellId = 27;
        rechargePlayer.Stash.Add(new StashStoreItem(0, 0, 0, 0, 1002, 1, 0, rechargeState));
        var recharge = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            rechargeAbilitySpellId: 27).Execute(
                rechargePlayer,
                new DiabloRechargeItemIntent(
                    new DiabloPlayerItemReference(
                        DiabloPlayerItemLocation.Stash,
                        0,
                        rechargeState.StableItemId,
                        0),
                    0,
                    DiabloItemServiceSourceKind.ClassAbility),
                1);

        var identifyState = AuthoritativeItemState.Empty with {
            StableItemId = 709,
            Magical = 1,
            Identified = false,
        };
        var identifyPlayer = CreatePlayer(gold: 0);
        identifyPlayer.AbilitySpellId = 5;
        identifyPlayer.Stash.Add(new StashStoreItem(0, 0, 0, 0, 1003, 1, 0, identifyState));
        var identify = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            identifyAbilitySpellId: 5).Execute(
                identifyPlayer,
                new DiabloIdentifyItemIntent(
                    new DiabloPlayerItemReference(
                        DiabloPlayerItemLocation.Stash,
                        0,
                        identifyState.StableItemId,
                        0),
                    0,
                    DiabloItemServiceSourceKind.ClassAbility),
                1);

        Assert.Equal(DiabloRuleFailure.NotAllowed, repair.Failure);
        Assert.Equal(DiabloRuleFailure.NotAllowed, recharge.Failure);
        Assert.Equal(DiabloRuleFailure.NotAllowed, identify.Failure);
        Assert.False(repairPlayer.StashOpen);
        Assert.False(rechargePlayer.StashOpen);
        Assert.False(identifyPlayer.StashOpen);
    }

    [Fact]
    public void StashGoldRequiresOpenSessionAndTransfersAtomically()
    {
        var player = CreatePlayer(gold: 100);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var closed = processor.Execute(
            player,
            new DiabloDepositStashGoldIntent(25),
            1);
        Assert.False(closed.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, closed.Failure);

        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 2).Succeeded);
        Assert.True(processor.Execute(player, new DiabloDepositStashGoldIntent(75), 3).Succeeded);
        Assert.Equal(25U, player.Gold);
        Assert.Equal(75U, player.StashGold);

        Assert.True(processor.Execute(player, new DiabloWithdrawStashGoldIntent(20), 4).Succeeded);
        Assert.Equal(45U, player.Gold);
        Assert.Equal(55U, player.StashGold);

        Assert.True(processor.Execute(player, new DiabloCloseStashIntent(), 5).Succeeded);
        var closedMove = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Stash, 0),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0)),
            6);
        Assert.False(closedMove.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, closedMove.Failure);
    }

    [Fact]
    public void StashGoldOverflowRejectsWithoutMutatingEitherBalance()
    {
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());
        var player = CreatePlayer(gold: uint.MaxValue - 10);
        player.StashGold = uint.MaxValue - 5;
        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 1).Succeeded);

        var deposit = processor.Execute(player, new DiabloDepositStashGoldIntent(10), 2);

        Assert.False(deposit.Succeeded);
        Assert.Equal(DiabloRuleFailure.InsufficientResources, deposit.Failure);
        Assert.Equal(uint.MaxValue - 10, player.Gold);
        Assert.Equal(uint.MaxValue - 5, player.StashGold);

        player.Gold = uint.MaxValue - 5;
        player.StashGold = 10;
        var withdraw = processor.Execute(player, new DiabloWithdrawStashGoldIntent(10), 3);

        Assert.False(withdraw.Succeeded);
        Assert.Equal(DiabloRuleFailure.InsufficientResources, withdraw.Failure);
        Assert.Equal(uint.MaxValue - 5, player.Gold);
        Assert.Equal(10U, player.StashGold);
    }

    [Fact]
    public void StashAndVendorSessionsAreMutuallyExclusive()
    {
        var processor = new DiabloStoreCommandProcessor(CreateCatalog());
        var player = CreatePlayer();

        Assert.True(processor.Execute(player, new DiabloOpenStoreIntent(1), 1).Succeeded);
        var openWhileVendor = processor.Execute(player, new DiabloOpenStashIntent(), 2);
        Assert.False(openWhileVendor.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, openWhileVendor.Failure);

        Assert.True(processor.Execute(player, new DiabloCloseStoreIntent(), 3).Succeeded);
        Assert.True(processor.Execute(player, new DiabloOpenStashIntent(), 4).Succeeded);
        var vendorWhileOpen = processor.Execute(player, new DiabloOpenStoreIntent(1), 5);
        Assert.False(vendorWhileOpen.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, vendorWhileOpen.Failure);
    }

    [Fact]
    public void InventoryMoveUsesStableReferenceWhenPresent()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 700,
                ItemType = 1,
                InventoryWidth = 1,
                InventoryHeight = 1,
            });
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(inventory: [item], inventoryGrid: grid);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloMoveInventoryItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 700),
                0,
                8),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(-1, player.InventoryGrid[0]);
        Assert.Equal(0, player.InventoryGrid[8]);
    }

    [Fact]
    public void InventoryMoveRejectsStaleStableReference()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            42,
            100,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 700,
                ItemType = 1,
            });
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(inventory: [item], inventoryGrid: grid);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloMoveInventoryItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 701),
                0,
                8),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, result.Failure);
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Equal(-1, player.InventoryGrid[8]);
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
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        catalog.AddStore(2, [], AuthoritativeStoreRole.Witch);
        catalog.AddStore(99, [], AuthoritativeStoreRole.Generic);
        var processor = new DiabloStoreCommandProcessor(catalog);

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.AdriaStoreId),
            0).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRefillManaIntent(), 1).Succeeded);
        Assert.Equal(640, player.Mana);

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(99),
            2).Succeeded);
        var equipment = new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 3);
        Assert.True(processor.Execute(player, new DiabloIdentifyItemIntent(equipment, 0), 3).Succeeded);
        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(1),
            4).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRepairItemIntent(equipment, 0), 4).Succeeded);
        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(2),
            5).Succeeded);
        Assert.True(processor.Execute(player, new DiabloRechargeItemIntent(equipment, 0), 6).Succeeded);

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
    public void AdriaServiceRequiresTheConfiguredTownLevel()
    {
        var player = CreatePlayer(gold: 1000, mana: 32, manaMaximum: 640);
        player.LevelId = 1;
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var open = processor.Execute(
            player,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.AdriaStoreId),
            0);

        Assert.False(open.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, open.Failure);
        Assert.Null(player.ActiveStoreId);

        player.ActiveStoreId = DiabloStoreCommandProcessor.AdriaStoreId;
        var refill = processor.Execute(player, new DiabloRefillManaIntent(), 1);
        Assert.False(refill.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, refill.Failure);
        Assert.Equal(32, player.Mana);

        player.LevelId = 7;
        player.ActiveStoreId = null;
        var configuredProcessor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            townLevelId: 7);
        Assert.True(configuredProcessor.Execute(
            player,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.AdriaStoreId),
            2).Succeeded);
    }

    [Fact]
    public void StorytellerServiceAllowsPaidIdentificationOnlyThroughTheTownService()
    {
        var item = new OwnedStoreItem(
            0,
            0,
            405,
            100,
            0,
            AuthoritativeItemState.Empty with {
                ItemType = 1,
                Value = 100,
                IdentifiedValue = 100,
                Magical = 1,
                Identified = false,
            });
        var player = CreatePlayer(
            gold: 1000,
            inventory: [item],
            inventoryGrid: [0, .. Enumerable.Repeat(-1, 39)]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        Assert.True(processor.Execute(
            player,
            new DiabloOpenStoreIntent(DiabloStoreCommandProcessor.StorytellerStoreId),
            0).Succeeded);
        var identify = processor.Execute(player, new DiabloIdentifyItemIntent(null, 0), 1);

        Assert.True(identify.Succeeded);
        Assert.True(Assert.Single(player.Inventory).State.Identified);
        Assert.Equal(900U, player.Gold);

        player.ActiveStoreId = 1;
        var smithIdentify = processor.Execute(player, new DiabloIdentifyItemIntent(null, 0), 2);

        Assert.False(smithIdentify.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, smithIdentify.Failure);
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
    public void ContentBackedEquipmentTransfersUseSemanticSlotsAndIgnoreInventoryFootprint()
    {
        var definition = new AuthoritativeItemDefinition(
            900,
            7,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            2,
            true) {
            GenerationTags = new HashSet<string>(["Armor", "BodyArmor"], StringComparer.OrdinalIgnoreCase),
            EquipLocation = 3,
            ItemClass = 2,
            NativeItemIndex = 900,
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = definition.Generate(42);
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        grid[1] = 0;
        grid[8] = 0;
        grid[9] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 42, 0, 0, state)],
            inventoryGrid: grid,
            items: items);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 900),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 6)),
            1);

        Assert.True(result.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(42U, Assert.Single(player.Equipment).ItemSeed);
        Assert.Equal(6U, player.Equipment[0].Slot);
    }

    [Fact]
    public void ContentBackedEquipmentTransfersRejectMismatchedSemanticSlots()
    {
        var definition = new AuthoritativeItemDefinition(
            901,
            7,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            GenerationTags = new HashSet<string>(["Armor", "Helmet"], StringComparer.OrdinalIgnoreCase),
            EquipLocation = 4,
            ItemClass = 2,
            NativeItemIndex = 901,
        };
        var items = new AuthoritativeItemCatalog([definition]);
        var state = definition.Generate(43);
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 43, 0, 0, state)],
            inventoryGrid: grid,
            items: items);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 901),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 6)),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, result.Failure);
        Assert.Equal(43U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Empty(player.Equipment);
        Assert.Equal(0, player.InventoryGrid[0]);
    }

    [Fact]
    public void MovesStableSingleCellItemsBetweenInventoryAndBelt()
    {
        var state = AuthoritativeItemState.Empty with {
            StableItemId = 902,
            ItemType = 1,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 44, 0, 0, state)],
            inventoryGrid: grid);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var toBelt = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 902),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 0)),
            1);

        Assert.True(toBelt.Succeeded);
        Assert.Empty(player.Inventory);
        Assert.Equal(44U, Assert.Single(player.Belt).ItemSeed);

        var toInventory = processor.Execute(
            player,
            new DiabloMoveItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 0, 902),
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 8)),
            2);

        Assert.True(toInventory.Succeeded);
        Assert.Empty(player.Belt);
        Assert.Equal(44U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal(0, player.InventoryGrid[8]);
    }

    [Fact]
    public void AutoEquipMovesAnOccupiedHandBackToInventoryAtomically()
    {
        var sourceState = AuthoritativeItemState.Empty with {
            StableItemId = 903,
            EquipLocation = 1,
            ItemClass = 1,
            StatFlag = true,
        };
        var displacedState = AuthoritativeItemState.Empty with {
            StableItemId = 904,
            EquipLocation = 1,
            ItemClass = 1,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 45, 0, 0, sourceState)],
            inventoryGrid: grid,
            equipment: [new EquippedStoreItem(4, 46, displacedState)]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 903)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(45U, Assert.Single(player.Equipment).ItemSeed);
        Assert.Equal(4U, player.Equipment[0].Slot);
        Assert.Equal(46U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal(0, player.InventoryGrid[30]);
    }

    [Fact]
    public void AutoEquipTwoHandedItemDisplacesBothHandsInNativeOrder()
    {
        var sourceState = AuthoritativeItemState.Empty with {
            StableItemId = 905,
            EquipLocation = 2,
            ItemClass = 1,
            StatFlag = true,
        };
        var leftState = AuthoritativeItemState.Empty with {
            StableItemId = 906,
            EquipLocation = 1,
            ItemClass = 1,
            StatFlag = true,
        };
        var rightState = AuthoritativeItemState.Empty with {
            StableItemId = 907,
            EquipLocation = 1,
            ItemClass = 2,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 47, 0, 0, sourceState)],
            inventoryGrid: grid,
            equipment: [
                new EquippedStoreItem(4, 48, leftState),
                new EquippedStoreItem(5, 49, rightState),
            ]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 905)),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(47U, Assert.Single(player.Equipment).ItemSeed);
        Assert.Equal(4U, player.Equipment[0].Slot);
        Assert.Equal([49U, 48U], player.Inventory.Select(item => item.ItemSeed));
        Assert.Equal([0, 1], [player.InventoryGrid[30], player.InventoryGrid[31]]);
    }

    [Fact]
    public void RequestedEquipmentSlotPreservesExplicitRingSelection()
    {
        var sourceState = AuthoritativeItemState.Empty with {
            StableItemId = 908,
            EquipLocation = 5,
            StatFlag = true,
        };
        var displacedState = AuthoritativeItemState.Empty with {
            StableItemId = 909,
            EquipLocation = 5,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 50, 0, 0, sourceState)],
            inventoryGrid: grid,
            equipment: [new EquippedStoreItem(2, 51, displacedState)]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 908),
                EquipmentSlot: 2),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(50U, player.Equipment.Single(item => item.Slot == 2).ItemSeed);
        Assert.Equal(51U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal(0, player.InventoryGrid[30]);
    }

    [Fact]
    public void RequestedEquipmentSlotUsesNativeHandSelection()
    {
        var sourceState = AuthoritativeItemState.Empty with {
            StableItemId = 910,
            EquipLocation = 1,
            ItemClass = 2,
            StatFlag = true,
        };
        var otherHandState = AuthoritativeItemState.Empty with {
            StableItemId = 911,
            EquipLocation = 1,
            ItemClass = 1,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 52, 0, 0, sourceState)],
            inventoryGrid: grid,
            equipment: [new EquippedStoreItem(4, 53, otherHandState)]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 910),
                EquipmentSlot: 5),
            1);

        Assert.True(result.Succeeded);
        Assert.Equal(53U, player.Equipment.Single(item => item.Slot == 4).ItemSeed);
        Assert.Equal(52U, player.Equipment.Single(item => item.Slot == 5).ItemSeed);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void RequestedEquipmentSlotUsesContentWeaponClassification()
    {
        var sourceDefinition = new AuthoritativeItemDefinition(
            920,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            true) {
            ItemClass = 1,
            EquipLocation = 1,
            NativeItemIndex = 920,
            GenerationTags = new HashSet<string>(["Weapon", "OneHanded"], StringComparer.OrdinalIgnoreCase),
        };
        var otherDefinition = sourceDefinition with {
            ItemId = 921,
            ItemClass = 2,
            NativeItemIndex = 921,
        };
        var items = new AuthoritativeItemCatalog([sourceDefinition, otherDefinition]);
        var source = new OwnedStoreItem(
            0,
            0,
            55,
            0,
            0,
            sourceDefinition.Generate(55) with { StatFlag = true });
        var other = new EquippedStoreItem(
            4,
            56,
            otherDefinition.Generate(56) with { StatFlag = true });
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [source],
            inventoryGrid: grid,
            equipment: [other]);
        var processor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            items: items);

        var result = processor.Execute(
            player,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 920),
                EquipmentSlot: 5),
            1);

        Assert.True(result.Succeeded, result.Failure.ToString());
        Assert.Equal(55U, player.Equipment.Single(item => item.Slot == 4).ItemSeed);
        Assert.Equal(56U, Assert.Single(player.Inventory).ItemSeed);
    }

    [Fact]
    public void RequestedEquipmentSlotRejectsIncompatibleTopology()
    {
        var sourceState = AuthoritativeItemState.Empty with {
            StableItemId = 912,
            EquipLocation = 5,
            StatFlag = true,
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray();
        grid[0] = 0;
        var player = CreatePlayer(
            inventory: [new OwnedStoreItem(0, 0, 54, 0, 0, sourceState)],
            inventoryGrid: grid);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloAutoEquipItemIntent(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 912),
                EquipmentSlot: 0),
            1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
        Assert.Equal(54U, Assert.Single(player.Inventory).ItemSeed);
        Assert.Equal(0, player.InventoryGrid[0]);
        Assert.Empty(player.Equipment);
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

    [Fact]
    public void CathedralMapRemainsOwnedAndEmitsTransientOpenEvent()
    {
        var player = CreatePlayer();
        player.EntityId = 42;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            9001,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 9001,
                MiscId = 43,
                ItemActionKind = DiabloItemActionKind.OpenCathedralMap,
            }));
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    9001)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Single(player.Inventory);
        var opened = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.MapOpened, opened.Kind);
        Assert.Equal(42U, opened.TargetEntityId);
    }

    [Fact]
    public void NaKrulNoteRemainsOwnedAndEmitsBookNineDialogue()
    {
        var player = CreatePlayer();
        player.EntityId = 43;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            9002,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 9002,
                MiscId = 42,
                ItemActionKind = DiabloItemActionKind.ReadQuestNote,
            }));
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog());

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    9002)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Single(player.Inventory);
        var dialogue = Assert.Single(result.Events);
        Assert.Equal(DiabloGameplayEventKind.Dialogue, dialogue.Kind);
        Assert.Equal(43U, dialogue.SourceEntityId);
        Assert.Equal(322, dialogue.MessageId);
    }

    [Fact]
    public void ItemActionDialogueIdentityResolvesFromTheActiveDefinition()
    {
        var player = CreatePlayer();
        player.EntityId = 44;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            9003,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 9003,
                MiscId = 42,
                ItemActionKind = DiabloItemActionKind.ReadQuestNote,
            }));
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(9003, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 42,
                ItemActionKind = DiabloItemActionKind.ReadQuestNote,
                ItemActionMessageId = 777,
            },
        ]);
        var processor = new DiabloStoreCommandProcessor(new StoreCatalog(), items: items);

        var result = processor.Execute(
            player,
            new DiabloUseConsumableIntent(
                new DiabloPlayerItemReference(
                    DiabloPlayerItemLocation.Inventory,
                    0,
                    9003)),
            1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(777, Assert.Single(result.Events).MessageId);
        Assert.Single(player.Inventory);
    }

    private static AuthoritativePlayerState CreatePlayer(
        uint gold = 100,
        int life = 640,
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
            life,
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
        => LoadClass("warrior");

    private static AuthoritativePlayerClassDefinition LoadClass(string symbol)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "server", "content", "base", "classes.tsv");
        return AuthoritativePlayerClassCatalog.LoadTsv("classes.tsv", File.ReadAllText(path))
            .Resolve(symbol);
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
