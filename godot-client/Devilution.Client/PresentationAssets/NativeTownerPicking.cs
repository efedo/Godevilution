using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.PresentationAssets;

/** cursor.cpp resident selection: pixel search first, then native neighbouring occupied tiles. */
public static class NativeTownerPicking
{
    public static NativeTownResidentSnapshot? Select(NativeSceneViewport view, IReadOnlyList<NativeTownResidentSnapshot> residents,
        NativeTownerCatalog? catalog, IReadOnlyDictionary<int, NativeTownerOpacityMask> masks, double tick, double x, double y)
    {
        if (!view.ContainsViewportPoint(x, y)) return null;
        var tile = ResolveTile(view, x, y, out var flip);
        if (catalog is not null) {
            var pixel = TrySelectPixelBased(view, residents, catalog, masks, tile, tick, x, y);
            if (pixel is not null) return pixel;
        }
        return TrySelectTowner(view, residents, tile, flip);
    }

    /** cursor.cpp ConvertToTileGrid / ShiftToDiamondGridAlignment, including integer half-pixel edge ties. */
    public static (int X, int Y) ResolveTile(NativeSceneViewport view, double x, double y, out bool flip)
    {
        var sx = Math.Floor(x - view.OriginX);
        var sy = Math.Floor(y - view.OriginY + 16);
        var column = (int)Math.Floor(sx / 64);
        var row = (int)Math.Floor(sy / 32);
        var px = (int)(sx - column * 64);
        var py = (int)(sy - row * 32);
        var flipY = py < px / 2;
        var flipX = py >= 32 - px / 2;
        flip = flipY && flipX || (flipY || flipX) && px < 32;
        return (Math.Clamp(column + row + (flipX ? 1 : 0), 0, view.LevelWidth - 1),
            Math.Clamp(row - column - (flipY ? 1 : 0), 0, view.LevelHeight - 1));
    }

    /** cursor.cpp TrySelectPixelBased: reverse PointsInRectangle({-1,-1},{3,8}); signed division truncates toward zero. */
    private static NativeTownResidentSnapshot? TrySelectPixelBased(NativeSceneViewport view,
        IReadOnlyList<NativeTownResidentSnapshot> residents, NativeTownerCatalog catalog,
        IReadOnlyDictionary<int, NativeTownerOpacityMask> masks, (int X, int Y) tile, double tick, double x, double y)
    {
        for (var row = 6; row >= -1; row--)
            for (var column = 1; column >= -1; column--) {
                var adjacentX = tile.X + column + row / 2;
                var adjacentY = tile.Y - column + row / 2 + ((row & 1) != 0 ? 1 : 0);
                var resident = FindTowner(view, residents, adjacentX, adjacentY);
                if (resident is null || !catalog.TryGetSprite(catalog.Residents[resident.ResidentSlot].SpriteId, out var sprite)) continue;
                var frame = sprite.ResolveFrame(tick, resident.NativeDirection);
                var anchor = view.CellBottomAnchor(adjacentX, adjacentY);
                if (masks.TryGetValue(frame.AtlasId, out var mask)
                    && mask.HitTest(frame, sprite.AlignmentWidth, (float)anchor.X, (float)anchor.Y, x, y)) return resident;
            }
        return null;
    }

    /** cursor.cpp TrySelectTowner: later occupied cells replace earlier selections. */
    private static NativeTownResidentSnapshot? TrySelectTowner(NativeSceneViewport view,
        IReadOnlyList<NativeTownResidentSnapshot> residents, (int X, int Y) tile, bool flip)
    {
        var selected = flip ? FindTowner(view, residents, tile.X, tile.Y + 1)
            : FindTowner(view, residents, tile.X + 1, tile.Y);
        selected = FindTowner(view, residents, tile.X, tile.Y) ?? selected;
        return FindTowner(view, residents, tile.X + 1, tile.Y + 1) ?? selected;
    }

    /** Native InitTownerInfo / InitCows put positive anchors and three negative NW footprint cells in dMonster. */
    private static NativeTownResidentSnapshot? FindTowner(NativeSceneViewport view,
        IReadOnlyList<NativeTownResidentSnapshot> residents, int x, int y)
    {
        if ((uint)x >= (uint)view.LevelWidth || (uint)y >= (uint)view.LevelHeight) return null;
        for (var i = 0; i < residents.Count; i++) {
            var resident = residents[i];
            if (resident.LevelId == 0
                && (x == resident.PositionX || resident.TownerId == 9 && x == resident.PositionX - 1)
                && (y == resident.PositionY || resident.TownerId == 9 && y == resident.PositionY - 1)) return resident;
        }
        return null;
    }
}
