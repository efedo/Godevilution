using Devilution.Client.Protocol;

namespace Devilution.Client.PresentationAssets;

/** Pixel-space bounds for one native raster, expressed as a half-open rectangle. */
public readonly record struct NativeScenePixelBounds(double X, double Y, int Width, int Height)
{
    public bool IntersectsViewport(int viewportWidth, int viewportHeight)
        => viewportWidth > 0 && viewportHeight > 0
            && Width > 0 && Height > 0
            && X < viewportWidth && X + Width > 0
            && Y < viewportHeight && Y + Height > 0;
}

/** Native dSpecial lookup and screen placement used by the scene renderer. */
public static class NativeScenePlacement
{
    /** Native resident widths align to the tile center; raster bottoms are inclusive. */
    public static NativeScenePixelBounds ResolveResidentBounds(double bottomX, double bottomY,
        int alignmentWidth, int rasterWidth, int rasterHeight)
        => new(bottomX - (alignmentWidth - 64) / 2d, bottomY - rasterHeight + 1, rasterWidth, rasterHeight);

    /**
     * Resolves the special shown while drawing a cell. Town looks one cell back on both axes;
     * Cathedral reads the current cell. This mirrors DrawTileContent in world_renderer.cpp.
     */
    public static bool TryResolveSpecialAtDrawCell(
        NativeSceneFamily family,
        int width,
        int height,
        IReadOnlyList<uint> specialLayer,
        int drawCellX,
        int drawCellY,
        out NativeSceneSpecial special,
        out int ownerX,
        out int ownerY)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(specialLayer);
        special = null!;
        ownerX = 0;
        ownerY = 0;
        if (width <= 0 || height <= 0 || (long)width * height > 131072
            || specialLayer.Count != (long)width * height
            || (uint)drawCellX >= (uint)width || (uint)drawCellY >= (uint)height
            || family.Specials.Count == 0)
            return false;

        var lookup = family.Specials[0];
        ownerX = drawCellX + lookup.SourceTileOffsetX;
        ownerY = drawCellY + lookup.SourceTileOffsetY;
        if ((uint)ownerX >= (uint)width || (uint)ownerY >= (uint)height)
            return false;
        var protocolSpecialId = specialLayer[ownerY * width + ownerX];
        return family.TryGetSpecial(protocolSpecialId, out special);
    }

    /**
     * Native specials use the draw cell's left edge. Town applies its -32 vertical anchor after
     * the delayed lookup; Cathedral uses zero. Raster bottoms are inclusive in the native draw.
     */
    public static NativeScenePixelBounds ResolveSpecialBounds(
        NativeSceneViewport viewport,
        int drawCellX,
        int drawCellY,
        NativeSceneSpecial special)
    {
        ArgumentNullException.ThrowIfNull(special);
        var bottomAnchor = viewport.CellBottomAnchor(drawCellX, drawCellY);
        return new NativeScenePixelBounds(
            bottomAnchor.X - NativeSceneViewport.HalfTileWidth + special.AnchorX,
            bottomAnchor.Y + special.AnchorY - special.Raster.Height + 1,
            special.Raster.Width,
            special.Raster.Height);
    }
}
