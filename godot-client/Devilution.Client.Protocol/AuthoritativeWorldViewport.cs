namespace Devilution.Client.Protocol;

/**
 * Deterministic screen transform for the authoritative tile world.
 *
 * The Godot presentation layer owns the actual viewport, while this value
 * keeps camera clamping and input hit-testing independent of Godot types.
 */
public readonly record struct AuthoritativeWorldViewport(
    int ViewportWidth,
    int ViewportHeight,
    int LevelWidth,
    int LevelHeight,
    double CameraX,
    double CameraY,
    double TileSize)
{
    public static AuthoritativeWorldViewport Create(
        int viewportWidth,
        int viewportHeight,
        int levelWidth,
        int levelHeight,
        double cameraX,
        double cameraY,
        double tileSize = 32)
    {
        if (viewportWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (viewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        if (levelWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelWidth));
        if (levelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelHeight));
        if (!double.IsFinite(cameraX) || !double.IsFinite(cameraY))
            throw new ArgumentOutOfRangeException(nameof(cameraX));
        if (!double.IsFinite(tileSize) || tileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        return new AuthoritativeWorldViewport(
            viewportWidth,
            viewportHeight,
            levelWidth,
            levelHeight,
            cameraX,
            cameraY,
            tileSize);
    }

    /** Top-left screen coordinate of the clamped level rectangle. */
    public double OriginX => ResolveOrigin(
        ViewportWidth,
        LevelWidth * TileSize,
        CameraX,
        TileSize);

    /** Top-left screen coordinate of the clamped level rectangle. */
    public double OriginY => ResolveOrigin(
        ViewportHeight,
        LevelHeight * TileSize,
        CameraY,
        TileSize);

    public (double X, double Y) WorldToScreen(double worldX, double worldY)
        => (
            OriginX + (worldX + 0.5) * TileSize,
            OriginY + (worldY + 0.5) * TileSize);

    public (int X, int Y) ScreenToTile(double screenX, double screenY)
        => (
            (int)Math.Floor((screenX - OriginX) / TileSize),
            (int)Math.Floor((screenY - OriginY) / TileSize));

    public bool ContainsScreenPoint(double screenX, double screenY)
    {
        var tile = ScreenToTile(screenX, screenY);
        return tile.X >= 0 && tile.Y >= 0
            && tile.X < LevelWidth
            && tile.Y < LevelHeight;
    }

    private static double ResolveOrigin(
        int viewportSize,
        double contentSize,
        double camera,
        double tileSize)
    {
        if (contentSize <= viewportSize)
            return (viewportSize - contentSize) / 2;

        var desired = viewportSize / 2d - (camera + 0.5) * tileSize;
        return Math.Clamp(desired, viewportSize - contentSize, 0d);
    }
}
