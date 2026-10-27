namespace Devilution.Client.Protocol;

/**
 * Maps held screen directions to one native grid step. The isometric view draws
 * grid +X toward the lower right and +Y toward the lower left, so screen up is
 * (-1, -1) and two held keys combine into the eight native walk directions.
 */
public static class ScreenDirectionMovement
{
    public static (int X, int Y) Resolve(bool up, bool down, bool left, bool right)
    {
        var x = 0;
        var y = 0;
        if (up && !down) { x -= 1; y -= 1; }
        if (down && !up) { x += 1; y += 1; }
        if (right && !left) { x += 1; y -= 1; }
        if (left && !right) { x -= 1; y += 1; }
        return (Math.Sign(x), Math.Sign(y));
    }
}
