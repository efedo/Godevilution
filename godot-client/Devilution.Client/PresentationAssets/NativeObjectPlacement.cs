namespace Devilution.Client.PresentationAssets;

/** Native object centering uses nominal CEL width; raster spill remains in its culling footprint. */
public static class NativeObjectPlacement
{
    public static NativeScenePixelBounds ResolveBounds(double drawTileX, double drawTileY,
        NativeObjectFrame frame, int drawTileMinusOwnerX = 0, int drawTileMinusOwnerY = 0)
    {
        ArgumentNullException.ThrowIfNull(frame);
        // Object::getRenderingOffset subtracts native worldToScreen(owner - drawTile).
        return new NativeScenePixelBounds(
            drawTileX + 32d * (drawTileMinusOwnerY - drawTileMinusOwnerX) - (frame.NativeWidth - 64) / 2d,
            drawTileY - 16d * (drawTileMinusOwnerX + drawTileMinusOwnerY) - frame.Height + 1,
            frame.Width, frame.Height);
    }
}
