using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;

namespace Devilution.Client.PresentationAssets;

public enum NativeSceneLocation
{
    Town,
    Cathedral,
}

public enum NativeSceneTileType
{
    Square,
    TransparentSquare,
    LeftTriangle,
    RightTriangle,
    LeftTrapezoid,
    RightTrapezoid,
}

public enum NativeSceneSlotRole
{
    None,
    Content,
    Foliage,
}

public sealed record NativeSceneLevelBindingKey(string ContentManifestHash, uint LevelId);

public sealed record NativeSceneLevelBinding(
    NativeSceneLevelBindingKey Key,
    NativeSceneLocation Location);

public sealed record NativeSceneSourceHash(string Path, string Sha256);

public sealed record NativeSceneAtlas(
    int Id,
    string Kind,
    string Path,
    int Width,
    int Height,
    int CellWidth,
    int CellHeight,
    int CellCount,
    string Sha256);

public sealed record NativeSceneRasterReference(
    int AtlasId,
    int X,
    int Y,
    int Width,
    int Height);

public sealed record NativeSceneMegaTile(uint MegaTileId, IReadOnlyList<ushort> PieceIds);

public sealed record NativeSceneFrame(
    ushort SourceFrameId,
    ushort AdjustedFrameId,
    uint FirstUsePieceId,
    byte FirstUseSlotIndex,
    NativeSceneTileType SourceType,
    NativeSceneTileType StoredType,
    bool IsFloorTransform,
    NativeSceneRasterReference StoredRaster,
    NativeSceneRasterReference? FoliageRaster);

public sealed record NativeSceneDPiece(uint PieceId, byte SolFlags, string FloorClass);

public sealed record NativeSceneSlot(
    uint PieceId,
    byte SlotIndex,
    ushort SourceWord,
    ushort RemappedWord,
    ushort SourceFrameId,
    ushort AdjustedFrameId,
    NativeSceneTileType SourceType,
    NativeSceneTileType? FirstUseFrameType,
    NativeSceneTileType EffectiveType,
    bool IsFloorBase,
    bool DrawsFoliage,
    NativeSceneSlotRole Role,
    NativeSceneRasterReference? FloorRaster,
    NativeSceneRasterReference? Raster);

public sealed record NativeSceneSpecial(
    uint SpecialId,
    NativeSceneRasterReference Raster,
    string Anchor,
    int AnchorX,
    int AnchorY,
    string DrawPhase,
    int SourceTileOffsetX,
    int SourceTileOffsetY);

/** Complete validated Town or Cathedral CEL/MIN/TIL scene family. */
public sealed class NativeSceneFamily
{
    private readonly IReadOnlyDictionary<uint, NativeSceneDPiece> piecesById;
    private readonly IReadOnlyDictionary<uint, IReadOnlyList<NativeSceneSlot>> slotsByPieceId;
    private readonly IReadOnlyDictionary<uint, NativeSceneMegaTile> megaTilesById;
    private readonly IReadOnlyDictionary<uint, NativeSceneSpecial> specialsById;
    private readonly IReadOnlyDictionary<int, NativeSceneAtlas> atlasesById;

    internal NativeSceneFamily(
        NativeSceneLocation location,
        byte microTileLength,
        int pieceCount,
        IReadOnlyList<NativeSceneSourceHash> sourceHashes,
        IReadOnlyList<NativeSceneAtlas> atlases,
        IReadOnlyList<NativeSceneMegaTile> megaTiles,
        IReadOnlyList<NativeSceneFrame> frames,
        IReadOnlyList<NativeSceneDPiece> pieces,
        IReadOnlyList<NativeSceneSlot> slots,
        IReadOnlyList<NativeSceneSpecial> specials,
        string drawPhase)
    {
        Location = location;
        MicroTileLength = microTileLength;
        PieceCount = pieceCount;
        SourceHashes = Array.AsReadOnly(sourceHashes.ToArray());
        Atlases = Array.AsReadOnly(atlases.ToArray());
        MegaTiles = Array.AsReadOnly(megaTiles.ToArray());
        Frames = Array.AsReadOnly(frames.ToArray());
        DPieces = Array.AsReadOnly(pieces.ToArray());
        MinSlots = Array.AsReadOnly(slots.ToArray());
        Specials = Array.AsReadOnly(specials.ToArray());
        DrawPhase = drawPhase;
        piecesById = pieces.ToFrozenDictionary(piece => piece.PieceId);
        slotsByPieceId = slots.GroupBy(slot => slot.PieceId)
            .ToFrozenDictionary(group => group.Key,
                group => (IReadOnlyList<NativeSceneSlot>)Array.AsReadOnly(
                    group.OrderBy(slot => slot.SlotIndex).ToArray()));
        megaTilesById = megaTiles.ToFrozenDictionary(tile => tile.MegaTileId);
        specialsById = specials.ToFrozenDictionary(special => special.SpecialId);
        atlasesById = atlases.ToFrozenDictionary(atlas => atlas.Id);
    }

    public NativeSceneLocation Location { get; }
    public byte MicroTileLength { get; }
    public int PieceCount { get; }
    public IReadOnlyList<NativeSceneSourceHash> SourceHashes { get; }
    public IReadOnlyList<NativeSceneAtlas> Atlases { get; }
    public IReadOnlyList<NativeSceneMegaTile> MegaTiles { get; }
    public IReadOnlyList<NativeSceneFrame> Frames { get; }
    public IReadOnlyList<NativeSceneDPiece> DPieces { get; }
    public IReadOnlyList<NativeSceneSlot> MinSlots { get; }
    public IReadOnlyList<NativeSceneSpecial> Specials { get; }
    public string DrawPhase { get; }

    public bool TryGetDPiece(uint pieceId, out NativeSceneDPiece piece)
        => piecesById.TryGetValue(pieceId, out piece!);

    public IReadOnlyList<NativeSceneSlot> GetSlots(uint pieceId)
        => slotsByPieceId.TryGetValue(pieceId, out var slots) ? slots : Array.Empty<NativeSceneSlot>();

    public bool TryGetMegaTile(uint megaTileId, out NativeSceneMegaTile megaTile)
        => megaTilesById.TryGetValue(megaTileId, out megaTile!);

    /** The protocol special layer uses zero for no sprite and one-based sprite IDs. */
    public bool TryGetSpecial(uint protocolSpecialId, out NativeSceneSpecial special)
    {
        special = null!;
        return protocolSpecialId > 0
            && specialsById.TryGetValue(protocolSpecialId - 1, out special!);
    }

    public bool TryGetAtlas(int atlasId, out NativeSceneAtlas atlas)
        => atlasesById.TryGetValue(atlasId, out atlas!);

    public bool HasValidFinePieceLayer(int width, int height, IReadOnlyList<uint> pieceIds)
    {
        ArgumentNullException.ThrowIfNull(pieceIds);
        return width > 0 && height > 0
            && (long)width * height <= 131072
            && pieceIds.Count == (long)width * height
            && pieceIds.All(pieceId => pieceId < (uint)PieceCount);
    }

    public bool HasValidSpecialLayer(int width, int height, IReadOnlyList<uint> specialIds)
    {
        ArgumentNullException.ThrowIfNull(specialIds);
        if (width <= 0 || height <= 0 || (long)width * height > 131072)
            return false;
        var cellCount = checked(width * height);
        return specialIds.Count == 0
            || specialIds.Count == cellCount
                && specialIds.All(specialId => specialId == 0 || TryGetSpecial(specialId, out _));
    }
}

/** Immutable client-only scene pack, explicitly bound to admitted content and level identities. */
public sealed class NativeSceneCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumDocumentCharacters = 32 * 1024 * 1024;
    public const int MaximumContentManifestHashCount = 64;
    public const int MaximumLevelBindingCount = 4096;
    public const int MaximumSourceHashCount = 64;
    public const int MaximumAtlasCountPerFamily = 256;
    public const int MaximumMegaTileCount = 4096;
    public const int MaximumFrameCount = 4096;
    public const int MaximumSpecialCount = 4096;
    public const int MaximumAtlasDimension = 8192;
    public const long MaximumAtlasPixels = 4L * 1024 * 1024;
    public const int MaximumAtlasFileBytes = 128 * 1024 * 1024;

    private static readonly string[] RootProperties = [
        "schemaVersion", "packId", "contentManifestHashes", "levelBindings", "families",
    ];
    private static readonly string[] LevelBindingProperties = ["contentManifestHash", "levelId", "location"];
    private static readonly string[] FamilyProperties = [
        "location", "microTileLength", "pieceCount", "sourceHashes", "atlases", "megaTiles",
        "frames", "dPieces", "minSlots", "specials", "drawPhase",
    ];
    private static readonly string[] SourceHashProperties = ["path", "sha256"];
    private static readonly string[] AtlasProperties = [
        "id", "kind", "path", "width", "height", "cellWidth", "cellHeight", "cellCount", "sha256",
    ];
    private static readonly string[] MegaTileProperties = ["megaTileId", "pieceIds"];
    private static readonly string[] FrameProperties = [
        "sourceFrameId", "adjustedFrameId", "firstUsePieceId", "firstUseSlotIndex", "sourceType",
        "storedType", "isFloorTransform", "storedRaster", "foliageRaster",
    ];
    private static readonly string[] DPieceProperties = ["pieceId", "solFlags", "floorClass"];
    private static readonly string[] SlotProperties = [
        "pieceId", "slot", "sourceWord", "remappedWord", "sourceFrameId", "adjustedFrameId", "sourceType",
        "firstUseType", "effectiveType", "floorRaster", "raster", "role",
    ];
    private static readonly string[] SpecialProperties = [
        "specialId", "raster", "anchor", "anchorX", "anchorY", "drawPhase", "sourceTileOffsetX", "sourceTileOffsetY",
    ];
    private static readonly string[] RasterProperties = ["atlasId", "x", "y", "width", "height"];

    private readonly IReadOnlySet<string> contentManifestHashes;
    private readonly IReadOnlyDictionary<NativeSceneLevelBindingKey, NativeSceneLocation> levelBindings;
    private readonly IReadOnlyDictionary<NativeSceneLocation, NativeSceneFamily> families;

    private NativeSceneCatalog(
        string packId,
        IReadOnlySet<string> contentManifestHashes,
        IReadOnlyDictionary<NativeSceneLevelBindingKey, NativeSceneLocation> levelBindings,
        IReadOnlyDictionary<NativeSceneLocation, NativeSceneFamily> families)
    {
        PackId = packId;
        this.contentManifestHashes = contentManifestHashes;
        this.levelBindings = levelBindings;
        this.families = families;
    }

    public string PackId { get; }
    public IReadOnlySet<string> ContentManifestHashes => contentManifestHashes;
    public IReadOnlyDictionary<NativeSceneLevelBindingKey, NativeSceneLocation> LevelBindings => levelBindings;
    public IReadOnlyDictionary<NativeSceneLocation, NativeSceneFamily> Families => families;

    public bool SupportsContentManifestHash(string? hash)
        => hash is not null && contentManifestHashes.Contains(hash);

    public bool TryResolveFamily(string? contentManifestHash, uint levelId, out NativeSceneFamily family)
    {
        family = null!;
        if (contentManifestHash is null
            || !levelBindings.TryGetValue(new NativeSceneLevelBindingKey(contentManifestHash, levelId), out var location))
            return false;
        return families.TryGetValue(location, out family!);
    }

    /** Parses the native scene art compiler's schema without touching Godot resources. */
    public static bool TryParse(
        string? json,
        out NativeSceneCatalog? catalog,
        out IReadOnlyList<string> diagnostics)
    {
        catalog = null;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) {
            errors.Add("The native scene catalog is empty.");
            diagnostics = errors.AsReadOnly();
            return false;
        }
        if (json.Length > MaximumDocumentCharacters) {
            errors.Add("The native scene catalog exceeds the 32 MiB size limit.");
            diagnostics = errors.AsReadOnly();
            return false;
        }

        try {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                errors.Add("The native scene catalog root must be an object.");
                diagnostics = errors.AsReadOnly();
                return false;
            }
            if (!ValidateProperties(root, "catalog", RootProperties, errors)) {
                diagnostics = errors.AsReadOnly();
                return false;
            }
            if (!TryGetInt32(root, "schemaVersion", out var schemaVersion)
                || schemaVersion != CurrentSchemaVersion)
                errors.Add($"The native scene catalog schemaVersion must be {CurrentSchemaVersion}.");
            if (!TryGetString(root, "packId", out var packId) || !IsStableId(packId))
                errors.Add("The native scene catalog packId must be a stable lowercase identifier.");
            if (!TryCalculatePackId(root, out var calculatedPackId))
                errors.Add("The native scene catalog packId could not be calculated from its content.");
            else if (!string.Equals(packId, calculatedPackId, StringComparison.Ordinal))
                errors.Add("The native scene catalog packId does not match its content hash.");

            var hashes = new HashSet<string>(StringComparer.Ordinal);
            if (!TryGetArray(root, "contentManifestHashes", out var hashesElement)) {
                errors.Add("The native scene catalog must contain a contentManifestHashes array.");
            } else if (hashesElement.GetArrayLength() is < 1 or > MaximumContentManifestHashCount) {
                errors.Add($"The native scene catalog must bind between 1 and {MaximumContentManifestHashCount} content hashes.");
            } else {
                var index = 0;
                foreach (var entry in hashesElement.EnumerateArray()) {
                    if (entry.ValueKind != JsonValueKind.String || !IsSha256(entry.GetString()))
                        errors.Add($"Native scene contentManifestHashes[{index}] must be a lowercase SHA-256 hash.");
                    else if (!hashes.Add(entry.GetString()!))
                        errors.Add($"Native scene content hash '{entry.GetString()}' is repeated.");
                    index++;
                }
            }

            var bindings = new Dictionary<NativeSceneLevelBindingKey, NativeSceneLocation>();
            if (!TryGetArray(root, "levelBindings", out var bindingsElement)) {
                errors.Add("The native scene catalog must contain a levelBindings array.");
            } else if (bindingsElement.GetArrayLength() is < 1 or > MaximumLevelBindingCount) {
                errors.Add($"The native scene catalog must contain 1..{MaximumLevelBindingCount} level bindings.");
            } else {
                var index = 0;
                foreach (var entry in bindingsElement.EnumerateArray()) {
                    if (!TryParseLevelBinding(entry, index++, out var key, out var location, out var error)) {
                        errors.Add(error);
                        continue;
                    }
                    if (!hashes.Contains(key.ContentManifestHash)) {
                        errors.Add($"Native scene level binding {key.ContentManifestHash}/{key.LevelId} references an unlisted content hash.");
                        continue;
                    }
                    if (!bindings.TryAdd(key, location))
                        errors.Add($"Native scene level binding {key.ContentManifestHash}/{key.LevelId} is repeated.");
                }
            }

            var families = new Dictionary<NativeSceneLocation, NativeSceneFamily>();
            if (!TryGetArray(root, "families", out var familiesElement)) {
                errors.Add("The native scene catalog must contain a families array.");
            } else if (familiesElement.GetArrayLength() != 2) {
                errors.Add("The native scene catalog must contain exactly the Town and Cathedral families.");
            } else {
                var index = 0;
                foreach (var entry in familiesElement.EnumerateArray()) {
                    if (!TryParseFamily(entry, index++, out var family, out var error)) {
                        errors.Add(error);
                        continue;
                    }
                    if (!families.TryAdd(family!.Location, family))
                        errors.Add($"Native scene family '{FormatLocation(family.Location)}' is repeated.");
                }
                if (!families.ContainsKey(NativeSceneLocation.Town)
                    || !families.ContainsKey(NativeSceneLocation.Cathedral))
                    errors.Add("The native scene catalog must include one Town and one Cathedral family.");
            }

            if (errors.Count > 0) {
                diagnostics = errors.AsReadOnly();
                return false;
            }
            catalog = new NativeSceneCatalog(
                packId!,
                hashes.ToFrozenSet(StringComparer.Ordinal),
                new ReadOnlyDictionary<NativeSceneLevelBindingKey, NativeSceneLocation>(bindings),
                new ReadOnlyDictionary<NativeSceneLocation, NativeSceneFamily>(families));
            diagnostics = Array.Empty<string>();
            return true;
        } catch (JsonException exception) {
            errors.Add($"The native scene catalog is not valid JSON: {exception.Message}");
        } catch (InvalidOperationException exception) {
            errors.Add($"The native scene catalog has an invalid JSON shape: {exception.Message}");
        } catch (OverflowException exception) {
            errors.Add($"The native scene catalog exceeds supported numeric bounds: {exception.Message}");
        }
        diagnostics = errors.AsReadOnly();
        return false;
    }

    /** Cell order from native Direction::East traversal: diagonal sum, then ascending x. */
    public static IReadOnlyList<(int X, int Y)> ResolveIsometricCellOrder(int width, int height)
    {
        if (width <= 0 || height <= 0 || checked((long)width * height) > 131072)
            throw new ArgumentOutOfRangeException(nameof(width));
        return Enumerable.Range(0, checked(width * height))
            .Select(cell => (X: cell % width, Y: cell / width))
            .OrderBy(cell => cell.X + cell.Y)
            .ThenBy(cell => cell.X)
            .ThenBy(cell => cell.Y)
            .ToArray();
    }

    /** Calculates the schema-1 content-derived ID over compact UTF-8 JSON with packId blank. */
    public static string CalculatePackId(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !TryCalculatePackId(document.RootElement, out var packId))
            throw new ArgumentException("The catalog JSON must be an object with a packId property.", nameof(json));
        return packId;
    }

    private static bool TryCalculatePackId(JsonElement root, out string packId)
    {
        packId = string.Empty;
        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Count(property => property.NameEquals("packId")) != 1)
            return false;
        using var buffer = new MemoryStream();
        var found = false;
        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject()) {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("packId")) {
                    found = true;
                    writer.WriteStringValue(string.Empty);
                } else {
                    property.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        if (!found)
            return false;
        var digest = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
        packId = $"native-scene-{digest[..24]}";
        return true;
    }

    private static bool TryParseLevelBinding(
        JsonElement entry,
        int index,
        out NativeSceneLevelBindingKey key,
        out NativeSceneLocation location,
        out string error)
    {
        key = default!;
        location = default;
        error = string.Empty;
        var path = $"levelBindings[{index}]";
        if (entry.ValueKind != JsonValueKind.Object) {
            error = $"Native scene {path} must be an object.";
            return false;
        }
        if (!ValidateProperties(entry, path, LevelBindingProperties, out error))
            return false;
        if (!TryGetString(entry, "contentManifestHash", out var hash) || !IsSha256(hash)) {
            error = $"Native scene {path} contentManifestHash must be a lowercase SHA-256 hash.";
            return false;
        }
        if (!TryGetUInt32(entry, "levelId", out var levelId)) {
            error = $"Native scene {path} levelId must be a uint32.";
            return false;
        }
        if (!TryGetLocation(entry, "location", out location)) {
            error = $"Native scene {path} location must be town or cathedral.";
            return false;
        }
        key = new NativeSceneLevelBindingKey(hash!, levelId);
        return true;
    }

    private static bool TryParseFamily(
        JsonElement entry,
        int index,
        out NativeSceneFamily? family,
        out string error)
    {
        family = null;
        error = string.Empty;
        var path = $"families[{index}]";
        if (entry.ValueKind != JsonValueKind.Object) {
            error = $"Native scene {path} must be an object.";
            return false;
        }
        if (!ValidateProperties(entry, path, FamilyProperties, out error))
            return false;
        if (!TryGetLocation(entry, "location", out var location)) {
            error = $"Native scene {path} location must be town or cathedral.";
            return false;
        }
        if (!TryGetByte(entry, "microTileLength", out var microTileLength)
            || microTileLength != (location == NativeSceneLocation.Town ? 16 : 10)) {
            error = $"Native scene {path} microTileLength does not match the native location.";
            return false;
        }
        if (!TryGetInt32(entry, "pieceCount", out var pieceCount) || pieceCount is < 1 or > 1379) {
            error = $"Native scene {path} pieceCount must be in 1..1379.";
            return false;
        }
        if (!TryGetString(entry, "drawPhase", out var drawPhase) || drawPhase != "after-actors") {
            error = $"Native scene {path} drawPhase must be after-actors.";
            return false;
        }

        if (!TryParseSourceHashes(entry, path, out var sourceHashes, out error)
            || !TryParseAtlases(entry, path, location, out var atlases, out var atlasesById, out error)
            || !TryParseMegaTiles(entry, path, pieceCount, out var megaTiles, out error)
            || !TryParseFrames(entry, path, pieceCount, microTileLength, atlasesById, out var frames, out var framesById, out error)
            || !TryParseDPieces(entry, path, pieceCount, out var pieces, out var piecesById, out error)
            || !TryParseSlots(entry, path, pieceCount, microTileLength, piecesById, framesById, atlasesById, out var slots, out error)
            || !TryParseSpecials(entry, path, location, atlasesById, out var specials, out error))
            return false;

        family = new NativeSceneFamily(
            location,
            microTileLength,
            pieceCount,
            sourceHashes,
            atlases,
            megaTiles,
            frames,
            pieces,
            slots,
            specials,
            drawPhase!);
        return true;
    }

    private static bool TryParseSourceHashes(
        JsonElement family,
        string path,
        out IReadOnlyList<NativeSceneSourceHash> sourceHashes,
        out string error)
    {
        sourceHashes = Array.Empty<NativeSceneSourceHash>();
        error = string.Empty;
        if (!TryGetArray(family, "sourceHashes", out var entries)
            || entries.GetArrayLength() is < 1 or > MaximumSourceHashCount) {
            error = $"Native scene {path} sourceHashes must contain 1..{MaximumSourceHashCount} entries.";
            return false;
        }
        var parsed = new List<NativeSceneSourceHash>(entries.GetArrayLength());
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            var itemPath = $"{path}.sourceHashes[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object) {
                error = $"Native scene {itemPath} must be an object.";
                return false;
            }
            if (!ValidateProperties(entry, itemPath, SourceHashProperties, out error))
                return false;
            if (!TryGetString(entry, "path", out var sourcePath)) {
                error = $"Native scene {itemPath} path is missing or empty.";
                return false;
            }
            if (!PresentationAssetPath.TryNormalizeRelativePath(sourcePath, out var normalized, out var pathError)) {
                error = $"Native scene {itemPath} has an invalid source path: {pathError}.";
                return false;
            }
            if (!TryGetString(entry, "sha256", out var hash) || !IsSha256(hash)) {
                error = $"Native scene {itemPath} sha256 must be a lowercase SHA-256 hash.";
                return false;
            }
            if (!paths.Add(normalized)) {
                error = $"Native scene source path '{normalized}' is repeated in {path}.";
                return false;
            }
            parsed.Add(new NativeSceneSourceHash(normalized, hash!));
        }
        sourceHashes = parsed.AsReadOnly();
        return true;

    }

    private static bool TryParseAtlases(
        JsonElement family,
        string path,
        NativeSceneLocation location,
        out IReadOnlyList<NativeSceneAtlas> atlases,
        out IReadOnlyDictionary<int, NativeSceneAtlas> byId,
        out string error)
    {
        atlases = Array.Empty<NativeSceneAtlas>();
        byId = new Dictionary<int, NativeSceneAtlas>();
        error = string.Empty;
        if (!TryGetArray(family, "atlases", out var entries)
            || entries.GetArrayLength() is < 1 or > MaximumAtlasCountPerFamily) {
            error = $"Native scene {path} atlases must contain 1..{MaximumAtlasCountPerFamily} entries.";
            return false;
        }
        var parsed = new List<NativeSceneAtlas>(entries.GetArrayLength());
        var ids = new Dictionary<int, NativeSceneAtlas>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            var itemPath = $"{path}.atlases[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object) {
                error = $"Native scene {itemPath} must be an object.";
                return false;
            }
            if (!ValidateProperties(entry, itemPath, AtlasProperties, out error))
                return false;
            if (!TryGetInt32(entry, "id", out var id) || id < 0) {
                error = $"Native scene {itemPath} id must be a non-negative integer.";
                return false;
            }
            if (!TryGetString(entry, "kind", out var kind)
                || kind is not ("frames" or "floor" or "content" or "foliage" or "specials")) {
                error = $"Native scene {itemPath} kind is unsupported.";
                return false;
            }
            if (!TryGetString(entry, "path", out var resourcePath)) {
                error = $"Native scene {itemPath} path is missing or empty.";
                return false;
            }
            if (!PresentationAssetPath.TryNormalizeRelativePath(resourcePath, out var normalizedPath, out var pathError)) {
                error = $"Native scene {itemPath} has an invalid resource path: {pathError}.";
                return false;
            }
            if (!normalizedPath.StartsWith($"sprites/{FormatLocation(location)}/", StringComparison.Ordinal)) {
                error = $"Native scene {itemPath} path must be under the matching location's sprites directory.";
                return false;
            }
            if (!TryGetPositiveInt32(entry, "width", out var width)
                || !TryGetPositiveInt32(entry, "height", out var height)
                || !TryGetPositiveInt32(entry, "cellWidth", out var cellWidth)
                || !TryGetPositiveInt32(entry, "cellHeight", out var cellHeight)
                || !TryGetInt32(entry, "cellCount", out var cellCount)
                || width > MaximumAtlasDimension || height > MaximumAtlasDimension
                || (long)width * height > MaximumAtlasPixels
                || cellWidth > width || cellHeight > height
                || cellCount is < 1 or > 4096
                || (long)((width + cellWidth - 1) / cellWidth)
                    * ((height + cellHeight - 1) / cellHeight) < cellCount) {
                error = $"Native scene {itemPath} dimensions or cell metadata exceed bounds.";
                return false;
            }
            if (!TryGetString(entry, "sha256", out var sha256) || !IsSha256(sha256)) {
                error = $"Native scene {itemPath} sha256 must be a lowercase SHA-256 hash.";
                return false;
            }
            var atlas = new NativeSceneAtlas(id, kind!, normalizedPath, width, height,
                cellWidth, cellHeight, cellCount, sha256!);
            if (!ids.TryAdd(id, atlas)) {
                error = $"Native scene atlas id {id} is repeated in {path}.";
                return false;
            }
            if (!paths.Add(normalizedPath)) {
                error = $"Native scene atlas path '{normalizedPath}' is repeated in {path}.";
                return false;
            }
            parsed.Add(atlas);
        }
        if (ids.Count != 0 && Enumerable.Range(0, ids.Count).Any(id => !ids.ContainsKey(id))) {
            error = $"Native scene {path} atlas IDs must be zero-based and contiguous.";
            return false;
        }
        atlases = parsed.AsReadOnly();
        byId = new ReadOnlyDictionary<int, NativeSceneAtlas>(ids);
        return true;
    }

    private static bool TryParseMegaTiles(
        JsonElement family,
        string path,
        int pieceCount,
        out IReadOnlyList<NativeSceneMegaTile> megaTiles,
        out string error)
    {
        megaTiles = Array.Empty<NativeSceneMegaTile>();
        error = string.Empty;
        if (!TryGetArray(family, "megaTiles", out var entries)
            || entries.GetArrayLength() is < 1 or > MaximumMegaTileCount) {
            error = $"Native scene {path} megaTiles must contain 1..{MaximumMegaTileCount} entries.";
            return false;
        }
        var parsed = new List<NativeSceneMegaTile>(entries.GetArrayLength());
        var ids = new HashSet<uint>();
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            var itemPath = $"{path}.megaTiles[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object) {
                error = $"Native scene {itemPath} must be an object.";
                return false;
            }
            if (!ValidateProperties(entry, itemPath, MegaTileProperties, out error))
                return false;
            if (!TryGetUInt32(entry, "megaTileId", out var megaTileId)) {
                error = $"Native scene {itemPath} megaTileId must be a uint32.";
                return false;
            }
            if (!TryGetArray(entry, "pieceIds", out var pieceIds) || pieceIds.GetArrayLength() != 4) {
                error = $"Native scene {itemPath} pieceIds must contain four d-piece IDs.";
                return false;
            }
            var pieces = new ushort[4];
            var pieceIndex = 0;
            foreach (var piece in pieceIds.EnumerateArray()) {
                if (piece.ValueKind != JsonValueKind.Number
                    || !piece.TryGetUInt16(out pieces[pieceIndex])
                    || pieces[pieceIndex] >= pieceCount) {
                    error = $"Native scene {itemPath} references a d-piece outside pieceCount.";
                    return false;
                }
                pieceIndex++;
            }
            if (!ids.Add(megaTileId)) {
                error = $"Native scene mega-tile ID {megaTileId} is repeated in {path}.";
                return false;
            }
            parsed.Add(new NativeSceneMegaTile(megaTileId, Array.AsReadOnly(pieces)));
        }
        megaTiles = parsed.AsReadOnly();
        return true;
    }

    private static bool TryParseFrames(
        JsonElement family,
        string path,
        int pieceCount,
        byte microTileLength,
        IReadOnlyDictionary<int, NativeSceneAtlas> atlases,
        out IReadOnlyList<NativeSceneFrame> frames,
        out IReadOnlyDictionary<ushort, NativeSceneFrame> byId,
        out string error)
    {
        frames = Array.Empty<NativeSceneFrame>();
        byId = new Dictionary<ushort, NativeSceneFrame>();
        error = string.Empty;
        if (!TryGetArray(family, "frames", out var entries) || entries.GetArrayLength() > MaximumFrameCount) {
            error = $"Native scene {path} frames must be an array of at most {MaximumFrameCount} entries.";
            return false;
        }
        var parsed = new List<NativeSceneFrame>(entries.GetArrayLength());
        var ids = new Dictionary<ushort, NativeSceneFrame>();
        var adjusted = new HashSet<ushort>();
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            var itemPath = $"{path}.frames[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object) {
                error = $"Native scene {itemPath} must be an object.";
                return false;
            }
            if (!ValidateProperties(entry, itemPath, FrameProperties, out error))
                return false;
            if (!TryGetUInt16(entry, "sourceFrameId", out var sourceId) || sourceId == 0
                || !TryGetUInt16(entry, "adjustedFrameId", out var adjustedId) || adjustedId == 0
                || !TryGetUInt32(entry, "firstUsePieceId", out var firstUsePiece) || firstUsePiece >= pieceCount
                || !TryGetByte(entry, "firstUseSlotIndex", out var firstUseSlot) || firstUseSlot >= microTileLength
                || !TryGetTileType(entry, "sourceType", out var sourceType)
                || !TryGetTileType(entry, "storedType", out var storedType)
                || !TryGetBoolean(entry, "isFloorTransform", out var isFloorTransform)) {
                error = $"Native scene {itemPath} frame metadata is invalid.";
                return false;
            }
            if (!TryGetRaster(entry, "storedRaster", false, itemPath, atlases, out var storedRaster, out error))
                return false;
            if (!TryGetRaster(entry, "foliageRaster", true, itemPath, atlases, out var foliageRaster, out error))
                return false;
            if (storedRaster is null || AtlasKind(atlases, storedRaster.AtlasId) != "frames"
                || (isFloorTransform != (foliageRaster is not null))
                || (foliageRaster is not null && AtlasKind(atlases, foliageRaster.AtlasId) != "foliage")) {
                error = $"Native scene {itemPath} frame raster roles are inconsistent.";
                return false;
            }
            if (storedRaster.Width != 32 || storedRaster.Height != 32
                || (foliageRaster is not null && (foliageRaster.Width != 32 || foliageRaster.Height != 16))) {
                error = $"Native scene {itemPath} raster dimensions do not match native microtiles.";
                return false;
            }
            var frame = new NativeSceneFrame(sourceId, adjustedId, firstUsePiece, firstUseSlot,
                sourceType, storedType, isFloorTransform, storedRaster, foliageRaster);
            if (!ids.TryAdd(sourceId, frame) || !adjusted.Add(adjustedId)) {
                error = $"Native scene frame IDs are repeated in {path}.";
                return false;
            }
            parsed.Add(frame);
        }
        frames = parsed.AsReadOnly();
        byId = new ReadOnlyDictionary<ushort, NativeSceneFrame>(ids);
        return true;
    }

    private static bool TryParseDPieces(
        JsonElement family,
        string path,
        int pieceCount,
        out IReadOnlyList<NativeSceneDPiece> pieces,
        out IReadOnlyDictionary<uint, NativeSceneDPiece> byId,
        out string error)
    {
        pieces = Array.Empty<NativeSceneDPiece>();
        byId = new Dictionary<uint, NativeSceneDPiece>();
        error = string.Empty;
        if (!TryGetArray(family, "dPieces", out var entries) || entries.GetArrayLength() != pieceCount) {
            error = $"Native scene {path} dPieces must contain exactly pieceCount entries.";
            return false;
        }
        var parsed = new List<NativeSceneDPiece>(pieceCount);
        var ids = new Dictionary<uint, NativeSceneDPiece>();
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            var itemPath = $"{path}.dPieces[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object) {
                error = $"Native scene {itemPath} must be an object.";
                return false;
            }
            if (!ValidateProperties(entry, itemPath, DPieceProperties, out error))
                return false;
            if (!TryGetUInt32(entry, "pieceId", out var pieceId) || pieceId >= pieceCount
                || !TryGetByte(entry, "solFlags", out var solFlags)
                || !TryGetString(entry, "floorClass", out var floorClass)
                || floorClass is not ("floor" or "content")) {
                error = $"Native scene {itemPath} d-piece metadata is invalid.";
                return false;
            }
            var isFloor = (solFlags & (1 | 4)) == 0;
            if ((floorClass == "floor") != isFloor) {
                error = $"Native scene {itemPath} floorClass conflicts with the .sol flags.";
                return false;
            }
            var piece = new NativeSceneDPiece(pieceId, solFlags, floorClass!);
            if (!ids.TryAdd(pieceId, piece)) {
                error = $"Native scene d-piece ID {pieceId} is repeated in {path}.";
                return false;
            }
            parsed.Add(piece);
        }
        if (ids.Count != pieceCount || Enumerable.Range(0, pieceCount).Any(id => !ids.ContainsKey((uint)id))) {
            error = $"Native scene {path} d-piece IDs must cover 0 through {pieceCount - 1}.";
            return false;
        }
        pieces = parsed.AsReadOnly();
        byId = new ReadOnlyDictionary<uint, NativeSceneDPiece>(ids);
        return true;
    }

    private static bool TryParseSlots(
        JsonElement family,
        string path,
        int pieceCount,
        byte microTileLength,
        IReadOnlyDictionary<uint, NativeSceneDPiece> pieces,
        IReadOnlyDictionary<ushort, NativeSceneFrame> frames,
        IReadOnlyDictionary<int, NativeSceneAtlas> atlases,
        out IReadOnlyList<NativeSceneSlot> slots,
        out string error)
    {
        slots = Array.Empty<NativeSceneSlot>();
        error = string.Empty;
        var expectedCount = checked(pieceCount * microTileLength);
        if (!TryGetArray(family, "minSlots", out var entries) || entries.GetArrayLength() != expectedCount) {
            error = $"Native scene {path} minSlots must contain exactly pieceCount × microTileLength entries.";
            return false;
        }
        var parsed = new List<NativeSceneSlot>(expectedCount);
        var keys = new HashSet<(uint Piece, byte Slot)>();
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            var itemPath = $"{path}.minSlots[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object) {
                error = $"Native scene {itemPath} must be an object.";
                return false;
            }
            if (!ValidateProperties(entry, itemPath, SlotProperties, out error))
                return false;
            if (!TryGetUInt32(entry, "pieceId", out var pieceId) || pieceId >= pieceCount
                || !TryGetByte(entry, "slot", out var slotIndex) || slotIndex >= microTileLength
                || !TryGetUInt16(entry, "sourceWord", out var sourceWord)
                || !TryGetUInt16(entry, "remappedWord", out var remappedWord)
                || !TryGetUInt16(entry, "sourceFrameId", out var sourceFrameId)
                || !TryGetUInt16(entry, "adjustedFrameId", out var adjustedFrameId)
                || !TryGetTileType(entry, "sourceType", out var sourceType)
                || !TryGetTileType(entry, "effectiveType", out var effectiveType)
                || !TryGetString(entry, "firstUseType", out var firstUseTypeName)
                || !TryGetRole(entry, "role", out var role)
                || !pieces.TryGetValue(pieceId, out var piece)) {
                error = $"Native scene {itemPath} slot metadata is invalid.";
                return false;
            }
            if (!keys.Add((pieceId, slotIndex))) {
                error = $"Native scene {itemPath} repeats piece/slot {pieceId}/{slotIndex}.";
                return false;
            }
            if (!TryGetRaster(entry, "floorRaster", true, itemPath, atlases, out var floorRaster, out error)
                || !TryGetRaster(entry, "raster", true, itemPath, atlases, out var raster, out error))
                return false;
            if (sourceWord == 0) {
                if (remappedWord != 0 || sourceFrameId != 0 || adjustedFrameId != 0
                    || sourceType != NativeSceneTileType.Square
                    || firstUseTypeName != "none"
                    || effectiveType != NativeSceneTileType.Square
                    || role != NativeSceneSlotRole.None || floorRaster is not null || raster is not null) {
                    error = $"Native scene {itemPath} empty MIN slot has non-empty frame data.";
                    return false;
                }
            } else {
                if (!TryParseTileType(firstUseTypeName, out var firstUseType)) {
                    error = $"Native scene {itemPath} firstUseType is invalid.";
                    return false;
                }
                var sourceFrameFromWord = (ushort)(sourceWord & 0x0FFF);
                var adjustedFrameFromWord = (ushort)(remappedWord & 0x0FFF);
                if (sourceFrameId == 0 || adjustedFrameId == 0
                    || sourceFrameFromWord != sourceFrameId
                    || adjustedFrameFromWord != adjustedFrameId
                    || ((sourceWord >> 12) & 0x7) != (uint)sourceType
                    || !frames.TryGetValue(sourceFrameId, out var frame)
                    || frame.AdjustedFrameId != adjustedFrameId
                    || frame.SourceType != firstUseType) {
                    error = $"Native scene {itemPath} source/remapped words conflict with frame metadata.";
                    return false;
                }
                var floorBase = piece.FloorClass == "floor" && slotIndex < 2;
                var drawsFoliageExpected = floorBase && sourceType == NativeSceneTileType.TransparentSquare;
                var expectedType = floorBase
                    ? slotIndex == 0 ? NativeSceneTileType.LeftTriangle : NativeSceneTileType.RightTriangle
                    : sourceType;
                var expectedRole = floorBase
                    ? drawsFoliageExpected ? NativeSceneSlotRole.Foliage : NativeSceneSlotRole.None
                    : NativeSceneSlotRole.Content;
                if (effectiveType != expectedType
                    || role != expectedRole
                    || floorBase != (floorRaster is not null)
                    || (expectedRole == NativeSceneSlotRole.None) != (raster is null)) {
                    error = $"Native scene {itemPath} role or floor raster conflicts with native MIN semantics.";
                    return false;
                }
                if (floorRaster is not null
                    && (AtlasKind(atlases, floorRaster.AtlasId) != "floor"
                        || floorRaster.Width != 32 || floorRaster.Height != 32)) {
                    error = $"Native scene {itemPath} floor raster must be a 32x32 floor atlas cell.";
                    return false;
                }
                if (raster is not null) {
                    var expectedAtlasKind = role == NativeSceneSlotRole.Foliage ? "foliage" : "content";
                    if (AtlasKind(atlases, raster.AtlasId) != expectedAtlasKind
                        || raster.Width != 32
                        || raster.Height != (role == NativeSceneSlotRole.Foliage ? 16 : 32)) {
                        error = $"Native scene {itemPath} raster does not match its declared role.";
                        return false;
                    }
                }
            }
            var inferredFloorBase = sourceWord != 0 && piece.FloorClass == "floor" && slotIndex < 2;
            var inferredFoliage = inferredFloorBase && sourceType == NativeSceneTileType.TransparentSquare;
            NativeSceneTileType? inferredFirstUseType = null;
            if (sourceWord != 0) {
                TryParseTileType(firstUseTypeName, out var parsedFirstUseType);
                inferredFirstUseType = parsedFirstUseType;
            }
            parsed.Add(new NativeSceneSlot(pieceId, slotIndex, sourceWord, remappedWord,
                sourceFrameId, adjustedFrameId, sourceType, inferredFirstUseType, effectiveType,
                inferredFloorBase, inferredFoliage, role, floorRaster, raster));
        }
        if (keys.Count != expectedCount) {
            error = $"Native scene {path} does not cover every MIN piece/slot pair.";
            return false;
        }
        slots = parsed.AsReadOnly();
        return true;
    }

    private static bool TryParseSpecials(
        JsonElement family,
        string path,
        NativeSceneLocation location,
        IReadOnlyDictionary<int, NativeSceneAtlas> atlases,
        out IReadOnlyList<NativeSceneSpecial> specials,
        out string error)
    {
        specials = Array.Empty<NativeSceneSpecial>();
        error = string.Empty;
        if (!TryGetArray(family, "specials", out var entries)
            || entries.GetArrayLength() is < 1 or > MaximumSpecialCount) {
            error = $"Native scene {path} specials must contain 1..{MaximumSpecialCount} entries.";
            return false;
        }
        var parsed = new List<NativeSceneSpecial>(entries.GetArrayLength());
        var ids = new HashSet<uint>();
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            var itemPath = $"{path}.specials[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object) {
                error = $"Native scene {itemPath} must be an object.";
                return false;
            }
            if (!ValidateProperties(entry, itemPath, SpecialProperties, out error))
                return false;
            if (!TryGetUInt32(entry, "specialId", out var specialId)
                || !TryGetString(entry, "anchor", out var anchor)
                || anchor != "bottom-left"
                || !TryGetInt32(entry, "anchorX", out var anchorX)
                || !TryGetInt32(entry, "anchorY", out var anchorY)
                || !TryGetString(entry, "drawPhase", out var drawPhase)
                || drawPhase != "after-actors"
                || !TryGetInt32(entry, "sourceTileOffsetX", out var offsetX)
                || !TryGetInt32(entry, "sourceTileOffsetY", out var offsetY)) {
                error = $"Native scene {itemPath} anchor or draw metadata is invalid.";
                return false;
            }
            var expectedOffset = location == NativeSceneLocation.Town ? -1 : 0;
            var expectedAnchorY = location == NativeSceneLocation.Town ? -32 : 0;
            if (anchorX != 0 || anchorY != expectedAnchorY
                || offsetX != expectedOffset || offsetY != expectedOffset
                || !TryGetRaster(entry, "raster", false, itemPath, atlases, out var raster, out error)
                || raster is null
                || AtlasKind(atlases, raster.AtlasId) != "specials"
                || raster.Width != 64) {
                error = $"Native scene {itemPath} special raster or native offset is invalid.";
                return false;
            }
            if (!ids.Add(specialId)) {
                error = $"Native scene special ID {specialId} is repeated in {path}.";
                return false;
            }
            parsed.Add(new NativeSceneSpecial(specialId, raster, anchor!, anchorX, anchorY,
                drawPhase!, offsetX, offsetY));
        }
        if (ids.Count != 0 && Enumerable.Range(0, ids.Count).Any(id => !ids.Contains((uint)id))) {
            error = $"Native scene {path} special IDs must be zero-based and contiguous.";
            return false;
        }
        specials = parsed.AsReadOnly();
        return true;
    }

    private static bool TryGetRaster(
        JsonElement owner,
        string propertyName,
        bool nullable,
        string path,
        IReadOnlyDictionary<int, NativeSceneAtlas> atlases,
        out NativeSceneRasterReference? raster,
        out string error)
    {
        raster = null;
        error = string.Empty;
        if (!owner.TryGetProperty(propertyName, out var entry)) {
            error = $"Native scene {path}.{propertyName} is missing.";
            return false;
        }
        if (entry.ValueKind == JsonValueKind.Null) {
            if (nullable)
                return true;
            error = $"Native scene {path}.{propertyName} cannot be null.";
            return false;
        }
        var rasterPath = $"{path}.{propertyName}";
        if (entry.ValueKind != JsonValueKind.Object) {
            error = $"Native scene {rasterPath} must be an object.";
            return false;
        }
        if (!ValidateProperties(entry, rasterPath, RasterProperties, out error))
            return false;
        if (!TryGetInt32(entry, "atlasId", out var atlasId)
            || !atlases.TryGetValue(atlasId, out var atlas)
            || !TryGetInt32(entry, "x", out var x)
            || !TryGetInt32(entry, "y", out var y)
            || !TryGetPositiveInt32(entry, "width", out var width)
            || !TryGetPositiveInt32(entry, "height", out var height)
            || x < 0 || y < 0 || x > atlas.Width - width || y > atlas.Height - height) {
            error = $"Native scene {rasterPath} atlas region is invalid or outside its atlas.";
            return false;
        }
        raster = new NativeSceneRasterReference(atlasId, x, y, width, height);
        return true;
    }

    private static string? AtlasKind(IReadOnlyDictionary<int, NativeSceneAtlas> atlases, int atlasId)
        => atlases.TryGetValue(atlasId, out var atlas) ? atlas.Kind : null;

    private static bool ValidateProperties(
        JsonElement element,
        string path,
        IReadOnlyCollection<string> allowed,
        out string error)
    {
        error = string.Empty;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject()) {
            if (!seen.Add(property.Name)) {
                error = $"The native scene catalog repeats property '{property.Name}' at {path}.";
                return false;
            }
            if (!allowed.Contains(property.Name, StringComparer.Ordinal)) {
                error = $"The native scene catalog has unknown property '{property.Name}' at {path}.";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateProperties(
        JsonElement element,
        string path,
        IReadOnlyCollection<string> allowed,
        List<string> errors)
    {
        if (ValidateProperties(element, path, allowed, out var error))
            return true;
        errors.Add(error);
        return false;
    }

    private static bool TryGetArray(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array;
    }

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString();
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryGetInt32(JsonElement element, string name, out int value)
    {
        value = default;
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryGetPositiveInt32(JsonElement element, string name, out int value)
        => TryGetInt32(element, name, out value) && value > 0;

    private static bool TryGetUInt32(JsonElement element, string name, out uint value)
    {
        value = default;
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetUInt32(out value);
    }

    private static bool TryGetUInt16(JsonElement element, string name, out ushort value)
    {
        value = default;
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetUInt16(out value);
    }

    private static bool TryGetByte(JsonElement element, string name, out byte value)
    {
        value = default;
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetByte(out value);
    }

    private static bool TryGetBoolean(JsonElement element, string name, out bool value)
    {
        value = default;
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetLocation(JsonElement element, string name, out NativeSceneLocation location)
    {
        location = default;
        if (!TryGetString(element, name, out var value))
            return false;
        location = value switch {
            "town" => NativeSceneLocation.Town,
            "cathedral" => NativeSceneLocation.Cathedral,
            _ => default,
        };
        return value is "town" or "cathedral";
    }

    private static bool TryGetTileType(JsonElement element, string name, out NativeSceneTileType type)
    {
        type = default;
        if (!TryGetString(element, name, out var value))
            return false;
        return TryParseTileType(value, out type);
    }

    private static bool TryParseTileType(string? value, out NativeSceneTileType type)
    {
        type = default;
        type = value switch {
            "square" => NativeSceneTileType.Square,
            "transparent-square" => NativeSceneTileType.TransparentSquare,
            "left-triangle" => NativeSceneTileType.LeftTriangle,
            "right-triangle" => NativeSceneTileType.RightTriangle,
            "left-trapezoid" => NativeSceneTileType.LeftTrapezoid,
            "right-trapezoid" => NativeSceneTileType.RightTrapezoid,
            _ => default,
        };
        return value is "square" or "transparent-square" or "left-triangle" or "right-triangle"
            or "left-trapezoid" or "right-trapezoid";
    }

    private static bool TryGetRole(JsonElement element, string name, out NativeSceneSlotRole role)
    {
        role = default;
        if (!TryGetString(element, name, out var value))
            return false;
        role = value switch {
            "none" => NativeSceneSlotRole.None,
            "content" => NativeSceneSlotRole.Content,
            "foliage" => NativeSceneSlotRole.Foliage,
            _ => default,
        };
        return value is "none" or "content" or "foliage";
    }

    private static bool IsStableId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64 || value[0] is < 'a' or > 'z')
            return false;
        return value.Skip(1).All(character => character is >= 'a' and <= 'z'
            or >= '0' and <= '9' or '.' or '-' or '_');
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 }
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string FormatLocation(NativeSceneLocation location)
        => location == NativeSceneLocation.Town ? "town" : "cathedral";
}
