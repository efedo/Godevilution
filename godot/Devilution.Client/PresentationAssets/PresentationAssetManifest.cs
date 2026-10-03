using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Devilution.Client.PresentationAssets;

/** Stable presentation resource categories. These IDs never enter the protocol. */
public enum PresentationAssetKind
{
    Texture,
    Audio,
    Localization,
}

/** Optional atlas metadata for a texture. Frame selection is presentation-only. */
public sealed record PresentationFrameLayout(
    int FrameCount,
    int FrameWidth,
    int FrameHeight,
    int Columns,
    int Rows,
    double FramesPerSecond,
    bool Loop);

/** A single pack binding after strict manifest validation. */
public sealed record PresentationAssetDefinition(
    string Id,
    PresentationAssetKind Kind,
    string Path,
    PresentationFrameLayout? Frames,
    string? Text);

/** Resolved region in a texture atlas. */
public sealed record PresentationFrameRegion(int X, int Y, int Width, int Height);

/** Read-only diagnostics exposed to client acceptance without leaking server data. */
public sealed record PresentationPackObservation(
    string ManifestPath,
    bool IsActive,
    string? PackId,
    int SchemaVersion,
    string? Diagnostic,
    IReadOnlyList<string> LoadedTextureKeys,
    IReadOnlyList<string> MissingTextureKeys,
    IReadOnlyList<string> LoadedAudioKeys,
    IReadOnlyList<string> MissingAudioKeys);

/** Read-only result for one texture lookup, including whether fallback was used. */
public sealed record PresentationTextureBindingObservation(
    string Key,
    bool IsDeclared,
    bool IsLoaded,
    bool UsedFallback,
    string? ResourcePath,
    PresentationFrameRegion? FrameRegion,
    string? Diagnostic);

/** Immutable, validated client-only presentation catalog. */
public sealed class PresentationAssetCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumAssetCount = 256;
    public const int MaximumIdLength = 64;
    public const int MaximumPathLength = 240;
    public const int MaximumTextLength = 1024;

    private readonly IReadOnlyDictionary<string, PresentationAssetDefinition> assets;

    private PresentationAssetCatalog(
        string packId,
        int schemaVersion,
        IReadOnlyDictionary<string, PresentationAssetDefinition> assets)
    {
        PackId = packId;
        SchemaVersion = schemaVersion;
        this.assets = assets;
    }

    public string PackId { get; }

    public int SchemaVersion { get; }

    public IReadOnlyDictionary<string, PresentationAssetDefinition> Assets => assets;

    public bool TryGet(string id, out PresentationAssetDefinition definition)
        => assets.TryGetValue(id, out definition!);

    public bool TryGetLocalization(string id, out string text)
    {
        if (TryGet(id, out var definition)
            && definition.Kind == PresentationAssetKind.Localization
            && !string.IsNullOrEmpty(definition.Text)) {
            text = definition.Text;
            return true;
        }

        text = string.Empty;
        return false;
    }

    /** Parses and validates the entire document before returning any catalog. */
    public static bool TryParse(
        string? json,
        [NotNullWhen(true)] out PresentationAssetCatalog? catalog,
        out IReadOnlyList<string> diagnostics)
    {
        catalog = null;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) {
            errors.Add("The presentation manifest is empty.");
            diagnostics = errors;
            return false;
        }
        if (json.Length > 1_000_000) {
            errors.Add("The presentation manifest exceeds the 1 MB size limit.");
            diagnostics = errors;
            return false;
        }

        try {
            using var shape = JsonDocument.Parse(json, new JsonDocumentOptions {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
            ValidateJsonObjectShape(shape.RootElement, "manifest", errors);
        } catch (JsonException exception) {
            errors.Add($"The presentation manifest is not valid JSON: {exception.Message}");
            diagnostics = errors;
            return false;
        }
        if (errors.Count > 0) {
            diagnostics = errors.AsReadOnly();
            return false;
        }

        ManifestDocument? document;
        try {
            document = JsonSerializer.Deserialize<ManifestDocument>(json, JsonOptions);
        } catch (JsonException exception) {
            errors.Add($"The presentation manifest is not valid JSON: {exception.Message}");
            diagnostics = errors;
            return false;
        } catch (NotSupportedException exception) {
            errors.Add($"The presentation manifest uses an unsupported JSON shape: {exception.Message}");
            diagnostics = errors;
            return false;
        }

        if (document is null) {
            errors.Add("The presentation manifest has no document.");
            diagnostics = errors;
            return false;
        }
        if (document.SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Unsupported presentation manifest schema {document.SchemaVersion}; expected {CurrentSchemaVersion}.");
        if (!IsStableId(document.PackId, MaximumIdLength))
            errors.Add("The presentation manifest packId is not a stable identifier.");
        if (document.Assets is null)
            errors.Add("The presentation manifest must contain an assets array.");
        else if (document.Assets.Count > MaximumAssetCount)
            errors.Add($"The presentation manifest contains more than {MaximumAssetCount} assets.");

        var definitions = new Dictionary<string, PresentationAssetDefinition>(StringComparer.Ordinal);
        if (document.Assets is not null) {
            foreach (var entry in document.Assets) {
                if (entry is null) {
                    errors.Add("The presentation manifest contains a null asset entry.");
                    continue;
                }
                if (!IsStableId(entry.Id, MaximumIdLength)) {
                    errors.Add($"Asset id '{entry.Id ?? "<null>"}' is not a stable identifier.");
                    continue;
                }
                if (!definitions.TryAdd(entry.Id, null!)) {
                    errors.Add($"Asset id '{entry.Id}' is declared more than once.");
                    continue;
                }
                if (!TryParseKind(entry.Kind, out var kind)) {
                    errors.Add($"Asset '{entry.Id}' has unsupported kind '{entry.Kind ?? "<null>"}'.");
                    continue;
                }
                var valid = true;
                var path = string.Empty;
                if (kind != PresentationAssetKind.Localization) {
                    if (!PresentationAssetPath.TryNormalizeRelativePath(entry.Path, out path, out var pathError)) {
                        errors.Add($"Asset '{entry.Id}' has an unsafe path: {pathError}");
                        valid = false;
                    }
                } else if (!string.IsNullOrEmpty(entry.Path)) {
                    errors.Add($"Localization asset '{entry.Id}' must not declare a resource path.");
                    valid = false;
                }

                PresentationFrameLayout? frames = null;
                if (entry.Frames is not null) {
                    if (kind != PresentationAssetKind.Texture) {
                        errors.Add($"Asset '{entry.Id}' declares frame metadata but is not a texture.");
                        valid = false;
                    } else if (!TryValidateFrames(entry.Frames, out frames, out var frameError)) {
                        errors.Add($"Asset '{entry.Id}' has invalid frame metadata: {frameError}");
                        valid = false;
                    }
                }
                if (kind == PresentationAssetKind.Localization) {
                    if (string.IsNullOrEmpty(entry.Text) || entry.Text.Length > MaximumTextLength)
                        errors.Add($"Localization asset '{entry.Id}' must contain text of at most {MaximumTextLength} characters.");
                    if (string.IsNullOrEmpty(entry.Text) || entry.Text.Length > MaximumTextLength)
                        valid = false;
                } else if (!string.IsNullOrEmpty(entry.Text)) {
                    errors.Add($"Non-localization asset '{entry.Id}' must not declare text.");
                    valid = false;
                }

                if (valid)
                    definitions[entry.Id] = new PresentationAssetDefinition(entry.Id, kind, path, frames, entry.Text);
            }
        }

        if (errors.Count > 0) {
            diagnostics = errors.AsReadOnly();
            return false;
        }

        catalog = new PresentationAssetCatalog(
            document.PackId!,
            document.SchemaVersion,
            new ReadOnlyDictionary<string, PresentationAssetDefinition>(definitions));
        diagnostics = Array.Empty<string>();
        return true;
    }

    private static bool IsStableId([NotNullWhen(true)] string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength)
            return false;
        if (value[0] is < 'a' or > 'z')
            return false;
        for (var index = 1; index < value.Length; index++) {
            var character = value[index];
            if ((character is < 'a' or > 'z')
                && (character is < '0' or > '9')
                && character is not ('.' or '-' or '_'))
                return false;
        }
        return true;
    }

    private static void ValidateJsonObjectShape(
        JsonElement element,
        string location,
        List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name))
                    errors.Add($"The presentation manifest repeats property '{property.Name}' at {location}.");
                ValidateJsonObjectShape(property.Value, $"{location}.{property.Name}", errors);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                ValidateJsonObjectShape(item, $"{location}[{index++}]", errors);
        }
    }

    private static bool TryParseKind(string? value, out PresentationAssetKind kind)
    {
        if (value is not null) {
            if (string.Equals(value, "texture", StringComparison.OrdinalIgnoreCase)) {
                kind = PresentationAssetKind.Texture;
                return true;
            }
            if (string.Equals(value, "audio", StringComparison.OrdinalIgnoreCase)) {
                kind = PresentationAssetKind.Audio;
                return true;
            }
            if (string.Equals(value, "localization", StringComparison.OrdinalIgnoreCase)) {
                kind = PresentationAssetKind.Localization;
                return true;
            }
        }
        kind = default;
        return false;
    }

    private static bool TryValidateFrames(
        FrameDocument frame,
        out PresentationFrameLayout? layout,
        out string error)
    {
        layout = null;
        error = string.Empty;
        if (!frame.Loop.HasValue
            || frame.FrameCount is < 1 or > 256
            || frame.FrameWidth is < 1 or > 4096
            || frame.FrameHeight is < 1 or > 4096
            || frame.Columns is < 1 or > 64
            || frame.Rows is < 1 or > 64
            || frame.FrameCount > checked(frame.Columns * frame.Rows)
            || !double.IsFinite(frame.FramesPerSecond)
            || frame.FramesPerSecond <= 0
            || frame.FramesPerSecond > 240) {
            error = "frameCount, dimensions, atlas shape, or framesPerSecond is outside the supported bounds.";
            return false;
        }
        layout = new PresentationFrameLayout(
            frame.FrameCount,
            frame.FrameWidth,
            frame.FrameHeight,
            frame.Columns,
            frame.Rows,
            frame.FramesPerSecond,
            frame.Loop.Value);
        return true;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private sealed class ManifestDocument
    {
        public int SchemaVersion { get; set; }
        public string? PackId { get; set; }
        public List<AssetDocument?>? Assets { get; set; }
    }

    private sealed class AssetDocument
    {
        public string? Id { get; set; }
        public string? Kind { get; set; }
        public string? Path { get; set; }
        public FrameDocument? Frames { get; set; }
        public string? Text { get; set; }
    }

    private sealed class FrameDocument
    {
        public int FrameCount { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
        public double FramesPerSecond { get; set; }
        public bool? Loop { get; set; }
    }
}

/** Resource-path checks shared by manifest and optional pack selection. */
public static class PresentationAssetPath
{
    public static bool TryNormalizeRelativePath(
        string? raw,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) {
            error = "path is empty";
            return false;
        }
        if (raw.Length > PresentationAssetCatalog.MaximumPathLength) {
            error = "path is too long";
            return false;
        }
        if (raw.Any(char.IsControl) || raw.Contains('\\')) {
            error = "path contains a control character or backslash";
            return false;
        }
        if (raw.StartsWith('/') || raw.Contains("://", StringComparison.Ordinal) || raw.Contains(':')) {
            error = "absolute and URI-like paths are not permitted";
            return false;
        }
        var parts = raw.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or "..")) {
            error = "path contains an empty, current-directory, or parent-directory segment";
            return false;
        }
        if (parts.Any(part => part.EndsWith('.') || part.EndsWith(' '))) {
            error = "path contains a Windows-ambiguous trailing dot or space";
            return false;
        }
        normalized = string.Join('/', parts);
        return true;
    }

    public static bool TryNormalizeResourcePath(
        string? raw,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)
            || !raw.StartsWith("res://", StringComparison.Ordinal)
            || raw.IndexOf('\0') >= 0) {
            error = "only res:// resource paths are permitted";
            return false;
        }
        var relative = raw["res://".Length..];
        if (!TryNormalizeRelativePath(relative, out var safeRelative, out error))
            return false;
        normalized = $"res://{safeRelative}";
        return true;
    }

    public static bool TryCombineResourcePath(
        string manifestPath,
        string relativePath,
        out string combined,
        out string error)
    {
        combined = string.Empty;
        if (!TryNormalizeResourcePath(manifestPath, out var normalizedManifest, out error))
            return false;
        if (!TryNormalizeRelativePath(relativePath, out var normalizedRelative, out error))
            return false;
        var slash = normalizedManifest.LastIndexOf('/');
        var directory = slash < 0 ? normalizedManifest : normalizedManifest[..(slash + 1)];
        var candidate = directory + normalizedRelative;
        return TryNormalizeResourcePath(candidate, out combined, out error);
    }
}
