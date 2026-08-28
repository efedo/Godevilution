using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicFixedPointQ6Tests
{
    [Fact]
    public void PreservesLegacyRawValuesAndProjections()
    {
        var raw = DeterministicFixedPointQ6.FromWhole(10) + 32;

        Assert.Equal(672, raw);
        Assert.Equal(10, DeterministicFixedPointQ6.ToWholeTruncated(raw));
        Assert.Equal(11, DeterministicFixedPointQ6.ToWholeCeiling(raw));
    }

    [Fact]
    public void TruncationAndCeilingMatchNativeSignedDivision()
    {
        Assert.Equal(0, DeterministicFixedPointQ6.ToWholeTruncated(-32));
        Assert.Equal(0, DeterministicFixedPointQ6.ToWholeCeiling(-32));
        Assert.Equal(0, DeterministicFixedPointQ6.ToWholeTruncated(31));
        Assert.Equal(1, DeterministicFixedPointQ6.ToWholeCeiling(31));
    }

    [Fact]
    public void WholeConversionChecksOverflow()
    {
        Assert.Throws<OverflowException>(() =>
            DeterministicFixedPointQ6.FromWhole(int.MaxValue));
    }
}
