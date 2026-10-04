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
