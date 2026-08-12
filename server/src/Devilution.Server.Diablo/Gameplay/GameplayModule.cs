using Devilution.Engine;
using Devilution.Protocol.V1;

namespace Devilution.Server.Gameplay;

/** Legacy host-facing store-rule contract retained during adapter migration. */
public interface IStoreGameplayRules
{
    CommandRejectReason? ValidatePurchase(StoreItem item, uint playerGold);

    CommandRejectReason? ValidateSale(OwnedStoreItem item);

    CommandRejectReason? ValidateRepair(OwnedStoreItem item, uint playerGold);

    CommandRejectReason? ValidateRecharge(OwnedStoreItem item, uint playerGold);

    CommandRejectReason? ValidateIdentification(OwnedStoreItem item, uint playerGold);

    CommandRejectReason? ValidateManaRefill(int currentMana, int maximumMana, uint playerGold);

    uint GetSalePrice(OwnedStoreItem item);

    uint GetRepairPrice(OwnedStoreItem item);

    uint GetRechargePrice(OwnedStoreItem item);

    uint GetIdentificationPrice(OwnedStoreItem item);

    uint GetManaRefillPrice(int currentMana, int maximumMana);
}

/**
 * Compatibility adapter for callers that still consume protocol rejection
 * codes. New engine composition loads <see cref="DiabloModule"/> directly.
 */
public sealed class DiabloGameplayModule : IGameplayModule, IStoreGameplayRules
{
    public static DiabloGameplayModule Instance { get; } = new();
    private readonly DiabloModule module;

    public DiabloGameplayModule(StoreServicePricing? pricing = null)
    {
        module = new DiabloModule(pricing);
    }

    public StoreServicePricing Pricing => module.Pricing;

    public IDiabloStoreRules Rules => module.Rules;

    public GameplayModuleIdentity Identity => module.Identity;

    public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
    {
        module.Register(registry, context);
    }

    public CommandRejectReason? ValidatePurchase(StoreItem item, uint playerGold)
    {
        return ToCommandRejectReason(module.Rules.ValidatePurchase(item, playerGold));
    }

    public CommandRejectReason? ValidateSale(OwnedStoreItem item)
    {
        return ToCommandRejectReason(module.Rules.ValidateSale(item));
    }

    public CommandRejectReason? ValidateRepair(OwnedStoreItem item, uint playerGold)
    {
        return ToCommandRejectReason(module.Rules.ValidateRepair(item, playerGold));
    }

    public CommandRejectReason? ValidateRecharge(OwnedStoreItem item, uint playerGold)
    {
        return ToCommandRejectReason(module.Rules.ValidateRecharge(item, playerGold));
    }

    public CommandRejectReason? ValidateIdentification(OwnedStoreItem item, uint playerGold)
    {
        return ToCommandRejectReason(module.Rules.ValidateIdentification(item, playerGold));
    }

    public CommandRejectReason? ValidateManaRefill(
        int currentMana,
        int maximumMana,
        uint playerGold)
    {
        return ToCommandRejectReason(
            module.Rules.ValidateManaRefill(currentMana, maximumMana, playerGold));
    }

    public uint GetSalePrice(OwnedStoreItem item) => module.Rules.GetSalePrice(item);

    public uint GetRepairPrice(OwnedStoreItem item) => module.Rules.GetRepairPrice(item);

    public uint GetRechargePrice(OwnedStoreItem item) => module.Rules.GetRechargePrice(item);

    public uint GetIdentificationPrice(OwnedStoreItem item)
        => module.Rules.GetIdentificationPrice(item);

    public uint GetManaRefillPrice(int currentMana, int maximumMana)
        => module.Rules.GetManaRefillPrice(currentMana, maximumMana);

    internal static CommandRejectReason? ToCommandRejectReason(DiabloRuleFailure? failure)
    {
        return failure switch {
            null => null,
            DiabloRuleFailure.InvalidTarget => CommandRejectReason.InvalidTarget,
            DiabloRuleFailure.NotAllowed => CommandRejectReason.NotAllowed,
            DiabloRuleFailure.InsufficientResources => CommandRejectReason.InsufficientResources,
            DiabloRuleFailure.Malformed => CommandRejectReason.Malformed,
            DiabloRuleFailure.TooLate => CommandRejectReason.TooLate,
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
    }

    internal static DiabloRuleFailure? ToDiabloRuleFailure(CommandRejectReason? failure)
    {
        return failure switch {
            null or CommandRejectReason.Unspecified => null,
            CommandRejectReason.InvalidTarget => DiabloRuleFailure.InvalidTarget,
            CommandRejectReason.NotAllowed => DiabloRuleFailure.NotAllowed,
            CommandRejectReason.InsufficientResources => DiabloRuleFailure.InsufficientResources,
            CommandRejectReason.Malformed => DiabloRuleFailure.Malformed,
            CommandRejectReason.TooLate => DiabloRuleFailure.TooLate,
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
    }
}
