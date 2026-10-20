using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.Text.Json;

namespace Devilution.Client.PresentationAssets;

public enum NativePlayerLocation
{
    Town,
    Cathedral,
}

public enum NativePlayerArmor
{
    Light,
    Medium,
    Heavy,
}

public enum NativePlayerWeapon
{
    Unarmed,
    UnarmedShield,
    Sword,
    SwordShield,
    Bow,
    Axe,
    Mace,
    MaceShield,
    Staff,
}

public enum NativePlayerGraphic
{
    TownStand,
    TownWalk,
    Stand,
    Walk,
    Attack,
    Hit,
    Block,
    Lightning,
    Fire,
    Magic,
    Death,
}

public readonly record struct NativePlayerClipKey(
    uint ClassId,
    NativePlayerLocation Location,
    NativePlayerArmor Armor,
    NativePlayerWeapon Weapon,
    NativePlayerGraphic Graphic);

public readonly record struct NativePlayerLevelBindingKey(
    string ContentManifestHash,
    uint LevelId);

public sealed record NativePlayerClip(
    NativePlayerClipKey Key,
    string Path,
    int FrameWidth,
    int FrameHeight,
    int FramesPerDirection,
    int TicksPerFrame,
    int ActionFrame,
    int? AnchorWidth = null,
    int? AnimationFrames = null)
{
    public int EffectiveAnchorWidth => AnchorWidth ?? FrameWidth;

    public int EffectiveAnimationFrames => AnimationFrames ?? FramesPerDirection;
}

/** Validated client-only catalog of native player animation atlases. */
public sealed class NativePlayerCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumClipCount = 4096;
    public const int MaximumLevelBindingCount = 4096;
    public const int MaximumSpellCount = 2048;
    public const int MaximumDocumentCharacters = 8 * 1024 * 1024;
    public const int MaximumAtlasDimension = 8192;
    public const int MaximumAtlasPixels = 4 * 1024 * 1024;

    private static readonly string[] RootProperties = ["schemaVersion", "packId", "contentManifestHashes", "levelBindings", "clips", "spells"];
    private static readonly string[] ClipProperties = [
        "classId", "location", "armor", "weapon", "graphic", "path",
        "frameWidth", "frameHeight", "framesPerDirection", "animationFrames", "ticksPerFrame", "actionFrame", "anchorWidth",
    ];
    private static readonly string[] SpellProperties = ["spellId", "graphic"];
    private static readonly string[] LevelBindingProperties = ["contentManifestHash", "levelId", "location"];

    private readonly IReadOnlyDictionary<NativePlayerClipKey, NativePlayerClip> clips;
    private readonly IReadOnlyDictionary<uint, NativePlayerGraphic> spellGraphics;
    private readonly IReadOnlySet<string> contentManifestHashes;
    private readonly IReadOnlyDictionary<NativePlayerLevelBindingKey, NativePlayerLocation> levelBindings;

    private NativePlayerCatalog(
        string packId,
        IReadOnlySet<string> contentManifestHashes,
        IReadOnlyDictionary<NativePlayerLevelBindingKey, NativePlayerLocation> levelBindings,
        IReadOnlyDictionary<NativePlayerClipKey, NativePlayerClip> clips,
        IReadOnlyDictionary<uint, NativePlayerGraphic> spellGraphics)
    {
        PackId = packId;
        this.contentManifestHashes = contentManifestHashes;
        this.levelBindings = levelBindings;
        this.clips = clips;
        this.spellGraphics = spellGraphics;
    }

    public string PackId { get; }

    public IReadOnlySet<string> ContentManifestHashes => contentManifestHashes;

    public IReadOnlyDictionary<NativePlayerLevelBindingKey, NativePlayerLocation> LevelBindings => levelBindings;

    public bool SupportsContentManifestHash(string? contentManifestHash)
        => contentManifestHash is not null && contentManifestHashes.Contains(contentManifestHash);

    public bool TryResolveLocation(
        string? contentManifestHash,
        uint levelId,
        out NativePlayerLocation location)
    {
        location = default;
        return contentManifestHash is not null
            && levelBindings.TryGetValue(new NativePlayerLevelBindingKey(contentManifestHash, levelId), out location);
    }

    public IReadOnlyDictionary<NativePlayerClipKey, NativePlayerClip> Clips => clips;

    public IReadOnlyDictionary<uint, NativePlayerGraphic> SpellGraphics => spellGraphics;

    public bool TryGetClip(NativePlayerClipKey key, out NativePlayerClip clip)
        => clips.TryGetValue(key, out clip!);

    public bool TryGetSpellGraphic(uint spellId, out NativePlayerGraphic graphic)
        => spellGraphics.TryGetValue(spellId, out graphic);

    /** Parses the compact native-player schema without loading Godot resources. */
    public static bool TryParse(
        string? json,
        out NativePlayerCatalog? catalog,
        out IReadOnlyList<string> diagnostics)
    {
        catalog = null;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) {
            errors.Add("The native player catalog is empty.");
            diagnostics = errors.AsReadOnly();
            return false;
        }
        if (json.Length > MaximumDocumentCharacters) {
            errors.Add("The native player catalog exceeds the 8 MiB size limit.");
            diagnostics = errors.AsReadOnly();
            return false;
        }

        try {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 24,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                errors.Add("The native player catalog root must be an object.");
                diagnostics = errors.AsReadOnly();
                return false;
            }
            if (!ValidateProperties(root, "catalog", RootProperties, errors)) {
                diagnostics = errors.AsReadOnly();
                return false;
            }

            if (!TryGetInt32(root, "schemaVersion", out var schemaVersion)
                || schemaVersion != CurrentSchemaVersion)
                errors.Add($"The native player catalog schemaVersion must be {CurrentSchemaVersion}.");
            if (!TryGetString(root, "packId", out var packId) || !IsStableId(packId))
                errors.Add("The native player catalog packId must be a stable lowercase identifier.");
            var parsedContentHashes = new HashSet<string>(StringComparer.Ordinal);
            if (!root.TryGetProperty("contentManifestHashes", out var contentHashesElement)
                || contentHashesElement.ValueKind != JsonValueKind.Array) {
                errors.Add("The native player catalog must contain a contentManifestHashes array.");
            } else if (contentHashesElement.GetArrayLength() is < 1 or > 64) {
                errors.Add("The native player catalog must bind between 1 and 64 content manifest hashes.");
            } else {
                var index = 0;
                foreach (var entry in contentHashesElement.EnumerateArray()) {
                    if (entry.ValueKind != JsonValueKind.String
                        || !IsSha256(entry.GetString())) {
                        errors.Add($"Native player contentManifestHashes[{index}] must be a lowercase SHA-256 hash.");
                    } else if (!parsedContentHashes.Add(entry.GetString()!)) {
                        errors.Add($"Native player content manifest hash '{entry.GetString()}' is declared more than once.");
                    }
                    index++;
                }
            }
            if (!root.TryGetProperty("clips", out var clipsElement)
                || clipsElement.ValueKind != JsonValueKind.Array) {
                errors.Add("The native player catalog must contain a clips array.");
            } else if (clipsElement.GetArrayLength() > MaximumClipCount) {
                errors.Add($"The native player catalog contains more than {MaximumClipCount} clips.");
            }

            var parsedLevelBindings = new Dictionary<NativePlayerLevelBindingKey, NativePlayerLocation>();
            if (!root.TryGetProperty("levelBindings", out var levelBindingsElement)
                || levelBindingsElement.ValueKind != JsonValueKind.Array) {
                errors.Add("The native player catalog must contain a levelBindings array.");
            } else if (levelBindingsElement.GetArrayLength() > MaximumLevelBindingCount) {
                errors.Add($"The native player catalog contains more than {MaximumLevelBindingCount} level bindings.");
            } else {
                var index = 0;
                foreach (var entry in levelBindingsElement.EnumerateArray()) {
                    if (!TryParseLevelBinding(entry, index++, out var key, out var location, out var error)) {
                        errors.Add(error);
                        continue;
                    }
                    if (!parsedContentHashes.Contains(key.ContentManifestHash)) {
                        errors.Add($"Native player levelBindings[{index - 1}] references a content hash not declared in contentManifestHashes.");
                        continue;
                    }
                    if (!parsedLevelBindings.TryAdd(key, location))
                        errors.Add($"Native player level binding '{key.ContentManifestHash}/{key.LevelId}' is declared more than once.");
                }
            }

            var parsedClips = new Dictionary<NativePlayerClipKey, NativePlayerClip>();
            if (clipsElement.ValueKind == JsonValueKind.Array
                && clipsElement.GetArrayLength() <= MaximumClipCount) {
                var index = 0;
                foreach (var entry in clipsElement.EnumerateArray()) {
                    if (!TryParseClip(entry, index++, out var clip, out var error)) {
                        errors.Add(error);
                        continue;
                    }
                    var parsedClip = clip!;
                    if (!parsedClips.TryAdd(parsedClip.Key, parsedClip))
                        errors.Add($"Native player clip '{FormatKey(parsedClip.Key)}' is declared more than once.");
                }
            }

            var parsedSpellGraphics = new Dictionary<uint, NativePlayerGraphic>();
            if (root.TryGetProperty("spells", out var spellsElement)) {
                if (spellsElement.ValueKind != JsonValueKind.Array) {
                    errors.Add("The optional native player spells field must be an array.");
                } else if (spellsElement.GetArrayLength() > MaximumSpellCount) {
                    errors.Add($"The native player catalog contains more than {MaximumSpellCount} spell bindings.");
                } else {
                    var index = 0;
                    foreach (var entry in spellsElement.EnumerateArray()) {
                        if (!TryParseSpell(entry, index++, out var spellId, out var graphic, out var error)) {
                            errors.Add(error);
                            continue;
                        }
                        if (!parsedSpellGraphics.TryAdd(spellId, graphic))
                            errors.Add($"Native spell id {spellId} is declared more than once.");
                    }
                }
            }

            if (errors.Count > 0) {
                diagnostics = errors.AsReadOnly();
                return false;
            }
            catalog = new NativePlayerCatalog(
                packId!,
                parsedContentHashes.ToFrozenSet(StringComparer.Ordinal),
                new ReadOnlyDictionary<NativePlayerLevelBindingKey, NativePlayerLocation>(parsedLevelBindings),
                new ReadOnlyDictionary<NativePlayerClipKey, NativePlayerClip>(parsedClips),
                new ReadOnlyDictionary<uint, NativePlayerGraphic>(parsedSpellGraphics));
            diagnostics = Array.Empty<string>();
            return true;
        } catch (JsonException exception) {
            errors.Add($"The native player catalog is not valid JSON: {exception.Message}");
            diagnostics = errors.AsReadOnly();
            return false;
        } catch (InvalidOperationException exception) {
            errors.Add($"The native player catalog has an invalid JSON shape: {exception.Message}");
            diagnostics = errors.AsReadOnly();
            return false;
        }
    }

    public static string FormatKey(NativePlayerClipKey key)
        => $"{key.ClassId}/{key.Location.ToString().ToLowerInvariant()}/{key.Armor.ToString().ToLowerInvariant()}/{FormatWeapon(key.Weapon)}/{FormatGraphic(key.Graphic)}";

    public static string FormatWeapon(NativePlayerWeapon weapon)
        => weapon switch {
            NativePlayerWeapon.Unarmed => "unarmed",
            NativePlayerWeapon.UnarmedShield => "unarmed-shield",
            NativePlayerWeapon.Sword => "sword",
            NativePlayerWeapon.SwordShield => "sword-shield",
            NativePlayerWeapon.Bow => "bow",
            NativePlayerWeapon.Axe => "axe",
            NativePlayerWeapon.Mace => "mace",
            NativePlayerWeapon.MaceShield => "mace-shield",
            NativePlayerWeapon.Staff => "staff",
            _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
        };

    public static string FormatGraphic(NativePlayerGraphic graphic)
        => graphic switch {
            NativePlayerGraphic.TownStand => "town-stand",
            NativePlayerGraphic.TownWalk => "town-walk",
            NativePlayerGraphic.Stand => "stand",
            NativePlayerGraphic.Walk => "walk",
            NativePlayerGraphic.Attack => "attack",
            NativePlayerGraphic.Hit => "hit",
            NativePlayerGraphic.Block => "block",
            NativePlayerGraphic.Lightning => "lightning",
            NativePlayerGraphic.Fire => "fire",
            NativePlayerGraphic.Magic => "magic",
            NativePlayerGraphic.Death => "death",
            _ => throw new ArgumentOutOfRangeException(nameof(graphic)),
        };

    private static bool TryParseClip(
        JsonElement entry,
        int index,
        out NativePlayerClip? clip,
        out string error)
    {
        clip = null;
        error = string.Empty;
        var location = $"clips[{index}]";
        if (entry.ValueKind != JsonValueKind.Object) {
            error = $"Native player {location} must be an object.";
            return false;
        }
        if (!ValidateProperties(entry, location, ClipProperties, out error))
            return false;

        if (!TryGetUInt32(entry, "classId", out var classId) || classId > 2) {
            error = $"Native player {location} classId must be one of the base classes 0, 1, or 2.";
            return false;
        }
        if (!TryGetString(entry, "location", out var locationName)
            || !TryParseLocation(locationName, out var nativeLocation)) {
            error = $"Native player {location} has an unsupported location.";
            return false;
        }
        if (!TryGetString(entry, "armor", out var armorName)
            || !TryParseArmor(armorName, out var armor)) {
            error = $"Native player {location} has an unsupported armor variant.";
            return false;
        }
        if (!TryGetString(entry, "weapon", out var weaponName)
            || !TryParseWeapon(weaponName, out var weapon)) {
            error = $"Native player {location} has an unsupported weapon variant.";
            return false;
        }
        if (!TryGetString(entry, "graphic", out var graphicName)
            || !TryParseGraphic(graphicName, out var graphic)) {
            error = $"Native player {location} has an unsupported graphic.";
            return false;
        }
        if (!IsGraphicAvailableAtLocation(graphic, nativeLocation)) {
            error = $"Native player {location} graphic is not available for its location.";
            return false;
        }
        if (!IsNormalizedWeapon(nativeLocation, graphic, weapon)) {
            error = $"Native player {location} weapon is not a normalized native graphic variant.";
            return false;
        }
        if (!TryGetString(entry, "path", out var path)) {
            error = $"Native player {location} path is missing or empty.";
            return false;
        }
        if (path!.Length > PresentationAssetCatalog.MaximumPathLength) {
            error = $"Native player {location} path is too long.";
            return false;
        }
        if (!PresentationAssetPath.TryNormalizeRelativePath(path, out var normalizedPath, out var pathError)) {
            error = $"Native player {location} path is invalid: {pathError}";
            return false;
        }
        if (!TryGetPositiveInt32(entry, "frameWidth", out var frameWidth)
            || !TryGetPositiveInt32(entry, "frameHeight", out var frameHeight)
            || !TryGetPositiveInt32(entry, "framesPerDirection", out var framesPerDirection)
            || !TryGetPositiveInt32(entry, "ticksPerFrame", out var ticksPerFrame)
            || !TryGetInt32(entry, "actionFrame", out var actionFrame)) {
            error = $"Native player {location} frame metadata must contain positive dimensions, counts and ticks plus a non-negative actionFrame.";
            return false;
        }
        int? anchorWidth = null;
        if (entry.TryGetProperty("anchorWidth", out _)) {
            if (!TryGetPositiveInt32(entry, "anchorWidth", out var parsedAnchorWidth)
                || parsedAnchorWidth > frameWidth) {
                error = $"Native player {location} anchorWidth must be positive and no larger than frameWidth.";
                return false;
            }
            anchorWidth = parsedAnchorWidth;
        }
        int? animationFrames = null;
        if (entry.TryGetProperty("animationFrames", out _)) {
            if (!TryGetPositiveInt32(entry, "animationFrames", out var parsedAnimationFrames)
                || parsedAnimationFrames > framesPerDirection) {
                error = $"Native player {location} animationFrames must be positive and no larger than framesPerDirection.";
                return false;
            }
            animationFrames = parsedAnimationFrames;
        }
        var effectiveAnimationFrames = animationFrames ?? framesPerDirection;
        if (ticksPerFrame > sbyte.MaxValue
            || actionFrame < 0
            || actionFrame > effectiveAnimationFrames) {
            error = $"Native player {location} ticksPerFrame or actionFrame is outside native animation bounds.";
            return false;
        }
        var atlasWidth = (long)frameWidth * framesPerDirection;
        var atlasHeight = (long)frameHeight * 8;
        if (atlasWidth > MaximumAtlasDimension
            || atlasHeight > MaximumAtlasDimension
            || atlasWidth * atlasHeight > MaximumAtlasPixels) {
            error = $"Native player {location} atlas dimensions exceed the bounded pixel dimensions.";
            return false;
        }

        var key = new NativePlayerClipKey(classId, nativeLocation, armor, weapon, graphic);
        clip = new NativePlayerClip(
            key,
            normalizedPath,
            frameWidth,
            frameHeight,
            framesPerDirection,
            ticksPerFrame,
            actionFrame,
            anchorWidth,
            animationFrames);
        return true;
    }

    private static bool TryParseSpell(
        JsonElement entry,
        int index,
        out uint spellId,
        out NativePlayerGraphic graphic,
        out string error)
    {
        spellId = 0;
        graphic = default;
        error = string.Empty;
        var location = $"spells[{index}]";
        if (entry.ValueKind != JsonValueKind.Object) {
            error = $"Native player {location} must be an object.";
            return false;
        }
        if (!ValidateProperties(entry, location, SpellProperties, out error))
            return false;
        if (!TryGetUInt32(entry, "spellId", out spellId) || spellId == 0) {
            error = $"Native player {location} spellId must be a non-zero uint32.";
            return false;
        }
        if (!TryGetString(entry, "graphic", out var graphicName)
            || graphicName is not ("fire" or "lightning" or "magic")) {
            error = $"Native player {location} graphic must be fire, lightning, or magic.";
            return false;
        }
        graphic = graphicName switch {
            "fire" => NativePlayerGraphic.Fire,
            "lightning" => NativePlayerGraphic.Lightning,
            _ => NativePlayerGraphic.Magic,
        };
        return true;
    }

    private static bool TryParseLevelBinding(
        JsonElement entry,
        int index,
        out NativePlayerLevelBindingKey key,
        out NativePlayerLocation location,
        out string error)
    {
        key = default;
        location = default;
        error = string.Empty;
        var bindingLocation = $"levelBindings[{index}]";
        if (entry.ValueKind != JsonValueKind.Object) {
            error = $"Native player {bindingLocation} must be an object.";
            return false;
        }
        if (!ValidateProperties(entry, bindingLocation, LevelBindingProperties, out error))
            return false;
        if (!TryGetString(entry, "contentManifestHash", out var contentManifestHash)
            || !IsSha256(contentManifestHash)) {
            error = $"Native player {bindingLocation} contentManifestHash must be a lowercase SHA-256 hash.";
            return false;
        }
        if (!TryGetUInt32(entry, "levelId", out var levelId)) {
            error = $"Native player {bindingLocation} levelId must be a uint32.";
            return false;
        }
        if (!TryGetString(entry, "location", out var locationName)
            || !TryParseLocation(locationName, out location)) {
            error = $"Native player {bindingLocation} location must be town or cathedral.";
            return false;
        }
        key = new NativePlayerLevelBindingKey(contentManifestHash!, levelId);
        return true;
    }

    private static bool ValidateProperties(
        JsonElement element,
        string location,
        IReadOnlyCollection<string> allowed,
        out string error)
    {
        error = string.Empty;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject()) {
            if (!names.Add(property.Name)) {
                error = $"The native player catalog repeats property '{property.Name}' at {location}.";
                return false;
            }
            if (!allowed.Contains(property.Name, StringComparer.Ordinal)) {
                error = $"The native player catalog has unknown property '{property.Name}' at {location}.";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateProperties(
        JsonElement element,
        string location,
        IReadOnlyCollection<string> allowed,
        List<string> errors)
    {
        if (ValidateProperties(element, location, allowed, out var error))
            return true;
        errors.Add(error);
        return false;
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

    private static bool IsStableId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64 || value[0] is < 'a' or > 'z')
            return false;
        return value.Skip(1).All(character => character is >= 'a' and <= 'z'
            or >= '0' and <= '9' or '.' or '-' or '_');
    }

    private static bool IsSha256(string? value)
    {
        if (value is null || value.Length != 64)
            return false;
        return value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool TryParseLocation(string? value, out NativePlayerLocation location)
    {
        location = value switch {
            "town" => NativePlayerLocation.Town,
            "cathedral" => NativePlayerLocation.Cathedral,
            _ => default,
        };
        return value is "town" or "cathedral";
    }

    private static bool TryParseArmor(string? value, out NativePlayerArmor armor)
    {
        armor = value switch {
            "light" => NativePlayerArmor.Light,
            "medium" => NativePlayerArmor.Medium,
            "heavy" => NativePlayerArmor.Heavy,
            _ => default,
        };
        return value is "light" or "medium" or "heavy";
    }

    private static bool TryParseWeapon(string? value, out NativePlayerWeapon weapon)
    {
        weapon = value switch {
            "unarmed" => NativePlayerWeapon.Unarmed,
            "unarmed-shield" => NativePlayerWeapon.UnarmedShield,
            "sword" => NativePlayerWeapon.Sword,
            "sword-shield" => NativePlayerWeapon.SwordShield,
            "bow" => NativePlayerWeapon.Bow,
            "axe" => NativePlayerWeapon.Axe,
            "mace" => NativePlayerWeapon.Mace,
            "mace-shield" => NativePlayerWeapon.MaceShield,
            "staff" => NativePlayerWeapon.Staff,
            _ => default,
        };
        return value is "unarmed" or "unarmed-shield" or "sword" or "sword-shield"
            or "bow" or "axe" or "mace" or "mace-shield" or "staff";
    }

    private static bool TryParseGraphic(string? value, out NativePlayerGraphic graphic)
    {
        graphic = value switch {
            "town-stand" => NativePlayerGraphic.TownStand,
            "town-walk" => NativePlayerGraphic.TownWalk,
            "stand" => NativePlayerGraphic.Stand,
            "walk" => NativePlayerGraphic.Walk,
            "attack" => NativePlayerGraphic.Attack,
            "hit" => NativePlayerGraphic.Hit,
            "block" => NativePlayerGraphic.Block,
            "lightning" => NativePlayerGraphic.Lightning,
            "fire" => NativePlayerGraphic.Fire,
            "magic" => NativePlayerGraphic.Magic,
            "death" => NativePlayerGraphic.Death,
            _ => default,
        };
        return value is "town-stand" or "town-walk" or "stand" or "walk" or "attack"
            or "hit" or "block" or "lightning" or "fire" or "magic" or "death";
    }

    private static bool IsGraphicAvailableAtLocation(
        NativePlayerGraphic graphic,
        NativePlayerLocation location)
        => location == NativePlayerLocation.Town
            ? graphic is NativePlayerGraphic.TownStand or NativePlayerGraphic.TownWalk
                or NativePlayerGraphic.Lightning or NativePlayerGraphic.Fire or NativePlayerGraphic.Magic
                or NativePlayerGraphic.Death
            : graphic is not (NativePlayerGraphic.TownStand or NativePlayerGraphic.TownWalk);

    private static bool IsNormalizedWeapon(
        NativePlayerLocation location,
        NativePlayerGraphic graphic,
        NativePlayerWeapon weapon)
    {
        if (graphic == NativePlayerGraphic.Death)
            return weapon == NativePlayerWeapon.Unarmed;
        if (location == NativePlayerLocation.Town
            && graphic is NativePlayerGraphic.Lightning or NativePlayerGraphic.Fire or NativePlayerGraphic.Magic)
            return weapon is not (NativePlayerWeapon.Sword or NativePlayerWeapon.SwordShield
                or NativePlayerWeapon.Mace or NativePlayerWeapon.MaceShield);
        return true;
    }
}
