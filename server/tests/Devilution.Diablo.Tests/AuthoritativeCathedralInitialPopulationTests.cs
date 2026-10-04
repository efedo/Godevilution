using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralInitialPopulationTests
{
    [Fact]
    public void ComposedPhaseUsesManagedSelectionsAndHoldsThenResetsPopulationSeed()
    {
        var parameters = CreateParameters();
        var actual = AuthoritativeCathedralInitialPopulationGenerator.Generate(parameters);
        Assert.Single(actual.Bestiary.Entries);
        Assert.Equal(109, actual.Bestiary.Entries[0].NativeType);
        var theme = Assert.Single(actual.Themes.Themes);
        Assert.Equal(1, theme.RegionId);
        Assert.NotEqual(actual.Bestiary.FinalRandomState, actual.Themes.FinalRandomState);
        Assert.True(actual.Themes.HeldPopulatedCells[11 + 11 * 112]);

        var held = new AuthoritativeCathedralPopulationGeometry(
            parameters.PreThemeGeometry.Pieces, parameters.PreThemeGeometry.Special,
            parameters.PreThemeGeometry.Transparency, parameters.PreThemeGeometry.PieceProperties,
            actual.Themes.HeldPopulatedCells, parameters.PreThemeGeometry.PlayerOccupied,
            parameters.PreThemeGeometry.MonsterOccupied);
        var expected = AuthoritativeCathedralPopulationGenerator.Generate(new(
            1, parameters.PopulationSeed, parameters.StorySeed, held, Objects(),
            actual.Bestiary.PopulationDefinitions, Player(), 0, [100, 101, 102]));
        Assert.Equal(expected.RandomAfterGolems, actual.Population.RandomAfterGolems);
        Assert.Equal(expected.FinalRandomState, actual.FinalRandomState);
        Assert.Equal(expected.Objects, actual.Population.Objects);
        Assert.Equal(expected.ActiveObjectIds, actual.Population.ActiveObjectIds);
        Assert.Equal(expected.AvailableObjectIds, actual.Population.AvailableObjectIds);
        Assert.Equal(expected.TileObjectRefs, actual.Population.TileObjectRefs);
    }

    [Fact]
    public void RepeatedComposedGenerationIsDetachedAndDoesNotMutatePreThemeGeometry()
    {
        var parameters = CreateParameters();
        var before = parameters.PreThemeGeometry.Populated.ToArray();
        var first = AuthoritativeCathedralInitialPopulationGenerator.Generate(parameters);
        var second = AuthoritativeCathedralInitialPopulationGenerator.Generate(parameters);
        Assert.Equal(before, parameters.PreThemeGeometry.Populated);
        Assert.Equal(first.FinalRandomState, second.FinalRandomState);
        Assert.Equal(first.Themes.Themes, second.Themes.Themes);
        Assert.Equal(first.Themes.HeldPopulatedCells, second.Themes.HeldPopulatedCells);
        Assert.Equal(first.Population.Objects, second.Population.Objects);
        Assert.NotSame(first.Population.Objects, second.Population.Objects);
        Assert.NotSame(first.Themes.HeldPopulatedCells, second.Themes.HeldPopulatedCells);
    }

    [Fact]
    public void ComposedAdmissionRejectsBadCatalogsAndUnsafeTransparencyBeforeGeneration()
    {
        Assert.Throws<ArgumentNullException>(() => AuthoritativeCathedralInitialPopulationGenerator.Generate(null!));
        Assert.Throws<InvalidDataException>(() => CreateParameters(nextTransparency: 127));
        Assert.Throws<InvalidDataException>(() => CreateParameters(catalog: Catalog()[..137]));
        Assert.Throws<InvalidDataException>(() => CreateParameters(objects: Objects()[..108]));
    }

    private static AuthoritativeCathedralInitialPopulationParameters CreateParameters(
        int nextTransparency = 1,
        AuthoritativeCathedralRawMonsterDefinition[]? catalog = null,
        AuthoritativeCathedralPopulationObjectDefinition[]? objects = null)
    {
        var pieces = Enumerable.Repeat((ushort)1, 112 * 112).ToArray();
        var transparency = new byte[112 * 112];
        // A 3x3 floor enclosed by a populated-region-sized solid/trap wall ring.
        for (var y = 9; y <= 13; y++) {
            for (var x = 9; x <= 13; x++) {
                var index = x + y * 112;
                transparency[index] = 1;
                if (x is 9 or 13 || y is 9 or 13)
                    pieces[index] = 2;
            }
        }
        var sol = new byte[1379];
        sol[2] = 129;
        var geometry = new AuthoritativeCathedralPopulationGeometry(pieces, new byte[112 * 112],
            transparency, sol, new bool[112 * 112], new bool[112 * 112], new bool[112 * 112]);
        return new(1, 1, 0x13579BDF, geometry, catalog ?? Catalog(), objects ?? Objects(),
            Player(), 0, [100, 101, 102], nextTransparency, []);
    }

    private static AuthoritativeCathedralPopulationPlayer Player()
        => new(0, 1, true, false, new(40, 40), new(40, 40));

    private static AuthoritativeCathedralPopulationObjectDefinition[] Objects()
        => Enumerable.Range(0, 109).Select(id =>
            new AuthoritativeCathedralPopulationObjectDefinition(id, 0, 0, 1, 1, 0)).ToArray();

    private static AuthoritativeCathedralRawMonsterDefinition[] Catalog()
        => Enumerable.Range(0, 138).Select(id => new AuthoritativeCathedralRawMonsterDefinition(
            id, AuthoritativeCathedralMonsterAvailability.Never, 0, 0, 0,
            id is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27,
            2, 4, 0, 0, 0, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            false, 64, [1, 0, 0, 0, 0, 0], [1, 1, 1, 1, 1, 1])).ToArray();
}
