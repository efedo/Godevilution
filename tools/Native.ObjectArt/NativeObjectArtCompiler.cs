using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeTownerArt;

namespace Devilution.Tools.NativeObjectArt;

/** Imports the original L1 and L2 object CEL families into profile-bound, client-only atlases. */
public static class NativeObjectArtCompiler
{
    public const int MaximumCatalogBytes = 8 * 1024 * 1024;
    public const int MaximumSourceBytes = 64 * 1024 * 1024;
    public const int MaximumAtlasFileBytes = 128 * 1024 * 1024;
    public const long MaximumPackAtlasPixels = 16L * 1024 * 1024;
    public const string CatalogFileName = "native-object-catalog.json";
    public const string ObjectMetadataPath = "objects/objdat.tsv";
    public const string CathedralPalettePath = "levels/l1data/l1_1.pal";
    /** L5-L8 objects (batch 2.3), under the Catacombs palette the scene pack uses. */
    public const string CatacombsCatalogFileName = "native-object-catalog-catacombs.json";
    public const string CatacombsPalettePath = "levels/l2data/l2_1.pal";
    public const string CanonicalObjectMetadataSha256 = "99ca3881bb95f5ee70a1bc811b00d11cf41e7f2a8e7defd77acc4af82e587dee";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed record ExpectedObject(
        uint Id,
        string Symbol,
        string Stem,
        int AnimDelay,
        int AnimLen,
        int AnimWidth,
        bool Animated);

    private static readonly ExpectedObject[] ExpectedObjects = [
        new(1, "OBJ_L1LDOOR", "l1doors", 1, 0, 64, false),
        new(2, "OBJ_L1RDOOR", "l1doors", 2, 0, 64, false),
        new(5, "OBJ_CHEST1", "chest1", 1, 0, 96, false),
        new(6, "OBJ_CHEST2", "chest2", 1, 0, 96, false),
        new(7, "OBJ_CHEST3", "chest3", 1, 0, 96, false),
        new(48, "OBJ_SARC", "sarc", 1, 5, 128, false),
        new(53, "OBJ_TRAPL", "traphole", 1, 0, 64, false),
        new(54, "OBJ_TRAPR", "traphole", 2, 0, 64, false),
        new(57, "OBJ_BARREL", "barrel", 1, 9, 96, false),
        new(58, "OBJ_BARRELEX", "barrelex", 1, 10, 96, false),
    ];
    private static readonly int[] MinimumFrameCounts = [4, 6, 6, 6, 5, 2, 9, 10];
    // The Cathedral set with the L2 doors and without the L1-only sarcophagus.
    private static readonly ExpectedObject[] CatacombsObjects = [
        new(5, "OBJ_CHEST1", "chest1", 1, 0, 96, false),
        new(6, "OBJ_CHEST2", "chest2", 1, 0, 96, false),
        new(7, "OBJ_CHEST3", "chest3", 1, 0, 96, false),
        new(42, "OBJ_L2LDOOR", "l2doors", 1, 0, 64, false),
        new(43, "OBJ_L2RDOOR", "l2doors", 2, 0, 64, false),
        new(53, "OBJ_TRAPL", "traphole", 1, 0, 64, false),
        new(54, "OBJ_TRAPR", "traphole", 2, 0, 64, false),
        new(57, "OBJ_BARREL", "barrel", 1, 9, 96, false),
        new(58, "OBJ_BARRELEX", "barrelex", 1, 10, 96, false),
    ];
    private static readonly int[] CatacombsMinimumFrameCounts = [6, 6, 6, 4, 2, 9, 10];

    /** One object art family: its levels, palette, objects and output catalog. */
    private sealed record ObjectFamily(
        string Location,
        uint[] LevelIds,
        string PalettePath,
        string PaletteDescription,
        string CatalogFile,
        string AtlasDirectory,
        ExpectedObject[] Objects,
        int[] MinimumFrames);

    private static readonly ObjectFamily CathedralFamily = new("cathedral", [1], CathedralPalettePath, "Cathedral palette",
        CatalogFileName, "sprites", ExpectedObjects, MinimumFrameCounts);
    private static readonly ObjectFamily CatacombsFamily = new(NativePlayerArtNames.CatacombsLocation, [5, 6, 7, 8],
        CatacombsPalettePath, "Catacombs palette", CatacombsCatalogFileName, "sprites/catacombs", CatacombsObjects,
        CatacombsMinimumFrameCounts);

    // Native objdat.tsv: AddShrine selects frames 1-11 or 12-22 in each 128px CEL;
    // intrinsic trapped chests reuse the corresponding ordinary chest CEL.
    private static ObjectFamily WithNativeInteractions(ObjectFamily family)
        => family with {
            LevelIds = family.Location == "cathedral" ? [1, 2, 3, 4] : family.LevelIds,
            Objects = [.. family.Objects,
                new(59, "OBJ_SHRINEL", "lshrineg", 1, 11, 128, false),
                new(60, "OBJ_SHRINER", "rshrineg", 1, 11, 128, false),
                new(68, "OBJ_TCHEST1", "chest1", 1, 0, 96, false),
                new(69, "OBJ_TCHEST2", "chest2", 1, 0, 96, false),
                new(70, "OBJ_TCHEST3", "chest3", 1, 0, 96, false)],
            MinimumFrames = [.. family.MinimumFrames, 22, 22],
        };

    public static NativeObjectImportResult Import(NativeObjectImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Import(options, NativePlayerArtProfiles.LoadDirectories(options.ProfileDirectories));
    }

    /** Imports for an already verified profile set; tests use it to add Catacombs bindings. */
    internal static NativeObjectImportResult Import(NativeObjectImportOptions options, NativePlayerArtProfileSet profileSet)
    {
        var sourceRoot = options.Archive is null ? RequireDirectory(options.SourceRoot, "original source root") : string.Empty;
        var textDataRoot = RequireDirectory(options.TextDataRoot, "text-data root");
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        if (Directory.Exists(outputRoot) || File.Exists(outputRoot))
            throw new IOException($"Output root already exists: {outputRoot}");

        var cathedral = WithNativeInteractions(CathedralFamily);
        var catacombs = WithNativeInteractions(CatacombsFamily);
        var bindings = SelectBindings(profileSet, cathedral);
        if (bindings.Length is < 1 or > 256)
            throw new InvalidDataException("Verified native profiles must provide one to 256 explicit Cathedral level bindings.");
        // Profiles that carry L5-L8 (batch 2.1) also get the Catacombs family.
        var catacombsBindings = SelectBindings(profileSet, catacombs);
        if (catacombsBindings.Length > 256)
            throw new InvalidDataException("Verified native profiles provide too many Catacombs level bindings.");

        var metadataPath = ResolveFile(textDataRoot, ObjectMetadataPath, "native object metadata");
        var metadataBytes = ReadBoundedFile(metadataPath, 1024 * 1024, "native object metadata");
        if (Sha256Hex(metadataBytes) != CanonicalObjectMetadataSha256)
            throw new InvalidDataException("objdat.tsv does not match the canonical base-game native object definitions.");

        var outputParent = Path.GetDirectoryName(outputRoot)
            ?? throw new InvalidDataException("Output root must have a parent directory.");
        Directory.CreateDirectory(outputParent);
        var leaf = Path.GetFileName(outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(leaf))
            throw new InvalidDataException("Output root must name a new directory.");
        var staging = Path.Combine(outputParent, $".{leaf}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try {
            var catalog = WriteFamilyCatalog(cathedral, bindings, metadataBytes, options.Archive, sourceRoot, staging);
            if (catacombsBindings.Length != 0)
                WriteFamilyCatalog(catacombs, catacombsBindings, metadataBytes, options.Archive, sourceRoot, staging);
            Directory.Move(staging, outputRoot);
            return new NativeObjectImportResult(outputRoot, catalog.PackId, catalog.Sprites.Count, catalog.Objects.Count);
        } catch {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    private static NativeObjectProfileBinding[] SelectBindings(NativePlayerArtProfileSet profileSet, ObjectFamily family)
        => profileSet.LevelBindings
            .Where(binding => family.LevelIds.Contains(binding.LevelId)
                && string.Equals(binding.Location, family.Location, StringComparison.Ordinal))
            .Select(binding => (binding.ContentManifestHash, binding.LevelId))
            .Distinct()
            .OrderBy(binding => binding.ContentManifestHash, StringComparer.Ordinal)
            .ThenBy(binding => binding.LevelId)
            .Select(binding => new NativeObjectProfileBinding(binding.ContentManifestHash, binding.LevelId, family.Location))
            .ToArray();

    private static NativeObjectArtCatalog WriteFamilyCatalog(
        ObjectFamily family,
        NativeObjectProfileBinding[] bindings,
        byte[] metadataBytes,
        Godevilution.Tools.Archive.NativeArchiveSource? archive,
        string sourceRoot,
        string staging)
    {
        var objectSpecs = ParseObjects(metadataBytes, family.Objects);
        var spriteSpecs = family.Objects
            .Select(spec => spec.Stem)
            .Distinct(StringComparer.Ordinal)
            .Select((stem, spriteId) => new SpriteSpec(
                spriteId,
                $"objects/{stem}.cel",
                family.Objects.First(spec => spec.Stem == stem).AnimWidth,
                family.MinimumFrames[spriteId]))
            .ToArray();

        var paletteBytes = ReadSourceFile(archive, sourceRoot, family.PalettePath,
            NativePlayerPalette.ColorCount * NativePlayerPalette.BytesPerColor, family.PaletteDescription, family.PaletteDescription);
        var palette = NativePlayerPalette.Parse(paletteBytes, family.PaletteDescription);

        var sourceHashes = new List<NativeObjectSourceHash>(spriteSpecs.Length + 2) {
            new($"assets/txtdata/{ObjectMetadataPath}", Sha256Hex(metadataBytes)),
            new(family.PalettePath, Sha256Hex(paletteBytes)),
        };
        var atlases = new List<NativeObjectAtlas>(spriteSpecs.Length);
        var sprites = new List<NativeObjectSpriteMetadata>(spriteSpecs.Length);
        long aggregateAtlasPixels = 0;
        long aggregateFileBytes = 0;

        foreach (var spriteSpec in spriteSpecs) {
            var celBytes = ReadSourceFile(archive, sourceRoot, spriteSpec.SourcePath, MaximumSourceBytes,
                "native object CEL", $"native object CEL {spriteSpec.SourcePath}");
            sourceHashes.Add(new NativeObjectSourceHash(spriteSpec.SourcePath, Sha256Hex(celBytes)));
            NativeTownerCelSheet sheet;
            try {
                sheet = NativeTownerCelDecoder.Decode(celBytes, spriteSpec.NativeWidth, expectedGroupCount: 1, expectedFramesPerGroup: 0);
            } catch (Exception exception) when (exception is InvalidDataException or OverflowException) {
                throw new InvalidDataException($"Native object CEL '{spriteSpec.SourcePath}' is malformed: {exception.Message}", exception);
            }
            var frames = sheet.Directions[0];
            if (frames.Count < spriteSpec.MinimumFrameCount)
                throw new InvalidDataException($"Native object CEL '{spriteSpec.SourcePath}' must contain at least {spriteSpec.MinimumFrameCount} source frames.");
            if (frames.Count > NativeTownerCelDecoder.MaximumFramesPerGroup)
                throw new InvalidDataException($"Native object CEL '{spriteSpec.SourcePath}' exceeds the supported source-frame count.");
            var pixels = new NativeTownerCelSheet([frames]);
            NativeTownerAtlasPixels atlasPixels;
            try {
                atlasPixels = NativeTownerArtPng.EncodeAtlas(pixels, palette);
            } catch (Exception exception) when (exception is InvalidDataException or OverflowException) {
                throw new InvalidDataException($"Native object CEL '{spriteSpec.SourcePath}' cannot be packed: {exception.Message}", exception);
            }
            aggregateAtlasPixels = AddAtlasPixelsToAggregate(aggregateAtlasPixels, atlasPixels.Width, atlasPixels.Height);
            if (atlasPixels.Png.Length > MaximumAtlasFileBytes)
                throw new InvalidDataException($"Generated object atlas for '{spriteSpec.SourcePath}' exceeds the {MaximumAtlasFileBytes} byte file limit.");
            aggregateFileBytes = checked(aggregateFileBytes + atlasPixels.Png.Length);
            if (aggregateFileBytes > MaximumAtlasFileBytes)
                throw new InvalidDataException($"Generated native object atlas family exceeds the {MaximumAtlasFileBytes} byte aggregate file limit.");

            var atlasId = spriteSpec.SpriteId;
            var path = $"{family.AtlasDirectory}/{atlasId}-{Path.GetFileNameWithoutExtension(spriteSpec.SourcePath)}.png";
            var fullPath = ResolveOutputPath(staging, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, atlasPixels.Png);
            atlases.Add(new NativeObjectAtlas(atlasId, path, atlasPixels.Width, atlasPixels.Height, Sha256Hex(atlasPixels.Png)));
            var cellWidth = atlasPixels.Width / frames.Count;
            var cellHeight = atlasPixels.Height;
            var frameRegions = frames.Select((frame, index) => new NativeObjectFrameRegion(
                index + 1,
                atlasId,
                checked(index * cellWidth),
                checked(cellHeight - frame.Height),
                frame.Width,
                frame.Height)).ToArray();
            sprites.Add(new NativeObjectSpriteMetadata(
                spriteSpec.SpriteId,
                spriteSpec.SourcePath,
                spriteSpec.NativeWidth,
                frames.Count,
                Array.AsReadOnly(frameRegions)));
        }

        var catalogPayload = new NativeObjectArtCatalog(
            1,
            string.Empty,
            Array.AsReadOnly(bindings),
            Array.AsReadOnly(sourceHashes.OrderBy(source => source.Path, StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(atlases.OrderBy(atlas => atlas.Id).ToArray()),
            Array.AsReadOnly(sprites.OrderBy(sprite => sprite.SpriteId).ToArray()),
            Array.AsReadOnly(objectSpecs.Select(spec => new NativeObjectDefinitionMetadata(
                spec.Id, spec.SpriteId, spec.AnimDelay, spec.AnimLen, spec.AnimWidth, spec.Animated)).ToArray()));
        var canonical = JsonSerializer.SerializeToUtf8Bytes(catalogPayload, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        var digest = Sha256Hex(canonical);
        var catalog = catalogPayload with { PackId = $"native-object-{digest[..24]}" };
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        json = NormalizeCrlf(json) + "\r\n";
        var catalogBytes = new UTF8Encoding(false).GetBytes(json);
        if (catalogBytes.Length > MaximumCatalogBytes)
            throw new InvalidDataException($"Generated native object catalog exceeds the {MaximumCatalogBytes} byte limit.");
        File.WriteAllBytes(Path.Combine(staging, family.CatalogFile), catalogBytes);
        return catalog;
    }

    public static IReadOnlyList<NativeObjectDefinitionMetadata> ReadBaseObjectDefinitions(string textDataRoot)
    {
        var root = RequireDirectory(textDataRoot, "text-data root");
        var metadataPath = ResolveFile(root, ObjectMetadataPath, "native object metadata");
        var bytes = ReadBoundedFile(metadataPath, 1024 * 1024, "native object metadata");
        if (Sha256Hex(bytes) != CanonicalObjectMetadataSha256)
            throw new InvalidDataException("objdat.tsv does not match the canonical base-game native object definitions.");
        return Array.AsReadOnly(ParseObjects(bytes, ExpectedObjects).Select(spec => new NativeObjectDefinitionMetadata(
            spec.Id, spec.SpriteId, spec.AnimDelay, spec.AnimLen, spec.AnimWidth, spec.Animated)).ToArray());
    }

    public static IReadOnlyList<(int SpriteId, string SourcePath, int NativeWidth)> ReadBaseSpriteDefinitions(string textDataRoot)
    {
        var objects = ReadBaseObjectDefinitions(textDataRoot);
        var expected = ExpectedObjects.ToDictionary(item => item.Id);
        return Array.AsReadOnly(objects
            .Select(obj => (obj.SpriteId, expected[obj.NativeObjectId].Stem, obj.AnimWidth))
            .Distinct()
            .OrderBy(item => item.SpriteId)
            .Select(item => (item.SpriteId, $"objects/{item.Stem}.cel", item.AnimWidth))
            .ToArray());
    }

    public static long AddAtlasPixelsToAggregate(long currentPixels, int atlasWidth, int atlasHeight)
    {
        if (currentPixels < 0 || atlasWidth < 1 || atlasHeight < 1)
            throw new InvalidDataException("Native object atlas dimensions or aggregate pixel count are invalid.");
        var next = checked(currentPixels + checked((long)atlasWidth * atlasHeight));
        if (next > MaximumPackAtlasPixels)
            throw new InvalidDataException($"Generated native object atlases exceed the {MaximumPackAtlasPixels} pixel family limit.");
        return next;
    }

    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record SpriteSpec(int SpriteId, string SourcePath, int NativeWidth, int MinimumFrameCount);
    private sealed record ParsedObject(uint Id, int SpriteId, int AnimDelay, int AnimLen, int AnimWidth, bool Animated, string SourcePath);

    private static IReadOnlyList<ParsedObject> ParseObjects(byte[] bytes, ExpectedObject[] expectedObjects)
    {
        string text;
        try {
            text = new UTF8Encoding(false, true).GetString(bytes);
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException("objdat.tsv must be valid UTF-8.", exception);
        }
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string[] expectedHeader = ["id", "file", "minLevel", "maxLevel", "levelType", "theme", "quest", "flags", "animDelay", "animLen", "animWidth", "selectionRegion", "behavior"];
        if (lines.Length < 2 || !lines[0].Split('\t').SequenceEqual(expectedHeader, StringComparer.Ordinal))
            throw new InvalidDataException("objdat.tsv has an unexpected ordered header.");

        var result = new List<ParsedObject>(expectedObjects.Length);
        var spriteIdsByPath = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var expected in expectedObjects) {
            var rowIndex = checked((int)expected.Id + 1);
            if (rowIndex >= lines.Length)
                throw new InvalidDataException($"objdat.tsv is missing canonical row {expected.Symbol}.");
            var fields = lines[rowIndex].Split('\t');
            if (fields.Length != expectedHeader.Length
                || fields[0] != expected.Symbol
                || fields[1] != expected.Stem
                || !TryParseInt(fields[8], 1, 120, out var animDelay)
                || !TryParseInt(fields[9], 0, 64, out var animLen)
                || !TryParseInt(fields[10], 1, 128, out var animWidth))
                throw new InvalidDataException($"objdat.tsv row {rowIndex + 1} does not match canonical object {expected.Symbol}.");
            var flags = fields[7].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (flags.Distinct(StringComparer.Ordinal).Count() != flags.Length
                || flags.Any(flag => flag.Length == 0)
                || flags.Contains("Animated", StringComparer.Ordinal) != expected.Animated
                || animDelay != expected.AnimDelay
                || animLen != expected.AnimLen
                || animWidth != expected.AnimWidth)
                throw new InvalidDataException($"objdat.tsv animation metadata for {expected.Symbol} is not canonical.");
            var sourcePath = $"objects/{expected.Stem}.cel";
            if (!spriteIdsByPath.TryGetValue(sourcePath, out var spriteId)) {
                spriteId = spriteIdsByPath.Count;
                spriteIdsByPath.Add(sourcePath, spriteId);
            }
            result.Add(new ParsedObject(expected.Id, spriteId, animDelay, animLen, animWidth, expected.Animated, sourcePath));
        }
        if (spriteIdsByPath.Count != expectedObjects.Select(expected => expected.Stem).Distinct(StringComparer.Ordinal).Count())
            throw new InvalidDataException("The selected native object family must resolve to its distinct source CEL files.");
        return result.AsReadOnly();
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
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException($"{description} path is unsafe.");
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !File.Exists(fullPath))
            throw new FileNotFoundException($"{description} does not exist under the selected root.", fullPath);
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
