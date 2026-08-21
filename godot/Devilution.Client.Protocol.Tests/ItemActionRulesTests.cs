using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ItemActionRulesTests
{
    [Fact]
    public void AcceptsStableReferenceToAnOpenStashItem()
    {
        var player = new PlayerSnapshot {
            StashOpen = true,
            Stash = {
                new StashItemSnapshot {
                    Page = 7,
                    Cell = 42,
                    Item = Item(7001),
                },
            },
        };

        Assert.True(AuthoritativeItemActionRules.IsServiceTarget(
            player,
            new PlayerItemReference {
                Location = PlayerItemLocation.Stash,
                Page = 7,
                Slot = 42,
                StableItemId = 7001,
            },
            stashOpen: true));
    }

    [Fact]
    public void RejectsClosedStashAndStaleStableReferences()
    {
        var player = new PlayerSnapshot {
            Stash = {
                new StashItemSnapshot {
                    Page = 7,
                    Cell = 42,
                    Item = Item(7001),
                },
            },
        };
        var reference = new PlayerItemReference {
            Location = PlayerItemLocation.Stash,
            Page = 7,
            Slot = 42,
            StableItemId = 7002,
        };

        Assert.False(AuthoritativeItemActionRules.IsServiceTarget(
            player,
            reference,
            stashOpen: false));
        Assert.False(AuthoritativeItemActionRules.IsServiceTarget(
            player,
            reference,
            stashOpen: true));
    }

    private static ItemSnapshot Item(uint stableItemId)
    {
        return new ItemSnapshot {
            State = new ItemStateSnapshot {
                StableItemId = stableItemId,
            },
        };
    }
}
