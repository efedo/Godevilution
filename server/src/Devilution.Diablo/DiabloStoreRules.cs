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

/** Read-only service quote with an optional transaction rejection reason. */
public sealed record DiabloStoreServiceQuote(uint Price, DiabloRuleFailure? RejectReason);

/** Optional quote surface; legacy/custom rule providers need not implement it. */
public interface IDiabloStoreQuoteRules
{
    DiabloStoreServiceQuote GetRepairQuote(OwnedStoreItem item, uint playerGold);

    DiabloStoreServiceQuote GetRechargeQuote(OwnedStoreItem item, uint playerGold);

    DiabloStoreServiceQuote GetIdentificationQuote(OwnedStoreItem item, uint playerGold);

    DiabloStoreServiceQuote GetManaRefillQuote(
        int currentMana,
        int maximumMana,
        uint playerGold);
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
public sealed class DiabloStoreRules : IDiabloStoreRules, IDiabloStoreQuoteRules
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

    public DiabloStoreServiceQuote GetRepairQuote(OwnedStoreItem item, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(item);
        var failure = ValidateRepair(item, playerGold);
        return CreateServiceQuote(failure, () => GetRepairPrice(item));
    }

    public DiabloStoreServiceQuote GetRechargeQuote(OwnedStoreItem item, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(item);
        var failure = ValidateRecharge(item, playerGold);
        return CreateServiceQuote(failure, () => GetRechargePrice(item));
    }

    public DiabloStoreServiceQuote GetIdentificationQuote(OwnedStoreItem item, uint playerGold)
    {
        ArgumentNullException.ThrowIfNull(item);
        var failure = ValidateIdentification(item, playerGold);
        return CreateServiceQuote(failure, () => GetIdentificationPrice(item));
    }

    public DiabloStoreServiceQuote GetManaRefillQuote(
        int currentMana,
        int maximumMana,
        uint playerGold)
    {
        var failure = ValidateManaRefill(currentMana, maximumMana, playerGold);
        return CreateServiceQuote(
            failure,
            () => GetManaRefillPrice(currentMana, maximumMana));
    }

    private static DiabloStoreServiceQuote CreateServiceQuote(
        DiabloRuleFailure? failure,
        Func<uint> getPrice)
    {
        var price = failure is null or DiabloRuleFailure.InsufficientResources
            ? getPrice()
            : 0;
        return new DiabloStoreServiceQuote(price, failure);
    }

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
        var maximumDurability = ResolveMaximumDurability(state);
        if (maximumDurability <= state.Durability)
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
        var hasContentClassification = items is not null
            && items.TryGetGenerationTagsByMiscId(
                state.MiscId,
                out var generationTags)
            && generationTags.Count > 0;
        return AuthoritativeItemClassification.IsStaff(state, items)
            || !hasContentClassification && state.MiscId is 23 or 27;
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
        var values = ResolveValues(state);
        var value = state.Magical != 0 && state.Identified
            ? values.IdentifiedValue
            : values.Value;
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
        var maximumDurability = ResolveMaximumDurability(state);
        var due = maximumDurability - state.Durability;
        if (due <= 0 || maximumDurability <= 0)
            return 0;
        var values = ResolveValues(state);
        if (state.Magical != 0 && state.Identified) {
            return (uint)Math.Max(
                0,
                Pricing.MagicalRepairPercent * values.IdentifiedValue * due
                    / (maximumDurability * Pricing.MagicalRepairDivisor));
        }
        return (uint)Math.Max(
            values.Value * due / (maximumDurability * Pricing.NormalRepairDivisor),
            1);
    }

    public uint GetRepairPrice(OwnedStoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetRepairPrice(item.State);
    }

    private int ResolveMaximumDurability(AuthoritativeItemState state)
    {
        if (state.MaxDurability != 0)
            return state.MaxDurability;
        if (items is not null) {
            if (items.TryGetByState(state, out var definition)
                && definition.MaxDurability > 0)
                return definition.MaxDurability;
            if (items.TryGetDurabilityMetadataByMiscId(
                    state.MiscId,
                    out _,
                    out var maximumDurability))
                return maximumDurability;
        }
        return 0;
    }

    private (int Value, int IdentifiedValue) ResolveValues(AuthoritativeItemState state)
    {
        var value = state.Value;
        var identifiedValue = state.IdentifiedValue;
        if (items is not null && (value == 0 || identifiedValue == 0)) {
            if (items.TryGetByState(state, out var definition)) {
                if (value == 0)
                    value = definition.Value;
                if (identifiedValue == 0)
                    identifiedValue = definition.IdentifiedValue;
            } else if (items.TryGetValueMetadataByMiscId(
                    state.MiscId,
                    out var contentValue,
                    out var contentIdentifiedValue)) {
                if (value == 0)
                    value = contentValue;
                if (identifiedValue == 0)
                    identifiedValue = contentIdentifiedValue;
            }
        }
        return (value, identifiedValue);
    }

    public uint GetRechargePrice(AuthoritativeItemState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var maximumCharges = ResolveMaximumCharges(state);
        var due = maximumCharges - state.Charges;
        if (due <= 0 || maximumCharges <= 0)
            return 0;
        var spellId = ResolveSpellId(state);
        var baseValue = checked(ResolveValues(state).Value + (int)Pricing.GetSpellStaffCost(spellId));
        return (uint)Math.Max(0, baseValue * due / (maximumCharges * Pricing.RechargeDivisor));
    }

    private int ResolveMaximumCharges(AuthoritativeItemState state)
    {
        if (state.MaxCharges != 0)
            return state.MaxCharges;
        if (items is not null) {
            var hasDefinition = items.TryGetByState(state, out var definition);
            if (hasDefinition && definition.MaxCharges != 0)
                return definition.MaxCharges;
            var miscId = state.MiscId != 0
                ? state.MiscId
                : hasDefinition
                    ? definition.MiscId
                    : 0;
            if (items.TryGetChargedItemMetadataByMiscId(
                    miscId,
                    out var maximumCharges,
                    out _))
                return maximumCharges;
        }
        return 0;
    }

    private int ResolveSpellId(AuthoritativeItemState state)
    {
        if (state.SpellId != 0)
            return state.SpellId;
        if (items is not null) {
            var hasDefinition = items.TryGetByState(state, out var definition);
            if (hasDefinition && definition.SpellId != 0)
                return definition.SpellId;
            var miscId = state.MiscId != 0
                ? state.MiscId
                : hasDefinition
                    ? definition.MiscId
                    : 0;
            if (items.TryGetChargedItemMetadataByMiscId(
                    miscId,
                    out _,
                    out var spellId))
                return spellId;
        }
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
