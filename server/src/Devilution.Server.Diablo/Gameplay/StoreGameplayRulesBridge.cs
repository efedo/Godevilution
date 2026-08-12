using Devilution.Diablo;

namespace Devilution.Server.Gameplay;

/** Preserves custom host rule adapters while the module boundary is extracted. */
internal sealed class StoreGameplayRulesBridge(IStoreGameplayRules rules) : IDiabloStoreRules
{
    public DiabloRuleFailure? ValidatePurchase(StoreItem item, uint playerGold)
        => DiabloGameplayModule.ToDiabloRuleFailure(rules.ValidatePurchase(item, playerGold));

    public DiabloRuleFailure? ValidateSale(OwnedStoreItem item)
        => DiabloGameplayModule.ToDiabloRuleFailure(rules.ValidateSale(item));

    public DiabloRuleFailure? ValidateRepair(OwnedStoreItem item, uint playerGold)
        => DiabloGameplayModule.ToDiabloRuleFailure(rules.ValidateRepair(item, playerGold));

    public DiabloRuleFailure? ValidateRecharge(OwnedStoreItem item, uint playerGold)
        => DiabloGameplayModule.ToDiabloRuleFailure(rules.ValidateRecharge(item, playerGold));

    public DiabloRuleFailure? ValidateIdentification(OwnedStoreItem item, uint playerGold)
        => DiabloGameplayModule.ToDiabloRuleFailure(rules.ValidateIdentification(item, playerGold));

    public DiabloRuleFailure? ValidateManaRefill(int currentMana, int maximumMana, uint playerGold)
        => DiabloGameplayModule.ToDiabloRuleFailure(
            rules.ValidateManaRefill(currentMana, maximumMana, playerGold));

    public uint GetSalePrice(OwnedStoreItem item) => rules.GetSalePrice(item);

    public uint GetRepairPrice(OwnedStoreItem item) => rules.GetRepairPrice(item);

    public uint GetRechargePrice(OwnedStoreItem item) => rules.GetRechargePrice(item);

    public uint GetIdentificationPrice(OwnedStoreItem item) => rules.GetIdentificationPrice(item);

    public uint GetManaRefillPrice(int currentMana, int maximumMana)
        => rules.GetManaRefillPrice(currentMana, maximumMana);
}
