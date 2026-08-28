using Devilution.Engine;

namespace Devilution.Server.Simulation;

/** Backward-compatible host facade over the engine ID allocator. */
public sealed class StableEntityIdAllocator : DeterministicEntityIdAllocator
{
    public StableEntityIdAllocator(uint firstId = 1)
        : base(firstId)
    {
    }

    public new uint Allocate() => base.Allocate();
}
