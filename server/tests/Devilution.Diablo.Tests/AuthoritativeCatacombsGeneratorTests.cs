using System.Buffers.Binary;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCatacombsGeneratorTests
{
    [Theory]
    [InlineData(5, 1677631846U, 3738464040U, 927053264U, 27, 28, 26, 62)]
    [InlineData(6, 2034738122U, 3211844236U, 3785622512U, 33, 26, 34, 52)]
    [InlineData(7, 680552750U, 2378242562U, 3180734697U, 27, 26, 78, 52)]
    [InlineData(8, 1999936419U, 2237732347U, 972963310U, 39, 74, 48, 46)]
    public void OrdinaryFamilyMatchesExistingNativeGolden(int depth, uint seed, uint attempt, uint final,
        int mainX, int mainY, int previousX, int previousY)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var bytes = File.ReadAllBytes(Path.Combine(directory.FullName, "test", "fixtures", "diablo", $"{depth}-{seed}.dun"));
        var words = Enumerable.Range(0, bytes.Length / 2)
            .Select(index => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2, 2))).ToArray();
        Assert.Equal(new ushort[] { 40, 40 }, words.Take(2));
        foreach (var entry in new[] { AuthoritativeCatacombsEntry.Main, AuthoritativeCatacombsEntry.Previous }) {
            var result = AuthoritativeCatacombsGenerator.Generate(depth, seed, entry);
            Assert.Equal(attempt, result.AttemptSeed);
            Assert.Equal(final, result.FinalRandomState);
            Assert.Equal(words.Skip(2).Take(1600), result.MegaTiles);
            Assert.Equal(words.Skip(20802).Take(6400),
                Enumerable.Range(0, 6400).Select(cell => (ushort)result.GetTransparency(16 + cell % 80, 16 + cell / 80)));
            Assert.Equal(entry == AuthoritativeCatacombsEntry.Main ? new(mainX, mainY) : new AuthoritativeCathedralPoint(previousX, previousY),
                result.EntryPosition);
            if (depth == 5)
                Assert.Equal(new AuthoritativeCathedralPoint(33, 56),
                    AuthoritativeCatacombsGenerator.Generate(depth, seed, AuthoritativeCatacombsEntry.TownWarpDown).EntryPosition);
        }
    }

    [Fact]
    public void UnsupportedDepthPolicyAndEntryFailBeforeGeneration()
    {
        foreach (var depth in new[] { -1, 0, 1, 4, 9, int.MaxValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCatacombsGenerator.Generate(depth, 0, AuthoritativeCatacombsEntry.Main));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCatacombsGenerator.Generate(5, 0, (AuthoritativeCatacombsEntry)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCatacombsGenerator.Generate(5, 0,
            AuthoritativeCatacombsEntry.Main, policy: (AuthoritativeCatacombsGenerationPolicy)99));
        foreach (var depth in new[] { 6, 7, 8 })
            Assert.Throws<ArgumentOutOfRangeException>(() => AuthoritativeCatacombsGenerator.Generate(depth, 0, AuthoritativeCatacombsEntry.TownWarpDown));
    }

    [Fact]
    public void RetainedZeroAndWinningAttemptReplayUseIndependentOwnedState()
    {
        var zero = AuthoritativeCatacombsGenerator.Generate(5, 0, AuthoritativeCatacombsEntry.Main);
        var retained = AuthoritativeCatacombsGenerator.Generate(5, 0xABCDEF01, AuthoritativeCatacombsEntry.Main, 0);
        Assert.Equal(0U, retained.InitialLevelSeed);
        Assert.Null(zero.InitialLevelSeed);
        Assert.Equal(zero.MegaTiles, retained.MegaTiles);
        Assert.Equal(zero.NativeTransparency, retained.NativeTransparency);
        Assert.Equal(zero.AttemptSeed, retained.AttemptSeed);
        Assert.Equal(zero.FinalRandomState, retained.FinalRandomState);
        _ = AuthoritativeCatacombsGenerator.Generate(8, uint.MaxValue, AuthoritativeCatacombsEntry.Previous);
        var replay = AuthoritativeCatacombsGenerator.Generate(5, zero.AttemptSeed, AuthoritativeCatacombsEntry.Main);
        Assert.Equal(zero.MegaTiles, replay.MegaTiles);
        Assert.Equal(zero.ReplacementMegaTiles, replay.ReplacementMegaTiles);
        Assert.Equal(zero.ProtectedMegaTiles, replay.ProtectedMegaTiles);
        Assert.Equal(zero.NativeTransparency, replay.NativeTransparency);
        Assert.Equal(zero.NativeTileFlags, replay.NativeTileFlags);
        Assert.Equal(zero.Themes, replay.Themes);
        Assert.Equal(zero.FinalRandomState, replay.FinalRandomState);
        Assert.Equal(zero.AttemptSeed, replay.AttemptSeed);
        Assert.Equal((byte)0, zero.GetTransparency(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => zero.GetTransparency(112, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => zero.GetMegaTile(-1, 0));
        Assert.Throws<NotSupportedException>(() => ((IList<ushort>)zero.MegaTiles)[0] = 1);
    }

    private static readonly Lazy<NativeCatacombsGenerationReference> NativeReference =
        new(NativeCatacombsGenerationReference.Load);

    public static IEnumerable<object?[]> NativeCaseInputs()
    {
        uint[] common = [0, 1, 2588, 0x80000000, uint.MaxValue];
        uint[] legacy = [1677631846, 2034738122, 680552750, 1999936419];
        for (var depth = 5; depth <= 8; depth++) {
            foreach (var seed in common.Append(legacy[depth - 5])) {
                yield return [depth, seed, AuthoritativeCatacombsEntry.Main, null];
                yield return [depth, seed, AuthoritativeCatacombsEntry.Previous, null];
                if (depth == 5) yield return [depth, seed, AuthoritativeCatacombsEntry.TownWarpDown, null];
            }
            var supplied = 0x13579BDFU + (uint)depth;
            var retained = 0x2468ACE0U + (uint)depth;
            yield return [depth, retained, AuthoritativeCatacombsEntry.Main, null];
            yield return [depth, supplied, AuthoritativeCatacombsEntry.Main, 0U];
            yield return [depth, supplied, AuthoritativeCatacombsEntry.Main, retained];
        }
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(NativeCaseInputs))]
    public void PureGenerationAndMaterializationMatchNativeCase(int depth, uint seed,
        AuthoritativeCatacombsEntry entry, uint? initialLevelSeed)
    {
        var reference = NativeReference.Value;
        var item = Assert.Single(reference.Cases, item => item.Result is { } result
            && result.Depth == depth && result.InputSeed == seed && result.Entry == entry
            && result.InitialLevelSeed == initialLevelSeed);
        var expected = item.Result!;
        var actual = AuthoritativeCatacombsGenerator.Generate(expected.Depth, expected.InputSeed, expected.Entry,
            expected.InitialLevelSeed, expected.Policy);
        Assert.Equal(expected.Depth, actual.Depth);
        Assert.Equal(expected.Policy, actual.Policy);
        Assert.Equal(expected.Entry, actual.Entry);
        Assert.Equal(expected.InputSeed, actual.InputSeed);
        Assert.Equal(expected.InitialLevelSeed, actual.InitialLevelSeed);
        Assert.Equal(expected.AttemptSeed, actual.AttemptSeed);
        Assert.Equal(expected.FinalRandomState, actual.FinalRandomState);
        Assert.Equal(expected.MegaTiles, actual.MegaTiles);
        Assert.Equal(expected.ReplacementMegaTiles, actual.ReplacementMegaTiles);
        Assert.Equal(expected.ProtectedMegaTiles, actual.ProtectedMegaTiles);
        Assert.Equal(expected.NativeTransparency, actual.NativeTransparency);
        Assert.Equal(expected.NativeTileFlags, actual.NativeTileFlags);
        Assert.Equal(expected.Themes, actual.Themes);
        Assert.Equal(expected.StairsUp, actual.StairsUp);
        Assert.Equal(expected.StairsDown, actual.StairsDown);
        Assert.Equal(expected.TownWarpStairs, actual.TownWarpStairs);
        Assert.Equal(expected.EntryPosition, actual.EntryPosition);
        Assert.Equal(expected.MinimumBounds, actual.MinimumBounds);
        Assert.Equal(expected.MaximumBounds, actual.MaximumBounds);
        Assert.Equal(expected.SetPieceRoom, actual.SetPieceRoom);
        Assert.Equal(expected.SetPiece, actual.SetPiece);
        Assert.Equal(expected.NextTransparencyValue, actual.NextTransparencyValue);
        var geometry = AuthoritativeCatacombsGeometryMaterializer.Materialize((uint)actual.Depth,
            actual, reference.Pieces, reference.Sol, "catacombs-" + actual.Depth);
        Assert.Equal(item.FinePieces, geometry.Level.NativePieceLayer);
        Assert.Equal(item.FineSpecials.Select(v => (ushort)v), geometry.Level.NativeSpecialLayer);
        Assert.Equal(item.Triggers, geometry.Triggers);
        Assert.Single(geometry.Triggers, t => t.Kind == AuthoritativeCatacombsTriggerKind.NextLevel);
    }
}
