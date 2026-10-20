using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Devilution.Tools.NativePlayerArt;

public sealed record NativePlayerArtImportOptions(
    string SourceRoot,
    string TextDataRoot,
    string GameContentDirectory,
    string OutputRoot,
    string TownPaletteRelativePath,
    string CathedralPaletteRelativePath,
    IReadOnlyList<string> ContentManifestHashes,
    IReadOnlyList<NativePlayerArtLevelBinding> LevelBindings);

public sealed record NativePlayerArtImportResult(
    string OutputDirectory,
    int ClipCount,
    IReadOnlyList<string> MissingAssets,
    IReadOnlyList<string> Diagnostics,
    string PackId);

public sealed record NativePlayerArtCatalog(
    int SchemaVersion,
    string PackId,
    IReadOnlyList<string> ContentManifestHashes,
    IReadOnlyList<NativePlayerArtLevelBinding> LevelBindings,
    IReadOnlyList<NativePlayerArtCatalogClip> Clips,
    IReadOnlyList<NativePlayerSpellGraphic> Spells);

/** frameWidth is the padded atlas cell width; anchorWidth and animationFrames retain native alignment and cycle metadata. */
public sealed record NativePlayerArtCatalogClip(
    uint ClassId,
    string Location,
    string Armor,
    string Weapon,
    string Graphic,
    string Path,
    int FrameWidth,
    int FrameHeight,
    int TicksPerFrame,
    int FramesPerDirection,
    int ActionFrame,
    int? AnchorWidth = null,
    int? AnimationFrames = null);

/** Compiles explicitly extracted native player CL2 files into an isolated Godot asset directory. */
public static class NativePlayerArtCompiler
{
    public const int MaximumCatalogClips = 4096;
    public const int MaximumCatalogBytes = 8 * 1024 * 1024;
    public const long MaximumPackPngBytes = 512L * 1024 * 1024;

    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly GraphicSpec[] GraphicSpecs = [
        new("town", "town-stand", "st"),
        new("town", "town-walk", "wl"),
        new("town", "lightning", "lm"),
        new("town", "fire", "fm"),
        new("town", "magic", "qm"),
        new("town", "death", "dt"),
        new("cathedral", "stand", "as"),
        new("cathedral", "walk", "aw"),
        new("cathedral", "attack", "at"),
        new("cathedral", "hit", "ht"),
        new("cathedral", "block", "bl"),
        new("cathedral", "lightning", "lm"),
        new("cathedral", "fire", "fm"),
        new("cathedral", "magic", "qm"),
        new("cathedral", "death", "dt"),
    ];

    private static readonly IReadOnlyDictionary<string, char> ArmorCharacters = new Dictionary<string, char>(StringComparer.Ordinal) {
        ["light"] = 'l',
        ["medium"] = 'm',
        ["heavy"] = 'h',
    };

    private static readonly IReadOnlyDictionary<string, char> WeaponCharacters = new Dictionary<string, char>(StringComparer.Ordinal) {
        ["unarmed"] = 'n',
        ["unarmed-shield"] = 'u',
        ["sword"] = 's',
        ["sword-shield"] = 'd',
        ["bow"] = 'b',
        ["axe"] = 'a',
        ["mace"] = 'm',
        ["mace-shield"] = 'h',
        ["staff"] = 't',
    };

    public static NativePlayerArtImportResult Import(NativePlayerArtImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sourceRoot = RequireDirectory(options.SourceRoot, "source root");
        var textDataRoot = RequireDirectory(options.TextDataRoot, "text data root");
        var gameContentDirectory = RequireDirectory(options.GameContentDirectory, "game content directory");
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var sortedContentHashes = ValidateContentManifestHashes(options.ContentManifestHashes);
        var sortedLevelBindings = ValidateLevelBindings(sortedContentHashes, options.LevelBindings);
        if (Directory.Exists(outputRoot) || File.Exists(outputRoot))
            throw new IOException($"Output root already exists: {outputRoot}");

        var townPalettePath = ResolveSourcePath(sourceRoot, options.TownPaletteRelativePath);
        var cathedralPalettePath = ResolveSourcePath(sourceRoot, options.CathedralPaletteRelativePath);
        var townPalette = NativePlayerPalette.Parse(File.ReadAllBytes(townPalettePath), "Town palette");
        var cathedralPalette = NativePlayerPalette.Parse(File.ReadAllBytes(cathedralPalettePath), "Cathedral palette");
        var metadata = NativePlayerArtMetadata.Load(textDataRoot, gameContentDirectory);

        var outputParent = Path.GetDirectoryName(outputRoot)
            ?? throw new InvalidDataException("Output root must have a parent directory.");
        Directory.CreateDirectory(outputParent);
        var outputLeaf = Path.GetFileName(outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(outputLeaf))
            throw new InvalidDataException("Output root must name a new directory.");
        var staging = Path.Combine(outputParent, $".{outputLeaf}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try {
            var catalogDirectory = staging;
            var spritesDirectory = Path.Combine(catalogDirectory, "sprites");
            Directory.CreateDirectory(spritesDirectory);
            var clips = new List<NativePlayerArtCatalogClip>();
            var missing = new List<string>();
            var diagnostics = metadata.Diagnostics.ToList();
            long packBytes = 0;
            using var packHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendHash(packHash, "content-manifest-hashes", Encoding.UTF8.GetBytes(string.Join("\n", sortedContentHashes)));
            AppendHash(packHash, "level-bindings", SerializeIndentedCrLf(sortedLevelBindings));

            foreach (var playerClass in metadata.Classes) {
                byte[]? classTranslation;
                try {
                    var classTranslationPath = Path.Combine(sourceRoot, "plrgfx", $"{playerClass.ClassTranslation}.trn");
                    classTranslation = ReadOptionalTranslation(classTranslationPath, "class");
                } catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException) {
                    throw new InvalidDataException($"Native player class translation 'plrgfx/{playerClass.ClassTranslation}.trn' is malformed: {exception.Message}", exception);
                }
                foreach (var armor in NativePlayerArtNames.ArmorNames) {
                    foreach (var graphicSpec in GraphicSpecs) {
                        var sourceWeapons = NativePlayerArtNames.WeaponNames
                            .Select(weapon => NativePlayerClassMetadata.NormalizeWeapon(graphicSpec.Location, graphicSpec.Graphic, weapon))
                            .Distinct(StringComparer.Ordinal);
                        foreach (var weapon in sourceWeapons) {
                            if (graphicSpec.Graphic == "block"
                                && weapon is not "unarmed-shield" and not "sword-shield" and not "mace-shield")
                                continue;
                            if (clips.Count >= MaximumCatalogClips)
                                throw new InvalidDataException($"Native player pack exceeds the {MaximumCatalogClips} clip limit.");
                            var frameCount = playerClass.GetFrames(graphicSpec.Graphic, weapon, armor);
                            var actionFrame = playerClass.GetActionFrame(graphicSpec.Graphic, weapon);
                            var anchorWidth = playerClass.GetWidth(graphicSpec.Graphic, weapon);
                            var prefix = string.Concat(playerClass.ClassCharacter, ArmorCharacters[armor], WeaponCharacters[weapon]);
                            var relativeSource = $"plrgfx/{playerClass.SpritePath}/{prefix}/{prefix}{graphicSpec.SourceSuffix}.cl2";
                            var sourcePath = ResolveSourcePath(sourceRoot, relativeSource);
                            var clipKey = $"classId={playerClass.ClassId} location={graphicSpec.Location} armor={armor} weapon={weapon} graphic={graphicSpec.Graphic}";
                            if (!File.Exists(sourcePath)) {
                                missing.Add($"{clipKey} source={relativeSource}");
                                continue;
                            }

                            var fileInfo = new FileInfo(sourcePath);
                            if (fileInfo.Length is < 1 or > NativeCl2SpriteSheetDecoder.MaximumInputBytes)
                                throw new InvalidDataException($"CL2 input '{relativeSource}' exceeds the supported size bounds.");
                            byte[] cl2Data;
                            NativeCl2Sheet sheet;
                            try {
                                cl2Data = File.ReadAllBytes(sourcePath);
                                sheet = NativeCl2SpriteSheetDecoder.Decode(cl2Data, anchorWidth, frameCount, allowAdditionalFrames: true);
                            } catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException) {
                                throw new InvalidDataException($"CL2 source '{relativeSource}' is malformed: {exception.Message}", exception);
                            }
                            byte[] png;
                            try {
                                var graphicTranslationPath = Path.ChangeExtension(sourcePath, ".trn");
                                var graphicTranslation = ReadOptionalTranslation(graphicTranslationPath, "graphic");
                                var palette = graphicSpec.Location == NativePlayerArtNames.TownLocation ? townPalette : cathedralPalette;
                                ReadOnlyMemory<byte>? graphicTranslationMemory = graphicTranslation is null
                                    ? (ReadOnlyMemory<byte>?)null
                                    : new ReadOnlyMemory<byte>(graphicTranslation);
                                ReadOnlyMemory<byte>? classTranslationMemory = classTranslation is null
                                    ? (ReadOnlyMemory<byte>?)null
                                    : new ReadOnlyMemory<byte>(classTranslation);
                                png = NativePlayerArtPng.EncodeAtlas(sheet, palette, graphicTranslationMemory, classTranslationMemory);
                            } catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException) {
                                throw new InvalidDataException($"Native player source '{relativeSource}' or its palette translation is malformed: {exception.Message}", exception);
                            }
                            packBytes = checked(packBytes + png.Length);
                            if (packBytes > MaximumPackPngBytes)
                                throw new InvalidDataException($"Generated player pack exceeds the {MaximumPackPngBytes} byte aggregate limit.");

                            var sourceFrameCount = sheet.Directions[0].Count;
                            var frameWidth = sheet.Directions.SelectMany(direction => direction).Max(frame => frame.Width);
                            var frameHeight = sheet.Directions.SelectMany(direction => direction).Max(frame => frame.Height);
                            var relativePng = $"sprites/{playerClass.ClassId}/{graphicSpec.Location}/{armor}/{weapon}/{graphicSpec.Graphic}.png";
                            var outputPath = ResolveOutputPath(catalogDirectory, relativePng);
                            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                            File.WriteAllBytes(outputPath, png);
                            AppendHash(packHash, relativePng, png);
                            clips.Add(new NativePlayerArtCatalogClip(
                                playerClass.ClassId,
                                graphicSpec.Location,
                                armor,
                                weapon,
                                graphicSpec.Graphic,
                                relativePng,
                                frameWidth,
                                frameHeight,
                                graphicSpec.Graphic switch {
                                    "town-stand" or "stand" => 4,
                                    "death" => 2,
                                    "block" => 3,
                                    _ => 1,
                                },
                                sourceFrameCount,
                                actionFrame,
                                anchorWidth,
                                frameCount));
                        }
                    }
                }
            }

            if (clips.Count == 0)
                throw new InvalidDataException("No valid native player CL2 assets were found; no pack was published.");

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            var identityDocument = JsonSerializer.SerializeToUtf8Bytes(new {
                schemaVersion = 1,
                contentManifestHashes = sortedContentHashes,
                levelBindings = sortedLevelBindings,
                clips,
                spells = metadata.Spells,
            }, jsonOptions);
            AppendHash(packHash, "catalog-metadata", ConvertLfToCrLf(identityDocument));
            var packHashHex = Convert.ToHexString(packHash.GetHashAndReset()).ToLowerInvariant();
            var packId = $"native-player-art-{packHashHex[..16]}";
            var catalog = new NativePlayerArtCatalog(
                1,
                packId,
                sortedContentHashes,
                sortedLevelBindings,
                clips,
                metadata.Spells);
            var catalogJson = ConvertLfToCrLf(JsonSerializer.SerializeToUtf8Bytes(catalog, jsonOptions));
            if (catalogJson.Length > MaximumCatalogBytes)
                throw new InvalidDataException($"Generated native player catalog exceeds the {MaximumCatalogBytes} byte limit.");
            File.WriteAllBytes(Path.Combine(catalogDirectory, "native-player-catalog.json"), catalogJson);

            var missingPath = Path.Combine(catalogDirectory, "missing-assets.txt");
            if (missing.Count != 0)
                File.WriteAllText(missingPath, string.Join("\r\n", missing.Order(StringComparer.Ordinal)) + "\r\n", new UTF8Encoding(false));
            if (diagnostics.Count != 0) {
                var diagnosticsPath = Path.Combine(catalogDirectory, "import-diagnostics.txt");
                File.WriteAllText(diagnosticsPath, string.Join("\r\n", diagnostics.Order(StringComparer.Ordinal)) + "\r\n", new UTF8Encoding(false));
            }

            Directory.Move(staging, outputRoot);
            return new NativePlayerArtImportResult(outputRoot, clips.Count, missing.AsReadOnly(), diagnostics.AsReadOnly(), packId);
        } catch {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    public static NativeCl2Sheet DecodeAndApplyTranslations(
        ReadOnlySpan<byte> data,
        int width,
        int expectedFramesPerDirection,
        byte[]? graphicTranslation,
        byte[]? classTranslation)
    {
        var sheet = NativeCl2SpriteSheetDecoder.Decode(data, width, expectedFramesPerDirection);
        ValidateTranslation(graphicTranslation, "graphic");
        ValidateTranslation(classTranslation, "class");
        var translated = new List<IReadOnlyList<NativeCl2Frame>>(sheet.Directions.Count);
        foreach (var direction in sheet.Directions) {
            var translatedFrames = new List<NativeCl2Frame>(direction.Count);
            foreach (var frame in direction) {
                var indices = frame.Indices.ToArray();
                for (var pixel = 0; pixel < indices.Length; pixel++) {
                    if (frame.Opacity[pixel] == 0)
                        continue;
                    if (graphicTranslation is not null)
                        indices[pixel] = graphicTranslation[indices[pixel]];
                    if (classTranslation is not null)
                        indices[pixel] = classTranslation[indices[pixel]];
                }
                translatedFrames.Add(frame with { Indices = indices });
            }
            translated.Add(translatedFrames);
        }
        return new NativeCl2Sheet(translated);
    }

    public static IReadOnlyList<string> ValidateContentManifestHashes(IReadOnlyList<string>? hashes)
    {
        if (hashes is null || hashes.Count is < 1 or > 64)
            throw new InvalidDataException("One to 64 content-manifest hashes are required.");
        var normalized = new List<string>(hashes.Count);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hash in hashes) {
            if (hash is null || !Sha256Pattern.IsMatch(hash) || !unique.Add(hash))
                throw new InvalidDataException("Content-manifest hashes must be unique lowercase 64-digit SHA-256 values.");
            normalized.Add(hash);
        }
        return normalized.OrderBy(hash => hash, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<NativePlayerArtLevelBinding> ValidateLevelBindings(
        IReadOnlyList<string>? admittedHashes,
        IReadOnlyList<NativePlayerArtLevelBinding>? bindings)
    {
        var normalizedHashes = ValidateContentManifestHashes(admittedHashes);
        if (bindings is null || bindings.Count is < 1 or > 4096)
            throw new InvalidDataException("One to 4096 explicit content level bindings are required.");
        var admitted = normalizedHashes.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<(string Hash, uint LevelId)>();
        foreach (var binding in bindings) {
            if (!admitted.Contains(binding.ContentManifestHash)
                || !seen.Add((binding.ContentManifestHash, binding.LevelId))
                || binding.Location is not "town" and not "cathedral")
                throw new InvalidDataException("Content level bindings must be unique, use admitted hashes, and name town or cathedral.");
        }
        foreach (var hash in normalizedHashes) {
            if (!bindings.Any(binding => string.Equals(binding.ContentManifestHash, hash, StringComparison.Ordinal)))
                throw new InvalidDataException($"Content manifest hash '{hash}' has no explicit level binding.");
        }
        return bindings
            .OrderBy(binding => binding.ContentManifestHash, StringComparer.Ordinal)
            .ThenBy(binding => binding.LevelId)
            .ToArray();
    }

    private static byte[] SerializeIndentedCrLf<T>(T value)
        => ConvertLfToCrLf(JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }));

    private static byte[] ConvertLfToCrLf(byte[] utf8)
    {
        var text = Encoding.UTF8.GetString(utf8).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        return new UTF8Encoding(false).GetBytes(text);
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

    private static string ResolveSourcePath(string sourceRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new InvalidDataException("Source paths must be nonempty relative paths.");
        var normalized = relativePath.Replace('\\', '/');
        var parts = normalized.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Any(char.IsControl)))
            throw new InvalidDataException($"Unsafe source path '{relativePath}'.");
        var fullPath = Path.GetFullPath(Path.Combine(sourceRoot, Path.Combine(parts)));
        if (!IsContainedPath(sourceRoot, fullPath))
            throw new InvalidDataException($"Source path '{relativePath}' escapes its input root.");
        return fullPath;
    }

    private static string ResolveOutputPath(string outputRoot, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsContainedPath(outputRoot, fullPath))
            throw new InvalidDataException($"Generated path '{relativePath}' escapes the output root.");
        return fullPath;
    }

    private static bool IsContainedPath(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static byte[]? ReadOptionalTranslation(string path, string description)
    {
        if (!File.Exists(path))
            return null;
        var translation = File.ReadAllBytes(path);
        if (translation.Length != NativePlayerPalette.ColorCount)
            throw new InvalidDataException($"{description} translation '{path}' must contain exactly 256 bytes; found {translation.Length}.");
        return translation;
    }

    private static void ValidateTranslation(byte[]? translation, string description)
    {
        if (translation is not null && translation.Length != NativePlayerPalette.ColorCount)
            throw new InvalidDataException($"{description} translation must contain exactly 256 bytes.");
    }

    private static void AppendHash(IncrementalHash hash, string name, ReadOnlySpan<byte> bytes)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(name));
        hash.AppendData(new byte[] { 0 });
        hash.AppendData(bytes);
        hash.AppendData(new byte[] { 0 });
    }

    private sealed record GraphicSpec(string Location, string Graphic, string SourceSuffix);
}
