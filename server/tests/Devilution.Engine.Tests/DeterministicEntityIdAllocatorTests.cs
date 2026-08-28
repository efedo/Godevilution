using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicEntityIdAllocatorTests
{
    [Fact]
    public void AllocatesTheRequestedRangeWithoutReturningZero()
    {
        var allocator = new DeterministicEntityIdAllocator(9);

        Assert.Equal(9U, allocator.Allocate());
        Assert.Equal(10U, allocator.Allocate());
    }

    [Fact]
    public void ExhaustionDoesNotWrapToZero()
    {
        var allocator = new DeterministicEntityIdAllocator(uint.MaxValue);

        Assert.Equal(uint.MaxValue, allocator.Allocate());
        Assert.Throws<InvalidOperationException>(() => allocator.Allocate());
    }

    [Fact]
    public void RejectsZeroAsTheInitialIdentity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DeterministicEntityIdAllocator(0));
    }
}
