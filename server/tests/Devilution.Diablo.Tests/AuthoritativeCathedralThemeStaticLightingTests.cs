using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralThemeStaticLightingTests
{
    private const int Width = 112;
    private static byte[] Dark() => Enumerable.Repeat((byte)15, Width * Width).ToArray();
    private static int At(int x, int y) => x + y * Width;

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 5)]
    [InlineData(3, 8)]
    [InlineData(4, 10)]
    [InlineData(5, 13)]
    [InlineData(6, 15)]
    public void RadiusFiveUsesNativeFloatFalloffInEveryQuadrant(int distance, byte expected)
    {
        var light = Dark();
        var preLight = Dark();
        AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, new(50, 50), 5, false);

        Assert.Equal(0, light[At(50, 50)]);
        Assert.Equal(expected, light[At(50 + distance, 50)]);
        Assert.Equal(expected, light[At(50 - distance, 50)]);
        Assert.Equal(expected, light[At(50, 50 + distance)]);
        Assert.Equal(expected, light[At(50, 50 - distance)]);
        Assert.All(preLight, value => Assert.Equal(15, value));
    }

    [Fact]
    public void DiagonalUsesTruncatedNativeDistanceAndDoesNotDarkenLiveTiles()
    {
        var light = Dark();
        var preLight = Dark();
        light[At(52, 52)] = 1;
        AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, new(50, 50), 5, false);

        Assert.Equal(3, light[At(51, 51)]); // floor(sqrt(8^2 + 8^2)) = 11.
        Assert.Equal(1, light[At(52, 52)]);
        Assert.Equal(15, light[At(65, 50)]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(111, 0)]
    [InlineData(0, 111)]
    [InlineData(111, 111)]
    public void CornerLightingIsBoundedAndLeavesPreLightUntouched(int x, int y)
    {
        var light = Dark();
        var preLight = Dark();
        AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, new(x, y), 5, false);

        Assert.Equal(0, light[At(x, y)]);
        Assert.All(preLight, value => Assert.Equal(15, value));
        Assert.Equal(15, light[At(50, 50)]);
    }

    [Fact]
    public void LoadingMutatesPreLightAndRestoresInclusiveRadiusPlusTwo()
    {
        var light = Enumerable.Repeat((byte)2, Width * Width).ToArray();
        var preLight = Dark();
        AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, new(50, 50), 5, true);

        Assert.Equal(0, preLight[At(50, 50)]);
        Assert.Equal(3, preLight[At(51, 51)]);
        Assert.Equal(0, light[At(50, 50)]);
        Assert.Equal(15, light[At(57, 57)]);
        Assert.Equal(2, light[At(58, 57)]);
    }

    [Theory]
    [InlineData(-1, 50, 5)]
    [InlineData(112, 50, 5)]
    [InlineData(50, -1, 5)]
    [InlineData(50, 112, 5)]
    [InlineData(50, 50, -1)]
    [InlineData(50, 50, 16)]
    public void InvalidPositionOrRadiusFailsBeforeMutation(int x, int y, int radius)
    {
        var light = Dark();
        var preLight = Dark();
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, new(x, y), radius, false));
        Assert.All(light, value => Assert.Equal(15, value));
        Assert.All(preLight, value => Assert.Equal(15, value));
    }

    [Fact]
    public void WrongChannelSizeFailsBeforeMutation()
    {
        var light = Dark();
        var preLight = new byte[1];
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, new(50, 50), 5, false));
        Assert.All(light, value => Assert.Equal(15, value));
    }
}
