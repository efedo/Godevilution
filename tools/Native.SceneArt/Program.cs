using Devilution.Tools.NativeSceneArt;

return NativeSceneArtProgram.Run(args);

internal static class NativeSceneArtProgram
{
    public static int Run(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h") {
            PrintUsage(Console.Out);
            return 0;
        }

        try {
            var values = ParseArguments(args);
            var result = NativeSceneArtCompiler.Import(new NativeSceneArtImportOptions(
                Required(values, "--source-root"),
                Required(values, "--output-root"),
                Required(values, "--town-palette"),
                Required(values, "--cathedral-palette"),
                values.TryGetValue("--profile-directory", out var profiles) ? profiles : []));
            Console.WriteLine($"Published native scene pack '{result.PackId}' with {result.PieceCount} d-pieces, {result.FrameCount} frames, and {result.SpecialCount} specials to {result.OutputDirectory}.");
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
            if (flag is not "--source-root" and not "--output-root" and not "--town-palette"
                and not "--cathedral-palette" and not "--profile-directory")
                throw new ArgumentException($"Unknown option '{flag}'.");
            if (++index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '{flag}' requires a value.");
            var allowMultiple = flag == "--profile-directory";
            if (!result.TryGetValue(flag, out var entries)) {
                entries = [];
                result.Add(flag, entries);
            } else if (!allowMultiple) {
                throw new ArgumentException($"Option '{flag}' may be specified only once.");
            }
            entries.Add(args[index]);
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, List<string>> values, string key)
        => values.TryGetValue(key, out var entries) && entries.Count == 1
            ? entries[0]
            : throw new ArgumentException($"Required option '{key}' is missing.");

    private static int ReportFailure(Exception exception)
    {
        Console.Error.WriteLine($"Native scene art import failed: {exception.Message}");
        return 1;
    }

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine("Usage: Native.SceneArt --source-root EXTRACTED_GAME_ROOT --output-root NEW_DIRECTORY");
        output.WriteLine("       --town-palette RELATIVE_PAL --cathedral-palette RELATIVE_PAL");
        output.WriteLine("       --profile-directory COMPILED_PROFILE_DIR [--profile-directory COMPILED_PROFILE_DIR ...]");
        output.WriteLine("Only explicitly extracted source assets and validated compiled native Cathedral profiles are accepted.");
        output.WriteLine("The source root must contain Town and L1 CEL/MIN/TIL/SOL/special files and the selected palettes.");
        output.WriteLine("The output directory must not already exist. Malformed inputs abort publication.");
    }
}
