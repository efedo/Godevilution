using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloFixedPointQ6Tests
{
    [Fact]
    public void FacadeConstantsMatchEnginePrimitive()
    {
        Assert.Equal(DeterministicFixedPointQ6.FractionBits, DiabloFixedPointQ6.FractionBits);
        Assert.Equal(DeterministicFixedPointQ6.Scale, DiabloFixedPointQ6.Scale);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(100)]
    public void FacadeWholeConversionMatchesEnginePrimitive(int value)
    {
        Assert.Equal(
            DeterministicFixedPointQ6.FromWhole(value),
            DiabloFixedPointQ6.FromWhole(value));
    }

    [Theory]
    [InlineData(640)]
    [InlineData(671)]
    [InlineData(-32)]
    [InlineData(-1)]
    public void FacadeProjectionMatchesEnginePrimitive(int raw)
    {
        Assert.Equal(
            DeterministicFixedPointQ6.ToWholeTruncated(raw),
            DiabloFixedPointQ6.ToWholeTruncated(raw));
        Assert.Equal(
            DeterministicFixedPointQ6.ToWholeCeiling(raw),
            DiabloFixedPointQ6.ToWholeCeiling(raw));
    }
}
