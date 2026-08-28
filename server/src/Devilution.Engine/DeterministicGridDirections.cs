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

    /** Rotates a native direction by a signed offset around the direction ring. */
    public static int Rotate(int direction, int offset)
    {
        ValidateDirection(direction);
        var rotated = (int)(((long)direction + offset) % Displacements.Count);
        return rotated < 0 ? rotated + Displacements.Count : rotated;
    }

    /** Enumerates the native alternating clockwise/counter-clockwise sweep. */
    public static IReadOnlyList<int> EnumerateAlternating(int direction)
    {
        ValidateDirection(direction);
        var directions = new int[Displacements.Count];
        for (var offset = 0; offset < directions.Length; offset++) {
            directions[offset] = offset == 0
                ? direction
                : offset % 2 == 1
                    ? Rotate(direction, (offset + 1) / 2)
                    : Rotate(direction, -(offset / 2));
        }
        return Array.AsReadOnly(directions);
    }

    /** Enumerates the native five-direction RandomWalk turn candidate order. */
    public static IReadOnlyList<int> EnumerateTurnCandidates(
        int direction,
        int firstTurn,
        int secondTurn)
    {
        ValidateDirection(direction);
        return Array.AsReadOnly(new[] {
            direction,
            Rotate(direction, firstTurn),
            Rotate(direction, -firstTurn),
            Rotate(direction, secondTurn),
            Rotate(direction, -secondTurn),
        });
    }

    /** Enumerates native RoundWalk's turn-90, turn-45, straight order. */
    public static IReadOnlyList<int> EnumerateRoundWalkPrimary(
        int direction,
        int turn)
    {
        ValidateDirection(direction);
        if (turn == 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        var turn45 = Rotate(direction, turn);
        var turn90 = Rotate(turn45, turn);
        return Array.AsReadOnly(new[] { turn90, turn45, direction });
    }

    /** Enumerates native straight, left/right lateral candidate order. */
    public static IReadOnlyList<int> EnumerateStraightAndLateral(
        int direction,
        int turn)
    {
        ValidateDirection(direction);
        if (turn == 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        return Array.AsReadOnly(new[] {
            direction,
            Rotate(direction, turn),
            Rotate(direction, -turn),
        });
    }

    /** Enumerates every direction clockwise from the supplied direction. */
    public static IReadOnlyList<int> EnumerateClockwise(int direction)
    {
        ValidateDirection(direction);
        return Array.AsReadOnly(Enumerable.Range(0, Displacements.Count)
            .Select(offset => Rotate(direction, offset))
            .ToArray());
    }

    /** Returns the native adjacent-cell displacement for a direction. */
    public static (int X, int Y) GetDisplacement(DeterministicGridDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return Displacements[(int)direction];
    }

    private static void ValidateDirection(int direction)
    {
        if (direction is < 0 or >= 8)
            throw new ArgumentOutOfRangeException(nameof(direction));
    }
}
