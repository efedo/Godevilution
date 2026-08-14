using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloStoreRulesTests
{
    [Fact]
    public void MatchesLegacySaleServiceAndRefillPricing()
    {
        var state = AuthoritativeItemState.Empty with {
            ItemType = 10,
            Magical = 1,
            Identified = true,
            Value = 100,
            IdentifiedValue = 100,
            Charges = 0,
            MaxCharges = 1,
            Durability = 5,
            MaxDurability = 20,
        };
        var rules = new DiabloStoreRules();

        Assert.Equal(25U, rules.GetSalePrice(state));
        Assert.Equal(11U, rules.GetRepairPrice(state));
        Assert.Equal(50U, rules.GetRechargePrice(state));
        Assert.Equal(100U, rules.GetIdentificationPrice());
        Assert.Null(rules.ValidateManaRefill(32, 640, 10));
        Assert.Equal(10U, rules.GetManaRefillPrice(32, 640));
    }

    [Fact]
    public void ReturnsModuleFailuresWithoutProtocolTypes()
    {
        var rules = new DiabloStoreRules();
        var deleted = AuthoritativeItemState.Empty with { Deleted = true };
        var unaffordable = new StoreItem(0, 42, 75);

        Assert.Equal(DiabloRuleFailure.InvalidTarget, rules.ValidateSale(deleted));
        Assert.Equal(DiabloRuleFailure.InsufficientResources, rules.ValidatePurchase(unaffordable, 74));
        Assert.Equal(DiabloRuleFailure.InvalidTarget, rules.ValidateManaRefill(-1, 10, 100));
        Assert.Equal(DiabloRuleFailure.NotAllowed, rules.ValidateManaRefill(10, 10, 100));
    }

    [Fact]
    public void ExplicitChargedSourceOverridesLegacyItemTypeForRecharge()
    {
        var rules = new DiabloStoreRules();
        var charged = AuthoritativeItemState.Empty with {
            ItemType = 1,
            SpellSourceKind = DiabloItemSpellSourceKind.ChargedItem,
            SpellId = 900,
            Charges = 1,
            MaxCharges = 10,
            Value = 100,
        };
        var consumable = charged with {
            SpellSourceKind = DiabloItemSpellSourceKind.ConsumableItem,
        };

        Assert.Null(rules.ValidateRecharge(charged, 100));
        Assert.Equal(
            DiabloRuleFailure.InvalidTarget,
            rules.ValidateRecharge(consumable, 100));
    }

    [Fact]
    public void LegacyRechargeFieldsRemainCompatibleWhenSourceMetadataIsAbsent()
    {
        var rules = new DiabloStoreRules();
        var legacy = AuthoritativeItemState.Empty with {
            ItemType = 10,
            Charges = 0,
            MaxCharges = 1,
            Value = 100,
        };

        Assert.Null(rules.ValidateRecharge(legacy, 100));
    }
}
