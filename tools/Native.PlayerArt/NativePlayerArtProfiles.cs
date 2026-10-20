using System.Text.Json;
using System.Text.RegularExpressions;
using Devilution.Data;

namespace Devilution.Tools.NativePlayerArt;

public readonly record struct NativePlayerArtLevelBinding(string ContentManifestHash, uint LevelId, string Location);

public sealed record NativePlayerArtProfileSet(
    IReadOnlyList<string> ContentManifestHashes,
    IReadOnlyList<NativePlayerArtLevelBinding> LevelBindings);

/** Resolves only compiler-produced native Cathedral profiles and derives their content identities. */
public static class NativePlayerArtProfiles
{
    public const string ClassProgressionAdapterId = "devilution.diablo.cathedral-native-class-progression.v1";
    public const string DepthProgressionAdapterId = "devilution.diablo.cathedral-native-depth-progression.v1";
    public const int MaximumProfileBytes = 1024 * 1024;

    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const string ClassProfileFileName = "cathedral-native-class-progression-playtest.json";
    private const string DepthProfileFileName = "cathedral-native-depth-progression-playtest.json";

    public static NativePlayerArtProfileSet LoadDirectories(IReadOnlyList<string>? profileDirectories)
    {
        if (profileDirectories is null || profileDirectories.Count is < 1 or > 64)
            throw new InvalidDataException("One to 64 compiled native Cathedral profile directories are required.");

        var hashes = new SortedSet<string>(StringComparer.Ordinal);
        var bindings = new Dictionary<(string Hash, uint LevelId), string>();
        foreach (var profileDirectory in profileDirectories) {
            if (string.IsNullOrWhiteSpace(profileDirectory))
                throw new InvalidDataException("Native Cathedral profile directories must be nonempty.");
            var root = Path.GetFullPath(profileDirectory);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Native Cathedral profile directory does not exist: {root}");
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Native Cathedral profile directory '{root}' cannot be a reparse point.");
            var classPath = Path.Combine(root, ClassProfileFileName);
            var depthPath = Path.Combine(root, DepthProfileFileName);
            var hasClass = File.Exists(classPath);
            var hasDepth = File.Exists(depthPath);
            if (hasClass == hasDepth)
                throw new InvalidDataException($"Profile directory '{root}' must contain exactly one supported native Cathedral profile metadata file.");

            var profilePath = hasClass ? classPath : depthPath;
            var expectedAdapter = hasClass ? ClassProgressionAdapterId : DepthProgressionAdapterId;
            var expectedFormat = hasClass ? 4 : 3;
            using var document = ReadProfile(profilePath);
            if (RequiredInt32(document, "formatVersion") != expectedFormat
                || !string.Equals(RequiredString(document, "adapterId"), expectedAdapter, StringComparison.Ordinal))
                throw new InvalidDataException($"Native Cathedral profile '{profilePath}' has an unsupported format or adapter.");

            var classSymbol = RequiredString(document, "heroClassSymbol");
            if (hasClass && classSymbol is not "warrior" and not "sorcerer")
                throw new InvalidDataException($"Class-progression profile '{profilePath}' must bind Warrior or Sorcerer.");
            if (!hasClass && classSymbol != "rogue")
                throw new InvalidDataException($"Depth-progression profile '{profilePath}' must retain the legacy Rogue class.");
            if (RequiredInt32(document, "nativeDepth") != 4)
                throw new InvalidDataException($"Native Cathedral profile '{profilePath}' must qualify exactly four depths.");

            var declaredHash = RequiredString(document, "contentManifestHash");
            if (!Sha256Pattern.IsMatch(declaredHash))
                throw new InvalidDataException($"Native Cathedral profile '{profilePath}' has an invalid content manifest hash.");

            var contentDirectory = ResolveProfileDirectory(root, RequiredString(document, "contentDirectory"), profilePath);
            var populationDirectory = ResolveProfileDirectory(root, RequiredString(document, "populationDirectory"), profilePath);
            var runtimeDirectory = ResolveProfileDirectory(root, RequiredString(document, "runtimeDirectory"), profilePath);
            var contentPack = ContentPackLoader.LoadManifestDirectory(contentDirectory);
            var populationPack = ContentPackLoader.LoadManifestDirectory(populationDirectory);
            var runtimePack = ContentPackLoader.LoadManifestDirectory(runtimeDirectory);
            RequireProfilePackIdentity(document, "contentId", "contentVersion", contentPack, profilePath);
            RequireProfilePackIdentity(document, "populationPackId", "populationPackVersion", populationPack, profilePath);
            RequireProfilePackIdentity(document, "runtimePackId", "runtimePackVersion", runtimePack, profilePath);
            ValidateRuntimeClassBinding(runtimePack, classSymbol, hasClass, profilePath);

            var manifest = new ContentManifest(contentPack.Id, contentPack.Version, [contentPack, populationPack, runtimePack]);
            if (!string.Equals(manifest.Sha256, declaredHash, StringComparison.Ordinal))
                throw new InvalidDataException($"Native Cathedral profile '{profilePath}' content manifest hash does not match its compiled packs.");
            hashes.Add(declaredHash);
            foreach (var binding in ReadLevelBindings(contentPack, declaredHash, profilePath)) {
                var key = (binding.ContentManifestHash, binding.LevelId);
                if (bindings.TryGetValue(key, out var existingLocation) && existingLocation != binding.Location)
                    throw new InvalidDataException($"Native Cathedral profiles disagree on level {binding.LevelId} for content hash {binding.ContentManifestHash}.");
                bindings[key] = binding.Location;
            }
        }

        if (bindings.Count is < 1 or > 4096)
            throw new InvalidDataException("Qualified native Cathedral profiles must produce one to 4096 unique level bindings.");
        var orderedBindings = bindings
            .OrderBy(entry => entry.Key.Hash, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.LevelId)
            .Select(entry => new NativePlayerArtLevelBinding(entry.Key.Hash, entry.Key.LevelId, entry.Value))
            .ToArray();
        return new NativePlayerArtProfileSet(hashes.ToArray(), orderedBindings);
    }

    private static JsonDocument ReadProfile(string path)
    {
        var file = new FileInfo(path);
        if (file.Length is < 1 or > MaximumProfileBytes)
            throw new InvalidDataException($"Native Cathedral profile metadata '{path}' is outside the supported size bounds.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Native Cathedral profile metadata '{path}' cannot be a reparse point.");
        try {
            return JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 32 });
        } catch (JsonException exception) {
            throw new InvalidDataException($"Native Cathedral profile metadata '{path}' is not valid JSON.", exception);
        }
    }

    private static string ResolveProfileDirectory(string root, string relativePath, string profilePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' contains an unsafe pack directory.");
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !Directory.Exists(fullPath))
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' references a missing or escaping pack directory.");
        return fullPath;
    }

    private static void RequireProfilePackIdentity(JsonDocument document, string idName, string versionName, ContentPack pack, string profilePath)
    {
        if (!string.Equals(RequiredString(document, idName), pack.Id, StringComparison.Ordinal)
            || !string.Equals(RequiredString(document, versionName), pack.Version, StringComparison.Ordinal))
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' does not match its {idName} pack identity.");
    }

    private static IReadOnlyList<NativePlayerArtLevelBinding> ReadLevelBindings(ContentPack pack, string hash, string profilePath)
    {
        var matches = pack.Tables.Where(table => table.SourcePath == "levels.tsv").ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' must contain exactly one levels.tsv table.");
        var table = matches[0];
        if (!table.Columns.Contains("level_id", StringComparer.Ordinal)
            || !table.Columns.Contains("level_symbol", StringComparer.Ordinal))
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' levels.tsv must define level_id and level_symbol.");
        var result = new List<NativePlayerArtLevelBinding>(5);
        var seen = new HashSet<uint>();
        foreach (var row in table.Rows) {
            var levelId = row.RequiredUInt32("level_id");
            var symbol = row.Required("level_symbol");
            if (levelId > 4)
                continue;
            var expectedSymbol = levelId switch {
                0 => "town",
                1 => "cathedral",
                _ => $"cathedral_depth_{levelId}",
            };
            if (!string.Equals(symbol, expectedSymbol, StringComparison.Ordinal))
                throw new InvalidDataException($"Native Cathedral profile '{profilePath}' level {levelId} is '{symbol}', expected '{expectedSymbol}'.");
            if (!seen.Add(levelId))
                throw new InvalidDataException($"Native Cathedral profile '{profilePath}' duplicates level {levelId}.");
            result.Add(new NativePlayerArtLevelBinding(hash, levelId, levelId == 0 ? "town" : "cathedral"));
        }
        if (seen.Count != 5)
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' must explicitly contain levels 0 through 4.");
        return result;
    }

    private static void ValidateRuntimeClassBinding(ContentPack runtimePack, string classSymbol, bool classBound, string profilePath)
    {
        const string tableName = "native_cathedral_runtime/native-character-start.tsv";
        string[] columns = [
            "class_id",
            "class_symbol",
            "hero_base_life_maximum",
            "hero_base_mana_maximum",
            "hero_light_radius",
            "hero_idle_frames",
        ];
        var matches = runtimePack.Tables.Where(table => table.SourcePath == tableName).ToArray();
        if (!classBound) {
            if (matches.Length != 0)
                throw new InvalidDataException($"Legacy Rogue profile '{profilePath}' must retain the table-absent compatibility identity.");
            return;
        }
        if (matches.Length != 1
            || !matches[0].Columns.SequenceEqual(columns, StringComparer.Ordinal)
            || matches[0].Rows.Count != 1)
            throw new InvalidDataException($"Class-bound profile '{profilePath}' must contain one canonical {tableName} row.");
        var row = matches[0].Rows[0];
        var expectedClassId = classSymbol switch {
            "warrior" => 0U,
            "sorcerer" => 2U,
            _ => throw new InvalidDataException($"Class-bound profile '{profilePath}' has an unsupported class symbol."),
        };
        if (row.RequiredUInt32("class_id") != expectedClassId
            || !string.Equals(row.Required("class_symbol"), classSymbol, StringComparison.Ordinal))
            throw new InvalidDataException($"Class-bound profile '{profilePath}' metadata does not match its canonical native character-start row.");
    }

    private static JsonElement RequiredProperty(JsonDocument document, string name)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(name, out var value))
            throw new InvalidDataException($"Native Cathedral profile metadata is missing '{name}'.");
        return value;
    }

    private static int RequiredInt32(JsonDocument document, string name)
    {
        if (!RequiredProperty(document, name).TryGetInt32(out var value))
            throw new InvalidDataException($"Native Cathedral profile field '{name}' must be an integer.");
        return value;
    }

    private static string RequiredString(JsonDocument document, string name)
    {
        var value = RequiredProperty(document, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Native Cathedral profile field '{name}' must be a nonempty string.");
        return value.GetString()!;
    }
}
