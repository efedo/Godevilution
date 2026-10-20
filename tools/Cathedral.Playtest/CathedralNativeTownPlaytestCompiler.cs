using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Tools.NativePlayerArt;

namespace Godevilution.Tools;

/** Inputs for an isolated format-5 native Town profile built on a qualified class-progression profile. */
public sealed record CathedralNativeTownPlaytestCompilerOptions(
    string ClassProfileDirectory,
    string TownSourceDirectory,
    string RosterTablePath,
    string OutputDirectory,
    uint TownSeed);

/** Closed outer profile. The nested format-4 class profile remains the original identity and is copied unchanged. */
public sealed record CathedralNativeTownPlaytestProfile(
    int FormatVersion,
    string AdapterId,
    JsonElement ClassProfile,
    string TownDirectory,
    string TownPackId,
    string TownPackVersion,
    string TownPackTreeHash,
    string TownCatalogFingerprint,
    uint TownInitialSeed,
    string ContentManifestHash);

/** Compiles private original Town geometry and roster files into an additive semantic pack. */
public static class CathedralNativeTownPlaytestCompiler
{
    public const string AdapterId = "devilution.diablo.cathedral-native-town-progression.v1";
    public const string ProfileFileName = "cathedral-native-town-progression-playtest.json";
    public const string TownDirectoryName = "native_town_geometry";
    public const string TownPackId = "godevilution.cathedral-native-town-geometry";
    public const string TownPackVersion = "1.0.0";
    public const int FormatVersion = 5;
    public const int MaximumRosterBytes = 1024 * 1024;
    public const long MaximumInputPackBytes = 256L * 1024 * 1024;
    public const int MaximumDunBytes = 128 * 1024;
    public const int MaximumTilBytes = 512 * 1024;
    public const int MaximumSolBytes = AuthoritativeDunTilePropertyCatalog.NativeTileCount;

    private const string ClassProfileFileName = "cathedral-native-class-progression-playtest.json";
    private const string TownIdentityPath = TownDirectoryName + "/identity.tsv";
    private const string TownMapsPath = TownDirectoryName + "/maps.tsv";
    private const string TownMapTilesPath = TownDirectoryName + "/map_tiles.tsv";
    private const string TownPieceDefinitionsPath = TownDirectoryName + "/piece_definitions.tsv";
    private const string TownTilePropertiesPath = TownDirectoryName + "/tile_properties.tsv";
    private const string TownRosterPath = TownDirectoryName + "/roster.tsv";
    private const string TownSettingsPath = TownDirectoryName + "/settings.tsv";

    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly (string MapId, string FileName, int Width, int Height)[] Maps = [
        ("sector1_south", "sector1s.dun", 25, 25),
        ("sector2_south", "sector2s.dun", 25, 23),
        ("sector3_south", "sector3s.dun", 23, 25),
        ("sector4_south", "sector4s.dun", 23, 23),
        ("automap", "automap.dun", 40, 40),
    ];

    private sealed record NativeTownCoarseMap(int Width, int Height, IReadOnlyList<ushort> TileLayer);

    private static readonly (string SourceType, uint Id, string Symbol, bool Publish)[] TownerTypes = [
        ("TOWN_SMITH", 0, "smith", true),
        ("TOWN_HEALER", 1, "healer", true),
        ("TOWN_DEADGUY", 2, "deadguy", false),
        ("TOWN_TAVERN", 3, "tavern", true),
        ("TOWN_STORY", 4, "story", true),
        ("TOWN_DRUNK", 5, "drunk", true),
        ("TOWN_WITCH", 6, "witch", true),
        ("TOWN_BMAID", 7, "barmaid", true),
        ("TOWN_PEGBOY", 8, "pegboy", true),
        ("TOWN_COW", 9, "cow", true),
        ("TOWN_FARMER", 10, "farmer", false),
        ("TOWN_GIRL", 11, "girl", false),
        ("TOWN_COWFARM", 12, "cowfarmer", false),
    ];

    private static readonly IReadOnlyDictionary<string, string> Directions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["SouthWest"] = "south_west",
            ["South"] = "south",
            ["SouthEast"] = "south_east",
            ["East"] = "east",
            ["NorthEast"] = "north_east",
            ["North"] = "north",
            ["NorthWest"] = "north_west",
            ["West"] = "west",
        };

    private static readonly IReadOnlyDictionary<string, (uint Id, string Symbol, bool Publish)> TownerBySourceType =
        TownerTypes.ToDictionary(value => value.SourceType,
            value => (value.Id, value.Symbol, value.Publish), StringComparer.Ordinal);

    public static CathedralNativeTownPlaytestProfile Compile(CathedralNativeTownPlaytestCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClassProfileDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TownSourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RosterTablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);

        var classRoot = RequireDirectory(options.ClassProfileDirectory, "class profile directory");
        var townSourceRoot = RequireDirectory(options.TownSourceDirectory, "Town source directory");
        var rosterPath = RequireInputFile(options.RosterTablePath, MaximumRosterBytes, "towner roster table");
        var outputRoot = ValidateFreshOutputPath(options.OutputDirectory);
        RejectOutputInputOverlap(outputRoot, [classRoot, townSourceRoot, rosterPath]);

        var classProfilePath = Path.Combine(classRoot, ClassProfileFileName);
        using var classProfileDocument = ReadJsonProfile(classProfilePath);
        var profileSet = NativePlayerArtProfiles.LoadDirectories([classRoot]);
        if (profileSet.ContentManifestHashes.Count != 1)
            throw new InvalidDataException("The Town profile requires exactly one qualified class-profile content identity.");
        var classManifestHash = RequiredString(classProfileDocument.RootElement, "contentManifestHash");
        if (!string.Equals(profileSet.ContentManifestHashes[0], classManifestHash, StringComparison.Ordinal))
            throw new InvalidDataException("The qualified class profile identity changed while the Town profile was being compiled.");
        var classProfile = classProfileDocument.RootElement.Clone();

        var townPack = CompileTownPack(townSourceRoot, rosterPath, options.TownSeed);
        var outputParent = Path.GetDirectoryName(outputRoot)
            ?? throw new InvalidDataException("Town profile output must have a parent directory.");
        var outputLeaf = Path.GetFileName(outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(outputLeaf))
            throw new InvalidDataException("Town profile output must name a new directory.");
        Directory.CreateDirectory(outputParent);
        var stagingRoot = Path.Combine(outputParent, $".{outputLeaf}.town-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        try {
            CopyClassProfile(classRoot, stagingRoot, classProfileDocument.RootElement);
            WritePack(Path.Combine(stagingRoot, TownDirectoryName), townPack);

            var innerProfile = JsonDocument.Parse(classProfile.GetRawText());
            using (innerProfile) {
                var contentPack = LoadProfilePack(stagingRoot, innerProfile.RootElement, "contentDirectory");
                var populationPack = LoadProfilePack(stagingRoot, innerProfile.RootElement, "populationDirectory");
                var runtimePack = LoadProfilePack(stagingRoot, innerProfile.RootElement, "runtimeDirectory");
                var publishedTownPack = ContentPackLoader.LoadManifestDirectory(Path.Combine(stagingRoot, TownDirectoryName));
                if (!string.Equals(publishedTownPack.Id, TownPackId, StringComparison.Ordinal)
                    || !string.Equals(publishedTownPack.Version, TownPackVersion, StringComparison.Ordinal))
                    throw new InvalidDataException("Published Town geometry pack identity changed during compilation.");

                var manifest = new ContentManifest(contentPack.Id, contentPack.Version,
                    [contentPack, populationPack, runtimePack, publishedTownPack]);
                var composed = ComposedContent.Compose(manifest);
                var townCatalog = AuthoritativeNativeTownCatalog.Load(composed);
                _ = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap
                    .ActivateNativeTownProgression(composed, options.TownSeed);

                var profile = new CathedralNativeTownPlaytestProfile(
                    FormatVersion,
                    AdapterId,
                    classProfile,
                    TownDirectoryName,
                    publishedTownPack.Id,
                    publishedTownPack.Version,
                    ComputePackTreeHash(Path.Combine(stagingRoot, TownDirectoryName)),
                    townCatalog.Fingerprint,
                    options.TownSeed,
                    manifest.Sha256);
                WriteProfile(Path.Combine(stagingRoot, ProfileFileName), profile);
                Directory.Move(stagingRoot, outputRoot);
                return profile;
            }
        } catch {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
            throw;
        }
    }

    /** Builds the seven strict semantic tables without publishing a profile directory. */
    public static ContentPack CompileTownPack(string townSourceDirectory, string rosterTablePath, uint townSeed)
    {
        var sourceRoot = RequireDirectory(townSourceDirectory, "Town source directory");
        var rosterPath = RequireInputFile(rosterTablePath, MaximumRosterBytes, "towner roster table");

        var maps = new List<(string Id, NativeTownCoarseMap Map)>(Maps.Length);
        foreach (var (mapId, fileName, expectedWidth, expectedHeight) in Maps) {
            var path = RequireInputFile(Path.Combine(sourceRoot, fileName), MaximumDunBytes, $"Town map '{mapId}'");
            var bytes = File.ReadAllBytes(path);
            maps.Add((mapId, ReadNativeTownCoarseMap(fileName, bytes, expectedWidth, expectedHeight)));
        }

        var tilPath = RequireInputFile(Path.Combine(sourceRoot, "town.til"), MaximumTilBytes, "Town .til definitions");
        var pieceCatalog = AuthoritativeDunPieceCatalog.LoadTil("town.til", File.ReadAllBytes(tilPath));
        var solPath = RequireInputFile(Path.Combine(sourceRoot, "town.sol"), MaximumSolBytes, "Town .sol flags");
        var solSource = File.ReadAllBytes(solPath);
        if (solSource.Length is < 1 or > AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            throw new InvalidDataException("Native Town .sol data must contain between one and 1379 flag bytes.");
        var rawFlags = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        solSource.CopyTo(rawFlags, 0);
        var tilePropertyRows = new List<string[]>(rawFlags.Length);
        // The native property byte uses 0xBF for known semantics; 0x40 is
        // reserved by this Town pack and remains bound in source_flags.
        for (var pieceId = 0; pieceId < rawFlags.Length; pieceId++) {
            var flags = rawFlags[pieceId];
            tilePropertyRows.Add([
                pieceId.ToString(CultureInfo.InvariantCulture),
                flags.ToString(CultureInfo.InvariantCulture),
                Bool((flags & (byte)AuthoritativeDunTileFlags.Solid) != 0),
                Bool((flags & (byte)AuthoritativeDunTileFlags.BlockLight) != 0),
                Bool((flags & (byte)AuthoritativeDunTileFlags.BlockMissile) != 0),
                Bool((flags & (byte)AuthoritativeDunTileFlags.Transparent) != 0),
                Bool((flags & (byte)AuthoritativeDunTileFlags.TransparentLeft) != 0),
                Bool((flags & (byte)AuthoritativeDunTileFlags.TransparentRight) != 0),
                Bool((flags & (byte)AuthoritativeDunTileFlags.Trap) != 0),
            ]);
        }

        var tables = new List<TsvTable> {
            CreateTable(TownIdentityPath,
                ["level_id", "level_symbol", "generator_id", "generator_revision"],
                [["0", "town", "original-diablo.town", "1"]]),
            CreateTable(TownMapsPath,
                ["map_id", "mega_width", "mega_height"],
                maps.Select(value => new[] {
                    value.Id,
                    value.Map.Width.ToString(CultureInfo.InvariantCulture),
                    value.Map.Height.ToString(CultureInfo.InvariantCulture),
                })),
            CreateTable(TownMapTilesPath,
                ["map_id", "x", "y", "mega_tile_id"],
                maps.SelectMany(value => value.Map.TileLayer.Select((tile, index) => new[] {
                    value.Id,
                    (index % value.Map.Width).ToString(CultureInfo.InvariantCulture),
                    (index / value.Map.Width).ToString(CultureInfo.InvariantCulture),
                    tile.ToString(CultureInfo.InvariantCulture),
                }))),
            CreateTable(TownPieceDefinitionsPath,
                ["mega_tile_id", "piece_north_west", "piece_north_east", "piece_south_west", "piece_south_east"],
                pieceCatalog.Definitions.Select(definition => new[] {
                    definition.MegaTileId.ToString(CultureInfo.InvariantCulture),
                    definition.NorthWestPieceId.ToString(CultureInfo.InvariantCulture),
                    definition.NorthEastPieceId.ToString(CultureInfo.InvariantCulture),
                    definition.SouthWestPieceId.ToString(CultureInfo.InvariantCulture),
                    definition.SouthEastPieceId.ToString(CultureInfo.InvariantCulture),
                })),
            CreateTable(TownTilePropertiesPath,
                ["piece_id", "source_flags", "solid", "block_light", "block_missile", "transparent", "transparent_left", "transparent_right", "trap"],
                tilePropertyRows),
            CompileRoster(rosterPath),
            CreateTable(TownSettingsPath,
                ["is_spawn", "is_multiplayer", "is_hellfire", "character_level", "warp_unlocks", "farmer_quest_state", "grave_quest_state", "entry", "town_warp_from", "water_quest_state", "initial_random_state"],
                [["false", "false", "false", "1", "0", "other", "other", "main", "0", "not_available", townSeed.ToString(CultureInfo.InvariantCulture)]]),
        };
        return new ContentPack(TownPackId, TownPackVersion, tables);
    }

    private static TsvTable CompileRoster(string rosterPath)
    {
        var table = TsvTable.Parse("towners.tsv", ReadUtf8(rosterPath, MaximumRosterBytes));
        foreach (var column in new[] { "type", "name", "shortName", "position_x", "position_y", "direction" })
            if (!table.Columns.Contains(column, StringComparer.Ordinal))
                throw new InvalidDataException($"Town roster source is missing required column '{column}'.");

        var occurrences = TownerTypes.ToDictionary(value => value.SourceType, _ => 0, StringComparer.Ordinal);
        var output = new List<string[]>(11);
        foreach (var row in table.Rows) {
            var type = row.Required("type");
            if (!TownerBySourceType.TryGetValue(type, out var mapping))
                throw new InvalidDataException($"Town roster contains unsupported native identity '{type}'.");
            occurrences[type]++;
            if (type != "TOWN_COW" && occurrences[type] > 1)
                throw new InvalidDataException($"Town roster duplicates native identity '{type}'.");
            if (!mapping.Publish)
                continue;

            var x = row.RequiredInt32("position_x");
            var y = row.RequiredInt32("position_y");
            if (x is < 0 or >= 112 || y is < 0 or >= 112)
                throw new InvalidDataException($"Town resident '{type}' has an out-of-bounds fine-grid position.");
            var sourceDirection = row.Required("direction");
            if (!Directions.TryGetValue(sourceDirection, out var direction))
                throw new InvalidDataException($"Town resident '{type}' has unsupported direction '{sourceDirection}'.");
            output.Add([
                mapping.Id.ToString(CultureInfo.InvariantCulture),
                mapping.Symbol,
                row.Required("name"),
                row.Required("shortName"),
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                direction,
            ]);
        }

        foreach (var identity in TownerTypes) {
            var expected = identity.SourceType == "TOWN_COW" ? 3 : 1;
            if (occurrences[identity.SourceType] != expected)
                throw new InvalidDataException($"Town roster source must contain {expected} row(s) for '{identity.SourceType}'.");
        }
        if (output.Count != 11)
            throw new InvalidDataException("Native base Town roster must publish exactly eight human residents and three cows.");
        output = output.OrderBy(row => int.Parse(row[0], CultureInfo.InvariantCulture)).ToList();
        return CreateTable(TownRosterPath,
            ["towner_id", "towner_symbol", "name", "short_name", "position_x", "position_y", "direction"],
            output);
    }

    private static NativeTownCoarseMap ReadNativeTownCoarseMap(
        string sourcePath,
        ReadOnlySpan<byte> bytes,
        int expectedWidth,
        int expectedHeight)
    {
        if (bytes.Length < sizeof(ushort) * 2)
            throw new InvalidDataException($"Native Town map '{sourcePath}' is missing its width and height header.");
        var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[sizeof(ushort)..]);
        if (width != expectedWidth || height != expectedHeight)
            throw new InvalidDataException(
                $"Native Town map '{sourcePath}' has dimensions {width}x{height}; expected {expectedWidth}x{expectedHeight}.");
        var tileCount = checked((int)width * height);
        var prefixLength = checked(sizeof(ushort) * (2 + tileCount));
        if (bytes.Length < prefixLength)
            throw new InvalidDataException(
                $"Native Town map '{sourcePath}' is truncated before its complete {width}x{height} coarse tile layer.");
        var tiles = new ushort[tileCount];
        for (var index = 0; index < tiles.Length; index++)
            tiles[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(sizeof(ushort) * (2 + index))..]);
        return new NativeTownCoarseMap(width, height, tiles);
    }

    private static void CopyClassProfile(string sourceRoot, string targetRoot, JsonElement profile)
    {
        var paths = new[] {
            RequiredString(profile, "contentDirectory"),
            RequiredString(profile, "populationDirectory"),
            RequiredString(profile, "runtimeDirectory"),
        };
        long totalBytes = 0;
        foreach (var relativePath in paths.Distinct(StringComparer.Ordinal)) {
            var source = ResolveRelativeDirectory(sourceRoot, relativePath);
            var target = ResolveRelativeDirectoryForWrite(targetRoot, relativePath);
            CopyDirectoryBounded(source, target, ref totalBytes);
        }
        var sourceProfile = Path.Combine(sourceRoot, ClassProfileFileName);
        var targetProfile = Path.Combine(targetRoot, ClassProfileFileName);
        File.Copy(sourceProfile, targetProfile, overwrite: false);
    }

    private static ContentPack LoadProfilePack(string profileRoot, JsonElement profile, string directoryProperty)
        => ContentPackLoader.LoadManifestDirectory(
            ResolveRelativeDirectory(profileRoot, RequiredString(profile, directoryProperty)));

    private static TsvTable CreateTable(string sourcePath, IReadOnlyList<string> columns, IEnumerable<string[]> rows)
    {
        var output = new StringBuilder();
        output.AppendJoin('\t', columns).Append('\n');
        foreach (var row in rows) {
            if (row.Length != columns.Count)
                throw new InvalidDataException($"Generated Town table '{sourcePath}' has an invalid row width.");
            output.AppendJoin('\t', row).Append('\n');
        }
        return TsvTable.Parse(sourcePath, output.ToString());
    }

    private static void WritePack(string root, ContentPack pack)
    {
        Directory.CreateDirectory(root);
        foreach (var table in pack.Tables) {
            var path = Path.Combine(root, table.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToCrLf(table.ToCanonicalTsv()), Utf8);
        }
        var manifest = new { formatVersion = 1, id = pack.Id, version = pack.Version };
        File.WriteAllText(Path.Combine(root, ContentPackLoader.ManifestFileName),
            ToCrLf(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })), Utf8);
        var loaded = ContentPackLoader.LoadManifestDirectory(root);
        if (loaded.Id != pack.Id || loaded.Version != pack.Version
            || !loaded.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal)
                .SequenceEqual(pack.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Town semantic pack changed during publication.");
    }

    private static void WriteProfile(string path, CathedralNativeTownPlaytestProfile profile)
        => File.WriteAllText(path, ToCrLf(JsonSerializer.Serialize(profile, new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        })), Utf8);

    private static JsonDocument ReadJsonProfile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is < 1 or > NativePlayerArtProfiles.MaximumProfileBytes)
            throw new InvalidDataException($"Qualified class profile metadata '{path}' is missing or outside the supported size bound.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Qualified class profile metadata cannot be a reparse point.");
        try {
            return JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 32 });
        } catch (JsonException exception) {
            throw new InvalidDataException("Qualified class profile metadata is not valid JSON.", exception);
        }
    }

    private static string RequireDirectory(string requested, string label)
    {
        var path = Path.GetFullPath(requested);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"{label} does not exist: {path}");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"{label} cannot be a reparse point.");
        RejectReparseAncestors(path);
        return path;
    }

    private static string RequireInputFile(string requested, int maximumBytes, string label)
    {
        var path = Path.GetFullPath(requested);
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException($"{label} does not exist.", path);
        if (file.Length is < 1 || file.Length > maximumBytes)
            throw new InvalidDataException($"{label} is outside the supported {maximumBytes}-byte limit.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"{label} cannot be a reparse point.");
        RejectReparseAncestors(Path.GetDirectoryName(path)!);
        return path;
    }

    private static string ReadUtf8(string path, int maximumBytes)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > maximumBytes)
            throw new InvalidDataException("Towner roster exceeds its supported size bound.");
        try {
            return Utf8.GetString(bytes);
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException("Towner roster is not valid UTF-8.", exception);
        }
    }

    private static string ResolveRelativeDirectory(string root, string relative)
    {
        var normalized = relative.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (Path.IsPathRooted(relative) || relative.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException("Class profile contains an unsafe pack directory.");
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !Directory.Exists(fullPath))
            throw new InvalidDataException("Class profile references a missing or escaping pack directory.");
        RejectReparseTree(fullPath);
        return fullPath;
    }

    private static string ResolveRelativeDirectoryForWrite(string root, string relative)
    {
        var normalized = relative.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (Path.IsPathRooted(relative) || relative.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException("Class profile contains an unsafe pack directory.");
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Class profile pack directory escapes the output root.");
        return fullPath;
    }

    private static void CopyDirectoryBounded(string source, string target, ref long totalBytes)
    {
        Directory.CreateDirectory(target);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source)) {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Qualified class profile pack cannot contain reparse points.");
            var destination = Path.Combine(target, Path.GetFileName(entry));
            if ((attributes & FileAttributes.Directory) != 0) {
                CopyDirectoryBounded(entry, destination, ref totalBytes);
                continue;
            }
            var fileLength = new FileInfo(entry).Length;
            totalBytes = checked(totalBytes + fileLength);
            if (totalBytes > MaximumInputPackBytes)
                throw new InvalidDataException("Qualified class profile packs exceed the aggregate copy size bound.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(entry, destination, overwrite: false);
        }
    }

    private static string ValidateFreshOutputPath(string requested)
    {
        var path = Path.GetFullPath(requested);
        RejectReparseAncestors(path);
        if (Path.Exists(path))
            throw new IOException("Town profile output must be a new path.");
        return path;
    }

    private static void RejectOutputInputOverlap(string output, IReadOnlyCollection<string> inputs)
    {
        foreach (var input in inputs)
            if (IsWithin(output, input) || IsWithin(input, output))
                throw new InvalidDataException("Town profile output cannot overlap an input directory or file.");
    }

    private static bool IsWithin(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative == "." || (!Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Town profile path traverses reparse point '{directory.FullName}'.");
    }

    private static void RejectReparseTree(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory)) {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory)) {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Qualified class profile packs cannot contain reparse points.");
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(path);
            }
        }
    }

    private static string ComputePackTreeHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(path => new { FullPath = path, Relative = Path.GetRelativePath(root, path).Replace('\\', '/') })
                     .OrderBy(value => value.Relative, StringComparer.Ordinal)) {
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.FullPath)));
            hash.AppendData(Encoding.UTF8.GetBytes($"{file.Relative.Length}:{file.Relative}:{fileHash}\n"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string RequiredString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Qualified class profile is missing '{property}'.");
        return value.GetString()!;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string ToCrLf(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);
}
