using Devilution.Domain.Inventory;

namespace Devilution.Diablo;

/** Native-compatible stash dimensions and deterministic page-grid helpers. */
public static class DiabloStashLayout
{
    public const int PageCount = 100;
    public const int ColumnCount = InventoryGridLayout.ColumnCount;
    public const int RowCount = 10;
    public const int CellCount = ColumnCount * RowCount;
    public const int MaximumItems = PageCount * CellCount;

    public static int[] CreateEmptyPage()
    {
        return Enumerable.Repeat(-1, CellCount).ToArray();
    }

    public static bool CanPlace(
        IReadOnlyList<int> cells,
        uint targetCell,
        int width,
        int height)
    {
        return cells.Count == CellCount
            && InventoryGridLayout.CanPlace(cells, targetCell, width, height);
    }

    public static void Fill(
        IList<int> cells,
        uint targetCell,
        int width,
        int height,
        int itemIndex = 0)
    {
        InventoryGridLayout.Fill(
            cells,
            checked((int)targetCell),
            itemIndex,
            width,
            height);
    }
}
