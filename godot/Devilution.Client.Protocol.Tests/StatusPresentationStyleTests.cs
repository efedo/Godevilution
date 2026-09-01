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
}
