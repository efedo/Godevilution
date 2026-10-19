using System.Reflection;

using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Guards the opt-in native-profile CLI without touching licensed input packs. */
public sealed class CathedralNativePopulationPlaytestCompilerTests
{
    [Fact]
    public void NativeProfileUsesItsOwnAdapterAndSidecarNames()
    {
        Assert.Equal("devilution.diablo.cathedral-native-population.v1",
            CathedralNativePopulationPlaytestCompiler.AdapterId);
        Assert.Equal("cathedral-native-population-playtest.json",
            CathedralNativePopulationPlaytestCompiler.ProfileFileName);
        Assert.Equal("native_cathedral_population",
            CathedralNativePopulationPlaytestCompiler.PopulationDirectoryName);
        Assert.Equal("native_cathedral_runtime",
            CathedralNativePopulationPlaytestCompiler.RuntimeDirectoryName);
        Assert.Equal("devilution.diablo.cathedral-native-class-progression.v1",
            CathedralNativePopulationPlaytestCompiler.ClassProgressionAdapterId);
        Assert.Equal("cathedral-native-class-progression-playtest.json",
            CathedralNativePopulationPlaytestCompiler.ClassProgressionProfileFileName);
        Assert.Equal("native-character-start.tsv",
            CathedralNativePopulationPlaytestCompiler.NativeCharacterStartTablePath);
        Assert.Equal(8, typeof(CathedralNativePopulationPlaytestCompilerOptions)
            .GetConstructors().Single().GetParameters().Length);
    }

    [Fact]
    public void NativeClassSelectorIsRestrictedToTheNewDepthProgressionProfile()
    {
        using var output = new TemporaryDirectory();
        var unavailableInput = Path.Combine(output.Path, "unavailable-input");
        var absent = Path.Combine(output.Path, "must-not-exist");
        var common = new[] {
            "--base-directory", unavailableInput,
            "--geometry-directory", unavailableInput,
            "--metadata-directory", unavailableInput,
            "--monster-runtime-table", unavailableInput,
            "--inventory-footprints", unavailableInput,
            "--output-directory", absent,
            "--depth-seeds", "2588,2588,1,4294967295",
        };

        var unsupportedClass = InvokeMain([
            "--native-cathedral-depth-progression", "--native-class", "monk", .. common,
        ]);
        Assert.Equal(1, unsupportedClass.ExitCode);
        Assert.Contains("--native-class accepts only warrior or sorcerer", unsupportedClass.Error);
        Assert.False(Directory.Exists(absent));

        var wrongMode = InvokeMain([
            "--native-cathedral-population", "--native-class", "warrior", .. common,
        ]);
        Assert.Equal(1, wrongMode.ExitCode);
        Assert.Contains("only with --native-cathedral-depth-progression", wrongMode.Error);
        Assert.False(Directory.Exists(absent));

        var acceptedClass = InvokeMain([
            "--native-cathedral-depth-progression", "--native-class", "warrior", .. common,
        ]);
        Assert.Equal(1, acceptedClass.ExitCode);
        Assert.DoesNotContain("--native-class accepts only", acceptedClass.Error);
        Assert.False(Directory.Exists(absent));
    }

    [Fact]
    public void CliRejectsIncompleteInputsAndUnsupportedDepthSeedsBeforeReadingOrPublishing()
    {
        using var output = new TemporaryDirectory();
        var absent = Path.Combine(output.Path, "must-not-exist");
        var unavailableInput = Path.Combine(output.Path, "unavailable-input");

        var incomplete = InvokeMain(["--native-cathedral-population"]);
        Assert.Equal(1, incomplete.ExitCode);
        Assert.Contains("--native-cathedral-population", incomplete.Error);
        var wrongSeeds = InvokeMain([
            "--native-cathedral-population",
            "--base-directory", unavailableInput,
            "--geometry-directory", unavailableInput,
            "--metadata-directory", unavailableInput,
            "--monster-runtime-table", unavailableInput,
            "--inventory-footprints", unavailableInput,
            "--output-directory", absent,
            "--depth-seeds", "2588,2588,1,1",
        ]);
        Assert.Equal(1, wrongSeeds.ExitCode);
        Assert.Contains("four explicit supported depth seeds [2588,2588,1,4294967295]", wrongSeeds.Error);

        Assert.False(Directory.Exists(absent));

        var baseDirectory = Path.Combine(output.Path, "base");
        var nestedOutput = Path.Combine(baseDirectory, "compiled");
        var overlapping = InvokeMain([
            "--native-cathedral-population",
            "--base-directory", baseDirectory,
            "--geometry-directory", Path.Combine(output.Path, "geometry"),
            "--metadata-directory", Path.Combine(output.Path, "metadata"),
            "--monster-runtime-table", Path.Combine(output.Path, "runtime.tsv"),
            "--inventory-footprints", Path.Combine(output.Path, "footprints.tsv"),
            "--output-directory", nestedOutput,
            "--depth-seeds", "2588,2588,1,4294967295",
        ]);
        Assert.Equal(1, overlapping.ExitCode);
        Assert.Contains("overlap", overlapping.Error.ToLowerInvariant());
        Assert.False(Directory.Exists(nestedOutput));
    }

    private static MainResult InvokeMain(string[] args)
    {
        var program = typeof(CathedralNativePopulationPlaytestCompiler).Assembly
            .GetType("Godevilution.Tools.Program", throwOnError: true)!;
        var main = program.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Cathedral.Playtest has no public Main entry point.");
        var originalError = Console.Error;
        using var error = new StringWriter();
        try {
            Console.SetError(error);
            var exitCode = (int)(main.Invoke(null, [args])
                ?? throw new InvalidOperationException("Cathedral.Playtest Main returned no status."));
            return new(exitCode, error.ToString());
        } finally {
            Console.SetError(originalError);
        }
    }

    private sealed record MainResult(int ExitCode, string Error);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "Godevilution-Native-Cathedral-CLI-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
