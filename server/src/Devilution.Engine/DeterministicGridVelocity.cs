namespace Devilution.Engine;

/** Native-compatible fixed-point velocity construction for grid projectiles. */
public static class DeterministicGridVelocity
{
    /**
     * Calculates the native rotated-grid velocity used by Diablo projectiles.
     * The checked magnitude and unchecked Q16 scaling intentionally preserve
     * the legacy arithmetic contract while keeping the operation reusable by
     * other authoritative modules.
     */
    public static (int X, int Y) CalculateNative(
        int startX,
        int startY,
        int destinationX,
        int destinationY,
        int speed)
    {
        if (startX == destinationX && startY == destinationY)
            return (0, 0);

        var deltaX = startX - destinationX;
        var deltaY = startY - destinationY;
        var rotatedX = deltaY - deltaX;
        var rotatedY = -(deltaY + deltaX);
        var magnitude = MathF.Sqrt((float)checked(
            rotatedX * rotatedX + rotatedY * rotatedY));
        var normalX = (int)(unchecked(rotatedX << 16) / magnitude);
        var normalY = (int)(unchecked(rotatedY << 16) / magnitude);
        normalY /= 2;
        return (unchecked(normalX * speed), unchecked(normalY * speed));
    }
}
