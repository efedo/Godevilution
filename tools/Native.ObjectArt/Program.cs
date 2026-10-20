namespace Devilution.Tools.NativeObjectArt;

internal static class Program
{
    private static int Main(string[] args)
    {
        try {
            if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal)) {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            string? sourceRoot = null;
            string? textDataRoot = null;
            string? outputRoot = null;
            var profiles = new List<string>();
            for (var index = 0; index < args.Length; index++) {
                var option = args[index];
                if (index + 1 >= args.Length)
                    throw new ArgumentException($"Option '{option}' requires a value.");
                var value = args[++index];
                switch (option) {
                case "--source-root":
                    sourceRoot = SetOnce(sourceRoot, value, option);
                    break;
                case "--text-data-root":
                    textDataRoot = SetOnce(textDataRoot, value, option);
                    break;
                case "--profile-directory":
                    profiles.Add(value);
                    break;
                case "--output-root":
                    outputRoot = SetOnce(outputRoot, value, option);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
                }
            }

            var result = NativeObjectArtCompiler.Import(new NativeObjectImportOptions(
                sourceRoot ?? throw new ArgumentException("--source-root is required."),
                textDataRoot ?? throw new ArgumentException("--text-data-root is required."),
                profiles,
                outputRoot ?? throw new ArgumentException("--output-root is required.")));
            Console.WriteLine($"Published {result.SpriteCount} native Cathedral object sprites and {result.ObjectCount} object definitions.");
            Console.WriteLine($"Pack: {result.PackId}");
            Console.WriteLine($"Output: {result.OutputDirectory}");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string SetOnce(string? current, string value, string option)
    {
        if (current is not null)
            throw new ArgumentException($"Option '{option}' may be supplied only once.");
        return value;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Native Cathedral object art importer");
        Console.WriteLine("  --source-root <extracted-original-files>");
        Console.WriteLine("  --text-data-root <repository-assets/txtdata>");
        Console.WriteLine("  --profile-directory <verified-native-Cathedral-profile> (repeatable)");
        Console.WriteLine("  --output-root <new-directory>");
    }
}
