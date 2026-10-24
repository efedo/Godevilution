namespace Godevilution.Tools.Setup;

/**
 * Native.Setup ensure --repository-root DIR --setup-root DIR [--archive DIABDAT.MPQ]
 * Exit 0: current.json describes a ready build. Exit 3: no archive was found.
 */
internal static class Program
{
    public const int NoArchiveExitCode = 3;
    private const string Usage = "Native.Setup ensure --repository-root <repository> --setup-root <directory> [--archive <DIABDAT.MPQ>]";

    private static int Main(string[] args)
    {
        try {
            if (args.Length == 0 || args[0] != "ensure")
                throw new ArgumentException(Usage);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 1; index < args.Length; index += 2) {
                if (index + 1 >= args.Length || args[index] is not ("--repository-root" or "--setup-root" or "--archive")
                    || string.IsNullOrWhiteSpace(args[index + 1]) || !values.TryAdd(args[index], args[index + 1]))
                    throw new ArgumentException(Usage);
            }
            if (!values.TryGetValue("--repository-root", out var repositoryRoot) || !values.TryGetValue("--setup-root", out var setupRoot))
                throw new ArgumentException(Usage);
            repositoryRoot = Path.GetFullPath(repositoryRoot);
            setupRoot = Path.GetFullPath(setupRoot);
            values.TryGetValue("--archive", out var explicitArchive);

            var archive = NativeArchiveDiscovery.Find(NativeArchiveDiscovery.CurrentEnvironment(explicitArchive, setupRoot));
            if (archive is null) {
                Console.WriteLine("No Diablo DIABDAT.MPQ was found (checked the remembered path, GODEVILUTION_DIABDAT,");
                Console.WriteLine("the DevilutionX data folder, the GOG install and the usual install folders).");
                Console.WriteLine("Supply it once with: ./run-godot-client.ps1 -DiabloArchive <path to DIABDAT.MPQ>");
                return NoArchiveExitCode;
            }
            if (archive.Origin == NativeArchiveOrigin.Explicit) {
                Directory.CreateDirectory(setupRoot);
                File.WriteAllText(Path.Combine(setupRoot, NativeArchiveDiscovery.ConfiguredPathFileName), archive.Path);
            }

            var fingerprint = NativeSetupFingerprint.Compute(repositoryRoot, archive.Path);
            if (NativeSetupBuilder.TryReuse(setupRoot, repositoryRoot, fingerprint) is not null) {
                Console.WriteLine($"Native setup is current ({fingerprint[..16]}); archive: {archive.Path}");
                return 0;
            }
            Console.WriteLine($"Building native setup {fingerprint[..16]} from {archive.Path} ({archive.Origin}). This runs once per input change.");
            NativeSetupBuilder.Build(setupRoot, repositoryRoot, archive.Path, fingerprint, Console.Out);
            Console.WriteLine($"Native setup ready: {Path.Combine(setupRoot, NativeSetupBuilder.StateFileName)}");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException
            or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) {
            Console.Error.WriteLine($"Native setup failed: {exception.Message}");
            return 1;
        }
    }
}
