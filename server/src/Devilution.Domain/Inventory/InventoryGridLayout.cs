namespace Devilution.Domain.Inventory;

/** Deterministic geometry and occupancy rules for the authoritative inventory grid. */
public static class InventoryGridLayout
{
    public const int ColumnCount = 10;

    public static bool HasValidDimensions(int width, int height)
    {
        return width is >= 1 and <= ColumnCount && height is >= 1 and <= 40;
    }

    public static bool IsWithinBounds(int cellCount, uint targetCell, int width, int height)
    {
        if (cellCount <= 0 || targetCell >= (uint)cellCount)
            return false;

        var normalizedWidth = Math.Max(1, width);
        var normalizedHeight = Math.Max(1, height);
        var column = (int)targetCell % ColumnCount;
        var row = (int)targetCell / ColumnCount;
        var rowCount = (cellCount + ColumnCount - 1) / ColumnCount;
        return column + (long)normalizedWidth <= ColumnCount
            && row + (long)normalizedHeight <= rowCount;
    }

    public static bool CanPlace(
        IReadOnlyList<int> cells,
        uint targetCell,
        int width,
        int height,
        params int[] ignoredItemIndices)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(ignoredItemIndices);
        if (!IsWithinBounds(cells.Count, targetCell, width, height))
            return false;

        var normalizedWidth = Math.Max(1, width);
        var normalizedHeight = Math.Max(1, height);
        var column = (int)targetCell % ColumnCount;
        var row = (int)targetCell / ColumnCount;
        for (var y = 0; y < normalizedHeight; y++) {
            for (var x = 0; x < normalizedWidth; x++) {
                var cell = (row + y) * ColumnCount + column + x;
                if (cell >= cells.Count)
                    return false;
                var occupant = cells[cell];
                if (occupant >= 0 && !ignoredItemIndices.Contains(occupant))
                    return false;
            }
        }
        return true;
    }

    public static bool HasExactFootprint(
        IReadOnlyList<int> cells,
        int targetCell,
        int itemIndex,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (targetCell < 0 || !IsWithinBounds(cells.Count, (uint)targetCell, width, height))
            return false;

        var normalizedWidth = Math.Max(1, width);
        var normalizedHeight = Math.Max(1, height);
        var column = targetCell % ColumnCount;
        var row = targetCell / ColumnCount;
        for (var y = 0; y < normalizedHeight; y++) {
            for (var x = 0; x < normalizedWidth; x++) {
                var cell = (row + y) * ColumnCount + column + x;
                if (cell >= cells.Count || cells[cell] != itemIndex)
                    return false;
            }
        }
        return true;
    }

    public static int FindFirstPlacement(IReadOnlyList<int> cells, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(cells);
        for (uint cell = 0; cell < (uint)cells.Count; cell++) {
            if (CanPlace(cells, cell, width, height))
                return (int)cell;
        }
        return -1;
    }

    public static int GetItemIndexAt(IReadOnlyList<int> cells, uint cell)
    {
        ArgumentNullException.ThrowIfNull(cells);
        return cell < (uint)cells.Count ? cells[(int)cell] : -1;
    }

    public static int FindAnchor(IReadOnlyList<int> cells, int itemIndex)
    {
        ArgumentNullException.ThrowIfNull(cells);
        for (var cell = 0; cell < cells.Count; cell++) {
            if (cells[cell] == itemIndex)
                return cell;
        }
        return -1;
    }

    public static void Clear(IList<int> cells, int itemIndex)
    {
        ArgumentNullException.ThrowIfNull(cells);
        for (var cell = 0; cell < cells.Count; cell++) {
            if (cells[cell] == itemIndex)
                cells[cell] = -1;
        }
    }

    public static void Fill(IList<int> cells, int targetCell, int itemIndex, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (targetCell < 0 || !IsWithinBounds(cells.Count, (uint)targetCell, width, height))
            throw new ArgumentOutOfRangeException(nameof(targetCell));

        var normalizedWidth = Math.Max(1, width);
        var normalizedHeight = Math.Max(1, height);
        var column = targetCell % ColumnCount;
        var row = targetCell / ColumnCount;
        for (var y = 0; y < normalizedHeight; y++)
            for (var x = 0; x < normalizedWidth; x++)
                cells[(row + y) * ColumnCount + column + x] = itemIndex;
    }

    public static void RemoveAndCompact(IList<int> cells, int removedItemIndex)
    {
        ArgumentNullException.ThrowIfNull(cells);
        for (var cell = 0; cell < cells.Count; cell++) {
            if (cells[cell] == removedItemIndex)
                cells[cell] = -1;
            else if (cells[cell] > removedItemIndex)
                cells[cell]--;
        }
    }
}
