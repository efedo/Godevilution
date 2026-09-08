namespace Devilution.Engine;

/** Allocates non-zero stable entity IDs for an authoritative runtime lifetime. */
public class DeterministicEntityIdAllocator
{
    private uint nextId;

    public DeterministicEntityIdAllocator(uint firstId = 1)
    {
        if (firstId == 0)
            throw new ArgumentOutOfRangeException(nameof(firstId));
        nextId = firstId;
    }

    public uint Allocate()
    {
        if (nextId == 0)
            throw new InvalidOperationException("Entity ID space is exhausted.");
        var allocated = nextId;
        nextId = allocated == uint.MaxValue ? 0 : allocated + 1;
        return allocated;
    }

    /** Reserves an externally-created ID so later allocations cannot reuse it. */
    public void Reserve(uint reservedId)
    {
        if (reservedId == 0)
            throw new ArgumentOutOfRangeException(nameof(reservedId));
        if (nextId == 0)
            return;
        if (reservedId < nextId)
            return;
        nextId = reservedId == uint.MaxValue ? 0 : reservedId + 1;
    }

    /** Allocates the next non-zero ID that is not occupied by caller-owned state. */
    public uint Allocate(Func<uint, bool> isOccupied)
    {
        ArgumentNullException.ThrowIfNull(isOccupied);
        while (true) {
            var candidate = Allocate();
            if (!isOccupied(candidate))
                return candidate;
        }
    }

    /** Finds one available ID without retaining allocator state. */
    public static uint FindFirstAvailable(uint firstId, Func<uint, bool> isOccupied)
    {
        return new DeterministicEntityIdAllocator(firstId).Allocate(isOccupied);
    }
}
