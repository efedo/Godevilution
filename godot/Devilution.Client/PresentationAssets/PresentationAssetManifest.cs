using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Protocol.V1;

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

/** Presentation-only action selector; Idle matches a missing/unspecified action. */
public enum PresentationActorAction
{
    Idle,
    Move,
    Attack,
    Cast,
    Death,
    LevelTransition,
    RestartTown,
}

/** Typed optional actor filters. A null field is a wildcard. */
public sealed record PresentationActorSelector(
    uint? CharacterClassId,
    PresentationActorAction? Action,
    PlayerCombatMode? CombatMode,
    PlayerDirection? FacingDirection)
{
    public int Specificity
        => (CharacterClassId.HasValue ? 1 : 0)
            + (Action.HasValue ? 1 : 0)
            + (CombatMode.HasValue ? 1 : 0)
            + (FacingDirection.HasValue ? 1 : 0);
}

/** A validated semantic player binding. It never changes protocol or gameplay state. */
public sealed record PresentationActorRule(
    string Id,
    string AssetId,
    PresentationActorSelector Selector);

/** Result of deterministic semantic selection, exposed for actual-scene evidence. */
public sealed record PresentationActorSelection(
    string RuleId,
    string AssetId,
    int Specificity);

/** Per-entity observation captured only after an actual world draw lookup. */
public sealed record PresentationPlayerTextureBindingObservation(
    uint EntityId,
    bool IsSemanticSelection,
    string? RuleId,
    string AssetId,
    PresentationTextureBindingObservation? Binding);

/** Immutable, validated client-only presentation catalog. */
public sealed class PresentationAssetCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumAssetCount = 256;
    public const int MaximumIdLength = 64;
    public const int MaximumPathLength = 240;
    public const int MaximumTextLength = 1024;
    public const int MaximumActorRuleCount = 512;

    private readonly IReadOnlyDictionary<string, PresentationAssetDefinition> assets;
    private readonly IReadOnlyList<PresentationActorRule> actorRules;

    private PresentationAssetCatalog(
        string packId,
        int schemaVersion,
        IReadOnlyDictionary<string, PresentationAssetDefinition> assets,
        IReadOnlyList<PresentationActorRule> actorRules)
    {
        PackId = packId;
        SchemaVersion = schemaVersion;
        this.assets = assets;
        this.actorRules = actorRules;
    }

    public string PackId { get; }

    public int SchemaVersion { get; }

    public IReadOnlyDictionary<string, PresentationAssetDefinition> Assets => assets;

    public IReadOnlyList<PresentationActorRule> ActorRules => actorRules;

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
        if (document.Actors is not null && document.Actors.Count > MaximumActorRuleCount)
            errors.Add($"The presentation manifest contains more than {MaximumActorRuleCount} actor rules.");

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

        var actorRules = new List<PresentationActorRule>();
        var actorIds = new HashSet<string>(StringComparer.Ordinal);
        if (document.Actors is not null && document.Actors.Count <= MaximumActorRuleCount) {
            foreach (var entry in document.Actors) {
                if (entry is null) {
                    errors.Add("The presentation manifest contains a null actor rule.");
                    continue;
                }
                if (!IsStableId(entry.Id, MaximumIdLength)) {
                    errors.Add($"Actor rule id '{entry.Id ?? "<null>"}' is not a stable identifier.");
                    continue;
                }
                if (!actorIds.Add(entry.Id)) {
                    errors.Add($"Actor rule id '{entry.Id}' is declared more than once.");
                    continue;
                }
                if (!IsStableId(entry.AssetId, MaximumIdLength)
                    || !definitions.TryGetValue(entry.AssetId!, out var actorAsset)
                    || actorAsset is null
                    || actorAsset.Kind != PresentationAssetKind.Texture) {
                    errors.Add($"Actor rule '{entry.Id}' must reference a declared texture asset.");
                    continue;
                }
                if (!TryParseActorSelector(entry, out var selector, out var selectorError)) {
                    errors.Add($"Actor rule '{entry.Id}' has an invalid selector: {selectorError}");
                    continue;
                }
                actorRules.Add(new PresentationActorRule(entry.Id, entry.AssetId!, selector));
            }
        }
        for (var left = 0; left < actorRules.Count; left++) {
            for (var right = left + 1; right < actorRules.Count; right++) {
                var first = actorRules[left];
                var second = actorRules[right];
                if (first.Selector.Specificity == second.Selector.Specificity
                    && SelectorsOverlap(first.Selector, second.Selector))
                    errors.Add($"Actor rules '{first.Id}' and '{second.Id}' have an ambiguous equal-specificity overlap.");
            }
        }

        if (errors.Count > 0) {
            diagnostics = errors.AsReadOnly();
            return false;
        }

        catalog = new PresentationAssetCatalog(
            document.PackId!,
            document.SchemaVersion,
            new ReadOnlyDictionary<string, PresentationAssetDefinition>(definitions),
            actorRules.AsReadOnly());
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

    private static bool TryParseActorSelector(
        ActorDocument entry,
        out PresentationActorSelector selector,
        out string error)
    {
        selector = default!;
        error = string.Empty;
        if ((entry.Action is not null && string.IsNullOrWhiteSpace(entry.Action))
            || (entry.CombatMode is not null && string.IsNullOrWhiteSpace(entry.CombatMode))
            || (entry.Facing is not null && string.IsNullOrWhiteSpace(entry.Facing))) {
            error = "selector values must not be empty";
            return false;
        }
        if (entry.ClassId is null && string.IsNullOrWhiteSpace(entry.Action)
            && string.IsNullOrWhiteSpace(entry.CombatMode)
            && string.IsNullOrWhiteSpace(entry.Facing)) {
            error = "at least one typed selector is required";
            return false;
        }
        PresentationActorAction? action = null;
        if (!string.IsNullOrWhiteSpace(entry.Action)
            && !TryParseActorAction(entry.Action, out action)) {
            error = $"unsupported action '{entry.Action}'";
            return false;
        }
        PlayerCombatMode? combatMode = null;
        if (!string.IsNullOrWhiteSpace(entry.CombatMode)
            && !TryParseEnumName<PlayerCombatMode>(entry.CombatMode, out combatMode)) {
            error = $"unsupported combat mode '{entry.CombatMode}'";
            return false;
        }
        PlayerDirection? facing = null;
        if (!string.IsNullOrWhiteSpace(entry.Facing)
            && !TryParseEnumName<PlayerDirection>(entry.Facing, out facing)) {
            error = $"unsupported facing direction '{entry.Facing}'";
            return false;
        }
        selector = new PresentationActorSelector(entry.ClassId, action, combatMode, facing);
        return true;
    }

    private static bool SelectorsOverlap(
        PresentationActorSelector first,
        PresentationActorSelector second)
        => (!first.CharacterClassId.HasValue || !second.CharacterClassId.HasValue
            || first.CharacterClassId == second.CharacterClassId)
            && (!first.Action.HasValue || !second.Action.HasValue
                || first.Action == second.Action)
            && (!first.CombatMode.HasValue || !second.CombatMode.HasValue
                || first.CombatMode == second.CombatMode)
            && (!first.FacingDirection.HasValue || !second.FacingDirection.HasValue
                || first.FacingDirection == second.FacingDirection);

    private static bool TryParseActorAction(string value, out PresentationActorAction? action)
    {
        action = value.Trim().ToLowerInvariant() switch {
            "idle" => PresentationActorAction.Idle,
            "move" => PresentationActorAction.Move,
            "attack" => PresentationActorAction.Attack,
            "cast" => PresentationActorAction.Cast,
            "death" => PresentationActorAction.Death,
            "level-transition" or "level_transition" or "leveltransition" => PresentationActorAction.LevelTransition,
            "restart-town" or "restart_town" or "restarttown" => PresentationActorAction.RestartTown,
            _ => null,
        };
        return action.HasValue;
    }

    private static bool TryParseEnumName<T>(string value, out T? parsed)
        where T : struct, Enum
    {
        parsed = null;
        var normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
        foreach (var candidate in Enum.GetValues<T>()) {
            if (string.Equals(candidate.ToString().Replace("_", string.Empty), normalized, StringComparison.OrdinalIgnoreCase)) {
                parsed = candidate;
                return true;
            }
        }
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
        public List<ActorDocument?>? Actors { get; set; }
    }

    private sealed class ActorDocument
    {
        public string? Id { get; set; }
        public string? AssetId { get; set; }
        public uint? ClassId { get; set; }
        public string? Action { get; set; }
        public string? CombatMode { get; set; }
        public string? Facing { get; set; }
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
