using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Godot;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace Devilution.Client;

/** Result of resolving a native player selection through Godot resources. */
internal readonly record struct NativePlayerTextureBinding(
    NativePlayerCatalog Catalog,
    string? ContentManifestHash,
    bool IdentityMatched,
    NativePlayerPresentationSelection? Selection,
    AssetPalette.TextureFrame? Frame,
    string? ResourcePath,
    PresentationFrameRegion? Region,
    string? Diagnostic)
{
    public bool IsLoaded => Frame is not null;
}

/** Loads an optional client-only presentation pack while retaining legacy and procedural fallbacks. */
public sealed class AssetPalette
{
    public const string DefaultManifestPath = "res://assets/presentation-pack.json";
    public const string ManifestEnvironmentVariable = "GODEVILUTION_PRESENTATION_PACK";
    public const string NativePlayerManifestEnvironmentVariable = "GODEVILUTION_NATIVE_PLAYER_PACK";
    private const int MaximumNativePlayerManifestBytes = NativePlayerCatalog.MaximumDocumentCharacters;
    private const int NativePlayerTextureCacheCapacity = 12;

    private readonly Dictionary<string, Texture2D?> textures = [];
    private readonly Dictionary<string, AudioStream?> audio = [];
    private readonly HashSet<string> loadedTextureKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingTextureKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> loadedAudioKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingAudioKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PresentationFrameRegion?> lastDrawnTextureRegions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> lastDrawnTexturePaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NativePlayerTextureCacheEntry> nativePlayerTextureCache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> nativePlayerTextureLru = [];
    private readonly HashSet<string> loadedNativePlayerAtlasPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingNativePlayerAtlasPaths = new(StringComparer.Ordinal);
    private readonly PresentationFrameClock frameClock = new();
    private readonly string manifestPath;
    private string? nativePlayerManifestPath;
    private NativePlayerCatalog? nativePlayerCatalog;
    private string? nativePlayerDiagnostic;
    private string? nativePlayerContentManifestHash;
    private readonly PresentationAssetCatalog? catalog;
    private readonly PresentationAssetResolver? resolver;
    private readonly PresentationLocalization? localization;
    private readonly string? diagnostic;

    public AssetPalette()
    {
        LoadNativePlayerCatalog();
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

    /** Sets the admitted server content identity required to activate class-specific art. */
    internal bool SetNativePlayerContentIdentity(string? contentManifestHash)
    {
        var normalized = string.IsNullOrWhiteSpace(contentManifestHash) ? null : contentManifestHash;
        if (string.Equals(nativePlayerContentManifestHash, normalized, StringComparison.Ordinal))
            return false;
        nativePlayerContentManifestHash = normalized;
        return true;
    }

    /** Resolves native player art only when the admitted content hash is explicitly catalog-bound. */
    internal bool TryGetNativePlayerTextureFrame(
        PlayerSnapshot player,
        double currentTick,
        out NativePlayerTextureBinding binding)
    {
        if (nativePlayerCatalog is null) {
            binding = default;
            return false;
        }

        var identityMatched = nativePlayerCatalog.SupportsContentManifestHash(nativePlayerContentManifestHash);
        if (!identityMatched) {
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                false,
                null,
                null,
                null,
                null,
                nativePlayerContentManifestHash is null
                    ? "Native player pack is inactive because no admitted content manifest identity was supplied."
                    : $"Native player pack does not include content manifest '{nativePlayerContentManifestHash}'.");
            return true;
        }

        if (!nativePlayerCatalog.TryResolveLocation(
                nativePlayerContentManifestHash,
                player.LevelId,
                out var location)) {
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                true,
                null,
                null,
                null,
                null,
                $"Native player pack has no location binding for content manifest '{nativePlayerContentManifestHash}' level {player.LevelId}.");
            return true;
        }

        if (!NativePlayerPresentation.TryResolve(player, nativePlayerCatalog, location, currentTick, out var selection)) {
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                true,
                null,
                null,
                null,
                null,
                $"Native player pack does not support class id {player.CharacterClassId}.");
            return true;
        }

        if (selection.ClipKey is not { } clipKey
            || !nativePlayerCatalog.TryGetClip(clipKey, out var clip)) {
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                true,
                selection,
                null,
                null,
                null,
                selection.Diagnostic ?? "No native player clip was selected.");
            return true;
        }
        if (selection.FrameIndex is not { } frameIndex || selection.DirectionIndex is < 0 or >= NativePlayerPresentation.NativeDirectionCount) {
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                true,
                selection,
                null,
                null,
                null,
                selection.Diagnostic ?? "The native player frame or direction is unresolved.");
            return true;
        }
        if (!PresentationAssetPath.TryCombineResourcePath(
                nativePlayerManifestPath!,
                clip.Path,
                out var resourcePath,
                out var pathError)) {
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                true,
                selection,
                null,
                null,
                null,
                $"Native player atlas path was refused: {pathError}.");
            return true;
        }

        var texture = LoadNativePlayerAtlas(resourcePath, out var loadDiagnostic);
        if (texture is null) {
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                true,
                selection,
                null,
                null,
                null,
                selection.Diagnostic ?? loadDiagnostic);
            return true;
        }
        if (texture.GetWidth() < clip.FrameWidth * clip.FramesPerDirection
            || texture.GetHeight() < clip.FrameHeight * NativePlayerPresentation.NativeDirectionCount) {
            missingNativePlayerAtlasPaths.Add(resourcePath);
            binding = new NativePlayerTextureBinding(
                nativePlayerCatalog,
                nativePlayerContentManifestHash,
                true,
                selection,
                null,
                null,
                null,
                $"Native player atlas '{resourcePath}' is smaller than its declared frame grid.");
            return true;
        }

        var region = new PresentationFrameRegion(
            frameIndex * clip.FrameWidth,
            selection.DirectionIndex * clip.FrameHeight,
            clip.FrameWidth,
            clip.FrameHeight);
        binding = new NativePlayerTextureBinding(
            nativePlayerCatalog,
            nativePlayerContentManifestHash,
            true,
            selection,
            new TextureFrame(texture, region),
            resourcePath,
            region,
            selection.Diagnostic);
        return true;
    }

    internal NativePlayerPackObservation ObserveNativePlayerPack()
    {
        var identityMatched = nativePlayerCatalog?.SupportsContentManifestHash(nativePlayerContentManifestHash) == true;
        var diagnostic = nativePlayerCatalog is null
            ? nativePlayerDiagnostic
            : identityMatched
                ? null
                : nativePlayerContentManifestHash is null
                    ? "No admitted content manifest identity was supplied; native player art is inactive."
                    : $"The admitted content manifest '{nativePlayerContentManifestHash}' is not bound to this native player pack.";
        return new NativePlayerPackObservation(
            nativePlayerManifestPath,
            nativePlayerCatalog is not null,
            nativePlayerCatalog?.PackId,
            nativePlayerContentManifestHash,
            identityMatched,
            nativePlayerCatalog?.ContentManifestHashes.OrderBy(hash => hash, StringComparer.Ordinal).ToArray()
                ?? Array.Empty<string>(),
            diagnostic,
            loadedNativePlayerAtlasPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            missingNativePlayerAtlasPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
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

    private void LoadNativePlayerCatalog()
    {
        var configuredPath = Environment.GetEnvironmentVariable(NativePlayerManifestEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredPath)) {
            nativePlayerDiagnostic = "Native player pack is disabled; using the existing player presentation path.";
            return;
        }
        if (!PresentationAssetPath.TryNormalizeResourcePath(configuredPath.Trim(), out var normalizedPath, out var pathError)) {
            nativePlayerDiagnostic = $"Native player pack refused: {pathError}.";
            return;
        }
        nativePlayerManifestPath = normalizedPath;
        if (!FileAccess.FileExists(normalizedPath)) {
            nativePlayerDiagnostic = "Native player catalog was not found; using the existing player presentation path.";
            return;
        }

        using var file = FileAccess.Open(normalizedPath, FileAccess.ModeFlags.Read);
        if (file is null) {
            nativePlayerDiagnostic = "Native player catalog could not be opened; using the existing player presentation path.";
            return;
        }
        if (file.GetLength() > MaximumNativePlayerManifestBytes) {
            nativePlayerDiagnostic = "Native player catalog exceeds the 8 MiB size limit.";
            return;
        }
        if (!NativePlayerCatalog.TryParse(file.GetAsText(), out nativePlayerCatalog, out var diagnostics))
            nativePlayerDiagnostic = string.Join(" ", diagnostics);
        else
            nativePlayerDiagnostic = null;
    }

    private Texture2D? LoadNativePlayerAtlas(string resourcePath, out string? diagnostic)
    {
        if (nativePlayerTextureCache.TryGetValue(resourcePath, out var cached)) {
            nativePlayerTextureLru.Remove(cached.Node);
            nativePlayerTextureLru.AddFirst(cached.Node);
            diagnostic = cached.Diagnostic;
            return cached.Texture;
        }

        Texture2D? texture = null;
        diagnostic = null;
        if (!ResourceLoader.Exists(resourcePath, "Texture2D")) {
            diagnostic = $"Native player atlas '{resourcePath}' is unavailable.";
        } else {
            try {
                texture = ResourceLoader.Load<Texture2D>(resourcePath);
            } catch (Exception exception) {
                diagnostic = $"Native player atlas '{resourcePath}' could not be loaded: {exception.Message}";
            }
            if (texture is null) {
                diagnostic ??= $"Native player atlas '{resourcePath}' is unavailable.";
            } else if (texture.GetWidth() > NativePlayerCatalog.MaximumAtlasDimension
                || texture.GetHeight() > NativePlayerCatalog.MaximumAtlasDimension
                || (long)texture.GetWidth() * texture.GetHeight() > NativePlayerCatalog.MaximumAtlasPixels) {
                texture = null;
                diagnostic = $"Native player atlas '{resourcePath}' exceeds the bounded texture dimensions.";
            }
        }

        if (texture is null)
            missingNativePlayerAtlasPaths.Add(resourcePath);
        else
            loadedNativePlayerAtlasPaths.Add(resourcePath);
        var node = nativePlayerTextureLru.AddFirst(resourcePath);
        nativePlayerTextureCache.Add(resourcePath, new NativePlayerTextureCacheEntry(texture, diagnostic, node));
        while (nativePlayerTextureCache.Count > NativePlayerTextureCacheCapacity) {
            var leastRecent = nativePlayerTextureLru.Last;
            if (leastRecent is null)
                break;
            nativePlayerTextureCache.Remove(leastRecent.Value);
            nativePlayerTextureLru.RemoveLast();
        }
        return texture;
    }

    private sealed record NativePlayerTextureCacheEntry(
        Texture2D? Texture,
        string? Diagnostic,
        LinkedListNode<string> Node);

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
