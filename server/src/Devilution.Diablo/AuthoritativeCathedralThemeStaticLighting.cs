namespace Devilution.Diablo;

/** Native zero-offset static object lighting; not indexed light allocation or vision. */
internal static class AuthoritativeCathedralThemeStaticLighting
{
    private const int Width = AuthoritativeCathedralThemeParameters.Width;
    private const int CellCount = Width * Width;

    internal static void Apply(
        Span<byte> light,
        Span<byte> preLight,
        AuthoritativeCathedralPopulationPoint position,
        int radius,
        bool loadingMapObjects)
    {
        if (light.Length != CellCount || preLight.Length != CellCount
            || !AuthoritativeCathedralThemeParameters.Contains(position)
            || radius is < 0 or >= 16)
            throw new InvalidDataException("Static Cathedral light inputs exceed native bounds.");

        // AddObjectLight always calls DoLighting with a zero pixel offset.
        // RotateRadius consequently retains offset/block zero in all quadrants.
        var target = loadingMapObjects ? preLight : light;
        target[position.X + position.Y * Width] = 0;
        var minX = position.X < 15 ? position.X + 1 : 15;
        var maxX = position.X + 15 > Width ? Width - position.X : 15;
        var minY = position.Y < 15 ? position.Y + 1 : 15;
        var maxY = position.Y + 15 > Width ? Width - position.Y : 15;

        // Preserve native quadrant-specific bounds, including edge asymmetry.
        // A generic radial brush is not equivalent near the dungeon border.
        for (var quadrant = 0; quadrant < 4; quadrant++) {
            var yBound = quadrant is 1 or 2 ? maxY : minY;
            var xBound = quadrant < 2 ? maxX : minX;
            for (var y = 0; y < yBound; y++)
                for (var x = 1; x < xBound; x++) {
                    var distance = (int)Math.Sqrt(64 * (x * x + y * y));
                    if (distance >= 128)
                        continue;
                    var (dx, dy) = quadrant switch {
                        0 => (x, y),
                        1 => (y, -x),
                        2 => (-x, -y),
                        _ => (-y, x),
                    };
                    var tileX = position.X + dx;
                    var tileY = position.Y + dy;
                    if (tileX is < 0 or >= Width || tileY is < 0 or >= Width)
                        continue;
                    var index = tileX + tileY * Width;
                    // MakeLightTable uses float division/multiplication and
                    // rounds by adding 0.5 before uint8 truncation.
                    var maximumDistance = (radius + 1) * 8;
                    var shade = distance > maximumDistance
                        ? (byte)15
                        : (byte)((float)distance / maximumDistance * 15F + 0.5F);
                    if (shade < target[index])
                        target[index] = shade;
                }
        }

        if (!loadingMapObjects)
            return;
        // DoUnLight reads the newly updated pre-light channel. It deliberately
        // overwrites even a brighter live value within inclusive radius + 2.
        var restoreRadius = radius + 2;
        for (var y = Math.Max(0, position.Y - restoreRadius); y <= Math.Min(Width - 1, position.Y + restoreRadius); y++)
            for (var x = Math.Max(0, position.X - restoreRadius); x <= Math.Min(Width - 1, position.X + restoreRadius); x++) {
                var index = x + y * Width;
                light[index] = preLight[index];
            }
    }
}
