using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeStoreServicePresentationTests
{
    [Theory]
    [InlineData(1U, true, false, false, false)]
    [InlineData(2U, false, true, false, false)]
    [InlineData(11U, false, false, true, false)]
    [InlineData(10U, false, false, false, true)]
    [InlineData(3U, false, false, false, false)]
    [InlineData(4U, false, false, false, false)]
    [InlineData(99U, false, false, false, false)]
    public void UsesOnlyTheCurrentStoreCapabilities(
        uint storeId,
        bool repair,
        bool recharge,
        bool identify,
        bool refillMana)
    {
        var availability = Evaluate(
            connected: true,
            activeStore: Store(storeId, repair, recharge, identify, refillMana),
            hasSelectedServiceItem: true,
            hasRepairAbility: true,
            hasRechargeAbility: true,
            hasIdentifyAbility: true,
            hasIdentifyScroll: true);

        Assert.Equal(repair, availability.CanRepair);
        Assert.Equal(recharge, availability.CanRecharge);
        Assert.Equal(identify, availability.CanIdentify);
        Assert.Equal(refillMana, availability.CanRefillMana);
    }

    [Theory]
    [InlineData(true, false, false, false, true, false, false)]
    [InlineData(false, true, false, false, false, true, false)]
    [InlineData(false, false, true, false, false, false, true)]
    [InlineData(false, false, false, true, false, false, true)]
    public void PreservesClassAbilityAndScrollActionsWithoutAStore(
        bool repairAbility,
        bool rechargeAbility,
        bool identifyAbility,
        bool identifyScroll,
        bool expectedRepair,
        bool expectedRecharge,
        bool expectedIdentify)
    {
        var availability = Evaluate(
            connected: true,
            activeStore: null,
            hasSelectedServiceItem: true,
            repairAbility,
            rechargeAbility,
            identifyAbility,
            identifyScroll);

        Assert.Equal(expectedRepair, availability.CanRepair);
        Assert.Equal(expectedRecharge, availability.CanRecharge);
        Assert.Equal(expectedIdentify, availability.CanIdentify);
        Assert.True(availability.CanRefillMana);
    }

    [Fact]
    public void NoStoreWithoutAbilityDisablesItemServicesButKeepsLegacyRefill()
    {
        var availability = Evaluate(
            connected: true,
            activeStore: null,
            hasSelectedServiceItem: true,
            hasRepairAbility: false,
            hasRechargeAbility: false,
            hasIdentifyAbility: false,
            hasIdentifyScroll: false);

        Assert.False(availability.CanRepair);
        Assert.False(availability.CanRecharge);
        Assert.False(availability.CanIdentify);
        Assert.True(availability.CanRefillMana);
    }

    [Fact]
    public void MissingServiceMetadataPreservesLegacyActiveStoreAvailability()
    {
        var availability = Evaluate(
            connected: true,
            activeStore: new StoreSnapshot { StoreId = 99 },
            hasSelectedServiceItem: true,
            hasRepairAbility: false,
            hasRechargeAbility: false,
            hasIdentifyAbility: false,
            hasIdentifyScroll: false);

        Assert.True(availability.CanRepair);
        Assert.True(availability.CanRecharge);
        Assert.True(availability.CanIdentify);
        Assert.True(availability.CanRefillMana);
    }

    [Fact]
    public void PresentDeniedCapabilitiesDoNotFallBackToClassAbilitiesOrScrolls()
    {
        var availability = Evaluate(
            connected: true,
            activeStore: Store(99, false, false, false, false),
            hasSelectedServiceItem: true,
            hasRepairAbility: true,
            hasRechargeAbility: true,
            hasIdentifyAbility: true,
            hasIdentifyScroll: true);

        Assert.False(availability.CanRepair);
        Assert.False(availability.CanRecharge);
        Assert.False(availability.CanIdentify);
        Assert.False(availability.CanRefillMana);
    }

    [Fact]
    public void ConnectionAndStableServiceTargetGateOnlyItemActions()
    {
        var store = Store(1, true, true, true, true);
        var disconnected = Evaluate(
            connected: false,
            activeStore: store,
            hasSelectedServiceItem: true,
            hasRepairAbility: false,
            hasRechargeAbility: false,
            hasIdentifyAbility: false,
            hasIdentifyScroll: false);
        Assert.False(disconnected.CanRepair);
        Assert.False(disconnected.CanRecharge);
        Assert.False(disconnected.CanIdentify);
        Assert.False(disconnected.CanRefillMana);

        var noTarget = Evaluate(
            connected: true,
            activeStore: store,
            hasSelectedServiceItem: false,
            hasRepairAbility: false,
            hasRechargeAbility: false,
            hasIdentifyAbility: false,
            hasIdentifyScroll: false);
        Assert.False(noTarget.CanRepair);
        Assert.False(noTarget.CanRecharge);
        Assert.False(noTarget.CanIdentify);
        Assert.True(noTarget.CanRefillMana);
    }

    [Fact]
    public void StoreReplacementAndCloseUseOnlyTheLatestSnapshot()
    {
        var smith = Evaluate(
            connected: true,
            activeStore: Store(1, true, false, false, false),
            hasSelectedServiceItem: true,
            hasRepairAbility: false,
            hasRechargeAbility: false,
            hasIdentifyAbility: false,
            hasIdentifyScroll: false);
        var witch = Evaluate(
            connected: true,
            activeStore: Store(2, false, true, false, false),
            hasSelectedServiceItem: true,
            hasRepairAbility: false,
            hasRechargeAbility: false,
            hasIdentifyAbility: false,
            hasIdentifyScroll: false);
        var closed = Evaluate(
            connected: true,
            activeStore: null,
            hasSelectedServiceItem: true,
            hasRepairAbility: false,
            hasRechargeAbility: false,
            hasIdentifyAbility: false,
            hasIdentifyScroll: false);

        Assert.True(smith.CanRepair);
        Assert.False(smith.CanRecharge);
        Assert.False(witch.CanRepair);
        Assert.True(witch.CanRecharge);
        Assert.False(closed.CanRepair);
        Assert.False(closed.CanRecharge);
    }

    private static AuthoritativeStoreServiceAvailability Evaluate(
        bool connected,
        StoreSnapshot? activeStore,
        bool hasSelectedServiceItem,
        bool hasRepairAbility,
        bool hasRechargeAbility,
        bool hasIdentifyAbility,
        bool hasIdentifyScroll)
    {
        return AuthoritativeStoreServicePresentation.Evaluate(
            connected,
            activeStore,
            hasSelectedServiceItem,
            hasRepairAbility,
            hasRechargeAbility,
            hasIdentifyAbility,
            hasIdentifyScroll);
    }

    private static StoreSnapshot Store(
        uint storeId,
        bool repair,
        bool recharge,
        bool identify,
        bool refillMana)
    {
        return new StoreSnapshot {
            StoreId = storeId,
            Services = new StoreServicesSnapshot {
                Repair = repair,
                Recharge = recharge,
                Identify = identify,
                RefillMana = refillMana,
            },
        };
    }
}