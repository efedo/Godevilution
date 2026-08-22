namespace Devilution.Diablo;

/** Rule-level rejection independent of transport and server command types. */
public enum DiabloRuleFailure
{
    InvalidTarget,
    NotAllowed,
    InsufficientResources,
    Malformed,
    TooLate,
}

/** Replaceable, protocol-independent rules consumed by store transitions. */
public interface IDiabloStoreRules
{
    DiabloRuleFailure? ValidatePurchase(StoreItem item, uint playerGold);

    DiabloRuleFailure? ValidateSale(OwnedStoreItem item);

    DiabloRuleFailure? ValidateRepair(OwnedStoreItem item, uint playerGold);

    DiabloRuleFailure? ValidateRecharge(OwnedStoreItem item, uint playerGold);

    DiabloRuleFailure? ValidateIdentification(OwnedStoreItem item, uint playerGold);

    DiabloRuleFailure? ValidateManaRefill(int currentMana, int maximumMana, uint playerGold);

    uint GetSalePrice(OwnedStoreItem item);

    uint GetRepairPrice(OwnedStoreItem item);

    uint GetRechargePrice(OwnedStoreItem item);

    uint GetIdentificationPrice(OwnedStoreItem item);

    uint GetManaRefillPrice(int currentMana, int maximumMana);
}

/** Deterministic Diablo store validation and pricing rules. */
public sealed class DiabloStoreRules : IDiabloStoreRules
{
    private readonly AuthoritativeItemCatalog? items;

    public DiabloStoreRules(
        StoreServicePricing? pricing = null,
        AuthoritativeItemCatalog? items = null)
    {
        Pricing = pricing ?? StoreServicePricing.Default;
        this.items = items;
    }

    public StoreServicePricing Pricing { get; }

    public DiabloRuleFailure? ValidatePurchase(StoreItem item, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.State.Deleted)
            return DiabloRuleFailure.InvalidTarget;
        return playerGold < item.Price ? DiabloRuleFailure.InsufficientResources : null;
    }

    public DiabloRuleFailure? ValidateSale(AuthoritativeItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Deleted ? DiabloRuleFailure.InvalidTarget : null;
    }

    public DiabloRuleFailure? ValidateSale(OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ValidateSale(item.State);
    }

    public DiabloRuleFailure? ValidateRepair(AuthoritativeItemState state, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Deleted)
            return DiabloRuleFailure.InvalidTarget;
        if (state.MaxDurability <= state.Durability)
            return DiabloRuleFailure.NotAllowed;
        var price = GetRepairPrice(state);
        if (price == 0)
            return DiabloRuleFailure.NotAllowed;
        return playerGold < price ? DiabloRuleFailure.InsufficientResources : null;
    }

    public DiabloRuleFailure? ValidateRepair(OwnedStoreItem item, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ValidateRepair(item.State, playerGold);
    }

    public DiabloRuleFailure? ValidateRecharge(AuthoritativeItemState state, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Deleted)
            return DiabloRuleFailure.InvalidTarget;
        if (!IsRechargeable(state))
            return DiabloRuleFailure.InvalidTarget;
        if (ResolveMaximumCharges(state) <= state.Charges)
            return DiabloRuleFailure.NotAllowed;
        var price = GetRechargePrice(state);
        if (price == 0)
            return DiabloRuleFailure.NotAllowed;
        return playerGold < price ? DiabloRuleFailure.InsufficientResources : null;
    }

    private bool IsRechargeable(AuthoritativeItemState state)
    {
        if (state.SpellSourceKind is { } explicitKind)
            return explicitKind == DiabloItemSpellSourceKind.ChargedItem;
        if (items is not null) {
            var hasDefinition = items.TryGetByState(state, out var definition);
            if (hasDefinition && definition.SpellSourceKind is { } contentKind)
                return contentKind == DiabloItemSpellSourceKind.ChargedItem;
            var miscId = state.MiscId != 0
                ? state.MiscId
                : hasDefinition
                    ? definition.MiscId
                    : 0;
            if (items.TryGetSpellSourceKindByMiscId(miscId, out contentKind))
                return contentKind == DiabloItemSpellSourceKind.ChargedItem;
            if (hasDefinition && definition.GenerationTags.Count > 0)
                return definition.GenerationTags.Contains("Staff", StringComparer.OrdinalIgnoreCase);
        }
        return AuthoritativeItemClassification.IsStaff(state, items)
            || state.MiscId is 23 or 27;
    }

    public DiabloRuleFailure? ValidateRecharge(OwnedStoreItem item, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ValidateRecharge(item.State, playerGold);
    }

    public DiabloRuleFailure? ValidateIdentification(AuthoritativeItemState state, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Deleted || state.Magical == 0)
            return DiabloRuleFailure.InvalidTarget;
        if (state.Identified)
            return DiabloRuleFailure.NotAllowed;
        return playerGold < GetIdentificationPrice() ? DiabloRuleFailure.InsufficientResources : null;
    }

    public DiabloRuleFailure? ValidateIdentification(OwnedStoreItem item, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ValidateIdentification(item.State, playerGold);
    }

    public DiabloRuleFailure? ValidateManaRefill(int currentMana, int maximumMana, uint playerGold)
    {
        if (currentMana < 0 || maximumMana < 0 || currentMana > maximumMana)
            return DiabloRuleFailure.InvalidTarget;
        if (currentMana == maximumMana)
            return DiabloRuleFailure.NotAllowed;
        return playerGold < GetManaRefillPrice(currentMana, maximumMana)
            ? DiabloRuleFailure.InsufficientResources
            : null;
    }

    public uint GetSalePrice(AuthoritativeItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var value = state.Magical != 0 && state.Identified ? state.IdentifiedValue : state.Value;
        return (uint)Math.Max(value / Pricing.SaleDivisor, 1);
    }

    public uint GetSalePrice(OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetSalePrice(item.State);
    }

    public uint GetRepairPrice(AuthoritativeItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var due = state.MaxDurability - state.Durability;
        if (due <= 0 || state.MaxDurability <= 0)
            return 0;
        if (state.Magical != 0 && state.Identified) {
            return (uint)Math.Max(
                0,
                Pricing.MagicalRepairPercent * state.IdentifiedValue * due
                    / (state.MaxDurability * Pricing.MagicalRepairDivisor));
        }
        return (uint)Math.Max(
            state.Value * due / (state.MaxDurability * Pricing.NormalRepairDivisor),
            1);
    }

    public uint GetRepairPrice(OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetRepairPrice(item.State);
    }

    public uint GetRechargePrice(AuthoritativeItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var maximumCharges = ResolveMaximumCharges(state);
        var due = maximumCharges - state.Charges;
        if (due <= 0 || maximumCharges <= 0)
            return 0;
        var spellId = ResolveSpellId(state);
        var baseValue = checked(state.Value + (int)Pricing.GetSpellStaffCost(spellId));
        return (uint)Math.Max(0, baseValue * due / (maximumCharges * Pricing.RechargeDivisor));
    }

    private int ResolveMaximumCharges(AuthoritativeItemState state)
    {
        if (state.MaxCharges != 0)
            return state.MaxCharges;
        if (items is not null
            && items.TryGetByState(state, out var definition)
            && definition.MaxCharges != 0)
            return definition.MaxCharges;
        return 0;
    }

    private int ResolveSpellId(AuthoritativeItemState state)
    {
        if (state.SpellId != 0)
            return state.SpellId;
        if (items is not null
            && items.TryGetByState(state, out var definition))
            return definition.SpellId;
        return 0;
    }

    public uint GetRechargePrice(OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetRechargePrice(item.State);
    }

    public uint GetIdentificationPrice() => Pricing.IdentificationPrice;

    public uint GetIdentificationPrice(OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetIdentificationPrice();
    }

    public uint GetManaRefillPrice(int currentMana, int maximumMana)
    {
        var missingWholeMana = Math.Max(
            1,
            (maximumMana - currentMana + Pricing.ManaChunk - 1) / Pricing.ManaChunk);
        return (uint)missingWholeMana;
    }
}
