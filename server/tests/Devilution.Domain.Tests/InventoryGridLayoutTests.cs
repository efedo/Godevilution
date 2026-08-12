using Devilution.Domain.Inventory;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class InventoryGridLayoutTests
{
    [Fact]
    public void PlacementRespectsTenColumnGeometry()
    {
        var cells = Enumerable.Repeat(-1, 40).ToArray();

        Assert.True(InventoryGridLayout.CanPlace(cells, 8, width: 2, height: 2));
        Assert.False(InventoryGridLayout.CanPlace(cells, 9, width: 2, height: 1));
        Assert.False(InventoryGridLayout.CanPlace(cells, 38, width: 2, height: 2));
        Assert.False(InventoryGridLayout.CanPlace(cells, 40, width: 1, height: 1));

        var partialRow = Enumerable.Repeat(-1, 3).ToArray();
        Assert.False(InventoryGridLayout.CanPlace(partialRow, 2, width: 2, height: 1));
        Assert.False(InventoryGridLayout.HasExactFootprint(partialRow, 2, itemIndex: 0, width: 2, height: 1));
    }

    [Fact]
    public void PlacementCanIgnoreItemsParticipatingInAnAtomicMove()
    {
        var cells = Enumerable.Repeat(-1, 40).ToArray();
        cells[8] = 0;
        cells[9] = 0;
        cells[18] = 1;
        cells[19] = 1;

        Assert.False(InventoryGridLayout.CanPlace(cells, 8, width: 2, height: 2, 0));
        Assert.True(InventoryGridLayout.CanPlace(cells, 8, width: 2, height: 2, 0, 1));
    }

    [Fact]
    public void FillClearAndFootprintChecksShareOneCanonicalLayout()
    {
        var cells = Enumerable.Repeat(-1, 40).ToArray();

        InventoryGridLayout.Fill(cells, targetCell: 8, itemIndex: 3, width: 2, height: 2);

        Assert.Equal(8, InventoryGridLayout.FindAnchor(cells, 3));
        Assert.True(InventoryGridLayout.HasExactFootprint(cells, 8, itemIndex: 3, width: 2, height: 2));
        Assert.Equal(3, InventoryGridLayout.GetItemIndexAt(cells, 18));

        InventoryGridLayout.Clear(cells, 3);

        Assert.DoesNotContain(3, cells);
        Assert.Equal(-1, InventoryGridLayout.FindAnchor(cells, 3));
    }

    [Fact]
    public void RemovalCompactsLaterInventoryIndices()
    {
        var cells = new[] { 0, 1, 1, 2, -1 };

        InventoryGridLayout.RemoveAndCompact(cells, removedItemIndex: 1);

        Assert.Equal(new[] { 0, -1, -1, 1, -1 }, cells);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(10, 40, true)]
    [InlineData(0, 1, false)]
    [InlineData(11, 1, false)]
    [InlineData(1, 41, false)]
    public void DimensionsHaveExplicitAuthoritativeLimits(int width, int height, bool expected)
    {
        Assert.Equal(expected, InventoryGridLayout.HasValidDimensions(width, height));
    }
}
