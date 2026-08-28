using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicLegacyRandomTests
{
    [Fact]
    public void PreservesTheNativeLcgAndHighBitGenerationPath()
    {
        var random = new DeterministicLegacyRandom(0x12345678);

        Assert.Equal(0x955E76D9U, random.NextUInt32());
        Assert.Equal(0x560EB8EEU, random.NextUInt32());

        random = new DeterministicLegacyRandom(0x12345678);
        Assert.Equal(0, random.GenerateRnd(1));
        Assert.Equal(0x955E76D9U, random.State);

        random = new DeterministicLegacyRandom(0);
        Assert.Equal(1, random.GenerateRnd(40000));
    }

    [Fact]
    public void SupportsStateRestoreDiscardAndValidatedRanges()
    {
        var random = new DeterministicLegacyRandom(7);
        var first = random.NextUInt32();
        random.Discard(2);
        var afterDiscard = random.State;
        random.Restore(first);
        random.Discard(2);

        Assert.Equal(afterDiscard, random.State);
        Assert.InRange(random.RandomIntBetween(2, 5), 2, 5);
        Assert.InRange(random.RandomIntBetween(2, 5, halfOpen: true), 2, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Discard(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.RandomIntBetween(5, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.RandomIntBetween(2, 2, halfOpen: true));
    }

    [Fact]
    public void ExposesCompatibilityShapedRollsWithoutChangingTheLcgSequence()
    {
        var random = new DeterministicLegacyRandom(0xCAFEBABE);
        var expected = new DeterministicLegacyRandom(0xCAFEBABE);

        Assert.Equal(
            unchecked((uint)expected.AdvanceRndSeed()),
            random.AdvanceSeed());
        Assert.Equal(
            expected.RandomIntLessThan(37),
            random.Next(37));
        Assert.Equal(expected.State, random.State);
        Assert.Equal(0, random.Next(0));
        Assert.Equal(0, random.Next(-1));
    }
}
