using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Pure client projection of the server-owned inventory footprint grid. */
public sealed class InventoryLayout
{
    private InventoryLayout(int[] occupants, int[] anchors)
    {
        Occupants = occupants;
        Anchors = anchors;
    }

    public IReadOnlyList<int> Occupants { get; }

    public IReadOnlyList<int> Anchors { get; }

    public static InventoryLayout Build(
        IReadOnlyList<ItemSnapshot>? items,
        IReadOnlyList<int>? authoritativeGrid,
        int columns = 10,
        int rows = 4)
    {
        if (columns <= 0 || rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns));

        var occupants = Enumerable.Repeat(-1, checked(columns * rows)).ToArray();
        var anchors = Enumerable.Repeat(-1, items?.Count ?? 0).ToArray();
        if (items is null)
            return new InventoryLayout(occupants, anchors);

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++) {
            var state = items[itemIndex].State;
            var width = Math.Clamp((int)Math.Min(state?.InventoryWidth ?? 1U, (uint)columns), 1, columns);
            var height = Math.Clamp((int)Math.Min(state?.InventoryHeight ?? 1U, (uint)rows), 1, rows);
            var anchor = -1;
            if (authoritativeGrid is not null)
                for (var cell = 0; cell < authoritativeGrid.Count; cell++)
                    if (authoritativeGrid[cell] == itemIndex) {
                        anchor = cell;
                        break;
                    }
            if (anchor < 0 || anchor >= occupants.Length || !CanFit(occupants, anchor, width, height, columns, rows))
                anchor = FindFirstFit(occupants, width, height, columns, rows);
            if (anchor < 0)
                continue;

            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    occupants[(anchor / columns + y) * columns + anchor % columns + x] = itemIndex;
            anchors[itemIndex] = anchor;
        }

        return new InventoryLayout(occupants, anchors);
    }

    private static int FindFirstFit(int[] occupants, int width, int height, int columns, int rows)
    {
        for (var cell = 0; cell < occupants.Length; cell++)
            if (CanFit(occupants, cell, width, height, columns, rows))
                return cell;
        return -1;
    }

    private static bool CanFit(int[] occupants, int anchor, int width, int height, int columns, int rows)
    {
        var column = anchor % columns;
        var row = anchor / columns;
        if (column + width > columns || row + height > rows)
            return false;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (occupants[(row + y) * columns + column + x] >= 0)
                    return false;
        return true;
    }
}
