using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Devilution.Client.PresentationAssets;

public sealed record NativeTownerAtlas(int Id, string Path, int Width, int Height, string Sha256);

public sealed record NativeTownerFrame(
    int AtlasId,
    int X,
    int Y,
    int Width,
    int Height,
    int DirectionIndex,
    int FrameIndex);

public sealed record NativeTownerResident(
    int ResidentSlot,
    int TownerId,
    int PositionX,
    int PositionY,
    int NativeDirection,
    int SpriteId);

public sealed class NativeTownerSprite
{
    private readonly IReadOnlyList<NativeTownerFrame> frames;
    private readonly IReadOnlyList<int> animationOrder;

    internal NativeTownerSprite(
        int id,
        int alignmentWidth,
        int directionGroupCount,
        int sourceFramesPerDirection,
        int animationFrames,
        int ticksPerFrame,
        IReadOnlyList<int> animationOrder,
        IReadOnlyList<NativeTownerFrame> frames)
    {
        Id = id;
        AlignmentWidth = alignmentWidth;
        DirectionGroupCount = directionGroupCount;
        SourceFramesPerDirection = sourceFramesPerDirection;
        AnimationFrames = animationFrames;
        TicksPerFrame = ticksPerFrame;
        this.animationOrder = Array.AsReadOnly(animationOrder.ToArray());
        this.frames = Array.AsReadOnly(frames.ToArray());
    }

    public int Id { get; }
    public int AlignmentWidth { get; }
    public int DirectionGroupCount { get; }
    public int SourceFramesPerDirection { get; }
    public int AnimationFrames { get; }
    public int TicksPerFrame { get; }
    public IReadOnlyList<int> AnimationOrder => animationOrder;

    /** Resolves only cosmetic art time; native simulation animation cursors are not represented here. */
    public NativeTownerFrame ResolveFrame(double presentationTick, int nativeDirection)
    {
        if (nativeDirection is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(nativeDirection));
        var direction = DirectionGroupCount == 1 ? 0 : nativeDirection;
        var ticks = double.IsFinite(presentationTick) && presentationTick > 0
            ? presentationTick / TicksPerFrame
            : 0;
        var flooredTicks = Math.Floor(ticks);
        var step = flooredTicks >= long.MaxValue ? long.MaxValue : (long)flooredTicks;
        var frame = AnimationOrderFrame(step);
        return frames[checked(direction * SourceFramesPerDirection + frame)];
    }

    private int AnimationOrderFrame(long step)
    {
        if (step <= 0)
            return 0;
        if (animationOrder.Count == 0)
            return (int)(step % AnimationFrames);
        return animationOrder[(int)(step % animationOrder.Count)];
    }
}

/** Immutable, bounded Town resident art catalog bound to exact format-5 content and Town identities. */
public sealed class NativeTownerCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumDocumentCharacters = 8 * 1024 * 1024;
    public const int MaximumAtlasFileBytes = 128 * 1024 * 1024;
    public const int MaximumAtlasDimension = 8192;
    public const long MaximumAtlasPixels = 4L * 1024 * 1024;
    public const long MaximumAggregateAtlasPixels = 16L * 1024 * 1024;
    public const int MaximumSpriteFrames = 512;
    public const int MaximumAnimationOrderEntries = 1024;

    private const string CanonicalTownerMetadataSha256 = "91546c020a1a24b3cae189bcc80ef7bf2c50675f5b92243b43e6317dea993b9e";
    private static readonly string[] RootProperties = [
        "schemaVersion", "packId", "profileBindings", "sourceHashes", "atlases", "sprites", "residents",
    ];
    private static readonly string[] ProfileBindingProperties = [
        "contentManifestHash", "townCatalogFingerprint", "levelId", "location",
    ];
    private static readonly string[] SourceHashProperties = ["path", "sha256"];
    private static readonly string[] AtlasProperties = ["id", "path", "width", "height", "sha256"];
    private static readonly string[] SpriteProperties = [
        "spriteId", "alignmentWidth", "directionGroupCount", "sourceFramesPerDirection",
        "animationFrames", "ticksPerFrame", "animationOrder", "frames",
    ];
    private static readonly string[] FrameProperties = [
        "group", "frame", "atlasId", "x", "y", "width", "height",
    ];
    private static readonly string[] ResidentProperties = [
        "residentSlot", "townerId", "positionX", "positionY", "nativeDirection", "spriteId",
    ];
    private static readonly string[] ExpectedSourcePaths = BuildExpectedSourcePaths();
    private static readonly (int AnimationFrames, int TicksPerFrame)[] ExpectedAnimation = [
        (16, 3), (20, 6), (16, 3), (25, 3), (18, 3), (19, 6), (18, 6), (20, 6), (12, 3),
    ];
    private static readonly string[] ExpectedAnimationOrderSha256 = [
        "286125321fbf34d12835b171db55132cdf3b2e676b25cb32c4f795da25940441",
        "242e8765c3aebcf649175379e71ab446c20b1363577b5c22e14665da606ee78b",
        "347fb2b0c92e1680188279d4d229e3f47c8ba893463d9e4a61491445865a4d46",
        "465d37846c5b6b6d885a857d56cff5f55d4c3cadbffb0eb2a8b169fea14b8a19",
        "36c292d3c64257084bce44336d781179e1d2b6b329b4c6ecec7d7082e7902beb",
        "9b493ae976f31b0eabb2d94fb87cbab9978df164708ee1c329d44959e57e1ddc",
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    ];
    private static readonly (int TownerId, int X, int Y, int Direction, int SpriteId)[] ExpectedResidents = [
        (0, 62, 63, 1, 0),
        (1, 55, 79, 7, 1),
        (3, 55, 62, 1, 2),
        (4, 62, 71, 0, 3),
        (5, 71, 84, 0, 4),
        (6, 80, 20, 0, 5),
        (7, 43, 66, 0, 6),
        (8, 11, 53, 0, 7),
        (9, 58, 16, 1, 8),
        (9, 56, 14, 3, 8),
        (9, 59, 20, 4, 8),
    ];

    private readonly IReadOnlySet<(string ContentManifestHash, string TownCatalogFingerprint)> bindings;
    private readonly IReadOnlyDictionary<int, NativeTownerAtlas> atlasesById;
    private readonly IReadOnlyDictionary<int, NativeTownerSprite> spritesById;

    private NativeTownerCatalog(
        string packId,
        IReadOnlyList<(string ContentManifestHash, string TownCatalogFingerprint)> bindings,
        IReadOnlyList<NativeTownerSourceHash> sourceHashes,
        IReadOnlyList<NativeTownerAtlas> atlases,
        IReadOnlyList<NativeTownerSprite> sprites,
        IReadOnlyList<NativeTownerResident> residents)
    {
        PackId = packId;
        this.bindings = bindings.ToFrozenSet();
        ProfileBindings = Array.AsReadOnly(bindings.ToArray());
        SourceHashes = Array.AsReadOnly(sourceHashes.ToArray());
        Atlases = Array.AsReadOnly(atlases.ToArray());
        Sprites = Array.AsReadOnly(sprites.ToArray());
        Residents = Array.AsReadOnly(residents.ToArray());
        atlasesById = Atlases.ToFrozenDictionary(atlas => atlas.Id);
        spritesById = Sprites.ToFrozenDictionary(sprite => sprite.Id);
    }

    public string PackId { get; }
    public IReadOnlyList<(string ContentManifestHash, string TownCatalogFingerprint)> ProfileBindings { get; }
    public IReadOnlyList<NativeTownerSourceHash> SourceHashes { get; }
    public IReadOnlyList<NativeTownerAtlas> Atlases { get; }
    public IReadOnlyList<NativeTownerSprite> Sprites { get; }
    public IReadOnlyList<NativeTownerResident> Residents { get; }

    public bool SupportsBinding(string? contentManifestHash, string? townCatalogFingerprint)
        => contentManifestHash is not null && townCatalogFingerprint is not null
            && bindings.Contains((contentManifestHash, townCatalogFingerprint));

    public bool TryGetSprite(int spriteId, out NativeTownerSprite sprite)
        => spritesById.TryGetValue(spriteId, out sprite!);

    public bool TryGetAtlas(int atlasId, out NativeTownerAtlas atlas)
        => atlasesById.TryGetValue(atlasId, out atlas!);

    public bool ResidentsMatches(
        IReadOnlyList<(int ResidentSlot, int TownerId, int PositionX, int PositionY, int NativeDirection)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count != Residents.Count)
            return false;
        for (var index = 0; index < rows.Count; index++) {
            var row = rows[index];
            var expected = Residents[index];
            if (row.ResidentSlot != expected.ResidentSlot
                || row.TownerId != expected.TownerId
                || row.PositionX != expected.PositionX
                || row.PositionY != expected.PositionY
                || row.NativeDirection != expected.NativeDirection)
                return false;
        }
        return true;
    }

    /** Parses the schema-1 compiler output and rejects any noncanonical base Town roster. */
    public static bool TryParse(string? json, out NativeTownerCatalog? catalog, out IReadOnlyList<string> errors)
    {
        catalog = null;
        var diagnostics = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) {
            diagnostics.Add("The native Town resident catalog is empty.");
            errors = diagnostics.AsReadOnly();
            return false;
        }
        if (json.Length > MaximumDocumentCharacters) {
            diagnostics.Add($"The native Town resident catalog exceeds {MaximumDocumentCharacters} characters.");
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
            diagnostics.Add($"The native Town resident catalog is not valid JSON: {exception.Message}");
        } catch (InvalidOperationException exception) {
            diagnostics.Add($"The native Town resident catalog has an invalid JSON shape: {exception.Message}");
        } catch (OverflowException exception) {
            diagnostics.Add($"The native Town resident catalog exceeds supported numeric bounds: {exception.Message}");
        }
        errors = diagnostics.AsReadOnly();
        return false;
    }

    /** Calculates the pack ID over compact UTF-8 JSON with packId blank, as the importer does. */
    public static string CalculatePackId(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var digest = CalculateDigest(document.RootElement);
        return "native-towner-" + digest[..24];
    }

    private static NativeTownerCatalog Parse(JsonElement root)
    {
        RequireObject(root, "catalog", RootProperties);
        if (ReadInt32(root, "schemaVersion") != CurrentSchemaVersion)
            throw new InvalidDataException($"The native Town resident catalog schemaVersion must be {CurrentSchemaVersion}.");
        var packId = ReadString(root, "packId");
        if (!packId.StartsWith("native-towner-", StringComparison.Ordinal)
            || packId.Length != "native-towner-".Length + 24
            || !IsLowerHex(packId["native-towner-".Length..]))
            throw new InvalidDataException("The native Town resident catalog packId is malformed.");
        if (packId != "native-towner-" + CalculateDigest(root)[..24])
            throw new InvalidDataException("The native Town resident catalog packId does not match its content.");

        var bindings = ParseBindings(RequiredArray(root, "profileBindings"));
        var sourceHashes = ParseSourceHashes(RequiredArray(root, "sourceHashes"));
        var atlases = ParseAtlases(RequiredArray(root, "atlases"));
        var sprites = ParseSprites(RequiredArray(root, "sprites"), atlases);
        var residents = ParseResidents(RequiredArray(root, "residents"));
        return new NativeTownerCatalog(packId, bindings, sourceHashes, atlases, sprites, residents);
    }

    private static IReadOnlyList<(string ContentManifestHash, string TownCatalogFingerprint)> ParseBindings(JsonElement entries)
    {
        if (entries.GetArrayLength() is < 1 or > 64)
            throw new InvalidDataException("The native Town resident catalog must bind one to 64 format-5 profiles.");
        var result = new List<(string ContentManifestHash, string TownCatalogFingerprint)>(entries.GetArrayLength());
        string? previousHash = null;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, "profile binding", ProfileBindingProperties);
            var hash = ReadString(entry, "contentManifestHash");
            var fingerprint = ReadString(entry, "townCatalogFingerprint");
            if (!IsSha256(hash) || !IsSha256(fingerprint))
                throw new InvalidDataException("Native Town profile bindings require lowercase SHA-256 hashes and fingerprints.");
            if (ReadUInt32(entry, "levelId") != 0 || ReadString(entry, "location") != "town")
                throw new InvalidDataException("Native Town profile bindings must identify Town level zero.");
            if (previousHash is not null && string.CompareOrdinal(previousHash, hash) >= 0)
                throw new InvalidDataException("Native Town profile bindings must be unique and sorted by content hash.");
            result.Add((hash, fingerprint));
            previousHash = hash;
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<NativeTownerSourceHash> ParseSourceHashes(JsonElement entries)
    {
        if (entries.GetArrayLength() != ExpectedSourcePaths.Length)
            throw new InvalidDataException("The native Town resident catalog must hash all nine CELs, the Town palette and canonical towner metadata.");
        var result = new List<NativeTownerSourceHash>(entries.GetArrayLength());
        string? previousPath = null;
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, "source hash", SourceHashProperties);
            var path = ReadString(entry, "path");
            var sha256 = ReadString(entry, "sha256");
            if (!IsSafeRelativePath(path, 240) || !IsSha256(sha256))
                throw new InvalidDataException("A native Town resident source hash has an invalid path or SHA-256 value.");
            if (previousPath is not null && string.CompareOrdinal(previousPath, path) >= 0)
                throw new InvalidDataException("Native Town resident source hashes must be unique and sorted by path.");
            observed.Add(path);
            if (path == "assets/txtdata/towners/towners.tsv" && sha256 != CanonicalTownerMetadataSha256)
                throw new InvalidDataException("The native Town resident catalog does not bind the canonical towner animation metadata.");
            result.Add(new NativeTownerSourceHash(path, sha256));
            previousPath = path;
        }
        if (!observed.SetEquals(ExpectedSourcePaths))
            throw new InvalidDataException("The native Town resident catalog source-hash set is not the canonical Town palette, metadata and nine CEL families.");
        return result.AsReadOnly();
    }

    private static IReadOnlyList<NativeTownerAtlas> ParseAtlases(JsonElement entries)
    {
        if (entries.GetArrayLength() != 9)
            throw new InvalidDataException("The native Town resident catalog must contain exactly nine sprite atlases.");
        var result = new List<NativeTownerAtlas>(entries.GetArrayLength());
        var paths = new HashSet<string>(StringComparer.Ordinal);
        long totalPixels = 0;
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, $"atlas[{index}]", AtlasProperties);
            var id = ReadInt32(entry, "id");
            var path = ReadString(entry, "path");
            var width = ReadInt32(entry, "width");
            var height = ReadInt32(entry, "height");
            var sha256 = ReadString(entry, "sha256");
            if (id != index || !IsSafeRelativePath(path, 240) || !path.StartsWith("sprites/", StringComparison.Ordinal)
                || !path.EndsWith(".png", StringComparison.Ordinal) || !paths.Add(path)
                || width is < 1 or > MaximumAtlasDimension || height is < 1 or > MaximumAtlasDimension
                || !IsSha256(sha256))
                throw new InvalidDataException($"Native Town atlas[{index}] metadata is invalid or out of canonical order.");
            var pixels = checked((long)width * height);
            if (pixels > MaximumAtlasPixels)
                throw new InvalidDataException($"Native Town atlas[{index}] exceeds its pixel limit.");
            totalPixels = checked(totalPixels + pixels);
            if (totalPixels > MaximumAggregateAtlasPixels)
                throw new InvalidDataException("The native Town resident atlas family exceeds its aggregate size limit.");
            result.Add(new NativeTownerAtlas(id, path, width, height, sha256));
            index++;
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<NativeTownerSprite> ParseSprites(JsonElement entries, IReadOnlyList<NativeTownerAtlas> atlases)
    {
        if (entries.GetArrayLength() != 9)
            throw new InvalidDataException("The native Town resident catalog must contain exactly nine native sprite definitions.");
        var result = new List<NativeTownerSprite>(9);
        var totalFrameCount = 0;
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, $"sprite[{index}]", SpriteProperties);
            var spriteId = ReadInt32(entry, "spriteId");
            var alignmentWidth = ReadInt32(entry, "alignmentWidth");
            var groups = ReadInt32(entry, "directionGroupCount");
            var sourceFrames = ReadInt32(entry, "sourceFramesPerDirection");
            var animationFrames = ReadInt32(entry, "animationFrames");
            var ticksPerFrame = ReadInt32(entry, "ticksPerFrame");
            var expected = ExpectedAnimation[index];
            if (spriteId != index || alignmentWidth != (index == 8 ? 128 : 96)
                || groups != (index == 8 ? 8 : 1)
                || sourceFrames is < 1 or > 64 || animationFrames != expected.AnimationFrames
                || animationFrames > sourceFrames || ticksPerFrame != expected.TicksPerFrame)
                throw new InvalidDataException($"Native Town sprite[{index}] metadata does not match the canonical animation family.");

            var order = ParseAnimationOrder(RequiredArray(entry, "animationOrder"), animationFrames, index);
            if ((index < 6) != (order.Count > 0))
                throw new InvalidDataException($"Native Town sprite[{index}] animation order does not match its native metadata.");
            if (ComputeAnimationOrderSha256(order) != ExpectedAnimationOrderSha256[index])
                throw new InvalidDataException($"Native Town sprite[{index}] animation order differs from the canonical towner metadata.");
            var atlas = atlases[index];
            if (atlas.Width % sourceFrames != 0 || atlas.Height % groups != 0)
                throw new InvalidDataException($"Native Town sprite[{index}] atlas dimensions do not match its frame grid.");
            var cellWidth = atlas.Width / sourceFrames;
            var cellHeight = atlas.Height / groups;
            var framesElement = RequiredArray(entry, "frames");
            var expectedFrames = checked(groups * sourceFrames);
            if (expectedFrames > MaximumSpriteFrames || framesElement.GetArrayLength() != expectedFrames)
                throw new InvalidDataException($"Native Town sprite[{index}] must describe every source frame exactly once.");
            totalFrameCount = checked(totalFrameCount + expectedFrames);
            if (totalFrameCount > 4096)
                throw new InvalidDataException("The native Town resident catalog exceeds its aggregate source-frame limit.");

            var frames = new List<NativeTownerFrame>(expectedFrames);
            var maxWidth = 0;
            var maxHeight = 0;
            var frameIndex = 0;
            foreach (var frameElement in framesElement.EnumerateArray()) {
                RequireObject(frameElement, $"sprite[{index}].frames[{frameIndex}]", FrameProperties);
                var group = ReadInt32(frameElement, "group");
                var frame = ReadInt32(frameElement, "frame");
                var atlasId = ReadInt32(frameElement, "atlasId");
                var x = ReadInt32(frameElement, "x");
                var y = ReadInt32(frameElement, "y");
                var width = ReadInt32(frameElement, "width");
                var height = ReadInt32(frameElement, "height");
                var expectedGroup = frameIndex / sourceFrames;
                var expectedFrame = frameIndex % sourceFrames;
                if (group != expectedGroup || frame != expectedFrame || atlasId != index
                    || x != checked(expectedFrame * cellWidth)
                    || width is < 1 || width > cellWidth || height is < 1 || height > cellHeight
                    || x > atlas.Width - width || y < checked(expectedGroup * cellHeight)
                    || y > atlas.Height - height || checked(y + height) != checked((expectedGroup + 1) * cellHeight))
                    throw new InvalidDataException($"Native Town sprite[{index}] frame[{frameIndex}] is not a bottom-aligned cell in its atlas.");
                maxWidth = Math.Max(maxWidth, width);
                maxHeight = Math.Max(maxHeight, height);
                frames.Add(new NativeTownerFrame(atlasId, x, y, width, height, group, frame));
                frameIndex++;
            }
            if (maxWidth != cellWidth || maxHeight != cellHeight)
                throw new InvalidDataException($"Native Town sprite[{index}] atlas grid does not use the maximum native frame dimensions.");
            result.Add(new NativeTownerSprite(spriteId, alignmentWidth, groups, sourceFrames,
                animationFrames, ticksPerFrame, order, frames));
            index++;
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<int> ParseAnimationOrder(JsonElement entries, int animationFrames, int spriteId)
    {
        if (entries.GetArrayLength() > MaximumAnimationOrderEntries)
            throw new InvalidDataException($"Native Town sprite[{spriteId}] animation order exceeds its limit.");
        var result = new int[entries.GetArrayLength()];
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            if (entry.ValueKind != JsonValueKind.Number || !entry.TryGetInt32(out var frame) || frame < 0 || frame >= animationFrames)
                throw new InvalidDataException($"Native Town sprite[{spriteId}] animation order contains an invalid frame.");
            result[index++] = frame;
        }
        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<NativeTownerResident> ParseResidents(JsonElement entries)
    {
        if (entries.GetArrayLength() != ExpectedResidents.Length)
            throw new InvalidDataException("The native Town resident catalog must contain eight humans and three cows.");
        var result = new List<NativeTownerResident>(entries.GetArrayLength());
        var index = 0;
        foreach (var entry in entries.EnumerateArray()) {
            RequireObject(entry, $"resident[{index}]", ResidentProperties);
            var expected = ExpectedResidents[index];
            var resident = new NativeTownerResident(
                ReadInt32(entry, "residentSlot"),
                ReadInt32(entry, "townerId"),
                ReadInt32(entry, "positionX"),
                ReadInt32(entry, "positionY"),
                ReadInt32(entry, "nativeDirection"),
                ReadInt32(entry, "spriteId"));
            if (resident != new NativeTownerResident(index, expected.TownerId, expected.X, expected.Y, expected.Direction, expected.SpriteId))
                throw new InvalidDataException($"Native Town resident[{index}] does not match the canonical base roster.");
            result.Add(resident);
            index++;
        }
        return result.AsReadOnly();
    }

    private static string CalculateDigest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The native Town resident catalog root must be an object.");
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
                throw new InvalidDataException("The native Town resident catalog must contain exactly one packId.");
        }
        return Convert.ToHexString(SHA256.HashData(output.ToArray())).ToLowerInvariant();
    }

    private static string ComputeAnimationOrderSha256(IReadOnlyList<int> animationOrder)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', animationOrder))))
            .ToLowerInvariant();

    private static void RequireObject(JsonElement value, string path, IReadOnlyCollection<string> allowedProperties)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Native Town {path} must be an object.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject()) {
            if (!seen.Add(property.Name))
                throw new InvalidDataException($"Native Town {path} repeats property '{property.Name}'.");
            if (!allowedProperties.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException($"Native Town {path} has unknown property '{property.Name}'.");
        }
        foreach (var required in allowedProperties) {
            if (!seen.Contains(required))
                throw new InvalidDataException($"Native Town {path} is missing property '{required}'.");
        }
    }

    private static JsonElement RequiredArray(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Native Town property '{name}' must be an array.");
        return value;
    }

    private static string ReadString(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(value.GetString()))
            throw new InvalidDataException($"Native Town property '{name}' must be a nonempty string.");
        return value.GetString()!;
    }

    private static int ReadInt32(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new InvalidDataException($"Native Town property '{name}' must be a 32-bit integer.");
        return number;
    }

    private static uint ReadUInt32(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out var number))
            throw new InvalidDataException($"Native Town property '{name}' must be an unsigned 32-bit integer.");
        return number;
    }

    private static bool IsSafeRelativePath(string value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength || value.Contains('\\')
            || value.StartsWith("/", StringComparison.Ordinal) || value.Contains(':'))
            return false;
        var segments = value.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not ("." or "..")
            && !segment.Any(char.IsControl));
    }

    private static bool IsSha256(string value)
        => value.Length == 64 && IsLowerHex(value);

    private static bool IsLowerHex(string value)
        => value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string[] BuildExpectedSourcePaths()
    {
        string[] paths = [
            "assets/txtdata/towners/towners.tsv",
            "levels/towndata/town.pal",
            "towners/animals/cow.cel",
            "towners/drunk/twndrunk.cel",
            "towners/healer/healer.cel",
            "towners/smith/smithn.cel",
            "towners/townboy/pegkid1.cel",
            "towners/twnf/twnfn.cel",
            "towners/townwmn1/wmnn.cel",
            "towners/townwmn1/witch.cel",
            "towners/strytell/strytell.cel",
        ];
        Array.Sort(paths, StringComparer.Ordinal);
        return paths;
    }
}

public sealed record NativeTownerSourceHash(string Path, string Sha256);
