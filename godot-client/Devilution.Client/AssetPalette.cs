using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace Devilution.Client;

/** Loads an optional client-only presentation pack while retaining legacy and procedural fallbacks. */
public sealed class AssetPalette
{
    public const string DefaultManifestPath = "res://assets/presentation-pack.json";
    public const string ManifestEnvironmentVariable = "GODEVILUTION_PRESENTATION_PACK";

    private readonly Dictionary<string, Texture2D?> textures = [];
    private readonly Dictionary<string, AudioStream?> audio = [];
    private readonly HashSet<string> loadedTextureKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingTextureKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> loadedAudioKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingAudioKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PresentationFrameRegion?> lastDrawnTextureRegions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> lastDrawnTexturePaths = new(StringComparer.Ordinal);
    private readonly PresentationFrameClock frameClock = new();
    private readonly string manifestPath;
    private readonly PresentationAssetCatalog? catalog;
    private readonly PresentationAssetResolver? resolver;
    private readonly PresentationLocalization? localization;
    private readonly string? diagnostic;

    public AssetPalette()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ManifestEnvironmentVariable);
        manifestPath = string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultManifestPath
            : configuredPath.Trim();
        if (!PresentationAssetPath.TryNormalizeResourcePath(manifestPath, out var normalizedPath, out var pathError)) {
            diagnostic = $"Presentation pack refused: {pathError}";
            return;
        }
        manifestPath = normalizedPath;
        if (!FileAccess.FileExists(manifestPath)) {
            diagnostic = "Presentation pack manifest was not found; using legacy/procedural fallbacks.";
            return;
        }

        using var file = FileAccess.Open(manifestPath, FileAccess.ModeFlags.Read);
        if (file is null) {
            diagnostic = "Presentation pack manifest could not be opened; using legacy/procedural fallbacks.";
            return;
        }
        if (file.GetLength() > 1_000_000) {
            diagnostic = "Presentation pack manifest exceeds the 1 MB size limit; using legacy/procedural fallbacks.";
            return;
        }
        if (!PresentationAssetCatalog.TryParse(file.GetAsText(), out catalog, out var diagnostics)) {
            diagnostic = string.Join(" ", diagnostics);
            return;
        }
        resolver = new PresentationAssetResolver(catalog, manifestPath);
        localization = new PresentationLocalization(catalog);
    }

    public Texture2D? Get(string assetName)
        => GetTextureFrame(assetName)?.Texture;

    internal TextureFrame? GetTextureFrame(string assetName)
    {
        if (textures.TryGetValue(assetName, out var cached)) {
            if (cached is null)
                return null;
            var cachedRegion = ResolveRegion(assetName);
            RecordTextureUse(assetName, cachedRegion);
            return new TextureFrame(cached, cachedRegion);
        }

        PresentationAssetDefinition? definition = null;
        var known = false;
        if (catalog is not null && catalog.TryGet(assetName, out var knownDefinition)) {
            definition = knownDefinition;
            known = true;
        }
        PresentationAssetResolution? resolution = null;
        PresentationFrameRegion? declaredRegion = null;
        var declared = false;
        if (known && definition!.Kind == PresentationAssetKind.Texture && resolver is not null) {
            if (resolver.TryResolveFrame(
                    assetName,
                    ResolveFrameIndex(assetName),
                    out var resolved,
                    out var resolvedRegion)) {
                declared = true;
                resolution = resolved;
                declaredRegion = resolvedRegion;
            }
        }
        if (known && !declared) {
            textures[assetName] = null;
            missingTextureKeys.Add(assetName);
            lastDrawnTextureRegions[assetName] = null;
            lastDrawnTexturePaths[assetName] = null;
            return null;
        }
        var resourcePath = declared
            ? resolution!.ResourcePath
            : LegacyTexturePath(assetName);
        if (resourcePath is null) {
            textures[assetName] = null;
            missingTextureKeys.Add(assetName);
            lastDrawnTextureRegions[assetName] = null;
            lastDrawnTexturePaths[assetName] = null;
            return null;
        }
        var texture = ResourceLoader.Exists(resourcePath, "Texture2D")
            ? ResourceLoader.Load<Texture2D>(resourcePath)
            : null;
        if (declared
            && texture is not null
            && definition!.Frames is { } layout
            && (texture.GetWidth() < layout.Columns * layout.FrameWidth
                || texture.GetHeight() < layout.Rows * layout.FrameHeight))
            texture = null;
        textures[assetName] = texture;
        if (texture is null)
            missingTextureKeys.Add(assetName);
        else
            loadedTextureKeys.Add(assetName);
        lastDrawnTextureRegions[assetName] = texture is null || !declared ? null : declaredRegion;
        lastDrawnTexturePaths[assetName] = resourcePath;
        return texture is null
            ? null
            : new TextureFrame(texture, declared ? declaredRegion : null);
    }

    /** Resolves semantic actor art without allowing a missing selected asset to fall through to another art key. */
    internal TextureFrame? GetPlayerTextureFrame(
        PlayerSnapshot player,
        out PresentationActorSelection? selection,
        out string assetName)
    {
        selection = null;
        assetName = "player";
        if (catalog is not null
            && SemanticPlayerPresentation.TrySelect(catalog, player, out var selected)) {
            selection = selected;
            assetName = selected.AssetId;
        }
        return GetTextureFrame(assetName);
    }

    internal AudioStream? GetAudio(string assetName)
    {
        if (audio.TryGetValue(assetName, out var cached))
            return cached;

        PresentationAssetDefinition? definition = null;
        var known = false;
        if (catalog is not null && catalog.TryGet(assetName, out var knownDefinition)) {
            definition = knownDefinition;
            known = true;
        }
        PresentationAssetResolution? resolution = null;
        var declared = false;
        if (known && definition!.Kind == PresentationAssetKind.Audio && resolver is not null) {
            if (resolver.TryResolve(assetName, out var resolved)) {
                declared = true;
                resolution = resolved;
            }
        }
        if (known && !declared) {
            audio[assetName] = null;
            missingAudioKeys.Add(assetName);
            return null;
        }
        var resourcePath = declared
            ? resolution!.ResourcePath
            : LegacyAudioPath(assetName);
        if (resourcePath is null) {
            audio[assetName] = null;
            missingAudioKeys.Add(assetName);
            return null;
        }
        var stream = ResourceLoader.Exists(resourcePath, "AudioStream")
            ? ResourceLoader.Load<AudioStream>(resourcePath)
            : null;
        audio[assetName] = stream;
        if (stream is null)
            missingAudioKeys.Add(assetName);
        else
            loadedAudioKeys.Add(assetName);
        return stream;
    }

    internal string Localize(string key, string fallback)
        => localization?.Resolve(key, fallback) ?? fallback;

    internal void AdvanceAnimation(double delta)
        => frameClock.Advance(delta);

    internal void ResetAnimation()
        => frameClock.Reset();

    internal PresentationPackObservation ObservePack()
        => new(
            manifestPath,
            catalog is not null,
            catalog?.PackId,
            catalog?.SchemaVersion ?? 0,
            diagnostic,
            loadedTextureKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            missingTextureKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            loadedAudioKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            missingAudioKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray());

    internal PresentationTextureBindingObservation? ObserveTexture(string assetName)
    {
        if (!textures.ContainsKey(assetName))
            return null;
        PresentationAssetDefinition? definition = null;
        var known = false;
        if (catalog is not null && catalog.TryGet(assetName, out var knownDefinition)) {
            definition = knownDefinition;
            known = true;
        }
        PresentationAssetResolution? resolution = null;
        var declared = false;
        if (known && definition!.Kind == PresentationAssetKind.Texture && resolver is not null) {
            if (resolver.TryResolveFrame(
                    assetName,
                    ResolveFrameIndex(assetName),
                    out var resolved,
                    out _)) {
                declared = true;
                resolution = resolved;
            }
        }
        var loaded = textures.TryGetValue(assetName, out var texture) && texture is not null;
        var path = lastDrawnTexturePaths.TryGetValue(assetName, out var drawnPath)
            ? drawnPath
            : declared ? resolution!.ResourcePath : known ? null : LegacyTexturePath(assetName);
        var missing = textures.TryGetValue(assetName, out texture) && texture is null;
        return new PresentationTextureBindingObservation(
            assetName,
            known,
            loaded,
            !declared || missing,
            path,
            loaded && lastDrawnTextureRegions.TryGetValue(assetName, out var drawnRegion)
                ? drawnRegion
                : null,
            missing ? "Texture resource is unavailable or has the wrong declared kind; procedural fallback is active." : null);
    }

    private PresentationFrameRegion? ResolveRegion(string assetName)
    {
        if (resolver is not null
            && resolver.TryResolveFrame(
                assetName,
                ResolveFrameIndex(assetName),
                out _,
                out var region))
            return region;
        return null;
    }

    private void RecordTextureUse(string assetName, PresentationFrameRegion? region)
    {
        lastDrawnTextureRegions[assetName] = region;
        if (resolver is not null
            && resolver.TryResolve(assetName, out var resolution))
            lastDrawnTexturePaths[assetName] = resolution.ResourcePath;
        else
            lastDrawnTexturePaths[assetName] = LegacyTexturePath(assetName);
    }

    private int ResolveFrameIndex(string assetName)
    {
        if (catalog is null || !catalog.TryGet(assetName, out var definition))
            return 0;
        return frameClock.ResolveFrame(definition.Frames);
    }

    private static string? LegacyTexturePath(string assetName)
    {
        if (!PresentationAssetPath.TryNormalizeRelativePath(
                $"{assetName}.svg",
                out var relative,
                out _))
            return null;
        return $"res://assets/{relative}";
    }

    private static string? LegacyAudioPath(string assetName)
    {
        if (!PresentationAssetPath.TryNormalizeRelativePath(
                $"audio/{assetName}.ogg",
                out var relative,
                out _))
            return null;
        return $"res://assets/{relative}";
    }

    internal readonly record struct TextureFrame(
        Texture2D Texture,
        PresentationFrameRegion? Region);
}
