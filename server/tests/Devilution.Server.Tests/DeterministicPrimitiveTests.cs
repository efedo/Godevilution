using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class DeterministicPrimitiveTests
{
    [Fact]
    public void DiabloFixedPointQ6PreservesLegacyRawValuesAndProjections()
    {
        var raw = DiabloFixedPointQ6.FromWhole(10) + 32;

        Assert.Equal(672, raw);
        Assert.Equal(10, DiabloFixedPointQ6.ToWholeTruncated(raw));
        Assert.Equal(11, DiabloFixedPointQ6.ToWholeCeiling(raw));
    }

    [Fact]
    public void DiabloRandomUsesTheLegacyLcgAndHighBitPath()
    {
        var random = new DiabloRandom(0x12345678);

        Assert.Equal(0x955E76D9U, random.NextUInt32());
        Assert.Equal(0x560EB8EEU, random.NextUInt32());

        random = new DiabloRandom(0x12345678);
        Assert.Equal(0, random.GenerateRnd(1));
        Assert.Equal(0x955E76D9U, random.State);

        // Native chooses the high-bit path from the limit, not the rolled value.
        random = new DiabloRandom(0);
        Assert.Equal(1, random.GenerateRnd(40000));
    }

    [Fact]
    public void EntityIdsAreNonZeroAndDoNotWrapToZero()
    {
        var allocator = new StableEntityIdAllocator(uint.MaxValue);

        Assert.Equal(uint.MaxValue, allocator.Allocate());
        Assert.Throws<InvalidOperationException>(() => allocator.Allocate());
    }
}
