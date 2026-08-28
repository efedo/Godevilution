namespace Devilution.Engine;

/** Native-compatible eight-way grid directions and coordinate conversion. */
public enum DeterministicGridDirection
{
    South = 0,
    SouthWest = 1,
    West = 2,
    NorthWest = 3,
    North = 4,
    NorthEast = 5,
    East = 6,
    SouthEast = 7,
}

/** Deterministic direction math shared by authoritative grid systems. */
public static class DeterministicGridDirections
{
    private static readonly IReadOnlyList<(int X, int Y)> Displacements = [
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
        (0, -1),
        (1, -1),
        (1, 0),
    ];

    /** Native direction-indexed displacements shared by movement and combat. */
    public static IReadOnlyList<(int X, int Y)> AdjacentDisplacements
        => Displacements;

    /** Converts one native adjacent-cell displacement to its facing direction. */
    public static DeterministicGridDirection FromAdjacentStep(int directionX, int directionY)
    {
        for (var index = 0; index < Displacements.Count; index++) {
            if (Displacements[index] == (directionX, directionY))
                return (DeterministicGridDirection)index;
        }
        throw new ArgumentOutOfRangeException(nameof(directionX));
    }

    /** Selects the native eight-way facing sector from one coordinate to another. */
    public static DeterministicGridDirection Toward(
        int startX,
        int startY,
        int destinationX,
        int destinationY)
    {
        var deltaX = (long)destinationX - startX;
        var deltaY = (long)destinationY - startY;
        int direction;
        if (deltaX >= 0) {
            if (deltaY >= 0) {
                if (5 * deltaX <= deltaY * 2)
                    return DeterministicGridDirection.SouthWest;
                direction = (int)DeterministicGridDirection.South;
            } else {
                deltaY = -deltaY;
                if (5 * deltaX <= deltaY * 2)
                    return DeterministicGridDirection.NorthEast;
                direction = (int)DeterministicGridDirection.East;
            }
            if (5 * deltaY <= deltaX * 2)
                direction = (int)DeterministicGridDirection.SouthEast;
        } else {
            deltaX = -deltaX;
            if (deltaY >= 0) {
                if (5 * deltaX <= deltaY * 2)
                    return DeterministicGridDirection.SouthWest;
                direction = (int)DeterministicGridDirection.West;
            } else {
                deltaY = -deltaY;
                if (5 * deltaX <= deltaY * 2)
                    return DeterministicGridDirection.NorthEast;
                direction = (int)DeterministicGridDirection.North;
            }
            if (5 * deltaY <= deltaX * 2)
                direction = (int)DeterministicGridDirection.NorthWest;
        }
        return (DeterministicGridDirection)direction;
    }

    /** Rotates a native direction counter-clockwise by the requested number of steps. */
    public static DeterministicGridDirection RotateLeft(
        DeterministicGridDirection direction,
        int steps = 1)
        => (DeterministicGridDirection)(((int)direction + 8 - (steps % 8)) % 8);

    /** Returns the native adjacent-cell displacement for a direction. */
    public static (int X, int Y) GetDisplacement(DeterministicGridDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return Displacements[(int)direction];
    }
}
