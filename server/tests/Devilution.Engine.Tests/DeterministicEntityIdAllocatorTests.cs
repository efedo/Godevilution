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

    [Fact]
    public void SkipsCallerOwnedOccupiedIdentities()
    {
        var allocator = new DeterministicEntityIdAllocator(20);

        Assert.Equal(
            22U,
            allocator.Allocate(candidate => candidate is 20 or 21));
        Assert.Equal(23U, allocator.Allocate());
    }

    [Fact]
    public void CollisionAtTheEndOfTheIdentitySpaceFailsWithoutReturningZero()
    {
        var allocator = new DeterministicEntityIdAllocator(uint.MaxValue);

        Assert.Throws<InvalidOperationException>(
            () => allocator.Allocate(static _ => true));
    }

    [Fact]
    public void OccupancyCallbackIsRequired()
    {
        var allocator = new DeterministicEntityIdAllocator();

        Assert.Throws<ArgumentNullException>(
            () => allocator.Allocate(null!));
    }
}
