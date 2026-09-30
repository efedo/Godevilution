using System.Buffers.Binary;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralLevel1GeneratorTests
{
    [Theory]
    [InlineData(2588U, 77, 46, 49, 49)]
    [InlineData(743271966U, 51, 82, 81, 47)]
    public void OrdinaryLevel1MatchesNativeGolden(
        uint seed,
        int mainX,
        int mainY,
        int previousX,
        int previousY)
    {
        var expected = ReadGolden(seed);
        var main = AuthoritativeCathedralLevel1Generator.Generate(seed, AuthoritativeCathedralEntry.Main);
        var previous = AuthoritativeCathedralLevel1Generator.Generate(seed, AuthoritativeCathedralEntry.Previous);

        var expectedUp = new AuthoritativeCathedralPoint((mainX - 19) / 2, (mainY - 20) / 2);
        Assert.Equal(expectedUp, main.StairsUp);
        Assert.Equal(expected.MegaTiles, main.MegaTiles);
        Assert.Equal(expected.Transparency, main.NativeTransparency);
        Assert.Equal(main.MegaTiles, previous.MegaTiles);
        Assert.Equal(main.NativeTransparency, previous.NativeTransparency);
        Assert.Equal((mainX, mainY), (main.EntryPosition.X, main.EntryPosition.Y));
        Assert.Equal((previousX, previousY), (previous.EntryPosition.X, previous.EntryPosition.Y));
    }

    [Fact]
    public void IndependentInstancesAndRepeatedGenerationAreDeterministic()
    {
        var first = AuthoritativeCathedralLevel1Generator.Generate(2588, AuthoritativeCathedralEntry.Main);
        var second = AuthoritativeCathedralLevel1Generator.Generate(2588, AuthoritativeCathedralEntry.Main);
        var independent = AuthoritativeCathedralLevel1Generator.Generate(2588, AuthoritativeCathedralEntry.Main);

        Assert.Equal(first.InputSeed, second.InputSeed);
        Assert.Equal(first.AttemptSeed, second.AttemptSeed);
        Assert.Equal(first.FinalRandomState, second.FinalRandomState);
        Assert.Equal(first.MegaTiles, second.MegaTiles);
        Assert.Equal(first.NativeTransparency, second.NativeTransparency);
        Assert.Equal(first.StairsUp, second.StairsUp);
        Assert.Equal(first.StairsDown, second.StairsDown);
        Assert.Equal(first.EntryPosition, second.EntryPosition);
        Assert.Equal(first.MegaTiles, independent.MegaTiles);
        Assert.Equal(first.NativeTransparency, independent.NativeTransparency);
    }

    [Fact]
    public void InterleavingDifferentSeedsDoesNotPerturbEarlierSeed()
    {
        var first = AuthoritativeCathedralLevel1Generator.Generate(2588, AuthoritativeCathedralEntry.Main);
        _ = AuthoritativeCathedralLevel1Generator.Generate(743271966, AuthoritativeCathedralEntry.Previous);
        var again = AuthoritativeCathedralLevel1Generator.Generate(2588, AuthoritativeCathedralEntry.Main);

        Assert.Equal(first.MegaTiles, again.MegaTiles);
        Assert.Equal(first.NativeTransparency, again.NativeTransparency);
        Assert.Equal(first.FinalRandomState, again.FinalRandomState);
        Assert.Equal(first.EntryPosition, again.EntryPosition);
    }

    [Fact]
    public void RejectsInvalidEntryBeforeGeneration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthoritativeCathedralLevel1Generator.Generate(
                2588,
                (AuthoritativeCathedralEntry)99));
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(0x80000000U)]
    [InlineData(uint.MaxValue)]
    [InlineData(4142173246U)] // The first LCG advance produces signed int.MinValue.
    public void SupportsEdgeSeedsWithNativeOutputShape(uint seed)
    {
        var result = AuthoritativeCathedralLevel1Generator.Generate(
            seed,
            AuthoritativeCathedralEntry.Main);

        Assert.Equal(40 * 40, result.MegaTiles.Count);
        Assert.Equal(80 * 80, result.NativeTransparency.Count);
        Assert.Equal(result.MegaTiles[0], result.GetMegaTile(0, 0));
        Assert.Equal(result.MegaTiles[^1], result.GetMegaTile(39, 39));
        Assert.Equal(result.NativeTransparency[0], result.GetTransparency(0, 0));
        Assert.Equal(result.NativeTransparency[^1], result.GetTransparency(79, 79));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetMegaTile(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetMegaTile(40, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetTransparency(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetTransparency(80, 0));
    }

    private static Golden ReadGolden(uint seed)
    {
        var path = Path.Combine(FindRepositoryRoot(), "test", "fixtures", "diablo", $"1-{seed}.dun");
        var bytes = File.ReadAllBytes(path);
        var words = new ushort[bytes.Length / sizeof(ushort)];
        for (var index = 0; index < words.Length; index++)
            words[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort), sizeof(ushort)));
        Assert.Equal((ushort)40, words[0]);
        Assert.Equal((ushort)40, words[1]);
        var megaTiles = words.Skip(2).Take(40 * 40).ToArray();
        var transparency = words.Skip(2 + 40 * 40 * 13).Take(80 * 80).ToArray();
        return new Golden(megaTiles, transparency);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record Golden(IReadOnlyList<ushort> MegaTiles, IReadOnlyList<ushort> Transparency);
}
