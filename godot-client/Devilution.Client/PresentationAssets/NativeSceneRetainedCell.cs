using Devilution.Client.Protocol;

namespace Devilution.Client.PresentationAssets;

public readonly record struct NativeSceneRasterDraw(NativeSceneRasterReference Raster, NativeScenePixelBounds Bounds);

/** Retains the original slot order and raster placement until this cell's geometry changes. */
public sealed class NativeSceneRetainedCell(NativeSceneViewport referenceView, int x, int y)
{
    private bool built;
    private uint piece;
    private NativeSceneSpecial? special;
    private readonly List<NativeSceneRasterDraw> floors = [], content = [], specials = [];
    public IReadOnlyList<NativeSceneRasterDraw> Floors => floors;
    public IReadOnlyList<NativeSceneRasterDraw> Content => content;
    public IReadOnlyList<NativeSceneRasterDraw> Specials => specials;

    /** Camera motion uses canvas transforms; it never changes these reference-space commands. */
    public bool Update(NativeSceneFamily family, uint nextPiece, NativeSceneSpecial? nextSpecial)
    {
        if (built && piece == nextPiece && special == nextSpecial) return false;
        built = true; piece = nextPiece; special = nextSpecial;
        floors.Clear(); content.Clear(); specials.Clear();
        var anchor = referenceView.CellBottomAnchor(x, y);
        foreach (var slot in family.GetSlots(piece)) {
            var left = anchor.X - NativeSceneViewport.HalfTileWidth + (slot.SlotIndex & 1) * 32;
            var top = anchor.Y - 31 - slot.SlotIndex / 2 * 32;
            if (slot.IsFloorBase && slot.FloorRaster is { } floor)
                floors.Add(new(floor, new(left, top, floor.Width, floor.Height)));
            if (slot.Raster is not { } raster || slot.Role == NativeSceneSlotRole.None) continue;
            if (slot.Role == NativeSceneSlotRole.Foliage) top -= 16;
            content.Add(new(raster, new(left, top, raster.Width, raster.Height)));
        }
        if (special is not null)
            specials.Add(new(special.Raster, NativeScenePlacement.ResolveSpecialBounds(referenceView, x, y, special)));
        return true;
    }
}
