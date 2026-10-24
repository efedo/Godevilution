using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Tools.NativePlayerArt;

namespace Devilution.Tools.NativeTownerArt;

/** Compiles explicitly extracted Diablo Town CEL files into a profile-bound, client-only art pack. */
public static class NativeTownerArtCompiler
{
    public const int MaximumCatalogBytes = 8 * 1024 * 1024;
    public const long MaximumPackPngBytes = 512L * 1024 * 1024;
    public const string CatalogFileName = "native-towner-catalog.json";
    public const string TownPalettePath = "levels/towndata/town.pal";
    public const string TownerMetadataPath = "towners/towners.tsv";
    public const string CanonicalTownerMetadataSha256 = "91546c020a1a24b3cae189bcc80ef7bf2c50675f5b92243b43e6317dea993b9e";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly IReadOnlyDictionary<string, (int TownerId, int SpriteId)> SupportedTowners =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal) {
            ["TOWN_SMITH"] = (0, 0),
            ["TOWN_HEALER"] = (1, 1),
            ["TOWN_TAVERN"] = (3, 2),
            ["TOWN_STORY"] = (4, 3),
            ["TOWN_DRUNK"] = (5, 4),
            ["TOWN_WITCH"] = (6, 5),
            ["TOWN_BMAID"] = (7, 6),
            ["TOWN_PEGBOY"] = (8, 7),
            ["TOWN_COW"] = (9, 8),
        };

    private static readonly IReadOnlyDictionary<string, int> NativeDirections =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
            ["South"] = 0,
            ["SouthWest"] = 1,
            ["West"] = 2,
            ["NorthWest"] = 3,
            ["North"] = 4,
            ["NorthEast"] = 5,
            ["East"] = 6,
            ["SouthEast"] = 7,
        };

    public static NativeTownerImportResult Import(NativeTownerImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sourceRoot = options.Archive is null ? RequireDirectory(options.SourceRoot, "source root") : string.Empty;
        var textDataRoot = RequireDirectory(options.TextDataRoot, "text-data root");
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        if (Directory.Exists(outputRoot) || File.Exists(outputRoot))
            throw new IOException($"Output root already exists: {outputRoot}");

        var profileSet = NativePlayerArtProfiles.LoadDirectories(options.ProfileDirectories);
        if (profileSet.ContentManifestHashes.Count is < 1 or > 64
            || profileSet.TownCatalogFingerprints.Count != profileSet.ContentManifestHashes.Count)
            throw new InvalidDataException("Every native towner profile binding must come from a verified format-5 Town profile.");
        var profileBindings = profileSet.ContentManifestHashes
            .Order(StringComparer.Ordinal)
            .Select(hash => new NativeTownerProfileBinding(hash, profileSet.TownCatalogFingerprints[hash], 0, "town"))
            .ToArray();

        var metadataPath = ResolveFile(textDataRoot, TownerMetadataPath, "Town towner metadata");
        var rosterBytes = ReadBoundedFile(metadataPath, 1024 * 1024, "Town towner metadata");
        if (NativeTownerArtPng.Sha256Hex(rosterBytes) != CanonicalTownerMetadataSha256)
            throw new InvalidDataException("Town towner metadata does not match the canonical base-game roster and animation order.");
        var residentsAndSprites = ParseTownerMetadata(rosterBytes);
        var sourcePaths = residentsAndSprites.Sprites
            .OrderBy(sprite => sprite.SpriteId)
            .Select(sprite => (sprite.SpriteId, sprite.SourcePath, sprite.AlignmentWidth, sprite.AnimationFrames, sprite.TicksPerFrame, sprite.AnimationOrder))
            .ToArray();

        var paletteBytes = ReadSourceFile(options.Archive, sourceRoot, TownPalettePath,
            NativePlayerPalette.ColorCount * NativePlayerPalette.BytesPerColor, "Town palette", "Town palette");
        var palette = NativePlayerPalette.Parse(paletteBytes, "Town palette");

        var outputParent = Path.GetDirectoryName(outputRoot)
            ?? throw new InvalidDataException("Output root must have a parent directory.");
        Directory.CreateDirectory(outputParent);
        var leaf = Path.GetFileName(outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(leaf))
            throw new InvalidDataException("Output root must name a new directory.");
        var staging = Path.Combine(outputParent, $".{leaf}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try {
            var spriteDirectory = Path.Combine(staging, "sprites");
            Directory.CreateDirectory(spriteDirectory);
            var sourceHashes = new List<NativeTownerSourceHash>(11) {
                new($"assets/txtdata/{TownerMetadataPath}", NativeTownerArtPng.Sha256Hex(rosterBytes)),
                new(TownPalettePath, NativeTownerArtPng.Sha256Hex(paletteBytes)),
            };
            var atlases = new List<NativeTownerAtlas>(9);
            var sprites = new List<NativeTownerSpriteMetadata>(9);
            long packBytes = 0;
            long aggregateAtlasPixels = 0;

            foreach (var spriteSpec in sourcePaths) {
                var celBytes = ReadSourceFile(options.Archive, sourceRoot, spriteSpec.SourcePath,
                    NativeTownerCelDecoder.MaximumInputBytes, "Towner CEL", $"Towner CEL {spriteSpec.SourcePath}");
                sourceHashes.Add(new NativeTownerSourceHash(spriteSpec.SourcePath, NativeTownerArtPng.Sha256Hex(celBytes)));
                NativeTownerCelSheet sheet;
                try {
                    var groupCount = spriteSpec.SpriteId == 8 ? 8 : 1;
                    sheet = NativeTownerCelDecoder.Decode(celBytes, spriteSpec.AlignmentWidth, groupCount, expectedFramesPerGroup: 0);
                } catch (Exception exception) when (exception is InvalidDataException or OverflowException) {
                    throw new InvalidDataException($"Native towner CEL '{spriteSpec.SourcePath}' is malformed: {exception.Message}", exception);
                }
                var actualFrames = sheet.Directions[0].Count;
                if (actualFrames < spriteSpec.AnimationFrames
                    || sheet.Directions.Any(group => group.Count != actualFrames))
                    throw new InvalidDataException($"Native towner CEL '{spriteSpec.SourcePath}' does not contain the complete animation cycle in every group.");
                if (spriteSpec.AnimationOrder.Any(frame => frame >= spriteSpec.AnimationFrames))
                    throw new InvalidDataException($"Native towner metadata for '{spriteSpec.SourcePath}' references a frame outside its active animation cycle.");

                NativeTownerAtlasPixels atlasPixels;
                try {
                    atlasPixels = NativeTownerArtPng.EncodeAtlas(sheet, palette);
                } catch (Exception exception) when (exception is InvalidDataException or OverflowException) {
                    throw new InvalidDataException($"Native towner CEL '{spriteSpec.SourcePath}' cannot be packed: {exception.Message}", exception);
                }
                aggregateAtlasPixels = AddAtlasPixelsToAggregate(aggregateAtlasPixels, atlasPixels.Width, atlasPixels.Height);
                var atlasPath = $"sprites/{spriteSpec.SpriteId}-{Path.GetFileNameWithoutExtension(spriteSpec.SourcePath)}.png";
                var atlasFullPath = ResolveOutputPath(staging, atlasPath);
                Directory.CreateDirectory(Path.GetDirectoryName(atlasFullPath)!);
                File.WriteAllBytes(atlasFullPath, atlasPixels.Png);
                packBytes = checked(packBytes + atlasPixels.Png.Length);
                if (packBytes > MaximumPackPngBytes)
                    throw new InvalidDataException($"Generated native towner art exceeds the {MaximumPackPngBytes} byte pack limit.");

                var atlasId = spriteSpec.SpriteId;
                atlases.Add(new NativeTownerAtlas(atlasId, atlasPath, atlasPixels.Width, atlasPixels.Height,
                    NativeTownerArtPng.Sha256Hex(atlasPixels.Png)));
                var cellWidth = atlasPixels.Width / actualFrames;
                var cellHeight = atlasPixels.Height / sheet.Directions.Count;
                var frames = new List<NativeTownerFrameRegion>(checked(actualFrames * sheet.Directions.Count));
                for (var group = 0; group < sheet.Directions.Count; group++) {
                    for (var frameIndex = 0; frameIndex < actualFrames; frameIndex++) {
                        var frame = sheet.Directions[group][frameIndex];
                        frames.Add(new NativeTownerFrameRegion(
                            group,
                            frameIndex,
                            atlasId,
                            checked(frameIndex * cellWidth),
                            checked(group * cellHeight + cellHeight - frame.Height),
                            frame.Width,
                            frame.Height));
                    }
                }
                sprites.Add(new NativeTownerSpriteMetadata(
                    spriteSpec.SpriteId,
                    spriteSpec.AlignmentWidth,
                    sheet.Directions.Count,
                    actualFrames,
                    spriteSpec.AnimationFrames,
                    spriteSpec.TicksPerFrame,
                    spriteSpec.AnimationOrder,
                    frames.AsReadOnly()));
            }

            var catalogPayload = new NativeTownerArtCatalog(
                1,
                string.Empty,
                profileBindings,
                sourceHashes.OrderBy(source => source.Path, StringComparer.Ordinal).ToArray(),
                atlases.OrderBy(atlas => atlas.Id).ToArray(),
                sprites.OrderBy(sprite => sprite.SpriteId).ToArray(),
                residentsAndSprites.Residents);
            var canonical = JsonSerializer.SerializeToUtf8Bytes(catalogPayload, new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            var digest = Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
            var catalog = catalogPayload with { PackId = $"native-towner-{digest[..24]}" };
            var json = JsonSerializer.Serialize(catalog, JsonOptions);
            json = NormalizeCrlf(json) + "\r\n";
            var catalogBytes = new UTF8Encoding(false).GetBytes(json);
            if (catalogBytes.Length > MaximumCatalogBytes)
                throw new InvalidDataException($"Generated native towner catalog exceeds the {MaximumCatalogBytes} byte limit.");
            File.WriteAllBytes(Path.Combine(staging, CatalogFileName), catalogBytes);
            Directory.Move(staging, outputRoot);
            return new NativeTownerImportResult(outputRoot, catalog.PackId, catalog.Sprites.Count, catalog.Residents.Count);
        } catch {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    /** Reads only the canonical base roster used by the importer; conditional Hellfire rows are excluded. */
    public static IReadOnlyList<NativeTownerResidentMetadata> ReadBaseTownRoster(string textDataRoot)
    {
        var root = RequireDirectory(textDataRoot, "text-data root");
        var metadataPath = ResolveFile(root, TownerMetadataPath, "Town towner metadata");
        var bytes = ReadBoundedFile(metadataPath, 1024 * 1024, "Town towner metadata");
        return ParseTownerMetadata(bytes).Residents;
    }

    /** Reads canonical source animation controls without importing licensed sprite bytes. */
    public static IReadOnlyList<NativeTownerSpriteSourceMetadata> ReadBaseTownSpriteMetadata(string textDataRoot)
    {
        var root = RequireDirectory(textDataRoot, "text-data root");
        var metadataPath = ResolveFile(root, TownerMetadataPath, "Town towner metadata");
        var bytes = ReadBoundedFile(metadataPath, 1024 * 1024, "Town towner metadata");
        if (NativeTownerArtPng.Sha256Hex(bytes) != CanonicalTownerMetadataSha256)
            throw new InvalidDataException("Town towner metadata does not match the canonical base-game roster and animation order.");
        return ParseTownerMetadata(bytes).Sprites.Select(sprite => new NativeTownerSpriteSourceMetadata(
            sprite.SpriteId, sprite.SourcePath, sprite.AlignmentWidth, sprite.AnimationFrames,
            sprite.TicksPerFrame, Array.AsReadOnly(sprite.AnimationOrder.ToArray()))).ToArray();
    }

    private sealed record SpriteSpec(
        int SpriteId,
        string SourcePath,
        int AlignmentWidth,
        int AnimationFrames,
        int TicksPerFrame,
        IReadOnlyList<int> AnimationOrder);

    private sealed record ParsedMetadata(IReadOnlyList<SpriteSpec> Sprites, IReadOnlyList<NativeTownerResidentMetadata> Residents);

    private static ParsedMetadata ParseTownerMetadata(byte[] bytes)
    {
        string text;
        try {
            text = new UTF8Encoding(false, true).GetString(bytes);
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException("Town towner metadata must be valid UTF-8.", exception);
        }
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string[] expectedHeader = ["type", "name", "shortName", "position_x", "position_y", "direction", "animWidth", "animPath", "animFrames", "animDelay", "gossipTexts", "animOrder"];
        if (lines.Length < 2 || !lines[0].Split('\t').SequenceEqual(expectedHeader, StringComparer.Ordinal))
            throw new InvalidDataException("towners.tsv has an unexpected ordered header.");

        var selected = new List<(int TownerId, int SpriteId, int X, int Y, int Direction, SpriteSpec Sprite)>();
        var sprites = new Dictionary<int, SpriteSpec>();
        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++) {
            var line = lines[lineIndex];
            if (line.Length == 0)
                continue;
            var fields = line.Split('\t');
            if (fields.Length != expectedHeader.Length)
                throw new InvalidDataException($"towners.tsv row {lineIndex + 1} has the wrong number of fields.");
            if (!SupportedTowners.TryGetValue(fields[0], out var identity))
                continue;
            typeCounts[fields[0]] = typeCounts.GetValueOrDefault(fields[0]) + 1;
            if (!TryParseInt(fields[3], 0, 111, out var x)
                || !TryParseInt(fields[4], 0, 111, out var y)
                || !NativeDirections.TryGetValue(fields[5], out var direction)
                || !TryParseInt(fields[6], identity.TownerId == 9 ? 128 : 96, identity.TownerId == 9 ? 128 : 96, out var alignmentWidth)
                || !TryParseInt(fields[8], 1, 64, out var animationFrames)
                || !TryParseInt(fields[9], 1, 120, out var ticksPerFrame))
                throw new InvalidDataException($"towners.tsv row {lineIndex + 1} has invalid Town sprite metadata.");
            var sourcePath = identity.TownerId == 9
                ? "towners/animals/cow.cel"
                : NormalizeSourceCelPath(fields[7], lineIndex + 1);
            var order = ParseAnimationOrder(fields[11], animationFrames, lineIndex + 1);
            if (identity.TownerId == 9 && order.Count != 0)
                throw new InvalidDataException("Native cow animations do not use an explicit frame-order table.");
            var sprite = new SpriteSpec(identity.SpriteId, sourcePath, alignmentWidth, animationFrames, ticksPerFrame, order);
            if (sprites.TryGetValue(identity.SpriteId, out var existing)) {
                if (existing != sprite)
                    throw new InvalidDataException("Multiple Town residents disagree about their shared native sprite metadata.");
            } else {
                sprites.Add(identity.SpriteId, sprite);
            }
            selected.Add((identity.TownerId, identity.SpriteId, x, y, direction, sprite));
        }

        foreach (var entry in SupportedTowners) {
            var expected = entry.Key == "TOWN_COW" ? 3 : 1;
            if (typeCounts.GetValueOrDefault(entry.Key) != expected)
                throw new InvalidDataException($"towners.tsv must contain {expected} supported rows for {entry.Key}.");
        }
        if (selected.Count != 11 || sprites.Count != 9)
            throw new InvalidDataException("The base Town roster must contain eight humans, three cows, and nine native sprite families.");

        var residents = selected.Select((entry, slot) => new NativeTownerResidentMetadata(
            slot, entry.TownerId, entry.X, entry.Y, entry.Direction, entry.SpriteId)).ToArray();
        return new ParsedMetadata(sprites.Values.OrderBy(sprite => sprite.SpriteId).ToArray(), residents);
    }

    private static IReadOnlyList<int> ParseAnimationOrder(string raw, int animationFrames, int row)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<int>();
        var result = new List<int>();
        foreach (var token in raw.Split(',')) {
            if (!TryParseInt(token, 0, animationFrames - 1, out var frame))
                throw new InvalidDataException($"towners.tsv row {row} has an invalid animation-order frame.");
            result.Add(frame);
            if (result.Count > 1024)
                throw new InvalidDataException($"towners.tsv row {row} has an excessively long animation order.");
        }
        return result.AsReadOnly();
    }

    private static string NormalizeSourceCelPath(string raw, int row)
    {
        var normalized = raw.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains(':')
            || normalized.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException($"towners.tsv row {row} has an unsafe native sprite path.");
        return normalized + ".cel";
    }

    internal static long AddAtlasPixelsToAggregate(long currentPixels, int atlasWidth, int atlasHeight)
    {
        if (currentPixels < 0 || atlasWidth < 1 || atlasHeight < 1)
            throw new InvalidDataException("Native towner atlas dimensions or aggregate pixel count are invalid.");
        var nextPixels = checked(currentPixels + checked((long)atlasWidth * atlasHeight));
        if (nextPixels > NativeTownerArtPng.MaximumAggregateAtlasPixels)
            throw new InvalidDataException($"Generated native towner art exceeds the {NativeTownerArtPng.MaximumAggregateAtlasPixels} pixel family limit.");
        return nextPixels;
    }

    private static bool TryParseInt(string value, int minimum, int maximum, out int result)
        => int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out result)
            && result >= minimum && result <= maximum;

    private static byte[] ReadSourceFile(Godevilution.Tools.Archive.NativeArchiveSource? archive, string sourceRoot,
        string relativePath, int maximumBytes, string description, string pathDescription)
        => archive?.ReadBounded(relativePath, maximumBytes, description)
            ?? ReadBoundedFile(ResolveFile(sourceRoot, relativePath, pathDescription), maximumBytes, description);

    private static byte[] ReadBoundedFile(string path, int maximumBytes, string description)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1 || info.Length > maximumBytes)
            throw new InvalidDataException($"{description} '{path}' is missing, empty, or exceeds {maximumBytes} bytes.");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"{description} '{path}' cannot be a reparse point.");
        return File.ReadAllBytes(path);
    }

    private static string ResolveFile(string root, string relativePath, string description)
    {
        var normalized = relativePath.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (Path.IsPathRooted(relativePath)
            || relativePath.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException($"{description} path is unsafe.");
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !File.Exists(fullPath))
            throw new FileNotFoundException($"{description} does not exist under the selected source root.", fullPath);
        var directory = Path.GetDirectoryName(fullPath)!;
        while (directory.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"{description} path cannot pass through a reparse point.");
            if (string.Equals(directory, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                break;
            directory = Path.GetDirectoryName(directory)!;
        }
        return fullPath;
    }

    private static string RequireDirectory(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException($"A {description} is required.");
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"The {description} does not exist: {fullPath}");
        return fullPath;
    }

    private static string ResolveOutputPath(string root, string relativePath)
    {
        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException("Generated atlas path is unsafe.");
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Generated atlas path escapes the staging directory.");
        return fullPath;
    }

    private static string NormalizeCrlf(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\n', '\r').Replace("\r", "\r\n", StringComparison.Ordinal);
}
