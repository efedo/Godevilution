namespace Devilution.Diablo;

/** Protocol-independent locations for authoritative player-owned items. */
public enum DiabloPlayerItemLocation
{
    Inventory,
    Belt,
    Equipment,
}

public readonly record struct DiabloPlayerItemReference(
    DiabloPlayerItemLocation Location,
    uint Slot);

/** Base intent for the Diablo store and owned-item transition boundary. */
public abstract record DiabloStoreIntent;

public sealed record DiabloOpenStoreIntent(uint StoreId) : DiabloStoreIntent;

public sealed record DiabloPurchaseIntent(uint StoreId, uint StoreSlot) : DiabloStoreIntent;

public sealed record DiabloSellItemIntent(
    DiabloPlayerItemReference? Item,
    uint LegacyInventoryIndex) : DiabloStoreIntent;

public sealed record DiabloRepairItemIntent(
    DiabloPlayerItemReference? Item,
    uint LegacyInventoryIndex,
    DiabloItemServiceSourceKind Source = DiabloItemServiceSourceKind.Vendor) : DiabloStoreIntent;

public sealed record DiabloRechargeItemIntent(
    DiabloPlayerItemReference? Item,
    uint LegacyInventoryIndex,
    DiabloItemServiceSourceKind Source = DiabloItemServiceSourceKind.Vendor) : DiabloStoreIntent;

/** Declares whether an item service is vendor-owned or a class capability. */
public enum DiabloItemServiceSourceKind
{
    Vendor,
    ClassAbility,
}

public sealed record DiabloIdentifyItemIntent(
    DiabloPlayerItemReference? Item,
    uint LegacyInventoryIndex,
    DiabloItemServiceSourceKind Source = DiabloItemServiceSourceKind.Vendor) : DiabloStoreIntent;

public sealed record DiabloRefillManaIntent : DiabloStoreIntent;

public sealed record DiabloMoveInventoryItemIntent(
    uint InventoryIndex,
    uint TargetCell) : DiabloStoreIntent;

public sealed record DiabloMoveItemIntent(
    DiabloPlayerItemReference Item,
    DiabloPlayerItemReference Destination) : DiabloStoreIntent;

/** Result of a module-owned transition before mapping to a wire protocol. */
public readonly record struct DiabloIntentResult(
    bool Succeeded,
    DiabloRuleFailure? Failure)
{
    public static DiabloIntentResult Accepted { get; } = new(true, null);

    public static DiabloIntentResult Rejected(DiabloRuleFailure failure)
    {
        return new(false, failure);
    }
}
