using Devilution.Data;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Contract and refusal coverage for the independent Cathedral door package. */
public sealed class CathedralDoorPlaytestCompilerTests
{
    [Fact]
    public void DoorPackageUsesIndependentAdapterIdentityAndSafeDefaults()
    {
        var options = new CathedralDoorPlaytestCompilerOptions(
            new ContentPack("base", "1.0.0", []),
            NewOutput(),
            "pieces",
            "properties",
            [1U, 2U, 3U, 4U]);

        Assert.Equal("godevilution.cathedral-doors-playtest", options.PackId);
        Assert.Equal("0.1.0", options.PackVersion);
        Assert.Equal("devilution.diablo.cathedral-doors.v1", CathedralDoorPlaytestCompiler.AdapterId);
        Assert.Equal("cathedral-doors-playtest.json", CathedralDoorPlaytestCompiler.ProfileFileName);
        Assert.Equal(CathedralDepthPlaytestPlacementPolicy.None, options.PlacementPolicy);
    }

    [Fact]
    public void RejectsMalformedSeedCountBeforeCreatingOutput()
    {
        var output = NewOutput();
        try
        {
            Assert.Throws<ArgumentException>(() => CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    new ContentPack("base", "1.0.0", []),
                    output,
                    "pieces",
                    "properties",
                    [1U, 2U, 3U])));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void RejectsAlternatePackIdentityWithoutCreatingOutput()
    {
        var output = NewOutput();
        try
        {
            Assert.Throws<ArgumentException>(() => CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    new ContentPack("base", "1.0.0", []),
                    output,
                    "pieces",
                    "properties",
                    [1U, 2U, 3U, 4U]) {
                    PackId = "godevilution.cathedral-depths-playtest",
                }));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void RejectsPreviousEntryThatTheDoorLauncherCannotActivate()
    {
        var output = NewOutput();
        try
        {
            Assert.Throws<ArgumentException>(() => CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    new ContentPack("base", "1.0.0", []),
                    output,
                    "pieces",
                    "properties",
                    [1U, 2U, 3U, 4U]) {
                    Entry = AuthoritativeCathedralEntry.Previous,
                }));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void LegacyDepthProfileIdentityRemainsSeparate()
    {
        Assert.Equal("godevilution.cathedral-depths-playtest",
            new CathedralDepthPlaytestCompilerOptions(
                new ContentPack("base", "1.0.0", []), NewOutput(), "pieces", "properties",
                [1U, 2U, 3U, 4U]).PackId);
        Assert.Equal("devilution.diablo.cathedral-depths.v1", CathedralDepthPlaytestCompiler.AdapterId);
        Assert.Equal("cathedral-depths-playtest.json", CathedralDepthPlaytestCompiler.ProfileFileName);
    }

    /** Uses caller-provided private semantic inputs only; no licensed fixture is checked in. */
    [Fact(Explicit = true)]
    public void CompilesDeterministicDoorMarkerWhenPrivateSemanticInputsAreAvailable()
    {
        var geometryRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_CATHEDRAL_DOOR_PLAYTEST_GEOMETRY");
        if (string.IsNullOrWhiteSpace(geometryRoot))
            geometryRoot = FindRepositoryRoot() is { } root
                ? Path.Combine(root, ".local", "cathedral-depths-playtest", "content")
                : null;
        if (geometryRoot is null
            || !File.Exists(Path.Combine(geometryRoot, "native_dun_piece_definitions.tsv"))
            || !File.Exists(Path.Combine(geometryRoot, "native_dun_tile_properties.tsv")))
        {
            Assert.Skip("Private semantic Cathedral TIL/SOL inputs are not available.");
            return;
        }

        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(FindRepositoryRoot()!, "server", "content", "base"));
        var first = NewOutput();
        var second = NewOutput();
        var completed = false;
        try
        {
            var seeds = new uint[] { 2588, 2588, 1, uint.MaxValue };
            var firstProfile = CathedralDoorPlaytestCompiler.Compile(new(
                basePack, first,
                File.ReadAllText(Path.Combine(geometryRoot!, "native_dun_piece_definitions.tsv")),
                File.ReadAllText(Path.Combine(geometryRoot!, "native_dun_tile_properties.tsv")),
                seeds));
            var secondProfile = CathedralDoorPlaytestCompiler.Compile(new(
                basePack, second,
                File.ReadAllText(Path.Combine(geometryRoot!, "native_dun_piece_definitions.tsv")),
                File.ReadAllText(Path.Combine(geometryRoot!, "native_dun_tile_properties.tsv")),
                seeds));

            Assert.Equal(CathedralDoorPlaytestCompiler.AdapterId, firstProfile.AdapterId);
            Assert.Equal(seeds, firstProfile.DepthSeeds);
            Assert.False(firstProfile.NativePopulationClaim);
            Assert.Equal(4, firstProfile.DoorMappings.Count);
            Assert.Equal(new[] { 1U, 2U }, new[] {
                firstProfile.DoorMappings[0].LeftNativeObjectId,
                firstProfile.DoorMappings[0].RightNativeObjectId,
            });
            Assert.Equal(firstProfile.ContentManifestHash, secondProfile.ContentManifestHash);
            Assert.Equal(firstProfile.PackFileHash, secondProfile.PackFileHash);
            Assert.True(File.Exists(Path.Combine(first, CathedralDoorPlaytestCompiler.ProfileFileName)));
            Assert.False(File.Exists(Path.Combine(first, CathedralDepthPlaytestCompiler.ProfileFileName)));
            var doorTable = ContentPackLoader.LoadManifestDirectory(
                    Path.Combine(first, CathedralDoorPlaytestCompiler.ContentDirectoryName))
                .Tables.Single(table => table.SourcePath == AuthoritativeCathedralDoorCatalog.TableName);
            Assert.Equal(4, doorTable.Rows.Count);
            Assert.Equal(new[] { "l1_left_door", "l1_right_door" }, new[] {
                doorTable.Rows[0].Required("left_object_symbol"),
                doorTable.Rows[0].Required("right_object_symbol"),
            });
            var files = Directory.EnumerateFiles(first, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(first, path)).Order().ToArray();
            Assert.Equal(files, Directory.EnumerateFiles(second, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(second, path)).Order());
            foreach (var file in files)
                Assert.Equal(File.ReadAllBytes(Path.Combine(first, file)),
                    File.ReadAllBytes(Path.Combine(second, file)));
            completed = true;
        }
        finally
        {
            if (completed)
            {
                DeleteOutput(first);
                DeleteOutput(second);
            }
        }
    }

    private static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        return null;
    }

    private static string NewOutput()
        => Path.Combine(Path.GetTempPath(), "godevilution-cathedral-doors-" + Guid.NewGuid().ToString("N"));

    private static void DeleteOutput(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
