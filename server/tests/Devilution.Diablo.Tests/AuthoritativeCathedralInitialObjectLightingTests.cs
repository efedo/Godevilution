using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralInitialObjectLightingTests
{
    private const int Cells = 112 * 112;

    [Theory]
    [InlineData(0, 5)]
    [InlineData(87, 3)]
    public void ActiveInitialLightUsesItsNativeRadiusRegardlessOfRawFlag(int type, int radius)
    {
        var population = Population(type);
        var light = Enumerable.Repeat((byte)15, Cells).ToArray();
        var preLight = Enumerable.Repeat((byte)7, Cells).ToArray();
        var expected = light.ToArray();
        AuthoritativeCathedralThemeStaticLighting.Apply(expected, preLight.ToArray(), new(50, 50), radius, false);

        AuthoritativeCathedralInitialObjectLighting.Apply(light, preLight, population);

        Assert.Equal(expected, light);
        Assert.All(preLight, value => Assert.Equal(7, value));
        Assert.False(population.Objects[126].ApplyLighting);
        Assert.Equal(2588U, population.FinalRandomState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(87)]
    [InlineData(57)]
    public void InactiveLightOrActiveNonLightDoesNotContribute(int type)
    {
        var population = Population(type, active: type == 57);
        var light = Enumerable.Repeat((byte)15, Cells).ToArray();
        var preLight = light.ToArray();
        AuthoritativeCathedralInitialObjectLighting.Apply(light, preLight, population);
        Assert.All(light, value => Assert.Equal(15, value));
        Assert.All(preLight, value => Assert.Equal(15, value));
    }

    [Theory]
    [InlineData(44)]
    [InlineData(45)]
    [InlineData(46)]
    [InlineData(47)]
    public void CatacombsTorchesUseEightRadiusOnLiveGridOnly(int type)
    {
        var population = Population(type);
        var light = Enumerable.Repeat((byte)15, Cells).ToArray();
        var preLight = Enumerable.Repeat((byte)7, Cells).ToArray();
        var expected = light.ToArray();
        AuthoritativeCathedralThemeStaticLighting.Apply(expected, preLight.ToArray(), new(50, 50), 8, false);

        AuthoritativeCathedralInitialObjectLighting.ApplyCatacombs(light, preLight, population);

        Assert.Equal(expected, light);
        Assert.NotEqual((byte)15, light[50 + 50 * 112]);
        Assert.All(preLight, value => Assert.Equal((byte)7, value));
        Assert.False(population.Objects[126].ApplyLighting);
        Assert.Equal(2588U, population.FinalRandomState);
        var cathedral = Enumerable.Repeat((byte)15, Cells).ToArray();
        AuthoritativeCathedralInitialObjectLighting.Apply(cathedral, preLight, population);
        Assert.All(cathedral, value => Assert.Equal((byte)15, value));
    }

    [Fact]
    public void CatacombsLightsRespectActivePrefixAndKeepStoryCandleRadius()
    {
        var light = Enumerable.Repeat((byte)15, Cells).ToArray();
        var preLight = light.ToArray();
        AuthoritativeCathedralInitialObjectLighting.ApplyCatacombs(light, preLight, Population(44, active: false));
        AuthoritativeCathedralInitialObjectLighting.ApplyCatacombs(light, preLight, Population(57));
        Assert.All(light, value => Assert.Equal((byte)15, value));
        var expected = light.ToArray();
        AuthoritativeCathedralThemeStaticLighting.Apply(expected, preLight.ToArray(), new(50, 50), 3, false);
        AuthoritativeCathedralInitialObjectLighting.ApplyCatacombs(light, preLight, Population(87));
        Assert.Equal(expected, light);
        Assert.All(preLight, value => Assert.Equal((byte)15, value));
    }

    [Fact]
    public void CatacombsIncompleteChannelsRejectBeforeChangingInput()
    {
        var light = Enumerable.Repeat((byte)15, Cells).ToArray();
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralInitialObjectLighting.ApplyCatacombs(light, new byte[1], Population(44)));
        Assert.All(light, value => Assert.Equal((byte)15, value));
    }

    [Fact]
    public void IncompleteChannelsRejectBeforeChangingInput()
    {
        var light = Enumerable.Repeat((byte)15, Cells).ToArray();
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralInitialObjectLighting.Apply(light, new byte[1], Population(0)));
        Assert.All(light, value => Assert.Equal(15, value));
    }

    private static AuthoritativeCathedralPopulationResult Population(int type, bool active = true)
    {
        var objects = Enumerable.Range(0, 127)
            .Select(slot => new AuthoritativeCathedralPopulationObjectState { Slot = slot }).ToArray();
        objects[126] = objects[126] with { Type = type, Position = new(50, 50), ApplyLighting = false };
        // Nonidentity allocation exercises the active prefix, not slot-number
        // iteration or a scan of stale inactive objects.
        return new(objects, active ? 1 : 0, [.. Enumerable.Range(0, 127).Reverse()],
            [.. Enumerable.Range(0, 127)], new int[Cells], [], new ushort[Cells],
            new byte[Cells], new byte[Cells], 123, 2588);
    }
}
