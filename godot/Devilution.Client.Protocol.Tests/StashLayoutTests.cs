using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class StashLayoutTests
{
    [Fact]
    public void BuildsPageOccupancyAndPreservesOffPageAnchors()
    {
        var items = new[] {
            Item(2, 11, 2, 1),
            Item(3, 5, 1, 1),
        };

        var layout = StashLayout.Build(items, 2);

        Assert.Equal(11, layout.Anchors[0]);
        Assert.Equal(-1, layout.Anchors[1]);
        Assert.Equal(0, layout.GetItemIndexAt(11));
        Assert.Equal(0, layout.GetItemIndexAt(12));
        Assert.Equal(-1, layout.GetItemIndexAt(5));
    }

    [Fact]
    public void RejectsOverlappingOrOutOfBoundsPageItems()
    {
        var overlapping = new[] {
            Item(0, 10, 2, 1),
            Item(0, 11, 1, 1),
        };
        var outOfBounds = new[] { Item(0, 19, 2, 1) };

        Assert.Throws<InvalidDataException>(() => StashLayout.Build(overlapping, 0));
        Assert.Throws<InvalidDataException>(() => StashLayout.Build(outOfBounds, 0));
    }

    [Fact]
    public void KeepsEachPagedGridIndependentAcrossTheFullPageRange()
    {
        var items = new[] {
            Item(0, 99, 1, 1),
            Item(99, 0, 1, 1),
        };

        var first = StashLayout.Build(items, 0);
        var last = StashLayout.Build(items, 99);

        Assert.Equal(0, first.GetItemIndexAt(99));
        Assert.Equal(-1, first.GetItemIndexAt(0));
        Assert.Equal(1, last.GetItemIndexAt(0));
        Assert.Equal(-1, last.GetItemIndexAt(99));
    }

    [Fact]
    public void ClientModelValidatesAndExposesAuthoritativeStashPage()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 7,
                        StashGold = 123,
                        StashOpen = true,
                        Stash = { Item(4, 22, 1, 2) },
                    },
                },
            }));

        var layout = model.GetStashLayout(4);

        Assert.Equal(22, layout.Anchors[0]);
        Assert.True(model.IsStashOpen);
        Assert.Equal(0, layout.GetItemIndexAt(32));
        Assert.Equal(-1, layout.GetItemIndexAt(42));
    }

    [Fact]
    public void ClientModelRejectsOverlappingAuthoritativeSessions()
    {
        var model = new AuthoritativeClientModel();

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 7,
                        ActiveStoreId = 1,
                        StashOpen = true,
                    },
                },
            })));
    }

    private static StashItemSnapshot Item(uint page, uint cell, uint width, uint height)
    {
        return new StashItemSnapshot {
            Page = page,
            Cell = cell,
            Item = new ItemSnapshot {
                ItemSeed = checked(page * 1000 + cell + 1),
                State = new ItemStateSnapshot {
                    ItemType = 1,
                    ItemIndex = checked((int)cell),
                    InventoryWidth = width,
                    InventoryHeight = height,
                },
            },
        };
    }
}
