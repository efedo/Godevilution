using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Tools.NativePlayerArt;

namespace Devilution.Tools.NativeSceneArt;

/** Imports explicitly extracted Town and Cathedral scene assets into a private, profile-bound client pack. */
public static class NativeSceneArtCompiler
{
    public const int MaximumCatalogBytes = 32 * 1024 * 1024;
    public const long MaximumPackPngBytes = 512L * 1024 * 1024;
    public const int MaximumSourceBytes = 128 * 1024 * 1024;
    public const long MaximumAggregateSourceBytes = 256L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static NativeSceneArtImportResult Import(NativeSceneArtImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sourceRoot = RequireDirectory(options.SourceRoot, "source root");
        var outputRoot = Path.GetFullPath(options.OutputRoot);
        if (Directory.Exists(outputRoot) || File.Exists(outputRoot))
            throw new IOException($"Output root already exists: {outputRoot}");

        var profiles = NativePlayerArtProfiles.LoadDirectories(options.ProfileDirectories);
        var levelBindings = profiles.LevelBindings
            .Select(binding => new NativeSceneArtLevelBinding(binding.ContentManifestHash, binding.LevelId, binding.Location))
            .ToArray();
        var inputFiles = new List<SourceFile>();
        long aggregateSourceBytes = 0;
        var townInputs = LoadFamilyInputs(sourceRoot, "town", options.TownPaletteRelativePath, inputFiles, ref aggregateSourceBytes);
        var cathedralInputs = LoadFamilyInputs(sourceRoot, "cathedral", options.CathedralPaletteRelativePath, inputFiles, ref aggregateSourceBytes);

        var town = DecodeFamily("town", townInputs, inputFiles);
        var cathedral = DecodeFamily("cathedral", cathedralInputs, inputFiles);
        var sourceHashes = inputFiles.Select(file => NativeSceneSourceHash.FromBytes(file.RelativePath, file.Bytes))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();

        var outputParent = Path.GetDirectoryName(outputRoot)
            ?? throw new InvalidDataException("Output root must have a parent directory.");
        var outputLeaf = Path.GetFileName(outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(outputLeaf))
            throw new InvalidDataException("Output root must name a new directory.");
        Directory.CreateDirectory(outputParent);
        var staging = Path.Combine(outputParent, $".{outputLeaf}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try {
            var totalPngBytes = 0L;
            var familyCatalogs = new[] {
                BuildFamilyCatalog(town, sourceHashes, staging, ref totalPngBytes),
                BuildFamilyCatalog(cathedral, sourceHashes, staging, ref totalPngBytes),
            };
            var catalogPayload = new NativeSceneArtCatalog(
                1,
                string.Empty,
                profiles.ContentManifestHashes,
                levelBindings,
                familyCatalogs);
            var packId = BuildPackId(catalogPayload);
            var catalog = catalogPayload with { PackId = packId };
            var json = SerializeIndentedCrLf(catalog);
            if (json.Length > MaximumCatalogBytes)
                throw new InvalidDataException($"Native scene catalog exceeds the {MaximumCatalogBytes}-byte limit.");
            File.WriteAllBytes(Path.Combine(staging, "native-scene-catalog.json"), json);
            Directory.Move(staging, outputRoot);

            return new NativeSceneArtImportResult(
                outputRoot,
                packId,
                checked(town.Decoded.Pieces.Count + cathedral.Decoded.Pieces.Count),
                checked(town.Decoded.Frames.Count + cathedral.Decoded.Frames.Count),
                checked(town.Decoded.Specials.Count + cathedral.Decoded.Specials.Count));
        } catch {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    private static DecodedSourceFamily DecodeFamily(string location, FamilyInputs inputs, IReadOnlyList<SourceFile> allInputs)
    {
        try {
            var decoded = NativeSceneArtDecoder.DecodeFamily(
                location,
                inputs.Cel.Bytes,
                inputs.Min.Bytes,
                inputs.Til.Bytes,
                inputs.Sol.Bytes,
                inputs.Palette.Bytes);
            var specials = NativeSceneArtDecoder.DecodeSpecials(inputs.Specials.Bytes);
            decoded = decoded with { Specials = specials };
            var sources = allInputs.Where(file => file.Location == location)
                .Select(file => NativeSceneSourceHash.FromBytes(file.RelativePath, file.Bytes))
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .ToArray();
            return new DecodedSourceFamily(decoded, sources);
        } catch (Exception exception) when (exception is InvalidDataException or OverflowException or ArgumentException) {
            throw new InvalidDataException($"Native {location} scene inputs are invalid: {exception.Message}", exception);
        }
    }

    private static NativeSceneArtFamilyCatalog BuildFamilyCatalog(
        DecodedSourceFamily source,
        IReadOnlyList<NativeSceneSourceHash> allSourceHashes,
        string stagingDirectory,
        ref long totalPngBytes)
    {
        var family = source.Decoded;
        var atlases = new List<NativeSceneArtAtlas>();
        var frameSet = new RasterSet();
        var floorSet = new RasterSet();
        var contentSet = new RasterSet();
        var foliageSet = new RasterSet();
        var frameRasterIds = new Dictionary<ushort, int>();
        var foliageRasterIds = new Dictionary<ushort, int>();
        var floorRasterIds = new Dictionary<(uint Piece, byte Slot), int>();
        var contentRasterIds = new Dictionary<(uint Piece, byte Slot), (int Index, bool Foliage)>();

        foreach (var frame in family.Frames) {
            frameRasterIds.Add(frame.SourceFrameId, frameSet.Add(frame.StoredRaster));
            if (frame.FoliageRaster is not null)
                foliageRasterIds.Add(frame.SourceFrameId, foliageSet.Add(frame.FoliageRaster));
        }
        foreach (var piece in family.Pieces) {
            foreach (var slot in piece.Slots) {
                var key = (piece.PieceId, slot.SlotIndex);
                if (slot.FloorRaster is not null)
                    floorRasterIds.Add(key, floorSet.Add(slot.FloorRaster));
                if (slot.ContentRaster is not null) {
                    var isFoliage = slot.DrawFoliage;
                    var index = isFoliage ? foliageSet.Add(slot.ContentRaster) : contentSet.Add(slot.ContentRaster);
                    contentRasterIds.Add(key, (index, isFoliage));
                }
            }
        }

        var frameReferences = PackFixedRasterSet(
            "frames", family.Location, frameSet, family.PaletteRgb, stagingDirectory, atlases, ref totalPngBytes);
        var floorReferences = PackFixedRasterSet(
            "floor", family.Location, floorSet, family.PaletteRgb, stagingDirectory, atlases, ref totalPngBytes);
        var contentReferences = PackFixedRasterSet(
            "content", family.Location, contentSet, family.PaletteRgb, stagingDirectory, atlases, ref totalPngBytes);
        var foliageReferences = PackFixedRasterSet(
            "foliage", family.Location, foliageSet, family.PaletteRgb, stagingDirectory, atlases, ref totalPngBytes);
        var specialReferences = PackSpecials(
            family.Location, family.Specials, family.PaletteRgb, stagingDirectory, atlases, ref totalPngBytes);

        var frameCatalog = family.Frames.Select(frame => new NativeSceneArtFrame(
            frame.SourceFrameId,
            frame.AdjustedFrameId,
            frame.FirstUsePieceId,
            frame.FirstUseSlotIndex,
            TileTypeName(frame.SourceType),
            TileTypeName(frame.StoredType),
            frame.IsFloorTransform,
            frameReferences[frameRasterIds[frame.SourceFrameId]],
            frame.FoliageRaster is null ? null : foliageReferences[foliageRasterIds[frame.SourceFrameId]])).ToArray();

        var dPieceCatalog = family.Pieces.Select(piece => new NativeSceneArtDPiece(
            piece.PieceId,
            piece.Properties,
            piece.IsFloorPiece ? "floor" : "content")).ToArray();
        var frameTypes = family.Frames.ToDictionary(frame => frame.SourceFrameId, frame => TileTypeName(frame.SourceType));
        var minSlotCatalog = family.Pieces.SelectMany(piece => piece.Slots.Select(slot => {
                var key = (piece.PieceId, slot.SlotIndex);
                NativeSceneRasterReference? floorReference = null;
                NativeSceneRasterReference? rasterReference = null;
                var role = "none";
                if (floorRasterIds.TryGetValue(key, out var floorRasterId))
                    floorReference = floorReferences[floorRasterId];
                if (contentRasterIds.TryGetValue(key, out var contentRaster)) {
                    var isFoliage = contentRaster.Foliage;
                    role = isFoliage ? "foliage" : "content";
                    rasterReference = isFoliage
                        ? foliageReferences[contentRaster.Index]
                        : contentReferences[contentRaster.Index];
                }
                return new NativeSceneArtSlot(
                    piece.PieceId,
                    slot.SlotIndex,
                    slot.SourceRawWord,
                    slot.AdjustedRawWord,
                    slot.SourceFrameId,
                    slot.AdjustedFrameId,
                    TileTypeName(slot.SourceType),
                    slot.SourceFrameId == 0 ? "none" : frameTypes[slot.SourceFrameId],
                    TileTypeName(slot.DrawType),
                    role,
                    floorReference,
                    rasterReference);
            })).ToArray();

        var specials = family.Specials.Select((raster, index) => new NativeSceneArtSpecial(
            checked((uint)index),
            specialReferences[index],
            "bottom-left",
            0,
            family.Location == "town" ? -32 : 0,
            "after-actors",
            family.Location == "town" ? -1 : 0,
            family.Location == "town" ? -1 : 0)).ToArray();

        return new NativeSceneArtFamilyCatalog(
            family.Location,
            family.MicroTileLength,
            family.Pieces.Count,
            source.Sources,
            atlases,
            family.MegaTiles,
            frameCatalog,
            dPieceCatalog,
            minSlotCatalog,
            specials,
            "after-actors");
    }

    private static IReadOnlyList<NativeSceneRasterReference> PackFixedRasterSet(
        string kind,
        string location,
        RasterSet rasters,
        byte[] palette,
        string stagingDirectory,
        List<NativeSceneArtAtlas> atlases,
        ref long totalPngBytes)
    {
        var references = new NativeSceneRasterReference[rasters.Rasters.Count];
        const int cellWidth = NativeSceneArtDecoder.FrameWidth;
        const int cellHeight = NativeSceneArtDecoder.FrameHeight;
        const int maximumCellsPerAtlas = 4096;
        const int columnsPerAtlas = 128;
        for (var start = 0; start < rasters.Rasters.Count; start += maximumCellsPerAtlas) {
            var count = Math.Min(maximumCellsPerAtlas, rasters.Rasters.Count - start);
            var cells = rasters.Rasters.Skip(start).Take(count).ToArray();
            var columns = Math.Min(columnsPerAtlas, count);
            var rows = checked((count + columns - 1) / columns);
            var atlasIndex = atlases.Count;
            var relativePath = $"sprites/{location}/{kind}-{atlasIndex:D3}.png";
            var bytes = NativeSceneArtPng.EncodeAtlas(cells, columns, cellWidth, cellHeight, 0, palette);
            totalPngBytes = checked(totalPngBytes + bytes.LongLength);
            if (totalPngBytes > MaximumPackPngBytes)
                throw new InvalidDataException($"Native scene atlases exceed the {MaximumPackPngBytes}-byte pack limit.");
            var absolutePath = GetOutputPath(stagingDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllBytes(absolutePath, bytes);
            var width = checked(columns * cellWidth);
            var height = checked(rows * cellHeight);
            atlases.Add(new NativeSceneArtAtlas(atlasIndex, kind, relativePath, width, height, cellWidth, cellHeight, count,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            for (var localIndex = 0; localIndex < count; localIndex++) {
                var globalIndex = start + localIndex;
                references[globalIndex] = new NativeSceneRasterReference(
                    atlasIndex,
                    localIndex % columns * cellWidth,
                    localIndex / columns * cellHeight,
                    rasters.Rasters[globalIndex].Width,
                    rasters.Rasters[globalIndex].Height);
            }
        }
        return references;
    }

    private static IReadOnlyList<NativeSceneRasterReference> PackSpecials(
        string location,
        IReadOnlyList<NativeSceneRaster> sprites,
        byte[] palette,
        string stagingDirectory,
        List<NativeSceneArtAtlas> atlases,
        ref long totalPngBytes)
    {
        var references = new NativeSceneRasterReference[sprites.Count];
        var start = 0;
        while (start < sprites.Count) {
            var count = 0;
            var cellHeight = 0;
            var columns = 1;
            var rows = 1;
            while (start + count < sprites.Count) {
                var proposedHeight = Math.Max(cellHeight, sprites[start + count].Height);
                var proposedCount = count + 1;
                var proposedColumns = Math.Min(128, proposedCount);
                var proposedRows = checked((proposedCount + proposedColumns - 1) / proposedColumns);
                var width = checked(proposedColumns * 64);
                var height = checked(proposedRows * proposedHeight);
                if (width > NativeSceneArtPng.MaximumAtlasDimension
                    || height > NativeSceneArtPng.MaximumAtlasDimension
                    || checked((long)width * height) > NativeSceneArtPng.MaximumAtlasPixels)
                    break;
                count = proposedCount;
                cellHeight = proposedHeight;
                columns = proposedColumns;
                rows = proposedRows;
            }
            if (count == 0)
                throw new InvalidDataException($"Native {location} special sprite {start} cannot fit in a bounded atlas.");

            var cells = sprites.Skip(start).Take(count).ToArray();
            var atlasIndex = atlases.Count;
            var relativePath = $"sprites/{location}/specials-{atlasIndex:D3}.png";
            var bytes = NativeSceneArtPng.EncodeAtlas(cells, columns, 64, cellHeight, 0, palette, bottomAlignCells: true);
            totalPngBytes = checked(totalPngBytes + bytes.LongLength);
            if (totalPngBytes > MaximumPackPngBytes)
                throw new InvalidDataException($"Native scene atlases exceed the {MaximumPackPngBytes}-byte pack limit.");
            var absolutePath = GetOutputPath(stagingDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllBytes(absolutePath, bytes);
            var atlasWidth = checked(columns * 64);
            var atlasHeight = checked(rows * cellHeight);
            atlases.Add(new NativeSceneArtAtlas(atlasIndex, "specials", relativePath, atlasWidth, atlasHeight, 64, cellHeight, count,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            for (var localIndex = 0; localIndex < count; localIndex++) {
                var sprite = sprites[start + localIndex];
                var x = localIndex % columns * 64;
                var y = localIndex / columns * cellHeight + cellHeight - sprite.Height;
                references[start + localIndex] = new NativeSceneRasterReference(atlasIndex, x, y, 64, sprite.Height);
            }
            start += count;
        }
        return references;
    }

    private static FamilyInputs LoadFamilyInputs(
        string sourceRoot,
        string location,
        string paletteRelativePath,
        List<SourceFile> sources,
        ref long aggregateSourceBytes)
    {
        var town = location == "town";
        var cel = ReadRequired(sourceRoot, town ? SelectTownSourcePath("town.cel", path => File.Exists(GetSourcePath(sourceRoot, path))) : "levels/l1data/l1.cel", location, sources, ref aggregateSourceBytes);
        var min = ReadRequired(sourceRoot, town ? SelectTownSourcePath("town.min", path => File.Exists(GetSourcePath(sourceRoot, path))) : "levels/l1data/l1.min", location, sources, ref aggregateSourceBytes);
        var til = ReadRequired(sourceRoot, town ? SelectTownSourcePath("town.til", path => File.Exists(GetSourcePath(sourceRoot, path))) : "levels/l1data/l1.til", location, sources, ref aggregateSourceBytes);
        var sol = ReadRequired(sourceRoot, town ? SelectTownSourcePath("town.sol", path => File.Exists(GetSourcePath(sourceRoot, path))) : "levels/l1data/l1.sol", location, sources, ref aggregateSourceBytes);
        var palette = ReadRequired(sourceRoot, paletteRelativePath, location, sources, ref aggregateSourceBytes,
            expectedLength: NativeSceneArtDecoder.PaletteByteCount);
        var specials = ReadRequired(sourceRoot,
            location == "town" ? "levels/towndata/towns.cel" : "levels/l1data/l1s.cel", location, sources, ref aggregateSourceBytes);
        return new FamilyInputs(cel, min, til, sol, palette, specials);
    }

    internal static string SelectTownSourcePath(string sourceFileName, Func<string, bool> exists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentNullException.ThrowIfNull(exists);
        if (sourceFileName is "." or ".." || Path.IsPathRooted(sourceFileName)
            || sourceFileName.Contains('/') || sourceFileName.Contains('\\') || sourceFileName.Contains(':')
            || sourceFileName.Any(char.IsControl))
            throw new InvalidDataException("Native Town source names must be single relative filenames.");

        var newerPath = $"nlevels/towndata/{sourceFileName}";
        return exists(newerPath) ? newerPath : $"levels/towndata/{sourceFileName}";
    }

    private static SourceFile ReadRequired(
        string sourceRoot,
        string relativePath,
        string location,
        List<SourceFile> sources,
        ref long aggregateSourceBytes,
        long? expectedLength = null)
    {
        var fullPath = ResolveSourcePath(sourceRoot, relativePath);
        var file = ReadResolved(fullPath, relativePath, location, ref aggregateSourceBytes, expectedLength);
        sources.Add(file);
        return file;
    }

    private static SourceFile ReadResolved(
        string fullPath,
        string relativePath,
        string location,
        ref long aggregateSourceBytes,
        long? expectedLength)
    {
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length < 1 || fileInfo.Length > GetMaximumSourceLength(relativePath, location))
            throw new InvalidDataException($"Native {location} source '{relativePath}' is outside the supported size bound.");
        if (expectedLength is not null && fileInfo.Length != expectedLength.Value)
            throw new InvalidDataException($"Native {location} source '{relativePath}' must contain exactly {expectedLength.Value} bytes.");
        ValidateSourceLength(relativePath, location, fileInfo.Length);
        if (fileInfo.Length > MaximumAggregateSourceBytes - aggregateSourceBytes)
            throw new InvalidDataException($"Native scene source files exceed the {MaximumAggregateSourceBytes}-byte aggregate limit.");
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Native {location} source '{relativePath}' cannot be a reparse point.");
        try {
            var bytes = File.ReadAllBytes(fullPath);
            aggregateSourceBytes = checked(aggregateSourceBytes + bytes.LongLength);
            return new SourceFile(relativePath.Replace('\\', '/'), location, bytes);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            throw new IOException($"Unable to read native {location} source '{relativePath}': {exception.Message}", exception);
        }
    }

    private static long GetMaximumSourceLength(string relativePath, string location)
        => Path.GetExtension(relativePath).ToLowerInvariant() switch {
            ".pal" => NativeSceneArtDecoder.PaletteByteCount,
            ".sol" => NativeSceneArtDecoder.NativePieceCount,
            ".min" => checked((long)NativeSceneArtDecoder.NativePieceCount * (location == "town" ? 16 : 10) * sizeof(ushort)),
            ".til" => 65_535L * 8,
            _ => MaximumSourceBytes,
        };

    private static void ValidateSourceLength(string relativePath, string location, long length)
    {
        switch (Path.GetExtension(relativePath).ToLowerInvariant()) {
        case ".pal":
            if (length != NativeSceneArtDecoder.PaletteByteCount)
                throw new InvalidDataException($"Native {location} palette '{relativePath}' must contain exactly {NativeSceneArtDecoder.PaletteByteCount} bytes.");
            break;
        case ".sol":
            if (length > NativeSceneArtDecoder.NativePieceCount)
                throw new InvalidDataException($"Native {location} SOL '{relativePath}' exceeds {NativeSceneArtDecoder.NativePieceCount} bytes.");
            break;
        case ".min": {
            var microTileLength = location == "town" ? 16 : 10;
            var pieceByteLength = checked(microTileLength * sizeof(ushort));
            if (length < pieceByteLength || length % pieceByteLength != 0)
                throw new InvalidDataException($"Native {location} MIN '{relativePath}' must contain complete d-piece records.");
            break;
        }
        case ".til":
            if (length < 8 || length % 8 != 0)
                throw new InvalidDataException($"Native {location} TIL '{relativePath}' must contain complete eight-byte mega tiles.");
            break;
        }
    }

    private static string ResolveSourcePath(string sourceRoot, string relativePath)
    {
        var normalized = NormalizeSafeRelativePath(relativePath);
        var fullPath = GetSourcePath(sourceRoot, normalized);
        var segments = normalized.Split('/');
        var rootPrefix = sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException($"Native scene input path '{relativePath}' escapes its source root.");
        if (!File.Exists(fullPath)) {
            throw new FileNotFoundException($"Native scene input is missing: {relativePath}", fullPath);
        }

        var cursor = sourceRoot;
        foreach (var segment in segments) {
            cursor = Path.Combine(cursor, segment);
            if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Native scene input path '{relativePath}' traverses a reparse point.");
        }
        return fullPath;
    }

    private static string GetSourcePath(string sourceRoot, string relativePath)
    {
        var segments = NormalizeSafeRelativePath(relativePath).Split('/');
        return Path.GetFullPath(Path.Combine(sourceRoot, Path.Combine(segments)));
    }

    private static string NormalizeSafeRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = relativePath.Replace('\\', '/');
        var segments = normalized.Split('/');
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':')
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl)))
            throw new InvalidDataException($"Native scene path '{relativePath}' is not a safe relative path.");
        return string.Join('/', segments);
    }

    private static string RequireDirectory(string path, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Native scene {description} does not exist: {fullPath}");
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Native scene {description} cannot be a reparse point.");
        return fullPath;
    }

    private static string GetOutputPath(string root, string relativePath)
    {
        var segments = NormalizeSafeRelativePath(relativePath).Split('/');
        var fullPath = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Native scene output path escapes its staging directory.");
        return fullPath;
    }

    private static string BuildPackId(NativeSceneArtCatalog catalogPayload)
    {
        var canonicalOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var canonicalPayload = JsonSerializer.SerializeToUtf8Bytes(catalogPayload with { PackId = string.Empty }, canonicalOptions);
        var digest = Convert.ToHexString(SHA256.HashData(canonicalPayload)).ToLowerInvariant();
        return $"native-scene-{digest[..24]}";
    }

    private static byte[] SerializeIndentedCrLf(NativeSceneArtCatalog catalog)
    {
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        var crlf = json.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\n', '\r')
            .Replace("\r", "\r\n", StringComparison.Ordinal) + "\r\n";
        return Encoding.UTF8.GetBytes(crlf);
    }

    private static string TileTypeName(NativeSceneTileType type)
        => type switch {
            NativeSceneTileType.Square => "square",
            NativeSceneTileType.TransparentSquare => "transparent-square",
            NativeSceneTileType.LeftTriangle => "left-triangle",
            NativeSceneTileType.RightTriangle => "right-triangle",
            NativeSceneTileType.LeftTrapezoid => "left-trapezoid",
            NativeSceneTileType.RightTrapezoid => "right-trapezoid",
            _ => throw new InvalidDataException($"Unsupported native tile type {(byte)type}."),
        };

    private sealed record SourceFile(string RelativePath, string Location, byte[] Bytes);
    private sealed record FamilyInputs(SourceFile Cel, SourceFile Min, SourceFile Til, SourceFile Sol, SourceFile Palette, SourceFile Specials);
    private sealed record DecodedSourceFamily(NativeSceneDecodedFamily Decoded, IReadOnlyList<NativeSceneSourceHash> Sources);

    private sealed class RasterSet
    {
        private readonly Dictionary<string, List<int>> indexesByHash = new(StringComparer.Ordinal);
        public List<NativeSceneRaster> Rasters { get; } = [];

        public int Add(NativeSceneRaster raster)
        {
            raster.Validate("Native scene atlas raster");
            var key = Convert.ToHexString(SHA256.HashData(Concat(raster.Indices, raster.Opacity))).ToLowerInvariant();
            if (indexesByHash.TryGetValue(key, out var candidates)) {
                foreach (var candidate in candidates) {
                    var previous = Rasters[candidate];
                    if (previous.Width == raster.Width && previous.Height == raster.Height
                        && previous.Indices.AsSpan().SequenceEqual(raster.Indices)
                        && previous.Opacity.AsSpan().SequenceEqual(raster.Opacity))
                        return candidate;
                }
            } else {
                candidates = [];
                indexesByHash.Add(key, candidates);
            }
            var index = Rasters.Count;
            Rasters.Add(raster);
            candidates.Add(index);
            return index;
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var result = new byte[checked(first.Length + second.Length)];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}
