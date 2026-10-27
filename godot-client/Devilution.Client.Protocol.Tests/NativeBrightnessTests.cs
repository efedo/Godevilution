using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeBrightnessTests
{
    [Fact]
    public void NeutralBrightnessKeepsEveryPaletteChannel()
    {
        for (var channel = 0; channel <= 255; channel++)
            Assert.Equal((byte)channel, NativeBrightness.MapTone(0, (byte)channel));
    }

    [Theory]
    [InlineData(100, 64, 160)]
    [InlineData(100, 128, 255)]
    [InlineData(50, 64, 112)]
    [InlineData(50, 200, 243)]
    [InlineData(100, 0, 0)]
    [InlineData(100, 255, 255)]
    public void ToneCurveMatchesNativeMapTone(int brightness, byte channel, byte expected)
        => Assert.Equal(expected, NativeBrightness.MapTone(brightness, channel));

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(37, 35)]
    [InlineData(100, 100)]
    [InlineData(140, 100)]
    public void StoredValuesSnapToTheNativeRangeAndStep(int stored, int expected)
        => Assert.Equal(expected, NativeBrightness.Normalize(stored));

    [Theory]
    [InlineData(0, 1, 5)]
    [InlineData(95, 1, 100)]
    [InlineData(100, 1, 100)]
    [InlineData(5, -1, 0)]
    [InlineData(0, -1, 0)]
    public void AdjustMovesOneStepWithinTheRange(int brightness, int direction, int expected)
        => Assert.Equal(expected, NativeBrightness.Adjust(brightness, direction));
}
