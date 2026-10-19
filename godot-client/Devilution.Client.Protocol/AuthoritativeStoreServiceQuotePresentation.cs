using System.Globalization;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

public enum StoreServiceQuoteAction
{
    Repair,
    Recharge,
    Identify,
    RefillMana,
}

public enum StoreServiceQuoteState
{
    Legacy,
    Available,
    InsufficientResources,
    Unavailable,
}

public readonly record struct StoreServiceQuotePresentation(
    StoreServiceQuoteState State,
    string? ButtonText,
    uint? Price,
    bool CanInvoke);

/** Presents server-authored store quotes without predicting service eligibility or outcomes. */
public static class AuthoritativeStoreServiceQuotePresentation
{
    public static StoreServiceQuotePresentation ResolveItem(
        StoreSnapshot? activeStore,
        PlayerItemReference? selectedItem,
        StoreServiceQuoteAction action,
        bool currentConnectionAndTarget,
        bool legacyAvailability)
    {
        if (activeStore?.Quotes is not { } quotes)
            return new(StoreServiceQuoteState.Legacy, null, null, legacyAvailability);

        if (!currentConnectionAndTarget
            || !legacyAvailability
            || selectedItem is null
            || action == StoreServiceQuoteAction.RefillMana
            || !TryGetItemQuote(quotes.Items, selectedItem, action, out var quote))
            return Unavailable(action);

        if (!TryClassify(quote, out var reason))
            return Unavailable(action);

        return reason switch {
            CommandRejectReason.Unspecified => Available(action, quote.Price),
            CommandRejectReason.InsufficientResources => Insufficient(action, quote.Price),
            _ => Unavailable(action),
        };
    }

    public static StoreServiceQuotePresentation ResolveRefillMana(
        StoreSnapshot? activeStore,
        bool currentConnection,
        bool legacyAvailability)
    {
        if (activeStore?.Quotes is not { } quotes)
            return new(StoreServiceQuoteState.Legacy, null, null, legacyAvailability);
        if (!currentConnection
            || !legacyAvailability
            || quotes.RefillMana is not { } refillQuote
            || !TryClassify(refillQuote, out var reason))
            return Unavailable(StoreServiceQuoteAction.RefillMana);

        return reason switch {
            CommandRejectReason.Unspecified => Available(StoreServiceQuoteAction.RefillMana, refillQuote.Price),
            CommandRejectReason.InsufficientResources => Insufficient(StoreServiceQuoteAction.RefillMana, refillQuote.Price),
            _ => Unavailable(StoreServiceQuoteAction.RefillMana),
        };
    }

    private static bool TryGetItemQuote(
        IEnumerable<StoreItemServiceQuotes> rows,
        PlayerItemReference selectedItem,
        StoreServiceQuoteAction action,
        out StoreServicePriceQuote quote)
    {
        quote = new StoreServicePriceQuote();
        if (!IsSupportedReference(selectedItem))
            return false;

        var occupiedSlots = new HashSet<(PlayerItemLocation Location, uint Page, uint Slot)>();
        StoreItemServiceQuotes? match = null;
        foreach (var row in rows) {
            if (row is null || row.Item is null || !IsSupportedReference(row.Item)
                || !occupiedSlots.Add((row.Item.Location, row.Item.Page, row.Item.Slot))
                || (row.Repair is not null && !TryClassify(row.Repair, out _))
                || (row.Recharge is not null && !TryClassify(row.Recharge, out _))
                || (row.Identify is not null && !TryClassify(row.Identify, out _)))
                return false;

            if (SameReference(row.Item, selectedItem)) {
                if (match is not null)
                    return false;
                match = row;
            }
        }

        if (match is null)
            return false;
        var selectedQuote = action switch {
            StoreServiceQuoteAction.Repair => match.Repair,
            StoreServiceQuoteAction.Recharge => match.Recharge,
            StoreServiceQuoteAction.Identify => match.Identify,
            _ => null,
        };
        if (selectedQuote is null)
            return false;
        quote = selectedQuote;
        return true;
    }

    private static bool IsSupportedReference(PlayerItemReference reference)
    {
        return reference.Page == 0 && reference.Location is
            PlayerItemLocation.Inventory or
            PlayerItemLocation.Belt or
            PlayerItemLocation.Equipment;
    }

    private static bool SameReference(PlayerItemReference left, PlayerItemReference right)
    {
        return left.Location == right.Location
            && left.Slot == right.Slot
            && left.StableItemId == right.StableItemId
            && left.Page == right.Page;
    }

    private static bool TryClassify(StoreServicePriceQuote quote, out CommandRejectReason reason)
    {
        reason = quote.RejectReason;
        switch (reason) {
        case CommandRejectReason.Unspecified:
        case CommandRejectReason.InsufficientResources:
            return true;
        case CommandRejectReason.Malformed:
        case CommandRejectReason.TooLate:
        case CommandRejectReason.NotAllowed:
        case CommandRejectReason.InvalidTarget:
            return quote.Price == 0;
        default:
            return false;
        }
    }

    private static StoreServiceQuotePresentation Available(StoreServiceQuoteAction action, uint price)
    {
        var verb = Verb(action);
        var priceText = price == 0 ? "free" : $"{price.ToString(CultureInfo.InvariantCulture)} gold";
        return new(StoreServiceQuoteState.Available, $"{verb} — {priceText}", price, true);
    }

    private static StoreServiceQuotePresentation Insufficient(StoreServiceQuoteAction action, uint price)
    {
        var verb = Verb(action);
        return new(
            StoreServiceQuoteState.InsufficientResources,
            $"{verb} — need {price.ToString(CultureInfo.InvariantCulture)} gold",
            price,
            false);
    }

    private static StoreServiceQuotePresentation Unavailable(StoreServiceQuoteAction action)
    {
        return new(StoreServiceQuoteState.Unavailable, $"{Verb(action)} unavailable", null, false);
    }

    private static string Verb(StoreServiceQuoteAction action)
    {
        return action switch {
            StoreServiceQuoteAction.Repair => "Repair selected",
            StoreServiceQuoteAction.Recharge => "Recharge selected",
            StoreServiceQuoteAction.Identify => "Identify selected",
            StoreServiceQuoteAction.RefillMana => "Refill mana",
            _ => "Service unavailable",
        };
    }
}
