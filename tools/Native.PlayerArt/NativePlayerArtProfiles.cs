using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Devilution.Data;
using Devilution.Diablo;

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
    public const string TownProgressionAdapterId = "devilution.diablo.cathedral-native-town-progression.v1";
    public const int MaximumProfileBytes = 1024 * 1024;

    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HexSha256Pattern = new("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const string ClassProfileFileName = "cathedral-native-class-progression-playtest.json";
    private const string DepthProfileFileName = "cathedral-native-depth-progression-playtest.json";
    private const string TownProfileFileName = "cathedral-native-town-progression-playtest.json";

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
            var townPath = Path.Combine(root, TownProfileFileName);
            var hasClass = File.Exists(classPath);
            var hasDepth = File.Exists(depthPath);
            var hasTown = File.Exists(townPath);
            if (hasTown) {
                if (!hasClass || hasDepth)
                    throw new InvalidDataException($"Town profile directory '{root}' must retain exactly one format-4 class profile metadata file.");
                using var townDocument = ReadProfile(townPath);
                var nestedClassProfile = RequiredProperty(townDocument.RootElement, "classProfile");
                if (nestedClassProfile.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException($"Town profile '{townPath}' must embed its unchanged format-4 class profile.");
                using var classDocument = JsonDocument.Parse(nestedClassProfile.GetRawText(), new JsonDocumentOptions { MaxDepth = 32 });
                using var retainedClassDocument = ReadProfile(classPath);
                if (!JsonEquivalent(classDocument.RootElement, retainedClassDocument.RootElement))
                    throw new InvalidDataException($"Town profile '{townPath}' does not match the retained format-4 class profile.");
                var baseProfile = ValidateBaseProfile(root, classDocument, classPath, classBound: true);
                var townProfile = ValidateTownProfile(root, townDocument, townPath, baseProfile);
                hashes.Add(townProfile.ContentManifestHash);
                foreach (var binding in ReadLevelBindings(baseProfile.ContentPack,
                    townProfile.ContentManifestHash, townPath)) {
                    var key = (binding.ContentManifestHash, binding.LevelId);
                    if (bindings.TryGetValue(key, out var existingLocation) && existingLocation != binding.Location)
                        throw new InvalidDataException($"Native Cathedral profiles disagree on level {binding.LevelId} for content hash {binding.ContentManifestHash}.");
                    bindings[key] = binding.Location;
                }
                continue;
            }
            if (hasClass == hasDepth)
                throw new InvalidDataException($"Profile directory '{root}' must contain exactly one supported native Cathedral profile metadata file.");

            var profilePath = hasClass ? classPath : depthPath;
            using var document = ReadProfile(profilePath);
            var verified = ValidateBaseProfile(root, document, profilePath, classBound: hasClass);
            hashes.Add(verified.ContentManifestHash);
            foreach (var binding in ReadLevelBindings(verified.ContentPack, verified.ContentManifestHash, profilePath)) {
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

    private sealed record VerifiedBaseProfile(
        string ContentManifestHash,
        ContentPack ContentPack,
        ContentPack PopulationPack,
        ContentPack RuntimePack);

    private sealed record VerifiedTownProfile(string ContentManifestHash);

    private static VerifiedBaseProfile ValidateBaseProfile(
        string root,
        JsonDocument document,
        string profilePath,
        bool classBound)
    {
        var expectedAdapter = classBound ? ClassProgressionAdapterId : DepthProgressionAdapterId;
        var expectedFormat = classBound ? 4 : 3;
        if (RequiredInt32(document, "formatVersion") != expectedFormat
            || !string.Equals(RequiredString(document, "adapterId"), expectedAdapter, StringComparison.Ordinal))
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' has an unsupported format or adapter.");

        var classSymbol = RequiredString(document, "heroClassSymbol");
        if (classBound && classSymbol is not "warrior" and not "sorcerer")
            throw new InvalidDataException($"Class-progression profile '{profilePath}' must bind Warrior or Sorcerer.");
        if (!classBound && classSymbol != "rogue")
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
        ValidateRuntimeClassBinding(runtimePack, classSymbol, classBound, profilePath);

        var manifest = new ContentManifest(contentPack.Id, contentPack.Version, [contentPack, populationPack, runtimePack]);
        if (!string.Equals(manifest.Sha256, declaredHash, StringComparison.Ordinal))
            throw new InvalidDataException($"Native Cathedral profile '{profilePath}' content manifest hash does not match its compiled packs.");
        return new VerifiedBaseProfile(declaredHash, contentPack, populationPack, runtimePack);
    }

    private static VerifiedTownProfile ValidateTownProfile(
        string root,
        JsonDocument townDocument,
        string profilePath,
        VerifiedBaseProfile baseProfile)
    {
        if (RequiredInt32(townDocument, "formatVersion") != 5
            || !string.Equals(RequiredString(townDocument, "adapterId"), TownProgressionAdapterId, StringComparison.Ordinal))
            throw new InvalidDataException($"Town profile '{profilePath}' has an unsupported format or adapter.");

        var townDirectory = ResolveProfileDirectory(root, RequiredString(townDocument, "townDirectory"), profilePath);
        var townPack = ContentPackLoader.LoadManifestDirectory(townDirectory);
        if (!string.Equals(townPack.Id, RequiredString(townDocument, "townPackId"), StringComparison.Ordinal)
            || !string.Equals(townPack.Version, RequiredString(townDocument, "townPackVersion"), StringComparison.Ordinal))
            throw new InvalidDataException($"Town profile '{profilePath}' does not match its semantic Town pack identity.");
        if (!string.Equals(townPack.Id, "godevilution.cathedral-native-town-geometry", StringComparison.Ordinal)
            || !string.Equals(townPack.Version, "1.0.0", StringComparison.Ordinal))
            throw new InvalidDataException($"Town profile '{profilePath}' references an unsupported semantic Town pack.");

        var expectedTreeHash = RequiredString(townDocument, "townPackTreeHash");
        if (!HexSha256Pattern.IsMatch(expectedTreeHash)
            || !string.Equals(ComputePackTreeHash(townDirectory), expectedTreeHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Town profile '{profilePath}' Town pack tree hash does not match its files.");
        var catalogFingerprint = RequiredString(townDocument, "townCatalogFingerprint");
        if (!HexSha256Pattern.IsMatch(catalogFingerprint))
            throw new InvalidDataException($"Town profile '{profilePath}' has an invalid Town catalog fingerprint.");
        var townSeedElement = RequiredProperty(townDocument, "townInitialSeed");
        if (!townSeedElement.TryGetUInt32(out var townSeed))
            throw new InvalidDataException($"Town profile '{profilePath}' initial seed must be an unsigned 32-bit integer.");

        var settings = townPack.Tables.SingleOrDefault(table => table.SourcePath == "native_town_geometry/settings.tsv")
            ?? throw new InvalidDataException($"Town profile '{profilePath}' has no bound native Town settings table.");
        if (!settings.Columns.SequenceEqual([
                "is_spawn", "is_multiplayer", "is_hellfire", "character_level", "warp_unlocks",
                "farmer_quest_state", "grave_quest_state", "entry", "town_warp_from",
                "water_quest_state", "initial_random_state",
            ], StringComparer.Ordinal)
            || settings.Rows.Count != 1
            || settings.Rows[0].RequiredUInt32("initial_random_state") != townSeed)
            throw new InvalidDataException($"Town profile '{profilePath}' seed does not match its bound Town settings.");

        var manifest = new ContentManifest(baseProfile.ContentPack.Id, baseProfile.ContentPack.Version,
            [baseProfile.ContentPack, baseProfile.PopulationPack, baseProfile.RuntimePack, townPack]);
        var declaredHash = RequiredString(townDocument, "contentManifestHash");
        if (!Sha256Pattern.IsMatch(declaredHash)
            || !string.Equals(manifest.Sha256, declaredHash, StringComparison.Ordinal))
            throw new InvalidDataException($"Town profile '{profilePath}' content manifest hash does not match its four compiled packs.");
        var catalog = AuthoritativeNativeTownCatalog.Load(ComposedContent.Compose(manifest));
        if (!string.Equals(catalog.Fingerprint, catalogFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException($"Town profile '{profilePath}' catalog fingerprint does not match its compiled Town semantics.");
        return new VerifiedTownProfile(declaredHash);
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
        RejectReparseTree(fullPath, profilePath);
        return fullPath;
    }

    private static void RejectReparseTree(string root, string profilePath)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory)) {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory)) {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Native Cathedral profile '{profilePath}' pack contains a reparse point.");
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(path);
            }
        }
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
        => RequiredProperty(document.RootElement, name);

    private static JsonElement RequiredProperty(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(name, out var value))
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
        => RequiredString(document.RootElement, name);

    private static string RequiredString(JsonElement root, string name)
    {
        var value = RequiredProperty(root, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Native Cathedral profile field '{name}' must be a nonempty string.");
        return value.GetString()!;
    }

    private static string ComputePackTreeHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(path => new { FullPath = path, Relative = Path.GetRelativePath(root, path).Replace('\\', '/') })
                     .OrderBy(value => value.Relative, StringComparer.Ordinal)) {
            if ((File.GetAttributes(file.FullPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Town semantic pack cannot contain reparse-point files.");
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.FullPath)));
            hash.AppendData(Encoding.UTF8.GetBytes($"{file.Relative.Length}:{file.Relative}:{fileHash}\n"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
            return false;
        switch (left.ValueKind) {
        case JsonValueKind.Object: {
            var leftProperties = left.EnumerateObject().ToArray();
            var rightProperties = right.EnumerateObject().ToArray();
            if (leftProperties.Length != rightProperties.Length)
                return false;
            var leftByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in leftProperties)
                if (!leftByName.TryAdd(property.Name, property.Value))
                    return false;
            foreach (var property in rightProperties) {
                if (!leftByName.TryGetValue(property.Name, out var value)
                    || !JsonEquivalent(value, property.Value))
                    return false;
            }
            return true;
        }
        case JsonValueKind.Array: {
            var leftValues = left.EnumerateArray().ToArray();
            var rightValues = right.EnumerateArray().ToArray();
            return leftValues.Length == rightValues.Length
                && leftValues.Zip(rightValues).All(pair => JsonEquivalent(pair.First, pair.Second));
        }
        case JsonValueKind.String:
            return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
        case JsonValueKind.Number:
            return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
        case JsonValueKind.True:
        case JsonValueKind.False:
        case JsonValueKind.Null:
            return true;
        default:
            return false;
        }
    }
}
