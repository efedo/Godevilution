using Devilution.Tools.NativePlayerArt;

return NativePlayerArtProgram.Run(args);

internal static class NativePlayerArtProgram
{
    public static int Run(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h") {
            PrintUsage(Console.Out);
            return 0;
        }

        try {
            var values = ParseArguments(args);
            var profiles = NativePlayerArtProfiles.LoadDirectories(
                values.TryGetValue("--profile-directory", out var profileDirectories) ? profileDirectories : []);
            var options = new NativePlayerArtImportOptions(
                Required(values, "--source-root"),
                Required(values, "--metadata-root"),
                Required(values, "--game-content-directory"),
                Required(values, "--output-root"),
                Required(values, "--town-palette"),
                Required(values, "--cathedral-palette"),
                profiles.ContentManifestHashes,
                profiles.LevelBindings);
            var result = NativePlayerArtCompiler.Import(options);
            Console.WriteLine($"Published native player pack '{result.PackId}' with {result.ClipCount} clips to {result.OutputDirectory}.");
            if (result.MissingAssets.Count != 0) {
                Console.WriteLine($"Missing native player assets: {result.MissingAssets.Count}.");
                foreach (var missing in result.MissingAssets)
                    Console.WriteLine($"missing: {missing}");
            }
            foreach (var diagnostic in result.Diagnostics)
                Console.WriteLine($"diagnostic: {diagnostic}");
            return 0;
        } catch (InvalidDataException exception) {
            return ReportFailure(exception);
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            return ReportFailure(exception);
        }
    }

    private static Dictionary<string, List<string>> ParseArguments(IReadOnlyList<string> args)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index++) {
            var flag = args[index];
            if (flag is not "--source-root" and not "--metadata-root" and not "--game-content-directory"
                and not "--output-root" and not "--town-palette" and not "--cathedral-palette"
                and not "--profile-directory")
                throw new ArgumentException($"Unknown option '{flag}'.");
            if (++index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '{flag}' requires a value.");
            var allowMultiple = flag == "--profile-directory";
            if (!result.TryGetValue(flag, out var values)) {
                values = [];
                result.Add(flag, values);
            } else if (!allowMultiple) {
                throw new ArgumentException($"Option '{flag}' may be specified only once.");
            }
            values.Add(args[index]);
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, List<string>> values, string key)
        => values.TryGetValue(key, out var entries) && entries.Count == 1
            ? entries[0]
            : throw new ArgumentException($"Required option '{key}' is missing.");

    private static int ReportFailure(Exception exception)
    {
        Console.Error.WriteLine($"Native player art import failed: {exception.Message}");
        return 1;
    }

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine("Usage: Native.PlayerArt --source-root DIR --metadata-root DIR --game-content-directory DIR");
        output.WriteLine("       --output-root NEW_DIR --town-palette RELATIVE_PAL --cathedral-palette RELATIVE_PAL");
        output.WriteLine("       --profile-directory COMPILED_PROFILE_DIR [--profile-directory COMPILED_PROFILE_DIR ...]");
        output.WriteLine("Only validated native Cathedral class-progression (Warrior/Sorcerer) and depth-progression (Rogue) profiles are admitted.");
        output.WriteLine("The source root must contain explicitly extracted plrgfx files and the two selected palettes.");
        output.WriteLine("The output root must not already exist. Missing CL2 variants are recorded; malformed inputs abort publication.");
    }
}
