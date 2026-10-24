using System.Security.Cryptography;
using System.Text;

namespace Godevilution.Tools.Setup;

/**
 * Identifies one native setup build. It changes when the archive, an open asset
 * the build reads, the base content, or any tool or game-module source (and so
 * any profile schema) changes. The archive is identified by path, size and time
 * so an unchanged launch does not hash hundreds of megabytes.
 */
public static class NativeSetupFingerprint
{
    public const int SchemaVersion = 2;

    /** Repository-relative inputs; directories are hashed recursively without bin/obj. */
    public static IReadOnlyList<string> InputPaths { get; } = [
        "assets/txtdata",
        "assets/fonts",
        "assets/data/inv/objcurs-widths.txt",
        "assets/levels/towndata/automap.dun",
        "server/content/base",
        "server/src/Devilution.Data",
        "server/src/Devilution.Domain",
        "server/src/Devilution.Engine",
        "server/src/Devilution.Diablo",
        "tools/Cathedral.Playtest",
        "tools/Native.Archive",
        "tools/Native.PlayerArt",
        "tools/Native.SceneArt",
        "tools/Native.TownerArt",
        "tools/Native.ObjectArt",
        "tools/Native.Setup",
    ];

    private static readonly string[] ExcludedSegments = ["bin", "obj", ".vs"];

    public static string Compute(string repositoryRoot, string archivePath)
    {
        var archive = new FileInfo(archivePath);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, $"schema\t{SchemaVersion}");
        Append(hash, $"archive\t{archive.FullName}\t{archive.Length}\t{archive.LastWriteTimeUtc.Ticks}");
        foreach (var (relative, content) in InputFiles(repositoryRoot)) {
            Append(hash, $"file\t{relative}\t{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(content)))}");
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /** Repository-relative '/' names with their full paths, in ordinal order. */
    public static IEnumerable<(string Relative, string FullPath)> InputFiles(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var files = new List<(string, string)>();
        foreach (var input in InputPaths) {
            var path = Path.Combine(root, input);
            if (File.Exists(path)) {
                files.Add((input, path));
            } else if (Directory.Exists(path)) {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) {
                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (!relative.Split('/').Any(segment => ExcludedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
                        files.Add((relative, file));
                }
            } else {
                throw new FileNotFoundException($"Native setup input '{input}' is missing.");
            }
        }
        return files.OrderBy(file => file.Item1, StringComparer.Ordinal);
    }

    private static void Append(IncrementalHash hash, string line) => hash.AppendData(Encoding.UTF8.GetBytes(line + "\n"));
}
