using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Pure client projection of one server-owned native-compatible stash page. */
public sealed class StashLayout
{
    public const int PageCount = AuthoritativeProtocolLimits.NativeStashPageCount;
    public const int ColumnCount = AuthoritativeProtocolLimits.NativeStashGridWidth;
    public const int RowCount = AuthoritativeProtocolLimits.NativeStashGridHeight;
    public const int CellCount = AuthoritativeProtocolLimits.NativeStashCellCount;

    private StashLayout(uint page, int[] occupants, int[] anchors)
    {
        Page = page;
        Occupants = occupants;
        Anchors = anchors;
    }

    public uint Page { get; }

    public IReadOnlyList<int> Occupants { get; }

    /** Anchor cell for each item in the supplied snapshot list, or -1 off-page. */
    public IReadOnlyList<int> Anchors { get; }

    public static StashLayout Build(
        IReadOnlyList<StashItemSnapshot>? items,
        uint page)
    {
        if (page >= PageCount)
            throw new ArgumentOutOfRangeException(nameof(page));

        var occupants = Enumerable.Repeat(-1, CellCount).ToArray();
        var anchors = Enumerable.Repeat(-1, items?.Count ?? 0).ToArray();
        if (items is null)
            return new StashLayout(page, occupants, anchors);

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++) {
            var item = items[itemIndex]
                ?? throw new InvalidDataException("The stash projection contains a missing item.");
            if (item.Page != page)
                continue;
            var state = item.Item?.State
                ?? throw new InvalidDataException("The stash projection contains missing item state.");
            var width = checked((int)Math.Max(1U, state.InventoryWidth));
            var height = checked((int)Math.Max(1U, state.InventoryHeight));
            if (item.Cell >= CellCount
                || width > ColumnCount
                || height > RowCount
                || !CanFit(occupants, checked((int)item.Cell), width, height)) {
                throw new InvalidDataException("The stash projection contains an invalid footprint.");
            }

            anchors[itemIndex] = checked((int)item.Cell);
            Fill(occupants, checked((int)item.Cell), itemIndex, width, height);
        }

        return new StashLayout(page, occupants, anchors);
    }

    public int GetItemIndexAt(uint cell)
    {
        return cell < CellCount ? Occupants[(int)cell] : -1;
    }

    private static bool CanFit(
        IReadOnlyList<int> occupants,
        int anchor,
        int width,
        int height)
    {
        var column = anchor % ColumnCount;
        var row = anchor / ColumnCount;
        if (column + width > ColumnCount || row + height > RowCount)
            return false;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (occupants[(row + y) * ColumnCount + column + x] >= 0)
                    return false;
        return true;
    }

    private static void Fill(
        IList<int> occupants,
        int anchor,
        int itemIndex,
        int width,
        int height)
    {
        var column = anchor % ColumnCount;
        var row = anchor / ColumnCount;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                occupants[(row + y) * ColumnCount + column + x] = itemIndex;
    }
}
