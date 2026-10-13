using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Button availability from the current authoritative store snapshot and local presentation inputs. */
public readonly record struct AuthoritativeStoreServiceAvailability(
    bool CanRepair,
    bool CanRecharge,
    bool CanIdentify,
    bool CanRefillMana);

/**
 * Selects service-panel availability without predicting item eligibility, pricing,
 * or mutation. The server remains authoritative for every submitted service.
 */
public static class AuthoritativeStoreServicePresentation
{
    public static AuthoritativeStoreServiceAvailability Evaluate(
        bool connected,
        StoreSnapshot? activeStore,
        bool hasSelectedServiceItem,
        bool hasRepairAbility,
        bool hasRechargeAbility,
        bool hasIdentifyAbility,
        bool hasIdentifyScroll)
    {
        var services = activeStore?.Services;
        var hasActiveStore = activeStore is not null;

        // Older servers omit Services. Preserve their prior any-active-store
        // presentation until a capability-bearing snapshot is available.
        var canRepair = hasActiveStore
            ? services is null || services.Repair
            : hasRepairAbility;
        var canRecharge = hasActiveStore
            ? services is null || services.Recharge
            : hasRechargeAbility;
        var canIdentify = hasActiveStore
            ? services is null || services.Identify
            : hasIdentifyAbility || hasIdentifyScroll;
        var canRefillMana = !hasActiveStore || services is null || services.RefillMana;

        return new AuthoritativeStoreServiceAvailability(
            connected && hasSelectedServiceItem && canRepair,
            connected && hasSelectedServiceItem && canRecharge,
            connected && hasSelectedServiceItem && canIdentify,
            connected && canRefillMana);
    }
}