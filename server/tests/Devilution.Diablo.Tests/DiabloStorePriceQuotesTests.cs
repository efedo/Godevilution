using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloStorePriceQuotesTests
{
    private const uint GenericStoreId = 99;

    [Fact]
    public void PaidTransactionsSpendTheQuotedPriceForAllFourServices()
    {
        var repairState = AuthoritativeItemState.Empty with {
            StableItemId = 101,
            Value = 100,
            Durability = 5,
            MaxDurability = 20,
        };
        AssertTransactionPrice(
            repairState,
            quote => quote.Repair!,
            reference => new DiabloRepairItemIntent(reference, 0));

        var rechargeState = AuthoritativeItemState.Empty with {
            StableItemId = 102,
            Value = 100,
            SpellId = 7,
            SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            Charges = 1,
            MaxCharges = 10,
        };
        AssertTransactionPrice(
            rechargeState,
            quote => quote.Recharge!,
            reference => new DiabloRechargeItemIntent(reference, 0));

        var identifyState = AuthoritativeItemState.Empty with {
            StableItemId = 103,
            Value = 100,
            Magical = 1,
        };
        AssertTransactionPrice(
            identifyState,
            quote => quote.Identify!,
            reference => new DiabloIdentifyItemIntent(reference, 0));

        var rules = new DiabloStoreRules();
        var refillProcessor = new DiabloStoreCommandProcessor(
            new StoreCatalog(),
            rules);
        var refillPlayer = CreatePlayer(gold: 100, mana: 32, manaMaximum: 640);
        refillPlayer.ActiveStoreId = DiabloStoreCommandProcessor.AdriaStoreId;
        var refill = refillProcessor.CaptureStoreQuotes(refillPlayer)!.RefillMana!;
        var goldBefore = refillPlayer.Gold;

        var result = refillProcessor.Execute(refillPlayer, new DiabloRefillManaIntent(), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(refill.Price, goldBefore - refillPlayer.Gold);
    }

    [Fact]
    public void AQuoteDoesNotReserveFundsOrAuthorizeAnUnaffordableRepair()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(GenericStoreId, [], AuthoritativeStoreRole.Generic);
        var processor = new DiabloStoreCommandProcessor(catalog, new DiabloStoreRules());
        var player = CreatePlayer(gold: 1000);
        player.ActiveStoreId = GenericStoreId;
        var itemState = AuthoritativeItemState.Empty with {
            StableItemId = 501,
            Value = 100,
            Durability = 5,
            MaxDurability = 20,
        };
        player.Inventory.Add(new OwnedStoreItem(0, 0, 1, 0, 0, itemState));
        var reference = new DiabloPlayerItemReference(
            DiabloPlayerItemLocation.Inventory,
            0,
            itemState.StableItemId);
        var quote = Assert.Single(processor.CaptureStoreQuotes(player)!.Items).Repair!;
        Assert.Null(quote.RejectReason);
        Assert.True(quote.Price > 0);

        player.Gold = quote.Price - 1;
        var goldBefore = player.Gold;
        var result = processor.Execute(player, new DiabloRepairItemIntent(reference, 0), 1);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.InsufficientResources, result.Failure);
        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(5, player.Inventory[0].State.Durability);
    }

    [Fact]
    public void QuotesRetainUnaffordableCostAndSuppressInvalidTargetCost()
    {
        var rules = new DiabloStoreRules();
        var repairable = new OwnedStoreItem(
            0,
            0,
            1,
            0,
            0,
            AuthoritativeItemState.Empty with {
                Value = 100,
                Durability = 5,
                MaxDurability = 20,
            });

        var unaffordable = rules.GetRepairQuote(repairable, 0);
        Assert.Equal(DiabloRuleFailure.InsufficientResources, unaffordable.RejectReason);
        Assert.Equal(rules.GetRepairPrice(repairable), unaffordable.Price);
        var exactBudget = rules.GetRepairQuote(repairable, unaffordable.Price);
        Assert.Null(exactBudget.RejectReason);
        Assert.Equal(unaffordable.Price, exactBudget.Price);

        var full = repairable with {
            State = repairable.State with { Durability = repairable.State.MaxDurability },
        };
        var notNeeded = rules.GetRepairQuote(full, 1000);
        Assert.Equal(DiabloRuleFailure.NotAllowed, notNeeded.RejectReason);
        Assert.Equal(0U, notNeeded.Price);

        var invalid = rules.GetRepairQuote(
            repairable with { State = repairable.State with { Deleted = true } },
            1000);
        Assert.Equal(DiabloRuleFailure.InvalidTarget, invalid.RejectReason);
        Assert.Equal(0U, invalid.Price);

        var fullStaff = repairable with {
            State = repairable.State with {
                SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
                Charges = 10,
                MaxCharges = 10,
            },
        };
        var rechargeNotNeeded = rules.GetRechargeQuote(fullStaff, 1000);
        Assert.Equal(DiabloRuleFailure.NotAllowed, rechargeNotNeeded.RejectReason);
        Assert.Equal(0U, rechargeNotNeeded.Price);

        var zeroPriceRules = new DiabloStoreRules(new StoreServicePricing(identificationPrice: 0));
        var unidentifiedMagic = repairable with {
            State = repairable.State with { Magical = 1, Identified = false },
        };
        var freeIdentification = zeroPriceRules.GetIdentificationQuote(unidentifiedMagic, 0);
        Assert.Null(freeIdentification.RejectReason);
        Assert.Equal(0U, freeIdentification.Price);
    }

    [Fact]
    public void CaptureUsesExactInventoryBeltAndEquipmentReferencesWithoutMutation()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(GenericStoreId, [], AuthoritativeStoreRole.Generic);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(1, 1, 100, 100, 0, 0, 0, 20, 20, 2, 1, true) {
                NativeItemIndex = 101,
            },
        ]);
        var random = new Devilution.Engine.DeterministicLegacyRandom(1234);
        var processor = new DiabloStoreCommandProcessor(
            catalog,
            rules: new DiabloStoreRules(items: items),
            random: random,
            items: items);
        var player = CreatePlayer(gold: 250, mana: 64, manaMaximum: 640);
        player.ActiveStoreId = GenericStoreId;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            1,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 11,
                Durability = 1,
                MaxDurability = 4,
                InventoryWidth = 0,
                InventoryHeight = 0,
            }));
        player.Inventory[0] = player.Inventory[0] with {
            State = player.Inventory[0].State with { ItemIndex = 101 },
        };
        player.Belt.Add(new BeltStoreItem(
            3,
            2,
            AuthoritativeItemState.Empty with { StableItemId = 22, Magical = 1 }));
        player.Equipment.Add(new EquippedStoreItem(
            5,
            3,
            AuthoritativeItemState.Empty with { StableItemId = 33, Charges = 1, MaxCharges = 2 }));
        var inventoryBefore = player.Inventory.ToArray();
        var beltBefore = player.Belt.ToArray();
        var equipmentBefore = player.Equipment.ToArray();
        var goldBefore = player.Gold;
        var manaBefore = player.Mana;
        var randomBefore = random.State;

        var quotes = processor.CaptureStoreQuotes(player)!;

        Assert.Collection(
            quotes.Items,
            item => Assert.Equal(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Inventory, 0, 11),
                item.Item),
            item => Assert.Equal(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Belt, 3, 22),
                item.Item),
            item => Assert.Equal(
                new DiabloPlayerItemReference(DiabloPlayerItemLocation.Equipment, 5, 33),
                item.Item));
        Assert.Equal(inventoryBefore, player.Inventory);
        Assert.Equal(beltBefore, player.Belt);
        Assert.Equal(equipmentBefore, player.Equipment);
        Assert.Equal(0, player.Inventory[0].State.InventoryWidth);
        Assert.Equal(0, player.Inventory[0].State.InventoryHeight);
        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(manaBefore, player.Mana);
        Assert.Equal(randomBefore, random.State);
    }

    [Fact]
    public void QuoteFieldsFollowTheActiveVendorServiceRoles()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [], AuthoritativeStoreRole.Smith);
        catalog.AddStore(2, [], AuthoritativeStoreRole.Witch);
        catalog.AddStore(3, [], AuthoritativeStoreRole.Wirt);
        catalog.AddStore(GenericStoreId, [], AuthoritativeStoreRole.Generic);
        var processor = new DiabloStoreCommandProcessor(
            catalog,
            adriaStoreId: 73,
            storytellerStoreId: 74);
        var player = CreatePlayer(gold: 1000);
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            1,
            0,
            0,
            AuthoritativeItemState.Empty with {
                StableItemId = 1,
                Value = 100,
                Magical = 1,
                Durability = 5,
                MaxDurability = 20,
                SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
                Charges = 1,
                MaxCharges = 10,
            }));

        player.ActiveStoreId = 1;
        var smith = Assert.Single(processor.CaptureStoreQuotes(player)!.Items);
        Assert.NotNull(smith.Repair);
        Assert.Null(smith.Recharge);
        Assert.Null(smith.Identify);

        player.ActiveStoreId = 2;
        var witch = Assert.Single(processor.CaptureStoreQuotes(player)!.Items);
        Assert.Null(witch.Repair);
        Assert.NotNull(witch.Recharge);
        Assert.Null(witch.Identify);

        player.ActiveStoreId = 74;
        var storyteller = Assert.Single(processor.CaptureStoreQuotes(player)!.Items);
        Assert.Null(storyteller.Repair);
        Assert.Null(storyteller.Recharge);
        Assert.NotNull(storyteller.Identify);

        player.ActiveStoreId = GenericStoreId;
        var generic = Assert.Single(processor.CaptureStoreQuotes(player)!.Items);
        Assert.NotNull(generic.Repair);
        Assert.NotNull(generic.Recharge);
        Assert.NotNull(generic.Identify);
    }

    [Fact]
    public void LegacyRulesAndOpenStashDoNotInvokeOrExposePaidQuotes()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(GenericStoreId, [], AuthoritativeStoreRole.Generic);
        var player = CreatePlayer(gold: 100);
        player.ActiveStoreId = GenericStoreId;
        var processor = new DiabloStoreCommandProcessor(catalog, new RulesWithoutQuoteProvider());

        Assert.Null(processor.CaptureStoreQuotes(player));

        player.ActiveStoreId = null;
        player.StashOpen = true;
        Assert.Null(new DiabloStoreCommandProcessor(catalog).CaptureStoreQuotes(player));
    }

    private static void AssertTransactionPrice(
        AuthoritativeItemState itemState,
        Func<DiabloStoreItemServiceQuotes, DiabloStoreServiceQuote> selectQuote,
        Func<DiabloPlayerItemReference, DiabloStoreIntent> createIntent)
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(GenericStoreId, [], AuthoritativeStoreRole.Generic);
        var processor = new DiabloStoreCommandProcessor(catalog, new DiabloStoreRules());
        var player = CreatePlayer(gold: 10000);
        player.ActiveStoreId = GenericStoreId;
        player.Inventory.Add(new OwnedStoreItem(0, 0, 1, 0, 0, itemState));
        var reference = new DiabloPlayerItemReference(
            DiabloPlayerItemLocation.Inventory,
            0,
            itemState.StableItemId);
        var quote = selectQuote(Assert.Single(processor.CaptureStoreQuotes(player)!.Items));
        var goldBefore = player.Gold;

        var result = processor.Execute(player, createIntent(reference), 1);

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(quote.Price, goldBefore - player.Gold);
    }

    private static AuthoritativePlayerState CreatePlayer(
        uint gold,
        int mana = 0,
        int manaMaximum = 0)
    {
        var player = DiabloPlayerTemplate.CreateDefault(
            gold,
            life: 640,
            mana: manaMaximum,
            levelId: AuthoritativePortalRules.TownLevelId).CreatePlayer();
        player.Mana = mana;
        return player;
    }

    private sealed class RulesWithoutQuoteProvider : IDiabloStoreRules
    {
        private static InvalidOperationException UnexpectedCall()
            => new("A legacy rules provider must not be called to capture quotes.");

        public DiabloRuleFailure? ValidatePurchase(StoreItem item, uint playerGold) => throw UnexpectedCall();
        public DiabloRuleFailure? ValidateSale(OwnedStoreItem item) => throw UnexpectedCall();
        public DiabloRuleFailure? ValidateRepair(OwnedStoreItem item, uint playerGold) => throw UnexpectedCall();
        public DiabloRuleFailure? ValidateRecharge(OwnedStoreItem item, uint playerGold) => throw UnexpectedCall();
        public DiabloRuleFailure? ValidateIdentification(OwnedStoreItem item, uint playerGold) => throw UnexpectedCall();
        public DiabloRuleFailure? ValidateManaRefill(int currentMana, int maximumMana, uint playerGold) => throw UnexpectedCall();
        public uint GetSalePrice(OwnedStoreItem item) => throw UnexpectedCall();
        public uint GetRepairPrice(OwnedStoreItem item) => throw UnexpectedCall();
        public uint GetRechargePrice(OwnedStoreItem item) => throw UnexpectedCall();
        public uint GetIdentificationPrice(OwnedStoreItem item) => throw UnexpectedCall();
        public uint GetManaRefillPrice(int currentMana, int maximumMana) => throw UnexpectedCall();
    }
}
