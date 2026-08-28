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
}
