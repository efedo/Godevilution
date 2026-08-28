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
        var allocated = nextId;
        if (allocated == 0)
            throw new InvalidOperationException("Entity ID space is exhausted.");
        nextId = allocated == uint.MaxValue ? 0 : allocated + 1;
        return allocated;
    }
}
