namespace Devilution.Engine;

/** Deterministic Bresenham traversal for caller-owned grid visibility rules. */
public static class DeterministicGridLineTraversal
{
    /**
     * Returns whether every interior cell between two endpoints is clear.
     * Endpoints are never passed to the blocking predicate, matching native
     * projectile and movement admission rules. Long arithmetic keeps malformed
     * coordinates from overflowing during traversal, while the step limit
     * prevents an unbounded caller-controlled walk.
     */
    public static bool HasLineOfSight(
        int startX,
        int startY,
        int endX,
        int endY,
        Func<int, int, bool> isBlocked,
        int maximumSteps = 4096)
    {
        ArgumentNullException.ThrowIfNull(isBlocked);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSteps);

        long x = startX;
        long y = startY;
        var deltaX = Math.Abs((long)endX - startX);
        var deltaY = Math.Abs((long)endY - startY);
        if (Math.Max(deltaX, deltaY) > maximumSteps)
            return false;
        var stepX = startX < endX ? 1L : -1L;
        var stepY = startY < endY ? 1L : -1L;
        var error = deltaX - deltaY;

        while (true) {
            if ((x != startX || y != startY) && (x != endX || y != endY)
                && isBlocked((int)x, (int)y)) {
                return false;
            }
            if (x == endX && y == endY)
                return true;

            var doubledError = 2L * error;
            if (doubledError > -deltaY) {
                error -= deltaY;
                x += stepX;
            }
            if (doubledError < deltaX) {
                error += deltaX;
                y += stepY;
            }
        }
    }

    /**
     * Mirrors native `LineClear`: traverse in the native normalized direction,
     * skip the original start cell, and test each visited cell (including the
     * endpoint when it is reached by the traversal).
     */
    public static bool HasNativeLineClear(
        int startX,
        int startY,
        int endX,
        int endY,
        Func<int, int, bool> isClear,
        int maximumSteps = 4096)
    {
        ArgumentNullException.ThrowIfNull(isClear);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSteps);

        long x = startX;
        long y = startY;
        long targetX = endX;
        long targetY = endY;
        var originalStartX = x;
        var originalStartY = y;
        var deltaX = targetX - x;
        var deltaY = targetY - y;
        if (Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) > maximumSteps)
            return false;

        var steps = 0;
        if (Math.Abs(deltaX) > Math.Abs(deltaY)) {
            if (deltaX < 0) {
                (x, targetX) = (targetX, x);
                (y, targetY) = (targetY, y);
                deltaX = -deltaX;
                deltaY = -deltaY;
            }

            var directionIncrement = deltaY > 0 ? 1L : -1L;
            var decision = deltaY > 0
                ? 2 * deltaY - deltaX
                : 2 * deltaY + deltaX;
            var diagonalIncrement = 2 * deltaY;
            var horizontalIncrement = deltaY > 0
                ? 2 * (deltaY - deltaX)
                : 2 * (deltaX + deltaY);
            while (steps < maximumSteps && (x != targetX || y != targetY)) {
                if ((decision <= 0) ^ (directionIncrement < 0)) {
                    decision += diagonalIncrement;
                } else {
                    decision += horizontalIncrement;
                    y += directionIncrement;
                }
                x++;
                steps++;
                if ((x != originalStartX || y != originalStartY)
                    && !isClear(checked((int)x), checked((int)y))) {
                    return false;
                }
            }
        } else {
            if (deltaY < 0) {
                (x, targetX) = (targetX, x);
                (y, targetY) = (targetY, y);
                deltaX = -deltaX;
                deltaY = -deltaY;
            }

            var directionIncrement = deltaX > 0 ? 1L : -1L;
            var decision = deltaX > 0
                ? 2 * deltaX - deltaY
                : 2 * deltaX + deltaY;
            var diagonalIncrement = 2 * deltaX;
            var verticalIncrement = deltaX > 0
                ? 2 * (deltaX - deltaY)
                : 2 * (deltaY + deltaX);
            while (steps < maximumSteps && (x != targetX || y != targetY)) {
                if ((decision <= 0) ^ (directionIncrement < 0)) {
                    decision += diagonalIncrement;
                } else {
                    decision += verticalIncrement;
                    x += directionIncrement;
                }
                y++;
                steps++;
                if ((x != originalStartX || y != originalStartY)
                    && !isClear(checked((int)x), checked((int)y))) {
                    return false;
                }
            }
        }

        return x == targetX && y == targetY;
    }
}
