using System.Security.Cryptography;
using Devilution.Client.PresentationAssets;
using Godot;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace Devilution.Client;

internal sealed record NativeScenePackObservation(
    string? CatalogPath,
    bool IsLoaded,
    string? PackId,
    string? ContentManifestHash,
    bool IdentityMatched,
    string? Diagnostic,
    IReadOnlyList<string> LoadedAtlasPaths,
    IReadOnlyList<string> MissingAtlasPaths);

/** Loads a private native scene pack only for exact admitted content and level bindings. */
internal sealed class NativeSceneAssets
{
    public const string ManifestEnvironmentVariable = "GODEVILUTION_NATIVE_SCENE_PACK";
    private const int TextureCacheCapacity = 8;

    private readonly Dictionary<string, AtlasCacheEntry> textureCache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> textureLru = [];
    private readonly HashSet<string> loadedAtlasPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> missingAtlasPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<NativeSceneLocation, FamilyPreflightResult> familyPreflightResults = [];
    private readonly string? manifestPath;
    private readonly NativeSceneCatalog? catalog;
    private readonly string? loadDiagnostic;
    private string? contentManifestHash;

    public NativeSceneAssets()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ManifestEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredPath)) {
            loadDiagnostic = "Native scene pack is disabled; using the existing world presentation.";
            return;
        }
        if (!PresentationAssetPath.TryNormalizeResourcePath(configuredPath.Trim(), out var normalizedPath, out var pathError)) {
            loadDiagnostic = $"Native scene pack refused: {pathError}.";
            return;
        }
        manifestPath = normalizedPath;
        if (!FileAccess.FileExists(normalizedPath)) {
            loadDiagnostic = "Native scene catalog was not found; using the existing world presentation.";
            return;
        }

        using var file = FileAccess.Open(normalizedPath, FileAccess.ModeFlags.Read);
        if (file is null) {
            loadDiagnostic = "Native scene catalog could not be opened; using the existing world presentation.";
            return;
        }
        if (file.GetLength() > NativeSceneCatalog.MaximumDocumentCharacters) {
            loadDiagnostic = "Native scene catalog exceeds the 32 MiB size limit.";
            return;
        }
        if (!NativeSceneCatalog.TryParse(file.GetAsText(), out var loadedCatalog, out var diagnostics))
            loadDiagnostic = string.Join(" ", diagnostics);
        else
            catalog = loadedCatalog;
    }

    public NativeSceneCatalog? Catalog => catalog;

    public bool SetContentManifestHash(string? hash)
    {
        var normalized = string.IsNullOrWhiteSpace(hash) ? null : hash;
        if (string.Equals(contentManifestHash, normalized, StringComparison.Ordinal))
            return false;
        contentManifestHash = normalized;
        return true;
    }

    public bool TryResolveFamily(
        string? admittedContentHash,
        uint levelId,
        LevelLayout level,
        out NativeSceneFamily family,
        out string? diagnostic)
    {
        family = null!;
        diagnostic = null;
        if (catalog is null) {
            diagnostic = loadDiagnostic;
            return false;
        }
        if (admittedContentHash is null
            || !string.Equals(admittedContentHash, contentManifestHash, StringComparison.Ordinal)
            || !catalog.SupportsContentManifestHash(admittedContentHash)) {
            diagnostic = admittedContentHash is null
                ? "Native scene pack is inactive because no admitted content identity was supplied."
                : $"Native scene pack is not bound to admitted content manifest '{admittedContentHash}'.";
            return false;
        }
        if (!catalog.TryResolveFamily(admittedContentHash, levelId, out family)) {
            diagnostic = $"Native scene pack has no family binding for level {levelId} and the admitted content identity.";
            return false;
        }
        if (!family.HasValidFinePieceLayer(level.Width, level.Height, level.NativePieceLayer)) {
            family = null!;
            diagnostic = "Native scene pack requires a complete authoritative d-piece layer.";
            return false;
        }
        if (!family.HasValidSpecialLayer(level.Width, level.Height, level.NativeSpecialLayer)) {
            family = null!;
            diagnostic = "The authoritative special layer is incomplete or outside the bound native scene family.";
            return false;
        }
        if (!PreflightFamilyAtlases(family, out diagnostic)) {
            family = null!;
            return false;
        }
        return true;
    }

    private bool PreflightFamilyAtlases(NativeSceneFamily family, out string? diagnostic)
    {
        if (familyPreflightResults.TryGetValue(family.Location, out var previous)) {
            diagnostic = previous.Diagnostic;
            return previous.Succeeded;
        }

        foreach (var atlas in family.Atlases) {
            var probeRaster = new NativeSceneRasterReference(atlas.Id, 0, 0, 1, 1);
            if (TryGetTexture(family, probeRaster, out _, out _, out var atlasDiagnostic))
                continue;
            diagnostic = $"Native scene {family.Location.ToString().ToLowerInvariant()} family atlas preflight failed: "
                + (atlasDiagnostic ?? $"atlas {atlas.Id} could not be loaded.");
            familyPreflightResults.Add(family.Location, new FamilyPreflightResult(false, diagnostic));
            return false;
        }

        diagnostic = null;
        familyPreflightResults.Add(family.Location, new FamilyPreflightResult(true, null));
        return true;
    }

    public bool TryGetTexture(
        NativeSceneFamily family,
        NativeSceneRasterReference raster,
        out Texture2D? texture,
        out string? resourcePath,
        out string? diagnostic)
    {
        texture = null;
        resourcePath = null;
        diagnostic = null;
        if (!family.TryGetAtlas(raster.AtlasId, out var atlas)) {
            diagnostic = $"Native scene atlas {raster.AtlasId} is not declared.";
            return false;
        }
        if (!PresentationAssetPath.TryCombineResourcePath(
                manifestPath!,
                atlas.Path,
                out var normalizedPath,
                out var pathError)) {
            diagnostic = $"Native scene atlas path was refused: {pathError}.";
            return false;
        }
        resourcePath = normalizedPath;
        if (textureCache.TryGetValue(normalizedPath, out var cached)) {
            Touch(cached);
            texture = cached.Texture;
            diagnostic = cached.Diagnostic;
            return texture is not null;
        }

        string? loadError = null;
        Texture2D? loaded = null;
        if (!FileAccess.FileExists(normalizedPath)) {
            loadError = $"Native scene atlas '{normalizedPath}' is unavailable.";
        } else {
            using var file = FileAccess.Open(normalizedPath, FileAccess.ModeFlags.Read);
            if (file is null) {
                loadError = $"Native scene atlas '{normalizedPath}' could not be opened.";
            } else if (file.GetLength() is <= 0 or > NativeSceneCatalog.MaximumAtlasFileBytes) {
                loadError = $"Native scene atlas '{normalizedPath}' exceeds the bounded file size.";
            } else {
                var bytes = file.GetBuffer(checked((long)file.GetLength())).ToArray();
                var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(digest, atlas.Sha256, StringComparison.Ordinal)) {
                    loadError = $"Native scene atlas '{normalizedPath}' does not match its declared SHA-256 hash.";
                } else if (!ResourceLoader.Exists(normalizedPath, "Texture2D")) {
                    loadError = $"Native scene atlas '{normalizedPath}' is not a Texture2D resource.";
                } else {
                    try {
                        loaded = ResourceLoader.Load<Texture2D>(normalizedPath);
                    } catch (Exception exception) {
                        loadError = $"Native scene atlas '{normalizedPath}' could not be loaded: {exception.Message}";
                    }
                    if (loaded is null) {
                        loadError ??= $"Native scene atlas '{normalizedPath}' could not be loaded.";
                    } else if (loaded.GetWidth() != atlas.Width
                        || loaded.GetHeight() != atlas.Height
                        || loaded.GetWidth() > NativeSceneCatalog.MaximumAtlasDimension
                        || loaded.GetHeight() > NativeSceneCatalog.MaximumAtlasDimension
                        || (long)loaded.GetWidth() * loaded.GetHeight() > NativeSceneCatalog.MaximumAtlasPixels) {
                        loaded = null;
                        loadError = $"Native scene atlas '{normalizedPath}' dimensions do not match its bounded declaration.";
                    }
                }
            }
        }

        var node = textureLru.AddFirst(normalizedPath);
        textureCache.Add(normalizedPath, new AtlasCacheEntry(loaded, loadError, node));
        while (textureCache.Count > TextureCacheCapacity) {
            var leastRecent = textureLru.Last;
            if (leastRecent is null)
                break;
            textureCache.Remove(leastRecent.Value);
            textureLru.RemoveLast();
        }
        if (loaded is null)
            missingAtlasPaths.Add(normalizedPath);
        else
            loadedAtlasPaths.Add(normalizedPath);
        texture = loaded;
        diagnostic = loadError;
        return loaded is not null;
    }

    public NativeScenePackObservation ObservePack()
    {
        var identityMatched = catalog?.SupportsContentManifestHash(contentManifestHash) == true;
        var diagnostic = catalog is null
            ? loadDiagnostic
            : identityMatched
                ? null
                : contentManifestHash is null
                    ? "No admitted content manifest identity was supplied; native scene rendering is inactive."
                    : $"The admitted content manifest '{contentManifestHash}' is not bound to this native scene pack.";
        return new NativeScenePackObservation(
            manifestPath,
            catalog is not null,
            catalog?.PackId,
            contentManifestHash,
            identityMatched,
            diagnostic,
            loadedAtlasPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            missingAtlasPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
    }

    private void Touch(AtlasCacheEntry entry)
    {
        textureLru.Remove(entry.Node);
        textureLru.AddFirst(entry.Node);
    }

    private sealed record AtlasCacheEntry(
        Texture2D? Texture,
        string? Diagnostic,
        LinkedListNode<string> Node);

    private sealed record FamilyPreflightResult(bool Succeeded, string? Diagnostic);
}
