using System.Buffers.Binary;
using System.Security.Cryptography;
using Devilution.Client.PresentationAssets;
using Godot;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace Devilution.Client;

internal sealed record NativeObjectPackObservation(
    string? CatalogPath,
    bool IsLoaded,
    string? PackId,
    string? ContentManifestHash,
    uint LevelId,
    bool IdentityMatched,
    bool AtlasesReady,
    string? Diagnostic,
    IReadOnlyList<string> LoadedAtlasPaths,
    IReadOnlyList<string> MissingAtlasPaths);

/** Loads the optional Cathedral object family only for its exact admitted content and level binding. */
internal sealed class NativeObjectAssets
{
    public const string ManifestEnvironmentVariable = "GODEVILUTION_NATIVE_OBJECT_PACK";

    private readonly Dictionary<int, Texture2D> textures = [];
    private readonly List<string> loadedAtlasPaths = [];
    private readonly List<string> missingAtlasPaths = [];
    private readonly string? catalogPath;
    private readonly NativeObjectCatalog? catalog;
    private readonly string? loadDiagnostic;
    private string? contentManifestHash;
    private uint levelId;
    private string? diagnostic;
    private string? preflightDiagnostic;
    private bool identityMatched;
    private bool preflightAttempted;
    private bool atlasesReady;

    public NativeObjectAssets()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ManifestEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredPath)) {
            loadDiagnostic = "Native object art is disabled; using the existing object presentation.";
            return;
        }
        if (!PresentationAssetPath.TryNormalizeResourcePath(configuredPath.Trim(), out var normalizedPath, out var pathError)) {
            loadDiagnostic = $"Native object pack refused: {pathError}.";
            return;
        }
        catalogPath = normalizedPath;
        if (!FileAccess.FileExists(normalizedPath)) {
            loadDiagnostic = "Native object catalog was not found; using the existing object presentation.";
            return;
        }

        using var file = FileAccess.Open(normalizedPath, FileAccess.ModeFlags.Read);
        if (file is null) {
            loadDiagnostic = "Native object catalog could not be opened; using the existing object presentation.";
            return;
        }
        if (file.GetLength() > NativeObjectCatalog.MaximumDocumentCharacters) {
            loadDiagnostic = $"Native object catalog exceeds the {NativeObjectCatalog.MaximumDocumentCharacters} character size limit.";
            return;
        }
        if (!NativeObjectCatalog.TryParse(file.GetAsText(), out var loadedCatalog, out var errors))
            loadDiagnostic = string.Join(" ", errors);
        else
            catalog = loadedCatalog;
    }

    public NativeObjectCatalog? Catalog => catalog;

    public bool Bind(string? admittedContentHash, uint currentLevelId)
    {
        contentManifestHash = admittedContentHash;
        levelId = currentLevelId;
        identityMatched = catalog?.SupportsBinding(admittedContentHash, currentLevelId) == true;
        if (!identityMatched) {
            diagnostic = catalog is null
                ? loadDiagnostic
                : admittedContentHash is null
                    ? "Native object art is inactive because no admitted content identity was supplied."
                    : $"Native object art is not bound to admitted content '{admittedContentHash}' at level {currentLevelId}.";
            return false;
        }

        if (!preflightAttempted) {
            preflightAttempted = true;
            atlasesReady = PreflightAtlases();
            preflightDiagnostic = diagnostic;
        }
        diagnostic = atlasesReady ? null : preflightDiagnostic;
        return atlasesReady;
    }

    public bool TryGetTexture(int atlasId, out Texture2D texture)
    {
        if (identityMatched && atlasesReady && textures.TryGetValue(atlasId, out texture!))
            return true;
        texture = null!;
        return false;
    }

    public NativeObjectPackObservation ObservePack()
        => new(catalogPath, catalog is not null, catalog?.PackId, contentManifestHash, levelId,
            identityMatched, atlasesReady, diagnostic ?? loadDiagnostic,
            loadedAtlasPaths.ToArray(), missingAtlasPaths.ToArray());

    private bool PreflightAtlases()
    {
        if (catalog!.Atlases.Count != 8) {
            diagnostic = "Native object pack must provide the complete eight-atlas family.";
            return false;
        }

        var pendingTextures = new Dictionary<int, Texture2D>();
        foreach (var atlas in catalog.Atlases) {
            if (!PresentationAssetPath.TryCombineResourcePath(catalogPath!, atlas.Path, out var path, out var pathError)) {
                diagnostic = $"Native object atlas path refused: {pathError}.";
                textures.Clear();
                return false;
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file is null || file.GetLength() is <= 0 or > NativeObjectCatalog.MaximumAtlasFileBytes) {
                diagnostic = $"Native object atlas '{path}' is missing or exceeds its bounded file size.";
                missingAtlasPaths.Add(path);
                textures.Clear();
                return false;
            }

            var bytes = file.GetBuffer(checked((long)file.GetLength())).ToArray();
            if (!HasExpectedPng(bytes, atlas)) {
                diagnostic = $"Native object atlas '{path}' hash or bounded PNG dimensions differ from its declaration.";
                missingAtlasPaths.Add(path);
                textures.Clear();
                return false;
            }

            using var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok
                || image.GetWidth() != atlas.Width
                || image.GetHeight() != atlas.Height) {
                diagnostic = $"Native object atlas '{path}' could not be decoded at its declared dimensions.";
                missingAtlasPaths.Add(path);
                textures.Clear();
                return false;
            }

            try {
                var texture = ImageTexture.CreateFromImage(image);
                if (texture is null) {
                    diagnostic = $"Native object atlas '{path}' could not be uploaded.";
                    missingAtlasPaths.Add(path);
                    textures.Clear();
                    return false;
                }
                pendingTextures.Add(atlas.Id, texture);
            } catch (Exception exception) {
                diagnostic = $"Native object atlas '{path}' could not be uploaded: {exception.Message}";
                missingAtlasPaths.Add(path);
                textures.Clear();
                return false;
            }
            loadedAtlasPaths.Add(path);
        }

        foreach (var entry in pendingTextures)
            textures.Add(entry.Key, entry.Value);
        return true;
    }

    private static bool HasExpectedPng(byte[] bytes, NativeObjectAtlas atlas)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == atlas.Sha256
            && bytes.Length >= 33
            && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) == 13
            && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == atlas.Width
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == atlas.Height;
}
