namespace Devilution.Client.Protocol;

/** Shared native 64x32 diamond transform for drawing, camera and pointer input. */
public readonly record struct NativeSceneViewport
{
    public const int HalfTileWidth = 32;
    public const int HalfTileHeight = 16;
    public const int BottomInclusiveOffset = 15;

    private NativeSceneViewport(int viewportWidth, int viewportHeight, int levelWidth,
        int levelHeight, double cameraX, double cameraY)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        LevelWidth = levelWidth;
        LevelHeight = levelHeight;
        CameraX = cameraX;
        CameraY = cameraY;
    }

    public int ViewportWidth { get; }
    public int ViewportHeight { get; }
    public int LevelWidth { get; }
    public int LevelHeight { get; }
    public double CameraX { get; }
    public double CameraY { get; }

    public static NativeSceneViewport Create(int viewportWidth, int viewportHeight,
        int levelWidth, int levelHeight, double cameraX, double cameraY)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (levelWidth <= 0 || levelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelWidth));
        if (!double.IsFinite(cameraX) || !double.IsFinite(cameraY))
            throw new ArgumentOutOfRangeException(nameof(cameraX));
        return new NativeSceneViewport(viewportWidth, viewportHeight, levelWidth, levelHeight, cameraX, cameraY);
    }

    public double OriginX => ResolveOrigin(ViewportWidth,
        -(double)LevelHeight * HalfTileWidth, (double)LevelWidth * HalfTileWidth,
        (CameraX - CameraY) * HalfTileWidth);

    public double OriginY => ResolveOrigin(ViewportHeight,
        -HalfTileHeight, ((double)LevelWidth + LevelHeight - 1) * HalfTileHeight,
        (CameraX + CameraY) * HalfTileHeight);

    /** Cell centers, including fractional actor positions during walking. */
    public (double X, double Y) WorldToScreen(double worldX, double worldY)
        => (OriginX + (worldX - worldY) * HalfTileWidth,
            OriginY + (worldX + worldY) * HalfTileHeight);

    /** Bottom-center anchor of the native 64x31 floor and native actor sprites. */
    public (double X, double Y) CellBottomAnchor(double worldX, double worldY)
    {
        var center = WorldToScreen(worldX, worldY);
        return (center.X, center.Y + BottomInclusiveOffset);
    }

    /** Half-open ownership in world coordinates; exact diamond-edge ties go positive. */
    public (int X, int Y) ScreenToTile(double screenX, double screenY)
    {
        if (!double.IsFinite(screenX) || !double.IsFinite(screenY))
            throw new ArgumentOutOfRangeException(nameof(screenX));
        var horizontal = (screenX - OriginX) / HalfTileWidth;
        var vertical = (screenY - OriginY) / HalfTileHeight;
        return (ToCell((horizontal + vertical) / 2), ToCell((vertical - horizontal) / 2));
    }

    public bool ContainsScreenPoint(double screenX, double screenY)
    {
        if (!double.IsFinite(screenX) || !double.IsFinite(screenY))
            return false;
        var cell = ScreenToTile(screenX, screenY);
        return cell.X >= 0 && cell.Y >= 0 && cell.X < LevelWidth && cell.Y < LevelHeight;
    }

    /** Tests the half-open pixel bounds used by the rendered viewport. */
    public bool ContainsViewportPoint(double screenX, double screenY)
        => double.IsFinite(screenX) && double.IsFinite(screenY)
            && screenX >= 0 && screenY >= 0
            && screenX < ViewportWidth && screenY < ViewportHeight;

    private static int ToCell(double position)
        => (int)Math.Clamp(Math.Floor(position + 0.5), int.MinValue, int.MaxValue);

    private static double ResolveOrigin(int viewport, double minimum, double maximum, double camera)
    {
        if (maximum - minimum <= viewport)
            return (viewport - maximum - minimum) / 2;
        return Math.Clamp(viewport / 2d - camera, viewport - maximum, -minimum);
    }
}
