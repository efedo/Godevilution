using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralThemeGeneratorTests
{
    [Fact]
    public void ShrineFitUsesNativeDirectionOffsetsAndHoldsOnlySelectedRegion()
    {
        var map = CreateMap();
        map.Pieces[Index(21, 20)] = 3;
        map.PieceProperties[3] = 1 | 128;
        var parameters = CreateParameters(map, seed: 4);

        var result = AuthoritativeCathedralThemeGenerator.Generate(parameters);

        Assert.Equal([new AuthoritativeCathedralThemeEntry(AuthoritativeCathedralThemeGenerator.ThemeShrine, 0)], result.Themes);
        Assert.Equal(25, result.HeldPopulatedCells.Count(held => held));
        Assert.All(RegionCells(20, 20, 5), point => Assert.True(result.HeldPopulatedCells[Index(point.X, point.Y)]));
        Assert.False(result.HeldPopulatedCells[Index(19, 19)]);

        var expectedRandom = new DeterministicLegacyRandom(4);
        Assert.Equal(1, expectedRandom.GenerateRnd(4));
        Assert.Equal(expectedRandom.State, result.FinalRandomState);
    }

    [Fact]
    public void ShrineFitSupportsTheNativeSecondOrientation()
    {
        var map = CreateMap();
        map.Pieces[Index(20, 21)] = 3;
        map.PieceProperties[3] = 1 | 128;

        var result = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(map, seed: 4));

        Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeShrine, result.Themes.Single().ThemeType);
    }

    [Fact]
    public void Obj5PreservesNativeNegativeRandomFallback()
    {
        // A 3x3 room has no ordinary 5x5 fit; only the native negative-roll fallback succeeds.
        var parameters = CreateParameters(CreateMap(size: 3), seed: 988045466, bestiary: [8]);

        var result = AuthoritativeCathedralThemeGenerator.Generate(parameters);

        Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeSkeletonRoom, result.Themes.Single().ThemeType);
        var expectedRandom = new DeterministicLegacyRandom(988045466);
        Assert.Equal(2, expectedRandom.GenerateRnd(4));
        Assert.Equal(-3, expectedRandom.GenerateRnd(5));
        Assert.Equal(expectedRandom.State, result.FinalRandomState);
    }

    [Fact]
    public void InitialPopulatedAndTriggersRejectThemeRegionsWithoutClearingHeldInput()
    {
        var populated = CreateMap();
        populated.InitialPopulated[Index(20, 20)] = true;
        var populatedResult = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(populated));
        Assert.Empty(populatedResult.Themes);
        Assert.True(populatedResult.HeldPopulatedCells[Index(20, 20)]);

        var triggeredResult = AuthoritativeCathedralThemeGenerator.Generate(
            CreateParameters(CreateMap(), triggers: [new AuthoritativeCathedralPopulationPoint(20, 20)]));
        Assert.Empty(triggeredResult.Themes);
        Assert.Equal(0, triggeredResult.HeldPopulatedCells.Count(held => held));

        var oversized = CreateMap(size: 11);
        var oversizedResult = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(oversized));
        Assert.Empty(oversizedResult.Themes);
        Assert.Equal(0, oversizedResult.HeldPopulatedCells.Count(held => held));
    }

    [Fact]
    public void SignedTransparencyAndCardinalOutOfBoundsBoundaryAreHandledSafely()
    {
        var edge = CreateMap(left: 0, top: 0, size: 3);
        var result = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(edge, seed: 0, maxAttempts: 1024));

        Assert.Single(result.Themes);
        Assert.Equal(0, result.Themes[0].RegionId);
        Assert.Equal(9, result.HeldPopulatedCells.Count(held => held));

        var signed = CreateMap();
        var signedResult = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(signed, seed: 4));
        Assert.Equal(25, signedResult.HeldPopulatedCells.Count(held => held));
        Assert.False(signedResult.HeldPopulatedCells[Index(19, 19)]);
    }

    [Fact]
    public void CathedralObj3UsesTheSameFlipCoinFrequencyAtEveryDepth()
    {
        foreach (var depth in Enumerable.Range(1, 4)) {
            var result = AuthoritativeCathedralThemeGenerator.Generate(
                CreateParameters(CreateMap(size: 3), depth: depth, seed: 0, bestiary: []));
            // Source row-major Obj3 short-circuits/retries end at Barrel, not MonsterPit.
            Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeBarrel, result.Themes.Single().ThemeType);
            Assert.Equal(2_910_103_114U, result.FinalRandomState);
        }
    }

    [Theory]
    [InlineData(34)]
    [InlineData(35)]
    [InlineData(36)]
    [InlineData(37)]
    [InlineData(42)]
    [InlineData(43)]
    [InlineData(44)]
    [InlineData(45)]
    public void GoatShrineRecognizesOriginalGoatIdsAndRejectsAdjacentNonGoats(int nativeId)
    {
        var actualGoat = AuthoritativeCathedralThemeGenerator.Generate(
            CreateParameters(CreateMap(), seed: 0, bestiary: [nativeId]));
        var satyrAndFamiliar = AuthoritativeCathedralThemeGenerator.Generate(
            CreateParameters(CreateMap(), seed: 0, bestiary: [33, 41]));

        Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeGoatShrine, actualGoat.Themes.Single().ThemeType);
        Assert.NotEqual(AuthoritativeCathedralThemeGenerator.ThemeGoatShrine, satyrAndFamiliar.Themes.Single().ThemeType);
    }

    [Fact]
    public void SuccessfulFountainAndTreasureSelectionsConsumeTheirOneUseFlags()
    {
        var fountain = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(
            CreateMap(), seed: SeedForRetryCandidate(AuthoritativeCathedralThemeGenerator.ThemeBloodFountain), bestiary: []));
        var treasure = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(
            CreateMap(size: 3), seed: SeedForRetryCandidate(AuthoritativeCathedralThemeGenerator.ThemeTreasure), bestiary: []));

        Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeBloodFountain, fountain.Themes.Single().ThemeType);
        Assert.False(fountain.Flags.BloodFountainAvailable);
        Assert.True(fountain.Flags.TreasureAvailable);
        Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeTreasure, treasure.Themes.Single().ThemeType);
        Assert.False(treasure.Flags.TreasureAvailable);
    }

    [Fact]
    public void Obj5PositiveRandomUsesTheNativeCandidateRollAndSucceeds()
    {
        var result = AuthoritativeCathedralThemeGenerator.Generate(
            CreateParameters(CreateMap(), seed: 0, bestiary: [34]));

        var expectedRandom = new DeterministicLegacyRandom(0);
        Assert.Equal(0, expectedRandom.GenerateRnd(4));
        Assert.Equal(1, expectedRandom.GenerateRnd(5));
        Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeGoatShrine, result.Themes.Single().ThemeType);
        Assert.Equal(expectedRandom.State, result.FinalRandomState);
    }

    [Fact]
    public void ThemeSelectionAttemptLimitFailsClosedWhenFirstCandidateCannotFit()
    {
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeGenerator.Generate(
            CreateParameters(CreateMap(size: 3), seed: 0, bestiary: [], maxAttempts: 1)));
    }

    [Fact]
    public void NativeTransparencyLoopChecksTheRegionAfterNextTransparency()
    {
        var map = CreateMap();
        FillRegion(map, 40, 40, 3, 1);

        var result = AuthoritativeCathedralThemeGenerator.Generate(CreateParameters(map, seed: 4, nextTransparency: 0));

        Assert.Equal(2, result.Themes.Count);
        Assert.Contains(result.Themes, theme => theme.RegionId == 1);
    }

    [Fact]
    public void ParametersAndResultsAreDetachedAndRequireFreshObjectOccupancy()
    {
        var map = CreateMap();
        map.Pieces[Index(21, 20)] = 3;
        map.PieceProperties[3] = 1 | 128;
        var parameters = CreateParameters(map, seed: 4);
        map.Pieces[Index(20, 20)] = 0;
        map.Transparency[Index(20, 20)] = 99;
        map.PieceProperties[1] = 1;

        var result = AuthoritativeCathedralThemeGenerator.Generate(parameters);

        Assert.Equal(AuthoritativeCathedralThemeGenerator.ThemeShrine, result.Themes.Single().ThemeType);
        Assert.Throws<NotSupportedException>(() => ((IList<bool>)result.HeldPopulatedCells)[0] = true);
        Assert.Throws<NotSupportedException>(() => ((IList<AuthoritativeCathedralThemeEntry>)result.Themes).Add(new(0, 0)));

        var occupied = CreateMap();
        occupied.Objects[Index(20, 20)] = true;
        Assert.Throws<InvalidDataException>(() => CreateParameters(occupied));
    }

    [Fact]
    public void MalformedAndUnsafeNativeInputsFailClosed()
    {
        Assert.Throws<InvalidDataException>(() => CreateParameters(CreateMap(), nextTransparency: 127));
        Assert.Throws<InvalidDataException>(() => CreateParameters(CreateMap(), depth: 5));

        var badPiece = CreateMap();
        badPiece.Pieces[0] = AuthoritativeCathedralThemeParameters.PieceCount;
        Assert.Throws<InvalidDataException>(() => CreateParameters(badPiece));

        Assert.Throws<InvalidDataException>(() => CreateParameters(
            CreateMap(), triggers: [new AuthoritativeCathedralPopulationPoint(-1, 0)]));
        Assert.Throws<InvalidDataException>(() => CreateParameters(CreateMap(), bestiary: [8, 8]));
        Assert.Throws<InvalidDataException>(() => CreateParameters(CreateMap(), bestiary: [138]));
        Assert.Throws<InvalidDataException>(() => CreateParameters(
            CreateMap(), bestiary: Enumerable.Range(0, AuthoritativeCathedralThemeParameters.MaxSelectedBestiaryTypes + 1).ToArray()));
    }

    private static AuthoritativeCathedralThemeParameters CreateParameters(
        ThemeMap map,
        int depth = 1,
        uint seed = 4,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint>? triggers = null,
        IReadOnlyList<int>? bestiary = null,
        int nextTransparency = 0,
        int maxAttempts = 1_000)
        => new(
            depth,
            map.Pieces,
            map.Transparency,
            map.PieceProperties,
            map.InitialPopulated,
            map.Objects,
            nextTransparency,
            triggers ?? [],
            bestiary ?? [8],
            seed,
            maxAttempts);

    private static ThemeMap CreateMap(
        int left = 20,
        int top = 20,
        int size = 5)
    {
        var pieces = new ushort[AuthoritativeCathedralThemeParameters.CellCount];
        var transparency = Enumerable.Repeat(byte.MaxValue, AuthoritativeCathedralThemeParameters.CellCount).ToArray();
        var properties = new byte[AuthoritativeCathedralThemeParameters.PieceCount];
        var initialPopulated = new bool[AuthoritativeCathedralThemeParameters.CellCount];
        var objects = new bool[AuthoritativeCathedralThemeParameters.CellCount];
        properties[0] = 1;
        properties[1] = 0;

        for (var y = top; y < top + size; y++)
            for (var x = left; x < left + size; x++) {
                pieces[Index(x, y)] = 1;
                transparency[Index(x, y)] = 0;
            }

        return new ThemeMap(pieces, transparency, properties, initialPopulated, objects);
    }

    private static IEnumerable<AuthoritativeCathedralPopulationPoint> RegionCells(int left, int top, int size)
    {
        for (var y = top; y < top + size; y++)
            for (var x = left; x < left + size; x++)
                yield return new AuthoritativeCathedralPopulationPoint(x, y);
    }

    private static void FillRegion(ThemeMap map, int left, int top, int size, byte region)
    {
        foreach (var point in RegionCells(left, top, size)) {
            map.Pieces[Index(point.X, point.Y)] = 1;
            map.Transparency[Index(point.X, point.Y)] = region;
        }
    }

    private static int Index(int x, int y)
        => x + y * AuthoritativeCathedralThemeParameters.Width;

    private static uint SeedForRetryCandidate(int candidate)
    {
        for (uint seed = 0; seed < 100_000; seed++) {
            var random = new DeterministicLegacyRandom(seed);
            _ = random.GenerateRnd(4);
            if (random.GenerateRnd(17) == candidate)
                return seed;
        }
        throw new InvalidOperationException($"No deterministic seed found for retry candidate {candidate}.");
    }

    private sealed record ThemeMap(
        ushort[] Pieces,
        byte[] Transparency,
        byte[] PieceProperties,
        bool[] InitialPopulated,
        bool[] Objects);
}
