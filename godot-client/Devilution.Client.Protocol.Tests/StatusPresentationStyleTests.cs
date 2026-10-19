using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class StatusPresentationStyleTests
{
    [Theory]
    [InlineData(StatusEffectBehavior.Infravision, StatusPresentationKind.Infravision)]
    [InlineData(StatusEffectBehavior.Search, StatusPresentationKind.Search)]
    [InlineData(StatusEffectBehavior.Etherealize, StatusPresentationKind.Etherealize)]
    [InlineData(StatusEffectBehavior.Unspecified, StatusPresentationKind.None)]
    public void ResolvesAuthoritativeStatusFamilies(
        StatusEffectBehavior behavior,
        StatusPresentationKind expected)
    {
        Assert.Equal(expected, StatusPresentationStyle.Resolve(behavior));
    }

    [Fact]
    public void ResolvesPlayerStatusesInStableOrderWithServerValues()
    {
        var player = new PlayerSnapshot {
            ManaShieldActive = true,
            ReflectCharges = 3,
        };
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 2,
            RemainingTicks = 100,
            Behavior = StatusEffectBehavior.Search,
        });
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 1,
            RemainingTicks = 200,
            Behavior = StatusEffectBehavior.Infravision,
        });

        var badges = StatusPresentationStyle.Resolve(player);

        Assert.Equal(
            [
                StatusPresentationKind.ManaShield,
                StatusPresentationKind.Reflect,
                StatusPresentationKind.Infravision,
                StatusPresentationKind.Search,
            ],
            badges.Select(badge => badge.Kind));
        Assert.Equal(3U, badges[1].Charges);
        Assert.Equal(200U, badges[2].RemainingTicks);
        Assert.Equal("Mana Shield   Reflect x3   Infravision   Search", StatusPresentationStyle.Format(badges));
    }

    [Fact]
    public void UsesLongestRemainingDurationForDuplicatePresentationFamilies()
    {
        var player = new PlayerSnapshot();
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 1,
            RemainingTicks = 4,
            Behavior = StatusEffectBehavior.Search,
        });
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 2,
            RemainingTicks = 9,
            Behavior = StatusEffectBehavior.Search,
        });

        var badge = Assert.Single(StatusPresentationStyle.Resolve(player));

        Assert.Equal(StatusPresentationKind.Search, badge.Kind);
        Assert.Equal(9U, badge.RemainingTicks);
    }

    [Fact]
    public void ReusableProjectionExposesOnlyActivePresentationFamilies()
    {
        var player = new PlayerSnapshot {
            ManaShieldActive = true,
        };
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 1,
            RemainingTicks = 0,
            Behavior = StatusEffectBehavior.Search,
        });
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 2,
            RemainingTicks = 4,
            Behavior = StatusEffectBehavior.Etherealize,
        });

        var projection = StatusPresentationStyle.ResolvePlayer(player);

        Assert.True(projection.Contains(StatusPresentationKind.ManaShield));
        Assert.True(projection.Contains(StatusPresentationKind.Etherealize));
        Assert.False(projection.Contains(StatusPresentationKind.Search));
        Assert.Equal(
            [StatusPresentationKind.ManaShield, StatusPresentationKind.Etherealize],
            projection.Badges.Select(badge => badge.Kind));
    }

    [Fact]
    public void DetailedFormatPreservesServerOwnedDurationsAndPermanentStatuses()
    {
        var player = new PlayerSnapshot();
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 1,
            RemainingTicks = 12,
            Behavior = StatusEffectBehavior.Search,
        });
        player.StatusEffects.Add(new StatusEffectSnapshot {
            EffectId = 2,
            RemainingTicks = uint.MaxValue,
            Behavior = StatusEffectBehavior.Etherealize,
        });

        var projection = StatusPresentationStyle.ResolvePlayer(player);

        Assert.Equal(
            "Search (12 ticks)   Etherealize (permanent)",
            StatusPresentationStyle.FormatDetailed(projection.Badges));
    }

    [Fact]
    public void DetailedFormatUsesStableEmptyState()
    {
        Assert.Equal("none", StatusPresentationStyle.FormatDetailed([]));
    }
}
