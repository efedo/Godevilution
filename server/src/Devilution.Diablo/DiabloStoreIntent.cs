namespace Devilution.Diablo;

/** Protocol-independent locations for authoritative player-owned items. */
public enum DiabloPlayerItemLocation
{
    Inventory,
    Belt,
    Equipment,
    Stash,
}

public readonly record struct DiabloPlayerItemReference(
    DiabloPlayerItemLocation Location,
    uint Slot,
    uint StableItemId = 0,
    uint Page = 0);

/** Base intent for the Diablo store and owned-item transition boundary. */
public abstract record DiabloStoreIntent;

public sealed record DiabloOpenStoreIntent(uint StoreId) : DiabloStoreIntent;

/** Closes the player's current vendor or service store; repeated closes are safe. */
public sealed record DiabloCloseStoreIntent : DiabloStoreIntent;

/** Opens the server-owned stash session for item and gold commands. */
public sealed record DiabloOpenStashIntent : DiabloStoreIntent;

/** Closes the server-owned stash session; repeated closes are safe. */
public sealed record DiabloCloseStashIntent : DiabloStoreIntent;

/** Moves carried wallet gold into the open server-owned stash. */
public sealed record DiabloDepositStashGoldIntent(uint Amount) : DiabloStoreIntent;

/** Moves stash gold into the carried authoritative wallet. */
public sealed record DiabloWithdrawStashGoldIntent(uint Amount) : DiabloStoreIntent;

/** Splits a carried gold stack onto the authoritative presentation cursor. */
public sealed record DiabloSplitGoldIntent(
    DiabloPlayerItemReference Item,
    uint Amount) : DiabloStoreIntent;

/** Reorganizes carried inventory using the native deterministic placement order. */
public sealed record DiabloSortInventoryIntent : DiabloStoreIntent;

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
    ConsumableItem,
}

public sealed record DiabloIdentifyItemIntent(
    DiabloPlayerItemReference? Item,
    uint LegacyInventoryIndex,
    DiabloItemServiceSourceKind Source = DiabloItemServiceSourceKind.Vendor,
    DiabloPlayerItemReference? SourceItem = null) : DiabloStoreIntent;

/** Consumes an owned spell book and applies its learned-spell transition. */
public sealed record DiabloReadBookIntent(
    DiabloPlayerItemReference? Item,
    uint LegacyInventoryIndex) : DiabloStoreIntent;

/** Consumes a content-owned item action and applies its server-owned effect. */
public sealed record DiabloUseConsumableIntent(
    DiabloPlayerItemReference Item,
    DiabloPlayerItemReference? Target = null) : DiabloStoreIntent;

public sealed record DiabloRefillManaIntent : DiabloStoreIntent;

public sealed record DiabloMoveInventoryItemIntent(
    DiabloPlayerItemReference? Item,
    uint InventoryIndex,
    uint TargetCell) : DiabloStoreIntent
{
    /** Retains the positional constructor used by legacy callers. */
    public DiabloMoveInventoryItemIntent(uint inventoryIndex, uint targetCell)
        : this(null, inventoryIndex, targetCell)
    {
    }
}

public sealed record DiabloMoveItemIntent(
    DiabloPlayerItemReference Item,
    DiabloPlayerItemReference Destination) : DiabloStoreIntent;

/** Atomically auto-equips an inventory item and returns displaced equipment to inventory. */
public sealed record DiabloAutoEquipItemIntent(
    DiabloPlayerItemReference Item,
    uint? EquipmentSlot = null) : DiabloStoreIntent;

/** Result of a module-owned transition before mapping to a wire protocol. */
public readonly record struct DiabloIntentResult(
    bool Succeeded,
    DiabloRuleFailure? Failure)
{
    /** Transient presentation events emitted by this accepted transition. */
    public IReadOnlyList<DiabloGameplayEvent> Events { get; init; } = [];

    public static DiabloIntentResult Accepted { get; } = new(true, null);

    public static DiabloIntentResult Rejected(DiabloRuleFailure failure)
    {
        return new(false, failure);
    }
}
