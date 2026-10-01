using System.Globalization;
using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/** Explicit offline pack compilation; neither installation discovery nor archive loading. */
internal static class Program
{
    private const string Usage = "Cathedral.Playtest --base-directory <pack> --geometry-directory <semantic TSVs> --output-directory <new directory> [--seed <uint32>]";
    private const string DepthUsage = "Cathedral.Playtest --base-directory <pack> --geometry-directory <semantic TSVs> --output-directory <new directory> --depth-seeds <seed1,seed2,seed3,seed4> [--placement-policy <none|authored-diagnostic>]";

    public static int Main(string[] args)
    {
        try {
            if (args is ["--help"]) {
                Console.WriteLine(Usage);
                Console.WriteLine(DepthUsage);
                return 0;
            }
            if (args.Contains("--depth-seeds", StringComparer.Ordinal))
                return CompileDepths(args);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index += 2) {
                if (index + 1 >= args.Length || args[index] is not ("--base-directory" or "--geometry-directory" or "--output-directory" or "--seed")
                    || string.IsNullOrWhiteSpace(args[index + 1]) || !values.TryAdd(args[index], args[index + 1]))
                    throw new ArgumentException(Usage);
            }
            if (!values.TryGetValue("--base-directory", out var baseDirectory)
                || !values.TryGetValue("--geometry-directory", out var geometryDirectory)
                || !values.TryGetValue("--output-directory", out var outputDirectory))
                throw new ArgumentException(Usage);
            var seed = 2588U;
            if (values.TryGetValue("--seed", out var text)
                && !uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
                throw new ArgumentException("The seed must be an unsigned 32-bit integer.");

            var basePack = ContentPackLoader.LoadManifestDirectory(baseDirectory);
            var geometry = ContentPackLoader.LoadDirectory("explicit-cathedral-geometry", "1", geometryDirectory);
            var pieces = geometry.Tables.SingleOrDefault(table => table.SourcePath == AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath)
                ?? throw new InvalidDataException("Missing explicit native_dun_piece_definitions.tsv.");
            var properties = geometry.Tables.SingleOrDefault(table => table.SourcePath == AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath)
                ?? throw new InvalidDataException("Missing explicit native_dun_tile_properties.tsv.");
            var profile = CathedralL1PlaytestCompiler.Compile(new CathedralL1PlaytestCompilerOptions(
                basePack, outputDirectory, pieces.ToCanonicalTsv(), properties.ToCanonicalTsv(), seed));
            Console.WriteLine($"Compiled diagnostic Cathedral profile: {Path.GetFullPath(outputDirectory)}");
            Console.WriteLine($"Content manifest: {profile.ContentManifestHash}");
            Console.WriteLine("Native geometry; authored diagnostic population. This is not campaign or native population parity.");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException
            or InvalidOperationException or InvalidDataException) {
            Console.Error.WriteLine($"Cathedral profile compilation failed: {exception.Message}");
            return 1;
        }
    }

    private static int CompileDepths(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2) {
            if (index + 1 >= args.Length
                || args[index] is not ("--base-directory" or "--geometry-directory"
                    or "--output-directory" or "--depth-seeds" or "--placement-policy")
                || string.IsNullOrWhiteSpace(args[index + 1])
                || !values.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException(DepthUsage);
        }
        if (!values.TryGetValue("--base-directory", out var baseDirectory)
            || !values.TryGetValue("--geometry-directory", out var geometryDirectory)
            || !values.TryGetValue("--output-directory", out var outputDirectory)
            || !values.TryGetValue("--depth-seeds", out var seedText))
            throw new ArgumentException(DepthUsage);

        var seedParts = seedText.Split(',', StringSplitOptions.TrimEntries);
        if (seedParts.Length != 4
            || seedParts.Any(part => !uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException("The --depth-seeds value must contain four unsigned 32-bit integers.");
        var seeds = seedParts
            .Select(part => uint.Parse(part, NumberStyles.None, CultureInfo.InvariantCulture))
            .ToArray();
        var placementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic;
        if (values.TryGetValue("--placement-policy", out var policyText))
            placementPolicy = policyText switch {
                "none" => CathedralDepthPlaytestPlacementPolicy.None,
                "authored-diagnostic" => CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                _ => throw new ArgumentException("The placement policy must be 'none' or 'authored-diagnostic'."),
            };

        var basePack = ContentPackLoader.LoadManifestDirectory(baseDirectory);
        var geometry = ContentPackLoader.LoadDirectory("explicit-cathedral-geometry", "1", geometryDirectory);
        var pieces = geometry.Tables.SingleOrDefault(table =>
                table.SourcePath == AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath)
            ?? throw new InvalidDataException("Missing explicit native_dun_piece_definitions.tsv.");
        var properties = geometry.Tables.SingleOrDefault(table =>
                table.SourcePath == AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath)
            ?? throw new InvalidDataException("Missing explicit native_dun_tile_properties.tsv.");
        var profile = CathedralDepthPlaytestCompiler.Compile(
            new CathedralDepthPlaytestCompilerOptions(
                basePack,
                outputDirectory,
                pieces.ToCanonicalTsv(),
                properties.ToCanonicalTsv(),
                seeds) {
                PlacementPolicy = placementPolicy,
            });
        Console.WriteLine($"Compiled Cathedral depths 1-4 profile: {Path.GetFullPath(outputDirectory)}");
        Console.WriteLine($"Content manifest: {profile.ContentManifestHash}");
        Console.WriteLine("Native geometry; authored diagnostics are opt-in and make no native population claim.");
        return 0;
    }
}
