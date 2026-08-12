namespace Devilution.Server.Simulation;

/** Allocates non-zero stable entity IDs for a server lifetime. */
public sealed class StableEntityIdAllocator
{
    private uint nextId;

    public StableEntityIdAllocator(uint firstId = 1)
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
