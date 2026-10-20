using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Devilution.Client.PresentationAssets;

public sealed record NativeObjectProfileBinding(string ContentManifestHash, uint LevelId, string Location);

public sealed record NativeObjectSourceHash(string Path, string Sha256);

public sealed record NativeObjectAtlas(int Id, string Path, int Width, int Height, string Sha256);

public sealed record NativeObjectFrame(int AtlasId, int X, int Y, int Width, int Height, int NativeWidth, uint NativeFrame);

public sealed record NativeObjectSpriteFrame(int NativeFrame, int AtlasId, int X, int Y, int Width, int Height);

public sealed class NativeObjectSprite
{
    private readonly IReadOnlyList<NativeObjectSpriteFrame> frames;

    internal NativeObjectSprite(int id, string sourcePath, int nativeWidth, int sourceFrameCount, IReadOnlyList<NativeObjectSpriteFrame> frames)
    {
        Id = id;
        SourcePath = sourcePath;
        NativeWidth = nativeWidth;
        SourceFrameCount = sourceFrameCount;
        this.frames = Array.AsReadOnly(frames.ToArray());
    }

    public int Id { get; }
    public string SourcePath { get; }
    public int NativeWidth { get; }
    public int SourceFrameCount { get; }
    public IReadOnlyList<NativeObjectSpriteFrame> Frames => frames;

    internal bool TryResolveFrame(uint oneBasedFrame, out NativeObjectFrame frame)
    {
        if (oneBasedFrame == 0 || oneBasedFrame > frames.Count) {
            frame = null!;
            return false;
        }
        var resolved = frames[checked((int)oneBasedFrame - 1)];
        frame = new NativeObjectFrame(resolved.AtlasId, resolved.X, resolved.Y, resolved.Width, resolved.Height, NativeWidth, oneBasedFrame);
        return true;
    }
}

public sealed record NativeObjectDefinition(
    uint NativeObjectId,
    int SpriteId,
    int AnimDelay,
    int AnimLen,
    int AnimWidth,
    bool Animated);

/** Immutable, strict catalog for the original L1 door, chest, sarcophagus, trap-hole and barrel art. */
public sealed class NativeObjectCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumDocumentCharacters = 8 * 1024 * 1024;
    public const int MaximumAtlasFileBytes = 128 * 1024 * 1024;
    public const int MaximumAtlasDimension = 8192;
    public const long MaximumAtlasPixels = 4L * 1024 * 1024;
    public const long MaximumAggregateAtlasPixels = 16L * 1024 * 1024;
    public const int MaximumSpriteFrames = 64;

    private const string CanonicalObjectMetadataSha256 = "99ca3881bb95f5ee70a1bc811b00d11cf41e7f2a8e7defd77acc4af82e587dee";
    private static readonly string[] RootProperties = ["schemaVersion", "packId", "profileBindings", "sourceHashes", "atlases", "sprites", "objects"];
    private static readonly string[] BindingProperties = ["contentManifestHash", "levelId", "location"];
    private static readonly string[] SourceHashProperties = ["path", "sha256"];
    private static readonly string[] AtlasProperties = ["id", "path", "width", "height", "sha256"];
    private static readonly string[] SpriteProperties = ["spriteId", "sourcePath", "nativeWidth", "sourceFrameCount", "frames"];
    private static readonly string[] FrameProperties = ["nativeFrame", "atlasId", "x", "y", "width", "height"];
    private static readonly string[] ObjectProperties = ["nativeObjectId", "spriteId", "animDelay", "animLen", "animWidth", "animated"];

    private static readonly string[] SpritePaths = [
        "objects/l1doors.cel",
        "objects/chest1.cel",
        "objects/chest2.cel",
        "objects/chest3.cel",
        "objects/sarc.cel",
        "objects/traphole.cel",
        "objects/barrel.cel",
        "objects/barrelex.cel",
    ];
    private static readonly int[] SpriteWidths = [64, 96, 96, 96, 128, 64, 96, 96];
    private static readonly int[] SpriteMinimumFrameCounts = [4, 6, 6, 6, 5, 2, 9, 10];
    private static readonly string[] SpriteStems = ["l1doors", "chest1", "chest2", "chest3", "sarc", "traphole", "barrel", "barrelex"];
    private static readonly (uint Id, int SpriteId, int Delay, int Length, int Width, bool Animated)[] ExpectedObjects = [
        (1, 0, 1, 0, 64, false),
        (2, 0, 2, 0, 64, false),
        (5, 1, 1, 0, 96, false),
        (6, 2, 1, 0, 96, false),
        (7, 3, 1, 0, 96, false),
        (48, 4, 1, 5, 128, false),
        (53, 5, 1, 0, 64, false),
        (54, 5, 2, 0, 64, false),
        (57, 6, 1, 9, 96, false),
        (58, 7, 1, 10, 96, false),
    ];

    private readonly IReadOnlySet<(string Hash, uint LevelId)> bindings;
    private readonly IReadOnlyDictionary<uint, NativeObjectDefinition> objectsById;
    private readonly IReadOnlyDictionary<int, NativeObjectSprite> spritesById;
    private readonly IReadOnlyDictionary<int, NativeObjectAtlas> atlasesById;

    private NativeObjectCatalog(
        string packId,
        IReadOnlyList<NativeObjectProfileBinding> profileBindings,
        IReadOnlyList<NativeObjectSourceHash> sourceHashes,
        IReadOnlyList<NativeObjectAtlas> atlases,
        IReadOnlyList<NativeObjectSprite> sprites,
        IReadOnlyList<NativeObjectDefinition> objects)
    {
        PackId = packId;
        ProfileBindings = Array.AsReadOnly(profileBindings.ToArray());
        SourceHashes = Array.AsReadOnly(sourceHashes.ToArray());
        Atlases = Array.AsReadOnly(atlases.ToArray());
        Sprites = Array.AsReadOnly(sprites.ToArray());
        Objects = Array.AsReadOnly(objects.ToArray());
        bindings = ProfileBindings.Select(binding => (binding.ContentManifestHash, binding.LevelId)).ToFrozenSet();
        objectsById = Objects.ToFrozenDictionary(item => item.NativeObjectId);
        spritesById = Sprites.ToFrozenDictionary(item => item.Id);
        atlasesById = Atlases.ToFrozenDictionary(item => item.Id);
    }

    public string PackId { get; }
    public IReadOnlyList<NativeObjectProfileBinding> ProfileBindings { get; }
    public IReadOnlyList<NativeObjectSourceHash> SourceHashes { get; }
    public IReadOnlyList<NativeObjectAtlas> Atlases { get; }
    public IReadOnlyList<NativeObjectSprite> Sprites { get; }
    public IReadOnlyList<NativeObjectDefinition> Objects { get; }

    public bool SupportsBinding(string? contentManifestHash, uint levelId)
        => contentManifestHash is not null && bindings.Contains((contentManifestHash, levelId));

    public bool TryGetObject(uint nativeObjectId, out NativeObjectDefinition definition)
        => objectsById.TryGetValue(nativeObjectId, out definition!);

    public bool TryGetSprite(int spriteId, out NativeObjectSprite sprite)
        => spritesById.TryGetValue(spriteId, out sprite!);

    public bool TryGetAtlas(int atlasId, out NativeObjectAtlas atlas)
        => atlasesById.TryGetValue(atlasId, out atlas!);

    public bool TryResolveFrame(uint nativeObjectId, uint oneBasedFrame, out NativeObjectFrame frame)
    {
        if (objectsById.TryGetValue(nativeObjectId, out var definition)
            && spritesById.TryGetValue(definition.SpriteId, out var sprite)
            && sprite.TryResolveFrame(oneBasedFrame, out frame))
            return true;
        frame = null!;
        return false;
    }

    public static bool TryParse(string? json, out NativeObjectCatalog? catalog, out IReadOnlyList<string> errors)
    {
        catalog = null;
        var diagnostics = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) {
            diagnostics.Add("The native object catalog is empty.");
            errors = diagnostics.AsReadOnly();
            return false;
        }
        if (json.Length > MaximumDocumentCharacters) {
            diagnostics.Add($"The native object catalog exceeds {MaximumDocumentCharacters} characters.");
            errors = diagnostics.AsReadOnly();
            return false;
        }
        try {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
            catalog = Parse(document.RootElement);
            errors = Array.Empty<string>();
            return true;
        } catch (InvalidDataException exception) {
            diagnostics.Add(exception.Message);
        } catch (JsonException exception) {
            diagnostics.Add($"The native object catalog is not valid JSON: {exception.Message}");
        } catch (InvalidOperationException exception) {
            diagnostics.Add($"The native object catalog has an invalid JSON shape: {exception.Message}");
        } catch (OverflowException exception) {
            diagnostics.Add($"The native object catalog exceeds supported numeric bounds: {exception.Message}");
        } catch (ArgumentException exception) {
            diagnostics.Add($"The native object catalog has duplicate or invalid identifiers: {exception.Message}");
        }
        errors = diagnostics.AsReadOnly();
        return false;
    }

    public static string CalculatePackId(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        return "native-object-" + CalculateDigest(document.RootElement)[..24];
    }

    private static NativeObjectCatalog Parse(JsonElement root)
    {
        RequireObject(root, "catalog", RootProperties);
        if (ReadInt32(root, "schemaVersion") != CurrentSchemaVersion)
            throw new InvalidDataException($"The native object catalog schemaVersion must be {CurrentSchemaVersion}.");
        var packId = ReadString(root, "packId");
        const string prefix = "native-object-";
        if (!packId.StartsWith(prefix, StringComparison.Ordinal)
            || packId.Length != prefix.Length + 24
            || !IsLowerHex(packId[prefix.Length..]))
            throw new InvalidDataException("The native object catalog packId is malformed.");
        if (packId != prefix + CalculateDigest(root)[..24])
            throw new InvalidDataException("The native object catalog packId does not match its content.");

        var profileBindings = ParseBindings(RequiredArray(root, "profileBindings"));
        var sourceHashes = ParseSourceHashes(RequiredArray(root, "sourceHashes"));
        var atlases = ParseAtlases(RequiredArray(root, "atlases"));
        var sprites = ParseSprites(RequiredArray(root, "sprites"), atlases);
        var objects = ParseObjects(RequiredArray(root, "objects"));
        return new NativeObjectCatalog(packId, profileBindings, sourceHashes, atlases, sprites, objects);
    }

    private static IReadOnlyList<NativeObjectProfileBinding> ParseBindings(JsonElement entries)
    {
        if (entries.GetArrayLength() is < 1 or > 64)
            throw new InvalidDataException("The native object catalog must bind one to 64 Cathedral profiles.");
        var result = new List<NativeObjectProfileBinding>(entries.GetArrayLength());
        string? previousHash = null;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, "profile binding", BindingProperties);
            var hash = ReadString(entry, "contentManifestHash");
            if (!IsSha256(hash) || ReadUInt32(entry, "levelId") != 1 || ReadString(entry, "location") != "cathedral")
                throw new InvalidDataException("Native object art bindings must identify lowercase SHA-256 Cathedral level 1.");
            if (previousHash is not null && string.CompareOrdinal(previousHash, hash) >= 0)
                throw new InvalidDataException("Native object art profile bindings must be unique and sorted by content hash.");
            result.Add(new NativeObjectProfileBinding(hash, 1, "cathedral"));
            previousHash = hash;
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<NativeObjectSourceHash> ParseSourceHashes(JsonElement entries)
    {
        string[] expectedPaths = [
            "assets/txtdata/objects/objdat.tsv",
            "levels/l1data/l1_1.pal",
            .. SpritePaths,
        ];
        Array.Sort(expectedPaths, StringComparer.Ordinal);
        if (entries.GetArrayLength() != expectedPaths.Length)
            throw new InvalidDataException("The native object catalog must hash objdat.tsv, the L1 palette and all eight source CEL files.");
        var result = new List<NativeObjectSourceHash>(entries.GetArrayLength());
        string? previousPath = null;
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, "source hash", SourceHashProperties);
            var path = ReadString(entry, "path");
            var sha256 = ReadString(entry, "sha256");
            if (!IsSafeRelativePath(path, 240) || !IsSha256(sha256))
                throw new InvalidDataException("A native object source hash has an invalid path or SHA-256 value.");
            if (previousPath is not null && string.CompareOrdinal(previousPath, path) >= 0)
                throw new InvalidDataException("Native object source hashes must be unique and sorted by path.");
            if (path == "assets/txtdata/objects/objdat.tsv" && sha256 != CanonicalObjectMetadataSha256)
                throw new InvalidDataException("The native object catalog does not bind canonical base-game objdat.tsv.");
            observed.Add(path);
            result.Add(new NativeObjectSourceHash(path, sha256));
            previousPath = path;
        }
        if (!observed.SetEquals(expectedPaths))
            throw new InvalidDataException("The native object source-hash set is not canonical.");
        return result.AsReadOnly();
    }

    private static IReadOnlyList<NativeObjectAtlas> ParseAtlases(JsonElement entries)
    {
        if (entries.GetArrayLength() != SpritePaths.Length)
            throw new InvalidDataException("The native object catalog must contain exactly eight source atlases.");
        var result = new List<NativeObjectAtlas>(entries.GetArrayLength());
        var paths = new HashSet<string>(StringComparer.Ordinal);
        long aggregatePixels = 0;
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, $"atlas[{index}]", AtlasProperties);
            var id = ReadInt32(entry, "id");
            var path = ReadString(entry, "path");
            var width = ReadInt32(entry, "width");
            var height = ReadInt32(entry, "height");
            var sha256 = ReadString(entry, "sha256");
            var expectedPath = $"sprites/{index}-{SpriteStems[index]}.png";
            if (id != index || path != expectedPath || !paths.Add(path)
                || width is < 1 or > MaximumAtlasDimension || height is < 1 or > MaximumAtlasDimension
                || !IsSha256(sha256))
                throw new InvalidDataException($"Native object atlas[{index}] metadata is invalid or out of canonical order.");
            var pixels = checked((long)width * height);
            if (pixels > MaximumAtlasPixels)
                throw new InvalidDataException($"Native object atlas[{index}] exceeds its pixel limit.");
            aggregatePixels = checked(aggregatePixels + pixels);
            if (aggregatePixels > MaximumAggregateAtlasPixels)
                throw new InvalidDataException("The native object atlas family exceeds its aggregate pixel limit.");
            result.Add(new NativeObjectAtlas(id, path, width, height, sha256));
            index++;
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<NativeObjectSprite> ParseSprites(JsonElement entries, IReadOnlyList<NativeObjectAtlas> atlases)
    {
        if (entries.GetArrayLength() != SpritePaths.Length)
            throw new InvalidDataException("The native object catalog must contain exactly eight source sprite definitions.");
        var result = new List<NativeObjectSprite>(entries.GetArrayLength());
        var totalFrames = 0;
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, $"sprite[{index}]", SpriteProperties);
            var id = ReadInt32(entry, "spriteId");
            var sourcePath = ReadString(entry, "sourcePath");
            var nativeWidth = ReadInt32(entry, "nativeWidth");
            var sourceFrameCount = ReadInt32(entry, "sourceFrameCount");
            if (id != index || sourcePath != SpritePaths[index] || nativeWidth != SpriteWidths[index]
                || sourceFrameCount < SpriteMinimumFrameCounts[index] || sourceFrameCount > MaximumSpriteFrames)
                throw new InvalidDataException($"Native object sprite[{index}] metadata does not match its canonical source.");
            var atlas = atlases[index];
            if (atlas.Width != checked(nativeWidth * sourceFrameCount))
                throw new InvalidDataException($"Native object sprite[{index}] atlas width does not match its source-frame grid.");
            var cellHeight = atlas.Height;
            var frameEntries = RequiredArray(entry, "frames");
            if (frameEntries.GetArrayLength() != sourceFrameCount)
                throw new InvalidDataException($"Native object sprite[{index}] must include every source frame exactly once.");
            totalFrames = checked(totalFrames + sourceFrameCount);
            if (totalFrames > 512)
                throw new InvalidDataException("The native object catalog exceeds its aggregate source-frame limit.");
            var frames = new List<NativeObjectSpriteFrame>(sourceFrameCount);
            var maxHeight = 0;
            var frameIndex = 0;
            foreach (var frameEntry in frameEntries.EnumerateArray()) {
                RequireObject(frameEntry, $"sprite[{index}].frames[{frameIndex}]", FrameProperties);
                var nativeFrame = ReadInt32(frameEntry, "nativeFrame");
                var atlasId = ReadInt32(frameEntry, "atlasId");
                var x = ReadInt32(frameEntry, "x");
                var y = ReadInt32(frameEntry, "y");
                var width = ReadInt32(frameEntry, "width");
                var height = ReadInt32(frameEntry, "height");
                if (nativeFrame != frameIndex + 1 || atlasId != index
                    || x != checked(frameIndex * nativeWidth) || width != nativeWidth
                    || height is < 1 || height > cellHeight
                    || y != cellHeight - height || x > atlas.Width - width)
                    throw new InvalidDataException($"Native object sprite[{index}] frame[{frameIndex}] is not a bottom-aligned source-frame cell.");
                frames.Add(new NativeObjectSpriteFrame(nativeFrame, atlasId, x, y, width, height));
                maxHeight = Math.Max(maxHeight, height);
                frameIndex++;
            }
            if (maxHeight != cellHeight)
                throw new InvalidDataException($"Native object sprite[{index}] atlas does not use the maximum source-frame height.");
            result.Add(new NativeObjectSprite(index, sourcePath, nativeWidth, sourceFrameCount, frames));
            index++;
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<NativeObjectDefinition> ParseObjects(JsonElement entries)
    {
        if (entries.GetArrayLength() != ExpectedObjects.Length)
            throw new InvalidDataException("The native object catalog must contain the ten supported base-game L1 object IDs.");
        var result = new List<NativeObjectDefinition>(entries.GetArrayLength());
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, $"object[{index}]", ObjectProperties);
            var expected = ExpectedObjects[index];
            var item = new NativeObjectDefinition(
                ReadUInt32(entry, "nativeObjectId"),
                ReadInt32(entry, "spriteId"),
                ReadInt32(entry, "animDelay"),
                ReadInt32(entry, "animLen"),
                ReadInt32(entry, "animWidth"),
                ReadBoolean(entry, "animated"));
            if (item != new NativeObjectDefinition(expected.Id, expected.SpriteId, expected.Delay, expected.Length, expected.Width, expected.Animated))
                throw new InvalidDataException($"Native object[{index}] metadata does not match its canonical base-game object identity.");
            result.Add(item);
            index++;
        }
        return result.AsReadOnly();
    }

    private static string CalculateDigest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The native object catalog root must be an object.");
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) {
            writer.WriteStartObject();
            var packIdCount = 0;
            foreach (var property in root.EnumerateObject()) {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("packId")) {
                    writer.WriteStringValue(string.Empty);
                    packIdCount++;
                } else {
                    property.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            if (packIdCount != 1)
                throw new InvalidDataException("The native object catalog must contain exactly one packId.");
        }
        return Convert.ToHexString(SHA256.HashData(output.ToArray())).ToLowerInvariant();
    }

    private static void RequireObject(JsonElement value, string path, IReadOnlyCollection<string> properties)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Native object {path} must be an object.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject()) {
            if (!seen.Add(property.Name))
                throw new InvalidDataException($"Native object {path} repeats property '{property.Name}'.");
            if (!properties.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException($"Native object {path} has unknown property '{property.Name}'.");
        }
        foreach (var required in properties)
            if (!seen.Contains(required))
                throw new InvalidDataException($"Native object {path} is missing property '{required}'.");
    }

    private static JsonElement RequiredArray(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Native object property '{name}' must be an array.");
        return value;
    }

    private static string ReadString(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(value.GetString()))
            throw new InvalidDataException($"Native object property '{name}' must be a nonempty string.");
        return value.GetString()!;
    }

    private static int ReadInt32(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new InvalidDataException($"Native object property '{name}' must be a 32-bit integer.");
        return number;
    }

    private static uint ReadUInt32(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out var number))
            throw new InvalidDataException($"Native object property '{name}' must be an unsigned 32-bit integer.");
        return number;
    }

    private static bool ReadBoolean(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"Native object property '{name}' must be a boolean.");
        return value.GetBoolean();
    }

    private static bool IsSafeRelativePath(string value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength || value.Contains('\\')
            || value.StartsWith("/", StringComparison.Ordinal) || value.Contains(':'))
            return false;
        return value.Split('/').All(segment => segment.Length > 0 && segment is not ("." or "..")
            && !segment.Any(char.IsControl));
    }

    private static bool IsSha256(string value) => value.Length == 64 && IsLowerHex(value);
    private static bool IsLowerHex(string value) => value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
