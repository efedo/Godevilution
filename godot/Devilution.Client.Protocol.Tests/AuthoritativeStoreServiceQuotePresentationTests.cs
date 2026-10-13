using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeStoreServiceQuotePresentationTests
{
    private static readonly PlayerItemReference Selected = Reference(
        PlayerItemLocation.Inventory, slot: 2, stableItemId: 41);

    [Fact]
    public void MissingQuotesPreserveLegacyAvailabilityAndText()
    {
        var item = AuthoritativeStoreServiceQuotePresentation.ResolveItem(
            new StoreSnapshot(), Selected, StoreServiceQuoteAction.Repair,
            currentConnectionAndTarget: true, legacyAvailability: true);
        var refill = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            new StoreSnapshot(), currentConnection: true, legacyAvailability: true);

        Assert.Equal(StoreServiceQuoteState.Legacy, item.State);
        Assert.Null(item.ButtonText);
        Assert.True(item.CanInvoke);
        Assert.Equal(StoreServiceQuoteState.Legacy, refill.State);
        Assert.Null(refill.ButtonText);
        Assert.True(refill.CanInvoke);
    }

    [Fact]
    public void PresentQuotesWithoutRefillProjectionFailClosed()
    {
        var result = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            Store(), currentConnection: true, legacyAvailability: true);

        AssertUnavailableWithoutPrice(result);
    }

    [Fact]
    public void AvailableQuotesShowZeroAsFreeAndPositivePriceAsGold()
    {
        var store = Store(Row(Selected, CommandRejectReason.Unspecified, 0));
        var free = ResolveItem(store, StoreServiceQuoteAction.Repair);
        store.Quotes.Items[0].Repair.Price = 125;
        var priced = ResolveItem(store, StoreServiceQuoteAction.Repair);

        Assert.Equal("Repair selected — free", free.ButtonText);
        Assert.Equal(0U, free.Price);
        Assert.True(free.CanInvoke);
        Assert.Equal("Repair selected — 125 gold", priced.ButtonText);
        Assert.Equal(125U, priced.Price);
        Assert.True(priced.CanInvoke);
    }

    [Fact]
    public void ServiceSpecificVendorMayOmitUnsupportedQuoteFields()
    {
        var store = Store(new StoreItemServiceQuotes {
            Item = Selected,
            Repair = new StoreServicePriceQuote {
                RejectReason = CommandRejectReason.Unspecified,
                Price = 18,
            },
        });

        var repair = ResolveItem(store, StoreServiceQuoteAction.Repair);
        var recharge = ResolveItem(store, StoreServiceQuoteAction.Recharge);

        Assert.Equal("Repair selected — 18 gold", repair.ButtonText);
        Assert.True(repair.CanInvoke);
        AssertUnavailableWithoutPrice(recharge);
    }

    [Fact]
    public void SelectedServiceUsesItsOwnReasonAndPrice()
    {
        var store = Store(new StoreItemServiceQuotes {
            Item = Selected,
            Repair = new StoreServicePriceQuote {
                RejectReason = CommandRejectReason.InsufficientResources,
                Price = 23,
            },
            Recharge = new StoreServicePriceQuote {
                RejectReason = CommandRejectReason.Unspecified,
                Price = 7,
            },
            Identify = new StoreServicePriceQuote {
                RejectReason = CommandRejectReason.NotAllowed,
            },
        });

        var repair = ResolveItem(store, StoreServiceQuoteAction.Repair);
        var recharge = ResolveItem(store, StoreServiceQuoteAction.Recharge);
        var identify = ResolveItem(store, StoreServiceQuoteAction.Identify);

        Assert.Equal("Repair selected — need 23 gold", repair.ButtonText);
        Assert.False(repair.CanInvoke);
        Assert.Equal("Recharge selected — 7 gold", recharge.ButtonText);
        Assert.True(recharge.CanInvoke);
        Assert.Equal("Identify selected unavailable", identify.ButtonText);
        Assert.Null(identify.Price);
        Assert.False(identify.CanInvoke);
    }

    [Fact]
    public void LegacyZeroStableItemIdCanMatchExactly()
    {
        var legacyReference = Reference(PlayerItemLocation.Belt, slot: 1, stableItemId: 0);
        var store = Store(Row(legacyReference, CommandRejectReason.Unspecified, 12));
        var result = AuthoritativeStoreServiceQuotePresentation.ResolveItem(
            store, legacyReference, StoreServiceQuoteAction.Recharge,
            currentConnectionAndTarget: true, legacyAvailability: true);

        Assert.Equal("Recharge selected — 12 gold", result.ButtonText);
        Assert.True(result.CanInvoke);
    }

    [Fact]
    public void InsufficientResourcesRetainsNeededPriceAndDisablesAction()
    {
        var result = ResolveItem(
            Store(Row(Selected, CommandRejectReason.InsufficientResources, 75)),
            StoreServiceQuoteAction.Repair,
            legacyAvailability: true);

        Assert.Equal(StoreServiceQuoteState.InsufficientResources, result.State);
        Assert.Equal("Repair selected — need 75 gold", result.ButtonText);
        Assert.Equal(75U, result.Price);
        Assert.False(result.CanInvoke);
    }

    [Fact]
    public void PermissionDenialHidesAvailableAndInsufficientPrices()
    {
        var available = ResolveItem(
            Store(Row(Selected, CommandRejectReason.Unspecified, 25)),
            StoreServiceQuoteAction.Repair,
            legacyAvailability: false);
        var insufficient = ResolveItem(
            Store(Row(Selected, CommandRejectReason.InsufficientResources, 75)),
            StoreServiceQuoteAction.Repair,
            legacyAvailability: false);

        AssertUnavailableWithoutPrice(available);
        AssertUnavailableWithoutPrice(insufficient);
    }

    [Fact]
    public void OtherKnownRejectionsHidePriceAndUseGenericUnavailableText()
    {
        foreach (var reason in new[] {
            CommandRejectReason.Malformed,
            CommandRejectReason.TooLate,
            CommandRejectReason.NotAllowed,
            CommandRejectReason.InvalidTarget,
        }) {
            var result = ResolveItem(
                Store(Row(Selected, reason, 0)), StoreServiceQuoteAction.Identify);

            Assert.Equal(StoreServiceQuoteState.Unavailable, result.State);
            Assert.Equal("Identify selected unavailable", result.ButtonText);
            Assert.Null(result.Price);
            Assert.False(result.CanInvoke);
        }
    }

    [Fact]
    public void SelectedQuoteMustMatchLocationSlotStableIdAndPage()
    {
        var wrongReferences = new[] {
            Reference(PlayerItemLocation.Belt, 2, 41),
            Reference(PlayerItemLocation.Inventory, 3, 41),
            Reference(PlayerItemLocation.Inventory, 2, 42),
            Reference(PlayerItemLocation.Inventory, 2, 41, page: 1),
        };

        foreach (var wrong in wrongReferences) {
            var result = ResolveItem(Store(Row(wrong, CommandRejectReason.Unspecified, 9)),
                StoreServiceQuoteAction.Repair);
            AssertUnavailableWithoutPrice(result);
        }
    }

    [Fact]
    public void MissingSelectedQuoteIsUnavailableWithoutPrice()
    {
        var result = ResolveItem(Store(Row(
            Reference(PlayerItemLocation.Inventory, 1, 12),
            CommandRejectReason.Unspecified, 20)), StoreServiceQuoteAction.Repair);

        AssertUnavailableWithoutPrice(result);
    }

    [Fact]
    public void DuplicateSlotRowsFailClosedEvenWhenStableIdsDiffer()
    {
        var store = Store(
            Row(Selected, CommandRejectReason.Unspecified, 9),
            Row(Reference(PlayerItemLocation.Inventory, 2, 99), CommandRejectReason.Unspecified, 11));

        AssertUnavailableWithoutPrice(ResolveItem(store, StoreServiceQuoteAction.Repair));
    }

    [Fact]
    public void StashAndNonzeroPageReferencesAreOutsideThePaidQuoteContract()
    {
        var stash = Reference(PlayerItemLocation.Stash, 2, 41, page: 0);
        var nonzeroPage = Reference(PlayerItemLocation.Inventory, 2, 41, page: 1);

        AssertUnavailableWithoutPrice(ResolveItem(
            Store(Row(stash, CommandRejectReason.Unspecified, 10)), StoreServiceQuoteAction.Repair));
        AssertUnavailableWithoutPrice(ResolveItem(
            Store(Row(nonzeroPage, CommandRejectReason.Unspecified, 10)), StoreServiceQuoteAction.Repair));
    }

    [Fact]
    public void MalformedUnselectedRowInvalidatesWholeQuoteList()
    {
        var store = Store(Row(Selected, CommandRejectReason.Unspecified, 9));
        store.Quotes.Items.Add(new StoreItemServiceQuotes {
            Item = Reference(PlayerItemLocation.Belt, 0, 52),
            Repair = new StoreServicePriceQuote {
                RejectReason = (CommandRejectReason)99,
            },
        });

        AssertUnavailableWithoutPrice(ResolveItem(store, StoreServiceQuoteAction.Repair));
    }

    [Fact]
    public void UnknownRejectReasonIsUnavailable()
    {
        var result = ResolveItem(
            Store(Row(Selected, (CommandRejectReason)99, 0)), StoreServiceQuoteAction.Repair);

        AssertUnavailableWithoutPrice(result);
    }

    [Fact]
    public void NonzeroPriceForNonResourceRejectionIsMalformed()
    {
        var result = ResolveItem(
            Store(Row(Selected, CommandRejectReason.NotAllowed, 10)), StoreServiceQuoteAction.Repair);

        AssertUnavailableWithoutPrice(result);
    }

    [Fact]
    public void MissingNestedQuoteForRequestedActionIsUnavailable()
    {
        var store = Store(new StoreItemServiceQuotes {
            Item = Reference(PlayerItemLocation.Inventory, 2, 41),
            Repair = new StoreServicePriceQuote { RejectReason = CommandRejectReason.Unspecified },
            Recharge = new StoreServicePriceQuote { RejectReason = CommandRejectReason.Unspecified },
            // Identify is absent.
        });

        AssertUnavailableWithoutPrice(ResolveItem(store, StoreServiceQuoteAction.Identify));
    }

    [Fact]
    public void DisconnectedOrStaleTargetDoesNotExposeQuote()
    {
        var store = Store(Row(Selected, CommandRejectReason.InsufficientResources, 80));

        AssertUnavailableWithoutPrice(ResolveItem(
            store, StoreServiceQuoteAction.Repair, current: false));
        AssertUnavailableWithoutPrice(ResolveItem(
            store, StoreServiceQuoteAction.Repair, current: true, legacyAvailability: false));
    }

    [Fact]
    public void RefillQuotesShowFreeAndPositiveServerPrices()
    {
        var store = Store();
        store.Quotes.RefillMana = new StoreServicePriceQuote {
            RejectReason = CommandRejectReason.Unspecified,
            Price = 0,
        };
        var free = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            store, currentConnection: true, legacyAvailability: true);
        store.Quotes.RefillMana.Price = 30;
        var priced = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            store, currentConnection: true, legacyAvailability: true);

        Assert.Equal("Refill mana — free", free.ButtonText);
        Assert.True(free.CanInvoke);
        Assert.Equal("Refill mana — 30 gold", priced.ButtonText);
        Assert.Equal(30U, priced.Price);
        Assert.True(priced.CanInvoke);
    }

    [Fact]
    public void RefillInsufficientResourcesShowsCostButRemainsDisabled()
    {
        var store = Store();
        store.Quotes.RefillMana = new StoreServicePriceQuote {
            RejectReason = CommandRejectReason.InsufficientResources,
            Price = 40,
        };
        var result = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            store, currentConnection: true, legacyAvailability: true);
        var denied = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            store, currentConnection: true, legacyAvailability: false);

        Assert.Equal("Refill mana — need 40 gold", result.ButtonText);
        Assert.Equal(40U, result.Price);
        Assert.False(result.CanInvoke);
        AssertUnavailableWithoutPrice(denied);
    }

    [Fact]
    public void RefillOtherRejectionsAreGenericAndPriceFree()
    {
        var store = Store();
        store.Quotes.RefillMana = new StoreServicePriceQuote {
            RejectReason = CommandRejectReason.NotAllowed,
            Price = 0,
        };
        var result = AuthoritativeStoreServiceQuotePresentation.ResolveRefillMana(
            store, currentConnection: true, legacyAvailability: true);

        AssertUnavailableWithoutPrice(result);
        Assert.Equal("Refill mana unavailable", result.ButtonText);
    }

    [Fact]
    public void InvalidActionCannotSelectAnItemQuote()
    {
        var result = ResolveItem(Store(Row(Selected, CommandRejectReason.Unspecified, 1)),
            (StoreServiceQuoteAction)99);

        AssertUnavailableWithoutPrice(result);
    }

    private static StoreServiceQuotePresentation ResolveItem(
        StoreSnapshot store,
        StoreServiceQuoteAction action,
        bool current = true,
        bool legacyAvailability = true)
    {
        return AuthoritativeStoreServiceQuotePresentation.ResolveItem(
            store, Selected, action, current, legacyAvailability);
    }

    private static StoreSnapshot Store(params StoreItemServiceQuotes[] rows)
    {
        var quotes = new StoreServiceQuotesSnapshot();
        quotes.Items.AddRange(rows);
        return new StoreSnapshot { Quotes = quotes };
    }

    private static StoreItemServiceQuotes Row(
        PlayerItemReference item,
        CommandRejectReason reason,
        uint price)
    {
        var quote = new StoreServicePriceQuote { RejectReason = reason, Price = price };
        return new StoreItemServiceQuotes {
            Item = item,
            Repair = quote.Clone(),
            Recharge = quote.Clone(),
            Identify = quote.Clone(),
        };
    }

    private static PlayerItemReference Reference(
        PlayerItemLocation location,
        uint slot,
        uint stableItemId,
        uint page = 0)
    {
        return new PlayerItemReference {
            Location = location,
            Slot = slot,
            StableItemId = stableItemId,
            Page = page,
        };
    }

    private static void AssertUnavailableWithoutPrice(StoreServiceQuotePresentation result)
    {
        Assert.Equal(StoreServiceQuoteState.Unavailable, result.State);
        Assert.Null(result.Price);
        Assert.False(result.CanInvoke);
    }
}
